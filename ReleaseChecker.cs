using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitHubReleaseFetcher;

public enum RepoState { Pending, Checking, Ok, NoReleases, NotReachable, RateLimited }

public sealed class RepoStatus
{
    public string Repo { get; init; } = "";
    public RepoState State { get; set; } = RepoState.Pending;
    public string StableTag { get; set; } = "";
    public string PreTag { get; set; } = "";
    public DateTime? LastCheck { get; set; }
    public string Details { get; set; } = "";
}

public sealed class RepoCheckResult
{
    public RepoState State { get; set; }
    public string StableTag { get; set; } = "";
    public string PreTag { get; set; } = "";
    public string Details { get; set; } = "";
    public int NewFiles { get; set; }
    public int FailedFiles { get; set; }
    /// <summary>Set when the GitHub API limit was hit; the round must stop until this time.</summary>
    public DateTime? RateLimitedUntilUtc { get; set; }
}

internal sealed class AssetInfo
{
    public string Name = "";
    public long Size;
    public string BrowserUrl = "";
    public string ApiUrl = "";
}

internal sealed class ReleaseInfo
{
    public string Tag = "";
    public bool Prerelease;
    public DateTime Published;
    public string ZipUrl = "";
    public string TarUrl = "";
    public List<AssetInfo> Assets = new();
}

/// <summary>Talks to the GitHub REST API and downloads release files.</summary>
public sealed class ReleaseChecker : IDisposable
{
    public const string ReleasesFolder = "Releases";
    public const string PreReleasesFolder = "Pre-Releases";

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromHours(2);

    private readonly HttpClient _http;

    // last known API rate limit values (from response headers)
    public int? RateRemaining { get; private set; }
    public int? RateLimit { get; private set; }
    public DateTime? RateResetUtc { get; private set; }

