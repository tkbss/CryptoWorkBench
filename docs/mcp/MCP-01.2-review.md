# MCP-01.2 – Independent Code Review

Datum: 22.10.2026. Entscheidung: **CHANGES REQUIRED**.

Zwei korrekturbedürftige **Medium**-Findings, keine belegten Critical-/High-Findings. Keine Produktivdateien oder bestehenden Tests geändert. Kein Commit, Push, Merge, Rebase oder Reset; MCP-01.3 wurde nicht begonnen. `.codex/config.toml` wurde weder gelesen noch verändert.

## 1. Gegenstand und Unabhängigkeit

- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
- Geprüfter Branch: `feature/mcp-server`.
- HEAD: `55297e2effabcb2310f71dadb8cda63fb2616420`.
- Gegenstand sind die tatsächlichen uncommitted Änderungen gegenüber HEAD, einschließlich neuer untracked Quellen und ihrer Wechselwirkungen mit Core, GUI und bestehenden Tests.
- Grundlagen: `MCP-00-architecture.md` (MCP-00-R1), `MCP-00-implementation-plan.md`, `MCP-01.2-validation.md`.
- Ein separater Review-Agent (`independent_review`) hat die Implementierung unabhängig gelesen und eigene Headless-Gegenproben ausgeführt. Der Hauptagent hat ergänzende Fehler-/Race-Gegenproben ausgeführt und diesen Bericht zusammengeführt. Der Hauptagent war an der vorherigen Implementierung beteiligt; der separate Reviewer nicht. Das unabhängige Urteil lautet ebenfalls CHANGES REQUIRED.

Prüfumfang: `InterpreterExecution`, `OutputOperations`, App-Registrierung, `InterpreterHostQueue`, alle drei neuen Automation-Dateien, Editor-/Variable-/KeyMap-ViewModels, Return-/TextAnchor-Pfad der bestehenden View, Sidebar-Import und Save-Grenzen sowie beide geänderten GUI-Testdateien und `WorkbenchAutomationTests`. Der Implementierungsbericht wurde mit dem Code abgeglichen. Dieser Review fügt im Zielworktree ausschließlich die vorliegende Datei hinzu.

## 2. Architektur- und Integrationsbewertung

Die grundlegende Konsolidierung ist nachvollziehbar: Es gibt einen gemeinsamen Parse-/Runner-Kern, einen gemeinsamen Service und eine statische prozessweite Admission. Die produktiven GUI-Runner-Nebenwege wurden entfernt; auch KeyMap verwendet den asynchronen Kern. Service und Pipeline benötigen keine ViewModels oder Editorreferenzen. Die GUI erhält detached lokale Modelle und übernimmt deren Darstellung; ein DI-Zyklus ist nicht erkennbar.

`InterpreterHostQueue` reserviert mit einem nicht wartenden Versuch. Die GUI-ScriptBook-Ausführung behält ihre Reservation über Dateilesen, optionales Clear, alle physischen Zeilen, Dispatcher-Übergänge und Finalisierung; die inneren Zeilen reservieren nicht erneut. Parser und Zustandsaufnahme liegen unter `AccessState`, Runner laufen im gültigen asynchronen Core-Scope. Früh abbrechende oder werfende Jobs geben ihre Reservation über die vorhandenen using/finally-Wege frei. Für diesen asynchronen Ausführungspfad wurde kein belegter Admission-Leak, paralleler Runner oder UI-Deadlock gefunden.

Der gleiche vollständige Reservierungsvertrag gilt jedoch **nicht** für die nachträgliche GUI-Präsentation kurzer Variablenmutationen bzw. `SetupVariables`: Finding R1.

Frische Runner je ausführbarer Parse-Einheit, genau ein produktiver Passwort-Handler, getrennte runnerlokale Diagnosen und der gemeinsame globale Variablenbestand sind korrekt angelegt. Der Passwort-Drain bleibt auch nach Runner-Cancellation unter Hostscope und Admission. Er beobachtet späte Faults, wartet auf die zurückgegebenen Callback-Tasks und entsorgt Antworten erst nach Runner-Ende defensiv. Die spätere interne Late-Response-Beobachtung kann wegen idempotenter Entsorgung gefahrlos nochmals Dispose aufrufen. Ein dauerhaft nicht zurückkehrender Callback hält bewusst Busy; eine zeitgesteuerte Umgehung ist nicht vorhanden. Ein zusätzlicher Runnerstart oder vorzeitiges Secret-Dispose während dieser Quarantäne wurde nicht nachgewiesen.

## 3. Findings

### R1 – Medium: Admission endet vor der Variablenpräsentation

