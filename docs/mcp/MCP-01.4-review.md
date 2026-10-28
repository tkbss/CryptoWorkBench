# MCP-01.4 – Unabhängiger Code-Review

Datum: 24.10.2026. Entscheidung: **CHANGES REQUIRED**.

**Vier Medium-Findings und ein Low-Finding; keine belegten Critical- oder High-Findings.** Die vollständigen frisch ausgeführten regulären Suiten bestehen mit 8 141 bestandenen Fällen. Eigene Gegenproben mit den tatsächlichen Produktionsklassen zeigen dennoch Fehler an Lifecycle-, Mutations- und Präsentationsgrenzen. Vor einer Commitfreigabe sind R1–R4 zu korrigieren; R5 sollte im selben Korrekturschritt behoben werden.

## 1. Gegenstand, Unabhängigkeit und Dateigrenze

- Zielworktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
- Branch: `feature/mcp-server`.
- HEAD vor und nach der fachlichen Prüfung: `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.
- Grundlage: der konkrete Reviewauftrag aus `Pasted text.txt` sowie die fünf verbindlichen Dokumente `docs/mcp/MCP-00-architecture.md`, `MCP-00-implementation-plan.md`, `MCP-01.3-validation.md`, `MCP-01.3-review.md` einschließlich finalem Re-Review und `MCP-01.4-validation.md`.

Der separat eingesetzte Reviewer `independent_mcp014_review` war an der Implementierung nicht beteiligt und erhielt keine Implementierungshistorie. Er las Auftrag, Pflichtdokumente, tatsächliche uncommitted Quellen, alle 47 neuen Testfälle und die betroffene vorhandene GUI-/Registry-Infrastruktur selbst. Er entwickelte und führte die unten beschriebenen zusätzlichen Headless-Gegenproben selbst aus und verfasste diesen Bericht. Der implementierende Hauptagent kontrolliert ausschließlich Provenienz und Dateigrenzen; die fachliche Entscheidung stammt vom unabhängigen Reviewer.

Geprüfter vollständiger Änderungsumfang gegenüber HEAD, vor Anlage dieses Berichts:

| Datei | Umfang/Funktion |
| --- | --- |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` | Getrackt: +7/−12; Editorrechte/-verfügbarkeit, Save-Drain und Editor-Disposal. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | Getrackt: +65/−28; Registrierung, Präsentation, Save und Lifecyclebindung. |
| `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` | Getrackt: +12/−9; Save-Ziel und ausdrückliche Überschreibentscheidung. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml` | Getrackt: +2/−2; tatsächlicher `WorkbenchTextEditor`. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | Getrackt: +11/−5; Attach/Detach und gebundene Anchor-Ausgabe. |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | Neu; Eingabe-, Dokumenttausch-, Änderungs- und Undo-/Redo-Sperre. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs` | Neu; Identität, Revision und UI-Snapshot. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs` | Neu; lokale gebundene Vorher-/Nachher-Bestätigung. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs` | Neu; Reads, Edits und ExecuteEditor. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` | Neu; SaveIntent, Lease, Commit und Snapshot-Writer. |
| `CryptoWorkBenchGuiTest/EditorAutomationTests.cs` | Neu; 22 Testmethoden, durch Parametrisierung 47 Fälle. |
| `docs/mcp/MCP-01.4-validation.md` | Neu; Implementierungsnachweis, keine unabhängige Freigabe. |

Der getrackte Diff umfasst insgesamt +97/−56 Zeilen; sämtliche neuen Dateien wurden zusätzlich vollständig gelesen. Zeilenangaben dieses Berichts beziehen sich auf den tatsächlichen hier geprüften uncommitted Stand.

Im Zielrepository wurde ausschließlich `docs/mcp/MCP-01.4-review.md` als Quelldatei angelegt. Produktions- und Testquellen sowie bestehende Dokumente wurden nicht geändert. Builds erzeugten lediglich reguläre Buildartefakte. Neue Probequellen und Logs liegen außerhalb des Zielrepositories unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-independent-review/`. Kein Commit, Push, Merge, Rebase oder Reset, keine Masterübernahme, kein Beginn von MCP-01.5. `.codex/config.toml` wurde weder gelesen noch verändert.

## 2. Architektur und Integration

Die gemeinsame Pipeline bleibt erhalten: `ExecuteEditorAsync` benutzt `Accept`/`RunOwnedAsync`, `InterpreterHostQueue.Reserve`, einen Hostlauf für das ganze Buch und den vorhandenen `WorkbenchExecutionPipeline`-Zeilenkern. Frische Runner teilen weiterhin denselben prozessweiten Variablenbestand. Eine zweite Interpreterpipeline oder zweite Variablensammlung ist nicht hinzugekommen. Bestehende serviceeigene Cancellation, Deadlines, Retention, Fingerprints und Passwortquarantäne werden weiterverwendet.

Es gibt keine csproj-, Solution-, Paket-, Abstractions-, MCP-Transport- oder CryptoScript-Änderung. Die Referenzgrenzen bleiben Core ohne Hostreferenz, Abstractions ohne GUI/Core, MCP ausschließlich mit Abstractions und GUI mit Core/Abstractions. Der neue Präsentationsvertrag ist hostintern; der Service benötigt keine Referenz auf das konkrete Editor-ViewModel. Keine neue öffentliche Toolregistrierung, produktive HTTP-Hostaktivierung, Authentisierung oder Sprachänderung wurde vorgezogen.

Die Bridge arbeitet grundsätzlich mit dem tatsächlichen angehängten Control. Identitätsgebundenes Detach verhindert das Abmelden eines Nachfolgeeditors durch eine alte View. Dokumenttausch erhält eine neue editorId. `Current` beobachtet Dokument und Version auch beim reentranten Lesen, bevor später abonnierte Bridge-Handler laufen. Die vorhandenen Tests bestätigen dafür zusammengehörigen Text, Revision, Länge und Identität ohne Doppelzählung desselben beobachteten Versionswechsels. Worker-Reads werden auf den UI-Dispatcher geführt; die off-thread verwendete Verfügbarkeit dereferenziert kein Control. Dirty bleibt konservativ Dirty/Unknown und wird durch Save oder zurückgesetztes IsModified nicht pauschal Clean.

Insert/Append respektieren genaue UTF-16-Offsets und die Surrogat-/CRLF-Grenzen. Die lokale Replace-Bestätigung erhält vollständige immutable Vorher-/Nachher-Inhalte und den gebundenen Auftrag. Nach dem Dialog werden Rechte, Epoche, Lifecycle, Identität und Revision erneut geprüft. Admission bleibt während des Dialogs gehalten, gewöhnliche Benutzereingabe darf die Revision invalidieren. No-ops erzeugen nach gültiger Prüfung weder Dialog noch Undo-Aktion oder Revision. Echte Änderungen verwenden eine Undo-Gruppe und keine synthetischen Return-Ereignisse. R4 betrifft dagegen Reentrancy *innerhalb* des anschließenden realen Edits.

## 3. Findings

### R1 – Medium: Editorlauf beginnt trotz vorherigem Detach oder Dokumenttausch

**Datei/Zeilen:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs:173–180`, insbesondere `:175–176`; tatsächlicher anschließender Runneraufruf in der bestehenden `WorkbenchAutomationService.cs:49–51`.

**Ursache:** Die Lifecycleprüfung im `beforeLine`-Callback unterdrückt bei ungültiger Registrierung nur `Presentation.Begin`. Der Callback endet weiterhin erfolgreich. Der Buchkern wartet auf diesen `Task` und führt danach die Parse-Einheit aus. Auch `afterLine` setzt beim ungültigen Editor durch `return true` die Fortsetzung fort. Es fehlt eine verbindliche Abbruchgrenze zwischen angenommener Editoridentität und noch nicht begonnener Ausführung. Ein bloßes `return false` im derzeit als `Task` verwendeten beforeLine-Vertrag würde diesen Fehler nicht beheben: der Buchkern wertet keinen booleschen Rückgabewert aus.

**Unabhängige Reproduktion:** In derselben UI-Dispatchaktion wird der reale Editor auf `VAR reviewShouldNotRun=0x(01)` gesetzt, der Request erstellt und `ExecuteEditorAsync` aufgerufen. Bevor die UI für den ersten `beforeLine`-Callback freigegeben wird, wird entweder die View detached oder das Document durch `new TextDocument("successor")` ersetzt. Es gibt keine künstliche Produktionsnaht und keinen Sleep zur Steuerung dieses Rennens. Beide Varianten erzeugen anschließend die Variable:

```text
EXEC_False_ACCEPTED: True
EXEC_False: state=Completed; effects=Possible; variable=True
EXEC_True_ACCEPTED: True
EXEC_True: state=Completed; effects=Possible; variable=True; text=successor
```

**Auswirkung:** Die ungültig gewordene Editorbindung verhindert zwar Textausgaben in den Nachfolger, verhindert aber keine noch nicht gestarteten Interpreter-/Dateieffekte des alten Auftrags. Dies verletzt den ausdrücklich geforderten Schutz gegen Ausführung nach unzulässigem Detach/Dokumenttausch. Die Probe betrifft den heute zugelassenen lokalen GUI-Caller; ein externer MCP-Ausführungsbypass wurde nicht gefunden.

**Korrektur:** Ungültige Registrierung/Dokumentidentität vor dem ersten und vor weiteren noch nicht begonnenen Zeilenschritten verbindlich ablehnen beziehungsweise den serviceeigenen Auftrag kontrolliert abbrechen. Bereits mögliche Effekte müssen korrekt erhalten bleiben; vor dem ersten Runner müssen Effects.None und unveränderte stateVersion gelten. Die Identitätsprüfung muss die tatsächliche Fortsetzung des Buchkerns steuern und darf nicht nur die Präsentation auslassen. Request-Wait-Cancellation darf dabei weiterhin keinen angenommenen Auftrag besitzen.

**Erforderliche Regressionen:** Beide oben kontrolliert reproduzierten Vorstartvarianten sowie Detach/Dokumenttausch zwischen zwei Zeilen. Vorstart ohne Variable/Dateieffekt; nach erster Zeile keine weitere Zeile, sichere terminale Publikation und weiterhin nutzbare Idempotenz.

### R2 – Medium: Leerer Text-Setter löscht während Save die Undo-Historie

**Datei/Zeilen:** `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs:57–72`, im Kontext der nur für Undo-/Redo-Kommandos vorgezogenen Prüfung `:19–39`.

**Ursache:** Die Commit-Lease schützt wirkliche Dokumentänderungen mit `Changing` und schützt die produktiven Undo-/Redo-Kommandos gesondert vor Stackzugriff. Der geerbte hostseitig erreichbare `Text`-Setter kann jedoch bei einem bereits leeren Dokument die Undo-Historie löschen, ohne eine Textänderung und damit ohne `Changing` auszulösen. Die Klasse schützt diese reine Historienänderung nicht.

**Unabhängige Reproduktion:** Im tatsächlichen `WorkbenchTextEditor` wird zunächst `"x"` gesetzt und durch `Document.Remove(0, 1)` entfernt. Das Dokument ist leer, `UndoStack.CanUndo` ist true. Während `CommitSaveAsync` in einem kontrolliert offenen Delete-Delegate seine aktive Lease hält, wird ausschließlich `h.Editor.Text = ""` aufgerufen. Ergebnis:

```text
EMPTY_TEXT_HISTORY: before=True; after=False; error=
```

Es wurde an der Fehlerstelle weder `UndoStack.Undo()` noch `ClearAll()` direkt aufgerufen. Der sichtbare Text bleibt leer; trotzdem ist die vorhandene Rückgängig-Historie verloren. Revision/Text allein würden diese Mutation nicht erkennen.

**Auswirkung:** Die Anforderung, während Save auch die Undo-/Redo-Historie zu erhalten und danach vollständig weiter nutzen zu können, ist für die ausdrücklich zu prüfenden direkten Textzuweisungen nicht erfüllt. Die normalen Keyboard- und Editor-Undo-/Redo-Kommandos funktionieren dagegen in den eigenen Kontrollproben korrekt. Die Probe behauptet keinen heute nachgewiesenen nativen GUI-Klickpfad, der diesen Setter während Save aufruft; sie zeigt die Lücke der angebotenen direkten Host-API und der behaupteten vollständigen Sperre.

**Korrektur:** Alle vom Host angebotenen Textzuweisungen/Leeren-Einstiege einschließlich gleicher leerer Zuweisung vor möglichen Historieneffekten an die Lease binden. Falls geerbte Bibliothekssetter technisch nicht vollständig abfangbar sind, die produktive Hostgrenze explizit auf geschützte Mutationsmethoden umstellen und deren Verwendung an sämtlichen solchen Aufrufern sicherstellen; allein `Changing` beziehungsweise das Verstecken einer Property bei weiterhin als `TextEditor` typisierten Aufrufern genügt nicht. Historie weder austauschen noch durch synthetische Gruppen vermeintlich reparieren.

**Erforderliche Regression:** Leeres Dokument mit vorhandener Undo- und separat vorhandener Redo-Historie; direkte gleiche Textzuweisung während verzögertem Commit; nach Commit ursprüngliche Historie tatsächlich ausführen und ihren Inhalt prüfen.

### R3 – Medium: Reentranter Shutdown liegt vor Veröffentlichung der Save-Completion

**Datei/Zeilen:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs:44–56`; synchron beobachtbare Propertyänderung in `Controls/WorkbenchTextEditor.cs:62–67`; konsumierender Drain in `WorkbenchAutomationRegistry.cs:582–589`.

