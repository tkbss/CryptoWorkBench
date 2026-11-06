# MCP-00-AP2.1-R1 – Independent Implementation Review

Stand: 26.10.2026. **Gesamtbewertung: CHANGES REQUIRED.** Vier konkrete Fehler des AP2.1-Grundkerns wurden gegen die unveränderten Produktbinaries reproduziert. Die bestehenden Tests bestehen weiterhin; sie decken diese Übergangskombinationen nicht ab. Keine Produktkorrektur, Teständerung oder Freigabe erfolgt durch diesen Bericht.

## 1. Reviewbasis und Umfang

Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, verifizierter HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Ausgangsstatus des Reviews: sechs modifizierte getrackte Dateien, 25 ungetrackte Dateien, Staging leer.

Verglichen wurden die tatsächlichen Quellen, `mcp00-ap21/originals/`, `baseline.json`, `published.json`, `manifest.json` und `ap21-only.diff` unter `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp`. Alle 71 im AP2.1-Manifest erfassten Artefakte und alle zehn publizierten Dateien stimmen mit ihren gespeicherten Hashes überein. Der Review beurteilt das Delta gegenüber dem bereits uncommitteten MCP-01.4-Stand, nicht lediglich den Git-Diff gegen HEAD. Registry, v1-Editoroperationskörper, CryptoScript-Ausführung und historische Testkörper werden deshalb nicht als neue AP2.1-Implementierung ausgegeben.

Vertragsbasis: aktuelles R2, AP0 mit datierter IME-Eigentümerergänzung, D02-S1, H4-Integrationsbericht und U1-Undo-/Redo-Bericht. Die CJK-Risikoakzeptanz gilt; fehlende native CJK-Negativtests sind kein AP2.1-Blocker. Sie beweisen weder native Erstfokusunterdrückung noch andere GUI-/Callbackeigenschaften.

Methodik: getrennte kritische Quell- und Callchainanalyse, Hashabgleich, Prüfung der Roh-TRX/Logs, zwei unveränderte Headless-Testläufe und vier zusätzliche In-Memory-Gegenbeispiele. Diese Prüfung erfolgt im selben Arbeitskontext wie die Implementierung; sie ist keine personell oder organisatorisch unabhängige Drittzertifizierung. Befunde werden aus konkretem Code und neuen Ausführungen hergeleitet, nicht aus der Selbsteinschätzung des Implementierungsberichts.

Es wurde ausschliesslich dieser neue Bericht im Produkt angelegt. Für Baseline und neue TRX-Dateien wurden temporäre Prüfdaten unter `C:\Users\stefa\AppData\Local\Temp` verwendet. Keine Testquelldatei und kein bestehendes Artefakt wurde verändert. Die zusätzlichen Gegenbeispiele wurden mit PowerShell 7.6.5 / .NET 10.0.11 per `Add-Type` ausschliesslich im Speicher kompiliert. Reflection ruft vorhandene interne Produktmethoden und die vorhandene Headless-Fixture auf; sie ändert keine Assembly, Guardbedingung oder Produktfeldwerte. Es gibt dabei kein sichtbares natives Windows-Prüffenster.

## 2. Unabhängige Codeanalyse

Positiv bestätigt:

- Der Service erstellt UI-affin einen Coordinator an seiner bestehenden EditorBridge. Es gibt keine zusätzliche Text-/Revisionsquelle, Interpreterpipeline oder Variablenkopie.
- Normale `TryBegin`-Admission prüft Phase, aktive Operation, ObserverDepth, Bridgeidentität, geschlossene Dokumentupdates und bestehende Locks vor Ticketregistrierung. Native Actions laufen erst nach Registrierung.
- Ursprüngliche Undo-/Redo-Bindings bleiben in beiden nativen Listen erhalten. Die Gesten werden nicht durch allgemeine KeyDown-Sonderlogik ersetzt.
- Private Command- und Textdelegation werden vor nativen Callbacks verbraucht und durch `finally` zurückgesetzt. Ein kontrollierter Observer erbt damit keinen pauschalen Reentrancybypass.
- TextInput und direkte PerformTextInput-Aufrufe sind vermittelt. Erfolgreiche Normalfälle erzeugen nur einen TextEntering-/TextEntered-Zyklus.
- Clipboardtickets halten Originaldokument, Bridgeidentität, Revision und Generation; vor dem verspäteten Commit werden diese sowie Auswahlobjekt und Caret geprüft.
- Der neue Coordinator enthält keine EndUpdate-Reparaturschleife und keinen automatischen Faulted-Recoverypfad. Der alte einmalige UpdateStarted-Ausgleich ist bestehender Code.
- Ausstehende Operations- und Callbacktickets sind gemeinsam auf 64 begrenzt; dies ist kein D02-Leasebudget. Ein nicht terminierender Provider darf den lokalen Drain weiter blockieren.

Die folgenden vier Findings widerlegen jedoch zentrale behauptete Eigenschaften bei Kombinationen, die von den 23 neuen Tests nicht erfasst werden. Ihre Referenzpositionen beziehen sich auf den hashverifizierten Produktstand dieses Reviews.

## 3. Findings nach Severity

| ID | Severity | Kernbefund | Einordnung |
|---|---|---|---|
| F01 | High | Früherer Service löscht den Coordinator-Verweis des neuen Servicebesitzers | AP2.1-Blocker |
| F02 | High | Callback kann Receipt vor dem eigenen Ende publizieren; anschliessender Fehler fehlt im Beleg | AP2.1-Blocker |
| F04 | High | Commit prüft offene Updategruppe nicht erneut; Mutation erfolgt vor Faulted | AP2.1-Blocker |
| F03 | Medium | Fokusverlust entwertet das einzige Drainticket dauerhaft und verhindert ResumeGui | AP2.1-Blocker |

Keine Critical-Einstufung: Es ist kein produktiver externer MCP-Owner- oder HTTP-Angriff nachgewiesen. Die Fehler betreffen den bereits implementierten internen Grundkern und dessen Eignung als sichere Basis für AP3–AP6.

### F01 – Fremder Coordinator-Verweis wird beim späteren Attach entfernt

**Position:** `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:70–76`, im Zusammenhang mit `BeforeAttach:84–86`, `EditorBridge.cs:71–89` und `WorkbenchTextArea.cs:46–47,101`.

**Reproduzierte Callchain:** Service A registriert Editor E. A detacht E vollständig. Service B darf E danach registrieren, weil A Unavailable und ohne Pending ist. B beginnt einen Drain und befindet sich in Releasing. A registriert nun einen anderen Editor E2. A hält intern noch seine alte Registration für E; `Attach` setzt deshalb `old.Coordinator = null`, ohne zu prüfen, ob dieser Verweis noch A gehört. Tatsächlich gehört er inzwischen B. `E.TextArea.PerformTextInput("X")` läuft anschliessend über den Unbound-Fallback und verändert E trotz B.Releasing.

