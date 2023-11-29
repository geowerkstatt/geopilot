using Asp.Versioning;
using GeoCop.Api;
using GeoCop.Api.Authorization;
using GeoCop.Api.Conventions;
using GeoCop.Api.FileAccess;
using GeoCop.Api.StacServices;
using GeoCop.Api.Validation;
using GeoCop.Api.Validation.Interlis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    // DotNetStac.Api uses the "All" policy for access in the STAC browser.
    options.AddPolicy("All",
            policy =>
            {
                policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
});

builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new StacRoutingConvention(GeocopPolicies.Admin));
        options.Conventions.Add(new GeocopJsonConvention());

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

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:ClientId"];
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Allow token to be in a cookie in addition to the default Authorization header
                context.Token = context.Request.Cookies["geocop.auth"];
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
        Title = $"geocop API Documentation",
    });

    // Include existing documentation in Swagger UI.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));

    options.EnableAnnotations();
    options.SupportNonNullableReferenceTypes();

    // Workaround for STAC API having multiple actions mapped to the "search" route.
    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(GeocopPolicies.Admin, policy =>
    {
        policy.Requirements.Add(new GeocopUserRequirement
        {
            RequireAdmin = true,
        });
    });
    options.AddPolicy(GeocopPolicies.User, policy =>
    {
        policy.Requirements.Add(new GeocopUserRequirement());
    });

    var adminPolicy = options.GetPolicy(GeocopPolicies.Admin) ?? throw new InvalidOperationException("Missing Admin authorization policy");
    options.DefaultPolicy = adminPolicy;
    options.FallbackPolicy = adminPolicy;
});
builder.Services.AddTransient<IAuthorizationHandler, GeocopUserHandler>();

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings.TryAdd(".log", "text/plain");
contentTypeProvider.Mappings.TryAdd(".xtf", "application/interlis+xml");
builder.Services.AddSingleton<IContentTypeProvider>(contentTypeProvider);

builder.Services.AddSingleton<IValidationRunner, ValidationRunner>();
builder.Services.AddHostedService(services => (ValidationRunner)services.GetRequiredService<IValidationRunner>());
builder.Services.AddTransient<IValidationService, ValidationService>();
builder.Services.AddTransient<IDirectoryProvider, DirectoryProvider>();
builder.Services.AddTransient<IFileProvider, PhysicalFileProvider>();
builder.Services.AddTransient<IValidationAssetPersistor, ValidationAssetPersistor>();
builder.Services.AddTransient<IPersistedAssetDeleter, PersistedAssetDeleter>();

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

var configureContextOptions = (DbContextOptionsBuilder options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Context"), o =>
    {
        o.UseNetTopologySuite();
        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
};

builder.Services.AddDbContextFactory<Context>(configureContextOptions);
builder.Services.AddDbContext<Context>(configureContextOptions);

builder.Services.AddStacData(builder => { });

builder.Services
    .AddHealthChecks()
    .AddCheck<DbHealthCheck>("Db");

var app = builder.Build();

// Migrate db changes on startup
using var scope = app.Services.CreateScope();
using var context = scope.ServiceProvider.GetRequiredService<Context>();
context.Database.Migrate();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "geocop API v1.0");
});

if (app.Environment.IsDevelopment())
{
    app.UseCors("All");

    if (!context.DeliveryMandates.Any())
        context.SeedTestData();

    app.UseMiddleware<DevelopmentAuthorizationMiddleware>();
}
else
{
    // Disallow CORS for all origins in production
    app.UseCors();

    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var authorizationService = context.RequestServices.GetRequiredService<IAuthorizationService>();
    if (context.Request.Path.StartsWithSegments("/browser") && !(await authorizationService.AuthorizeAsync(context.User, GeocopPolicies.Admin)).Succeeded)
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

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.MapReverseProxy();

app.Run();