**Ursache:** `EditorBridge.Lock` setzt `IsReadOnly=true` und kann dabei synchrone Avalonia-PropertyChanged-Callbacks auslösen. Der Service veröffentlicht `saveCompletion` erst nach Rückkehr dieser Callbacks. `lock(sync)` verhindert dieses Rennen nicht, da ein Callback auf demselben UI-Thread reentrant in denselben Monitor eintreten kann. Der Lifecycle wird hinter der Leaseaufnahme nicht erneut geprüft.

**Unabhängige Reproduktion:** Ein echter `Editor.PropertyChanged`-Handler ruft bei `IsReadOnly=true` `BeginShutdown()` und `DrainAsync()` auf. Das geschieht vor Delete. Der Drain ist schon abgeschlossen; nach Rückkehr des Handlers beginnt der Save dennoch mit Delete und Write:

```text
SAVE_REENTRANT_SHUTDOWN: error=;
drainCompletedInsideCallback=True; deletes=1; text=confirmed
```

**Auswirkung:** Ein Shutdown kann den erforderlichen Service-Drain als abgeschlossen ansehen, obwohl bereits reservierte Save-Arbeit gleich eine destruktive Dateioperation startet. Außerdem wird ein noch vor der dokumentierten Commitgrenze eingetroffener Shutdown nicht abgewiesen. Die Probe injiziert einen zulässigen synchronen Propertycallback, keine Änderung der Produktion; es wird nicht behauptet, die derzeitige native Closing-Anbindung rufe exakt aus diesem Callback auf. Die allgemeine native Closing-Integration bleibt MCP-01.6, der bereits in MCP-01.4 zugesagte Service-/Save-Drain ist hier trotzdem unvollständig.

**Korrektur:** Save-Ownership und ein drainbares Completion-Signal vor jeder reentrant beobachtbaren Lease-/Propertyoperation veröffentlichen und bei jedem Fehler vollständig zurückrollen. Nach solchen Callbacks Lifecycle, konkrete Registrierung und Revisionsbindung erneut prüfen, bevor die destruktive Commitgrenze überschritten wird. Vor Commit kein Delete/Open bei Shutdown; nach tatsächlich begonnenem Commit weiterhin ohne neue Cancel-Lücke finalisieren.

**Erforderliche Regressionen:** PropertyChanged ruft BeginShutdown/Drain und als Variante DisposeAsync auf; der Drain darf keine offene reservierte Save-Arbeit übersehen. Vor Commit muss entweder ohne Dateieffekt abgebrochen werden oder bereits verbindlich veröffentlichte Finalisierung vollständig abgewartet werden. Reentranter Detach an derselben Grenze sollte zusätzlich geprüft werden.

**Positive Abgrenzung:** Die eigene separate Gegenprobe hält *nach tatsächlicher Löschung* vor Rückkehr des Delete-Delegates an, startet Shutdown, Request-Cancellation und Detach und prüft Drain. Dort bleiben Admission und Lease korrekt gehalten; nach Freigabe wird der bestätigte Snapshot geschrieben. R3 betrifft ausdrücklich die frühere Ownership-Veröffentlichung.

### R4 – Medium: Reentranter Dokumenttausch überträgt Editabschluss auf den Nachfolger