**Gemessene Ausgabe:** `B.phase=Releasing; E.Coordinator=null:True; text=baseX`.

**Verletzte Invariante:** Genau eine tatsächliche Besitz-/Zulassungsquelle; alte Registrierungen dürfen keine Berechtigung oder Freigabe eines Nachfolgers verändern. R2 Identitäts-/Originalbindung, AP0 Lifecyclegrenze und AP2.1-R1/R6.

**Auswirkung:** Beide frühe TextArea-Zulassung und zusätzliche Dokumentguards verlieren den Coordinatorbezug. Der Fehler benötigt zwei Serviceinstanzen und eine erlaubte Detach-/Reattachfolge; er ist kein pauschaler Angriff durch externe Clients. Die vorhandene Zweitserviceprobe prüft nur eine direkte Übernahme eines noch registrierten Editors und verfehlt diesen Fall.

**Empfehlung:** Trennen einer alten Editorverknüpfung nur bei exakter Eigentümerschaft des Verweises; Registration-/Coordinatorhandoff symmetrisch und identitätsgebunden gestalten. Regression für A.detach(E) → B.attach(E) → B.drain → A.attach(E2) mit abgewiesenem Input in E ergänzen. Nicht als spätere GUIinventur behandeln: Der Fehler liegt in der neu eingeführten grundlegenden Attachlogik.

### F02 – Selbstabschluss eines laufenden Callbacktickets verliert den Fehlerbeleg

**Position:** `EditorCoordinator.cs:314–330` (`CallbackTicket.Observe/Dispose`) und `238–257` (`Finish`).

**Reproduzierte Callchain:** `t = TryBegin(); cb = t.RegisterCallback(); t.Dispose(); cb.Observe(() => { cb.Dispose(); completionInside = t.Completion.IsCompleted; throw ...; });`. BodyDone/Sealed sind beim Callbackstart bereits gesetzt. `cb.Dispose` setzt done, reduziert die Zähler und ruft sofort `Finish(t)` auf. Anders als beim aktiven OperationTicket existiert keine Sperre für den gerade laufenden Callback. `Finish` publiziert einen unveränderlichen Receipt ohne Fehler. Erst nach Rückkehr aus `owner.Observe` setzt CallbackTicket.Observe `operation.Error = CallbackFailed`; der bereits publizierte Receipt wird nicht mehr ersetzt.

**Gemessene Ausgabe:** `completionInsideCallback=True; receipt.error=null; observerFailures=1`.

**Verletzte Invariante:** Terminaler Beleg erst nach letzten registrierten Folgen; Callbackfehler dürfen nicht verschwinden. AP0 O14/D01 und Producer-/Callbackabschluss; D02-S1 setzt für seine Modellannahmen vollständig abgeschlossene Verpflichtungen voraus.

**Auswirkung:** Ein Aufrufer erhält vorzeitig einen scheinbar fehlerfreien terminalen Beleg. Bei vorher angewandtem Edit würde ein später Callbackfehler im Resultat fehlen. Das Gegenbeispiel benötigt keine Textmutation und zeigt den Abschlussfehler direkt. `ObserverDepth` verhindert dabei weiterhin eine neue mutierende Admission und verzögert CheckDrain bis zum Observerende: Es wird ausdrücklich **kein** während des Callbackkörpers tatsächlich publizierter GUI-Owner-Wechsel behauptet. Die Receiptfinalisierung allein ist bereits falsch.

**Empfehlung:** Laufende Callbackausführung als eigene Abschlussverpflichtung halten. Dispose innerhalb Observe darf höchstens Abschluss anfordern; Fehlerzuordnung und Callbackende müssen vor `Finish` und Receiptpublikation stattfinden. Den analogen Schutz des OperationTicket nicht mit einem Schutz aller Callbacktickets verwechseln. Neue Probe für Self-dispose + Throw sowie angewandten Edit + späten Callbackfehler.

### F04 – Später Commit überspringt die erneute Updategruppenprüfung

**Position:** `EditorCoordinator.cs:109–111` gegenüber `144–175`, insbesondere Guard `149` und spätes Faulten `173`.

**Reproduzierte Callchain:** Ein Ticket wird bei geschlossenem Dokumentupdate angenommen. Danach öffnet der Host `Document.BeginUpdate()` ohne Textänderung. Das Ticket bleibt nach Revision, Generation und Identität aktuell. `Commit(ticket, () => Document.Insert(4, "X"), false, null)` prüft nur Active/ObserverDepth, `IsCurrent` und MutationLocked; `Document.IsInUpdate` fehlt am Commit. Die Mutation wird in die schon offene Gruppe ausgeführt. Erst anschliessend wird Faulted gesetzt. Die Probe schliesst ausschliesslich ihre selbst geöffnete Updategruppe wieder; der Produktcode wurde nicht repariert.

**Gemessene Ausgabe:** `openUpdate commitRan=True; commitReturn=True; text=baseX; phase=Faulted`.

**Verletzte Invariante:** Frühe Zulassung vor Text-/Historywirkung, erneute atomare Zulassungsprüfung am tatsächlichen Commit, keine Vermischung mit inkompatiblen offenen Gruppen. AP0 Admission-/Commitmodell und AP2.1-R1/R2/R5.

**Auswirkung:** Ein zuvor zulässiger asynchroner Intent kann in einen inzwischen inkompatiblen Dokumentzustand schreiben. Das spätere Faulted begrenzt Folgewirkungen, ersetzt aber die geforderte wirkungslose Ablehnung nicht. Eine konkrete OS-/Clipboard-Nested-Pump-Auslösung wurde nicht behauptet; reproduziert wurde die bereits angebotene interne Commit-Schnittstelle. Im selben Guard fehlt auch die erneute IdentityLocked-Prüfung aus CanBegin; dieser zusätzliche Zweig ist statisch sichtbar, wurde hier aber nicht als separates dynamisches Finding gezählt.

**Empfehlung:** Am Commit dieselben aktuellen relevanten Idle-/Lockbedingungen wie bei Admission prüfen, bevor `active` gesetzt oder eine native Action aufgerufen wird. Alte Tickets dürfen diese Bedingungen nicht ersetzen. Definiertes Ablehnungs-/Finalisierungsverhalten prüfen, ohne fremde Updategruppen zu schliessen oder History nachträglich zu reparieren.

### F03 – Fokusinvalidierung macht einen erfolgreichen Drain nicht wieder aufnehmbar

**Position:** `WorkbenchTextArea.cs:36`, `EditorCoordinator.cs:98,199–219`.