**Datei/Zeile:** `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:52`, ebenfalls `:59`. Zusammenhang: `InterpreterHostQueue.cs:35–39`, `WorkbenchAutomationService.Mutate`, `VariableViewModel.cs:61–64`.

**Ursache:** In `ApplyVariables(WorkbenchAutomationService.Shared.Mutate(...))` wird zuerst das Argument vollständig ausgewertet. `Shared.Mutate` hat bei seiner Rückkehr die `RunState`-Reservation und den Core-Scope bereits freigegeben. Erst danach aktualisiert `ApplyVariables` die beobachtbare GUI-Collection. Deshalb können reentrante `CollectionChanged`-Abonnenten oder konkurrierende Aufträge während dieser noch laufenden Veröffentlichung eine neue Hostmutation zulassen. Dass alle ViewModels dieselbe Serviceinstanz verwenden, schließt diese Zeitlücke nicht.

**Reproduktion durch den unabhängigen Reviewer:**

1. Der gemeinsame Interpreter enthält `expected=0x(01)`; zwei echte `VariableViewModel`-Instanzen nehmen den Zustand auf.
2. Das Haupt-VM bereitet einen Wertwechsel auf `0x(02)` vor, das zweite VM auf `0x(FF)`.
3. Ein einmalig aktiver `DataVariables.CollectionChanged`-Abonnent des Haupt-VM führt den tatsächlichen `ModifyValueCommand` des zweiten VM mit dessen ausgewähltem Eintrag aus.
4. Der äußere ModifyValueCommand mutiert und beginnt seine Präsentation. Im Callback ist `Shared.IsBusy == false`; die zweite Mutation wird angenommen.
5. Nach Abschluss steht im Interpreter `0x(FF)`, im Haupt-VM weiterhin `0x(02)`.

Kompakte Probe-Ausgabe: `attempted=True; busy=False; core=0x(FF); gui=0x(02)`. Die Variante mit `SetupVariables()` statt äußerer Mutation ist ebenfalls reproduziert: Core `FF`, Haupt-GUI `01`.

**Auswirkung:** Ein konkurrierender/reentranter produktiver Variablenbefehl erhält während der GUI-Präsentation kein Busy. Eine erfolgreich zurückgekehrte GUI-Operation kann einen bereits überholten Zustand anzeigen. Dies verletzt MCP-00-R1, Architektur-Lockregel 5 (`MCP-00-architecture.md:229`: Admission bis nach UI-Präsentation), und den beabsichtigten konsistenten Host-Abschluss.

**Konkrete Lösung:** Eine Reservation über Mutation, Capture **und** das abschließende `ApplyVariables` halten. Dafür kann der Service einen lokalen Präsentationscallback innerhalb der noch gehaltenen Reservation aufrufen oder einen geeigneten internen Operations-/Reservationsvertrag anbieten; er benötigt weiterhin keine Referenz auf ein ViewModel. Die kurzen GUI-Mutationen können auf ihrem UI-Thread abschließen, ohne synchron auf den Dispatcher zu warten. Der Core-Zustandszugriff und die UI-Präsentation dürfen intern getrennte Phasen sein, die gemeinsame Admission muss beide umfassen. Gleiches für `SetupVariables` anwenden.

**Erforderlicher Regressionstest:** Äußere echte Wertmutation bzw. SetupVariables, einmaliger CollectionChanged-Callback und zweite echte ausgewählte Wertmutation. Der zweite Befehl muss abgewiesen werden, und angezeigter/endgültiger Interpreterwert müssen übereinstimmen. Der vorhandene `ModifyValueCommand.Execute(null)`-Aufruf testet diese Lücke nicht, weil er vor dem eigentlichen Mutationspfad zurückkehrt.

### R2 – Medium: Capture-Exception verwirft Ausführungsstatus und Diagnosen

