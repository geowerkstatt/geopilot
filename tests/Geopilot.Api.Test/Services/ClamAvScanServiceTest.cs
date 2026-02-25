using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class ClamAvScanServiceTest
{
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<ILogger<ClamAvScanService>> loggerMock;
    private ClamAvScanService service;

    // EICAR is a standard antivirus test string — not actual malware,
    // but all AV scanners detect it by convention. See https://www.eicar.org/download-anti-malware-testfile/
    private static readonly byte[] EicarTestContent = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"u8.ToArray();

    [TestInitialize]
    public void Initialize()
    {
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<ClamAvScanService>>();

        var options = new Mock<IOptions<ClamAvOptions>>();
        options.Setup(o => o.Value).Returns(new ClamAvOptions { Host = "localhost", Port = 3310 });

        service = new ClamAvScanService(cloudStorageServiceMock.Object, options.Object, loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        cloudStorageServiceMock.VerifyAll();
    }

    [TestMethod]
    public void ConstructorThrowsWhenHostMissing()
    {
        var options = new Mock<IOptions<ClamAvOptions>>();
        options.Setup(o => o.Value).Returns(new ClamAvOptions { Host = "", Port = 3310 });

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ClamAvScanService(cloudStorageServiceMock.Object, options.Object, loggerMock.Object));
    }

    [TestMethod]
    public void ConstructorThrowsWhenPortInvalid()
    {
        var options = new Mock<IOptions<ClamAvOptions>>();
        options.Setup(o => o.Value).Returns(new ClamAvOptions { Host = "localhost", Port = 0 });

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ClamAvScanService(cloudStorageServiceMock.Object, options.Object, loggerMock.Object));
    }

    [TestMethod]
    public void ConstructorThrowsWhenPortExceedsMax()
    {
        var options = new Mock<IOptions<ClamAvOptions>>();
        options.Setup(o => o.Value).Returns(new ClamAvOptions { Host = "localhost", Port = 70000 });

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ClamAvScanService(cloudStorageServiceMock.Object, options.Object, loggerMock.Object));
    }

    [TestMethod]
    public async Task CheckFilesAsyncReturnsCleanForEmptyList()
    {
        var result = await service.CheckFilesAsync([]);

        Assert.IsTrue(result.IsClean);
        Assert.IsNull(result.ThreatDetails);
    }

    [TestMethod]
    public async Task CheckFilesAsyncReturnsCleanForSafeFile()
    {
        SetupDownload("uploads/job1/clean.xtf", "perfectly safe content"u8.ToArray());

        var result = await service.CheckFilesAsync(["uploads/job1/clean.xtf"]);

        Assert.IsTrue(result.IsClean);
        Assert.IsNull(result.ThreatDetails);
    }

    [TestMethod]
    public async Task CheckFilesAsyncDetectsEicarThreat()
    {
        SetupDownload("uploads/job1/eicar.xtf", EicarTestContent);

        var result = await service.CheckFilesAsync(["uploads/job1/eicar.xtf"]);

        Assert.IsFalse(result.IsClean);
        Assert.IsNotNull(result.ThreatDetails);
        Assert.Contains("uploads/job1/eicar.xtf", result.ThreatDetails);
    }

    [TestMethod]
    public async Task CheckFilesAsyncMixedFilesReportsOnlyThreats()
    {
        SetupDownload("uploads/job1/clean.xtf", "safe"u8.ToArray());
        SetupDownload("uploads/job1/eicar.xtf", EicarTestContent);

        var result = await service.CheckFilesAsync(["uploads/job1/clean.xtf", "uploads/job1/eicar.xtf"]);

        Assert.IsFalse(result.IsClean);
        Assert.IsNotNull(result.ThreatDetails);
        Assert.Contains("uploads/job1/eicar.xtf", result.ThreatDetails);
        Assert.DoesNotContain("uploads/job1/clean.xtf", result.ThreatDetails);
    }

    [TestMethod]
    public async Task CheckFilesAsyncMultipleCleanFilesReturnsClean()
    {
        SetupDownload("uploads/job1/a.xtf", "content a"u8.ToArray());
        SetupDownload("uploads/job1/b.xtf", "content b"u8.ToArray());

        var result = await service.CheckFilesAsync(["uploads/job1/a.xtf", "uploads/job1/b.xtf"]);

        Assert.IsTrue(result.IsClean);
        Assert.IsNull(result.ThreatDetails);
    }

    private void SetupDownload(string key, byte[] content)
    {
        cloudStorageServiceMock
            .Setup(s => s.DownloadAsync(key, It.IsAny<Stream>()))
            .Returns<string, Stream>(async (_, destination) =>
            {
                await destination.WriteAsync(content);
            });
    }
}
