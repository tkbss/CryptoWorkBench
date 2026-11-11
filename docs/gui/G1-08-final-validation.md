# G1-08 – Final Integration, Cross-Platform Validation and Acceptance

Stand: 7. November 2026. Technische Empfehlung: **CONDITIONAL ACCEPTANCE** für den geprüften Windows-Umfang. Die Eigentümerabnahme des G1-Gesamtabschlusses steht aus. Legacy bleibt Standard; die neue Shell startet ausdrücklich mit `--dock-shell`. Kein Commit, Push, Merge oder L1-Import.

Die maschinenlesbaren Einzelresultate, Dateilisten und SHA-256-Werte stehen in [G1-08-results.json](evidence/G1-08-results.json). Rohbelege liegen unter `artifacts/g1-08`; fehlgeschlagene Zwischenläufe bleiben erhalten. [Native Abnahmecheckliste](G1-08-native-acceptance-checklist.md) enthält die nicht ausgeführten Maus- und Plattformprüfungen. Erfolgreiche API-Relokation wird nicht als Maus-Redock ausgewiesen.

## 1. Ausgangsstand und Integrität

Ausschließlich `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Vor Änderungen wurden 1.096 getrackte beziehungsweise nicht ignorierte Dateien gehasht, die 93 vorhandenen uncommitteten Dateien gesichert (33 modifiziert, 60 ungetrackt), der binäre Git-Diff sowie Worktree-/Ref-Zustände gespeichert. Die 1.095 von G1-07 erfassten Quelldateien stimmen mit dem Anfangsstand überein. Das neue Paket behandelt diese Arbeiten als gemeinsame Baseline.

`master` steht bereits auf `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` und enthält inzwischen den L1-Merge; der separate L1-Branch steht auf `a34ffbdc580d00b4d8eb7fabef5b8569099ea512`. Es wurde nichts davon übernommen. Historische Berichte, Evidence, Prototypen und Locks werden gegen den Anfangsstand byteweise per SHA-256 geprüft. Die vollständigen Anfangs- und Endmanifeste sowie der finale Status sind in der Evidence referenziert.

Die anfängliche Regression bestätigt GUI 944, MCP 176 und Core 11.512 in beiden Konfigurationen. Der erste native Release-Docklauf verfehlte 13 Vordergrundprüfungen (54 bestanden); der damalige Wrapper stoppte korrekt. Dieser Lauf wird weder gelöscht noch als vollständige native Baseline ausgegeben. Die endgültigen nativen Wiederholungen nach den Korrekturen sind separat ausgewiesen.

## 2. G1-01–G1-07-Testhistorie

Die vorhandenen sieben Berichte und Evidence-Dateien wurden als historische, bereits bedingt abgenommene Nachweise gelesen und unverändert erhalten. Zahlen gelten je Debug und Release; native Zahlen sind Checks, keine zusätzlichen NUnit-Fälle.

| Paket | Schwerpunkt | GUI bestanden | Core bestanden | Native Windows kumulativ |
|---|---|---:|---:|---:|
| G1-01 | Dock/Prism-Machbarkeit | 797 | 11.508 | 60 |
| G1-02 | Opt-in Shell, Legacy, Fallback | 810 | 11.508 | 150 einschließlich Foundation |
| G1-03 | Autoritativer Editor, MCP | 830 | 11.508 | 263 |
| G1-04 | Variables, Info, Auswahl/Copy | 850 | 11.508 | 329 |
| G1-05 | Output-Pipeline | 871 | 11.512 | 387 |
| G1-06 | Diagnostics/Navigation | 897 | 11.512 | 457 |
| G1-07 | Layout, Maximierung, Restore | 944 | 11.512 | 719 |

MCP bleibt 176 bestanden mit 13 nicht ausgeführten Explicit-Fällen. Core hat 26 Skipped und einen Explicit-Hardwaretest; G1-01 Headless umfasst 12 Fälle. Historische native Vordergrund-/App-Freigabe-Timeouts und der R3-Befund bleiben in ihren ursprünglichen Berichten nachvollziehbar. Ihre grünen Abschlussläufe waren keine Linux-/macOS-Freigabe.

## 3. Unabhängiger Architektur-Review

Prüfpfade: `App.axaml.cs`, `DockEditorSession`, `EditorDockFactory`, `DockShellLayout`, `DockShellWindow`, `DockToolPresentation`, die sechs Pane-Präsentationen sowie die Automation-/Editor-Lebenszyklen. Die bestehende Prism-Anwendung erzeugt einen Container; v1/v2-Automation referenzieren denselben Service. Layout-Restore erfolgt vor dem produktiven Editor-Binding. Ein struktureller Shell-Startfehler darf auf Legacy zurückfallen; ein produktiver Startfehler wird beendet und erzeugt keinen zweiten Interpreter/Editor.

`DockEditorSession` besitzt genau eine View, einen Workbench-Editor und ein Dokument. Logische Pane-IDs und globale ViewModels bleiben beim Float/Redock, Close/Show und Maximize/Restore erhalten. Die Factory verwendet die gemeinsame Coordinator-Beobachtung; Pending, Active, Modalität, Lease und Shutdown sperren Relokation. Ein Layoutwechsel ersetzt keine WorkspaceGeneration, Revision, Undo-Stack oder Editorregistrierung.

Variables werden einmal an den Interpreterzustand angeschlossen; Info verwendet die gemeinsame History. Output und Diagnostics veröffentlichen in ihre Sessionmodelle, auch wenn der jeweilige Presenter geschlossen ist. Wiederanzeigen bindet denselben Zustand. Die neuen Ressourcenprüfungen messen History-/Workspace-Subscriptions vor und nach den Zyklen und verlangen beim Shutdown null verbleibende Registrierungen, Hosts und DockControls. Dispose ist erneut aufrufbar. Das beweist die geprüften Lebenszyklen, keine allgemeine Langzeit-Leakfreiheit.

Die native Integration fand einen echten visuellen Ownership-Fehler bei erneut angewandten AvaloniaEdit-Templates nach File-Open und Editor-Floating. Die gezielte Korrektur löst alte ScrollViewer-/ContentPresenter-Inhalte vor der Zuweisung im Basistemplate. Sie ersetzt weder Editor noch TextArea/TextView noch Dokument. Zwei permanente Regressionen erzwingen beide Templateebenen und prüfen Identität, SaveIntent-Snapshot und Undo/Redo. Details in Abschnitt 13.

## 4. Editor-/MCP-Sicherheitsbewertung

EditorBridge-Registrierung, Coordinator-Tickets, Revision/WorkspaceGeneration, Lease-Admission und Mutationsschutz bleiben die Autorität. Keine G1-08-Produktänderung betrifft diese Admissionregeln. Die R3-Korrektur synchronisiert ausschließlich die Test-Fixture mit dem tatsächlich gestarteten Shutdown-Drain; sie öffnet keine Produzenten erneut.

SaveIntent erfasst Editoridentität und Revision. Native Dialogschritte werden über den vorhandenen File-Intent-Vertrag zugelassen; vor Commit wird Aktualität erneut geprüft. Öffnen lädt Text ohne Interpreterausführung. Der integrierte Test vergleicht den exakten gespeicherten Unicode-Text und bestätigt unveränderte Ausführungsergebnisse beim Wiederöffnen. Shutdown erfasst die normale Präsentationsanordnung vor dem Drain und speichert erst beim bestätigten Schließen. Abgebrochenes Schließen bleibt in der bestehenden Layoutsuite geprüft.

Die unveränderten nativen MCP-Szenarien verbinden den tatsächlichen STDIO-/TLS-Transport, führen eine Editoroperation aus, prüfen Lease-/GUI-/Dock-Sperren, geben frei und prüfen GUI-Fortsetzung sowie Shutdown mit offenen Hosts. Sicherheitsrelevante Regressionen wurden in den abschließenden Suiten nicht gefunden. Die produktive Lease-Uhr ist auf Windows beschränkt; Linux/macOS werden deshalb ausdrücklich nicht für produktive MCP-Leases freigegeben (Abschnitte 7/8).

## 5. Nachgewiesene Ursache des R3-Befunds

Klassifikation: **nachgewiesener Test-/Fixture-Synchronisationsfehler im bereits vor G1 vorhandenen Test**, kein nachgewiesener Produkt-Admissionfehler. Die alte Testvariante erfasste in der IsReadOnly-Benachrichtigung zuerst `DrainAsync()` und rief erst danach `BeginShutdown()` auf. Der erste Drain erfasst die zu diesem Zeitpunkt vorhandenen Verpflichtungen; die spätere Coordinator-Schließaufgabe gehört nicht rückwirkend zu diesem Task. Der Test wartete anschließend nur auf den früheren Drain und nahm vollständigen Shutdown an.

Der permanente Test `R3PreShutdownDrainCannotCertifyLaterCoordinatorCallbacks` hält ein echtes Coordinator-Callback-Ticket fest und erzeugt diese Reihenfolge deterministisch. Nach einem Dispatcher-Fence gilt: `earlyDrain=True; shutdownDrain=False; phase=Releasing; pending=2; active=False; observer=0`. Busy, MutationLocked, IsReadOnly und Document.IsInUpdate sind bereits false. Dennoch ist direkte Mutation während der noch offenen Coordinator-Freigabe richtig verboten: `ExecutionBusyException` aus `WorkbenchTextEditor.OnUpdateFinished`, Zeile 237 des Ausgangsstands. Die Fehlersignatur entspricht dem früher beobachteten `Document.Insert`.

Nach Freigabe des Callback-Tickets und Abwarten des **nach** `BeginShutdown()` angeforderten Drain ist die direkte Mutation des abgelösten Dokuments wieder möglich. Neue Produzenten bleiben weiterhin abgewiesen. Dateischreibzähler bleibt null, Sentinel-Datei und bestätigter Dokumenttext bleiben während der Ablehnung unverändert. Kein Sleep, Retry oder privates Zustandsmanipulieren erzeugt diesen Nachweis.

Die bestehende R3-Fixture wartet nun in den Varianten `shutdown` und `shutdown-throw` zusätzlich auf `h.Service.DrainAsync()` nach Beginn des Shutdown. Alle bisherigen Assertions bleiben erhalten. Die Produktimplementierung von Shutdown/Drain ist unverändert. Der Vergleich mit HEAD zeigt, dass die problematische Testreihenfolge bereits vor den uncommitteten G1-Arbeiten bestand; G1 kann Scheduling beeinflussen, verursacht diese falsche Warteannahme aber nicht.

Begrenzte Matrix vor und nach Korrektur: jeweils Debug/Release, pro Konfiguration vier isolierte Läufe der sechs R3-Varianten plus zwei kombinierte Läufe `EditorReviewCorrectionTests|Dock` (vorher 196, nachher 205 Fälle). Alle 12 Vorher-Läufe bestanden; das ist ausdrücklich kein Ursachennachweis. Der deterministische Ticketablauf und die anschließend grünen 12 Nachher-Läufe sowie zwei vollständige Debug-/Release-Matrizen liefern den Nachweis. Rohbelege: `r3-before`, `red-tests-2`, `r3-after`, `final-2`, `independent-repeat`.

## 6. Vollständiger Windows-Integrationstest

Die neuen Modi `integration`, `integration-input` und `integration-restart` erweitern ausschließlich den vorhandenen G1ShellProbe. Es gibt keine zweite native Testinfrastruktur. Der externe Debug-Ablauf `integration-Debug-3.json` besteht 28 Checks; der zugehörige echte Prozessneustart `integration-restart-Debug-3.json.integration.json` besteht sieben Restore-Checks und anschließend die unveränderten 54 Editor-/MCP-Checks.

| Ablauf | Tatsächlich ausgeführter Nachweis |
|---|---|
| Eingabe und Enter | Externe Tastatureingabe von `VAR integrated=0x(AB) Print(0x(CD)) Info(HASH-SHA256)` und Return; Variables, Output und InfoDoc geprüft |
| Info/Diagnostics | Copy-Example-Button programmgesteuert mit echtem Clipboard; Syntax-/semantische Meldungen, Quellnavigation, Stale-Ablehnung, Fortsetzung |
| Editor | Undo/Redo, Line History und stabile Dokumentidentität |
| Datei | Native Save-/Open-Dialoge per externer Tastatur/Maus; exakter Text `// G1-08 ä€😀` plus kontrolliertes Script; keine Ausführung beim Öffnen |
| Layout | Alle sechs Bereiche nativ gefloatet/angedockt, Maximize/Restore, Info floating und Status geschlossen hinterlassen |
| Shutdown/Restore | Kontrolliertes Schließen, gespeichertes normales Layout, neuer Prozess; Info floating, Status geschlossen; kein fachlicher Zustand geladen |
| MCP | Anschließend echter Transport, Operation, Lease-Sperren, Release, GUI-Fortsetzung und Shutdown mit offenen Hosts |

