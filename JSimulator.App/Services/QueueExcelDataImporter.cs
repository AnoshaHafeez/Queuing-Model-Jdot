using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;

namespace JSimulator.App.Services;

/// <summary>
/// Converts J. Data's operational timestamps into the parameters required by
/// the single-server queueing models. The import intentionally uses
/// <c>Arrival_Time</c>, <c>Service_In</c>, and <c>Service_Out</c> only:
/// payment and departure can include activities outside the service station.
/// </summary>
public static class QueueExcelDataImporter
{
    /// <summary>
    /// Reads the first worksheet in an .xls or .xlsx file.
    /// Required headers (case/underscore/space insensitive): Date, Arrival_Time,
    /// Service_In, Service_Out.
    /// </summary>
    public static QueueExcelImportResult Import(string path)
    {
        // Legacy .xls files can use non-Unicode code pages. Registering once is safe.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        if (!reader.Read())
            throw new InvalidOperationException("The workbook's first worksheet is empty.");

        var columns = ReadHeader(reader);
        int dateColumn = FindRequiredColumn(columns, "Date");
        int arrivalColumn = FindRequiredColumn(columns, "ArrivalTime", "Arrival");
        int serviceInColumn = FindRequiredColumn(columns, "ServiceIn", "ServiceStart");
        int serviceOutColumn = FindRequiredColumn(columns, "ServiceOut", "ServiceEnd");

        var observations = new List<QueueObservation>();
        int sourceRow = 1; // Header is row 1 for user-facing error messages.
        while (reader.Read())
        {
            sourceRow++;
            if (IsBlankRow(reader)) continue;

            if (!TryBuildObservation(
                    reader.GetValue(dateColumn), reader.GetValue(arrivalColumn),
                    reader.GetValue(serviceInColumn), reader.GetValue(serviceOutColumn),
                    out var observation))
            {
                throw new InvalidOperationException(
                    $"Row {sourceRow} has an invalid Date, Arrival_Time, Service_In, or Service_Out value. " +
                    "Each imported row needs complete timestamps.");
            }

            observations.Add(observation);
        }

        // Two interarrival observations are the minimum required for an unbiased
        // sample variance, so at least three complete customer records are needed.
        if (observations.Count < 3)
            throw new InvalidOperationException("At least three complete customer records are required to estimate rates and variability.");

        // J. Data records separate store sessions on different calendar days.
        // Do not treat overnight / multi-day closures as a customer's waiting time
        // between arrivals; that would drastically understate lambda and inflate
        // Var(A). Calculate only consecutive arrivals from the same operating day.
        var interarrivalMinutes = observations
            .GroupBy(x => x.Arrival.Date)
            .SelectMany(day =>
            {
                var dailyArrivals = day.Select(x => x.Arrival).OrderBy(time => time).ToList();
                return dailyArrivals.Zip(dailyArrivals.Skip(1),
                    (previous, current) => (current - previous).TotalMinutes);
            })
            .ToList();
        var serviceMinutes = observations.Select(x => (x.ServiceOut - x.ServiceIn).TotalMinutes).ToList();

        if (interarrivalMinutes.Any(value => value < 0) || serviceMinutes.Any(value => value <= 0))
            throw new InvalidOperationException("Arrival times must be chronological and service durations must be greater than zero.");

        double meanInterarrival = interarrivalMinutes.Average();
        double meanService = serviceMinutes.Average();
        if (meanInterarrival <= 0 || meanService <= 0)
            throw new InvalidOperationException("The imported timestamps produce a zero average interarrival or service time.");

        // λ = 1 / E[A] and μ = 1 / E[S]. Because the spreadsheet records clock
        // times, the unit is minutes: customers/minute and minutes² respectively.
        return new QueueExcelImportResult(
            observations.Count,
            interarrivalMinutes.Count,
            ArrivalRate: 1.0 / meanInterarrival,
            ServiceRate: 1.0 / meanService,
            ArrivalTimeVariance: SampleVariance(interarrivalMinutes),
            ServiceTimeVariance: SampleVariance(serviceMinutes));
    }

