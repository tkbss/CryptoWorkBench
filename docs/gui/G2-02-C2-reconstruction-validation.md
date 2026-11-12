# G2-02-C2 – Commit Reconstruction Validation

**READY FOR COMMIT APPROVAL**

Stand: 12.11.2026. Variante A wurde in getrennten, isolierten Quellenständen erfolgreich validiert. G1 und G2 sind baubar; die vollständigen Regressionen besitzen erfolgreiche Debug-/Release-Läufe. **Der erste G2-GUI-Debug-Lauf hatte einen bekannten intermittierenden Headless-Initialisierungsfehler; erst die unveränderte vollständige Wiederholung bestand.** Beide Ergebnisse bleiben erhalten. Keine Commits, kein Push, Merge oder Rebase; keine Änderungen am Index des bestehenden GUI-Worktrees.

## 1. Arbeitsgrenzen und Sicherung

Gelesene Entscheidungsgrundlage: [C1-Audit](G2-02-C1-git-dependency-audit.md). Der bestehende GUI-Worktree blieb während Rekonstruktion und Tests unverändert. Als einzige neue Datei wird dort dieser angeforderte Bericht ergänzt.

| Bezug | Verifizierter Stand |
| --- | --- |
| Original | `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1` |
| Branch / HEAD | `feature/gui-g1` / `7ecac04483c10722c4cb4b459e3f6cb5157c6af1` |
| master | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`, unverändert |
| Merge-Base | GUI-HEAD; die vier neueren master-Commits sind nicht Bestandteil dieser Rekonstruktion |
| Originalstatus vor C2 | 38 getrackte Änderungen, 125 untracked Dateien einschliesslich C1; keine gestagten Änderungen |
| Isolationsverzeichnis | `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\g2-02-c2` |

Alle folgenden Artefaktpfade beziehen sich auf dieses Isolationsverzeichnis, **nicht** auf das artifacts-Verzeichnis innerhalb des ursprünglichen GUI-Worktrees.

Der aktuelle vollständige nicht ignorierte Arbeitsstand wurde mit `git ls-files --cached --others --exclude-standard` erfasst: **1.161 Dateien, 51.641.836 Bytes**, einschliesslich aller untracked Quellen und C1. Jede Datei wurde nach `snapshot/` kopiert und ihr SHA-256 unmittelbar mit der Quelle verglichen. Kein Kopierfehler. Ignorierte alte Build-/Testausgaben bleiben am Originalort erhalten; sie werden weder in die Quellrekonstruktion übernommen noch gelöscht.

[Quellinventar](../../../../g2-02-c2/source-manifest.json), [Sicherungsübersicht](../../../../g2-02-c2/snapshot-summary.json), [Git-Ausgangsstatus](../../../../g2-02-c2/git-before.json).

| Manifest | SHA-256 |
| --- | --- |
| Sicherung aktueller Stand, 1.161 Dateien | `5619057A8CBDAB7249F2C3EA1178B005B9857C34C793E2253BEABA09CDF3852E` |
| A06, 1.149 Dateien | `B69E8940396E0E7BADC9768419A9305D237EB1BDF42C71D8F95F985EE158D0A7` |
| G1 rekonstruiert, 1.151 Dateien | `359DC0A88AB1BE61AE8E36E940F8283DBCC410B663E8C87039549B03CF74D76B` |
| G2 rekonstruiert, 1.160 Dateien | `660BB86D070F8DB459AB7D15C9D99FC5BB0FFCFEC6F70815E6EFD95ACDF549F6` |

Die A06-Originalsicherung wurde nicht verändert. Sämtliche 1.149 gesicherten Dateien wurden erneut gegen ihr vorhandenes Manifest verifiziert.

## 2. Rekonstruktion G1

`g1/` wurde ausschliesslich aus den verifizierten A06-Quellen aufgebaut. Ergänzt wurden die zwei nach dem damaligen Snapshot entstandenen Abschlussdateien:

- `docs/gui/G1-A06-cross-platform-final-validation.md`
- `docs/gui/evidence/G1-A06-results.json`

Ergebnis: **1.151 Dateien**, alle 1.149 A06-Dateien hashidentisch, exakt zwei dokumentierte Ergänzungen. Alle 28 später in G2 veränderten G1-Neudateien besitzen hier ihre historische Fassung; insbesondere der native Launcher und `DockDocumentWindowChrome.cs`. Die Editor-XAML-Datei bleibt auf dem damaligen HEAD-/G1-Stand. Keine G2-Tests, -Styles oder -Berichte sind enthalten.

Eine eigenständige lokale Git-Kopie mit separatem Objektbestand bildet den unveränderten GUI-HEAD ab. `read-tree HEAD` initialisierte ausschliesslich deren isolierten Index auf HEAD; es wurden keine rekonstruierten Änderungen gestagt und keine Commits erzeugt. Der Originalindex wurde nie als Arbeitsindex verwendet. Die getrennte Prism-Kopie besitzt keine gemeinsam schreibbaren Hardlinks zu den ursprünglichen Quellen.

[G1-Manifest](../../../../g2-02-c2/g1-source-manifest.json), [A06-Vergleich](../../../../g2-02-c2/g1-reconstruction-check.json).

## 3. Rekonstruktion G2

Erst nach erfolgreichen G1-Builds, Regressionen und 26 nativen Läufen wurde `g2/` aus dem validierten G1-Quellinventar aufgebaut. Bin/obj, .deps und Testausgaben wurden dabei nicht kopiert. Anschliessend wurden ausschliesslich die 40 in F1/C1 dokumentierten Änderungen angewandt:

- 29 geänderte Dateien aus der aktuellen Sicherung;
- zehn neue G2-Dateien einschliesslich der vier Abschluss-/Teilberichte;
- Entfernung von `CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs`.

Der unabhängig berechnete Dateidelta G1 → G2 stimmt exakt mit der deklarierten Liste überein: **40 Pfade, keine Abweichung**. Der resultierende G2-Stand umfasst **1.160 Dateien** und ist byteweise/SHA-256-identisch mit der aktuellen Sicherung abzüglich des bewusst separaten C1-Auditberichts. Keine zusätzliche oder fehlende Quelldatei.

C1 und der vorliegende C2-Bericht sind Dokumentation der Commit-Vorbereitung, keine der 40 Produktstandsänderungen. Sie gehören gegebenenfalls in einen anschliessenden Dokumentationscommit.

[G2-Manifest](../../../../g2-02-c2/g2-source-manifest.json), [G2-Endvergleich](../../../../g2-02-c2/g2-reconstruction-check.json), [unabhängiger Commit-Grenzenvergleich](../../../../g2-02-c2/commit-boundary-check.json).

Dies sind vollständige Dateibaum-/Inhaltsvergleiche. Es wurden keine neuen Git-Commit- oder Tree-Objekte als angeblich fertige Commits erzeugt.

## 4. Neue Build- und Regressionsergebnisse

**Alle Ergebnisse dieses Abschnitts stammen aus neuen C2-Läufen am 12.11.2026**, nicht aus übernommenen A06-/F1-Zahlen. Testresultate wurden aus den einzelnen TRX-outcomes gezählt.

Umgebung: Windows, .NET SDK **10.0.401**, native Layoutproben melden RenderScaling **1.5**. Die vorhandenen Avalonia-/Dock-Pins und Lockdateien bleiben unverändert. Die isolierten Prism-Checkouts wurden auf den offiziellen Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` mit jeweils 1.180 gepinnten Dateien geprüft.

