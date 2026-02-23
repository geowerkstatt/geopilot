using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.ProcessorPluginA.Processors;

/// <summary>
/// Provides functionality to process pipeline transfer files and extract their contents as dictionary outputs.
/// </summary>
public class MyProcessorB
{
    private const string FileOutput = "file_output";

    /// <summary>
    /// Reads the contents of the specified pipeline transfer file and returns the data as a dictionary output.
    /// </summary>
    /// <param name="file">The pipeline transfer file to read. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A dictionary containing the file contents, with the output key mapped to the file's text content.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object>> PipelineProcessRun(IPipelineTransferFile file, CancellationToken cancellationToken)
    {
        if (file != null)
        {
            using Stream fileStream = file.OpenFileStream();

            using StreamReader reader = new StreamReader(fileStream);
            string text = reader.ReadToEnd();
            return new Dictionary<string, object>()
            {
                { FileOutput, text },
            };
        }
        else
        {
            return new Dictionary<string, object>()
            {
                { FileOutput, "" },
            };
        }
    }
}
