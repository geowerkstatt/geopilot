using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geowerkstatt.IlitoolsWrapperApi.Ili2gpkg;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Ilitools-wrapper based implementation of the <see cref="IIli2GpkgClient"/> interface using gRPC to perform the ili2gpkg operations.
/// </summary>
internal sealed class Ili2GpkgClient : IIli2GpkgClient
{
    private readonly Ili2gpkgService.Ili2gpkgServiceClient client;
    private readonly ILogger<Ili2GpkgClient> logger;

    internal Ili2GpkgClient(GrpcChannel grpcChannel, ILogger<Ili2GpkgClient> logger)
    {
        this.logger = logger;

        client = new Ili2gpkgService.Ili2gpkgServiceClient(grpcChannel);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> SchemaImportAsync(Ili2GpkgArgs args, IPipelineFile modelFile, IPipelineFile outputFile, IPipelineFile logFile, IPipelineFile xtfLogFile, IPipelineFile? inputFile = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg schema import operation into {NewOrExisting} database.", inputFile is null ? "new" : "existing");

        var request = CreateConvertRequest(ConvertOperation.OperationSchemaImport, args);
        using var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(request, cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.ModelFile, modelFile, cancellationToken);

        if (inputFile is not null)
        {
            await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, inputFile, cancellationToken);
        }

        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.DbFile, outputFile, logFile, xtfLogFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> ImportAsync(Ili2GpkgArgs args, IPipelineFile? inputFile, IPipelineFile outputFile, IReadOnlyList<IPipelineFile> transferFiles, IPipelineFile logFile, IPipelineFile xtfLogFile, IReadOnlyList<IPipelineFile>? modelFiles = null, CancellationToken cancellationToken = default)
    {
        if (inputFile is null && !args.DoSchemaImport)
        {
            throw new ArgumentException("Input gpkg file must be provided for import unless --doSchemaImport is specified.", nameof(inputFile));
        }

        logger.LogInformation("Starting Ili2Gpkg import operation for {Count} transfer file(s).", transferFiles.Count);

        var request = CreateConvertRequest(ConvertOperation.OperationImport, args);
        using var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(request, cancellationToken);
        if (inputFile is not null)
        {
            await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, inputFile, cancellationToken);
        }

        foreach (var transferFile in transferFiles)
        {
            await SendFileAsync(call.RequestStream, Ili2gpkgFileType.TransferFile, transferFile, cancellationToken);
        }

        await SendModelFilesAsync(call.RequestStream, modelFiles, cancellationToken);

        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.DbFile, outputFile, logFile, xtfLogFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> ExportAsync(Ili2GpkgArgs args, IPipelineFile gpkgFile, IPipelineFile transferFile, IPipelineFile logFile, IPipelineFile xtfLogFile, IReadOnlyList<IPipelineFile>? modelFiles = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg export operation.");

        var request = CreateConvertRequest(ConvertOperation.OperationExport, args);
        using var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(request, cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, gpkgFile, cancellationToken);
        await SendModelFilesAsync(call.RequestStream, modelFiles, cancellationToken);
        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.TransferFile, transferFile, logFile, xtfLogFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> UpdateAsync(Ili2GpkgArgs args, IPipelineFile inputFile, IPipelineFile outputFile, IReadOnlyList<IPipelineFile> transferFiles, IPipelineFile logFile, IPipelineFile xtfLogFile, IReadOnlyList<IPipelineFile>? modelFiles = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg update operation for {Count} transfer file(s).", transferFiles.Count);

        var request = CreateConvertRequest(ConvertOperation.OperationUpdate, args);
        using var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(request, cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, inputFile, cancellationToken);
        foreach (var transferFile in transferFiles)
        {
            await SendFileAsync(call.RequestStream, Ili2gpkgFileType.TransferFile, transferFile, cancellationToken);
        }

        await SendModelFilesAsync(call.RequestStream, modelFiles, cancellationToken);

        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, Ili2gpkgFileType.DbFile, outputFile, logFile, xtfLogFile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ili2GpkgResult> ValidateAsync(Ili2GpkgArgs args, IPipelineFile gpkgFile, IPipelineFile logFile, IPipelineFile xtfLogFile, IReadOnlyList<IPipelineFile>? modelFiles = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Ili2Gpkg validation operation.");

        var request = CreateConvertRequest(ConvertOperation.OperationValidate, args);
        using var call = client.Convert(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(request, cancellationToken);
        await SendFileAsync(call.RequestStream, Ili2gpkgFileType.DbFile, gpkgFile, cancellationToken);
        await SendModelFilesAsync(call.RequestStream, modelFiles, cancellationToken);
        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, null, null, logFile, xtfLogFile, cancellationToken);
    }

    private async Task SendModelFilesAsync(IClientStreamWriter<ConvertRequest> requestStream, IReadOnlyList<IPipelineFile>? modelFiles, CancellationToken cancellationToken)
    {
        foreach (var modelFile in modelFiles ?? [])
        {
            logger.LogInformation("Sending delivered model file {FileName}.", modelFile.OriginalFileName);
            await SendFileAsync(requestStream, Ili2gpkgFileType.ModelFile, modelFile, cancellationToken);
        }
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
            Dataset = args.Dataset ?? string.Empty,
            MetaConfig = args.MetaConfig ?? string.Empty,
            ToolVersion = args.ToolVersion ?? string.Empty,
            CreateFk = args.CreateFk,
            CreateGeomIdx = args.CreateGeomIdx,
            CreateEnumTabs = args.CreateEnumTabs,
            CreateTidCol = args.CreateTidCol,
            BeautifyEnumDispName = args.BeautifyEnumDispName,
            Smart1Inheritance = args.Smart1Inheritance,
            Smart2Inheritance = args.Smart2Inheritance,
            SqlColsAsText = args.SqlColsAsText,
            DoSchemaImport = args.DoSchemaImport,
            DeleteData = args.DeleteData,
            SkipPolygonBuilding = args.SkipPolygonBuilding,
            ImportBid = args.ImportBid,
            DisableAreaValidation = args.DisableAreaValidation,
            ForceTypeValidation = args.ForceTypeValidation,
            ValidConfig = args.ValidConfig ?? string.Empty,
        };

        if (args.Models != null)
        {
            info.Models.AddRange(args.Models);
        }

        if (args.ModelDirs != null)
        {
            info.ModelDirs.AddRange(args.ModelDirs);
        }

        if (args.PluginIds != null)
        {
            info.PluginIds.AddRange(args.PluginIds);
        }

        if (args.Topics != null)
        {
            info.Topics.AddRange(args.Topics);
        }

        if (args.Baskets != null)
        {
            info.Baskets.AddRange(args.Baskets);
        }

        if (args.ExportModels != null)
        {
            info.ExportModels.AddRange(args.ExportModels);
        }

        if (args.TransferFormat is { } transferFormat)
        {
            info.TransferFormat = transferFormat switch
            {
                Ili2GpkgTransferFormat.Xtf => TransferFormat.Xtf,
                Ili2GpkgTransferFormat.Itf => TransferFormat.Itf,
                Ili2GpkgTransferFormat.Gml => TransferFormat.Gml,
                Ili2GpkgTransferFormat.Gml20 => TransferFormat.Gml20,
                _ => throw new ArgumentOutOfRangeException(nameof(args), transferFormat, "Invalid transfer format."),
            };
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

        using var stream = await file.OpenReadAsync(cancellationToken);

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

    private async Task<Ili2GpkgResult> ReceiveResponseAsync(IAsyncStreamReader<ConvertResponse> responseStream, Ili2gpkgFileType? outputFileType, IPipelineFile? outputFile, IPipelineFile logFile, IPipelineFile xtfLogFile, CancellationToken cancellationToken)
    {
        var targetFiles = new Dictionary<Ili2gpkgFileType, IPipelineFile>
        {
            [Ili2gpkgFileType.LogFile] = logFile,
            [Ili2gpkgFileType.XtfLogFile] = xtfLogFile,
        };
        if (outputFileType is { } outputType && outputFile is not null)
        {
            targetFiles[outputType] = outputFile;
        }

        var success = false;
        var responseCompleted = false;
        Ili2gpkgFileStart? currentFile = null;
        var openStreams = new Dictionary<Ili2gpkgFileType, FileStream>();

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
                        if (targetFiles.TryGetValue(currentFile.Type, out var targetFile) && !openStreams.TryGetValue(currentFile.Type, out var stream))
                        {
                            stream = targetFile.OpenWriteFileStream();
                            openStreams.Add(currentFile.Type, stream);
                        }

                        break;
                    case ConvertResponse.PayloadOneofCase.Chunk:
                        if (currentFile is not null && openStreams.TryGetValue(currentFile.Type, out var currentStream))
                        {
                            await currentStream.WriteAsync(response.Chunk.Memory, cancellationToken);
                        }

                        break;
                }
            }

            responseCompleted = true;
        }
        finally
        {
            foreach (var (streamType, stream) in openStreams)
            {
                try
                {
                    await stream.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while closing file stream.");
                    if (streamType == outputFileType)
                    {
                        // If the output file stream fails to close, fail the operation as the file might only be partially written.
                        success = false;
                    }
                }
            }

            // Delete a partial output on error, but keep the log files.
            if (!responseCompleted && outputFileType is { } type && openStreams.TryGetValue(type, out var partialOutput))
            {
                try
                {
                    File.Delete(partialOutput.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while deleting partial output file.");
                }
            }
        }

        logger.LogInformation("Ili2Gpkg operation completed. Success: {Success}", success);
        return new Ili2GpkgResult(success);
    }
}
