# Design-Council – UI-Redesign „Retro-Futuristic“

Dieses Dokument hält den gesamten Redesign-Prozess nachvollziehbar fest:
- wer bewertet,
- was gefordert wurde,
- was umgesetzt ist,
- welche Bewertungen vergeben wurden.

> **Status:** Zwischenstand (WIP). Entwurf v1 ist umgesetzt und kompiliert. **Runde 1 (Bewertung von v1) steht noch aus.**
> Ziel: Jedes Council-Mitglied bewertet das UI mit **mindestens 8/10**.

---

## 1. Zusammensetzung des Councils

Der Council besteht ausschließlich aus Claude-Modellen. Jedes Mitglied hat eine eigene Rolle.

| Mitglied | Rolle |
|---|---|
| Opus 5.5 | Visual Design & Retro-Futurismus (Palette, Typografie, Konsistenz, Hierarchie) |
| Sonnet 5.5 | UX & Informationsarchitektur (Klarheit, Vereinfachung, Abläufe) |
| Haiku 5.5 | Erstnutzer-Sicht, Lesbarkeit & Augenschonung (WCAG-Kontrast, Status nie nur über Farbe) |
| Fable 5.2 | WinForms-Umsetzung & technische Qualität (Theming, DPI, Fallstricke) |

**Abbruchkriterium:** Alle vier vergeben ≥ 8/10 für die Designqualität.

---

## 2. Runde 0 – Ausgangslage (altes UI)

| Mitglied | Bewertung | Kernaussage |
|---|---|---|
| Opus 5.5 | **3/10** | Kein visuelles System, hart codierte Farben, umgekehrte Hierarchie, Courier-Log |
| Sonnet 5.5 | **3/10** | Formular-Stapel: Einstellungen oben, Tabelle unten; doppelte Repo-Ansicht; unlesbare Statuszeile |
| Haiku 5.5 | **4/10** | Kein Dark Mode, DarkOrange-Warntext nur ca. 2,3:1 Kontrast, Status nur über Zeilenfarbe |
| Fable 5.2 | **4/10** | Layout solide, aber keine Theme-Architektur, unskalierte Pixelwerte, nicht themebare Controls |

### Gemeinsame Anforderungen aus Runde 0

**Informationsarchitektur (Sonnet)**
- Kopfleiste mit „Jetzt prüfen“ als einziger gefüllter Hauptaktion und einem Zahnrad für die Einstellungen.
- Vier Kennzahlen-Kacheln statt der „|“-Statuszeile.
- Repo-Tabelle als Hauptfläche, Hinzufügen direkt darüber, Fehler inline statt MessageBox.
- Separates Repo-Listen-Fenster entfällt. Entfernen direkt in der Tabelle (Entf, Kontextmenü, Bestätigung).
- Einstellungen in einem Seitenpanel. Aktivitäts-Log unten, einklappbar.
- Fortschritt der Prüfung sichtbar. Tray-Verhalten unverändert.
- Keine Funktion darf verloren gehen. Alle Texte DE und EN.

**Visual Design (Opus)**
- Zentrale Farb-Tokens.
  - Dunkel: kühles Graphit, z. B. `#12151C`, `#181C25`, `#E4E7EC`.
  - Hell: warmes Papier, z. B. `#F2F0EB`, `#FAF9F6`, `#1C2028`.
- Genau eine Akzentfarbe „Signal-Petrol“ (Dunkel `#4CC2C4`, Hell `#0E7C86`), getrennt von den Statusfarben.
- Schriften:
  - Segoe UI für den Text,
  - Bahnschrift (nur für Titel und Kennzahlen, in Versalien),
  - Cascadia Mono bzw. Consolas für Log, Tags und Zeiten.
- Retro-Futurismus als „Instrumententafel“:
  - Countdown `T–hh:mm`,
  - 2-px-Signallinie als Fortschrittsbalken,
  - Eckmarken nur an einer Stelle.
- Verboten:
  - Scanlines, Neon-Glow, Pixel- oder Orbitron-Schriften,
  - reines Schwarz oder Weiß,
  - mehr als eine Akzentfarbe,
  - ein „halber“ Dark Mode.

**Lesbarkeit (Haiku)**
- WCAG AA (Text ≥ 4,5:1, Rahmen ≥ 3:1) in beiden Modi.
- Status immer als Symbol und Text, nie nur über Farbe.
- Ursache sichtbar machen statt nur „nicht erreichbar“.
- Keine Animationen, kein Blinken. Mindestschriftgröße 9 pt.

**Technik (Fable)**
- Eigenes Theme-System, weil es in .NET 8 kein `SetColorMode` gibt.
- Erkennung über `AppsUseLightTheme` und Reaktion auf `WM_SETTINGCHANGE` („ImmersiveColorSet“). Hoher Kontrast nutzt die Systemfarben.
- Dunkle Titelleiste per `DwmSetWindowAttribute` (20/19, unter Windows 11 zusätzlich 34/35/36).
- Dunkle Scrollbars per `SetWindowTheme("DarkMode_Explorer")`.
- Eigene Controls statt GroupBox, Button, ComboBox und NumericUpDown.
- DataGridView statt ListView. Themed ToolStrip-Renderer.
- Alle Pixelwerte DPI-skaliert. Kein `WS_EX_COMPOSITED`. MessageBox und FolderBrowserDialog bleiben System.

---

## 3. Entwurf v1 – umgesetzt

