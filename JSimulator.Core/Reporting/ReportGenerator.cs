using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using JSimulator.Core.Models;

namespace JSimulator.Core.Reporting;

/// <summary>
/// Builds a professional, multi-section PDF report (inputs, formulas,
/// results, optional chart, and auto-generated interpretation) from a
/// <see cref="ReportData"/> bundle. Pure C#/QuestPDF — no UI dependency,
/// so it can be called from any project (WPF app, tests, CLI).
/// </summary>
public static class ReportGenerator
{
    static ReportGenerator()
    {
        // QuestPDF Community license — free for this project's use case.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Generates the PDF and returns the raw file bytes.</summary>
    public static byte[] Generate(ReportData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1A1F2E"));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>Generates the PDF and writes it directly to disk.</summary>
    public static void GenerateToFile(ReportData data, string filePath) =>
        System.IO.File.WriteAllBytes(filePath, Generate(data));

    private static void ComposeHeader(QuestPDF.Infrastructure.IContainer container, ReportData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(data.Title).FontSize(20).Bold().FontColor("#4C6EF5");
                    c.Item().Text("Queuing Suite — Analytical Report").FontSize(10).FontColor("#6B7280");
                });
                row.ConstantItem(160).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm}").FontSize(9).FontColor("#6B7280");
                    c.Item().AlignRight().Text($"Model(s): {string.Join(", ", data.Results.Select(r => r.ModelName))}").FontSize(9).FontColor("#6B7280");
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E3E7F1");
        });
    }

    private static void ComposeContent(QuestPDF.Infrastructure.IContainer container, ReportData data)
    {
        container.PaddingTop(16).Column(col =>
        {
            col.Spacing(18);

            col.Item().Element(c => SectionInputs(c, data.Input));

            foreach (var result in data.Results)
            {
                col.Item().Element(c => SectionModel(c, data.Input, result));
            }

            if (data.Simulation != null && data.Results.Count > 0)
            {
                col.Item().Element(c => SectionSimulation(c, data.Results[0], data.Simulation));
            }

            if (data.ChartImage is { Length: > 0 })
            {
                col.Item().Element(c => SectionChart(c, data.ChartImage, data.ChartCaption));
            }

            if (!string.IsNullOrWhiteSpace(data.AdditionalNotes))
            {
                col.Item().Element(c => SectionNotes(c, "Additional Notes", new List<string> { data.AdditionalNotes! }));
            }
        });
    }

    private static void SectionInputs(QuestPDF.Infrastructure.IContainer container, QueueInput input)
    {
        container.Column(col =>
        {
            col.Item().Text("Inputs").FontSize(13).Bold();
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                });

                void Row(string label1, string value1, string label2, string value2)
                {
                    table.Cell().Element(LabelCell).Text(label1);
                    table.Cell().Element(ValueCell).Text(value1);
                    table.Cell().Element(LabelCell).Text(label2);
                    table.Cell().Element(ValueCell).Text(value2);
                }

                Row("Arrival rate (λ)", $"{input.ArrivalRate:N4}", "Service rate (μ)", $"{input.ServiceRate:N4}");
                Row("Mean interarrival time", $"{input.MeanInterarrivalTime:N4}", "Mean service time", $"{input.MeanServiceTime:N4}");
                Row("Arrival time variance", $"{input.ArrivalTimeVariance:N4}", "Service time variance", $"{input.ServiceTimeVariance:N4}");
                Row("Ca² (arrival SCV)", $"{input.ArrivalScv:N4}", "Cs² (service SCV)", $"{input.ServiceScv:N4}");
                Row("Traffic intensity (ρ)", $"{input.Rho:P2}", "Arrival distribution", $"{input.ArrivalDistribution}");
            });
        });
    }

    private static void SectionModel(QuestPDF.Infrastructure.IContainer container, QueueInput input, QueueMetrics metrics)
    {
        container.Column(col =>
        {
            col.Item().Text($"{metrics.ModelName} Results").FontSize(13).Bold();
            col.Item().PaddingTop(2).Text(ReportNarrative.Description(metrics.ModelName)).FontSize(9).FontColor("#6B7280").Italic();

            col.Item().PaddingTop(8).Background("#F7F8FC").Padding(10).Text(ReportNarrative.Formula(metrics.ModelName)).FontSize(9).FontFamily("Consolas");

            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    for (int i = 0; i < 6; i++) c.RelativeColumn();
                });

                foreach (var header in new[] { "ρ", "L", "Lq", "W", "Wq", "P0" })
                    table.Cell().Element(HeaderCell).Text(header);

                table.Cell().Element(ValueCellCenter).Text($"{metrics.Rho:P2}");
                table.Cell().Element(ValueCellCenter).Text($"{metrics.L:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{metrics.Lq:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{metrics.W:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{metrics.Wq:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{metrics.P0:P2}");
            });

            col.Item().PaddingTop(8).Element(c => SectionNotes(c, "Interpretation", ReportNarrative.Interpretation(input, metrics)));
        });
    }

    private static void SectionSimulation(QuestPDF.Infrastructure.IContainer container, QueueMetrics analytical, SimulationResult simulation)
    {
        container.Column(col =>
        {
            col.Item().Text("Simulation vs. Analytical").FontSize(13).Bold();

            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    for (int i = 0; i < 5; i++) c.RelativeColumn();
                });

                foreach (var header in new[] { "Source", "ρ", "L", "Lq", "W", "Wq" })
                    table.Cell().Element(HeaderCell).Text(header);

                table.Cell().Element(ValueCell).Text("Analytical");
                table.Cell().Element(ValueCellCenter).Text($"{analytical.Rho:P2}");
                table.Cell().Element(ValueCellCenter).Text($"{analytical.L:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{analytical.Lq:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{analytical.W:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{analytical.Wq:F4}");

                var sim = simulation.Metrics;
                table.Cell().Element(ValueCell).Text("Simulated");
                table.Cell().Element(ValueCellCenter).Text($"{sim.Rho:P2}");
                table.Cell().Element(ValueCellCenter).Text($"{sim.L:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{sim.Lq:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{sim.W:F4}");
                table.Cell().Element(ValueCellCenter).Text($"{sim.Wq:F4}");
            });

            col.Item().PaddingTop(8).Element(c => SectionNotes(c, null, ReportNarrative.SimulationComparison(analytical, simulation)));
        });
    }

    private static void SectionChart(QuestPDF.Infrastructure.IContainer container, byte[] imageBytes, string? caption)
    {
        container.Column(col =>
        {
            col.Item().Text("Chart").FontSize(13).Bold();
            col.Item().PaddingTop(6).MaxHeight(260).Image(imageBytes).FitArea();
            if (!string.IsNullOrWhiteSpace(caption))
                col.Item().PaddingTop(4).AlignCenter().Text(caption).FontSize(9).FontColor("#6B7280").Italic();
        });
    }

    private static void SectionNotes(QuestPDF.Infrastructure.IContainer container, string? title, List<string> notes)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrEmpty(title))
                col.Item().Text(title).FontSize(11).Bold();

            foreach (var note in notes)
            {
                col.Item().PaddingTop(3).Row(row =>
                {
                    row.ConstantItem(12).Text("•").FontColor("#4C6EF5");
                    row.RelativeItem().Text(note).FontSize(9.5f).LineHeight(1.3f);
                });
            }
        });
    }

    private static void ComposeFooter(QuestPDF.Infrastructure.IContainer container)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor("#E3E7F1");
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Queuing Suite").FontSize(8).FontColor("#9CA3AF");
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(8).FontColor("#9CA3AF");
                    x.CurrentPageNumber().FontSize(8).FontColor("#9CA3AF");
                    x.Span(" of ").FontSize(8).FontColor("#9CA3AF");
                    x.TotalPages().FontSize(8).FontColor("#9CA3AF");
                });
            });
        });
    }

    private static QuestPDF.Infrastructure.IContainer LabelCell(QuestPDF.Infrastructure.IContainer c) =>
        c.PaddingVertical(3).DefaultTextStyle(x => x.FontColor("#6B7280").FontSize(9));

    private static QuestPDF.Infrastructure.IContainer ValueCell(QuestPDF.Infrastructure.IContainer c) =>
        c.PaddingVertical(3).DefaultTextStyle(x => x.FontSize(9.5f).Bold());

    private static QuestPDF.Infrastructure.IContainer ValueCellCenter(QuestPDF.Infrastructure.IContainer c) =>
        c.PaddingVertical(4).AlignCenter().DefaultTextStyle(x => x.FontSize(10));

    private static QuestPDF.Infrastructure.IContainer HeaderCell(QuestPDF.Infrastructure.IContainer c) =>
        c.Background("#4C6EF5").Padding(5).AlignCenter().DefaultTextStyle(x => x.FontColor("#FFFFFF").FontSize(9).Bold());
}
