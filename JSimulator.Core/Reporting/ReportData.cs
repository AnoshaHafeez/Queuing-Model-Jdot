using JSimulator.Core.Models;

namespace JSimulator.Core.Reporting;

/// <summary>
/// Everything the report generator needs to produce a PDF: the inputs and
/// results for one or more models, plus optional chart images and a
/// simulation comparison. Built by the App layer (which has access to the
/// WPF/OxyPlot chart controls) and handed off to <see cref="ReportGenerator"/>,
/// which itself has no UI dependency.
/// </summary>
public class ReportData
{
    /// <summary>Title shown at the top of the report, e.g. "Queueing Analysis Report".</summary>
    public string Title { get; set; } = "Queueing Analysis Report";

    /// <summary>When the report was generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    /// <summary>Inputs used to produce the analytical result(s).</summary>
    public QueueInput Input { get; set; } = new();

    /// <summary>One result per model shown in the report (usually one, but Comparison can pass all three).</summary>
    public List<QueueMetrics> Results { get; set; } = new();

    /// <summary>Optional discrete-event simulation result, shown alongside the analytical numbers if present.</summary>
    public SimulationResult? Simulation { get; set; }

    /// <summary>Optional chart, PNG-encoded, embedded after the results table.</summary>
    public byte[]? ChartImage { get; set; }

    /// <summary>Optional caption shown under <see cref="ChartImage"/>.</summary>
    public string? ChartCaption { get; set; }

    /// <summary>Free-form notes appended by the caller in addition to the auto-generated interpretation.</summary>
    public string? AdditionalNotes { get; set; }
}
