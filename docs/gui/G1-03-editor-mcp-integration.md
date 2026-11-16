# G1-03 – CryptoScript Editor Integration and MCP Lifecycle

Stand: 6. November 2026. **CONDITIONAL GO für G1-04 nach Eigentümerabnahme.**

Der produktive CryptoScript-Editor ist in die ausdrücklich aktivierte Dock-Shell integriert. Visuelle Hostwechsel erhalten Editor, TextDocument, Undo/Redo, fachliche History und produktive Registrierung. Busy-, Pending-, Dialog- und MCP-Lease-Zustände verhindern Layoutwechsel. Der reale Desktop-MCP-Pfad wurde einschließlich gepinntem STDIO/TLS-Transport, Editoränderung, bestätigter Ausführung und Shutdown geprüft. Die Bestandsshell bleibt Standard; G1-04 wurde nicht begonnen.

## 1. Ausgangsstand und Arbeitsgrenze

Repository `tkbss/CryptoWorkBench`, Worktree `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.

Zu Beginn lagen die abgenommenen G1-01-/G1-02-Ergebnisse uncommitted vor: 17 modifizierte getrackte und 28 ungetrackte Dateien. Vor Änderungen wurden alle 45 Dateien unter `artifacts/g1-03/baseline/` kopiert und mit SHA-256 in `baseline.json` erfasst; der vollständige Ausgangsstatus steht in `initial-status.txt`. Die Analyse und der Entwurf wurden vor produktiven Änderungen in `artifacts/g1-03/lifecycle-design.md` festgehalten.

Die beiden bisherigen Berichte, beide Evidence-JSONs, sämtliche G1-01-Prototypdateien und alle übernommenen Paket-Locks bleiben bytegleich. G1-02-Produktcode und dessen nativer Runner wurden gezielt weiterentwickelt. Die 13 permanenten G1-02-Shell-Tests bleiben unverändert. Die früheren Evidenzdateien beschreiben weiterhin ihren damaligen Quellstand.

Technologie unverändert: .NET 10, SDK 10.0.401, Runtime 10.0.12, Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock 12.1.0.13, lokale Prism-Integration auf `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Keine neuen Pakete, Prism-Patches oder Lockänderungen gegenüber G1-02. Andere Worktrees wurden nicht bearbeitet.

## 2. Analyse des bisherigen Lebenszyklus

`CryptoScriptEditView` verband bisher visuelles Attach mit `ViewModel.AttachEditor()` und visuelles Detach mit `DetachEditor()`. Das Abhängen einer Ansicht war deshalb fachlich wirksam: EditorBridge-Registrierung wurde beendet, MCP-Lease freigegeben, Coordinator-Drain gestartet und Generationen beziehungsweise VM-Lebenszyklus verändert. `Dispose()` beendet außerdem Passwort- und History-Bindungen. Diesen Pfad unverändert beim Dock-Reparenting auszuführen hätte laufende Requests entwertet und die fachliche Session verloren.

`EditorBridge` besitzt die registrierte Editor-/TextDocument-Identität und Revision. `EditorCoordinator` verwaltet Admission, Tickets, Pending-/Active-/Observer-Zustände, GUI-/MCP-Phasen und WorkspaceGeneration. `InterpreterHostQueue` bleibt die einzige Ausführungsserialisierung. SaveIntent bindet Registrierung, Revision und WorkspaceGeneration; vorhandene Dateioperationen halten ihre ursprünglichen Pending-Verpflichtungen über asynchrone Schritte. `App.CoordinateClosing` widerruft Admission, beendet Transport und wartet den bestehenden Drain ab, bevor das Fenster tatsächlich geschlossen wird.

Ein nativer Floating-Host konsultiert beim Schließen nicht automatisch `Document.CanClose`. Der bereits in G1-02 eingeführte Host-Schutz bleibt daher erforderlich. Außerdem ist die Eingabegeneration von der WorkspaceGeneration zu unterscheiden: Der bestehende LostFocus-Pfad darf weiterhin kurzlebige Input-Intents invalidieren. G1-03 behauptet und erzwingt keine unveränderte Eingabegeneration bei Fokuswechseln.

## 3. Gewählter Entwurf

Die bestehende Prism-DI erzeugt genau eine `DockEditorSession` mit dem bestehenden Singleton-ViewModel, einem `CryptoScriptEditView`, einem `WorkbenchTextEditor` und dessen ursprünglichem TextDocument/UndoStack. `DockShellState.DocumentId` bleibt die stabile logische Dock-ID. Die separate EditorBridge-EditorId entsteht einmal beim produktiven Start und bleibt über alle visuellen Wechsel identisch; sie wird nicht künstlich auf die Dock-ID gesetzt.

