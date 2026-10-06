namespace JSimulator.App.Services;

/// <summary>
/// Holds the currently imported J. Data profile for this app session. It keeps
/// Calculator, Simulation, and Comparison aligned after a single Excel import;
/// it is intentionally not persisted because each workbook is an analysis input.
/// </summary>
public sealed class ImportedQueueProfile
{
    public static ImportedQueueProfile Instance { get; } = new();

    public QueueExcelImportResult? Current { get; private set; }

    /// <summary>Raised on the UI thread after a valid workbook is imported.</summary>
    public event Action<QueueExcelImportResult>? Imported;

    private ImportedQueueProfile() { }

    public void Publish(QueueExcelImportResult result)
    {
        Current = result;
        Imported?.Invoke(result);
    }
}