**Reproduzierte Callchain:** C.BeginDrain erzeugt und beendet ein Drainticket bei Generation 2. Eine zweite vorhandene Headless-Hostfixture erhält den Fokus; die tatsächliche LostFocus-Subscription von C erhöht dessen Generation auf 3. ResumeGui lehnt das Zertifikat wegen Generation mismatch ab. Erneutes BeginDrain liefert unverändert dasselbe Zertifikat mit Generation 2 zurück. Damit gibt es in diesem Zustand keinen erfolgreichen ResumeGui-Pfad.

**Gemessene Ausgabe:** `actualFocusLoss generation=2->3; drainComplete=True; resume=False; sameDrain=True; phase=Releasing`.

**Verletzte Invariante:** Stabile Identität eines laufenden/abgeschlossenen Kontrollübergangs; Fokusinvalidierung alter Eingabeintents darf einen sicher beendeten Übergang nicht dauerhaft unabschliessbar machen. AP2.1 Kontrollgeneration, Drain und zulässige Folgeoperationen.

**Auswirkung:** Dauerhafte logische Schreibsperre trotz abgeschlossenem Drain, ohne Hänger oder Faulted. Dies ist kein Monitor-/Threaddeadlock. Die normale Produktion verwendet derzeit keinen MCP-Übergang; betroffen ist dennoch die neue, ausdrücklich für spätere Integration angebotene lokale BeginDrain/ResumeGui-Schnittstelle. Ein künstliches Detach/Reattach als Ausweg ist kein vertraglicher erfolgreicher Abschluss dieses Übergangs.

**Empfehlung:** Eingabeintent-Invalidierung und die Identität des Kontroll-/Drainübergangs unterscheiden oder eine explizite sichere Fortschreibung vorsehen. Die Wiederaufnahme darf nicht auf ein Zertifikat warten, das BeginDrain prinzipiell nie ersetzen kann. Fokusverlust während und nach dem Drainabschluss testen.

## 4. Nativer Eingangs- und Undo-/Redo-Vertrag

Die Quellkette lautet: native Ctrl+Z/Y-Gestenerkennung in DefaultInputHandler → RoutedCommand → explizite IRoutedCommandBindable-Liste der WorkbenchTextArea → frühe Zulassung → einmalige Rückleitung zur originalen nativen Bindung. Die ursprünglichen Registrierungen bleiben bestehen; U1 wird insoweit korrekt umgesetzt. Die CanExecute-Rückleitung verwendet denselben einmaligen Zugriff und schafft keine zweite Besitzquelle.

Bei normalem TextInput folgt der frühe Guard vor der nativen PerformTextInput-Kette. Der direkte öffentliche PerformTextInput-Aufruf wird im virtuellen OnTextEntering abgefangen: innerer nativer Aufruf unter Ticket, inneres Delegationsrecht vor Subscriber verbraucht, äusserer Aufruf anschliessend handled. Vorhandene Tests bestätigen genau einen Eventzyklus. Die Fehlerwege der privaten Flags besitzen `finally`; ein entkommener nativer Fehler setzt den Coordinator konservativ Faulted. Kein eigener UndoStack und kein allgemeiner Ctrl+Z/Y-KeyDown-Ersatz.

Tab/ShiftTab werden durch die enge `OnKeyDown`-Grenze vor dem direkten nativen Tabhandler geführt. Der vollständige Mehrzeilen-/Rechteck-/ReadOnly-/Completion-Keymatrixnachweis ist in den neuen 23 Tests nicht vorhanden und bleibt OPEN. Dasselbe gilt für alle Transformationen und weitere Search-/Pointerrouten. Native History und Text-Eingänge werden ausserdem durch F01 beeinträchtigt, wenn die Coordinatorverknüpfung verloren geht.

IME ist früh an beiden realen Controls deaktiviert und gegen true coerciert; Optionsänderungen werden auf TextDrag=false zurückgeführt. Dies ist eine Code-/Headlessaussage, kein neuer Windows-Nativdurchlauf. Die U1-Nativbelege bleiben historische Evidenz einer isolierten Variante.

## 5. Ticket-, Observer- und Drainvertrag

Die synchrone Normalreihenfolge ist nachvollziehbar: CanBegin → Bridge-Snapshot → Ticket in HashSet → erneute Bindungsprüfung → active setzen → native Wirkung → Wirkungserfassung → active löschen → BodyDone/Sealed → gegebenenfalls Callbackdrain → Receipt/Completion → CheckDrain. F04 betrifft die unvollständige zweite Prüfung; F02 betrifft den verfrühten Callbackabschluss.

RegisterCallback zählt vor der Rückgabe des Tickets und verwirft Registrierung nach Sealing/BodyDone. Die 64er-Grenze umfasst Operations- und Callbacktickets. Die TCS verwenden RunContinuationsAsynchronously; keine synchrone Fortsetzung wird aus TrySetResult in die Registry eingeschleust. Ein Waitabbruch ist kein Dispose des angenommenen Jobs. Ein unendlicher Clipboardprovider kann den lokalen Drain unbegrenzt offenhalten; dies ist beabsichtigtes fail-closed Verhalten, keine sichere Kapazitätsfreigabe.

Observe sperrt neue vermittelte Mutationen auch ohne aktive Native Action. Fehler werden gezählt und bei aktiver Operation als CallbackFailed zugeordnet. Bei einer gewöhnlichen späten CallbackTicket.Observe-Ausführung wird der Fehler vor dem äusseren Dispose auf das Originalticket übertragen; bei Self-dispose greift F02. Allgemeine fremde `+=`-Subscriber und async-void-Callbacks werden dadurch nicht automatisch sicher. Eigene schreibende Completion-/Returnfortsetzungen benötigen später eigene angenommene Aktionen und dürfen nicht als reine Observer gelten.

Kritische Bridgebeobachtung: `Finish` ruft ObserveText am passenden Original erneut auf und faultet bei einer dortigen Exception; nach Dokument-/Identitätswechsel wird keine Nachfolgerrevision verwendet. Dagegen laufen Bridge.Current in CanBegin/IsCurrent und Bridge.IsLive vor CallbackTicket.Observe ausserhalb dieser finalen Catch-Grenze. Es gibt keinen gezielten produktiven Fault-Injection-Nachweis für Revisionsüberlauf oder Ausfall dieser kritischen Eintrittsbeobachtung. Dieser Punkt bleibt OPEN; es wird ohne passende ausgeführte Fehlerprobe kein zusätzlicher bestätigter Produktfehler behauptet. Allgemeine UI-Observerfehler dürfen nicht als Rechtfertigung dienen, kritische Bridgefehler nur kosmetisch zu zählen.

Registry.DisposeAsync wartet weiterhin nur auf Registry-/Save-Tasks und detacht dann die Bridge. Es wartet nicht auf den neuen vollständigen Editor-/Callbackdrain. Das ist als spätere Gesamtintegration benannt und ohne MCP-Owner-Aktivierung kein hier neu eingeführter vermeintlicher L2-Erfolg. AP4/AP6 müssen diesen Beitrag real verbinden. F03 ist davon unabhängig ein lokaler Protokollfehler bei vollständig beendetem Drain.

