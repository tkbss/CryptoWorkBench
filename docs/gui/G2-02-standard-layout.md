# G2-02 – Standardlayout der Dock-Shell

## Ausgangszustand (11.11.2026)

Arbeitsverzeichnis: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1`.
Branch: `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.
Der übergeordnete Checkout steht auf `master` (`fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`) und wurde nicht bearbeitet.

Der GUI-Worktree enthielt bereits umfangreiche uncommittete G1-Implementierungen einschließlich Shell, Regressionstests und Sprachkernänderungen. Vor dem ersten Eingriff wurden `git status`, der vollständige Diff der getrackten Dateien und ein ZIP der Shell, Editor-View und GUI-Tests unter `artifacts/g2-02/baseline/` gesichert. Diese vorhandenen Änderungen wurden nicht zurückgesetzt. Andere Worktrees und Linux-Prüfungen wurden nicht bearbeitet. Kein Commit, Push, Merge oder PR.

Bisheriges Layout: horizontale Dreiteilung mit Variables/Output/Info links, Editor mittig und Diagnostics/Status rechts. Tool-Tabs lagen unten. Jede View besaß eine zusätzliche Layout-Menüzeile. Editorstatus (anfangs Ready, danach Zeile/Spalte), Status-Tool und Shell-Footer waren getrennt. Fenstertitel: CryptoWorkBench — Dock preview.

## Implementierung

`DockShellLayout` erzeugt nun eine vertikale Teilung 75/25. Oben liegen Editor (70 %) und Variables/Info (30 %); unten Output/Diagnostics über die gesamte Breite. Variables und Output sind die aktiven Tool-Tabs. Splitter bleiben Dock-Modelle. Status bleibt als stabile, zunächst geschlossene Legacy-Identität verfügbar.

`DockShellStyles.axaml` übernimmt für ToolControl die bestehende Dock-Template-Struktur mit Drag-/Drop-Eigenschaften, Tracking und Deferred/Recycling-Presenter; ausschließlich die Tabposition wird nach oben verlegt. Ein einfacher Style-Setter genügte wegen des lokalen Bottom-Werts im Bibliothekstemplate nicht. Dokument- und Tool-Tabs erhalten gemeinsame Mindesthöhe, Schriftgröße und Abstände; aktive Tabs zusätzlich halbfette Schrift. Farben und Auswahlzustände kommen aus dem Dock-Fluent-Theme. Kein vollständiges Theme-System.

Die redundante Layout-Menüzeile wird durch ein Kontextmenü im View-Inhalt ersetzt. Sämtliche Layoutbefehle bleiben außerdem im Hauptmenü View erreichbar, einschließlich gezieltem Maximieren einzelner Views. Feedback erscheint zentral.

Die einzige standardmäßig sichtbare Statusleiste liegt außerhalb von Dock. Sie zeigt den unveränderten StatusViewModel-Text, separat Layout-/Fensterfeedback und die bisherige Zeilen-/Spaltenanzeige. Langer Statustext bleibt selektierbar und horizontal scrollbar. Der Dock-Editor blendet seinen alten Statusrahmen aus und entfernt Schatten/Außenabstand; die klassische GUI behält ihre Anzeige. Output und Diagnostics erhalten kleinere Außenabstände. Fenstertitel: CryptoWorkBench; Mindestgröße 640 × 420 logische Pixel.

## Persistenz und Risiken

Schema 1, Dateipfad, atomare Speicherung, Validierung und Startup-Restore bleiben unverändert. Bestehende gültige Layouts werden mit ihrer ursprünglichen Anordnung und geschlossenen Tools wiederhergestellt. Keine automatische Migration und kein Löschen vorhandener Benutzerdateien. Ein ungültiges Layout nutzt den bestehenden sicheren Default-Fallback.

Das neue Default wird bei fehlender Layoutdatei oder ausdrücklich gewähltem Reset erzeugt. Das zusätzliche obere Split-Containerobjekt wird wie die bestehenden Hauptanker geschützt, damit das Floaten einer ganzen Gruppe seine Rückkehrstruktur nicht entfernt. Fehlende Rückkehranker werden in der passenden G2-Zeile ergänzt; alte Layouts ohne diese Zeile verwenden weiterhin ihre vorhandene Hauptstruktur.

Der alte Status-Dock bleibt zur Kompatibilität und über View verfügbar. Wenn ein altes Layout ihn enthält oder der Benutzer ihn ausdrücklich öffnet, ist dadurch weiterhin eine zusätzliche Statusansicht möglich. Das neue Standardlayout zeigt ausschließlich die zentrale Leiste. Diese Ausnahme vermeidet eine ungefragte Veränderung gespeicherter Benutzerlayouts.

Cross-Window-Mouse-Redock und dessen Factory-/Host-Lifecycle wurden nicht geändert. Das lokale ToolControl-Template muss bei späteren Dock-Bibliotheksupdates mit dem Upstream-Template abgeglichen werden.

