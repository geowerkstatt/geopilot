using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Geopilot.Api;

internal sealed class GeopilotApiApp : WebApplicationFactory<GeopilotApiApp>
{
    internal const string AdminHeader = "X-Test-Admin";

    internal HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(AdminHeader, "true");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment("Development");
        builder.UseContentRoot(Directory.GetCurrentDirectory());
    }
}
