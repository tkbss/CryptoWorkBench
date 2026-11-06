# C01-F3 – ARM64 GUI lease release investigation

Datum: 2026-11-05 (Europe/Zurich). Status: **READY FOR CI RETEST**.

## Ergebnis und Evidenzgrenze

**A – Zu frühe Testassertion ist nachgewiesen.** Im Release-only-Fall garantiert `WorkbenchAutomationService.DrainAsync` weder den Abschluss der Lease-Cancellation noch die Pensionierung der Lease oder die Rückkehr des Coordinators zur Phase `Gui`. Der alte Test verwendete diesen Task dennoch als unmittelbares GUI-Freigabezertifikat.

Ein deterministischer Gegenbeweis reproduziert genau diesen unzulässigen Schluss: Die ursprüngliche Editorarbeit und der Service-Drain sind erfolgreich abgeschlossen; die ursprüngliche Lease existiert noch, die Phase ist `Releasing` und `CanStartGui == false`. Das gelingt mit einem kontrolliert zurückgehaltenen Cancellation-Callback sowohl in einer instrumentierten Produktkopie als auch mit der unveränderten Produkt-DLL.

**B – Dauerhaft fehlende Lifecycle-Fortsetzung wurde im untersuchten Pfad widerlegt:** Nach Freigabe des Callbacks führt der bereits vorhandene Lease-Timer ohne Eingriff des Prüfprogramms `TickLease → CheckLease → ResumeGui → RetireLease` aus. Die GUI funktioniert danach tatsächlich wieder. **C – Native Tickets, Editor-Locks oder ein anderer verbleibender GUI-Blocker erklären den reproduzierten Zustand nicht:** Tickets und Coordinator-Callbacks sind null, es gibt keine aktive Mutation oder native Document-Update-Gruppe, beide Editor-Locks sind frei und die ursprüngliche Registration ist weiterhin angehängt und ausgewählt.

Die historische ARM64-TRX enthält keine Zustandsinstrumentierung. Daher lässt sich die exakte Threadpool-/Dispatcher-Reihenfolge dieses einzelnen CI-Laufs nicht rückwirkend beweisen. Nachgewiesen sind die falsche Synchronisationsannahme, ein passender deterministischer Ablauf und die vorhandene automatische Erholung. Ein umfassender Ausschluss beliebiger anderer Lifecycle-Defekte wird nicht behauptet.

## Originale CI-Evidenz