## Tests und visuelle Abnahme

Die abschließenden Ergebnisse stehen unten. Die neuen Tests prüfen Gruppierung, Proportionen, gespeicherte benutzerdefinierte Anordnung, Reset, Focus/Restore, Tabposition und Status inklusive Zeile/Spalte bei 1920×1080, 1366×768, 1536×864, 1280×720, 1093×614, 911×512 und 640×420 logischen Clientgrößen. Die kleineren Größen modellieren verfügbare Fläche bei 125/150 %, ersetzen jedoch keine native Windows-DPI-Abnahme.

Bestehende G1-Tests werden nicht entfernt: Default-Erwartungen berücksichtigen fünf sichtbare Panes plus geschlossene Status-Identität; Tests für alle sechs Identitäten öffnen Status explizit. Struktur-Korruptionstests adressieren weiterhin den Tool-Container, nun innerhalb der oberen Zeile. Per-View-Menütests bedienen das neue Kontextmenü. Die vorhandenen historischen Schema-1-Fixtures bleiben unverändert.

## Start und manuelle Prüfung

```powershell
& 'C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1\CryptoWorkBenchAvalonia.Desktop\bin\Debug\net10.0\CryptoWorkBenchAvalonia.Desktop.exe' --dock-shell
```

Bei vorhandenem Benutzerlayout zunächst dessen Wiederherstellung prüfen. Für das neue Standardlayout bewusst **View → Reset to Default Layout** wählen; beim regulären Schließen wird die gewählte Anordnung gespeichert.

Eigentümerabnahme: Tabwechsel und Splitter, verschobene/floating Tools, Maximize/Restore, Reset und Neustart, kleines/maximiertes Fenster, 125/150 % Windows-Skalierung; anschließend Enter-Ausführung, Open/Save/History, Variables-Details/Copy, Info-Auswahl/Copy Example, Output-Clear/Quellennavigation, Diagnostics-Filter/Copy/Quellennavigation und MCP-Verbindung prüfen.

## Geänderte Dateien dieses Auftrags

