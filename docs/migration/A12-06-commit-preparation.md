# A12-06 – Commit Preparation & Integration Boundary Review

Stand: 28. Oktober 2026. **Commit-Vorbereitung abgeschlossen; noch keine Commit- oder Mergefreigabe umgesetzt.** Der geprüfte Endstand ist unter Windows aus einer bereinigten Quellkopie aufbaubar. Die Entscheidung über die Übernahme der uncommitteten MCP-/AP2.1-Basis und eine separate CI-Anpassung bleiben Voraussetzungen der späteren Integration. Die abgenommenen F2-Korrekturen wurden nicht erneut technisch untersucht.

## 1. Git-Inventar und Herkunft

Zielworktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Die Staging Area ist leer. Vor Erstellung dieses Berichts gibt es 127 Porcelain-Einträge: 44 modifizierte versionierte und 83 unversionierte Dateien. Nur 21 versionierte Dateien haben tatsächlich einen Git-Inhaltsdiff; 23 weitere werden im Status als modifiziert geführt, ergeben aber unter Git-Normalisierung keinen Inhaltsdiff. Diese 23 sind kein zusätzlicher fachlicher Commitumfang.

HEAD enthält 739 Dateien. Darin liegen bereits die vier MCP-Commits seit dem gemeinsamen Vorfahren: Architekturplanung `5de3320`, MCP-01.1 `55297e2`, MCP-01.2 `0a9eb35` und MCP-01.3 `bd48c57`. MCP-Verträge, Transport und Ausführungspipeline aus diesen Commits sind daher bereits Teil der Branchbasis und nicht als neue Avalonia-Arbeit zu deklarieren.

Vollständige maschinenlesbare Inventare: [HEAD-Baum mit Blob-IDs](../../artifacts/a12-06/head-tree.txt), [Status](../../artifacts/a12-06/status.txt), [alle 127 Einträge mit Kategorie, Hash und Überlappungskennzeichnung](../../artifacts/a12-06/inventory.json), [CSV](../../artifacts/a12-06/inventory.csv), [vollständiges Quellmanifest](../../artifacts/a12-06/source-manifest.json), [Arbeitsdiff](../../artifacts/a12-06/working.patch). Dateien ohne Statusänderung werden über HEAD-Baum und Quellmanifest vollständig erfasst.

Die Herkunft der übernommenen Arbeit ist anhand von `artifacts/a12-01/baseline/transferred-changes.json` eindeutig belegt: **34 Dateien, davon sechs damals versioniert/modifiziert und 28 damals unversioniert**. Die sechs Bestandsdateien sind WorkbenchAutomationRegistry, WorkbenchAutomationService, CryptoScriptEditViewModel, SidebarViewModel sowie CryptoScriptEditView.axaml und dessen Codebehind. Die übrigen Dateien umfassen Editorcontrols, EditorBridge/Coordinator, zugehörige Services/Tests und MCP-/AP2.1-Dokumentation. Die komplette Liste steht im Commit-Anhang.

Avalonia änderte später auch mehrere dieser übernommenen Dateien. `alsoAvaloniaDelta` im Inventar kennzeichnet die Überlappung anhand des A12-01-Migrationsmanifests. Eine einzelne Datei kann deshalb mehreren logisch aufeinanderfolgenden Commits angehören; ein pauschales Staging ihres heutigen Inhalts würde die Herkunft vermischen.

Die A12-01-Portierung umfasst 20 damalige Bestandsdateien und drei neue C#-Dateien: UiDispatch, GuiTestSession und Avalonia12DispatcherTests. Hinzu kommen die spätere korrigierte Prism-Integration, F1 und C1. Historische Zwischenstände mit dem alten Prism-ABI-Blocker sind keine geeigneten einzeln freizugebenden Produktstände.

## 2. Konkrete Commit-Empfehlung

Die folgenden Bezeichnungen **C1 bis C5 sind Commitnummern**, nicht die Namen des historischen C1-Testarbeitspakets. Die vollständigen Dateilisten stehen unten und zusätzlich in [commit-files.json](../../artifacts/a12-06/commit-files.json).

