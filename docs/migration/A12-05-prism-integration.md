# A12-05 – Integrate Temporary Prism 12 into Avalonia 12 Migration

Stand: 27. Oktober 2026. **Abschlussbewertung: A12-05 COMPLETE.**

Die temporäre Prism-Integration ist im tatsächlichen Worktree `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, umgesetzt. Beide Lösungen bauen in Debug und Release; die verpflichtenden Regressionen und nativen Windows-Smokes bestehen. Der Arbeitsstand bleibt uncommittet für den gezielten Integrationsreview. Linux und macOS sind für diesen neuen Kandidaten weiterhin OPEN. Es wurden keine produktiven GitHub-Actions-Workflows geändert und keine weiteren MCP-Arbeitspakete begonnen.

## 1. Ausgangsbasis und Integrität

HEAD vor und nach der Integration: `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Vor dem ersten Integrationsedit wurden Git-Status einschließlich unversionierter Dateien, Branch, HEAD, Worktree-Liste sowie staged/unstaged Binärpatches aufgenommen. Die Staging Area war leer und bleibt leer.

Die 780 über Git ermittelten bestehenden Quelldateien einschließlich uncommitteter Änderungen und Projektdateien wurden vollständig nach [baseline/source](../../artifacts/a12-05/baseline/source/) kopiert und jeweils per SHA-256 gegen das Original geprüft. Das [Quellmanifest](../../artifacts/a12-05/baseline/source-manifest.json) enthält Pfade, Größen und Hashes. Zusätzlich wurden vor den Produktänderungen alle 8.532 vorhandenen historischen Evidenzdateien gehasht: [historisches Manifest](../../artifacts/a12-05/baseline/history-manifest.json). Generierte bin/obj-Caches sind keine Quellbasis; historische Artefakte bleiben an ihrem bisherigen Ort erhalten. Abschluss der Sicherung: 18:58:40 UTC / 19:58:40 Europe/Zurich.

Der [abschließende Integritätsvergleich](../../artifacts/a12-05/integrity-final.json) bestätigt:

- Von den 780 ursprünglichen Quelldateien sind ausschließlich `.gitignore`, `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj` und `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` absichtlich geändert.
- Alle 8.532 historischen Evidenzdateien sind SHA-256-identisch.
- Alle 1.180 offiziellen Prism-Quelldateien sind SHA-256-identisch zum neu beschafften Checkout; auch dessen Git-Status ist sauber.
- Die bereits vorher modifizierte `.github/workflows/dotnet.yml` bleibt bytegleich zur Ausgangsbasis.

Damit bleiben insbesondere EditorCoordinator, EditorBridge, UiDispatch, ExecutionContext-/Admission-/Lease-Verträge sowie CryptoScript-Ausführungspipeline und sämtliche bestehenden Testquellen unverändert. `.codex/config.toml` wurde weder gelesen noch verändert. Keine Änderungen an anderen bestehenden Worktrees; kein Commit, Push, Merge, Reset, Rebase oder Stash.

## 2. Tatsächlich geänderte Produkt- und Builddateien

Die folgende Liste bezieht sich auf den gesicherten Arbeitsstand direkt vor A12-05, nicht auf sämtliche bereits vorher bestehenden Differenzen zu HEAD.