    private static Dictionary<string, int> ReadHeader(IExcelDataReader reader)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < reader.FieldCount; column++)
        {
            var name = NormalizeHeader(reader.GetValue(column)?.ToString());
            if (!string.IsNullOrEmpty(name) && !columns.ContainsKey(name)) columns.Add(name, column);
        }
        return columns;
    }

    private static int FindRequiredColumn(IReadOnlyDictionary<string, int> columns, params string[] acceptableNames)
    {
        foreach (var name in acceptableNames)
        {
            if (columns.TryGetValue(NormalizeHeader(name), out int index)) return index;
        }

        throw new InvalidOperationException(
            $"Missing required column '{acceptableNames[0]}'. Expected: Date, Arrival_Time, Service_In, Service_Out.");
    }

    private static string NormalizeHeader(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool IsBlankRow(IExcelDataReader reader)
    {
        for (int column = 0; column < reader.FieldCount; column++)
        {
            if (reader.GetValue(column) is { } value && !string.IsNullOrWhiteSpace(value.ToString())) return false;
        }
        return true;
    }

    private static bool TryBuildObservation(object? dateValue, object? arrivalValue, object? serviceInValue,
        object? serviceOutValue, out QueueObservation observation)
    {
        observation = default;
        if (!TryReadTimestamp(dateValue, arrivalValue, out var arrival) ||
            !TryReadTimestamp(dateValue, serviceInValue, out var serviceIn) ||
            !TryReadTimestamp(dateValue, serviceOutValue, out var serviceOut)) return false;

        // A time-only workbook row can cross midnight. Move a clock time to the
        // following day when it would otherwise precede the arrival/service start.
        while (serviceIn < arrival) serviceIn = serviceIn.AddDays(1);
        while (serviceOut < serviceIn) serviceOut = serviceOut.AddDays(1);

        observation = new QueueObservation(arrival, serviceIn, serviceOut);
        return true;
    }

    private static bool TryReadTimestamp(object? dateValue, object? timeValue, out DateTime timestamp)
    {
        timestamp = default;
        if (TryReadFullDateTime(timeValue, out timestamp)) return true;
        if (!TryReadDate(dateValue, out var date) || !TryReadTime(timeValue, out var time)) return false;

        timestamp = date.Add(time);
        return true;
    }

    private static bool TryReadFullDateTime(object? value, out DateTime dateTime)
    {
        dateTime = default;
        if (value is DateTime parsed && parsed.Year > 1900)
        {
            dateTime = parsed;
            return true;
        }

        if (value is double serial && serial >= 1)
        {
            dateTime = DateTime.FromOADate(serial);
            return true;
        }

        // Text values are deliberately handled as time-of-day below. Treating a
        // value such as "11:12" as a complete DateTime would silently assign it
        // today's date instead of the Date value from the same spreadsheet row.
        return false;
    }

    private static bool TryReadDate(object? value, out DateTime date)
    {
        date = default;
        if (value is DateTime parsed)
        {
            date = parsed.Date;
            return true;
        }
        if (value is double serial && serial >= 1)
        {
            date = DateTime.FromOADate(serial).Date;
            return true;
        }
        return value is string text && DateTime.TryParse(text, CultureInfo.CurrentCulture,
            DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private static bool TryReadTime(object? value, out TimeSpan time)
    {
        time = default;
        switch (value)
        {
            case DateTime dateTime:
                time = dateTime.TimeOfDay;
                return true;
            case TimeSpan timeSpan:
                time = timeSpan;
                return true;
            case double serial when serial >= 0 && serial < 1:
                time = TimeSpan.FromDays(serial);
                return true;
            case string text when TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out var parsedTime):
                time = parsedTime;
                return true;
            case string text when DateTime.TryParse(text, CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsedDateTime):
                time = parsedDateTime.TimeOfDay;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Uses the unbiased sample variance, Σ(xᵢ - x̄)² / (n - 1), to estimate the
    /// population variance required by M/G/1 and G/G/1 from observed data.
    /// </summary>
    private static double SampleVariance(IReadOnlyCollection<double> values)
    {
        if (values.Count < 2) throw new InvalidOperationException("At least two time intervals are required to calculate variance.");
        double mean = values.Average();
        return values.Sum(value => Math.Pow(value - mean, 2)) / (values.Count - 1);
    }

    private readonly record struct QueueObservation(DateTime Arrival, DateTime ServiceIn, DateTime ServiceOut);
}

/// <summary>Queueing inputs estimated from timestamps in minutes.</summary>
public sealed record QueueExcelImportResult(
    int RecordCount,
    int InterarrivalIntervalCount,
    double ArrivalRate,
    double ServiceRate,
    double ArrivalTimeVariance,
    double ServiceTimeVariance);