## 6. D01-Fehlersemantik

Interne Belege enthalten Original-ID/-Revision, nullable beobachtete Revision, getrennte Text-/Historywirkung und Finalisierung. `noAutomaticRetry` ist fest. Keine Script-/Secretdaten oder Exception.Message werden transportiert. Normale kontrollierte Callbackfehler nach belegter Wirkung behalten applied/applied und complete mit Fehler; echte Folge-Undo-/Redo-Proben bestehen. Unkontrollierte native Exceptions führen zu indeterminate History und Faulted; offene Updates werden nicht automatisch geschlossen.

Grenzen: Diese Evidenz ist noch keine vollständige v2-Transportprojektion. Die Historybeobachtung ist eine Kombination aus bekannter Aktion, Versionsänderung und PropertyChanged-Signal, keine allgemeine beweisende Inspektion beliebiger UndoStack-Inhalte. Tests für native History-only-Wirkung und entkommene Undo-Exceptions sind vorhanden. Composite-/mehrteilige Hostaktionen, beliebige fremde Historymanipulationen und fehlgeschlagene kritische Snapshots sind nicht vollständig nachgewiesen. Solche späteren Pfade dürfen nicht ohne spezifische Wirkungsnachweise auf `none` oder vollständig `applied` abgebildet werden.

F02 ist ein konkreter D01-Verstoss, weil der Fehler erst nach terminaler Receiptpublikation zugeordnet wird. F04 zeigt zusätzlich, dass ein nachträgliches Faulted keine frühe Admission ersetzt. Der alte v1-UpdateEditor-Methodenkörper wurde in AP2.1 nicht migriert; seine bestehende EndUpdate-/Fehlerbehandlung ist kein Beweis für den neuen Receiptvertrag.

## 7. Bridge-/Serviceintegration und Clipboard

Die Bridge bleibt alleinige Identitäts-/Revisionsquelle; Coordinator-Tickets speichern Bindungen, keine zweite bearbeitbare Dokumentkopie. Vor normalem Attach wird geprüft, vor Dokumentwechsel die Generation invalidiert, Detach bindet weiter an das Original. Die normale Zweitservice-Abweisung besteht. F01 zeigt den nicht geprüften späteren Handofffall und blockiert daher die Abnahme dieser Integration.

Cut/Paste/Copy: Ticket vor Clipboardawait; Lesen/Export am Original; Paste/Cut nur nach Revision-/Generation-/Dokument-/Auswahl-/Caretprüfung. Providerfehler werden als CallbackFailed erfasst und das Ticket durch using beendet. Ein abgebrochenes Providertask läuft durch denselben Catch; eine Cancellation eines separaten Waiters ist dagegen kein nachgewiesener Providerabschluss. Der Produktadapter verwendet die reale Clipboard-API. Nur die Testfixture besitzt einen Reflection-Proxy zur verzögerten Plattformantwort; er ist kein öffentlicher Produktbypass.

Die vorhandene verzögerte Pasteprobe prüft Dokumentwechsel plus Drain. Nicht vollständig abgedeckt sind verzögertes Cut/Copy, sämtliche Providerfehler-/Abbruchkombinationen, Detach/Reattach während jeder Clipboardphase, Rechteck-/WholeLine-Formate und TextCopied/TextPasted-Ereignisäquivalenz. Diese Punkte sind OPEN und nicht als bestätigte neue Fehler ausgegeben. Native Format-/Completionvollständigkeit bleibt AP3; erneute Zulassung bei zwischenzeitlich offenem Update gehört hingegen bereits zum bestehenden Commit und fällt unter F04.

CryptoScript-Pipeline, globaler Variablenbestand und EC21-Code sind im AP2.1-Delta unverändert. `WholeEditorPrintIsInsertedOnceWithoutDuplicatingSource` besteht mit seiner historischen zusätzlichen out:-Mutation; D05/EC21 bleibt AP5. Ein PASS dieses historischen Tests ist kein Nachweis der Zielsemantik ohne Sourceausgabe.
## 8. GUI-/Core-/MCP-Regressionen und Compilerwarnungen

Die vorhandenen Ergebnisdateien wurden inhaltlich und gegen das AP2.1-Manifest geprüft. Neue Läufe erfolgten mit `--no-build --no-restore`, sodass weder Produktquellen noch bin/obj neu gebaut wurden.

| Nachweis | Ergebnis / Einordnung |
|---|---|
| Vorhanden `mcp00-ap21/results/gui-final.trx` | 457 PASS, 0 FAIL, keine nicht ausgeführten Fälle |
| Neuer R1-Lauf `ap21-r1-targeted.trx` | Unveränderte Coordinator-Gruppe: 23 PASS, 0 FAIL |
| Neuer R1-Lauf `ap21-r1-gui.trx` | Unveränderte vollständige GUI-Suite: 457 PASS, 0 FAIL |
| Vier zusätzliche In-Memory-Callchains | Vier reproduzierte Gegenbeispiele F01–F04; keine neu gespeicherte Testklasse und keine Teständerung |
| Vorhanden `core.trx` | 7.667 PASS, 0 FAIL; **fünf** NotExecuted-Einträge im Roh-TRX, davon vier reguläre Skips und ein Explicit-Test |
| Vorhanden `mcp.trx` | 89 PASS, 0 FAIL; **ein** NotExecuted-Explicit-Test im Roh-TRX |

Neue TRX-Dateien: `C:\Users\stefa\AppData\Local\Temp\ap21-r1-results`. Die reproduzierten Gegenbeispielausgaben und ihre vollständige In-Memory-Reproduktion stehen in diesem Bericht. Core und MCP wurden nicht nochmals ausgeführt: Ihre unveränderten Quellen/Artefakte und vorhandenen Resultate sind geprüft; eine erneute pauschale Ausführung würde die gefundenen Coordinator-Übergänge nicht zusätzlich absichern.

Die Konsolenzahlen „Core 4 Skips“ und „MCP 0 Skips“ aus dem Implementierungsbericht lassen explizit nicht gestartete Fälle weg. Vollständige Nichtausführungsliste aus den Roh-TRX:

| Suite / Test | Grund |
|---|---|
| Core: `MeasureLocalHardware` | Explicit: manuelle Hardwaremessung, keine routinemässige CI-Ausführung |
| Core: `NativePublicationDoesNotFollowLateSymlink` | Symbolic-link creation privilege unavailable |
| Core: `TargetSymlinksIncludingDanglingLinksAreRejected(True)` | Derselbe fehlende Symlink-Erstellungsnachweis |
| Core: `TargetSymlinksIncludingDanglingLinksAreRejected(False)` | Derselbe fehlende Symlink-Erstellungsnachweis |
| Core: `MacOsInheritedExtendedAclIsRemovedBeforePublication` | Darwin-ACL-Plattformfall auf diesem Windows-Host nicht ausgeführt |
| MCP: `InstalledCodexDiscoversAndCallsStatus` | Explicit; benötigt `CRYPTOWORKBENCH_CODEX_PATH` zum installierten lokalen Codex |

