using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Geopilot.Api;

internal sealed class GeopilotApiApp : WebApplicationFactory<GeopilotApiApp>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment("Development");
        builder.UseContentRoot(Directory.GetCurrentDirectory());
    }
}
