# G2-03-03 – Productive Avalonia Theme Implementation

Stand: 12.11.2026. **READY FOR OWNER VISUAL REVIEW.**

Basis: `feature/gui-g2-03`, `d7c59ef69b9bbbd477c5c0e5bdc330d25ec1ff71`, ausschliesslich Worktree `artifacts/worktrees/gui-g2-03`. G2-03-02 wurde laut Eigentümerauftrag am 12.11.2026 ACCEPTED. Die finale visuelle und funktionale Abnahme dieser Implementierung bleibt beim Eigentümer.

## Implementierungsarchitektur und Dateien

Die Anwendung verwendet eine globale `Application.RequestedThemeVariant`: System entspricht `ThemeVariant.Default`, Light und Dark setzen die jeweilige Variante. Avalonia verfolgt im Systemmodus die Betriebssystempräferenz. Ein Wechsel ersetzt weder Views noch Editor-Dokument, Undo-Stack oder Dock-Layout. Bestehende und neue Floating-Fenster erben die Anwendungsvariante.

Die folgenden relativen Produktpfade liegen unter `CryptoWorkBenchAvalonia`:

| Datei(en) | Änderung |
|---|---|
| `Themes/ThemeResources.axaml` | Zentrale Light-/Dark-ThemeDictionaries mit semantischen Cwb-Brushes, vorhandenen Dock-Tokens und DataGrid-Header-/Auswahlressourcen. |
| `Themes/Controls.axaml` | DynamicResource-Styles für Fenster, Menüs, Toolflächen, DataGrid, AvaloniaEdit mit Auswahl/Caret sowie Warnungen und Fehler. Bestehende Fluent-ControlThemes bleiben erhalten. |
| `Themes/Info.axaml`, `Views/InfoView.axaml` | Themeabhängige Markdown-Links, Inline-Code und Codeblöcke. Styles werden im Viewer nach dessen eigenen Styles eingebunden. |
| `Services/Appearance/ThemePreferenceStore.cs` | Unabhängige versionierte JSON-Persistenz, Validierung, Fallback, Schreibfehlerbehandlung. |
| `Services/Appearance/ThemePreferenceService.cs` | UI-Thread-gebundene globale Präferenz; Radio-Menü System/Light/Dark, Aktualisierung bei Menüöffnung, Rückmeldung bei Persistenzfehlern. |
| `App.axaml`, `App.axaml.cs` | Ressourcen laden, Präferenz vor Erzeugung der Shell anwenden; injizierbarer Store für isolierte Tests. |
| `Shell/DockShellWindow.axaml`, `.axaml.cs` | View → Theme und Statusressourcen; Rückmeldung zu Lade-/Schreibfehlern. |
| `Shell/DockDiagnosticsView.cs` | Feste schwarze Texte und helle Warn-/Fehlerflächen durch semantische Klassen ersetzt, einschliesslich Kategorieüberschrift. |
| `Shell/DockOutputView.cs` | Codefont-Fallback; Print 14 DIP, Metadaten 12 DIP. |
| `Views/CryptoScriptEditView.axaml`, `StatusView.axaml`, `MainWindow.axaml`, `KeyMapView.axaml` | Feste Legacy-Flächen und Diagnosetexte tokenisiert. |

Testdateien: `CryptoWorkBenchGuiTest/ThemeTests.cs` enthält 12 neue Fälle zu Persistenz/Fallback und Theme-/Fenster-/Editor-/Layoutinvarianten sowie konkreten Diagnosefarben. `ThemeNativeProbe.cs`, `DockShellNativeProbe.cs` und `eng/gui/native/Program.cs` ergänzen native Review-Modi im bestehenden Testhost mit produktiver App/Shell, echtem CryptoScript und isolierter Konfiguration; Inter-Font wie im Desktop-Einstiegspunkt.

Keine vollständigen Views oder Dock-ControlTemplates dupliziert. Keine Änderungen an WindowDecorations, OwnerMode, Dock-Lifecycle, Splitter-Hitflächen, Core oder MCP; keine Paket-Upgrades. Native Titelleisten und Dateidialoge bleiben vom Betriebssystem verwaltet. Bestehende eigene Dialoge verwenden die globalen Fenster-/Fluent-Ressourcen; eine vollständige native Dialogmatrix wurde nicht geprüft.

## Persistenz und Fallback

Produktiver Pfad: `%LOCALAPPDATA%/CryptoWorkBench/appearance-v1.json`, unabhängig von der Dock-Layoutdatei. Beispiel:

```json
{"schemaVersion":1,"theme":"System"}
```

