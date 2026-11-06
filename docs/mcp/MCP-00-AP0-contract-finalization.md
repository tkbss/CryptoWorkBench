# MCP-00-AP0-R1 – Contract Finalization & Owner Decisions Integration

Stand: 26.10.2026, Revision AP0-R1. **D01–D08 durch den Projekteigentümer genehmigt; keine produktive Implementierungs-, Teständerungs- oder HTTP-Freigabe.**

## 1. Executive Summary

Die R2-Architektur ist als Grundlage tragfähig: genau ein sichtbarer Editor, eine EditorBridge als Identitäts-/Revisionsquelle, ein EditorCoordinator für GUI-/MCP-Besitz, bestehende serviceeigene Jobs und dieselbe fail-fast Interpreterpipeline samt globalem Variablenbestand. AP0 konkretisiert deren Verträge; es implementiert sie nicht.

**O14 – genehmigter Vertrag (D01):** Jede beantwortete Editoränderung erhält einen strukturierten Wirkungsbeleg. `textEffect`, `historyEffect` und `finalization` sind getrennt. Ein bereits angewandter Edit mit Callbackfehler meldet `applied` und einen Fehler; ein unklarer Abschluss meldet `indeterminate` und sperrt neue Schreibarbeit über `faulted`. `Busy` und `RevisionConflict` garantieren nur dann Wirkungslosigkeit, wenn sie vor jedem nativen Mutations-/Historyeintritt entstehen. Kein Rollback, Stackaustausch oder automatischer Retry.

**O15 – genehmigte Designrichtung (D02):** 4.096 Erwerbs-IDs je Aktivierungsepoche, 256 je Principal innerhalb dieser Epoche und höchstens 4 MiB Lease-Protokollspeicher. Tombstones bleiben bis zum kontrollierten Epochenabschluss erhalten. Erst unwiderruflicher Credentialwiderruf, eine Admission-/Commitbarriere und vollständiger Job-/Callback-/Editordrain erlauben die Freigabe alter Tombstones und ein neues Kontingent. Kein Reset durch blosses Disable/Enable. Der formale und experimentelle Sicherheitsnachweis fehlt; bei nicht nachweisbarer Sicherheit gilt der konservative prozessgebundene Rückfall ohne Tombstonelöschung.

Die H4-IDs M01–M48 sind unten einzeln erfasst. Die unabhängige Produktsuche ergänzt M49 KeyMap-Initialisierung, M50 Variablenrefresh und M51 GUI-Modellbearbeitung. Observer inklusive verzögerter Info-/KeyMap-Posts, Overload-Properties, Dialogen und Registryfortsetzungen bleiben Teil von H. Ein fehlender Hostwrapper ist kein Fremd-API-Ausschluss.

**Bereitschaft AP2–AP6:** konkrete Entwurfsgrundlage, aber keine unbedingte Implementierungsbereitschaft. D01–D08 sind entschieden; D02 benötigt noch formalen und experimentellen Sicherheitsnachweis, und das native AP1-IME-Gate bleibt offen. U1-Korrektur, vollständige GUI-/Observerintegration, Savegeneration, EC21, einheitliche stateVersion und v2 fehlen im Produkt. AP7/8 bleiben gesperrt.

## 2. Verbindliche Architekturgrundlage

### 2.1 Rangfolge und Kennzeichnung

Verbindlich ist [MCP-00-R2](MCP-00-R2-consolidated-architecture-decision.md), insbesondere D1–D11 und O01–O16. Historische Grundlagen bleiben unverändert: [Architektur](MCP-00-architecture.md), [Implementierungsplan](MCP-00-implementation-plan.md), [Editor Coordination Design](MCP-00-editor-coordination-design.md), [MCP-01.4 Review](MCP-01.4-review.md), [Architekturentscheidung](MCP-01.4-editor-architecture-decision.md), [Validierung](MCP-01.4-validation.md), B1/B2-/U0-/Post-U0-Berichte im selben Verzeichnis. Abweichende alte Queue-, API- oder Freigabeaussagen sind historische Aussagen ihres Standes, keine Änderung von R2.

**D** = genehmigte R2-Vorgabe oder ausdrücklich genehmigte AP0-R1-Entscheidung D01–D08; **F** = aktueller Quell-/Metadatenbefund; **E** = vorhandener experimenteller Nachweis, nicht hier wiederholt; **P** = konkreter Entscheidungsvorschlag dieses Berichts; **O** = fehlende Entscheidung oder Evidenz. D01–D08 sind durch den AP0-R1-Auftrag vom 26.10.2026 genehmigt (Entscheidungsmatrix Abschnitt 12). D01 genehmigt den strukturierten MutationReceipt-Vertrag, D02 die epochengebundene Designrichtung und Ausgangslimits, D03/D04 die CAS-/v2-Regeln, D05–D08 die dort bezeichneten Produktgrenzen. Neue interne Mechanismen und Nachweispläne sind weiterhin P, keine bewiesene Implementation. Insbesondere bedeutet D02 weder bewiesene Replay-Sicherheit noch Produktfreigabe. „Muss“ bezeichnet den verbindlichen Zielvertrag, keine Umsetzungserlaubnis. R2 und historische Berichte werden nicht geändert.

### 2.2 Ursprünglicher AP0-Ausgangsstand und Prüfmethodik

Produktworktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`. Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`; Upstream `origin/master`, +4/-10. Staging leer. Sechs getrackte Änderungen: `WorkbenchAutomationRegistry.cs`, `WorkbenchAutomationService.cs`, `CryptoScriptEditViewModel.cs`, `SidebarViewModel.cs`, `CryptoScriptEditView.axaml`, `CryptoScriptEditView.axaml.cs`. 19 unversionierte Dateien: Control, vier Automation-Service-Dateien, zwei GUI-Testdateien und zwölf MCP-Berichte einschliesslich R2. Genau dieser uncommittete Stand wurde gelesen, nicht HEAD allein. Keine AGENTS.md im Produktbaum oder den geprüften Elternverzeichnissen gefunden.

Statische Prüfung der produktiven C#-/AXAML-Einstiege: Dokumentsetter, Insert/Remove/Replace/Clear, History, Commands, Bindings, Variablenzugriffe, Shared, Execute/Run, Subscriptions, Property-/Collectiondispatch, Dispatcher, Cancellation-/Timerdelegates. Frameworkpfade wurden an den installierten Hosteingängen und den vorhandenen H1/H3/U1-Quellanalysen abgeglichen. Keine Behauptung eines neuen nativen oder dynamischen Vollständigkeitsbeweises. Build-/Laufzeittests wurden nicht ausgeführt.

Quellverweise in den Matrizen verwenden diese Kürzel (Methoden sind die stabilen Suchanker):

| Kürzel | Produktdatei |
|---|---|
| CT | [Controls/WorkbenchTextEditor.cs](../../CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs) |
| EV | [Views/CryptoScriptEditView.axaml.cs](../../CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs) und zugehörige AXAML |
| VM | [ViewModels/CryptoScriptEditViewModel.cs](../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs) |
| SB | [ViewModels/SidebarViewModel.cs](../../CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs) |
| EB | [Services/Automation/EditorBridge.cs](../../CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs) |
| EO | [Services/Automation/WorkbenchEditorOperations.cs](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs) |
| SC | [Services/Automation/WorkbenchSaveCommit.cs](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs) |
| AS / AR | [WorkbenchAutomationService.cs](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs) / [WorkbenchAutomationRegistry.cs](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs) |
| Q / WP | [InterpreterHostQueue.cs](../../CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs) / [WorkbenchExecutionPipeline.cs](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs) |
| CC / RP | [InfoCompletionController.cs](../../CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs), einschliesslich InfoCompletionData / [InfoCompletionReopenPolicy.cs](../../CryptoWorkBenchAvalonia/Models/InfoCompletionReopenPolicy.cs) |
| VV / KM | [VariableViewModel.cs](../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs) / [KeyMapViewModel.cs](../../CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs) |

### 2.3 Evidenzregister und unveränderte Grenzen

