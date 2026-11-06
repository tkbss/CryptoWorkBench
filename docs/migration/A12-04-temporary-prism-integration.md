# A12-04 – Temporary Prism 12 Integration

Stand: 27. Oktober 2026. **Abschlussbewertung: CHANGES REQUIRED.**

Die temporäre Integration ist in einer isolierten Kopie gebaut und funktional geprüft. NU1902 ist ohne Änderung offizieller Prism-Dateien durch einen eng begrenzten externen Build-Pin behoben. Release/Debug, die vollständigen GUI-/Core-/MCP-Suiten sowie Coordinator, F1 und C1 bestehen mit den separat ausgewiesenen bekannten Skips. Ein wiederholt auffälliger nativer Ressourcen-Smoke verhindert jedoch ein uneingeschränktes GO: Nach geschlossenen Dialogen bleiben in manchen Läufen Objekte erreichbar. Die Ursache ist nicht bewiesen. Keine automatische Produktintegration oder Freigabe weiterer MCP-Arbeitspakete; Stopp zur Eigentümerentscheidung.

## 1. Ausgangsbasis und Integrität

Produktiver Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

Testkopie: `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\a12-04-lab`. `app/` enthält den tatsächlichen uncommitteten A12-Stand einschließlich F1 und C1; `upstream/` ist ein neuer detached Checkout aus dem offiziellen Prism-Repository. Das ist kein zusätzlicher Produktworktree.

Vor den Integrationsänderungen wurden Git-Status, unstaged Diff, HEAD, Branch und Worktree-Liste erfasst und 778 Quelldateien beim Kopieren per SHA-256 verglichen. Die Staging Area war leer. Die ergänzende Aufnahme umfasst 8.081 historische Evidenzdateien. Die Quellkopie und ihr Manifest lagen vor dem ersten Integrationsedit vor; das umfangreiche historische Hashing lief ergänzend parallel zur Vorbereitung und wurde vor der finalen Prüfung abgeschlossen. Die Zeitdatei markiert das Ende dieser Aufnahme, nicht den Beginn.

[Integritätsnachweis](../../artifacts/a12-04/integrity-final.json): alle 778 ursprünglichen Dateien im Produktworktree unverändert, alle 8.081 historischen Evidenzdateien unverändert, alle 1.180 offiziellen Prism-Dateien unverändert. In der Anwendungskopie ist von diesen 778 Dateien ausschließlich `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj` geändert. Der ursprüngliche Git-Status war vor Ablage dieses neuen Berichts/Evidenzpakets identisch. Auch die bereits zuvor modifizierte produktive `.github/workflows/dotnet.yml` wurde nicht verändert.

EditorCoordinator, EditorBridge, Admission/Leases, Dokumentidentität/Kontrollgeneration, Undo-/Redo-Delegation, UiDispatch/ExecutionContext-Grenze, Core/Parser/Runner und MCP-Verträge bleiben damit bytegleich. `.codex/config.toml` wurde weder gelesen noch verändert. Kein Commit, Push, Merge oder Eingriff in andere bestehende Worktrees.

## 2. Paketprüfung und Integrationsstrategie

