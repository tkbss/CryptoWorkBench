# G2-02-R1 – Dock-Header und Output-Korrekturen

**Status: READY FOR VISUAL REVIEW.** Eigentümerabnahme offen; Prüfgrenzen und intermittierender Headless-Startfehler siehe unten.

## Ausgangszustand und Umfang

GUI-Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. G2-02-Bericht gelesen. Die bereits vorhandenen uncommitteten G1/G2-Änderungen bleiben erhalten. Ausgangsstatus, getrackter Diff sowie Shell-/GUI-Testdateien wurden vor R1 unter `artifacts/g2-02-r1/baseline/` gesichert.

R1 ändert vier produktive GUI-Dateien, Testabdeckung und native Prüfwerkzeuge. Kein CryptoScript-Core, keine MCP-Verträge, kein Theme-System, keine neuen Run-/Dokumentfunktionen, keine Änderung am Cross-Window-Redock-Lifecycle. Kein Commit, Push, Merge oder PR.

## Ursache und Framework-Lösung

Die Dock-Fluent-Struktur besteht aus `ToolDockControl → ToolChromeControl → ToolControl → ToolTabStrip`. `ToolChromeControl` zeichnet die zusätzliche Titel-/Gripzeile einschließlich des aktiven View-Namens und eigener Aktionen; `ToolTabStrip` zeichnet separat die Tabs. Die G2-02-Positionierung der Tabs nach oben entfernte daher den Framework-Header nicht. Zusätzlich enthielt `DockVariablesView` eine eigene Überschrift „Variables“. Beim Editor existiert nur die Dokument-Tab-Leiste.

Die vorhandene Framework-Einstellung **`IToolDock.GripMode = GripMode.Hidden`** unterdrückt die zusätzliche Chrome-Zeile. Sie wird in `EditorDockFactory.InitDockable` als reine Präsentationsvorgabe auf alle Tool-Gruppen angewendet: Default, Restore, Splits und Floating. Die Framework-Templates für Chrome, der Dock-Manager und seine Lifecycle-/Admission-Verträge bleiben unverändert.

Dock blendet normalerweise bei nur einem Tool dessen Tab aus, weil der Grip den Titel übernimmt. Der Shell-Style hält deshalb den `ItemsPresenter` des `ToolTabStrip:singleitem` sichtbar. Damit bleibt auch nach Schließen/Floaten eines Nachbartabs genau eine erreichbare Tab-Leiste übrig.

Die vorhandene freie Fläche `PART_BorderFill` innerhalb der Tab-Leiste erhält `DockProperties.IsDragArea=True`. Ihr DataContext ist die Tool-Gruppe; Dock verarbeitet deshalb den Gruppen-Drag selbst. Individuelle Tabs behalten ihre ursprüngliche Drag-Fläche. Ein Tooltip erklärt die Gruppenfläche. Es gibt weder eigenen Drag-Code noch neue Host-/Redock-Logik.

Erhaltene Bedienwege:

- Einzelnes Tool: Tab auswählen und ziehen.
- Ganze Gruppe: freie Fläche der Tab-Leiste ziehen; zusätzlich vorhandenes Tab-Kontextmenü „Float all“.
- Float, Close und weitere bestehende Dock-Aktionen: Framework-Kontextmenü am Tab.
- Show/Dock/Close sowie Maximize/Restore/Reset: Hauptmenü View; Layoutbefehle zusätzlich im bestehenden Inhalts-Kontextmenü.
- Splitter: unveränderte Dock-Splitter.

Die frühere separate Titelzeile und ihre Buttons entfallen bewusst; die notwendigen Befehle bleiben über diese Bedienwege erreichbar. In einem schwebenden Tool-Fenster ist ebenfalls der Tab bzw. die freie Tab-Leistenfläche die Drag-Fläche. Der native Rahmen des Hauptfensters ist kein zusätzlicher Dock-Header.

## Output und Variables

