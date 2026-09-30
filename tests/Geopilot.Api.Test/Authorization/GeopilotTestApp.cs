using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Geopilot.Api.Authorization;

internal abstract class GeopilotTestApp : WebApplicationFactory<Context>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSolutionRelativeContentRoot("src/Geopilot.Api", "*.slnx");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            var pipelineDefinition = Path.Combine(ctx.HostingEnvironment.ContentRootPath, "PipelineDefinitions", "basicPipeline_01.yaml");
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Context"] = TestDatabaseFixture.ConnectionString,
                ["Pipeline:Definition"] = pipelineDefinition,
                ["Upload:CleanupIntervalMinutes"] = "1440",
                ["ClamAV:Enabled"] = "false",
            });
        });
    }
}
