using Geopilot.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class UploadServiceExtensionsTest
{
    private static readonly Dictionary<string, string?> ValidS3Configuration = new()
    {
        ["Upload:Cloud:Provider"] = "S3",
        ["Upload:Cloud:BucketName"] = "uploads",
        ["Upload:Cloud:S3:ServiceUrl"] = "http://localhost:8333",
        ["Upload:Cloud:S3:Region"] = "us-east-1",
        ["Upload:Cloud:S3:AccessKeyId"] = "test",
        ["Upload:Cloud:S3:SecretAccessKey"] = "test",
    };

    [TestMethod]
    public void S3ProviderRegistersS3UploadStorageWithoutConnectionString()
    {
        using var services = BuildServices(ValidS3Configuration);

        Assert.IsInstanceOfType<S3UploadStorage>(services.GetRequiredService<IUploadStorage>());
        Assert.IsNull(services.GetRequiredService<IOptions<UploadCloudOptions>>().Value.ConnectionString);
    }

    [TestMethod]
    public void S3ProviderRequiresS3Section()
    {
        using var services = BuildServices(new()
        {
            ["Upload:Cloud:Provider"] = "S3",
            ["Upload:Cloud:BucketName"] = "uploads",
        });

        Assert.ThrowsExactly<OptionsValidationException>(() => services.GetRequiredService<IOptions<UploadS3Options>>().Value);
    }

    [TestMethod]
    public void AzureBlobProviderRequiresConnectionString()
    {
        using var services = BuildServices(new()
        {
            ["Upload:Cloud:BucketName"] = "uploads",
        });

        var exception = Assert.ThrowsExactly<OptionsValidationException>(() => services.GetRequiredService<IOptions<UploadCloudOptions>>().Value);
        StringAssert.Contains(exception.Message, "Upload:Cloud:ConnectionString", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Upload:Backend")]
    [DataRow("Upload:Cloud:Provider")]
    public void UndefinedValueThrows(string key)
    {
        var builder = CreateBuilder(new() { [key] = "2" });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => builder.AddUploadServices());
        Assert.AreEqual($"Unsupported {key} '2'.", exception.Message);
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> configuration)
    {
        var builder = CreateBuilder(configuration);
        builder.AddUploadServices();
        return builder.Services.BuildServiceProvider();
    }

    private static WebApplicationBuilder CreateBuilder(Dictionary<string, string?> configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }
}
