# G2-I02 – Final Local Master Integration / F1-Abschluss

Datum: 12.11.2026

**G2-I02 COMPLETE – LOCAL MASTER INTEGRATED**

## 1. Merge und Ausgangszustände

Der in G2-I02 autorisierte lokale Merge ist abgeschlossen. F1 hat keinen Merge oder Commit wiederholt. Keine Produktdatei, bestehende Testdatei oder Paketkonfiguration wurde geändert. Kein Push, Pull Request, Rebase, Squash oder Remote-Merge.

| Gegenstand | SHA |
|---|---|
| Ursprünglicher master / Parent 1 | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| GUI-Branch / Parent 2 | `d611da8d765cf3045ec344bdc7135dacf25a5928` |
| Merge-Commit / finaler master / integration/g2-i01 | `787b9f2974edab4b4d7a9702e853cd5460297802` |
| Gemeinsamer finaler Git-Tree | `da118fde6d8ee2b1a36288f3762570b0b4e229ac` |

Commit-Nachricht: `Integrate G1/G2 dock GUI with L1 assignments`.

Der vorhandene Merge wurde auf `integration/g2-i01` mit genau den zwei angegebenen Parents in dieser Reihenfolge abgeschlossen. Anschließend erfolgte im sauberen Hauptcheckout `git merge --ff-only integration/g2-i01`. Es wurde kein zweiter Merge-Commit erzeugt. In einer lokalen Nachprüfungsroutine wurde nach dem erfolgreichen Commit ein PowerShell-Ausdruck korrigiert; der Commit wurde nicht erneut ausgeführt, sondern anschließend separat vollständig verifiziert.

Vor dem Commit wurden exakt die zwei freigegebenen Dateien hinzugefügt: `CryptoWorkBenchGuiTest/DockAssignmentIntegrationTests.cs` und `docs/gui/G2-I01-integration-validation.md`. Der SHA-256 der validierten Testdatei im Integrations-Worktree war exakt `E94193B7573EC28FE1D3FB40CF8D334322AE188F95882D3A2F9E4102473F941E`.

## 2. Quell- und Git-Integrität

- 166 Änderungspfade gegenüber Parent 1: 128 neue, 38 geänderte, keine gelöschten Dateien. Davon 164 unverändert übernommene GUI-Pfade und die zwei freigegebenen Integrationsdateien. Vollständiges Inventar in Anhang A.
- Vollständiger Kandidatenindex und Commit-Tree mit 1.183 Pfaden verglichen: identisch. Auch die 1.183 physischen Master-Dateien wurden unter Anwendung der Git-Clean-Filter/Zeilenendennormalisierung gegen ihre Commit-Blobs gehasht: keine Abweichung.
- master und Integrationsbranch zeigen auf denselben Commit und Tree. GUI-Branch, andere lokale Branches und Remote-Tracking-Referenzen bleiben unverändert. Die GUI-Indexdatei ist unverändert.
- Hauptcheckout war nach Fast-Forward und beim F1-Start sauber. Abschließend ist ausschließlich dieser ausdrücklich zugelassene Bericht untracked; der Index ist sauber. Integrations-Worktree bleibt sauber.
- Geschützte Worktree-Status wurden mit `GIT_OPTIONAL_LOCKS=0` ausschließlich lesend geprüft. `gui-g1`, `g1-i01-composition` und `script-assignment` sind sauber. Bereits vorhandene 47 Statuseinträge in `prism-local-nuget` und 58 in `ar-01` blieben unverändert; sie wurden weder übernommen noch bearbeitet. Einzelstatus in den lokalen Nachweisen.
- Alle 397 G2-I01-Nachweisdateien waren vor dem Merge unverändert. Beim F1-Start wurden zusätzlich sämtliche 136 vorhandenen G2-I02-Nachweisdateien inventarisiert und anschließend auf Erhalt geprüft. Alte Logs und Fehlversuche werden nicht überschrieben.
- Commit-Diff-Whitespace-Prüfung: Exitcode 2 mit exakt 20 genehmigten historischen EOF-Befunden. Diese stammen aus unveränderten GUI-Blobs und wurden nicht bereinigt. Der leere getrackte Working-Tree-Diff hat Exitcode 0.

## 3. Builds und vollständige Regressionen auf master

