# Übergabe – Stand 16.11.2026

Diese Datei fasst zusammen, was in der Cloud-Sitzung gemacht wurde und wo die Arbeit weitergeht. Den Design-Prozess im Detail (Council, Bewertungen, Anforderungen) beschreibt [`DESIGN_COUNCIL.md`](DESIGN_COUNCIL.md). Die vollständige Aufgabenliste steht in [`TODO.md`](TODO.md).

---

## 1. Erledigt

### 1.1 Fehler HTTP 415 bei Downloads – behoben (Commit `d08a08c`)
- **Symptom:** Laut Log schlugen alle Quellcode-Downloads (`…-source.zip`, `…-source.tar.gz`) mit `HTTP 415` fehl. Die Abfrage der Release-Liste funktionierte.
- **Ursache:** Die Quellcode-Archive wurden über die API mit dem Header `Accept: application/octet-stream` angefragt, und dieser Header wird mit 415 abgelehnt.
- **Änderung in `ReleaseChecker.cs`:**
  - Quellcode-Archive werden jetzt mit `Accept: application/vnd.github+json` angefragt.
  - Asset-Downloads über die API behalten `application/octet-stream`, weil dieser Endpunkt den Header braucht.
  - Technisch kam dazu nur der Parameter `octet` in `DownloadFileAsync`.
- **Offen:** Gegen GitHub live getestet ist die Änderung noch nicht, weil die Cloud-Umgebung GitHub-Anfragen blockiert. **Bitte nach dem Bauen im Log prüfen, ob die `…-source.zip/.tar.gz` jetzt heruntergeladen werden.**
- Warum der Fehler anfangs nicht auftrat, ist nicht geklärt.

### 1.2 Repository-Aufräumen
- Alles liegt auf `main`. Den zusätzlichen Branch hat der Nutzer gelöscht.
- Den fehlerhaften Release „GitHub Release Fetcher V1.0“ hat der Nutzer gelöscht. Der Git-Tag `GitHubReleaseFetcher` existiert eventuell noch.

### 1.3 UI-Redesign – Entwurf v1 (Commits `a60d4f0`, `8c32f92`, **WIP**)
- Design-Council aus Opus 5.5, Sonnet 5.5, Haiku 5.5 und Fable 5.2 eingerichtet.
- **Runde 0:** Bewertung des alten UI: 3, 3, 4 und 4 von 10. Die Anforderungen stehen in `DESIGN_COUNCIL.md`.
- Entwurf v1 vollständig umgesetzt:

  | Datei | Änderung |
  |---|---|
  | `Theme.cs` (neu) | Paletten Dunkel/Hell/Hoher Kontrast, Modus System/Hell/Dunkel, Schriftwahl, dunkle Titelleiste und Scrollbars |
  | `UiControls.cs` (neu) | Alle eigenen, themebaren Controls |
  | `MainForm.cs` | Neuer Aufbau: Kopfleiste, Kacheln, Repo-Tabelle, einklappbares Log, Einstellungs-Overlay. **Prüf-, Rate-Limit- und Tray-Logik unverändert.** |
  | `AppSettings.cs` | Neue Einstellungen `Theme` und `LogExpanded` |
  | `Lang.cs` | Neue Texte DE/EN; nicht mehr benutzte Texte des alten Layouts entfernt |
  | `RepoListForm.cs` | Entfernt; Entfernen jetzt direkt in der Tabelle (Entf, Kontextmenü) |

- Hinweise (Repo hinzufügen, Token-Prüfung, kein Ordner, API-Limit) erscheinen jetzt im Fenster statt als MessageBox. Die Bestätigung beim Entfernen ist ein gestalteter Dialog.
- Neue Tastenkürzel:
  - **F5:** Jetzt prüfen
  - **Esc:** Einstellungen schließen
  - **Entf:** Ausgewählte Repos entfernen
  - **Enter:** Im Repo-Feld hinzufügen, im Token-Feld speichern
- Beim ersten Start ohne Download-Ordner öffnen sich die Einstellungen automatisch.

---

## 2. Wie bisher geprüft wurde

