# A12-05-R1 – Independent Temporary Prism Integration Review

Stand: 27. Oktober 2026. **Bewertung: CHANGES REQUIRED.**

Der tatsächlich integrierte Kandidat besteht die erneut ausgeführten Windows-Builds, Regressionen und Prism-Funktionsprüfungen. Drei reproduzierte P2-Befunde verhindern die vorbehaltlose Annahme der Buildintegration: Ein Restore ohne explizite Configuration verwendet den falschen Lockpfad, die MSBuild-Hooks beeinflussen fremde Projekte im selben Prozess, und die Checkoutprüfung akzeptiert zusätzliche ignorierte Compilerquellen. Es wurden keine Produktkorrekturen vorgenommen. Die Ergebnisse werden zur Eigentümerentscheidung vorgelegt.

## 1. Gegenstand und unabhängige Reproduktion

Geprüfter Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Die bereits vorhandenen uncommitteten Änderungen aus Migration, MCP und AP2.1 gehören zur Ausgangsbasis.

Die Bewertung beruht auf erneut gelesenen Implementierungsdateien, einem neuen offiziellen Prism-Fetch, separatem NuGet-/HTTP-Cache, neuen Rebuilds, Testläufen und gezielten Negativproben. Historische Erfolgslogs wurden nicht als Ersatz für diese Ausführung verwendet. Der Review erfolgte im selben Chat wie die Implementierung; er ist eine unabhängige technische Reproduktion, keine personell unabhängige Zweitbegutachtung. Der funktionale Probe-Harness wurde aus A12-05 übernommen, seine Assertions geprüft und gegen den aktuellen Produktstand erneut ausgeführt. Diese Herkunft wird ausdrücklich offengelegt.

Vor den Prüfungen wurden Git-Status, HEAD, Branch, staged/unstaged Diffs und 816 über Git ermittelte vorhandene Quell-/Konfigurationsdateien gesichert und gehasht. Die Staging Area war leer. Hinzu kommen 1.180 offizielle Prism-Dateien und 4.319 historische Evidenzdateien. Das historische R1-Manifest schließt generierte Verzeichnisse mit den Segmenten `packages`, `fresh-packages`, `cli`, `bin` und `obj` aus; es ist kein neuer Vollhash aller alten Binär-/Paket-Caches. Sicherungsabschluss: 19:19:34 UTC / 20:19:34 Europe/Zurich.

Evidenz: [R1-Ausgangsbasis](../../artifacts/a12-05-r1/baseline/), [Quellintegrität](../../artifacts/a12-05-r1/results/source-integrity.json), [abschließende Historien-/Git-Prüfung](../../artifacts/a12-05-r1/results/integrity-final.json).

## 2. Neue Findings

Alle Kennungen in diesem Abschnitt gehören zu **A12-05-R1**. Insbesondere R1-F01 ist ein neuer Lockfile-Befund und hat keinen Bezug zur verbindlich abgeschlossenen Eigentümerentscheidung über **A12-04-F01**.

### A12-05-R1-F01 — P2: Default-Restore umgeht den vorgesehenen Debug-Lock

**Position:** [eng/prism/Lock.props](../../eng/prism/Lock.props), Zeile 10; dokumentierter Bedienweg in [eng/prism/README.md](../../eng/prism/README.md), Zeile 38.

**Ursache:** `NuGetLockFilePath` wird beim frühen Import durch `CustomAfterMicrosoftCommonProps` aus `$(Configuration)` gebildet. Ohne expliziten globalen Configuration-Wert ist dieser zu diesem Zeitpunkt leer. Die spätere SDK-Vorgabe `Debug` korrigiert den bereits interpolierten Pfad nicht. Die abschließend evaluierte Anwendung zeigt deshalb gleichzeitig `Configuration=Debug` und `eng/prism/locks//CryptoWorkBenchAvalonia.lock.json`. Dasselbe tritt bei der äußeren und inneren Prism-Auswertung auf.

**Reproduktion:** In einer vorbereiteten PowerShell lässt sich der falsche Pfad schon ohne Restore erkennen:

```powershell
. ./eng/prism/Enter-Prism.ps1
dotnet msbuild CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj '-getProperty:Configuration,NuGetLockFilePath,RestoreLockedMode'
```

Der tatsächliche Bypass wurde ausschließlich in der vollständigen isolierten Kopie `artifacts/a12-05-r1/default-restore/app` geprüft. Dort wurde nur der erwartete Anwendungslock unter `eng/prism/locks/Debug` absichtlich durch ungültigen Text ersetzt. Aus dem Root dieser Kopie, nach Vorbereitung ihrer eigenen Hooks:

```powershell
dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj --locked-mode --force
dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj --locked-mode --force -p:Configuration=Debug
```

Ergebnis: Der erste Befehl gelingt mit Exit 0 und erzeugt einen neuen Lock direkt unter `eng/prism/locks`; der beschädigte vorgesehene Debug-Lock bleibt ungelesen. Der zweite Befehl verwendet den erwarteten Lock und scheitert mit Exit 1 / NU1004. Das Original-Lockfile im Produktworktree blieb unverändert.

**Auswirkung:** Der ausdrücklich dokumentierte normale CLI-Restore und Builds mit implizitem Restore ohne Configuration garantieren den geprüften Debug-Paketgraphen nicht. Ein fehlender Lock am versehentlich gewählten Pfad wird trotz Locked Mode neu erzeugt. Die erfolgreichen Läufe von `Build.ps1` mit expliziter Configuration widerlegen diesen Fehler nicht; dieser Bedienweg ist nicht betroffen.

**Empfohlene Korrektur:** Configuration vor Bildung des Lockpfads zuverlässig bestimmen oder die Pfadbildung an einen geeigneten späteren Auswertungspunkt verlegen, einschließlich Multi-Target-Outer-Build. Zusätzlich die Existenz des vorgesehenen Locks erzwingen. Regression: Restore/Build ohne `-c` beziehungsweise `-p:Configuration` muss den Debug-Lock verwenden und bei fehlendem oder beschädigtem vorgesehenem Lock scheitern.

**Evidenz:** [Propertyauswertung](../../artifacts/a12-05-r1/results/default-configuration-app.json), [Prism outer](../../artifacts/a12-05-r1/results/default-configuration-prism-outer.json), [Prism inner](../../artifacts/a12-05-r1/results/default-configuration-prism-inner.json), [Zusammenfassung](../../artifacts/a12-05-r1/results/default-lock-bypass-summary.json), [Default-Restore](../../artifacts/a12-05-r1/results/default-restore-corrupt-debug-lock.log), [expliziter Debug-Restore](../../artifacts/a12-05-r1/results/explicit-debug-corrupt-lock.log).

### A12-05-R1-F02 — P2: Prozessweite Hooks verändern fremde Projekte

**Position:** [eng/prism/Lock.props](../../eng/prism/Lock.props), Zeilen 2–10; [eng/prism/PrismSecurity.targets](../../eng/prism/PrismSecurity.targets), Zeilen 3–8; [eng/prism/Enter-Prism.ps1](../../eng/prism/Enter-Prism.ps1), Zeilen 5–10.

**Ursache:** Die über Umgebungsvariablen importierten Hooks gelten für alle MSBuild-Projekte dieses Prozesses und seiner Nachkommen. Die PropertyGroups haben keine Bedingung auf den vorgesehenen Repository-/Projektpfad. Der Security-Pin prüft ausschließlich vier Projektnamen, nicht deren Herkunft. `Enter-Prism.ps1` setzt zusätzlich unbeschränkte Umgebungsproperties und überschreibt vorhandene Hookwerte ohne Wiederherstellung.

**Reproduktion:** Unter `artifacts/a12-05-r1/foreign` liegt ein unabhängiges minimales `OtherApplication.csproj` mit `TargetFramework=net10.0` und `GeneratePackageOnBuild=true`. Die Hooks einer isolierten Integrationskopie unter `preparation/clean` werden in den Prozess geladen. Zum Ausschluss der Eltern-Directory.Build-Dateien werden die normalen Verzeichnisimporte deaktiviert:

```powershell
. ./artifacts/a12-05-r1/preparation/clean/eng/prism/Enter-Prism.ps1
dotnet msbuild artifacts/a12-05-r1/foreign/OtherApplication.csproj -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false '-getProperty:NuGetLockFilePath,RestoreLockedMode,GeneratePackageOnBuild,DISABLE_GITVERSIONING'
dotnet restore artifacts/a12-05-r1/foreign/OtherApplication.csproj -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false
```

Ergebnis: Das fremde Projekt erhält `RestoreLockedMode=true`, `DISABLE_GITVERSIONING=true` und entgegen seiner eigenen Einstellung `GeneratePackageOnBuild=false`. Sein erfolgreicher Restore schreibt `OtherApplication.lock.json` in das Lockverzeichnis der Integrationskopie. Ein zweites fremdes Projekt namens `Prism.Events.csproj` erhält darüber hinaus den privaten PackageReference-Pin für `Microsoft.Build.Tasks.Git [10.0.303]`. Die Auswertung weist den externen Security-Hook als definierende Datei aus. Das Problem besteht auch mit expliziter Configuration; dann liegt der fremde Lock im entsprechenden Konfigurationsunterordner.

**Auswirkung:** Eine andere Lösung, die aus derselben vorbereiteten Shell oder in einem entsprechend gestarteten IDE-Prozess gebaut wird, kann ungefragt andere Lock-, Versionierungs- und Pack-Einstellungen erhalten. Bei Namenskollision wird auch ihr Abhängigkeitsgraph verändert. Die README-Empfehlung einer separaten Shell begrenzt das Bedienrisiko, erfüllt aber keine technische Eingrenzung. Dies ist keine maschinenweite oder dauerhaft gespeicherte Umgebungsänderung: betroffen sind der vorbereitete Prozess und seine Nachkommen. Es wurde kein anderer bestehender Worktree angefasst.

**Empfohlene Korrektur:** Sämtliche Properties und Items auf die vorgesehenen kanonischen Projektpfade beziehungsweise eine klar begrenzte Projektmenge konditionieren. Den Security-Pin an die vier konkreten Projekte im verifizierten Checkout binden. Unbeschränkte Umgebungsproperties vermeiden; bestehende Hookwerte und die Umgebung beim Verlassen wiederherstellen oder einen begrenzten Build-Kindprozess verwenden. Negative Regressionen für ein fremdes Projekt und eine gleichnamige fremde Prism-Datei ergänzen.

**Evidenz:** [fremde Properties](../../artifacts/a12-05-r1/results/foreign-properties.json), [tatsächlicher fremder Restore](../../artifacts/a12-05-r1/results/foreign-restore.log), [Lockschreibzugriff](../../artifacts/a12-05-r1/results/foreign-impact.json), [Namenskollision](../../artifacts/a12-05-r1/results/foreign-name-collision.json).

### A12-05-R1-F03 — P2: Ignorierte zusätzliche Compilerquelle besteht die Checkoutprüfung

**Position:** [eng/prism/Prepare-Prism.ps1](../../eng/prism/Prepare-Prism.ps1), Zeilen 23–24.

**Ursache:** `git status --porcelain --untracked-files=all` meldet keine ignorierten Dateien. Die offiziellen Ignore-Regeln und die MSBuild-Compile-Globs sind nicht deckungsgleich. Eine durch Git ignorierte Datei kann deshalb dennoch in die Kompilierung eingehen. HEAD, Origin und sauberer Git-Status beweisen nicht, dass sämtliche Compilerinputs aus dem gepinnten Baum stammen.

**Reproduktion:** `preparation/clean` wurde mit dem unveränderten Vorbereitungsskript frisch vom offiziellen Repository erzeugt. Alle 1.180 ursprünglichen Dateien stimmen mit dem tatsächlichen Produktcheckout überein. Ausschließlich in dieser neuen Evidenzkopie wurde anschließend die zusätzliche harmlose Markerdatei `.deps/prism/src/Prism.Events/Debug/R1IgnoredSource.cs` angelegt; kein vorhandener offizieller Quelltext wurde bearbeitet. Inhalt:

```csharp
namespace R1ReviewOnly { internal sealed class IgnoredSourceMarker { } }
```

