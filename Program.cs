namespace GitHubReleaseFetcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // first, so that even the "already running" message gets visual styles and DPI awareness
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, "GitHubReleaseFetcher_SingleInstance_7F3A2C", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "GitHub Release Fetcher is already running (see system tray).\n" +
                "GitHub Release Fetcher läuft bereits (siehe Infobereich/Tray).",
                "GitHub Release Fetcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new MainForm());
    }
}