Die folgenden Läufe stammen aus G2-I02 nach dem tatsächlichen Master-Fast-Forward, nicht aus der historischen G2-I01-Baseline. F1 hat ihre TRX-Dateien und gespeicherten SHA-256 geprüft; die erfolgreichen Suiten wurden nicht unnötig neu ausgeführt.

Offizieller Prism-Pin `358118cd640d9a22ff8cf21c8ad197fa038b7990`, 1.180 gepinnte Dateien verifiziert; SDK 10.0.401. Locked Restore und Builds beider Lösungen sowie separater Locked Restore/Build des nativen Runners in Debug und Release erfolgreich.

| Konfiguration | Buildfehler | Warnungen erste / zweite Lösung | Neue Warnungen gegenüber G2-I01 |
|---|---:|---:|---:|
| Debug | 0 | 85 / 0 | 0 |
| Release | 0 | 91 / 0 | 0 |

Die vollständigen nach Repository-Pfad normalisierten Warnungsmengen stimmen mit G2-I01 überein.

| Suite | Debug PASS | Release PASS | NotExecuted je Konfiguration |
|---|---:|---:|---:|
| Core | 11.723 | 11.723 | 27 |
| GUI | 1.131 | 1.131 nach Wiederholung | 0 |
| MCP | 176 | 176 | 13 |

Der erste Release-GUI-Lauf hatte 1.130 PASS und 1 Failed: `ShortPrintIsVisibleAndSourceButtonNavigates(911,512)` scheiterte vor dem Testkörper in `Dispatcher.VerifyAccess → DefaultRenderLoop.Add → AvaloniaHeadlessPlatform.Initialize → HeadlessUnitTestSession.EnsureIsolatedApplication`. Der vollständige unveränderte Wiederholungslauf ohne Neubuild bestand mit 1.131 PASS. Beide TRX-Dateien, Logs und Exitcodes bleiben erhalten. Dies ist kein Nachweis einer Behebung des intermittierenden Fehlers.

Die 40 NotExecuted je Konfiguration bleiben separat: Core-Symlink-Privilegien, Darwin-spezifische Prüfungen und manuelle Hardwaremessung sowie explizite MCP-Installed-Codex-/Pairing-Szenarien ohne passende externe Testumgebung. Keine dieser Prüfungen wird als bestanden erklärt.

Der fallweise Vergleich gegen G2-I01 zeigt in beiden Konfigurationen keine fehlenden oder zusätzlichen Fälle und keine Ergebnisänderungen in den erfolgreichen Läufen: Core 11.750, GUI 1.131, MCP 189 Fälle einschließlich NotExecuted. Gleichnamige Datenfälle bleiben als Multimenge erhalten; ausschließlich belegte Laufzeitzeitpunkte in MCP-Contract-Anzeigenamen werden normalisiert. Alle vier neuen L1-/Dock-Kombinationstests sind je Konfiguration Passed: docked/floating sowie gültige/partiell fehlgeschlagene MCP-Zuweisung. Sie prüfen atomaren Variablenersatz, Variables, Print/Quellnavigation, Diagnose ohne Mutation, History/Undo/Redo, Lease/Redaction/Idempotenz und Wiederherstellung des globalen Variablenbestands.

## 4. Native Windows-Erstversuche und F1-Fortsetzung

Die ursprüngliche G2-I02-Phase schloss zwölf Debug-Szenarien mit 612 Checks und Exitcode 0 ab. Danach wurde Computer Use vom Benutzer mit Escape beendet. `Debug-layout-invalid` hatte bereits Ausgabedateien, aber kein abschließendes Ergebnis-JSON und keinen dokumentierten erfolgreichen Exitcode: der ursprüngliche Versuch bleibt **INTERRUPTED / NOT VERIFIED**. Die begonnenen Logs und die ungültige Layoutdatei sind erhalten. Die Release-Phase, vier F8-Szenarien und zwei direkte Desktop-Smokes waren noch nicht ausgeführt.

F1 fand keine verbliebenen zugehörigen Prüfprozesse. Die zwölf abgeschlossenen Debug-Szenarien wurden nicht wiederholt. Zuerst wurden die übrigen 26 automatisierten Szenarien ohne Computer Use, externe Eingaben oder alternative Eingabemethoden ausgeführt: alle Exitcode 0, keine Timeouts, keine fehlgeschlagenen Assertions. Darunter ist die vollständig neu protokollierte Prüfung `Debug-layout-invalid`; ihr Erfolg ersetzt nicht rückwirkend den unterbrochenen Erstversuch.