Git ignoriert sie durch die Upstream-Regel `[Dd]ebug/`. Aus dem Root der Evidenzkopie:

```powershell
pwsh ./eng/prism/Prepare-Prism.ps1
. ./eng/prism/Enter-Prism.ps1
dotnet msbuild .deps/prism/src/Prism.Events/Prism.Events.csproj -p:Configuration=Release -p:TargetFramework=net10.0 -getItem:Compile
```

Ergebnis: Prepare endet mit Exit 0 und meldet den offiziellen Stand als verifiziert, obwohl die Compile-Liste `Debug/R1IgnoredSource.cs` enthält. Die zusätzliche Datei wurde nicht in die Produktbinärdateien eingebaut. Vergleichsproben mit einer gewöhnlichen untracked Datei, falschem Origin und einer leeren/beschädigten Checkoutstruktur scheitern jeweils korrekt mit Exit 1.

**Auswirkung:** Die Vorbereitung kann zusätzliche lokale Compilerinputs fälschlich als unveränderten offiziellen Quellstand akzeptieren. Damit ist die zugesagte Zurückweisung eines veränderten Checkouts nicht vollständig. Der tatsächlich geprüfte Produktcheckout ist unverändert; dies ist ein reproduzierbarer Mangel der Verifikationslogik, kein Nachweis manipulierter Produktbinärdateien.

**Empfohlene Korrektur:** Die relevanten Buildinputs einschließlich ignorierter Quelldateien gegen den gepinnten Git-Baum prüfen oder den Build aus einem verifizierten, isolierten Quellbaum mit getrennten Ausgaben durchführen. Generierte `bin`-/`obj`-Dateien kontrolliert zulassen; ein pauschales Verbot sämtlicher ignorierter Dateien wäre nach normalen Builds ungeeignet. Die vorliegende Negativprobe muss nach einer Korrektur abgewiesen werden.

**Evidenz:** [frischer offizieller Fetch](../../artifacts/a12-05-r1/results/prepare-fresh.log), [Quellvergleich](../../artifacts/a12-05-r1/results/official-source-comparison.json), [akzeptierte Markerdatei](../../artifacts/a12-05-r1/results/prepare-ignored-source.log), [Ignore-Regel](../../artifacts/a12-05-r1/results/ignored-source-rule.txt), [Compile-Items](../../artifacts/a12-05-r1/results/ignored-source-compile-items.json), [Negativproben](../../artifacts/a12-05-r1/results/preparation-negative-summary.json).

## 3. R1/R2 – Quellintegration, MSBuild und Locks

Der tatsächliche `.deps/prism`-Checkout entspricht dem offiziellen Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Alle 1.180 Dateien stimmen per SHA-256 mit der unabhängig neu beschafften offiziellen Kopie überein. Es gibt keine lokalen Produkt-Prism-Patches. Die vier zusammengehörigen Projekte stammen aus diesem Baum; die zwei relativen ProjectReferences in der Anwendung und vier Lösungseinträge führen dorthin. Die geprüften Produkt-/Builddateien enthalten keine absoluten Benutzer-/Laborpfade: [Pfadprüfung](../../artifacts/a12-05-r1/results/portable-path-review.json). Pfade in Ausführungslogs und Testevidenzen sind keine Produktabhängigkeiten.

Die Pfadbildung mit Repositoryroot, `Join-Path` und relativen Projektverweisen ist grundsätzlich für PowerShell 7 auf anderen Plattformen geeignet. Tatsächliche Unix-Auflösung und IDE-Ausführung bleiben separat nachzuweisen. Prepare führt keinen Reset oder Überschreibschritt für vorhandene falsche Checkouts aus; die unabhängigen Kontrollen für untracked Datei, falsches Origin und beschädigte Struktur bestehen. Die Lücke für ignorierte Eingaben steht in R1-F03.

Die neue [Reproduktion](../../artifacts/a12-05-r1/Reproduce.ps1) nutzt .NET SDK 10.0.401 und eigene Verzeichnisse unter `artifacts/a12-05-r1` für Pakete, HTTP-Cache und CLI-Home. Beide Lösungen wurden in **Debug und Release** jeweils mit `--locked-mode --force --no-http-cache -p:Configuration=<Konfiguration>` restauriert und anschließend mit `--no-restore -t:Rebuild` vollständig gebaut. Alle acht Restore-/Build-Schritte bestehen. Die NuGet-Quelle ergibt sich aus der vorhandenen Root-NuGet.Config mit nuget.org; kein privater Feed wurde eingeführt.

