# Offene Aufgaben

Stand: 16.11.2026, abends (nach der Windows-Sitzung). Hintergrund und Details stehen in [`HANDOVER.md`](HANDOVER.md) und [`DESIGN_COUNCIL.md`](DESIGN_COUNCIL.md).

## 1. Vorbereitung (Windows-PC)
- [x] .NET SDK installiert (.NET 10 SDK baut das `net8.0-windows`-Projekt)
- [x] Arbeitsordner `A:\ClaudeCode\GitHubReleaseFetcher` ist ein Git-Checkout von `main`
- [x] Claude Code im Repo-Ordner gestartet

## 2. Bauen und Download-Fix prüfen
- [x] Build (`dotnet publish` wie in `build.bat`) läuft fehlerfrei und ohne Warnungen durch
- [x] `publish\GitHubReleaseFetcher.exe` startet
- [x] **Fix für HTTP 415:** live bestätigt, alle `…-source.zip/.tar.gz` werden heruntergeladen

## 3. Neues UI unter Windows prüfen
- [x] Schriften werden erkannt (Bahnschrift, Cascadia Mono, Segoe MDL2 Assets)
- [x] Titelleiste im Dark Mode dunkel (unter Windows 10 mit „Akzentfarbe auf Titelleisten“ bleibt das aktive Fenster akzentfarben – Systemverhalten)
- [x] Scrollbars von Tabelle, Einstellungen und Log im Dark Mode dunkel
- [x] Log-Konsole: Farben für WARN und ERROR, scrollt ans Ende
- [x] Platzhaltertext sichtbar
- [x] Theme-Umschaltung System/Hell/Dunkel funktioniert sofort
- [ ] Windows-Einstellung Hell/Dunkel live übernommen (Modus „System“) – per Code-Review abgesichert, nicht live getestet
- [ ] Hoher Kontrast – nur Code-Review
- [ ] DPI-Skalierung 125 % und 150 % – nur Code-Review (100 % getestet)
- [x] Mindestgröße 900×620: Tabelle bleibt sichtbar (mind. 3 Zeilen)
- [ ] Tray-Menü fotografieren – nicht möglich gewesen (kein Zugriff auf die Taskleiste); nutzt denselben Renderer wie das getestete Kontextmenü
- [x] Funktionen wie vorher: Repos hinzufügen (mehrere, URLs), entfernen (Entf, Kontextmenü), Jetzt prüfen (Button, F5), Log ein-/ausklappen, Theme, Sprache, Esc
- [ ] Noch selbst kurz durchklicken: Ordner wählen, Token speichern/prüfen/entfernen, Tray „Jetzt prüfen“ und „Beenden“
- [x] Gefundene Fehler behoben (siehe DESIGN_COUNCIL.md, Abschnitt 4)

## 4. Design-Council
- [x] Windows-Screenshots (Dunkel, Hell, Einstellungen, Prüfung läuft, Leerzustand, Bestätigungsdialog, Mindestgröße) – API-Limit und Tray-Menü nicht herbeiführbar
- [x] Runde 1: 7 / 7 / 6 / 7
- [x] Runde 2: 8 / 8 / 8 / 7, Runde 3 (Fable): 8 → **alle vier ≥ 8/10**
- [x] Alle Runden in `DESIGN_COUNCIL.md` festgehalten

## 5. Optional (Anregungen für 9/10)
Siehe `DESIGN_COUNCIL.md`, Abschnitt 8 – z. B. Tray-Symbol mit Fehlermarkierung, Einstellungs-Panel ohne Scrollen, Log-Zeilenumbruch schaltbar, gethemte Tooltips.

## 6. Abschluss
- [x] `README.md` (EN/DE) an das neue UI angepasst, mit Screenshots
- [x] WIP-Hinweis in `DESIGN_COUNCIL.md` entfernt
- [ ] Versionsnummer in `GitHubReleaseFetcher.csproj` erhöhen (derzeit `1.0.0`; der Nutzer entscheidet)
- [ ] Neuen Release auf GitHub erstellen (Nutzer)
- [ ] Optional: alten Git-Tag `GitHubReleaseFetcher` löschen, falls er noch existiert (Nutzer)
