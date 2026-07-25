var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var jwtKey = builder.AddParameter("jwt-key", secret: true);
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume();
var redis = builder.AddRedis("redis").WithDataVolume();

var identityDb = postgres.AddDatabase("identity");
var catalogDb = postgres.AddDatabase("catalog");
var subscriptionsDb = postgres.AddDatabase("subscriptions");

var identity = builder.AddProject<Projects.OpenFlix_Identity>("identity")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithEnvironment("Jwt__Key", jwtKey);

var catalog = builder.AddProject<Projects.OpenFlix_Catalog>("catalog")
    .WithReference(catalogDb).WithReference(redis)
    .WaitFor(catalogDb).WaitFor(redis)
    .WithEnvironment("Jwt__Key", jwtKey);

var subscriptions = builder.AddProject<Projects.OpenFlix_Subscriptions>("subscriptions")
    .WithReference(subscriptionsDb).WaitFor(subscriptionsDb)
    .WithEnvironment("Jwt__Key", jwtKey);

builder.AddProject<Projects.OpenFlix_Gateway>("gateway")
    .WithReference(identity).WithReference(catalog).WithReference(subscriptions)
    .WaitFor(identity).WaitFor(catalog).WaitFor(subscriptions)
    .WithExternalHttpEndpoints();

builder.Build().Run();