**Datei/Zeilen:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs:101–110`, insbesondere `:107–110`; veränderliche Registrierung bei Dokumenttausch in `EditorBridge.cs:107–117`.

**Ursache:** Vor dem Edit werden Identität und Revision geprüft, danach darf `doc.Insert`/`Replace` synchrone Changed-Callbacks ausführen. Ein solcher Callback kann das Editor-Document tauschen. Die Bridge aktualisiert dann `Id` und `RawDocument` desselben Registration-Objekts. Der fortgesetzte alte Edit setzt ohne neue Identitätsprüfung `registration.Editor.IsModified=true` und baut seine Erfolgsantwort aus `registration.Snapshot()` des Nachfolgedokuments.

**Unabhängige Reproduktion:** Ein `Changed`-Handler des alten Dokuments weist dem echten Editor `new TextDocument("successor")` zu und setzt für dieses Dokument ausdrücklich `IsModified=false`. Ein davor revisionsgebunden angenommener Append-Edit läuft weiter. Die Antwort ist erfolgreich und nennt die neue editorId, während sie auf einen Auftrag für die alte editorId reagiert. Das Nachfolgedokument ist danach IsModified=true:

```text
EDIT_REENTRANT_SWAP: ok=True;
responseId=<Nachfolger>; requestedId=<anderer alter Editor>;
successorId=<Nachfolger>; modified=True; text=successor
```

**Auswirkung:** Ein alter Auftrag verändert den Dirty-/Modified-Zustand eines anderen Dokuments und meldet dessen Identität/Länge als Erfolg des ursprünglichen Edits. Der neue Text wird in dieser Probe nicht überschrieben; die Identitäts- und Ergebnisbindung ist dennoch verletzt. Ein neuer Editorauftrag kann seine weitere Revisionslogik damit auf eine irreführende Antwort stützen.

**Korrektur:** Die konkrete Dokument-/Identitätsbindung über die gesamte synchrone Mutation einschließlich Change-/UpdateFinished-Callbacks erhalten. Dokument-/Editorwechsel während dieser Transaktion entweder kontrolliert zurückweisen/deferieren oder nach Callbackrückkehr den alten Effekt ausdrücklich seiner ursprünglichen Identität zuordnen, ohne den Nachfolger zu markieren oder als erfolgreichen Mutationsgegenstand auszugeben. Ein bereits geändertes altes Dokument darf dabei nicht fälschlich als seiteneffektfreier Fehler dargestellt werden.

**Erforderliche Regressionen:** Dokumenttausch und View-/Editorwechsel aus Changed sowie UpdateFinished; Nachfolgertext und IsModified bleiben unberührt, Antwort bezieht sich niemals auf eine andere editorId. Zusätzlich Same-Document-Reentrancy auf klare Busy-/Revisionsregeln prüfen.

### R5 – Low: Fehlerdiagnose des vorigen Laufs bleibt nach fehlerfreiem Editorlauf sichtbar

**Datei/Zeilen:** `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:74–76`, `:168–176`; Einbindung in `WorkbenchEditorOperations.cs:173–181`. Vergleich des bestehenden GUI-Einstiegs mit `ClearDiagnostics` in `CryptoScriptEditViewModel.cs:194–199` und beim Buchimport in `:242`.

**Ursache:** Der neue Editor-Gesamtlauf beginnt nur einzelne Zeilen über `BeginLine`, das History und Printzustand aktualisiert. Die vorhandene Diagnosesammlung wird vor diesem neuen Gesamtlauf nicht geleert; `Present` hängt nur an.

**Unabhängige Reproduktion:** Ein normaler GUI-Lauf `KEY missing=Check(0x(AA))` erzeugt eine Diagnose. Anschließend wird ein fehlerfreier Gesamteditorlauf `VAR cleanRun=0x(01)` ausgeführt:

```text
DIAGNOSTIC_RESET: previous=1; currentGui=1;
currentResult=0; hasErrors=False; state=Completed;
status=Line successfull parsed
```

**Auswirkung:** GUI und sicheres Ergebnis widersprechen sich; eine alte Error-Diagnose bleibt neben dem Erfolgsstatus des neuen Laufs stehen. Kein falscher Interpretereffekt oder externer Geheimnisexport wurde daraus abgeleitet.

**Korrektur:** Die GUI-Präsentation einmal pro neu angenommenem Gesamteditorlauf initialisieren und dabei alte Diagnosen leeren. Nicht vor jeder Buchzeile leeren, sonst gehen Diagnosen früherer Zeilen desselben Buchs verloren; abgewiesene Requests und idempotente Retries dürfen die Präsentation nicht als neuen Lauf zurücksetzen.

**Erforderlicher Test:** Fehlerhafter Vorlauf, anschließend erfolgreicher Editorlauf; GUI enthält nur Diagnosen des aktuellen Laufs. Mehrzeilige fortsetzbare Diagnosen desselben Buchs bleiben gemeinsam sichtbar.

## 4. Frisch ausgeführte Tests und eigene Gegenproben

Alle folgenden Ausführungen stammen aus diesem unabhängigen Review am 24.10.2026. Die vorhandenen Logs unter `artifacts/mcp014/` wurden nicht als frische eigene Resultate ausgegeben.

| Ausführung | Ergebnis/Nachweis außerhalb des Zielrepositories |
| --- | --- |
| `dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release --no-restore --filter FullyQualifiedName~EditorAutomationTests` | **47 bestanden**; `results/review-editor47.trx`, zusätzlich `../mcp014-editor47-run.log`. |
| Vollständiges GUI-Testprojekt, Release, `--no-restore` | **385 bestanden**, keine Fehler/Skips; `results/review-gui.trx`, `review-gui.log`. |
| Vollständiges Core-Testprojekt, Release, `--no-restore` | **7 667 bestanden, 4 bestehende Skips**, keine Fehler; `results/review-core.trx`, `review-core.log`. |
| Vollständiges reguläres MCP-Testprojekt, Release, `--no-restore` | **89 bestanden**, keine Fehler; `results/review-mcp.trx`, `review-mcp.log`. |
| `dotnet build CryptoWorkBench.sln -c Release --no-restore` | Erfolgreich, 0 Fehler und 0 Warnungen im inkrementellen Build; `build-core.log`. |
| `dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Release --no-restore` | Erfolgreich, 0 Fehler und 0 Warnungen im inkrementellen Build; `build-gui.log`. |
| `git diff --check` | Erfolgreich. |
| Eigenes externes Headless-Projekt `dotnet run --project .../mcp014-independent-review/probe.csproj -c Release --no-restore` | Alle unten beschriebenen Beobachtungen; vollständige Quelle `Program.cs`, Projekt `probe.csproj`, abschließendes Log `probes-final-complete.log`. |

Die 47 Fälle sind bereits in den 385 GUI-Fällen enthalten. Insgesamt bestehen **8 141 reguläre Fälle**, die Zahl wird nicht durch den separaten 47er-Lauf erhöht. Die vier Core-Skips bleiben der Darwin-ACL-Fall und drei Windows-Symlinkfälle ohne nötiges Privileg. Die expliziten Hardwarebenchmark- und Installed-Codex-Fälle wurden nicht aktiviert. Ein in der Konsolenausgabe als skipped genannter expliziter Fall wird nicht zusätzlich in die reguläre Skipsumme hineingezählt.

Das eigene Probeprojekt verwendet den echten produktiven Service, das konkrete ViewModel, die XAML-View und ihren `WorkbenchTextEditor`, echte AvaloniaEdit-Dokumente sowie `Avalonia.Headless.HeadlessUnitTestSession` 11.3.20. Der Assemblyname nutzt die bereits vorhandene Friend-Assembly-Grenze für interne Hostmethoden; am Zielprojekt wurde dafür nichts verändert. Es wurde kein Fake-Editor implementiert. Save verwendet den echten Snapshot-Writer; nur Delete- und Bestätigungszeitpunkte werden über die vorhandenen privaten Hostverträge gesteuert. Probeordner und Logs sind Reviewmaterial, keine zusätzlichen Repositorytests.

Eigene Probegruppen und Ergebnisse:

| Probe | Beobachtung |
| --- | --- |
| `ExecuteLifecycle(false/true)` | Vorstart-Detach beziehungsweise Dokumenttausch reproduzieren R1 deterministisch. |
| `SaveReentrantShutdown` | Echter IsReadOnly-Propertycallback reproduziert R3 vor Delete. |
| `EmptyTextHistory` | Direkter leerer Editor.Text-Setter reproduziert R2 während aktiver Save-Lease. |
| `ReentrantSwap` | Echter Changed-Callback reproduziert R4; alte Anfrage erhält neue Identität, Nachfolger wird modified. |
| `DiagnosticReset` | Erfolgreicher Neulauf reproduziert R5. |
| `SupportedInputAndHistory` | Während offenem Delete: `PerformTextInput` für Return und japanischen Text, reale Clipboard-Paste/Cut, Editor.Undo/Redo, Ctrl+Z/Ctrl+Y über Headless-Keyereignisse, direkte SelectedText-/Remove-/Replace-/Document-Zuweisungen. Snapshot unverändert; danach Undo ergibt `first`, Redo `first second`, gespeicherter Text `first second`. |
| `AfterDeleteShutdown` | Datei tatsächlich gelöscht, Delete-Delegate noch offen: Shutdown, Cancellation und Detach; Drain unvollständig, Busy und Lease erhalten. Nach Freigabe tatsächliches Write von `snapshot`, danach entsperrt. |
| `CancelAtLease` | Cancellation im IsReadOnly-Callback unmittelbar vor Commit: OperationCanceledException, 0 Deletes, Sentinel unverändert, Admission und ReadOnly freigegeben. Die zweite Cancellationprüfung funktioniert; die Lifecycleprüfung aus R3 fehlt dagegen. |
| `RevokedApproval` | MCP-Epoche während Replace-Zustimmung widerrufen: Edit Forbidden, Text `before` unverändert, Admission frei; externe Ausführung ebenfalls Forbidden. |
| `Retry` | Gesamteditor-Print, gleicher ExecuteEditor-Request erneut, anschließend voll berechtigter aktivierter externer Caller: identisches Resultat, unveränderter Text, genau ein Print, externer Aufruf Forbidden. |
| `WriteFailure` | Sentinel tatsächlich gelöscht, Lease an dieser Grenze nachweislich aktiv, anschließend echter File.Open-Fehler durch Verzeichnis am Zielpfad. UnauthorizedAccessException, kein behaupteter Rollback, Snapshot unverändert, keine Datei, beide Sperren freigegeben. |
| `OldImport` | Echter ScriptBook-Import am PrintEvent angehalten, alte View detached und neue View am selben VM angehängt, dann fortgesetzt. Nachfolgertext bleibt `successor`, VM zeigt auf Nachfolger, Importvariable entsteht ohne Quell-/Outputeinfügung in den Nachfolger. |
| `RawUndoBoundary` | Absichtlicher direkter `Document.UndoStack.Undo()` unter Lease: Text unverändert und ExecutionBusyException, Undo-Eintrag bereits verbraucht. Separat abgegrenzte rohe Bibliotheks-API, siehe Abschnitt 6; kein zusätzlicher neuer Finding. |

Die Vorstartproben benutzen die natürliche UI-Dispatcher-Grenze, die Saveproben explizite TaskCompletionSources und der Import einen begrenzt wartenden Print-Callback. Die Keyboard-Hilfen erzeugten vier CS0618-Warnungen im *externen Probeprojekt* wegen des älteren Headless-KeyPress/KeyRelease-Overloads; die Aufrufe liefen erfolgreich. Dies sind keine neuen Produktionswarnungen. Die Proben wurden schrittweise erweitert; `probes-first.log`, `probes-expanded.log` und `probes-final.log` bleiben als frühere Teilstände erhalten. Keine dieser Ausführungen scheiterte an einer Headless-Dispose-NRE.

Die bestehende Testsuite wurde nicht in ein neues Projekt kopiert und als unabhängige Gegenprobe umetikettiert. `Program.cs` wurde für diesen Review eigenständig geschrieben. Seine Ausgabe protokolliert sowohl die reproduzierten Abweichungen als auch positive Kontrollen; ein Exitcode 0 dieser Beobachtungsprobe bedeutet ausdrücklich nicht, dass R1–R5 erfüllt wären.

## 5. Bewertung aller 47 neuen EditorAutomationTests

Die Tests verwenden tatsächlich die produktive XAML-View und ihren `WorkbenchTextEditor`. Ihre steuerbaren Dialog-/Delete-Delegates halten sinnvolle reale Commitgrenzen offen. Sie sind eine brauchbare Integrationsbasis, decken jedoch nicht alle im Validierungsbericht pauschal beanspruchten Mutations-/Lifecycle-Grenzen ab.

| Methode / Fallzahl | Aussagekraft und Grenze |
| --- | --- |
| `ActualReturnKeepsPendingExecutionAnchorOutputAndRevision` / 1 | Echte Return-Eingabe und PendingExecution/Anchor-Ausgabe; guter positiver Pfad. Keine Save-Lease während Return. |
| `WholeEditorPrintIsInsertedOnceWithoutDuplicatingSource` / 1 | Prüft tatsächlichen Print, unveränderte Quelle bis auf Ausgabe, Revision und redigierte Resultatausgabe. |
| `ActualReplaceDialogShowsExactBeforeAfterAndHonoursChoice` / 2 | Echte lokale Fenster, vollständige gebundene TextBox-Inhalte, Zustimmung/Ablehnung. Kein nativer Plattform-Dialognachweis. |
| `EarlierChangeSubscribersCannotObserveNewTextWithOldRevision` / 1 | Gute frühe Changed-/DocumentChanged-Lesereentrancy mit fehlendem Document. Prüft keinen Identitätswechsel während eines gerade zugelassenen MCP-Edits (R4). |
| `EditsRejectWrongInstanceEditorStaleRevisionAndConcurrentGuiAdmission` / 1 | Belegt Instanz-, Identitäts-, Revisions- und Busy-Verweigerung ohne Edit. |
| `LateDetachOfOldViewCannotUnregisterSuccessorView` / 1 | Konkreter alter/neuer Viewwechsel am selben VM; sinnvolle Identitätsregression. |
| `MissingDocumentIsUnavailableAndServiceDisposalUnregisters` / 1 | Verfügbarkeit, neue Identität bei Documentwiederkehr und Service-Detach. |
| `AttachDetachAndDocumentSwapHaveDistinctIdentities` / 1 | Registrierung und Dokumentlebensdauer im regulären Ablauf. Kein ausstehender ExecuteEditor-Start (R1). |
| `RevisionsTrackTypingUndoRedoHistoryAndOutputsWithoutReadEffects` / 1 | Reale Revisionen/Dirty, regulärer Undo/Redo, tatsächliche History; Ausgabe ist hier eine direkte Documenteinfügung. Assertions prüfen monotones Wachstum, nicht jede denkbare reentrante Mutationsfolge. |
| `EditsAreExactSingleUndoGroupsAndNoOpsPreserveRevision` / 3 | Alle Modi, echte einzelne Undo-Aktion, Redo und No-op ohne Dialog/Revision. Gut; keine Changed-Callbacks, die Dokumentidentität wechseln. |
| `Utf16OffsetsDoNotSplitSurrogatesOrCrLf` / 9 | Präzise positive/negative UTF-16-Grenzfälle einschließlich −1 und Überlauf des Textendes. |
| `ReplaceApprovalBindsContentAndRechecksRevision` / 2 | Tatsächlich offener Approval-Task mit Benutzeredit, exakte Inhalte/Principal und sichere Ablehnung. |
| `ReplaceCannotApplyToSuccessorAndMissingOwnerFailsClosed` / 1 | Zustimmung nach Detach/Reattach sowie testweise fehlende Bestätigung. Der fehlende Owner wird durch den Confirmation-Adapter simuliert; echte Dialogfälle stehen separat. |
| `SnapshotIsAtomicAcrossBackgroundReadsAndDispatcherWrites` / 1 | Worker-Reads gegen 40 UI-Schritte mit konsistentem Text/Länge/Revision. |
| `ExecuteEditorUsesImmutableBookAndRegisterWithNoSourceReinsertion` / 1 | Unveränderlicher Snapshot trotz weiterem Tippen, exaktes out:-Skipping, Variablenpräsentation, terminaler Retry/Fingerprintkonflikt. Kein Detach-/Documenttausch-Rennen. |
| `EditorExecutionRechecksStateAndRevisionAndRejectsExternalExecution` / 1 | StateConflict/RevisionConflict und externe Sperre auch mit Epoche/Rechten; sinnvoller Sicherheitsnachweis. |
| `SaveIntentPrecedesDialogAndStaleRevisionNeverDeletes` / 2 | Produktiver VM-Orchestrator mit künstlichen Picker-/Overwrite-Waits; Snapshot vor beiden Grenzen und Sentinel unverändert. Testet nicht den echten nativen Picker/StorageProvider. |
| `SaveLockCoversActualMutationsDuringDelayedDelete` / 8 | Reale Lease und Deletepause. `typing` ruft Document.Insert statt Tastatur auf; `paste` ruft SelectedText statt Clipboard-Paste auf; `output` ist eine direkte Einfügung. Undo/Redo benutzen korrekt Editor.Undo/Redo und prüfen Nutzbarkeit danach. Keine gleiche leere Textzuweisung (R2), keine Lease-Propertyreentrancy (R3). Eigene zusätzliche Eingabe-/Clipboard-/Keyboardkontrollen ergänzen diese Grenze. |
| `SavePreservesAsciiWhitespaceLfAppendAndConservativeDirty` / 1 | Reale Dateibytes, Unicode-Fallback, LF/Whitespace und direkter synchroner Append; sinnvoller Bestandsvergleich. |
| `SaveFailuresReleaseBothLocksWithoutRollbackOrCleanClaim` / 2 | Deleteexception und echter Openfehler nach tatsächlichem Delete, beide Sperren frei; sinnvoll. Kein kontrollierter Fehler erst beim Stream.Dispose, der durch statische Ownershipprüfung abgedeckt wurde. |
| `ClosingAndCancellationDrainBegunCommitIncludingDetach` / 1 | Hält bereits angenommene Commitarbeit offen, prüft Service-Drain und Detach. Kein nativer Closingnachweis, keine Reentrancy vor Completionveröffentlichung (R3). |
| `SaveRejectedBeforeCommitHasNoTargetEffects` / 5 | Vorab bekannte Verweigerung, Busy, Cancellation, Detach, Shutdown ohne Zieldateieffekt. Keine unmittelbar innerhalb der Leaseaufnahme eintreffenden Lifecycle-Ereignisse. |

Summe: **47 Fälle**. Keiner der vorhandenen Tests wurde für den Review verändert oder abgeschwächt. Die fünf Findings folgen aus zusätzlicher tatsächlicher Implementierungsprüfung und ausführbaren Gegenproben, nicht allein aus fehlenden Testnamen.

## 6. Sicherheitsbewertung von Editor, Save und Dateigrenzen

**Externe Ausführung:** Der zusätzliche explizite Source-Check hält ExecuteEditor für externe Caller geschlossen. Aktivierungsepoche und execution.run allein öffnen keinen Interpreterweg; IncludeText bleibt verboten. Die eigenen aktivierten/autorisierten Gegenproben liefern Forbidden. Ohne registrierten Editor bleibt die bekannte EditorUnavailable-Priorität erhalten. Es gibt keinen vom MCP-Client gesetzten approved-/force-Schalter. R1 ist eine lokale Lifecyclelücke, keine belegte Umgehung dieser externen Sperre.

**Save vor Commit:** SaveIntent entsteht synchron vor Aufruf des Pickerdelegates und damit vor dem ersten relevanten await der Befehlskette. Er enthält einen konkreten immutable EditorSnapshot; Picker und Überschreibdialog halten keine Admission. SaveTarget verbindet ausgewählten lokalen Pfad, konkretes Delete-Delegate des gewählten Storagehandles und ausdrücklich erteilte Überschreibentscheidung. Fehlende/andere Antwort als Yes wird bereits in Sidebar abgelehnt. Vor Delete/Open werden normalerweise Admission, Revision, Identität und Lifecycle geprüft. Veraltete Revision, Busy und vorab bekannte Ablehnung/Cancellation/Detach/Shutdown haben nach frischen Tests keinen Zieldateieffekt. R3 korrigiert die noch offene reentrante Grenze innerhalb dieses Prüfschritts.

**Begonnener Commit:** Admission und Lease bleiben über Delete, Worker-Write und dessen Dispose erhalten. `WriteSnapshot` liest ausschließlich den bestätigten String. Der Task.Run-Aufruf umfasst die gesamte synchrone Writerfunktion einschließlich der using-Disposals; der äußere finally gibt erst nach Taskabschluss die Lease auf dem UI-Dispatcher und anschließend Admission/Completion frei. Damit gibt es im gelesenen Code keine zusätzliche Cancelprüfung zwischen Delete und Open/Write/Dispose. Der eigene Shutdown-nach-Delete-Nachweis bestätigt diesen wichtigen positiven Pfad. Ein kontrolliert angehaltener tatsächlicher Stream.Dispose wurde nicht behauptet; diese letzte Grenze wurde am realen using-/Task-/finally-Aufbau geprüft.

**Undo-/Redo-Abgrenzung:** Die produktiven AvaloniaEdit-Defaultbindings werden wirklich entfernt und durch vor dem Stackzugriff prüfende Commandbindings ersetzt. Editor.Undo/Redo und Ctrl+Z/Ctrl+Y verbrauchen unter Lease in den eigenen Proben keine Historie; danach funktioniert Undo/Redo. Der direkte rohe `Document.UndoStack.Undo()` bleibt dagegen nachweislich unsicher: Changing kommt nach dem Stackpop. Dies ist im Validierungsbericht ausdrücklich als nicht unterstützter Hostweg abgegrenzt und wird hier nicht als neu eingeführter zusätzlicher Fehler gezählt. Die vollständige Bibliotheks-API ist deshalb nicht generell geschützt. R2 ist gesondert relevant, weil es über die ausdrücklich geprüfte direkte Textzuweisung am produktiven Editor läuft, ohne rohen UndoStack-Aufruf an der Fehlerstelle.

**Weitere Mutationswege:** Wirkliche direkte Insert/Remove/Replace-/SelectedText-Änderungen und Documenttausch werden vor Text-/Anchoränderung abgefangen. Die eigenen Return-, Textinput-, Clipboard- und Commandproben ergänzen die Testmatrix. Headless-Textinput prüft den TextEntering-Weg, keinen nativen Betriebssystem-IME-Kompositionszyklus. History und neue MCP-Edits treffen die gemeinsame Busy-Grenze. Die alte Importprobe bestätigt den Schutz des Nachfolgeeditors; die gebundene Return-Anchorprüfung beachtet Document und VMLifecycle. R1 und R4 bleiben die konkret reproduzierten Ausnahmen.

**Dateisemantik:** Der Writer öffnet weiterhin FileMode.Append und schreibt jede nichtleere/nicht ausschließlich aus Whitespace bestehende physische Zeile mit Encoding.ASCII und einem LF. CRLF und Unicode-Fallback sind in den frisch ausgeführten Byteprüfungen enthalten. Bestätigtes DeleteAsync bleibt vor Write, direkter synchroner Append benutzt denselben Koordinator. Es gibt keinen atomaren Replace, kein CryptoScript-Save/CreateNew und keinen pauschalen Clean-Nachweis. Der produktive Sidebar benutzt weiterhin den Pfad desselben ausgewählten Storagehandles, dessen Delete-Delegate bestätigt wurde; keine durch MCP-01.4 neu eingeführte inkonsistente Zielumleitung wurde gefunden. Die bisherige File.Exists-/Storagehandle-/Pfadsemantik verhindert keine externen Dateisystemrennen und behauptet das auch nicht.

**Fehler und Drain:** Der reale Openfehler nach erfolgreichem Delete propagiert zum Aufrufer; der bestehende Sidebar-Guard stellt generisch einen Fehlschlag dar. Ein Rollback wird nicht behauptet. Die alte Datei kann verloren sein. In der eigenen Probe bleiben Snapshot/Dirty unverändert und beide Sperren werden frei. Save besitzt keine executionId, keinen Interpreter-Scope und erhöht stateVersion nicht. Status zeigt über IsBusy auch die Save-Admission. Keine zusätzliche unbeobachtete Taskexception oder ein Dispatcher-/Interpreter-Deadlock wurde in den kontrollierten normalen Abschluss-, Fehler- und Cancellationpfaden nachgewiesen; R3 betrifft eine zu frühe Drainantwort.

## 7. Bestehende MCP-01.2-/MCP-01.3-Verträge und Risiken

Die früher korrigierten MCP-01.3-R1–R4 wurden nicht durch eine zweite Registry ersetzt oder sichtbar zurückgebaut. Timerstate/Retention, Annahmerollback, Callbackdrain vor terminaler Publikation und der tatsächliche Runner-Eintrittsmarker bleiben im bestehenden Code erhalten. Die vollständige frische GUI-Suite enthält die alten 35 Registryfälle, 30 Korrekturfälle, MCP-01.2-Admission-/Diagnose-/Passwortfälle und die übrigen bisherigen GUI-Regressionen. Sie bestehen. Dies ist ein aktueller Integrationsnachweis; die ursprünglichen GC-/Timer-Fehlerinjektionsproben des MCP-01.3-Re-Reviews werden hier nicht als erneut unabhängig ausgeführte Einzelproben ausgegeben.

Der ungeklärte bekannte `HeadlessUnitTestSession.Dispose`-Race bleibt getrennt bewertet. In diesem Review trat keine Dispose-NRE auf. Daraus folgt weder eine Ursachenklärung noch eine generelle Reparatur des Avalonia-Races. Kein Master-Backport wurde übernommen. Die nachgewiesenen fünf aktuellen Fehler benötigen diesen Race nicht und wurden in regulär abgeschlossenen Headless-Sessions beobachtet.

Weitere bleibende Grenzen: keine neue macOS-/Linux-Ausführung, kein nativer Desktop-/StorageProvider-/IME-/Closing-Smoke und kein realer Installed-Codex-/HTTP-End-to-End-Nachweis. Ein nie zurückkehrendes Delete oder notwendiger Callback kann Drain weiter absichtlich offen halten. Prozess-/Betriebssystemabbruch kann einen begonnenen nichttransaktionalen Save unterbrechen. Externe Dateirennen, fehlende verlustfreie Save-Baseline und die bestehende Print-/Info-Multicast-Grenze bleiben dokumentiert. Die erfolgreichen inkrementellen Builds behaupten keine Beseitigung der zuvor bekannten Nullable-Warnungen.

## 8. Übergabe und Entscheidung

Vor einem Commit R1–R4 beheben und durch gezielte Regressionen sowie erneuten betroffenen GUI-Lauf verifizieren; R5 ebenfalls korrigieren. Dabei die positiv nachgewiesenen Grenzen erhalten: keine neue Cancel-Lücke nach Delete, bestehende Commandhistorie, unveränderliche Save-/Execution-Snapshots, gleiche Dateisemantik, fail-fast Admission und weiterhin vollständig gesperrte externe Ausführung.

MCP-01.5 bleibt ein gesonderter Auftrag für authentisierte Caller/Grants, auftragsbezogene lokale Ausführungsbestätigung, bewusste Ausgabeoffenlegung und produktive Tool-/HTTP-Anbindung. Die externe ExecuteEditor-Sperre darf erst zusammen mit diesen Schutzmaßnahmen geöffnet werden. R1 darf nicht in diese Phase verschoben werden, weil die aktuelle lokale Implementierung bereits den geforderten Editor-Lifecyclevertrag verletzt.

MCP-01.6 bleibt ein gesonderter Auftrag für natives Closing/ShutdownRequested, geordnetes StopAccepting/BeginShutdown/Drain, Dialog-/HTTP-Lifetime und finale Desktopfreigabe. R3 betrifft den bereits hier angebotenen Service-Drain und ist vor dessen Übergabe zu korrigieren; eine native Closingintegration würde die fehlende Completionveröffentlichung nicht automatisch heilen.

**CHANGES REQUIRED.** Die bestandenen 8 141 regulären Tests und die positiven eigenen Kontrollen belegen viel erhaltenes Verhalten, widerlegen die fünf unabhängig reproduzierten Findings jedoch nicht. Dieser Review endet mit dem Bericht; er nimmt keine Korrekturen und keinen Commit vor.


---

## 9. Korrekturstatus R1–R5 – 24.10.2026, vom Implementierer ergänzt

**READY FOR RE-REVIEW; keine unabhängige Freigabe.** Die ursprüngliche Reviewentscheidung **CHANGES REQUIRED**, alle fünf Findings R1–R5, ihre damaligen Gegenproben und die unabhängige Beurteilung in den Abschnitten 1–8 bleiben unverändert als Historie erhalten. Dieser Anhang dokumentiert ausschließlich die beauftragte Korrektur und deren Validierung durch den Implementierer. Er ersetzt nicht den gesondert folgenden unabhängigen Re-Review.

### 9.1 Verifizierter Ausgangsstand und Änderungsgrenze

Vor der ersten Korrektur wurden Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0` und die vorhandenen uncommitted MCP-01.4-Änderungen bestätigt. Der Ausgangssnapshot umfasst 747 Quelldateien/Dokumente. Die beiden Originalberichte sind bytegetreu gesichert; ihre ursprünglichen Bytes bleiben das unveränderte Präfix der ergänzten Dateien.

