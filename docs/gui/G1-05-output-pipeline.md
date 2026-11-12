# G1-05 – Print Output Pipeline and Source Mapping

Stand: 6. November 2026. **CONDITIONAL GO für G1-06 nach ausdrücklicher Eigentümerabnahme**, unter den unten genannten übernommenen Plattform-/Interaktionsgrenzen. G1-05 ist implementiert und vollständig geprüft; G1-06 wurde nicht begonnen.

## 1. Ausgangsstand und Sicherung

Ausschließlich `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. G1-04 ist gemäß Auftrag abgenommen. Die 21 modifizierten getrackten und 40 ungetrackten Dateien wurden vor Produktänderungen vollständig unter `artifacts/g1-05/baseline/` gesichert. `baseline.json` enthält SHA-256, `initial-status.txt` den vollständigen Status. Andere Worktrees wurden nicht bearbeitet. Keine Paket-/Prism-/Grammatikänderungen, kein Commit, Push oder Merge.

Der vor Produktänderungen geschriebene Datenflussentwurf liegt unter `artifacts/g1-05/dataflow-design.md`. Alle G1-01–G1-04-Berichte, Evidence-JSONs, Prototypen und Paket-Locks bleiben bytegleich; die abschließende Hashprüfung ist in der Evidenz erfasst.

## 2. Bisherige Print-Pipeline

`OutputOperations.Print(OperationInvocation)` löst typisierte Variablen auf. Header verwenden `PrintOutput`; SECRET und KEY ergänzen für historische ScriptBooks an ihren Fortsetzungszeilen `out: `. Die String-Überladung erzeugt `out: ` plus das erste Argument, sendet ein synchrones statisches PrintEvent im geschützten HostOutputCallback und schreibt denselben Text auf die Konsole. Mehrzeilige Werte bleiben ein Ereignis. Info hat ein eigenes Ereignis und ist kein Print.

`WorkbenchExecutionPipeline` parst jeweils eine Einheit, installiert die Output-Handler unter der bestehenden InterpreterHostQueue-Zulassung, führt den Runner aus, sammelt Outputs in Ereignisreihenfolge und entfernt die Handler nach dem Drain der Passwort-Callbacks. Der finale Snapshot enthält bereits entstandene Ausgaben auch bei semantischem Fehler, Abbruch oder fehlgeschlagener Variablenerfassung. Syntaxfehler vor Runnerstart erzeugen keine Prints. Die GUI publiziert nach Abschluss der Einheit, nicht live während einer laufenden Einheit; ein ScriptBook publiziert nach jeder physischen Zeile.

`CryptoScriptEditViewModel.Present` verarbeitet PrintMessage, Info, Diagnostics und Variables. Bisher fügt der native Enter-Pfad über einen TextAnchor und `EditorIntent.Write` die PrintMessage hinter der ausgeführten Zeile ein. Die native Enter-Fortsetzung läuft zusätzlich über den vorhandenen WorkbenchTextArea-/Coordinator-Pfad. Diese Einfügung verändert TextDocument, Undo und Revision. Legacy behält dieses Verhalten.

ScriptBooks überspringen ausschließlich physische Zeilen, die mit `out:` beginnen. Legacy Open führt die übrigen Zeilen aus und hängt den originalen Dateitext an; es konvertiert historische Ausgaben nicht. Die Dock-Open-/Save-Commands laden ohne Ausführung und speichern den vollständigen Text über den vorhandenen PreserveText-Vertrag. Historische `out:`-Zeilen werden weder entfernt noch neu interpretiert. Line History bleibt am bestehenden BeginLine gebunden; Outputanzeige und Navigation rufen sie nicht auf.

## 3. Datenmodell und Source Mapping

`PrintOutputEntry` ist ein unveränderlicher Record mit DocumentId, ExecutionId, Sequence, nullable SourceLine/SourceColumn, Text und SourceRevision. DocumentId ist die bestehende logische Editorregistrierungs-ID (keine Dock-Tab-ID); Documentersatz erzeugt bereits im EditorBridge eine neue ID. ExecutionId entspricht der bestehenden angenommenen Host-Ausführung; mehrere ScriptBook-Einheiten teilen diese ID und erhalten fortlaufende Sequenzen. PresentationId dedupliziert die einzelnen Ergebnisbatches.

Die ANTLR-Funktionstokens liefern Line und Column. `AntlrToFunctionCallExpression` übergibt eine optionale SourcePosition an den AST; die Spalte wird einmal von nullbasiert zu einsbasiert konvertiert. FunctionCallEvaluator → FunctionCall → OperationInvocation reichen diese Metadaten an ein zusätzliches strukturiertes PrintResultEvent weiter. Synthetische ASTs und direkte String-Prints haben ausdrücklich keine Position. Keine Suche nach dem Wort Print, keine Grammatikänderung und keine Änderung der Auswertungsreihenfolge.

Der Host hört für Prints ausschließlich auf das strukturierte Ereignis; PrintEvent und Konsolentext bleiben für bisherige Verbraucher unverändert. ExecutionOutput ergänzt nur hostlokale Metadaten. Der Text wird weder dekodiert, normalisiert, gekürzt noch von `out:`-Präfixen befreit. Unicode, HEX, BASE64 und mehrzeilige Ausgabe bleiben erhalten.

Der native Editor liefert die tatsächlich ausgeführte physische Quellzeile. Nach seiner vorhandenen Enter-Fortsetzung erfasst der angenommene GUI-Lauf die Dokumentversion und prüft den exakten Zeilentext. So ist eine Navigation nach normalem Enter möglich, ohne nachträglich generierte Zeilen zu zählen. Wird der ausgeführte Text durch eine Enter-Einfügung mitten in der Zeile nicht mehr eindeutig abgebildet, bleibt die Position unbekannt. Direkte GUI-Calls erhalten nur dann eine Dokumentposition, wenn ihr Text dem Dokument entspricht. GUI-Gesamteditor-Ausführung erfasst den Quellstand in BeginExecution und addiert den physischen ScriptBook-Zeilenoffset. Der lokale Book-Pfad erfasst seinen Kontext auf dem UI-Dispatcher; angehängter Fremdtext bekommt keine erfundene Quellposition.

## 4. Service, View und Lebensdauer

Die bestehende Singleton-DockEditorSession besitzt genau einen DockOutputService. DockShellPresentation hält genau eine DockOutputView und löst deren alten Presenter vor Reparenting. Das Modell bleibt bei Float/Redock und Close/Show identisch. Keine zweite Editor-, Interpreter-, Coordinator- oder MCP-Instanz entsteht.

Der Service hält eine schreibgeschützte ObservableCollection und prüft UI-Thread, Sessiongültigkeit und deduplizierte Ergebnisidentität. Veröffentlichung erfolgt ausschließlich aus dem bereits zugelassenen GUI-Present-Pfad innerhalb der bestehenden Präsentationsbeobachtung. Ausgaben bleiben über mehrere Ausführungen und visuelle Wechsel erhalten. Clear Output entfernt nur die Einträge; bereits publizierte Batches werden dadurch nicht erneut zugelassen. Session-Dispose sperrt spätere Veröffentlichung und gibt Einträge sowie Identitätsmengen frei. Keine Speicherung in ScriptBooks, Layout oder VariableState.

Die View verwendet vorhandene Avalonia-Controls und Dock-Fluent-Themes: virtualisierte ListBox, SelectableTextBlock, horizontales Scrollen für lange Texte, Quellenbuttons und Clear Output. Der Text ist read-only, selektierbar und mit Ctrl+C kopierbar. Die Quellenbutton-Beschriftung zeigt Zeile/Spalte oder ausdrücklich „Source unknown“. Bei abgelehnter Navigation erscheint ein sichtbarer Hinweis. Kein Copy All in G1-05.

## 5. Navigation und Ablösung der Einfügung

Navigation prüft DocumentId, dieselbe TextDocument-Version, gültige Zeilen-/Spaltengrenzen und die bestehenden CanRelocate-/Coordinator-Gates einschließlich Busy, Pending, Observer, Dialog, Lease und Shutdown. Nur Caret, Scrollposition und Fokus werden verändert; kein Text, keine History, keine Undo-Operation und keine Ausführung. Ein nativer Floating-Editor wird über seinen bestehenden TopLevel aktiviert. Eine spätere Änderung, auch Undo zu demselben sichtbaren Text mit anderer Version, macht den alten Eintrag ungültig. Es findet kein stilles Rebasing statt.

Bei installiertem DockOutputService führt der ViewModel-Pfad den alten `publishOutput`-Schreibcallback nicht aus. Enter-Admission, WorkbenchTextArea, EditorIntent und native Fortsetzung bleiben unverändert. Die normale Enter-Zeile verändert weiterhin erwartungsgemäß den Quelltext; die Output-Publikation selbst verändert weder TextDocument/UndoStack noch Dirty-State, Revision, Caret oder SaveIntent. Ohne DockOutputService bleibt Legacy unverändert Standard.

## 6. GUI-/MCP-Grenze und Nebenläufigkeit

Autorisierte lokale GUI-Line-, lokale ScriptBook- und bestehende GUI-Editor-Gesamtausführung publizieren Print-Ereignisse. MCP-V2-Ausführung behält ausschließlich ihre bestehenden Receipts und Output-Rückgaben. Die Projektion liest unverändert Kind/Text; Source-Metadaten erweitern keinen externen Vertrag. MCP-Textänderungen führen nichts aus und erzeugen keinen Output. Es gibt kein Polling, Erraten oder Replay aus Editorzustand.

Die bestehende Queue serialisiert Interpreterzugriff, drainiert Host-Callbacks und validiert die Originaldestination vor GUI-Present. Der Output-Service prüft deren Gültigkeit nochmals während der Publikation. Bereits entstandene Teilprints überleben Fehler und Abbruch, solange die Originalsession zur Veröffentlichung berechtigt bleibt. Shutdown widerruft diese Berechtigung, wartet auf laufende Arbeit und entsorgt dann den Zustand. Verspätete Resultate können keinen Nachfolgerzustand befüllen.

## 7. Permanente Tests und Review

Neue `DockOutputTests` prüfen Identitäten, Metadaten, Reihenfolge, Unicode/Mehrzeiligkeit, reine Info-/Diagnostic-/Syntaxfälle, Teilresultate, native Enter-Ausführung in beiden Hosts, Undo/Redo ohne Replay, Navigation einschließlich Stale/Bounds/Fokus, Copy/Ctrl+C, Clear, mehrfache Dockzyklen, geschlossene Tools während asynchroner Arbeit, Abbruch, Dedup/Revocation, lokale Book- und GUI-Editor-Ausführung, MCP-V2-/Lease-Grenze, Shutdown mit spätem Callback und lange scrollbare Ausgabe. `PrintSourceTests` prüfen Parser-/Nested-Tokenpositionen, strukturiertes und unverändertes Legacy-Ereignis sowie explizit unbekannte synthetische Quellen.

Die vorhandenen G1-01–G1-04-Testfälle bleiben erhalten. Zwei G1-03-Native-Enter-Prädikate werden aufgrund der ausdrücklich abgelösten Dock-Ausgabe angepasst: Sie verlangen jetzt denselben Printtext im Output-Service **und** exakt Script plus nativen Zeilenumbruch im Editor. Die drei Fälle der bestehenden CaptureFailure-Regression vergleichen weiterhin den vollständigen ExecutionOutput-Record; ihre Erwartung ergänzt die exakten Source-Koordinaten (1:20 bzw. 1:45). Keine Assertion wird entfernt oder gelockert, kein Core-Bestandstest geändert.

Zwischenbefunde bleiben in `artifacts/g1-05/`: initiale SDK-Sandboxgrenze, korrigierter Null-Template-Lifecycle, native CRLF-Erwartung und erweiterte Record-Gleichheitsprüfung. Das Review ergänzte UI-Dispatch beim lokalen Book-Quellkontext und eine eigene Regression dafür. Frühere fehlgeschlagene Läufe werden nicht als Abschlussnachweis verwendet.

## 8. Native Windows-Prüfung und Testmatrix

Neue G1ShellProbe-Modi: `output` (programmgesteuert) und `output-input` (externe Maus-/Tastatureingabe an dokumentierten Checkpoints). G1-01-Prototyp sowie G1-02–G1-04-Modi werden separat wiederholt. Float/Redock-Vorbereitung bleibt programmgesteuert; daraus wird keine vollständige Cross-Window-Maus-Redock-Freigabe abgeleitet.

Alle folgenden Angaben gelten getrennt für Debug und Release. Vollständige Lösungen wurden in beiden Konfigurationen gebaut. Die finalen GUI-TRX-Dateien sind `GUI-Debug-review.trx` und `GUI-Release-review.trx`; MCP/Core liegen unter `verified/` (Debug) und `release/` (Release). `foundation-headless/` enthält G1-01-Headless, `foundation-native-{Debug,Release}.json` dessen native Prüfungen. Alle Pfade beziehen sich auf `artifacts/g1-05/`.

| Suite | Debug Passed | Release Passed | Failed | Unverändert nicht ausgeführt |
|---|---:|---:|---:|---|
| GUI vollständig | 871 | 871 | 0 | 0 |
| davon neue G1-05-Fälle | 21 | 21 | 0 | 0 |
| MCP vollständig | 176 | 176 | 0 | 13 Explicit |
| Core vollständig | 11.512 | 11.512 | 0 | 26 Skipped, 1 Explicit |
| davon neue PrintSource-Fälle | 4 | 4 | 0 | 0 |
| G1-01 Headless | 12 | 12 | 0 | 0 |
| G1-01 Windows nativ | 60 | 60 | 0 | 0 |
| G1-02 Dock / Legacy / Fallback | 67 / 10 / 13 | 67 / 10 / 13 | 0 | 0 |
| G1-03 Editor und echter Desktop-MCP-Transport | 54 | 54 | 0 | 0 |
| G1-03 Startup-Fehler | 5 | 5 | 0 | 0 |
| G1-03 externe Tastatur und Transport | 54 | 54 | 0 | 0 |
| G1-04 Tools programmgesteuert | 32 | 32 | 0 | 0 |
| G1-04 externe Tool-Eingaben | 34 | 34 | 0 | 0 |
| G1-05 Output programmgesteuert | 29 | 29 | 0 | 0 |
| G1-05 externe Output-Eingaben | 29 | 29 | 0 | 0 |
| **Windows nativ gesamt** | **387** | **387** | **0** | **0** |

Abweichung zur G1-04-Baseline **Debug**: GUI +21, Core +4, native +58; MCP und Headless unverändert. **Release**: dieselben Änderungen; keine zusätzlich ausgelassenen Tests. Explicit wird nie als Passed gezählt. TRX-Gesamtzahlen umfassen bei MCP 189 und bei Core 11.539 Fälle; die tatsächlich ausgeführten und bestandenen Zahlen stehen oben. Die unveränderten Explicit-/Skip-Kategorien werden separat ausgewiesen.

Die externen G1-05-Checkpoints verwenden echte Windows-Eingaben über den computer-use-Skill (`@oai/sky`): Return, KP_Enter, Ctrl+Z/Ctrl+Y in beiden Editorhosts, Mausauswahl bzw. Doppelklick plus Ctrl+A und Ctrl+C in beiden Outputhosts, Klick auf den Quellenbutton sowie Clear Output. Der native Probe verifiziert den exakten Clipboard-Inhalt, Fokus/Caret, unveränderten Dokumentstand, kein Replay und finalen Shutdown mit offenem Floating-Output. In Debug wurde angedockter Text vollständig mit Mauszug markiert; Floating- und Release-Auswahl verwendeten Doppelklick plus Ctrl+A. Erste unvollständige Auswahlversuche wurden nicht als bestandene Copy-Prüfung gewertet. Keine behauptete externe Maus-Redock-Prüfung: Layoutwechsel bleiben Factory-Aufrufe der Probe.

Die externen G1-03-Checkpoints umfassen Unicode, Undo/Redo, beide Enter-Tasten, Line History und eine einzelne `(`-Taste (aktive Schweizer Belegung: Shift+8); die folgenden TLS-/STDIO-/Dialog-/Shutdown-Prüfungen sind programmgesteuert. G1-04 prüft Identifier-/Wertkopie, Mausauswahl von Unicode-Prosa, sichtbare Copy-Example-Buttons, internen Link, Back und Editorfokus. Die programmgesteuerten Modi benutzen echte native Windows, aber keine externen SendInput-Eingaben; dies ist in jedem Ergebnisfile gekennzeichnet.

Review-/Zwischenlaufabweichungen: Die neue native Output-Probe benötigte denselben expliziten Visual-Attach-/Foreground-Readiness-Schritt wie G1-03; frühe Aufrufe vor dem angebundenen Editor erfüllten den ersten Output-Checkpoint nicht. Die korrigierte Probe besteht in beiden Modi und Konfigurationen. Ein Debug-Nachbuild mit zuvor für Release aufgelösten Assets meldete die nur in Debug verwendete DeveloperTools-Erweiterung als fehlend; ein konfigurationsgerechtes `restore --locked-mode` behebt dies ohne Paketänderung. Alle Zwischenlogs bleiben erhalten. Der finale Review verschärfte den Teilresultat-Test auf einen tatsächlich abbrechenden `Print(missing)`-Fehler; beide GUI-Suiten wurden danach vollständig wiederholt.

Reproduktion:

```powershell
./eng/gui/Validate-G1Shell.ps1 -Configuration Both -Native -EditorIntegration -ToolIntegration -OutputIntegration -EvidenceDirectory artifacts/g1-05/reproduction
./prototypes/G1DockFeasibility/Validate.ps1 -Configuration Both -Native -EvidenceDirectory artifacts/g1-05/reproduction-foundation
# Separat für Debug und Release, mit externer Bedienung an den Checkpoints:
./eng/gui/native/bin/Debug/net10.0/G1ShellProbe.exe editor-input artifacts/g1-05/repro-editor-Debug.json
./eng/gui/native/bin/Debug/net10.0/G1ShellProbe.exe tools-input artifacts/g1-05/repro-tools-Debug.json
./eng/gui/native/bin/Debug/net10.0/G1ShellProbe.exe output-input artifacts/g1-05/repro-output-Debug.json
```

## 9. Dateien, Abschlussprüfung und Risiken

Die [Ergebnisevidenz](evidence/G1-05-results.json) enthält G1-05-Dateiliste, Git-Status, SHA-256, Erhaltungsprüfung und konkrete Testläufe. `git diff --check` besteht; alle ungetrackten Dateien werden zusätzlich auf Whitespace und JSON-Parsebarkeit geprüft. Die drei übernommenen Lockdateien ohne abschließende LF bleiben bytegleich. Endstatus einschließlich Vorarbeiten: **31 modifizierte getrackte und 47 ungetrackte Dateien**, nichts gestaged, unveränderter HEAD. Von den 61 anfänglichen Dateien bleiben 54 bytegleich; sieben werden gezielt weiterentwickelt. Zehn zuvor saubere getrackte Dateien werden geändert und sieben neue Dateien hinzugefügt. Keine andere Worktree-Arbeit wird angefasst; keine Probeprozesse bleiben zurück.

Übernommene Grenzen: Linux/macOS nativ, gemischte DPI/Multi-Monitor, vollständige Cross-Window-Maus-Redock-/Overlay-Matrix, native File-Picker und Installed-Codex-Explicit-Fälle bleiben außerhalb dieses Nachweises. Output ist ein flüchtiges, auf ausdrücklichem Print beruhendes Protokoll; es behält auch ausdrücklich ausgegebenes Schlüsselmaterial bis Clear/Shutdown im Speicher. Es gibt in G1 keine automatische Rotation oder Persistenz. Quellen werden bei jeder späteren Revision konservativ als veraltet abgewiesen; keine Mehrdokumentnavigation.


## 10. Geänderte Dateien und Freigabeentscheidung

G1-05 ändert gegenüber der Sicherung:

- `CryptoScript/Model/AntlrToFunctionCallExpression.cs`
- `CryptoScript/Model/Ast/FunctionCallExpressionNode.cs`
- `CryptoScript/Model/FunctionCall.cs`
- `CryptoScript/Model/FunctionCallEvaluator.cs`
- `CryptoScript/Model/OperationInvocation.cs`
- `CryptoScript/Operations/OutputOperations.cs`
- `CryptoScriptUnitTest/PrintSourceTests.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs`
- `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs`
- `CryptoWorkBenchAvalonia/Shell/DockOutputService.cs`
- `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs`
- `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs`
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`
- `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs`
- `CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs`
- `CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs`
- `CryptoWorkBenchGuiTest/DockOutputTests.cs`
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`
- `CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs`
- `docs/gui/G1-05-output-pipeline.md`
- `docs/gui/evidence/G1-05-results.json`
- `eng/gui/Validate-G1Shell.ps1`
- `eng/gui/native/Program.cs`

Keine neue kritische Regression festgestellt. **CONDITIONAL GO für G1-06 nach Eigentümerabnahme**, mit den übernommenen Plattform-/Redock-/Langzeitgrenzen. Die Entscheidung ist keine allgemeine plattformübergreifende Veröffentlichungsfreigabe. Nach G1-05 wird gestoppt; G1-06 ist nicht begonnen.
