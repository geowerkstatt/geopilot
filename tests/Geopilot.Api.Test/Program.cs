using Geopilot.Api;
using Geopilot.Api.StacServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Stac.Api.WebApi;

// This entry point is used by GeopilotApiApp for integration tests.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiVersioning();
builder.Services.AddCors();

builder.Services
    .AddControllers()
    .ConfigureApplicationPartManager(options =>
    {
        options.ApplicationParts.Add(new AssemblyPart(typeof(Context).Assembly));
        options.ApplicationParts.Add(new AssemblyPart(typeof(StacApiController).Assembly));
    });

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings.TryAdd(".log", "text/plain");
contentTypeProvider.Mappings.TryAdd(".xtf", "application/interlis+xml");
builder.Services.AddSingleton<IContentTypeProvider>(contentTypeProvider);

var factory = new Mock<IDbContextFactory<Context>>();
factory.Setup(f => f.CreateDbContext()).Returns(AssemblyInitialize.DbFixture.GetTestContext);
builder.Services.AddSingleton(factory.Object);
builder.Services.AddTransient((provider) => provider.GetRequiredService<IDbContextFactory<Context>>().CreateDbContext());

builder.Services.AddStacData(builder => { });

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();
