# MCP-01.2 – Gemeinsame Host-Ausführungspipeline: Validierung

Stand: 22.10.2026. Implementierung und lokale Windows-Validierung abgeschlossen. Kein unabhängiger Review durchgeführt; kein Commit, Push, Merge, Rebase oder Reset.

## Ausgangszustand und Basis

- Tatsächlicher Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
- Branch: `feature/mcp-server`; HEAD vor und nach der Arbeit: `55297e2effabcb2310f71dadb8cda63fb2616420` (MCP-01.1).
- Dieser Worktree war vor Änderungen sauber. Der separate Chat-Arbeitsordner unter `Documents/ChatGPT/CryptoWorkBench-Mcp` enthält nur `review-probes` und besitzt keinen gültigen HEAD; seine Projektverweise identifizieren den tatsächlichen Worktree. Keine Verschiebung oder Neuanlage eines Projekt-Worktrees.
- Verbindliche Grundlage: MCP-00-R1, `MCP-00-architecture.md`, `MCP-00-implementation-plan.md` und MCP-01.1.
- Keine anwendbaren `AGENTS.md` im tatsächlichen Repository oder seinen übergeordneten Verzeichnissen gefunden. `.codex/config.toml` wurde weder gelesen noch verändert.

Gemeinsame Basis mit `master`: `027815e9ea6f2dab11bd03485500b819907f2862`. Zwei zusätzliche master-Commits wurden lesend identifiziert und nicht übernommen:

| Commit | Änderung | Auswirkung bei späterer Integration |
| --- | --- | --- |
| `4b785324c9796dc0f3bb7605904343941970b767` | Datei-Verschlüsselung über Encrypt, neue FileEncryption-Komponenten und Tests | Berührt u. a. Runner, Funktionsauswertung, Diagnosekontexte, Snapshot-Veröffentlichung und Core-Projektdatei. Spätere Integration muss die gemeinsame Pipeline und neue Dateieffekte erneut testen. Kein allgemeiner Datei-Verschlüsselungsvertrag wird durch MCP-01.2 hinzugefügt. |
| `69434392f06e3f247395fe111e98a9eb974a148d` | macOS-ARM64-ABI für native Dateiveröffentlichung, Targets und CI | Keine unmittelbare Abhängigkeit der Host-Orchestrierung. Die dortigen nativen Build-/Publishtests sind nach einer späteren Integration gesondert erforderlich; sie sind kein Bestandteil dieses Branch-Nachweises. |

## Geänderte Dateien und Verantwortlichkeiten

Alle Pfade relativ zum tatsächlichen Worktree.

| Datei | Änderung |
| --- | --- |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs` (neu) | Gemeinsamer Host-Koordinator, exklusive Operationen, submission-Einstieg, sequenzielle Buchschleife, zugelassene kurze Variablenmutationen und Clear. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs` (neu) | Ein Parse-/Runner-Kern für alle GUI-Ausführungen, Parserreset, Diagnosekopien, geordneter Output-Collector, Passwort-Finalisierung und Zustandsaufnahme auch nach Fehlern. Enthält ausschließlich interne Ergebnis-/Ausgabe-/Syntaxdiagnose-Typen. |
| `CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs` (neu) | Aus dem Variablen-VM extrahierte Aufnahme detached GUI-Modelle unter dem Interpreter-Gate. Keine Abhängigkeit von ViewModels. |
| `CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs` | Nicht wartende prozessweite Reservation, vollständiger asynchroner Host-Scope und kurze synchrone Zustandsoperationen. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | GUI-Präsentation und History delegieren Interpreterarbeit an den Service. Synchrone ParseLine-/ExecuteLine-/OpenScriptBook-/Buch-Ausführungswege entfernt. |
| `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs` | Mutationen und SetupVariables verwenden dieselbe Admission; GUI-Collections werden ausschließlich aus detached Modellen aufgebaut. |
| `CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs` | Auch Initialisierung und diagnostischer Testzugang verwenden die gemeinsame asynchrone Pipeline. Synchroner Runner-Nebenweg entfernt. |
| `CryptoWorkBenchAvalonia/App.axaml.cs` | Registrierung genau der gemeinsamen Serviceinstanz im Prism-Container. |
| `CryptoScript/Model/InterpreterExecution.cs` | Kleiner interner `HostOutputCallback`-Helfer schützt asynchrone Ausgabe-Callbacks unter Erhalt synchroner Core-Reentrancy. |
| `CryptoScript/Operations/OutputOperations.cs` | Die zwei Event-Invokationen verwenden diesen Schutz. |
| `CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs` | Vorhandene Fälle auf asynchrone Ausführung im Headless-Dispatcher migriert; bewusst geänderte Teilzustandsanzeige separat angepasst. |
| `CryptoWorkBenchGuiTest/AsyncPasswordGuiTests.cs` | Handler an die Host-Pipeline statt an wiederverwendete Testrunner übergeben; bestehende echte Dialog-Tests erhalten. |
| `CryptoWorkBenchGuiTest/WorkbenchAutomationTests.cs` (neu) | 15 zusätzliche Pipeline-/Admission-/Callback-Testfälle. |
| `docs/mcp/MCP-01.2-validation.md` (neu) | Dieser Bericht; Notwendigkeit und Vertragsauswirkung der Core-Anpassung wurden vor ihrer Umsetzung hier dokumentiert und nach vollständiger Regression präzisiert. |