Damit sind 7.672 Core-Einträge und 90 MCP-Einträge im jeweiligen Roh-TRX berücksichtigt. Keine Aussage, die manuelle Hardwaremessung, Darwin-/Symlinkfälle oder echte installierte Codex-Kompatibilität seien PASS. Die bestehenden Tests wurden weder abgeschwächt noch deaktiviert.

Neue Warnungen: CS0618 für `IClipboard.GetTextAsync` im neuen WorkbenchTextArea-Adapter (aktuell Zeile 140), für Headless KeyPress/KeyRelease in `EditorCoordinatorTests.cs:51` und GetTextAsync im Clipboardtest (Zeile 281). Die ersten Buildlogs tragen frühere Zeilennummern derselben API. Diese Warnungen stammen aus neuen Aufrufern vorhandener Kompatibilitäts-APIs, nicht aus einem Avalonia-/Paketupgrade. Nullable-Warnungen zu InsightWindow, MainViewModel-Commands und Sidebar-Dereferenzierung stammen aus bestehendem Code; verschobene View-Zeilennummern sind kein neuer Fehlerbeweis. Keine unbelegte Behauptung, eine obsolete API verursache F01–F04.

Reproduktionsbefehle für die unveränderten Suites:

```powershell
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --filter FullyQualifiedName~EditorCoordinatorTests --logger 'trx;LogFileName=ap21-r1-targeted.trx' --results-directory 'C:\Users\stefa\AppData\Local\Temp\ap21-r1-results'
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --logger 'trx;LogFileName=ap21-r1-gui.trx' --results-directory 'C:\Users\stefa\AppData\Local\Temp\ap21-r1-results'
```

## 9. Headless-Session-Abschlussbefund

Das ursprüngliche `gui-third.trx` enthält tatsächlich 456 PASS / 1 FAIL. Der fehlgeschlagene Fall ist `OpenNativeUpdateFaultsWithoutEndUpdateRepairLoop`; sein Stack beginnt in `Avalonia.Headless.HeadlessUnitTestSession.Dispose`, danach in `EditorCoordinatorTests.Ui` an der äusseren Session-Dispose-Grenze. Der Rohbefund nennt eine NullReferenceException, keine fehlgeschlagene Text-/Historyassertion und keine konkrete Produktmethode als Throwstelle.

Die aktuelle Fixture erstellt pro Ui-Lauf eine neue HeadlessSession, lädt Fluent- und AvaloniaEdit-Styles, templatisiert den Editor und prüft Fokus explizit. Der Produkt-Host wird vor dem äusseren Sessionende asynchron disposed, dann das Window geschlossen. Der betreffende Test öffnet eine eigene Updategruppe, prüft Faulted und schliesst seine eigene Gruppe wieder. Diese Fakten belegen die Reihenfolge, aber nicht die interne Fehlerursache im Framework.

Bewertung: **OPEN / INCONCLUSIVE bezüglich der Ursache**. Ein produktiver Lifecyclefehler ist dadurch nicht nachgewiesen. Ein reines Style-/Fokusproblem ist ebenfalls nicht belegt; das historische Fixtureproblem aus dem ersten Testlauf ist eine andere Fehlerklasse. Gemeinsamer Headless-Zustand, Sessionteardown oder ein intermittierender Frameworkfehler bleiben mögliche Erklärungen, keine gesicherten Findings. Die unveränderte Wiederholung der 23er-Gruppe und vollständigen 457er-Suite reproduzierte den Fehler nicht. Das ist kein Fixnachweis und keine Rechtfertigung, die historische TRX zu ersetzen.

Keine weitere ungezielte Wiederholungsschleife und keine Assertionänderung. Bei erneutem Auftreten sind vollständige Frameworkstack-/Session-Lifecycle-Daten zu erfassen. Dieser OPEN-Punkt wird nicht mit einem fehlenden CJK-Test vermischt.

## 10. Exakte Abdeckung und offene AP3–AP6-Abhängigkeiten

Legende: **N** = neuer früher nativer Eingang im Produkt vorhanden; **T** = Teilintegration, keine vollständige Vertragsabdeckung; **D** = Funktion durch permanente Konfiguration deaktiviert; **O** = kein neuer vollständiger AP2.1-Vermittlungspfad. N ist kein PASS sämtlicher Varianten und steht unter den Findings. Die AP0-IDs bleiben vollständig erhalten; die historischen AP0-Zeilen werden nicht umgeschrieben.

### 10.1 Alle 51 Mutationspfade