1. **C1 – `Preserve inherited MCP AP2.1 editor coordination baseline`**: ausschließlich die 34 belegten übertragenen Dateien in ihrer A12-01-Ausgangsversion aus `artifacts/a12-01/baseline/source/`. Dies benötigt die ausdrückliche Eigentümerentscheidung, wo diese Arbeit autoritativ committed wird. Empfehlung: zuerst als eigenständige MCP-Basis festhalten, danach dieselbe Historie für den Migrationsbranch verwenden. Hier wurde keine solche Git-Aktion ausgeführt. Der aktuelle Inhalt überlappender Dateien darf nicht ungeprüft in C1 gelangen.
2. **C2 – `Migrate Avalonia 12 with verified temporary Prism source integration`**: Avalonia-Portierung und finaler A12-05/F2-Adapter zusammen. Das Paket enthält relative ProjectReferences, Solutioneinträge, beide Hooks, Scope-/Integritätsprüfung, Ein-/Ausstiegsskripte, NuGet.Config, Ignore-Ausnahmen und sämtliche 27 geprüften Locks. Bei SidebarViewModel ist für diesen separaten Commit der Stand unmittelbar vor A12-01-F1 aus `artifacts/a12-01-f1/baseline/source/` vorgesehen. Alle übrigen C2-Dateien verwenden den geprüften aktuellen Stand. C2 setzt C1 voraus.
3. **C3 – `Handle script book import cancellation explicitly`**: F1-Diff auf SidebarViewModel gegenüber der Vor-F1-Sicherung plus die neue ScriptBookImportCancellationTests-Datei. Die acht Tests prüfen die eigentliche Änderung. C3 setzt C2 voraus. Alternativ können C2 und C3 bewusst zusammengefasst werden, wenn jeder publizierte Migrationscommit bereits alle F1-Fixes enthalten soll; die MCP-Grenze bleibt trotzdem erhalten.
4. **C4 – `Restrict PSN diagnostic assertions to exception messages`**: ausschließlich EmvMasterKeyDerivationOptionCTests.cs, das historische C1-Testarbeitspaket. Es korrigiert die Prüfung von Message statt des pfadabhängigen ToString-Ausgabekontexts, ohne Core-Code zu verändern. Dieser Commit ist sachlich unabhängig von Prism/Avalonia und kann separat integriert werden, sofern der Zielbranch dieselbe Testbasis besitzt.
5. **C5 – `Record Avalonia migration decisions and reviews`**: die 13 vorhandenen Migrationsberichte plus dieser Bericht. Historische BLOCKED-/Review-Bewertungen bleiben als zeitbezogene Evidenz erhalten und werden nicht umgeschrieben. MCP-/AP2.1-Dokumente gehören dagegen zur belegten C1-Basis.

**Warum C2 zusammengehört:** Die Avalonia-12-Portierung mit altem Prism-Paketgraphen erzeugte den dokumentierten Dialog-ABI-Fehler. Die ProjectReferences brauchen den beschafften Quellbaum; dessen reproduzierbarer Restore braucht Scope, Hooks, Audit-/Security-Pin und Locks. Diese Teile isoliert zu committen würde zeitweilig inkompatible oder nicht vorbereitbare Stände erzeugen. UiDispatch und die gemeinsame GUI-Testsession gehören zur API-/Threading-Portierung und dürfen nicht ohne ihre Aufrufstellen getrennt werden.

**Warum C1 getrennt bleiben muss:** Controls, EditorBridge/Coordinator, Admission-/Lease-Anbindung und Tests sind übernommene MCP-/AP2.1-Arbeit. Sie werden von der Migration genutzt, sind aber kein implizit genehmigter Bestandteil eines „Avalonia update“-Commits. Falls die Eigentümerentscheidung diese Basis nicht übernimmt, ist der aktuelle Migrationsstand nicht durch bloßes Weglassen dieser Dateien reproduzierbar. Dann wäre ein neuer, ausdrücklich beauftragter Portierungsumfang nötig.

Die vorgeschlagene Zwischencommitfolge wurde nicht als Git-Historie erzeugt und nicht für jeden Zwischenstand neu getestet. Verifiziert ist der vollständige geplante Endbaum. Vor tatsächlichen Commits müssen die ausgewählten Diffs gegen die genannten Baselines geprüft werden; bis dahin kein pauschales `git add .` und kein Force-Add ignorierter Dateien.

## 3. Reproduzierbarkeit und Veröffentlichungsgrenze