| Datei | Inhalt |
|---|---|
| `Theme.cs` | Paletten Dark/Light/HighContrast, Modus System/Hell/Dunkel, Systemerkennung, Schriftwahl mit Fallbacks, Titelleiste und Scrollbars (native), flackerfreies Umschalten |
| `UiControls.cs` | `FlatButton` (Primary/Secondary/Ghost), `InputBox` mit Platzhalter, `SegmentedControl`, `Stepper`, `CardPanel`, `HeaderBar` mit Signal-/Fortschrittslinie, `StatTile` und `TilesPanel` (4 Spalten oder 2×2), `ThemedGrid` (Statussymbol + Text, Auswahl-Akzentbalken, Leerzustand), `NoticeLine`, `ThemedMenuRenderer`, `ThemedDialog` |
| `MainForm.cs` | Neuer Aufbau (siehe unten); Prüf-, Rate-Limit- und Tray-Logik unverändert übernommen |
| `AppSettings.cs` | Neue Einstellungen `Theme` (`system`/`light`/`dark`) und `LogExpanded` |
| `Lang.cs` | Neue Texte DE/EN für Kacheln, Einstellungen, Hinweise, Leerzustand |
| `RepoListForm.cs` | **entfernt**; Funktion jetzt in der Haupttabelle |

**Aufbau des Hauptfensters**
1. **Kopfleiste:** Logo-Klammer, Titel in Versalien und Version. Rechts „Einstellungen“ (Ghost) und „Jetzt prüfen F5“ (Primary). Unten eine 2-px-Signallinie, die während der Prüfung als Fortschritt läuft.
2. **Kacheln:**
   - STATUS: Bereit / x von n / Pausiert, darunter Token-Hinweis.
   - NÄCHSTE PRÜFUNG: Countdown `T–hh:mm`, darunter Uhrzeit und letzte Prüfung.
   - ERREICHBAR: x/y, darunter Problemanzahl.
   - API-ANFRAGEN ÜBRIG: Restwert mit Messbalken und Reset-Zeit.
3. **01 // Repositories:**
   - Eingabe mit Platzhalter, „Hinzufügen“ und Inline-Hinweiszeile.
   - Tabelle mit den Spalten Repository, Status (Form-Symbol + Text), Release, Pre-Release, Letzte Prüfung, Details.
   - Sortierbar, Mehrfachauswahl.
   - Entfernen per Entf oder Kontextmenü, mit gestalteter Bestätigung.
4. **02 // Aktivität:** Einklappbar. Eingeklappt sieht man die letzte Logzeile. Aufgeklappt zeigt eine Konsole die Stufen WARN und ERROR in den Statusfarben. Daneben „Log-Datei öffnen“.
5. **Einstellungen:** Overlay-Panel von rechts (Esc schließt) mit:
   - Erscheinungsbild (System/Hell/Dunkel),
   - Sprache,
   - Download-Ordner,
   - Prüfintervall (Stepper mit effektivem Intervall),
   - GitHub-Token mit Inline-Prüfergebnis,
   - Tray-Hinweis.

**Statussymbole (formunterscheidbar, auch ohne Farbe):**

| Status | Symbol |
|---|---|
| Erreichbar | ● gefüllter Kreis |
| Keine Releases | ○ Ring |
| Nicht erreichbar | ✕ |
| API-Limit | ‖ Pause |
| Prüft | Ring mit Punkt |
| Wartet | gepunkteter Ring |

---

## 4. Prüfung bisher

- **Kompilierung:** fehlerfrei (Kompilierprüfung unter Linux gegen die WinForms-Referenzassemblies).
- **Vorab-Sichtprüfung:**
  - Das echte Programm lief unter Wine mit Ersatzschriften: Inter statt Segoe UI, Barlow Semi Condensed statt Bahnschrift.
  - Wine zeigt weder die dunkle Titelleiste noch dunkle Scrollbars noch Segoe-Symbole. Diese Punkte **müssen unter echtem Windows geprüft werden**.
- Dabei gefundene und bereits behobene Punkte:
  - Controls waren beim Start teils hell (Feldinitialisierung vor der Theme-Auflösung).
  - Datumsformat zu lang.
  - Platzhalter verschwand bei Fokus.
  - Senkrechte Zelltrennlinien in der Tabelle.
  - Horizontaler Scrollbalken bei geöffneten Einstellungen.
  - Leere Hinweiszeilen erzeugten Lücken.
  - Fehlendes Pfeilsymbol (jetzt Vektor).
  - Einstellungen quetschten die Kacheln (jetzt Overlay).
  - Countdown während der Prüfung sinnlos (jetzt „läuft“ bzw. „Prüfe...“).
  - Tabelle bei Mindestgröße verdeckt (Log-Höhe jetzt relativ).

---

## 5. Nächste Schritte

1. Unter **Windows** bauen (`build.bat`) und Screenshots der Szenen anfertigen:
   - Hell und Dunkel,
   - Einstellungen offen,
   - Prüfung läuft,
   - API-Limit,
   - Leerzustand,
   - Bestätigungsdialog,
   - Tray-Menü,
   - Mindestgröße.
2. **Runde 1:** Alle vier Council-Mitglieder bewerten Screenshots und Code (1–10) mit konkreten Mängeln.
3. Iterieren, bis alle ≥ 8/10 vergeben. Jede Runde hier dokumentieren.
4. Danach README (Screenshots und Bedienung) aktualisieren.