### Locked Restore und Build

Je Zustand und Konfiguration wurde der unveränderte Wrapper `eng/prism/Build.ps1` ausgeführt. Er führt Locked Restore und anschliessend Build mit `--no-restore` für beide Solutions aus. Zusätzlich wurde der native Probe-Runner je Debug/Release explizit mit `--locked-mode` restored und mit `--no-restore -m:1` gebaut.

| Stand | Debug | Release | Native Runner Debug / Release |
| --- | --- | --- | --- |
| G1 | PASS, beide Solutions | PASS, beide Solutions | Restore und Build PASS / PASS |
| G2 | PASS, beide Solutions | PASS, beide Solutions | Restore und Build PASS / PASS |

Keine Buildfehler. Die Avalonia-Solution meldet bei G1 und G2 jeweils **85 Debug- und 91 Release-Warnungen**, die Root-Solution jeweils null. Es wird kein warnungsfreier Build behauptet. Meldungen und Native-Buildwarnungen bleiben vollständig in den Logs erhalten. Der Restore verwendete die konfigurierte NuGet-Quelle und vorhandene Paket-Caches; ein vollständig leerer externer Cache wurde nicht erzwungen.

### Regressionen

| Stand / Suite | Debug | Release | NotExecuted je Konfiguration |
| --- | ---: | ---: | ---: |
| G1 GUI | 1.108 PASS | 1.108 PASS | 0 |
| G1 MCP | 176 PASS | 176 PASS | 13 |
| G1 Core | 11.512 PASS | 11.512 PASS | 27 |
| G2 GUI, erster Lauf | **1.123 PASS, 1 FAILED** | 1.124 PASS | 0 |
| G2 GUI, vollständige Wiederholung | **1.124 PASS, 0 FAILED** | nicht erneut ausgeführt | 0 |
| G2 MCP | 176 PASS | 176 PASS | 13 |
| G2 Core | 11.512 PASS | 11.512 PASS | 27 |