Keine Projektdateien, Solution-Dateien, Paketpins oder Lockfiles geändert. Kein produktiver MCP-Ausführungszugriff hinzugefügt.

## Produktive Aufrufketten nach Migration

1. **Return:** View-Eingabehandler → `ParseLineAsync` → `ExecuteLineAsync` → `WorkbenchAutomationService.RunAsync` → `InterpreterHostQueue.RunAsync` → `Task.Run`/`InterpreterExecution.RunAsync` → `WorkbenchExecutionPipeline.ExecuteAsync` → Parser unter `AccessState` → frischer Runner/`ExecuteAsync` → finale Zustandsaufnahme → Dispatcher-Barriere/Diagnose-/Variablenpräsentation. Die View fügt anschließend `PrintMessage` weiterhin einmal über ihren TextAnchor ein. `PendingExecution` bleibt beobachtet.
2. **ScriptBook-Import:** Sidebar-Dateiauswahl und gegebenenfalls Clear-Bestätigung → `OpenScriptBookAsync` → genau eine Service-/Queue-Reservation einschließlich Dateilesen, optionalem Dictionary-Clear und Editor-Clear → zentrale `ExecuteScriptBookAsync`-Schleife → derselbe Parse-/Runner-Kern je physischer nicht übersprungener Zeile → UI-Präsentation und Anhängen jeder bearbeiteten Zeile. Keine innere Queue-Reservation. Die Sidebar setzt ihren Erfolgsstatus weiterhin nur bei `LastScriptBookSucceeded`.
3. **Variablenwert/-parameter:** bestehende DelegateCommands → `WorkbenchAutomationService.Mutate` → dieselbe nicht wartende Reservation und synchroner kurzer `InterpreterExecution.Enter`-Scope → Mutation und detached Capture → GUI-Collection aktualisieren. Das ist kein synchroner Runner- oder Parsepfad und wartet weder auf Task noch Dispatcher. Auch ein separat erzeugtes Variablen-VM kann die globale Reservation nicht umgehen.
4. **KeyMap:** Konstruktor/`Initialization` → `LoadInfoAsync` → `ExecuteSubmissionAsync("Info(keymap)")` → derselbe Service und Parse-/Runner-Kern. Busy wird ohne Queue-Warten oder automatische Wiederholung als Initialisierungsfehler angezeigt.
5. **Passwort:** Runner → vorhandener Core-`HostCallback` → für die Einheit registrierter Host-Handler → `PasswordDialogHost.RequestAsync` → UI-Dispatcher/Owner-Dialog → Antwort oder Abbruch → Runner-Unwind und Host-Drain → Subscription entfernen.

Der Service besitzt keine ViewModels, keinen Editor und keinen RegionManager. Prism registriert `WorkbenchAutomationService.Shared`; Editor-DI und bisherige manuelle GUI-Konstruktionen verwenden dieselbe Instanz, ebenso KeyMap und Variablen-VM. Es entsteht kein DI-Zyklus. Die synchronen bisherigen Ausführungs-APIs wurden entfernt; Tests blockieren den UI-Dispatcher nicht durch synchrones Warten auf ihre Ersatzmethoden.

## Runner- und Admission-Lebensdauer

