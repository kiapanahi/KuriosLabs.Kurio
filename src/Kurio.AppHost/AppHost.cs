var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Kurio_Server>("server");

builder.Build().Run();