Die anderen Operationen im integrierten Ablauf sind bewusst programmgesteuert. Die getrennten externen Baseline-Modi decken reale Mausauswahl, Copy Example, Clipboard, Return/KP_Enter, Undo/Redo, History, Completion, Diagnostics-Filter/Quellbuttons und Layoutmenüs ab. Die JSON-Felder unterscheiden diese Eingabearten. Native Dateidialoge wurden nicht durch Datei-API-Aufrufe als externe Bedienung ausgegeben.

Zusätzlich wurde die tatsächliche Release-Executable `CryptoWorkBenchAvalonia.Desktop.exe --dock-shell` gestartet, nach separater Computer-Use-App-Freigabe sichtbar aktiviert und extern mit `VAR desktopSmoke=0x(AB) Print(0x(CD))`/Return bedient. Variables zeigte `desktopSmoke = 0x(AB)`, Output `out: 0x(CD)`. Schließen über die Titelleiste beendete den Prozess. Die zuvor nicht vorhandene, dabei neu erzeugte Benutzerlayoutdatei wurde als Rohbeleg gesichert und nur nach Vergleich ihres exakten Hashes wieder entfernt; keine vorherige Benutzerdatei wurde verändert. Beleg: `product-desktop-smoke.json` und `product-desktop-layout.json`. Der erste Zugriff auf diese separate App lief in einen Freigabe-Timeout; nach Nutzerfreigabe wurde die Sichtprüfung vollständig ausgeführt.