Alle folgenden Dateien liegen unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/`; Quellkopien darin sind Experimente.

| E | Bericht | Verwendeter Befund / erhaltene Grenze |
|---|---|---|
| H1 | [H1](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h1/MCP-00-H1-feasibility.md) | 69/70, TitleCase FAIL; frühe Hostgrenzen/Return; kein Gesamt-GO |
| H2 | [H2](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h2/MCP-00-H2-feasibility.md) | 26/28, target-error Started/Changed FAIL; Move kann Quelle trotz Zielproblem löschen |
| H2R | [H2-R](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h2r/MCP-00-H2R-feasibility.md) | 44/44 neue Proben, darunter reproduzierte unsichere Altpfade; frühes Drag-Aus erforderlich |
| H3 | [H3](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h3/MCP-00-H3-feasibility.md) | 84/85; später ungeschützter Fokusobserver FAIL; explizites Observe positiv |
| H4 | [Integration](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h4/MCP-00-H4-feasibility.md), [M01–M48](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h4/MUTATION-PATHS.md), [Observer](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h4/OBSERVER-INVENTORY.md) | 50/50 begrenzte integrierte Proben; Legacy 433/434, EC21 FAIL; native/Sidebar/Dialog/Observervarianten OPEN |
| IME | [H4-IME](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h4-ime/MCP-00-H4-IME-feasibility.md) | 22 Headlessfälle positiv; Ctrl+Z-Störung; echte CJK-Sperre nicht bewiesen |
| U1 | [Undo/Redo](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h4-ime-u1/MCP-00-H4-IME-U1-review.md) | 15 Proben/18 native Shortcutfolgen positiv; native Bindings erhalten, keine neue Historyengine; nur Labor |
| U2 | [U2](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp00-h4-ime-u2/MCP-00-H4-IME-U2-feasibility.md) | INCONCLUSIVE; keine funktionsfähige CJK-Positivkontrolle, Null-HIMC beweist keinen Drain |

R2: leerer Textsetter kann History löschen. R4: Abschluss kann falsche Nachfolgeridentität erhalten. R6: Setter/Clear aus UpdateStarted kann nach Wirkung offenen Abschluss hinterlassen. R7: EndUpdate aus Ablehnungsbereinigung kann Normalisierer erneut aufrufen. Diese historischen Findings bleiben bestehen; H verhindert unterstützte Hostvarianten durch frühe Vermittlung, repariert damit nicht die breitere direkte AvaloniaEdit-API.

## 3. Komponenten und Zustandsverantwortlichkeiten

### 3.1 Einzige Autoritäten

| Komponente | Verantwortung im Zielvertrag P; vorhandene Grundlage F |
|---|---|
| EditorCoordinator | Genau ein serviceeigener Editor-Slot. Besitzphase, epochgebundene Lease, Kontrollgeneration, Active/ObserverDepth, Tickets und vollständiger Editor-/Callbackdrain. UI-affine Entscheidungen; Admission/Commit mit der Epochenautorität des Services serialisieren. Liefert Drainbeitrag, aktiviert selbst keine Epoche. Kein Text-/Revisionszähler oder Runner. |
| EditorBridge | Bestehende Registrierung; Originaldokument, editorId, Revision, konsistenter Snapshot. `ObserveText`/`ObserveDocument` auch vor reentranten Reads. Dokument-/Registrierungswechsel invalidiert alte Identität. Revision ist monoton, nicht zwingend +1 je Request. |
| WorkbenchTextEditor/kontrollierte TextArea | Früher Eintritt vor nativer Text-/Stackwirkung, native Algorithmen genau einmal delegieren. Native Undo-/Redo-Registrierung nach U1 erhalten. Drag und IME vor Erstfokus konfigurieren; Bestandslocks nur Bibliotheksmechanismen, keine eigene Besitzautorität. |
| WorkbenchAutomationService | Vertrauenswürdige Caller-/Grantprüfung, gemeinsamer GUI-/MCP-Eingang, Koordinatorzugriff, stateVersion und immutable Snapshots. Keine Rückabhängigkeit zum VM; Präsentationsadapter. |
| WorkbenchAutomationRegistry | Partial desselben Services; einzige serverseitige Epochenautorität, atomare Admissionfence, begrenzter Credentialkontext und Abschlusszertifikat nach 5.6. Bestehende executionId/Fingerprint-/Job-/Deadline-/Retention-/Cancel-/Quarantäneregeln bleiben; kein Reset des Executionbudgets durch Rotation. Hält Editor-Ticket bis Ressourcen-/Callback-/Ergebnisabschluss. Keine zweite Jobregistry. |
| InterpreterHostQueue | Bestehendes prozessweites `Reserve()` mit `Wait(0)`; kein Warten auf Konkurrenz. Reservation für einzelne Mutationen/Save/Run, niemals für zehn Minuten Lease. |
| WorkbenchExecutionPipeline | Bestehender Parserreset, Runner je Parse-Einheit, gemeinsamer Variablenbestand, Diagnose-/Outputkopie, Passwortcallbackdrain. Keine Leasezustände im Interpreter. |
| MCP/Abstractions | Hostneutrale v2-DTOs, Schemas, sichere Fehlerprojektion. Transport hat weder Controlreferenzen noch Dictionary-/Runnerzugriff. HTTP lebt kürzer als angenommene Jobs. |

`lineActive`, `bookActive`, `IsReadOnly`, `IsBusy` und UI-CanExecute sind Hinweise/abgeleitete Mechanismen. Sie dürfen keinen Besitz vergeben. Das bestehende `AsyncLocal` für Jobkorrelation bzw. Reentrancyabwehr im Interpreter ist keine Mutationsberechtigung und wird nicht dafür erweitert.

Phasen bleiben R2: `unavailable`, `gui`, `acquiring`, `mcp`, `releasing`, `faulted`. Active/ObserverDepth/Pending sind orthogonale Felder desselben Coordinators. Keine zusätzlichen Runner- oder Interpretationszustände. Mutationseffekt ist ein Antwortbeleg, kein paralleles Besitzmodell.

### 3.2 Verbindliche Zielreihenfolge; Umsetzung weiterhin ausstehend

1. Transport validiert strikt v2 und Größen; Service authentisiert Principal, aktuelle ActivationEpoch und Grants. Fremddetails bleiben verborgen. Instance prüfen. Bei Job-Retry zuerst autorisierte bestehende executionId/Fingerprint-Auflösung; ein alter Job braucht keine neue Lease.
2. Auf UI und an der gemeinsamen atomaren Admission-/Commitgrenze: für MCP den ursprünglichen authentisierten Epochenkontext gegen die aktive, nicht schliessende Epoche erneut prüfen; niemals gegen den inzwischen aktuellen Kontext austauschen. Fälligen Leaseablauf anhand suspendzählender monotoner Zeit vollziehen; Besitzer/Lease/editorId/Phase prüfen. Active/ObserverDepth und inkompatible Pending-Tickets prüfen. Noch keine Property-/Dialogcallbacks.
3. Für Serviceedit, Ausführung, Save, Book und Variablenmutation Q fail-fast reservieren. Kurze native GUI-Eingabe benötigt nur Coordinator-Zulassung; sie darf mit kompatiblem GUI-Return-Pending koexistieren. Acquire hält Q höchstens während atomarer Übernahmeprüfung, nicht über die Lease.
4. Bridge erfasst Originalregistrierung/Dokument; expectedRevision und gegebenenfalls stateVersion, Range/UTF-16/CRLF/Limits prüfen. Bei Fehler Q freigeben, keinerlei native Mutation beginnen. Revisionsprüfung erfolgt damit nach Admission; Fehlerpriorität Busy vor RevisionConflict ist zulässig und dokumentiert.
5. Angenommene Operation samt Bindung/Ticket und Completion **vor** IsReadOnly-/Status-/Dialogcallback veröffentlichen. Registrierung von Job und Reservation atomar im bestehenden Service. Vor Veröffentlichung/Acceptance auftretender Fehler rollt nur hostinterne Zulassung zurück, niemals Dokumenteffekte.
6. Bei Bestätigungsawait: kein `RunUpdate` offen. Ticket/Reservation bleiben gebunden. Anschliessend Rechte, Epoche, Leasefrist/-ID, Generation, Originalidentität, Revision, gegebenenfalls stateVersion und Zustimmung erneut prüfen. Renewal ändert Generation nicht. Releasing verhindert neuen Commit/Runnerstart.
7. Vor nativer Wirkung Active setzen, private einmalige Delegation verbrauchen, native Operation ausführen. Jeder Hostobserver läuft kontrolliert; reentrante neue Schreibaufträge werden vor Wirkung abgewiesen. Angenommene Book-/Returnfortsetzungen benutzen ihr explizites Ticket, keine ambient Berechtigung.
8. Originaloperation schliesst ihre eigenen Gruppen in ihrer normalen finally-Kette. Wirkungsbeleg aus Originaldokument/Bridge und beobachtbarem Historyabschluss aufnehmen; Dirty-/Präsentationsfehler separat erfassen. Kein universelles `while(IsInUpdate) EndUpdate`, kein künstliches Reparaturundo.
9. Notwendige UI-/Passwort-/Cancellation-/Ressourcencallbacks drainen, immutable Ergebnisse kopieren; Registryfinalisierung abschliessen. Ticket wird auf UI finalisiert, Q erst nach zugehöriger Arbeit freigegeben. Keine Sperre über await halten, wenn ein Callback dieselbe Sperre braucht; niemals synchron unter `sync` auf Dispatcher warten. Off-UI Registryabschluss postet Ticketabschluss ohne `sync`-Lock und wartet asynchron darauf.
10. Erst nach gebundenem Drain und Freigabe der zugehörigen Q-Reservation GUI/unavailable veröffentlichen. Der Ticketabschluss darf zuvor nur als drainbereit markiert werden; die endgültige Phasenpublikation folgt als gebundener UI-Schritt ohne offene Registrylocks. Antwort aus eingefrorenem Beleg projizieren; danach kann HTTP scheitern, ohne die Wirkung rückgängig zu machen. Faulted bleibt schreibgesperrt bis kontrollierter Recovery mit Konsistenznachweis/neuer Identität.

Operationserfassung bedeutet nicht Leaseverlängerung. Ein Intent vor Clipboard/Picker ist noch kein angenommener Commit: Acquire darf ihn entwerten. Ein Save nach Commitgrenze, eine offene native Gruppe und Returnausgabe nach Runnerende halten dagegen Drainpflichten.

### 3.3 Interne Schnittstellenskizze (Spezifikation)

```csharp
// Hostintern; keine transportierbaren Delegates oder Avalonia-Objekte.
Admission TryBegin(CallerContext caller, OperationKind kind, ExpectedBinding expected);
GuiIntent CaptureIntent(IntentKind kind); // D/id/revision/generation/caret/selection
Admission TryCommit(GuiIntent intent);
void Observe(ObserverId id, Action nonMutatingCallback);
Task<MutationReceiptV2> FinalizeAsync(OperationTicket originalTicket);
Task ReleaseAsync(LeaseBinding originalLease); // idempotenter Drain, kein GUI-unlock hier
```

Admission liefert entweder Ablehnung vor Wirkung oder genau ein Ticket. Tickets sind nicht fälschbare Hostobjekte mit Originalregistrierung, erlaubter Fortsetzungsart und einmaligem Abschluss. `Observe` stellt keine Tickets aus. Frameworkinterne native Delegation ist eng, synchron und vor Callback verbraucht. Neue UI-Commands innerhalb eines Observers erhalten kein Fortsetzungsrecht.

## 4. Editoroperationen und Wirkungs-/Fehlersemantik (O14)

### 4.1 DTO und Invarianten

D01: `EditorMutationReceiptV2` enthält `contractVersion`, `instanceId`, ursprüngliche `editorId`, `expectedRevision`, nullable `observedRevision`, `textEffect`, `historyEffect`, `finalization`, nullable `error`, `retryPolicy`, `requiresStateRefresh`. Optionales Erfolgssnapshot-Metadatum ist nicht nötig für Wirkungserkennung; Scripttext gehört nicht in diesen Beleg. `observedRevision` gilt ausschliesslich für die ursprüngliche editorId, auch wenn danach eine neue Registrierung existiert.

| Feld/Werte | Bedeutung und Begründung |
|---|---|
| textEffect: `none`, `applied`, `partial`, `indeterminate` | `none` ist nachgewiesene Wirkungslosigkeit; `applied` vollständig erwarteter Textedit; `partial` nachgewiesener Teil einer Compositeoperation; `indeterminate` fehlender Nachweis. Versionsänderung allein beweist nicht vollständige Zielwirkung. |
| historyEffect: `none`, `applied`, `indeterminate` | Separat, weil History ohne Textänderung betroffen sein kann (R2, Stackpop, No-op-Setter). Keine Offenlegung des Stackinhalts. |
| finalization: `complete`, `faulted` | Complete beweist geschlossene eigene Gruppen/Locks und sicheren Folgezustand, nicht zwingend fachlichen Erfolg. Faulted sperrt weitere Mutationen; Text kann trotzdem sicher applied sein. |
| observedRevision: Dezimalstring oder null | Zuverlässig kopierte Originalrevision, keine geschätzte +1 und keine Revision des Nachfolgers. null bei fehlendem sicheren Snapshot. |
| error: null oder SafeError | Fehlerkategorie unabhängig von Wirkung. Statischer Code/messageKey; keine Exception.Message, keine Script-/Schlüsselinhalte. |
| retryPolicy: `noAutomaticRetry`, `sameRequestOnly` | Edit grundsätzlich noAutomaticRetry. sameRequestOnly ausschliesslich für ausdrücklich idempotente Lifecycle-/Jobretrievalverträge; kein generelles „retryable: true“. |
| requiresStateRefresh | Bei Konflikt, Callbackfehler, Teil-/Unklarwirkung true. False bei vollständig bestätigtem Erfolg/No-op. Kein Ersatz für Auth oder Revision. |

| Situation | text / history / Abschluss | Antwort / Folgeverhalten |
|---|---|---|
| Auth/Owner/Busy/Range/Revision vor nativer Wirkung abgewiesen | none / none / complete | Fehler, observedRevision nur falls autorisiert sicher gelesen; Konflikt verlangt Refresh. |
| Gültiger No-op (identischer Replace oder leerer Insert/Append) | none / none / complete | Erfolg; kein Dialog, keine Undo-Aktion, gleiche Revision. Prüfung von Lease und Revision bleibt erforderlich. |
| Vollständiger Edit und normaler Abschluss | applied / applied / complete | Erfolg, tatsächliche Revision; genau eine native Undo-Gruppe je MCP-Edit. |
| Änderung vollständig, danach kontrollierter Observerfehler | applied / applied / complete, sofern belegbar | `CallbackFailed`, MCP isError=true, Wirkung ausdrücklich erhalten. IsModified-Fehler ist kein Busy. |
| Teil einer mehrschrittigen Hostoperation angewandt | partial / applied oder indeterminate / complete oder faulted | `MutationFailed` bzw. `FinalizationFailed`; kein Rollback. Für simplen v2-Insert/Replace kein künstlicher partial-Wert ohne Nachweis. |
| Text angewandt, Abschluss/History unklar | applied oder indeterminate / indeterminate / faulted | `FinalizationFailed`/`EditorFaulted`, Zustand abfragen, kein weiterer Edit. |
| Undo-/Redo-Handler wirft | tatsächlicher Textbeleg; History ggf. indeterminate | Kein none aus unverändertem Text/CanUndo ableiten. Native Folgefähigkeit prüfen, bei fehlender Gewissheit faulted. Kein Raw-Undo-MCP-Tool. |
| HTTP-Antwort verloren | Client hat keinen Receipt | Client betrachtet Ausgang als ungewiss; GetStatus/GetEditor, niemals neuen expectedRevision-Wert blind übernehmen und wiederholen. |

Wenn DTO-Validierung/Auth bereits vor Identitätsprüfung scheitert, wird ein einfacher sicherer v2-Fehler mit `none/none` nur für diese Anfrage geliefert, ohne fremde observedRevision. Ungültige UUIDs dürfen nicht in ein gültiges Receipt erzwungen werden; Protokollfehler werden vor Dispatch gemeldet. Keine Aussage über andere gleichzeitig laufende Operationen.

### 4.2 Mess- und Fehlergrenze

F: EO.UpdateEditorAsync speichert `originalVersion`, fängt Callbackfehler, ruft gegebenenfalls `EndUpdate`, setzt IsModified und erstellt Snapshot. Ein nachgewiesener Edit kann dabei als Erfolg erscheinen, obwohl callbackFailure gesetzt wurde; bei fehlendem `after` entsteht ein gewöhnliches InternalError. Die aktuelle Reply-Struktur trägt keinen differenzierten Beleg. Ein unverändertes Textversionobjekt ist ausserdem kein Historybeweis.

P: Aufnahmepunkte sind vor Handler-/Stackeintritt, nach nativer Wirkung und nach finalem Gruppen-/Identityabschluss. Sichere Beobachtung darf selbst keine unkontrollierten UIcallbacks auslösen. Normale Completion-/Dirty-/Caretfehler werden gesammelt und nicht verschwiegen. Das Ergebnis ist erst nach relevanten Finalizern terminal; ein zuvor kopierter Textsnapshot bleibt als Wirkungsevidenz erhalten, wird aber bei einem späteren Finalisierungsfehler nicht als voll erfolgreicher Abschluss ausgegeben. Reentrante Read-Aufrufe liefern einen letzten abgeschlossenen Snapshot mit entsprechender Stale-/Busyinformation oder Busy; keinen halbfertigen editorId/revision/text-Mix.

U1 fordert Prüfung vor Stackpop und Erhalt beider nativer Bindinglisten. Nach Fehler dürfen `CanUndo/CanRedo` allein nicht den Nachweis von korrekter Gruppenstruktur ersetzen. H-Tests müssen echten Undo, Redo und Folgeedit überprüfen. R6/R7 dürfen nicht durch neue Catch-all-EndUpdate-Schleifen kaschiert werden.

### 4.3 Verlorene Antworten und Transport

Edits erhalten keine neue unbegrenzte Deduplizierungsregistry. Eine wirksame Änderung erhöht die Bridge-Revision; ein identischer Retry mit alter Revision erhält RevisionConflict, auch nach Undo zum gleichen Text. Das beweist dem Client jedoch nicht, welcher Request wirkte. Bei Antwortverlust muss er den aktuellen autorisierten Zustand prüfen und seine fachliche Absicht neu beurteilen; kein automatischer Retry mit aktualisierter Revision. Bei No-op kann Textgleichheit keine frühere Zustellung beweisen.

MCP-Toolresultat trägt den Beleg in `structuredContent`. `isError=true` bei Host-/Callback-/Finalisierungsfehler; Erfolg mit No-op bleibt false. Scriptdiagnosen bleiben im Jobresultat. Die vorhandene AutomationToolResults-Regel bleibt: SyntaxError/Aborted/Cancelled/TimedOut/Failed/Rejected ergeben bei executionCall ebenfalls isError=true; das bedeutet weder fehlende Annahme noch Wirkungslosigkeit. Kein HTTP-/JSON-RPC-Fehler ersetzt das strukturierte Ergebnis. HTTP 5xx/Disconnect kann keinen „none“-Beleg ersetzen. Sicherer Textteil: feste Meldung „Editor operation failed after an applied change; refresh state.“ ohne Nutzlast, Stacktrace, Pfad, Principal oder Leasecredential.

## 5. Lease-v2, Idempotenz und Kapazitätsgrenzen (O15)

**D02 ist eine genehmigte Designrichtung mit noch erforderlichem formalen und experimentellen Sicherheitsnachweis.** Die folgenden atomaren Übergänge sind deren konkrete Spezifikation P, keine bereits implementierte oder bewiesene Garantie. D03 genehmigt den einzelnen letzten CAS-Receipt, D04 striktes v2, D08 die suspendzählende Frist. Rückfall und Nachweisgates stehen in 5.8/5.9.

### 5.1 Bindung, Zeit und Autorisierung

Jede Lease bindet `(instanceId, editorId, PrincipalId, ActivationEpoch, leaseId)`. Principal/Epoche kommen ausschliesslich aus serverseitig geprüften Credentials, nie aus Toolargumenten. Der authentisierte Caller ist immutable: eine alte Anfrage darf bei späterem Dispatch weder neu mit der aktuellen Epoche etikettiert noch durch den Service automatisch mit neuen Credentials wiederholt werden. Gleicher Principal oder gleiche UUID genügt nicht. Lokale GUI-Aktionen erhalten keinen künstlichen MCP-Epochenkontext; sie prüfen dieselbe Coordinator-/Shutdown-/Übergangsbarriere und werden bei angenommenen Wirkungen in den globalen Drain einbezogen.

P: Ein servereigener, monotoner, nicht zurücksetzbarer `epochSerial` innerhalb der instanceId identifiziert logisch die Epoche. Ein eventuell zusätzlich verwendeter Guid-Handle ist nur deren opake Darstellung. Zählerüberlauf sperrt weitere Aktivierung; kein Wrap. Bei vollständigem Prozessneustart werden neue instanceId und neue Credentialwurzel erzeugt, alte Instanzcredentials werden nicht übernommen. Ein nicht authentisierter clientseitiger Epochenwert ist kein Ersatz. Widerrufsentscheidung erfolgt durch exakten Vergleich mit dem **einzigen aktiven** Kontext und dessen Annahmeflag; eine unbegrenzt wachsende Liste sämtlicher alter Epochen ist nicht erforderlich und darf nicht zum Speicherbypass werden.

Acquire/Renew verlangen editor.control + editor.write; Edit zusätzlich gültige aktuelle leaseId und editor.write, Execute zusätzlich execution.run und konkrete lokale Zustimmung. D04: editor.control und leaseId sind zusätzliche, niemals allein ausreichende Bedingungen; Edit/Execute prüfen ebenfalls editor.control. Release verlangt editor.control und identische eigene aktive Epoche/Principalbindung; Entzug von editor.write verhindert freiwilligen Release nicht. Nach vollständigem Epochenwiderruf werden auch Release-/Resultat-/Cancelrequests mit alten Credentials Forbidden; lokale Abschluss-/Cancelrechte bleiben intern verfügbar. Lease-IDs authentisieren nicht.

Anfangslease und jede erfolgreiche Erneuerung: Annahmezeit + 600 Sekunden, nicht Restzeit + 600. Keine implizite Verlängerung; keine absolute Höchstdauer bei rechtzeitigem Renew. `now >= deadline` bedeutet abgelaufen, auch ohne zugestellten Timer. leaseVersion verändert weder Revision noch stateVersion noch Jobdeadline.

**D08 – Zeitvertrag:** Benötigt wird eine pro Plattform verifizierte, innerhalb der Instanz monotone elapsed-time-Quelle, deren Fortschritt Standby/Suspend vollständig einschliesst. TimeProvider.System/Stopwatch oder eine Uhr mit „monotonic“-Namen sind ohne Plattformnachweis nicht ausreichend. UTC dient nur zur Darstellung von acceptedAt/expiresAt; Rückstellen der Wanduhr darf die Frist nie verlängern. Ein plattformgerechter Adapter muss Auflösung, Überlauf, Suspend/Hibernate-Verhalten und Wiederaufnahmegarantie dokumentieren. Keine bestimmte native API wird hier ungeprüft als geeignet freigegeben.

Vor Wiederaufnahme von mutierender GUI-/MCP-Zulassung und vor jedem Renew/Commit prüft der Host die suspendzählende Frist. Resumehandler und normaler Requesteingang verwenden dieselbe Prüfung; verspätete Resumezustellung darf kein Zeitfenster eröffnen. Abgelaufen führt zu Releasing, bestehender notwendiger Drain läuft weiter. Bei unzuverlässiger oder zurückgesprungener Zeitquelle wird neue Arbeit gesperrt und die Lease sicher entzogen; eine nicht beweisbar gültige Restfrist wird nicht verlängert. Beispiel: Acquire 09:00, Suspend 09:03, Resume 09:12 bedeutet Ablauf, auch wenn nur drei aktive Minuten gemessen wurden. Resume 09:08 lässt höchstens zwei Minuten Restfrist; ein danach ausdrücklich erfolgreich angenommenes Renew setzt regulär zehn Minuten.

Timer tragen instanceId, unveränderliche Epochbindung, leaseId, leaseVersion und Originaldeadline. Nach Dispatch prüfen sie diese Bindungen und die aktuelle Zeit; alte Timer sind wirkungslos gegenüber neuer Lease/Epoche. Timer sind Weckhinweise, niemals Autorität für Fristgültigkeit. Timerinstallationsfehler vor Acquire-Veröffentlichung verhindert Annahme; spätere Timerfehler sperren neue Arbeit und lösen sicheren Entzug/Drain aus. Verpasste Timer dürfen keine unbefristete Lease erzeugen.

### 5.2 Acquire und Replay

Schlüssel: `(instanceId, authentisierte ActivationEpoch, PrincipalId, acquisitionId)`; Fingerprint über normalisierte contractVersion, instanceId, editorId und expectedRevision. Auth-/Epochenprüfung geschieht vor Lookup und **erneut atomar mit Annahme**. Slots enthalten erfolgreiche und gültig verarbeitete abgewiesene Anfragen. Slotreservierung, Entscheidung und Receiptpublikation erfolgen unter derselben serialisierten Admissiongrenze vor UI-Observern; Callbacks dürfen nicht zwischen Epochenprüfung und Besitzänderung einschieben.

| Fall | Verbindliche Semantik |
|---|---|
| Neue ID, aktuelle Epoche, GUI/idle, gültige Revision, Q frei | Slot reservieren; Lease und Receipt atomar erfassen; Generation erhöhen, Phase MCP; Q nur kurz, nicht über Lease halten. |
| Zwei identische Requests in derselben Epoche | Genau eine Entscheidung/Lease. Replay liefert den eingefrorenen Receipt, keine neue Übernahme/Frist. |
| Gleiche ID mit abweichendem Fingerprint innerhalb derselben Epoche | IdempotencyConflict, keine Wirkung. |
| Verlorene Antwort bei aktiver Lease | Identischer Retry im ursprünglichen Credentialkontext liefert historische Acquire-Daten, replayed=true und momentanen leaseState. Kein neues Acquire. |
| Retry nach Leaseende innerhalb derselben noch aktiven Epoche | Alter Receipt oder AcquisitionRetired aus Tombstone, niemals Wiedererwerb. |
| Neue ID bei bestehendem Besitz, auch gleicher Principal | EditorOwned; keine Übertragung oder Verlängerung. Keine fremden Leasedetails. |
| Busy/RevisionConflict/EditorUnavailable | Terminale Entscheidung unter dieser ID speichern; später günstige Bedingungen reaktivieren sie nicht. Neuer fachlicher Versuch benötigt neue ID. |
| Kapazität/Bytebudget ausgeschöpft | CapacityExceeded vor Wirkung. Annahmebudget dieser Epoche bleibt erschöpft; keine Freigabe durch Receiptkompaktierung. Details siehe 5.5. |
| Alte Epoche, auch bereits authentisiert vor Disable | Forbidden an Admission-/Commitgrenze, vor jedem Tabellenzugriff mit Wirkung. Keine neue Berechtigung durch dieselbe instanceId oder denselben Principal. |
| Identische acquisitionId unter neuen Credentials der sicher eröffneten Folgeepoche | Darf als **neue** Anfrage akzeptiert werden, weil der serverseitige Epochkontext verschieden ist. Kein Retry/Transfer des alten Auftrags; eine alte gespeicherte Anfrage wird niemals automatisch umgebunden. |
| Alte leaseId in neuer Epoche | Keine neue Berechtigung; LeaseNotFound/Forbidden nach Authregeln. Neue Lease-ID wird serverseitig frisch und innerhalb der Instanz nie wiederverwendet. |

P: Lease-ID-Erzeugung muss Nichtwiederverwendung ohne Archiv aller alten IDs sicherstellen, etwa mittels nicht überlaufender serverseitiger Lease-Sequenz mit injektiver UUID-Kodierung innerhalb der instanceId. Alle gespeicherten Leasebindungen enthalten zusätzlich Epoche/Instanz. Ein altes leaseId-Bitmuster darf nach Tabellenfreigabe nicht neu ausgegeben werden; diese Invariante ist explizit zu testen. acquisitionId ist dagegen clientgewählt und darf nur im **neuen authentisierten Epoch-Namensraum** wiederverwendet werden.

Schema-/Authfehler belegen keine Slots. Reparierte neue gültige Anfragen sind neue Versuche. Es gibt weder wartende Akquisitionen noch automatische Reaktivierung bei Epochenwechsel. Lokales erneutes Pairing/Autorisieren ist kein Transportretry.

### 5.3 Renew als CAS ohne wachsende Receipthistorie

D03: Eine aktive Lease speichert höchstens den letzten erfolgreichen CAS-Receipt: inputVersion, outputVersion, acceptedAt, expiresAt. Aktuelle Epochbindung, Owner, Frist und Phase werden zuerst geprüft, dann CAS und Receipt atomar geändert.

- expectedLeaseVersion = aktuelle Version: einmal inkrementieren, Frist jetzt + 600 Sekunden.
- expectedLeaseVersion = letzter inputVersion und aktuelle Version = letzter outputVersion: denselben Receipt liefern, keine erneute Friständerung.
- Ältere/grössere Version: LeaseVersionConflict ohne Wirkung, Status neu lesen; kein automatisches Nachladen und Erneuern.
- Releasing, abgelaufene/widerrufene Lease oder nicht mehr aktive Epoche: kein Renewal, auch nicht als erneute Zustellung eines früheren Erfolgs mit neuer Wirkung.

Verlorene Antwort erlaubt nur Wiederholung derselben Version mit demselben authentisierten Kontext. Nach weiteren erfolgreichen Renewals muss ein älterer Receipt nicht rekonstruierbar sein. UInt64-Dezimalstrings sind kanonisch; Überlauf ergibt VersionExhausted, kein Wrap. Das ist keine absolute zeitliche Leasehöchstdauer. Eine rechtzeitig erneuerte Lease verhindert durch ihre lange Dauer keinen sicheren Speicherbound.

### 5.4 Release, Reclaim, Ablauf und Shutdown

Release ist durch ursprüngliche leaseId/editorId/Principal/Epoche gebunden, ohne neue Release-ID. Erster Aufruf setzt Releasing/Generation, Wiederholung erzeugt keine zusätzliche Freigabe/Cancelaktion. `releasing` bedeutet Entzug neuer Arbeit, nicht abgeschlossenen Drain. Nach Drain gilt released mit Grund released/expired/revoked/reclaimed/detached/shutdown. Alte Abschlüsse können keine Nachfolgerlease entsperren.

Innerhalb derselben aktiven Epoche löst Release zuerst eigene gespeicherte Leasebindung auf, dann gegebenenfalls Bridge; Dokumentwechsel verhindert den idempotenten alten Abschluss nicht. Nach Epochenwiderruf gewinnt Forbidden vor Receiptlookup. Alte Receipts sind dann keine extern abrufbaren Berechtigungen; lokale Finalisierung benutzt Originaltickets ohne neues Clientrecht.

Leaseende ist nicht automatisch Epochenende: normaler Release/Ablauf lässt die aktive Epoche und deren Tombstones bestehen. Nur kontrollierte lokale Deaktivierung/Rotation schliesst sie. Shutdown schliesst zusätzlich die gesamte Hostzulassung und darf niemals eine Folgeepoche aktivieren. Bereits begonnene native Wirkung/Savecommit werden sicher finalisiert; Run-Cancel/Deadline bleibt bestehender Vertrag, kein Rollback. Eine wartende Zustimmung wird invalidiert, kein neuer Runnerstart nach Widerruf.

### 5.5 Speicherbudget und sichere Kapazitätswiederverwendung

D02-Ausgangswerte: **4.096 acquisitionId-Slots pro aktiver Epoche, 256 je Principal darin, insgesamt höchstens 4 MiB Lease-Protokollspeicher.** Compact-Tombstones bleiben für die gesamte aktive und anschliessend schliessende Epoche erhalten. Innerhalb einer Epoche keine TTL-/LRU-Löschung der Replaybarriere. P: maximal 64 terminale Detailreceipts, höchstens 15 Minuten; Details werden im gleichen Slot kompaktisiert, aktive Bindung nie verdrängt. Tombstone enthält Principalreferenz, Epoche, acquisitionId, editorId, Fingerprint, terminalen Code und bei Erfolg leaseId/Grund. Keine Nutztexte/Secrets.

P: 2 MiB für bis zu 4.096 kompakte Slots mit budgetiertem Overhead; verbleibende 2 MiB für Details, aktive Lease, Indizes, Timer-/Epochenabschlussdaten. Tatsächlichen Managed-Overhead messen. Principalreferenzen fest begrenzen; keine unbeschränkte Nebenmap. Ressourcen für Renew/Release/Drain vor Acquire reservieren, Renewal überschreibt seinen einzigen Receipt. Wird Bytebudget vor Slotlimit erreicht, wird die Annahme neuer Erwerbs-IDs **für den Rest der Epoche verriegelt**, auch wenn spätere Detailkompaktierung Platz schafft. Dadurch kann eine nicht gespeicherte CapacityExceeded-Anfrage nicht später unter derselben Epoche wirksam werden. Bereits gespeicherte Retries sowie Renew/Release/Drain bleiben möglich. Principalquote analog bis Epochenende, globale Quote global innerhalb der Epoche.

Es existiert höchstens eine aktive **oder** schliessende Epochen-Tabelle, niemals zwei Kontingente parallel. Neue Tabelle erst nach vollständig abgeschlossenem und entsorgtem alten Kontext; eine fehlgeschlagene Neuallokation lässt den Host deaktiviert. Vorzugsweise denselben fest budgetierten Tabellenspeicher nach L2 leeren und wiederverwenden, statt zwei Heapgenerationen zu akkumulieren. Alte Caller/Responses dürfen nur begrenzte kopierte Identitätsdaten, keine Referenz auf die ganze alte Tabelle halten. Das 4-MiB-Limit umfasst den zurechenbaren lebenden Lease-Protokollspeicher samt Overhead; es ist keine Behauptung einer 4-MiB-Prozess-RSS-Grenze. Temporäre Serialisierungs-/Transportdaten bleiben separat begrenzt und dürfen keinen versteckten Receipt- oder Tombstonespeicher bilden. Begrenzte Aktivierungs-/Transportoperationen dürfen keine unbounded Pendingliste erzeugen. Kein „Enable“ pro Request, kein automatischer Reset bei Cap, kein fernaufrufbares Rotations-Tool. Wiederholtes lokales Disable/Enable ist erst nach Abschlusssequenz zulässig und erzeugt keine gleichzeitig lebenden Kontingente; neue Credentials sind gesondert lokal autorisiert. Langfristig viele **sequentiell sicher abgeschlossene** Epochen sind erlaubte Kapazitätswiederverwendung, kein Replaybypass.

Bestehende Execution-ID-/Resultatgrenzen bleiben separat: Epochenrotation setzt die 10.000-Execution-ID-Grenze der Registry oder andere Prozessbudgets **nicht** zurück und dupliziert sie nicht. Ergebnis-/Quarantänedaten werden nicht wegen Leasekapazität vorzeitig gelöscht. Epochen-/Credentialhistorie darf keine unbegrenzten revokedEpochs-/Key-/Auditlisten im Arbeitsspeicher erzeugen; bounded Logs ohne Secrets, genau ein aktueller Credentialkontext plus begrenzte Abschlussdaten.

### 5.6 Atomarer Epochenabschluss und Komponentenverantwortung

F: AR.ActivateMcp ruft derzeit RevokeMcpCore und setzt sofort epoch; RevokeMcpCore führt revokedEpochs, DrainAsync wartet auf active.Task/saveCompletion. Das beweist weder alle Editor-/Dispatcher-/Providerfortsetzungen noch sichere Kapazitätsrotation. **Dieser bestehende Hook darf nicht unverändert als D02-Implementation gelten.**

P: Der vorhandene Service/Registry bleibt einzige Autorität für Aktivierungsepoche und Credentials. Ein Abschlussflag und ein completion-Ticket konkretisieren dessen vorhandenen Lifecycle, kein zweites Besitzmodell und keine neuen Runnerzustände. Der Coordinator besitzt weiter GUI/MCP/Releasing/Faulted und meldet Originalticketdrain; die Registry besitzt Job-/Ressourcenabschluss. Credentialadapter und Transport authentisieren nur gegen die vom Service publizierte aktive Epoche. Zustandsnamen unten sind logische Übergänge dieses Lifecycle, keine zusätzliche Ausführungs-State-Machine.

| Übergang / Linearisierungspunkt | Atomare Wirkung und Abschlussbedingung |
|---|---|
| Active(E) → Closing(E), L1 | Unter gemeinsamer Admissionserialisierung: active credential reference deaktivieren, E nicht mehr annahmefähig, closeTicket erfassen; neue GUI/MCP-Zustandsarbeit für den Übergang sperren, alte Intents invalidieren. Vor externen Cancellation-/Propertycallbacks publizieren. Gleichzeitige Disableaufrufe teilen dasselbe Ticket. |
| Admission oder Commit gegen L1 | Prüfung des unveränderlichen Caller.E und Open-Flag und Erfassung des Auftrags/Commitrechts atomar. Gewinnt Anfrage vorher, ist sie registrierte Drainpflicht. Gewinnt L1, keine neue Wirkung. Kein check-then-await-then-write ohne erneute Prüfung. |
| Closing(E), ausserhalb Locks | Cancel/Providerclose anfordern; Job-, Dialog-, Save-, Return-, native Update-, Timer- und Dispatcherproduzenten schliessen/drainen. Keine UIawaits unter Registrylock. UI-Coordinator und off-UI Registry reichen gebundene Completiondaten weiter. |
| Closing(E) → Closed(E), L2 | Service prüft gemeinsames Abschlusszertifikat: keine offenen akzeptierten Jobs/Callbacks/Editorwirkungen; keine Producer können noch ungezählte Arbeit registrieren; keine offenen Gruppen/Locks/P-Tickets/Q-Reservationen; endgültige Resultat-/Diagnosekopien und Ressourcenabschluss; alle Producer sealed. Erst dann alte Lease-/Credential-/Tombstonedaten entsorgen. |
| Closed(E) → Active(E+1), L3 | Gesonderte lokale Aktivierung; Shutdown=false, keine Fault-/Quarantänelücke, gültiger Editor und globale Admission idle, neuer nicht wiederverwendeter Epochserial/Credentialkontext und erfolgreich angelegtes leeres Budget. Atomare Publikation nach vollständiger Vorbereitung; erst danach Credentials ausgeben/Requests annehmen. |

Ein `pendingCount == 0`, terminaler Jobstate, CancellationRequested oder abgeschlossener HTTP-Wait allein ist **kein** Abschlusszertifikat. Vor Post/Register eines bereits akzeptierten Jobs wird dessen Callbackticket gezählt; dessen Ende erst nach der letzten Fortsetzung. Vor Nullprüfung müssen Producer geschlossen sein, damit kein spätes Add nach Null möglich ist. Provider, die trotz Dispose noch Callback liefern können, benötigen einen nachgewiesenen vollständigen Callbackabschluss; bis dahin bleibt L2 gesperrt. Ein nur wirkungslos gemachter, aber weiterhin laufender oder noch ausstehender angenommener Callback erfüllt D02 nicht. Stale-Epoch-/Timerprüfungen bleiben zusätzliche Abwehr gegen alte, noch nicht angenommene Zustellungen oder fehlerhafte Provider, ersetzen aber keinen Drainbeweis bereits registrierter Callbackarbeit. Dispatcher-Barriere allein reicht nicht für externe Provider.

Bereits authentisierte, **noch nicht angenommene** HTTP-Requests dürfen physisch noch warten: sie behalten E und können nach L1 nur Forbidden liefern, weder Tabellenzugriff mit Wirkung noch neue Tickets. Sie gehören nicht zum accepted-job-drain, unterliegen aber den bestehenden begrenzten Transport-/Body-/Concurrencylimits. Alle bereits angenommenen Aufträge und deren Callbacks müssen vor L3 abgeschlossen sein. Lokale GUI-Aufträge, die bei L1 Dokument/Interpreter verwenden, werden ebenfalls über vorhandene Tickets berücksichtigt; während des Übergangs keine neuen inkompatiblen Zustandsaufträge. Lesen und lokale Cancel-/Rückforderungsbedienung bleiben möglich. Nach Closed darf GUI wieder nutzbar sein; L3 prüft erneut Idle, damit zwischenzeitliche GUI-Arbeit nicht übergangen wird.

Ein bereits angenommener Commit darf nach L1 nur seine notwendige Finalisierung beenden, keine neue unabhängige Mutation beginnen. Alte Epochgültigkeit wird dabei nicht als Clientrecht wiederhergestellt: explizites Originalticket autorisiert den Abschluss. Eine neue Run-Parseeinheit bzw. noch nicht gestartete Bestätigung revalidiert Zulassung/Cancel vor Beginn.

### 5.7 Fehler, dauerhaft blockierter Drain und Shutdown

| Fehlerzeitpunkt | Sicheres Verhalten; Kapazität |
|---|---|
| Fehler vor L1 | Kein teilweiser Reset. Alte Epoche bleibt nur dann aktiv, wenn Widerruf noch nicht zugesagt und Invarianten intakt sind; sonst Annahme sperren. Keine neue Epoche. |
| Fehler beim Credentialadapter nach L1 | Servicebarriere bleibt geschlossen und entscheidet unabhängig vom Cache. Keine alte Berechtigung wieder einsetzen. Provider-/Credentialdaten konservativ halten bis Abschluss; neue Epoche gesperrt. |
| Cancel-/Dispose-/Propertycallback wirft | Fehler sicher erfassen, Drain übriger Ressourcen fortsetzen; Throw ist kein Abschlussbeweis. Bei unklarem Zustand faulted, Tombstones/Budget unverändert halten. |
| Job/Passwortprovider/Save/Dispatcher hängt dauerhaft | Closing bleibt blockiert, kein Zeitablauf erzwingt L2. Neues Enable erhält Busy bzw. EditorFaulted, kein neues Kontingent. Lokale Diagnose/Cancel zulässig, kein Force-Unlock. Ressourcen wachsen nicht je erneutem Enable. |
| Warten auf Drain wird vom Aufrufer abgebrochen | Nur dieser Wait endet; closeTicket und serviceeigene Finalisierung bleiben, keine Kapazitätsfreigabe. |
| Fehler bei Ergebnis-/Abschlusskopie oder Q-/Lockfreigabe | Kein vollständiges Zertifikat; faulted/Closing, keine neue Epoche, kein behauptetes none nach Wirkung. |
| Fehler nach L2, vor/during L3-Vorbereitung | Deaktiviert bleiben. Nicht publizierte Credentials verwerfen; Epochserial nicht wiederverwenden; keine alte Tabelle reaktivieren. |
| Fehler nach L3-Publikation | Neue Epoche gilt als existent und muss ihrerseits über L1/L2 geschlossen werden; kein Zurückrollen auf alte Credentials/Epochen. |
| Shutdown während beliebiger Phase | stopping wird sticky; L3 endgültig verboten. Nötigen Drain abwarten, keine vorzeitige GUI-/Leasefreigabe. |

Bei nicht terminierendem Drain kann Verfügbarkeit nur durch später nachgewiesenen Abschluss oder einen ausdrücklich kontrollierten vollständigen Prozessneustart wiederhergestellt werden. Prozessbeendigung ist kein erfolgreicher Drainbeweis und garantiert keine Rücknahme externer Dateiwirkungen. Neuer Prozess verwendet neue Instanz/Credentials; keine automatische Beendigung, kein simulierter Epochreset im laufenden fehlerhaften Prozess. Sicherheit hat Vorrang vor garantierter Wiederverfügbarkeit.

### 5.8 Sicherheitsargument, Grenzen und konservativer Rückfall

Zu beweisende Invarianten:

1. **Eindeutige Autorität:** höchstens ein annahmefähiger Epochkontext; nur Service publiziert ihn, Caller.E bleibt unveränderlich.
2. **Epochfence:** jede neue Wirkung hat einen atomaren Admission-/Commitpunkt vor L1 oder wird abgewiesen. Vor L1 angenommene Wirkung ist vollständig im Abschlusszertifikat enthalten.
3. **Kein verlorenes Replaygedächtnis:** innerhalb E bleibt jede verarbeitete Erwerbs-ID bis L2 im begrenzten Ledger; Speicherablehnung verriegelt Neuannahme, statt abgewiesene ungespeicherte IDs später zulassen zu können.
4. **Keine Altwirkung nach Wiederverwendung:** L3 erfordert L2; nach L2 können alte Credentials/Caller/Timer kein Recht erhalten. Identische acquisitionId unter E+1 ist ein anderer authentisierter Namensraum, keine Wiederholung von E.
5. **Begrenzung:** eine Epochen-Tabelle <= 4 MiB, <= 4.096 Slots, <= 256/Principal; ein Renewreceipt; keine historischen Schlüssel-/Epochsets oder parallelen Closingtabellen. Executionbudget separat unverändert.
6. **Zeit:** Suspend zählt, stale Timer/Resume/Renew können Fristen oder Epochen nicht reaktivieren; bei ungewisser Zeit keine neue Schreibzulassung.

Fallunterscheidung für alten Request: Vor L1 angenommen → in Drain, daher vor L3 abgeschlossen. Vor L1 nur authentisiert → nach L1 Commitprüfung lehnt ab. Nach L1 angekommen → Auth/Admission lehnt ab. Verlorene Antwort → identischer Kontext bleibt gleich gebunden. Diese Argumentation ist **ein Beweisentwurf**, kein formaler Nachweis sämtlicher Produkt-/Frameworkpfade. Ein unregistrierter produktiver Callback widerlegt die Drainprämisse und blockiert Kapazitätswiederverwendung.

**Rückfall:** Kann Epochfence, Nichtwiederverwendung, Ressourcenbound oder vollständiger Drain nicht formal und experimentell nachgewiesen werden, wird die Wiederverwendung nicht implementiert/freigegeben. Konservatives Modell: 4.096 Erwerbs-IDs pro instanceId, 256/Principal über alle Epochen dieser Instanz, 4 MiB Budget, Tombstones bis vollständigem Prozessende; neue Aktivierung setzt das Budget nicht zurück. Keine Löschung wegen Alter/Cap. Nach Neustart neue instanceId/Credentials. Auch der Rückfall erlaubt keine Übernahme während unklarem Drain und repariert keine Callbacklücke; er verhindert nur Replay durch vergessenes Erwerbsgedächtnis. Keine stille Abschwächung oder behauptete D02-Abnahme.

### 5.9 Konkretes formales und experimentelles Testmodell

VR, hier nicht ausgeführt: Ein endliches Zustandsmodell mit zwei Principals, zwei Epochkontexten, Kapazität 2/1, zwei Acquire-IDs (einschliesslich gleicher ID in beiden Epochen), zwei Leaseversionen und expliziten Job-/Callback-/Committickets. Aktionen: Authenticate, Admit, Commit, PostCallback, SealProducer, Finish, Disable(L1), Close(L2), Enable(L3), Compact, Renew, Release, Suspend/Resume, Fail/CancelWait. Alle Interleavings und Fehlerpunkte prüfen; Zählerüberlauf gesondert. Safety ist unbedingt; Liveness nur unter Annahme terminierender Provider. Kein Modell darf Drain durch reine Timeouttransition überspringen. Zusätzlich induktiven Invariantenbeweis über beliebig viele sichere Epochwechsel und eine Refinement-Zuordnung zu den echten Admission-/Commit-/Callbackstellen erstellen; ein endliches Zwei-Epochen-Modell allein beweist keine allgemeine Implementationssicherheit. Beweisartefakt und Gegenbeispieltraces separat beauftragen, ohne neue Produktzustände.

| Test-ID | Steuerbarer Ablauf / zwingendes Orakel |
|---|---|
| E01 | Authenticate(E) pausieren; L1/L2/L3; dann alten Acquire/Edit/Execute/Renew/Release dispatchen → Forbidden, kein Slot/Commit/Frist-/Historyeffekt in E+1. |
| E02 | Pause exakt zwischen Prüfung und Registrierung bzw. vor Commit; Disable konkurriert → entweder atomar vorher angenommener und gezählter Auftrag oder wirkungslose Ablehnung, niemals ungezählter Zwischenfall. |
| E03 | Alle 4.096 Slots bzw. 256 Principal-Slots verbrauchen, zusätzlich frühes Bytebudget erschöpfen; kompaktieren → Neuannahme in E weiter gesperrt, gespeicherte Retries/aktive Renew/Release weiterhin korrekt. |
| E04 | Sicher L1/L2, erst dann L3; neue Epoche hat wieder leeres Kontingent. Identische acquisitionId mit alten Credentials abgewiesen, mit frisch autorisiertem neuen Kontext genau einmal annehmbar. Alte leaseId nie gültig. |
| E05 | Accepted Job endet, Return-P/Save-Write/Password-/Cancellationcallback künstlich offen → L2/L3 unmöglich. Dann jeden Abschluss einzeln freigeben, Resultatkopien/Q/Locks zuletzt; erst vollständiges Zertifikat erlaubt neue Epoche. |
| E06 | Producer versucht Post/Add unmittelbar nach pending=0 → Sealing verhindert ungezählte Arbeit oder hält Drain offen. Reale Dispatcher-/Providerpfade statt nur Counterstub prüfen. |
| E07 | Disable/Dispose/Cancel/Timer/Resultatkopie wirft; parallele Enableaufrufe; Waitabbruch → genau ein closeTicket, kein Kontingentreset, keine neue Credentialpublikation, keine wachsende Pendingliste. |
| E08 | Dauerhaft blockierter Provider über beliebig viele Enable-/Disableversuche → konstant begrenzter Speicher, keine L2/L3; diagnostizierter Stillstand statt Force-Unlock. |
| E09 | Viele sequenzielle sichere Epochen mit GC-/Heapmessung → keine retained revokedEpochs/Keys/Tables/Closures; <=4 MiB Leasebudget plus separat begrenzte Transport-/Executionbudgets. Execution-ID-Kontingent nicht zurückgesetzt. |
| E10 | Erfolgreiches CAS, verlorene Antwort, gleiches CAS mehrfach; späteres CAS und alter Timer/Epochcallback → genau eine Verlängerung je Version und konstanter Speicher. |
| E11 | Fake suspendzählende Uhr und echte Plattformprobe: Resume vor/nach 600 Sekunden, Wanduhr vor/zurück, verzögerter Resume-/Timerdispatch → keine Suspendverlängerung, Deadlineprüfung vor nächster Wirkung. |
| E12 | Crash/Shutdown an L1/L2/L3-Punkten und Überlauf von Epoch-/Leasesequenz → kein reuse, stopping verhindert L3; Neustart verwirft alte Credentials/Instanz. Kein behaupteter Drainerfolg bei Prozessabbruch. |

Abnahme verlangt Modellprüfung/Review **und** Produktintegrationstests mit realen Registry-, Coordinator-, Passwort-, Save-, Dispatcher- und Credentialpfaden; reine H4- oder Fakecountertests reichen nicht. Lease-Protokollspeicher inklusive Objekt-/Closure-/Timeroverhead messen. Keine Aussage, diese Prüfungen seien mit AP0-R1 bereits bestanden.

## 6. MCP-DTOs und Schemas

### 6.1 Versionierter Vertrag

D04: Einheitlicher Top-Levelname `contractVersion: "2"` in neuen Anfragen/Antworten, bewusst neuer Name gegenüber bestehendem Reply.SchemaVersion="1". Der Wechsel ist breaking und muss in Abstractions und Toolmapping gemeinsam erfolgen. capability `editor.exclusiveControl.v2` wird erst bei implementierter Funktion veröffentlicht. v1-Edit-/Execute-Requests ohne leaseId werden **vor Dispatch** als UnsupportedContractVersion/LeaseRequired abgewiesen; keine still hinzugefügte Defaultlease. JSON-RPC-/MCP-Protokollversion ist davon unabhängig.

Die nachfolgenden vollständigen Requestobjekte sind JSON-Schema-2020-12-Bausteine; `$ref`-Ziel für ein konkretes Tool ist dessen `$defs`-Eintrag. Weitere Properties sind überall verboten. Formatprüfung für UUID/date-time und semantische UInt64-/UTF-16-Validierung müssen aktiv sein; Schema allein beweist keine Besitzrechte.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:cryptoworkbench:mcp:editor-contract:2",
  "$defs": {
    "uuid": {"type":"string","format":"uuid","not":{"const":"00000000-0000-0000-0000-000000000000"}},
    "version": {"type":"string","pattern":"^(0|[1-9][0-9]{0,19})$"},
    "acquire": {
      "type":"object","additionalProperties":false,
      "required":["contractVersion","instanceId","editorId","acquisitionId","expectedRevision"],
      "properties": {
        "contractVersion":{"const":"2"},"instanceId":{"$ref":"#/$defs/uuid"},
        "editorId":{"$ref":"#/$defs/uuid"},"acquisitionId":{"$ref":"#/$defs/uuid"},
        "expectedRevision":{"$ref":"#/$defs/version"}
      }
    },
    "renew": {
      "type":"object","additionalProperties":false,
      "required":["contractVersion","instanceId","editorId","leaseId","expectedLeaseVersion"],
      "properties": {
        "contractVersion":{"const":"2"},"instanceId":{"$ref":"#/$defs/uuid"},
        "editorId":{"$ref":"#/$defs/uuid"},"leaseId":{"$ref":"#/$defs/uuid"},
        "expectedLeaseVersion":{"$ref":"#/$defs/version"}
      }
    },
    "release": {
      "type":"object","additionalProperties":false,
      "required":["contractVersion","instanceId","editorId","leaseId"],
      "properties": {
        "contractVersion":{"const":"2"},"instanceId":{"$ref":"#/$defs/uuid"},
        "editorId":{"$ref":"#/$defs/uuid"},"leaseId":{"$ref":"#/$defs/uuid"}
      }
    },
    "edit": {
      "type":"object","additionalProperties":false,
      "required":["contractVersion","instanceId","editorId","leaseId","expectedRevision","mode","text"],
      "properties": {
        "contractVersion":{"const":"2"},"instanceId":{"$ref":"#/$defs/uuid"},
        "editorId":{"$ref":"#/$defs/uuid"},"leaseId":{"$ref":"#/$defs/uuid"},
        "expectedRevision":{"$ref":"#/$defs/version"},
        "mode":{"enum":["insert","append","replace"]},"text":{"type":"string","maxLength":262144},
        "offset":{"type":"integer","minimum":0,"maximum":262144}
      },
      "allOf":[{"if":{"properties":{"mode":{"const":"insert"}}},
        "then":{"required":["offset"]},"else":{"not":{"required":["offset"]}}}]
    },
    "executeEditor": {
      "type":"object","additionalProperties":false,
      "required":["contractVersion","instanceId","editorId","leaseId","executionId","expectedRevision","expectedStateVersion"],
      "properties": {
        "contractVersion":{"const":"2"},"instanceId":{"$ref":"#/$defs/uuid"},
        "editorId":{"$ref":"#/$defs/uuid"},"leaseId":{"$ref":"#/$defs/uuid"},
        "executionId":{"$ref":"#/$defs/uuid"},"expectedRevision":{"$ref":"#/$defs/version"},
        "expectedStateVersion":{"$ref":"#/$defs/version"},
        "timeoutMs":{"type":"integer","minimum":1000,"maximum":120000},
        "outputPolicy":{"enum":["metadataOnly","includeText"]}
      }
    }
  }
}
```

