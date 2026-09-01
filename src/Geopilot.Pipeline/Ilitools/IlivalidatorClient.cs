using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geowerkstatt.IlitoolsWrapperApi.Ilivalidator;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Ilitools-wrapper based implementation of the <see cref="IIlivalidatorClient"/> interface using gRPC to validate
/// INTERLIS transfer files.
/// </summary>
internal sealed class IlivalidatorClient : IIlivalidatorClient
{
    private readonly IlivalidatorService.IlivalidatorServiceClient client;
    private readonly ILogger<IlivalidatorClient> logger;

    internal IlivalidatorClient(GrpcChannel grpcChannel, ILogger<IlivalidatorClient> logger)
    {
        this.logger = logger;

        client = new IlivalidatorService.IlivalidatorServiceClient(grpcChannel);
    }

    /// <inheritdoc />
    public async Task<IlivalidatorResult> ValidateAsync(
        IlivalidatorArgs args,
        IPipelineFile transferFile,
        IPipelineFile logFile,
        IPipelineFile xtfLogFile,
        IPipelineFile? modelRepositoryArchive = null,
        IReadOnlyList<IPipelineFile>? modelFiles = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting ilivalidator validation of {FileName}.", transferFile.OriginalFileName);

        using var call = client.Validate(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(CreateValidateRequest(args), cancellationToken);
        await SendFileAsync(call.RequestStream, TransferFileType(transferFile), transferFile, cancellationToken);

        foreach (var modelFile in modelFiles ?? [])
        {
            logger.LogInformation("Sending delivered model file {FileName}.", modelFile.OriginalFileName);
            await SendFileAsync(call.RequestStream, IlivalidatorFileType.ModelFile, modelFile, cancellationToken);
        }

        if (modelRepositoryArchive != null)
        {
            logger.LogInformation("Sending model repository archive {FileName}.", modelRepositoryArchive.OriginalFileName);
            await SendFileAsync(call.RequestStream, IlivalidatorFileType.RepositoryArchive, modelRepositoryArchive, cancellationToken);
        }

        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, logFile, xtfLogFile, cancellationToken);
    }

    /// <summary>
    /// The transfer file type sent to the wrapper carries the format: the tool switches to its INTERLIS 1 semantics
    /// only when the data file ends in .itf, so that extension decides between the two types.
    /// </summary>
    internal static IlivalidatorFileType TransferFileType(IPipelineFile transferFile)
    {
        return string.Equals(transferFile.FileExtension, "itf", StringComparison.OrdinalIgnoreCase)
            ? IlivalidatorFileType.TransferFileItf
            : IlivalidatorFileType.TransferFileXtf;
    }

    private static ValidateRequest CreateValidateRequest(IlivalidatorArgs args)
    {
        var info = new ValidateRequestInfo
        {
            MetaConfig = args.MetaConfig ?? string.Empty,
            AllObjectsAccessible = args.AllObjectsAccessible,
            ToolVersion = args.ToolVersion ?? string.Empty,
        };

        if (args.ModelDirs != null)
        {
            info.ModelDirs.AddRange(args.ModelDirs);
        }

        if (args.PluginIds != null)
        {
            info.PluginIds.AddRange(args.PluginIds);
        }

        return new ValidateRequest
        {
            Info = info,
        };
    }

    private static async Task SendFileAsync(IClientStreamWriter<ValidateRequest> requestStream, IlivalidatorFileType fileType, IPipelineFile file, CancellationToken cancellationToken)
    {
        const int ChunkSize = 10 * 1024 * 1024;
        using var buffer = MemoryPool<byte>.Shared.Rent(ChunkSize);

        var fileStart = new ValidateRequest
        {
            FileStart = new IlivalidatorFileStart
            {
                Type = fileType,
            },
        };
        await requestStream.WriteAsync(fileStart, cancellationToken);

        using var stream = await file.OpenReadAsync(cancellationToken);

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.Memory, cancellationToken);
            if (bytesRead <= 0) break;

            var content = new ValidateRequest
            {
                // Wrap the memory slice into a ByteString without copying.
                // Do not modify the memory before the chunk is fully written.
                Chunk = UnsafeByteOperations.UnsafeWrap(buffer.Memory[..bytesRead]),
            };
            await requestStream.WriteAsync(content, cancellationToken);
        }
    }

    private async Task<IlivalidatorResult> ReceiveResponseAsync(
        IAsyncStreamReader<ValidateResponse> responseStream,
        IPipelineFile logFile,
        IPipelineFile xtfLogFile,
        CancellationToken cancellationToken)
    {
        var success = false;
        IlivalidatorFileStart? currentFile = null;
        Stream? logFileStream = null;
        Stream? xtfLogFileStream = null;

        try
        {
            while (await responseStream.MoveNext(cancellationToken))
            {
                var response = responseStream.Current;
                switch (response.PayloadCase)
                {
                    case ValidateResponse.PayloadOneofCase.Status:
                        success = response.Status.Success;
                        break;
                    case ValidateResponse.PayloadOneofCase.FileStart:
                        currentFile = response.FileStart;
                        break;
                    case ValidateResponse.PayloadOneofCase.Chunk:
                        if (currentFile?.Type == IlivalidatorFileType.LogFile)
                        {
                            logFileStream ??= logFile.OpenWriteFileStream();
                            await logFileStream.WriteAsync(response.Chunk.Memory, cancellationToken);
                        }
                        else if (currentFile?.Type == IlivalidatorFileType.XtfLogFile)
                        {
                            xtfLogFileStream ??= xtfLogFile.OpenWriteFileStream();
                            await xtfLogFileStream.WriteAsync(response.Chunk.Memory, cancellationToken);
                        }

                        break;
                }
            }
        }
        finally
        {
            if (logFileStream != null)
            {
                await logFileStream.DisposeAsync();
            }

            if (xtfLogFileStream != null)
            {
                await xtfLogFileStream.DisposeAsync();
            }
        }

        logger.LogInformation("Ilivalidator validation completed. Success: {Success}", success);
        return new IlivalidatorResult(success);
    }
}