Schweizer Eingabe: Release verwendete getrennt Literal-`ä`, die echte Tastenkombination `Alt_R+e` für `€` und Literal-`😀`. Die Probe bestätigte exakt `ä€😀`; drei Undo-/Redo-Schritte entsprechen diesen drei getrennten Eingaben. `Shift_L+8` löste die Klammer-Completion aus. Debug und Release prüften Return und KP_Enter extern. Das ist kein Nachweis aller möglichen AltGr-Kombinationen.

Die ersten Integrationsversuche sind erhalten: eine ungeeignete virtualisierte Info-Codeblock-Fixture, ein vor Loaded wartender synthetischer Enter-Pfad, ein fehlendes Restart-Fixture und die beiden echten Template-Ownership-Abstürze. Der finale Ablauf verwendet einen sichtbaren kurzen Copy-Codeblock, wartet auf Loaded, und kopiert explizit das vorher gespeicherte Layout für den Neustart. Die beiden Produktabstürze wurden separat deterministisch reproduziert und korrigiert.

## 7. Linux-Nachweise und offene Anleitung

**Nicht nativ validiert.** Hier steht nur ein Windows-Desktop zur Verfügung; WSL ist nicht installiert, Docker ist nicht verfügbar, ein autorisierter Linux-Desktop/Runner wurde nicht gefunden. Windows-Läufe mit Linux-Verzeichnisnamen oder ein Xvfb-Start würden keine native Fokus-/Clipboard-Abnahme ersetzen.

