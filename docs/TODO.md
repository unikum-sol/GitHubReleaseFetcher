# Offene Aufgaben

Stand: 16.11.2026. Hintergrund und Details stehen in [`HANDOVER.md`](HANDOVER.md) und [`DESIGN_COUNCIL.md`](DESIGN_COUNCIL.md).

## 1. Vorbereitung (Windows-PC)
- [ ] .NET 8 SDK installiert (für `build.bat`)
- [ ] `git pull origin main` in `A:\ClaudeCode\GitHubReleaseFetcher`
- [ ] Claude Code im Repo-Ordner starten (Desktop-App oder `claude remote-control`)

## 2. Bauen und Download-Fix prüfen
- [ ] `build.bat` läuft fehlerfrei durch
- [ ] `publish\GitHubReleaseFetcher.exe` startet
- [ ] **Fix für HTTP 415:** Nach einer Prüfung stehen im Log Einträge „Heruntergeladen: …-source.zip / …-source.tar.gz“ und kein „HTTP 415“ mehr

## 3. Neues UI unter Windows prüfen
- [ ] Schriften werden erkannt: Segoe UI (Variable), Bahnschrift, Cascadia Mono/Consolas, Segoe Fluent Icons bzw. MDL2 Assets für die Button-Symbole
- [ ] Titelleiste wird im Dark Mode dunkel, auch beim Live-Umschalten
- [ ] Scrollbars von Tabelle und Log sind im Dark Mode dunkel
- [ ] Log-Konsole: Farben für WARN und ERROR, scrollt automatisch ans Ende
- [ ] Platzhaltertext in den Eingabefeldern ist sichtbar und flackert nicht
- [ ] Theme-Umschaltung System/Hell/Dunkel in den Einstellungen funktioniert sofort
- [ ] Windows-Einstellung Hell/Dunkel wird live übernommen (Modus „System“)
- [ ] Hoher Kontrast (Windows-Einstellung) ist lesbar
- [ ] DPI-Skalierung 100 %, 125 % und 150 %: nichts abgeschnitten oder verschoben
- [ ] Mindestgröße 900×620: Tabelle bleibt sichtbar
- [ ] Tray-Menü ist gestaltet und die Infozeilen sind lesbar
- [ ] Funktionen wie vorher:
  - [ ] Ordner wählen und öffnen
  - [ ] Token speichern, prüfen und entfernen
  - [ ] Intervall
  - [ ] Sprache
  - [ ] Repos hinzufügen (auch mehrere und URLs)
  - [ ] Repos entfernen (Entf und Kontextmenü)
  - [ ] Jetzt prüfen (Button, F5, Tray)
  - [ ] Log-Datei öffnen
  - [ ] Schließen (X) schickt das Programm in den Tray, Beenden geht nur über das Tray-Menü
- [ ] Gefundene Fehler beheben

## 4. Design-Council (Hauptaufgabe)
- [ ] Windows-Screenshots dieser Szenen:
  - Dunkel
  - Hell
  - Einstellungen offen
  - Prüfung läuft
  - API-Limit
  - Leerzustand
  - Bestätigungsdialog
  - Tray-Menü
  - Mindestgröße
- [ ] **Runde 1:** Opus 5.5, Sonnet 5.5, Haiku 5.5 und Fable 5.2 bewerten Screenshots und Code (1–10)
- [ ] Mängel umsetzen und weitere Runden durchführen, bis **alle vier ≥ 8/10** geben
- [ ] Jede Runde mit Bewertungen und Änderungen in `DESIGN_COUNCIL.md` festhalten

## 5. Anregungen aus Runde 0, noch nicht umgesetzt
Diese Punkte entscheidet der Council in Runde 1, oder der Nutzer.
- [ ] Getrennte Statuswerte statt nur „Nicht erreichbar“: nicht gefunden (404), Zugriff verweigert (403), Token ungültig (401), Netzwerk/Timeout. Die Ursache steht bisher nur in der Spalte „Details“ (Haiku).
- [ ] Tray-Symbol zeigt den Zustand an, z. B. eine Markierung bei Fehlern (Haiku)
- [ ] DPI-Modus `PerMonitorV2` statt `SystemAware`, damit die App auf Monitoren mit unterschiedlicher Skalierung scharf bleibt (Fable)
- [ ] Tooltips für reine Symbol-Buttons (Log ein-/ausklappen, Einstellungen schließen)
- [ ] Zeilenumbruch im Log ein- und ausschaltbar machen; bisher ist er immer an (Haiku)
- [ ] Hinweis beim zweiten Programmstart (`Program.cs`) bleibt eine System-MessageBox mit festem zweisprachigem Text

## 6. Abschluss
- [ ] `README.md` (EN/DE) an das neue UI anpassen. Es beschreibt noch das separate Repo-Listen-Fenster, die Sprach-Auswahlliste und die alte Statuszeile. Neu dazu: Theme-Auswahl, Tastenkürzel, Screenshots.
- [ ] WIP-Hinweis in `DESIGN_COUNCIL.md` entfernen
- [ ] Versionsnummer in `GitHubReleaseFetcher.csproj` erhöhen (derzeit `1.0.0`; der Nutzer entscheidet über die neue Nummer)
- [ ] Neuen Release auf GitHub erstellen (Nutzer)
- [ ] Optional: alten Git-Tag `GitHubReleaseFetcher` löschen, falls er noch existiert (Nutzer)