Geändert wurden nur die unmittelbar notwendigen sechs Produktionsdateien `Controls/WorkbenchTextEditor.cs`, `Services/Automation/EditorBridge.cs`, `Services/Automation/WorkbenchEditorOperations.cs`, `Services/Automation/WorkbenchSaveCommit.cs`, `Services/Automation/WorkbenchAutomationService.cs` und `ViewModels/CryptoScriptEditViewModel.cs`, jeweils unter `CryptoWorkBenchAvalonia/`. Hinzugekommen ist ausschließlich `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs` als neue Testquelle. Die beiden hier beauftragten Dokumente erhielten getrennte Nachträge. Alle bestehenden Tests einschließlich der bisherigen 47 EditorAutomationTests bleiben unverändert.

Keine Änderungen an Core, Abstractions, MCP-Transport, Sprache, Paketen, csproj-Dateien oder Solutions; keine zweite Pipeline oder Variablensammlung, keine neue Toolregistrierung/produktive HTTP-Aktivierung, keine vorgezogene externe Ausführungsfreigabe. Kein Commit, Push, Merge, Rebase oder Reset, keine Masterübernahme und kein MCP-01.5. `.codex/config.toml` wurde weder gelesen noch verändert.

### 9.2 Korrekturen und neue Regressionen

| Finding | Korrektur im aktuellen Stand | Neue Testnachweise in `EditorReviewCorrectionTests.cs` |
| --- | --- | --- |
| **R1** | `WorkbenchEditorOperations.cs:206–228` verknüpft die Identitätsprüfung über RequestCancel mit der serviceeigenen Execution-CTS und Cancelled-Outcome. `WorkbenchAutomationService.cs:47–59` prüft diesen Token nach beforeLine und verhindert den tatsächlichen Pipelineaufruf. Stale Präsentation wird nach Callbacks erneut geprüft. Vorstart: Effects.None/Version unverändert; nach früherer Zeile: deren Effekte bleiben, keine weitere Zeile. Erst nach einem bereits vollständig ausgeführten letzten Runner erfolgender Detach schreibt einen erfolgreichen Abschluss nicht rückwirkend um. Registry, Admission und Fingerprints bleiben erhalten. | :82 vier Detach-/Swap-/Vorstart-/Zwischenzeilenfälle mit idempotentem Retry; :111 zwei erfolgreiche Final-Runner-Kontrollen. **6 Fälle.** |
| **R2** | `WorkbenchTextEditor.cs:45–99`: Guard vor TextArea-Beobachtern am tatsächlich gebundenen Document; UpdateStarted verwirft auch gleiche leere/nichtleere Textzuweisung und Clear vor Historieneffekten. Das bereits von AvaloniaEdit eröffnete Update wird exakt ausgeglichen. Kein versteckter Setter, Stackaustausch oder synthetische Reparaturgruppe. Der geerbte Basistypzugang ist mitgeprüft. Produktive Undo-/Redo-Kommandos bleiben erhalten. | :134 zwölf Empty-/Undo-/Redo-/Concrete-/Base-/Clear-/Document-/SelectedText-Varianten; :174 zwei identische nichtleere Setter. Historie wird nach Save tatsächlich ausgeführt, IsInUpdate und Snapshot bleiben korrekt. **14 Fälle.** |
| **R3** | `WorkbenchSaveCommit.cs:50–59` veröffentlicht Completion vor Lease-/IsReadOnly-Callbacks und validiert danach Cancellation, Lifecycle, Identität und Revision erneut. :74–89 finalisiert Rollback/Freigabe/Signal auch bei Fehler; :95–125 erweitert denselben Ownership-Schutz auf den direkten synchronen Append. Ein vor Commit eintreffender Shutdown verhindert Delete/Open; nach tatsächlichem Delete bleibt Write einschließlich Dispose bis Abschluss geschützt. | :196 sechs reentrante Shutdown-/Drain-/Dispose-/Detach-/Cancel-/Throw-Initialisierungen; :233 synchroner Append; :249 tatsächliches Delete vor Shutdown/Cancel/Detach mit erfolgreicher Finalisierung. **8 Fälle.** |
| **R4** | `WorkbenchTextEditor.cs:100–143` hält eine synchrone Identity-Lease, sperrt Document-/Editorwechsel und erlaubt die eine autorisierte Textänderung erst nach äußeren UpdateStarted-Callbacks. `EditorBridge.cs:77–96` und VM-Detach/-Setter halten Registrierung/Referenz stabil und verschieben Lifecycleende. `WorkbenchEditorOperations.cs:100–148` kopiert die Antwort vor Freigabe für die angefragte editorId; schon ausgeführte Texteffekte werden bei werfenden Callbacks nicht als wirkungsloser Fehler oder als Mutation eines Nachfolgers gemeldet. | :275 vierzehn Changed-/UpdateFinished-Varianten für Document-/Editor-/Viewwechsel, Detach, Same-Document-/Service-Reentrancy und Exception; :326 zwei UpdateStarted-Kontrollen gegen das Stehlen der autorisierten Änderung. Jeweils anschließender zulässiger Edit, geschlossene Gruppen/Sperren und unberührter Nachfolger. **16 Fälle.** |
| **R5** | `CryptoScriptEditViewModel.cs:79–83` initialisiert Status und Diagnosen über BeginExecution; `WorkbenchEditorOperations.cs:217–221` ruft dies genau einmal im tatsächlich neu angenommenen Worker nach Identitätsprüfung auf. Keine Initialisierung pro Buchzeile, bei Ablehnung oder idempotentem Retry. Vorige immutable Registry-Diagnosen bleiben erhalten. | :352 semantischer und Syntax-Vorlauf plus erfolgreicher Folgelauf; :375 gemeinsame Buchdiagnosen und Retry ohne Reset; :390 Busy-/RevisionConflict ohne Status-/Diagnoseänderung. **5 Fälle.** |

