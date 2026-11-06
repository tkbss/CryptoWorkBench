# A12-02 – Prism Compatibility Review

Technischer Stand: 26. Oktober 2026. Lizenz-/Distributionsnachtrag: 27. Oktober 2026. Technischer Spike; keine Produktfreigabe.

**Ergebnis:** Die technische Kompatibilität des unten fixierten Prism-Quellstands mit dem tatsächlichen A12-01-Arbeitsstand ist für die ausgeführten Windows-/Headless-Prüfungen nachgewiesen. Der ursprüngliche Dialogblocker A12-F01 ist in der Wegwerfkopie behoben. **Die befristete, projektspezifische Supportausnahme erlaubt die unten beschriebene Redistribution; eine Produktintegration ist weiterhin nicht beschlossen oder durchgeführt. A12-01 bleibt im ursprünglichen Worktree technisch BLOCKED.** Es wurden dort keine Paket- oder Quelländerungen übernommen.

Der Kandidat ist eine belastbare Grundlage für die nächste Eigentümerentscheidung, kein freigegebenes NuGet-Paket. Die beschriebenen Selbstbuild-, CI- und Distributionsrechte sind für CryptoWorkBench durch die befristete Supportausnahme geklärt. Offen bleiben die Integrationsentscheidung, Paket-/CI-Verantwortung, eine bekannte Build-Abhängigkeitswarnung sowie plattformübergreifende Nachweise.

**Ausgangsbasis und Isolation**

| Gegenstand | Festgestellter Stand |
|---|---|
| A12-01-Worktree | C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12 |
| Branch / HEAD | feature/avalonia12-migration / bd48c57fa4cae9dc733da7d4521eb6f79a9017f0 |
| Aufnahme | 2026-10-26T17:59:56.4774846Z |
| Tatsächliche Quellen | 771 versionierte beziehungsweise nicht ignorierte unversionierte Dateien; einschließlich uncommitteter produktiver Änderungen |
| Bestandsaufnahme | 9183 vorhandene Dateien mit Größe und Änderungszeit; getrennt von den Quell-Hashes |
| Wegwerfkopie | C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-A12-02-Spike\app |
| Offizielle Prism-Quellen | C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-A12-02-Spike\upstream |
| Probe / Cache | Unter demselben Spike-Verzeichnis: probes, packages, cli |
| Ausgangsframework | Avalonia 12.1.4 / AvaloniaEdit 12.0.0 / net10.0 |

Die Kopie wurde Datei für Datei aus dem tatsächlichen Arbeitsstand erstellt und gegen SHA-256 geprüft, nicht aus HEAD allein. Alte Buildausgaben und A12-01-Testartefakte wurden nicht als Anwendungsquellen übernommen. Die initiale Indexänderungsliste war leer; vorhandene unstaged/untracked Änderungen wurden erhalten. Git-Status, Diffs, Worktree-Liste, Quellmanifest und Dateiinventar liegen unter artifacts/a12-02/baseline.

In der Anwendungskopie wurde ausschließlich CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj verändert: Die beiden Prism-Paketreferenzen wurden durch ProjectReferences auf die getrennten offiziellen Prism-Projekte ersetzt. Alle übrigen 770 kopierten Dateien blieben hash-identisch. Probeprogramme liegen außerhalb des Anwendungsbaums. Die offiziellen Prism-Quellen wurden nicht gepatcht; deren Git-Status ist sauber. Es gab keinen Commit, Push, Merge, Reset, Rebase oder Stash. Am ursprünglichen MCP- und master-Worktree wurden keine Schreiboperationen vorgenommen. .codex/config.toml wurde weder gelesen noch geändert.

**Offizieller Kandidat und öffentliche Pakete**

