using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

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
    /// <exception cref="InvalidOperationException">Auth:AccessTokenFormat is not a supported value, or an entry of Auth:RequiredClaims has no name or no value.</exception>
    public static AccessTokenFormat AddGeopilotAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var requiredClaims = builder.ReadRequiredClaims();
        var accessTokenFormat = builder.Configuration.GetValue("Auth:AccessTokenFormat", AccessTokenFormat.Jwt);
        switch (accessTokenFormat)
        {
            case AccessTokenFormat.Jwt:
                builder.AddJwtBearerAuthentication(requiredClaims);
                break;

            case AccessTokenFormat.Opaque:
                builder.AddOpaqueTokenAuthentication(requiredClaims);
                break;

            default:
                throw new InvalidOperationException($"Unsupported Auth:AccessTokenFormat '{accessTokenFormat}'.");
        }

        return accessTokenFormat;
    }

    private static RequiredClaim[] ReadRequiredClaims(this WebApplicationBuilder builder)
    {
        var requiredClaims = builder.Configuration.GetSection("Auth:RequiredClaims").Get<RequiredClaim[]>() ?? [];
        if (requiredClaims.Any(claim => string.IsNullOrWhiteSpace(claim.Name) || string.IsNullOrWhiteSpace(claim.Value)))
        {
            throw new InvalidOperationException("Every entry of Auth:RequiredClaims needs a Name and a Value.");
        }

        return requiredClaims;
    }

    private static void AddOpaqueTokenAuthentication(this WebApplicationBuilder builder, IReadOnlyList<RequiredClaim> requiredClaims)
    {
        builder.Services
            .AddOptions<OpaqueTokenOptions>(JwtBearerDefaults.AuthenticationScheme)
            .BindConfiguration("Auth")
            .PostConfigure(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.RequiredClaims = requiredClaims;
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

    private static void AddJwtBearerAuthentication(this WebApplicationBuilder builder, IReadOnlyList<RequiredClaim> requiredClaims)
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
                    OnTokenValidated = async context =>
                    {
                        // Before the anonymous return: an anonymous endpoint that reads the current user must see no user.
                        foreach (var required in requiredClaims)
                        {
                            var hasClaim = context.Principal?.HasClaim(claim =>
                                string.Equals(claim.Type, required.Name, StringComparison.Ordinal) &&
                                string.Equals(claim.Value, required.Value, StringComparison.Ordinal)) == true;
                            if (!hasClaim)
                            {
                                context.Fail($"Token does not have required claim {required.Name} with the configured value.");
                                return;
                            }
                        }

                        var endpoint = context.HttpContext.GetEndpoint();
                        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                        {
                            return;
                        }

                        // Fetch user info during authentication, so an unreachable identity provider
                        // fails with a typed reason here instead of a 403 in the authorization handler.
                        // The result is discarded on purpose: the resolver keeps it for the authorization
                        // handlers, and asks nothing for a registered machine client.
                        var token = ((JsonWebToken)context.SecurityToken).EncodedToken;
                        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var userResolver = context.HttpContext.RequestServices.GetRequiredService<IGeopilotUserResolver>();
                        try
                        {
                            _ = await userResolver.PrefetchUserInfoAsync(subject, token, context.HttpContext.RequestAborted);
                        }
                        catch (IdentityProviderUnavailableException ex)
                        {
                            context.Fail(ex);
                        }
                    },
                    OnChallenge = async context =>
                    {
                        if (context.AuthenticateFailure is IdentityProviderUnavailableException)
                        {
                            context.HandleResponse();
                            await Results.Problem(
                                statusCode: StatusCodes.Status503ServiceUnavailable,
                                title: "Authentication unavailable",
                                detail: "Authentication currently not possible.")
                                .ExecuteAsync(context.HttpContext);
                        }
                    },
                };
            });
    }
}