Damit sind vor den interaktiven Ergänzungen je Konfiguration 19 automatisierte Szenarien, 867 Checks und 18 Layout-Captures nachgewiesen. Die G2-I01-Referenz von 964 Checks je Konfiguration enthält zusätzlich 48 Independent-Windows- und 49 Independent-Restart-Checks. Historische G2-I01-Ergebnisse werden nicht als neue Master-Ergebnisse ausgegeben.

Für die vier F8-Floating-/Neustartprüfungen und die beiden Desktop-Start-/Schließprüfungen wurde nach Abschluss der automatisierten Phase ausdrücklich erneut um Freigabe gebeten. Der Eigentümer antwortete: „Ja, diese Computer-Use-Prüfungen freigeben“. Die Freigabe ist separat dokumentiert und betrifft ausschließlich diese Prüfungen. Keine erneute Merge- oder Commit-Freigabe wurde angefordert.

### Tatsächlich nachgewiesene native Szenarien

Die vier ergänzenden Floating-/Neustartprüfungen sind abgeschlossen: Debug 48/48 und 49/49, Release 48/48 und 49/49. Beim ersten Release-Independent-Windows-Versuch lief die 90-Sekunden-Frist auf F8 während der Sitzungsfortsetzung ab. Das Ergebnis enthält 13 erfolgreiche Vorprüfungen und eine `TimeoutException` in `DockIndependentWindowNativeProbe.cs:80`, Prozess-Exitcode 1. Der äußere 180-Sekunden-Prozesswächter lief nicht ab (`timedOut=false`). Dieser Erstversuch ist fehlgeschlagen und bleibt unverändert in `artifacts/g2-i02/independent-f1/Release-independent-windows.json` samt Command-/Trace-Dateien erhalten. Der unveränderte Wiederholungslauf in `artifacts/g2-i02/independent-f1-retry-1/` bestand nach rechtzeitigem F8 mit 48/48; dort liegt auch die anschließend erstmals ausgeführte erfolgreiche Release-Neustartprüfung mit 49/49. Kein Neubuild und keine Quelländerung. Der Timeout ist kein Nachweis eines neuen Docking- oder Fensterzustandsfehlers und wird nicht durch den erfolgreichen Folgelauf verdeckt.

Insgesamt sind 42 native Szenarien mit 1.928 erfolgreichen Checks und 36 Layout-Captures nachgewiesen. Debug und Release des tatsächlichen Desktop-Programms wurden zusätzlich sichtbar mit Dock-Shell gestartet und über Alt+F4 regulär geschlossen: beide Exitcode 0, beide Layoutprofile hashidentisch wiederhergestellt. Die abschließende Computer-Use-Fensterliste und die separate Prozessprüfung fanden keine verbliebenen zugehörigen Fenster oder Prozesse.

PASS setzt Exitcode 0, keinen Timeout, keine JSON-Fehler und die vollständige erwartete Check-/Capture-Anzahl voraus. NOT VERIFIED ist kein PASS. Layout-Captures werden nicht zu den Assertions addiert.

| Szenario | Debug | Release | Checks je Konfiguration | Layout-Captures je Konfiguration |
|---|---|---|---:|---:|
| corrections-layout | PASS | PASS | 0 | 9 |
| diagnostics | PASS | PASS | 35 | 0 |
| dock | PASS | PASS | 67 | 0 |
| editor | PASS | PASS | 54 | 0 |
| fallback | PASS | PASS | 13 | 0 |
| independent-restart | PASS | PASS | 49 | 0 |
| independent-windows | PASS | PASS | 48 | 0 |
| integration | PASS | PASS | 28 | 0 |
| layout | PASS | PASS | 49 | 0 |
| layout-invalid | PASS | PASS | 50 | 0 |
| layout-restart | PASS | PASS | 56 | 0 |
| layout-roles | PASS | PASS | 194 | 0 |
| legacy | PASS | PASS | 10 | 0 |
| native-acceptance | PASS | PASS | 68 | 0 |
| operation-contract | PASS | PASS | 21 | 0 |
| output | PASS | PASS | 29 | 0 |
| partial-failure | PASS | PASS | 22 | 0 |
| redock | PASS | PASS | 134 | 0 |
| standard-layout | PASS | PASS | 0 | 9 |
| startup-failure | PASS | PASS | 5 | 0 |
| tools | PASS | PASS | 32 | 0 |