Die View erhält einen internen Session-Modus. In diesem Modus hängen visuelle Attach-/Detach-Ereignisse nur Eingabe-, Caret- und Popup-Handler an beziehungsweise ab. Sie registrieren und deregistrieren den Editor nicht. Der öffentliche Legacy-Konstruktor behält seine bisherige Semantik.

Dock erzeugt leichte Presenter. Bevor derselbe View einen neuen Presenter erhält, wird der vorherige Content explizit gelöst. Das vermeidet konkurrierende Parents und zusätzliche Editorinstanzen. Die Session beginnt die Registrierung einmal nach erfolgreicher struktureller Shell-Erzeugung. Nach dem bestehenden Shutdown-Drain wird sie idempotent entsorgt. Das als Editor-Abhängigkeit bereits erzeugte InfoViewModel wird dabei ebenfalls entsorgt, damit seine History-Subscription endet; dessen Tool-View bleibt ein Platzhalter.

Layoutänderungen laufen in einem synchronen Coordinator-Observer-Scope. Dieser Scope erteilt keine Ticket-, Schreib- oder Interpreter-Autorität; insbesondere können reentrante Popup-/Visual-Callbacks keine Ausführung oder Textmutation erlangen. Completion und Parameterhinweise werden vor dem Hostwechsel geschlossen. Visuelle Handler werden für den neuen Host wieder angebunden. Die vorhandene LostFocus-Invalidierung bleibt bestehen.

## 4. Verhalten bei Docking und Shutdown

| Zustand / Aktion | Verhalten |
|---|---|
| Idle: Float, Redock, zulässiger Dock-Bereich | Gleiche View, gleicher Editor, TextDocument, UndoStack, Registrierung und WorkspaceGeneration. Auswahl und Caret bleiben erhalten. |
| GUI-Ausführung oder reservierter Interpreter-Host | Layoutänderung abgelehnt, keine Wiederholung. |
| MCP-Lease, auch ohne gerade laufenden Job | Layoutänderung abgelehnt; Lease wird nicht zum Docking freigegeben. |
| Aktiver/Pending-Coordinator, Observer, Editor-Lock oder Document-Update | Layoutänderung abgelehnt. |
| Datei-/Passwort-/Bestätigungsdialog | Pending- beziehungsweise Modalzustand sperrt Layoutänderung. |
| Completion/Parameterhinweis bei sonst idle Editor | Popup wird beendet, danach Hostwechsel; verspätete Insertion hat keine Wirkung. |
| Floating-Host mit CryptoScript schließen | Direkter nativer Close wird abgelehnt. Redock ist der Rückweg. |
| Tool-Fenster während Busy schließen | Ebenfalls konservativ gesperrt, damit kein indirekter Layoutwechsel entsteht. |
| MainWindow während Request schließen | Bestehender Shutdown-/Drain-Vertrag; ursprüngliche Verpflichtung wird abgewartet, anschließend alle Hosts und Registrierung beendet. |

Die Sperre betrifft konservativ die gesamte Dock-Struktur. Dynamische `DockCapabilityPolicy` und `IDockDropPolicy` greifen beim UI-Drag/Drop einschließlich der Drop-Validierung. Überschriebene Factory-Mutationsgrenzen sichern Move, Swap, Split, Float, Gruppen-Float, Dokumentgruppen und Close. Menüaktionen prüfen dieselbe Session-Bedingung. Der native Host prüft CloseReason und Busy-Zustand; Owner-/Application-Shutdown bleibt möglich. Es gibt keine Warteschlange für abgelehnte Dock-Aktionen und kein späteres automatisches Nachholen.

Der reguläre Start bleibt Legacy; `--dock-shell` aktiviert die neue Shell. Strukturelle Fehler vor dem Registrierungsstart dürfen weiterhin auf Legacy zurückfallen. Fehler ab produktiver Initialisierung werden weitergeworfen: Admission wird widerrufen, die angefangene Registrierung und das Layout werden freigegeben. Ein zweiter Editor wird nicht erzeugt. Zu diesem Zeitpunkt ist das Fenster noch nicht gezeigt und der Desktop-Hook installiert lediglich Consent-Handler, keinen bereits laufenden Transport. Dieser spätere Fehlerpfad wird nativ mit einer injizierten Exception nach erfolgreicher Registrierung geprüft.

## 5. Editor- und Dateiaktionen