Die folgende Liste bezieht sich auf den gesicherten G1-Ausgangszustand, nicht auf den bereits zuvor stark abweichenden Branch-HEAD.
- `CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs`
- `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs`
- `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs`
- `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs`
- `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs`
- `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs`
- `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml`
- `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs`
- `CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs`
- `CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs`
- `CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs`
- `CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs`
- `CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs`
- `CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs`
- `CryptoWorkBenchGuiTest/DockLayoutTests.cs`
- `CryptoWorkBenchGuiTest/DockMouseRedockTests.cs`
- `CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs`
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`
- `CryptoWorkBenchGuiTest/DockShellTests.cs`
- `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml`
- `CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml (neu)`
- `CryptoWorkBenchGuiTest/DockStandardLayoutTests.cs (neu)`
- `CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs (neu)`
- `eng/gui/native/Program.cs`
- `docs/gui/G2-02-standard-layout.md (neu)`

Zusätzliche lokale Nachweise liegen unter `artifacts/g2-02/` (Baseline, Build-/Testlogs, TRX, native Geometriemessungen, Windows-Screenshots). Der Vergleich aller bereits getrackten Diffs gegen die Baseline ergab außerhalb der angepassten Editor-XAML keine weitere Veränderung; insbesondere blieben Sprachkern, MCP, vorhandene ViewModel-Änderungen und Lockdateien erhalten.

## Native Windows-Nachweise

Die native Probe verwendet eine eigene Layoutdatei beim Ergebnis-JSON. `standard-layout` misst sieben Zustände (fünf Clientgrößen, maximiert, nach Restore); `standard-review` öffnet dieselbe produktive Shell zur interaktiven Prüfung ohne automatische Aktionen. Beide laufen über den bestehenden G1ShellProbe-Host. Die normale Benutzerdatei in LocalAppData bleibt unberührt.

Verfügbares Windows-Rendering: **150 % (RenderScaling 1.5)**. Die angegebenen 1920×1080 und 1366×768 der Probe sind logische Clientgrößen, keine umgeschalteten Monitorauflösungen. 911×512 entspricht ungefähr der verfügbaren Fläche eines 1366×768-Displays bei 150 % vor Abzug von Fensterrahmen/Taskleiste. Echte Monitorauflösungen und 125 % wurden nicht umgestellt. Maximiert wurde die vorhandene Anzeige verwendet (2560×1369,33 logische Clientpixel).

Mit der Computer-Use-Skill wurden am nativen Fenster die Standardansicht, der Wechsel auf Diagnostics und Info sowie die maximierte Ansicht geprüft. Die Tabs und Statuszeile waren sichtbar. Screenshots:

- `artifacts/g2-02/windows-standard.png`
- `artifacts/g2-02/windows-info-diagnostics.png`
- `artifacts/g2-02/windows-maximized.png`

Die frühen `native.json.*.png`-Dateien sind unvollständige RenderTargetBitmap-Exporte und **keine gültigen visuellen Nachweise**; sie werden nicht für die Abnahme verwendet. Die finale Probe exportiert daher nur Geometrie; die nutzbaren Bilder stammen aus Windows.Graphics.Capture.


## Abschließende Prüfergebnisse

**READY FOR VISUAL REVIEW** — Implementierung und automatisierte Prüfungen erfolgreich; endgültige Eigentümerabnahme ausstehend.

| Prüfung | Debug | Release |
|---|---:|---:|
| Beide Produkt-Solutions, Locked Restore + Build | Exit 0 | Exit 0 |
| GUI | 1116 bestanden, 0 Fehler | 1116 bestanden, 0 Fehler |
| MCP | 176 bestanden, 0 Fehler | 176 bestanden, 0 Fehler |
| Core | 11512 bestanden, 0 Fehler | 11512 bestanden, 0 Fehler |

Die TRX-Einzelresultate enthalten zusätzlich jeweils **13 NotExecuted** bei MCP und **27 NotExecuted** bei Core. Der CLI-Bericht zählt bei Core 26 Skips; ein weiterer Explicit-Test ist nicht im ausgeführten CLI-Gesamtumfang enthalten. Diese Fälle werden ausdrücklich nicht als bestanden gewertet (externe Installed-Codex-/manuelle Voraussetzungen, Hardwaremessung und plattformspezifische Tests). GUI: keine ausgelassenen Fälle. Die bestehenden Compilerwarnungen (u.a. Nullability, AVA2001 und parameterisierte XAML-Window-Konstruktion) bleiben; kein Buildfehler.

Nachweise: `artifacts/g2-02/final-validation/commands.json`, die sechs TRX-Dateien und zugehörige Logs. Ausführung über das vorhandene `eng/gui/Validate-G1Shell.ps1 -Configuration Both`. Der erste Sandbox-Build scheiterte an Windows-Rechten der SDK-Initialisierung; die genehmigten Builds außerhalb der Sandbox liefen erfolgreich.

Gezielte Dock-Prüfung: **318/318 bestanden**, darunter **8 neue G2-Testfälle**. Initiale Fehlversuche bleiben dokumentiert (`validation`, `targeted`, `dock-r2`): Bottom-Template-Priorität, veraltete G1-Default-/Menü-/Gruppierungsannahmen und ein korrigierter XAML-Namensraum. Keine Tests entfernt oder als Umgehung deaktiviert.

Native Windows-Proben, jeweils Exit 0:

- `native-final.json`: sieben Fensterzustände, keine Fehler, RenderScaling 1.5.
- `layout-r2.json`: 49 Checks bestanden.
- `layout-restart-r2.json`: 56 Checks bestanden, einschließlich gespeicherter Splits und floating Info.
- `cleanup-save.json`: 14 Checks bestanden (Szenario anchor).
- `cleanup-restore.json`: 30 Checks bestanden, einschließlich Reset, erneutem Redock, erhaltenen Identitäten/Editorzustand und HWND-Cleanup.

Die zusätzliche native Neustartprobe zeigte zunächst eine veraltete Testvorbereitung: die alten Dreispaltenwerte .30/.48/.22 summierten sich in der neuen oberen Zeile nicht mehr zu eins und wurden korrekt normalisiert. Die Testvorbereitung setzt nun .30/.70 in der oberen Zeile und .25 im unteren Bereich. Die Prüfung der wiederhergestellten .30 bleibt unverändert streng; der Wiederholungslauf besteht. Die beiden angepassten nativen Proben wurden anschließend im Debug-Nativhost neu kompiliert und erfolgreich ausgeführt; sie ändern keine produktive Logik.

## Offene Punkte

- Interaktive Eigentümerabnahme unter Windows, einschließlich echter 1920×1080-/1366×768-Monitorflächen und 125-%-Skalierung. 150 % wurde auf dem vorhandenen Monitor nativ geprüft.
- Bekannter Cross-Window-Mouse-Redock-Fehler bleibt außerhalb G2-02; keine Fehlerbehebung am Dock-Lifecycle behauptet.
- Vollständige Light-/Dark-Umschaltung ist nicht Bestandteil dieses Auftrags. Neue Styles verwenden Theme-Ressourcen; bestehende harte Farben anderer G1-Views wurden nicht umfassend überarbeitet.
- Gespeicherte Altlayouts können den Legacy-Status-Dock weiterhin enthalten; keine ungefragte Migration.
- Externe/Explicit- und nicht unter Windows ausführbare Tests bleiben offen, wie oben ausgewiesen.

Der übergeordnete `master`-Checkout wurde abschließend als sauber bestätigt. Die Änderungen bleiben uncommittet ausschließlich im GUI-Worktree.
