using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;

namespace Geopilot.Api.Authorization;

internal sealed class JwtTestApp : WebApplicationFactory<Context>
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
                ["CloudStorage:Enabled"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Only replace OIDC discovery with our test signing key.
            // All TokenValidationParameters from Program.cs remain untouched,
            // so attack tests verify the production validation config.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var oidcConfig = new OpenIdConnectConfiguration
                {
                    Issuer = JwtTestTokenBuilder.Issuer,
                };
                oidcConfig.SigningKeys.Add(JwtTestTokenBuilder.SigningKey);

                // Replace OIDC discovery with our static test config. Must override
                // ConfigurationManager (not just Configuration) because the framework's
                // PostConfigure already created one from the production Authority.
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidcConfig);
            });

            var mockUserInfo = new Mock<IGeopilotUserInfoService>();
            mockUserInfo
                .Setup(s => s.GetUserInfoAsync(It.IsAny<string>()))
                .Returns<string>(token =>
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(token);
                    var sub = jwt.Subject;

                    if (sub == JwtTestTokenBuilder.AdminSub)
                    {
                        return Task.FromResult<UserInfoResponse?>(new UserInfoResponse
                        {
                            Sub = JwtTestTokenBuilder.AdminSub,
                            Email = "admin@geopilot.ch",
                            Name = "Andreas Admin",
                        });
                    }

                    if (sub == JwtTestTokenBuilder.UserSub)
                    {
                        return Task.FromResult<UserInfoResponse?>(new UserInfoResponse
                        {
                            Sub = JwtTestTokenBuilder.UserSub,
                            Email = "user@geopilot.ch",
                            Name = "Ursula User",
                        });
                    }

                    return Task.FromResult<UserInfoResponse?>(null);
                });
            services.AddSingleton<IGeopilotUserInfoService>(mockUserInfo.Object);
        });
    }
}