- Die Reservation wird vor dem Start des Workerjobs mit einem nicht wartenden Versuch erworben. Eine bereits reservierte Operation, ein reentranter Hostaufruf oder ein konkurrierender Core-Owner ergeben Busy. Kein Auftrag wird hinten angestellt.
- Ein Buch hält eine Reservation und einen Core-Hostscope über sämtliche Zeilen, UI-Übergänge, optionales Clear und Finalisierung. Zwischen zwei Runnern ist die Workbench weiterhin exklusiv belegt.
- Parsing, Dictionary-Mutation und Capture erfolgen nur unter den bestehenden Core-Gates. Kein Gate wird über synchrones UI-Warten betrieben. Asynchrone Passwort-/Dispatcher-Waits bleiben responsiv.
- `submission` parst den gesamten Text als eine Einheit mit mehreren Statements. Parser-/Lexerfehler verhindern diese Einheit vollständig.
- `scriptBook` verarbeitet die übergebenen physischen Zeilen in Reihenfolge, einschließlich Leerzeilen. Ausschließlich `StartsWith("out:", StringComparison.Ordinal)` überspringt die Ausführung. Führende Leerzeichen und `OUT:` erfüllen diese Regel nicht. Fehlerzeile wird angehängt, danach stoppt das Buch; frühere Effekte bleiben erhalten. LF/CRLF-Dateien verwenden weiterhin `ReadAllLinesAsync`.
- Je syntaktisch ausführbarer Einheit entsteht ein neuer Runner. Leerzeilen sind gültige leere Programme; übersprungene out:-Zeilen und syntaktisch fehlerhafte Einheiten erzeugen keinen produktiven Runner. Kein Singleton-Runner, Pool oder weiteres Variablendictionary. Unterschiedliche RunnerIds sehen denselben prozessweiten Bestand.
- Pro Runner ist produktiv genau ein Passwort-Handler angemeldet. Interne kontrollierte Runner-/Statement-/Handler-Parameter bleiben ausschließlich Testzugänge.
- Die Pipeline verfolgt die vom Passwortadapter gelieferten Tasks bis zu deren Abschluss. Nach Runner-Ende werden auch verspätete Antworten defensiv und idempotent entsorgt; späte Taskfehler werden ohne Rohtext beobachtet. Erst danach wird der Handler entfernt. Admission und Core-Hostscope werden bis zu diesem Drain gehalten. Ein neuer Runner kann die runnerlokale Passwortsperre daher nicht umgehen.
- Ein nie beendeter Callback hält die Workbench absichtlich Busy. Es gibt keine zeitgesteuerte Freigabe oder Wiederholung. Ein vollständiges Auftragsregister und veröffentlichte cancellationRequested-/Stopping-Zustände werden hier nicht vorgezogen.
- Vor jeder Parse-Einheit werden Syntaxliste, Syntaxflag/-puffer und Lexerflag zurückgesetzt. Syntaxdaten werden in interne Records kopiert. Semantische Fehler werden mit allen öffentlichen Feldern und kopierter Location übernommen. Die identische Exception-Diagnose wird auf dieselbe Kopie abgebildet, sodass ihre zusätzliche Transportposition keine Duplikate erzeugt.
- Print/Info werden am Ursprung synchron und geordnet gesammelt. Der Collector rendert sie nicht nochmals in der GUI; deren bestehende Events und die Dispatcher-Barriere bleiben für die Darstellung zuständig. Interne Payloads sind keine externen MCP-Snapshots.

## Erhaltene GUI-Semantik und bewusste Anpassungen

**Erhalten:** Mehrfachstatements, physische Buchzeilen, exakte out:-Regel, leere Programme, Fehler-/Stop-Verhalten, kein Rollback, keine automatische Wiederholung, History je tatsächlich bearbeiteter Scriptzeile, Info-Navigation, Print-Format und Reihenfolge, einmalige TextAnchor-Einfügung, SecureSave/SecureLoad mit responsiven Owner-Dialogen sowie Erfolg trotz fortsetzbarer semantischer Error-Diagnosen. Parser und Sprachoperationen wurden nicht geändert. Die bestehende synchrone gleichthreadige Reentrancy direkter Core-Aufrufe bleibt erhalten.

**Bewusst geänderte Teilzustandsanzeige:** Nach geworfenem Laufzeitfehler oder Abbruch werden die tatsächlich verbliebenen Variablen ebenfalls aufgenommen und angezeigt. Der bisherige Test ohne Variablenrefresh nach Teilfehler prüft jetzt ausdrücklich `before` sichtbar, `after` nicht vorhanden. Fehlerstatus und Diagnosen bleiben erhalten. Ein Fehler der GUI-Collection-Aktualisierung meldet einen Präsentationsfehler, dupliziert Diagnosen nicht und rollt den Interpreterzustand nicht zurück.

**Zusätzliche Durchsetzung der geforderten Syntaxgrenze:** Der bisherige GUI-Code prüfte ausschließlich das Syntaxflag; gewöhnliche Lexerfehler wie ein angehängtes `@` konnten trotz Eintrag in der globalen Fehlerliste durchlaufen. Die gemeinsame Pipeline prüft nun auch das vor jeder Einheit zurückgesetzte Lexerflag und kopiert diese Diagnosen. Diese gezielte Korrektur verhindert die Ausführung einer fehlerhaften Parse-Einheit; der CryptoScript-Parser selbst bleibt unverändert.

### Notwendige minimale Core-Anpassung und Reentrancy

Die Host-Pipeline kann nicht alle statischen PrintEvent-/InfoEvent-Abonnenten isolieren: Ihre Invokation findet im für Statements erlaubten Zustandszugriff statt. Ein synchroner Ausgabe-Callback konnte dadurch direkt das Dictionary verändern. Dies lässt sich allein durch einen zusätzlichen Host-Abonnenten nicht verhindern.