- [Run 41911689126](https://github.com/tkbss/CryptoWorkBench/actions/runs/41911689126), [ARM64-Job 126860631900](https://github.com/tkbss/CryptoWorkBench/actions/runs/41911689126/job/126860631900).
- Originale Joblogs erneut gelesen; originale `Gui.trx` aus Artefakt `13362329016`, `test-results-osx-arm64` ausgewertet.
- ZIP-SHA-256: `13e6f9cf80ed1a0c502aed5528ca75a1ed4acf145dc95c9de9c3ee9996d556a6`, identisch mit dem GitHub-Digest.
- `AcceptedDialogKeepsGuiBlockedUntilOriginalReturns(False)`: Failed, 20,528 ms; `LeaseV2Tests.cs:230`, `CanStartGui` erwartet True, tatsächlich False.
- Die Variante `True` besteht im selben Original-Lauf, 12,308 ms. Gesamte GUI: 788 Passed / 1 NotExecuted / 1 Failed.
- Basis ist unverändert `5dda5b28ff864c5aa776e0f3a6620d424f8a5eb2`, Branch `cleanup/remove-console-app`. PR #4 bleibt ungemergt.

## Unterschied der Abschlüsse

| Vorgang | Tatsächliche Verpflichtung |
| --- | --- |
| `ReleaseEditorAsync` | Markiert die Lease als Released, startet `Lifetime.CancelAsync()` und den Coordinator-Drain; kann ausdrücklich `ReleasePhase.Releasing` zurückgeben. Die MCP-Aktivierung bleibt bestehen. |
| Ursprüngliche Editorarbeit | Wartet weiterhin auf den ursprünglichen Dialog. Ihr `finally` entsorgt das Ticket, entfernt `editCompletion`, signalisiert dessen Task und ruft `CheckLease` auf. Die asynchrone Cancellation kann zu diesem Zeitpunkt noch laufen. |
| Coordinator-/Lease-Drain | Bestätigt die registrierten Editorverpflichtungen. Er wartet nicht auf `ActiveLease.CancelledCallbacks` und schaltet die Phase nicht selbst auf `Gui`. |
| `DrainAsync`, Release-only | Snapshot aus `OutstandingServiceWork`, optionalem `epochClose` und optionalem Shutdown-Drain. Der Release-only-Pfad legt keinen `epochClose` an; Lease-Cancellation und GUI-Rückkehr fehlen in diesem Task. |
| `CheckLease` | Verlangt unter anderem abgeschlossene Cancellation-Callbacks, erfolgreichen Coordinator-Drain und keine offene Service-Arbeit. Erst dann `ResumeGui` und `RetireLease`. Läuft am Ende der Editorarbeit, bei bestimmten Lease-APIs und durch den bestehenden Ein-Sekunden-Timer. |
| `DisableMcp` | Widerruft die Aktivierung und startet `FinishEpochCloseAsync`. Dieser Pfad wartet zusätzlich auf Cancellation und Coordinator-Quieszenz, pensioniert die Lease, ruft `ResumeGui` auf und schliesst erst danach `epochClose` ab. `DrainAsync` umfasst hier diesen zusätzlichen Task. |
| `CanStartGui` | Verlangt Phase `Gui`, keinen Shutdown-/Epoch-Close-Blocker, keine aktive Coordinator-Mutation, `ObserverDepth == 0`, die richtige angehängte Registration mit Dokument, keine native Update-Gruppe sowie freie Mutation-/Identity-Locks. |

Quellen: `WorkbenchAutomationRegistry.cs:729` und `:757`, `WorkbenchLeaseV2.cs:51`, `:147`, `:152`, `:175`, `:258`, `:308`, `WorkbenchBindingV2.cs:166`, `EditorCoordinator.cs:189`, `:350`, `:385`, `:423` (alle unter `CryptoWorkBenchAvalonia/Services/Automation`).

`ResumeGui` meldet den Workspace-Wechsel innerhalb eines Observers. In diesem Observer ist `CanStartGui` absichtlich noch false; bei Disable kann zusätzlich `epochClose` noch offen sein. Deshalb wäre ein Eventhandler, der ausschliesslich auf `CanStartGui == true` wartet, selbst fehlerhaft. Die Phase `Gui` ist das beobachtbare Übergangssignal; die tatsächliche Admission wird anschliessend ausserhalb des Observers und nach dem Service-Drain geprüft.

## Isolierter Gegenbeweis

Alle Prüfprogramme, instrumentierten Kopien und Rohbelege liegen ausschliesslich im ignorierten Verzeichnis [artifacts/C01-F3](../../artifacts/C01-F3/). Kein Produktquelltext wurde instrumentiert oder verändert.

`setup-probe.ps1` kopiert die getrackten Avalonia-Quellen. Nur in dieser Kopie werden Eintritt/Austritt von `CheckLease`, `TickLease`, `ResumeGui` und `RetireLease` protokolliert. Der separate Prozess verwendet den Assemblynamen des bestehenden Test-Friend-Assemblies und eigene Headless-Session, Service, Editor und Window. Private Zustände werden dort nur lesend per Reflection erfasst. Die Beobachtung erzeugt keine neuen Produktions-APIs.

Vier vorab definierte Szenarien: `revoke=false/true`, jeweils mit/ohne gezielt zurückgehaltenen Cancellation-Callback. Der Callback wird am ursprünglichen Lease-Token registriert; ein Task-Gate erzwingt die relevante Reihenfolge, kein Sleep. Der ursprüngliche Bestätigungsdialog bleibt separat offen. Danach wird sein tatsächlicher Task abgeschlossen, der Callback freigegeben und ausschliesslich auf die vorhandene Workspace-Benachrichtigung gewartet. Es gibt keine direkten Aufrufe von `CheckLease`/`ResumeGui` und kein Polling einer Lease-/Status-API, das die Pensionierung selbst auslösen könnte. Zusätzlich werden 50 neutrale Dispatcher-Aufträge eingereiht.

Erfasst werden monotone Zeit, Thread, Phase, offene Tickets und Coordinator-Callbacks, Identität der ursprünglichen Lease nur als Boolean, Release-Grund, Cancellation-/Lease-Drain-/Epoch-/Service-Taskstatus, CheckLease-/ResumeGui-Zeitpunkte und sämtliche oben genannten Admission-Bedingungen. Keine Credentials, Tokens, GUIDs, Scripttexte, Secrets oder Runtime-Nutzlasten werden ausgegeben. Die Eingabefunktion wird mit festen synthetischen Zeichen geprüft, ohne diese zu protokollieren.

Auszug aus `probe-trace.jsonl`, Release-only mit gehaltenem Callback; Millisekunden relativ zum Probe-Start:

| Zeitpunkt | Ereignis | Phase | Cancellation | Lease-Drain | Service-Drain | CanStartGui |
| --- | --- | --- | --- | --- | --- | --- |
| 37,71 | Ursprünglicher Edit zurückgekehrt | Releasing | Running | abgeschlossen | abgeschlossen | false |
| 38,04 | Cancellation abgeschlossen | Releasing | abgeschlossen | abgeschlossen | abgeschlossen | false |
| 1033,46 | Vorhandener `TickLease` | Releasing | abgeschlossen | abgeschlossen | abgeschlossen | false |
| 1033,56 | `CheckLease` Eintritt | Releasing | abgeschlossen | abgeschlossen | abgeschlossen | false |
| 1033,79 | `ResumeGui` Austritt | Gui | abgeschlossen | abgeschlossen | abgeschlossen | true |
| 1034,02 | `RetireLease` Austritt | Gui | abgeschlossen | abgeschlossen | abgeschlossen | true |
| 1034,15 | Beobachteter GUI-Abschluss | Gui | abgeschlossen | abgeschlossen | abgeschlossen | true |

Bereits bei 37,71 ms: 0 Tickets, 0 Coordinator-Callbacks, keine aktive Mutation, keine Observer, keine native Update-Gruppe, freie Locks. Bei 38,04 ms sind auch alle Cancellation-Voraussetzungen erfüllt; der nächste vorhandene Timer-Turn stellt die GUI wieder her. Eine feste Latenz von einer Sekunde wird damit nicht zugesichert; der Timer ist der nachgewiesene vorhandene Wakeup.

Disable-Gegenprobe: Bei 1069,08 ms ist der ursprüngliche Edit zurück, aber sowohl Service-Drain als auch `epochClose` bleiben offen. Nach Callback-Rückkehr folgen Lease-Pensionierung und `ResumeGui`; erst bei 1069,32 ms sind Drain und GUI-Admission gemeinsam erfolgreich.

Die zweite Ausführung verwendet dieselben vier Szenarien mit der **unveränderten Produkt-DLL** (`ProbeInstrumented=false`). Dort ist der Release-only-Service-Drain bei 42,24 ms abgeschlossen und die GUI noch gesperrt; bei 1003,67 ms ist sie ohne manuelles Nachhelfen wieder frei. Alle acht Szenarien (4 instrumentiert, 4 uninstrumentiert) bestehen einschliesslich wirklicher GUI-Eingabe nach Rückkehr. Die alte Sofortassertion wäre in beiden gehaltenen Release-only-Szenarien deterministisch falsch.

Reproduktion nach vorhandenem Release-Build, vom Cleanup-Worktree aus:

```powershell
pwsh -NoProfile -File artifacts/C01-F3/reproduce.ps1
pwsh -NoProfile -File artifacts/C01-F3/reproduce.ps1 -Uninstrumented
```

Beim Aufbau der isolierten Programme mussten fehlende transitive Compilerreferenzen und die native HarfBuzz-DLL-Kopie ergänzt werden; diese Setupfehler liegen als Logs bei. Vor dem ersten Szenariolauf war dadurch noch kein Lifecycle-Ergebnis entstanden. Der erste reguläre Locked-Restore-Versuch wurde durch die Sandbox abgebrochen; nach Ausführung mit den erforderlichen lokalen Rechten liefen die eigentlichen Tests. Keine fehlgeschlagenen Produkt-/Regressionstests wurden wiederholt, um grün zu werden.

## Minimaler Kandidat

Einziger getrackter Quelldiff: `CryptoWorkBenchGuiTest/LeaseV2Tests.cs`, ausschliesslich `AcceptedDialogKeepsGuiBlockedUntilOriginalReturns`; **28 Einfügungen, 2 Löschungen**. Zusätzlich dieser Bericht.

- Vor Dialogrückkehr bleiben Edit und Service-Drain offen. `CanStartGui == false` und wirkungsloser echter Textinput werden ausdrücklich geprüft.
- Der Test registriert vor Freigabe des Dialogs einen lokalen `WorkspaceChanged`-Handler. Dieser signalisiert die Phase `Gui` über eine TCS mit asynchronen Fortsetzungen.
- `Task.WhenAll(edit, drain, guiPhase.Task)` muss innerhalb von fünf Sekunden abschliessen. Das wartet auf autoritative Produktionsereignisse und lässt Dispatcher/Lease-Timer weiterlaufen. Kein `Task.Delay`, keine Retry-Schleife und kein zustandsveränderndes Polling.
- Ein Timeout scheitert mit Phase, Pending, Active, ObserverDepth, Admission, beiden Editor-Locks, Document-Update und allen drei Taskstatus. Der Handler wird stets abgemeldet. Die äussere Testausführung besitzt zusätzlich den bestehenden Hang-Wächter.
- Nach dem Abschluss bleiben die bisherigen No-Effect-/History-Assertions erhalten; `CanStartGui` muss true sein und ein echter Textinput muss den Editor verändern. Die Revoke-Assertions bleiben erhalten.

Keine Änderung an Produktcode, Service-Drain, Timer, Coordinator, MCP-Lifecycle, gemeinsamen Test-Hosts, Parallelitätsattributen, CI, Projekten oder Locks. C01 und C01-F2 bleiben unverändert. Keine neuen Testfälle, Deaktivierungen oder Skips.

## Parallelität und Dispatcher

`LeaseV2Tests` ist bereits `NonParallelizable`; dies wurde nicht erweitert. Die GUI-Tests verwenden die vorhandene gemeinsame Headless-Dispatcher-Session wegen threadgebundener AvaloniaEdit-Caches, erstellen aber eigene Service-/Window-/Editor-Instanzen. Der neue Handler und seine TCS gehören ausschliesslich dem jeweiligen Test und werden im `finally` entfernt. Die Fortsetzung läuft wieder auf dem UI-Kontext, also nach Rückkehr aus dem Workspace-Observer. Ein anderer Test muss keinen Zustand setzen oder räumen, damit die Bedingung erfüllt wird.

Die isolierte Probe deckt beide Callback-Reihenfolgen und zusätzlich eingereihte Dispatcher-Arbeit ab. Die gezielten Tests und die gesamte GUI-Suite prüfen das normale NUnit-/Dispatcher-Umfeld. Das ist kein Nachweis für beliebige Dispatcher-Starvation oder ein zusätzlicher Parallel-Stresstest. Eine unerwartet dauerhaft ausbleibende Benachrichtigung wird weiterhin als Fehler sichtbar.

## Windows-Verifikation und Empfehlung

Windows x64, SDK 10.0.401, Runtime 10.0.12. Gezielte Suite: alle `LeaseV2Tests`, `EditorCoordinatorTests`, `B4F1LifecycleTests` und `B4F2LifecycleTests`. Beide `revoke`-Varianten sind enthalten und bestehen sowohl gezielt als auch in der vollständigen GUI-Suite.

| Prüfung | Debug | Release |
| --- | --- | --- |
| Gezielte Suite (59) | 59 Passed / 0 NotExecuted / 0 Failed | 59 Passed / 0 NotExecuted / 0 Failed |
| Locked Restore und Build beider Solutions | PASS | PASS |
| Core vollständig (11.559) | 11.532 Passed / 27 NotExecuted / 0 Failed | 11.532 Passed / 27 NotExecuted / 0 Failed |
| GUI vollständig (790) | 790 Passed / 0 NotExecuted / 0 Failed | 790 Passed / 0 NotExecuted / 0 Failed |
| MCP vollständig (189) | 176 Passed / 13 NotExecuted / 0 Failed | 176 Passed / 13 NotExecuted / 0 Failed |
| Windows RID-Locked-Publish | nicht vorgesehen | PASS |
| Vorhandener Publish-Artefaktvalidator | nicht vorgesehen | PASS: 5 native Assets und 133 InfoDocs |

Der unveränderte F2-Test `HttpBodyLimitAndListenerDisposal` besteht in beiden vollständigen MCP-Läufen. Die ursprünglichen Gesamtfallzahlen bleiben erhalten. Die Core-Ausnahmen sind 18 lokale Windows-Symlinkprivilegien, 8 Darwin-Fälle und ein expliziter Hardwarebenchmark; die 13 MCP-Ausnahmen sind die bisherigen expliziten lokalen Interaktionsfälle. Skip-Namen und Gründe wurden je Konfiguration mit den F2-TRX verglichen. Nur die zufälligen 32-stelligen IDs der vier beobachteten temporären Symlinkverzeichnis-Familien (`CWE-F032`, `CWE-F06`, `CWE-F03`, `CWM`) wurden für diesen Vergleich normalisiert. Ein erster bytegenauer Vergleich meldete genau diese Laufpfade als Unterschied; alle Rohgründe bleiben erhalten. Keine neuen Skips, keine wiederholten fehlgeschlagenen Testläufe.

`artifacts/C01-F3/test-matrix.json` enthält die tatsächlichen TRX-Zahlen und Roh-Ausnahmegründe; `verification.log` bestätigt beide Varianten, F2, den begrenzten Dateiumfang und die unveränderten 14 C01-Pfade. `targeted.log`, `regression.log`, die acht TRX-Dateien, Build-/Publish-Logs und beide Probe-Traces dokumentieren die ausgeführten Läufe. `LeaseV2Tests.patch` enthält den vollständigen zusätzlichen Quelldiff; `evidence-sha256.json` enthält die Hashes der primären Belege. Alle Artefakte bleiben lokal und ignoriert.

Abschliessend geprüft: HEAD `5dda5b28ff864c5aa776e0f3a6620d424f8a5eb2` und sein Tree `158ef7e0749a417d7efd7e0e1e068fabe55d146e` unverändert, Index leer, `git diff --check` erfolgreich. Nur `LeaseV2Tests.cs` ist modifiziert, nur dieser Bericht kommt unversioniert hinzu. Produktquellen, alle C01-Löschungen/-Blobs, F1-/F2-Berichte und `TransportTests.cs` unverändert. PR #4 ist weiterhin offen/unmerged mit dem bisherigen Head; Remote-master steht weiterhin auf `f401fa7774d5c17172119e7b6838250f6fcf15be`. Andere Worktrees und Branches wurden nicht verändert.

Empfehlung: ausschliesslich diese Test-Synchronisation zur Abnahme vorlegen. Kein Architektur- oder Produktfix ist durch die untersuchten Befunde erforderlich. Ein neuer macOS-ARM64-Nachweis bleibt dem anschliessend gesondert freizugebenden CI-Retest vorbehalten. Keine Commits, Pushes, Merges oder CI-Reruns in F3.
