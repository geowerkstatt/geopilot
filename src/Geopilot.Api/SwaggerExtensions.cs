using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Geopilot.Api;

/// <summary>
/// Provides extension methods related to Swagger.
/// </summary>
public static class SwaggerExtensions
{
    private const string SchemeName = "Authorization";

    /// <summary>
    /// Adds a security definition and requirement for OpenId Connect using the well-known configuration of the <paramref name="authority"/>.
    /// </summary>
    /// <param name="options">The swagger options.</param>
    /// <param name="authority">The authority and token issuer.</param>
    public static void AddOpenIdConnect(this SwaggerGenOptions options, string authority)
    {
        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
        {
            Name = SchemeName,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri($"{authority}/.well-known/openid-configuration"),
        });
        options.AddOAuth2SecurityRequirement();
    }

    /// <summary>
    /// Adds a security definition and requirement for OAuth2 authorization code flow.
    /// </summary>
    /// <param name="options">The swagger options.</param>
    /// <param name="authUrl">The authorization URL.</param>
    /// <param name="tokenUrl">The token URL.</param>
    /// <param name="apiServerScope">An optional scope defined for the client.</param>
    public static void AddGeopilotOAuth2(this SwaggerGenOptions options, string authUrl, string tokenUrl, string? apiServerScope)
    {
        var scopes = new Dictionary<string, string>
        {
            { "openid", "Open Id" },
            { "email", "User Email" },
            { "profile", "User Profile" },
        };
        if (apiServerScope != null)
        {
            scopes.Add(apiServerScope, "geopilot API (required)");
        }

        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
        {
            Name = SchemeName,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    Scopes = scopes,
                    AuthorizationUrl = new Uri(authUrl),
                    TokenUrl = new Uri(tokenUrl),
                    RefreshUrl = new Uri(tokenUrl),
                },
            },
        });
        options.AddOAuth2SecurityRequirement();
    }

    /// <summary>
    /// Adds a security requirement for OAuth2.
    /// </summary>
    /// <param name="options">The swagger options.</param>
    private static void AddOAuth2SecurityRequirement(this SwaggerGenOptions options)
    {
        options.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = [],
        });
    }
}