**Datei/Zeile:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs:108–109`; analog der Capture-Aufruf im Syntaxfehlerzweig `:49–51`. GUI-Folge: `CryptoScriptEditViewModel.ExecuteLineAsync` erreicht seinen `Present`-Aufruf nicht.

**Ursache:** Die finale Variablenaufnahme steht außerhalb der Fehlerbehandlung des Runners. Auch wenn Ausführung und Diagnosekopien bereits vorliegen, verhindert eine Exception aus `VariablePresentationCapture.Capture` die Konstruktion/Rückgabe des gesamten `ExecutionPresentation`. Die GUI fängt an dieser Stelle nur Cancellation und Busy, aber keinen Capturefehler ab. Damit gehen auch die unabhängig vom Capture bereits verfügbaren Diagnosen und der Ausführungsstatus verloren. Der frühere asynchrone GUI-Pfad hatte die Capture-Aufnahme innerhalb seiner Fehlerbehandlung.

**Reproduktion mit ausdrücklich kontrollierter Fehler-Injektion:**

1. Ein Headless-Editor führt `VAR previous=0x(01)` aus; Status wird auf den erkennbaren Testmarker `PREVIOUS-STATUS` gesetzt.
2. Ein temporäres `VariableDeclaration`-Testsubstitut mit werfendem virtuellem `Value`-Getter wird in das gemeinsame Dictionary aufgenommen. Dies simuliert gezielt den im Reviewauftrag verlangten Fehler der Zustandsaufnahme; es ist keine Behauptung, dass normale CryptoScript-Syntax dieses Testobjekt erzeugt.
3. `ParseLineAsync("VAR applied=0x(02) KEY absent=Check(0x(AA))")` ausführen. Die neue Variable entsteht und Check erzeugt eine fortsetzbare Error-Diagnose. Anschließend wirft Capture.
4. Beobachtung: `exception=InvalidOperationException; applied=True; status=PREVIOUS-STATUS; diagnostics=0; gui=previous; busy=False`.

**Auswirkung:** Bereits ausgeführte Effekte bleiben korrekt erhalten, werden jedoch zusammen mit den erzeugten Diagnosen nicht als abgeschlossener/fehlgeschlagener Hostzustand an die GUI zurückgegeben. Direkte ParseLine-Aufrufer erhalten eine nicht eingeordnete Exception und alten Status; die Return-View kann nur noch einen generischen Scriptfehler melden. Auch der ursprüngliche Syntaxfehler kann durch einen nachfolgenden Capturefehler verdeckt werden. Die Reservation wird dabei korrekt freigegeben; das Problem ist der verlorene Ergebnis-/Diagnosevertrag und eine nicht als veraltet markierte Ansicht.

**Konkrete Lösung:** Capture als eigenen fehlbaren Finalisierungsschritt behandeln. Runner-Outcome, Diagnose-/Ausgabekopien und gegebenenfalls Syntaxdiagnosen bei Capturefehler erhalten; im internen Ergebnis eine fehlgeschlagene Zustandsaufnahme ausdrücklich kennzeichnen. Die GUI muss einen passenden Fehlerstatus anzeigen und darf den alten Variablenbestand nicht als erfolgreich aktualisiert ausgeben. Keine Wiederholung der Scriptoperationen und kein Rollback einführen. Hierfür genügt eine interne Fehlerrepräsentation; das vollständige externe Snapshot-/Retention-Modell aus MCP-01.3 muss nicht vorgezogen werden.

**Erforderliche Regressionstests:** Capturefehler nach erfolgreicher Ausführung, nach fortsetzbarer Error-Diagnose, nach bereits erfolgten Teilwirkungen eines Laufzeitfehlers sowie im Syntaxfehlerpfad. Diagnose und Outcome müssen lesbar bleiben, der Capturefehler sichtbar sein und Admission nach sicherer Finalisierung wieder frei werden.

## 4. Zusätzliche Gegenproben

Die temporären Probeprojekte lagen außerhalb des Zielworktrees unter dem separaten Chat-Arbeitsordner. Sie nutzten die aktuellen Release-Assemblies bzw. Projektverweise und vorhandene interne Testzugänge. Produktivquellen wurden nicht für die Proben verändert. Die Probeordner werden nach Berichtserstellung entfernt; Verfahren und Resultate sind hier festgehalten.

| Gegenprobe | Ergebnis |
| --- | --- |
| Abbruch unmittelbar nach Start einer leeren Hostoperation, 200 Iterationen | Keine hängenbleibende Reservation; danach IsBusy jeweils false. Dies prüft das Scheduling-Rennen, erzwingt aber nicht in jeder Iteration exakt dieselbe Schedulerposition vor Task.Run. |
| Passwortantwort und Cancellation konkurrierend aus zwei Tasks, 100 Iterationen | Kein vorzeitiges Secret-Dispose während der noch laufenden Testoperation; Secret nach Ende entsorgt; keine Reservation verloren. |
| Exception im afterLine-Callback zwischen erster und zweiter Buchzeile | Erste Wirkung vorhanden, zweite Zeile nicht ausgeführt, Exception beobachtet, IsBusy danach false. |
| Werfender Print-Abonnent vor dem Collector | Ausführung fehlgeschlagen, `before` vorhanden, `after` fehlt, eine Diagnose, Admission frei. Collector hier leer: Der frühere Multicast-Abonnent verhindert die weitere Eventauslieferung. Kein verdeckter Erfolg/weiterlaufender Runner. |
| Capturefehler nach Zustandsänderung und Check-Diagnose | R2 reproduziert. |
| Echte ausgewählte Mutation einer separat erzeugten Variablenansicht während aktiv gehaltenem Hostjob | Mutation abgewiesen; Corewert bleibt `01`. Idle-Kontrolle desselben Befehls ändert ihn auf `FF`. Die gemeinsame Admission selbst funktioniert. |
| CollectionChanged-Exception während Buch-Präsentation | Erfolgsflag false, Fehlerstatus gesetzt, bearbeitete erste Zeile angehängt, zweite nicht ausgeführt, Admission frei. |
| GUI-Status-/CollectionChanged-Callbacks und daraus gestarteter Task.Run während einer Scriptausführung versuchen AccessState(Clear) | Jeweils ExecutionBusyException, ursprünglicher Zustand erhalten. Die vermutete Umgehung über einen geerbten Dispatcher-Kontext wurde hier nicht bestätigt und ist kein Finding. |
| CollectionChanged während kurzer Variablenmutation bzw. SetupVariables führt tatsächliche zweite VM-Mutation aus | R1 reproduziert, durch separaten Reviewer. |

Die Print-Abonnentenbeobachtung ist ein ergänzender Test-/Vertragsklärpunkt für die spätere Ausgabeveröffentlichung: Der jetzige Collector kann nicht aufnehmen, was ein früherer werfender Multicast-Abonnent ihm nicht mehr zustellt. Wegen des sichtbaren Ausführungsfehlers und der bestehenden Event-Exception-Semantik wird daraus hier kein zusätzlicher Blocker abgeleitet. Ein entsprechender Info-Fall sollte bei ergänzenden Tests ebenfalls aufgenommen werden.

## 5. Testabdeckung und Aussagegrenzen

Im Review erneut ausgeführt:

- Alle **15** bestehenden `WorkbenchAutomationTests`: bestanden (auch vom separaten Reviewer erneut ausgeführt).
- Alle **28** bestehenden `SemanticDiagnosticExecutionTests`: bestanden. Darunter die synchrone Core-Reentrancy und Wiederherstellung der Diagnosekontexte.
- `git diff --check`: erfolgreich.
- Die oben beschriebenen zusätzlichen Headless-/Stress-Gegenproben.

Die vollständigen im Implementierungsbericht dokumentierten Ergebnisse (Core 7 667 bestanden/4 übersprungen, GUI 254 bestanden, MCP-01.1 89 bestanden, beide Release-Solutions/Restore erfolgreich) wurden als vorhandene Nachweise geprüft; in diesem Review wurden diese vollständigen Suiten nicht noch einmal insgesamt ausgeführt. Bestehende explizite/plattformabhängige Ausschlüsse und native Desktop-Smoke-Grenzen bleiben bestehen.

Die 15 neuen Pipeline-Fälle liefern sinnvolle Nachweise für Mehrfachstatements, gemeinsame Variablen trotz neuer RunnerIds, physische Zeilen und exaktes out:-Skipping, Parserreset, Teilwirkungen, fortsetzbare Errors, Buchreservation, Passwortdrain sowie sichere Passwort-/Ausgabe-Callbacks. Sie ersetzen jedoch keine Prüfung aller Abschlussphasen:

- Der Test mit `ModifyValueCommand.Execute(null)` erreicht keine Mutation und konnte R1 nicht finden.
- Aufnahmefehler werden nicht geprüft; der vorhandene GUI-Refresh-Fehlertest betrifft `ApplyVariables` und erreicht den früheren Capture-Schritt nicht. Daher blieb R2 unentdeckt.
- Das Zusammentreffen tatsächlicher zweiter Variablenmutation mit CollectionChanged war nicht abgedeckt.
- Die neuen Output-Callback-Tests prüfen abgewiesene Zugriffe, aber keine werfenden Abonnenten oder deren Einfluss auf Collector-Reihenfolge.
- Die Passworttests prüfen klar definierte späte Antwort/Fault-Szenarien; die zusätzlichen Stressläufe erweitern die Race-Abdeckung, sind aber kein Beweis für jede Schedulerinterleaving-Variante.

Nach Behebung sind zuerst die konkreten R1-/R2-Regressionen und anschließend die vollständigen bestehenden Core-, GUI- und MCP-Suiten sowie beide Release-Builds erneut erforderlich.

## 6. Abgleich mit MCP-00-R1 und Semantik

| Vertragsbereich | Bewertung |
| --- | --- |
| Ein Service/eine Pipeline, globaler Zustand, keine parallelen Runner | Im produktiven Ausführungspfad erfüllt; keine separaten Dictionaries oder Runnerpools. |
| Gemeinsame nicht wartende Admission, gesamtes Buch reserviert | Für die asynchrone Scriptausführung erfüllt; vorzeitiges Ende kurzer Mutations-/Setup-Präsentation ist R1. |
| Sichere Passwort-Lebensdauer | Nach Codeprüfung und Gegenproben erfüllt; bewusst dauerhaft Busy bei nie abgeschlossenem Callback. |
| Output-Callback-Schutz und synchrone Core-Reentrancy | HostOutputCallback schützt bei vorhandenem asynchronem Ambient-Scope; ohne diesen bleibt synchroner Core-Vertrag erhalten. Keine neue öffentliche Core-API. 28 unveränderte Diagnose-/Reentrancy-Tests bestanden. |
| Return, Mehrfachstatements, Buchzeilen/out:, History, Print/Info, TextAnchor | Produktive Wege überprüft; keine zusätzliche Umdeutung oder doppelte Ausgabeimplementierung gefunden. Originale View-Einfügung bleibt bestehen. |
| Semantische Error-Diagnosen und Teilwirkungen | Error-Schweregrad ist korrekt kein automatischer Abbruch; keine Wiederholung und kein Rollback. Bewusste Teilzustandsanzeige nach Fehlern ist fachlich richtig, aber der Capturefehlerpfad ist R2. |
| Diagnosekopien | SemanticError-Felder und unveränderliche Location werden unabhängig kopiert; identische zusätzliche Exception-Diagnose auf dieselbe Kopie abgebildet. Syntax-/Lexerlisten werden pro Einheit zurückgesetzt und kopiert. Bei Capturefehler fehlt ihre Rückgabe: R2. |
| Lexerfehler | Die dokumentierte Host-Korrektur verhindert fehlerhafte Parse-Einheiten einschließlich gewöhnlicher Lexerfehler. Keine Änderung der Grammatik oder gültiger CryptoScript-Operationen gefunden. |
| Abhängigkeitsgrenzen | Kein neues Projekt-/Paket-/Solution-Delta, keine MCP-Typen im Core; Host bleibt im Avalonia-Projekt. |
| Umfang | Kein produktiver MCP-Ausführungszugriff, keine HTTP-Aktivierung, Auth/Pairing, Editorrevisionen, Save-Commit-Sperre oder Ergebnisretention vorgezogen. |

Die zwei dokumentierten master-Commits `4b78532` (Dateiverschlüsselung) und `6943439` (macOS-ARM64-ABI) sind weiterhin ausschließlich auf master. Keine Übernahme oder Integration vorgenommen; deren Runner-/Datei-/Build-Wechselwirkungen bleiben bei späterer Integration neu zu prüfen.

## 7. Verbleibende Risiken für MCP-01.3 und bewusste Grenzen

- Die bestehende Save-Lücke ist dokumentierter Folgeumfang MCP-01.4 und wird hier nicht als neues Finding wiederholt. Keine durch den aktuellen Diff nachgewiesene zusätzliche Save-Verschlechterung.
- Die internen GUI-Modelle/Diagnosekopien sind kein veröffentlichbarer externer Snapshotvertrag. Redigierte immutable DTOs, stale/faulted-Ergebnisse, Host-executionId-Korrelation, Retention, Idempotenz und Limits bleiben MCP-01.3; R2 muss trotzdem bereits als interner Finalisierungsfehler beherrscht werden.
- Ein Auftrag und sein Passwortcallback sind noch nicht durch ein vollständiges serviceweites Lifecycle-/Cancellation-Register modelliert. Die aktuelle Quarantäne ist sicherheitsorientiert und kann unbegrenzt Busy halten. Sie darf im Folgeschritt nicht durch Request-Cancellation oder Timeout vorzeitig aufgehoben werden.
- Für zukünftige Veröffentlichung sollten auch KeyMap-/Status-Präsentationsreihenfolge und werfende statische Event-Abonnenten explizit getestet werden. Für sie wurde hier kein zusätzlicher reproduzierter Blocker behauptet.
- Direkte synchrone Core-Reentrancy bleibt bewusst erlaubt; der Host darf daraus keinen produktiven zweiten GUI-Ausführungspfad ableiten.
- Keine Aussage über native Windows-Dialog-/Shutdown-Smokes oder andere Betriebssysteme; die ausgeführten GUI-Gegenproben verwenden Avalonia.Headless.

## 8. Abschlussentscheidung

**CHANGES REQUIRED.** R1 und R2 müssen korrigiert und durch gezielte Regressionen abgesichert werden. Die grundsätzlich tragfähige gemeinsame Pipeline und die bestandenen Tests rechtfertigen noch keine Commitfreigabe, solange nach einer zugelassenen Mutation veralteter GUI-Zustand erscheinen bzw. bei Capturefehlern der Ausführungs-/Diagnoseabschluss verloren gehen kann.

Im Rahmen dieses Reviews wurden keine Korrekturen implementiert. Nach diesem Bericht wird gestoppt; die gemeinsame Prüfung und ein gesonderter Korrekturauftrag bleiben offen.


## 9. Korrekturstatus R1/R2 – 22.10.2026

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


## Final Re-Review – 22.10.2026

Die Überschrift entspricht der ausdrücklichen Vorgabe des Auftrags. Tatsächlicher Prüftag: **23.10.2026**, Europe/Zurich. Der vorherige Review einschließlich Findings, Reproduktionen, historischer Entscheidung CHANGES REQUIRED und Korrekturstatus bleibt unverändert erhalten. Dieser Abschnitt enthält die abschließende unabhängige Bewertung des aktuellen Korrekturstands.

### Geprüfter Stand und Unabhängigkeit

- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
- Branch: `feature/mcp-server`; HEAD unverändert `55297e2effabcb2310f71dadb8cda63fb2616420`.
- Gegenstand: tatsächliche aktuelle R1-/R2-Quellen, ihre produktiven Verbraucher, die 19 neuen Review-Regressionen sowie die bestehenden 15 Pipeline-Fälle. Architektur, Implementierungsplan, Validierungsbericht und bisheriger Review wurden dagegen abgeglichen.
- Der separate Reviewer `independent_review` war nicht an den Implementierungskorrekturen beteiligt. Er hat die Quellen unabhängig geprüft, alle 34 Fälle erneut ausgeführt und zusätzlich eigene ursprüngliche Gegenproben wiederholt. Der an der Korrektur beteiligte Hauptagent hat die Ergebnisse zusammengeführt, die Quellzeilen verifiziert und einen weiteren Testlauf mit aktuellem Build durchgeführt.
- Ausschließlich der vorliegende Reviewbericht wurde ergänzt. Keine Produktiv- oder Teständerungen, keine zusätzlichen Refactorings, kein Commit/Push/Merge/Rebase und kein Beginn von MCP-01.3. `.codex/config.toml` wurde weder gelesen noch verändert.

### R1 – VERIFIED FIXED

**Tatsächliche Quellreferenzen:**

- `CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs:35–43`: Eine Reservation umfasst Zustandsoperation und den optionalen Präsentationscallback. Der synchrone Core-Scope endet an Zeile 39; die Präsentation erfolgt an Zeile 42 vor der Reservationsfreigabe.
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:51–56`: Mutation und detached Capture werden unter dem Core-Scope ausgeführt; der lokale Callback wird durchgereicht, ohne eine ViewModel-Abhängigkeit einzuführen.
- `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:49–59`: Echte Commands und SetupVariables verwenden diesen Vertrag. `ApplyVariables` liegt vollständig innerhalb der Admission.