Unter `artifacts/a12-06/clean-source/` wurde ausschließlich der versionierte beziehungsweise explizit nicht ignorierte Quellbestand kopiert. Keine Git-Metadaten, keine ursprüngliche `.deps`, kein bin/obj, keine historischen Artefakte oder lokalen Paketcacheverzeichnisse wurden hineinkopiert. Das ist eine Export-Simulation des geplanten Endbaums, kein behaupteter tatsächlicher Commit/Checkout. Der neue A12-06-Bericht ist buildneutral und entstand anschließend.

Die unveränderten dokumentierten `eng/prism/Build.ps1`-Befehle wurden in Release und Debug gegen diese Kopie ausgeführt. Der Adapter beschafft den offiziellen Pin frisch in der Kopie, restauriert locked und baut beide Lösungen. Der vorhandene isolierte NuGet-Cache aus F2-R1 wurde bewusst extern wiederverwendet; dies ist kein erneuter Fresh-Cache-Sicherheitstest. Netzwerkzugriff zu GitHub und nuget.org, Git, PowerShell 7 und SDK gemäß global.json bleiben Voraussetzungen. [Release-Log](../../artifacts/a12-06/results/clean-release.log), [Debug-Log](../../artifacts/a12-06/results/clean-debug.log), zugehörige Exitdateien im gleichen Ordner.

Zum notwendigen Commitumfang gehören 13 Debug-Locks und 14 Release-Locks. Der zusätzliche Release/Bootstrap.lock.json ist deklarierte historische Probe-Provenienz, kein Produktprojekt und keine Buildabhängigkeit. Empfehlung: im ersten kontrollierten Commit unverändert mitführen, um den geprüften F2-Dateisatz nicht nebenbei zu bereinigen; eine spätere Entfernung wäre eine eigene bewusste Dokumentationsentscheidung.

`.gitignore` schließt `.deps`, artifacts, Paket-/Buildausgaben aus und nimmt ausschließlich die vorgesehenen Debug-/Release-Lockpfade wieder auf. [Ignore-Nachweis](../../artifacts/a12-06/ignore-rules.txt). Die explizite Quell-/Commitliste enthält keine `.deps`-/Cache-/Artefaktpfade, keine Maildateien und keine typischen privaten Schlüssel-/Credential-Dateien. Eine zusätzliche Musterprüfung fand keine privaten Schlüsselblöcke oder typischen GitHub-/AWS-Schlüssel in dieser Liste: [Prüfergebnis](../../artifacts/a12-06/distribution-check.json). Das ist eine begrenzte Musterprüfung, keine Garantie für beliebig kodierte Geheimnisse.

`.codex/config.toml` wurde weder gelesen noch kopiert. Dafür wird keine pauschale Ignore-Garantie behauptet; das explizite Manifest ist die Veröffentlichungsgrenze. Private Support-E-Mails dürfen nicht hinzugefügt werden. Versionierbar sind die bereits vorhandenen Entscheidungszusammenfassungen; die private Korrespondenz selbst bleibt außerhalb des Commitumfangs.

Die historische Roh-Evidenz unter artifacts wird **nicht** versioniert. Damit bleiben relative Berichtlinks auf diese Evidenzen in einem nackten Clone teilweise ohne Ziel. Vor einer öffentlichen Review-/Releaseübergabe sollte ein getrenntes, auf private Inhalte geprüftes Evidenzpaket mit Hashmanifest bereitgestellt werden; keine pauschale Aufnahme der Laborordner in Git. Es wurde kein solches Paket veröffentlicht.

## 4. CI-Befund ohne Änderung am Workflow

Die vor A12-05 als modifiziert geführte `.github/workflows/dotnet.yml` ist tatsächlich vorhanden und wurde gelesen. Wichtig: Der aktuelle Git-Inhaltsdiff gegen HEAD ist leer; `git hash-object` und `HEAD:.github/workflows/dotnet.yml` ergeben beide **0733473b829787d0f491834595061459e10ef108**. Der Porcelain-Status allein darf hier keinen eigenständigen CI-Änderungscommit begründen.

Der vorhandene Workflow enthält vier RID-Jobs (Windows x64, Linux x64, macOS ARM64/x64), Restore-/Build-/Test-/Publish-Schritte und wird bei Push/PR nach master ausgelöst. Er startet normale dotnet-Befehle ohne Prism-Vorbereitung. Ein frischer Checkout des geplanten Quellstands kann dadurch die neuen ProjectReferences nicht auflösen beziehungsweise scheitert am Guard. Das ist eine noch ausstehende CI-Integration, keine erneute Bewertung der akzeptierten F2-Implementierung.

