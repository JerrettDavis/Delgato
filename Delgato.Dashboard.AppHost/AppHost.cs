var builder = DistributedApplication.CreateBuilder(args);

// Delgato Agent Swarm Worker Service
var delgatoWorker = builder.AddProject<Projects.Delgato>("delgato-worker")
    .WithHttpHealthCheck("/health");

var apiService = builder.AddProject<Projects.Delgato_Dashboard_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Delgato Dashboard Web Frontend
builder.AddProject<Projects.Delgato_Dashboard_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(delgatoWorker)
    .WaitFor(apiService)
    .WaitFor(delgatoWorker);

builder.Build().Run();