Das neue `eng/gui/Validate-G1Platform.ps1` ruft die bestehenden Locked-Restore-/Build-/Testscripte für Debug und Release sowie die unveränderten zwölf Prototyp-Headlesstests auf. Es verlangt ein neues Evidence-Verzeichnis innerhalb des Checkouts, schreibt OS/Architektur und startet mit `-Desktop` optional die tatsächliche Desktop-Anwendung. Auf Linux wird dafür DISPLAY/WAYLAND_DISPLAY verlangt. Das Skript wurde auf Windows mit beiden Konfigurationen ausgeführt; seine Linux-Ausführung bleibt offen. Befehle, Voraussetzungen und zwölf konkrete Desktop-Schritte stehen in der Checkliste.

Zusätzlich konkreter Blocker für produktives MCP: `WorkbenchLeaseV2.cs` verwendet standardmäßig nur unter Windows `GetTickCount64`; andere Betriebssysteme führen zu `PlatformNotSupportedException` und sicherer Lease-Ablehnung. Tests mit injizierter Uhr beweisen keine produktive Linux-Lease. Eine geeignete suspendzählende Plattformuhr erfordert einen gesonderten Sicherheitsnachweis; G1-08 ändert diesen Vertrag nicht.

## 8. macOS und CI-Kompatibilität

**Nicht auf macOS ausgeführt; produktive MCP-Leases durch dieselbe Plattformuhr blockiert.** Keine angemeldete macOS-GUI und kein autorisierter Runner für den uncommitteten Arbeitsstand vorhanden. Der read-only GitHub-Abruf für `feature/gui-g1` fand keinen Actions-Lauf. Ein vorhandener Lauf von master würde diesen lokalen Quellstand ohnehin nicht zertifizieren.

Review von `.github/workflows/dotnet.yml`: Windows x64, Linux x64, macOS arm64 (`macos-26`) und x64 (`macos-26-intel`); SDK 10.0.401; Locked-Build beider Solutions in Debug und Release; vollständige Release-Core-/GUI-/MCP-Suiten, Coordinator/F1/C1, native F01-Dateiveröffentlichung und RID-Publish mit Asset-/InfoDoc-Prüfung. Trigger sind Push/PR nach master. Die CI führt derzeit weder alle G1-Native-Desktopchecks noch die separate G1-Prototypsuite automatisch aus. Beide-Konfigurationen-Testmatrix kann über den vorbereiteten Plattformwrapper ergänzt werden, sobald ein autorisierter Checkout/Runner vorliegt.