D02/D04: ActivationEpoch bleibt serverseitiger authentisierter Kontext und ist ausdrücklich kein Requestfeld. Aufgrund additionalProperties:false werden clientseitige epoch/activationEpoch/ownerId-Zusätze abgewiesen. Ein fehlender leaseId wird nicht aus aktuellem Besitz ergänzt. Epochenwiderruf liefert Forbidden ohne Information über fremde/neue Credentials; Aktivierung während Closing ist ein hostinterner Busy-/EditorFaulted-Fall, kein weiteres MCP-Tool. Die vorhandenen Error-Enums decken diese Fälle ab; kein neuer Runnerzustand und kein clientseitiges Epochresetargument.

Semantik zusätzlich: UInt64 <= 18446744073709551615; leaseVersion beginnt bei 1. Editlimit 262.144 UTF-16-Codeeinheiten **im resultierenden Dokument**; JSON-Schema maxLength zählt nicht überall UTF-16, deshalb Hostprüfung zwingend. Offset darf Surrogatpaar/CRLF nicht teilen und muss im aktuellen Dokument liegen. Default timeoutMs=30000, outputPolicy=metadataOnly; normalisierte Defaults gehen in Executionfingerprint ein, ebenso leaseId und Zustimmungsoptionen. Keine clientseitige ActivationEpoch/PrincipalId.

