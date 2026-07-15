using System.Text;

namespace Game.Client.Diagnostics;

public static class CrashReportWriter
{
    public static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YjsE",
        "Logs",
        "crash-latest.log");

    public static void TryWrite(Exception exception, string? stateName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            var path = CrashLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var report = new StringBuilder(2048)
                .AppendLine("YjsE crash report")
                .Append("CapturedUtc: ").AppendLine(DateTimeOffset.UtcNow.ToString("O"))
                .Append("State: ").AppendLine(stateName ?? "unknown")
                .Append("Runtime: ").AppendLine(Environment.Version.ToString())
                .Append("OS: ").AppendLine(Environment.OSVersion.ToString())
                .AppendLine()
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(path, report);
        }
        catch
        {
            // Crash reporting must never hide the original failure.
        }
    }
}