Paketstände und RID-Locks bleiben unverändert: Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock/Serializer 12.1.0.13 und vorhandene gepinnte Prism-Integration. macOS-Publish verwendet den bestehenden Xcode-/Native-Shim-Pfad und muss auf dem tatsächlichen Mac erfolgen. Windows-Restore mit gepinnten Paketen ist keine macOS-Runtimebestätigung. Kein CI-Push, keine Pipelineänderung und kein plattformfremder Publish als Erfolgsnachweis.

## 9. Cross-Window-Maus-Redock

**Offen, nicht als bestanden gezählt.** Die verfügbare Windows-Computer-Use-API bietet eine `drag`-Geste zwischen Koordinaten eines Zielfensters, aber keine getrennte MouseDown/Move/MouseUp-Folge zwischen mehreren nativen Fenstern. API-Float/Redock, native Owner-/Fokuschecks und externe Menübedienung bestehen; sie ersetzen keinen Mausnachweis.

Die Checkliste enthält M1–M8 mit genauer Bedienung und Erwartung: Herausziehen, Rückkehr ins Hauptfenster, Transfer zwischen Hosts, sichtbare Drop-Zonen, Escape-Abbruch, Editor-Floating, Lease-/Busy-Ablehnung und gültiger Neustart danach. Alle Ergebnisfelder bleiben OFFEN. Es wurden keine außerhalb des Zielfensters erfundenen Koordinaten oder eigenen Eingabe-Injektoren verwendet.

## 10. Layout-/Persistenzsicherheit

Unabhängiger Review und vorhandene G1-07-Regressionen bestätigen Schema 1, feste Pane-/Dock-IDs, Typ-/Struktur-Whitelist, doppelte Property-/ID-Ablehnung, feste Closed-Inventory, maximal 256 KiB JSON, Tiefe 64 beim Parser sowie 20 Ebenen/100 Knoten im Dock-Baum. Unbekannte Typen und ungültige Floating-Hierarchien werden vor Host-/Editorerzeugung verworfen. Die neue Null-Floating-Entry-Regression schließt eine zuvor durchrutschende NullReferenceException gezielt als InvalidDataException ab; der Standard-Dock-Aufbau bleibt erhalten.

Die Speicherung projiziert ausschließlich Präsentationsstruktur/Geometrie. Gleicher-Verzeichnis-Tempdatei und atomarer Austausch schützen das bisherige Layout bei Schreibfehlern. Gespeichert wird die normale Anordnung hinter einer Maximierung; Restore/Reset, geschlossene Tools, Offscreen-Koordinaten und MainWindow-/Floating-Geometrie bleiben geprüft. Gezielte native Neustarts verwenden extreme positive/negative Koordinaten und maximierten Zustand; erreichbare Begrenzung wird geprüft. Benutzerlayoutdateien werden von den Probes nicht verwendet.

Der neue Live-Sentinel-Test setzt echten Quelltext, Variable/Print mit `CAFEDEADBEEF`, Info-Inhalt, MCP-Callername, private Pane-Titel, InstanceId und EditorId, floatet und maximiert und untersucht den serialisierten Snapshot. Keiner der Werte erscheint. Das ist ein konkreter Sentinelnachweis, kein Durchprobieren sämtlicher Geheimnisarten; die strikte Strukturprojektion verhindert die Aufnahme von Schlüsseln, Credentials, Interpreter-/VariableState und sonstigen Businessobjekten. ScriptBook-Format und VariableState-Persistenz bleiben unverändert.

## 11. Ressourcen, Belastung und Bindingmeldungen

Neuer begrenzter Ressourcenlauf: drei Sessionstarts × zwölf Zyklen × sechs Pane-IDs = 216 Float/Maximize/Capture+Decode/Restore/Redock-Sequenzen, Close/Show für Tools. Innerhalb jedes Zyklus werden Hosts/DockControls, Dokument/Undo/Snapshot/Generation, Event-Subscriptionzahlen sowie Pending/Active geprüft. Capture/Decode ist hier eine In-Memory-Runde; physische Save-/Load-Runden stammen zusätzlich aus den Layout-/Neustartprobes und werden nicht als 216 Festplattenspeicherungen ausgegeben. Ausführung mit geschlossenen Variables/Output veröffentlicht korrekt; nach Wiederanzeigen erscheinen dieselben Ergebnisse. Shutdown mit zwei Hosts entfernt alle Registrierungen/Hosts/Subscriptions; erneutes Dispose bleibt sicher.