Die beiden Event-Invokationen verwenden deshalb einen kleinen internen `InterpreterExecution.HostOutputCallback`-Scope. Im asynchronen Interpreter-Hostscope verwendet er den vorhandenen `HostCallback`-Schutz; ohne solchen Ambient-Scope ist er wirkungslos. Damit werden unzulässige Parser-/Dictionary-/Host-Zugriffe aus GUI-Ausgabe-Callbacks abgewiesen, während die fünf bestehenden Core-Tests für synchrone Print-Reentrancy und die Wiederherstellung ihrer Diagnosekontexte unverändert bestehen. Passwort-Callback-Regeln, öffentliche Core-APIs, Ausgabetext, Ereignisreihenfolge und Sprachsemantik bleiben erhalten.

Diese Notwendigkeit wurde vor der Core-Änderung dokumentiert. Der zunächst allgemeine Callback-Schutz wurde nach der vollständigen Core-Regression auf asynchrone Scopes präzisiert; es wurden keine Core-Regressionstests abgeschwächt oder entfernt.

## Validierung

Umgebung: Windows, .NET SDK `10.0.401`, Release/net10.0. Die vorhandenen vollständigen Suiten wurden ausgeführt, nicht nur gefilterte Teilmengen.

| Prüfung | Ergebnis |
| --- | --- |
| `dotnet restore CryptoWorkBench.sln` | Erfolgreich |
| `dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | Erfolgreich |
| Release-Build `CryptoWorkBench.sln --no-restore` | Erfolgreich, 0 Fehler |
| Release-Build `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore` | Erfolgreich, 0 Fehler; 8 bestehende Nullable-Warnungen in MainViewModel, SidebarViewModel und CryptoScriptEditView |
| Vollständige Core-Suite | 7 667 bestanden, 4 übersprungen, 0 fehlgeschlagen; 7 671 im Lauf |
| Vollständige GUI-Suite einschließlich neuer Integrationstests | 254 bestanden, 0 übersprungen, 0 fehlgeschlagen |
| Vollständige unveränderte MCP-01.1-Suite | 89 bestanden, 0 übersprungen, 0 fehlgeschlagen |
| Neue MCP-01.2-Testfälle | 15 bestanden, Bestandteil der 254 GUI-Tests; bestehende GUI-Fallzahl 239 vollständig erhalten |
| `git diff --check` | Erfolgreich |
| Projekt-/Paket-/Solution-Abhängigkeiten | Geprüft, unverändert |

Die vier übersprungenen Core-Fälle sind bestehende plattform-/berechtigungsabhängige ACL-/Symlink-Prüfungen. Der explizite Hardware-Messfall wird von einem normalen Lauf ebenfalls nicht aktiviert. Der bestehende explizite MCP-Fall `InstalledCodexDiscoversAndCallsStatus` wurde im normalen Lauf ebenfalls nicht aktiviert; die 89 automatischen MCP-Fälle sind vollständig bestanden. Keine neuen Ausschlüsse eingeführt. Beim Core-Neubau bleiben außerdem sechs bestehende Nullable-Warnungen in VariableDictionary, BlockHeaderVariableDeclaration und Tr31Header sichtbar.

Lokale TRX-Nachweise liegen in den ignorierten `TestResults`-Ordnern der jeweiligen Projekte: `mcp012-core.trx`, `mcp012-gui.trx`, `mcp012-mcp.trx`. Keine daraus abgeleitete Behauptung nativer Desktop-Smoke-Tests oder anderer Betriebssysteme.

### Abdeckung der geforderten Fälle

| Anforderung | Nachweis |
| --- | --- |
| 1, 7: Mehrfachstatements, neue RunnerIds, gemeinsamer Bestand | `SubmissionRunsMultipleStatementsAndFreshRunnersShareState` |
| 2, 3: physische Buchzeilen, Leerzeilen, exaktes out: | `BookUsesPhysicalLinesAndExactOutputPrefix` (3 Fälle); bestehende LF-/CRLF-Import-/Return-Tests |
| 4: Syntax/Lexer, Parserreset, Diagnosekopie | `SyntaxFailurePreventsWholeSubmissionAndNextParseResetsAllErrors`, `LexerOnlyFailureStopsSubmissionAndResetsForNextUnit`; bestehende redigierte Syntaxdiagnosen |
| 5, 6: fortsetzbare Errors, Teilzustand | `ContinuingErrorCompletesAndLaterThrownErrorPublishesPartialState`; migrierter GUI-Teilzustandstest |
| 8: gleichzeitige GUI-Anforderungen | `CompetingGuiRequestsAndDetachedVariableEditAreImmediatelyBusy` mit zwei Editor-VMs |
| 9, 10: Buch gegen Mutation/Clear, Busy zwischen Runnern | `BookKeepsAdmissionBetweenRunnersAndClearCannotInterleave`; begrenzte konkurrierende Execute-Waits, keine zweite Reservation |
| 11: echte Owner-Dialoge, SecureSave/Load | unveränderte Szenarien in `AsyncPasswordGuiTests`, einschließlich Erfolg, falschem Passwort, UserCancel, Owner-/Tokenabbruch und Dispatcher-Responsivität |
| 12: verspätete Antwort/Fault und sichere Freigabe | `CancelledPasswordKeepsAdmissionUntilLateResponseOrFaultIsDrained` (2 Fälle), einschließlich Secret-Entsorgung und erfolgreicher Folgeausführung |
| 13: Print/Info-Reihenfolge, keine Duplikate | `OutputOriginOrderAndDiagnosticCopiesSurviveRunnerDisposal`; bestehende GUI-Reihenfolge- und tatsächliche Return/TextAnchor/Save/Reload-Tests |
| 14: Reentrancy, Dictionary, geerbter Kindtask | `PasswordCallbackAndInheritedChildCannotAccessDictionaryOrReenterHost`, `OutputCallbacksCannotMutateParseOrReenterHost` (Print und Info), vollständige unveränderte Core-Reentrancy-Suite |

### Abhängigkeitskontrolle

Alle neun Projektdateien wurden auf Projektverweise geprüft. Unverändert: Abstractions hat keine Projektverweise; Mcp referenziert ausschließlich Abstractions und das bestehende ASP.NET-/SDK-Paket; Core referenziert keines der Automation-/MCP-/GUI-Projekte. Avalonia referenziert weiterhin nur CryptoScript. Die Pipeline liegt wie vorgegeben im Avalonia-Projekt und verwendet keine MCP-Transporttypen. Desktop→Avalonia, Console→Core und die vorhandenen Testprojektverweise bleiben unverändert. Es gibt keinen neuen Framework-, Paket- oder Runtime-Bedarf und keinen zusätzlichen Projektzyklus.

## Bekannte Risiken, verbleibende Nebenwege und Folgeumfang

- **GUI-Save bleibt MCP-01.4:** Sidebar `SaveScriptBookCommand → GuardAsync → SaveScriptBook` prüft Busy weiterhin nur beim Einstieg. Zwischen Ordnerzugriff, Picker, eigener Überschreibbestätigung und `DeleteAsync` kann der Inhalt oder Ausführungszustand wechseln. Der öffentliche `CryptoScriptEditViewModel.SaveScriptBook` liest weiterhin das dann aktuelle Dokument und schreibt ASCII/LF, Whitespace-gefiltert, im Append-Modus. Weder die frühe Sidebar-Prüfung noch diese direkte Methode sind ein revisionsgebundener Save-Commit. Die vorhandene Delete-vor-Write-Semantik und externe Dateirennen bleiben ausdrücklich bestehen. Der Service stellt eine ganze asynchrone Operation umfassende Reservation bereit; Editorrevisionen, Snapshotbindung und Commit-Sperre wurden nicht vorgezogen.
- **Passwort-Quarantäne als sichere Basis vorgezogen:** Task-Drain und defensive Antwortentsorgung sind bereits für MCP-01.2 erforderlich, weil ein frischer Runner andernfalls eine späte Antwort umgehen könnte. Das vollständige serviceweite Auftrags-/Cancellation-/Lifecycle-Modell bleibt MCP-01.3. Ein nicht kooperativer Host kann weiterhin dauerhaft Busy halten.
- **MCP-01.3:** Host-executionId über mehrere Runner, öffentliche unveränderliche/redigierte Snapshots, letzter abgeschlossener Status und stale-Anzeige, Ergebnisregister, Retention, Idempotenz, Request-Wait versus Execution-Cancellation, Deadline, korrelierte Aufträge sowie koordinierter Shutdown/Drain sind offen. Die internen GUI-Payloads enthalten lokale Diagnose-/Variableninformationen und dürfen nicht als externer Transportvertrag veröffentlicht werden.
- **Bestehender Core-Vertrag:** Direkte synchrone Runner-/Dictionary-Verwendung in Core/Console und deren Tests bleibt zulässig; die GUI besitzt keinen solchen produktiven Interpreter-Nebenweg mehr. Es gibt weiterhin genau einen globalen Zustand pro Prozess, keine isolierten Workspaces oder Transaktionsgarantie.
- **Weitere Grenzen:** Kein neuer MCP-Toolzugriff, HTTP-Hoststart, Pairing/Auth, Editorrevisionen, persistenter Auftrag oder neue Sprachfunktion. Keine automatische Übernahme der master-Dateiverschlüsselung. Native Desktop-Smokes, weitere Plattformen und unabhängiger Review wurden hier nicht ausgeführt.

Die Arbeit stoppt nach MCP-01.2. Branch und HEAD bleiben unverändert; alle Änderungen sind uncommitted für den separat vorgesehenen Review.


## Nachtrag: Korrektur und erneute Validierung von R1/R2 – 22.10.2026

Die bisherigen Abschnitte dokumentieren den ursprünglichen Implementierungsstand. Dieser Nachtrag enthält den aktuellen Korrektur- und Validierungsstand nach dem unabhängigen Review.

Die ursprünglichen Review-Findings, ihre Reproduktionen und die Entscheidung **CHANGES REQUIRED** bleiben unverändert als Historie erhalten. Dieser Nachtrag dokumentiert ausschließlich die angeforderten Korrekturen von R1 und R2 und deren Implementierungsnachweise. Er ersetzt keinen unabhängigen erneuten Review. Aktueller Korrekturstatus: **READY FOR RE-REVIEW**.

Vor Änderungen wurden der tatsächliche Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `55297e2effabcb2310f71dadb8cda63fb2616420` und der dort vorhandene Reviewbericht geprüft. Der vorhandene uncommitted Implementierungsstand wurde weiterbearbeitet. Kein Commit, Push, Merge, Rebase oder Reset; `.codex/config.toml` wurde weder gelesen noch verändert.

### R1 – Reservation bis nach der vollständigen Präsentation

`InterpreterHostQueue.RunState` hält seine Reservation jetzt über die Zustandsmutation, detached Capture und einen optionalen internen Präsentationscallback. Der kurze synchrone Core-State-Scope endet vor der GUI-Präsentation; die Admission endet erst nach deren Rückkehr. Der Callback läuft direkt auf dem aufrufenden GUI-Thread. Es gibt weder synchrones Dispatcher-Warten noch einen neuen Runner-/Parsepfad.

`WorkbenchAutomationService.Mutate` reicht den Callback ohne ViewModel-Abhängigkeit weiter. `VariableViewModel` übergibt `ApplyVariables` sowohl bei echten Wert-/Parametermutationsbefehlen als auch bei `SetupVariables`. Ein reentranter Befehl eines zweiten VM wird daher schon an der weiterhin belegten Reservation abgewiesen. Die bestehenden Commands behandeln Busy weiterhin ohne Exception nach außen; direkte Service-/Setup-Anforderungen erhalten `ExecutionBusyException`. Werfende Collection-Abonnenten behalten ihren ursprünglichen Fehlertransport; using/finally gibt die Reservation zuverlässig frei. Bereits erfolgte Mutationen werden nicht zurückgerollt.

### R2 – Script-Outcome und Capture-Abschluss getrennt

Die gemeinsame Pipeline führt die Variablenaufnahme in `FinalizePresentation` als eigenen fehlbaren Finalisierungsschritt aus, einmal je Einheit und weiterhin unter dem Core-Gate. Das gilt auch für den Syntax-/Lexerfehlerzweig. Bei Capturefehler werden die bereits verfügbaren Syntaxdiagnosen, semantischen Diagnosekopien, die referenzkorrekt zugeordnete Exception-Diagnose, Ausgaben und der ursprüngliche Script-Status zurückgegeben.

Der kleine interne Ergebnistyp unterscheidet nun:

- `Succeeded` und `Status`: tatsächlicher Script-Outcome und dessen ursprüngliche Statusmeldung, unverändert durch Capturefehler.
- `CaptureFailed`: separater fehlgeschlagener Aufnahme-/Finalisierungsschritt; es wird keine rohe Capture-Exception im Payload gespeichert.
- `HostSucceeded`: nur Script-Erfolg **und** erfolgreiches Capture ergeben einen erfolgreichen Hostabschluss.
- `EffectsPossible`: konservative Kennzeichnung möglicher Scriptwirkungen bei einer Runner-Einheit. Bei Syntax-/Lexerfehler ohne Runner ist sie false; true behauptet keine sicher erfolgte Mutation.

Bei fehlgeschlagenem Capture ist die Variablenliste lediglich ein leeres internes Platzhalterfeld zusammen mit `CaptureFailed=true`, ausdrücklich kein gültiger leerer Zustand. Die GUI wendet diese Liste nicht an. Sie behält ihre bisherige Collection, setzt `VariableViewModel.IsSnapshotStale` und zeigt den ursprünglichen Script-Status zusammen mit `Variable state capture failed; displayed variables are stale.` an. Nach einer späteren erfolgreichen Aufnahme/Präsentation wird die lokale Stale-Kennzeichnung gelöscht. Eine werfende GUI-Collection-Aktualisierung markiert den Bestand ebenfalls als stale und propagiert ihren Fehler wie zuvor.

Die Buchschleife prüft auch den vollständigen Hostabschluss. Ein Capturefehler nach einer erfolgreich ausgeführten Zeile lässt deren Effekte und ihre angehängte Zeile bestehen, stoppt aber die folgende Zeile und meldet keinen vollständigen Bucherfolg. Die KeyMap-Präsentation berücksichtigt denselben Capturefehlerstatus. Es gibt keinen zweiten Capture-Versuch, keine automatische Scriptausführung als Wiederholung und keinen Rollback.

### In diesem Korrekturauftrag geänderte Dateien und relevante Zeilen

| Datei | Relevante Stellen und Änderung |
| --- | --- |
| `CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs:35` | RunState mit Präsentationscallback; Aufruf unter der noch gehaltenen Reservation bei Zeile 42. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:51` | Callbackweitergabe für kurze Mutationen; Buchabschluss mit `HostSucceeded` bei Zeile 46. |
| `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:49` | Mutation/Präsentation gemeinsam reservieren; SetupVariables bei Zeile 59; lokale Stale-Kennzeichnung bei Zeile 62 und deren Pflege in ApplyVariables bei Zeile 68. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs:14` | Interner Ergebniszustand; Syntaxfehler-Finalisierung bei Zeile 57 und Runner-Finalisierung bei Zeile 115; einmaliges Capture mit Fehlerkennzeichnung ab Zeile 119. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:144` | Diagnosen und ursprünglicher Outcome bleiben sichtbar; Capturefehler separat anzeigen, alte Collection erhalten und als stale markieren. |
| `CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs:47` | Capturefehler auch beim gemeinsamen KeyMap-Aufruf sichtbar halten. |
| `CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs` (neu) | 19 gezielte neue Regressionstestfälle. |
| `docs/mcp/MCP-01.2-validation.md` | Ergänzter Korrekturstatus und aktuelle vollständige Validierung. |
| `docs/mcp/MCP-01.2-review.md` | Dieser zusätzliche Korrekturstatus; ursprünglicher Reviewtext unverändert erhalten. |