Insgesamt **49 neue Fälle in 12 Methoden**, mit echter XAML-View, tatsächlichem WorkbenchTextEditor und gemeinsamer Hostinfrastruktur. Keine bestehende Probe wurde als abgeschwächte Ersatzversion übernommen; die neuen Tests ergänzen die alten 47.

### 9.3 Wiederholung der ursprünglichen unabhängigen Gegenproben

Die unveränderte vom unabhängigen Reviewer erstellte Quelle `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-independent-review/Program.cs` wurde erneut gegen den Korrekturstand gebaut und ausgeführt. **Dies ist eine Wiederholung durch den Implementierer, kein unabhängiger Re-Review.** Die ursprünglichen Logs bleiben erhalten, neue Ergebnisse liegen in `mcp014-corrections/original-probes-final.log`.

```text
EXEC_False: state=Cancelled; effects=None; variable=False
EXEC_True: state=Cancelled; effects=None; variable=False; text=successor
SAVE_REENTRANT_SHUTDOWN: error=ShuttingDown;
  drainCompletedInsideCallback=False; deletes=0; text=sentinel
EMPTY_TEXT_HISTORY: before=True; after=True; error=ExecutionBusyException
EDIT_REENTRANT_SWAP: ok=True; responseId == requestedId == actualEditorId;
  modified=True; text=old edit
DIAGNOSTIC_RESET: previous=1; currentGui=0; currentResult=0;
  hasErrors=False; state=Completed
```

Die ID-Gleichheit in der Darstellung der ReentrantSwap-Zeile ist eine Zusammenfassung der im Log konkret protokollierten identischen GUIDs; das ursprüngliche Document bleibt das bearbeitete Document. Es wird kein übernommenes Nachfolgedokument als Erfolg gemeldet.

Weiterhin erfolgreich sind die unveränderten positiven Kontrollen für Input-/Clipboard-/History-/Command-Sperren, produktives Undo/Redo nach Save, Drain nach tatsächlich begonnenem Delete einschließlich Cancel/Detach, Cancellation vor Commit ohne Delete, Epochwiderruf und externe ExecuteEditor-Sperre, idempotenter Retry ohne erneuten Print, Writefehler nach Delete mit freigegebenen Sperren und alter Import ohne Änderung des Nachfolgeeditors.

Die rohe UndoStack-Negativkontrolle bleibt wie beauftragt abgegrenzt: direktes `Document.UndoStack.Undo()` kann weiterhin den Stack vor Ablehnung verbrauchen und wirft in diesem Lauf InvalidOperationException. Dies ist **keine** Freigabe beliebiger roher Bibliotheksoperationen. R2 betrifft den tatsächlichen Textsetter inklusive Basistypreferenz; dieser ist jetzt geschützt. Die produktiven Editor-Undo-/Redo-Kommandos bleiben die unterstützte Grenze.

### 9.4 Vollständige Validierung und Zwischenfehler

Alle folgenden Ergebnisse stammen aus diesem Korrekturlauf. Logs/TRX liegen unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-corrections/`, Ablauf und Exitcodes in `validate.ps1` und `validation-results.json`.

| Prüfung | Ergebnis |
| --- | --- |
| Restore beider Solutions | erfolgreich |
| Release-Build beider Solutions | erfolgreich; keine neue Warnungsstelle, acht bestehende GUI-Nullable-Warnungen |
| Neue Korrektursuite separat | **49 bestanden**, `results/corrections-final.trx` |
| Alte EditorAutomationTests separat | **47 bestanden**, `results/existing47-final.trx` |
| Vollständige GUI-Suite | **434 bestanden**, 0 Fehler/Skips, `results/gui-final.trx` |
| Vollständige Core-Suite | **7 667 bestanden**, 4 bisherige Skips, 0 Fehler, `results/core-final.trx` |
| Vollständige reguläre MCP-Suite | **89 bestanden**, 0 Fehler, `results/mcp-final.trx` |
| Unveränderte Originalgegenproben | vollständig erneut ausgeführt, Ergebnisse oben und in `original-probes-final.log` |
| Referenzen / `git diff --check` / Änderungsscope | erfolgreich; bestehende Tests unverändert, ursprüngliche Dokumentpräfixe bytegetreu erhalten |

Gesamt: **8 190 bestandene reguläre Fälle**, ohne doppelte Addition der in der GUI-Suite enthaltenen alten 47 und neuen 49 Editorfälle. Die vier Core-Skips bleiben Darwin-ACL und drei Windows-Symlinkfälle ohne passendes Privileg. Expliziter Hardwarebenchmark und Installed-Codex-Roundtrip wurden nicht aktiviert.

Zwischenfehler wurden nicht als bestandene Ergebnisse ausgegeben: Ein lokaler C#-Variablenkonflikt und ein überflüssig gewordener Konstruktorparameter wurden im neuen Code korrigiert; der neue Testzugriff auf die ReadOnlyObservableCollection wurde auf INotifyCollectionChanged umgestellt. Der erste ausführbare neue Lauf hatte eine falsche Erwartung im Syntaxfall (semantische Collection statt bisherigem Syntax-Statustext); dieser neue Test prüft jetzt Status und immutable SyntaxError-Ergebnis. Der fehlgeschlagene TRX `corrections-second.trx` bleibt erhalten. Bestehende Tests und Syntaxpräsentation wurden dafür nicht verändert. Die vier Obsolete-Warnungen der unverändert wiederholten externen Tastaturproben sind protokolliert.

In diesem Korrekturlauf trat keine HeadlessUnitTestSession.Dispose-NRE auf. Die bekannte sporadische Headless-Lifecycle-Grenze aus MCP-01.3 bleibt ursächlich ungeklärt; kein Master-Backport und keine allgemeine Behebung wird behauptet. Weitere Einzelheiten der Korrekturen, Befehle und Zwischenstände stehen im neuen Korrekturabschnitt von `MCP-01.4-validation.md`.

### 9.5 Verbleibende Grenzen und nächste Entscheidung

Gemeinsame Pipeline, ein globaler Variablenbestand, fail-fast Admission, serviceeigene Execution-Cancellation, Runner-/Passwortquarantäne und immutable Ergebnisse/Retention bleiben erhalten. Der Writer behält ASCII, Whitespace-Filter, LF, Append und bestätigtes Delete vor Write. Kein atomarer Replace, Rollbackversprechen oder pauschales Clean. Externe Dateisystemrennen und ein nicht zurückkehrendes Delete bleiben Grenzen. Der frühe Dokumentupdate-Guard ist an der gepinnten AvaloniaEdit-Version geprüft; Paketupgrades benötigen entsprechende Regressionen.

MCP-01.5 bleibt für authentisierte lokale Ausführungsbestätigung/Transportanbindung zuständig; externe Ausführung bleibt gesperrt. MCP-01.6 muss nativen Closing-/HTTP-Shutdown vor Dispatcherende vollständig anbinden. Headless-Tests ersetzen keine native IME-/OS-Closing-Garantie; neue macOS-/Linux-CI wurde nicht behauptet.

**Entscheidung des Korrekturlaufs: READY FOR RE-REVIEW.** R1–R5 sind implementiert und durch neue Regressionen, die wiederholten Originalproben und vollständige Suiten belegt. Der Implementierer erklärt den Stand ausdrücklich nicht selbst als unabhängig freigegeben. Ein neuer unabhängiger Re-Review folgt separat. Keine Korrektur über R1–R5 hinaus, kein Commit und kein MCP-01.5-Beginn.


---

## Final Re-Review – 24.10.2026

**Entscheidung: CHANGES REQUIRED. R1, R3 und R5: VERIFIED FIXED. R2 und R4: NOT RESOLVED im vollständig beauftragten Reentrancy-/Callbackvertrag.** Die ursprünglichen fünf konkreten Gegenproben zeigen inzwischen korrigiertes Verhalten. Zwei zusätzliche, unabhängig reproduzierte **Medium**-Findings R6/R7 verhindern die abschließende Freigabe. Keine neuen belegten Critical-/High-Findings. Die 49 neuen und 47 bisherigen Editorfälle sowie die anschließend zusätzlich ausgeführte vollständige GUI-Suite bestehen unverändert.

### Unabhängigkeit, Stand und geprüfter Umfang

Derselbe ursprüngliche Reviewer `independent_mcp014_review` führte diesen abschließenden Re-Review durch. Er war an den Korrekturen nicht beteiligt, erhielt den vollständigen neuen Auftrag und prüfte den tatsächlichen Quellcode selbst. Der implementierende Hauptagent übernimmt nicht die fachliche Bewertung dieses Abschnitts; er kontrolliert ausschließlich Provenienz, Bytehistorie und Dateigrenzen.

Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, frisch bestätigter HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Verbindliche Grundlagen: MCP-00-R1-Architektur und Implementierungsplan, der vollständige MCP-01.4-Validierungsbericht einschließlich Korrekturanhang sowie dieser bisherige Review einschließlich Implementierernachtrag. Die bereits im ursprünglichen Review gelesenen unveränderten MCP-00-Verträge wurden an den maßgeblichen Editor-/Save-/Lifecycleabschnitten erneut abgeglichen. Der Korrekturanhang wurde als Behauptung des Implementierers behandelt und nicht als unabhängiger Nachweis übernommen.

Gelesen und geprüft wurden die sechs korrigierten Produktionsdateien `Controls/WorkbenchTextEditor.cs`, `Services/Automation/EditorBridge.cs`, `WorkbenchEditorOperations.cs`, `WorkbenchAutomationService.cs`, `WorkbenchSaveCommit.cs` und `ViewModels/CryptoScriptEditViewModel.cs`, sämtliche 49 neuen `EditorReviewCorrectionTests` und die unveränderten 47 `EditorAutomationTests` einschließlich ihres produktiven Headless-Harness. Die vorhandenen Registry-/Admission-/Drain-/Presentation-Verträge wurden im Zusammenhang berücksichtigt. Gegenüber der Übergabe wurden keine Produktiv- oder Testquellen geändert.

Der bisherige Bericht war vor diesem Append 52 078 Bytes lang. Dieser Abschnitt wurde ausschließlich angehängt; alle bisherigen Bytes einschließlich der historischen Entscheidung CHANGES REQUIRED und des Implementierernachtrags bleiben erhalten. Neue Quellen/Logs liegen ausschließlich unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-final-independent-review/`. Keine Produktiv-/Teständerung, kein Commit/Push/Merge/Rebase/Reset, keine Masterübernahme, kein MCP-01.5. `.codex/config.toml` wurde weder gelesen noch verändert.

### R1 – VERIFIED FIXED: Identität steuert den tatsächlichen Runnerstart

**Aktuelle Quellen:** `WorkbenchEditorOperations.cs:206–228`, `:230–241`; `WorkbenchAutomationService.cs:47–59`.

`ContinueEditor` registriert den Abbruch bei ungültiger Editoridentität über `RequestCancel` an der serviceeigenen Execution-CTS und setzt den registrierten Outcome. Die Buchschleife prüft den Token jetzt unmittelbar nach dem UI-beforeLine und vor dem tatsächlichen Pipelineaufruf. Der boolesche Callbackwert allein wäre im bestehenden Task-Vertrag nicht ausreichend; die zusätzliche serviceeigene Cancellationprüfung ist hier die wirksame Fortsetzungsgrenze. Auch reentrantes Invalidieren aus BeginExecution/Begin wird danach erneut geprüft. Request-Wait-Cancellation wurde nicht als Executiontoken eingeführt.

Die unveränderten eigenen ursprünglichen Vorstartproben wurden in diesem Re-Review selbst frisch ausgeführt:

```text
EXEC_False: state=Cancelled; effects=None; variable=False
EXEC_True: state=Cancelled; effects=None; variable=False; text=successor
```

Die sechs frisch bestandenen R1-Regressionen treffen beide Vorstart-/Zwischenzeilenvarianten, prüfen Effects.None/Version 0 vor dem ersten Runner und Possible/Version 1 nach einer tatsächlich ausgeführten Zeile sowie die Abwesenheit der Folgevariable. Idempotenter Retry liefert dasselbe Resultat. Die beiden Kontrollen zum erst nach dem erfolgreich beendeten letzten Runner erfolgenden Identitätswechsel bleiben Completed; der Abschluss wird nicht rückwirkend umetikettiert. Eine externe Ausführung wird weiterhin abgewiesen. Kein verbleibender R1-Blocker wurde nachgewiesen.

### R2 – NOT RESOLVED: ursprünglicher Setterfehler behoben, Updateabschluss noch nicht allgemein reentranzsicher

**Aktuelle Quellen:** `WorkbenchTextEditor.cs:44–56`, `:68–98`, `:145–170`; produktive Undo-/Redo-Bindings `:19–40`.

Der neue frühe UpdateStarted-Guard wird am tatsächlichen Dokument vor den TextArea-Beobachtern installiert. Er erfasst auch geerbte Textsetter/Clear über eine als `TextEditor` deklarierte Referenz. Die ursprüngliche eigene `EmptyTextHistory`-Probe bestätigt jetzt:

```text
EMPTY_TEXT_HISTORY: before=True; after=True; error=ExecutionBusyException
```

Alle 14 R2-Regressionen bestehen frisch: zwölf Varianten für Undo/Redo, concrete/base Text/Clear, Document.Text und SelectedText sowie zwei gleiche nichtleere Textzuweisungen. Sie prüfen erhaltene Historie, Snapshot/Caret und tatsächliches Undo/Redo nach Save. In diesen abgewiesenen Standardupdates bleibt IsInUpdate=false. Die Ursache des ursprünglichen leeren Setterfehlers ist damit tatsächlich behoben; es wurde keine Property nur versteckt und kein Ersatzstack eingeführt.

Der vollständig geforderte Vertrag eines sauber und ohne neue Reentrancyfolgen abgewiesenen Updates bleibt jedoch durch **R7** verletzt: Das aus dem Guard synchron gerufene EndUpdate löst UpdateFinished aus und kann einen ansonsten terminierenden bedingten Hosthandler rekursiv wieder aufrufen. Deshalb erfolgt keine uneingeschränkte R2-Freigabe. Die rohe UndoStack-Grenze wird davon getrennt beibehalten; R7 benötigt keinen rohen Stackaufruf.

### R3 – VERIFIED FIXED: Ownership vor beobachtbaren Leaseänderungen

**Aktuelle Quellen:** `WorkbenchSaveCommit.cs:44–61`, `:74–89`, `:100–125`; `EditorBridge.cs:141–142`; unveränderter Drain in `WorkbenchAutomationRegistry.cs:582–594`.

Die Save-Completion wird jetzt nach Admission und initialer Bindungsprüfung vor EditorBridge.Lock veröffentlicht. Reentrante ReadOnly-Propertycallbacks sehen deshalb bereits drainbare Arbeit. Hinter den Callbacks werden Cancellation, Lifecycle, Registrierung und Revision erneut geprüft, bevor Delete/Open beginnen. Der direkte synchrone Append veröffentlicht ebenfalls früh Ownership und prüft die Bindung nach der Leaseaufnahme erneut. Sämtliche untersuchten Initialisierungs-/Cleanupfehler geben Lease und Admission frei und signalisieren die Completion.

Die unveränderte eigene ursprüngliche Gegenprobe liefert frisch:

```text
SAVE_REENTRANT_SHUTDOWN: error=ShuttingDown;
drainCompletedInsideCallback=False; deletes=0; text=sentinel
```

Die acht R3-Regressionen bestehen, darunter reentrantes DisposeAsync, Shutdown/Drain, Detach, Cancel, werfende Initialisierung, Shutdown plus Fehler, synchroner Append und Shutdown nach tatsächlich erfolgtem Delete. Zusätzliche eigene isolierte Proben ergänzen einen Fehler **beim ReadOnly-Zurücksetzen** sowie DisposeAsync aus dem synchronen Append-Callback:

```text
DISPOSE_UNLOCK_THROW: closedEarly=False; deletes=0; error=IOException;
busy=False; locked=False; readonly=False
SYNC_SAVE_DISPOSE: early=False; error=ExecutionBusyException;
text=sentinel; busy=False; locked=False
```

Disposal/Drain beendeten sich nicht innerhalb des initialen Leasecallbacks, warteten aber nach dem abgewickelten Save vollständig ab. Es wurde kein Deadlock und kein verlorenes Completion-Signal nachgewiesen. Die eigene unveränderte positive Probe nach tatsächlichem Delete bestätigt weiterhin offene Drainarbeit, gehaltene Lease trotz Detach sowie anschließendes Write des bestätigten Snapshots trotz Shutdown/Cancel. Kein zusätzlicher Cancelcheck öffnet eine Delete-/Write-Lücke. Kein verbleibender R3-Blocker wurde nachgewiesen.

### R4 – NOT RESOLVED: ursprünglicher Dokumenttausch verhindert, UpdateStarted-Lücke bleibt

**Aktuelle Quellen:** `WorkbenchTextEditor.cs:83–141`; `EditorBridge.cs:70–103`; `CryptoScriptEditViewModel.cs:50–61`, `:96–104`; `WorkbenchEditorOperations.cs:100–145`.

Die Identity-Lease verhindert die ursprüngliche Übertragung auf ein Nachfolgedokument. Document-/VM-Referenzwechsel werden verweigert, Lifecycle-Detach bis nach Aufnahme der Mutationsantwort aufgeschoben und die Antwort ausdrücklich mit der angefragten editorId erstellt. Bereits erfolgte Änderungen werden bei den in den Tests behandelten Changed-/UpdateFinished-Exceptions korrekt ihrer ursprünglichen Identität zugeordnet. Die unveränderte eigene ursprüngliche ReentrantSwap-Probe zeigt frisch identische angefragte/gelieferte/tatsächliche editorId und `old edit` im alten Dokument; es wird kein Nachfolger übernommen.

Die 16 neuen R4-Regressionen bestehen, einschließlich vierzehn Changed-/UpdateFinished-Varianten und zweier UpdateStarted-Fälle für Insert beziehungsweise Documenttausch. Auch die eigene zusätzliche UpdateStarted-IOException-Probe endet ohne Textwirkung mit InternalError, freier Admission, IsInUpdate=false und erfolgreichem Folgeedit. Ein zusätzlich absichtlich werfender VM-Observer beim verzögerten Detach lässt eine bereits realisierte Änderung weiterhin als erfolgreiche Mutation des ursprünglichen Editors sichtbar; Registrierung und Sperren enden korrekt.

Diese Nachweise decken aber **nicht** den wirkungslosen leeren Basistyp-Textsetter aus UpdateStarted ab. Die eigene zusätzliche Probe **R6** hinterlässt bereits angewandten Text, eine fehlgeschlagene Mutationsantwort und eine offene Bibliotheksupdategruppe; Folgeedits bleiben Busy. **R7** zeigt zusätzlich rekursive UpdateFinished-Zustellung während der Identity-Lease. Damit sind die beauftragten vollständigen Same-Document-/Callback-/Updategruppenverträge nicht erfüllt und R4 bleibt NOT RESOLVED.

### R5 – VERIFIED FIXED: einmaliger Präsentationsbeginn je neuem Gesamtlauf

**Aktuelle Quellen:** `EditorBridge.cs:13–18`, `CryptoScriptEditViewModel.cs:79–85`, `WorkbenchEditorOperations.cs:216–221`.

BeginExecution leert die alte Diagnosesammlung einmal im tatsächlich neu angenommenen Worker. Die Zeileninitialisierung leert sie nicht erneut. Die Annahme-/Duplikatprüfungen liegen vor dem Worker, sodass Busy/RevisionConflict und idempotente Retries keine neue Präsentation beginnen. Frühere immutable Registerresultate bleiben unberührt.

Die unveränderte eigene ursprüngliche Probe liefert frisch:

```text
DIAGNOSTIC_RESET: previous=1; currentGui=0; currentResult=0;
hasErrors=False; state=Completed
```

Alle fünf R5-Regressionen bestehen: semantischer und Syntax-Vorlauf, fehlerfreier Folgelauf, mehrere fortsetzbare Buchdiagnosen, Retry ohne Collection-/Statusreset sowie Ablehnungen ohne Präsentationsänderung. Kein verbleibender R5-Blocker wurde nachgewiesen.

### Neues Finding R6 – Medium: leerer Basistyp-Setter aus UpdateStarted vergiftet den Editabschluss

**Dateien/Zeilen:** `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs:83–98`, insbesondere `:85` und `:96`; `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs:114–142`, insbesondere die nur nachträgliche Updatebereinigung `:127`.

**Ursache und Reihenfolge:** Vor der ersten autorisierten Änderung sind IdentityLocked=true, editChangeSeen=false und editWriteAllowed=false. Der frühe UpdateStarted-Guard lässt den äußeren BeginUpdate deshalb passieren. Ein danach laufender UpdateStarted-Observer kann einen geerbten Textsetter ausführen. Für eine bereits leere Textzuweisung gibt es keine Changing-Grenze, an der BeforeChange dies noch abweisen könnte. Das innere Update erzeugt bei bereits offenem äußerem Update auch kein neues äußeres UpdateStarted-Ereignis. Der Setter erreicht den geerbten UndoStack.ClearAll innerhalb der bereits offenen Updategruppe. Die Bibliothek wirft dort `No undo group should be open at this point`; späteres EndUpdate wirft `There are no open undo groups`.

**Eigene isolierte Reproduktion:** Echter WorkbenchTextEditor, zunächst `Text="x"` und `Document.Remove(0,1)`; Text leer, Historie vorhanden. Ein einmaliger UpdateStarted-Observer versucht ausschließlich `TextEditor baseEditor = editor; baseEditor.Text = ""` und beobachtet/fängt die daraus kommende Exception. Anschließend wird ein normal revisionsgebundener Host-Append `"edit"` angefordert. Es gibt keinen direkten UndoStack-Aufruf und keine manipulierte interne Produktionsvariable. Ergebnis in einem frischen Prozess:

```text
STARTED_EMPTY_ISOLATED_True: ok=False; error=InternalError;
text=edit; beforeRev=2; afterRev=3; updateOpen=True;
identity=False; busy=False; nextError=Busy; undo=True
```

Nach Entfernen des Observers wurde ein **neuer** gültig revisionsgebundener Folgeedit tatsächlich ausgeführt und mit Busy abgewiesen. Die prozessweite Admission ist frei; blockierend bleibt das beschädigte Dokumentupdate. Der ursprüngliche Texteffekt ist trotzdem bereits vorhanden. Der eine nachträgliche EndUpdate-Versuch stellt weder einen stabilen Updatezustand noch eine Mutationsantwort für diesen Effekt her.

**Wichtige Herkunftsabgrenzung:** Dieselbe Callbackfolge wurde zusätzlich ohne Host-Identity-Lease über ein direktes Document.Insert als Bibliothekskontrolle ausgeführt. Auch dort zeigt AvaloniaEdit die UndoGroup-Exceptions und IsInUpdate=true. Der Fehler der rohen Bibliothekskombination wurde somit **nicht neu durch die Identity-Lease erfunden**. Das verbleibende Hostproblem besteht darin, dass die ausdrücklich zugesagte Abschirmung dieser direkten Basistyp-/UpdateStarted-Reentrancy sie passieren lässt und anschließend einen unvollständigen Editabschluss hinterlässt. Die Beobachtung ist keine neue CryptoScript-/Runner-/Sprachursache und kein Nachweis eines bereits vorhandenen nativen GUI-Handlers, der genau diesen Setter registriert hat. Ein ohne äußeres Update ausgeführter normaler Basistyp-Textsetter bleibt zulässig; die ursprünglichen Save-Lease-Setterproben sind jetzt korrekt geschützt.

**Auswirkung:** Unterstützte direkte Text-API und beobachtbare Hostcallbacks können einen gültigen MCP-Edit mit tatsächlichem Texteffekt in InternalError überführen und weitere Editor-/Saveoperationen durch IsInUpdate=true blockieren. Dies verletzt die beauftragte Same-Document-/Callbackfehler-/Folgeedit-Garantie von R4. Es wird keine Interpreterzustandsänderung oder externe Ausführungsfreigabe daraus abgeleitet.

**Korrekturvorschlag:** Die vor der autorisierten Änderung möglichen reentranten Setter-/Clear-/Historieneffekte an einer wirksamen Hostgrenze vor dem Stackzugriff abweisen oder diese Callbackmutationen kontrolliert auf einen späteren erlaubten Schritt verschieben. Der Schutz muss auch nested Updates und Basistypreferenzen erfassen; allein Changing und äußeres UpdateStarted genügen hierfür nicht. Eine nachträgliche einzelne EndUpdate-Reparatur ist kein verlässlicher Ersatz. Bei bereits realisiertem Texteffekt muss der Abschluss weiter der ursprünglichen Identität zugeordnet bleiben. Die Bibliotheksgrenze gegebenenfalls mit einer ausdrücklich engeren, tatsächlich durchgesetzten Host-API behandeln, ohne den bisherigen direkten Settervertrag stillschweigend auszunehmen.

**Erforderliche Regression:** Leerer Text mit Undo-/Redo-Historie, geerbter Setter/Clear aus UpdateStarted vor dem autorisierten Insert; keine offene Updategruppe, keine verschwiegene Textwirkung, unveränderte korrekte Identität, funktionierender Folgeedit und weiter nutzbare ursprüngliche Historie. Die vorhandenen zwei UpdateStarted-Regressionen nur mit Insert/Documenttausch decken diese Fallklasse nicht ab.

### Neues Finding R7 – Medium: abgewiesene Updates erzeugen rekursive UpdateFinished-Callbacks

**Datei/Zeilen:** `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs:83–91`, insbesondere `:90`; beim Editor-Edit im Zusammenspiel mit `WorkbenchEditorOperations.cs:114–127`.