### 6.2 Antwortschema O14 und sichere Fehler

```json
{
  "$schema":"https://json-schema.org/draft/2020-12/schema",
  "$id":"urn:cryptoworkbench:mcp:mutation-receipt:2",
  "type":"object","additionalProperties":false,
  "required":["contractVersion","instanceId","editorId","expectedRevision","observedRevision","textEffect","historyEffect","finalization","error","retryPolicy","requiresStateRefresh"],
  "properties":{
    "contractVersion":{"const":"2"},
    "instanceId":{"type":"string","format":"uuid"},"editorId":{"type":"string","format":"uuid"},
    "expectedRevision":{"type":"string","pattern":"^(0|[1-9][0-9]{0,19})$"},
    "observedRevision":{"anyOf":[{"type":"null"},{"type":"string","pattern":"^(0|[1-9][0-9]{0,19})$"}]},
    "textEffect":{"enum":["none","applied","partial","indeterminate"]},
    "historyEffect":{"enum":["none","applied","indeterminate"]},
    "finalization":{"enum":["complete","faulted"]},
    "error":{"anyOf":[{"type":"null"},{"$ref":"#/$defs/error"}]},
    "retryPolicy":{"const":"noAutomaticRetry"},"requiresStateRefresh":{"type":"boolean"}
  },
  "$defs":{"error":{
    "type":"object","additionalProperties":false,"required":["code","messageKey"],
    "properties":{
      "code":{"enum":["Forbidden","UnsupportedContractVersion","InvalidArgument","InvalidRange","LimitExceeded","InstanceMismatch","EditorUnavailable","EditorOwned","Busy","RevisionConflict","StateConflict","LeaseRequired","LeaseNotFound","LeaseExpired","LeaseRevoked","LeaseVersionConflict","IdempotencyConflict","AcquisitionRetired","CapacityExceeded","VersionExhausted","TimerUnavailable","ConfirmationDenied","ConfirmationUnavailable","ShuttingDown","CallbackFailed","MutationFailed","FinalizationFailed","EditorFaulted","InternalError"]},
      "messageKey":{"type":"string","pattern":"^mcp\\.[a-zA-Z0-9.]+$","maxLength":96}
    }
  }},
  "allOf":[
    {"if":{"properties":{"finalization":{"const":"faulted"}}},"then":{"properties":{"requiresStateRefresh":{"const":true},"error":{"$ref":"#/$defs/error"}}}},
    {"if":{"properties":{"textEffect":{"enum":["partial","indeterminate"]}}},"then":{"properties":{"requiresStateRefresh":{"const":true},"error":{"$ref":"#/$defs/error"}}}}
  ]
}
```

