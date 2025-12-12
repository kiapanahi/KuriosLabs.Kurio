var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.Kurio_Server>("server");

var web = builder.AddProject<Projects.Kurio_Web>("web");

web.WithReference(server)
    .WaitFor(server);

builder.Build().Run();
