using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geowerkstatt.IlitoolsWrapperApi;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Text;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Ilitools-wrapper based implementation of the <see cref="IIli2GpkgClient"/> interface using gRPC to perform the ili2gpkg operations.
/// </summary>
internal sealed class Ili2GpkgClient : IIli2GpkgClient
{
    private readonly Ili2gpkgService.Ili2gpkgServiceClient client;
    private readonly ILogger<Ili2GpkgClient> logger;

    internal Ili2GpkgClient(ILogger<Ili2GpkgClient> logger)
    {
        this.logger = logger;

        var channel = GrpcChannel.ForAddress("http://localhost:5555");
        client = new Ili2gpkgService.Ili2gpkgServiceClient(channel);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> SchemaImportAsync(Ili2GpkgArgs args, IPipelineFile modelFile, IPipelineFile gpkgFile, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg schema import operation.");

        var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(CreateConvertRequest(ConvertOperation.OperationSchemaImport, args), cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.ModelFile, modelFile, cancellationToken);
        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.DbFile, gpkgFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> ImportAsync(Ili2GpkgArgs args, IPipelineFile inputFile, IPipelineFile outputFile, IReadOnlyList<IPipelineFile> transferFiles, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg import operation for {Count} transfer file(s).", transferFiles.Count);

        var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(CreateConvertRequest(ConvertOperation.OperationImport, args), cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, inputFile, cancellationToken);
        foreach (var transferFile in transferFiles)
        {
            await SendFileAsync(call.RequestStream, Ili2gpkgFileType.TransferFile, transferFile, cancellationToken);
        }

        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.DbFile, outputFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> ExportAsync(Ili2GpkgArgs args, IPipelineFile gpkgFile, IPipelineFile transferFile, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg export operation.");

        var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(CreateConvertRequest(ConvertOperation.OperationExport, args), cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, gpkgFile, cancellationToken);
        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.TransferFile, transferFile, cancellationToken);
    }

    private static ConvertRequest CreateConvertRequest(ConvertOperation operation, Ili2GpkgArgs args)
    {
        var info = new ConvertRequestInfo
        {
            Operation = operation,
            DefaultSrsCode = args.DefaultSrsCode ?? 0,
            DisableValidation = args.DisableValidation,
            CreateBasketCol = args.CreateBasketCol,
            SqlEnableNull = args.SqlEnableNull,
            SkipReferenceErrors = args.SkipReferenceErrors,
            SkipGeometryErrors = args.SkipGeometryErrors,
            ImportTid = args.ImportTid,
            StrokeArcs = args.StrokeArcs,
        };

        if (args.Models != null)
        {
            info.Models.AddRange(args.Models);
        }

        return new ConvertRequest
        {
            Info = info,
        };
    }

    private static async Task SendFileAsync(IClientStreamWriter<ConvertRequest> requestStream, Ili2gpkgFileType fileType, IPipelineFile file, CancellationToken cancellationToken)
    {
        const int ChunkSize = 10 * 1024 * 1024;
        using var buffer = MemoryPool<byte>.Shared.Rent(ChunkSize);

        var fileStart = new ConvertRequest
        {
            FileStart = new Ili2gpkgFileStart
            {
                Type = fileType,
            },
        };
        await requestStream.WriteAsync(fileStart, cancellationToken);

        using var stream = file.OpenReadFileStream();

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.Memory, cancellationToken);
            if (bytesRead <= 0) break;

            var content = new ConvertRequest
            {
                // Wrap the memory slice into a ByteString without copying.
                // Do not modify the memory before the chunk is fully written.
                Chunk = UnsafeByteOperations.UnsafeWrap(buffer.Memory[..bytesRead]),
            };
            await requestStream.WriteAsync(content, cancellationToken);
        }
    }

    private async Task<Ili2GpkgResult> ReceiveResponseAsync(IAsyncStreamReader<ConvertResponse> responseStream, Ili2gpkgFileType outputFileType, IPipelineFile outputFile, CancellationToken cancellationToken)
    {
        var success = false;
        var logBuilder = new StringBuilder();
        Ili2gpkgFileStart? currentFile = null;
        Stream? outputFileStream = null;

        try
        {
            while (await responseStream.MoveNext(cancellationToken))
            {
                var response = responseStream.Current;
                switch (response.PayloadCase)
                {
                    case ConvertResponse.PayloadOneofCase.Status:
                        success = response.Status.Success;
                        break;
                    case ConvertResponse.PayloadOneofCase.FileStart:
                        currentFile = response.FileStart;
                        break;
                    case ConvertResponse.PayloadOneofCase.Chunk:
                        if (currentFile?.Type == Ili2gpkgFileType.LogFile)
                        {
                            logBuilder.Append(response.Chunk.ToStringUtf8());
                        }
                        else if (currentFile?.Type == outputFileType)
                        {
                            outputFileStream ??= outputFile.OpenWriteFileStream();
                            await outputFileStream.WriteAsync(response.Chunk.Memory, cancellationToken);
                        }

                        break;
                }
            }
        }
        finally
        {
            if (outputFileStream != null)
            {
                await outputFileStream.DisposeAsync();
            }
        }

        logger.LogInformation("Ili2Gpkg operation completed. Success: {Success}", success);
        return new Ili2GpkgResult(success, logBuilder.ToString());
    }
}