Zeilenangaben beziehen sich auf den abschließend validierten Korrekturstand. Keine zusätzlichen Änderungen am Core, MCP-01.1-Transport, SDK, JSON-Vertrag, Projektgraphen oder an bestehenden Tests in diesem Korrekturauftrag.

### Neue Regressionen und konkrete Nachweise

Alle Fälle stehen in `WorkbenchReviewRegressionTests` und laufen nicht parallel zu anderem globalem Interpreterzustand.

| Test | Fälle und Nachweis |
| --- | --- |
| `RealSelectedMutationIsBusyUntilOuterMutationOrSetupPresentationCompletes` (Zeile 39) | 2 Fälle: echte äußere Mutation 01→02 bzw. SetupVariables; CollectionChanged versucht tatsächlichen ausgewählten zweiten VM-Befehl auf FF. Busy bleibt aktiv, der zweite Befehl ändert nichts, Core und Haupt-GUI stimmen überein. Nach Freigabe funktioniert derselbe zweite Befehl. Zusätzlicher direkter Service-/Setup-Eintritt erhält Busy. |
| `ThrowingMutationOrSetupPresentationPreservesExceptionAndReleasesAdmission` (Zeile 81) | 2 Fälle: werfender Präsentationscallback bei Mutation und Setup; dieselbe Exception bleibt erhalten, kein Admission-Leak, keine Rücknahme der Wirkung, Folgemutation gelingt. |
| `CaptureFailurePreservesScriptOutcomeDiagnosticsOutputsAndEffects` (Zeile 149) | 5 Fälle: regulärer Erfolg, fortsetzbarer Error, spätere Runtime-Abbruchstelle nach Teilwirkung, Syntaxfehler und Lexerfehler. Outcome/Status, Diagnoseanzahl, Ausgabereihenfolge, Syntaxkopien und mögliche Effekte bleiben erhalten; genau ein Capture-Versuch. Nach deaktivierter Fehlerinjektion gelingt der nächste Lauf. |
| `GuiKeepsOriginalStatusAndDiagnosticsAndMarksUnchangedVariablesStale` (Zeile 184) | Dieselben 5 Fälle im Headless-Editor: Hostabschluss false, ursprünglicher Status plus sichtbarer Capturefehler, keine Collection-Aktualisierung, identisches altes GUI-Objekt, stale=true. Folgelauf aktualisiert und löscht stale. |
| `CaptureFailurePreservesEverySemanticFieldAndExceptionDiagnosticIdentity` (Zeile 219) | Alle öffentlichen SemanticError-Felder und Location werden trotz Capturefehler unabhängig kopiert; Reihenfolge und Ausnahme-Diagnoseidentität bleiben erhalten, spätere Originaländerungen beeinflussen die Kopie nicht. |
| `BookDoesNotClaimCompleteSuccessOrRunNextLineAfterCaptureFailure` (Zeile 261) | Bereits ausgeführte Zeile bleibt wirksam/angehängt; keine nächste Zeile, kein vollständiger Bucherfolg. |
| `EarlierThrowingOutputSubscriberStillAbortsAndDoesNotHideRuntimeFailure` (Zeile 283) | 2 Fälle Print/Info: früherer werfender Abonnent stoppt weiterhin den Lauf; vorherige Wirkung bleibt, spätere fehlt. Auch zusätzlicher Capturefehler ersetzt die ursprüngliche Runtime-Diagnose nicht. Nach Bereinigung funktionieren Ausgabe und Folgeauftrag. |
| `KeyMapReportsCaptureFailureWithoutLosingItsScriptStatus` (Zeile 318) | KeyMap verwendet weiter dieselbe Pipeline und meldet Capturefehler zusätzlich zum Script-Status. |