Erforderliche spätere Anpassungen, **hier nicht umgesetzt**:

- Vor dem Restore den offiziellen Checkout beschaffen und die beiden aktuellen DirectoryBuild-Hooks setzen. Jeder Actions-run-Schritt startet einen neuen Prozess: Dot-Sourcing in einem früheren Schritt allein genügt nicht. Entweder pro relevanter Restore-/Build-/Test-/Publish-Shell Enter verwenden oder die benötigten Hookpfade bewusst jobweit bereitstellen. Build.ps1 räumt seine eigene Umgebung wieder auf.
- Restore und Build explizit auf dieselbe Configuration ausrichten; Debug und Release mit ihren geprüften Locks abdecken. Audit bleibt aktiv, Pin bleibt 10.0.303; kein stilles Ausschalten des Locked Mode.
- RID-Publish gesondert prüfen: Der heutige Publish-Schritt restauriert zusätzliche Runtimeassets. Die vorhandenen konfigurationsbezogenen Locks sind kein bereits erbrachter Nachweis für alle vier RID-Publishgraphen. Benötigte RID-Lockstrategie erst nach tatsächlicher Ausführung festlegen; nicht einfach `--no-restore` ergänzen oder Locks in CI neu erzeugen.
- GUI-Tests und native Plattformprüfungen mit der jeweiligen Runner-/Displayumgebung ausführen; Exitcodes, TRX und Skipgründe erhalten. Cache-Schlüssel an OS/SDK/Prism-Pin/Locks binden. SDK und PowerShell-Verfügbarkeit sowie Runner-/Action-Versionen vor Aktivierung überprüfen.

Die bestehende `.github/scripts/Validate-DesktopPublish.ps1` ist bereits in HEAD enthalten. Der separate Branch `ci/cross-platform-build-test` besitzt eigene CI-Arbeit und ist ebenfalls zu berücksichtigen, nicht ungeprüft zu übernehmen. Keine Actions-Ausführung oder CI-Grünfreigabe wird hier behauptet.

## 5. Branch- und spätere Mergegrenzen

[Worktrees](../../artifacts/a12-06/worktrees.txt), [Branches](../../artifacts/a12-06/branches.txt). `feature/mcp-server` und `feature/avalonia12-migration` zeigen derzeit auf denselben Commit bd48c57. Ihre Worktrees sind trotzdem unabhängig; uncommittete Dateien werden durch einen späteren Branchmerge nicht automatisch übertragen. Das aktuelle uncommittete MCP-Worktree wurde hier nicht verändert oder als automatisch identisch zum historischen Transfer angenommen. Vor einer späteren Übernahme muss sein dann aktueller Inhalt gegen die 34 belegten Ausgangsdateien abgeglichen werden.

`master` steht auf `5699ff10c9781264b653332109f772d7d8b26fef`; gemeinsamer Vorfahr ist `027815e9ea6f2dab11bd03485500b819907f2862`. Seitdem liegen auf master zehn und auf dem MCP-/Migrationszweig vier Commits. Diese Zahlen stammen aus lokalen Refs; es wurde kein Fetch ausgeführt.

Würde die Migration direkt nach master integriert, kämen auch die vier bereits committed MCP-Änderungen mit. Das muss eine ausdrückliche Integrationsentscheidung sein. Empfehlung: MCP-Basis und AP2.1-Herkunft zuerst eindeutig festlegen; die Avalonia-/Prism-Differenz anschließend gegen diese gemeinsame Basis reviewen. Parallele, inhaltlich doppelte AP2.1-Commits auf beiden Zweigen vermeiden. Dies ist eine fachliche Reihenfolge, kein ausgeführter Merge-/Rebaseplan.

Die Schnittmenge aus master-seitigen Änderungen und dem aktuellen Status umfasst `.github/workflows/dotnet.yml`, `.gitignore` und `CryptoWorkBenchGuiTest.csproj`: [Pfadüberschneidungen](../../artifacts/a12-06/master-overlap.txt). Eine Überschneidung ist noch kein nachgewiesener Textkonflikt. Besonders der GUI-Testprojektgraph verlangt eine semantische Entscheidung: master enthält einen Avalonia-Headless-Backport samt TestInfrastructure, während die Migration Avalonia 12 und die neue GuiTestSession benutzt. Zusätzlich verändert master Core-/Parser-/File-Operations- und Testbereiche; dafür wären nach einer späteren Integration neue Gesamtregressionen nötig, auch ohne Textkonflikt. [Master-Dateidelta](../../artifacts/a12-06/master-changed-paths.txt).