| ID | AP2.1-Iststand | Noch erforderlicher Nachweis / Integration |
|---|---|---|
| M01 | N: OnTextInput/OnTextEntering, direktes PerformTextInput | F01; gesamte Completion-/Observerfolge AP3 |
| M02 | N: mehrzeichiger Unicode derselbe Eingang | Physische Tastatur/Dead Keys/AltGr nicht neu nativ geprüft |
| M03 | D: IME=false an Editor und TextArea | CJK technisch OPEN, Restrisiko akzeptiert, kein CJK-Blocker |
| M04 | T: nativer Enter/Textpfad unter Ticket | Runner/Return-P über await nicht an Coordinatorgesamt-Drain angeschlossen |
| M05 | N/T: EnterLineBreak im nativen Editing-Routing | Vollständige Alias-/Return-/Callbackmatrix AP3/AP5 |
| M06 | N: Editing-Bindings für Delete/Backspace usw. | Gesamte echte Key-/ReadOnlymatrix AP3 |
| M07 | N: Editing-/Application-Delete über TextArea | Weitere Kontext-/Fallbackrouten einzeln abnehmen |
| M08 | N: enger Tab-/ShiftTab-Eingang | Mehrzeile/Rechteck/Completion-/ReadOnlykombinationen OPEN |
| M09 | N: originale Undo-/Redo-Bindings und frühe Delegation | F01; neuer eigener Windows-Nativlauf fehlt |
| M10 | N/T: Cut mit gebundenem Clipboardticket | Verzögerter Exportfehler, Abbruchvarianten und F04 |
| M11 | T: WholeLine-Cut und deletable segments | Rechteck-/Teilreadonly-/Formatsemantik nicht vollständig geprüft |
| M12 | N/T: gebundener Pasteawait und Commit | F04; echte Provider-/Fokus-/Abbruchkombinationen |
| M13 | T: Plaintext, Newlines, Tabnormalisierung | Native Format-/Ereignisäquivalenz, Rechteck/WholeLine-Paste AP3 |
| M14 | O: SearchPanel-eigene Replacebindings unvermittelt | AP3-Panelwrapper vor Wirkung und Observermatrix |
| M15 | O: SearchInputHandler-Fallback nicht eigener AP2.1-Guard | AP3 getrennt von Editing-Bindings integrieren |
| M16 | O: Find/Panelinstallation keine neue O-/L-Kette | AP3 Templates, neu erzeugte Panelinstanzen |
| M17 | T: View.TextEntered-Präsentation in Observe | Completionannahme selbst benötigt Intent/Commit, kein vollständiger AP2.1-Commit |
| M18 | O: Completioncontroller-/Reopen-Subscriptions unverändert | AP3 session-/generationgebundene Beobachter |
| M19 | O: Historyservice/VM-Schreibcommand unverändert | AP3 ganze Aktion inklusive Remove/Insert/Dirty vermitteln |
| M20 | D: Textdrag vor Fokus deaktiviert | Reale OS-Textdroprouten abnehmen; Datei-Drop ausserhalb |
| M21 | D: frühe Optionsvermittlung erhält Textdrag=false | Native Kindcontrol-/Routingvarianten offen |
| M22 | D: Dragstart durch permanente Option verhindert | Kein laufender Drag-/Move-Drain nachgewiesen |
| M23 | D: Textmove nicht unterstützt/aktiviert | Keine nachträgliche Umwertung der historischen H2-Fälle |
| M24 | N: DeleteLine als Editingcommand | Native Einzel-/Fehlerprobe offen |
| M25 | N: Whitespace-Transformationscommands | Mehrschrittige Teilwirkung/Observerfehler AP3 |
| M26 | N: Upper/Lower/InvertCase-Routing | Unicode-/Selectionvarianten und Fehlersemantik offen |
| M27 | N: vorhandener TitleCase-Command vermittelt, keine GUI-Funktion hinzugefügt | D07 bleibt; NotSupported-Verhalten gesondert prüfen |
| M28 | N: Tabs/Spaces-Transformationsbindings | Varianten-/Gruppen-/Callbackmatrix offen |
| M29 | T: Tab-Handler eingeschlossen | IndentationStrategy-/Selectionfortsetzungen nicht vollständig vermittelt |
| M30 | N: ToggleOverstrike im Editing-Routing plus TextInput | Virtuelle-/Sonderauswahlvarianten offen |
| M31 | T: Copyticket neu; Navigation/Pointer/Find unverändert | TextCopied-Äquivalenz und reine Observerpfade AP3 |
| M32 | T: zwei konkrete Viewobserver plus internes Observe | Keine allgemeine Umhüllung aller Hostsubscriber; AP3 |
| M33 | T: reale WorkbenchTextArea, IME/Drag früh, Bridge-Setup | Vollständige Init-/Template-/Lifecyclefehler AP3/AP4 |
| M34 | T: frühe Document-Coerce-/Attach-/Detachbindung | F01/F03, restliche Dialog-/Commitphasen AP4 |
| M35 | O: Sidebar/Picker/Bestätigung unverändert | AP3/AP4 Intent vor Dialog, Revalidierung |
| M36 | O: Booklesen/-clear/-run unverändert | AP3/AP5 ganze Job-/UI-/Variablenkette |
| M37 | O: v1-UpdateEditor-Methodenkörper unverändert | AP6 Lease/v2/D01 und ganze Commitkette |
| M38 | O: v1-ExecuteEditor und EC21 unverändert | AP5 Sourceausgabe, AP6 Autorität/Transport |
| M39 | O: interaktive Returnausgabe noch ohne Coordinator-P-Gesamtticket | AP3/AP5 Originaloutput/Drainanbindung |
| M40 | O: Print/Info-Posts und Navigation unverändert | AP3 Job-/Lifecycle-/Observerbindung |
| M41 | O: asynchroner Savepfad unverändert | AP4 Generation, Provider, Lock-/Callbackdrain |
| M42 | O: synchroner Savepfad unverändert | AP4 neue Besitz-/Observergrenzen |
| M43 | T: lokaler Coordinator-Drain; Registry/App-Shutdown unverändert | F03; vollständiges L1/L2/L3, Provider/Timer AP4/AP6 |
| M44 | T: vorhandene späte Guards um Coordinatorzustand ergänzt | Keine vollständige raw-API-Barriere; F01/F04 beachten |
| M45 | O: Variablen-ModifyValue/Buttons unverändert | AP3 GUI-/State-/Observerkoordination |
| M46 | O: Parameter-/ModifyVariable/DataGrid unverändert | AP3 vollständige Binding-/Commandkette |
| M47 | O: Passwortprovider-/Pipelinekette unverändert | AP4 echter Provider-/Dialog-/Antwortdrain |
| M48 | O: v1-Transport/Statusintegration unverändert | AP6 v2/Lease/Epochfence/Disconnect |
| M49 | O: KeyMap-Initialisierungsrun unverändert | AP3 Besitz-/Job-/Lifetimebindung |
| M50 | O: Shared.Refresh/Variablenpräsentation unverändert | AP3 Capture-/Observer-/Detachprüfung |
| M51 | O: TwoWay-Variablen-/Parameterbindings unverändert | AP3 Modell-/Selection-/Generationsbindung |

### 10.2 Alle 31 Observergruppen

