using System.Reflection;
using JSimulator.Core.Reporting;

namespace JSimulator.App.ViewModels;

public class AboutViewModel : ViewModelBase
{
    public string MM1Description => ReportNarrative.Description("M/M/1");
    public string MM1Formula => ReportNarrative.Formula("M/M/1");

    public string MG1Description => ReportNarrative.Description("M/G/1");
    public string MG1Formula => ReportNarrative.Formula("M/G/1");

    public string GG1Description => ReportNarrative.Description("G/G/1");
    public string GG1Formula => ReportNarrative.Formula("G/G/1");

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
