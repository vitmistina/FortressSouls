using FortressSouls.Application;
using FortressSouls.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});

builder.Services.AddFortressSoulsObservability(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseFortressSoulsCorrelationId();

var observabilityState = ObservabilityConfiguration.GetHealthState(builder.Configuration);
app.Logger.LogInformation(
    "Fortress Souls API starting with observability {ObservabilityState}",
    observabilityState);

app.Lifetime.ApplicationStarted.Register(() =>
{
    FortressSoulsTelemetry.RecordStartup(observabilityState);
});

app.MapGet("/api/health", () => HealthResponse.CreateBasic(observabilityState))
    .WithName("Health")
    .Produces<HealthResponse>();

app.Run();