Die 27 Core-NotExecuted umfassen 26 übersprungene Fälle und einen expliziten, nicht angeforderten Fall; 13 MCP-Fälle sind ebenfalls nicht ausgeführt. Sie werden nicht als PASS mitgezählt. G1 stimmt in Inventar und Ergebnissen mit A06 überein. Die erfolgreichen G2-Läufe stimmen mit F1 überein. Wiederholungsergebnisse werden nicht zum eindeutigen Testinventar addiert.

Aufruf je Suite: `dotnet test <Projekt> -c <Debug|Release> --no-build --no-restore --logger trx --results-directory <isolierter Nachweisordner> -- NUnit.NumberOfTestWorkers=0`. Die konkreten Argumente stehen in den jeweiligen command-Dateien.

### Fehlgeschlagener G2-Erstlauf und Wiederholung

Betroffen: `SingleTabRowsSurviveSwitchCloseResetRestoreAndStartup`.

Fehler: `InvalidOperationException: The calling thread cannot access this object because a different thread owns it.` Der Stack läuft über `Dispatcher.VerifyAccess`, `DefaultRenderLoop.Add`, `AvaloniaHeadlessPlatform.Initialize` und `HeadlessUnitTestSession.EnsureIsolatedApplication`, vor dem Testkörper. Dies entspricht dem bereits dokumentierten intermittierenden Headless-Startproblem.

- Originaler fehlgeschlagener Lauf: `g2/artifacts/c2-validation/CryptoWorkBenchGuiTest-Debug.trx` und gleichnamiges Log; Exit 1.
- Das erste gesamte G2-Validierungsskript endet deswegen ebenfalls mit Exit 1; dieses Ergebnis wurde nicht überschrieben.
- Genau eine vollständige Debug-GUI-Wiederholung ohne Quelländerung: `g2/artifacts/c2-gui-Debug-retry-1/gui.trx`, `gui.log`, `command.json`; Exit 0, 1.124 PASS.
- Keine gefilterte Wiederholung nur des fehlgeschlagenen Tests und keine Änderung von Produkt-/Testcode, um einen grünen Lauf zu erzeugen.