- **Kompilierung:** fehlerfrei, geprüft unter Linux gegen die WinForms-Referenzassemblies. Ein echtes `build.bat` unter Windows ist noch nicht gelaufen.
- **Sichtprüfung:** Das echte Programm lief unter Linux in Wine. Dafür gab es eine eigene Demo-Startdatei mit Beispieldaten, die nur in der Cloud existierte und **nicht** im Repo ist.
- Wine ersetzt die Schriften (Inter statt Segoe UI, Barlow statt Bahnschrift) und zeigt **nicht**:
  - die dunkle Titelleiste,
  - dunkle Scrollbars,
  - die Segoe-Symbole in den Buttons.
- **Runde 1 des Councils hat noch nicht stattgefunden.**

---

## 3. Unter Windows unbedingt prüfen (bekannte Unsicherheiten)

1. **Schriften:** Werden „Segoe UI Variable Text“, „Segoe UI Semibold“, „Bahnschrift SemiBold“, „Cascadia Mono“ und „Segoe Fluent Icons“ / „Segoe MDL2 Assets“ erkannt? Die Auswahl steht in `Theme.Pick`, mit Fallbacks.
2. **Titelleiste:** Wird sie im Dark Mode dunkel? Wechselt sie beim Live-Umschalten sofort? Zuständig ist `Theme.ApplyTitleBar`.
3. **Scrollbars:** Werden die Scrollbars von Tabelle und Log-Konsole im Dark Mode dunkel? Zuständig ist `SetWindowTheme("DarkMode_Explorer")`. Ob das bei den inneren Scrollbars der DataGridView greift, ist unsicher.
4. **Log-Konsole** (`ConsoleBox`): Erscheinen die farbigen Zeilen korrekt? Scrollt sie ans Ende?
5. **Platzhaltertext** in den Eingabefeldern (`PlainTextBox`): Flackert er? Wird er korrekt gezeichnet?
6. **DPI:** Passt die Darstellung bei 100 %, 125 % und 150 % Skalierung (`Ui.S`, App läuft `SystemAware`)?
7. **Live-Umschaltung:** Hell und Dunkel in den Einstellungen und über die Windows-Einstellung. Dazu den hohen Kontrast.
8. **Tray-Menü:** Ist es gestaltet und sind die Infozeilen lesbar?
9. **Mindestgröße** 900×620: Bleibt die Tabelle sichtbar?
10. **Download-Fix:** Werden die Quellcode-Archive geladen (siehe 1.1)?

---

## 4. Nächste Schritte

1. Unter Windows `git pull origin main`, dann `build.bat` ausführen und `publish\GitHubReleaseFetcher.exe` starten.
2. Die Punkte aus Abschnitt 3 prüfen und Fehler beheben.
3. Screenshots dieser Szenen anfertigen:
   - Dunkel und Hell,
   - Einstellungen offen,
   - Prüfung läuft,
   - API-Limit,
   - Leerzustand,
   - Bestätigungsdialog,
   - Tray-Menü,
   - Mindestgröße.
4. **Council Runde 1:** Alle vier Mitglieder bewerten Screenshots und Code (1–10) mit konkreten Mängeln. Iterieren, bis jedes ≥ 8/10 gibt. Jede Runde in `DESIGN_COUNCIL.md` festhalten.
5. Danach `README.md` aktualisieren. Es beschreibt noch das alte UI, z. B. das separate Repo-Listen-Fenster und die Sprachauswahl per Liste.
6. Den WIP-Hinweis in `DESIGN_COUNCIL.md` entfernen, sobald der Council zugestimmt hat.

**Startnachricht für die neue Sitzung (Vorschlag):**
> Lies docs/HANDOVER.md und docs/DESIGN_COUNCIL.md. Bau mit build.bat, prüf die Punkte aus Abschnitt 3 der Übergabe, mach echte Windows-Screenshots der Szenen und führ Runde 1 des Design-Councils durch (Opus 5.5, Sonnet 5.5, Haiku 5.5, Fable 5.2), bis jedes Mitglied ≥ 8/10 gibt.
