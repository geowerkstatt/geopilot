using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = JwtTestTokenBuilder.Issuer,
                    ValidAudience = JwtTestTokenBuilder.Audience,
                    IssuerSigningKey = JwtTestTokenBuilder.SigningKey,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
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
