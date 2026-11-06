# MCP-01.3 – Independent Code Review

Datum: 23.10.2026. Entscheidung: **CHANGES REQUIRED**.

Vier blockierende **Medium**-Findings, keine belegten Critical-/High-Findings. Alle erneut ausgeführten regulären Tests bestehen; die isolierten Gegenproben zeigen dennoch Fehler an Annahme-, Abschluss- und Ressourcengrenzen. Im Zielworktree wird ausschließlich dieser Bericht ergänzt. Keine Produktivänderungen, Teständerungen, Commits, Pushes, Merges, Rebases oder Resets; keine Übernahme von master. `.codex/config.toml` wurde weder gelesen noch verändert.

## 1. Gegenstand und Unabhängigkeit

- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
- Branch: `feature/mcp-server`.
- HEAD: `0a9eb3536ede1e24a070c7375b2852afb90d8ef4`.
- Grundlagen: `MCP-00-architecture.md` (MCP-00-R1), `MCP-00-implementation-plan.md`, `MCP-01.1-validation.md`, `MCP-01.2-validation.md`, `MCP-01.2-review.md`, `MCP-01.3-validation.md`.
- Ein separater Agent `independent_review` erhielt den Auftrag ohne Implementierungshistorie, prüfte den tatsächlichen Quellstand und reproduzierte R2–R4 selbstständig. Er war nicht an der Implementierung beteiligt. Der Hauptagent war an der Implementierung beteiligt; er führte zusätzliche Speichergegenproben und die vollständigen Testläufe aus und führte diesen Bericht zusammen. Beide Bewertungen lauten CHANGES REQUIRED.

Geprüft wurden sämtliche aktuellen Änderungen gegenüber HEAD: `App.axaml.cs`, `CryptoWorkBenchAvalonia.csproj`, `WorkbenchAutomationService.cs`, `WorkbenchExecutionPipeline.cs`, `InterpreterHostQueue.cs`, `CryptoScriptEditViewModel.cs`, `VariableViewModel.cs` sowie die untracked Dateien `AutomationProjection.cs`, `WorkbenchAutomationRegistry.cs`, `WorkbenchRegistryTests.cs` und `MCP-01.3-validation.md`. Die sieben getrackten Dateien umfassen 83 hinzugefügte und 27 entfernte Zeilen; die neuen Dateien wurden zusätzlich vollständig berücksichtigt. Pfade und Zeilen der Findings beziehen sich auf diesen uncommitted Quellstand.

## 2. Architektur und Integration

Die gemeinsame Pipeline bleibt erhalten. `WorkbenchAutomationService.Shared` wird auch als `IWorkbenchAutomationService` registriert. Es gibt weiterhin den globalen CryptoScript-Zustand, die prozessweite Admission und frische Runner je Parse-Einheit. Ein ScriptBook besitzt eine Hostidentität und mehrere Runneridentitäten. Der Service besitzt Execution-Cancellation und Worker; der Requesttoken wird nach Annahme nur am Antwort-Wait verwendet. Das zusätzliche Abstractions-Projektreferenz führt keinen GUI-/MCP-Abhängigkeitszyklus ein. Änderungen an CryptoScript-Sprachsyntax oder Kernsemantik sind nicht enthalten.

MCP-01.2-R1 bleibt grundsätzlich erhalten: Mutation/Refresh und die lokale Präsentation laufen unter derselben Hostreservation. MCP-01.2-R2 bleibt erhalten: Capturefehler zerstören nicht mehr die bereits kopierten Diagnosen, Ausgaben und den internen Runner-Outcome. Das externe Ergebnis kann wegen fehlender zuverlässiger Aufnahme Failed/CaptureFailed werden; dies ist von einem fehlgeschlagenen Script zu unterscheiden. Vorhandene Teilwirkungen werden nicht zurückgerollt.

Die DTO-Projektion kopiert Metadaten, verwendet immutable Sammlungen und sichere Katalogmeldungen. Sie übernimmt weder lebende GUI-/Dictionary-/SemanticError-Objekte noch VAR-/PATH-/Parameterwerte oder Passwortantworten. Ausgabeinhalt bleibt redigiert. Alte DTOs werden durch spätere Operationen nicht verändert. Diese Aussage betrifft die Transportprojektion; **R1 zeigt daneben eine unerlaubte interne Referenzhaltung**.

Status und Metadaten bleiben ohne Interpreterzutritt abfragbar, auch während Passwortwaits. Die reguläre Jobfinalisierung veröffentlicht Version und Snapshot gemeinsam unter `sync`; laufende Zugriffe erhalten stale Metadaten. Kurze Mutationen erhöhen die Version vor dem Capture, während die Hostadmission weiterhin Busy ist; der alte Snapshot ist in diesem Zwischenraum stale. Es wurde daraus keine bestätigte Freigabe eines veralteten Zustands als nutzbarer neuer Zustand abgeleitet. R3 betrifft dagegen den veröffentlichten terminalen Jobzustand während noch laufender Finalisierung.

Recovery liest den bestehenden Zustand erneut, ohne Script-Replay. Metadaten-Recovery vermeidet Value-Getter; ein Fehler ihrer Metadatenaufnahme blockiert den nächsten Script-/Mutationszutritt. Ein erfolgreicher Metadaten-Capture garantiert nicht, dass auch ein späterer vollständiger GUI-Value-Capture gelingt. Diese unterschiedliche Aufnahmegrenze ist bei künftigen Recoverytests zu berücksichtigen.

## 3. Findings

### R1 – Medium: Retention-Timer hält Ausführungsdelegate und Scriptrequest fest

