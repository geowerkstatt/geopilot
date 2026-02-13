using Asp.Versioning;
using Geopilot.Api;
using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Conventions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.Api.StacServices;
using Geopilot.Api.Validation;
using Geopilot.Api.Validation.Interlis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    // DotNetStac.Api uses the "All" policy for access in the STAC browser.
    options.AddPolicy(
        "All", policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
});

builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new StacRoutingConvention(GeopilotPolicies.Admin));
        options.Conventions.Add(new GeopilotJsonConvention());

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.Configure<BrowserAuthOptions>(builder.Configuration.GetSection("Auth"));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:ApiAudience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Allow token to be in a cookie in addition to the default Authorization header
                context.Token = context.Request.Cookies["geopilot.auth"];
                return Task.CompletedTask;
            },
        };
    });

builder.Services
    .AddApiVersioning(config =>
    {
        config.AssumeDefaultVersionWhenUnspecified = false;
        config.ReportApiVersions = true;
        config.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "1.0",
        Title = $"geopilot API Documentation",
    });

    // Include existing documentation in Swagger UI.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));

    options.EnableAnnotations();
    options.SupportNonNullableReferenceTypes();

    // Workaround for STAC API having multiple actions mapped to the "search" route.
    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    var authUrl = builder.Configuration["Auth:AuthorizationUrl"];
    var tokenUrl = builder.Configuration["Auth:TokenUrl"];
    var apiScope = builder.Configuration["Auth:ApiServerScope"];
    if (!string.IsNullOrEmpty(authUrl) && !string.IsNullOrEmpty(tokenUrl) && !string.IsNullOrEmpty(apiScope))
    {
        options.AddGeopilotOAuth2(authUrl, tokenUrl, apiScope);
    }
    else
    {
        var authority = builder.Configuration["Auth:Authority"];
        options.AddOpenIdConnect(authority!);
    }
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(GeopilotPolicies.Admin, policy =>
    {
        policy.Requirements.Add(new GeopilotUserRequirement
        {
            RequireAdmin = true,
        });
    });
    options.AddPolicy(GeopilotPolicies.User, policy =>
    {
        policy.Requirements.Add(new GeopilotUserRequirement
        {
            RequireAdmin = false,
        });
    });

    var adminPolicy = options.GetPolicy(GeopilotPolicies.Admin) ?? throw new InvalidOperationException("Missing Admin authorization policy");
    options.DefaultPolicy = adminPolicy;
    options.FallbackPolicy = adminPolicy;
});
builder.Services.AddTransient<IAuthorizationHandler, GeopilotUserHandler>();

builder.Services.Configure<ValidationOptions>(builder.Configuration.GetSection("Validation"));

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings.TryAdd(".log", "text/plain");
contentTypeProvider.Mappings.TryAdd(".xtf", "application/interlis+xml");
builder.Services.AddSingleton<IContentTypeProvider>(contentTypeProvider);

builder.Services.AddSingleton<IValidationJobStore, ValidationJobStore>();
builder.Services.AddTransient<IValidationService, ValidationService>();
builder.Services.AddTransient<IMandateService, MandateService>();
builder.Services.AddTransient<IDirectoryProvider, DirectoryProvider>();
builder.Services.AddTransient<IFileProvider, PhysicalFileProvider>();
builder.Services.AddTransient<IAssetHandler, AssetHandler>();
builder.Services.AddHostedService<ValidationRunner>();
builder.Services.AddHostedService<ValidationJobCleanupService>();
builder.Services.RegisterPipelineFactory();

builder.Services
    .AddHttpClient<IValidator, InterlisValidator>("INTERLIS_VALIDATOR_HTTP_CLIENT")
    .ConfigureHttpClient((services, httpClient) =>
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var checkServiceUrl = configuration.GetValue<string>("Validation:InterlisCheckServiceUrl")
            ?? throw new InvalidOperationException("Missing InterlisCheckServiceUrl to validate INTERLIS transfer files.");

        httpClient.BaseAddress = new Uri(checkServiceUrl);
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    });

builder.Services.AddHttpClient<IGeopilotUserInfoService, GeopilotUserInfoService>();
builder.Services.AddScoped<IGeopilotUserInfoService, GeopilotUserInfoService>();
builder.Services.AddHttpContextAccessor();

var configureContextOptions = (DbContextOptionsBuilder options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(Context)), o =>
    {
        o.UseNetTopologySuite();
        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
};

builder.Services.AddDbContextFactory<Context>(configureContextOptions);

builder.Services.AddStacData(builder => { });

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<Context>("Database")
    .AddCheck<ValidationServiceHealthCheck>("Validators")
    .AddCheck<StorageHealthCheck>("Storage");

// Set the maximum request body size to 100MB
const int MaxRequestBodySize = 104857600;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxRequestBodySize);
builder.Services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = MaxRequestBodySize);

var app = builder.Build();

// Migrate db changes on startup
using var scope = app.Services.CreateScope();
using var context = scope.ServiceProvider.GetRequiredService<Context>();
if (context.Database.GetPendingMigrations().Any())
{
    context.MigrateDatabase();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "geopilot API v1.0");

    options.OAuthClientId(builder.Configuration["Auth:ClientAudience"]);
    options.OAuth2RedirectUrl($"{builder.Configuration["Auth:ApiOrigin"]}/swagger/oauth2-redirect.html");
    options.OAuthUsePkce();
});

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("All");

    if (!context.Mandates.Any())
        context.SeedTestData();
}
else
{
    // Disallow CORS for all origins in production
    app.UseCors();
}

app.Use(async (context, next) =>
{
    var authorizationService = context.RequestServices.GetRequiredService<IAuthorizationService>();
    if (context.Request.Path.StartsWithSegments("/browser") && !(await authorizationService.AuthorizeAsync(context.User, GeopilotPolicies.Admin)).Succeeded)
    {
        context.Response.Redirect("/");
    }
    else if (context.Request.Path == "/browser")
    {
        context.Response.Redirect("/browser/");
    }
    else
    {
        await next(context);
    }
});

// By default Kestrel responds with a HTTP 400 if payload is too large.
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > MaxRequestBodySize)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsync("Payload Too Large");
        return;
    }

    await next.Invoke();
});

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health")
    .AllowAnonymous();

app.MapReverseProxy();

app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