Die in einigen GUI-Logs enthaltenen „Dock trace disabled“-Meldungen bei gesperrten `cwb-r4-...`-Dateien stammen aus `RealTraceWriteFailureDoesNotInterruptMove`: Der Test erzwingt reale Trace-Schreibfehler. Die betreffenden Tests bestanden; diese Meldungen sind vom Headless-Initialisierungsfehler zu unterscheiden.

## 5. Native Windows-Prüfungen

Alle Probeprozesse verwenden einen isolierten Layoutpfad neben ihrer jeweiligen Ergebnisdatei (`Output + ".layout.json"`), nicht das normale Benutzerlayout. Pro Prozess bestand eine Zeitgrenze von 180 Sekunden; kein nativer Lauf überschritt diese Grenze.

### Gemeinsame Proben und G2-Layoutproben

Die folgende Tabelle gilt **jeweils für Debug und Release**. Zahlen sind erfolgreiche Einzelchecks pro Lauf. Die letzten beiden Modi liefern Messzustände und eine separate failures-Liste, keine gleichartigen Einzelcheck-Zähler.

| Native Probe | G1 je Konfiguration | G2 je Konfiguration |
| --- | ---: | ---: |
| `native-acceptance` | 68 | 68 |
| `editor` | 54 | 54 |
| `tools` | 32 | 32 |
| `output` | 29 | 29 |
| `diagnostics` | 35 | 35 |
| `layout-roles` | 194 | 194 |
| `layout` | 48 | 49 |
| `layout-restart` | 56 | 56 |
| `layout-invalid` | 49 | 50 |
| `integration` | 28 | 28 |
| `redock` | 134 | 134 |
| `operation-contract` | 21 | 21 |
| `partial-failure` | 22 | 22 |
| `standard-layout` | — | 9 Messzustände, 0 Fehler |
| `corrections-layout` | — | 9 Messzustände, 0 Fehler |

G1: **26/26 native Läufe**, 1.540 erfolgreiche Einzelchecks insgesamt, keine Fehlerobjekte/fehlgeschlagenen Checks. G2: **30/30 Läufe**, 1.544 erfolgreiche Einzelchecks plus 36 Layout-Messzustände, alle vier Layout-failures-Listen leer. Das neue Layout erklärt die zwei zusätzlichen Checks pro Konfiguration gegenüber G1.

Die Layoutmessungen umfassen sieben logische Clientgrössen von 640×420 bis 1920×1080, maximiertes Fenster und Wiederherstellung. Sie prüfen unter anderem obere Tabs, Mindestbreiten und bei den Korrekturen fehlende redundante Header sowie sichtbaren Print-Text im Viewport. Sie ersetzen keine erneute vollständige visuelle Eigentümerabnahme.

### Unabhängige Floating Windows und echter Prozessneustart

Die vorhandenen `independent-windows`- und `independent-restart`-Proben wurden mit drei gleichzeitig abgedockten Panes ausgeführt. Computer Use aktivierte jeweils das konkret identifizierte Hauptfenster und sendete F8, um den bestehenden Prüflauf nach realer Windows-Aktivierung zu starten. Die folgenden Assertions steuerte der unveränderte native Probe-Code.

| Konfiguration | Erstlauf | Neustart aus dessen gespeicherter Layoutdatei |
| --- | ---: | ---: |
| Debug | **48/48 PASS** | **49/49 PASS** |
| Release | **48/48 PASS** | **49/49 PASS** |

Geprüft wurden native Titelleisten-/Min-/Max-Styles, fehlende HWND-Ownership, Taskleisten-Eignung, getrennte WindowState-Übergänge inklusive Hauptfenster, Aktivierung/Vordergrund ohne Zustandsänderung anderer Fenster, gezieltes Show/Restore, Command-Redock mit Host-Zerstörung, unveränderte Editor-/Document-/Undo-Identität, Reset und Shutdown. Der Neustart prüft zusätzlich die drei wiederhergestellten Hosts. Beim Schliessen wurden alle erfassten HWNDs zerstört, Hosts und Editorregistrierung freigegeben und keine sichtbaren Prozessfenster zurückgelassen.

