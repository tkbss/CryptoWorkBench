# P1 – Temporary Prism Local NuGet Integration

Stand: 2026-11-06. Entscheidung: **P1 READY FOR REVIEW**.

Die normale Restore-/Build-Kette funktioniert ohne die bisherigen Prism-IDE-Hooks.
Die vollständigen lokalen Windows-Regressionen sind in Debug und Release bestanden.
Die interaktive Visual-Studio-Abnahme einschliesslich F5 und die Ausführung der neuen
Vierplattform-CI bleiben **OPEN**. Diese Entscheidung ist keine Distributionsfreigabe.

## 1. Kandidat und Arbeitsgrenzen

| Identität | Wert |
|---|---|
| Repository | `tkbss/CryptoWorkBench` |
| Baseline und unveränderter HEAD | `c3586fe5b07116fb314fddc46148e78d679e808b` |
| Neuer Branch | `feature/prism-local-nuget` |
| Isolierter Worktree | `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\prism-local-nuget` |
| Offizieller Prism-Commit | `358118cd640d9a22ff8cf21c8ad197fa038b7990` |
| SDK / Anwendung | .NET SDK 10.0.401 / net10.0 |
| UI-Pakete der Anwendung | Avalonia 12.1.4, AvaloniaEdit 12.0.0 |
| Lokaler Host | Windows x64, OS-Version 10.0.26200, PowerShell 7.6.5 |
| Visual Studio | Enterprise 2026, 18.10.3+12224.181 |