Prism.Core und Prism.Events wurden für ihre unveränderten TFMs net462/net47/net8.0/net9.0/net10.0 ausgewertet und gebaut; die zwei Avalonia-Projekte für net9.0/net10.0. Outer-/Inner-Auswertung und konfigurationsspezifische Graphen funktionieren bei expliziter Configuration. Die 13 Debug- und 14 Release-Lockfiles bleiben bytegleich. Alle aus A12-04 übernommenen Locks stimmen mit ihrer Herkunft überein; nur der Debug-Core-Testlock wurde nachvollziehbar ergänzt. Release enthält zusätzlich den historischen Bootstrap-Probelock. [Lockherkunft](../../artifacts/a12-05-r1/results/lock-provenance.json), [Graphvergleich](../../artifacts/a12-05-r1/results/runtime-and-framework-graphs.json).

Die Guard-Prüfung besteht sowohl bei direktem Targetaufruf als auch bei echtem Restore in der isolierten Anwendungskopie: Ohne beide Hooks, ohne Props oder ohne Targets jeweils Exit 1 mit verständlicher Vorbereitungsmeldung; mit beiden Hooks gelingt das Target. [Target-Guards](../../artifacts/a12-05-r1/results/guard-summary.json), [Restore-Guards](../../artifacts/a12-05-r1/results/restore-guard-summary.json).

Der erneute Build mit dem tatsächlich installierten Visual-Studio-MSBuild.exe einschließlich Restore besteht mit 0 Fehlern und 43 Warnungen. Ein neu gestarteter IDE-Prozess muss die vorbereitete Umgebung erben. Die dokumentierte Voraussetzung ist plausibel und durch den VS-Buildengine-Lauf gestützt; ein vollständiger interaktiver Visual-Studio-Designer-/IntelliSense-Test und ein Rider-Lauf wurden nicht durchgeführt. Die Warnungen bleiben im [VS-Log](../../artifacts/a12-05-r1/results/visual-studio-msbuild.log) sichtbar. R1-F01 und R1-F02 bleiben unabhängig von diesen erfolgreichen expliziten Builds bestehen.

## 4. R3 – Build-Sicherheit, Paketidentitäten und Runtimegraph

Im tatsächlichen Debug-/Release-Graphen lösen alle deklarierten Frameworks der vier Prism-Projekte `Microsoft.Build.Tasks.Git` ausschließlich auf **10.0.303** auf. Der diagnostische Rebuild lädt `GetUntrackedFiles` und `LocateRepository` nachweislich aus der 10.0.303-DLL des neuen Review-Caches: [tatsächlich geladene Tasks](../../artifacts/a12-05-r1/results/loaded-git-tasks.txt), [vollständiges Diagnoseprotokoll](../../artifacts/a12-05-r1/results/task-load.log). Dort wurde keine alternative 10.0.300-Task-DLL geladen. Der externe Pin bleibt `PrivateAssets=all`; `Microsoft.SourceLink.* 10.0.300` ist eine andere Paketidentität und wird nicht mit der korrigierten Tasks-DLL gleichgesetzt.

NuGetAudit bleibt auf `true`, Modus `all`; keine neue Auditwarnungsunterdrückung wurde eingeführt. Beide erneut ausgeführten Audits melden keine bekannten verwundbaren Pakete im geprüften Graphen: [Avalonia](../../artifacts/a12-05-r1/results/audit-avalonia.log), [Core/MCP](../../artifacts/a12-05-r1/results/audit-core.log). Dies ist der Stand der abgefragten Auditdaten, keine allgemeine Sicherheitsgarantie. Die technische Begrenzung des Pins auf ausschließlich beabsichtigte Projektpfade ist wegen R1-F02 nicht erfüllt.

