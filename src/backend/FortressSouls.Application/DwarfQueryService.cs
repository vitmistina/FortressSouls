namespace FortressSouls.Application;

using System.Diagnostics;
using FortressSouls.Domain;
using FortressSouls.Observability;

public sealed class DwarfQueryService(
    IDwarfFortressAdapter adapter,
    DwarfAdapterDescriptor adapterDescriptor)
{
    private readonly IDwarfFortressAdapter _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    private readonly DwarfAdapterDescriptor _adapterDescriptor = adapterDescriptor ?? throw new ArgumentNullException(nameof(adapterDescriptor));

    public async Task<DwarfListQueryResult> ListDwarvesAsync(CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.DwarvesListActivityName,
            ActivityKind.Internal);

        activity?.SetTag(FortressSoulsTelemetry.AdapterTypeTagName, _adapterDescriptor.AdapterType);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity?.SetTag(FortressSoulsTelemetry.CorrelationIdTagName, correlationId);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var list = await _adapter.ListDwarvesAsync(cancellationToken);
            stopwatch.Stop();

            activity?.SetTag(FortressSoulsTelemetry.SnapshotSchemaVersionTagName, list.SchemaVersion);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);
            FortressSoulsTelemetry.RecordDwarfListDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                _adapterDescriptor.AdapterType,
                list.SchemaVersion,
                FortressSoulsTelemetry.SuccessOutcome);

            return new DwarfListQueryResult(_adapterDescriptor.AdapterType, list, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.CancelledOutcome);
            FortressSoulsTelemetry.RecordDwarfListDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                _adapterDescriptor.AdapterType,
                "unknown",
                FortressSoulsTelemetry.CancelledOutcome);
            throw;
        }
        catch (Exception exception) when (exception is DwarfFortressDataException or DwarfNotFoundException)
        {
            stopwatch.Stop();
            var outcome = MapOutcome(exception);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, outcome);
            activity?.SetTag(FortressSoulsTelemetry.ErrorCategoryTagName, MapErrorCategory(exception));
            FortressSoulsTelemetry.RecordDwarfListDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                _adapterDescriptor.AdapterType,
                "unknown",
                outcome);
            throw;
        }
    }

    public async Task<DwarfSnapshotQueryResult> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            FortressSoulsTelemetry.DwarvesSnapshotActivityName,
            ActivityKind.Internal);

        activity?.SetTag(FortressSoulsTelemetry.AdapterTypeTagName, _adapterDescriptor.AdapterType);
        activity?.SetTag(FortressSoulsTelemetry.DwarfIdTagName, dwarfId.ToString());
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity?.SetTag(FortressSoulsTelemetry.CorrelationIdTagName, correlationId);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var snapshot = await _adapter.GetDwarfSnapshotAsync(dwarfId, cancellationToken);
            stopwatch.Stop();

            activity?.SetTag(FortressSoulsTelemetry.SnapshotSchemaVersionTagName, snapshot.SchemaVersion);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);
            FortressSoulsTelemetry.RecordDwarfSnapshotDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                _adapterDescriptor.AdapterType,
                snapshot.SchemaVersion,
                FortressSoulsTelemetry.SuccessOutcome);

            return new DwarfSnapshotQueryResult(_adapterDescriptor.AdapterType, snapshot, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.CancelledOutcome);
            FortressSoulsTelemetry.RecordDwarfSnapshotDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                _adapterDescriptor.AdapterType,
                "unknown",
                FortressSoulsTelemetry.CancelledOutcome);
            throw;
        }
        catch (Exception exception) when (exception is DwarfFortressDataException or DwarfNotFoundException)
        {
            stopwatch.Stop();
            var outcome = MapOutcome(exception);
            activity?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, outcome);
            activity?.SetTag(FortressSoulsTelemetry.ErrorCategoryTagName, MapErrorCategory(exception));
            FortressSoulsTelemetry.RecordDwarfSnapshotDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                _adapterDescriptor.AdapterType,
                "unknown",
                outcome);
            throw;
        }
    }

    private static string MapOutcome(Exception exception) =>
        exception switch
        {
            DwarfNotFoundException => FortressSoulsTelemetry.NotFoundOutcome,
            DwarfFortressDataException => FortressSoulsTelemetry.ErrorOutcome,
            _ => FortressSoulsTelemetry.ErrorOutcome
        };

    private static string MapErrorCategory(Exception exception) =>
        exception switch
        {
            DwarfNotFoundException => "not_found",
            DwarfFortressDataException dataException => dataException.ErrorCode.ToString(),
            _ => "error"
        };

    private static string? GetCorrelationId() =>
        Activity.Current?.GetTagItem(FortressSoulsTelemetry.CorrelationIdTagName)?.ToString();
}
