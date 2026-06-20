using FortressSouls.Application;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/health", () => HealthResponse.CreateBasic())
    .WithName("Health")
    .Produces<HealthResponse>();

app.Run();