Alle **113 eindeutigen Paketversionen** im neuen Cache bestehen zwei getrennte Prüfungen: NuGet-Contenthash gegen Lockfile mittels SDK-`NuGet.Packaging.PackageArchiveReader.GetContentHash`, vollständiger Archiv-SHA-512 gegen `.nupkg.sha512`. Contenthash und Dateihash werden bei signierten Paketen nicht fälschlich gleichgesetzt. [Hashresultate](../../artifacts/a12-05-r1/package-contenthash-verification.json), [Prüfskript](../../artifacts/a12-05-r1/Verify-PackageHashes.ps1).

Der finale Runtimegraph enthält Prism.Avalonia, Prism.DryIoc.Avalonia, Prism.Core und Prism.Events als **Project-Libraries 1.0.0**, keine alten inkompatiblen Prism-Avalonia-Pakete oder Core/Events 9.0.537. Die technische Assemblyversion der vier Eigenbuilds ist 1.0.0.0 und keine offizielle Releaseversion. Container.Abstractions/Container.DryIoc bleiben die offiziellen Pakete 9.0.114; DryIoc bleibt 5.4.3. Die Anwendung löst Avalonia 12.1.4 auf, während Prisms eigene unveränderte Kompilierungsabhängigkeit 12.1.1 ist.

Paketidentität, Library-Typ und Contenthash der gespeicherten Graphen stimmen in beiden Konfigurationen mit A12-05 überein. Die vier Prism- und zwei Container-DLLs des neuen Probeoutputs stimmen per SHA-256 mit dem tatsächlichen Desktopoutput und den A12-05-Binärhashes überein. [Graphnachweis](../../artifacts/a12-05-r1/results/runtime-and-framework-graphs.json), [Binärnachweis](../../artifacts/a12-05-r1/results/binary-verification.json), [gespeicherte Assets und deps](../../artifacts/a12-05-r1/graphs/).

## 5. R4/R5 – Funktion, Regression und unveränderte Verträge

Alle Suite-Läufe erfolgten auf dem neu gebauten Release-Stand; vollständige Builds erfolgten zusätzlich in Debug. Gezählt sind TRX-Ergebnisse, ohne Addition separat ausgeführter Teilmengen zu ihren Gesamtsuiten.

| Prüfung | Gesamt | PASS | FAIL | NotExecuted |
|---|---:|---:|---:|---:|
| GUI | 480 | 480 | 0 | 0 |
| EditorCoordinator separat | 35 | 35 | 0 | 0 |
| F1 separat | 8 | 8 | 0 | 0 |
| Core vollständig | 7.672 | 7.667 | 0 | 5 |
| C1 separat | 9 | 9 | 0 | 0 |
| MCP vollständig | 90 | 89 | 0 | 1 |

[Zusammenfassung](../../artifacts/a12-05-r1/results/summary.json), [Befehle, vollständige Logs, Exitcodes und TRX](../../artifacts/a12-05-r1/results/). Der [Vergleich von Fallnamen und Ergebnissen](../../artifacts/a12-05-r1/results/case-outcome-comparison.json) gegen A12-05 zeigt in allen sechs Läufen **keine Unterschiede**. Ausschließlich variable MCP-Datumswerte im Format `dd/MM/yyyy HH:mm:ss +/-HH:mm` wurden für diesen Vergleich normalisiert.

Die fünf bisherigen Core-NotExecuted bleiben: `NativePublicationDoesNotFollowLateSymlink`, `TargetSymlinksIncludingDanglingLinksAreRejected(False/True)` wegen Symlinkprivilegien, `MacOsInheritedExtendedAclIsRemovedBeforePublication` wegen Darwin-Bedingung und der explizite Hardwaretest `MeasureLocalHardware`. MCP überspringt unverändert `InstalledCodexDiscoversAndCallsStatus`. Kein Test wurde entfernt, abgeschwächt oder neu übersprungen; `.codex/config.toml` wurde nicht geöffnet. Die abweichende Core-Konsolensumme von 7.671 erklärt sich durch den expliziten Hardwaretest; die TRX enthält 7.672 Resultate.

Die [neue Probe](../../artifacts/a12-05-r1/probe/) referenziert das tatsächliche Desktopprojekt und startet dessen App/MainWindow und DI-Container. Headless und nativ bestehen:

- Alle fünf Regionen: ContentRegion, FooterRegion, InfoFooterRegion, InfoRegion, SidebarRegion; Navigation InfoView → VariableView → InfoView.
- Tatsächlicher Prism-IDialogService mit registrierten Probe-ViewModels und DialogWindow, dessen AXAML sowie initiale und aktualisierte Title-/Content-Bindings.
- Eingangsparameter, Ergebnisparameter, CanCloseDialog-Veto, OnDialogOpened/OnDialogClosed, Window.Close mit None, OnError-Weitergabe und Fehler für einen unregistrierten Dialog.
- ViewModel ohne Title, normale Freigabe von Content/DataContext/OwnedWindows sowie 20 funktionale Öffnen-/Schließen-Zyklen.
- Editor-Anchor, Undo/Redo und Texteingabe.

Die ausschließlich neue R1-Probekopie verschärft den nativen Handle-Test: Ein nullable Handle darf nicht mehr durch den Vergleich mit `IntPtr.Zero` irrtümlich als gültig gelten; gefordert sind ein vorhandenes Handle und ein Wert ungleich null. Der Lauf bestätigt Handle **526976** und die Windows-Nachrichtenschleife. [Headless-Log](../../artifacts/a12-05-r1/results/headless.log), [nativer Prism-Lauf](../../artifacts/a12-05-r1/results/native-windows.log). In diesen Ausführungen tritt die bekannte MissingMethodException nicht auf. Der historische Harness bleibt unverändert.

Zusätzlich wurde die tatsächliche Release-Desktop-EXE interaktiv unter Windows geöffnet: Eingabe `VAR reviewR1=0x(CD)`, Ctrl+Z, Ctrl+Y, Wechsel VariableView/InfoView, nativer Speicherdialog, Abbruch mit Escape und Beenden mit Alt+F4. Keine Datei wurde gespeichert oder überschrieben. [Smoke-Journal](../../artifacts/a12-05-r1/native-desktop-smoke.md). Der Dateipicker ist ein eigener Betriebssystem-Smoke und wird nicht als Prism-IDialogService-Nachweis ausgegeben. Die Prism-Probe verwendet synthetische Testdialoge im tatsächlichen Container; sie ist kein Volltest sämtlicher Produktdialoge oder von DPI/IME/Accessibility.

Der Code-/Deltavergleich gegen die Vor-A12-05-Sicherung findet bei den damaligen Bestandsdateien ausschließlich Änderungen an `.gitignore`, Anwendungs-csproj und Avalonia-sln. Die neuen Integrationsdateien betreffen Build/Beschaffung/Locks/Dokumentation. EditorCoordinator, EditorBridge, exklusive Leases, Admission, Dokumentidentität, Kontrollgeneration, Undo-/Redo-Delegation, UiDispatch, ExecutionContext, Core, Parser, Runner sowie MCP-Schnittstellen und Ausführungspipeline bleiben bytegleich. Auch sämtliche vorhandenen Testquellen und die bereits vorher modifizierte produktive Workflowdatei bleiben erhalten. Während R1 ändern sich keine der 816 gesicherten Dateien und keine der 1.180 offiziellen Prism-Dateien.

## 6. Verbindliche Eigentümerentscheidung zu A12-04-F01

A12-04-F01 wird entsprechend dem Auftrag **weder als Produktfehler noch als Risiko oder Freigabeblocker** behandelt. Es wurden keine Retention-/GC-Root-Untersuchungen und keine Collectability-Stresstests durchgeführt. Die oben genannten normalen Dialog- und Lebenszyklusprüfungen enthalten keine solche Freigabebedingung. Historische Ergebnisse und deren damalige Einordnung bleiben unverändert; eine rückwirkende technische Auflösung wird nicht behauptet.

## 7. R6 – Plattformen, CI und Rückkehr zu offiziellen Paketen

