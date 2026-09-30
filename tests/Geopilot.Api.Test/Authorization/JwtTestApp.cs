using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using System.IdentityModel.Tokens.Jwt;

namespace Geopilot.Api.Authorization;

internal class JwtTestApp : GeopilotTestApp
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:AccessTokenFormat", "Jwt");

        // Read in the builder phase, so it has to come through UseSetting. Pinned here rather than taken from
        // appsettings.Development.json, so the security suite covers the machine delivery routes on purpose.
        builder.UseSetting("MachineDelivery:Enabled", "true");

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var oidcConfig = new OpenIdConnectConfiguration
                {
                    Issuer = JwtTestTokenBuilder.Issuer,
                };
                oidcConfig.SigningKeys.Add(JwtTestTokenBuilder.SigningKey);

                // Replace OIDC discovery with static test configuration. Must override
                // ConfigurationManager (not just Configuration) because the framework
                // PostConfigure already created one from the production Authority.
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidcConfig);
            });

            var mockUserInfo = new Mock<IGeopilotUserInfoService>();
            mockUserInfo
                .Setup(s => s.GetUserInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((token, _) =>
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(token);
                    var sub = jwt.Subject;

                    if (sub == JwtTestTokenBuilder.IdpUnavailableSub)
                    {
                        throw new IdentityProviderUnavailableException("User info request failed.");
                    }

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