Syntaxdarstellung, vorhandene Print-/Ausgabepräsentation, Return-Ausführung, Undo/Redo, Caret und Auswahl verwenden den bestehenden Editor. Die vier bisherigen History-Gesten sind am Editor-View angebunden und damit auch im Floating Window erreichbar. Die neue Shell bietet Open/Save im File-Menü sowie Ctrl+O/Ctrl+S im Editor.

Open ist ein eigener, textbasierter Shell-Command. Picker und Stream-Lesen bleiben an das ursprüngliche FileIntent gebunden. Nach Await-Grenzen werden Registrierung, Revision, WorkspaceGeneration und Shutdown geprüft; die Textänderung erfolgt mit einem frischen, regulären Coordinator-Ticket. Ein nichtleeres Dokument erfordert Ersetzungsbestätigung. Undo bleibt verfügbar; Laden gilt als Änderung und setzt Dirty. Es gibt keine Parserausführung, keine VariableState-Wiederherstellung und keine geänderte Legacy-OpenScriptBook-Semantik.

Save benutzt unverändert die vorhandene SaveIntent-/Commit-Admission, inklusive Overwrite-Bestätigung und Drain-Verpflichtung. Nur das interne Ziel erhält eine optionale `PreserveText`-Eigenschaft: Die Dock-Shell speichert den vollständigen UTF-8-Text einschließlich Unicode und Leerzeilen. Der Default bleibt die bisherige Legacy-ScriptBook-Serialisierung. Die vorhandene Dirty-/SaveIntent-Semantik wird nicht durch einen eigenen Snapshot- oder History-Mechanismus ersetzt.

## 6. MCP-Nachweise

`DesktopMcpSession` erhält jetzt `Window`, Connect-Button und denselben AutomationService statt einer festen MainWindow-Abhängigkeit. Consent, Pairing, Credentials, Zertifikatbindung, Transport, Widerruf und Drain bleiben derselbe Produktionspfad. Der bestehende `--mcp-stdio`-Einstieg ist unverändert.

Die neuen Headless-Tests verwenden isolierte AutomationServices zur Prüfung von Lease, Revision, GUI-Ausschluss, Pending-Requests, verspäteten Callbacks und Shutdown. Diese Tests sind kein Transportnachweis.

Der native Editor-Probe startet dagegen das echte Prism-App mit dem produktiven Desktop-Hook. Er bestätigt die realen Consent-/Pairing-Dialoge, übernimmt Token und Zertifikat-Pin ausschließlich im Arbeitsspeicher und startet die tatsächliche Desktop-EXE mit `--mcp-stdio`. JSON-RPC läuft über diese Bridge zum echten lokalen TLS-Host. Geprüft werden Initialize, Status/Editoridentität, Acquire, GUI-/Dock-Ausschluss, Update, Read, bestätigte Execute-Operation mit `completed`, Release, anschließendes Floating, Shutdown mit Pending-Ticket, Widerruf des bereits verbundenen Clients und sauberer STDIO-EOF-Exit. Keine Pairing-Secrets werden in Evidenz oder Logs gespeichert.

Das ist ein realer MCP-Transporttest mit einem Testclient, kein Nachweis mit einem installierten Codex-Client. Die 13 bisherigen Explicit-Codex-Fälle bleiben separat unausgeführt.

## 7. Native Windows-Prüfung

Der G1-02-Runner wurde erweitert, kein paralleler Runner eingeführt. `dock` behält eine G1-02-Platzhalter-Fixture; `legacy` und `fallback` prüfen weiterhin den Bestandspfad. `editor` prüft den produktiven Editor, reale HWND-/Owner-/Vordergrundbeziehungen, fünf Float-/Redock-Zyklen, Host-Close-Schutz, Popup-Lebenszyklus, echten Passwortdialog, Transport und Shutdown. Seine Texteingabe ist ausdrücklich programmgesteuert.

`editor-input` wartet an denselben Eingabepunkten auf externe Windows-Eingabe. Debug und Release wurden separat mit dem Computer-Use-Werkzeug bedient: Unicode `ä€😀`, Ctrl+Z, Ctrl+Y, Return im Floating Window, KP_Enter nach Redock, Alt+PageUp im Floating Window und eine einzelne `(`-Taste für Completion. Die anschließenden Identitäts-, History-, Dialog-, Transport- und Shutdown-Checks bestehen ebenfalls. Testdaten werden vom Probe vorbereitet; die genannten Eingaben kommen vom Windows-Werkzeug. Native Dock-Bewegungen selbst erfolgen über die produktive Factory, nicht als Cross-Window-Mausgeste.

## 8. Debug-/Release-Matrix

Alle Zahlen gelten **je Konfiguration**; beide Konfigurationen wurden separat ausgeführt.