Kein Merge-Test mit Index- oder Worktreeänderungen wurde ausgeführt. Linux und macOS bleiben OPEN. Die befristete Prism-Supportentscheidung und der verpflichtende spätere Wechsel auf offizielle Pakete bleiben bestehen. A12-04-F01 wird unverändert weder als Produktfehler noch als Risiko oder Blocker behandelt.

## 6. Vollständige Dateiumfänge der empfohlenen Commits

Die Listen sind absichtlich überlappend, wenn eine Datei nacheinander MCP-Basis, Portierung und F1 erhält. Maßgeblich sind die oben beschriebenen Versionen/Diffs, nicht jeweils der heutige Gesamtinhalt.
### C1 MCP AP2.1 baseline - requires owner decision

- `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs`
- `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs`
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs`
- `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml`
- `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs`
- `CryptoWorkBenchGuiTest/EditorAutomationTests.cs`
- `CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs`
- `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs`
- `docs/mcp/MCP-00-AP0-contract-finalization.md`
- `docs/mcp/MCP-00-AP2.1-F1-correction-review.md`
- `docs/mcp/MCP-00-AP2.1-F1-independent-rereview.md`
- `docs/mcp/MCP-00-AP2.1-implementation-review.md`
- `docs/mcp/MCP-00-AP2.1-independent-review.md`
- `docs/mcp/MCP-00-D02-S1-epoch-safety.md`
- `docs/mcp/MCP-00-editor-coordination-design.md`
- `docs/mcp/MCP-00-R2-consolidated-architecture-decision.md`
- `docs/mcp/MCP-01.4-avalonia12-feasibility.md`
- `docs/mcp/MCP-01.4-B1-feasibility.md`
- `docs/mcp/MCP-01.4-B2-design.md`
- `docs/mcp/MCP-01.4-B2-independent-review.md`
- `docs/mcp/MCP-01.4-B2-R1-independent-review.md`
- `docs/mcp/MCP-01.4-editor-architecture-decision.md`
- `docs/mcp/MCP-01.4-post-U0-architecture-options.md`
- `docs/mcp/MCP-01.4-review.md`
- `docs/mcp/MCP-01.4-U0-property-feasibility.md`
- `docs/mcp/MCP-01.4-validation.md`

### C2 Avalonia12 plus corrected Prism atomic integration

- `.gitignore`
- `CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj`
- `CryptoWorkBenchAvalonia/App.axaml.cs`
- `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs`
- `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj`
- `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln`
- `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs`
- `CryptoWorkBenchAvalonia/Services/PasswordDialogHost.cs`
- `CryptoWorkBenchAvalonia/Services/UiDispatch.cs`
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs`
- `CryptoWorkBenchGuiTest/AsyncPasswordGuiTests.cs`
- `CryptoWorkBenchGuiTest/Avalonia12DispatcherTests.cs`
- `CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs`
- `CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj`
- `CryptoWorkBenchGuiTest/EditorAutomationTests.cs`
- `CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs`
- `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs`
- `CryptoWorkBenchGuiTest/GuiTestSession.cs`
- `CryptoWorkBenchGuiTest/PinTranslationInfoTests.cs`
- `CryptoWorkBenchGuiTest/WorkbenchAutomationTests.cs`
- `CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs`
- `Directory.Build.props`
- `Directory.Build.targets`
- `eng/prism/Build.ps1`
- `eng/prism/Enter-Prism.ps1`
- `eng/prism/Leave-Prism.ps1`
- `eng/prism/Lock.props`
- `eng/prism/locks/Debug/CryptoScript.lock.json`
- `eng/prism/locks/Debug/CryptoScriptUnitTest.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBench.Automation.Abstractions.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBench.Mcp.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBench.Mcp.Tests.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBenchConsole.lock.json`
- `eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json`
- `eng/prism/locks/Debug/Prism.Avalonia.lock.json`
- `eng/prism/locks/Debug/Prism.Core.lock.json`
- `eng/prism/locks/Debug/Prism.DryIoc.Avalonia.lock.json`
- `eng/prism/locks/Debug/Prism.Events.lock.json`
- `eng/prism/locks/Release/Bootstrap.lock.json`
- `eng/prism/locks/Release/CryptoScript.lock.json`
- `eng/prism/locks/Release/CryptoScriptUnitTest.lock.json`
- `eng/prism/locks/Release/CryptoWorkBench.Automation.Abstractions.lock.json`
- `eng/prism/locks/Release/CryptoWorkBench.Mcp.lock.json`
- `eng/prism/locks/Release/CryptoWorkBench.Mcp.Tests.lock.json`
- `eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json`
- `eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json`
- `eng/prism/locks/Release/CryptoWorkBenchConsole.lock.json`
- `eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json`
- `eng/prism/locks/Release/Prism.Avalonia.lock.json`
- `eng/prism/locks/Release/Prism.Core.lock.json`
- `eng/prism/locks/Release/Prism.DryIoc.Avalonia.lock.json`
- `eng/prism/locks/Release/Prism.Events.lock.json`
- `eng/prism/Prepare-Prism.ps1`
- `eng/prism/PrismSecurity.targets`
- `eng/prism/ProjectScope.props`
- `eng/prism/README.md`
- `eng/prism/Test-Integration.ps1`
- `NuGet.Config`