**Datei/Zeile:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:338`, im Kontext von `RunOwnedAsync<T>` ab `:269`; Textrequest-Capture in `:232–240`.

**Ursache:** Der Retention-Callback und das Worker-Lambda verwenden dieselbe compilererzeugte Closure aus `RunOwnedAsync<T>`. Obwohl der Callback sichtbar nur `sync`/`job` verwendet, enthält sein Closureobjekt auch `operation`. Darüber bleibt die gesamte vom Aufrufer erfasste Objektstruktur erreichbar. Die nongenerische `Job.Completion.Task` beseitigt diese zweite Referenzkette nicht.

**Reproduktion:** Eine isolierte Release-Gegenprobe verwendet den vorhandenen internen TimeProvider-Zugang. Eine abgeschlossene `RunAsync`-Operation erfasst ein 16-MiB-Testobjekt. Nach Taskabschluss, `IsBusy=false` und erzwungener GC bleibt dessen WeakReference lebendig. Reflection zeigt die konkrete Kette `RetentionTimer.Callback.Target.operation.Target.payload`. Nach Auslösen des Retentionablaufs und erneuter GC ist das Objekt nicht mehr erreichbar. Eine zweite Probe verwendet unmittelbar `ExecuteTextAsync` mit einem normalen `TextExecution` und Script `VAR retentionProbe=0x(01020304)`: auch das Requestobjekt bleibt nach Abschluss lebendig und wird erst nach Ablauf sammelbar.

```text
AFTER_COMPLETE payloadAlive=True; busy=False; activeTimers=1
AFTER_EXPIRY payloadAlive=False
PUBLIC_TEXT_AFTER_COMPLETE requestAlive=True; busy=False
PUBLIC_TEXT_AFTER_EXPIRY requestAlive=False
```

**Auswirkung:** Das Ergebnisregister hält nach Abschluss Scripttext und beliebige vom GUI-Delegate erfasste Objekte bis zur Eviction/Retention weiter fest. Dies umgeht die beabsichtigte Begrenzung auf sichere Ergebnisprojektionen. Ein einzelner Testpayload von 16 MiB zeigt, dass die Größe dieser Referenzstruktur nicht vom Transportbudget begrenzt wird. Die 64-/15-Minuten-Grenzen begrenzen Anzahl/Zeit, nicht die Größe jedes erfassten GUI-Objektgraphen. Ein Remote-Auslesen dieser Objekte oder ein unentsorgtes Passwort wurde nicht nachgewiesen; die JSON-Redaktion funktioniert weiterhin. Die Behauptung, ausschließlich sichere Projektionen und minimale Jobdaten aufzubewahren, ist dennoch widerlegt.

**Lösung:** Retention-Timer in einem separaten Helfer oder mit statischem Callback und ausdrücklich minimalem State anlegen. Keine gemeinsame Closure mit `operation`, Pipeline, Request oder Präsentationscallback behalten. Nach Abschluss alle ausführungsbezogenen Referenzen freigeben, nur sichere Resultate/Tombstones behalten.

**Erforderlicher Test:** WeakReference-/GC-Prüfung nach Abschluss mit realem Textrequest sowie erfasstem GUI-Payload, während das Ergebnis noch abrufbar ist. Nicht lediglich den Typ von `Job.Task` prüfen. Das Ergebnis darf weiterhin existieren, der Request-/GUI-Payload muss sammelbar sein.

### R2 – Medium: Annahmefehler hinterlässt Reservation und Auftrag ohne Worker

**Datei/Zeile:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:228–232`; entsprechender GUI-Pfad `:257–263`.

**Ursache:** Nach erfolgreichem `Reserve()` folgen Jobanlage, Dictionaryeintrag, `active`-Zuweisung, Deadline-Timer und gegebenenfalls Benutzerregistrierung ohne umfassenden Rollback. Die Bereinigung in `RunOwnedAsync` greift erst, wenn dieser Worker tatsächlich gestartet wurde. Der einzelne Catch um die letzte Request-Cancellation-Prüfung deckt spätere Fehler nicht ab.

**Reproduktion:** Ein ausdrücklich fehlerinjizierender `TimeProvider.CreateTimer` wirft `InvalidOperationException` bei der Annahme. Danach liefert das Register `Running`, `FinishedAt=null`, `IsBusy=true`; ein weiterer Auftrag erhält Busy. Nach `BeginShutdown` beendet sich `DrainAsync` nicht, weil der registrierte Completion-Task keinen Worker besitzt.

**Auswirkung:** Ein fehlgeschlagener Annahmeschritt kann die prozessweite Hostadmission dauerhaft blockieren und einen Phantomauftrag hinterlassen. Dies verletzt die ausdrücklich geforderte Fehleratomizität. Die Probe simuliert einen Initialisierungsfehler am vorhandenen Testzugang; sie behauptet keinen regelmäßig auftretenden Fehler des System-TimeProviders. Wegen dieser Fehlerbedingung ist die Einstufung Medium trotz der schweren Folge angemessen.

**Lösung:** Annahme als Transaktion mit expliziter Ownership-Übergabe an den Worker implementieren. Vor der Übergabe bei jedem Fehler Job-/active-Eintrag entfernen, angelegte Timer, Registration und CTS bereinigen und die Reservation freigeben. Auch einen Fehler der Workerbereitstellung berücksichtigen. Ein abgewiesener Auftrag darf keine unvollständige angenommene ID hinterlassen.

**Erforderlicher Test:** Fehler an Timeranlage und Worker-/Registrierungsgrenze injizieren; anschließend NotFound bzw. konsistentes dokumentiertes Fehlerergebnis, freie Admission, abschließbarer Drain und erfolgreicher Folgeauftrag prüfen.

### R3 – Medium: Terminalzustand wird vor Ende der Cancellation-Callbacks veröffentlicht

**Datei/Zeile:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:304–305`; Callbackdrain `:346–351`; terminale Veröffentlichung `:401–403`.

**Ursache:** `FinalizeJob` setzt terminalen Zustand, `FinishedAt` und Version, bevor `ReleaseResourcesAsync` laufende `CancellationCallbacks` abwartet. `CancelAsync` kann solche Callbacks noch ausführen, während die eigentliche Operation bereits zurückgekehrt ist.

**Reproduktion:** Eine kontrollierte Operation registriert einen blockierenden Callback am serviceeigenen ExecutionToken. Nach explizitem Cancel wird der Worker freigegeben, der Callback bleibt blockiert. Das Ergebnis lautet:

```text
state=Cancelled; finished=True; busy=True;
drainCompleted=False; workCompleted=False
```

**Auswirkung:** `GetExecution` und das davon abgeleitete `GetDiagnostics.IsFinal` melden einen abgeschlossenen Auftrag, obwohl vorgeschriebene Abbruchbereinigung noch läuft. Die Admission bleibt in dieser Probe korrekt gehalten; es wurde kein paralleler Runnerzutritt nachgewiesen. Dennoch widerspricht die Publikation dem Vertrag in `MCP-00-architecture.md:256`, nach dem der Zustand bis zum Ende von Runner, Hostcallbacks und Finalisierung cancellationRequested bleibt. Auch der Retentionzeitpunkt wird dadurch zu früh festgelegt.

**Lösung:** Die terminale Veröffentlichung einschließlich Abschlusszeit erst nach dem notwendigen Callback-/Ressourcendrain unter weiterhin gehaltener Admission vornehmen. Fehler der Bereinigung dürfen dabei weder Completion verlieren noch den Auftrag ohne nachvollziehbaren Abschluss lassen.

**Erforderlicher Test:** Während eines blockierten Cancellation-Callbacks `FinishedAt=null`, nichtterminalen Zustand, `IsFinal=false`, Busy und unvollständigen Drain prüfen; erst nach Callbackfreigabe terminal publizieren. Deadline/Shutdown als Varianten ergänzen.

### R4 – Medium: Abbruch vor Runnerstart meldet mögliche Wirkungen und erhöht stateVersion

**Datei/Zeile:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs:136–156`, insbesondere `:156`; Weiterverarbeitung in `WorkbenchAutomationRegistry.cs:363` und `:392`.

**Ursache:** `EffectsPossible` wird ausschließlich aus `runnerId.HasValue` abgeleitet. Ein erzeugter Runner ist noch kein ausgeführter Runner. Der Core prüft Cancellation vor dem tatsächlichen Runnerstart, die Pipeline erzeugt anschließend dennoch eine Präsentation mit Wirkungsmarker.

