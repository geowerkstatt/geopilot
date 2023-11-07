using Asp.Versioning;
using GeoCop.Api;
using GeoCop.Api.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
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
});

builder.Services.AddSingleton<IValidatorService, ValidatorService>();
builder.Services.AddHostedService(services => (ValidatorService)services.GetRequiredService<IValidatorService>());
builder.Services.AddTransient<IValidator, InterlisValidator>();
builder.Services.AddTransient<IFileProvider, PhysicalFileProvider>(x => new PhysicalFileProvider(x.GetRequiredService<IConfiguration>(), "GEOCOP_UPLOADS_DIR"));

builder.Services.AddDbContext<Context>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DeliveryContext"), o =>
    {
        o.UseNetTopologySuite();
        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
});

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
    if (!context.DeliveryMandates.Any())
        context.SeedTestData();
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