### C3 F1 import cancellation

- `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs`
- `CryptoWorkBenchGuiTest/ScriptBookImportCancellationTests.cs`

### C4 C1 diagnostic assertions

- `CryptoScriptUnitTest/EmvMasterKeyDerivationOptionCTests.cs`

### C5 migration record

- `docs/migration/A12-01-C1-R1-independent-rereview.md`
- `docs/migration/A12-01-C1-core-diagnostic-tests.md`
- `docs/migration/A12-01-F1-R1-independent-rereview.md`
- `docs/migration/A12-01-F1-import-cancellation-fix.md`
- `docs/migration/A12-01-R1-independent-review.md`
- `docs/migration/A12-01-implementation-review.md`
- `docs/migration/A12-02-prism-compatibility-review.md`
- `docs/migration/A12-04-F01-dialog-retention-investigation.md`
- `docs/migration/A12-04-temporary-prism-integration.md`
- `docs/migration/A12-05-F2-R1-independent-rereview.md`
- `docs/migration/A12-05-F2-build-integration-corrections.md`
- `docs/migration/A12-05-R1-independent-review.md`
- `docs/migration/A12-05-prism-integration.md`
- `docs/migration/A12-06-commit-preparation.md`

### Statusmeldungen ohne Git-Inhaltsdiff – kein neuer fachlicher Commitumfang

- `.github/workflows/dotnet.yml`
- `CryptoWorkBench.Automation.Abstractions/AutomationContracts.cs`
- `CryptoWorkBench.Automation.Abstractions/AutomationErrors.cs`
- `CryptoWorkBench.Automation.Abstractions/AutomationSchemas.cs`
- `CryptoWorkBench.Automation.Abstractions/CryptoWorkBench.Automation.Abstractions.csproj`
- `CryptoWorkBench.Automation.Abstractions/IWorkbenchAutomationService.cs`
- `CryptoWorkBench.Mcp.Tests/CapturedLogs.cs`
- `CryptoWorkBench.Mcp.Tests/CodexCompatibilityTests.cs`
- `CryptoWorkBench.Mcp.Tests/ContractTests.cs`
- `CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj`
- `CryptoWorkBench.Mcp/AutomationToolResults.cs`
- `CryptoWorkBench.Mcp/CryptoWorkBench.Mcp.csproj`
- `CryptoWorkBench.Mcp/StatusTransport.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/AutomationProjection.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs`
- `CryptoWorkBenchGuiTest/WorkbenchRegistryCorrectionTests.cs`
- `CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs`
- `docs/mcp/MCP-01.1-review.md`
- `docs/mcp/MCP-01.2-review.md`
- `docs/mcp/MCP-01.2-validation.md`
- `docs/mcp/MCP-01.3-review.md`
- `docs/mcp/MCP-01.3-validation.md`

## 7. Abschluss

Kein Staging, Commit, Push, Merge, Reset, Rebase oder Stash. Keine Code-/Test-/Prism-/Workflowänderung und keine Änderung anderer Worktrees. Neu sind ausschließlich dieser Bericht und die A12-06-Evidenzen. Die konkrete Commitfolge ist zur Eigentümerentscheidung vorbereitet; vor Git-Integration sind die MCP-Zuordnung und die CI-/RID-/Plattformgrenzen ausdrücklich zu behandeln. Nach Erstellung dieses Berichts wird gestoppt.