**Reproduktion:** Die angenommene Hostoperation wird vor `pipeline.ExecuteAsync` angehalten. Expliziter Cancel wird registriert; erst danach läuft die Pipeline mit `VAR mustNotRun=0x(01)` weiter. Die Variable entsteht nicht und der Core verwirft den bereits abgebrochenen Token vor Runnerausführung. Beobachtet wird trotzdem `Cancelled; Effects=Possible; StateVersion=1; actualVariable=False`.

**Auswirkung:** Ein vollständig vor Ausführungsbeginn abgebrochener Auftrag erscheint als möglicherweise wirksam; allein dieser falsche Marker erzeugt eine neue Zustandsversion. Das verletzt `effects=none` vor Ausführungsbeginn nach MCP-00-R1 und den auf ausgeführten Hostgrenzen beruhenden Versionsvertrag. Das ist keine Forderung, nach bereits begonnener Ausführung Wirkungsfreiheit zu beweisen: dort ist Possible weiterhin richtig.

**Lösung:** Tatsächlichen Ausführungsbeginn von Runneranlage unterscheiden und Cancellation an der Parse-/Startgrenze berücksichtigen. Die Version anhand einer tatsächlich erreichten Ausführungs-/Mutationsgrenze erhöhen. Ein lediglich vorhandener Runner-Identifier genügt dafür nicht.

**Erforderlicher Test:** Cancel und Deadline nach Annahme, aber vor Eintritt in die Runnerausführung erzwingen; Effects.None, unveränderte stateVersion und unveränderten Interpreterzustand prüfen.

## 4. Ausgeführte Prüfungen und Gegenproben

Die regulären Tests wurden für diesen Review neu gegen den aktuellen Release-Quellstand ausgeführt, nicht aus dem Implementierungsbericht übernommen:

| Prüfung | Neues Ergebnis |
| --- | --- |
| `dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release --no-restore` | 308 bestanden, 0 fehlgeschlagen; enthält alle 35 Registryfälle |
| `dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj -c Release --no-restore` | 7 667 bestanden, 4 übersprungen, 0 fehlgeschlagen |
| `dotnet test CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj -c Release --no-restore` | 89 bestanden, 0 fehlgeschlagen; expliziter InstalledCodex-Test nicht ausgeführt |
| Werfender Timer bei Annahme | R2 reproduziert; isolierter Prozess verhindert Übertragung der blockierten statischen Admission auf andere Tests |
| Cancel vor Pipeline-/Runnerstart | R4 reproduziert |
| Blockierter Execution-Cancellation-Callback nach Workerende | R3 reproduziert; Gate bleibt korrekt belegt |
| 16-MiB-Payload und realer Textrequest nach Abschluss | R1 durch WeakReference, erzwungene GC und Closureinspektion reproduziert |
| Bereits unterdrückter ExecutionContext bei Annahme | Negative Gegenprobe: Completed, Busy=false; kein zusätzlicher Startfehler unter .NET 10 |

Testlogs liegen separat im Chat-Arbeitsordner `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp013-review-main/results/`: `review-gui.trx`, `review-core.trx`, `review-mcp.trx`. Die Speicherprobe liegt daneben in `Program.cs`/`probe.csproj`. Der unabhängige Reviewer hat seine drei Gegenproben unter `review-independent/admission-failure.cs.txt`, `prestart-cancellation.cs.txt` und `premature-terminal.cs.txt` im selben Chat-Arbeitsordner gesichert. Diese Probequellen sind keine Änderungen an Produktion oder bestehenden Tests. Die oben dokumentierten Abläufe und Ergebnisse bleiben auch ohne diese lokalen Dateien nachvollziehbar.

Kein erneuter Restore, nativer Desktop-Smoke oder produktiver HTTP-/Codex-End-to-End-Nachweis wurde behauptet. Die expliziten bzw. plattformbedingten Testausschlüsse bleiben bestehen. Die Release-Testbefehle bauen ihre Projektabhängigkeiten; sie ersetzen keinen unabhängigen Build aller nicht beteiligten Solutionziele.

## 5. Bewertung aller 35 neuen Registry-Testfälle

Die 29 Testmethoden ergeben durch Parametrisierung 35 Fälle. Die folgende Tabelle nennt jeden Methodenbezug; Namen sind teilweise auf ihren eindeutigen Anfang gekürzt.

| Testmethode / Fälle | Aussagekraft und Grenze |
| --- | --- |
| SnapshotsDetachAllGuiObjects… (1) | Sinnvoller Nachweis für Kopien und Redaktion aller Wertkategorien; prüft JSON, nicht interne Referenzhaltung R1. |
| VersionTracksMutationsRuntimeAndSyntaxBoundaries (1) | Deckt reguläre Mutation, Laufzeit und Syntaxgrenze ab; vor Runnerstart abgebrochene Annahme fehlt. |
| BookHasOneServiceIdentity… (1) | Belegt Host-/Runneridentitäten und Buchgesamtgrenze. |
| IntermediateExecutionSnapshot… (1) | Belegt letzten abgeschlossenen Variablenstand während eines Buchs. |
| BookSyntaxDiagnosticsPreservePhysicalLineNumbers (1) | Prüft physische Zeilenzuordnung. |
| SimultaneousIdenticalIdsExecuteAndPrintExactlyOnce (1) | Prüft aktives Duplikat, Argumentkonflikt und terminalen Retry. Der zweite Aufruf erfolgt erst nach Print-Eintritt des ersten: kein gleichzeitig freigegebenes Annahmerennen. |
| CancelledRequestBeforeAdmissionCreatesNoJob (1) | Belegt bereits vorab abgebrochenen Request; kein Abbruch genau zwischen letzter Prüfung und Ownership-Übergabe. |
| LostResponseWaitDoesNotCancelExecution… (1) | Guter kontrollierter Nachweis der Wait-/Execution-Trennung und des Wiederauffindens. |
| CancellationAndDeadlineWaitForSynchronousStatement… (4) | Prüft explizit/Timeout und letztes/nichtletztes Statement; aktive Retention und konservative späte Completion sinnvoll abgedeckt. |
| PasswordDeadlineAndExplicitCancellation… (2) | Guter Passwortdrain-, Secret-Dispose- und Admissionnachweis; anderer Callbackdrain nach FinalizeJob fehlt. |
| CaptureFailureKeepsOutcomeAndRecovery… (3) | Prüft Capturefehler bei Erfolg/Laufzeit-/Syntaxpfad, Diagnosen, Faulted und Recovery ohne Replay. |
| InvalidIdsArgumentsAndUnsafeExternalPaths… (1) | Prüft InvalidArgument, InstanceMismatch, StateConflict und deaktivierte externe Ausführung; kein Annahmefehler nach Reserve. |
| OwnershipPermissionAndActivationRevocation… (1) | Prüft Principal/Quelle/Permissions und alte Epochen, einschließlich fremdem Cancel. |
| RetentionEvictsOldestAndExpiredIdsNeverReplay (1) | Fake-Zeit prüft Count/TTL/Tombstones bei gleichen Zeitstempeln; keine konkurrierende Timer-/Lookup-Ausführung. |
| TombstoneCapacityRejectsNewIds… (1) | Grenzcheck 10 000 über direkte Dictionarybefüllung; nicht 10 000 echte Annahme-/Evictionübergänge. |
| ProjectionLimitsDoNotTruncateLocalOutput… (1) | Prüft 1 000-Eintragsgrenzen, Sequenz und lokale Vollständigkeit. |
| DiagnosticLimitsKeepErrorFlag… (1) | Prüft Truncation, Errorflag und sichere Diagnosetexte. |
| FinalPresentationRetainsAdmissionAndShutdown… (1) | Hält Präsentation vor FinalizeJob offen; belegt Shutdown/Drain dort, nicht R3 im anschließenden Ressourcenabschluss. |
| ByteBudgetEvictsResultsBeforeCountLimit… (1) | Prüft gespeicherte serialisierte Resultate plus aktuellen Snapshot; keine Objektgraphgröße oder Peakbelegung bei aktivem Job. |
| FailedMutationPublishesPartialState… (1) | Prüft Teilwirkung, Version und Capture nach Mutationsfehler. |
| CancelRequestAlreadyCancelled… (1) | Prüft vorab abgebrochenen Cancel-Wait und ersten Grund bei sequenziell nachfolgendem Timeout/Shutdown; kein simultaner Completionwettlauf. |
| BusyRejectionLeavesIdReusable… (1) | Belegt fehlenden Eintrag bei Busy und Wiederverwendbarkeit der abgewiesenen ID. |
| DiagnosticsStayImmutableAfterMutableSourceChanges (1) | Sinnvolle Mutation sowohl der Core- als auch lokalen Diagnosekopien. |
| HostFailureIsObservedAndResultUsesOnlyCatalogMessages (1) | Prüft Workerexception und sicheren externen Fehler; kein Fehler vor Workerstart. |
| ClearUsesSharedVersionBoundary… (1) | Prüft Shared-Clear und getrennte ServiceinstanceIds. |
| RecoveryFailureBlocksScriptAndMutation… (1) | Gute Negativprobe mit werfendem Metadatengetter und anschließender Recovery. |
| McpDisableCancelsOwnedWork… (1) | Prüft Widerruf/Drain/Verwerfen mit per Reflection gebundenem MCP-Owner; korrekt kein produktiver MCP-Ausführungszugang, aber kein Integrationstest seiner späteren Authentisierung. |
| CompletedRegistryNeverRetainsTypedGuiTaskResults (1) | Prüft nur nongenerischen Tasktyp und redigiertes JSON. Widerlegt R1 nicht; Titel beansprucht mehr als der Nachweis. |
| AcceptedWorkerDoesNotInheritRequestScopedAmbientObjects (1) | Belegt ExecutionContext-Unterdrückung im Worker, nicht das Ende von explizit erfassten Delegate-/Requestreferenzen. |

