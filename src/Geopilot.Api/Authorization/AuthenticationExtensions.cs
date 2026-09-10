using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Provides extension methods to register authentication services.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers the authentication services on <paramref name="builder"/> based on the configured token format.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The configured <see cref="AccessTokenFormat"/>.</returns>
    public static AccessTokenFormat AddGeopilotAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var accessTokenFormat = builder.Configuration.GetValue("Auth:AccessTokenFormat", AccessTokenFormat.Jwt);
        if (accessTokenFormat == AccessTokenFormat.Opaque)
        {
            builder.Services
                .AddOptions<OpaqueTokenOptions>(JwtBearerDefaults.AuthenticationScheme)
                .BindConfiguration("Auth")
                .PostConfigure(options =>
                {
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                })
                .Validate(options =>
                {
                    options.Validate();
                    return true;
                })
                .ValidateOnStart();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddScheme<OpaqueTokenOptions, OpaqueTokenHandler>(
                    JwtBearerDefaults.AuthenticationScheme,
                    _ => { });

            builder.Services.AddHttpClient(OpaqueTokenHandler.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }
        else
        {
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = builder.Configuration["Auth:Authority"];
                    options.Audience = builder.Configuration["Auth:Audience"];
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    options.MapInboundClaims = false;

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            // Allow token to be in a cookie in addition to the default Authorization header.
                            // Only override when a cookie is actually present, otherwise a stale/empty cookie
                            // would shadow a valid Authorization header and break Swagger/API clients.
                            var cookieToken = context.Request.Cookies[AuthDefaults.AuthCookieName];
                            if (!string.IsNullOrEmpty(cookieToken))
                            {
                                context.Token = cookieToken;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }

        return accessTokenFormat;
    }
}