| Ziel | Tatsächlich nachgewiesen | Verbleibender Status |
|---|---|---|
| Windows / .NET CLI | Frische explizite Debug-/Release-Restores, beide Lösungen, vollständige Rebuilds, Suiten, native Prism-/Desktop-Smokes | Funktion bestätigt; drei Buildintegrations-Findings offen |
| Windows / Visual Studio | Installiertes MSBuild.exe mit Restore/Build erfolgreich | Interaktive Designer-/IntelliSense-Vollprüfung nicht ausgeführt |
| Zorin Linux / Rider | Relative Referenzen und PowerShell-7-Ablauf im Code geprüft | **OPEN**: neuer Kandidat dort nicht ausgeführt |
| macOS | Plattformneutraler Vorbereitungscode geprüft | **OPEN**: Restore/Build/native Funktion dort nicht ausgeführt |
| GitHub Actions | Dokumentierter möglicher Aufruf mit Git, PowerShell und .NET sowie expliziter Configuration | Nur vorbereitet; keine neue Matrix ausgeführt, keine produktiven Workflows geändert |

Die historischen A12-03-Linux-Ergebnisse gehören zum alten Prism-Paketgraphen und zählen ausdrücklich nicht als A12-05-Nachweis. Die vorliegenden Windows-Erfolge bestätigen weder Linux-/macOS-Lockportabilität noch Rider-Prozessvererbung oder native Unix-Dialoge. Auch eine spätere Actions-Matrix muss die vorgesehenen Plattformen tatsächlich ausführen und die drei neuen Befunde berücksichtigen.

Die Rückbauanleitung in [eng/prism/README.md](../../eng/prism/README.md), Abschnitt „Remove after official release“, ist mit der vom Eigentümer mitgeteilten Supportbestätigung vereinbar: unveränderter temporärer Eigenbuild nur für CryptoWorkBench bis zur offiziellen Avalonia-12-Veröffentlichung, angekündigt für November 2026; danach verbindlicher Wechsel auf das zusammengehörige offizielle NuGet-Paar. Die Supportzusage wird hier als vorgegebene Projektentscheidung geprüft, nicht als unabhängig eingeholte neue Herstellererklärung oder garantierter Veröffentlichungstermin.

Der Rückbau ist technisch klar abgrenzbar: zwei ProjectReferences ersetzen, vier temporäre Lösungseinträge entfernen, Guard/Prepare/Hooks und ihre Prozessumgebung entfernen, Locks bewusst auf die offizielle Paketverwaltung umstellen, Graph/Hashes/Audit und vollständige Regressionen erneut prüfen. `.deps` erst nach Inhaltsprüfung entfernen; historische Evidenzen erhalten. Der temporäre Buildpfad ist nicht mit fachlichem Produktcode verflochten. Eine Freigabe des späteren Paketwechsels ist damit nicht vorweggenommen.

## 8. Integrität, Bewertung und Stopp

Die Abschlussprüfung bestätigt unveränderten HEAD und Branch, weiterhin leere Staging Area, keine unerwarteten Lockdateien im echten `eng/prism/locks`-Root sowie identische Hashes aller 4.319 im R1-Historienmanifest erfassten Dateien. Vor Erstellung dieses Berichts stimmt auch der Git-Status exakt mit der R1-Ausgangsbasis überein; danach kommt ausschließlich dieser neue Bericht hinzu. Die neuen Evidenzen liegen im ignorierten `artifacts/a12-05-r1/`. Negativproben und absichtlich beschädigte Testlocks bleiben ausschließlich dort, nicht im produktiven Quell-/Lockbaum.

**CHANGES REQUIRED** beruht auf den drei konkreten korrigierbaren P2-Findings, nicht auf A12-04-F01. Die Windows-Funktionalität ist erfolgreich reproduziert; Linux/macOS bleiben wie beauftragt OPEN. Die notwendigen Windows-Prüfungen waren möglich, daher lautet die Bewertung nicht BLOCKED.

Keine Produktkorrekturen, keine Änderung vorhandener offizieller Prism-Quelldateien, keine Änderung anderer bestehender Worktrees, keine produktiven CI-Änderungen, kein Commit/Push/Merge/Reset/Rebase/Stash und keine Fortsetzung anderer Arbeitspakete. Der Review endet hier zur Eigentümerentscheidung; es erfolgt keine automatische Integrationsfreigabe.