**Ursache und Reihenfolge:** Der Guard gleicht einen schon begonnenen abgewiesenen BeginUpdate durch synchrones EndUpdate aus. Dieses EndUpdate löst UpdateFinished aus, während die Save-/Identity-Lease weiterhin aktiv ist. Ein UpdateFinished-Handler, der einen noch nicht erfüllten Textzustand normalisieren will, versucht eine direkte Insert-Mutation. Diese eröffnet ein weiteres Update, das der Guard erneut durch EndUpdate beendet; derselbe Handler wird erneut betreten, bevor der vorausgehende Aufruf seine Busy-Exception überhaupt empfangen kann. Weil alle Inserts verworfen werden, bleibt die Normalisierungsbedingung wahr.

**Eigene isolierte Reproduktion mit tatsächlich terminierendem Kontrollfall:** Der Handler prüft `!document.Text.StartsWith("normalized:")` und versucht dann `document.Insert(0,"normalized:")`; Busy wird aufgefangen. Außerhalb einer Lease beendet derselbe Handler seine Arbeit nach dem Einfügen des Präfixes. Um keinen Prozessabsturz zu provozieren, besitzt ausschließlich die Gegenprobe zusätzlich eine ausdrückliche maximale Callbackzahl. Zwei getrennte Grenzen von 8 und 32 wurden sowohl unter Save-Lease als auch beim Host-Edit ausgeführt:

```text
FINISHED_NORMALIZER_False: limit=12; callbacks=2; maxDepth=2;
rejected=0; text=normalized:plain edit; updateOpen=False
FINISHED_NORMALIZER_True: limit=8; callbacks=8; maxDepth=8;
rejected=7; outerError=ExecutionBusyException; text=plain
FINISHED_NORMALIZER_True: limit=32; callbacks=32; maxDepth=32;
rejected=31; outerError=ExecutionBusyException; text=plain
FINISHED_NORMALIZER_EDIT: ok=True; limit=8; callbacks=8;
maxDepth=8; text=plain edit; updateOpen=False; busy=False
FINISHED_NORMALIZER_EDIT: ok=True; limit=32; callbacks=32;
maxDepth=32; text=plain edit; updateOpen=False; busy=False
```

Die Kontrollbeobachtungen und die absichtlich erreichten Grenzen werden in der Probe zusätzlich durch eigene Require-Prüfungen kontrolliert. Es wurden kein unlimitierter Lauf und **kein tatsächlicher Stackoverflow** ausgeführt. Aus unveränderter Bedingung und wiederholter identischer synchroner Zustellung folgt die Gefahr fortgesetzter Rekursion ohne diese künstliche Probegrenze; dies ist eine begründete Folgerung, kein als ausgeführt ausgegebener Crashnachweis.

**Auswirkung:** Eine abgewiesene Mutation endet mit einem zusätzlichen bedingten Hosthandler nicht zuverlässig als einzelner Busy-Vorgang, sondern kann synchron tiefe wiederholte Callbacks erzeugen und den Dispatcher/Saveabschluss blockieren beziehungsweise die Aufrufstapelgrenze gefährden. Der Save-Drain kann dann wegen noch nicht abgewickelter UI-Callbackarbeit nicht finalisieren. Die vorliegenden begrenzten Läufe enden nach Erreichen des Probelimits sauber und beweisen daher keinen beschädigten Writer oder verlorene Save-Completion. Ein solcher Normalisierer wurde nicht als heute tatsächlich im Produkt registrierter Handler nachgewiesen; die Verletzung betrifft den ausdrücklich geprüften reentranten Host-/Dokumentcallbackvertrag.

**Korrekturvorschlag:** Die Ablehnung/Updatebalance darf nicht erneut eine für dieselbe Operation aktive Abschlussbeobachtung rekursiv ausführen. Eine für die konkrete Bibliotheksreihenfolge belastbare Reentrancygrenze beziehungsweise aufgeschobene Callback-/Mutationsbehandlung ergänzen, dabei die frühe Historienprüfung und das exakt ausgeglichene Bibliotheksupdate erhalten. Nicht nur den Testhandler mit einem einmaligen Boolean versehen: außerhalb der Lease ist der bedingte Normalisierer bereits von selbst terminierend. Bei Bedarf die unterstützten Hostmutatoren/Observer explizit vermitteln, statt die gesamte rohe Event-/Setterkombination als automatisch geschützt auszugeben.

**Erforderliche Regressionen:** Der oben selbstterminierende Normalisierer gegen Save-Lease und Identity-Lease; begrenzte Callbacktiefe unabhängig vom künstlichen Testlimit, einzelner kontrollierter Busy-Abschluss, weiterhin geschlossene Gruppen, erhaltene Undo-/Redo-Historie und abgeschlossener Drain nach Freigabe. Die bestehenden R4-Callbacks setzen `called=true` vor dem Mutationsversuch und unterdrücken dadurch genau dieses Wiedereintrittssymptom.

### Frische Ausführungen und Bewertung der 49 neuen Tests

Die folgenden Ergebnisse stammen aus diesem unabhängigen Re-Review. Die vollständigen vorherigen Korrekturlaufzahlen des Implementierers werden nicht als neue unabhängige Gesamtläufe ausgegeben.

| Frische Ausführung | Ergebnis / neuer Beleg |
| --- | --- |
| Release-Testlauf mit Filter `FullyQualifiedName~EditorReviewCorrectionTests|FullyQualifiedName~EditorAutomationTests` | **96 bestanden**, 0 Fehler/Skips; aus dem TRX separat gezählt **49 Korrekturfälle + 47 bisherige Editorfälle**; `results/final-independent-editor96.trx`, `editor96.log`. |
| Unveränderte ursprüngliche eigene Probequelle und Projekt unter `mcp014-independent-review` | Vollständig selbst erneut ausgeführt; `original-probes.log`. Alle fünf ursprünglichen Fehlerbedingungen zeigen jetzt korrigierte Ergebnisse; positive Kontrollen und Raw-Undo-Negativgrenze bleiben erhalten. |
| Neue eigene zusätzliche Headless-Proben | `Program.cs`/`probe.csproj` im neuen Re-Review-Ordner; `additional-probes-first.log`, `started-empty-isolated.log`, `started-empty-confirmed.log`, `normalizer-isolated.log`. Quellen benutzen echte produktive Klassen und vorhandene Friend-Assembly-Grenze; keine Zielquellen geändert. |
| Vollständige GUI-Suite wegen der neu gefundenen Callbackgrenzen | **434 bestanden**, 0 Fehler/Skips; `results/final-independent-gui434.trx`, `gui434.log`. Die oben separat ausgeführten 96 sind darin enthalten. |
| `git diff --check` sowie Branch-/HEAD-Abgleich | Erfolgreich; erwarteter Branch/HEAD unverändert. |