Die öffentlichen NuGet-Versionslisten und neuesten Nuspec-Dateien wurden neu abgerufen; Rohdaten liegen unter [research](../../artifacts/a12-04/research/). `Prism.Avalonia` und `Prism.DryIoc.Avalonia` enden weiterhin bei `9.0.537.11300-pre` (stabil `9.0.537.11130`). Das aktuelle Nuspec referenziert Avalonia 11.3.0 und das AvaloniaCommunity-Repository. Kein veröffentlichtes kompatibles Paar wurde unter diesen Paket-IDs gefunden. Core und Events enden bei 9.0.537. Daher bleibt die temporäre Quellstrategie erforderlich. Quellen: [Avalonia-Paketindex](https://api.nuget.org/v3-flatcontainer/prism.avalonia/index.json), [DryIoc-Paketindex](https://api.nuget.org/v3-flatcontainer/prism.dryioc.avalonia/index.json).

Exakter offizieller Prism-Pin: [`358118cd640d9a22ff8cf21c8ad197fa038b7990`](https://github.com/PrismLibrary/Prism/tree/358118cd640d9a22ff8cf21c8ad197fa038b7990).

Die einfachste getestete Integration verwendet zwei ProjectReferences auf `upstream/src/Avalonia/Prism.Avalonia/Prism.Avalonia.csproj` und `Prism.DryIoc.Avalonia/Prism.DryIoc.Avalonia.csproj`. Die beiden bisherigen PackageReferences werden ersetzt. Core und Events werden dadurch ebenfalls aus demselben Checkout gebaut. Es gibt keinen DLL-Austausch, keinen eigenen Feed und keine Veröffentlichung eigener Prism-Pakete. `GeneratePackageOnBuild=false` verhindert die automatische Paketproduktion des Upstream-Builds.

[Geänderte Projektdatei](../../artifacts/a12-04/candidate/CryptoWorkBenchAvalonia.csproj), [externe Integrationsdateien und Lockfiles](../../artifacts/a12-04/integration/), [Reproduktionsskript](../../artifacts/a12-04/reproduce.ps1).

Der Host übergibt zwei explizite MSBuild-Erweiterungspunkte:

- `CustomAfterMicrosoftCommonProps=.../integration/Lock.props`: setzt externe Lockfile-Pfade nach Konfiguration und Projektname, auch für die äußere Multi-Target-Auswertung.
- `CustomAfterMicrosoftCommonTargets=.../integration/PrismSecurity.targets`: fügt nur den vier ausdrücklich benannten Prism-Projekten den exakten privaten Build-Pin hinzu.

Die Dateien gehören zur Integration, nicht zum offiziellen Checkout. Die Lockfiles enthalten konkrete Versionen und NuGet-Contenthashes. Debug und Release sind getrennt, weil die Anwendung Diagnosepakete nur für Debug referenziert. `--locked-mode --force` wurde für beide Lösungsgraphen, Core und Probe erfolgreich ausgeführt. Der erste Lockfile-Versuch über ausschließlich den Targets-Hook erfasste den äußeren Multi-Target-Pfad nicht vollständig; `Lock.props` korrigiert das. Frühere Logs bleiben erhalten, maßgeblich ist `locked-final/`.

Buildparameter: .NET SDK **10.0.401**, Anwendung **net10.0**, Avalonia-Laufzeit **12.1.4**, `DISABLE_GITVERSIONING=true`, `GeneratePackageOnBuild=false`, `NuGetAudit=true`, `NuGetAuditMode=all`. `global.json` sperrt SDK-Rollforward. NuGet.Config enthält ausschließlich nuget.org; CLI-Home und Paketcache sind isoliert. Die upstream definierten TFMs bleiben erhalten; die Anwendung konsumiert net10.0. Kein globales Überschreiben von TargetFrameworks.


**Vollständige Reproduktion zusätzlich ausgeführt:** Das mitgelieferte `reproduce.ps1` lief unverändert in `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\a12-04-reproduction` mit neuem offiziellen Checkout, frischem eigenem Paketcache und kopierten Lockfiles durch, Gesamtexit 0. Beide Locked-Restores/Builds, alle sechs Testläufe und Headless-Probe bestehen erneut mit identischen Gesamt-/PASS-/Skip-Zahlen. Die Lockfiles wurden dabei nicht geändert. [Rohbelege und Summary](../../artifacts/a12-04/reproduction/), [Wrapperlog](../../artifacts/a12-04/reproduction-wrapper.log). Der native Modus wurde in diesem Wrapper bewusst nicht erneut gestartet; die fünf separat dokumentierten nativen Ergebnisse einschließlich Fehler bleiben maßgeblich.

## 3. Tatsächlich aufgelöster Graph und Sicherheit

| Bestandteil | Aufgelöster Stand |
|---|---|
| Prism.Avalonia, Prism.DryIoc.Avalonia, Prism.Core, Prism.Events | Vier Projekte aus dem exakten Commit |
| Technische Assemblyversion dieser vier Projekte | 1.0.0.0 durch deaktivierte Git-Versionierung; keine offizielle Releaseversion |
| Prism.Container.Abstractions / Prism.Container.DryIoc | 9.0.114; veröffentlichte Upstream-Abhängigkeiten |
| DryIoc.dll | 5.4.3 |
| Avalonia / Markup.Xaml.Loader in der Anwendung | 12.1.4 |
| Avalonia / Loader in Prisms eigener Kompilierung | upstream 12.1.1 |
| System.Configuration.ConfigurationManager | 10.0.0 |
| Microsoft.SourceLink.GitHub / Common | 10.0.300 |
| Microsoft.Build.Tasks.Git | **10.0.303**, vorher 10.0.300 |
| Microsoft.Sbom.Targets | 4.1.5 |
| PolySharp / NETFramework.ReferenceAssemblies | 1.15.0 / 1.0.3 |

Vollständige Framework-/Bibliotheks-/Contenthash-/Restoregraphen: [graphs](../../artifacts/a12-04/graphs/); konfigurierte Prism-Graphen zusätzlich unter `locked-final/*assets.json`. Geladene Runtimeassemblies samt Pfad und Version stehen in den Dialoglogs. `graphs/prism-binary-hashes.json` identifiziert die geprüften Probeassemblies.

Der unveränderte Ausgangsrestore reproduziert NU1902 für Microsoft.Build.Tasks.Git 10.0.300 in allen vier Prism-Projekten: [Baseline-Log](../../artifacts/a12-04/security/baseline-restore.log). Das [offizielle Microsoft-Advisory](https://github.com/dotnet/sourcelink/security/advisories/GHSA-23fw-v26w-5fgq) nennt 10.0.303 als korrigierten Stand dieser Versionslinie.

Der externe Hook setzt `PackageReference Include="Microsoft.Build.Tasks.Git" VersionOverride="[10.0.303]" PrivateAssets="all"`. Dies ist ein einzelner exakter, dokumentierter Override der transitiven Build-Abhängigkeit. Die zentralen Prism-Paketdateien bleiben unverändert. SourceLink.GitHub/Common werden nicht pauschal aktualisiert. Der finale Graph enthält 10.0.303; die generierten Imports referenzieren dessen Targets. [Diagnostischer Rebuild](../../artifacts/a12-04/security/task-load.log) bestätigt tatsächlich geladene `LocateRepository`- und `GetUntrackedFiles`-Tasks aus `microsoft.build.tasks.git/10.0.303/.../Microsoft.Build.Tasks.Git.dll`, Exit 0.

Die vier separaten Prism-Audits und der Lösungsaudit melden keine bekannten verwundbaren Pakete im geprüften Graphen. NU1902 verschwindet durch die neue Auflösung, nicht durch Unterdrückung. NuGetAudit blieb aktiv; keine neue NoWarn-/AuditSuppress-Konfiguration. Upstreams vorhandene sonstige NoWarn-Werte wurden nicht verändert. Nullable-/SourceLink-/sonstige Buildwarnungen bleiben vollständig in den Logs; ein erfolgreicher Build bedeutet nicht Warnungsfreiheit.

Für diesen Sicherheitsfix ist somit keine neue Eigentümerentscheidung über Änderungen offizieller Prism-Quellen erforderlich. Die projektspezifische befristete Nutzungs-/Redistributionsausnahme wird entsprechend dem Auftrag und dem A12-02-Nachtrag als gegeben behandelt.

## 4. Tests und unveränderte Fallmengen

Alle folgenden Fälle wurden in A12-04 tatsächlich ausgeführt; gezielte Teilmengen werden nicht zur Gesamtsuite addiert. Gezählt werden TRX-Einzelergebnisse, nicht verkürzte Konsolensummen.

| Prüfung | Gesamt | PASS | FAIL | NotExecuted |
|---|---:|---:|---:|---:|
| GUI Release | 480 | 480 | 0 | 0 |
| Coordinator separat | 35 | 35 | 0 | 0 |
| F1 Import-Cancellation separat | 8 | 8 | 0 | 0 |
| Core vollständig | 7.672 | 7.667 | 0 | 5 |
| C1 ungültige PSN separat | 9 | 9 | 0 | 0 |
| MCP | 90 | 89 | 0 | 1 |
| Lösung Release / Debug | Build | erfolgreich | 0 Fehler | — |

[Summary](../../artifacts/a12-04/regression/summary.json), [TRX und genaue Befehle](../../artifacts/a12-04/regression/), [Fallmengenvergleich](../../artifacts/a12-04/regression/case-comparison.json).

GUI und Coordinator haben exakt dieselben Namenmengen wie F1; Core dieselben wie C1. Bei MCP unterscheiden sich sechs parametrisierte Anzeigenamen ausschließlich durch erzeugte Zeitstempel. Der Rohvergleich bleibt erhalten; die ausdrücklich auf `dd/MM/yyyy HH:mm:ss +HH:mm` begrenzte [Normalisierung](../../artifacts/a12-04/regression/mcp-timestamp-comparison.json) ergibt keine verbleibende Differenz. Die Testquellen sind unverändert. Die gezielte C1-Ausführung umfasst die neun vorhandenen PSN-Fälle; der externe absichtliche Leak-Harness aus C1 wurde hier nicht erneut ausgeführt.

Bekannte Core-Skips: `NativePublicationDoesNotFollowLateSymlink`, `TargetSymlinksIncludingDanglingLinksAreRejected(False/True)` (Symlinkprivileg), `MacOsInheritedExtendedAclIsRemovedBeforePublication` (Darwin), `MeasureLocalHardware` (expliziter Hardwaretest). MCP überspringt `InstalledCodexDiscoversAndCallsStatus`; keine Codex-Konfiguration wurde dafür geöffnet. Die historischen Core-Failures aus A12-02 sind mit dem übernommenen C1-Teststand nicht mehr vorhanden.

## 5. Tatsächlicher Prism-Dialog und Windows-Smoke

Die unveränderte A12-02-Probe wurde als externe Probe gegen den heutigen A12-04-Anwendungsgraphen neu gebaut. Sie startet die tatsächliche App und MainWindow, löst den tatsächlichen Prism-IDialogService auf und verwendet echte registrierte Prism-Dialoge. [Probequellen](../../artifacts/a12-04/probes/candidate/).

Headless und alle fünf nativen Läufe bestehen die funktionalen Assertions für: fünf Regionen; InfoView → VariableView → InfoView; Dialog-AXAML und Sichtbarkeit; initiale sowie aktualisierte Title-/Content-Bindings; Eingangsparameter; CanCloseDialog-Veto; OK mit Ergebnisparameter; Window.Close mit None; Fehlerweitergabe an OnError; ViewModel ohne Title; unregistrierten Dialogfehler; OnDialogClosed und geleerte Content-/DataContext-/OwnedWindows-Verweise. Zwanzig Dialogzyklen werden je Prozess geprüft. Der native Pfad verwendet UsePlatformDetect, echte Fensterhandles und Dispatcher-Nachrichtenverarbeitung. Dies ist kein Headless-Ersatz.

**A12-04-F01: sporadisch verbleibende starke Erreichbarkeit nach Dialogschluss.** Die zusätzliche GC-/WeakReference-Assertion ist nicht durchgehend grün:

| Nativer Lauf | Erreichbar von 60 | Gesamtexit |
|---|---:|---:|
| regression/native-windows | 0 | 0 |
| locked/native-windows | 6 | 1 |
| locked-final/native-1 | 15 | 1 |
| locked-final/native-2 | 0 | 0 |
| locked-final/native-3 | 9 | 1 |

Die Prüfung pumpt bis zu drei Sekunden Dispatcherarbeit und erzwingt GC. Retainierte Objekte treten als DialogWindow/ViewModel/View-Tripel auf; genaue Zyklusnummern stehen in den Logs. Die Assertion wurde weder abgeschwächt noch durch einen längeren Timeout grün gerechnet. Es wurde kein Heapdump-/GC-Root-Nachweis erstellt. Deshalb ist weder ein dauerhafter Prism-Leak bewiesen noch die Abweichung als bloßes Harnessproblem erklärt. Der frühere A12-02-Befund einer stabilen Nullmenge ist für diesen aktuellen Stand nicht bestätigt.

Erforderlich vor GO: reproduzierbaren nativen Ressourcenfall isolieren, Roots und Dispatcher-/Render-Retention untersuchen, Harness-Lebensdauer gegen Bibliotheksretention abgrenzen und strikte Wiederholung auf dem finalen Graphen bestehen. Falls dafür offizieller Prism-Code geändert werden müsste, vor einer solchen Änderung Eigentümerentscheidung einholen.

Zusätzlich wurde die isolierte Release-Desktop-EXE über Computer Use tatsächlich geöffnet: Testtext `VAR prismSmoke=0x(AB)` sichtbar eingegeben; Ctrl+Z leert den Editor, Ctrl+Y stellt ihn wieder her; Variable View und Info View sichtbar gewechselt; Anwendung mit Alt+F4 beendet. Screenshots sind im Chatprotokoll, das [Smoke-Journal](../../artifacts/a12-04/native-desktop-smoke.md) grenzt diese Interaktion vom programmgesteuerten Dialog-Smoke ab. Kein vollständiger manueller Produktdialogkatalog, DPI-/IME-/Accessibility-Test oder Linux-/macOS-Desktopnachweis.

## 6. CI-Konzept und Betriebssysteme

[Konkreter, nicht aktivierter CI-Entwurf](../../artifacts/a12-04/ci-proposal.yml). Keine produktive Workflowdatei wurde verändert. Der Entwurf nutzt einen frischen isolierten Aufbau über das mitgelieferte Skript, öffentliche Git-/NuGet-Downloads, den exakten Commit-Pin, SDK 10.0.401, Locked-Restore, Release/Debug und die vollständigen Suiten. Keine Veröffentlichung und kein eigener Feed. Das Skript verlangt eine neue Zieldirectory und prüft vor dem Kopieren jeden Eingangshash; ein bloßer Checkout des alten HEAD ersetzt die uncommittete Basis nicht.

Für eine spätere produktive Übernahme genügt ein geprüfter App-Checkout plus benachbarter Prism-Checkout am gleichen Pin und die zwei externen MSBuild-Dateien samt Lockfiles. Den neuen Integrationsstand erst nach Eigentümerentscheidung versionieren; bei einem späteren Quellstand das Baseline-Manifest bewusst erneuern. Action-Versionen im Entwurf entsprechen dem vorhandenen Repositorykonzept und sind vor Aktivierung anhand der dann gültigen Action-SHAs festzuschreiben.

Windows ist real gebaut/getestet. Linux und macOS können dieselben ProjectReferences, SDK-/NuGet-Auflösung und Hooks verwenden; keine Windows-DLLs werden kopiert. Upstreams Windows-spezifischer ReadMe-`type`-Aufruf ist durch dessen Betriebssystembedingung begrenzt. Die tatsächlichen Linux-/macOS-Restores, Builds, Lockfile-Portabilität und Tests wurden hier **nicht ausgeführt**. Die Matrix ist ein Vorschlag, kein Erfolgsausweis. Headless-Suiten auf Hosted Runnern ersetzen keinen echten Desktop-Smoke; unter Linux X11/Wayland und unter macOS Tastatur/Clipboard/Picker/Dialoge gesondert prüfen. Native-Smoke-Fehler dürfen in CI nicht ignoriert oder mit `continue-on-error` entwertet werden.

## 7. Rückkehr zu offiziellen NuGet-Paketen und Entscheidung

Nach Veröffentlichung des offiziellen kompatiblen Releases: Herkunft und Nuspec prüfen; beide temporären ProjectReferences durch das zusammengehörige offizielle Paketpaar ersetzen; offiziellen Graphen einschließlich Core/Events/Container auf konsistente Versionen prüfen; Prism-Checkout und Security-Hook entfernen; Lockfiles für beide Konfigurationen kontrolliert erneuern; Release/Debug, vollständige Suiten und nativen Dialoglebenszyklus wiederholen. `Lock.props` kann für allgemeines Locked-Restore erhalten bleiben. Keine alte 9.0.537-Core-/Events-Assembly als vermeintlich passendes Gegenstück stehen lassen.

Offene Findings: **F01 native Ressourcenprüfung** (GO-Hindernis); Linux/macOS/öffentliche Actions noch nicht tatsächlich validiert; bekannte sechs nicht ausgeführte Core-/MCP-Fälle; temporäre Assemblyversion 1.0.0.0 verlangt weiterhin Commit-/Hashprovenienz. Die Sicherheitswarnung NU1902 und die bisherige Dialog-ABI-Inkompatibilität sind für den geprüften Kandidaten behoben.

**CHANGES REQUIRED.** Die Integrationsstrategie und der Sicherheitsfix sind konkret vorbereitet. Vor Übernahme ist F01 belastbar zu klären; plattformübergreifende Nachweise bleiben gesondert erforderlich. Keine automatische Integration, Veröffentlichung oder weitere MCP-Freigabe. Stopp zur Eigentümerentscheidung.