Alle vier Prozesse endeten mit Exit 0. Nach Abschluss wurden keine Probe-Fenster und keine laufenden `G1ShellProbe`-Prozesse gefunden. Eine direkte Taskleisten-Klicksequenz oder erneute Titelleisten-Drag-Abnahme wird hier nicht behauptet; die sechs manuellen Eigentümer-PASS-Angaben aus F1 bleiben historische Abnahme. Command-Redock ist keine Behebung des separat bekannten Cross-Window-Mouse-Redock-Falls.

Beim ersten F8-Start konnte die unmittelbar folgende UI-Aufnahme das Hauptfenster nicht erfassen, weil der Probe-Code es gerade absichtlich minimierte. Es wurde nicht störend reaktiviert; der native Ergebnislauf bestand anschliessend vollständig. Dies war keine fehlgeschlagene Produktassertion.

## 6. Vollständige vorgesehene Commit-Inhalte

### Commit 1: G1-Baseline einschliesslich A06

**152 Pfade relativ zu HEAD: 37 Änderungen, 115 neue Dateien.** Sämtliche unveränderten HEAD-Dateien bleiben Teil des Baums. Die unten genannten Dateien werden aus `g1/` übernommen, nicht pauschal aus der aktuellen GUI-Arbeitskopie.

[Maschinenlesbare Statusliste](../../../../g2-02-c2/g1-commit-files.txt).

