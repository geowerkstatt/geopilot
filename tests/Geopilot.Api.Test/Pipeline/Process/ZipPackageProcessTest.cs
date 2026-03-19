using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.ZipPackage;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class ZipPackageProcessTest
{
    [TestMethod]
    public void SunnyDay()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("myPersonalZipArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        processResult.TryGetValue("status_message", out var statusMessage);
        var statusMessageDictionary = statusMessage as Dictionary<string, string>;
        var zipArchive = outputData as IPipelineFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("myPersonalZipArchive.zip", zipArchive.OriginalFileName);
        Assert.HasCount(4, statusMessageDictionary);
        var expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Paket mit 1 Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant 1 fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente 1 file." },
            { "en", "ZIP package containing 1 file(s) created." },
        };
        CollectionAssert.AreEqual(expectedStatusMessage, statusMessageDictionary);
    }

    [TestMethod]
    public void NoArchiveFileNameProvided()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineFile;
        processResult.TryGetValue("status_message", out var statusMessage);
        var statusMessageDictionary = statusMessage as Dictionary<string, string>;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("archive.zip", zipArchive.OriginalFileName);
        Assert.HasCount(4, statusMessageDictionary);
        var expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Paket mit 1 Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant 1 fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente 1 file." },
            { "en", "ZIP package containing 1 file(s) created." },
        };
        CollectionAssert.AreEqual(expectedStatusMessage, statusMessageDictionary);
    }

    [TestMethod]
    public async Task NoInputFilesProvided()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(Array.Empty<IPipelineFile>()));
        Assert.AreEqual($"ZipPackageProcess: No input files provided.", exception.Message);
    }

    [TestMethod]
    public async Task AllInputFilesAreNull()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var processResult = await process.RunAsync(new IPipelineFile?[] { null, null, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        processResult.TryGetValue("status_message", out var statusMessage);
        var statusMessageDictionary = statusMessage as Dictionary<string, string>;
        Assert.IsNull(outputData);
        Assert.HasCount(4, statusMessageDictionary);
        var expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Archiv nicht erstellt, keine gültigen Eingabedateien gefunden." },
            { "fr", "Archive ZIP non créée, aucun fichier d'entrée valide trouvé." },
            { "it", "Archivio ZIP non creato, nessun file di input valido trovato." },
            { "en", "ZIP archive not created, no valid input files found." },
        };
        CollectionAssert.AreEqual(expectedStatusMessage, statusMessageDictionary);
    }

    [TestMethod]
    public async Task MixedNullAndValidInputFiles()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("mixedArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = await process.RunAsync(new IPipelineFile?[] { null, uploadFile, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        processResult.TryGetValue("status_message", out var statusMessage);
        var statusMessageDictionary = statusMessage as Dictionary<string, string>;
        var zipArchive = outputData as IPipelineFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("mixedArchive.zip", zipArchive.OriginalFileName);
        Assert.HasCount(4, statusMessageDictionary);
        var expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Paket mit 1 Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant 1 fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente 1 file." },
            { "en", "ZIP package containing 1 file(s) created." },
        };
        CollectionAssert.AreEqual(expectedStatusMessage, statusMessageDictionary);
    }
}