Zusätzlich zu Regressionen für R1–R4 sollten echte Barrieren für zwei konkurrierende Annahmen, Cancel gegen Completion, Timerablauf gegen Abruf/Neuanlage und Recovery gegen neue Admission ergänzt werden. Die vorhandene Lockstruktur liefert für mehrere dieser Rennen eine plausible Absicherung; fehlende Tests allein werden hier nicht als weitere Fehler ausgegeben. Die testspezifische Teardown-Busy-Prüfung beweist kein vollständiges Ende von Timern und Referenzen.

## 6. Headless-Lifecycle und Evidenzgrenze

Der Implementierungsbericht nennt zwei sporadische NullReferenceExceptions direkt in `Avalonia.Headless.HeadlessUnitTestSession.Dispose`, bei `ProductiveSecureLoadDialogUpdatesVariablesAndStopsBookOnFailure("wrong")` und `RenderedMatrixLinkAndBackNavigateThroughActualView(1,1)`. Der gespeicherte TRX für den zweiten Zwischenfehler wurde geprüft. Der neue vollständige GUI-Lauf besteht. Das allein ist keine Entwarnung.

Der Master-Backport in Commit `ca77a94` wurde ausschließlich lesend verglichen. Er ersetzt im Sessionstart `task = Task.Run(...)` durch vorab konstruiertes `new Task(...)` und anschließendes `Start(TaskScheduler.Default)`. Damit kann der Worker der Session nicht mehr den noch nullen äußeren Task übergeben. Außerdem verschiebt der Patch `CompleteAdding` hinter das Warten auf den Dispatchtask. Der bekannte Race und die dokumentierte NRE in Dispose sind miteinander vereinbar. Der Stack der konkreten Zwischenläufe beweist aber nicht, dass dort genau dieser Nulltask vorlag; eine abschließende Kausalzuordnung ist nicht möglich.

Die produktiven Hostpfade warten auf ihre Operation/Präsentation und Passwortantwortbereinigung. Die regulären Tests benutzen kontrollierte Freigaben und warten auf diese Tasks. In der neuen Cancellationgegenprobe bleibt notwendige Arbeit trotz bereits terminaler Ergebnisanzeige offen (R3), jedoch weiterhin unter Admission und sichtbar für Drain. Die Abschlussanzeige allein ist daher kein hinreichendes Teardownsignal.

Statische Serviceinstanz, globale Events und Retention-Timer können testübergreifende Lebensdauer besitzen. Pipeline-Print-/Info-/Passwortsubscriptions werden im finally gelöst; dies ist nicht gleichbedeutend mit Freigabe aller Objekte. R1 reproduziert konkret fortbestehende Delegate-/Payloadreferenzen nach Taskabschluss. Auch die neuen Registrytests mit ManualClock prüfen im Teardown nur Busy=false und kontrollieren nicht die gesamte Timer-/Objektlebensdauer. Ein durch MCP-01.3 neu verursachter Headless-Dispose-NRE wurde dagegen nicht reproduziert. Die nachgewiesene Retentionlücke darf weder als Beweis für diese NRE noch der bekannte Avalonia-Race als Entlastung für die Retentionlücke verwendet werden.

Empfehlung nach den Korrekturen: vollständigen Worker-/Callbackdrain und Referenzfreigabe getrennt vom Sessiondispose testen; Headless-Dispose bei Wiederholung mit Session-/Dispatchtaskdaten instrumentieren. Den Backport nur in einer separat autorisierten Integrationsänderung übernehmen.

## 7. Abgleich mit MCP-00-R1 und Übergabegrenzen