Zusätzliche Hostinvarianten: Busy/RevisionConflict dürfen ausschliesslich `none/none/complete` begleiten. Bei historyEffect=indeterminate ist finalization=faulted. Erfolgsreceipt (`error=null`) ist entweder `none/none/complete` für No-op oder `applied/applied/complete`. Erfolgreicher Nicht-No-op braucht zuverlässige observedRevision. Ein vor Mutation intern fehlgeschlagener Request darf InternalError mit none/none tragen; nach Eintritt ohne Beweis niemals. messageKey ist serverseitig aus dem Codekatalog abzuleiten, nicht aus Exception-/Clienttext.

Beispiel eines bereits wirksamen Edits mit begrenztem Observerfehler:

```json
{
  "contractVersion":"2","instanceId":"11111111-1111-4111-8111-111111111111",
  "editorId":"22222222-2222-4222-8222-222222222222",
  "expectedRevision":"41","observedRevision":"42",
  "textEffect":"applied","historyEffect":"applied","finalization":"complete",
  "error":{"code":"CallbackFailed","messageKey":"mcp.editor.callbackFailedAfterEffect"},
  "retryPolicy":"noAutomaticRetry","requiresStateRefresh":true
}
```

### 6.3 Lifecycle-Antwortschemas und Fehlerhülle

Für Acquire/Renew/Release gilt eine einzige Hülle mit `contractVersion`, instanceId, result oder error, replayed, retryPolicy und requiresStateRefresh. result/error sind gegenseitig exklusiv. Definitionen der Werte und ihrer Beziehungen stehen in Abschnitt 5; das Schema enthält alle übertragbaren Felder, keine Principal-/Epochinformationen.

```json
{
  "$schema":"https://json-schema.org/draft/2020-12/schema",
  "$id":"urn:cryptoworkbench:mcp:lease-reply:2",
  "type":"object","additionalProperties":false,
  "required":["contractVersion","instanceId","result","error","replayed","retryPolicy","requiresStateRefresh"],
  "properties":{
    "contractVersion":{"const":"2"},"instanceId":{"type":"string","format":"uuid"},
    "result":{"anyOf":[{"type":"null"},{"$ref":"#/$defs/acquired"},{"$ref":"#/$defs/renewed"},{"$ref":"#/$defs/released"}]},
    "error":{"anyOf":[{"type":"null"},{"$ref":"urn:cryptoworkbench:mcp:mutation-receipt:2#/$defs/error"}]},
    "replayed":{"type":"boolean"},"retryPolicy":{"enum":["sameRequestOnly","noAutomaticRetry"]},
    "requiresStateRefresh":{"type":"boolean"}
  },
  "oneOf":[{"properties":{"result":{"type":"object"},"error":{"type":"null"}}},{"properties":{"result":{"type":"null"},"error":{"type":"object"}}}],
  "$defs":{
    "uuid":{"type":"string","format":"uuid"},
    "version":{"type":"string","pattern":"^(0|[1-9][0-9]{0,19})$"},
    "utc":{"type":"string","format":"date-time","pattern":"Z$"},
    "acquired":{
      "type":"object","additionalProperties":false,
      "required":["kind","acquisitionId","editorId","leaseId","revision","leaseVersion","acceptedAt","expiresAt","leaseState"],
      "properties":{
        "kind":{"const":"acquired"},"acquisitionId":{"$ref":"#/$defs/uuid"},"editorId":{"$ref":"#/$defs/uuid"},
        "leaseId":{"$ref":"#/$defs/uuid"},"revision":{"$ref":"#/$defs/version"},"leaseVersion":{"$ref":"#/$defs/version"},
        "acceptedAt":{"$ref":"#/$defs/utc"},"expiresAt":{"$ref":"#/$defs/utc"},
        "leaseState":{"enum":["active","releasing","released","expired","revoked"]}
      }
    },
    "renewed":{
      "type":"object","additionalProperties":false,
      "required":["kind","editorId","leaseId","inputLeaseVersion","leaseVersion","acceptedAt","expiresAt"],
      "properties":{
        "kind":{"const":"renewed"},"editorId":{"$ref":"#/$defs/uuid"},"leaseId":{"$ref":"#/$defs/uuid"},
        "inputLeaseVersion":{"$ref":"#/$defs/version"},"leaseVersion":{"$ref":"#/$defs/version"},
        "acceptedAt":{"$ref":"#/$defs/utc"},"expiresAt":{"$ref":"#/$defs/utc"}
      }
    },
    "released":{
      "type":"object","additionalProperties":false,
      "required":["kind","editorId","leaseId","phase","reason"],
      "properties":{
        "kind":{"const":"released"},"editorId":{"$ref":"#/$defs/uuid"},"leaseId":{"$ref":"#/$defs/uuid"},
        "phase":{"enum":["releasing","released"]},
        "reason":{"enum":["released","expired","revoked","reclaimed","detached","shutdown"]}
      }
    }
  }
}
```

`leaseState` bei Acquire ist eine Momentaufnahme, nicht die heutige Ablaufzeit eines historischen Receipts. Reclaim/detach/shutdown projizieren dort revoked; Release liefert den genaueren eigenen Grund. Ein LeaseVersionConflict muss keine aktuelle Version verraten: eigene Statusabfrage liefert sie. Status erweitert den bisherigen Vertrag um Besitzphase, editorWriteBlocked, mutationBusy und ownLease nur für den authentisierten aktuellen Besitzer (ID/Version/Frist); fremde Besitzer werden als editorOwned ohne Details dargestellt. GetEditor liefert weiterhin text/revision/editorId nur mit editor.read. Diese Statusintegration ist IR und vor AP6 im vollständigen Statusschema nachzuführen.

Beispiel innerhalb derselben serverseitig authentisierten Epoche E: Acquire um `2026-10-26T09:00:00Z` gibt Version 1/Frist 09:10. Renew(expected=1) um 09:08 gibt Version 2/09:18. Identischer Retry um 09:09 liefert Version 2/09:18. Nach Renew(expected=2) um 09:16 gilt Version 3/09:26; ein Retry expected=1 erhält Konflikt. Ein Timer für Version 1 kann Version 3 nicht entziehen. Dieselbe Erwerbs-ID nach Release innerhalb E erzeugt niemals eine neue Lease. Nach kontrolliertem Abschluss E und frischer lokaler Autorisierung E+1 darf dieselbe acquisitionId eine neue Anfrage sein; derselbe gespeicherte HTTP-Request mit Caller.E bleibt Forbidden. Keine Epoche wird aus der UUID oder aus einer aktuellen Servervariable für den alten Caller rekonstruiert. Suspend von 09:17 bis 09:28 lässt die Lease mit Frist 09:26 ablaufen; alter Timer oder Renew-Retry verlängert sie nicht.

Hostneutrale Signaturen P:

```csharp
Task<LeaseReplyV2> AcquireEditorAsync(AutomationCaller caller, AcquireEditorV2 request, CancellationToken wait);
Task<LeaseReplyV2> RenewEditorAsync(AutomationCaller caller, RenewEditorV2 request, CancellationToken wait);
Task<LeaseReplyV2> ReleaseEditorAsync(AutomationCaller caller, ReleaseEditorV2 request, CancellationToken wait);
Task<EditorMutationReceiptV2> UpdateEditorAsync(AutomationCaller caller, EditorEditV2 request, CancellationToken wait);
Task<ExecutionReplyV2> ExecuteEditorAsync(AutomationCaller caller, EditorExecutionV2 request, CancellationToken wait);
```

ExecutionReplyV2 erhält die bestehenden Job-/Diagnose-/Variablenfelder und ResultExpired-/NotFound-Semantik; SourceRevision bleibt angenommener Snapshot. Dessen vollständiges Schema ist zusammen mit dem bestehenden AutomationSchemas-Katalog in AP6 zu versionieren, nicht aus dem MutationReceipt abzuleiten. Kein execute_text-, Save-, Passwort-, RawUndo- oder Remote-Reclaim-Tool wird hinzugefügt.

## 7. Vollständige Mutationspfadmatrix

### 7.1 Leseschlüssel und gemeinsame Verträge

Jede Zeile nennt den **heutigen** Einstieg F; G/X/O/I/P bezeichnen **erforderliche Zielgrenzen**, nicht bereits vorhandene Produktklassen. Der Produktbestand enthält keinen H4-Coordinator. Bestehende IsExecuting-/Identity-/MutationLocks sind getrennt genannt. Alle benötigten Zeilen bleiben IMPLEMENTATION REQUIRED und VERIFICATION REQUIRED, soweit nicht ausdrücklich ausserhalb des MVP.

Profile bilden die wiederkehrenden Vertragsdimensionen vollständig ab:

- **N:** GUI-Besitz, G vor Originalhandler, kein Q für reinen nativen Textedit; Originaldokument/editorId und aktuelle Bridge-Revision, Active über synchrone Text-/History-/Caret-/Selectioncallbacks; keine awaits; Fehler nach Abschnitt 4, eigene Gruppen normal schliessen, niemals Stacktausch. Zustand T/H/C = Text/History/Caret-Auswahl.
- **I:** GUI-Intent vor externem await, D/editorId/revision/controlGeneration plus Auswahl/Caret und Providerart; kein offenes Update über await. Nach Antwort Owner/I erneut prüfen, dann G. Fehler vor Commit none/none; nach Eintritt O14. Acquire darf unerledigten Intent entwerten. Zustand T/H/C plus Clipboard/Popup.
- **X:** Serviceauftrag: Owner/Gültigkeit → Q → Revision/gegebenenfalls stateVersion → Ticket vor Callbacks. D/editorId/Generation und Job/Intent gebunden; alle awaits gehören demselben angenommenen Auftrag. Q/Ticket bis Ergebnis-/Ressourcen-/Callbackdrain. Zustand T/H und/oder Interpreter/Variablen V, Präsentation UI. Teilfehler kein Rollback.
- **S:** SaveIntent I vor Picker; Commit X/Q plus Snapshot/Ziel/Overwriteentscheidung. Vor Delete letzter Cancel-/Owner-/Generationscheck; danach Delete/Write/Unlock vollständig finalisieren, keine Cancellationlücke. Datei kann bei Fehler verändert sein, Dokument nicht automatisch clean. Kein Editor-Mutationsreceipt als Dateierfolg missbrauchen.
- **O:** Nichtschreibender Observer, auch bei späterer Zustellung Active/ObserverDepth. Keine neue G/X-Mutation, Exceptions sicher begrenzen; eigene UI-Präsentation erlaubt. Dispatcherposts tragen Lifecycle-/Auftragsbindung. Identitäts-/Fokuswechsel entwertet ausstehende Intents.
- **L:** Lifecyclephase vor Callback publizieren, Generation invalidieren, Originaltickets drainen; nachgelagerte Unlocks nur am Original. GUI erst wenn sicher, sonst faulted/unavailable. Kein Q für die gesamte Lease.

E-Spalte verweist auf Abschnitt 2.3/H4-Matrix; PASS bezieht sich immer auf deren bezeichneten Versuch. „Offen“ umfasst Produktintegration und den genannten zusätzlichen Nachweis. Native Standardbefehle sind Bestandteil der produktiven Bibliotheksroute, auch ohne eigene C#-Methode.

### 7.2 M01–M31: native und interaktive Pfade

| ID | F: Einstieg / Callchain und heutige Prüfung | P: Owner/Admission; Zustand; Bindung, await/Callback, Abschluss | E / konkreter fehlender Nachweis |
|---|---|---|---|
| M01 | EV AXAML → CT.TextArea → nativer TextInput/PerformTextInput; CT.TextEntering prüft nur MutationLocked | N; T/H/C; früher TextArea-Eintritt vor base, View/Completion unter O | H4 einfacher Commit positiv; Produkt-G und native Unicode-/Sonderauswahlvarianten fehlen |
| M02 | Derselbe TextInput mit mehrzeichigem Unicode/IME-artigem Commit | N; Textinhalt allein klassifiziert keine OS-Komposition; nicht pauschal Unicode sperren | H1/H4-IME Headless positiv; kein OS-Beweis |
| M03 | OS-IME → InputMethodClient/RawTextInput → TextArea; CT hat keine permanente IME-Konfiguration | Frühe native Deaktivierung an Editor **und** TextArea; L bei Fokus/Attach, alte Commits blockieren; kein Ersatz durch Labflag | U2 OPEN, AP1 zwingend; CJK-Positivkontrolle und Erstfokus/alte Commitfolgen |
| M04 | Enter → nativer EditingCommand → EV.TextEntering → ExecuteInputAsync → VM.ParseLineAsync → AS.RunAsync/AR → WP | N + X + P; T/H/C/V/UI. Outputticket vor erstem await; Originalanchor und Lifecycle, nicht pauschal alte Revision verlangen | H4 Return positiv; echte Tastatur/Fehler/Drainintegration fehlt |
| M05 | Shift-Enter/EnterLineBreak und Key-Aliase → native Handler, ggf. M04 | N, bei Return X/P; gleiche Originalbindung und O | H4 Shift-Enter positiv; alle Aliase/Fallbacks unter aktivem H fehlen |
| M06 | Delete/Backspace/Ctrl-Delete/Ctrl-Backspace → native Editing-Bindings | N vor Löschen; T/H/C | H1/H3 Varianten, H4 Teilprobe; komplette Produkt-Keymatrix offen |
| M07 | ApplicationCommands.Delete/EditingCommands-Fallback über Commandroute | N vor Default-/Context-/direktem Commandhandler; T/H/C | H3 Referenzpaare; H4 eigener Fallbacklauf offen |
| M08 | Tab/Shift-Tab → TextArea.OnKeyDown/OnTab, nicht nur Commandliste | N vor base; mehrteilige Selection-/Indentationcallbacks unter O | H4 Mehrzeilentab; Shift/Rechteck/ReadOnlybereiche offen |
| M09 | CT entfernt DefaultInputHandler Undo/Redo; GuardHistory in TextArea.CommandBindings → UndoStack | N **vor Stackpop**; U1 native Registrierungen erhalten, enge Delegation einmalig; T/H/C, Fehler O14 | U1 15/18 positiv; heutige Registrierung unverändert fehlerhaft, echte Shortcuts/Folgeedit abnehmen |
| M10 | Cut → nativer Clipboard-/Selectionpfad; kein Produkt-I-Adapter | I für Export und anschliessende Löschung; T/H/C/Clipboard; fehlgeschlagener Export keine Löschung | H4 Cut/error/turnover; produktive Clipboardroute fehlt |
| M11 | Cut ohne Auswahl/WholeLine/Rechteck/teilweise ReadOnly → native Selection | I wie M10, Formate/Segmente im Intent erhalten | H3 Teilbelege, integrierte Varianten/Providerfehler offen |
| M12 | Paste → nativer Clipboardread → Selection.ReplaceSelectionWithText | I; await read, danach Revision/Generation/Auswahl prüfen, G um native Einfügung | H4 deferred-paste positiv; Produktbindung/reale OS-Provider offen |
| M13 | Paste WholeLine/Rechteck/Tabs/Null/Newlines/ungültige Daten | I; native Formatsemantik/Normalisierung erhalten, Providerfehler vor Wirkung | H4 mehrzeilig/Fehler positiv; Formate/ReadOnly/Ereignisäquivalenz offen |
| M14 | Native SearchPanel ReplaceNext/ReplaceAll, kein Produkt-G | N; Regexvalidierung vor Mutation, T/H/C; Observer auch bei Resultatupdates | H4 Regex/mehrzeilig/invalid positiv; ReplaceNext separat/Produktintegration fehlen |
| M15 | SearchInputHandler Replace-Fallback an TextArea | N zusätzlich zum Panelwrapper; gleiche Revision/Callbackregeln | H1/H3 Teilbelege; Fallback unter aktivem H offen |
| M16 | Find erzeugt/öffnet natives Panel/Template | Find O, Replace N; Installation an jeder neuen Panelinstanz vor Nutzbarkeit, L bei Neuattach | H4 Find unter MCP; Templates/Neuinstallation offen |
| M17 | EV.TextEntered → CC.OpenForTypedParenthesis → CompletionWindow → InfoCompletionData.Complete → Document.Replace → _completed | I bis Annahme; G um Replace, Session an D/id/generation; Completioncallback O/enge Abschlussaktion | H4 echtes Popup Tab/turnover; Produkt hat keine Generationsprüfung |
| M18 | CC KeyDown/Caret/Document-/Window_Closed, RP.Changing → Refresh/Reopen/Close | O für Beobachtung, I für Commit; kein Schreibrecht aus Refresh; Dispatcher/Lifecycle revalidieren | H4 Teilprobe; Maus/andere Keys/Dispose/Reopen/IME offen |
| M19 | MainViewModel.LineHistoryCommand → HistoryService.LineBack/Forward → VM.OnHistoryChanged → Remove/Insert/Documentzuweisung/IsModified; nur IsExecuting | N um ganze schreibende Commandaktion; T/H/C/UI; Remove+Insert native Gruppe, kein reiner Observer | H4 ab HistoryService; echtes MainView-Binding/Gruppierung offen |
| M20 | Native eingehende Text-Copy-Drop-Route, CT setzt Dragoption heute nicht | D: dauerhaft vor Interaktion deaktivieren; kein Transferadapter; T/H darf nie entstehen | H2/H2R Historie bleibt; Produkt-Deaktivierungsregression erforderlich |
| M21 | Native DragEnter/Over/Leave/Kindcontrolrouting | Frühes Drag-Aus auch bei Optionsersatz; Auswahl/Navigation unter O weiter möglich | Routing/Optionenwechsel nativ prüfen; Datei-Drop ausserhalb MVP |
| M22 | Native StartDrag → DoDragDrop-await → Move-Remove | Beginn dauerhaft verhindern; kein spätes Flag als Drainbeweis | H2R zeigt Unsicherheit später Deaktivierung; kein laufender Drag bei Erstaktivierung |
| M23 | Interner/Cross-Editor/extern abgeschlossener Move, Cancel/Rechteck | Dauerhafte Deaktivierung; keine Source-/Targetwirkung anbieten | H2 Fehler nicht umwerten; native Deaktivierungsmatrix offen |
| M24 | Nativer DeleteLine-Command | N; T/H/C, originale Commandroute | H1/H3 Belege; integrierte Einzelprobe offen |
| M25 | RemoveLeading/TrailingWhitespace | N; T/H/C, Compositeoperation mit O14-Teilwirkungsgrenze | H1/H3; Produkt-/Fehlernachweis offen |
| M26 | Upper/Lower/InvertCase | N; T/H/C, gleiche Unicode-/Selectionsemantik | H1/H3; H4 Einzelvarianten offen |
| M27 | Öffentlicher nativer ConvertToTitleCase-Command; kein eigenes Produktmenü gefunden | D07: keine unterstützte CryptoWorkBench-GUI-Funktion, kein Menü/Shortcut neu anbieten. Erreichbare native Commandroute weiterhin N vor Delegation; NotSupported sicher behandeln, kein Bypass | H1 TitleCase FAIL bleibt unverändert; keine Altassertion ändern. VR: keine angebotene GUIaktion, dennoch korrekte frühe Ablehnung/Fehlerbehandlung vorhandener nativer Aufrufpfade |
| M28 | TabsToSpaces/SpacesToTabs, Leading-Varianten | N; T/H/C, bestehende Auswahl/Gruppen bewahren | H1/H3 Teilbelege; aktive-H-Kombinationen offen |
| M29 | Tab/IndentSelection → IndentationStrategy → Selection | N; Strategiecallbacks kontrolliert, kein unabhängiges Schreibrecht | H4 einfache Einrückung; Rechteck/eigene Strategie/Fallback offen |
| M30 | ToggleOverstrike + nachfolgender TextInput | Zustandstoggle kontrolliert, Commit N; T/H/C | H4 Überschreiben positiv; Toggle/virtuelle Auswahl offen |
| M31 | Navigation/Pointer/SelectAll/Copy/FindNext/Previous | Lesen unter MCP erlaubt; O; Copy erfasst Text vor Clipboardawait, keine spätere Löschung/Q-Reservation | H4 read-only Find/Copy; volle Pointer-/Suchroute/TextCopied-Äquivalenz offen |