Gemessene verwaltete Bytes nach erzwungenem GC, jeweils drei Starts: final Debug 32.442.904 → 49.852.888 → 61.880.264; final Release 28.151.808 → 39.329.192 → 42.765.760. In der unabhängigen Wiederholung Debug 28.100.272 → 28.181.312 → 28.650.856, Release 27.426.216 → 27.250.344 → 27.977.056. Die ersten Läufe zeigen Wachstum, die Wiederholung keinen vergleichbaren Trend. Gesamtprozess-/Framework-Caches und Testreihenfolge sind damit nicht getrennt; kein monotones produktives Leak bewiesen, aber auch keine Leakfreiheit. Langzeitprofiling bleibt ein mittleres Restrisiko. Output-Eintrags-/Sequenzhistorie lebt bis Session-Dispose; Clear ist keine Garantie konstanter Langzeitspeichernutzung.

Dock-Bindings wurden im gepinnten Quellstand untersucht: `Dock.Avalonia.Themes.Fluent/Controls/OverlayLayerDefaults.axaml` deklariert kompilierte Bindings auf `IHostOverlayServices` für Dialogs, Confirmations, Busy und die globalen Gegenstücke, jeweils mit Null-Fallback. Der verwendete RootDock stellt diese optionalen Services nicht bereit. Für G1s eigene Dialog-/Busy-Verträge werden diese Overlays nicht verwendet; die entsprechenden Meldungen haben in den bestandenen Szenarien keinen nachgewiesenen Funktionsverlust. Null-DataContext-Meldungen beim Teardown korrelieren mit Entfernen der Views; die Host-/Subscriptionprüfungen zeigen keine verbleibende Registrierung.

Drag-Drop-Adorner sind ein anderer Pfad (`Dock.Avalonia/Internal/AdornerHelper.cs`, DockAdorner/GlobalDockAdorner, DockTarget/GlobalDockTarget). Ihre Sichtbarkeit wurde mangels Cross-Window-Geste nicht zertifiziert. Deshalb kein pauschales Urteil „alle Overlaymeldungen harmlos“. Die tatsächlichen AvaloniaEdit-Visual-Parent-Abstürze waren Produktfehler und wurden korrigiert, nicht als Bindingrauschen abgetan. Kein Dock-Fork/Overlayumbau nur zur Logunterdrückung.

## 12. Endgültige Debug-/Release-Testmatrix

Die abschließenden Resultate und eine unabhängige Wiederholung werden in der Evidence aus TRX-/Probe-Dateien ausgewertet. `final-2` enthält Locked-Restore und beide vollständigen Builds sowie alle vier Suiten; `independent-repeat` startet jede Suite erneut in einem eigenen Testlauf auf unveränderten finalen Binaries. Es gibt keine pauschalen Test-Retries.

| Suite | Debug Passed | Release Passed | Failed | Skipped je Konfiguration | Explicit nicht ausgeführt |
|---|---:|---:|---:|---:|---:|
| GUI vollständig | 953 | 953 | 0 | 0 | 0 |
| davon G1-07-Bestand | 944 | 944 | 0 | 0 | 0 |
| davon G1-08 zusätzlich | 9 | 9 | 0 | 0 | 0 |
| MCP | 176 | 176 | 0 | 0 | 13 |
| Core | 11.512 | 11.512 | 0 | 26 | 1 |
| G1-01 Headless | 12 | 12 | 0 | 0 | 0 |

Alle acht unabhängigen Suite-Wiederholungen bestehen mit denselben Zahlen. TRX-Gesamtzahlen enthalten die nicht ausgeführten Explicit-Fälle (MCP 189, Core 11.539); diese werden nicht zu Passed addiert. Die bekannte Ausnahmezuordnung bleibt unverändert.

Native Baseline pro Konfiguration: Foundation 60; Dock 67; Legacy 10; Fallback 13; Editor 54; Startfehler 5; Tools 32; Output 29; Diagnostics 35; Layout 48; Restart 56; Invalid 49; Offscreen-Geometrie 56 = **514 programmgesteuerte Checks**. Externe Modi: Editor 54, Tools 34, Output 29, Diagnostics 35, Layout 53 = **205 Checks in Szenarien mit echten Eingabepunkten**. Die vollständige G1-07-Baseline beträgt damit 719 je Konfiguration. Finale Einzelpfade/Exitcodes und zusätzliche Integrationsergebnisse stehen in der Evidence; externe Szenarien enthalten weiterhin programmgesteuerte Fixturevorbereitung.

Alle **719/719 je Konfiguration** sind final bestanden. Debug: `native-final-Debug` und `input-final-Debug`. Release: `native-final-Release`, `input-final-Release` für Editor/Diagnostics/Layout sowie `input-review-Release` für Tools/Output. Die jeweils 514 programmgesteuerten Checks wurden anschließend in `native-repeat-Debug` und `native-repeat-Release` unabhängig vollständig wiederholt: erneut null Fehler. Die 205 externen Checks pro Konfiguration werden nicht als zweimal vollständig extern wiederholt behauptet.

