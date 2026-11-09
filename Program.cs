namespace GitHubReleaseFetcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "GitHubReleaseFetcher_SingleInstance_7F3A2C", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "GitHub Release Fetcher is already running (see system tray).\n" +
                "GitHub Release Fetcher läuft bereits (siehe Infobereich/Tray).",
                "GitHub Release Fetcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