### 7.3 M32–M51: Service, GUI, Lifecycle und zusätzliche Eingänge

| ID | F: Einstieg / Callchain und heutige Prüfung | P: Owner/Admission; Zustand; Bindung, await/Callback, Abschluss | E / konkreter fehlender Nachweis |
|---|---|---|---|
| M32 | Hostsubscription/Propertycallback → erneuter Service-/Commandwrite; kein allgemeines Observe im Produkt | O lehnt neue N/X vor Wirkung ab, auch spät; T/H/V unverändert durch Unteraufruf; äusseren Effekt nicht verschweigen | H3 Fokus FAIL; H4 kontrollierte Observer positiv; sämtliche Produkt-Subscriptions anbinden |
| M33 | EV DI-Konstruktor → InitializeComponent → Editor.Clear → VM.TextEditor → Attached/AttachEditor | L vor Nutzbarkeit, Init unter GUI/G oder unavailable, Bridge einzige Registrierung; Clear kann History ändern | H4 echte View; Erststart/Template/Options-/Fehlerkombinationen offen |
| M34 | CT.DocumentProperty.Coerce → ObserveDocument; EB.Attach/Detach/ObserveDocument; VM.TextEditor/Detach/AfterIdentity | L, Dokumentwechsel nur GUI/idle; D/id/generation wechseln, alte I/P ungültig. Deferred Actions dürfen Nachfolger nicht entsperren | H4 Identity/Detach/Reattach positiv; alle Commit-/Dialogphasen offen |
| M35 | SB.OpenScriptBookCommand → GuardAsync → OpenFilePickerAsync → Clear-Dialog → VM.OpenScriptBookAsync; IsExecuting nur vor Picker | I vor Picker/Bestätigung; danach X/Q, T/H/V/Dateilesen; Ziel/Generation/Entscheidung revalidieren | H4 VM-Dateikette; Sidebar ohne I, reale Dialog-/Storagekette offen |
| M36 | VM.OpenScriptBookAsync → AS.RunAsync → ReadAllLinesAsync → static ClearAdmitted → Document.Text="" → ExecuteBookAdmittedAsync → Insert/IsModified | X über Bookauftrag; D/id/generation und V/stateVersion derselben Instanz; UIawaits/beforeLine/afterLine; frühere Zeilen bleiben wirksam | H4 Book-Clear Teilprobe; Shared-Versionsfehler/Teilfehler/Lifecycletests offen |
| M37 | EO.UpdateEditorAsync → CheckEditor/Validate/Snapshot/Reserve → ReplaceConfirmation-await → Revalidate → LockIdentity/RunUpdate/Replace oder Insert/Snapshot | MCP-Lease + X/Q/O14; Originalidentität/revision; keine synthetischen Returnevents; eine Undo-Gruppe, No-op ohne Gruppe | Guards vorhanden, leaseId/G fehlen; H4 Append/Conflict, Replace/Insert/No-op/Dialog/Callbackfehler offen |
| M38 | EO.ExecuteEditorAsync → GUI-only Sperre → AR.Accept/Q → AS.ExecuteScriptBookAsync → WP/Presentation → derzeit Insert(output) | X/Q, executionId/Fingerprint + editorId/sourceRevision/stateVersion + Lease/Zustimmung. Job-/Passwortdrain; EC21 entfernt Sourceausgabe | H4 EC21/Waitabbruch; Produktkorrektur, Einzelzustimmung, v2/HTTP offen |
| M39 | EV.ExecuteInputAsync nach VM.ParseLineAsync → document.Insert(anchor.Offset, PrintMessage) | P vor await, X/Q für Run; Output eigener gebundener G; Original-D/VM-Lifecycle/Anchor, kompatibles Tippen erlaubt; P bis Insert/Verwerfen | H4 Return/delay/typing; Produkt ohne Pending-Output-Ticket |
| M40 | OutputOperations.Print/Info → VM Posts/Present → Status/Variables/Info/Main/Navigation; WP sammelt separat | X für Job, O für UIcallbacks; Print Original-P, Info/KM Lifecycle/Jobbindung; delayed Posts drainen/verwerfen | H4 Status; VM.Info ohne Lifecycleprüfung, komplette Posts/Navigation offen |
| M41 | SB.Save → VM.CaptureSaveIntent vor SelectSaveTarget → Ordneranlage/Picker/Overwrite → SC.CommitSaveAsync → Q/Lock → Delete/Task.Run(WriteSnapshot)/Unlock | S; D/id/revision/generation + Ziel/Entscheidung; Datei/Locks/UI; Unlock/finally originalgebunden | Snapshotbindung/Drain vorhanden, Generation fehlt. H4 VM-Picker/Shutdown; echte Storage-/Dialogfehler offen |
| M42 | VM.SaveScriptBook → SC.SaveScriptBook → Reserve → Snapshot/LockMutations → WriteSnapshot → Dispose | S synchron; GUI-Besitz zusätzlich, Callbackgrenze bei IsReadOnly/Unlock; keine clean-Markierung | H4 synchron positiv; Produkt-G/reentrante Propertyfehler offen |
| M43 | AR.BeginShutdown/DisableMcp/ActivateMcp/DrainAsync/DisposeAsync, App.Window.Closed/Desktop.Exit; Lease-Reclaim/Expiry fehlen | L plus Jobcancellation; Epochfence L1, vollständiges Zertifikat L2 vor L3; Timer epoch/id/version, suspendzählende Zeit; Save/Passwort nicht vorzeitig lösen | H4 Modellproben; E01–E12, vollständige Registry-/Editor-/Providerdrain- und Resumeintegration offen |
| M44 | CT.BeforeUpdate/BeforeChange/LockIdentity/AfterIdentity/LockMutations; späte Guards | Frühe N/X-Grenze davor; keine neue R7-EndUpdate-Ablehnungsrekursion; Property-/Identityabschluss O/L; T/H/D | R2/R4/R6/R7 bleiben; Produkt-H-Komposition separat prüfen |
| M45 | VariableView Button → VV.ModifyValueCommand → FindSelected → Value/ParseToDictionary → Shared.Mutate/RunState | GUI + X/Q; V/stateVersion; Auswahl/Modellgeneration vor Mutation prüfen, Präsentation O | H4 Command positiv; echter Button/DataGrid/injizierter Service offen |
| M46 | VariableView DataGrid/Button → ModifyVariableCommand → SetParameter/VariableDictionary.Update → Mutate | Wie M45; keine lebende Dictionarybearbeitung durch Binding; Auswahlobjekt kann ersetzt sein | H4 Parameter-/DataGridprobe OPEN |
| M47 | WP/Runner → PasswordRequested → PasswordDialogHost → Dialog/OwnerClosed/Cancellation.Post → späte Antwort | X/Q; ExecutionId/RequestId und Quarantäne, UI/O; Cancel nicht Freigabe, späte Antwort entsorgen | H4 Providerdrain; sichtbares Fenster/Shutdown/Hostwechsel offen |
| M48 | Mcp.StatusTransport/AutomationToolResults → Abstractions → Service; Status-Probe ohne Lifecycletools | D04 Auth/v2 → X/L, control + leaseId zusätzlich; ursprüngliche Epoche erneut atomar bei Admission/Commit; HTTP-Wait unabhängig | H4 serviceartiger Waitabbruch; E01/E02 alte authentisierte Requests, v2-/Credential-/Disconnectintegration offen |
| M49 (AP0) | KM.Konstruktor → LoadInfoAsync → Shared.ExecuteSubmissionAsync("Info(keymap)") → RunAsync/WP → Dispatcherdiagnostik | GUI-Ausführung trotz Infozweck: X/Q und Besitzprüfung am gemeinsamen RunGui-Eingang; Jobbindung/Lifecycle für Posts; kein Run unter MCP | F zusätzlicher konkreter Ausführungseingang; keine dedizierte H4-Probe; MCP/Detach/Shutdown testen |
| M50 (AP0) | VV.SetupVariables → Shared.Refresh → Q.RunState(CaptureMutation, ApplyVariables) | Zustandslesen unter Q, O-Präsentation; bei aktivem Lauf veröffentlichten stale Snapshot, keine lebende Enumeration; Read erteilt kein Schreibrecht | F Shared-Refresh; AP3 Capture-/Reentrancy-/Detachprüfung fehlt |
| M51 (AP0) | VariableView Value/Parameter-TwoWay-Bindings → VariableModel/ParameterModel.SetProperty → später Modify-Command | Detached Eingabemodelle; während MCP Editcontrols/Commit blockieren; Modelle an stateVersion/Generation binden, alte Entwürfe nicht auf neue Variablen übertragen; O für Properties | F Parametergrid IsReadOnly=false; Commandprobe beweist keinen Bindingeingang; echte Grid-/Selectiontests fehlen |

Scopepräzisierung M41: `CreateFolderAsync("CryptoScript")` ist bereits eine Dateisystemwirkung vor Save-Commit. Sie darf nach überholtem GUI-Intent nicht unkontrolliert neu beginnen: als kurzer angenommener GUI-Dateivorbereitungsschritt mit Drain vermitteln oder vorhandenen Ordner wählen. „Vor Commit keinerlei Dateiwirkung“ wäre am heutigen Pfad falsch. Der Schutz der Zieldatei vor Delete/Write bleibt zwingend.

Kein produktiver eigener Snippetmutator oder Raw-Undo-MCP-Endpunkt wurde in den untersuchten Hostquellen gefunden. Neu aktivierte Commands/Plugins müssen vor Freigabe in H aufgenommen werden. Direkter fremder Zugriff auf TextDocument/TextEditor.Text/UndoStack ausserhalb unterstützter Hostketten ist ausserhalb H; derselbe Aufruf in VM/Completion/Book oder einem benötigten Callback bleibt innerhalb H.

## 8. Observer-/Callbackvertrag

### 8.1 Regeln

Ein Observer ist nicht dadurch sicher, dass er während einer anderen Operation aufgerufen wird. `Observe` setzt auch beim späteren Dispatcher-/Fokuscallback einen kontrollierten Rahmen und stellt ihn im finally wieder her. Hosteigene Subscriber werden einzeln geschützt oder als rein/nichtwerfend nachgewiesen; eine einzige Hülle um Multicast.Invoke kann weitere notwendige Subscriber nach einem Throw überspringen. Insbesondere darf die Bridgebeobachtung nicht durch frühere UIhandler ausgelassen werden.

Neue vermittelte Schreibversuche aus O erhalten synchron vor Wirkung Busy; deren Ablehnung löst keine rekursive mutierende Statuszustellung aus. Nichtschreibende Status-/Popupaktualisierung bleibt erlaubt. Reine Beobachter mutieren weder Dokument/History noch Interpretervariablen. Schreibende Commandcallbacks (History, Completioncommit, gebundener Returninsert, Bookappend) sind angenommene Aktionen, keine Observerausnahme. Eine native Originaloperation darf ihre eigenen Schritte beenden, ohne allgemeinen Reentrancypass für Subscriber.

Asynchrone Fortsetzungen erhalten expliziten ursprünglichen Intent/Job/Ticketbezug. Ein Kindtask erbt kein Schreibrecht. Generation wird bei Besitz-/Dokument-/Registrierungswechsel erhöht; bei Fokuswechsel werden interaktive Clipboard-/Completionintents invalidiert. Modal erwarteter Fokuswechsel beim Save-/Bestätigungsdialog ist über dessen eigenes Intent-/Dialogticket zu modellieren, sodass der normale Picker nicht seinen eigenen Auftrag pauschal zerstört. Unabhängiger Besitzwechsel entwertet auch diesen Intent. Diese Differenzierung ist AP4-Verifikationspflicht.

Exceptions vor/innerhalb/nach nativer Wirkung gehen in O14 ein. Eigene Observerexceptions werden sicher protokolliert und verlassen den Observer nicht; ein Fehler im Bridge-/Abschlussinvariant ist nicht bloss kosmetisch und kann faulted erfordern. Dokumentwechsel aus Observer ist kein freier Schreibweg. Detach/Shutdown darf Releasing markieren und Folgezulassung entziehen, jedoch offene Originalgruppen nicht aus einem Callback heraus künstlich beenden.

### 8.2 Gegen Produkt abgeglichene Observerinventur

F = vorhandene Subscription/Delegation; P = notwendiger Vertrag; E/O = Evidenz und Rest. Keine Zeile behauptet bereits flächendeckendes Produkt-Observe.

