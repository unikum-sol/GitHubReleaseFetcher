using System.Text.RegularExpressions;

namespace GitHubReleaseFetcher;

/// <summary>
/// Accepts:
///   owner/repo
///   https://github.com/owner/repo  (also without https://, with www., with .git)
///   https://github.com/owner/repo/releases
///   https://github.com/owner/repo/releases/latest
///   https://github.com/owner/repo/releases/tag/v1.2.3
/// Returns the normalized form "owner/repo".
/// </summary>
public static class RepoParser
{
    private static readonly Regex OwnerRx = new(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.Compiled);
    private static readonly Regex NameRx = new(@"^[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);
    private static readonly Regex GitHubPrefixRx = new(@"^(?:https?://)?(?:www\.)?github\.com/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParse(string? input, out string repo)
    {
        repo = "";
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = input.Trim();

        // cut query string / fragment
        int cut = s.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) s = s.Substring(0, cut);

        bool isGitHubUrl = GitHubPrefixRx.IsMatch(s);
        if (isGitHubUrl)
        {
            s = GitHubPrefixRx.Replace(s, "");
        }
        else if (s.Contains("://") || s.Split('/')[0].Contains('.'))
        {
            // some other host (e.g. gitlab.com/...) -> not supported
            return false;
        }

        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        // short form must be exactly owner/repo
        if (!isGitHubUrl && parts.Length != 2) return false;

        string owner = parts[0];
        string name = parts[1];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);

        if (name == "." || name == "..") return false;
        if (!OwnerRx.IsMatch(owner) || !NameRx.IsMatch(name)) return false;

        repo = owner + "/" + name;
        return true;
    }

    /// <summary>Splits a text field into several entries (newline, space, comma, semicolon).</summary>
    public static IEnumerable<string> SplitInput(string text) =>
        (text ?? "").Split(new[] { '\r', '\n', ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
}