| Datei | Änderung für A12-05 |
|---|---|
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj` | Zwei alte Prism-PackageReferences durch zwei relative ProjectReferences ersetzt; Loader-Kommentar aktualisiert. |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | Vier Prism-Projekte unter `TemporaryPrism` samt Debug-/Release-Konfiguration aufgenommen, damit IDEs den vollständigen Quellgraphen laden. Die bisherigen Any-CPU-Konfigurationen bleiben bestehen. |
| `.gitignore` | `/.deps/` ignoriert; Debug-/Release-Lockfiles unter `eng/prism/locks` ausdrücklich versionierbar gemacht. |
| `Directory.Build.targets` (neu) | Verständlicher Fehler für das Anwendungsprojekt, wenn die erforderlichen externen Hooks nicht geladen sind. |
| `NuGet.Config` (neu) | Reproduzierbare Paketquelle: ausschließlich nuget.org. |
| `eng/prism/Prepare-Prism.ps1` (neu) | Offiziellen Checkout beschaffen und Commit, Origin sowie unveränderten Zustand prüfen. |
| `eng/prism/Enter-Prism.ps1` (neu) | Prozesslokale Umgebung für CLI/IDE mit beiden MSBuild-Hooks setzen. |
| `eng/prism/Build.ps1` (neu) | Beide Lösungen im Locked Mode restaurieren und in der gewählten Konfiguration bauen. |
| `eng/prism/Lock.props`, `PrismSecurity.targets` (neu) | Konfigurationslocks, Audit, Git-Versionierungsabschaltung, exakter privater Security-Pin und Abschaltung automatischer Paketproduktion. |
| `eng/prism/locks/Debug`, `Release` (neu) | 13 Debug- und 14 Release-Lockfiles; Release enthält zusätzlich den Validierungsprobe-Lock `Bootstrap.lock.json`. |
| `eng/prism/README.md` (neu) | CLI-/IDE-Bedienung, Portabilität, spätere CI-Nutzung, Lockpflege und Entfernung nach offiziellem Release. |

Zusätzlich neu: dieser Bericht und Evidenzen unter `artifacts/a12-05/`. Das [Reviewpaket](../../artifacts/a12-05/review/) enthält die finalen Integrationsdateien, ein Dateimanifest sowie Diffs der drei geänderten Bestandsdateien gegen die A12-05-Sicherung. Der gesamte Git-Diff zu HEAD enthält außerdem ältere, ausdrücklich nicht bearbeitete Migration-/MCP-Änderungen.

## 3. Prism-Pin, Bereitstellung und IDE-Nutzung

Offizieller Commit: [`358118cd640d9a22ff8cf21c8ad197fa038b7990`](https://github.com/PrismLibrary/Prism/tree/358118cd640d9a22ff8cf21c8ad197fa038b7990).

`Prepare-Prism.ps1` erzeugt bei Bedarf einen neuen Checkout aus `https://github.com/PrismLibrary/Prism.git` unter `.deps/prism`, setzt `core.autocrlf=false`, fetches exakt diesen Commit und checkt ihn detached aus. Ein vorhandener abweichender oder veränderter Checkout führt zum Abbruch, ohne dessen Inhalt zu überschreiben. Kein offizielles Prism-Projekt, keine Source- oder zentrale Paketdatei wurde gepatcht. [Beschaffungslog](../../artifacts/a12-05/validation/prepare.log), [Quellhashes](../../artifacts/a12-05/prism-source-manifest.json).

Die beiden relativen ProjectReferences zeigen auf Prism.Avalonia und Prism.DryIoc.Avalonia in diesem Checkout; ihre offiziellen transitiven Referenzen liefern Core und Events aus demselben Stand. Die produktiven Projekt-/Builddateien enthalten keine absoluten Windows-Pfade und keine Abhängigkeit von A12-04-Laborverzeichnissen.

Aus dem Repositoryroot:

```powershell
pwsh ./eng/prism/Build.ps1 -Configuration Release
pwsh ./eng/prism/Build.ps1 -Configuration Debug
```

Voraussetzungen sind Git, PowerShell 7 und das .NET-10-SDK gemäß bestehender `global.json`. Tatsächlich verwendet: SDK **10.0.401**; das unveränderte `global.json` erlaubt `latestPatch`. Die Anwendung bleibt **net10.0 / Avalonia 12.1.4**. Prisms eigene TargetFrameworks bleiben unverändert; durch die Aufnahme in die Lösung werden auch dessen weitere deklarierte TFMs gebaut.