| Konfiguration | Bestandene Szenarien | Nachgewiesene Checks | Layout-Captures | NOT VERIFIED |
|---|---:|---:|---:|---:|
| Debug | 21 | 964 | 18 | 0 |
| Release | 21 | 964 | 18 | 0 |

### Direkter Desktop-Einstieg

| Konfiguration | Echter Start mit --dock-shell und reguläres Schließen |
|---|---|
| Debug | PASS |
| Release | PASS |

Die CLI-Smokes sind zusätzlich zu den Runner-Szenarien ausgewiesen. PASS erfordert reguläres Prozessende mit Exitcode 0 und bestätigte Wiederherstellung des zuvor gesicherten Layoutprofils. Für die Independent-Szenarien wird F8 nur nach Auswahl und Aktivierung des beobachteten Hauptfensters ausgelöst; die Assertions selbst bleiben unverändert. Ausführungsdetails, HWND-/Lifecycle-Ergebnisse, Layouts und Prozess-Exitcodes liegen in den neuen F1-Nachweisen.

## 5. Verbleibende Einschränkungen

- Spezieller Cross-Window-Mouse-Redock-Fall bleibt offen. Erfolgreiches command-basiertes Redock beweist keine generelle Behebung dieses Spezialfalls.
- Intermittierender Headless-Teststartfehler wurde in G2-I02 erneut beobachtet und durch einen unveränderten vollständigen Wiederholungslauf ergänzt, nicht behoben.
- Native Fokus-/Aktivierungsprobleme bleiben bekannt. Die 26 automatisierten F1-Szenarien benötigten diesmal keine externe Aktivierung; daraus folgt keine generelle Behebung.
- Die drei einmaligen Popup-Cleanup-Befunde aus G2-I01 bleiben historisch dokumentiert und ungeklärt. Sie werden nicht als neue G2-I02-Fehler oder als behoben bezeichnet.
- Tatsächlich gemessene Layout-Skalierung 150 %. Windows 125 % bleibt offen.
- Linux-/macOS-Integrationsvalidierung, RID-Publish und neue Remote-CI für diesen Merge wurden nicht ausgeführt. Kein Push und keine Remote-Integration.
- Die bisherigen Eigentümerabnahmen von G2-02 bleiben erhalten. Sie ersetzen keine hier als NOT VERIFIED gekennzeichnete Master-Prüfung.

## 6. Git-Status und weiteres Vorgehen

Die abschließende F1-Integritätsprüfung bestätigt den erwarteten Merge-Commit und den vollständigen Tree mit 1.183 Pfaden, unveränderte geschützte Referenzen/Worktree-Status und den unveränderten GUI-Index. Alle 136 beim F1-Start vorhandenen G2-I02-Nachweisdateien sind weiterhin SHA-256-identisch. Der getrackte Working-Tree-Diff ist leer; `git diff --check` endet mit 0. Das neue Dokument wird zusätzlich separat auf Whitespace geprüft und gemeinsam mit den F1-Nachweisen in einem SHA-256-Abschlussinventar erfasst.

Die separate Dokumentprüfung `git diff --no-index --check -- NUL docs/gui/G2-I02-final-master-integration.md` liefert Exitcode 1 für den vorhandenen Dateiunterschied; keine Whitespace-Fehler, lediglich den Hinweis auf die konfigurierte zukünftige LF→CRLF-Normalisierung. Rohmeldung und Exitcode sind separat gespeichert. Keine Git-Konfiguration wurde verändert.

Der lokale Master-Merge bleibt bestehen. Nur dieser Bericht ist neu und uncommitted; kein zusätzlicher Commit wurde erstellt. Vor einer späteren Veröffentlichung sind die ausstehenden Plattform-/CI-Prüfungen und bekannten Restpunkte entsprechend der Eigentümerentscheidung weiterzuführen. Ein PARTIAL-Status bezeichnet ausschließlich noch fehlende native Abschlussnachweise, nicht einen erneut auszuführenden Merge.

## 7. Nachweisverzeichnis

Alle folgenden Pfade sind relativ zum Hauptrepository. Alte Nachweise bleiben erhalten; bin/obj, Logs und Hilfsskripte sind keine Commit-Inhalte.