[PR #3410](https://github.com/PrismLibrary/Prism/pull/3410) wurde am 7. September 2026 um 19:30:30 UTC gemergt. Verifizierter Merge-Commit:

358118cd640d9a22ff8cf21c8ad197fa038b7990

Der PR-Basisstand ist 1eeed5a76cde3094b2b88241b4d58f21392a8a37, der PR-Head a241a737d2043e107d56aef8bc9a19dec547a47d. Der Kandidat wurde detached aus dem offiziellen Repository ausgecheckt. Der vollständige SHA bezeichnet den geprüften Stand; es wird keine Aussage über jeden späteren master-Commit getroffen. [Issue #3391](https://github.com/PrismLibrary/Prism/issues/3391) ist geschlossen; die dort beschriebene Unterstützung konzentriert sich auf Desktop. Mobile/WASM gehören nicht zu diesem Nachweis.

Die öffentlichen NuGet-v3-Versionslisten und die neuesten Nuspec-Dateien wurden am Untersuchungstag abgefragt und gesichert. Unter den vorhandenen IDs ist kein veröffentlichtes, als Avalonia-12-Kombination ausgewiesenes Prism.Avalonia/Prism.DryIoc.Avalonia-Paar vorhanden:

| Paket | Neuester öffentlicher Stand | Bewertung |
|---|---|---|
| Prism.Avalonia | 9.0.537.11300-pre; stabil 9.0.537.11130 | Neuestes Nuspec verlangt Avalonia/Loader 11.3.0; AvaloniaCommunity-Herkunft |
| Prism.DryIoc.Avalonia | 9.0.537.11300-pre; stabil 9.0.537.11130 | Verweist auf dasselbe alte Prism.Avalonia-Paar und Container.DryIoc 9.0.107 |
| Prism.Core / Prism.Events | 9.0.537 | Kein separat veröffentlichtes Pendant des fixierten Gesamtquellstands |
| Prism.Container.Abstractions / Prism.Container.DryIoc | 9.0.114 | Vom neuen offiziellen Quellstand verwendete veröffentlichte Abhängigkeiten |

Quellen: [Prism.Avalonia-Versionsliste](https://api.nuget.org/v3-flatcontainer/prism.avalonia/index.json), [Prism.DryIoc.Avalonia-Versionsliste](https://api.nuget.org/v3-flatcontainer/prism.dryioc.avalonia/index.json), [Core](https://api.nuget.org/v3-flatcontainer/prism.core/index.json), [Events](https://api.nuget.org/v3-flatcontainer/prism.events/index.json), [Container-Abstractions](https://api.nuget.org/v3-flatcontainer/prism.container.abstractions/index.json), [Container-DryIoc](https://api.nuget.org/v3-flatcontainer/prism.container.dryioc/index.json). Dies belegt den geprüften öffentlichen Feed, nicht den Inhalt etwaiger privater Angebote.

**Tatsächlich verwendeter Graph**

| Bestandteil | Isoliertes Original | Testkandidat |
|---|---|---|
| Prism.Avalonia | Paket 9.0.537.11130 | Projekt aus Merge-SHA |
| Prism.DryIoc.Avalonia | Paket 9.0.537.11130 | Projekt aus demselben Merge-SHA |
| Prism.Core, Assembly Prism.dll | Paket 9.0.537 | Projekt aus demselben Merge-SHA |
| Prism.Events | Paket 9.0.537 | Projekt aus demselben Merge-SHA |
| Prism.Container.Abstractions | 9.0.107 | 9.0.114 |
| Prism.Container.DryIoc | 9.0.107 | 9.0.114 |
| DryIoc.dll | 5.4.3 | 5.4.3 |
| Avalonia / Loader zur Laufzeit | 12.1.4 | 12.1.4 |
| Avalonia-Mindeststand der Prism-Projekte | Alte Pakete: Avalonia 11 | 12.1.1; Anwendung vereinheitlicht auf 12.1.4 |
| ConfigurationManager im neuen Upstream | — | 10.0.0 |

Die vier zusammengehörigen Prism-Projekte werden gemeinsam gebaut. Core und Events stammen nicht aus dem alten 9.0.537-Paket, während nur die vom Upstream ausdrücklich referenzierten Container-Pakete aus NuGet kommen. Die aufgelösten Assets und die tatsächlich geladenen Assemblies bestätigen diesen Graphen. Es wurden keine DLLs manuell in vorhandene Anwendungsoutputs ausgetauscht.

Die unterstützte Upstream-Eigenschaft DISABLE_GITVERSIONING=true wurde gesetzt. Dadurch tragen die vier lokalen Assemblies die technische Standardversion 1.0.0.0; das ist **keine offizielle Prism-Releaseversion**. Ihre Identität ergibt sich aus SHA, Quellmanifest, Buildparametern und Binärhashes. Core/Events behalten die upstream definierte Strong-Name-Signierung; die Avalonia-Assemblies sind un-signiert. Es wurden keine .nupkg-Dateien erzeugt und keine Pakete veröffentlicht.

Vollständige aufgelöste Bibliotheken einschließlich NuGet-SHA512, Target-Abhängigkeiten, Frameworks und Restorequellen: artifacts/a12-02/research/resolved-graphs.json. Originalgraph: baseline/original-assets.json und original.deps.json. Assembly-Namen, Versionen und tatsächliche Ladepfade stehen in den Probeprotokollen.

**API- und AXAML-Befund**

Der [offizielle Quellstand](https://github.com/PrismLibrary/Prism/tree/358118cd640d9a22ff8cf21c8ad197fa038b7990) kompiliert Prism.Avalonia gegen Avalonia 12.1.1 und verwendet net9.0/net10.0. DialogWindow.axaml setzt ausdrücklich x:CompileBindings="False" und bindet den Fenstertitel an Title. Der alte binäre Aufruf der Avalonia-11-ReflectionBindingExtension wird durch den Neubau ersetzt. Der Dialog kann auch mit einem IDialogAware ohne Title-Eigenschaft aufgebaut werden.

Die Anwendung selbst brauchte keine Anpassung ihrer Prism-Aufrufe. App, MainWindow, Container-Auflösung, IRegionManager, Navigation, RegisterDialog und IDialogService.ShowDialog funktionieren im geprüften Graphen. Der PR enthält außerdem ein Dialogbeispiel im Avalonia-Demo und die Umstellung der Diagnosepakete. DelayedRegionCreationBehavior verwendet nun auch für Avalonia die Zugriffsprüfung des konkreten Zielelements. Die PR-Diskussion dokumentiert zuvor auffällige Region-Tests; die ausgeführten unveränderten Upstream-Suiten bestehen mit den unten ausgewiesenen vorhandenen Skips.

Der relevante offizielle PR-Diff ist unter research/official-pr3410.patch archiviert. GitHub-Metadaten und Kommentare liegen in research/github-*.json; 1180 offizielle Quelldateien sind in research/upstream-source-manifest.json gehasht.

**Buildumgebung und Reproduktion**

Windows 10.0.26200, win-x64; .NET SDK 10.0.401, MSBuild 18.9.11+e34a38d2a, Hostruntime 10.0.12. Keine zusätzlichen Workloads wurden installiert. Der isolierte NuGet.Config leert geerbte Paketquellen und enthält ausschließlich https://api.nuget.org/v3/index.json. NUGET_PACKAGES und DOTNET_CLI_HOME zeigen in die Wegwerfkopie.

Das mitgelieferte artifacts/a12-02/reproduce.ps1 rekonstruiert die hash-geprüfte Kopie, reproduziert zuerst den Originalfehler, checkt den fixierten offiziellen Commit aus und führt anschließend Probe, Builds und Regressionen durch. Es verlangt einen neuen Zielordner und SDK 10.0.401. Es ist als Ablaufhilfe syntaxgeprüft; der gesamte Wrapper wurde nicht ein weiteres Mal ausgeführt. Die darin dokumentierten einzelnen Build-/Testschritte wurden tatsächlich ausgeführt. Ein anderer Zielpfad kann die unten beschriebenen Core-Testartefakte verändern.

Wesentliche tatsächlich verwendete Befehle, wobei $lab das oben genannte Spike-Verzeichnis bezeichnet:

~~~powershell
$env:DOTNET_CLI_HOME="$lab\cli"
$env:NUGET_PACKAGES="$lab\packages\"

git init "$lab\upstream"
git -C "$lab\upstream" remote add origin https://github.com/PrismLibrary/Prism.git
git -C "$lab\upstream" fetch --depth 1 origin 358118cd640d9a22ff8cf21c8ad197fa038b7990
git -C "$lab\upstream" checkout --detach FETCH_HEAD

dotnet restore "$lab\probes\original\Bootstrap.csproj" --configfile "$lab\NuGet.Config" -p:Configuration=Release
dotnet run --project "$lab\probes\original\Bootstrap.csproj" -c Release --no-restore
# Erwartet Exit 1 mit MissingMethodException; vor Austausch der Projektdatei.

# Ausschließlich in app: die beiden Prism-PackageReferences durch die
# in candidate/CryptoWorkBenchAvalonia.csproj archivierten ProjectReferences ersetzen.
dotnet restore "$lab\probes\candidate\Bootstrap.csproj" --configfile "$lab\NuGet.Config" -p:Configuration=Release -p:DISABLE_GITVERSIONING=true
dotnet run --project "$lab\probes\candidate\Bootstrap.csproj" -c Release --no-restore -p:DISABLE_GITVERSIONING=true
dotnet run --project "$lab\probes\candidate\Bootstrap.csproj" -c Release --no-build -- native

dotnet build "$lab\upstream\src\Avalonia\Prism.DryIoc.Avalonia\Prism.DryIoc.Avalonia.csproj" -c Release -f net10.0 --no-restore -t:Rebuild -p:DISABLE_GITVERSIONING=true
~~~

Für Release und Debug wurde jeweils die Anwendungslösung CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln mit explizitem NuGet.Config und Configuration wiederhergestellt und mit --no-restore sowie DISABLE_GITVERSIONING=true gebaut. GUI/Coordinator/MCP wurden danach mit --no-build --no-restore getestet; Core wurde separat wiederhergestellt und getestet. Die exakten Befehle samt Coordinator-Filter FullyQualifiedName~EditorCoordinator sind zusätzlich in run-regression.ps1 und reproduce.ps1 festgehalten. Beide offiziellen Avalonia-Testprojekte wurden separat wiederhergestellt und mit dotnet test -c Release --no-restore -p:DISABLE_GITVERSIONING=true ausgeführt.

Ein unabhängiger Rebuild im selben Arbeitsverzeichnis erzeugte für alle vier Prism-DLLs identische SHA-256-Werte; siehe research/rebuild-comparison.json. Das belegt reproduzierbare Binärausgaben in dieser festgehaltenen Umgebung, nicht bereits orts- und zeitunabhängige Release-Reproduzierbarkeit auf beliebigen CI-Rechnern.

Frühe Werkzeug-/Harnessfehler bleiben dokumentiert: Ein NUGET_PACKAGES-Pfad ohne abschließenden Backslash führte beim Upstream-SourceRoot zu einem Buildfehler. Ein globales TargetFrameworks=net10.0 verursachte beim gemischten Anwendungsgraphen einen Avalonia-MSBuild-Fehler; die erfolgreichen Anwendungsläufe verwenden die unveränderten projektweisen TFMs. Der neue Probecode wurde auf den tatsächlich vorhandenen DialogResult-Konstruktor korrigiert und pumpt den Dispatcher nicht mehr nach dessen Shutdown. Dies waren Änderungen am neuen Testharness beziehungsweise an Aufrufparametern, keine Bibliotheks- oder Anwendungspatches.

**Vorher-/Nachher-Nachweis**

Im isolierten Original lief der vorhandene A12-01-Bootstrap bis zum tatsächlichen Prism-Dialogaufruf. Die Auflösung von Prism.Dialogs.DialogWindow scheiterte mit ContainerResolutionException und innerer MissingMethodException für:

Avalonia.Data.Binding Avalonia.Markup.Xaml.MarkupExtensions.ReflectionBindingExtension.ProvideValue(System.IServiceProvider)

Nach Einsatz des offiziellen Projektgraphen besteht derselbe grundlegende Bootstrap; der erweiterte Probeprozess besteht headless und nativ. Der ursprüngliche Exception-Pfad tritt in diesen Kandidatenläufen nicht mehr auf. Belege: baseline/original-dialog.log, candidate/bootstrap-full.log, candidate/headless.log, candidate/native-windows.log sowie native-windows-2.log und native-windows-3.log; dazu jeweils Exitdateien.

| Ausgeführter Probe-Fall | Ergebnis |
|---|---|
| Tatsächliche App und MainWindow aufbauen/anzeigen | PASS headless und nativ |
| CryptoScriptEditViewModel und TextEditor über DI | PASS |
| ContentRegion, FooterRegion, InfoFooterRegion, InfoRegion, SidebarRegion | PASS, exakt fünf Namen |
| InfoView → VariableView → InfoView, erfolgreiche Navigation und aktive View | PASS |
| Tatsächliches RegisterDialog und IDialogService.ShowDialog | PASS |
| Prism.Dialogs.DialogWindow, AXAML und sichtbarer Dialog | PASS |
| Initialer Titel und Inhalt sowie INotifyPropertyChanged-Updates | PASS |
| IDialogAware ohne Title-Eigenschaft | PASS Öffnen und Schließen |
| OnDialogOpened inklusive Eingangsparameter | PASS |
| CanCloseDialog=false verhindert Schließen; anschließende Freigabe | PASS |
| OK-Ergebnis und Antwortparameter 42 | PASS |
| Schließen über Window.Close ergibt ButtonResult.None | PASS |
| DialogResult.Exception erreicht OnError mit derselben Exception | PASS |
| Unregistrierter Dialog propagiert ContainerResolutionException | PASS |
| OnDialogClosed genau einmal, Content/DataContext geleert, keine OwnedWindows | PASS in jedem der 20 Zyklen |
| Schwache Referenzen auf 20 Fenster, 20 Views und 20 ViewModels | Final headless und drei native Läufe: 0/60 erreichbar |
| Editoranker, Undo/Redo und Texteingabe | PASS |
| Nativer Windows-Handle und echter Dispatcher-/Fensterbetrieb | PASS |

Der native Smoke verwendete UsePlatformDetect und echte Windows-Fenster mit einer laufenden UI-Schleife. Öffnung, Bindingänderungen und Schließung wurden programmatisch ausgelöst. Es war kein manueller Maus-/Tastaturtest und keine visuelle Designabnahme; die Dialoginteraktion blieb jedoch nicht offen.

Bei frühen nativen Speicherprüfungen waren 6 beziehungsweise 12 von 60 Objekten noch erreichbar. Der finale neue Harness verhindert Inlining der Zyklusfunktion und lässt vor der Prüfung bis zu drei Sekunden Dispatcher-Abwicklung mit GC zu. Derselbe strikte Null-Befund besteht danach in drei nativen Läufen. Die frühen Fehlprotokolle bleiben als native-early-gc.log und native-settle-attempt.log erhalten. Eine konkrete GC-Root-Ursache wurde nicht mit einem Heapdump nachgewiesen; der Befund ist daher ein begrenzter Ressourcen-Smoke und kein Beweis allgemeiner Leak-Freiheit.

Absichtlich werfende asynchrone Close-Callbacks, Langzeitbetrieb, umfangreiche Profiler-/Handle-Messungen und weitere individuelle Produktdialoge wurden nicht erschöpfend geprüft.

**Regressionsergebnisse**

Maßgeblich sind die einzelnen TRX-Ergebnisse einschließlich expliziter NotExecuted-Einträge, nicht nur die verkürzte Konsolensumme.

| Suite / Prüfung | Gesamt | PASS | FAIL | Nicht ausgeführt |
|---|---:|---:|---:|---:|
| Anwendungslösung Release | Build | erfolgreich | 0 Buildfehler | — |
| Anwendungslösung Debug | Build | erfolgreich | 0 Buildfehler | — |
| GUI | 472 | 472 | 0 | 0 |
| Coordinator separat | 35 | 35 | 0 | 0 |
| Core | 7672 | 7665 | 2 | 5 |
| MCP | 90 | 89 | 0 | 1 |
| Offiziell: Prism.Avalonia.Tests | 448 | 407 | 0 | 41 |
| Offiziell: Prism.DryIoc.Avalonia.Tests | 43 | 43 | 0 | 0 |

Die 35 Coordinator-Testnamen sind identisch zum A12-01-Finalstand; die vollständige Liste und ein leerer Differenzsatz stehen in regression/coordinator-comparison.json. Die bestehenden Tests wurden weder entfernt noch verändert oder neu übersprungen. Die 41 Prism-Skips kommen aus den unveränderten offiziellen Tests und begrenzen deren Abdeckung.

Die bekannte Core-Baseline-Abweichung RejectsInvalidPsnWithoutNormalizing("1") bleibt bestehen. Hinzu kommt in dieser Kopie derselbe Test mit "A1": AssertSanitized prüft das gesamte exception.ToString() einschließlich Quellpfaden, und CryptoWorkBench-A12-02-Spike enthält sowohl "1" als auch "A1". A12-01 hatte 7666 PASS / 1 FAIL; der Spike hat deshalb ausdrücklich 7665 PASS / 2 FAIL. Die zusätzliche Abweichung wird nicht als bestandener Test verbucht. Test- und Produktionsquellen sind unverändert; Fehlermeldungen und Quellprüfung belegen den Pfadbezug. Eine Korrektur dieser bestehenden Testlogik war nicht Teil des Auftrags.

Die fünf Core-NotExecuted-Fälle sind unverändert: drei Symlink-/Windows-Berechtigungsfälle, MacOsInheritedExtendedAclIsRemovedBeforePublication und der explizite Hardwaretest MeasureLocalHardware. MCP überspringt weiterhin InstalledCodexDiscoversAndCallsStatus. Die historischen A12-01- und Avalonia-11-Ergebnisse wurden nicht ersetzt oder umgedeutet.

EditorCoordinator, EditorBridge, UiDispatch, die ExecutionContext-Sicherheitsgrenze und sämtliche bestehenden Tests sind in der Kopie hash-identisch zur aufgenommenen Basis. Auch die gesamte GUI-Suite mit den vorhandenen Dispatcher-Sicherheitstests besteht. Prüfbelege: candidate/source-comparison.json, regression/summary.json und die vollständigen TRX-/Logdateien.

**Lizenz- und Distributionsentscheidung**

Die folgenden vier Absätze halten die ursprüngliche Recherche und Bewertung vom 26. Oktober 2026 fest. Die damalige offene Rechtefrage wird für den ausdrücklich beschriebenen Umfang durch den anschließenden Supportnachtrag abgelöst; die allgemeinen Lizenztexte werden dadurch nicht umgeschrieben.

Die [LICENSE-Datei des geprüften Commits](https://github.com/PrismLibrary/Prism/blob/358118cd640d9a22ff8cf21c8ad197fa038b7990/LICENSE) verlangt Community- oder Commercial-Lizenz plus Zustimmung zu den vollständigen Bedingungen. Sie nennt weniger als USD 1 Mio. Jahresumsatz **oder** höchstens USD 3 Mio. externes Kapital. Das ist keine ausreichende individuelle Berechtigungsprüfung; eine MIT-Angabe alter AvaloniaCommunity-Pakete darf nicht auf den neuen Gesamtgraphen übertragen werden.

Die [offizielle Lizenzfassung 1.0.0](https://cdn.prismlibrary.com/downloads/prism_license.pdf) nennt in §4.2(j) kumulative Organisationsbedingungen: unter USD 1 Mio. Umsatz beziehungsweise Nonprofit-Budget, höchstens fünf Entwickler und Ausschluss staatlicher Organisationen. Verbund- und Auftraggeberkonstellationen sind zu berücksichtigen. §4.2(j)(9) nimmt die Source-Code-Edition von Community aus und verweist auf Commercial Plus. Diese Angaben sind mit der verkürzten Repository-LICENSE nicht deckungsgleich; die konkrete Geltung für diesen öffentlichen Quellbuild muss der Eigentümer mit Prism klären.

§4.2(e–h) begrenzt Weitergabe auf eingebettete Release-Bibliotheken unter weiteren Bedingungen, einschließlich Entwicklerberechtigung, Hinweisen und Lizenzprüfungen. Ein privater Feed schafft keine zusätzlichen Rechte. Zugeordnete Entwickler und dedizierte Buildmaschinen unterliegen ebenfalls Bedingungen. Reproduzierbare Builds ersetzen diese Rechte nicht. Ob der Eigentümer qualifiziert ist beziehungsweise passende Rechte besitzt, ist hier **unbekannt**. Selbstbuild, CI-Nutzung und Redistribution sind daher **nicht freigegeben**.

Die Untersuchung verwendete ausschließlich öffentliche Quellen und den ausdrücklich beauftragten lokalen technischen Spike. Es wurden keine kostenpflichtigen Dienste, Abonnements oder privaten Feeds verwendet und keine dauerhafte Lizenz-/Distributionsentscheidung getroffen. Vor produktiver Weiterarbeit ist die Eigentümerentscheidung erforderlich. Die gesicherte PDF und Repository-LICENSE liegen unter research.

**Supportnachtrag vom 27. Oktober 2026 – befristete Ausnahme für CryptoWorkBench**

Grundlage ist die vom Projekteigentümer mitgeteilte schriftliche Bestätigung von Prism Support (Brian Lagunas). Dieser Nachtrag dokumentiert die mitgeteilte Entscheidung; die vollständige private Support-E-Mail wurde für diese Dokumentationsänderung nicht eingesehen und wird weder zitiert noch als Datei oder Anhang im Repository veröffentlicht. Das Datum bezeichnet den Dokumentationsnachtrag, nicht ein hier unabhängig geprüftes Versanddatum der E-Mail. Die private Originalbestätigung verbleibt beim Eigentümer.

Für **CryptoWorkBench** erlaubt die Bestätigung bis zur offiziellen Prism-Veröffentlichung mit Avalonia-12-Unterstützung:

- Den offiziellen, **unveränderten** Prism-Quellcode selbst zu kompilieren.
- Die daraus erzeugten Assemblies mit CryptoWorkBench auszuliefern.
- Diese Assemblies in öffentlichen GitHub-Actions-CI-Builds zu verwenden.
- Dies **ohne Commercial-Plus-Lizenz** durchzuführen.

Die Ausnahme ist **projektspezifisch und befristet**, keine allgemeine Freigabe für andere Projekte oder beliebige veränderte Prism-Forks. Laut der mitgeteilten Supportentscheidung wird die offizielle Veröffentlichung für **November 2026 angekündigt**; Prism.Avalonia bleibt mit .NET 10 kompatibel. Der Termin ist eine Ankündigung, kein hier nachgewiesenes Release. Die Befristung endet mit der offiziellen Veröffentlichung, nicht mit einer frei gewählten dauerhaften Nutzung des Eigenbuilds.

**Verpflichtender Folgeschritt:** Nach Veröffentlichung muss CryptoWorkBench auf die offiziellen Prism-NuGet-Pakete wechseln. Anschließend gilt laut Support die Community-Lizenz. Für diesen Wechsel sind die konkreten offiziellen Paketversionen und ihr Graph festzuhalten, die temporären Eigenbuild-Referenzen gegebenenfalls abzulösen und Builds, Regressionen sowie der Prism-Dialoglebenszyklus erneut zu prüfen. Diese spätere Umstellung ist verbindlich, wird mit diesem Dokumentationsauftrag aber weder implementiert noch automatisch gestartet.

**Aktuelle Bewertung:** Die bislang offene Berechtigung für den beschriebenen temporären Selbstbuild, die Auslieferung mit CryptoWorkBench und öffentliche GitHub-Actions-CI ist anhand der mitgeteilten Ausnahme geklärt. Daraus folgt keine technische Integrations-, Sicherheits- oder Produktfreigabe. Es wurden keine Produktquellen, Paketreferenzen, CI-Konfigurationen oder Prism-Assemblies verändert. Die noch ausstehenden technischen Nachweise und die Eigentümerentscheidung zur tatsächlichen Integration bleiben erforderlich.

A12-03 bleibt ein separater Linux-Validierungsbericht. Bei dieser Aktualisierung ist im Windows-Worktree unter docs/migration kein A12-03-Bericht vorhanden. Es wird keiner angelegt und es werden keine Linux-Nachweise rekonstruiert; spätere Linux-Ergebnisse werden separat zurückübertragen. macOS bleibt OPEN.

**Entscheidungsmatrix – aktualisiert nach Supportentscheidung**

| Kriterium | A: Offizielles kompatibles NuGet-Paar | B: Selbstgebauter offizieller Quellstand | C: Migration vorerst blockiert |
|---|---|---|---|
| Verfügbarkeit / Zeitbezug | Am Spike-Tag unter geprüften öffentlichen IDs nicht vorhanden; laut Support für November 2026 angekündigt | Technisch erfolgreich gebaut und geprüft | Sofort möglich |
| Wartbarkeit | Bevorzugt: upstream gepflegter Veröffentlichungsweg | Eigene Zuständigkeit für Commitwahl, Updates, Signierung und Paketidentität nötig | Kein eigener Prism-Buildprozess; zeitliche Abhängigkeit von Upstream |
| Upstream-Nähe | Offizielles Release, erneut mit A12 zu prüfen | Ungepatchter Merge-Commit; inzwischen veraltbarer Snapshot | Bestehender blockierter Stand bleibt erhalten |
| Sicherheit | Je konkretem Release Abhängigkeiten/Advisories prüfen | Erfasste SourceLink-Buildwarnung vor CI/Release bearbeiten | Kein neuer Kandidat ausgeliefert; vorhandene Abhängigkeiten bleiben zu pflegen |
| Paketverwaltung | Versionspin, Lock/Hashes und normale Restorequelle | Aktuell nur Projektgraph; internes Packaging/Feedkonzept wäre neue Entscheidung | Keine Paketumstellung |
| Lizenz / Distribution | Nach Veröffentlichung laut Support Community-Lizenz; Wechsel auf offizielle NuGet-Pakete verpflichtend | Nur für CryptoWorkBench bis zur offiziellen Veröffentlichung: unveränderten offiziellen Quellcode bauen, Assemblies mit der Anwendung ausliefern und öffentlich in GitHub Actions verwenden, ohne Commercial Plus | Keine Integration des Kandidaten; die projektspezifische Ausnahme ist dokumentiert |
| CI-/Releasefähigkeit | Nach Verfügbarkeit und erneuter Regression gut integrierbar | SDK/Graph/Hashes erfasst, aber kein freigegebener Paket-/Releaseprozess | Avalonia-12-Release bleibt blockiert |
| Empfehlung | Verpflichtender Wechsel nach offizieller Veröffentlichung | Befristete Option im bestätigten Umfang; gesonderte Integrationsentscheidung und offene technische Nachweise bleiben erforderlich | Aktueller technischer Produktstatus, solange kein kompatibler Graph integriert und validiert ist |

**Risiken und nächste Entscheidung**

Der Restore des fixierten Upstream-Stands meldet NU1902 für Microsoft.Build.Tasks.Git 10.0.300, eingebracht über die Build-/SourceLink-Abhängigkeiten. Die [offizielle dotnet/Sourcelink-Sicherheitsmeldung](https://github.com/advisories/GHSA-23fw-v26w-5fgq) ordnet dies CVE-2026-62900 zu; 10.0.300–10.0.301 sind betroffen, 10.0.303 ist korrigiert. Das ist ein konkreter zusätzlicher CI-/Release-Prüfpunkt. Im Spike wurde diese Abhängigkeit nicht stillschweigend aktualisiert und die Warnung nicht unterdrückt. Ein späterer korrigierter Kandidat muss erneut gebaut und geprüft werden.

Weitere Grenzen: Die lokalen Standard-Assemblyversionen sind keine geeignete endgültige Paketidentität. Restore-Artefakte und Hashes sind gesichert, aber Paketierung, Signing-/Provenienzstrategie, SBOM-Releaseprüfung und CI auf einem frischen Rechner bleiben offen. Die offiziellen 41 Test-Skips und die begrenzte native Ressourcenprobe sind keine Vollabdeckung. Linux und macOS bleiben **OPEN**, ebenso Mobile/WASM. Kein automatischer Übergang in weitere MCP- oder Avalonia-Migrationsarbeiten.

Empfehlung: Den Prism-Dialogblocker als **technisch lösbar nachgewiesen** führen, A12-01 produktiv jedoch **BLOCKED** lassen. Die mitgeteilte Supportausnahme klärt die Rechte für den befristeten Eigenbuild-Weg B im beschriebenen CryptoWorkBench-Umfang. Der Eigentümer entscheidet separat über dessen tatsächliche Integration; bis zur Integration und Validierung bleibt der technische Status C. Für B sind weiterhin die Sicherheitswarnung, Paketidentität und ein CI-/Release-Nachweis zu bearbeiten. Nach offizieller Veröffentlichung ist der Wechsel auf Paketweg A verpflichtend. A12-01 erhält ausschließlich einen Verweis auf diese Entscheidung; historische Testergebnisse und Befunde bleiben unverändert.

**Abschlussintegrität und neue Dateien – historischer Spike-Abschluss vom 26. Oktober 2026**

Alle 771 ursprünglichen Quelldateien sind zum Abschluss SHA-256-identisch zur Erstaufnahme. Für alle 9183 zuvor vorhandenen Dateien ergab der Vergleich von Existenz, Größe und Änderungszeit keine Abweichung. Der zweite Vergleich ist ein Metadatenvergleich, kein nachträglich behaupteter Vollhashvergleich sämtlicher alten Binärartefakte. Die 770 unveränderten Dateien der Anwendungskopie und die einzige beabsichtigte Projektdateiänderung sind einzeln nachgewiesen.

Beabsichtigt neu im A12-Worktree sind ausschließlich dieser Bericht und Dateien unter artifacts/a12-02. Dazu gehören Baselineaufnahmen, offizielle Forschungsbelege, Probequellen, Build-/Testprotokolle, TRX-Dateien, Paketgraphen, Reproduktionsskripte und Abschlussmanifeste. Die vollständige Dateiliste mit SHA-256 steht in artifacts/a12-02/new-files-manifest.json; diese Manifestdatei ist dort als absichtliche, nicht selbst gehashte Selbstreferenz aufgeführt. Git-Status und Indexvergleich sind unter status-after.txt und git-status-comparison.json dokumentiert. Es wurden keine alten A12-01-Evidenzen überschrieben.

Die getrennte Wegwerfkopie bleibt zur Nachprüfung unter dem oben genannten Spike-Pfad erhalten; ihre Existenz bedeutet keine Übernahme in das Produkt.
