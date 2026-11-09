using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GitHubReleaseFetcher;

/// <summary>
/// Persistent settings stored in %APPDATA%\GitHubReleaseFetcher\settings.json.
/// The GitHub token is encrypted with Windows DPAPI (current user only).
/// </summary>
public sealed class AppSettings
{
    public string DownloadFolder { get; set; } = "";
    public List<string> Repositories { get; set; } = new();
    public int CheckIntervalHours { get; set; } = 3;
    public string Language { get; set; } = "en";
    public string EncryptedToken { get; set; } = "";
    public DateTime? LastCheckUtc { get; set; }

    public const int MinIntervalHours = 1;
    public const int MaxIntervalHours = 168;

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitHubReleaseFetcher");

    private static string FilePath => Path.Combine(AppDataDir, "settings.json");

    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>Error text if the settings file could not be read (for logging after startup).</summary>
    public static string? LoadError { get; private set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (s != null)
                {
                    s.Normalize();
                    return s;
                }
            }
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
            try
            {
                // keep the broken file instead of overwriting it
                File.Copy(FilePath, Path.Combine(AppDataDir, $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json"), true);
            }
            catch { /* ignore */ }
        }

        var d = new AppSettings();
        d.Normalize();
        return d;
    }

    private void Normalize()
    {
        Repositories ??= new List<string>();
        Repositories = Repositories
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (CheckIntervalHours < MinIntervalHours) CheckIntervalHours = 3;
        if (CheckIntervalHours > MaxIntervalHours) CheckIntervalHours = MaxIntervalHours;
        if (Language != "de" && Language != "en") Language = "en";
        DownloadFolder ??= "";
        EncryptedToken ??= "";
    }

    public void Save()
    {
        lock (FileLock)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                string json = JsonSerializer.Serialize(this, JsonOpts);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex)
            {
                Logger.Error(Lang.F("log_settings_save_failed", ex.Message));
            }
        }
    }

    public string GetToken()
    {
        if (string.IsNullOrEmpty(EncryptedToken)) return "";
        try
        {
            byte[] data = ProtectedData.Unprotect(Convert.FromBase64String(EncryptedToken), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return "";
        }
    }

    public void SetToken(string token)
    {
        token = (token ?? "").Trim();
        EncryptedToken = token.Length == 0
            ? ""
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));
    }

    public bool HasToken => GetToken().Length > 0;
}