| Suite | Passed | Failed | Skipped | Explicit nicht ausgeführt |
|---|---:|---:|---:|---:|
| GUI vollständig, final | 830 | 0 | 0 | 0 |
| davon G1-03-Editor | 20 | 0 | 0 | 0 |
| davon unveränderte G1-02-Shell | 13 | 0 | 0 | 0 |
| MCP vollständig | 176 | 0 | 0 | 13 |
| Core vollständig | 11.508 | 0 | 26 | 1 |
| G1-01-Prototyp Headless | 12 | 0 | 0 | 0 |
| G1-01-Prototyp Windows nativ | 60 | 0 | 0 | 0 |
| G1-02-Fixture / Legacy / Fallback nativ | 67 / 10 / 13 | 0 | 0 | 0 |
| G1-03 Editor + echter MCP-Transport nativ | 54 | 0 | 0 | 0 |
| G1-03 produktiver Startfehler nativ | 5 | 0 | 0 | 0 |
| G1-03 externe Windows-Eingabe + Transport | 54 | 0 | 0 | 0 |

GUI steigt gegenüber G1-02 von 810 auf 830 (+20). MCP, Core und die 90 bisherigen nativen Checks bleiben unverändert. Die 26 Core-Skips betreffen unverfügbare Plattform-/Symlink-Voraussetzungen; `MeasureLocalHardware` bleibt Explicit. TRX stellt Skipped und Explicit als `NotExecuted` dar; Einzelresultate und NUnit-Zusammenfassung wurden gemeinsam ausgewertet. Explicit wird nicht als Passed gezählt.

Beide vollständigen Lösungen wurden in Debug und Release gebaut. Nach den zusätzlichen File-Command-Testfällen und der abschließenden Info-History-Cleanup-Korrektur wurde die gesamte Build-/Regressionsmatrix nochmals in beiden Konfigurationen ausgeführt.

Reproduktion:

```powershell
./eng/gui/Validate-G1Shell.ps1 -Configuration Both -Native -EditorIntegration `
  -EvidenceDirectory artifacts/g1-03/review
./prototypes/G1DockFeasibility/Validate.ps1 -Configuration Both -Native `
  -EvidenceDirectory artifacts/g1-03/review-foundation
# Nach Build, jeweils mit eigener Ausgabedatei und aktivem Testfenster:
./eng/gui/native/bin/Debug/net10.0/G1ShellProbe.exe editor-input artifacts/g1-03/review-input-Debug.json
```

Der letzte Modus verlangt die im Fenstertitel genannten tatsächlichen Eingaben; er ist kein unbeaufsichtigter CI-Lauf. Release entsprechend aus dem Release-Verzeichnis starten. Vorherige Rohdaten werden nicht überschrieben. Maßgebliche Läufe: `artifacts/g1-03/verified/`, `foundation/`, `native-input-Debug-final.json`, `native-input-Release-final.json`.

## 9. Befunde und Korrekturen während der Prüfung

- Ein erster nativer Hintergrundstart erhielt keinen Windows-Vordergrund. Das Fenster wurde beim Wiederholen explizit aktiviert; die echte Foreground-Prüfung wurde nicht abgeschwächt.
- Der anfängliche Popup-Test verwendete fälschlich `IsVisible`. AvaloniaEdit 12 implementiert CompletionWindowBase als Popup; `Hide()` schließt das Popup. Maßgeblich ist `IsOpen`, nicht Visual.IsVisible ([geprüfter Paket-Quellcommit](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/CodeCompletion/CompletionWindowBase.cs)). Der erste Befund war damit kein belegter Popup-Leak. Der explizite Popup-Abschluss vor der Layoutmutation wurde zusätzlich als klare Hostgrenze umgesetzt und auf reentrante/verspätete Insertion geprüft.
- Ein Probe-Readiness-Check akzeptierte vorübergehend `TopLevel == null` als Floating-Host. Er wartet jetzt auf ein tatsächliches anderes Window.
- Der neue Testclient serialisierte zunächst das Request-Interface statt des konkreten Vertragstyps und erwartete `succeeded` statt des vorhandenen `completed`. Beide Fehler wurden ausschließlich im Probe korrigiert; Transportvertrag und Sicherheitsprüfungen wurden nicht verändert.
- Die erste externe Textübertragung von `(` öffnete keine Completion. Die Wiederholung mit einer einzelnen Taste auf der aktiven Tastaturbelegung besteht in beiden Konfigurationen. Bulk-Textübertragung wird nicht als Nachweis des Tastentriggers gewertet.
- Das abschließende Review ergänzte Dispose des bereits als Editor-Abhängigkeit erzeugten InfoViewModel, damit dessen History-Subscription auch im Dock-Pfad endet. Der native Shutdown-Check prüft dieses Cleanup zusätzlich.
- Zwischenstände einschließlich fehlgeschlagener Tests bleiben unter `artifacts/g1-03/` erhalten. Kein fehlgeschlagener Zwischenlauf wurde nachträglich als bestanden ausgewiesen. Für den final geprüften Stand wurden keine Regressionen festgestellt.