| ID | Tatsächliche produktive Vermittlung in AP2.1 / Restpflicht |
|---|---|
| OBA01 | T: TextEntering unter frühem nativen Ticket; ursprünglicher MutationLocked-Handler bleibt |
| OBA02 | T: alte UpdateStarted/Changing-Guards um Coordinatorzustand ergänzt; kein globaler Raw-Undo-Schutz |
| OBA03 | T: TextEntered in Observe; TextEntering als mutierender nativer Teilpfad, Return/Completionfortsetzungen offen |
| OBA04 | Neu konkret vermittelt: View.Caret.PositionChanged → ObserveEditor → Observe → StatusText |
| OBA05 | T: Bridge/Coordinator-Lifecycle integriert, übrige Attached/Detached-/Disposekörper nicht pauschal geschützt; F01 |
| OBA06 | O: Overload-Closed-/Propertycallbacks unverändert |
| OBA07 | O: Completioncontroller Caret/KeyDown/DocumentChanged nicht separat umgestellt |
| OBA08 | O: Completioncontroller UpdateStarted/Changed/UpdateFinished unverändert |
| OBA09 | O: CompletionWindow.Closed unverändert |
| OBA10 | O: InfoCompletionData-Abschlussdelegate unverändert |
| OBA11 | O: ReopenPolicy.Changing unverändert |
| OBA12 | T: bestehende Bridgebeobachtung plus Originalnachbeobachtung in Finish; kritische Eintrittsfehler OPEN |
| OBA13 | O: HistoryService/VM-Schreibcallback unverändert, später als Aktion vermitteln |
| OBA14 | O: Print-Post/Lifecycle/Output-P nicht vollständig angeschlossen |
| OBA15 | O: Info-Post/Navigation unverändert |
| OBA16 | O: KeyMap-Info-/Initialisierungsposts unverändert |
| OBA17 | O: allgemeine ViewModelBase/Status/Sidebar/Variable-Propertydispatchs unverändert |
| OBA18 | O: direktes BindableBase in Main/Info/KeyMap unverändert |
| OBA19 | O: Diagnostics-Collection-/Folgeproperties nicht generell geschützt |
| OBA20 | O: Variablen-/Parameter-/Selectionpräsentation unverändert |
| OBA21 | O: InfoHistory/DataContext/Markdownnavigation unverändert |
| OBA22 | T: Document-Coerce-Grenze; keine generelle IsModified/IsReadOnly-/Propertyobserverhülle |
| OBA23 | O: ReplaceConfirmation-/Cancellation-/Dispatcherpfade unverändert |
| OBA24 | O: Passwortdialog-UIcallbacks unverändert |
| OBA25 | O: Passwort-OwnerClosed/Cancel/Post unverändert |
| OBA26 | O: Registry-Timer/Retention/Waiting/Publish nicht an neue Tickets angebunden |
| OBA27 | O: Pipeline-/Runner-Callbacks unverändert; bestehende Pipeline keine neue Coordinatorgarantie |
| OBA28 | O: beforeLine/afterLine/Present/appendLine unverändert |
| OBA29 | O: Save Picker/Overwrite/Delete/Write/Unlock unverändert |
| OBA30 | O/T: vorhandenes Service-Dispose detacht; App-/VM-Gesamtdrain nicht migriert |
| OBA31 | T: LostFocus invalidiert Generation (F03), Options/IME/Textdrag früh; Selection/Anchor/LineTracker/Template nicht vollständig vermittelt |

Diese Aufstellung unterscheidet neue Produktvermittlung von in Tests explizit mit Observe umhüllten Angreifer-Callbacks. Aus einer erfolgreichen injizierten Document-/Selectionprobe folgt keine automatische Vermittlung aller entsprechenden Produkt-Subscriptions. Offene spätere Aufgaben bleiben Gates; sie werden nicht pauschal zu weiteren AP2.1-Findings erklärt.

## 11. Sicherheits- und Freigabegrenzen

Bestätigt im geprüften Delta: keine neue MCP-Owner-Aktivierung, keine öffentliche Testumgehung, kein neuer HTTP-Listener, keine v2-Lease-/Credentialrotation oder Tombstonefreigabe, keine Bibliotheks-/Projekt-/Paketänderung, keine zweite Pipeline und keine EC21-Korrektur. Der vorhandene interne Registry-ActivateMcp-Hook stammt aus dem Vorbestand und publiziert keine neue Coordinator-MCP-Besitzphase. Bestehende v1-Pfade werden deshalb nicht als neue vollständige Lease-/Ownerabsicherung zertifiziert.

Der D02-S1-Modell-PASS bleibt an seine Annahmen gebunden. F02 widerspricht bereits dem erforderlichen lokalen Abschlussverständnis; ein Pendingzähler allein liefert kein vollständiges Produkt-L2-Zertifikat. Save, Passwort, Return, Registryjobs und alle Producer benötigen weiterhin reale Integrationstests. F01 ist trotz fehlender externer Übernahme ein lokaler Guardverlust und muss vor Verwendung der Basis korrigiert werden.

Historische H1–U2- und D02-S1-Evidenzen, aktuelle Architekturverträge sowie vorhandene uncommittete Arbeiten bleiben unverändert. Die abschliessende Byte-/Git-Prüfung wird am Ende dieses Berichts ausgewiesen. `.codex/config.toml` wurde vor jedem Inhaltszugriff ausgeschlossen und weder gelesen noch verändert. Keine native Windows-UI-Interaktion fand statt; daher wurde keine Prüffensterfreigabe benötigt oder fingiert.

## 12. Abschliessende Empfehlung

**CHANGES REQUIRED – AP2.1 derzeit nicht zur Abnahme empfehlen.** F01, F02 und F04 betreffen Besitzbindung, terminale Belege und frühe Commitzulassung; F03 betrifft die zuverlässige Beendigung des angebotenen Kontrollübergangs. Alle vier sind reproduzierte Fehler des neuen Grundkerns, keine bloss offenen Vollintegrationen aus AP3–AP6.

Empfohlen wird ein separat beauftragtes Korrekturpaket mit den vier beschriebenen Regressionen und anschliessendem erneuten Review. Der einmalige Headless-Disposefehler bleibt bis zu belastbarer Ursachenevidenz OPEN/INCONCLUSIVE. Neue native Windows-Nachweise sind von historischen U1-Ergebnissen zu unterscheiden; der akzeptierte CJK-Rest ist kein Ersatz für sonstige Eingabe-/Lifecycleprüfungen.

Keine automatische Korrektur, keine automatische Fortsetzung mit AP3 und keine produktive MCP-/HTTP-Freigabe durch dieses Ergebnis.
## Anhang A – Reproduzierbare zusätzliche Produktproben

Ausführung in einer frischen PowerShell-7.6/.NET-10-Sitzung. Den folgenden C#-Block als String in `$source` übernehmen, dann `Add-Type -TypeDefinition $source` ausführen. Aufruf:

```powershell
[AP21ReviewProbe]::Run('C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Mcp\CryptoWorkBenchGuiTest\bin\Debug\net10.0').GetAwaiter().GetResult()
```

Voraussetzung sind die unveränderten, bereits vorhandenen AP2.1-Test-/Produktbinaries und deren Abhängigkeiten. Kein Build, keine Produktdateiänderung, kein natives Windows-Fenster. Die vorhandene private Headless-Fixture dient nur zur Initialisierung; Reflection überbrückt interne Sichtbarkeit und verändert keine privaten Zustandsfelder. Alle vier Fehler werden durch Aufruf der vorhandenen Methoden und echte Dokument-/Fokusereignisse ausgelöst.