- `artifacts/g2-i02/`: ursprünglicher Preflight, 166-Pfad-Inventar, vollständiger Kandidatenindex, Commit-/Tree-/Parent-Verifikation, Fast-Forward-Log, Referenzen/Worktree-Status, physischer Quellvergleich, Whitespace-Rohbefunde.
- `artifacts/g2-i02/validation/`: tatsächliche Master-Builds und vollständige Debug-/Release-Suiten, einschließlich des fehlgeschlagenen ersten Release-GUI-Laufs.
- `artifacts/g2-i02/gui-Release-retry-1/`: erfolgreicher vollständiger unveränderter GUI-Wiederholungslauf.
- `artifacts/g2-i02/native/`: zwölf erfolgreiche ursprüngliche Debug-Szenarien und erhaltener unterbrochener Layout-Invalid-Versuch.
- `artifacts/g2-i02/native-f1-auto/`: 26 neue automatisierte F1-Szenarien mit separaten JSON-/Trace-/stdout-/stderr-Dateien und Exitcodes.
- `artifacts/g2-i02/independent-f1/`, `artifacts/g2-i02/desktop-cli-f1/`: ausschließlich tatsächlich ausgeführte ergänzende interaktive F1-Prüfungen; fehlende Resultate bleiben NOT VERIFIED.
- `artifacts/g2-i02/independent-f1-retry-1/`: erfolgreicher Release-Floating-Wiederholungslauf und anschließender erster Release-Neustartlauf; der fehlgeschlagene Floating-Erstversuch bleibt im ursprünglichen F1-Verzeichnis erhalten.
- `artifacts/g2-i02-f1/`: erneute Git-Prüfung, TRX-Hash-/Ergebnisverifikation, vorherige Nachweisinventare und Erhaltprüfung, vollständige native Szenariomatrix, Freigabe, Prozesskontrollen und SHA-256-Abschlussinventar.
- `artifacts/g2-i01/` und Integrations-Worktree-Artefakte: ausschließlich historische Vergleichsbasis; keine Umdeklaration zu neuen Läufen.

## Anhang A – Vollständiges Commit-Änderungsinventar

128 neue und 38 geänderte Dateien. Dieser nachträgliche G2-I02-Bericht ist nicht Teil des Merge-Commits.

