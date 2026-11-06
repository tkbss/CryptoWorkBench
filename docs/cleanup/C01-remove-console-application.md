# C01 – Remove obsolete Console Application

Datum: 2026-11-04 (Europe/Zurich). Status: **READY FOR ACCEPTANCE**.

## Arbeitsbasis und Isolation

- Repository: `tkbss/CryptoWorkBench`.
- `git fetch origin` bestätigt `origin/master` exakt auf dem erwarteten MCP-B4-Merge `f401fa7774d5c17172119e7b6838250f6fcf15be`.
- Cleanup-Branch: `cleanup/remove-console-app`, direkt von diesem Commit erstellt.
- Separater Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/C01-worktree`.
- Die ursprüngliche, saubere Arbeitskopie bleibt auf ihrem lokalen `master` bei `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Die synchronisierte Cleanup-Arbeitsbasis liegt ausschliesslich im neuen Worktree; vorhandene MCP-Worktrees bleiben unverändert.
- Keine Commit-, Push- oder Merge-Aktion; Änderungen bleiben unstaged zur Abnahme.
- Windows x64, .NET SDK `10.0.401`, Runtime `10.0.12`, PowerShell 7. Prism wird im eigenen `.deps/prism` aus dem gepinnten offiziellen Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` vorbereitet und durch die bestehenden Integritätsprüfungen verifiziert.

## Vollständiger Löschumfang

| Gelöschte Datei | Begründung |
| --- | --- |
| `CryptoWorkBenchConsole/CryptoWorkBenchConsole.csproj` | Obsoletes ausführbares Konsolenprojekt |
| `CryptoWorkBenchConsole/Program.cs` | Konsoleneinstieg und private Methoden `CreateInput`, `ExecuteProgram`, `WriteDiagnostics` |
| `CryptoWorkBenchConsole/input.txt` | Zugehörige Konsoleneingabe |
| `CryptoWorkBenchGuiTest/ConsoleSemanticDiagnosticTests.cs` | Ausschliesslich direkte Prüfungen des entfernten Konsolenprogramms |
| `eng/prism/locks/Debug/CryptoWorkBenchConsole.lock.json` | Lock des entfernten Projekts |
| `eng/prism/locks/Release/CryptoWorkBenchConsole.lock.json` | Lock des entfernten Projekts |

Damit entfällt das gesamte Konsolenprojektverzeichnis. Es wurden keine weiteren Testdateien gelöscht.

Die entfernte Testklasse lädt explizit `Assembly.Load("CryptoWorkBenchConsole")`, ermittelt `CryptoWorkBenchConsole.Program` und ruft dessen private Methoden `Main` und `ExecuteProgram` per Reflection auf. Sie enthält sechs einzelne `[Test]`-Fälle sowie zwei Methoden mit je zwei `[TestCase]`-Fällen, insgesamt **10 Fälle**:

| Testmethode | Fälle |
| --- | ---: |
| `SuccessWithoutDiagnosticsWritesNothing` | 1 |
| `WarningsUseFormatterAndDoNotBlock` | 2 |
| `FailurePreservesExceptionIdentityAndDoesNotDuplicateItsDiagnostic` | 1 |
| `ForeignExceptionDiagnosticIsAppendedByIdentityEvenWithEqualText` | 1 |
| `MainRunsSupportedV1ExampleOnStdoutWithoutDiagnostics` | 1 |
| `ExplicitPrintRemainsOnStdoutWithoutDiagnosticOutput` | 1 |
| `ScriptFailureSurvivesDiagnosticWriterFailure` | 2 |
| `SuccessfulScriptDoesNotSuppressDiagnosticWriterFailure` | 1 |

Die Suche in allen Testprojekten nach Projekt-/Assemblynamen, `Assembly.Load`, `GetMethod("Main")` und den privaten Konsolenmethoden ergab keine weitere ausschliesslich konsolenspezifische Testklasse. Die übrigen Reflection-Ladevorgänge betreffen Desktop/MCP. `CryptoScriptExecutionPresentationTests.ControlledRunner` und die gemeinsame Testinfrastruktur bleiben erhalten. Allgemeine Core-/GUI-Diagnostik, `Console.Out`-Erfassung für CryptoScript-Print/Parser und MCP-STDIO sind keine Abhängigkeiten vom entfernten Projekt und bleiben unverändert.

## Bereinigte Abhängigkeiten

| Datei | Änderung |
| --- | --- |
| `CryptoWorkBench.sln` | Konsolenprojekt mit GUID `{4997B695-77DC-4FC8-AFD1-66A6DC79CDF4}` und alle vier Debug-/Release-Konfigurationseinträge entfernt |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | Konsolenprojekt mit GUID `{BA7242A3-3540-4A9D-83FC-E44B5B214AD9}` und alle vier Konfigurationseinträge entfernt |
| `CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj` | Direkten Konsolen-`ProjectReference` entfernt |
| `eng/prism/ProjectScope.props` | Konsolenprojekt aus der exakten Projekt-Allowlist entfernt |
| `eng/prism/README.md` | Zahl der erfassten Workbench-Projekte von neun auf acht korrigiert |
| `eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json` | Nur Projektknoten `cryptoworkbenchconsole` entfernt |
| `eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json` | Nur Projektknoten `cryptoworkbenchconsole` entfernt |

Die beiden verbleibenden GUI-Locks wurden mit dem dokumentierten Updateverfahren neu ausgewertet: vorbereitete Prism-Umgebung, explizite Konfiguration, `dotnet restore CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -p:Configuration=<Debug|Release> -p:RestoreLockedMode=false --force-evaluate --configfile NuGet.Config`. Anschliessend wurde der vollständige Diff kontrolliert. Paketversionen, Content-Hashes und alle anderen Locks, einschliesslich RID-Locks, sind unverändert. Das temporäre Abschalten des Lock-Modus betrifft ausschliesslich diese gezielte Aktualisierung; alle anschliessenden regulären Restores verwenden Locked Mode.

Buildskripte, CI-Workflow, Projektdateien, Props/Targets und aktive Dokumentation wurden geprüft. Weitere obsolete Verweise waren dort nicht vorhanden. Der bestehende CI-Testlogger `console;verbosity=detailed`, Core-Standardausgabe und der native MCP-Prüfharness bleiben erhalten. Produktquellen von GUI, Desktop, CryptoScript, Automation-Abstraktionen und MCP sowie alle verbleibenden Testquellen und die gemeinsame Testinfrastruktur bleiben gegenüber der Arbeitsbasis unverändert.

## Windows-Regression

Ausführung über die bestehenden Skripte, ohne Testfilter oder neue Skip-/Ignore-Einstellungen:

```powershell
pwsh -NoProfile -File .github/scripts/Invoke-PrismCi.ps1 -Stage Build -Configuration Debug
pwsh -NoProfile -File .github/scripts/Invoke-PrismCi.ps1 -Stage Build -Configuration Release
# Je Konfiguration Debug und Release, je Suite Core, Gui und Mcp:
pwsh -NoProfile -File .github/scripts/Invoke-PrismCi.ps1 -Stage Suite -Configuration <configuration> -Suite <suite>
pwsh -NoProfile -File .github/scripts/Invoke-PrismCi.ps1 -Stage Publish -Configuration Release -RuntimeIdentifier win-x64
pwsh -NoProfile -File .github/scripts/Validate-DesktopPublish.ps1 -RuntimeIdentifier win-x64 -PublishDirectory artifacts/publish/win-x64
```

Die Build-Stufe führt für **beide** Solutions jeweils `dotnet restore --locked-mode -p:Configuration=...` und danach `dotnet build --no-restore` aus. Die Teststufe verwendet `--no-build --no-restore`; jedes TRX wird vor der nächsten Konfiguration separat gesichert.

| Prüfung | Debug | Release |
| --- | --- | --- |
| Locked Restore `CryptoWorkBench.sln` | PASS | PASS |
| Build `CryptoWorkBench.sln` | PASS, 0 Fehler | PASS, 0 Fehler |
| Locked Restore `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | PASS | PASS |
| Build `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | PASS, 0 Fehler | PASS, 0 Fehler |
| Vollständige Core-Suite | 11.559 TRX-Fälle: 11.532 Passed, 27 NotExecuted, 0 Failed | 11.559 TRX-Fälle: 11.532 Passed, 27 NotExecuted, 0 Failed |
| Vollständige GUI-Suite | 790 TRX-Fälle: 790 Passed, 0 NotExecuted, 0 Failed | 790 TRX-Fälle: 790 Passed, 0 NotExecuted, 0 Failed |
| Vollständige MCP-Suite | 189 TRX-Fälle: 176 Passed, 13 NotExecuted, 0 Failed | 189 TRX-Fälle: 176 Passed, 13 NotExecuted, 0 Failed |
| Locked RID Restore / Windows-Publish `win-x64` | nicht vorgesehen | PASS |
| Bestehende Publish-Artefaktprüfung | nicht vorgesehen | PASS: Apphost, verwaltete Abhängigkeiten, 5 native Assets, 133 InfoDocs |

Die GUI-Zahl ist in beiden Konfigurationen exakt **800 − 10 = 790**. Es wurden keine fremden Tests entfernt, deaktiviert oder zum Erreichen dieses Ergebnisses übersprungen. Die Avalonia-Builds melden 84 Warnungen in Debug und 90 in Release aus unveränderten Build-Eingaben; die anschliessenden Root-Solution-Builds melden jeweils 0 Warnungen. Alle vier Builds haben 0 Fehler. Die vollständigen Warnungen stehen in den Buildprotokollen.

Die Testzahlen werden aus einzelnen `UnitTestResult`-Einträgen der TRX-Dateien ermittelt. Konsolensummen lassen explizite Fälle teilweise weg. Bereits vorhandene `NotExecuted`-Fälle werden weder als bestanden ausgegeben noch neu deaktiviert. Der bisherige B4-Nachweis dokumentiert dieselben 27 Core- und 13 MCP-Ausnahmen: [MCP-B4-R3 independent review](../mcp/MCP-B4-R3-independent-review.md).

Die folgenden Ausnahmen treten in Debug und Release identisch auf:

| Core-Fall / Fallgruppe | Anzahl | Vorhandener Grund |
| --- | ---: | --- |
| `SymlinksRejected` | 1 | Fehlendes Windows-Symlinkprivileg |
| `SourceAndAncestorSymlinksAreRejected(False/True)` | 2 | Fehlendes Windows-Symlinkprivileg |
| `SourceSymlinkIsRejected` | 1 | Fehlendes Windows-Symlinkprivileg |
| `SymlinkFileOrAncestorRejected(False/True)` | 2 | Fehlendes Windows-Symlinkprivileg |
| `SourceAndAncestorLinksAreRejected` für `DES3-CBC`, `DES3-RETAIL`, `AES-CBC-MAC`, `AES-GMAC`, jeweils False/True | 8 | Fehlendes Windows-Symlinkprivileg |
| `SymlinkRejected` | 1 | Fehlendes Windows-Symlinkprivileg |
| `NativePublicationDoesNotFollowLateSymlink` | 1 | Fehlendes Windows-Symlinkprivileg |
| `TargetSymlinksIncludingDanglingLinksAreRejected(False/True)` | 2 | Fehlendes Windows-Symlinkprivileg |
| `OpenAtExclusiveCreatePreservesExistingSymlinkAndTarget`, `OpenAtTraversesDirectoriesAndGetPathReturnsBoundDirectory`, `OpenAtReadsSourceThroughBoundDirectory`, `OpenAtRejectsDirectoryAndSourceSymlinks`, `OpenAtCreatesPrivateFileAndLinkAtPublishesWithoutOverwrite`, `NativeFailurePreservesErrnoAndAllowsCleanup`, `GetPathTracksRenamedOpenDirectory` | 7 | Darwin-native ABI, auf Windows nicht ausführbar |
| `MacOsInheritedExtendedAclIsRemovedBeforePublication` | 1 | Darwin-ACLs |
| `MeasureLocalHardware` | 1 | Bereits expliziter manueller Hardwarebenchmark |
| **Core gesamt** | **27** | **18 Symlinkprivileg-, 8 Darwin- und 1 Explicit-Fall** |

| MCP-Fall / Fallgruppe | Anzahl | Vorhandener Grund |
| --- | ---: | --- |
| `InstalledCodexRefusesUntrustedOrExpiredServer("missing"/"different"/"expired")` | 3 | Explicit: installierter Codex |
| `InstalledCodexRejectsPortTakeoverOverHttps` | 1 | Explicit: installierter Codex |
| `InstalledCodexPinnedIdentityRejectsForeignPortOwner("isolated"/"ssl"/"bundle"/"proxy")` | 4 | Explicit: installierter Codex und Desktopadapter |
| `InstalledCodexRejectsNewWorkbenchIdentityEvenWithSameCredential` | 1 | Explicit: installierter Codex und Desktopadapter |
| `InstalledCodexDoesNotReplayAnAmbiguousDispatchedRequest` | 1 | Explicit: installierter Codex und Desktopadapter |
| `InstalledCodexDiscoversAndCallsStatus` | 1 | Explicit: `CRYPTOWORKBENCH_CODEX_PATH` |
| `InstalledCodexCompletesDesktopAes` | 1 | Explicit: gekoppelter disponibler Desktopeditor, AES-Seed und lokale Ausführungsfreigabe |
| `InstalledCodexCompletesDesktopAesAndRejectsTakeover` | 1 | Explicit: gekoppelter disponibler Desktop, lokale Ausführungsfreigabe und anschliessender GUI-Disconnect |
| **MCP gesamt** | **13** | **Bereits vorhandene Explicit-Fälle** |

Die Prüfung validiert Windows-Builds, Tests und Publish-Inhalte. Ein zusätzlicher interaktiver GUI-Smoke und die expliziten Installed-Codex-/Hardware-Szenarien sind keine in diesem Cleanup neu erhobenen Nachweise.

## Nachweis ohne Konsolenabhängigkeit

- Keine Treffer für Konsolenprojekt/-testklasse oder die beiden entfernten Projekt-GUIDs in aktiven versionierten Dateien ausserhalb historischer Dokumentation.
- Projektverzeichnis, beide eigenen Locks, Testquelle, Solution-Konfigurationen und Testprojektreferenz fehlen vollständig.
- Struktureller JSON-Vergleich gegen `HEAD`: Die zwei GUI-Locks unterscheiden sich ausschliesslich durch den entfernten Konsolen-Projektknoten.
- **34** frisch erzeugte Dependency-Manifeste (`project.assets.json`, `*.nuget.dgspec.json`, `*.deps.json`, einschliesslich Publish) enthalten keine Konsolenabhängigkeit.
- **105** verwaltete `Crypto*.dll`-Dateien aus Debug-/Release-Builds, Referenzassemblies und Publish wurden über PE-Metadaten geprüft: kein AssemblyReference auf `CryptoWorkBenchConsole`, keine Konsolen-Assembly unter diesen Dateien.
- Der Windows-Publish entsteht im zuvor leeren Worktree und besteht den unveränderten bestehenden Artefaktvalidator; historische Buildreste wurden nicht übernommen.
- `git diff --check` besteht. Historische Dokumentation und alle verbleibenden Produkt-/Testquellen bleiben unverändert.

Lokale Rohbelege und Prüfskripte liegen im Cleanup-Worktree unter [artifacts/C01](../../artifacts/C01/): Build-/Test-/Publish-Logs, sechs getrennte TRX-Dateien, `test-matrix.json` mit allen Ausnahmegründen, `update-locks.ps1`, `regression.ps1` und `audit.ps1`. Diese reproduzierbaren lokalen Artefakte sind wie bisher durch `artifacts/` ignoriert; dieser Bericht ist die versionierbare Zusammenfassung.

## Bewusst erhaltene historische Referenzen

Die folgenden **14 Dateien** enthalten weiterhin den exakten Projekt- oder Testklassennamen. Sie sind historische Architektur- oder Abnahmebelege und bleiben bytegleich zur Arbeitsbasis:

| Historische Datei | Einordnung |
| --- | --- |
| `docs/architecture/diagrams/01-project-references.mmd` | Architekturdiagramm der ausdrücklich angegebenen Baseline `d3099c80...` |
| `docs/architecture/evidence/01-introduction-baseline.md` | Quellverweise derselben Architektur-Baseline, einschliesslich damaliger Konsolenprojekt-/Programmpfade |
| `docs/architecture/evidence/G02-review-fixes/baseline-core.txt` | Historisches Build-/Restore-Protokoll |
| `docs/architecture/evidence/G02-review-fixes/baseline-gui.txt` | Historisches Build-/Restore-Protokoll |
| `docs/architecture/evidence/G02-review-fixes/build-core.txt` | Historisches Buildprotokoll |
| `docs/architecture/evidence/G02-review-fixes/build-gui.txt` | Historisches Buildprotokoll |
| `docs/architecture/evidence/G02-review-fixes/gui-full.trx` | Historische Testergebnisse einschliesslich Konsolentests |
| `docs/architecture/evidence/G02-review-fixes/l3-build-core.txt` | Historisches Buildprotokoll |
| `docs/architecture/evidence/G02-review-fixes/l3-build-gui.txt` | Historisches Buildprotokoll |
| `docs/architecture/evidence/G02-review-fixes/l3-gui-full.trx` | Historische Testergebnisse |
| `docs/architecture/evidence/G02-review-fixes/l3-gui-retry.trx` | Historische Testergebnisse |
| `docs/architecture/evidence/G03/security-final/verification.txt` | Historisches Sicherheits-/Regressionsprotokoll |
| `docs/architecture/evidence/G03/verification.txt` | Historisches Regressionsprotokoll |
| `docs/migration/A12-06-commit-preparation.md` | Historische Inventarliste einschliesslich damaliger Konsolen-Locks |

Auch die folgenden generischen Beschreibungen des damaligen Konsolenfrontends bzw. damaliger kombinierter GUI-/Konsolensuiten bleiben unverändert:

- `docs/architecture/chapters/01-introduction.md`: Frontends und Projektstruktur an der festgeschriebenen Architektur-Baseline.
- `docs/architecture/AP5-report.md`: damalige GUI-/Konsolen-Testmatrizen.
- `docs/architecture/G03-report.md`: damaliger Änderungsumfang mit unverändertem Konsolenquellcode.
- `docs/F01-implementation-report.md`: damaliger Solution-Build mit Konsole.
- `docs/F01-118-findings-report.md` und `docs/F01-118-B-backport-report.md`: damalige Builds einschliesslich historischer Konsole.
- `docs/F01-ARM64-correction-report.md`: damalige GUI-/Konsolen-Integrationssuite.
- `docs/F04-implementation-report.md`: damaliger Solution-Build und GUI-/Konsolen-Teststand.

Diese Texte beschreiben ihre jeweilige historische Baseline und sind keine aktuelle Bauanleitung. Die damaligen Quellpfade sind über Git an der dokumentierten Baseline nachvollziehbar. Sie werden bewusst nicht auf heutige Dateien umgeschrieben. Allgemeine Erwähnungen von Konsolenausgabe, Testloggern, Parserdiagnostik und MCP-STDIO bleiben sachlich gültig und bezeichnen nicht das entfernte Konsolenprojekt. C01 selbst nennt den entfernten Namen zur Dokumentation des Löschumfangs.