| Datei | Änderung gegenüber HEAD |
| --- | --- |
| `CryptoScript/Model/AntlrToFunctionCallExpression.cs` | geändert |
| `CryptoScript/Model/Ast/FunctionCallExpressionNode.cs` | geändert |
| `CryptoScript/Model/FunctionCall.cs` | geändert |
| `CryptoScript/Model/FunctionCallEvaluator.cs` | geändert |
| `CryptoScript/Model/OperationInvocation.cs` | geändert |
| `CryptoScript/Operations/OutputOperations.cs` | geändert |
| `CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs` | geändert |
| `CryptoWorkBenchAvalonia.Desktop/Program.cs` | geändert |
| `CryptoWorkBenchAvalonia/App.axaml.cs` | geändert |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | geändert |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | geändert |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj` | geändert |
| `CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs` | geändert |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` | geändert |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs` | geändert |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` | geändert |
| `CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs` | geändert |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | geändert |
| `CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs` | geändert |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | geändert |
| `CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj` | geändert |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs` | geändert |
| `eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json` | geändert |
| `eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json` | geändert |
| `eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json` | geändert |
| `eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json` | geändert |
| `eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json` | geändert |
| `eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json` | geändert |
| `eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.Desktop.lock.json` | geändert |
| `eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.lock.json` | geändert |
| `eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.Desktop.lock.json` | geändert |
| `eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.lock.json` | geändert |
| `eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.Desktop.lock.json` | geändert |
| `eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.lock.json` | geändert |
| `eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.Desktop.lock.json` | geändert |
| `eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.lock.json` | geändert |
| `CryptoScriptUnitTest/PrintSourceTests.cs` | neu |
| `CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockOutputService.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellState.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockWindowGeometry.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs` | neu |
| `CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs` | neu |
| `CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockEditorTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockMouseRedockTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockNativeChromeTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockOperationContractTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockOutputTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockPartialFailureTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockRedockNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockShellTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockToolNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockToolTests.cs` | neu |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs` | neu |
| `CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json` | neu |
| `docs/gui/G1-01-dock-feasibility.md` | neu |
| `docs/gui/G1-02-shell-implementation.md` | neu |
| `docs/gui/G1-03-editor-mcp-integration.md` | neu |
| `docs/gui/G1-04-variables-info-integration.md` | neu |
| `docs/gui/G1-05-output-pipeline.md` | neu |
| `docs/gui/G1-06-diagnostics-navigation.md` | neu |
| `docs/gui/G1-07-layout-persistence.md` | neu |
| `docs/gui/G1-08-final-validation.md` | neu |
| `docs/gui/G1-08-native-acceptance-checklist.md` | neu |
| `docs/gui/G1-A01-docking-architecture-review.md` | neu |
| `docs/gui/G1-A02-maximize-layout-roles.md` | neu |
| `docs/gui/G1-A03-persistence-cleanup-roles.md` | neu |
| `docs/gui/G1-A04-R1-actual-callback-audit.md` | neu |
| `docs/gui/G1-A04-R2-test-cleanup.md` | neu |
| `docs/gui/G1-A04-R3-partial-failure-audit.md` | neu |
| `docs/gui/G1-A04-R4-partial-failure-correction.md` | neu |
| `docs/gui/G1-A04-dock-operation-contract.md` | neu |
| `docs/gui/G1-A05-M1-final-owner-acceptance.md` | neu |
| `docs/gui/G1-A05-native-chrome-input-acceptance.md` | neu |
| `docs/gui/G1-A06-cross-platform-final-validation.md` | neu |
| `docs/gui/G1-N1-F01-F2-native-window-investigation.md` | neu |
| `docs/gui/G1-N1-F01-mouse-redock-correction.md` | neu |
| `docs/gui/G1-N1-F03-group-document-redock.md` | neu |
| `docs/gui/evidence/G1-01-results.json` | neu |
| `docs/gui/evidence/G1-02-results.json` | neu |
| `docs/gui/evidence/G1-03-results.json` | neu |
| `docs/gui/evidence/G1-04-results.json` | neu |
| `docs/gui/evidence/G1-05-results.json` | neu |
| `docs/gui/evidence/G1-06-results.json` | neu |
| `docs/gui/evidence/G1-07-results.json` | neu |
| `docs/gui/evidence/G1-08-results.json` | neu |
| `docs/gui/evidence/G1-A02-results.json` | neu |
| `docs/gui/evidence/G1-A03-results.json` | neu |
| `docs/gui/evidence/G1-A04-R4-results.json` | neu |
| `docs/gui/evidence/G1-A04-results.json` | neu |
| `docs/gui/evidence/G1-A05-results.json` | neu |
| `docs/gui/evidence/G1-A06-results.json` | neu |
| `docs/gui/evidence/G1-N1-F01-F2-results.json` | neu |
| `docs/gui/evidence/G1-N1-F01-results.json` | neu |
| `docs/gui/evidence/G1-N1-F03-results.json` | neu |
| `eng/gui/Test-DockNativeTrace.ps1` | neu |
| `eng/gui/Validate-G1LayoutRoles.ps1` | neu |
| `eng/gui/Validate-G1NativeAcceptance.ps1` | neu |
| `eng/gui/Validate-G1Platform.ps1` | neu |
| `eng/gui/Validate-G1Shell.ps1` | neu |
| `eng/gui/native/G1ShellProbe.csproj` | neu |
| `eng/gui/native/Program.cs` | neu |
| `eng/gui/native/packages.lock.json` | neu |
| `prototypes/G1DockFeasibility/G1DockFeasibility.csproj` | neu |
| `prototypes/G1DockFeasibility/NativeProbe.cs` | neu |
| `prototypes/G1DockFeasibility/ProbeWorkspace.cs` | neu |
| `prototypes/G1DockFeasibility/Program.cs` | neu |
| `prototypes/G1DockFeasibility/README.md` | neu |
| `prototypes/G1DockFeasibility/Session.cs` | neu |
| `prototypes/G1DockFeasibility/Tests/DockTests.cs` | neu |
| `prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj` | neu |
| `prototypes/G1DockFeasibility/Tests/packages.lock.json` | neu |
| `prototypes/G1DockFeasibility/Validate.ps1` | neu |
| `prototypes/G1DockFeasibility/Views.cs` | neu |
| `prototypes/G1DockFeasibility/packages.lock.json` | neu |