```text
M	CryptoScript/Model/AntlrToFunctionCallExpression.cs
M	CryptoScript/Model/Ast/FunctionCallExpressionNode.cs
M	CryptoScript/Model/FunctionCall.cs
M	CryptoScript/Model/FunctionCallEvaluator.cs
M	CryptoScript/Model/OperationInvocation.cs
M	CryptoScript/Operations/OutputOperations.cs
A	CryptoScriptUnitTest/PrintSourceTests.cs
M	CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs
A	CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs
M	CryptoWorkBenchAvalonia.Desktop/Program.cs
M	CryptoWorkBenchAvalonia/App.axaml.cs
M	CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs
M	CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs
M	CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj
M	CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs
M	CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs
M	CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs
M	CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs
M	CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs
A	CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs
A	CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs
A	CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs
A	CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs
A	CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs
A	CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs
A	CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs
A	CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs
A	CryptoWorkBenchAvalonia/Shell/DockOutputService.cs
A	CryptoWorkBenchAvalonia/Shell/DockOutputView.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellState.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml
A	CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml
A	CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs
A	CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs
A	CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs
A	CryptoWorkBenchAvalonia/Shell/DockWindowGeometry.cs
A	CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs
A	CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs
M	CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs
M	CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs
M	CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml
M	CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs
M	CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj
A	CryptoWorkBenchGuiTest/DockAssignmentIntegrationTests.cs
A	CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs
A	CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockEditorTests.cs
A	CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs
A	CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs
A	CryptoWorkBenchGuiTest/DockIndependentWindowNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockIndependentWindowTests.cs
A	CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs
A	CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs
A	CryptoWorkBenchGuiTest/DockLayoutTests.cs
A	CryptoWorkBenchGuiTest/DockMouseRedockTests.cs
A	CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs
A	CryptoWorkBenchGuiTest/DockNativeChromeTests.cs
A	CryptoWorkBenchGuiTest/DockOperationContractTests.cs
A	CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockOutputTests.cs
A	CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockPartialFailureTests.cs
A	CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs
A	CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockRedockNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockShellNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockShellTests.cs
A	CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockStandardLayoutTests.cs
A	CryptoWorkBenchGuiTest/DockToolNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockToolTests.cs
A	CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs
A	CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs
M	CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs
A	CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json
M	CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs
A	docs/gui/G1-01-dock-feasibility.md
A	docs/gui/G1-02-shell-implementation.md
A	docs/gui/G1-03-editor-mcp-integration.md
A	docs/gui/G1-04-variables-info-integration.md
A	docs/gui/G1-05-output-pipeline.md
A	docs/gui/G1-06-diagnostics-navigation.md
A	docs/gui/G1-07-layout-persistence.md
A	docs/gui/G1-08-final-validation.md
A	docs/gui/G1-08-native-acceptance-checklist.md
A	docs/gui/G1-A01-docking-architecture-review.md
A	docs/gui/G1-A02-maximize-layout-roles.md
A	docs/gui/G1-A03-persistence-cleanup-roles.md
A	docs/gui/G1-A04-R1-actual-callback-audit.md
A	docs/gui/G1-A04-R2-test-cleanup.md
A	docs/gui/G1-A04-R3-partial-failure-audit.md
A	docs/gui/G1-A04-R4-partial-failure-correction.md
A	docs/gui/G1-A04-dock-operation-contract.md
A	docs/gui/G1-A05-M1-final-owner-acceptance.md
A	docs/gui/G1-A05-native-chrome-input-acceptance.md
A	docs/gui/G1-A06-cross-platform-final-validation.md
A	docs/gui/G1-N1-F01-F2-native-window-investigation.md
A	docs/gui/G1-N1-F01-mouse-redock-correction.md
A	docs/gui/G1-N1-F03-group-document-redock.md
A	docs/gui/G2-02-C1-git-dependency-audit.md
A	docs/gui/G2-02-C2-reconstruction-validation.md
A	docs/gui/G2-02-R1-visual-corrections.md
A	docs/gui/G2-02-R2-floating-windows.md
A	docs/gui/G2-02-final-acceptance.md
A	docs/gui/G2-02-standard-layout.md
A	docs/gui/G2-I01-integration-validation.md
A	docs/gui/evidence/G1-01-results.json
A	docs/gui/evidence/G1-02-results.json
A	docs/gui/evidence/G1-03-results.json
A	docs/gui/evidence/G1-04-results.json
A	docs/gui/evidence/G1-05-results.json
A	docs/gui/evidence/G1-06-results.json
A	docs/gui/evidence/G1-07-results.json
A	docs/gui/evidence/G1-08-results.json
A	docs/gui/evidence/G1-A02-results.json
A	docs/gui/evidence/G1-A03-results.json
A	docs/gui/evidence/G1-A04-R4-results.json
A	docs/gui/evidence/G1-A04-results.json
A	docs/gui/evidence/G1-A05-results.json
A	docs/gui/evidence/G1-A06-results.json
A	docs/gui/evidence/G1-N1-F01-F2-results.json
A	docs/gui/evidence/G1-N1-F01-results.json
A	docs/gui/evidence/G1-N1-F03-results.json
A	eng/gui/Test-DockNativeTrace.ps1
A	eng/gui/Validate-G1LayoutRoles.ps1
A	eng/gui/Validate-G1NativeAcceptance.ps1
A	eng/gui/Validate-G1Platform.ps1
A	eng/gui/Validate-G1Shell.ps1
A	eng/gui/native/G1ShellProbe.csproj
A	eng/gui/native/Program.cs
A	eng/gui/native/packages.lock.json
M	eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json
M	eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json
M	eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.lock.json
A	prototypes/G1DockFeasibility/G1DockFeasibility.csproj
A	prototypes/G1DockFeasibility/NativeProbe.cs
A	prototypes/G1DockFeasibility/ProbeWorkspace.cs
A	prototypes/G1DockFeasibility/Program.cs
A	prototypes/G1DockFeasibility/README.md
A	prototypes/G1DockFeasibility/Session.cs
A	prototypes/G1DockFeasibility/Tests/DockTests.cs
A	prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj
A	prototypes/G1DockFeasibility/Tests/packages.lock.json
A	prototypes/G1DockFeasibility/Validate.ps1
A	prototypes/G1DockFeasibility/Views.cs
A	prototypes/G1DockFeasibility/packages.lock.json

```