| ID / F: Einstieg | P: Zustellung, Bindung und Fehlervertrag | E/O und Zuordnung |
|---|---|---|
| OBA01 CT.TextEntering | Synchron vor Wirkung früher G; MutationLocked zusätzlich | H4 Teilbeleg; M01–09 Produkt-G fehlt |
| OBA02 CT.UpdateStarted/Changing | Keine neue Mutation im Guard; spätes EndUpdate nicht allgemeine Ablehnungsstrategie | R6/R7 bleiben; M44 |
| OBA03 EV.TextEntering/TextEntered | Native angenommene Aktion; Return-P vor await, Completion-/Popupteil O | H4 echter Viewpfad; M04/17/39 Restfälle |
| OBA04 EV.Caret.PositionChanged → StatusText | O auch ohne Textoperation; Statusproperty darf nicht reentrant schreiben | H4 explizite Caretprobe; Produktwrapper fehlt |
| OBA05 EV.Attached/Detached | L vor Registrationänderung; Completiondispose/detach fehlerfest, unsubscribes erhalten | H4 Reattach/Clipboarddetach; Fehlerreihenfolgen offen |
| OBA06 OverloadInsightWindow.Closed / MyOverloadProvider.PropertyChanged | O, ursprüngliche Popupinstanz; alter Closed darf neues Fensterfeld nicht löschen | F zusätzlich Overloadprovider, keine dedizierte H4-Abnahme |
| OBA07 CC.Caret/KeyDown/TextArea.DocumentChanged | O, Session-ID/Generation; Commandcommit durch separate G-Grenze | H4 Tab/Turnover; weitere Annahmerouten offen |
| OBA08 CC.Document.UpdateStarted/Changed/UpdateFinished | O bei synchroner Libraryzustellung; Original-D prüfen, keine schreibende Normalisierung | H4 Controller; Fehler-/Dispose-/Reopenmatrix fehlt |
| OBA09 CC.Window_Closed | O, alte Session nur sich selbst schliessen/invalidieren | H4 Normalfall; alte Popupcallbacks offen |
| OBA10 InfoCompletionData._completed | Abschluss desselben Commits; Callbackfehler nach Replace als applied melden | H4 Tabkette; Throw nach Replace prüfen |
| OBA11 RP.Document.Changing | Reiner Sessionbeobachter/O, Unsubscribe an alter Instanz | F unverändert; Swap/Disposekombinationen offen |
| OBA12 EB.DocumentChanged/Document.Changed | Kritische Identitäts-/Revisionserfassung; vor Read nachbeobachten; keine Nachfolgerrevision in altes Ergebnis | Bridgecode/H4 Identity; Extremfehler offen |
| OBA13 HistoryService.LineHistoryChanged → VM.OnHistoryChanged | Schreibende Aktion N, kein pauschales O; gesamte Remove/Insert-/Dirtyfolge | H4 HistoryServiceprobe, MainViewbinding offen |
| OBA14 OutputOperations.PrintEvent → VM.OnPrintEvent | Post bindet Lifecycle und Job, nur Präsentation; eigentliche Textwirkung P | F Lifecyclecheck; H4 verzögerte Ausgabe, weitere Posts offen |
| OBA15 OutputOperations.InfoEvent → VM.OnInfoEvent | Post/Navigation O mit Lifecycle/Jobbindung; vor Regionenwechsel revalidieren | F kein Lifecyclecheck; vollständige Navigation offen |
| OBA16 OutputOperations.InfoEvent → KM.OnInfoEvent / LoadInfoAsync-Posts | O plus KM-Lifetime; Initialisierung separater X-Run | AP0 konkreter Eingang; Dispose-/MCP-/Fehlerprobe erforderlich |
| OBA17 ViewModelBase/Status/Sidebar/VV PropertyChanged | Einzelne Hostsubscriber rein/kontrolliert; CanExecute keine Besitzwahrheit | H4 Statuspropertyprobe, Produktdispatch fehlt |
| OBA18 MainViewModel/InfoViewModel/KM (direkt BindableBase) | Eigene Propertygrenzen; Basisklassenwrapper allein genügt nicht | H4 Main/Info teilweise; KM/alle Subscriber offen |
| OBA19 Status.Diagnostics.CollectionChanged → HasDiagnostics | O für Collection/Folgeproperty; immutable Diagnosen; Throw nicht als Captureerfolg ausgeben | H4 Diagnostics, weitere Subscriber offen |
| OBA20 VV.ApplyVariables/DataVariables/SelectedVariable + VariableModel/ParameterModel | O für Präsentation/Modelproperties, StateVersion-/Auswahlbindung; Fehler stale; Commit nur X | H4 ModifyValue ab Command, echte DataGrid-/Modellkette offen |
| OBA21 InfoHistoryChanged/InfoView.DataContextChanged/Markdownnavigation | O, View-Lifetime/Navigation beachten; nicht ausserhalb H | F Kette, keine vollständige H4-Abnahme |
| OBA22 Editor.IsModified/IsReadOnly/Document AvaloniaProperty | Phase/Ticket vor Wirkung; Identitywechsel L, Fehler O14 | H4 injizierter Propertyobserver, Produktabdeckung offen |
| OBA23 ReplaceConfirmation yes/no/ct.Register→Dispatcher.Post | Antwort an Originalrequest/Lease/D/Revision; nach await revalidieren, Late Close nur ursprünglicher Dialog | F vorhanden; echte Besitzer-/Shutdownfälle offen |
| OBA24 PasswordDialog Confirm/Cancel/Closed/Opened | O; Passwortfelder leeren; Antwort nur passende RequestId; keine Secrets loggen | H4 Providerprobe ersetzt sichtbaren Dialog nicht |
| OBA25 PasswordDialogHost.OwnerClosed/Cancellation.Register/Post | Dialoglifetime/Requestbindung, Close idempotent; Callback-/Antwortdrain | Host/WP vorhanden; reale Rennen offen |
| OBA26 AR.Timer/UserCancellation/Retention/WaitingForUser/Publish | Keine UI unter Registrylock abwarten; Job/Epoche/RegistrationId prüfen; vor Registrierung/Post zählen, Producer vor Nullprüfung schliessen; ReleaseResources und Zertifikat vor Epochenwechsel | Registrybasis; E05–E09 Callbackdrain, Speicherbound und späte Timer offen |
| OBA27 WP.PasswordRequested/Print/Info und Publish | Ein Runner pro Einheit, Output am Ursprung kopieren; Subscriber im finally entfernen, Antworten drainen | H4 Pipeline; Exception/Outputgrenzfälle prüfen |
| OBA28 GUI beforeLine/afterLine/Present/appendLine | Explizites X-Ticket, Originaleditor/Service; UIfortsetzung revalidieren; Teilwirkung erhalten | H4 Bookprobe; Fehler/Detach jeder Zeile offen |
| OBA29 Save Picker/Overwrite/Delete/Write/Lock/Unlock/finally | I/S; vor Commit kein überholter Write, nach Commit Originalfinalisierung | H4 VM-Provider; echter StorageProvider/Ordneranlage offen |
| OBA30 App.Window.Closed/Desktop.Exit/VM.Dispose | L/Shutdown, doppelte Dispose idempotent; kein synchroner UI-Deadlock beim Drain | H4 Service-Shutdown; reale Appbeendigung offen |
| OBA31 Fokus/Selection/Anchor/LineTracker/Options-/Templatecallbacks | Teil der nativen Route, O oder originalgebundener Algorithmus; neue Intentgeneration; Optionsersatz erhält Drag/IME-Sperre | H3 Fokus FAIL, H4 explizite Hülle; vollständige Produkt-/OSmatrix offen |

Statische Subscriptionvollständigkeit ist kein Beweis gegen unbekannte direkte Drittanbieter-Abonnenten. Fremdmutatoren ausserhalb H erhalten keine neue Garantie. Eigene fehlende Wrapper, interne Librarycallbacks innerhalb benötigter Commands und neue Produktabonnenten bleiben Abnahmearbeit innerhalb H.

## 9. EC21 und übrige Integrationsgrenzen

### 9.1 EC21

D: Gesamteditor-Ausführung verändert den Quelltext nicht durch zusätzliche `out:`-Einfügungen. F: EO.ExecuteEditorAsync fügt in seinem afterLine-Callback aktuell `"\n" + output` in registration.Document ein (Zeile 240 des analysierten Standes). P/IR: diese Einfügung im gesonderten AP5-Auftrag entfernen; Resultat-/Präsentationsoutput und SourceRevision erhalten. Kein implizites Clear oder Wiedereinfügen aller Quellzeilen.

Der kollidierende Test [EditorAutomationTests.cs](../../CryptoWorkBenchGuiTest/EditorAutomationTests.cs), `WholeEditorPrintIsInsertedOnceWithoutDuplicatingSource` (ab Zeile 105), erwartet Print plus out:-Text und Revisionsanstieg. D05 bestätigt die Sollsemantik verbindlich; Produktcode- und Teständerung benötigen weiterhin einen **separaten Auftrag**. Dieser Auftrag ändert weder Test noch Assertion. Neue Sollassertion: Text/Source-Revision durch Gesamtausführungsoutput unverändert; Output im Ergebnis vorhanden und redigiert. `ActualReturnKeepsPendingExecutionAnchorOutputAndRevision` bleibt für interaktive Ausgabe erhalten. H4s EC21-FAIL der alten Assertion wird nicht rückwirkend PASS.

### 9.2 Ein stateVersion-/Servicepfad

F: AS.ClearAdmitted ist statisch und ruft Shared.AdvanceVersion/CaptureMutation auf; VM kann einen anderen Service injizieren. VV.Mutate/SetupVariables und KM.LoadInfoAsync greifen ebenfalls auf Shared zu. Die Produkt-DI registriert Shared, dennoch darf der Vertrag nicht zufällig von identischer Konstruktion abhängen.

P/IR: genau die DI-Serviceinstanz besitzt Coordinator/Registry/stateVersion; Book-Clear wird instanzgebundene bereits-admittierte Stateoperation, keine neue Q-Reservation innerhalb eines Runs. VV/KM verwenden denselben Service. stateVersion wird konservativ bei potentieller Statewirkung erhöht, auch bei Teilfehler, vor Veröffentlichung des passenden Snapshots. Lease-/Editoränderung/Renewal ändern stateVersion nicht. Revision ist kein Ersatz. M50 Refresh kopiert nur und erhöht stateVersion nicht ohne Statewrite. Vor AP3/5 prüfen: nicht Shared injizierter Service + Book-Clear + Modify + KeyMap; Ergebnisversion und sichtbarer Variablenbestand stimmen überein.

### 9.3 Save über Besitzerwechsel

F: SaveIntent enthält Registration/Snapshot, keine controlGeneration. Acquire→Release ohne Textänderung wäre durch Revision nicht erkennbar. P: Besitzgeneration ergänzen; Intent/Overwriteziel vor Delete erneut prüfen. Angenommener Commit hält Q/Ticket bis Delete/Write/Unlock, auch nach Cancel/Shutdown. ASCII, Filter leerer/Whitespacezeilen, LF und Append bleiben; keine verlustfreie Speicherung oder clean-Baseline behaupten. Fehler nach Delete kann Datei beschädigt hinterlassen; sicher berichten, keinen unsicheren Rückbau. M41s Ordneranlage zusätzlich vermitteln.

### 9.4 Jobs, Passwortquarantäne und Ergebnisabschluss

Bestehende AR.Accept/RunOwnedAsync/ReleaseResourcesAsync/FinalizeJob-Kette ergänzen, nicht ersetzen. Request-Token vor Annahme und fürs Warten; serviceeigener Jobtoken für Run. Defaultdeadline 30 Sekunden, erlaubt 1–120 Sekunden ab Annahme einschliesslich Zustimmung. Renewal verschiebt keine Jobdeadline. Retention 15 Minuten ab terminalem Abschluss ist keine Leasehöchstdauer.

Passwort-Request-/Executionidentität, Quarantäne und Entsorgung verspäteter Antworten bleiben. Runnerende/Deadline/Cancel allein beweisen keinen Callbackdrain. Registry-Ressourcenfinalisierung und UI-/Outputtickets müssen abgeschlossen sein, bevor GUI freigegeben wird. Jobresultat braucht keine aktive Lease, aber gültige Owner-/Epochen-/Leserechte. GetExecution/Diagnostics/Cancel lösen keinen Run aus. D02-Epochenabschluss entzieht alte externe Ergebnisrechte unwiderruflich; intern nötige Finalisierungsdaten bleiben bis vollständigem Abschluss. Epochwechsel löscht keine offenen Jobs, setzt keine Execution-ID-Kontingente zurück und macht alte Results nicht für einen neuen Credentialkontext zugänglich. Das bestehende DrainAsync allein ist noch kein Zertifikat für sämtliche Callback-/Editorwirkungen; siehe 5.6/5.7.

Diagnosen/Fehler/Output/Variablen vor Freigabe in immutable hostneutrale Kopien projizieren. Keine ASTs, lebenden Dictionarywerte, mutable SemanticError-Objekte oder UI-Modelle im Transport. Capturefehler markieren Ergebnis/Stalezustand. Bestehende Jobzustände beibehalten; Editor faulted ist kein neuer Runnerzustand.

## 10. Sicherheits- und Lifecycleanforderungen

- Authentisierte Discovery/Status, default-off, numerisches Loopback, Host-/Port-/Originprüfung, Credentials/Epochenwiderruf und Body-/Concurrencylimits bleiben. Lease ersetzt weder Auth noch lokale Scriptzustimmung. Keine produktive HTTP-Aktivierung in AP0–AP7.
- Zustimmung bindet Script/Snapshot, editorId/revision, stateVersion, Client, Arbeitsverzeichnis und Ausgabeoption. Alte Dialogantwort nach Besitz-/Identitäts-/Statewechsel ungültig. Replacebestätigung für nichtleeres oder dirty/unknown Dokument bleibt.
- Fehler-/Logs: feste Codes/messageKeys und inhaltsfreie Korrelationsdaten. Keine Scripts, Passwörter, Schlüssel, Rohparameter, Leasecredentials, Exceptiondetails oder lokale Pfade in MCP-Fehlern. Editorinhalt nur mit editor.read; Outputtext mit output.read, Opt-in und lokaler Zustimmung; Variablen nur Metadaten.
- Bestehende Limits zusätzlich zu O15: 256 Ki UTF-16 Script/Edit, 1 MiB HTTP-Body, je Lauf 64 KiB Output und 1.000 Output-/Diagnose-/Variableneinträge, 64 terminale Ergebnisse/8 MiB, 10.000 Execution-IDs/Tombstones. Leasebudget darf aktive Jobdaten nicht verdrängen.
- Release/Expiry/Reclaim/Detach/Shutdown entziehen neue Arbeit sofort, niemals nötige Abschlussberechtigung des Originaltickets. Neue View erhält keine alte Completion-/Save-/Outputfortsetzung oder alten Unlock. Faulted nicht durch Renewal, Reattach allein oder Statusread schreibbar machen.
- H3/H4-Observerprinzip begrenzt eigene Callbacks. Nicht kontrollierbarer benötigter nativer Pfad erzwingt Architekturentscheid; kein stiller Bibliothekspatch, Avalonia-12-Upgrade oder B2.
- Native IME-Deaktivierung bleibt Gate trotz entfallener CJK-Bedienanforderung. Unicode, Schweizer Layout, Dead Keys, AltGr, Clipboard und Undo/Redo bleiben nötig. Text-Drag dauerhaft aus; Datei-Drop kein MVP-Gate.

## 11. Abhängigkeiten und Freigabegates AP1–AP8

Kategorien: **IMPLEMENTATION REQUIRED (IR)** = definierte Regel fehlt im Produkt; **VERIFICATION REQUIRED (VR)** = Produkt-/Plattformnachweis fehlt; **ARCHITECTURE DECISION REQUIRED (AR)** = Vertrag/Eigentümerentscheidung offen. Mehrere Kategorien derselben Zeile sind getrennte Aufgaben.

