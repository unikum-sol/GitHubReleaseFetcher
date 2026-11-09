using System.Globalization;
using System.Text;

namespace GitHubReleaseFetcher;

/// <summary>
/// Thread-safe log with timestamps in %APPDATA%\GitHubReleaseFetcher\log.txt.
/// Entries older than <see cref="RetentionDays"/> days are removed automatically.
/// </summary>
public static class Logger
{
    public const int RetentionDays = 31;
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly object Lock = new();

    public static string LogFile => Path.Combine(AppSettings.AppDataDir, "log.txt");

    /// <summary>Raised for every written line (may be raised on any thread).</summary>
    public static event Action<string>? LineWritten;

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        msg = (msg ?? "").Replace("\r", " ").Replace("\n", " ");
        string line = $"{DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture)} [{level}] {msg}";
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.AppDataDir);
                File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { /* logging must never crash the app */ }
        }

        try { LineWritten?.Invoke(line); } catch { /* ignore */ }
    }

    /// <summary>Removes log lines older than the retention period.</summary>
    public static void Cleanup()
    {
        lock (Lock)
        {
            try
            {
                if (!File.Exists(LogFile)) return;
                DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
                var lines = File.ReadAllLines(LogFile, Encoding.UTF8);
                var keep = new List<string>(lines.Length);
                foreach (var l in lines)
                {
                    if (l.Length >= TimeFormat.Length &&
                        DateTime.TryParseExact(l.Substring(0, TimeFormat.Length), TimeFormat,
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts))
                    {
                        if (ts >= cutoff) keep.Add(l);
                    }
                    else
                    {
                        keep.Add(l); // unknown format: keep
                    }
                }

                if (keep.Count != lines.Length)
                {
                    string tmp = LogFile + ".tmp";
                    File.WriteAllLines(tmp, keep, Encoding.UTF8);
                    File.Move(tmp, LogFile, true);
                }
            }
            catch { /* ignore */ }
        }
    }

    public static void EnsureExists()
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.AppDataDir);
                if (!File.Exists(LogFile)) File.WriteAllText(LogFile, "", Encoding.UTF8);
            }
            catch { /* ignore */ }
        }
    }
}