**Unabhängige Wiederholung der ursprünglichen Zwei-VM-Gegenprobe:** Zwei tatsächliche VariableViewModels besitzen echte ausgewählte Einträge. Im einmalig aktiven CollectionChanged-Callback versucht das zweite VM den Wert FF zu setzen. Die ursprüngliche äußere Mutation 01→02 sowie die SetupVariables-Variante wurden separat ausgeführt:

```text
setup=False; attempted=True; admissionBusy=True; coreGateBusy=False;
nestedSetupBlocked=True; core=0x(02); gui=0x(02); released=True; followUp=True

setup=True; attempted=True; admissionBusy=True; coreGateBusy=False;
nestedSetupBlocked=True; core=0x(01); gui=0x(01); released=True; followUp=True
```

Damit ist die gewünschte Trennung ausdrücklich nachgewiesen: Der Core-State-Scope ist vor der Präsentation frei, die gemeinsame Admission bleibt währenddessen belegt. Der zweite echte Mutationsbefehl hat keinen Effekt; ein direkter Setup-Eintritt erhält Busy. Nach äußerem Abschluss funktioniert derselbe zweite Befehl. Die ursprüngliche R1-Inkonsistenz tritt nicht mehr auf.

Die beiden unveränderten Fälle `ThrowingMutationOrSetupPresentationPreservesExceptionAndReleasesAdmission` belegen zudem Exception-Identität, zuverlässige Freigabe, erhaltene bereits erfolgte Mutation und erfolgreiche Folgemutation. Die Callbackausführung benötigt kein synchrones Dispatcher-Warten. Es wurde kein neuer Deadlock oder Admission-Leak gefunden.

