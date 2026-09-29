namespace SP621E.Core.Logging;

using System.IO;

/// <summary>
/// Structured logger with an in-app event surface (UI log tab) and a rotating
/// file sink under %LocalAppData%\SP621EMusicReactive\logs\. Keeps the last
/// <see cref="MaxFiles"/> files of <see cref="MaxBytesPerFile"/> each.
/// </summary>
public static class AppLog
{
    public static event Action<string>? EntryWritten;

    public const int MaxBytesPerFile = 512 * 1024;
    public const int MaxFiles = 3;

    private static readonly object Gate = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SP621EMusicReactive", "logs");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        var line = $"[{DateTimeOffset.Now:O}] {level} {message}";
        if (ex is not null)
            line += $" | {ex.GetType().Name}: {ex.Message}";

        EntryWritten?.Invoke(line);
        WriteFile(line);
    }

    private static void WriteFile(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                var file = Path.Combine(LogDirectory, "app.log");
                if (File.Exists(file) && new FileInfo(file).Length > MaxBytesPerFile)
                    Rotate(file);

                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void Rotate(string file)
    {
        var baseName = Path.GetFileNameWithoutExtension(file);
        var ext = Path.GetExtension(file);
        var dir = Path.GetDirectoryName(file)!;

        // app.log -> app.1.log -> app.2.log -> app.3.log (newest kept last index).
        var oldest = Path.Combine(dir, $"{baseName}.{MaxFiles}{ext}");
        if (File.Exists(oldest))
            File.Delete(oldest);
        for (var i = MaxFiles - 1; i >= 1; i--)
        {
            var from = Path.Combine(dir, $"{baseName}.{i}{ext}");
            if (File.Exists(from))
                File.Move(from, Path.Combine(dir, $"{baseName}.{i + 1}{ext}"), overwrite: true);
        }
        File.Move(file, Path.Combine(dir, $"{baseName}.1{ext}"), overwrite: true);
    }
}