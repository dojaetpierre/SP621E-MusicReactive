namespace SP621E.Bluetooth;

using System.IO;

/// <summary>
/// Saves GATT diagnostic reports under %LocalAppData%\SP621EMusicReactive\diagnostics\
/// so a human operator can return the file verbatim.
/// </summary>
public static class DiagnosticReportStore
{
    public static string DefaultDirectory
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "SP621EMusicReactive", "diagnostics");
        }
    }

    public static string NextReportPath()
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(DefaultDirectory, $"gatt-report-{stamp}.txt");
    }

    public static void Save(GattReport report, string? explicitPath = null)
    {
        var path = explicitPath ?? NextReportPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, report.ToString());
    }

    /// <summary>Saves and returns the path the report was written to.</summary>
    public static string SaveAndReturnPath(GattReport report, string? explicitPath = null)
    {
        var path = explicitPath ?? NextReportPath();
        Save(report, path);
        return path;
    }
}