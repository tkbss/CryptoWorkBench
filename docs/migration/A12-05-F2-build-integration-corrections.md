# A12-05-F2 – Prism Build Integration Corrections

Stand: 27. Oktober 2026. **Bewertung: F2 COMPLETE.**

Die drei P2-Findings aus [A12-05-R1](A12-05-R1-independent-review.md) sind korrigiert. Die 63 gezielten Korrekturprüfungen bestehen; Default-, Debug- und Release-Buildpfade, Visual-Studio-MSBuild, vollständige Regressionen und native Windows-Funktionsproben sind erfolgreich. Der Stand bleibt uncommittet für einen gezielten unabhängigen F2-Re-Review. Linux und macOS bleiben OPEN. Es erfolgt keine automatische Integrationsfreigabe.

## 1. Ausgangsbasis und Arbeitsgrenzen

Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`; Branch `feature/avalonia12-migration`; HEAD vor und nach F2: `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

Vor dem ersten Produktedit wurden der tatsächliche Git-Status einschließlich unversionierter Dateien, Branch/HEAD und staged/unstaged Binärdiffs gesichert. Alle 817 über Git ermittelten bestehenden Quell-/Konfigurationsdateien wurden kopiert und per SHA-256 gegen das Original geprüft; die vorhandenen uncommitteten Migration-/MCP-/AP2.1-Änderungen gehören vollständig zu dieser Ausgangsbasis. Außerdem wurden die 1.180 offiziellen Prism-Dateien und 8.485 historische Evidenzdateien gehasht. Sicherungsabschluss: 19:42:10 UTC / 20:42:10 Europe/Zurich.

Das historische F2-Manifest schließt generierte Verzeichnisse `packages`, `fresh-packages`, `http-cache`, `cli`, `fixture-cli`, `fixture-packages`, `bin`, `obj` und `.git` aus; es behauptet keinen neuen Vollhash aller alten Paket-/Binärcaches. Historische Berichte, Quellkopien, Testresultate und sonstige erfasste Evidenzen bleiben unverändert. [Ausgangsbasis](../../artifacts/a12-05-f2/baseline/), [abschließender Integritätsvergleich](../../artifacts/a12-05-f2/results/integrity.json).

Keine Änderungen an anderen bestehenden Worktrees, offiziellen Prism-Quelldateien, fachlichem Produktcode, vorhandenen Tests oder produktiven Actions-Workflows. `.codex/config.toml` wurde weder gelesen noch verändert. Die Staging Area bleibt leer; kein Commit, Push, Merge, Reset, Rebase oder Stash.

## 2. F01 – Default-Konfiguration und verpflichtende Locks

**Ursache:** Die frühere Lockpfadbildung erfolgte bei noch leerem `Configuration`. Das spätere SDK-Default `Debug` änderte den bereits gebildeten Pfad nicht. Dadurch konnte ein gewöhnlicher Restore einen neuen Lock im falschen Verzeichnis erzeugen und den vorgesehenen Debug-Lock umgehen.

**Korrektur:** [Lock.props](../../eng/prism/Lock.props) läuft nun vor den Directory.Build.props und setzt für die freigegebenen Projekte `Configuration=Debug`, sofern noch kein Wert vorliegt. Erst danach wird der konfigurierte Lockpfad gebildet. [PrismSecurity.targets](../../eng/prism/PrismSecurity.targets) prüft vor Restore/Build die unterstützte Konfiguration, den geladenen frühen Hook, die Existenz des erforderlichen Locks und die Übereinstimmung seines normalisierten Pfads mit Projekt/Konfiguration. Eine fehlende Datei wird ausdrücklich abgewiesen; beschädigte oder inkonsistente Locks werden im Locked Restore von NuGet abgewiesen. Ein Restore darf den fehlenden geprüften Lock nicht implizit neu anlegen.