    public ReleaseChecker()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,              // Authorization header is not forwarded on redirects
            MaxAutomaticRedirections = 10,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        string ver = typeof(ReleaseChecker).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"GitHubReleaseFetcher/{ver}");
    }

    public void Dispose() => _http.Dispose();

    private static void AddApiHeaders(HttpRequestMessage req, string token, bool octet = false)
    {
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(octet ? "application/octet-stream" : "application/vnd.github+json"));
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void ReadRateHeaders(HttpResponseMessage r)
    {
        if (TryHeaderInt(r, "X-RateLimit-Remaining", out long rem)) RateRemaining = (int)rem;
        if (TryHeaderInt(r, "X-RateLimit-Limit", out long lim)) RateLimit = (int)lim;
        if (TryHeaderInt(r, "X-RateLimit-Reset", out long reset))
            RateResetUtc = DateTimeOffset.FromUnixTimeSeconds(reset).UtcDateTime;
    }

    private static bool TryHeaderInt(HttpResponseMessage r, string name, out long value)
    {
        value = 0;
        return r.Headers.TryGetValues(name, out var vals) &&
               long.TryParse(vals.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Returns a time until which requests must pause, or null if the response is not a rate-limit response.</summary>
    private DateTime? GetRateLimitPause(HttpResponseMessage r)
    {
        bool is429 = (int)r.StatusCode == 429;
        bool is403 = r.StatusCode == HttpStatusCode.Forbidden;
        if (!is429 && !is403) return null;

        DateTime now = DateTime.UtcNow;
        var ra = r.Headers.RetryAfter;
        if (ra != null)
        {
            if (ra.Delta.HasValue) return now + ra.Delta.Value;
            if (ra.Date.HasValue) return ra.Date.Value.UtcDateTime;
        }

        if (RateRemaining == 0)
            return RateResetUtc.HasValue && RateResetUtc.Value > now ? RateResetUtc.Value : now.AddHours(1);

        // 429 without any information: be careful, wait one hour
        if (is429) return now.AddHours(1);
        return null; // normal 403 (access denied)
    }

    /// <summary>Quick token validation via /rate_limit (does not count against the limit).</summary>
    public async Task<(bool ok, int? limit, string error)> ValidateTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ApiTimeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/rate_limit");
            AddApiHeaders(req, token);
            using var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
            ReadRateHeaders(resp);
            if (resp.IsSuccessStatusCode) return (true, RateLimit, "");
            return (false, null, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Checks one repository and downloads the newest release and the newest pre-release
    /// (assets + source code zip/tar.gz). Never throws (except on cancellation).
    /// </summary>
    public async Task<RepoCheckResult> CheckRepoAsync(string repo, string downloadRoot, string token, CancellationToken ct)
    {
        var result = new RepoCheckResult();

        // Pre-emptive protection: do not run into the limit at all.
        if (RateRemaining.HasValue && RateRemaining.Value <= 1 && RateResetUtc.HasValue && RateResetUtc.Value > DateTime.UtcNow)
        {
            result.State = RepoState.RateLimited;
            result.RateLimitedUntilUtc = RateResetUtc.Value.AddSeconds(30);
            result.Details = Lang.F("msg_rate_wait", result.RateLimitedUntilUtc.Value.ToLocalTime().ToString("g"));
            Logger.Warn(Lang.F("log_repo_ratelimited", repo, result.RateLimitedUntilUtc.Value.ToLocalTime().ToString("g")));
            return result;
        }

        Logger.Info(Lang.F("log_checking_repo", repo));

        List<ReleaseInfo> releases;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ApiTimeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases?per_page=30");
            AddApiHeaders(req, token);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
            ReadRateHeaders(resp);

            if (!resp.IsSuccessStatusCode)
            {
                var pause = GetRateLimitPause(resp);
                if (pause.HasValue)
                {
                    result.State = RepoState.RateLimited;
                    result.RateLimitedUntilUtc = pause.Value.AddSeconds(30);
                    result.Details = Lang.F("msg_rate_wait", result.RateLimitedUntilUtc.Value.ToLocalTime().ToString("g"));
                    Logger.Warn(Lang.F("log_repo_ratelimited", repo, result.RateLimitedUntilUtc.Value.ToLocalTime().ToString("g")));
                    return result;
                }

                result.State = RepoState.NotReachable;
                string key = resp.StatusCode switch
                {
                    HttpStatusCode.NotFound => "log_repo_notfound",
                    HttpStatusCode.Unauthorized => "log_repo_unauthorized",
                    HttpStatusCode.Forbidden => "log_repo_forbidden",
                    _ => "log_repo_http"
                };
                string msg = Lang.F(key, repo, (int)resp.StatusCode);
                result.Details = msg.Replace($"[{repo}] ", "");
                Logger.Warn(msg);
                return result;
            }

            string json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            releases = ParseReleases(json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            result.State = RepoState.NotReachable;
            string msg = Lang.F("log_repo_timeout", repo);
            result.Details = msg.Replace($"[{repo}] ", "");
            Logger.Warn(msg);
            return result;
        }
        catch (JsonException)
        {
            result.State = RepoState.NotReachable;
            string msg = Lang.F("log_repo_badjson", repo);
            result.Details = msg.Replace($"[{repo}] ", "");
            Logger.Warn(msg);
            return result;
        }
        catch (Exception ex)
        {
            result.State = RepoState.NotReachable;
            string msg = Lang.F("log_repo_neterror", repo, ex.GetBaseException().Message);
            result.Details = msg.Replace($"[{repo}] ", "");
            Logger.Warn(msg);
            return result;
        }

        ReleaseInfo? stable = releases.Where(r => !r.Prerelease).OrderByDescending(r => r.Published).FirstOrDefault();
        ReleaseInfo? pre = releases.Where(r => r.Prerelease).OrderByDescending(r => r.Published).FirstOrDefault();

        if (stable == null && pre == null)
        {
            result.State = RepoState.NoReleases;
            result.Details = Lang.Get("details_uptodate");
            Logger.Info(Lang.F("log_repo_noreleases", repo));
            return result;
        }

        result.State = RepoState.Ok;
        result.StableTag = stable?.Tag ?? "";
        result.PreTag = pre?.Tag ?? "";
        Logger.Info(Lang.F("log_repo_ok", repo, stable?.Tag ?? "-", pre?.Tag ?? "-"));

        string repoDir = Path.Combine(downloadRoot, repo.Replace('/', '_'));
        try
        {
            if (stable != null) await DownloadReleaseAsync(repo, repoDir, ReleasesFolder, stable, token, result, ct).ConfigureAwait(false);
            if (pre != null) await DownloadReleaseAsync(repo, repoDir, PreReleasesFolder, pre, token, result, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // e.g. folder could not be created - repo itself was reachable
            result.FailedFiles++;
            Logger.Error(Lang.F("log_dl_fail", repo, repoDir, ex.GetBaseException().Message));
        }

        var parts = new List<string>();
        if (result.NewFiles > 0) parts.Add(Lang.F("details_files", result.NewFiles));
        if (result.FailedFiles > 0) parts.Add(Lang.F("details_failed", result.FailedFiles));
        if (parts.Count == 0) parts.Add(Lang.Get("details_uptodate"));
        result.Details = string.Join(", ", parts);
        return result;
    }

    private static List<ReleaseInfo> ParseReleases(string json)
    {
        var list = new List<ReleaseInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("not an array");

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (Bool(el, "draft")) continue;
            string tag = Str(el, "tag_name");
            if (tag.Length == 0) continue;

            var r = new ReleaseInfo
            {
                Tag = tag,
                Prerelease = Bool(el, "prerelease"),
                ZipUrl = Str(el, "zipball_url"),
                TarUrl = Str(el, "tarball_url"),
            };
            string pub = Str(el, "published_at");
            if (pub.Length == 0) pub = Str(el, "created_at");
            r.Published = DateTime.TryParse(pub, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : DateTime.MinValue;

            if (el.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    if (a.ValueKind != JsonValueKind.Object) continue;
                    var ai = new AssetInfo
                    {
                        Name = Str(a, "name"),
                        BrowserUrl = Str(a, "browser_download_url"),
                        ApiUrl = Str(a, "url"),
                        Size = a.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number && sz.TryGetInt64(out long l) ? l : -1
                    };
                    if (ai.Name.Length > 0 && (ai.BrowserUrl.Length > 0 || ai.ApiUrl.Length > 0)) r.Assets.Add(ai);
                }
            }
            list.Add(r);
        }
        return list;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        string s = new string(chars).Trim().TrimEnd('.', ' ');
        if (s.Length == 0 || s == "." || s == "..") s = "_";
        // reserved Windows device names
        string upper = s.ToUpperInvariant();
        string baseName = upper.Contains('.') ? upper.Substring(0, upper.IndexOf('.')) : upper;
        string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                              "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (reserved.Contains(baseName)) s = "_" + s;
        return s;
    }

    private async Task DownloadReleaseAsync(string repo, string repoDir, string kindFolder, ReleaseInfo rel,
        string token, RepoCheckResult result, CancellationToken ct)
    {
        string tagSafe = SanitizeName(rel.Tag);
        string relDir = Path.Combine(repoDir, kindFolder, tagSafe);
        bool isNew = !Directory.Exists(relDir);
        Directory.CreateDirectory(relDir);
        if (isNew)
            Logger.Info(Lang.F("log_new_release", repo, Lang.Get(rel.Prerelease ? "kind_prerelease" : "kind_release"), rel.Tag));

        bool useToken = !string.IsNullOrEmpty(token);

        // assets
        foreach (var a in rel.Assets)
        {
            ct.ThrowIfCancellationRequested();
            bool viaApi = (useToken && a.ApiUrl.Length > 0) || a.BrowserUrl.Length == 0;
            string url = viaApi ? a.ApiUrl : a.BrowserUrl;
            string target = Path.Combine(relDir, SanitizeName(a.Name));
            await DownloadFileAsync(repo, url, target, a.Size, viaApi, token, result, ct).ConfigureAwait(false);
        }

        // source code
        string repoName = SanitizeName(repo.Split('/')[1]);
        if (rel.ZipUrl.Length > 0)
            await DownloadFileAsync(repo, rel.ZipUrl, Path.Combine(relDir, $"{repoName}-{tagSafe}-source.zip"), -1, true, token, result, ct).ConfigureAwait(false);
        if (rel.TarUrl.Length > 0)
            await DownloadFileAsync(repo, rel.TarUrl, Path.Combine(relDir, $"{repoName}-{tagSafe}-source.tar.gz"), -1, true, token, result, ct).ConfigureAwait(false);
    }

    private async Task DownloadFileAsync(string repo, string url, string target, long expectedSize, bool viaApi,
        string token, RepoCheckResult result, CancellationToken ct)
    {
        string fileName = Path.GetFileName(target);

        if (File.Exists(target))
        {
            long len = new FileInfo(target).Length;
            if (expectedSize < 0 || len == expectedSize) return; // already present
            // differs from GitHub: keep old file under a new name (nothing is deleted)
            string old = target + $".old-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Move(target, old);
                Logger.Warn(Lang.F("log_dl_renamed_old", repo, Path.GetFileName(old)));
            }
            catch (Exception ex)
            {
                result.FailedFiles++;
                Logger.Error(Lang.F("log_dl_fail", repo, fileName, ex.Message));
                return;
            }
        }

        string tmp = target + ".part";
        Logger.Info(Lang.F("log_dl_start", repo, fileName));
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DownloadTimeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (viaApi) AddApiHeaders(req, token, octet: true);
            else if (!string.IsNullOrEmpty(token) && IsGitHubHost(url))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            if (viaApi && resp.RequestMessage?.RequestUri?.Host == "api.github.com") ReadRateHeaders(resp);
            if (!resp.IsSuccessStatusCode)
            {
                result.FailedFiles++;
                Logger.Error(Lang.F("log_dl_fail", repo, fileName, $"HTTP {(int)resp.StatusCode}"));
                return;
            }

            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await resp.Content.CopyToAsync(fs, cts.Token).ConfigureAwait(false);
            }

            long got = new FileInfo(tmp).Length;
            if (expectedSize >= 0 && got != expectedSize)
            {
                TryDelete(tmp);
                result.FailedFiles++;
                Logger.Error(Lang.F("log_dl_fail", repo, fileName, Lang.Get("log_dl_size_mismatch")));
                return;
            }

            File.Move(tmp, target, true);
            result.NewFiles++;
            Logger.Info(Lang.F("log_dl_ok", repo, fileName, FormatSize(got)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            TryDelete(tmp);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tmp);
            result.FailedFiles++;
            string reason = ex is OperationCanceledException ? "timeout" : ex.GetBaseException().Message;
            Logger.Error(Lang.F("log_dl_fail", repo, fileName, reason));
        }
    }

    private static bool IsGitHubHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
         u.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase));

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }

    public static string FormatSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v.ToString(i == 0 ? "0" : "0.0", CultureInfo.InvariantCulture)} {u[i]}";
    }
}