Output behält `ListBox`, schreibgeschützte `SelectableTextBlock`-Ausgabe und unveränderte Quellennavigation. Die vollständige Quellenbeschriftung bleibt als kompakter Button pro Eintrag erhalten. Clear Output bleibt oben, mit kleineren Abständen. Leeres Navigationsfeedback belegt keine Höhe mehr. Container-Padding und Mindesthöhe der ListBoxItems werden reduziert; Ausgabeeinträge haben keine feste Höhe. Die Ausgabe bleibt ohne Zeilenumbruch vollständig zugänglich; horizontale und vertikale Scrollbars sind bei Bedarf aktiv. Bei mehreren langen Hex-Einträgen werden beide Scrollrichtungen geprüft. Bestehende Copy-/Unicode-/Navigationsregressionen laufen unverändert mit.

Variables verliert nur die redundante innere Überschrift. Die Übersichtstabelle verwendet proportionale Spalten mit Mindestbreite und einzeilige, bei Bedarf gekürzte Werte. Eine separate Spaltenvorlage begrenzt diese Kürzung auf die Übersicht; die Detailansicht einschließlich ihrer Parametertabelle behält vollständige selektierbare, umbrechende Werte. Ohne Auswahl ist der gesamte Detailbereich unsichtbar und seine Grid-Zeile null hoch; die Tabelle nutzt die freie Höhe. Stale-/Refresh-Informationen bleiben erhalten.

Die zentrale Statusleiste aus G2-02 bleibt unverändert, einschließlich Ausführungsstatus, Layoutfeedback und Zeile/Spalte. Im Editor wird keine zweite Statusleiste eingeblendet.

## Layout und Persistenz

Die 70/30-Aufteilung, die untere 25-%-Zeile und alle Pane-/Container-Identitäten bleiben unverändert. `GripMode` gehört nicht zum Whitelist-Schema 1; R1 ergänzt kein serialisiertes Feld und benötigt keine Migration. Gespeicherte Benutzeranordnungen werden unverändert geladen; die neue Präsentationsvorgabe wird bei ihrer Initialisierung angewendet. Es erfolgt keine ungefragte Überschreibung oder Löschung von Benutzerlayouts.

Neue Tests prüfen exakten Capture/Restore-Roundtrip, Reset und anschließenden Startup-Restore, die unsichtbaren Chrome-Header sowie Maximize/Restore. Native Proben verwenden ausschließlich eigene Layoutdateien neben ihren Ergebnis-JSONs.

## Geänderte Dateien relativ zur R1-Baseline