Die frühe/anschließende Directory.Build-Importfolge gilt für Outer- und Inner-Builds. Auch die spätere Abschaltung von `GeneratePackageOnBuild` erfolgt damit in beiden Auswertungen. Eine andere Konfiguration als Debug/Release wird nicht stillschweigend einem Lock zugeordnet.

**Nachweis:** Ohne explizite Configuration ist der echte Anwendungsrestore mit frischem Cache erfolgreich. Zusätzlich besteht ein gewöhnliches `dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` einschließlich implizitem Restore ohne Configuration. Die isolierten Negativproben prüfen fehlende und beschädigte Debug- und Release-Locks sowie einen erfolgreichen Release-Restore trotz absichtlich beschädigtem Debug-Lock. Alle neun Kombinationen aus Default/Debug/Release und Anwendung/Prism-Outer/Prism-Inner verwenden den erwarteten Pfad. Im echten und isolierten `eng/prism/locks`-Root entstehen keine unkonfigurierten Locks.

Evidenz: [echter Default-Restore](../../artifacts/a12-05-f2/results/restore-default.log), [Default-Build](../../artifacts/a12-05-f2/results/build-default.log), [gezielte Prüfungen](../../artifacts/a12-05-f2/regression-verified/summary.json).

## 3. F02 – Projektidentität und begrenzte Umgebung

**Ursache:** Unbedingte Hook-Properties galten zuvor für alle Projekte eines vorbereiteten Prozesses. Der Security-Pin prüfte lediglich den Projektnamen. Zusätzlich setzte Enter mehrere unbeschränkte Umgebungsproperties und überschrieb vorhandene Hookdefinitionen.

**Korrektur:** Die kleine [ProjectScope.props](../../eng/prism/ProjectScope.props) enthält eine explizite Liste der neun vorgesehenen CryptoWorkBench-Projekte und der vier konkreten Prism-Projekte. Die Zuordnung verwendet den normalisierten vollständigen Projektpfad relativ zum Adapterroot, vereinheitlichte Separatoren und unter Windows die Groß-/Kleinschreibungsnormalisierung. Gleiche Dateinamen außerhalb dieser Pfade reichen nicht aus. Die Funktionsproperties, Lockpolicy und Auditsettings gelten nur für diese Projektmenge; der Security-Pin nur für die vier offiziellen Prism-Projektpfade. Der Root-Guard ist ebenfalls an den konkreten Anwendungspfad gebunden.

[Enter-Prism.ps1](../../eng/prism/Enter-Prism.ps1) setzt nur noch zwei Prozessvariablen:

| Variable | Externer Import | Zweck |
|---|---|---|
| `CustomBeforeDirectoryBuildProps` | `Lock.props` | Scope, Configuration/Lock und Versionierung vor Upstream-Referenzen festlegen |
| `CustomAfterDirectoryBuildProps` | `PrismSecurity.targets` | Nach Upstream-Props Paketierung auch im Outer-Build deaktivieren; Pin und Prüf-Targets installieren |

`DISABLE_GITVERSIONING`, `GeneratePackageOnBuild`, `NuGetAudit` und `NuGetAuditMode` werden nicht mehr als globale Prozesswerte gesetzt. Ein vom Aufrufer gesetztes `NUGET_PACKAGES` bleibt unverändert; allein `NuGetPackageRoot` wird im eigenen Projektkontext normalisiert. Das wurde auch mit einem Cachepfad ohne abschließenden Separator erfolgreich über Visual-Studio-MSBuild und den Default-Build geprüft.

Konflikte auf den beiden benötigten Hookvariablen verursachen einen Fehler, bevor die Umgebung verändert wird. Fremde Hookdefinitionen werden nicht ersetzt. Andere Hookvariablen bleiben erhalten. Eine alte A12-05-Umgebung wird erkannt; die Anleitung fordert dann einen neuen PowerShell-Prozess. Enter ist idempotent. Die neue [Leave-Prism.ps1](../../eng/prism/Leave-Prism.ps1) stellt die vorherigen Werte wieder her und überschreibt keine späteren bewussten Aufruferänderungen. [Build.ps1](../../eng/prism/Build.ps1) führt diese Wiederherstellung auch nach Fehlern durch, wenn es die Sitzung selbst vorbereitet hat. Es werden keine Benutzer-/Maschinenvariablen gespeichert.