| Vertrag | Bewertung |
| --- | --- |
| Eine Pipeline, globaler Zustand, frische Runner, ganze Hostoperation exklusiv | Grundsätzlich erhalten; keine zweite MCP-Pipeline. |
| Atomare Annahme mit serviceeigenem Worker | Normalfall unter Lock; Fehleratomizität durch R2 verletzt. |
| ID-/Principal-/Quellen-/Epochenbindung, kein Replay | Lookup/Fingerprint/Tombstones plausibel und gezielt getestet; Busy/StateConflict ohne Eintrag. |
| Request-Cancellation nur Antwort-Wait nach Annahme | Erhalten; neue Gegenproben bestätigen keinen vorzeitigen Gate-/CTS-Abbau durch verlorenen Wait. |
| Erster Abbruchgrund, späte Completion, Passwortquarantäne | Grundsätzlich erhalten; terminaler Zeitpunkt durch R3 verletzt. |
| Keine Wirkung vor Ausführungsbeginn | Durch R4 verletzt. |
| Unveränderliche sichere Snapshots und konservative Teilwirkungen | Transportprojektion erfüllt die geprüften Grenzen; R4 verfälscht Wirkungs-/Versionsmarker. |
| Capturefehler getrennt, Recovery ohne Replay | Erhalten; Aufnahmefehler bleiben sichtbar und blockieren unsicheren Zutritt. |
| 64 Ergebnisse / 15 Minuten / 8 MiB Transportdaten / 10 000 IDs | Count/TTL/Byte-Pruning und Tombstones vorhanden, aktive Jobs ausgenommen, Reihenfolge über Abschlusssequenz. R1 umgeht die Begrenzung auf Projektionen durch zusätzlichen Objektgraphen; R3 setzt die Abschlusszeit zu früh. |
| Keine langlebigen Request-/GUI-/Secretreferenzen | Durch R1 verletzt; keine Übertragung von Passwortwerten in externe DTOs nachgewiesen. |
| MCP-Disable nur MCP, Shutdown stoppt Annahme, Drain separat | Implementiert; Tests verwenden für MCP-Origin einen internen Simulationszugang, keine produktive Ausführungsfreigabe. |

Kein produktiver HTTP-Serverstart, keine neuen MCP-Tools, kein unbestätigter MCP-Interpreterzugang, keine Editorrevisionen, keine Save-Commit-Sperre, kein Pairing, keine produktive HTTP-Authentisierung, keine persistenten Auftragsdaten und keine neue CryptoScript-Sprachfunktion wurden vorgezogen. Der Testtransport gewinnt keinen unautorisierten Interpreterzugang. Die bekannte GUI-Save-Lücke bleibt ausdrücklich MCP-01.4.

Die bestehende Print-/Info-Multicast-Grenze bleibt ein bekanntes Risiko: Ein früher werfender Abonnent kann spätere Collectoraufrufe verhindern. Der Review hat keine Verschlimmerung dieser Eventsemantik durch MCP-01.3 belegt. Künftige Transport-/Outputarbeit muss diese Grenze weiterhin berücksichtigen; sie wird hier nicht als zusätzlicher Blocker umetikettiert.

## 8. Entscheidung

**CHANGES REQUIRED.** Vor Freigabe zum Commit R1–R4 korrigieren und gezielte Regressionen nachreichen. Die bestandenen regulären Suiten sind ein brauchbarer Integrationsnachweis, widerlegen die reproduzierten Fehler aber nicht. MCP-01.4 wurde nicht begonnen; nach Ergänzung dieses Berichts endet der Review.

## 9. Korrekturstatus R1–R4 (23.10.2026, Implementierungsnachtrag)

Die ursprünglichen vier Findings und die unabhängige Entscheidung **CHANGES REQUIRED** oben bleiben unverändert als Reviewhistorie erhalten. Dieser Nachtrag stammt vom implementierenden Agenten und ist **keine unabhängige Freigabe**. Aktueller Implementierungsstatus: **READY FOR RE-REVIEW**.

| Finding | Tatsächliche Korrektur | Nachweis |
| --- | --- | --- |
| R1 | Registry `:185–214`: statische Timercallbacks, eigener Retentionhelfer, minimaler State aus Service/ID/RegistrationId, kein Worker-/Requestdelegate. `DisposeAsync` ab `:585` dränt Arbeit und verwirft Retentiontimer. | 16-MiB-Payload und echter Textrequest bei abrufbarem Ergebnis sammelbar; reale Callback-/Statestruktur geprüft, Job nach Ablauf sammelbar; Tombstone/kein Replay und Service-Disposal getestet. Ursprüngliche Review-Speicherprobe unverändert erneut ausgeführt: beide WeakReferences schon direkt nach Abschluss false. |
| R2 | Registry `:218–263`: abgesicherte Annahme bis zur Ownership-Übergabe, vollständiger Rollback ohne angenommene ID/Tombstone bei Initialisierungsfehlern. Eine RegistrationId verhindert, dass verspätete alte Callbacks einen Retry mit derselben executionId abbrechen. | Timer-, Registration- und Workergrenzfehler, jeweils auch mit Request-Cancellation; GUI-Registrationbereinigung, Cancel an Übergabe, erfolgreicher Folgeauftrag, echter konkurrierender Doppelrequest mit genau einem Print und parallelen Ergebnisabfragen. |
| R3 | Registry `:364–365`, `:406–438`: notwendiger Callback-/Ressourcendrain vor terminaler Publikation, atomar geschlossene Cancelgrenze, beobachtete Cleanupfehler als Failed/InternalError; Completion und Admissionfreigabe unter abschließendem Lock/Finally. | Blockierter/werfender Callback, Explicit/Timeout/Shutdown, wiederholter Cancel, kein FinishedAt/IsFinal/Retention während Drain, Deadline während blockierter Ressourcenbereinigung, verlorener Antwortwait plus Shutdown sowie Capturefehler nach Teilwirkung. |
| R4 | `CryptoScriptRunner.cs:15–17` liefert die vorhandene aktive/letzte ExecutionId als reine Leseeigenschaft; Pipeline `:43–46`, `:73`, `:144` unterscheidet tatsächlichen Eintritt von Runneranlage. Keine Evaluierungs- oder Sprachänderung. | Vier Explicit-/Deadline-Fälle vor Pipeline bzw. nach Runneranlage mit None/unveränderter Version; Buch vor erster/nach erster ausgeführter Zeile; bisherige Tests für bereits gestartete Operationen unverändert grün. |

Geändert wurden in diesem Korrekturschritt Registry, Pipeline, der schmale Core-Ausführungsmarker, die neue `WorkbenchRegistryCorrectionTests.cs` sowie die beiden MCP-01.3-Dokumente. Die 30 neuen Fälle ergänzen die bisherigen Tests; keiner der bisherigen Fälle wurde verändert oder abgeschwächt. Internal-Testnähte sind instanzlokal und nicht über MCP/JSON oder Produktkonfiguration erreichbar. Die Workergrenzprobe ist kontrollierte Fehlerinjektion vor Übergabe, kein behaupteter realer Threadpool-Ausfall.

Neu validiert: **338 GUI-Fälle**, **7 667 Core-Fälle bei 4 bisherigen Skips**, **89 reguläre MCP-Fälle** bestanden; Restore und Release-Build beider Solutions erfolgreich. Projektgrenzen und JSON-/Transportverträge bleiben unverändert; `git diff --check` bestanden. Die Korrektur fügt keine Projektreferenz hinzu. Die reinen inkrementellen Endbuilds haben keine Warnungen; vorhandene sechs Core- und acht GUI-Nullable-Warnungen erschienen beim vorherigen Neubau weiterhin.