### Commit 2: G2-02 einschliesslich R1, R2 und F1

**40 Pfade relativ zu G1: 29 Änderungen, zehn neue Dateien, eine Löschung.** Quelle ist der geprüfte `g2/`-Stand.

[Maschinenlesbare Dateiliste](../../../../g2-02-c2/g2-commit-files.txt), [Delta mit Änderungsart](../../../../g2-02-c2/g2-changes.json).

| Datei | Änderung gegenüber G1 |
| --- | --- |
| `CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs` | entfernt |
| `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs` | geändert |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml` | geändert |
| `CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockIndependentWindowNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockIndependentWindowTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockLayoutTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockMouseRedockTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockNativeChromeTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockShellTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockStandardLayoutTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockToolNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs` | neu |
| `docs/gui/G2-02-final-acceptance.md` | neu |
| `docs/gui/G2-02-R1-visual-corrections.md` | neu |
| `docs/gui/G2-02-R2-floating-windows.md` | neu |
| `docs/gui/G2-02-standard-layout.md` | neu |
| `eng/gui/native/Program.cs` | geändert |

Die 28 überlappenden G1-Neudateien werden durch ihre zwei vollständigen Fassungen abgebildet; kein manuelles Hunk-Staging ist für diese Rekonstruktion nötig. Die Editor-XAML-Änderung gehört vollständig zu Commit 2. Die historische Chrome-Klasse wird in Commit 1 hinzugefügt und in Commit 2 entfernt.

Core-Print-Quellenmetadaten, Desktop-/MCP-Integration, Projekt- und Lockdateien, Fixtures, Test-/Probe-Dateien sowie G1-Prototypen sind in Commit 1 enthalten. G2 baut auf diesen Git-, Build-, Funktions- und Testabhängigkeiten auf. Die aktualisierten G2-Testannahmen bleiben zusammen mit der G2-Implementierung in Commit 2. Fremde L1-/master-Änderungen wurden nicht importiert.

C1/C2 können nach Eigentümerentscheidung in einem separaten Dokumentationscommit folgen. Die lokalen C2-Prüfskripte und Rohartefakte sind keine zusätzlichen Produktänderungen der ersten beiden Commits.

## 7. Integrität und Nachweisablage

[Quellenvergleich nach Regressionen](../../../../g2-02-c2/integrity-after-regressions.json) und [Abschlusskontrolle](../../../../g2-02-c2/final-integrity.json) dokumentieren:

- Alle ursprünglichen 1.161 gesicherten Dateien unverändert; ausschliesslich dieser Bericht ist neu.
- Alle 1.149 A06-Originaldateien und das A06-Manifest unverändert.
- G1 und G2 nach den Prüfungen weiterhin hashidentisch zu ihren Quellmanifesten; keine fehlenden oder zusätzlichen nicht ignorierten Dateien.
- Keine lokalen bin/obj/.deps/artifacts-Dateien in den Commit-Listen. Versionierbare Lockdateien und Testfixtures bleiben enthalten. Bereits versionierte historische Dokumentationsnachweise werden nicht als neue Build-Ausgaben entfernt.
- `git diff --check` erfolgreich für Original, G1 und G2; keine gestagten Änderungen. Originalindex, GUI-HEAD und master-Referenz unverändert; Hauptcheckout sauber.
- Keine Änderungen an fremden Worktrees.

Die Kopien enthalten ihre eigenen Buildausgaben; diese sind ignoriert und nicht Teil der geprüften Commit-Inventare. Die Git-Kopien dienen nur der Validierung. Keine neuen Commits oder Branch-Aktualisierungen wurden erzeugt.

Wichtige Nachweise unter dem Isolationsverzeichnis:

| Pfad | Inhalt |
| --- | --- |
| `source-manifest.json`, `snapshot/` | Vollständige aktuelle Sicherung |
| `a06-manifest.json` | Kopie des unveränderten historischen Manifests |
| `g1-source-manifest.json`, `g2-source-manifest.json` | Rekonstruierte vollständige Quellbäume |
| `g1/artifacts/c2-validation/`, `g2/artifacts/c2-validation/` | Build-/Restorelogs, konkrete Befehle und alle ersten TRX-Läufe |
| `g2/artifacts/c2-gui-Debug-retry-1/` | Separater vollständiger Wiederholungslauf |
| `g1/artifacts/c2-native/`, `g2/artifacts/c2-native/` | Native JSON-Resultate, Layoutdateien, stdout/stderr und Prozess-Exits |
| `g2/artifacts/c2-independent/` | Vier Floating-/Neustartläufe samt Layout und Prozessnachweisen |
| `g1-test-summary.json`, `g2-first-test-summary.json` | TRX-Auswertung mit NotExecuted und erstem Fehler |
| `g1-native-summary.json`, `g2-native-summary.json`, `g2-independent-summary.json` | Native Ergebnisübersichten |
| `Validate.ps1`, `Native.ps1`, `Retry-Gui.ps1`, `Independent.ps1`, `Reconstruct-G2.ps1` | Verwendete lokale C2-Treiber; keine Produktänderungen |
| `evidence-manifest.json` | SHA-256-Inventar der neuen Prüfartefakte |

Ein erster sandboxierter lokaler Git-Clone scheiterte vor der Rekonstruktion an Windows/MSYS-Prozessrechten (`NtCreateDirectoryObject ... 0xC0000022`). Derselbe isolierte Clone gelang anschliessend mit den erforderlichen Ausführungsrechten. Dies ist ein Vorbereitungsfehler der Umgebung, kein Build-/Test-PASS und keine Produktänderung.

## 8. Offene Risiken und Freigabegrenze

- Der intermittierende Headless-Teststartfehler ist in C2 erneut nachgewiesen und **nicht behoben**. Der erfolgreiche Vollwiederholungslauf bestätigt den unveränderten rekonstruierten Stand unter diesem bekannten Vorbehalt.
- Spezieller Cross-Window-Mouse-Redock bleibt separat offen. Programmatische native Redock-Checks und F1-Eigentümerabnahme heben diese Grenze nicht auf.
- Windows-Skalierung 125 % bleibt nicht vollständig verifiziert. Die neuen Layoutproben liefen mit RenderScaling 1.5; daraus folgt keine 125-%-Freigabe.
- Keine neue Linux-/macOS-Zertifizierung, kein neuer RID-Publish-/Paketierungstest, kein vollständiges erneutes Durchspielen aller historischen A06-Sonderproben. Die angeforderten Windows-Builds, vollständigen GUI/MCP/Core-Regressionen und relevanten nativen Lebenszyklusprüfungen wurden durchgeführt.
- Ein Commit-Verlauf auf dem neueren master ist nicht geprüft. Dessen L1-Assignment-Integration benötigt weiterhin eigene Integrationstests.
- Quellmanifeste und lokale Rohdaten sind aufzubewahren. Die späteren tatsächlichen Commit-Trees müssen gegen diese geprüften Fassungen verifiziert werden; Gitattributes-/Zeilenenden-Normalisierung ist bei Git-Blob-Vergleichen zu berücksichtigen.
- Keine neue funktionale Änderung und keine Multi-Dokument-Erweiterung.

**Ergebnis:** Die Reihenfolge G1 → G2-02 ist vollständig rekonstruierbar und in den oben ausgewiesenen Grenzen erfolgreich validiert. **READY FOR COMMIT APPROVAL** bedeutet ausschliesslich, dass der Projekteigentümer jetzt die Commit-Erstellung freigeben kann; C2 hat noch keinen Commit erstellt.

