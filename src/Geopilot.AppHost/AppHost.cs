using Aspire.Hosting.Docker;

var builder = DistributedApplication.CreateBuilder(args);

// Add a default for the database name
var postgresDbName = builder.AddParameter("db-name", "geopilot", publishValueAsDefault: true);
var postgresUser = builder.AddParameter("db-user", "HAPPYWALK");
var postgresPassword = builder.AddParameter("db-password", "SOMBERSPORK");

// Create these as variables to be used elsewhere
var dbPort = 5432;
var dbContainerName = "db";
var dbName = "geopilot";

var db = builder.AddPostgres("db", postgresUser, postgresPassword, dbPort)
    .WithImage("postgis/postgis") // Ensure we use the same image as docker-compose
    .WithContainerName(dbContainerName) // Use a fixed container name
    .WithLifetime(ContainerLifetime.Persistent) // Don't tear-down the container when we stop Aspire
    .WithEnvironment("POSTGRES_DB", postgresDbName)
    .WithHostPort(dbPort)
    .WithPgAdmin(pgAdmin =>
    {
        pgAdmin
            .WithEnvironment("PGADMIN_DEFAULT_EMAIL", "pgadmin@example.com")
            .WithEnvironment("PGADMIN_DEFAULT_PASSWORD", "BOUNCEAUTO")
            .WithEnvironment("PGADMIN_CONFIG_SERVER_MODE", "False")
            .WithEnvironment("PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED", "False")
            //.WithVolume("./config/pgadmin4-servers.json", "/pgadmin4/servers.json", isReadOnly: true)
            //.WithEntrypoint(@"/bin/sh -c /bin/echo '*:*:geopilot:HAPPYWALK:SOMBERSPORK' > /tmp/.pgpass chmod 0600 /tmp/.pgpass /entrypoint.sh")
            .WithHostPort(3001);
    });

var keycloak = builder.AddKeycloak("keycloak", port: 4011)
    .WithImageTag("latest")
    .WithEnvironment("KEYCLOAK_ADMIN", "SOMBERTOTE")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "STRANGENIGHT")
    .WithRealmImport("./config/realms");

var stacBrowser = builder
    .AddContainer("stac-browser", "ghcr.io/geowerkstatt/stac-browser", "3.2.0")
    .WithEnvironment("SB_catalogUrl", "https://localhost:5173/api/stac")
    .WithEnvironment("SB_locale", "de-CH")
    .WithEnvironment("SB_fallbackLocale", "de-CH")
    .WithEnvironment("SB_supportedLocales", "de-CH")
    .WithHttpEndpoint(8080, 8080);

var interlis = builder
    .AddContainer("interlis-check-service", "ghcr.io/geowerkstatt/interlis-check-service")
    .WithHttpEndpoint(3080, 8080);

builder
    .AddDockerfile("geopilot", "../../.")
    .WithBindMount("../../docker-entrypoint.sh", "/entrypoint.sh")
    .WithContainerName("geopilot")
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(db)
    .WaitFor(keycloak)
    .WithHttpsEndpoint(5173, 8443)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_HTTPS_PORTS", "8443")
    .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/https/cert.pem")
    .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__KeyPath", "/https/cert.key")
    .WithBuildSecret("ConnectionStrings__Context", builder.AddParameter("dbConn", $"Server={dbContainerName};Port={dbPort.ToString()};Database={dbName};User Id={postgresUser.Resource.Value};Password={postgresPassword.Resource.Value};")) //$"Server=aaaaaaa;Port={dbPort.ToString()};Database={dbName};User Id={postgresUser.Resource.Value};Password={postgresPassword.Resource.Value};") //TODO
    .WithEnvironment("ReverseProxy__Clusters__stacBrowserCluster__Destinations__stacBrowserDestination__Address", stacBrowser.GetEndpoint("http"))
    .WithEnvironment("Auth__Authority", "http://localhost:4011/realms/geopilot")
    .WithEnvironment("Auth__ClientId", "geopilot-client")
    .WithEnvironment("Auth__AuthorizationUrl", "http://localhost:4011/realms/geopilot/protocol/openid-connect/auth")
    .WithEnvironment("Auth__TokenUrl", "http://localhost:4011/realms/geopilot/protocol/openid-connect/token")
    .WithEnvironment("Auth__ApiOrigin", "http://localhost:5173")
    .WithEnvironment("Validation__InterlisCheckServiceUrl", interlis.GetEndpoint("http"))
    .WithEnvironment("PUID", "1000")
    .WithEnvironment("PGID", "1000")
    .WithVolume("./src/Geopilot.Api/Uploads", "/uploads")
    .WithVolume("./src/Geopilot.Api/Persistent", "/assets")
    .WithVolume("./src/Geopilot.Frontend/devPublic", "/public")
    .WithVolume("./certs", "/https", isReadOnly: true)
    .WithReferenceRelationship(stacBrowser)
    .WithReferenceRelationship(interlis);

builder.Build().Run();