**Verbleibende Grenze:** Die Präsentation ist weiterhin ein synchroner lokaler GUI-Schritt; sie ist keine Transaktion und rollt bereits erfolgte Interpreteränderungen bei einem Fehler nicht zurück. Ein blockierender lokaler Callback blockiert entsprechend diese Operation, erhält aber keinen zweiten Hosteintritt. Daraus ergibt sich kein neues blockierendes Finding.

### R2 – VERIFIED FIXED

**Tatsächliche Quellreferenzen:**

- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs:14–23`: Script-Outcome und ursprünglicher Status bleiben von `CaptureFailed` und `HostSucceeded` getrennt. Der ergänzte Präsentationsstatus benennt den Capturefehler ohne dessen Rohtext.
- `WorkbenchExecutionPipeline.cs:56–58` und `:108–116`: Sowohl Syntax-/Lexerfehler ohne Runner als auch Runner-Abschlüsse verwenden dieselbe Finalisierung. Semantische Diagnosekopien und die korrekte Zuordnung der Exception-Diagnose entstehen vor dem Capture.
- `WorkbenchExecutionPipeline.cs:119–133`: Genau ein Capture-Aufruf pro Einheit unter AccessState; der Capturefehler setzt nur seine eigene Kennzeichnung. Bereits vorhandene Daten werden weiterhin zurückgegeben. `EffectsPossible` ist bei Syntaxfehler ohne Runner false und bei vorhandener Runneridentität konservativ möglich; es behauptet keine nachgewiesene Mutation.
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:144–161`: Diagnosen und ursprünglicher Scriptstatus bleiben sichtbar. Bei Capturefehler bleibt die vorhandene Variablenliste unangetastet, wird als stale markiert und der Hostabschluss ist nicht erfolgreich.
- `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:61–80`: Erfolgreiche spätere Aktualisierung löscht die Stale-Kennzeichnung; eine fehlgeschlagene Collection-Präsentation setzt sie.
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:45–46`: Nach fehlgeschlagener Finalisierung stoppt die Buchschleife anhand des vollständigen Hostabschlusses. Bereits wirksame/angehängte Zeilen bleiben erhalten; keine Folgezeile und kein falscher Gesamterfolg.
- `CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs:44–48`: Auch die KeyMap-Präsentation macht Capturefehler zusätzlich zum erhaltenen Scriptstatus sichtbar.

**Eigene Gegenprobe des unabhängigen Reviewers:** Ein kontrolliert werfender Value-Getter simuliert den Aufnahmefehler nach einer Scriptwirkung und einer fortsetzbaren Check-Diagnose. Ergebnis:

```text
succeeded=True; hostSucceeded=False; captureFailed=True; effectsPossible=True;
diagnostics=1; outputs=1; captureAttempts=1; applied=True; busy=False;
status=Line successfull parsed
```

Der ursprüngliche Erfolg mit fortsetzbarer Error-Diagnose wird weder in einen anderen Script-Outcome umgedeutet noch durch die Capture-Exception ersetzt. Der zusätzliche Hostfehler bleibt ausdrücklich erkennbar. Es gibt einen Aufnahmeversuch, keine versteckte Wiederholung und keinen Rollback; die Reservation wird anschließend freigegeben.

Die erneut ausgeführten Szenarien decken außerdem regulären Erfolg, Runtime-Abbruch nach Teilwirkung und Syntax-/Lexerfehler ohne Ausführung ab. Interne Ergebnisse behalten die tatsächlich gesammelten Ausgaben in Reihenfolge sowie Syntax-/semantische Diagnoseinformationen. Der Feldvergleich prüft alle öffentlichen SemanticError-Felder und eine unabhängige Location-Kopie; die Exception-Diagnose wird nicht dupliziert. Die GUI-Tests belegen dasselbe alte Variablenobjekt, keine Collection-Aktualisierung, sichtbaren Capturefehler und stale=true, danach erfolgreiche Folgeausführung mit aufgehobener Stale-Kennzeichnung.

Capture-Exceptionobjekte, ihre Messages oder Stacktraces werden nicht in den internen Ergebnisfeldern gespeichert oder an die GUI weitergereicht. Das Testmarker-Verbot im GUI-Status besteht. Es wurde durch die Korrektur kein öffentlicher MCP-Ergebnisvertrag oder zusätzlicher Zugriff auf vertrauliche Daten eingeführt.

**Verbleibende Grenze:** Bei fehlgeschlagenem Capture existiert kein neuer verlässlicher Variablensnapshot; das leere interne Platzhalterfeld ist nur zusammen mit CaptureFailed zu verstehen und wird von den geprüften Verbrauchern nicht als frischer leerer Zustand angewandt. Vollständige externe Snapshot-/Retention- und Lifecycle-Verträge bleiben MCP-01.3 vorbehalten.

### Testbewertung und ausgeführte Validierung

Die 19 neuen Fälle treffen die konkreten früheren Fehlerstellen: R1 verwendet echte ausgewählte Befehle und deterministische CollectionChanged-Reentrancy; R2 erreicht ausdrücklich den fehlerhaften virtuellen Getter nach verschiedenen Outcomes. Die Zählerprüfung erkennt wiederholtes Capture, die GUI-Assertions erkennen eine fälschliche Anwendung der leeren Platzhalterliste, und die Folgeoperationen erkennen eine nicht freigegebene Admission. Die frühere Implementierung würde diese Anforderungen verfehlen. Die fünf Capturefehlerszenarien werden jeweils intern und in der GUI geprüft. Bestehende Testassertions wurden nicht verändert.

Der unabhängige Reviewer führte aus:

```powershell
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~WorkbenchReviewRegressionTests|FullyQualifiedName~WorkbenchAutomationTests' --logger 'console;verbosity=minimal'
```

Ergebnis: **34 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Zusätzlich wurden seine temporären unabhängigen Zwei-VM- und Capture-Probeprogramme mit den aktuellen Release-Assemblies gebaut und ausgeführt.

Der Hauptagent führte anschließend denselben Fallumfang erneut **mit Build des aktuellen Quellstands** aus:

```powershell
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release --no-restore --filter 'FullyQualifiedName~WorkbenchAutomationTests|FullyQualifiedName~WorkbenchReviewRegressionTests' -v quiet --logger 'trx;LogFileName=mcp012-final-rereview.trx'
```

Ergebnis ebenfalls **34 bestanden, 0 fehlgeschlagen, 0 übersprungen**; der lokale ignorierte TRX-Nachweis liegt unter `CryptoWorkBenchGuiTest/TestResults/mcp012-final-rereview.trx`. Keine Produktiv- oder Teständerungen zur Durchführung der Gegenproben. Die temporären Probeordner wurden nach Prüfung ihrer absoluten Pfade entfernt.

Die bereits nach den Korrekturen dokumentierten vollständigen Core-/GUI-/MCP-Suiten und beide Solution-Builds wurden ohne neue Implementierungsänderung auftragsgemäß nicht nochmals insgesamt ausgeführt. Die Nachweise aus dem Korrekturabschnitt bleiben bestehen. `git diff --check` ist sauber.

### Architektur, Umfang und neue Findings

Eine gemeinsame Hostpipeline, neue Runner je Parse-Einheit, globaler Zustand, durchgehende Buchreservation und die bisherigen asynchronen Core-Gates sind erhalten. Der Passwort-Drain einschließlich später Antwortentsorgung und Handlerabmeldung liegt weiterhin vor der Freigabe des Hostscopes; R1/R2 bauen ihn nicht um. Die erlaubte synchrone Core-Reentrancy und die bestehende asynchrone HostOutputCallback-Absicherung wurden durch die Korrekturen nicht verändert. Kein Projekt-/Solution-/Lockfile-Delta und keine neue ViewModel-Abhängigkeit des Service.

Die geprüften Print-/Info-Abonnentenfehler behalten den sichtbaren Scriptfehler und stoppen spätere Statements. Die bekannte Multicast-Grenze eines vor dem Collector werfenden Abonnenten bleibt ausdrücklich bestehen; es wurde keine zusätzliche Verschlechterung gefunden. Die bekannte GUI-Save-Lücke bleibt MCP-01.4. Ein dauerhaft offener Passwortcallback darf weiterhin sicherheitsorientiert Busy halten. Kein MCP-01.3-Ausführungs-/Retention-/Lifecycle-Vertrag, keine HTTP-Aktivierung oder zusätzliche Sprachfunktion wurde vorgezogen.

**Neue Findings nach Critical/High/Medium/Low: keine.** Insbesondere keine neuen blockierenden Findings im geprüften R1-/R2-Umfang. Die verbleibenden bekannten Grenzen sind kein neu behaupteter Vollnachweis nativer Desktop- oder weiterer Plattformtests.

### Abschlussentscheidung

**READY FOR COMMIT.** R1 und R2 sind nach Quellprüfung, unabhängig wiederholten ursprünglichen Gegenproben und erneut bestandenem 34-Fälle-Lauf nachweislich korrigiert. Diese Entscheidung autorisiert in diesem Auftrag keinen Commit: Branch und HEAD bleiben unverändert, die Arbeit stoppt nach dem ergänzten Reviewbericht.