```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
public static class AP21ReviewProbe {
 static string dir; static Assembly tests, product;
 static readonly BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Get(object o,string n) {
  var t=o.GetType(); var p=t.GetProperty(n,F);
  return p!=null?p.GetValue(o):t.GetField(n,F).GetValue(o);
 }
 static object Call(object o,string n,params object[] a) {
  var m=o.GetType().GetMethods(F).First(x=>x.Name==n&&x.GetParameters().Length==a.Length);
  try{return m.Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}
 }
 static object Host()=>Activator.CreateInstance(tests.GetType("CryptoWorkBenchGuiTest.EditorCoordinatorTests+Host"),true);
 static async Task Close(object h)=>await ((IAsyncDisposable)h).DisposeAsync();
 static async Task<string[]> Body(){
  var log=new List<string>();
  var h=Host(); var e=Get(h,"Editor"); var a=Get(h,"Service"); var ca=Get(h,"Coordinator");
  var b=Activator.CreateInstance(product.GetType("CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService"));
  try {
   Call(a,"DetachEditor",Get(h,"Registration")); Call(b,"AttachEditor",e,null); var cb=Get(b,"EditorCoordinator");
   var d=Call(cb,"BeginDrain");
   var e2=Activator.CreateInstance(e.GetType()); Call(a,"AttachEditor",e2,null);
   var lost=Get(e,"Coordinator")==null; Call(Get(e,"TextArea"),"PerformTextInput","X");
   log.Add("F01 B.phase="+Get(cb,"Phase")+"; E.Coordinator=null:"+lost+"; text="+Get(e,"Text"));
  } finally {await ((IAsyncDisposable)b).DisposeAsync();await Close(h);}
  h=Host(); var c=Get(h,"Coordinator");
  try {
   var t=Call(c,"TryBegin");var cb=Call(t,"RegisterCallback");Call(t,"Dispose");bool premature=false;
   Call(cb,"Observe",(Action)(()=>{
    Call(cb,"Dispose");premature=((Task)Get(t,"Completion")).IsCompleted;
    throw new Exception("late failure");
   }));
   var receipt=Get(c,"LastReceipt");
   log.Add("F02 completionInsideCallback="+premature+"; receipt.error="+(Get(receipt,"Error")??"null")+"; observerFailures="+Get(c,"ObserverFailures"));
  } finally {await Close(h);}
  h=Host();c=Get(h,"Coordinator");object h2=null;
  try {
   var drain=Call(c,"BeginDrain");var before=Get(c,"Generation");h2=Host();var after=Get(c,"Generation");
   log.Add("F03 actualFocusLoss generation="+before+"->"+after+"; drainComplete="+((Task)Get(drain,"Completion")).IsCompleted+"; resume="+Call(c,"ResumeGui",drain)+"; sameDrain="+ReferenceEquals(drain,Call(c,"BeginDrain"))+"; phase="+Get(c,"Phase"));
  } finally {if(h2!=null)await Close(h2);await Close(h);}
  h=Host();c=Get(h,"Coordinator");e=Get(h,"Editor");var doc=Get(e,"Document");
  try {
   var ticket=Call(c,"TryBegin");Call(doc,"BeginUpdate");bool ran=false;
   var result=Call(c,"Commit",ticket,(Action)(()=>{ran=true;Call(doc,"Insert",4,"X");}),false,null);
   log.Add("F04 openUpdate commitRan="+ran+"; commitReturn="+result+"; text="+Get(e,"Text")+"; phase="+Get(c,"Phase"));
   Call(doc,"EndUpdate");
  } finally {await Close(h);}
  return log.ToArray();
 }
 public static async Task<string[]> Run(string directory){
  dir=directory;
  AssemblyLoadContext.Default.Resolving+=(ctx,n)=>{
   var f=Path.Combine(dir,n.Name+".dll");return File.Exists(f)?ctx.LoadFromAssemblyPath(f):null;
  };
  product=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"CryptoWorkBenchAvalonia.dll"));
  tests=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"CryptoWorkBenchGuiTest.dll"));
  var headless=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"Avalonia.Headless.dll"));
  var st=headless.GetType("Avalonia.Headless.HeadlessUnitTestSession");
  var start=st.GetMethods().First(m=>m.Name=="StartNew");
  var sa=start.GetParameters().Select(p=>p.ParameterType==typeof(Type)?(object)tests.GetType("CryptoWorkBenchGuiTest.EditorCoordinatorTests+TestApp"):p.HasDefaultValue?p.DefaultValue:null).ToArray();
  var session=start.Invoke(null,sa);
  try {
   var dispatch=st.GetMethods().First(m=>m.Name=="Dispatch"&&m.IsGenericMethodDefinition&&m.GetParameters()[0].ParameterType.ToString().Contains("Task"));
   return await (Task<string[]>)dispatch.MakeGenericMethod(typeof(string[])).Invoke(session,new object[]{(Func<Task<string[]>>)Body,CancellationToken.None});
  }finally{((IDisposable)session).Dispose();}
 }
}
```

Tatsächlich beobachtete Ausgabe:

```text
F01 B.phase=Releasing; E.Coordinator=null:True; text=baseX
F02 completionInsideCallback=True; receipt.error=null; observerFailures=1
F03 actualFocusLoss generation=2->3; drainComplete=True; resume=False; sameDrain=True; phase=Releasing
F04 openUpdate commitRan=True; commitReturn=True; text=baseX; phase=Faulted
```

Die Proben sind gezielte Gegenbeispiele, kein vollständiger Zustandsraumnachweis. Insbesondere wird damit weder eine reale OS-Clipboard-Reentrancy noch eine vollständige Host-/Registry-/Callbackintegration behauptet.
## Anhang B – Abschliessender Erhaltungsnachweis

Der SHA-256-Vergleich gegen die vor dem Review erfasste Baseline umfasst **24.536 Originaldateien: null Abweichungen, keine fehlenden Dateien**. Erfasst sind Produktdateien einschliesslich vorhandener bin/obj-Artefakte, historische H1–U2-/D02-S1-Evidenzen, AP2.1-Prüfarbeitsbereich und die in der Baseline aufgenommenen Git-Metadaten. Die verbotene Konfigurationsdatei war vom Inhaltsvergleich ausgeschlossen.

Die vollständige Dateiaufstellung des Produktbaums zeigt als einzigen neuen Pfad diesen Bericht. HEAD und Staging sind identisch; der Porcelain-v2-Status einschliesslich Branch-/Upstreamdaten unterscheidet sich ausschliesslich um:

```text
? docs/mcp/MCP-00-AP2.1-independent-review.md
```

Damit bleiben sechs vorher modifizierte getrackte Dateien und nun 26 ungetrackte Dateien, Staging leer. Keine testbedingte bin/obj-Änderung. Keine bestehenden Quellen, Tests, Architekturverträge, Assertions oder historischen Ergebnisse wurden verändert. Der zunächst eingeschränkte Git-Lesezugriff wurde mit genehmigtem rein lesendem Zugriff wiederholt; der vollständige abschliessende Vergleich war erfolgreich.

Maschineller Prüfbeleg: `C:\Users\stefa\AppData\Local\Temp\ap21-r1-verification.json`; Baseline: `C:\Users\stefa\AppData\Local\Temp\ap21-r1-baseline.json`. Die Strukturprüfung bestätigt alle zwölf geforderten Hauptabschnitte sowie jeweils genau eine Tabellenzeile für M01–M51 und OBA01–OBA31. Dieser Erhaltungsabschnitt wurde anschliessend nur an den neuen Bericht angefügt.