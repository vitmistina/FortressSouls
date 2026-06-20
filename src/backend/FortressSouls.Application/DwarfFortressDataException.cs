namespace FortressSouls.Application;

using FortressSouls.Domain;

public interface IDwarfFortressAdapter
{
    Task<DwarfListResult> ListDwarvesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads a snapshot for the browser-selected dwarf ID. The adapter does not track any UI cursor state.
    /// </summary>
    Task<DwarfSnapshot> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken);
}

public enum DwarfFortressDataErrorCode
{
    MissingSource,
    SourceUnavailable,
    DataTooLarge,
    MalformedJson,
    UnsupportedSchema,
    InvalidData,
    InconsistentData
}

public sealed class DwarfFortressDataException : Exception
{
    public DwarfFortressDataException(DwarfFortressDataErrorCode errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public DwarfFortressDataErrorCode ErrorCode { get; }
}
