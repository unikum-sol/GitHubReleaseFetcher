namespace GitHubReleaseFetcher;

/// <summary>Simple EN/DE string table.</summary>
public static class Lang
{
    public static string Current { get; set; } = "en";

    private static readonly Dictionary<string, (string En, string De)> T = new()
    {
        // ---- window / groups
        ["app_title"] = ("GitHub Release Fetcher", "GitHub Release Fetcher"),
        ["grp_settings"] = ("Settings", "Einstellungen"),
        ["grp_repos"] = ("Repositories", "Repositories"),
        ["grp_status"] = ("Status", "Status"),
        ["grp_details"] = ("Detailed status / activity", "Detaillierter Status / Aktivität"),

        // ---- settings
        ["lbl_folder"] = ("Download folder:", "Download-Ordner:"),
        ["btn_browse"] = ("Browse...", "Auswählen..."),
        ["btn_open_folder"] = ("Open", "Öffnen"),
        ["lbl_token"] = ("GitHub token:", "GitHub-Token:"),
        ["btn_save_token"] = ("Save token", "Token speichern"),
        ["btn_remove_token"] = ("Remove token", "Token entfernen"),
        ["token_set"] = ("Token: saved (encrypted)", "Token: gespeichert (verschlüsselt)"),
        ["token_not_set"] = ("Token: not set (limit 60 requests/h)", "Token: nicht gesetzt (Limit 60 Anfragen/h)"),
        ["lbl_interval"] = ("Check interval (hours):", "Prüfintervall (Stunden):"),
        ["lbl_language"] = ("Language:", "Sprache:"),
        ["interval_ok"] = ("Effective interval: {0} h", "Effektives Intervall: {0} h"),
        ["interval_adjusted"] = ("Effective interval: {0} h (raised because of GitHub API limit for {1} repositories)",
                                 "Effektives Intervall: {0} h (erhöht wegen GitHub-API-Limit bei {1} Repositories)"),

        // ---- repos
        ["lbl_repo_input"] = ("Repository (owner/repo, URL or release page link):", "Repository (owner/repo, URL oder Release-Seiten-Link):"),
        ["btn_add"] = ("Add", "Hinzufügen"),
        ["btn_show_list"] = ("Show repository list", "Repository-Liste anzeigen"),
        ["btn_check_now"] = ("Check now", "Jetzt prüfen"),
        ["btn_open_log"] = ("Open log", "Log öffnen"),

        // ---- list columns
        ["col_repo"] = ("Repository", "Repository"),
        ["col_status"] = ("Status", "Status"),
        ["col_release"] = ("Latest release", "Neuester Release"),
        ["col_prerelease"] = ("Latest pre-release", "Neuester Pre-Release"),
        ["col_lastcheck"] = ("Last check", "Letzte Prüfung"),
        ["col_details"] = ("Details", "Details"),

        // ---- states
        ["st_pending"] = ("Waiting", "Wartet"),
        ["st_checking"] = ("Checking...", "Prüfe..."),
        ["st_ok"] = ("Reachable", "Erreichbar"),
        ["st_noreleases"] = ("Reachable (no releases)", "Erreichbar (keine Releases)"),
        ["st_notreachable"] = ("Not reachable", "Nicht erreichbar"),
        ["st_ratelimited"] = ("Postponed (API limit)", "Verschoben (API-Limit)"),

        // ---- status line
        ["status_idle"] = ("Idle", "Bereit"),
        ["status_checking"] = ("Checking repositories...", "Prüfe Repositories..."),
        ["status_line"] = ("State: {0}  |  Last check: {1}  |  Next check: {2}  |  Reachable: {3}/{4}  |  API requests left: {5}",
                           "Zustand: {0}  |  Letzte Prüfung: {1}  |  Nächste Prüfung: {2}  |  Erreichbar: {3}/{4}  |  API-Anfragen übrig: {5}"),
        ["never"] = ("never", "nie"),
        ["unknown"] = ("unknown", "unbekannt"),

        // ---- tray
        ["tray_state"] = ("State: {0}", "Zustand: {0}"),
        ["tray_last"] = ("Last check: {0}", "Letzte Prüfung: {0}"),
        ["tray_next"] = ("Next check: {0}", "Nächste Prüfung: {0}"),
        ["tray_repos"] = ("Reachable: {0} / {1}  (not reachable: {2})", "Erreichbar: {0} / {1}  (nicht erreichbar: {2})"),
        ["tray_rate"] = ("API requests left: {0}", "API-Anfragen übrig: {0}"),
        ["tray_open"] = ("Open window", "Fenster öffnen"),
        ["tray_check"] = ("Check now", "Jetzt prüfen"),
        ["tray_exit"] = ("Exit", "Beenden"),
        ["tray_balloon"] = ("Still running in the system tray. Exit via right-click on the tray icon.",
                            "Läuft weiter im Infobereich. Beenden über Rechtsklick auf das Tray-Symbol."),

        // ---- repo list window
        ["list_title"] = ("Repository list", "Repository-Liste"),
        ["btn_remove"] = ("Remove selected", "Ausgewählte entfernen"),
        ["btn_close"] = ("Close", "Schließen"),
        ["list_count"] = ("{0} repositories", "{0} Repositories"),
        ["confirm_remove"] = ("Remove {0} repository/repositories from the list?\n(Already downloaded files are NOT deleted.)",
                              "{0} Repository/Repositories aus der Liste entfernen?\n(Bereits heruntergeladene Dateien werden NICHT gelöscht.)"),

        // ---- messages
        ["msg_invalid_repo"] = ("Invalid input (ignored): {0}", "Ungültige Eingabe (ignoriert): {0}"),
        ["msg_duplicate_repo"] = ("Already in list: {0}", "Bereits in der Liste: {0}"),
        ["msg_no_folder"] = ("Please select a download folder first.", "Bitte zuerst einen Download-Ordner auswählen."),
        ["msg_rate_wait"] = ("GitHub API limit reached. Next check possible at {0}.", "GitHub-API-Limit erreicht. Nächste Prüfung möglich ab {0}."),
        ["msg_already_checking"] = ("A check is already running.", "Es läuft bereits eine Prüfung."),
        ["msg_token_check_ok"] = ("Token saved. Token is valid (limit {0} requests/h).", "Token gespeichert. Token ist gültig (Limit {0} Anfragen/h)."),
        ["msg_token_check_fail"] = ("Token saved, but GitHub rejected it: {0}", "Token gespeichert, aber GitHub hat ihn abgelehnt: {0}"),
        ["msg_token_check_unknown"] = ("Token saved. Validation not possible: {0}", "Token gespeichert. Prüfung nicht möglich: {0}"),
        ["msg_token_removed"] = ("Token removed.", "Token entfernt."),

        // ---- log messages
        ["log_start"] = ("Program started (version {0}).", "Programm gestartet (Version {0})."),
        ["log_exit"] = ("Program exited via tray menu.", "Programm über Tray-Menü beendet."),
        ["log_settings_save_failed"] = ("Could not save settings: {0}", "Einstellungen konnten nicht gespeichert werden: {0}"),
        ["log_settings_load_failed"] = ("Settings file was unreadable (copy kept), defaults used: {0}", "Einstellungsdatei war nicht lesbar (Kopie gesichert), Standardwerte verwendet: {0}"),
        ["log_folder_set"] = ("Download folder set: {0}", "Download-Ordner gesetzt: {0}"),
        ["log_folder_error"] = ("Download folder not usable: {0} ({1})", "Download-Ordner nicht nutzbar: {0} ({1})"),
        ["log_interval_set"] = ("Check interval set to {0} h.", "Prüfintervall auf {0} h gesetzt."),
        ["log_repo_added"] = ("Repository added: {0}", "Repository hinzugefügt: {0}"),
        ["log_repo_removed"] = ("Repository removed from list: {0}", "Repository aus Liste entfernt: {0}"),
        ["log_round_start"] = ("Check started ({0} repositories, token: {1}).", "Prüfung gestartet ({0} Repositories, Token: {1})."),
        ["log_round_end"] = ("Check finished: {0} reachable, {1} not reachable, {2} postponed, {3} new files.",
                             "Prüfung beendet: {0} erreichbar, {1} nicht erreichbar, {2} verschoben, {3} neue Dateien."),
        ["log_round_skipped_nofolder"] = ("Check skipped: no usable download folder.", "Prüfung übersprungen: kein nutzbarer Download-Ordner."),
        ["log_next"] = ("Next check: {0}", "Nächste Prüfung: {0}"),
        ["log_checking_repo"] = ("[{0}] Checking...", "[{0}] Prüfe..."),
        ["log_repo_notfound"] = ("[{0}] Not reachable: not found (HTTP 404) - wrong name, deleted or private without token.",
                                 "[{0}] Nicht erreichbar: nicht gefunden (HTTP 404) - falscher Name, gelöscht oder privat ohne Token."),
        ["log_repo_unauthorized"] = ("[{0}] Not reachable: token invalid or expired (HTTP 401).", "[{0}] Nicht erreichbar: Token ungültig oder abgelaufen (HTTP 401)."),
        ["log_repo_forbidden"] = ("[{0}] Not reachable: access denied (HTTP 403).", "[{0}] Nicht erreichbar: Zugriff verweigert (HTTP 403)."),
        ["log_repo_http"] = ("[{0}] Not reachable: HTTP {1}.", "[{0}] Nicht erreichbar: HTTP {1}."),
        ["log_repo_neterror"] = ("[{0}] Not reachable: {1}", "[{0}] Nicht erreichbar: {1}"),
        ["log_repo_timeout"] = ("[{0}] Not reachable: timeout.", "[{0}] Nicht erreichbar: Zeitüberschreitung."),
        ["log_repo_badjson"] = ("[{0}] Not reachable: unexpected answer from GitHub.", "[{0}] Nicht erreichbar: unerwartete Antwort von GitHub."),
        ["log_repo_ratelimited"] = ("[{0}] GitHub API limit reached - remaining repositories postponed until {1}.",
                                    "[{0}] GitHub-API-Limit erreicht - restliche Repositories verschoben bis {1}."),
        ["log_repo_noreleases"] = ("[{0}] Reachable, no published releases.", "[{0}] Erreichbar, keine veröffentlichten Releases."),
        ["log_repo_ok"] = ("[{0}] Reachable. Release: {1} | Pre-release: {2}", "[{0}] Erreichbar. Release: {1} | Pre-Release: {2}"),
        ["log_new_release"] = ("[{0}] New {1} found: {2}", "[{0}] Neuer {1} gefunden: {2}"),
        ["kind_release"] = ("release", "Release"),
        ["kind_prerelease"] = ("pre-release", "Pre-Release"),
        ["log_dl_start"] = ("[{0}] Downloading: {1}", "[{0}] Lade herunter: {1}"),
        ["log_dl_ok"] = ("[{0}] Downloaded: {1} ({2})", "[{0}] Heruntergeladen: {1} ({2})"),
        ["log_dl_fail"] = ("[{0}] Download failed: {1} - {2} (will be retried at next check)",
                           "[{0}] Download fehlgeschlagen: {1} - {2} (wird bei nächster Prüfung erneut versucht)"),
        ["log_dl_size_mismatch"] = ("size mismatch", "Größe stimmt nicht"),
        ["log_dl_renamed_old"] = ("[{0}] Existing file differs from GitHub, kept as: {1}", "[{0}] Vorhandene Datei weicht von GitHub ab, gesichert als: {1}"),
        ["log_dl_cleanup_part"] = ("incomplete download", "unvollständiger Download"),
        ["details_files"] = ("{0} new file(s)", "{0} neue Datei(en)"),
        ["details_failed"] = ("{0} download(s) failed", "{0} Download(s) fehlgeschlagen"),
        ["details_uptodate"] = ("up to date", "aktuell"),
    };

    public static string Get(string key) =>
        T.TryGetValue(key, out var v) ? (Current == "de" ? v.De : v.En) : key;

    public static string F(string key, params object?[] args)
    {
        try { return string.Format(Get(key), args); }
        catch (FormatException) { return Get(key); }
    }
}