Der erste Lauf der neu geschriebenen Proben hatte zwei Testkonstruktionsfehler (Core-Lesen unter gehaltener Admission und eine im Test selbst lebendig gehaltene GC-Referenz). Stack, Korrektur, Zwischenresultate und Logpfade sind im [Validierungsnachtrag, Abschnitt 11](MCP-01.3-validation.md#11-korrekturstatus-r1r4-nach-unabhängigem-review-23102026) dokumentiert. Der fehlgeschlagene TRX bleibt erhalten; erfolgreiche Nachläufe ersetzen diese Historie nicht.

Die beiden früheren Headless-Dispose-Ausnahmen bleiben ursächlich ungeklärt. In diesem Korrekturlauf trat keine auf; daraus folgt keine allgemeine Entwarnung. Kein Master-Backport und kein allgemeines Avalonia-Refactoring. Native Closing-/HTTP-Integration, GUI-Save-Lücke (MCP-01.4), Editorrevisionen, Auth/Pairing und die bekannte Print-/Info-Multicast-Grenze bleiben unveränderte Übergabepunkte. Kein Commit, Push, Merge oder Rebase. Die unabhängige Beurteilung der Korrektur folgt ausschließlich im separat beauftragten Re-Review.


## 10. Final Re-Review – 23.10.2026

**Entscheidung: READY FOR COMMIT. R1–R4: VERIFIED FIXED. Keine neuen blockierenden Findings.**

### Gegenstand, Unabhängigkeit und Änderungsgrenze

Geprüft wurde der tatsächliche korrigierte Worktree auf `feature/mcp-server`, HEAD weiterhin `0a9eb3536ede1e24a070c7375b2852afb90d8ef4`. Grundlagen waren MCP-00-R1/Implementierungsplan, der vollständige bisherige MCP-01.3-Review einschließlich Korrekturanhang und der Validierungsbericht. Die ursprüngliche Entscheidung CHANGES REQUIRED, die vier Findings und sämtliche Nachträge oberhalb dieses Abschnitts bleiben unverändert erhalten.

Derselbe unabhängige Agent `independent_review` wie beim ursprünglichen Review wurde erneut eingesetzt. Er war an den Korrekturen nicht beteiligt, las den aktuellen Code und alle 30 neuen Fälle, wiederholte seine ursprünglichen Gegenproben und entwickelte zusätzliche isolierte Lock-/Disposal-/Runnerproben. Sein eigenständiges Urteil lautet ebenfalls **READY FOR COMMIT**. Der Hauptagent war an der Korrekturimplementierung beteiligt; er führte ergänzend die 65 Registrytests, die unveränderte Speicherprobe und eigene Disposal-/Runnerproben aus und fügte diesen Bericht zusammen. Die Freigabe stützt sich auf unabhängige Quellcodeprüfung und Gegenproben, nicht allein auf die bestehenden grünen Validierungslogs.

Im Zielrepository ergänzt dieser Re-Review ausschließlich diesen Abschnitt. Quell-/Testdateien und `MCP-01.3-validation.md` wurden vor und nach der Prüfung per SHA-256 verglichen. Keine Produktiv- oder Teständerungen, kein Commit, Push, Merge oder Rebase, kein MCP-01.4-Beginn. `.codex/config.toml` wurde weder gelesen noch verändert.

### R1 – VERIFIED FIXED: Retention und Referenzlebensdauer

**Quellen:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:185–214` (Timerhelfer/-state), `:230–235` (Userregistration), `:429–433` (Freigabe ausführungsbezogener Ressourcen), `:501–511` (Expire), `:585–591` (Service-Disposal). Regressionen: `CryptoWorkBenchGuiTest/WorkbenchRegistryCorrectionTests.cs:42–108`.

`StartRetention` liegt außerhalb der Workerfunktion. Sein statisches Lambda besitzt keine gemeinsame Closure mit `RunOwnedAsync<T>`. Der tatsächliche Callbacktarget ist eine zustandslose Compiler-Singletoninstanz ohne Instanzfelder. `TimerTarget` enthält ausschließlich Service, executionId und RegistrationId. Keine Referenz auf Operation, TextExecution, Scripttext, Präsentation oder GUI-Payload ist darin vorhanden. Der Serviceanteil ermöglicht den Lookup des sicheren Registereintrags; er ist keine versteckte Workerclosure. Die Registration ist ebenfalls statisch und verwendet diese minimale Identifikation. Nach Ressourcenabschluss werden Callbacktask und Userregistration zurückgesetzt; der Registertask enthält kein typisiertes GUI-Ergebnis.

Die ursprüngliche Speichergegenprobe wurde unverändert gegen den korrigierten Release-Quellstand ausgeführt. Bereits während gültiger Retention sind sowohl das erfasste 16-MiB-Payload als auch ein echter TextExecution-Request sammelbar:

```text
AFTER_COMPLETE payloadAlive=False; busy=False; activeTimers=1
Retention.Callback.Target: WorkbenchAutomationService+<>c
AFTER_EXPIRY payloadAlive=False
PUBLIC_TEXT_AFTER_COMPLETE requestAlive=False; busy=False
PUBLIC_TEXT_AFTER_EXPIRY requestAlive=False
```

Die neuen Regressionen prüfen zusätzlich die tatsächlichen Felder von Callbacktarget und Timerstate. Sichere Ergebnisse bleiben während der Payload-/Requestfreigabe abrufbar. Ablauf entfernt den Job und entsorgt den Timer; eine WeakReference auf den Job wird anschließend ungültig. Die ID bleibt als Tombstone bekannt und ein identischer Retry führt nicht erneut aus. Wiederholtes Service-Disposal entfernt verbleibende Ergebnisse/Timer.

Auch der unabhängige Reviewer führte diese ursprüngliche Speicherquelle selbst unverändert aus und bestätigte dieselben Werte. Seine zusätzliche eigenständige Reflection-Gegenprobe meldete `CallbackTargetInstanceFields=0`, ausschließlich die Statefelder `Owner:WorkbenchAutomationService`, `Id:Guid`, `RegistrationId:Guid` und `SafeResultRetained=True`. Damit liegt für R1 ebenfalls eine vollständig vom Korrekturimplementierer unabhängige Ausführung vor.

**Verbleibende Grenze:** GC-Prüfungen wurden unter .NET 10 Release auf Windows durchgeführt. Die zusätzliche Strukturprüfung und der statische Callbackaufbau begrenzen die Aussage nicht auf einen zufällig erfolgreichen GC-Lauf. Die lokale sichere globale Variablenprojektion darf als aktueller Zustand bestehen bleiben; sie enthält keine rohen Script-/GUI-Werte.

### R2 – VERIFIED FIXED: Fehleratomizität und Ownership

**Quellen:** `WorkbenchAutomationRegistry.cs:218–263` (Accept/Rollback), `:53` (RegistrationId), `:190–207` und `:230–235` (identitätsgeprüfte Callbacks), `:329–404` (Workerownership und Completion). Regressionen: `WorkbenchRegistryCorrectionTests.cs:110–199`.

Vor der Übergabe an `RunOwnedAsync` stehen Anlage und Initialisierung unter demselben Registerlock und derselben Reservation. Jeder untersuchte Fehler davor entfernt Job und active, schließt Cancellation, meldet die Userregistration nicht wartend ab, bereinigt Timer/CTS, signalisiert die private Completion und gibt die Admission frei. Es wird kein Tombstone für einen nicht angenommenen Auftrag angelegt. Nach Rückgabe des serviceeigenen Workertasks liegt die weitere Verantwortung beim Worker; der äußere Annahmepfad nimmt ihm die Ressourcen nicht wieder ab.

Die ursprüngliche bewusst werfende Timerprobe findet jetzt keinen Auftrag mehr und meldet Busy=false. Da die unveränderte Probe ihre Clock auch beim zweiten Aufruf absichtlich weiter werfen lässt, endet sie beim Folgeaufruf erneut an dieser Injektion. Das wurde als erwartete Probegrenze dokumentiert und separat durch eine Fail-once-Variante ergänzt: danach NotFound, abgeschlossener Drain und erfolgreicher Retry **derselben executionId**, Completed mit Version 1.

Die 65 erneut ausgeführten Tests enthalten Timer-, Registrierungs- und Workergrenzfehler, jeweils auch mit gleichzeitigem Requestabbruch, GUI-Registrationbereinigung, Request-/Execution-Cancel an der Übergabe und den erfolgreichen Folgeauftrag. Die Workergrenznaht liegt vor der Ownership-Übergabe; sie simuliert keinen tatsächlichen Threadpool-Ausfall.

Der unabhängige Reviewer erzwang zusätzlich einen bereits gestarteten User-Cancel-Callback: Ein eigener Thread führte Cancel aus, ein vorgeschalteter LIFO-Callback bestätigte dessen Start; anschließend wartete der Thread nachweislich im Servicecallback auf dem vom AdmissionCheckpoint weiterhin gehaltenen `sync`. Erst dann wurde der Annahmefehler injiziert. Ergebnis:

```text
callback definitely waiting on held sync=True
completedAfterRollback=True
busy=False; drain=True; followup=True
```

`Unregister` wartet somit in diesem geprüften Rennen nicht auf den blockierten Callback. Nach Freigabe des Locks findet der Callback keinen passenden alten Eintrag. Ein alter Timercallback kann auch bei wiederverwendeter executionId keinen neuen Auftrag abbrechen, weil zusätzlich die pro Annahme neue RegistrationId verglichen wird. Der Test mit gespeicherten alten Callbackdaten bestätigt dies. Zwei über eine echte gemeinsame Barrier gestartete identische Requests erzeugen genau einen Print; parallele Abfragen und terminale Retries erzeugen keine weitere Ausführung.

**Verbleibende Grenze:** Fehlerinjektionen sind kontrollierte Initialisierungsfehler an den vorhandenen internen Testzugängen. Es wird kein regelmäßig auftretender Systemtimer-/Schedulerfehler behauptet. Die Ownership- und Lockprüfung zeigt für diese Fehlerpfade keine verbleibende Phantomoperation oder vorzeitige Freigabe.

### R3 – VERIFIED FIXED: vollständiger Drain vor Terminalzustand

**Quellen:** `WorkbenchAutomationRegistry.cs:364–365`, `:381–382` (Cleanup vor FinalizeJob), `:406–438` (Drain und CancellationClosed), `:465–490` (finale Publikation), `:387–403` (Retention/Reservation/Completion), `:581–591` (Drain/Dispose). Regressionen: `WorkbenchRegistryCorrectionTests.cs:202–260`, `:324–368`.

Der Worker wartet jetzt auf Timer-/Userregistration-Disposal und den Execution-Cancellation-Callbacktask, bevor er den terminalen Snapshot publiziert. Das Ende der möglichen Cancellation wird unter `sync` erst geschlossen, wenn der beobachtete Callbacktask vollständig abgeschlossen ist. Ein konkurrierender Cancel vor dieser Grenze wird drainiert; hinter der Grenze kann er keine neue Callbackarbeit mehr hinzufügen. Weder die Benutzerregistrierung noch laufende Cancellation-Callbacks werden unter dem Registerlock abgewartet. Callbackfehler werden beobachtet und als sicherer Failed/InternalError-Abschluss ohne Rohtext ausgegeben.

Die ursprüngliche unabhängige blockierte Cancellation-Callbackprobe liefert jetzt während des offenen Callbacks:

```text
state=CancellationRequested; finished=False; busy=True;
drainCompleted=False; workCompleted=False
```

Der neue Test prüft zusätzlich IsFinal=false und eine Stunde kontrollierten Zeitablauf, ohne Retention/Eviction des aktiven Auftrags. Explicit, Timeout, Shutdown und wiederholter Cancel behalten den ersten Grund. Nach Freigabe entstehen terminaler Zustand, Abschlusszeit und Retention; die TTL wird vom tatsächlichen Abschluss gemessen. Werfender Callback und werfender Ressourcen-Cleanup enden sicher mit Failed und beobachtetem Fehler.

Root und unabhängiger Reviewer prüften außerdem `DisposeAsync` während eines aktiven Callbacks. Das Disposal wartet ebenfalls; FinishedAt bleibt null, IsFinal false, Admission Busy und Drain/Dispose/Worker bleiben unvollständig. Nach Callbackfreigabe sind Arbeit und Disposal abgeschlossen, Busy=false und Lookup liefert ResultExpired. Die Kombination aus Deadline während blockiertem Cleanup, abgebrochenem Execute-Antwortwait, Shutdown, Ergebnis-Recovery und Capturefehler nach Teilwirkung ist in den erneut bestandenen Regressionen enthalten. Ein normal beendetes letztes Statement darf trotz später Deadline Completed bleiben; das ist der weiterhin beachtete Architekturvertrag.

**Verbleibende Grenze:** Ein notwendiger nie zurückkehrender Callback blockiert absichtlich weiter. Kein Timeout umgeht seine Ownership. Der geprüfte Callback hängt nicht von seinem eigenen Worker-/Disposalabschluss ab; ein solcher zyklischer Hostcallback wäre kein zulässiger Weg, einen Drain zu erzwingen. Keine verlorenen Completion-Signale oder schädlichen mehrfachen Disposals wurden in den kontrollierten Rennen nachgewiesen.

### R4 – VERIFIED FIXED: tatsächlicher Runner-Eintritt und Version

**Quellen:** `CryptoScript/Model/CryptoScriptRunner.cs:15–17`, `:85–90`, `:163–166`; `WorkbenchExecutionPipeline.cs:43–46`, `:73`, `:101–103`, `:144`, `:163`; `WorkbenchAutomationRegistry.cs:440`, `:469`. Regressionen: `WorkbenchRegistryCorrectionTests.cs:263–305` sowie die bisherigen Versions-/Teilwirkungsfälle.

Die neue Core-Leseeigenschaft projiziert ausschließlich die bereits vorhandene ID aus aktivem oder letztem Diagnosekontext. Der Core-Diff enthält nur diese Eigenschaft und ihre Dokumentation; Evaluierung, Mapping, Gates, Cancellation-Prüfungen und Sprachsemantik sind unverändert. Die Pipeline erfasst die vorherige ExecutionId und zählt nur deren tatsächlichen Wechsel als Ausführungseintritt. Eine allein erzeugte Runnerinstanz genügt nicht.

Die ursprüngliche unabhängige Vorstart-Cancelprobe liefert jetzt Cancelled, Effects.None und Version 0; die Variable fehlt. Die vier neuen Explicit-/Deadline-Fälle pausieren vor der Pipeline bzw. nach Runnererzeugung und bestätigen dasselbe. Die zwei Buchfälle behalten vor der ersten Ausführung None/Version 0; nach einer ausgeführten Zeile behalten sie Possible und genau eine Versionsgrenze. Die bisherigen Kontrollfälle für bereits gestartete synchrone Operationen und späte Completion bleiben bestanden.

Eine zusätzliche Probe verwendete ausdrücklich einen bereits benutzten Runner. Root ließ zuerst einen leeren Runner tatsächlich ausführen: Possible, Version 1 und eine ExecutionId. Beim zweiten Aufruf wurde nach Runnererzeugung, aber vor Eintritt abgebrochen: dieselbe alte ExecutionId, Cancelled, None, weiterhin Version 1, keine neue Variable. Der unabhängige Reviewer prüfte den alten Runner zusätzlich in einem separaten Servicezustand: None/Version 0. Damit wird kein früherer Lauf versehentlich als aktueller Beginn gezählt. Ein frischer leerer, tatsächlich gestarteter Runner ergibt dagegen korrekt Completed/Possible/Version 1.

**Verbleibende Grenze:** Der Marker bezeichnet den erreichten Runner-Eintritt, nicht eine nachgewiesene Variablenänderung. Possible nach diesem Eintritt ist weiterhin bewusst konservativ. Produktionspfade erzeugen unverändert frische Runner; die Wiederverwendung wurde ausschließlich als zusätzliche Negativprobe über den vorhandenen Testzugang benutzt.

### Testbewertung und neu ausgeführte Nachweise

Alle 30 Korrekturfälle in 16 Methoden wurden vom unabhängigen Reviewer gelesen. Die GC-Hilfen begrenzen lokale Referenzen mit NoInlining und prüfen zusätzlich den tatsächlichen Callbackgraphen. Die Rollbacktests treffen die drei vorgesehenen Fehlergrenzen und verlangen erfolgreiche Folgearbeit. Die Annahmebarrier startet zwei Anforderungen wirklich konkurrierend. Callback-/Cleanup-Grenzen werden durch Signale und eine kontrollierte Uhr angehalten; die Assertions prüfen nicht nur Busy, sondern auch FinishedAt, IsFinal, Drain, TTL und den ersten Grund. Die R4-Assertions prüfen Effects, stateVersion und tatsächliche Variablenabwesenheit unabhängig von einer bloßen RunnerId. Retention/Tombstones/Disposal und die Fehlerkombinationen sind geprüft. Die 35 bisherigen Registrytests wurden nicht geändert oder abgeschwächt.

Neue Ausführung im finalen Re-Review:

| Prüfung | Ergebnis |
| --- | --- |
| 30 Korrekturtests + 35 bisherige Registrytests, Release | **65 bestanden**, 0 fehlgeschlagen/übersprungen |
| Ursprüngliche Speicherprobe, unverändert | 16-MiB-Payload und echter Textrequest bereits während Retention sammelbar |
| Ursprüngliche unabhängige Timerfehlerprobe | Kein Phantomauftrag, Busy=false; Fail-once-Ergänzung mit erfolgreichem Retry |
| Ursprüngliche unabhängige blockierte Callbackprobe | Nichtterminal bis Callbackfreigabe |
| Ursprüngliche unabhängige Vorstart-Cancelprobe | None/Version 0, keine Variable |
| Zusätzlich bereits wartender Rollback-Usercallback | Kein Deadlock, Drain und Folgeauftrag erfolgreich |
| Zusätzlich Dispose mit aktivem Callback | Wartet; nach Freigabe ResultExpired und freie Admission |
| Zusätzlich vorheriger Runnerlauf und leerer Runner | Alte ID nicht als neuer Beginn gezählt; tatsächlich leerer Lauf zählt |
| Quell-/Test-/Validierungsdateihashes und `git diff --check` | unverändert bzw. erfolgreich |

Die vollständigen Core-/GUI-/MCP-Suiten wurden gemäß Auftrag ohne neue Implementierungsänderung in diesem Re-Review nicht erneut insgesamt ausgeführt. Deren Korrekturlaufnachweise bleiben im Validierungsbericht; sie werden hier nicht als neue unabhängige Gesamtläufe ausgegeben. Frische Reviewlogs/Probequellen liegen außerhalb des Zielrepositories unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp013-final-review/` (`results/final-review-registry65.trx`, `original-memory-probe.log`, `additional-probes.log`, `Program.cs`) und `review-independent-rereview/` (`supplemental.cs.txt`, aktuelle `Program.cs`). Die ursprünglichen Proben liegen weiterhin unter `review-independent/` und `mcp013-review-main/`.

Zusätzlich gesichert wurden `review-independent-rereview/rollback-active-callback.cs.txt` und `retention-graph.cs.txt`. Die Ausgaben dieser unabhängigen Zusatzproben sind in den Toolergebnissen des Chats festgehalten; separate Logdateien wurden dafür nicht behauptet. Ein erster Probeaufruf scheiterte innerhalb der eingeschränkten Buildumgebung ohne konkrete Builddiagnostik; der unveränderte Befehl mit autorisiertem Zugriff auf das Zielprojekt lief erfolgreich. Dies war kein reproduzierter Produkt-/Testfehler.

### Headless, Architektur und Abschluss

Die früheren sporadischen NullReferenceExceptions in `HeadlessUnitTestSession.Dispose` bleiben ursächlich ungeklärt. Keine neue Kausalbehauptung wird aus den bestandenen Proben abgeleitet. R1/R3 beseitigen die konkret nachgewiesene zusätzliche Referenzhaltung und vorzeitige Terminalpublikation; für die geprüften Pfade wurde kein neuer Worker-/Callback-Leak nachgewiesen. Der Master-Backport wurde nicht übernommen. Ein bestehender ungeklärter Headless-Race allein wird entsprechend dem Auftrag nicht als neuer Blocker gewertet.

Eine gemeinsame Hostpipeline, ein globaler Interpreterzustand, exklusive Admission, frische Runner und die MCP-01.2-Admission-/Diagnose-/Passwortquarantäne bleiben erhalten. Die ExecutionId-Eigenschaft ist minimal beobachtend. Es gibt keine zusätzliche Projektabhängigkeit dieses Korrekturschritts, keine Änderung an MCP-01.1-JSON/Transport, keine unbestätigte MCP-Ausführung, keine zusätzlichen Tools/HTTP-Aktivierung, Editorrevisionen oder Save-Änderungen und keine neue Sprachoperation. Die bekannte Save-Lücke bleibt MCP-01.4; native Closing-/Plattformintegration und die bekannte Print-/Info-Multicast-Grenze bleiben abgegrenzte Folgepunkte.

**READY FOR COMMIT.** Alle vier Findings sind durch tatsächliche Quellcodeprüfung und Gegenproben verifiziert behoben. Es bestehen keine neuen blockierenden Findings. Dieser Review führt selbst keinen Commit aus und endet mit der Ergänzung dieses Abschnitts.