**Nachweis:** Das fremde `OtherApplication.csproj` hat vor und nach Enter identische Lock-/Versionierungs-/Paketierungs-/Auditproperties. Sein Restore schreibt keinen Lock in die Integration. Das fremde `Prism.Events.csproj` erhält keinen Git-Tasks-Pin. Konfligierende Hooks bleiben erhalten, sonstige Callerwerte bleiben unverändert, Enter/Leave ist wiederholbar, und ein bewusst fehlgeschlagener Build stellt Hooks und Arbeitsverzeichnis wieder her. Unvorbereiteter Restore sowie einzeln fehlende Hooks scheitern weiter.

Evidenz: [unveränderte fremde Properties](../../artifacts/a12-05-f2/regression-verified/foreign-after.log), [fremde Namenskollision](../../artifacts/a12-05-f2/regression-verified/foreign-name-collision.log), [alle Umgebungstests](../../artifacts/a12-05-f2/regression-verified/summary.json), [Bedienungsanleitung](../../eng/prism/README.md).

## 4. F03 – Gepinnter Quellbaum und tatsächliche Compile-Inputs

**Ursache:** `git status --untracked-files=all` schließt ignorierte Dateien aus. Prisms Ignore-Regel für `Debug/` verdeckte die zusätzliche C#-Datei, während MSBuild sie als Compile-Input einschloss.

