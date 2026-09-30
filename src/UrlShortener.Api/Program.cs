var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness only: the process is up and serving requests. Readiness (database) arrives with persistence in GF-03.
app.MapHealthChecks("/health/live");

app.Run();