## 10. Geänderte Dateien und Abschlussstatus

G1-03 ergänzt `Shell/DockEditorSession.cs`, `DockEditorSession.Files.cs`, `EditorDockFactory.cs`, `CryptoWorkBenchGuiTest/DockEditorTests.cs` und `DockEditorNativeProbe.cs`. Weiterentwickelt werden `App.axaml.cs`, `CryptoScriptEditView.axaml.cs`, `InfoCompletionController.cs`, `WorkbenchSaveCommit.cs`, `DesktopMcpSession.cs`, die fünf Shell-Dateien Layout/Presentation/Startup/Window-XAML/Window-Codebehind sowie `DockShellNativeProbe.cs`, `eng/gui/native/Program.cs` und `eng/gui/Validate-G1Shell.ps1`. Hinzu kommen dieser Bericht und [G1-03-results.json](evidence/G1-03-results.json).

Keine Änderungen an Parser, AST, Evaluator, Assignment L1, VariableState-Persistenz, InterpreterHostQueue, EditorCoordinator, EditorBridge, STDIO-Protokoll oder dessen Admission. Tools bleiben Platzhalter; keine Output-/Diagnostics-/Variables-/Info-Migration, Mehrdokumentverwaltung oder Layout-Persistenz.

Abschlussstatus einschließlich erhaltener Vorarbeiten: **21 modifizierte getrackte und 35 ungetrackte Dateien**, kein Staging, unveränderter HEAD. `git diff --check` besteht; ungetrackte Dateien wurden zusätzlich auf Inhalt, Whitespace und JSON-Parsebarkeit geprüft. Die drei übernommenen NuGet-Locks ohne abschließenden Zeilenumbruch bleiben unverändert. Vollständiger Git-Status, Ausgangshashes, Erhaltungsprüfung, G1-03-Quelldateihashes, Einzeltests und native Checks stehen in der Ergebnisevidenz. Kein Commit, Push oder Merge.

## 11. Offene Risiken und Übergabe

Der bisherige G1-03-Blocker „Visual-Detach beendet produktive Session“ ist mit getrenntem Session-Eigentum und Tests adressiert. Offen bleiben:

1. Linux/macOS nativ, Multi-Monitor und gemischte DPI: auf diesem Windows-System nicht geprüft. Bestehender produktiver MCP-Lease-Clock ist weiterhin Windows-zertifiziert.
2. Vollständiges Maus-Redock zwischen getrennten OS-Fenstern und die gesamte Adorner-/Drop-Zielmatrix. Factory- und DockManager-Sperren sowie native Fensterwechsel sind geprüft, ersetzen aber diese Interaktionsmatrix nicht.
3. Bekannte optionale Dock-Overlay-Casts und Null-Bindings beim Teardown bleiben sichtbar; keine neue Dock-Dialog-/Confirmation-Servicearchitektur eingeführt. Vor Nutzung dieser Overlays gesondert klären.
4. Windows kann Hintergrund-Foreground-Aktivierung verweigern. Native Tests benötigen ein wirklich aktiviertes Testfenster; kein globaler OS-Workaround.
5. Ressourcenprüfung umfasst wiederholte Zyklen, entfernte Handler/Popups, Host-/DockControl-Abmeldung, idempotentes Dispose, keine verbleibende Registrierung und beendeten Transport. Kein Langzeit-Leak-/Stressnachweis über Stunden.
6. Kein neuer Installed-Codex-Client-Nachweis; 13 Explicit-Fälle bleiben offen. Native File-Picker-Interaktion wurde nicht zusätzlich manuell automatisiert; die produktiven Open-/Save-Commands sind mit asynchronem Storage-Provider und tatsächlichem Dateischreiben geprüft.

**CONDITIONAL GO für G1-04**, vorbehaltlich Eigentümerabnahme und Beibehaltung der genannten Plattform-/Interaktionsgrenzen. Keine Freigabe für eine allgemeine plattformübergreifende Veröffentlichung. Die Arbeit endet mit G1-03; G1-04 benötigt einen ausdrücklichen Folgeauftrag.
