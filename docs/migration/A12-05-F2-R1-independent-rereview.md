# A12-05-F2-R1 – Independent Prism Build Integration Re-Review

Stand: 28. Oktober 2026. **Bewertung: F2 ACCEPTED im geprüften Windows-Umfang.** Keine neuen blockierenden Findings nachgewiesen. Empfehlung: formale Abnahme der drei Buildkorrekturen durch den Eigentümer; Linux/macOS bleiben OPEN.

## Ausgangsbasis und Vorgehen

Geprüft wurde der tatsächliche uncommittete Stand in `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Vor Validierung wurden Git-Status, HEAD, staged/unstaged Binärdiffs sowie sämtliche über Git ermittelten vorhandenen Quell-/Konfigurationsdateien kopiert und per SHA-256 erfasst. Vorhandene Migration-/MCP-/AP2.1-Änderungen wurden als Ausgangsbasis erhalten.

[Baseline](../../artifacts/a12-05-f2-r1/baseline/), [Integritätsvergleich](../../artifacts/a12-05-f2-r1/results/integrity.json). Das historische Manifest schließt Paket-/HTTP-/CLI-Caches, bin/obj und Git-Metadaten aus; es ist kein Vollhash aller historischen generierten Binärdateien.

Dies ist eine erneute technische Prüfung im selben Chat, keine personell unabhängige Zweitbegutachtung. Implementierung und Assertions wurden gelesen; der bestehende Harness wurde erneut mit eigenen Evidenzpfaden ausgeführt und durch zusätzliche eigene Kontrollen ergänzt. Frühere Erfolgslogs ersetzen keine aktuellen Läufe. Der komplette Restore-/Build-/Testlauf verwendet einen neuen NuGet-Cache, HTTP-Cache und CLI-Home unter `artifacts/a12-05-f2-r1`. SDK: 10.0.401. [Reproduktionsskript](../../artifacts/a12-05-f2-r1/Reproduce.ps1).

## Bewertung der drei Korrekturen

### F01 – Lockauswahl: bestätigt

Der frühe Import setzt bei fehlender Configuration Debug vor der Pfadbildung. Die tatsächlichen Propertyabfragen für Default/Debug/Release und Anwendung/Prism-Outer/Prism-Inner ergeben die jeweiligen Konfigurationsverzeichnisse. Fehlende Locks werden durch den eigenen Existenzguard abgewiesen; beschädigte Locks scheitern im Locked Restore. Release bleibt bei beschädigtem Debug-Lock funktionsfähig. Kein Ersatzlock entsteht im unkonfigurierten Lockroot.

Zusätzlich wurde ein existierender Release-Lock explizit als Lockpfad für den Default-Debug-Restore angegeben: Exit 1 durch die Pfadvalidierung. [Eigene Gegenprobe](../../artifacts/a12-05-f2-r1/results/wrong-lock.log). Der echte Default-Restore mit frischem Cache und ein vollständiger `dotnet build` ohne Configuration einschließlich implizitem Restore bestehen. Die Korrektur ist damit nicht nur anhand des XML-Texts bestätigt.

### F02 – Isolation und Umgebung: bestätigt

Die Allowlist enthält neun vorgesehene CryptoWorkBench-Projekte und vier konkrete Prism-Projektpfade. NormalizePath/MakeRelative, Separatornormalisierung und Windows-Case-Normalisierung binden die Policy an vollständige Projektidentitäten. Eine fremde gleichnamige Datei reicht nicht aus. Die isolierte Fremdanwendung behält ihre Lock-, Pack-, Versionierungs- und Auditproperties bytegleich in der Propertyausgabe; ihr Restore schreibt keinen Integrationslock. Ein fremdes `Prism.Events.csproj` erhält keinen Git-Tasks-Pin.

Enter prüft Konflikte vor Umgebungsmutationen und setzt nur die beiden DirectoryBuild-Hooks. Versionierung/Audit/Pack und Cache-Normalisierung erfolgen im vorgesehenen Projektkontext. Bereits gesetzte konfliktbehaftete Hooks werden nicht ersetzt; andere Callerwerte bleiben erhalten. Idempotentes Enter, Leave und der Fehlerpfad des Buildwrappers bestehen. Die Wiederherstellung betrifft die Shell; bereits gestartete Kindprozesse behalten ihre geerbte Umgebung, wie dokumentiert.

Eigene zusätzliche MSBuild-Abfragen mit vollständig großgeschriebenem Anwendungspfad sowie einem Pfad mit `../` ergeben unter Windows denselben Scope und Debug-Lock. [Großschreibung](../../artifacts/a12-05-f2-r1/results/uppercase-path.json), [relativer Pfad](../../artifacts/a12-05-f2-r1/results/relative-path.json). Dies bestätigt die geprüften Windows-Pfade, keine allgemeine Unix- oder Dateisystem-Alias-Garantie.

### F03 – Quell- und Compilerinputprüfung: bestätigt

Prepare verifiziert HEAD und offizielles Origin sowie die gepinnten Dateiinhalte unabhängig vom Git-Statuscache und konfigurierbaren Clean-Filtern. Zulässig sind der exakte Git-Blobinhalt beziehungsweise ausschließlich CRLF→LF-normalisierte Bytes. Ein frischer offizieller Checkout des Commits `358118cd640d9a22ff8cf21c8ad197fa038b7990` wurde in der neuen Testfixture beschafft und geprüft.

Die ursprüngliche ignorierte `Debug/R1IgnoredSource.cs` wird abgewiesen. Reguläre bin/obj-Ausgaben der vier Projektroots bleiben zulässig; eine ausdrücklich zum Compile-Input erklärte `obj/unapproved.cs` wird abgewiesen. Die Prüfung beruht auf der ausgewerteten `@(Compile)`-Liste, die unmittelbar vor CoreCompile übergeben wird. Die zugelassenen generierten Dateien stammen aus den SDK-Properties für AssemblyInfo, GlobalUsings und Frameworkattribute, mit zusätzlicher Bindung an das eigene obj-Verzeichnis. Die erfolgreichen vollständigen Multi-Target-Rebuilds zeigen, dass reguläre SDK-Ausgaben nicht fälschlich blockiert werden.

Zusätzlich wurde ausschließlich in der neuen Fixture eine Junction unter `Prism.Events/obj` auf ein leeres Evidenzverzeichnis angelegt. Prepare lehnt sie mit dem konkreten Linkpfad ab: [Junction-Nachweis](../../artifacts/a12-05-f2-r1/results/junction.log). Der produktive Checkout wurde nicht manipuliert. Die dokumentierte Grenze gegenüber konkurrierender Dateimanipulation oder nicht vertrauenswürdigen Buildtools wird nicht als neuer Fehler gewertet.

## Gegenproben und Assertion-Prüfung

Alle **63 bestehenden gezielten Prüfungen bestehen erneut**: [Summary](../../artifacts/a12-05-f2-r1/regression/summary.json), [vollständige Protokolle](../../artifacts/a12-05-f2-r1/regression/). Die Assertions prüfen nicht nur Prozessstarts: Configuration/Lockpfad/Packwert werden aus MSBuild-JSON gelesen, fremde Properties vor/nach Enter verglichen, unerwünschte Lockdateien gesucht, PackageReferences auf den Pin geprüft und Umgebungswerte sowie Arbeitsverzeichnis nach Leave/Fehler kontrolliert.

Bei mehreren Negativfällen prüft der Harness zunächst den Exitcode. Die neuen Logs wurden ergänzend auf die erwarteten Ursachen geprüft: fehlender Lock mit eigener Fehlermeldung, beschädigter Lock mit NuGet-Fehler, unerwartete ignorierte Quelldatei und unerlaubter Compile-Input mit konkretem Pfad. Die zusätzlichen Kontrollen ergeben: falscher Lock Exit 1, Großschreibung Exit 0, relativer Pfad Exit 0, Junction Exit 1. [Zusammenfassung](../../artifacts/a12-05-f2-r1/results/additional-controls.json).

## Build-, Funktions- und Sicherheitsresultate

Beide Lösungen bestehen frische Locked Restores und vollständige Rebuilds in Debug und Release. Zusätzlich bestehen Default-Restore, Default-Build und Visual Studios installiertes MSBuild.exe mit Restore/Build. VS: 0 Fehler, 93 Warnungen; Default-Build: 0 Fehler, 87 Warnungen. Bestehende Nullable-/SourceLink-Warnungen bleiben in den Logs sichtbar und sind von Test-Skips zu unterscheiden.

| Release-Suite | Gesamt | PASS | FAIL | NotExecuted |
|---|---:|---:|---:|---:|
| GUI | 480 | 480 | 0 | 0 |
| Coordinator separat | 35 | 35 | 0 | 0 |
| F1 separat | 8 | 8 | 0 | 0 |
| Core vollständig | 7.672 | 7.667 | 0 | 5 |
| C1 separat | 9 | 9 | 0 | 0 |
| MCP vollständig | 90 | 89 | 0 | 1 |

[Ergebnisse](../../artifacts/a12-05-f2-r1/results/summary.json), [Fallnamen-/Outcomevergleich](../../artifacts/a12-05-f2-r1/results/case-outcome-comparison.json), [Befehle, TRX, Logs und Exitcodes](../../artifacts/a12-05-f2-r1/results/). Der Vergleich gegen F2 normalisiert ausschließlich die bekannten dynamischen MCP-Zeitstempel. Teilmengen werden nicht zur Gesamtsuite addiert. Die unveränderten Core-Skips betreffen drei Symlinkprivileg-Fälle, den macOS-ACL-Fall und den expliziten Hardwaretest; MCP überspringt weiterhin `InstalledCodexDiscoversAndCallsStatus`. Kein Test wurde geändert oder zusätzlich übersprungen.

Die erneut ausgeführte Probe startet das tatsächliche App/MainWindow mit DI, prüft alle fünf Regionen und InfoView→VariableView→InfoView sowie den echten IDialogService, DialogWindow-AXAML, Bindings, Parameter/Ergebnisse, CanCloseDialog, OnDialogOpened/OnDialogClosed, Fehlerweitergabe und normale Aufräumzyklen. Headless und native Windows-Ausführung bestehen. Der native Lauf bestätigt Handle **524492** und Nachrichtenverarbeitung; keine bekannte MissingMethodException. [Native Probe](../../artifacts/a12-05-f2-r1/results/native-windows.log). Die übernommenen synthetischen Testdialoge sind kein Volltest aller Produktdialoge. Ein zusätzlicher interaktiver Dateipicker-Smoke wurde in diesem Re-Review nicht erneut durchgeführt und wird nicht als aktuelle Evidenz angerechnet.

Die Framework-/Runtimegraphen entsprechen F2. Alle vier Prism-Projekte beziehen den Pin `Microsoft.Build.Tasks.Git/10.0.303`; der Diagnosebuild lädt LocateRepository und GetUntrackedFiles tatsächlich aus dieser DLL des neuen Caches. [Taskladungen](../../artifacts/a12-05-f2-r1/results/loaded-git-tasks.txt), [Graphvergleich](../../artifacts/a12-05-f2-r1/results/runtime-and-framework-graphs.json). NuGetAudit bleibt true/all; beide erneuten Audits melden keine bekannten verwundbaren Pakete. Keine neue Warnungsunterdrückung.

Alle 113 Paketversionen bestehen NuGet-Contenthash- und separate Archiv-SHA-512-Prüfung. Die vier Prism- und zwei Container-DLLs stimmen zwischen Desktop, Probe und der bisherigen Baseline per SHA-256 überein. [Pakethashes](../../artifacts/a12-05-f2-r1/package-contenthash-verification.json), [Binärvergleich](../../artifacts/a12-05-f2-r1/results/binary-verification.json). Alte inkompatible Prism-Avalonia-Pakete sind nicht in den Laufzeitgraph zurückgekehrt.

## Integrität, Grenzen und Abnahmeempfehlung

Die Abschlussprüfung bestätigt unveränderte vorhandene Produkt-/Builddateien, alle 1.180 offiziellen Prism-Dateien und die erfassten historischen Evidenzen. HEAD und Branch bleiben unverändert; die Staging Area bleibt leer. Insbesondere EditorCoordinator/EditorBridge, UiDispatch/ExecutionContext, Admission/Leases/Dokumentidentität, Undo/Redo, Core/Parser/Runner sowie MCP-Verträge und Ausführungspipeline sind bytegleich. Neu sind ausschließlich dieser Bericht und die Evidenzen unter `artifacts/a12-05-f2-r1/`.

Keine produktiven Änderungen, keine Änderungen an anderen Worktrees oder Actions-Workflows, kein Zugriff auf `.codex/config.toml`, kein Commit/Push/Merge/Reset/Rebase/Stash. Die Eigentümerentscheidung zu A12-04-F01 bleibt verbindlich: keine Retention-/GC-Root- oder Collectability-Untersuchung, keine erneute Fehler-/Risikoeinstufung dieses historischen Befunds.

Windows-CLI und VS-Buildengine sind aktuell geprüft. Interaktive VS-Designer-/IntelliSense-Funktion, Zorin/Rider und macOS sind nicht durch diese Läufe nachgewiesen. Linux/macOS bleiben OPEN; A12-03-Linux-Ergebnisse werden nicht angerechnet. Auch eine plattformübergreifende Actions-Matrix wurde nicht ausgeführt.

**F2 ACCEPTED:** Die drei ursprünglichen P2-Reproduktionen werden abgewehrt; im geprüften Umfang wurde kein neuer blockierender Mangel nachgewiesen. Formale Abnahme der F2-Korrekturen empfohlen, ohne Freigabe der offenen Plattformen vorwegzunehmen. Die zeitlich begrenzte Supportausnahme und der spätere verpflichtende Wechsel auf offizielle Prism-Pakete bleiben bestehen. Stopp zur Eigentümerentscheidung.