Das Testsubstitut verwendet einen kontrolliert werfenden virtuellen `Value`-Getter mit Aufrufzähler. Der Fehler wird nach Prüfung deaktiviert; keine produktive Capturefehler-API oder neue CryptoScript-Funktion wurde hinzugefügt. Die zwei zunächst falschen Annahmen zur Zahl der Runtime-Diagnosen in den neuen Tests wurden an die tatsächlich vorhandenen zwei Core-Diagnosen angepasst. Kein bestehender Test und keine bisherige Assertion wurden entfernt, deaktiviert oder abgeschwächt.

### Vollständige Validierung nach den Korrekturen

Windows, .NET SDK 10.0.401, Release/net10.0.

| Prüfung | Ergebnis |
| --- | --- |
| Neue 19 R1-/R2-Fälle plus alle bisherigen 15 Pipeline-Fälle | **34 bestanden**, 0 fehlgeschlagen |
| Restore `CryptoWorkBench.sln` | Erfolgreich |
| Restore `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | Erfolgreich |
| Release-Build beider Solutions | Erfolgreich, jeweils 0 Fehler; abschließende inkrementelle Läufe ohne Warnungen |
| Vollständige Core-Suite | **7 667 bestanden, 4 übersprungen**, 0 fehlgeschlagen; 7 671 im normalen Lauf |
| Vollständige GUI-Suite | **273 bestanden**, 0 übersprungen/fehlgeschlagen; bisherige 254 vollständig erhalten plus 19 neue Fälle |
| Vollständige automatische MCP-01.1-Suite | **89 bestanden**, 0 fehlgeschlagen |
| `git diff --check` | Erfolgreich |
| Architektur-/Projektabhängigkeiten | Alle neun Projektverweise geprüft; keine Änderungen an Projekt-/Solution-Dateien, Paketpins oder Lockfiles |

Die vorhandenen plattform-/berechtigungsabhängigen Core-Ausschlüsse sowie expliziten Hardware-/Installed-Codex-Smokes wurden nicht verändert. Es wurden keine neuen Ausschlüsse eingeführt. Beim vorangegangenen notwendigen Host-Neubau blieben die acht bereits dokumentierten Nullable-Warnungen sichtbar; sie sind nicht Gegenstand von R1/R2. Keine neue Aussage über native Desktop-Smokes oder andere Betriebssysteme.

Die aktuellen lokalen, ignorierten Testnachweise heißen `mcp012-fix-targeted.trx`, `mcp012-fix-core.trx`, `mcp012-fix-gui.trx` und `mcp012-fix-mcp.trx` in den jeweiligen Projekt-TestResults-Ordnern.

### Verbleibende Risiken und Umfangsgrenzen

- Die dokumentierte Multicast-Lücke bleibt bestehen: Wirft ein Print-/Info-Abonnent vor dem Collector, endet die weitere Eventzustellung und die interne Ausgabeliste kann leer sein. Der Scriptfehler und seine Diagnose bleiben sichtbar; kein falscher Erfolg, kein Weiterlaufen späterer Statements. Dies ist nun für beide Events einschließlich gleichzeitigem Capturefehler regressionsgeprüft. Keine allgemeine Event-Dispatcher-Änderung umgesetzt.
- Ein nie abgeschlossener Passwortcallback hält wie bisher die Admission bis zu sicherem Unwind/Drain Busy. R1/R2 ändern daran nichts.
- Capturefehler erlauben weiterhin keinen verlässlichen neuen Variablensnapshot. Die GUI zeigt deshalb ausdrücklich einen alten Bestand als stale. Es gibt keine Wiederholung, keinen Rollback und keine Behauptung eines frisch leeren Zustands.
- `EffectsPossible`, `CaptureFailed` und die lokale Stale-Kennzeichnung sind interne Host-/GUI-Informationen. Kein externer MCP-Snapshot-, Retention-, Idempotenz- oder vollständiger Lifecycle-/Cancellation-Vertrag wurde vorgezogen.
- Die bekannte Save-Lücke bleibt MCP-01.4 vorbehalten. Keine Editorrevision, Save-Commit-Sperre, HTTP-Aktivierung, Auth/Pairing oder weitere Sprachfunktion implementiert.
- Core-Reentrancy, asynchrone Core-Gates, frische Runner je Einheit, gemeinsame Variablen, Buchreservation und Passwort-Quarantäne bleiben erhalten. Die zwei zusätzlichen master-Commits wurden weder übernommen noch integriert.

**Abschluss dieses Korrekturauftrags: READY FOR RE-REVIEW.** R1 und R2 sind implementierungsseitig korrigiert und durch gezielte sowie vollständige Regressionen nachgewiesen. Die historische Reviewentscheidung bleibt CHANGES REQUIRED, bis ein unabhängiger erneuter Review die Korrekturen bewertet. Alle Änderungen bleiben uncommitted auf unverändertem Branch/HEAD; nach Dokumentation und Validierung wird gestoppt.