Akzeptiert werden Schema 1 und die exakten Werte System/Light/Dark. Fehlende Datei: System ohne Schreiben. Ungültige, nicht lesbare oder mehr als 4096 Bytes grosse Datei: System mit Fehlerhinweis; die vorhandene Datei wird beim Laden nicht überschrieben. Explizite Auswahl schreibt zunächst eine eindeutige temporäre Datei im selben Verzeichnis und ersetzt dann das Ziel. Bei Schreibfehler bleibt die gewählte Darstellung aktiv, die Statusrückmeldung meldet die fehlgeschlagene Speicherung. Eine explizite spätere Auswahl darf eine ungültige Datei ersetzen.

Die nativen Review-Dateien liegen ausschliesslich im Nachweisverzeichnis mit Suffixen `.appearance.json` und `.layout.json`. Das echte Benutzerprofil wurde für den Review nicht verändert. Die zuletzt über das native Menü gewählte Systempräferenz steht in `artifacts/g2-03-03/native-reviewed.json.appearance.json`; dieser Prüfbeleg liegt nur lokal vor und ist bewusst nicht versioniert.

## Designabgleich, Kontrast und Abweichungen

Verglichen wurden die akzeptierten [Light-](../../artifacts/g2-03-02/light.png) und [Dark-Prototypen](../../artifacts/g2-03-02/dark.png) mit den unten verlinkten nativen Ansichten. Die semantische Palette entspricht G2-03-01/G2-03-02. Zentrale Flächen: Editor `#FFFFFF/#1E2026`, Panel `#F7F8FA/#24272E`, Header `#E9ECF0/#292D35`, Text `#20242B/#E6E9EF`, Auswahl `#CDE5FF/#264F78`, Auswahltext `#20242B/#FFFFFF`, Akzent `#005FB8/#6CB6FF` (je Light/Dark).

UI-Grundgrösse 13 DIP, Editor/Print/Info-Code 14 DIP, Status/Output-Metadaten 12 DIP. Der produktive Inter-Font und vorhandene Codefont-Fallbacks werden verwendet. Bestehende Layoutabstände und Fluent-Control-Geometrie bleiben erhalten. Die im HTML vorgeschlagenen Tabellen-Paddings, 24/22-px-Codezeilen, zusätzliche Auswahlkontur und vereinfachte Detailansicht werden nicht durch neue Templates erzwungen. Dies ist eine visuelle Abweichung zur Eigentümerprüfung; insbesondere ist die Auswahl nativ durch Flächenfarbe und die bestehenden Fluent-Zustände dargestellt.

Für die identischen semantischen Paare gelten die in G2-03-02 berechneten sRGB-Kontraste:

| Paar | Light | Dark |
|---|---:|---:|
| Primärtext / Panel | 14,65:1 | 12,29:1 |
| Sekundärtext / Header | 5,97:1 | 7,29:1 |
| Auswahltext / Auswahlfläche | 12,05:1 | 8,50:1 |
| Warntext / Warnfläche | 6,15:1 | 8,86:1 |
| Fehlertext / Fehlerfläche | 6,12:1 | 8,19:1 |

