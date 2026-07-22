using Geopilot.Pipeline.Processes.ZipPackage;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Compression;

namespace Geopilot.Pipeline.Test.Processes;

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
        Assert.IsNotNull(processResult.ZipPackage);
        Assert.IsNotNull(processResult.StatusMessage);
        var statusMessageLocalized = processResult.StatusMessage;
        var zipArchive = processResult.ZipPackage;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("myPersonalZipArchive.zip", zipArchive.OriginalFileName);
        Assert.IsNotNull(statusMessageLocalized);
        Assert.AreEqual(4, statusMessageLocalized.Count);
        LocalizedText expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Paket mit 1 Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant 1 fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente 1 file." },
            { "en", "ZIP package containing 1 file(s) created." },
        };
        Assert.AreEqual(expectedStatusMessage, statusMessageLocalized);
    }

    [TestMethod]
    public void NoArchiveFileNameProvided()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.IsNotNull(processResult.ZipPackage);
        Assert.IsNotNull(processResult.StatusMessage);
        var zipArchive = processResult.ZipPackage;
        var statusMessageLocalized = processResult.StatusMessage;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("archive.zip", zipArchive.OriginalFileName);
        Assert.IsNotNull(statusMessageLocalized);
        Assert.AreEqual(4, statusMessageLocalized.Count);
        LocalizedText expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Paket mit 1 Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant 1 fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente 1 file." },
            { "en", "ZIP package containing 1 file(s) created." },
        };
        Assert.AreEqual(expectedStatusMessage, statusMessageLocalized);
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
        Assert.IsNotNull(processResult.StatusMessage);
        var outputData = processResult.ZipPackage;
        var statusMessageLocalized = processResult.StatusMessage;
        Assert.IsNull(outputData);
        Assert.IsNotNull(statusMessageLocalized);
        Assert.AreEqual(4, statusMessageLocalized.Count);
        LocalizedText expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Archiv nicht erstellt, keine gültigen Eingabedateien gefunden." },
            { "fr", "Archive ZIP non créée, aucun fichier d'entrée valide trouvé." },
            { "it", "Archivio ZIP non creato, nessun file di input valido trovato." },
            { "en", "ZIP archive not created, no valid input files found." },
        };
        Assert.AreEqual(expectedStatusMessage, statusMessageLocalized);
    }

    [TestMethod]
    public async Task MixedNullAndValidInputFiles()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("mixedArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = await process.RunAsync(new IPipelineFile?[] { null, uploadFile, null });
        Assert.IsNotNull(processResult);
        Assert.IsNotNull(processResult.ZipPackage);
        Assert.IsNotNull(processResult.StatusMessage);
        var zipArchive = processResult.ZipPackage;
        var statusMessageLocalized = processResult.StatusMessage;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("mixedArchive.zip", zipArchive.OriginalFileName);
        Assert.IsNotNull(statusMessageLocalized);
        Assert.AreEqual(4, statusMessageLocalized.Count);
        LocalizedText expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "ZIP Paket mit 1 Datei(en) erstellt." },
            { "fr", "Un paquet ZIP contenant 1 fichier(s) a été créé." },
            { "it", "È stato creato un pacchetto ZIP contenente 1 file." },
            { "en", "ZIP package containing 1 file(s) created." },
        };
        Assert.AreEqual(expectedStatusMessage, statusMessageLocalized);
    }

    [TestMethod]
    public async Task PreservesOriginalRelativePath()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("structuredArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var rootFile = new PipelineFile("TestData/UploadFiles/helloWorld.pdf", "helloWorld.pdf");
        var nestedFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf", "sub/folder");

        var processResult = await process.RunAsync(new IPipelineFile[] { rootFile, nestedFile });
        var zipArchive = processResult.ZipPackage;
        Assert.IsNotNull(zipArchive);

        using var zipStream = zipArchive.OpenReadFileStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        Assert.HasCount(2, archive.Entries);
        Assert.IsTrue(archive.Entries.Any(e => e.FullName == "helloWorld.pdf"));
        Assert.IsTrue(archive.Entries.Any(e => e.FullName == "sub/folder/RoadsExdm2ien.xtf"));
    }

    [TestMethod]
    public async Task SameFileNameInDifferentDirectoriesNoDuplicateWarning()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var mockLogger = new Mock<ILogger<ZipPackageProcessTest>>();
        var process = new ZipPackageProcess("archive", pipelineFileManager, mockLogger.Object);
        var file1 = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "data.xtf", "dir1");
        var file2 = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "data.xtf", "dir2");

        var processResult = await process.RunAsync(new IPipelineFile[] { file1, file2 });
        var zipArchive = processResult.ZipPackage;
        Assert.IsNotNull(zipArchive);

        using var zipStream = zipArchive.OpenReadFileStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        Assert.HasCount(2, archive.Entries);
        Assert.IsTrue(archive.Entries.Any(e => e.FullName == "dir1/data.xtf"));
        Assert.IsTrue(archive.Entries.Any(e => e.FullName == "dir2/data.xtf"));
    }
}