| Paket | Vorbedingungen / Restarbeiten | Kategorie und konkrete Abnahme | Empfehlung heute |
|---|---|---|---|
| AP1 Native IME | R2 O13, U2-Plan, CJK-Referenzumgebung; M02/03/OBA31 | VR: echte Positivkontrolle, Erstfokus, Preedit/Commit/Cancel, Fokus-/Besitz-/Dokumentwechsel/alte Nachrichten; normale Eingaben/Undo. Bei Scheitern AR zum Hostweg | Eigenständiges offenes Gate. Vorbereitung möglich; kein Erfolg aus Headless |
| AP2 Koordinationskern | D01 entschieden; erforderliches AP1-Gate; D02-Fence-/Ticketvertrag aus 5.6, eine Bridge/Coordinator/Q; M01/09/32/34/44 | IR: frühe Zulassung, O14, U1, Originaltickets/Faultabschluss. VR: History/Undo/Redo und atomare Epochenprüfung/Callbackzählung, E01/E02/E05/E06 | O14-Eigentümerentscheidung geschlossen; begrenzter Auftrag und Native-Gate weiterhin erforderlich |
| AP3 GUI/Observer | AP2; M01–40/45/46/49–51, OBA01–22/31; O01/02/05/07/08/09 | IR: komplette GUI-/Shared-/Observervermittlung, D07 kein angebotenes TitleCase. VR: jede unterstützte Variante unter H; native TitleCase-Fehlerroute weiterhin sicher; Producer-/Ticketvollständigkeit E05/E06 | Funktionsentscheid D07 geschlossen; keine historische Teständerung oder pauschale Hostfreigabe |
| AP4 Save/Dialog/Lifecycle | AP2, AP3-Schnittstellen; M34/35/41–43/47, OBA23–30 | IR: Generation/Zielbindung und vollständiges Epochenabschlusszertifikat inkl. Save/Return/Passwort/Dispatcher. VR: E05–E08/E12, reale Providerfehler und dauerhaft blockierter Drain ohne Reaktivierung | Technische Drainnachweise offen, Voraussetzung jeder D02-Kapazitätswiederverwendung |
| AP5 EC21/Execution | AP2/3, AP4-Drainvertrag; D05 genehmigt; M36/38–40/47/49 | IR: EC21-Insert entfernen, ein stateVersion-Service, Job-/P-Tickets. VR: unveränderter Gesamteditor, Return genau einmal, Originaljob-/Callbackdrain trotz Epochwiderruf | Sollsemantik entschieden; Produktcode-/Teständerungen weiterhin nur mit separatem Auftrag |
| AP6 Lease-v2/Zeitgeber | D02–D04/D08 genehmigt; AP2/4/5; D02-Sicherheitsnachweis aus 5.8/5.9 | IR: v2/Grants/CAS/Epochfence/Tombstones/suspendzählender Adapter; VR: formales Modell und E01–E12, reale Credentials/Heap/Provider/Uhr. Bei nicht belegbarer Sicherheit konservativer Rückfall, keine Tombstonefreigabe | Keine offene Wahl des Epochmodells, aber formaler/experimenteller Beweis fehlt; keine produktive Freigabe |
| AP7 Hostabnahme | AP1–AP6 einschliesslich erforderlicher IR/VR/AR | VR: volle aktive-H-Matrix, echte OS-/Dialog-/Providerläufe, Legacy/historische Proben getrennt, Projektion/Shutdown; unabhängiges Review | Nicht freigegeben; H4/U1 unzureichend |
| AP8 Produkt/HTTP | AP7 + separater MCP-01.5-/Auth-/Transport-/Securityauftrag | IR/VR: echte Clients/Credentials, Zustimmung, E2E-Disconnect/Retention/Projektion. AR: Produktaktivierung | Gesperrt; kein Folgeauftrag aus AP0 |

Reihenfolge: genehmigte D01–D08 als Grundlage; D02-Modellprüfung und AP1-Vorbereitung → erforderlicher Native-Nachweis → AP2 → AP3/AP4 mit vollständiger Ticket-/Producerinventur → AP5 → AP6 mit experimentellem D02-/Zeitquellennachweis → AP7 → separat AP8. Formaler Entwurf und Testspezifikation dürfen vorbereitet werden; sichere Kapazitätswiederverwendung erst nach beiden Nachweisen. Schemaentwurf/Review kann vor AP2 vorbereitet werden; keine Erlaubnis für vorgezogene produktive Leaseimplementierung.

| Vertragsorakel | Erwartung |
|---|---|
| O14-a Callbackfehler Started/Changing/Changed/Finished/Dirty/IdentityDispose | Kein falsches none/Busy; Originalrevision; sichere Gruppen oder faulted; Folgeedit nur bei Complete |
| O14-b Undo-/Redo-Throw nach möglichem Pop bei unverändertem Text | historyEffect nicht aus Textgleichheit ableiten; reale Historyfolgen prüfen |
| O14-c Disconnect nach Editcommit | Zustandsabfrage statt blindem Retry; kein zweiter Effekt mit alter Revision |
| O15-a 4.096 Slots/Epoche, 256/Principal/Epoche, Bytebudget | CapacityExceeded verriegelt Neuannahme in derselben Epoche; Kompaktierung öffnet sie nicht. Erst L2 und lokal autorisiertes L3 geben Kapazität frei |
| O15-b Kompaktierung nach 15 Minuten und nach 64 Details | Alte acquisitionId niemals neue Lease; Release alter leaseId beeinflusst Nachfolger nicht |
| O15-c CAS-Retry/konkurrierende Renewals/abgelaufene Lease/alter Timer | Je inputVersion höchstens eine Friständerung, konstanter Speicher, kein Wiederbeleben |
| O15-d Disable/Enable/gleicher Principal/neue Epoche/Restart | E01–E09: alte Credentials/Caller bleiben ungültig, identische acquisitionId nur im neuen authentisierten Namensraum, keine parallelen Tabellen/Reset bei blockiertem Drain; Executionbudget unverändert |
| IN-a Book-Clear mit injiziertem Service / Grid / KeyMap | Ein Variablenbestand und dessen Serviceversion, kein GUI-Run unter MCP |
| IN-b Picker und Acquire→Release bei identischem Text | Alter Intent keine Zielmutation; angenommener Commit drainiert trotz Shutdown |
| IN-c Observer/Posts/Kindtask | Kein ambient Schreibrecht, kein neuer Run/Write aus O; keine versteckte Queue später erlaubter Mutationen |

## 12. Genehmigte Eigentümerentscheidungen und verbleibende Nachweise

Genehmigung durch den Projekteigentümer am **26.10.2026**, Auftrag MCP-00-AP0-R1. D01–D08 sind keine offenen Eigentümerfragen mehr. Die Trennung zwischen genehmigtem Ziel, ausstehender Implementierung und fehlendem Nachweis bleibt verbindlich.

| Entscheidung | Verbindlicher Inhalt / Status | Noch erforderlich |
|---|---|---|
| AP0-D01 / O14 | **Genehmigt:** strukturierter MutationReceipt mit getrennter Text-/Historywirkung/Finalisierung; angewandte Wirkung sichtbar, faulted bei unklarem Abschluss, kein automatischer Retry/ungesichertes Rollback | IR/VR: frühe Wirkungserfassung, sichere Gruppen/Originalrevision, Fehler-/Transportproben; Abschnitte 4/6 |
| AP0-D02 / O15 | **Genehmigte Designrichtung, nicht bewiesene Implementation:** 4.096/Epoche, 256/Principal/Epoche, 4 MiB; Tombstones bis kontrolliertem Abschluss, keine absolute Höchstdauer erneuerter Lease | Formales Safety-Modell plus E01–E12/Produktnachweis; atomare Fence, Credentialunwiderruflichkeit, voller Drain, bounded Speicher/Rotation. Bei fehlendem Beweis Rückfall 5.8 |
| AP0-D03 / Renew | **Genehmigt:** nur letzter erfolgreicher Renew-CAS-Receipt; alte CAS-Version verlängert nie erneut | IR/VR: atomarer CAS/Receipt, Fristrennen, Epoch-/Timerbindung, konstante Speicherbelegung |
| AP0-D04 / v2 | **Genehmigt:** contractVersion="2", keine v1-Fallbackmutation, editor.control und leaseId als zusätzliche nicht hinreichende Bedingungen | IR/VR: Auth/Grants/Epoche an Admission/Commit, strikte Schemas, sichere Projektion, komplette Status-/Executionversionierung |
| AP0-D05 / EC21 | **Verbindlich:** Gesamteditor ohne zusätzliche out:-Quelltextmutation, interaktive Returnausgabe bleibt | Produktcode-/Teständerungen nur mit separatem Auftrag; beide Ausgabewege regressionsprüfen, historischen EC21-FAIL erhalten |
| AP0-D06 / IME | **Verbindlich:** native Unterdrückung OPEN, blockiert vollständige Hostfreigabe | AP1 Native-/CJK-Nachweise; bei nicht kontrollierbarem benötigtem Pfad neuer Architekturentscheid, kein stiller Upgrade/B2 |
| AP0-D07 / TitleCase | **Genehmigt:** keine unterstützte CryptoWorkBench-GUI-Funktion | M27/native Aufruf- und Fehlerroute korrekt absichern/inventarisieren; keine Altassertion ändern oder historischen FAIL umwerten |
| AP0-D08 / Suspend | **Genehmigt:** Suspend/Standby zählt zur zehnminütigen Frist; keine automatische Verlängerung | Suspendzählende Plattformzeitquelle und Resumeprüfung vor Wirkung, alte Timer abweisen; E11/Faultfall bei unzuverlässiger Zeit |

Verbleibende technische Reviewfragen betreffen den Beweis der spezifizierten Epochfence-/Drainmechanik, reale Speichergrössen und Plattformadapter, nicht erneut die fachliche Genehmigung D01–D08. IR/VR sind weiterhin offen. Nur falls ein benötigter Pfad die Prämissen widerlegt oder eine Abweichung von D01–D08 nötig wird, ist ein neuer ARCHITECTURE DECISION REQUIRED zu eröffnen. Der konservative Rückfall ist ausdrücklich beschrieben; kein automatisches Löschen von Tombstones zur Wiederherstellung der Verfügbarkeit.

## 13. Abschliessende Implementierungsbereitschaftsbewertung

**AP0-R1 integriert die genehmigten Entscheidungen D01–D08. O14 ist genehmigt; D02 ist genehmigte Designrichtung mit ausstehendem formalen und experimentellen Sicherheitsnachweis. Vollständige Hostimplementierung und AP2–AP6 erhalten weiterhin keine pauschale produktive Implementierungsfreigabe.** Der native IME-Nachweis bleibt eigenständiges AP1-Gate. H4/U1 rechtfertigen die Entwicklungsrichtung, aber keine vollständige Callback-/Pfadgarantie oder produktive HTTP-Aktivierung.

Die spätere Umsetzung ergänzt bestehende Komponenten: ein Coordinator, bestehende Bridge/Registry/Q/Pipeline, native History und ein Variablenbestand. Kein Laborflag, keine zweite Editorengine oder zusätzlichen Runnerzustände. Die Eigentümerentscheidungen D01–D08 liegen vor. Erforderliche technische/native Gates und gesonderte Paketaufträge bleiben; AP7/8 benötigen eigene Abnahme. Replay-Sicherheit beruht im D02-Ziel auf dauerhafter Epochenungültigkeit nach L1 plus vollständigem Abschluss vor Kapazitätswiederverwendung, nicht auf zeitlich begrenztem Vergessen alter Erwerbs-IDs.

### Historischer Erhaltungs- und Abschlussnachweis der ursprünglichen AP0-Fassung

Vor Berichtanlage wurden 23.332 vorhandene Dateien in Produktworktree, H1–U2 und Git-Metadaten (Worktree-Gitverzeichnis und gemeinsames Gitverzeichnis) per SHA-256 erfasst. `.codex/config.toml` wurde vor Inhaltszugriff ausgeschlossen, weder gelesen noch gehasht oder verändert. Temporäre Vergleichsdateien liegen ausschliesslich im Benutzer-Tempverzeichnis. Keine Produktbuilds oder Laufzeittests; E-Befunde wurden nur gelesen.

Abschlussvergleich am 26.10.2026: alle 23.332 zuvor erfassten Originaldateien unverändert (SHA-256), einschliesslich der erfassten Git-Metadaten. HEAD/Branch und staged Diff identisch, Staging weiterhin leer. Git-Status entspricht exakt der Ausgangsbasis plus diesem einen neuen unversionierten AP0-Bericht; keine weitere Datei hinzugekommen. Berichtprüfung: 13 Hauptabschnitte, M01–M51 und OBA01–OBA31 vollständig, alle lokalen Dokumentlinks auflösbar, vier JSON-Blöcke syntaktisch gültig. O14-Beispiel und Acquire-Beispiel zusätzlich gegen die jeweiligen JSON-Schemas mit PowerShell Test-Json geprüft. Dies sind Dokumentprüfungen, keine Produkt-/Native-Tests oder vollständige Schema-Konformitätsabnahme. Keine produktiven C#-/AXAML-/Projekt-/Teständerungen, keine Änderungen bestehender Verträge/Evidenzen, kein Commit/Push/Merge/Reset/Rebase/Stash, kein B2 und keine HTTP-Aktivierung. Dieser Bericht löst keine automatische Implementierung oder Freigabe aus.



### AP0-R1 – Arbeits- und Erhaltungsnachweis

Die obigen Angaben zur ursprünglichen AP0-Erstellung bleiben als historische Dokumentation bestehen. Für AP0-R1 wurde der aktuelle Stand erneut erfasst: gleicher HEAD/Branch, sechs geänderte getrackte Dateien, 20 unversionierte Dateien einschliesslich des bereits vorhandenen AP0-Berichts, Staging leer. Neue Baseline: 23.333 Dateien einschliesslich Bericht, H1–U2 und Git-Metadaten. `.codex/config.toml` vor Inhaltszugriff ausgeschlossen, weder gelesen noch gehasht oder verändert. Ausschliesslich dieses vorhandene Dokument wird überarbeitet; temporäre Vergleichsdaten liegen ausserhalb des Produkts.

R1-Abschlussprüfung am 26.10.2026 bestanden: Von 23.333 erfassten Dateien wurde ausschliesslich dieser AP0-Bericht geändert; alle übrigen 23.332 Dateien einschliesslich H1–U2 und Git-Metadaten sind SHA-256-identisch. Keine neue oder entfernte Datei; HEAD, Branch, Git-Status und staged Diff unverändert, Staging leer. Alle 51 Mutations- und 31 Observer-IDs in ursprünglicher Reihenfolge erhalten; 13 Hauptabschnitte und alle acht Entscheidungen vorhanden, lokale Dokumentlinks auflösbar. Vier JSON-Blöcke syntaktisch geprüft; 20 Dokument-Schemaprüfungen bestanden (gültige Requests/O14-Receipt, Ablehnung von v1, clientseitiger activationEpoch und fehlenden leaseIds). Keine vollständige Schema-Konformitäts- oder D02-Sicherheitsabnahme behauptet. Keine Produkt-/Native-/Modelltests, keine Implementierung von AP1–AP8. Die spezifizierten E01–E12 sind zukünftige Nachweise, keine gemeldeten Testerfolge.


## Eigentümerergänzung vom 26.10.2026 – AP2.1 / akzeptiertes IME-Restrisiko

Diese neue Entscheidung ersetzt ausschliesslich die früheren Aussagen, nach denen der fehlende native CJK-Negativtest den Beginn von AP2 oder zwingend die finale Hostfreigabe blockiert (insbesondere AP0-D06). Die damalige technische Evidenz wird dadurch nicht nachträglich verändert.

- Mehrstufige CJK-IME-Komposition gehört nicht zum unterstützten Funktionsumfang des CryptoScript-Editors. Der Projekteigentümer akzeptiert das verbleibende Risiko einer nicht vollständig nachgewiesenen nativen IME-Unterdrückung.
- Frühestmögliche **dauerhafte** IME-Deaktivierung an Editor und fokussierbarer TextArea bleibt verpflichtend, vor erstem Benutzerfokus und nach relevanter Neuinitialisierung. Keine Reaktivierung bei Release; `EnableTextDragDrop=false` bleibt ebenfalls verpflichtend.
- Normale Unicode-Eingabe, Schweizer Tastatur, Dead Keys, AltGr und Clipboard bleiben unterstützt. Keine pauschale Unicode-/WM_CHAR-Filterung.
- U2 bleibt historisch **INCONCLUSIVE**; echte CJK-Negativtests bleiben technisch OPEN. Der fehlende Nachweis wird nicht als PASS ausgegeben. Alle anderen Sicherheits-, Drain-, Credential-, Speicher- und Integrationsgates bleiben bestehen.

AP2.1 ist als produktives, begrenztes Grundpaket autorisiert. Der normale Produktbetrieb bleibt bei GUI-Besitz; eine reale MCP-Übernahme, externe Lease-v2-Tools, Credentialrotation, HTTP-Aktivierung und epochenbezogene Tombstonefreigabe sind nicht freigegeben. Die D02-S1-Modellprüfung ist weiterhin kein Produktdrain-Zertifikat.

Implementierung und Prüfresultate stehen in [MCP-00-AP2.1-implementation-review.md](MCP-00-AP2.1-implementation-review.md). Diese Ergänzung erteilt keine vollständige MCP-01.4-/HTTP-Freigabe. AP2.1 darf erst nach einem separaten unabhängigen Review als abgeschlossen gelten. EC21-Code und historischer EC21-Test bleiben AP5 und in AP2.1 unverändert. Historische H4-/H4-IME-/U1-/U2-Berichte und D02-S1 werden nicht umgeschrieben.