- `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs`
- `CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml`
- `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs`
- `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs`
- `CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs` (neu)
- `CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs`
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`
- `eng/gui/native/Program.cs`
- `docs/gui/G2-02-R1-visual-corrections.md` (neu)

## Tests

Die sechs neuen GUI-Testfälle prüfen:

1. Genau drei sichtbare Tab-Leisten für die drei Default-Gruppen, unsichtbare Framework-Header, Gruppen-Drag-Eigenschaft mit Gruppen-DataContext, Einzel-Tab-Sichtbarkeit, Tabwechsel, Persistenz, Reset/Startup sowie Maximize/Restore.
2. Kurzes Print-Ergebnis vollständig innerhalb des Scroll-Viewports bei 1200×760 und Quellenbutton-Navigation zur korrekten Spalte.
3. Dieselbe Prüfung bei 911×512 logischen Clientpixeln.
4. Dieselbe Prüfung bei der Mindestgröße 640×420.
5. Zwölf Ausgaben mit je 1024 Hex-Zeichen, vollständiger Text sowie tatsächlich positive horizontale und vertikale Scroll-Offsets.
6. Keine sichtbaren Variablendetails ohne Auswahl; proportionale Tabellenspalten und verfügbare Tabellenhöhe.

Gezielter Lauf: 46 Tests bestanden (neue Tests plus vorhandene Output-/Tool-Regressionen). Keine bestehenden Tests entfernt, deaktiviert oder abgeschwächt. Die native Headerprüfung musste nach dem ersten Lauf auf `TemplatedParent == ToolChromeControl` präzisiert werden, da die real gefüllte DataGrid ebenfalls innere Rahmen namens PART_Border enthält. Das ist eine Korrektur des Prüfselektors, keine Änderung der geprüften Anforderung.

Der native Mindestgrößenlauf deckte zunächst eine teilweise außerhalb des Viewports liegende Print-Zeile auf. Kleinere vertikale Abstände am Clear-Button, Eintrag und äußeren Panel beheben dies; die Anforderung bleibt unverändert. Danach bestanden alle sechs gezielten Tests (`minimum-r2/visual.trx`) und alle neun nativen Größen-/Fensterzustände (`corrections-layout-final.json`, keine Fehler). Die nativen Layoutprüfungen bestehen mit 49/49, Neustart/Wiederherstellung mit 56/56 Prüfungen.

Der vollständige Lauf `validation/` besteht für Debug und Release: Builds erfolgreich, jeweils GUI 1121 bestanden, MCP 176 bestanden / 13 nicht ausgeführt, Core 11512 bestanden / 27 nicht ausgeführt. Nach der abschließenden Abstandsänderung und Ergänzung des sechsten GUI-Falls besteht die GUI-Wiederholung `final-r2-Debug/gui.trx` mit **1122/1122**. Der finale Stand einschließlich separater Detailspalten ist wie folgt geprüft; MCP/Core-Code blieb unverändert. Die nicht ausgeführten Fälle werden nicht als bestanden gezählt.

| Prüfung | Debug | Release |
| --- | --- | --- |
| Build | erfolgreich (`verified-build-Debug.log`) | erfolgreich (`final-build-Release.log`) |
| GUI, finaler Stand | 1122 bestanden, 0 Fehler, seriell (`verified-serial-Debug/gui.trx`) | 1122 bestanden, 0 Fehler (`final-Release/gui.trx`) |
| MCP | 176 bestanden, 13 nicht ausgeführt | 176 bestanden, 13 nicht ausgeführt |
| Core | 11512 bestanden, 27 nicht ausgeführt | 11512 bestanden, 27 nicht ausgeführt |

Ein erster Abschlusslauf (`final-Debug/gui.trx`) meldete 1121 bestanden und einen Fehler beim Start der isolierten Headless-App (`Dispatcher.VerifyAccess` / `DefaultRenderLoop.Add`), vor Ausführung des 640×420-Testkörpers. Der gezielte Sechs-Test-Lauf sowie die native 640×420-Prüfung bestanden. Dieser Lauf bleibt als Evidenz erhalten; keine Assertions oder bestehenden Tests wurden dafür geändert.

Beim zusätzlichen finalen Debug-Lauf (`verified-Debug/gui.trx`) trat derselbe Startfehler erneut vor einem anderen Testkörper auf (`SingleTabRowsSurviveSwitchCloseResetRestoreAndStartup`); ebenfalls 1121 bestanden / 1 fehlgeschlagen. Damit ist ein intermittierender Headless-Initialisierungsfehler beobachtet, dessen Ursache innerhalb dieses Präsentationsauftrags nicht behoben wird. Der abschließende vollständige Lauf mit ausschließlich dem Runner-Parameter `NUnit.NumberOfTestWorkers=0` besteht mit 1122/1122, ohne Filter, Teständerungen oder automatische Wiederholung einzelner Fälle. Dies beweist keine Behebung des intermittierenden Fehlers. Die vorhandenen Trace-Fehlerfalltests protokollieren außerdem erwartete Meldungen zu gesperrten temporären Trace-Dateien.

## Native Windows-Prüfung und Grenzen

Prüfscript:

```text
VAR result=0x(0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF)
Print(result)
Info(HASH-SHA256)
```

Das Script wird über den bestehenden GUI-Editor-/Ausführungspfad geladen und ausgeführt. Es erzeugt eine globale Variable, Print-Ausgabe mit Quelle Zeile 2 / Spalte 1 und SHA-256-Dokumentation. Die isolierten Nativhost-Modi `corrections-layout` und `corrections-review` dienen nur der Prüfung, nicht als neue Produktfunktion.

Direkt am Windows-Fenster mit der Computer-Use-Skill beobachtet:

- Ein Tabstreifen je Gruppe, keine wiederholten Titelzeilen; sichtbare Ausgabe im 1200×760-Standardfenster.
- Variable in Tabelle gekürzt, keine leeren Detailbeschriftungen ohne Auswahl.
- Info-Dokumentation nach Tabwechsel, weiterhin sichtbare Print-Ausgabe.
- Quellenbutton springt nach Zeile 2 / Spalte 1 (Statusleiste beobachtet).
- Maus-Drag auf freier Tabfläche floatet Variables und Info gemeinsam; Reset stellt die Standardgruppen wieder her und erhält Script/Output.
- Tab-Kontextmenü mit Float, Float all und Close erreichbar; Schließen von Info lässt den einzelnen Variables-Tab sichtbar; Reset öffnet Info erneut.
- Maximiertes Hauptfenster ohne überlappende Dock-Leisten und mit sichtbarem Output.

Screenshots aus Windows.Graphics.Capture:

- `artifacts/g2-02-r1/windows-standard.png`
- `artifacts/g2-02-r1/windows-info-output.png`
- `artifacts/g2-02-r1/windows-tab-actions.png`
- `artifacts/g2-02-r1/windows-maximized.png`

Standard- und Info-Aufnahme wurden nach der letzten Abstandsänderung aktualisiert. Kontextmenü- und Maximize-Aufnahmen dokumentieren den vorherigen R1-Zwischenstand mit denselben Dock-Headern und geringfügig größeren Output-Abständen.

Verfügbarer Monitor: 3840×2160, Windows-Skalierung 150 %. Native Größenprüfungen verwenden logische Clientgrößen; 1280×720 entspricht 1920×1080 physischem Clientraum bei 150 %, 911×512 ungefähr 1366×768. Fensterrahmen und Taskleiste sind dabei nicht mit eingerechnet.

**NOT VERIFIED:** tatsächliche Monitorumschaltung auf 1920×1080 oder 1366×768; Windows-Skalierung 125 %; vollständige manuelle Matrix aller Drag-/Drop-Ziele; der ausdrücklich ausgeschlossene Cross-Window-Mouse-Redock-Fehler. Prüfungen äquivalenter Clientgrößen ersetzen diese Nachweise nicht.

## Startkommando

```powershell
& 'C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1\CryptoWorkBenchAvalonia.Desktop\bin\Debug\net10.0\CryptoWorkBenchAvalonia.Desktop.exe' --dock-shell
```

Ein gültiges gespeichertes Layout bleibt geladen. Für die Standardanordnung bei Bedarf bewusst **View → Reset to Default Layout** wählen. R1-Präsentationsänderungen gelten auch ohne Reset für wiederhergestellte Tool-Gruppen.

## Verbleibende Punkte

- Eigentümerabnahme und die oben als NOT VERIFIED gekennzeichneten Windows-Prüfungen bleiben offen.
- Intermittierender Headless-Dispatcher-Startfehler in zwei Debug-Läufen; vollständiger serieller Abschlusslauf erfolgreich, Ursache nicht behoben.
- Bekannter Cross-Window-Mouse-Redock-Fehler unverändert; keine Lifecycle-Korrektur in R1.
- Kein vollständiges Light-/Dark-System; neue Änderungen führen keine hart codierten Farben ein.
- Alte Benutzerlayouts können weiterhin den optionalen Legacy-Status-Dock enthalten (unveränderte G2-02-Kompatibilität). Der Standard zeigt nur die zentrale Statusleiste.