Der vorhandene Hauptworktree liegt unter
`C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench`. Sein sauberer `master`
blieb bei `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Die Köpfe der weiteren Worktrees
`feature/gui-g1` (`7ecac04483c10722c4cb4b459e3f6cb5157c6af1`) und
`feature/script-assignment` (`9a710654a32456ddd93b10027bd34dbbdbffbe81`) wurden ebenfalls
nicht verändert. Dort vorhandene eigenständige Änderungen gehören nicht zu P1.

Alle P1-Dateiänderungen liegen im neuen Worktree; nichts wurde gestaged oder committed.
Kein Push, Merge, Paket-Upload oder GitHub-Workflow wurde ausgeführt. Das neu angelegte
Prism-Checkout wurde weder zurückgesetzt noch gepatcht. Historische B4-Berichte bleiben unverändert.
Der konkrete uncommittete Stand wird durch das lokale
[Dateiinventar](../../artifacts/prism-p1/change-inventory.json) mit SHA-256 festgehalten;
es gibt deshalb noch keinen neuen Kandidaten-Commit oder freigegebenen Git-Tree.

## 2. Architektur vorher und nachher

Vorher enthielt die Anwendung zwei direkte Prism-ProjectReferences, die Solution vier
Prism-Quellprojekte. Normale Entwicklerprozesse benötigten `Enter-Prism.ps1` beziehungsweise
den Wrapper `Build.ps1` und prozessweite MSBuild-Hooks für Pin-, Lock- und Buildprüfungen.

Jetzt erzeugt ein separater Bootstrap vier lokale NuGet-Pakete. Die Anwendung referenziert
`CryptoWorkBench.Temporary.Prism.Avalonia` und
`CryptoWorkBench.Temporary.Prism.DryIoc.Avalonia`; Events und Core werden transitiv aufgelöst.
Die vier Quellprojekte sind aus der Anwendungssolution entfernt. Die normale Build-Kette
verwendet ausschliesslich PackageReferences, konfigurierte Lockdateien und MSBuild-interne
Hashprüfungen. Sie benötigt weder ein vorhandenes Prism-Checkout noch PowerShell-Hooks.

Der Bootstrap verwendet seine eigenen `Bootstrap.Lock.props`, `Bootstrap.Security.targets`
und `Bootstrap-NuGet.Config`. Seine beiden Prozess-Hooks werden im `finally` wiederhergestellt.
Die ursprüngliche Quellprüfung, die Prüfung tatsächlicher Compile-Eingaben und der
Security-Pin `Microsoft.Build.Tasks.Git` 10.0.303 bleiben im Bootstrap erhalten.
Die alten Enter-/Leave-/Build-Skripte und ihr Integrationstest wurden durch die getrennte
Bootstrap- und Paketprüfung ersetzt. Die Anwendung benötigt keine globale NuGet- oder
MSBuild-Konfiguration; solche Konfigurationen wurden nicht geändert.

Die einzige Änderung ausserhalb dieser Integrationsschicht ist die nachfolgend beschriebene,
angeforderte einzeilige MSB4186-Korrektur. Editor-, Coordinator-, MCP- und Anwendungslogik
wurden nicht verändert.

## 3. Pakete, Abhängigkeiten und Reproduzierbarkeit

Alle vier temporären IDs beginnen mit `CryptoWorkBench.Temporary.Prism.` und verwenden
Version **`1.0.0-prism.358118cd640d.p1`**. Sie enthalten ausschliesslich die net10.0-Release-DLL,
die unveränderte Upstream-Lizenz, Provenienz und NuGet-Metadaten.

| ID-Suffix | DLL | Deklarierte direkte Abhängigkeiten |
|---|---|---|
| Events | `Prism.Events.dll` | keine |
| Core | `Prism.dll` | lokale Events exakt; Prism.Container.Abstractions exakt 9.0.114 |
| Avalonia | `Prism.Avalonia.dll` | lokaler Core exakt; Avalonia und Avalonia.Markup.Xaml.Loader jeweils >=12.1.1,<13; System.Configuration.ConfigurationManager exakt 10.0.0 |
| DryIoc.Avalonia | `Prism.DryIoc.Avalonia.dll` | lokaler Core und lokale Avalonia exakt; Avalonia und Loader jeweils >=12.1.1,<13; Prism.Container.DryIoc exakt 9.0.114 |

Die Assemblynamen, Version 1.0.0.0 und Namespaces bleiben erhalten. Events und Core haben
PublicKeyToken `40ee6c3a2184dc59`; die beiden Avalonia-Assemblies haben keinen PublicKeyToken.
Paketidentität und Assemblyidentität sind bewusst getrennt.

Die unveränderten Prism-Projekte kompilieren gemäss ihren vorhandenen Locks gegen Avalonia
12.1.1. Die Anwendung löst weiterhin 12.1.4 auf. Der vollständige maschinenlesbare
[Abhängigkeitsgraph](P1-dependency-graph.json) enthält die vier net10.0-Quell-Buildgraphen,
sämtliche aufgelösten Anwendungspakete für Debug/Release mit Versionen, Content-Hashes und
Abhängigkeiten sowie die Assemblyreferenzen der vier DLLs. Die konkrete Paketdefinition
steht in [packages.json](../../eng/prism/packages.json).

Insbesondere zieht Prism.Container.DryIoc 9.0.114 DryIoc.dll 5.4.3 und
Prism.Container.Abstractions 9.0.114 nach; letzteres benötigt
Microsoft.Extensions.DependencyInjection.Abstractions 8.0.1. ConfigurationManager 10.0.0
zieht EventLog und ProtectedData 10.0.0 nach. Die offiziellen Containerpakete ziehen keine
offiziellen Gegenstücke der vier ersetzten Prism-Pakete nach. Der Assets-Graph wurde darauf
geprüft; doppelte Prism-Assemblies aus offiziellen und temporären Paketen entstehen nicht.

| Paket | SHA-256 der nupkg |
|---|---|
| Events | `540b14544aeb49a8bd693e532325132f29738d8b6e8f967d993c6e9a59f53fda` |
| Core | `4ae5b5b2117d5abc0b43afb9d4772781d149e765320436710479fa9fc9588da9` |
| Avalonia | `a19aa46300fd35f806ec5457907d85b6ef0e3be37a84c247e79755c293c0a5d0` |
| DryIoc.Avalonia | `44b5d4b78bae4b3bb5574e74154601a033f6253d31bccaaadd9200210a7a3963` |

[package-digests.json](../../eng/prism/package-digests.json) enthält zusätzlich alle DLL-Hashes
und vollständigen Assemblyidentitäten. Eine feste ZIP-Reihenfolge, feste Zeitstempel,
unkomprimierte Einträge, virtuelle Quellpfade und ein festes Copyright-Jahr vermeiden
bekannte variable Eingaben. Ein erneuter `Rebuild` in einen zuvor leeren Feed erzeugte auf
diesem Windows-Host dieselben vier nupkg- und DLL-Hashes. Der Checkout wurde anschliessend
erneut gegen alle 1.180 gepinnten Dateien geprüft.

Dies beweist die lokale Wiederholbarkeit, noch keine Reproduktion auf einem anderen OS oder
unabhängigen Rechner. Die CI muss dieselben Digests erreichen; bei Abweichung stoppt der
Bootstrap vor dem Ersetzen des Feeds. `-CandidateManifest` ist ausschliesslich eine explizite
Wartungsoption und ersetzt keine geprüften Digests automatisch.

14 Anwendungs-Lockdateien wurden kontrolliert geändert: je drei für Debug und Release sowie
je zwei für win-x64, linux-x64, osx-x64 und osx-arm64. Neu sind ausschliesslich die vier
temporären Pakete. Entfernt wurden die vier Quellprojekt-Einträge und nicht mehr transitive
Build-Abhängigkeiten Microsoft.NETFramework.ReferenceAssemblies 1.0.3,
Microsoft.NETFramework.ReferenceAssemblies.net461 1.0.3 und Microsoft.Sbom.Targets 4.1.5.
Bei weiterhin vorhandenen Paketen änderten sich weder Version noch Content-Hash.
Die vorhandenen Prism-Quell-Locks blieben unverändert. Nachweise:
[Änderungen](../../artifacts/prism-p1/lock-package-changes.json),
[Entfernungen](../../artifacts/prism-p1/lock-package-removals.json).

## 4. Restore, Build und Visual Studio

Einmalig vom Repository-Root in einem normalen PowerShell-7-Prozess:

```powershell
pwsh -NoProfile -File ./eng/prism/Bootstrap-Packages.ps1
```

Danach sind gewöhnliche Befehle ausreichend:

```powershell
dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --locked-mode
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Debug --no-restore
dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --locked-mode -p:Configuration=Release
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Release --no-restore
```

Für `CryptoWorkBench.sln` gilt dieselbe Abfolge. Nach dem Bootstrap die Avalonia-Solution
direkt in VS2026 öffnen, normalen automatischen NuGet-Restore aktivieren und
`CryptoWorkBenchAvalonia.Desktop` als Startprojekt wählen. Debug/Any CPU und F5 benötigen
keinen vorbereiteten Enter-Prism-Prozess. Diese Bedienanleitung ist von der folgenden
Abnahmeabgrenzung zu unterscheiden.

| Prüfung | Ergebnis / Evidenz |
|---|---|
| Normales dotnet, beide Solutions, Debug und Release, Locked Restore | PASS; vollständige Logs unter artifacts/prism-p1/Debug und Release |
| Installiertes VS2026-MSBuild, Avalonia-Solution, `/restore /t:Build /p:RestoreLockedMode=true`, Debug und Release | PASS; vs-msbuild-Debug.log und vs-msbuild-Release.log |
| Wiederholte Builds ohne Prism-Neubau | PASS; Hashes und Zeitstempel der Prism-Quelloutputs über beide VS-MSBuild-Läufe unverändert, no-prism-rebuild.json |
| Wiederholter Bootstrap | PASS; bestehender geprüfter Feed wird ohne Quell-Restore/-Build akzeptiert, bootstrap-no-rebuild.log |
| Interaktives Öffnen, IDE-Autorestore, F5 und Debugger | **OPEN** |

Die native IDE-Automation wurde versucht. Sie meldete `coordinate input geometry is unavailable`
und nicht verfügbare Dialogelemente; Fensterzustand und erfasste Oberfläche waren nicht
zuverlässig zuzuordnen. Deshalb gibt es keinen belastbaren Nachweis für Solution-Öffnung,
IDE-Autorestore oder F5. Die erfolgreichen Aufrufe des echten VS-MSBuild ersetzen diese
interaktive Abnahme nicht. Keine Aussage über einen dadurch belegten Produktfehler.

### Unabhängiger MSB4186-Befund

Das VS-MSBuild reproduzierte den Fehler bei
`[System.IO.Path]::GetFullPath(path, basePath)` in `CryptoScript/Native/FilePublication.targets`.
Der dortige .NET-Framework-MSBuild-Kontext unterstützt diese Überladung nicht.
Eine einzige Zeile verwendet jetzt `[MSBuild]::NormalizePath(projectDirectory, intermediatePath)`.
Damit sind relative und absolute Zwischenpfade weiterhin unterstützt; RID-Auswahl, clang-Aufrufe,
Architekturen, codesign und Kopier-/Publish-Semantik wurden nicht geändert.
Vorher-/Nachher-Nachweise: [vorher](../../artifacts/prism-p1/vs-msbuild-before.log),
[nachher](../../artifacts/prism-p1/vs-msbuild-after.log).
Die tatsächliche Ausführung der macOS-Native-Schritte bleibt ein CI-Gate.

## 5. Windows-Testmatrix

Die Zahlen stammen aus den Original-TRX-Dateien, nicht aus historischen B4-Berichten.

| Konfiguration | Suite | Gesamt | Passed | Failed | NotExecuted |
|---|---|---:|---:|---:|---:|
| Debug | Core | 11.559 | 11.532 | 0 | 27 |
| Debug | GUI | 792 | 792 | 0 | 0 |
| Debug | MCP | 189 | 176 | 0 | 13 |
| Release | Core | 11.559 | 11.532 | 0 | 27 |
| Release | GUI | 792 | 792 | 0 | 0 |
| Release | MCP | 189 | 176 | 0 | 13 |

Reproduktion: `eng/prism/Test-Regression.ps1 -Configuration Debug` beziehungsweise `Release`.
Originale: [Debug](../../artifacts/prism-p1/Debug), [Release](../../artifacts/prism-p1/Release).
[test-matrix.json](../../artifacts/prism-p1/test-matrix.json) hält auch jeden einzelnen
NotExecuted-Fall und dessen Grund fest.

Pro Konfiguration betreffen die 27 Core-Fälle sieben Darwin-ABI-Tests, einen Darwin-ACL-Test,
18 Tests ohne verfügbare Windows-Symlink-Berechtigung und einen expliziten Benchmark.
Die Konsolensumme lässt den expliziten Benchmark aus; massgeblich ist hier die TRX-Gesamtzahl.
Die 13 MCP-Fälle benötigen explizite installierte-Codex-/native Pairing-Testvoraussetzungen.
Sie wurden nicht als bestanden gewertet und im Rahmen dieser Paketumstellung nicht neu
interaktiv ausgeführt.

In jeder vollständigen GUI-Suite bestanden unter anderem 35 EditorCoordinator-, 23
HistoryCoordinator-, sechs B4F1Lifecycle- und ein B4F2Lifecycle-Test. Hinzu kommen die beiden
neuen PrismPackageIntegrationTests: tatsächliche DryIoc-Regionnavigation einschliesslich
Journal-Rücknavigation sowie tatsächlicher DialogService mit Parameterübergabe, Close-Veto,
erlaubtem Schliessen, einmaligem Ergebnis-Callback und aufgeräumtem Fensterinhalt.
Es handelt sich um Headless-GUI-Integrationstests, nicht um einen nativen F5-Nachweis.
Subtotale: [gui-subsets.json](../../artifacts/prism-p1/gui-subsets.json).

Die sechs Prüfungen aus `Test-LocalPackages.ps1` bestanden: Locked Restore, fehlendes Paket
trotz warmem Cache, manipuliertes Paket trotz warmem Cache, manipulierte aufgelöste Cache-DLL,
Wiederherstellung und eindeutiger Prism-Paketgraph. Die drei Gegenfälle mussten fehlschlagen
und taten dies mit der vorgesehenen Integritätsdiagnose. Die ursprünglichen Bytes wurden
jeweils im `finally` wiederhergestellt. Nachweis:
[results.json](../../artifacts/prism-p1/package-tests-final/results.json).

Die regulären GUI-Solution-Builds meldeten sechs bestehende Compiler/Avalonia-Warnungen,
keine Fehler; die Core-Solution-Builds keine Warnungen oder Fehler. Der unveränderte
Prism-Quell-Rebuild meldete 24 Compiler-/Dokumentationswarnungen und keine Fehler.
In den vollständigen Windows-Restore-/Build-Logs trat keine NU190x-Auditwarnung auf.

## 6. Publish und Plattformgrenzen

| Plattform | Locked RID-Restore auf Windows | Nativer P1-Build/Test/Publish | Neue CI-Ausführung |
|---|---|---|---|
| Windows x64 | PASS | Build/Test und Release-Publish PASS; interaktives VS/F5 OPEN | OPEN |
| Linux x64 | PASS | OPEN | OPEN |
| macOS x64 | PASS | OPEN | OPEN |
| macOS ARM64 | PASS | OPEN | OPEN |

Der lokale Windows-Publishvalidator bestätigte Apphost, verwaltete Abhängigkeiten, fünf
Native-Assets und 133 InfoDocs-Dateien:
[Publish](../../artifacts/prism-p1/publish-win-x64.log),
[Validator](../../artifacts/prism-p1/publish-validator.log).
Er startet die grafische Anwendung nicht. Die vier erfolgreichen RID-Restores prüfen
Lock-/Paketauflösung auf Windows und sind keine nativen Linux-/macOS-Tests.

Die vorhandene Workflow-Matrix bleibt bei windows-latest, ubuntu-latest, macos-26-intel und
macos-26. Sie bootstrapt den Feed, baut beide Solutions mit Locked Restore, prüft nach Debug
die Paket-Gegenfälle und führt danach die vorhandenen Release-, Core-, GUI-, Coordinator-,
MCP-, Native-Publication-, Publish- und InfoDocs-Gates aus. Die macOS-Architektur- und
Signaturprüfungen bleiben erhalten. Es gibt keine automatische Lock-Erneuerung und keine
automatische Annahme abweichender Paket-Digests.

Nächste Gates: nach separater Git-Freigabe die neue CI tatsächlich ausführen, insbesondere
plattformübergreifende Paket-Reproduktion und macOS-Native-Pfade prüfen; anschliessend die
native VS-/F5-Abnahme durchführen. Historische B4-CI-Ergebnisse sind keine Evidenz für P1.
Linux-/macOS-MCP-Aktivierung wird durch diese Änderung nicht freigeschaltet.

## 7. Sicherheits-, Lizenz- und Betriebsgrenzen

- `NuGetAudit` bleibt im Modus `all` eingeschaltet. Die lokale Paket-ID-Präfixzuordnung ist
  spezifischer als die nuget.org-Wildcard. Feed und Anwendungscache liegen unter ignoriertem
  `.packages/`; keine nupkg-Datei gehört zur vorgesehenen Git-Dateiliste.
- SHA-256-Prüfungen binden sowohl die Feedpakete als auch tatsächlich aufgelöste Prism-DLLs
  an die geprüften Werte. Konfiguration/RID, Existenz und Auswahl der vorgesehenen Lockdatei
  werden geprüft; normale Restores sind locked. `Refresh-ApplicationLocks.ps1` ist ein
  ausdrückliches Wartungswerkzeug und wird weder beim normalen Build noch in CI aufgerufen.
- Umbenannte lokale IDs erhalten nicht automatisch offizielle Prism-Advisories. Quellpin,
  Provenienz und offizielle transitive Pakete müssen weiterhin überprüft und später ersetzt
  werden. Hashprüfungen sind keine Sandbox gegen einen kompromittierten SDK-/Buildprozess,
  gleichzeitig schreibende Prozesse oder absichtlich veränderte Prüfvorschriften.
- Die Umsetzung stützt sich auf die vom Eigentümer angegebene temporäre Lizenzausnahme für
  unveränderte offizielle Quellen und die Verwendung/Verteilung der Assemblies mit
  CryptoWorkBench. Sie begründet keine zusätzliche Lizenz und keine Erlaubnis zur externen
  Veröffentlichung dieser inoffiziellen Pakete. Upstream-LICENSE bleibt unverändert.
- Pakete enthalten keine privaten Zertifikate, Credentials, Quellarchive oder Signing-Keys.
  Lokale Artefakte und Caches werden nicht committed. Paket-Gegenfälle dürfen nicht parallel
  zu einem Build oder IDE-Restore laufen, weil sie vorübergehend Testeingaben verändern.

## 8. Rückfall und Ablösung

Ein Rückfall erfolgt als gezielte Rücknahme dieses isolierten Integrationsdiffs auf die
angegebene Baseline, einschliesslich passender Locks und alter Prozessregeln. Die beiden
Integrationsarten dürfen nicht vermischt werden. Bestehende andere Worktrees, Checkouts
oder gemeinsame Caches werden dabei nicht gelöscht oder zurückgesetzt.

Sobald kompatible offizielle Prism-Pakete verfügbar und vom Eigentümer freigegeben sind:
die zwei direkten PackageReferences umstellen, lokale Feedzuordnung und temporäre
Packaging-/Digestregeln entfernen, alle betroffenen Locks bewusst regenerieren und prüfen,
danach Windows-Regressionen, Vierplattform-CI und native Abnahmen wiederholen. Die minimale
unabhängige MSB4186-Korrektur ist gesondert zu beurteilen. Historische Evidenz bleibt erhalten.

Die vollständige Bedien- und Wartungsanleitung steht in
[eng/prism/README.md](../../eng/prism/README.md). Die offenen nativen IDE- und CI-Gates sind
mit dem angeforderten Abschlusskriterium vereinbar: **P1 READY FOR REVIEW**.