Keine Hexwertkorrektur nötig. Die native Prüfung fand jedoch drei Ressourcen-/Prioritätsprobleme, die korrigiert wurden: DataGrid-Header und Auswahl verwendeten zunächst Fluent-Standardfarben; eine lokale schwarze Diagnose-Kategorie überschrieb die Dark-Textfarbe; Markdown setzte eigene lokale Link-/Codefarben. Die finale DataGrid-Tokenbelegung, entfernten lokalen Diagnosewerte und viewer-lokalen Info-Styles beheben diese Fälle. Die Markdown-Priorität ist auch im [Upstream-Fluent-Style](https://raw.githubusercontent.com/whistyun/Markdown.Avalonia/master/Markdown.Avalonia.Tight/StyleCollections/MarkdownStyleFluentTheme.axaml) nachvollziehbar; massgeblicher Nachweis für die installierte Version ist die kompilierte und nativ geprüfte Anwendung. Die Tabelle ist keine vollständige Accessibility-Prüfung aller Fluent-Hover-/Disabled-/Fokuszustände. BorderSubtle bleibt ein dekorativer Trenner.

Weitere sichtbare Unterschiede zum HTML:

- Kein Syntax-Highlighting. Die farbigen Tokens im Prototyp waren ausdrücklich konzeptionell und gehören nicht zu diesem Auftrag.
- Das echte Beispiel benötigt `PARAM hash=Parameters(HASH-SHA256)` und anschliessend `Hash(hash,message)`. Der konzeptionelle direkte Aufruf `Hash(HASH-SHA256,message)` erzeugte im ersten nativen Versuch keine drei Print-Ausgaben. Nur der Review-Beispieltext wurde korrigiert; Core und akzeptierter Prototyp bleiben unverändert.
- Daher vier Einträge in Variables statt drei. Vorhandene Spaltenreihenfolge, Format-/GMAC-/Parameterdetails, `out:`-Präfixe, Anführungszeichen und kleingeschriebene `0x(...)`-Darstellung bleiben erhalten. Drei echte Print-Ergebnisse sind belegt.
- Fluent-Menüs zeigen einen Radio-Punkt statt des konzeptionellen Häkchens. Menü-, Button-, Tab- und Scrollbarformen folgen den bestehenden Controls. Native Titelleisten ersetzen die HTML-Nachbildung.
- Bei 150 % ist der logische Arbeitsbereich kleiner; lange Tabellenwerte werden gekürzt und Details umbrechen. Info hat die vorhandenen Scrollbars; Diagnostics benötigt im kurzen unteren Bereich Scrollen. Keine Dock-Geometrieänderung zur Nachbildung der 100-%-HTML-Aufnahme.

## Automatisierte Validierung

| Prüfung | Debug | Release |
|---|---:|---:|
| Beide Produkt-Solutions: Locked Restore + Build | erfolgreich | erfolgreich |
| GUI, finaler Stand | 1.143 bestanden, 0 Fehler | 1.143 bestanden, 0 Fehler |
| MCP | 176 bestanden; 13 NotExecuted | 176 bestanden; 13 NotExecuted |
| Core | 11.723 bestanden; 27 NotExecuted | 11.723 bestanden; 27 NotExecuted |

Nachweise unter `artifacts/g2-03-03/`: [Testzusammenfassung](../../artifacts/g2-03-03/final-test-summary.json), `build-Debug-r7.log`, `build-Release-r7.log`, `gui-Debug-r7.trx`, `gui-Release-r7.trx` und die gleichnamigen GUI-Logs. Der R7-Lauf enthält die finalen Info-Styles und Diagnose-Farbprüfungen. Danach wurde nur ein überschüssiger Leerzeilenabschluss im nativen Einstiegspunkt entfernt und dieser Bericht aktualisiert.

Die 12 Theme-Fälle prüfen drei Persistenz-Roundtrips, fehlende und sechs ungültige Konfigurationen, Grössenlimit/Schreibfehler sowie den integrierten Wechseltest. Letzterer prüft Light/Dark/System→Default, bereits offene und neue Floating-Fenster, konkrete Editor- und Diagnosefarben, identische Document-/Undo-Objekte und exakt gleichen serialisierten Dockzustand. Die GUI-Tests laufen seriell mit `NUnit.NumberOfTestWorkers=0`; kein Fall deaktiviert.

MCP/Core Debug: `regression/CryptoWorkBench.Mcp.Tests-Debug.trx` und `regression/CryptoScriptUnitTest-Debug.trx`; Release: `CryptoWorkBench.Mcp.Tests-Release-final.trx` und `CryptoScriptUnitTest-Release-final.trx`. Diese vollständigen Regressionen stammen aus dem früheren Implementierungslauf; nachfolgend wurden nur GUI-Styles, GUI-Tests und der native Review-Host korrigiert. Core/MCP wurden nicht verändert. NotExecuted zählt nicht als bestanden; Core CLI zeigt 26 Skips, die TRX enthält zusätzlich einen expliziten nicht ausgeführten Fall. GUI hat keine Skips. Bestehende Compilerwarnungen und Dock-Trace-Lockmeldungen bleiben dokumentiert; die finalen Builds und Tests haben keine Fehler.

Reproduktion: vor jedem Konfigurationswechsel `eng/prism/Build.ps1 -Configuration Debug` beziehungsweise `Release` ausführen (passender Locked Restore), danach `dotnet test CryptoWorkBenchGuiTest -c <Konfiguration> --no-build --no-restore -- NUnit.NumberOfTestWorkers=0`. Die genauen Abschlussbefehle stehen in `native-final-validation-r7.ps1`.

Erhaltene Zwischenfehler werden nicht verdeckt: erster Sandbox-Prism-Fetch/Restore scheiterte; anschliessend wurde der offizielle Pin `358118cd640d9a22ff8cf21c8ad197fa038b7990` erfolgreich vorbereitet. Der erste Theme-Lauf (`theme.trx`) hatte vier Fehler; InvalidDataException-Behandlung und expliziter Ressourcen-Lookup wurden korrigiert. Ein späterer neuer Farbtest verglich White mit einem Hex-String und vergleicht jetzt Color. Debug-Builds mit Release-Restore-Assets scheiterten an der DeveloperTools-Referenz; konfigurationsbezogener Restore behebt das. `gui-Debug-r5.trx` enthält einen intermittierenden bestehenden Headless-Startfehler in `ShortPrintIsVisibleAndSourceButtonNavigates(911,512)` bei Dispatcher/RenderLoop vor dem Testkörper. R6 und R7 bestehen vollständig; dieser Startfehler wird nicht als behoben behauptet.

## Native Windows-Nachweise

Die Bilder stammen aus der produktiven Avalonia-App/Shell im vorhandenen nativen `G1ShellProbe`-Host, nicht aus HTML oder Headless-Rendering. Die Review-Modi laden echte Scriptdaten und isolieren die Konfiguration. Nach Zustimmung zur Desktop-Nutzung wurden Menüwechsel, Info, Diagnostics, Floating und Layout-Reset über die native Oberfläche bedient. Sämtliche Testfenster sind wieder geschlossen.

| Nachweis | Datei |
|---|---|
| Light: Editor, ausgewählter Digest, Output, Status und aktive/inaktive Tabs | [native-light-150.jpg](../../artifacts/g2-03-03/native-light-150.jpg) |
| Dark: derselbe erfolgreiche Beispielzustand | [native-dark-150.jpg](../../artifacts/g2-03-03/native-dark-150.jpg) |
| Dark Info mit korrigierten Links/Inline-Code | [native-info-dark-150.jpg](../../artifacts/g2-03-03/native-info-dark-150.jpg) |
| Floating Info in Dark | [native-floating-dark-150.jpg](../../artifacts/g2-03-03/native-floating-dark-150.jpg) |
| Dasselbe offene Floating-Fenster nach Wechsel zu Light | [native-floating-light-150.jpg](../../artifacts/g2-03-03/native-floating-light-150.jpg) |
| Theme-Menü Light / Dark | [Light](../../artifacts/g2-03-03/native-theme-menu-light.jpg), [Dark](../../artifacts/g2-03-03/native-theme-menu-dark.jpg) |
| Reale Diagnose nach `Print(missing)`, Textbereich gescrollt | [Light](../../artifacts/g2-03-03/native-diagnostics-light-150.jpg), [Dark](../../artifacts/g2-03-03/native-diagnostics-dark-150.jpg) |
| Systemauswahl: Rückkehr zur aktuellen hellen Systemdarstellung | [native-system-150.jpg](../../artifacts/g2-03-03/native-system-150.jpg) |
| Gemessene Skalierung, Clientgrösse und drei Print-Ergebnisse | `artifacts/g2-03-03/native-reviewed.json` – Prüfbeleg nur lokal vorhanden, bewusst nicht versioniert. |

Der Host meldet `RenderScaling=1.5`, Clientgrösse 1280 × 693,333 DIP (1920 × 1040 physische Clientpixel, zuzüglich nativer Fensterrahmen). Die Windows-Aufnahmen werden vom Capture-Werkzeug auf logische Pixel normalisiert; Hauptfensterbilder haben ungefähr 1283 × 725 Pixel und sind JPEG. Sie sind **keine** pixelidentischen 1920-×-1080-Aufnahmen des 100-%-Prototyps. Mauszeiger samt blauem Aufnahmeindikator gehören zum Nachweiswerkzeug, nicht zum Produktdesign. Floating-Aufnahmen besitzen einen anderen Ausschnitt.

Der Wechsel ohne Neustart und die Darstellung eines bereits geöffneten Info-Floating-Fensters wurden beobachtet. Nach dessen Schliessen und Reset to Default Layout waren Script, Auswahl und Print-Ausgaben weiter vorhanden. System wurde nativ gewählt und als `System` gespeichert; die Anwendung wechselte zur aktuellen hellen Systemdarstellung. Ein tatsächlicher Wechsel der Windows-Farbpräferenz während laufendem Systemmodus wurde nicht durchgeführt.

## Offene Punkte für die Eigentümerabnahme

1. Visuelle und funktionale Abnahme der nativen Light-/Dark-Ansichten und der genannten Abweichungen gegenüber HTML, insbesondere kompakte Controls, Auswahlkontur und 150-%-Platzbedarf.
2. **100 % und 125 % DPI nicht verifiziert.** Die verfügbare Umgebung lieferte 150 %; globale Windows-Skalierung wurde nicht geändert. Auch ein Monitorwechsel mit unterschiedlichen DPI bleibt offen.
3. Natives Betriebssystem-Farbsignal im laufenden Systemmodus sowie vollständige Dialog-/Popup-/Legacy- und High-Contrast-Prüfung bleiben offen. Automatisiert belegt ist die Default-Zuordnung und Theme-Vererbung, nicht ein künstlich behaupteter OS-Wechsel.

Technische Prüfungen erfolgreich, native Light-/Dark-Nachweise vorhanden: **READY FOR OWNER VISUAL REVIEW**. Keine Eigentümerfreigabe vorweggenommen, kein Commit, Push oder Merge und keine automatische Freigabe weiterer Arbeitspakete.
