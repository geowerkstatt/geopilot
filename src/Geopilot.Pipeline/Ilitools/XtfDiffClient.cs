using Geopilot.PipelineCore.Pipeline;
using Geowerkstatt.IlitoolsWrapperApi.XtfDiff;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Ilitools-wrapper based implementation of the <see cref="IXtfDiffClient"/> interface using gRPC.
/// </summary>
internal sealed class XtfDiffClient : IXtfDiffClient
{
    private readonly XtfDiffService.XtfDiffServiceClient client;
    private readonly ILogger<XtfDiffClient> logger;

    internal XtfDiffClient(GrpcChannel grpcChannel, ILogger<XtfDiffClient> logger)
    {
        this.logger = logger;

        client = new XtfDiffService.XtfDiffServiceClient(grpcChannel);
    }

    /// <inheritdoc />
    public async Task<bool> DiffAsync(
        IReadOnlyList<string>? modelDirs,
        IPipelineFile oldTransferFile,
        IPipelineFile newTransferFile,
        IPipelineFile logFile,
        IPipelineFile diffFile,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting XTF diff of {OldFileName} and {NewFileName}.", oldTransferFile.OriginalFileName, newTransferFile.OriginalFileName);

        using var call = client.Diff(cancellationToken: cancellationToken);

        var info = new DiffRequestInfo();
        if (modelDirs is not null)
        {
            info.ModelDirs.AddRange(modelDirs);
        }

        await call.RequestStream.WriteAsync(new DiffRequest { Info = info }, cancellationToken);
        await SendFileAsync(call.RequestStream, XtfDiffFileType.OldTransferFile, oldTransferFile, cancellationToken);
        await SendFileAsync(call.RequestStream, XtfDiffFileType.NewTransferFile, newTransferFile, cancellationToken);
        await call.RequestStream.CompleteAsync();

        return await ReceiveResponseAsync(call.ResponseStream, logFile, diffFile, cancellationToken);
    }

    private static async Task SendFileAsync(IClientStreamWriter<DiffRequest> requestStream, XtfDiffFileType fileType, IPipelineFile file, CancellationToken cancellationToken)
    {
        const int ChunkSize = 10 * 1024 * 1024;
        using var buffer = MemoryPool<byte>.Shared.Rent(ChunkSize);

        await requestStream.WriteAsync(new DiffRequest { FileStart = new XtfDiffFileStart { Type = fileType } }, cancellationToken);

        using var stream = await file.OpenReadAsync(cancellationToken);

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.Memory, cancellationToken);
            if (bytesRead <= 0) break;

            var content = new DiffRequest
            {
                // Wrap the memory slice into a ByteString without copying.
                // Do not modify the memory before the chunk is fully written.
                Chunk = UnsafeByteOperations.UnsafeWrap(buffer.Memory[..bytesRead]),
            };
            await requestStream.WriteAsync(content, cancellationToken);
        }
    }

    private async Task<bool> ReceiveResponseAsync(
        IAsyncStreamReader<DiffResponse> responseStream,
        IPipelineFile logFile,
        IPipelineFile diffFile,
        CancellationToken cancellationToken)
    {
        var success = false;
        XtfDiffFileStart? currentFile = null;
        Stream? logFileStream = null;
        Stream? diffFileStream = null;

        try
        {
            while (await responseStream.MoveNext(cancellationToken))
            {
                var response = responseStream.Current;
                switch (response.PayloadCase)
                {
                    case DiffResponse.PayloadOneofCase.Status:
                        success = response.Status.Success;
                        break;
                    case DiffResponse.PayloadOneofCase.FileStart:
                        currentFile = response.FileStart;
                        break;
                    case DiffResponse.PayloadOneofCase.Chunk:
                        if (currentFile?.Type == XtfDiffFileType.LogFile)
                        {
                            logFileStream ??= logFile.OpenWriteFileStream();
                            await logFileStream.WriteAsync(response.Chunk.Memory, cancellationToken);
                        }
                        else if (currentFile?.Type == XtfDiffFileType.DiffFile)
                        {
                            diffFileStream ??= diffFile.OpenWriteFileStream();
                            await diffFileStream.WriteAsync(response.Chunk.Memory, cancellationToken);
                        }

                        break;
                }
            }
        }
        finally
        {
            if (logFileStream is not null)
            {
                await logFileStream.DisposeAsync();
            }

            if (diffFileStream is not null)
            {
                await diffFileStream.DisposeAsync();
            }
        }

        logger.LogInformation("XTF diff completed. Success: {Success}", success);
        return success;
    }
}