Zusätzlich Debug: integrierter API-Ablauf 28/0, externer Ablauf 28/0, dazugehöriger Restart 7/0 plus 54/0 MCP/Editor. Release: integrierter API-Ablauf 28/0 und Restart 7/0 plus 54/0 MCP/Editor. Diese Ergänzungen werden nicht in die unveränderte 719er-Baseline eingerechnet. Der frühere Debug-API-Restart besteht ebenfalls, ist aber kein weiterer unabhängiger Funktionsumfang.

Zwei externe Release-Zwischenläufe bleiben Fehlbelege: Tools 13/2 mit unvollständiger/fehlender Info-Auswahl am Clipboard-Prüfpunkt, Output 28/1 nach einer vom Bedienagenten versehentlich doppelt gesendeten Return-Eingabe (`undo-does-not-reexecute-False`). Die gezielte Wiederholung startete die Textgeste innerhalb des sichtbaren Textes, prüfte die vollständige Emoji-Auswahl und sendete genau einen Return je Prüfpunkt. Ergebnis Tools 34/0 und Output 29/0 bei unverändertem Produktcode, Testcode, Zeitgrenzen und Assertions. Die UI-Settle-Wartezeiten der externen Bedienung sind keine Sleeps zur R3-Fehlerkaschierung.

## 13. Korrekturen und permanente Regressionen

| Befund | Kleine Korrektur | Nachweis |
|---|---|---|
| R3 wartet auf vor Shutdown erfassten Drain | Test wartet zusätzlich auf Shutdown-Drain; Produktadmission unverändert | deterministisches Callback-Ticket, alte Fehlersignatur, 12 Vorher-/12 Nachher-Läufe |
| Null in Floating-Window-Liste | explizite Nullvalidierung vor Zugriff | Rot: NullReferenceException; Grün: sichere InvalidDataException/Defaultlayout |
| Diagnostics-Filter ragen bei 220 px hinaus | horizontaler StackPanel wird WrapPanel, Buttonabstand | Rot: rechte Filterkante 320 > 220; Grün: alle Filter innerhalb View |
| AvaloniaEdit erhält bei Retemplate alte Visual-Parents | alte ScrollViewer.Content und PART_CP.Content vor Basistemplate lösen | zwei deterministische Rot/Grün-Fälle; nativer File-Open/Float-Ablauf vollständig grün |

Neun neue GUI-Fälle: R3 (1), Null-Floating (1), schmale Filter (1), Ressourcen (1), Live-Sentinel (1), rasche aufeinanderfolgende Dock-Kommandos mit/ohne Info-Beispiel (2), Editor-/TextArea-Retemplate (2). Bestehende Assertions werden nicht abgeschwächt oder deaktiviert.