Alle neuen Logs liegen unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-final-independent-review/`. Die Originalprobe wurde nicht verändert. Das neue eigene Beobachtungsprojekt zählt die künstlich begrenzten Reentrancysymptome; sein erfolgreicher Exit bedeutet, dass die protokollierten Gegenbeobachtungen reproduziert wurden, nicht dass der Produktvertrag erfüllt wäre. Zwei CS8604-Warnungen aus einem ersten zusätzlichen Probe-Log betrafen nullable Loggingparameter im *externen* Probeprojekt und wurden dort vor den isolierten Endläufen beseitigt; keine Produktionswarnung oder Teständerung. Die beschädigten Dokumente wurden pro Harness isoliert; die isolierte Bestätigungsprobe lief in einem separaten Prozess von den regulären Tests. Sie übertrug weder ihr Dokument noch eine gehaltene Admission auf diese Tests.

Die 49 neuen Tests wurden vollständig gelesen. Ihre Verteilung ist 6 R1-, 14 R2-, 8 R3-, 16 R4- und 5 R5-Fälle in zwölf Methoden. Sie verwenden tatsächlich XAML-View, WorkbenchTextEditor, reale Dokumente und Registry. Ihre Assertions treffen die ursprünglichen Fehlerbedingungen: richtige Effects/Version und tatsächliche Variablenabwesenheit, Undo-/Redo-Ausführung nach Save, nicht abgeschlossener Drain während Initialisierung/Commit, sichere ursprüngliche editorId und erfolgreiche Folgeedits, erhaltene immutable Diagnosen. Keine bestehende Assertion wurde im Re-Review abgeschwächt.

Die Aussagegrenzen sind konkret: Die zwei R4-UpdateStarted-Fälle versuchen Textinsert/Documenttausch, aber keinen gleichen leeren Basistyp-Setter mit Historieneffekt. Die Changed-/UpdateFinished-Reentrancyfälle setzen vor dem Versuch ein einmaliges `called`-Flag; sie prüfen deshalb keine von einer weiterhin wahren Textbedingung ausgelöste wiedereintretende Normalisierung. Die 14 R2-Fälle prüfen die gewöhnliche einzelne Ablehnung und Historie, aber keine zusätzliche UpdateFinished-Mutation aus der dabei ausgelösten Abschlusszustellung. Das erklärt, warum die neuen Gegenproben trotz aller 434 grünen GUI-Fälle Fehler nachweisen.

Die vollständigen Core- und MCP-Suiten wurden in diesem Re-Review nicht erneut ausgeführt: Core, Transport und Verträge wurden nicht geändert, die neu bestätigten Befunde liegen ausschließlich in der GUI-/AvaloniaEdit-Updateorchestrierung; die betroffene vollständige GUI-Suite wurde zusätzlich wiederholt. Die zuvor dokumentierten 7 667 Core-/89 MCP-Erfolge und vier Core-Skips bleiben frühere Implementierungsnachweise. Keine neue Gesamtsumme von 8 190 unabhängigen Re-Reviewfällen wird behauptet.

### Architektur, Sicherheitsgrenzen und verbleibende Risiken

Eine gemeinsame Hostpipeline, ein globaler Variablenbestand, fail-fast Admission, serviceeigene Execution-Cancellation und die bisherigen Passwort-/Retention-/Snapshotregeln bleiben erhalten. R1 nutzt dieselbe Registry und keine zweite Ausführung. Die eigenen Originalkontrollen bestätigen weiter idempotenten Retry ohne neuen Print, Epochwiderruf und **Forbidden** für externe ExecuteEditor-Aufrufe trotz Aktivierungs-/Ausführungsrechten. Keine authentisierte lokale MCP-Ausführungsbestätigung, HTTP-Hostaktivierung, neue Toolregistrierung, neue Projekt-/Paketabhängigkeit oder Änderung von MCP-01.1-/CryptoScript-Verträgen wurde vorgezogen.

Die Dateisemantik ist unverändert: ASCII einschließlich Fallback, Whitespace-Zeilenfilter, LF, Append und ausdrücklich bestätigtes Delete vor Write. Eigene positive Proben bestätigen tatsächliches Delete mit anschließendem geschütztem Write trotz Shutdown/Cancellation/Detach sowie echten Openfehler nach Delete mit freigegebenen Sperren und ohne Rollback-/Clean-Behauptung. R6/R7 sind keine neuen Dateitransaktionsbehauptungen. Externe Dateisystemrennen und der mögliche Verlust der alten Datei nach erfolgreichem Delete bleiben Grenzen.

Direkte rohe `Document.UndoStack`-Aufrufe bleiben außerhalb der unterstützten Host-API: Die unveränderte ursprüngliche Negativkontrolle verliert weiterhin einen Eintrag und wirft InvalidOperationException. R6/R7 benötigen dagegen an der Fehlerstelle keine rohen Stackzugriffe; sie benutzen Basistyp-Textsetter beziehungsweise Dokumentinsert aus ausdrücklich untersuchten Callbackgrenzen. Die Herkunft des verschachtelten Setters aus einer fehleranfälligen AvaloniaEdit-Kombination ist bei R6 ausdrücklich abgegrenzt.

Native IME-/StorageProvider-/Closing-Tests, macOS-/Linux-Nachweise und Installed-Codex-/produktive HTTP-Roundtrips wurden nicht neu erbracht. Der bekannte sporadische Avalonia.Headless-Dispose-Race trat in diesen Läufen nicht auf und gilt weiterhin nicht als allgemein behoben. Die Print-/Info-Multicast-Grenze bleibt unverändert. Ein nie zurückkehrender notwendiger Callback/Delete kann Drain offen halten; R7 zeigt zusätzlich vermeidbare rekursive Callbackarbeit. Keine Masterübernahme und kein stiller Paket-/Bibliothekswechsel wurden vorgenommen.

### Abschluss und Übergabe

**CHANGES REQUIRED.** Die konkreten ursprünglichen Symptome von R1–R5 sind durch Korrekturen und frische Gegenproben verbessert; R1, R3 und R5 sind unabhängig VERIFIED FIXED. Die vollständig beauftragten R2-/R4-Update-/Callbackgarantien bleiben wegen der neuen Medium-Findings R6/R7 NOT RESOLVED. Vor einer Freigabe sind diese beiden Grenzen zu korrigieren und mit den isolierten Gegenproben sowie geeigneten Regressionen erneut unabhängig zu prüfen.

MCP-01.5 und MCP-01.6 bleiben eigenständige Folgeaufträge. Die externe Ausführungssperre bleibt bis zur authentisierten lokalen Bestätigung geschlossen; native Closingintegration ersetzt keine korrekte Host-/Updatefinalisierung. Dieser unabhängige Re-Review nimmt keine Reparatur und keinen Commit vor und endet mit diesem Append.


---

## Korrekturrunde 2 – Architekturblocker R2/R4/R6/R7 – 24.10.2026

**Implementiererentscheidung: BLOCKED. Keine unabhängige Freigabe.** Der vollständige Final Re-Review und alle vorangehenden Berichtsbytes bleiben unverändert erhalten. Die erneut geprüften vier offenen Findings werden nicht als korrigiert ausgegeben. Der Auftrag erlaubt ausdrücklich BLOCKED, wenn die aktuelle AvaloniaEdit-API die zugesagten Eigenschaften im erlaubten Umfang nicht zuverlässig herstellen kann.

### Gemeinsame Ursachenanalyse und fehlende Abfangstelle

Das installierte Paket `Avalonia.AvaloniaEdit` 11.4.1 und die tatsächlich geladene Assembly verweisen auf denselben Commit `b56a35a5d4838f3c9a9adeba90dd80952f70e774`. Vor einer produktiven Änderung wurden dessen tatsächliche Quellen für [TextEditor](https://github.com/AvaloniaUI/AvaloniaEdit/blob/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit/TextEditor.cs), [TextDocument](https://github.com/AvaloniaUI/AvaloniaEdit/blob/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit/Document/TextDocument.cs) und [UndoStack](https://github.com/AvaloniaUI/AvaloniaEdit/blob/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit/Document/UndoStack.cs) gelesen. Paketversion 11.4.1 und AssemblyVersion 11.3.0.0 sind unterschiedliche Versionsfelder; der übereinstimmende InformationalVersion-Commit belegt die konkrete Bibliotheksherkunft.

BeginUpdate erhöht den Zähler und öffnet die Undo-Gruppe **vor** UpdateStarted (`TextDocument.cs:451–461`). Verschachtelte Updates melden UpdateStarted nicht nochmals. Replace beginnt sein try/finally erst nach BeginUpdate (:810–838); eine dort geworfene Exception lässt den begonnenen Zustand zurück. Eine leere Ersetzung auf leerem Dokument erreicht Changing überhaupt nicht (:841–844). Der nichtvirtuelle Basistyp-Textsetter führt anschließend Caret-Reset und ClearAll aus (`TextEditor.cs:257–273`); Clear delegiert an ihn (:1071). ClearAll kann über ThrowIfUndoGroupOpen die Undo-Gruppentiefe vor dem Werfen auf 0 setzen (`UndoStack.cs:252–264`, :436–450). Dies erklärt den bereits wirksamen Text und die später offene Dokumentgruppe bei R6, einschließlich der neu geprüften Clear-Variante.

EndUpdate liefert nach Undo-Ende und Zählerreset tatsächlich UpdateFinished aus (`TextDocument.cs:468–487`). Der aktuelle Ablehnungs-Guard ruft genau diese Methode auf (`WorkbenchTextEditor.cs:83–91`). Ein bedingter Normalisierer wird deshalb erneut synchron aufgerufen, während seine Bedingung mangels zugelassener Textänderung weiterhin wahr bleibt. R7 ist damit eine Folge dieser Guard-Bereinigung. Changing/Changed haben zwar den internen Bibliotheksschutz InDocumentChanging, aber er liegt weder um UpdateStarted noch um UpdateFinished und ist kein öffentlicher Lease-Hook.

Reflection am geladenen Paket bestätigt versiegeltes TextDocument/UndoStack und nichtvirtuelle relevante Setter/Methoden. Die DocumentProperty-Coercion kann Wechsel verhindern, aber keinen Setter auf einer schon gehaltenen Dokumentreferenz abfangen. IsReadOnly schützt UI-Eingaben, nicht direkte Dokumentoperationen. Zusätzliche isolierte Laufzeitproben zeigen: nur aus UpdateStarted werfen → IsInUpdate=True; nur Changing → leerer Setter leert vorhandene Undo-Historie ohne Changing; nur IsReadOnly → direkte Textzuweisung gelingt. Ein zusätzlicher Guard an diesen Stellen erfüllt deshalb den gemeinsamen Vertrag nicht.

### Entscheidung pro Finding

| Finding | Status dieser Runde | Frischer Nachweis |
| --- | --- | --- |
| R2 | **offen** | Gewöhnliche Save-Setterfälle erhalten Historie; der vollständige Callbackvertrag bleibt wegen R7 unerfüllt. Früheres Abweisen ist öffentlich nicht verfügbar, spätes Abweisen übersieht No-op-Setter. |
| R4 | **offen** | Ursprüngliche Identity-Lease-/Nachfolgerkontrollen bestehen, aber R6 lässt Textwirkung plus InternalError/offene Gruppe und Folge-Busy zurück. R7 betrifft auch die Identity-Lease. |
| R6 | **offen** | Unveränderte Originalprobe und neue Matrix reproduzieren leeren Basistyp-Setter und Clear aus UpdateStarted: Revision 2→3, Text angewandt, IsInUpdate=True, Folgeedit Busy. |
| R7 | **offen** | Originalnormalisierer ohne Lease endet bei 2 Aufrufen; mit Save-/Identity-Lease erreicht er die absichtlichen Grenzen 8/32, neue Sollprobe erreicht 16. Kein unlimitierter Lauf oder tatsächlicher Stackoverflow wurde ausgeführt. |
| R1/R3/R5 | **keine nachgewiesene Regression** | Alle 19 zugehörigen Korrekturfälle frisch bestanden; Originalproben für tatsächlichen Runnerabbruch, frühe Save-Ownership/Drain und Diagnoseinitialisierung erneut erfolgreich. |

### Ausgearbeiteter gemeinsamer Lösungsweg und Umfang

Um den bisherigen direkten Basistypvertrag zu erhalten, wird eine separat freizugebende Bibliothekskorrektur beziehungsweise eine konkret nachgewiesene Bibliotheksversion mit früher Mutationszulassung benötigt. Sie muss vor Updatezähler/Undo-Gruppe und vor vollständigen Setter-/Clear-Nebeneffekten greifen, interne Delegationen von reentranten Callbackaufrufen unterscheiden und angenommene Updates bei Handlerfehlern ausnahmesicher abschließen. Ein ambient Allow-Flag allein würde den Callbacks erneut dieselbe Befugnis geben. Eine solche Version wurde nicht behauptet, installiert oder durch einen stillen Binär-/Reflectionpatch ersetzt.

Eine reine Hostlösung wäre nur mit ausdrücklich geändertem Vertrag konsistent: alle produktiven Aufrufer vermitteln und rohe Basistyp-/Dokumentmutationen in beliebigen Callbacks technisch aus dem unterstützten Zugriff herausnehmen. Das wäre eine Erweiterung der ausgeschlossenen API-Fläche und ist unter dem aktuellen Auftrag kein zulässiger stiller Fix. Der Validierungsnachtrag enthält die konkrete Migrationstabelle für View-Clear, History, Import, Anchor-/Book-Ausgabe, Completion, MCP-Edit, UI-/Undo-/Redo-Kommandos und beide Savepfade. Dabei bleiben derselbe Editor, dieselbe Pipeline, fail-fast Admission, Identität/Revision, frühe Save-Completion und Delete-vor-Write ohne neue Cancellationlücke verbindlich. Abgewiesene Mutationen werden nicht automatisch später wiederholt.

Deshalb wurden keine produktiven Codeänderungen und keine nachträglichen Gruppenreparaturen umgesetzt. Keine bestehende Testassertion wurde entfernt oder abgeschwächt. Eine bloße Änderung der Erfolgsantwort nach bereits erfolgtem Text würde den beschädigten Editor nicht reparieren. Die gewählte Behandlung ist die ausdrücklich verlangte Blocker-Dokumentation vor Implementierung; eine funktionsfähige gemeinsame Korrektur wird nicht vorgetäuscht.

### Neue Proben und vollständige Validierung

Ein neuer externer Prüfstand `mcp014-corrections2/BoundaryProbe.cs` verwendet für 46 Sollvertragsprüfungen die tatsächliche XAML-View und den produktiven WorkbenchTextEditor. Ergebnis **42 bestanden, 4 fehlgeschlagen, Exitcode 1**. Er umfasst Setter aus allen drei Ereignisphasen, leere/nichtleere Dokumente, gleiche/andere Zuweisung/Clear/Document.Text, Save plus Documentwechselversuch, wiederholte Ablehnungen, echte Undo-/Redo-/Folgeeditprüfungen, rekursive beziehungsweise werfende UpdateFinished-Abonnenten und Saveversuche aus Editcallbacks. Die vier Fehlschläge sind die beiden R6-Varianten und die beiden R7-Leases. Bestehende erneut ausgeführte R4-Fälle ergänzen View-/Documentwechsel und werfende Changed-/UpdateFinished-Handler nach angewandtem Text.

Alle unabhängigen ursprünglichen Probequellen wurden unverändert erneut ausgeführt, einschließlich `started-empty`, `normalizer` und der zusätzlichen Dispose-/Unlock-/Detachfälle. Diese Wiederholung wurde vom Implementierer durchgeführt und ist kein unabhängiger Re-Review. Die Originalprogramme sichern die bekannten Fehler als Beobachtungen ab und können deshalb Exitcode 0 liefern; dies wird nicht als behobener Produktvertrag gezählt. Der neue Prüfstand prüft dagegen die geforderten Garantien und bleibt rot.

| Frische Prüfung | Ergebnis |
| --- | --- |
| Restore und Release-Build beider Solutions | erfolgreich; inkrementell 0 Warnungen/0 Fehler |
| 49 Korrekturtests / 47 ursprüngliche Editorfälle | 49/49 beziehungsweise 47/47 bestanden |
| Vollständige GUI-Suite | 434 bestanden |
| Vollständige Core-Suite | 7 667 bestanden, 4 bisherige Skips |
| Vollständige reguläre MCP-Suite | 89 bestanden |
| Neue externe Sollvertragsproben | 42 bestanden, **4 fehlgeschlagen** |
| Abhängigkeiten / diff-Prüfung | Paketgraph unverändert, AvaloniaEdit 11.4.1; `git diff --check` erfolgreich |

Die regulären 8 190 Erfolge zählen die 49+47 Editorfälle nicht doppelt. Explicit-Hardwarebenchmark und Installed-Codex-Roundtrip wurden nicht aktiviert. Logs, TRX, beide unveränderten Dokumentpräfixe, Quellenmanifest und Hashkontrolle liegen unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-corrections2/`. Der ausführliche [Validierungsnachtrag](MCP-01.4-validation.md#korrekturrunde-2--r2r4r6r7--24102026) enthält Bibliotheksreihenfolge, verworfene Alternativen, Host-Callsite-Migration, Befehle, Testabdeckung und Zwischenfehler.

Zwischenfehler: Im ersten neuen Scratch-Prüfstand wurde das Stringfeld Revision versehentlich mit `+ 1` verkettet. Nur dieser neue Prüfstand wurde auf einen kulturunabhängigen Vergleich des konkreten Hostzählers korrigiert; das erste Log bleibt erhalten. Danach bestehen 22/24 Callbackfälle, die zwei R6-Fälle bleiben rot. Die erweiterten Normalisiererproben ergeben die weiteren zwei Fehler. Ein zunächst falsch benannter Completion-Quellpfad wurde durch Suche auf `InfoCompletionController.cs` korrigiert. Keine Produktbuildfehler und keine abgeschwächten bestehenden Tests.

### Grenzen und Abschluss

Die direkten Setter-/Callbackgarantien werden nicht als neue akzeptierte Grenze wegdefiniert: R2/R4/R6/R7 bleiben offen und blockieren die Freigabe. Die bereits ausgeschlossenen rohen UndoStack-Aufrufe bleiben separat ausgeschlossen. Nichttransaktionales Save mit ASCII/Whitespace-Filter/LF/Append und bestätigtem Delete vor Write, externe Dateisystemrennen, native IME-/StorageProvider-/Closing-Lücken, sporadische Headless-Dispose-Ausnahmen und Print-/Info-Multicast-Grenzen bleiben unverändert.

**BLOCKED.** Nur die beiden beauftragten Dokumente werden ergänzt; die gesamte bisherige Reviewhistorie einschließlich des unabhängigen CHANGES REQUIRED bleibt bytegetreu erhalten. Branch/HEAD und sämtliche Quellen/Tests/Abhängigkeiten bleiben unverändert. Kein Commit/Push/Merge/Rebase/Reset, keine MCP-01.1-Vertragsänderung, kein MCP-01.5 und kein Lesen oder Ändern von `.codex/config.toml`. Für den nächsten Implementierungsschritt ist eine ausdrücklich festgelegte Bibliotheks-/Host-Architekturänderung erforderlich. Diese Runde endet hier; keine unabhängige Freigabe durch den Implementierer.