**Korrektur:** [Prepare-Prism.ps1](../../eng/prism/Prepare-Prism.ps1) prüft weiter HEAD, offizielles Origin und sauberen Status. Zusätzlich vergleicht es alle 1.180 Dateiinhalte mit dem Baum des unveränderten Pins `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Der Vergleich ist unabhängig von Index-Statusflags und konfigurierbaren Git-Clean-Filtern: Roh-Blobhash oder ausschließlich CRLF→LF-normalisierte Bytes müssen übereinstimmen. Diese eng begrenzte Normalisierung berücksichtigt die offiziellen Textattribute, ohne Quellcode umzuschreiben.

Zusätzliche ignorierte Dateien werden gesondert aufgelistet. Ausgenommen sind nur `bin/` und `obj/` direkt unter den vier tatsächlichen Prism-Projektverzeichnissen. `Debug/`, beliebige andere ignorierte Verzeichnisse und zusätzliche Dateien in den verlinkten WPF-Quellbäumen sind keine erlaubten Ausgaben. Symlinks/Junctions im Checkout werden abgewiesen. Die Prüfung löscht, repariert oder überschreibt nichts.

Vor jedem regulären Prism-`CoreCompile` schreibt der externe Hook die tatsächlich ausgewertete Compile-Liste und die drei SDK-Generatorkategorien in das jeweilige obj-Verzeichnis und ruft dieselbe Verifikation im Modus `-VerifyOnly` auf. Zugelassen sind gepinnte Quelldateien sowie die vom SDK benannten AssemblyInfo-, GlobalUsings- und Framework-Attribute-Dateien unter dem eigenen obj-Verzeichnis. Eine beliebige `obj/unapproved.cs` darf als ausgeschlossene Ausgabe existieren, wird aber bei expliziter Aufnahme in die Compilerliste abgewiesen. Design-Time-Auswertung führt diese ausführende Compile-Prüfung nicht aus.

**Nachweis:** Ein neuer offizieller Checkout wird erfolgreich vorbereitet. Die ursprüngliche `src/Prism.Events/Debug/R1IgnoredSource.cs` scheitert jetzt ausdrücklich. Gewöhnliche untracked Dateien, falsches Origin und beschädigte/leere Checkouts scheitern weiterhin. Reguläre bin/obj-Ausgaben bleiben zulässig; eine unerlaubte obj-Compilerquelle scheitert. Keine vorhandene offizielle Datei wurde für die Negativtests verändert. Die Compile-Listen aller vorhandenen Debug-/Release-Frameworkbuilds wurden mit der finalen Verifikation erneut geprüft; zusätzlich besteht ein finaler diagnostischer Rebuild.

Evidenz: [frischer Checkout](../../artifacts/a12-05-f2/regression-verified/prepare-fresh.log), [abgewiesene R1-Datei](../../artifacts/a12-05-f2/regression-verified/prepare-ignored-source.log), [abgewiesener obj-Input](../../artifacts/a12-05-f2/regression-verified/compile-unapproved-obj.log), [sämtliche Compile-Listen](../../artifacts/a12-05-f2/results/all-compile-inputs-final.json), [finale Quellprüfung](../../artifacts/a12-05-f2/results/final-source-check.log).

## 5. Vorher-/Nachher-Gegenproben und neue Regressionen

Das neue [Test-Integration.ps1](../../eng/prism/Test-Integration.ps1) reproduziert die Fälle in einem neuen Evidenzverzeichnis. Es kopiert die Buildintegration, erstellt eine minimale Anwendung an deren exakt freigegebener Projektidentität und beschafft Prism offiziell neu. Die tatsächlichen Produktlocks bleiben unangetastet. Die minimal gehaltene Fixture isoliert die Buildpolicy; vollständige Produktbuilds und Funktionsprüfungen erfolgen zusätzlich am echten Worktree.

| Fall | R1-Ausgangsbefund | Finaler F2-Nachweis |
|---|---|---|
| Default-Restore mit beschädigtem Debug-Lock | Exit 0, falscher neuer Lock | Exit ungleich 0, vorgesehener Lock wird geprüft |
| Fehlender Debug-/Release-Lock | Kein sicherer Existenzschutz | Verständlicher Fehler, keine Ersatzdatei |
| Default/Debug/Release, Outer/Inner | Default-Lockpfad ohne Configuration-Unterordner | Alle neun Pfade korrekt; Auto-Pack überall false |
| Fremde Anwendung im vorbereiteten Prozess | Fremde Properties geändert; Lock in Integration | Properties identisch; kein fremder Integrationslock |
| Fremdes `Prism.Events.csproj` | Security-Pin nach Namen | Kein Security-Pin |
| Bestehende Hookdefinition | Überschrieben | Konfliktfehler ohne Überschreiben |
| Ignorierte `Debug/R1IgnoredSource.cs` | Prepare erfolgreich, Compile-Item vorhanden | Prepare scheitert mit Dateipfad |
| Gewöhnliche untracked Datei / falsches Origin / beschädigter Checkout | Bereits abgewiesen | Weiterhin abgewiesen |
| Reguläre bin/obj-Ausgaben | Normaler Build benötigt sie | Zulässig; expliziter unerlaubter Compile-Input abgewiesen |
| Fehlende Hookhälfte | Restore scheitert | Weiterhin abgewiesen |
| Fehler im Buildwrapper | Keine automatische Umgebungsrückkehr | Hooks und Arbeitsverzeichnis wiederhergestellt |

**63/63 Checks bestanden** im finalen Lauf: [Summary](../../artifacts/a12-05-f2/regression-verified/summary.json), [Befehle und Logs](../../artifacts/a12-05-f2/regression-verified/). Die ursprünglichen Vorher-Nachweise bleiben unverändert unter [A12-05-R1](../../artifacts/a12-05-r1/results/).

Die F2-Entwicklungsprotokolle bleiben ebenfalls nachvollziehbar: Eine anfängliche MSBuild-Condition-Quotierung wurde korrigiert. Die erste Hookvariante ließ den äußeren Multi-Target-Build automatisch packen; die korrigierte After-DirectoryBuildProps-Platzierung verhindert das. Die dabei erzeugten zwei Events-Paketdateien wurden gezielt aus dem generierten Ausgabeverzeichnis nach [development-output](../../artifacts/a12-05-f2/development-output/) verschoben und nicht veröffentlicht. Die ursprünglichen Fehlerlogs sind unter `results/development-*.log` erhalten. Ein ungültiger CLI-Schalter im neuen Probe-Reproduktionsskript wurde entfernt; die Probe verwendet jetzt ihren normalen lokalen `packages.lock.json`. Keine dieser Zwischenvarianten ist der finale bewertete Stand.

## 6. Vollständige Builds und fachliche Regressionen

Die [Reproduktion](../../artifacts/a12-05-f2/Reproduce.ps1) verwendet eigene F2-Verzeichnisse für NuGet-Pakete, HTTP-Cache und CLI-Home sowie SDK 10.0.401. Beide Lösungen wurden in Debug und Release mit `--locked-mode --force --no-http-cache` restauriert und mit `--no-restore -t:Rebuild` vollständig gebaut. Alle vier Restores und vier Rebuilds bestehen. Zusätzlich bestehen Default-Restore, Default-Build einschließlich Restore sowie der tatsächliche Visual-Studio-MSBuild-Lauf mit Restore. VS meldet 0 Fehler und 93 Warnungen; der Default-Build meldet 0 Fehler und 87 Warnungen. Bestehende Nullable-/SourceLink-Warnungen bleiben sichtbar; COMPLETE bedeutet nicht Warnungsfreiheit.

| Suite auf Release | Gesamt | PASS | FAIL | NotExecuted |
|---|---:|---:|---:|---:|
| GUI | 480 | 480 | 0 | 0 |
| EditorCoordinator separat | 35 | 35 | 0 | 0 |
| F1 separat | 8 | 8 | 0 | 0 |
| Core vollständig | 7.672 | 7.667 | 0 | 5 |
| C1 separat | 9 | 9 | 0 | 0 |
| MCP vollständig | 90 | 89 | 0 | 1 |

[TRX-Zusammenfassung](../../artifacts/a12-05-f2/results/summary.json), [Fallnamen-/Ergebnisvergleich](../../artifacts/a12-05-f2/results/case-outcome-comparison.json), [Befehle, Logs und Exitcodes](../../artifacts/a12-05-f2/results/), [VS-Log](../../artifacts/a12-05-f2/results/visual-studio-msbuild.log).

Alle Fallnamen und Outcomes entsprechen R1 und damit der dort verifizierten A12-05-Baseline. Nur variable MCP-Datumswerte im Format `dd/MM/yyyy HH:mm:ss +/-HH:mm` werden für den Vergleich normalisiert. Keine Testquelle wurde bearbeitet. Die unveränderten Core-NotExecuted betreffen drei Symlinkprivileg-Fälle, den macOS-ACL-Fall und den expliziten Hardwaretest; MCP überspringt weiterhin `InstalledCodexDiscoversAndCallsStatus`. Die Core-Konsole zählt wegen des expliziten Hardwaretests 7.671 statt der 7.672 TRX-Resultate. Teilmengen werden nicht zusätzlich zur Gesamtsuite addiert.

Die [Prism-Probe](../../artifacts/a12-05-f2/probe/) ist eine unveränderte Kopie der funktionalen R1-Probe, referenziert das tatsächliche Desktopprojekt und hat einen eigenen lokalen Lock außerhalb der Produkt-Allowlist. Sie besteht headless und nativ: App/MainWindow/DI, alle fünf Regionen, InfoView → VariableView → InfoView, tatsächlicher IDialogService und DialogWindow-AXAML, Title-/Content-Bindings, Parameter und Ergebnisse, CanCloseDialog-Veto, OnDialogOpened/OnDialogClosed, Fehlerweitergabe sowie normale Schließ-/Aufräumzyklen. Der native Lauf bestätigt ein vorhandenes Windows-Handle und Nachrichtenschleifenbetrieb; die bekannte MissingMethodException tritt nicht auf. Die synthetischen Testdialoge sind kein Volltest sämtlicher Produktdialoge.

Zusätzlich besteht der interaktive Desktop-Smoke mit `VAR correctionF2=0x(EF)`, Undo/Redo, Variable-/Info-Region, nativem Speicherdialog, Abbruch und regulärem Beenden. Keine Datei wurde gespeichert. [Headless](../../artifacts/a12-05-f2/results/headless.log), [nativer Prism-Lauf](../../artifacts/a12-05-f2/results/native-windows.log), [interaktives Journal](../../artifacts/a12-05-f2/native-desktop-smoke.md).

## 7. Paketgraph, Security-Pin und Integrität

Die gespeicherten Debug-/Release-Assets stimmen nach Paketidentität, Library-Typ und Contenthash mit R1 überein. Alle Frameworks der vier Prism-Projekte lösen ausschließlich `Microsoft.Build.Tasks.Git/10.0.303` auf. Der finale diagnostische Rebuild lädt `LocateRepository` und `GetUntrackedFiles` tatsächlich aus der 10.0.303-DLL des F2-Caches; keine alternative 10.0.300-Tasks-DLL wird dort geladen. SourceLink-Pakete mit Version 10.0.300 sind andere Paketidentitäten.

NuGetAudit bleibt true/all. Beide erneuten Audits melden keine bekannten verwundbaren Pakete im abgefragten Graphen. Keine Warnungsunterdrückung wurde ergänzt. Alle 113 Paketversionen bestehen den NuGet-Contenthashvergleich gegen die Locks und separat den Archiv-SHA-512-Vergleich gegen `.nupkg.sha512`.

Der Runtimegraph enthält die vier zusammengehörigen Prism-Projekte aus dem gepinnten Baum, keine alten inkompatiblen Prism-Avalonia-Pakete. Container.Abstractions/Container.DryIoc bleiben 9.0.114, DryIoc bleibt 5.4.3, die Anwendung bleibt bei Avalonia 12.1.4. Die vier Prism- und zwei Container-DLLs stimmen zwischen tatsächlichem Desktop, neuer Probe und R1 per SHA-256 überein. Damit bleiben die getesteten Laufzeitbinärdateien trotz korrigierter Buildpolicy identisch.

Evidenz: [Runtime-/Frameworkgraphen](../../artifacts/a12-05-f2/results/runtime-and-framework-graphs.json), [geladene Tasks](../../artifacts/a12-05-f2/results/loaded-git-tasks.txt), [finaler Diagnosebuild](../../artifacts/a12-05-f2/results/task-load-final.log), [Paket-Hashes](../../artifacts/a12-05-f2/package-contenthash-verification.json), [Binärvergleich](../../artifacts/a12-05-f2/results/binary-verification.json), [Audit Avalonia](../../artifacts/a12-05-f2/results/audit-avalonia.log), [Audit Core/MCP](../../artifacts/a12-05-f2/results/audit-core.log).

Die Abschlussprüfung zeigt exakt sieben geänderte Bestandsdateien gegenüber der F2-Sicherung, ausschließlich aus der Buildintegration. Alle 1.180 Prism-Quelldateien sowie alle 8.485 erfassten historischen Evidenzdateien bleiben SHA-256-identisch. Alle Produktlocks bleiben unverändert. EditorCoordinator, EditorBridge, UiDispatch, Admission/Leases, Dokumentidentität/Kontrollgeneration, Undo-/Redo-Delegation, ExecutionContext, CryptoScript-Core/Parser/Runner und MCP-Schnittstellen/Ausführungspipeline bleiben bytegleich.

## 8. Geänderte Dateien und Re-Review-Paket

| Datei | F2-Änderung |
|---|---|
| `Directory.Build.targets` | Guard an konkreten Anwendungspfad binden |
| `eng/prism/Lock.props` | Scope, früher Debug-Default, konfigurationsgenauer Lock, lokal normalisierter Cachepfad |
| `eng/prism/PrismSecurity.targets` | Scope/Pin, Outer-/Inner-Packpolicy, Lockvalidierung und Compilerinputprüfung |
| `eng/prism/Prepare-Prism.ps1` | Gepinnte Dateiinhalte, ignorierte Zusätze, Linkprüfung und Compile-Inputs verifizieren |
| `eng/prism/Enter-Prism.ps1` | Zwei begrenzte Hooks; Konflikterkennung und vorherige Werte erhalten |
| `eng/prism/Build.ps1` | Umgebung im finally wiederherstellen |
| `eng/prism/README.md` | Korrigierte Bedienung, Tests, Grenzen und späterer Rückbau |
| `eng/prism/ProjectScope.props` (neu) | Explizite normalisierte Identitäten der 13 vorgesehenen Projekte |
| `eng/prism/Leave-Prism.ps1` (neu) | Vorherige Hookwerte wiederherstellen |
| `eng/prism/Test-Integration.ps1` (neu) | Reproduzierbare isolierte Regressionen |
| Dieser Bericht (neu) | F2-Ergebnis und Evidenzzuordnung |

Neue Evidenzen liegen ausschließlich unter `artifacts/a12-05-f2/`. Das [Re-Review-Paket](../../artifacts/a12-05-f2/review/) enthält die finalen Dateien, Diffs der sieben Bestandsdateien gegen die F2-Sicherung und ein SHA-256-Manifest. [Verifikationsskript](../../artifacts/a12-05-f2/Verify-Results.ps1).

## 9. Grenzen, Eigentümerentscheidung und Abschluss

Die geprüfte Ausführungsplattform ist Windows. Visual Studios tatsächliche MSBuild-Engine besteht; ein vollständiger interaktiver Designer-/IntelliSense-Test wurde nicht vorgenommen. Zorin Linux/Rider und macOS bleiben OPEN, bis dieser Kandidat dort tatsächlich restauriert, gebaut und nativ ausgeführt wurde. Alte A12-03-Linux-Ergebnisse zählen hierfür nicht. Es wurden keine produktiven GitHub-Actions-Workflows geändert oder neue plattformübergreifende CI-Läufe behauptet.

Die Quellprüfung ist keine Sandbox gegen einen bösartigen SDK-/NuGet-Build oder konkurrierende Dateimanipulation zwischen Prüfung und Compilerzugriff. Standard-SDK-/NuGet-generierte Imports und die explizit benannten SDK-Compilerdateien bleiben vertrauenswürdige Buildbestandteile. Abweichende Ausgabelayouts außerhalb der vorgesehenen obj-Verzeichnisse, zusätzliche Projekte und andere Konfigurationen erfordern eine bewusste Anpassung der kleinen Allowlist/Policy. Neue IDE-Prozesse müssen die vorbereitete Umgebung erben; Leave ändert keine bereits laufenden Kindprozesse.

Die Eigentümerentscheidung zu **A12-04-F01** bleibt verbindlich. Dieser historische Befund wird weder als Produktfehler noch als Risiko oder Freigabeblocker verwendet. Es gab keine Retention-/GC-Root-Untersuchung und keine Collectability-Stresstests. Normale Dialog-Funktion und Lebenszyklen wurden erneut geprüft; historische Messergebnisse bleiben erhalten.

Die gemeldete Supportausnahme gilt weiterhin nur bis zur offiziellen Avalonia-12-Veröffentlichung, angekündigt für November 2026. Danach ist die Rückkehr zu den zusammengehörigen offiziellen Prism-NuGet-Paketen verbindlich. Der Adapter einschließlich Scope-/Verifikationshooks ist über die aktualisierte README vollständig ablösbar; die fachliche Anwendung wurde dafür nicht verändert.

**F2 COMPLETE:** Alle drei beauftragten Findings sind korrigiert und die erforderlichen Windows-Regressionen bestehen. Der Arbeitsstand ist für den gezielten unabhängigen F2-Re-Review bereitgestellt. Stopp zur Eigentümerentscheidung, ohne Commit oder automatische Fortsetzung weiterer Arbeitspakete.