Upstream-Abgleich: [AvaloniaEdit 12.0.0 TextEditor](https://raw.githubusercontent.com/AvaloniaUI/AvaloniaEdit/12.0.0/src/AvaloniaEdit/TextEditor.cs) weist im Templatepfad TextArea dem neuen ScrollViewer zu; [TextArea](https://raw.githubusercontent.com/AvaloniaUI/AvaloniaEdit/12.0.0/src/AvaloniaEdit/Editing/TextArea.cs) weist TextView dem neuen PART_CP zu. Die lokalen Overrides lösen ausschließlich die vorherige visuelle Besitzbeziehung. Keine Abhängigkeitsversion wird geändert.

Erhaltene technische Zwischenfehler: Debug-Assets fehlten zunächst nach Release-Restore (AttachDeveloperTools); eine Testdatei hatte vor Ergänzung den nötigen Namespace nicht; ein generischer NameScope-Aufruf benötigte den passenden Cast; Native-Builds trafen eigene laufende Probe-/Testhost-Dateisperren. Diese Build-/Fixturefehler sind in den Rohlogs getrennt von den Produktbefunden und finalen Läufen erfasst.

## 14. Vollständige Liste geänderter Dateien

Gegenüber der gesicherten G1-07-Arbeitsbaseline, nicht gegenüber dem älteren HEAD:

| Datei | Änderung |
|---|---|
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | alte ScrollViewer-Visualbeziehung beim Retemplate lösen |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | alten PART_CP-Inhalt beim Retemplate lösen |
| `CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs` | Null-Floating-Eintrag ablehnen |
| `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs` | Filter umbrechen |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs` | partial und korrekter Shutdown-Drain in bestehender R3-Fixture |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs` | neuer deterministischer R3-Nachweis |
| `CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs` | acht zusätzliche Integrations-/Ressourcen-/Layoutfälle |
| `CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs` | integrierter Ablauf und Restart im vorhandenen Probe |
| `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs` | zusätzliche Modusverzweigung |
| `eng/gui/native/Program.cs` | neue Probe-Modi zulassen |
| `eng/gui/Validate-G1Platform.ps1` | reproduzierbarer Plattformwrapper |
| `docs/gui/G1-08-final-validation.md` | dieser Abschlussbericht |
| `docs/gui/G1-08-native-acceptance-checklist.md` | offene manuelle/Plattform-Abnahme |
| `docs/gui/evidence/G1-08-results.json` | neue strukturierte Evidence |

Die Evidence listet außerdem den vollständigen kumulativen Git-Status einschließlich aller älteren uncommitteten G1-Dateien, Endhashes und Baseline-Differenzen. Final: 36 modifizierte und 67 ungetrackte Dateien; gegenüber der Arbeitsbaseline sieben vorhandene Dateien geändert und sieben neu hinzugefügt. Alle 90 geschützten Dateien bleiben bytegleich. `master` und der L1-Worktree sind weiterhin sauber und auf denselben HEADs. `git diff --check` sowie zusätzliche Whitespace-/Textprüfungen der neuen G1-08-Dateien bestehen. Ignorierte Rohbelege, Wrapper und Testdateien unter `artifacts/g1-08` sind separat inventarisiert; historische Belege wurden nicht entfernt. Der Eigenhash der Evidence steht separat in `artifacts/g1-08/final-evidence.sha256`, damit kein zirkulärer Selbsthash behauptet wird.

## 15. Bekannte Probleme mit Schweregrad

| Schwere | Befund | Status/Auswirkung |
|---|---|---|
| Hoch, Plattformblocker | produktive MCP-Lease-Uhr nur Windows | Linux/macOS-Leases fail-closed; separate Uhrimplementierung/-zertifizierung erforderlich |
| Mittel | Cross-Window-Maus, Drop-Zonen und Drag-Abbruch nicht ausführbar | M1–M8 offen; keine uneingeschränkte Windows-Drag-Abnahme |
| Mittel | keine native Linux-/macOS-Sitzung/kein getesteter finaler Runner | dort weder Build/Headless noch native Freigabe behauptet |
| Mittel | Langzeitressourcen, initiales GC-Speicherwachstum | kurze Wiederholung stabiler, Ursache der Gesamtheap-Unterschiede nicht isoliert; Profiling offen |
| Niedrig im geprüften Umfang | optionale Service-Overlay-/Teardown-Bindings | keine beobachtete G1-Funktionsstörung; Drag-Adorner separat offen |
| Behoben | R3-Fixture, Null-Floating, Filterclipping, beide Retemplate-Ownership-Fehler | gezielte Regressionen und finale Matrix grün |

Kein ungeklärter sicherheitsrelevanter Coordinator-/MCP-Race wird als akzeptiert behandelt. Der untersuchte R3-Ablauf hat einen konkreten Fixture-Nachweis; die Plattformuhr wird als ausdrücklicher Blocker ausgewiesen.

## 16. Restrisiken und Eigentümerentscheidungen

Der Eigentümer entscheidet über den technischen Abschluss im bestätigten Windows-Umfang, die manuelle Ausführung von M1–M8, Linux-/macOS-Runner und Desktopabnahme sowie ein gesondertes MCP-Plattformuhrpaket. Langzeitprofiling sollte vor einer breiten Nutzung mit dauerhaft umfangreicher Output-/Info-Historie erfolgen. Eine Entscheidung zum Wechsel der Standard-GUI oder zur Integration nach master ist getrennt erforderlich. Dieses Paket führt beides nicht aus.

## 17. Empfehlung für den G1-Gesamtabschluss

**A – Technischer Abschluss: CONDITIONAL ACCEPTANCE.** Die integrierte Windows-Shell, Editor-/MCP-Verträge, korrigierten Integrationsmängel und vollständigen Regressionen liefern eine belastbare Grundlage zur Eigentümerabnahme. Die offenen Interaktions-/Plattform-/Langzeitnachweise verhindern eine uneingeschränkte Gesamtfreigabe.

**B – Plattformfreigabe:** Windows: native Validierung im dokumentierten automatisierbaren Umfang bestanden; Cross-Window-Mausabnahme offen. Linux: nicht validiert, produktive MCP-Leases zusätzlich durch die Windows-exklusive Uhr blockiert. macOS: nicht validiert, derselbe konkrete MCP-Blocker; CI-/Paketreview ist kein tatsächlicher Plattformlauf.

Nach G1-08 wird gestoppt. Kein Commit, Push, Merge, Standard-GUI-Wechsel oder nächstes Featurepaket. Der G1-Gesamtabschluss und jede spätere master-Integration benötigen ausdrückliche Eigentümerabnahme.