**IDE-Voraussetzung:** Visual Studio bzw. Rider als neuen Prozess aus einer mit `. ./eng/prism/Enter-Prism.ps1` vorbereiteten PowerShell starten. Ein bereits laufender IDE-Prozess übernimmt nachträgliche Umgebungsvariablen nicht. Die [Bedienungsanleitung](../../eng/prism/README.md) enthält konkrete Startbefehle. Das ist ein notwendiger Teil der temporären Integration, kein maschinenweiter MSBuild-Eingriff. Ein unvorbereiteter Build wurde mit dem erwarteten erklärenden Fehler geprüft: [Guard-Nachweis](../../artifacts/a12-05/validation/unprepared-guard.log).

Der Build mit **Visual Studios installiertem MSBuild.exe** inklusive Restore besteht: [Log](../../artifacts/a12-05/validation/visual-studio-msbuild.log), Exit 0. Ein interaktiver Designer-/IntelliSense-Volltest wurde nicht durchgeführt. Rider unter Linux ist vorbereitet, hier jedoch nicht ausgeführt.

Die aktuelle öffentliche Paketprüfung wurde vor dem Strategiewechsel durchgeführt: Beide [Prism.Avalonia](https://api.nuget.org/v3-flatcontainer/prism.avalonia/index.json)- und [Prism.DryIoc.Avalonia](https://api.nuget.org/v3-flatcontainer/prism.dryioc.avalonia/index.json)-Listen enden weiterhin bei `9.0.537.11300-pre`. Die aktuellen Nuspec-Dateien verweisen auf Avalonia 11.3.0 und das AvaloniaCommunity-Repository. Es wurde kein nachweislich kompatibles offizielles Avalonia-12-Paketpaar unter diesen IDs gefunden. [Gesicherte Rohdaten](../../artifacts/a12-05/research/). Daher wurde die beauftragte temporäre Strategie beibehalten.

## 4. Paketgraph, Build-Sicherheit und Lockfiles

| Bestandteil | Tatsächlich aufgelöst |
|---|---|
| Prism.Avalonia, Prism.DryIoc.Avalonia, Prism.Core, Prism.Events | Project-Libraries aus dem einen offiziellen Checkout |
| Technische Version dieser vier Assemblies | 1.0.0.0 bei deaktivierter Git-Versionierung; keine offizielle Releaseversion |
| Prism.Container.Abstractions / Prism.Container.DryIoc | 9.0.114, offizielle Upstream-Paketabhängigkeiten |
| DryIoc.dll | 5.4.3 |
| Avalonia und XAML-Loader im Produktgraphen | 12.1.4 |
| Avalonia in Prisms eigener Kompilierung | Unveränderte Upstream-Abhängigkeit 12.1.1 |
| Microsoft.Build.Tasks.Git | **10.0.303**, exakt gepinnt, PrivateAssets=all |
| Microsoft.SourceLink.GitHub / Common | 10.0.300 |

Die [.deps.json- und assets-Graphen beider Konfigurationen](../../artifacts/a12-05/graphs/) und die [maschinengeprüfte Auswertung](../../artifacts/a12-05/runtime-graph-verification.json) bestätigen vier Project-Identitäten statt alter inkompatibler Prism-Pakete. Core/Events 9.0.537 sind nicht im Produktlaufzeitgraphen. Die sechs Prism-/Container-Dateien im Probeoutput stimmen per SHA-256 mit dem tatsächlichen Desktopoutput überein: [Binärnachweis](../../artifacts/a12-05/runtime-binary-verification.json), [finale Nachprüfung](../../artifacts/a12-05/final-binary-recheck.json). Die geladenen Assemblypfade und Versionen stehen zusätzlich im nativen Probelog.

Der externe Targets-Hook verwendet ausschließlich für die vier benannten Prism-Projekte `PackageReference Include="Microsoft.Build.Tasks.Git" VersionOverride="[10.0.303]" PrivateAssets="all"`. Ein [diagnostischer Rebuild](../../artifacts/a12-05/validation/security-task-load.log) weist tatsächlich geladene `LocateRepository`- und `GetUntrackedFiles`-Tasks aus **10.0.303** nach. Die [evaluierte innere Projektpolicy](../../artifacts/a12-05/validation/evaluated-prism-inner-policy.json) bestätigt `DISABLE_GITVERSIONING=true`, `GeneratePackageOnBuild=false`, `NuGetAudit=true`, `NuGetAuditMode=all` und Locked Mode. Der Props-Hook greift auch bei der äußeren Multi-Target-Auswertung; die Paketreferenz wird im jeweiligen inneren Frameworkgraphen hinzugefügt.

Es wurden keine Prism-NuGet-Pakete erzeugt oder veröffentlicht. NuGetAudit blieb aktiv, keine NU1902-Unterdrückung wurde hinzugefügt. Die [Avalonia-/Prism-Auditantwort](../../artifacts/a12-05/validation/audit-avalonia.log) und der [Core-/MCP-Audit](../../artifacts/a12-05/validation/audit-core.log) enthalten keine bekannten verwundbaren Pakete. Sonstige bestehende Nullable-/SourceLink-/Buildwarnungen sind in den vollständigen Logs erhalten; COMPLETE bedeutet nicht Warnungsfreiheit.

Die übernommenen A12-04-Locks werden nach Konfiguration und Projektname adressiert. Der fehlende Debug-Core-Testlock wurde mit demselben Core-Paketgraphen ergänzt. Beide Lösungen wurden für **Debug und Release** zusätzlich mit neuem Paketcache und `--locked-mode --force --no-http-cache` restauriert, jeweils Exit 0. [Fresh-Restore-Logs](../../artifacts/a12-05/validation/), [keine Lockfileänderungen](../../artifacts/a12-05/fresh-lock-verification.json).

Für alle **113 eindeutigen Paketversionen** wurden die NuGet-Contenthashes mit `NuGet.Packaging.PackageArchiveReader.GetContentHash` direkt an den heruntergeladenen Paketen geprüft. Separat wurde der SHA-512-Hash jeder vollständigen Nupkg-Datei gegen `.nupkg.sha512` verglichen: [Ergebnis](../../artifacts/a12-05/package-contenthash-verification.json), [prüfbares Skript](../../artifacts/a12-05/Verify-PackageHashes.ps1). Bei signierten Paketen unterscheiden sich Contenthash und Dateihash absichtlich; Grundlage ist die [offizielle NuGet-Erklärung](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File). Der anfängliche ungeeignete Direktvergleich wurde korrigiert, ohne Pakete oder Locks zu ändern; [Prüfnotiz](../../artifacts/a12-05/research/package-hash-semantics.md).

## 5. Vollständige Testresultate

Gezählt sind TRX-Einzelergebnisse. Gezielte Teilmengen werden nicht zur Gesamtsuite addiert.

| Prüfung | Gesamt | PASS | FAIL | NotExecuted |
|---|---:|---:|---:|---:|
| GUI Release | 480 | 480 | 0 | 0 |
| EditorCoordinator separat | 35 | 35 | 0 | 0 |
| F1 Import-Cancellation separat | 8 | 8 | 0 | 0 |
| Core vollständig | 7.672 | 7.667 | 0 | 5 |
| C1 PSN-Diagnosen separat | 9 | 9 | 0 | 0 |
| MCP vollständig | 90 | 89 | 0 | 1 |
| Beide Lösungen, Release | Build | erfolgreich | 0 Fehler | — |
| Beide Lösungen, Debug | Build | erfolgreich | 0 Fehler | — |
| Visual-Studio-MSBuild | Restore + Build | erfolgreich | 0 Fehler | — |
| Prism headless | Funktionsprobe | erfolgreich | 0 Fehler | — |
| Prism native Windows | Funktionsprobe | erfolgreich | 0 Fehler | — |

[Zusammenfassung](../../artifacts/a12-05/validation/summary.json), [TRX, Befehle, Logs und Exitcodes](../../artifacts/a12-05/validation/), [Validierungsskript](../../artifacts/a12-05/Validate.ps1).

Der [Fallmengenvergleich gegen A12-04](../../artifacts/a12-05/validation/case-comparison.json) zeigt keine hinzugefügten oder entfernten Fälle. Wie zuvor werden lediglich erzeugte MCP-Zeitstempel im Format `dd/MM/yyyy HH:mm:ss +/-HH:mm` für diesen Vergleich normalisiert. Alle Testquellen bleiben bytegleich. Die Core-Konsole zählt 7.671 Fälle, weil der explizite Hardwaretest nicht in dieser Konsolensumme enthalten ist; TRX enthält unverändert 7.672 Ergebnisse.

Bekannte Core-Skips: `NativePublicationDoesNotFollowLateSymlink`, `TargetSymlinksIncludingDanglingLinksAreRejected(False/True)` (Symlinkprivileg), `MacOsInheritedExtendedAclIsRemovedBeforePublication` (Darwin) und `MeasureLocalHardware` (expliziter Hardwaretest). Bekannter MCP-Skip: `InstalledCodexDiscoversAndCallsStatus`; dafür wurde keine Codex-Konfiguration geöffnet.

Der erste Release-Versuch scheiterte ausschließlich am fehlenden abschließenden Separator des neu gesetzten isolierten NUGET_PACKAGES-Pfads. Der Vorbereitungsschritt normalisiert diesen Pfad jetzt. Das ursprüngliche Log bleibt erhalten; maßgeblich für den finalen Stand sind `review-build-Debug.log`, `review-build-Release.log` und die erfolgreichen Regressionen. Nach den abschließenden Buildanpassungen bleiben die getesteten Prism-Binärhashes identisch.

## 6. Prism-Funktionen und native Windows-Ergebnisse

Die neue [Probe](../../artifacts/a12-05/probe/) basiert auf den historischen A12-04-Probequellen, referenziert jedoch direkt das tatsächliche Desktopprojekt im Migrationsworktree. Sie startet dessen App/MainWindow und Container. Geprüft wurden fünf registrierte Regionen, InfoView → VariableView → InfoView, tatsächlicher Prism-IDialogService und DialogWindow-AXAML, initiale und aktualisierte Title-/Content-Bindings, Eingangsparameter, CanCloseDialog-Veto, OK samt Ergebnisparameter, Window.Close mit None, OnError-Weitergabe, ein ViewModel ohne Title, unregistrierter Dialogfehler sowie OnDialogClosed und geleerte Content-/DataContext-/OwnedWindows-Verweise. Die 20 funktionalen Dialogzyklen und Editor-Anchor-/Undo-/Redo-/Inputprüfungen bestehen headless sowie mit UsePlatformDetect, realem Windows-Handle und Dispatcher-Nachrichtenverarbeitung.

Zusätzlich wurde die tatsächliche Release-Desktop-EXE per Computer Use interaktiv geprüft: sichtbare Eingabe `VAR prismSmoke=0x(AB)`, Ctrl+Z, Ctrl+Y, Variable View und Info View, Öffnen des nativen Speicherdialogs, erfolgreicher Abbruch mit Escape und Beenden mit Alt+F4. Kein Speichern oder Überschreiben von Dateien. [Smoke-Journal](../../artifacts/a12-05/native-desktop-smoke.md). Die nativen IDialogService-Dialoge sind separat programmgesteuert geprüft; der interaktive Dateipicker wird nicht als Prism-Dialog ausgegeben. Kein vollständiger DPI-/IME-/Accessibility- oder Produktdialogkatalog-Nachweis.

## 7. Eigentümerentscheidung zu A12-04-F01

Der Projekteigentümer hat im ausdrücklichen A12-05-Implementierungsauftrag entschieden: **A12-04-F01 wird weder als Produktfehler noch als Risiko oder Freigabeblocker eingestuft. Für die weitere Migration wird so verfahren, als wäre das Retentionsproblem nicht detektiert worden.**

Diese Entscheidung gilt für die Bewertung dieser Integration. Die historischen Messergebnisse und Berichte, einschließlich ihrer damaligen Bewertungen, bleiben unverändert erhalten. Es wird keine rückwirkende technische Auflösung oder neue Ursachenfeststellung behauptet.

Es erfolgten keine weiteren GC-Root-/Retention-Untersuchungen, keine Prism-Änderung aufgrund von F01 und kein Retention-Stresstest als Freigabevoraussetzung. Nur in der **neuen** A12-05-Probekopie wurden WeakReference-Sammlung, erzwungene GC-Schleife und Collectability-Assertion entfernt. Alle übrigen Dialog-Funktions- und Lebenszyklusassertionen wurden beibehalten und bestehen. Der historische Harness bleibt unverändert.

## 8. Portabilität, CI und spätere offizielle Pakete

Die Beschaffung und Hooks sind mit Git, PowerShell 7 und .NET auf Windows, Linux und macOS vorbereitbar. Sie verwenden relative Repositorypfade und bauen unveränderte Quellen. Eine spätere freigegebene Actions-Matrix kann dieselben Buildskripte und Lockfiles verwenden. Es wurde weder ein experimenteller Workflow übernommen noch ein produktiver Workflow aktiviert oder geändert.

Tatsächlich validiert ist **Windows**. **Linux bleibt OPEN für den neu integrierten Prism-Kandidaten**; die Ergebnisse unter A12-03 betreffen den früheren Prism-Paketstand und werden hier nicht angerechnet. **macOS bleibt OPEN.** Auch die tatsächliche Rider-Ausführung unter Linux, Linux-/macOS-Lockfile-Portabilität und native Desktop-Smokes müssen dort separat nachgewiesen werden. Die vorbereiteten Skripte sind kein Ersatz für diese Plattformläufe.

Die vom Eigentümer mitgeteilte Supportbestätigung erlaubt den unveränderten temporären Selbstbuild und dessen Verwendung für CryptoWorkBench nur bis zur offiziellen Veröffentlichung mit Avalonia-12-Unterstützung, angekündigt für November 2026. Nach dieser Veröffentlichung ist der Wechsel auf das zusammengehörige offizielle NuGet-Paar verbindlich.

Die vollständige Rückbaufolge steht in [eng/prism/README.md](../../eng/prism/README.md): offizielle Paketversionen/Herkunft/Kompatibilität prüfen; zwei ProjectReferences durch PackageReferences ersetzen; vier Prism-Lösungseinträge entfernen; temporäre Beschaffung, Hooks und Umgebungsanforderung entfernen; Lockverwaltung bewusst übernehmen oder ablösen; Debug-/Release-Locks erneuern; Runtimegraph einschließlich Core/Events/Container und Audit prüfen; komplette Regressionen, Regionnavigation und native Dialog-Smokes wiederholen. Historische Evidenzen bleiben erhalten. Es erfolgt kein automatischer Strategiewechsel oder Release-Monitor.

## 9. Abschluss und Review

**A12-05 COMPLETE:** Die autorisierte Integration ist im Zielworktree durchgeführt; Builds und erforderliche Regressionen einschließlich Windows-Smokes sind erfolgreich. Die offenen Plattformnachweise sind oben ausdrücklich abgegrenzt. Die F01-Eigentümerentscheidung ist angewandt und dokumentiert.

Der gezielte Integrationsreview kann anhand des [Reviewpakets](../../artifacts/a12-05/review/), der [Bedienungsanleitung](../../eng/prism/README.md), des [finalen Git-Status](../../artifacts/a12-05/status-final.txt) und der verlinkten Evidenzen erfolgen. Die Evidenzdateien liegen weiterhin unter dem bereits grundsätzlich ignorierten `artifacts/`; sie sind vorhanden, aber nicht gestaged. Auch die neuen Produkt-/Builddateien sind bewusst uncommittet. Keine automatische Übernahme nach master, kein Push/Merge und keine Fortsetzung weiterer MCP-Arbeitspakete. Stopp zur gemeinsamen Bewertung.
