# G2-04-01-R4 — Independent Final Architecture Verification

**Entscheidung: CHANGES REQUIRED**  
**Ergebnisstatus: READY FOR OWNER REVIEW — nicht ACCEPTED**  
**Datum:** 17.11.2026  
**Repository:** tkbss/CryptoWorkBench  
**Verifizierte Baseline:** `32017a08c4771e73c12a50f6e1548476b9bb0a54`

Die sechs R3-Findings sind auf Vertragsebene weitgehend geschlossen: **fünf CLOSED, eines PARTIALLY CLOSED, keines OPEN**. Ein neuer **Low-Befund** betrifft den normativen KEY-Root-Golden und dieselbe Fixture im HTML: `Statement.Text` enthält dort ein Leerzeichen, das der tatsächliche Parser nicht übernimmt. Keine neuen High- oder Medium-Findings.

Die Host-/Snapshot-Zielarchitektur ist mit begrenzten Änderungen an den vorhandenen Strukturen realisierbar. Eine zweite Host-Autorität ist nicht erforderlich. Die Korrektur vor Eigentümerabnahme ist auf Fixture, Golden und deren Herkunftsnachweis begrenzt; ein neuer Architekturentwurf ist nicht nötig. Die native Implementierungsabnahme bleibt vollständig ausstehend.

## 1. Prüfgrundlage und Nachweisgrenze

`HEAD` und lokaler `master` wurden zu Beginn und am Ende gegen die Baseline geprüft. Beide stimmen exakt überein; der Worktree ist sauber. `origin` ist `https://github.com/tkbss/CryptoWorkBench`. Die Aussage betrifft den vorhandenen lokalen Masterstand; es wurde kein Fetch und keine Aussage über einen inzwischen veränderten Remote-Branch vorgenommen.

Direkt geprüft wurden die konsolidierte F2-Architektur, Accessor-Matrix, Copy-Vertrag, alle 19 Golden-Fälle, Corrections Report, alle 38 Implementierungskriterien, HTML und dessen Modell-/UI-/Build-/Validierungsquellen sowie R3. Keine dieser Prüfgrundlagen fehlt. Die 13 im F2-Provenienzmanifest aufgeführten Artefakthashes stimmen mit den vorhandenen Dateien überein.

Die F2-Validierung wurde erneut ausgeführt, wobei ihr einziger Dateischreibaufruf im Prüfharness abgefangen und ausschließlich ausgewertet wurde. **38/38 Dokumentationsprüfungen bestanden erneut.** Öffentliche BCL-Property-Metadaten wurden unabhängig auf der vorhandenen Runtime **.NET 10.0.14** abgefragt. Der C#-Abgleich erfolgte durch Quellprüfung, einschließlich vorhandener Testquellen; deren Tests wurden nicht ausgeführt.

Keine Produktbuilds, keine neuen oder geänderten Produkttests, keine native Avalonia-/Dock-/Clipboard-Sitzung und keine Browser-/Screenshot-Abnahme. Die Aussage über C#-Semantik ist ein nachvollziehbarer Quellbefund, keine behauptete Ausführung eines noch nicht implementierten Inspectors. F2 blieb unverändert. Nur dieser R4-Bericht und sein Prüfprotokoll wurden außerhalb des Repositorys erstellt.

## 2. Einzelentscheidungen zu R3

| R3 | Entscheidung | Unabhängige Begründung |
|---|---|---|
| **H01 — Complex Copy** | **CLOSED** | Der Copy-Vertrag verwendet tatsächliche Member, Arrays und Dictionary-Einträge; keine Inspector-Hüllen. Referenzalias wird als Wert wiederholt, echter Zyklus verhindert Parent-Copy. Fehler/NotLoaded werden nicht zu erfundenem null. PARAM.GetParameters bleibt ein separater Methodenadapter. Der neue Fixture-Herkunftsfehler wird unter L01 bewertet, nicht erneut als der frühere Hüllenfehler. |
| **H02 — Large Object Access** | **CLOSED** | Kein festes Root-/Tiefen-/Node-Limit mehr. ReadSpec, vollständige Range-Coverage, gleicher CaptureStamp, gehaltene Host-Admission, Payload-Segmente und vollständiger Clipboard-Endschritt sind konkret festgelegt. Der benötigte Share-Range-Accessor ist ausdrücklich eine geplante Core-Ergänzung. Physische Ressourcenfehler werden von funktionaler Kürzung unterschieden. |
| **M01 — Property Inventory** | **CLOSED** | Root-, Unterobjekt- und BCL-Inventar sind getrennt von Einträgen und Copy-Schemata definiert. Konkrete Runtime-Typen und Getter-Slots einschließlich transitiver Abhängigkeiten begrenzen Auswertung; unbekannte Getter bleiben sichtbar. Die auf .NET 10 geprüften Collection-Properties stimmen. |
| **M02 — Observer Scheduling** | **CLOSED** | Observer erfasst nur Absicht; ein koaleszierter Dispatcher-Post erfolgt nach Rückkehr. Admission wird neu geprüft. Request-Wechsel, Close und Dispose entwerten Ergebnisse; Busy führt nicht zu einer Retry-Schleife. Dies entspricht dem tatsächlichen Observer-Gate. |
| **M03 — Snapshot Lifecycle** | **CLOSED** | Normale Edits/MCP/InvalidateDraft erhalten historische detached Werte; tatsächlicher Reset, explizites null und ApplyVariables-null geben sie frei. Semantischer Reset wird nicht aus WorkspaceGeneration abgeleitet. Keine automatische Wiederbelebung alter Handles nach Release. |
| **L01 — Visual Fixtures** | **PARTIALLY CLOSED** | KEY-Metadaten, HeaderReference, OriginalKeyMetadata, große Werte und Zwei-Spalten-Default sind korrigiert. Neu entdeckt bleibt die falsche Herkunft des KEY-Text-Members in Fixture und Golden: R4-L01. |

„CLOSED“ bezeichnet die nachgeprüfte Architekturkorrektur. Es bezeichnet keine bestandene native Implementierungsprüfung.

## 3. Neuer Befund

### R4-L01 — KEY-Root-Golden übernimmt Quelltext statt tatsächlichem Statement.Text

**Schwere:** Low. **Zuordnung:** R3-L01 / F2-06; betroffen ist außerdem der normative Golden-Vergleich A09. Kein neuer allgemeiner Copy- oder Host-Defekt.

**Artefaktbelege:** [prototype-model.js:8][fixture] setzt den KEY-Text auf `'KEY demoKey='+keyValue`. [copy-golden-values.json:9][goldens] enthält denselben Wert im Root-Golden. [build-artifacts.cjs:20][build] erzeugt die fünf Root-Goldens aus den gleichen Fixture-Objekten. Der separate Referenzencoder prüft deshalb die Serialisierung, aber nicht unabhängig die fachliche Herkunft der Eingabe. Die entsprechende Fixture ist auch im ausgelieferten HTML eingebettet.

**C#-Belege:**

1. [CryptoLexer.g4:177][lexer-ws] verwirft Whitespace außerhalb von Tokens durch `WS ... -> skip`.
2. [AntlrToStatement.cs:14][statement-map] setzt `VariableDeclarationStatementNode.RawText` auf `context.GetText()`.
3. [StatementEvaluator.cs:45][statement-eval] übernimmt diesen Text in `result.Text`.
4. Die vorhandene [StatementExecutionTests.cs:43][statement-test]-Testquelle erwartet für `VAR value = "raw value"` ausdrücklich `VARvalue="raw value"`. Leerzeichen im Stringliteral bleiben erhalten; zwischen Tokens fehlen sie.

**Reproduzierbare Ableitung für die dokumentierte Fixture:**

```text
Eingabe:             KEY demoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)
C# Statement.Text:   KEYdemoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)
F2 Golden.Text:      KEY demoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)
```

Der Unterschied ist genau das zusätzliche Leerzeichen nach `KEY`. Der Wert der Property `Value`, die Schlüsselgröße und die übrigen Metadaten sind davon nicht betroffen. Der Befund wurde aus dem fixierten Parser-/Evaluatorpfad und den vorhandenen Testquellen abgeleitet; kein Produktlauf wird behauptet.

**Auswirkung:** Ein korrekt implementierter Inspector, der das tatsächliche `Text` unverändert kopiert, scheitert an A09 für `root-key`. Umgekehrt kann der HTML-Test weiterhin bestehen, weil Erwartung und Fixture dieselbe falsche Eingabe teilen. Die 38 grünen Artefaktprüfungen schließen diese Abweichung nicht aus.

**Minimal erforderliche Korrektur:** Nur den tatsächlichen `Text` der KEY-Fixture und des zugehörigen Root-Goldens korrigieren, das eingebettete HTML entsprechend neu erzeugen und Validierungs-/Provenienzbelege aktualisieren. Der menschenlesbare Beispiel-Quelltext darf sein Leerzeichen behalten. Den Inspector-Encoder nicht zum Entfernen oder Ergänzen von Leerzeichen ändern. Alternativ wäre eine ausdrücklich synthetische C#-Zuweisung von `Text` zu dokumentieren; die jetzige Behauptung eines unveränderten Script-Ergebnisses wäre dann aufzugeben. Empfohlen ist die Korrektur auf den realen Scriptwert.

**Akzeptanzkriterium:** Für das exakt dokumentierte Script stimmt `root-key.Text` mit dem Parser-/Evaluatorergebnis ohne Trennleerzeichen überein. Der erwartete Text muss unabhängig von `D.fixtures` festgelegt werden. Der gesamte Root-Payload bleibt ein exakter Codeeinheitenvergleich; insbesondere bleiben echte Leerzeichen innerhalb von Stringwerten, CRLF und abschließendes LF erhalten. Alle 19 Goldens und die eingebettete HTML-Fixture müssen danach konsistent sein.

**Keine Änderung durch R4:** Der Fehler wurde ausschließlich dokumentiert. F2 wurde nicht korrigiert oder neu gebaut.

## 4. Schwerpunkt A — Host-Lebenszyklus und minimale Realisierbarkeit

Die Architektur lässt sich im bestehenden Automation-Service umsetzen. Dabei sind mehrere explizite Zweckverzweigungen erforderlich; ein Wrapper um das unveränderte RunGuiAsync reicht nicht. Diese Anforderung steht bereits in F2 §6 und A25–A29.

| Station | Tatsächliche Baseline / notwendige Umsetzung des F2-Vertrags |
|---|---|
| Admission | [RunGuiAsync:391][run-gui] nimmt den Service-Lock, erzeugt ein ExecutionIntent und reserviert anschließend den Host. [Accept:264][accept] registriert den Job vor Workerstart. Die Reihenfolge kann wiederverwendet werden; der interne Zweck muss bei Stop/Epoch-Close/Phase-/Selection-Konflikt fail-fast bleiben. |
| Fehler bei Annahme | Accept entfernt teilweise veröffentlichte Registry-Einträge und gibt die Reservation frei. Der äußere RunGuiAsync-Catch disponiert das Intent. Der neue Einstieg muss diese Ownership-Teilung erhalten: Reserve-Fehler nach Intent-Erwerb sowie Accept-Fehler dürfen kein Ticket zurücklassen. |
| ExecutionIntent | [EditorCoordinator:553][intent] erzeugt ein vorhandenes OperationTicket, ohne ein langes Dokument-Update zu öffnen. Pending blockiert MCP-Erwerb, während normales Tippen möglich bleibt. Ein zusätzlicher globaler Read-Owner ist unnötig. |
| Worker/Core | [RunAdmittedAsync:45][host] startet die angenommene Arbeit ohne cancellable Task.Run-Starttoken. [InterpreterExecution.AccessState:87][core] ist synchron und threadgebunden; kein await darin. Zwischen Read-Schritten darf der asynchrone Core-Scope bestehen bleiben, während StateAccess verlassen ist. |
| **Recovery-Vorlauf** | [RunOwnedAsync:429][run-owned] ruft heute EnsureRecovered auf. [EnsureRecovered:685][recovery] kann alle Variablen neu projizieren und dabei ungeprüft virtuelle `Type`-/`Name`-Getter lesen. **F2s ausdrückliches Verbot jeder Read-Reprojektion und ungeprüfter Getter gilt auch hier: InspectorRead muss diesen Vorlauf überspringen.** Nur PendingVariables/Finalize zu verzweigen wäre eine fehlerhafte Implementierung des bereits geltenden Vertrags. |
| Ausführungsnebenwirkungen | PendingVariables/ExecutionResult.Variables, RecordUnit, Effects, AdvanceVersion, CompletionSequence/last, Bound, Retention und Tombstones dürfen nicht als regulärer Execution-Pfad für Reads laufen. Das bestehende [FinalizeJob:583][finalize] ist daher nicht unverändert verwendbar. Öffentlicher Busy-Zustand darf aktive Arbeit anzeigen; eine interne Read-ID darf dadurch nicht zur öffentlichen Execution werden. Auch [GetStatus/Lookup:126][public-status] muss den Zweck berücksichtigen. |
| Cancellation | Benutzer-/Auswahl-/Lifecycle-Cancel ist kooperativ. Der Getter muss enden, IO muss drainen; erst dann darf die Reservation frei werden. Der Read darf nicht den normalen Deadline-Timer starten. Das bestehende [ReleaseResourcesAsync:503][release] wartet außerhalb von sync auf Registrierungen/Callbacks und disponiert das Intent auf der UI. |
| MCP und Shutdown | [Acquire:247][acquire] verweigert bei active/Pending die Übernahme. [FinishEpochCloseAsync:51][epoch-close] startet BeginDrain auf der UI **vor** dem Warten auf bekannte Producer. Daraus folgt kein notwendiger Kreis „UI wartet auf Worker, Worker wartet auf UI“, solange der neue Pfad wie vorgeschrieben asynchron bleibt. Stop muss den internen Job bis zu dessen tatsächlichem Abschluss sehen. |
| Fehler/Publikation | Read-Staging bleibt privat. Cancel, Getter-/IO-Fehler oder Finalisierungsfehler dürfen keinen vollständigen Parent publizieren. Read-Erfolg allein ist nicht Finalisierungserfolg. Bereits abgeschlossene Teile bleiben gemäß Ereignismatrix erhalten. |
| Finale UI-Prüfung | Die operative Intent-Prüfung muss vor dessen Dispose erfolgen; `ExecutionIntent.Current` ist nach Dispose erwartungsgemäß false. Für die nach Finalisierung erfolgende Veröffentlichung sind die festgehaltenen Request-/Publikations-/Workspace-/Selection-Stempel erneut auf der UI zu vergleichen. Kein Wiederverwenden eines disponierten Intent als gültige Autorität. |

**Bewertung:** Kein belegter architekturbedingter Deadlock und kein Bedarf an einer parallelen Interpreterverwaltung. Die heiklen Stellen sind Recovery, Registry-Sichtbarkeit und die Fehlersignalisierung nach Cleanup. Sie sind konkrete Umsetzungsrisiken innerhalb der vorhandenen F2-Invarianten, keine zusätzlichen offenen Produktentscheidungen. F2 verbietet bereits Reprojektion und verlangt erfolgreiche Finalisierung vor Commit; das Fehlen des Methodennamens EnsureRecovered im Entwurf wird deshalb nicht künstlich zu einem neuen Architektur-Finding erhoben.

Für die spätere Prüfung von A25–A29 sind insbesondere diese Fälle nötig: vorher fehlgeschlagene Variablenprojektion mit einem nicht ausgewählten unbekannten Getter; Fehler vor und nach Publish; Cancel während eines Read-/IO-Schritts; Shutdown/MCP-Drain während des Read; viele Seitenreads bei gefüllter Execution-Retention; UI-Invalidierung zwischen Workerende und Dispatcher-Publikation. Getterzähler bleiben 0, StateRevision/History/LastResult bleiben unverändert, Ressourcen werden genau einmal freigegeben.

## 5. Schwerpunkt B — Vollständigkeit, Copy und große Werte

### Copy und Goldens

Die Schemata stimmen mit den öffentlichen fachlichen Membern überein. Insbesondere ist PARAM.GetParameters keine Property; Secret.Value ist tatsächlich `SECRET`; KeySizeInBits ist ein Objekt mit ulong-Bits; TR31H.Value ist ein Formatterstring und kein Wire-Header. `[JsonIgnore]` an KCV und IsKnown hebt den Inspector-Vertrag nicht auf.

| Golden-Fälle | Ergebnis des Quell-/Vertragsabgleichs |
|---|---|
| root-key | **Abweichung R4-L01 ausschließlich in Text.** Secret(Unknown), Unspecified/None, 256 Bit, leere Attribute/Derivation, drei null-KCVs, HEX_STRING und gequotete Type.Id sind richtig. |
| root-param, param-adapter | FromSnapshot setzt Value/Mechanism/HeaderReference ohne BuildValue. Die dokumentierte synthetische Konstruktion ist möglich. Der Root hat sieben Properties, der Methodenadapter eigene Dictionary-Copy. |
| root-split, root-shamir, split-shares | Descriptor-/Share-/Metadatenwerte passen zu [SecretSet.cs][secrets]. Die Split-Werte XORen zu 0607; die Shamir-Werte entsprechen dem angegebenen GF256-11B-Beispiel. X gehört nicht zum Bytearray-Payload. |
| root-header, tr31-definition, tr31-value | Create-Reihenfolge, Defaults, HeaderLength 16, null ExpectedLength und false IsLengthResolved passen zu [Tr31HeaderDefinition:28][header]. Die Reihenfolge des Value-Strings darf von ordinal sortiertem JSON abweichen. |
| key-type, key-size | Tatsächliche Member und Enumtexte korrekt; keine Inspector-Hüllen. |
| whitespace, empty, null, hex-string | String-Codeeinheiten bleiben unverändert; stringförmiges Hex wird nicht wie byte[] großgeschrieben. Empty/null haben den ausdrücklich vereinbarten unterschiedlichen UI-Typstatus. |
| bytes, empty-bytes, ulong-max | Hexdarstellung reversibel; 64-Bit-Maximalwert vollständig ohne Double-Zwischenschritt. |
| real-dictionary-key | Tatsächliche Keys type/status bleiben erhalten. Keine pauschale Key-Blacklist. |

Damit wurden alle 19 Fälle betrachtet: bei 18 wurde keine zusätzliche Abweichung festgestellt; root-key benötigt die genannte Korrektur. Die fünf Root-Erwartungen stammen aus demselben Fixture-Datensatz wie das Modell. Ihr erfolgreicher Testlauf allein ist deshalb kein unabhängiger C#-Semantikbeweis.

### Große Objekte und Konsistenz

Der Baseline-SecretDescriptor erlaubt mehr als 16 MiB Gesamtmaterial. F2 entfernt diese funktionale Grenze tatsächlich, statt nur den Zahlenwert zu erhöhen. Die erneute DOM-freie Prüfung erzeugte vollständig **33.423.360 Rohbytes**, **66.852.988 JSON-Zeichen**, SHA-256 `b1b6e0e777b6678f113ce4e59fc8f7d3cf282a21828828f8b2799f49d061ed3a`. Geprüft wurde das Shares-Array; dies ist kein Nachweis für den nativen vollständigen Root, Disk-Streaming oder Clipboard-Peaks.

CaptureStamp und Request-Zähler sind sinnvoll getrennt: mehrere Operationen derselben unveränderten Publikation können Teile ergänzen; A→B→A kann trotzdem alte Requests verwerfen. Die Kombination aus ursprünglichem Identifier, Weak-Origin, ausgewählter Zeilenidentität, Publikation, Workspace/Draft und globaler StateRevision verhindert das Umhängen auf ein neues gleichnamiges Objekt. Die Revision allein würde dafür nicht genügen.

Typisierte Pfade vermeiden Mehrdeutigkeit bei Dictionary-Keys mit Punkten/Klammern. Offset/Anzahl/Gesamtlänge und lückenlose Coverage verhindern die falsche Schlussfolgerung „gleiche Länge = vollständig“. Widersprüchliche Überlappungen werden abgewiesen. Getrennte Reads müssen den Pfad unter demselben Stamp neu auflösen; ein laufender großer Read hält dagegen die gemeinsame Admission. Externe, nicht kooperierende Objektmutation ist dadurch nicht abgesichert und wird von F2 ausdrücklich nicht als unterstützter Nebenpfad behauptet.

### Payload-Segmente und Clipboard

32 MiB Cache ist kein Root-Limit. Snapshotlokale Tempsegmente, verlustfreie UTF-16-Speicherung, begrenzte Formatierungsbuffer und Staging/Commit ergeben einen vollständigen Datenpfad. Payload-Leases verhindern Löschen während detached Copy; Close/Reset/Shutdown canceln noch nicht übergebene Arbeit und räumen nach Drain auf. Der finale vollständige Clipboard-String ist ausdrücklich vom Cachebudget ausgenommen. Daher besteht kein Widerspruch zwischen Cachebudget und vollständigem Copy.

Offen bleiben Messwerte, nicht Produktentscheidungen: tatsächlicher Peak-RAM/Disk, IO-Fehlerverhalten, Cancel-Latenz, Crash-Reste und native Grenzen für Länge/NUL/UTF-16. Ein Plattformfehler darf als Fehler enden; keine gekürzte oder normalisierte Erfolgsmeldung. Keine Aussage, dass ein Betriebssystem beliebig große Strings annehmen könnte. A15/A16/A19–A22 verlangen diese Nachweise ausdrücklich.

## 6. Schwerpunkt C — Properties und Getter

Die sieben konkreten Declaration-Typen besitzen die angegebenen Rootzahlen **6/5/5/7/14/6/6**; zusätzlich ist die exakte Basisklasse mit fünf Properties beschrieben. Geerbtes Text, Overrides, verdeckte Slots, Getter-Zugänglichkeit und Indexer sind Bestandteil des Inventars. Ein unbekannter Runtime-Untertyp wird nicht durch den Basisklasseneintrag zur Werteauswertung zugelassen.

Die .NET-10-Metadatenprobe bestätigt string Length/Chars, alle sieben aufgeführten Bytearray-Properties, List Capacity/Count/Item, ReadOnlyCollection Count/Item, Dictionary einschließlich **Capacity**, ReadOnlyDictionary, die vier Keys-/Values-Collection-Typen sowie KeyValuePair. Die beiden benannten Standard-String-Comparer und die aufgeführten primitiven Typen haben keine öffentlichen Instanz-Properties. `{}` für den bekannten stateless Comparer ist ausdrücklich eine öffentliche Memberprojektion, keine Rekonstruktionsgarantie und kein Muster für unbekannte Objekte.

Inventarvollständigkeit ist nicht Auswertungsfreigabe: Collection-Einträge, Metadaten und komplexer Copy-Wert sind unterschiedliche Sichten. Fremde IEnumerable-/IList-/IDictionary-Unterbauten bleiben unsupported. Bei Core-erzeugten ReadOnly-Collections ist die geprüfte Konstruktorherkunft Teil der Zulassung. Keine beliebige Enumeration oder PropertyInfo.GetValue-Ausführung ist autorisiert.

| Kritischer Typ | Ergebnis |
|---|---|
| PARAM | [Mechanism/GetParameters:103][param] lesen bekannte Felder; PrintOutput/BuildValue verändern dagegen Zustand. F2 schließt letztere aus und gibt kein lebendes Dictionary an die UI. |
| SecretShare | [Value:17][sharevalue] klont vollständig. Der geplante CopyValueRange ist deshalb notwendig und klar als noch nicht vorhanden bezeichnet. Der bekannte SecretSet-Konstruktor erzwingt Share-Länge = Descriptor.KeySizeBits/8; Range-Grenzen müssen geprüft werden. |
| TR31H | Die versiegelte Definition kopiert/validiert ihren Unterbau und begrenzt Optional Blocks. Value verwendet den bekannten Formatter, der bei Optional Blocks Newtonsoft aufruft. Der Standardkonfigurationsvorbehalt verhindert fremde DefaultSettings-/Converter-Ausführung; andere Member bleiben zugänglich. |
| KCV/KCVC/KCVK | [KeyCheckValueCalculator:13][kcv] liest transitiv Value und dekodiert Material. Exakter Runtime-/Getter-Audit und begrenzte Worker-Dekodierung sind erforderlich. F2 verlangt Äquivalenz einschließlich null und Kleinschreibung. Die Formatfälle umfassen auch NORMAL_STRING über [FormatConversions:36][formats], nicht nur Hex/Base64; dies ist bei A08 mit zu prüfen. |
| Unbekannte Overrides | Sichtbares Metadateninventar ohne Auswertung ist konsistent. Dies muss auch über transitive Getter und den Host-Vorlauf gelten, nicht nur im Projector selbst. |

Keine zusätzliche fehlende Baseline-Property oder freigegebene unkontrollierte Getterfamilie festgestellt. Die Implementierung muss die Matrix mit tatsächlicher Reflection-Metadatenmenge vergleichen; die jetzige Quellprüfung ersetzt diesen späteren Test nicht.

## 7. Schwerpunkt D — Snapshot, GUI und Docking

[WorkbenchBindable][bindable] und [EditorCoordinator.Observe:347][observe] bestätigen den von R3 beanstandeten Observer-Kontext. F2s Dispatcher-Übergabe beseitigt ihn. Der Callback darf insbesondere kein synchrones RunState und kein Warten auf Capture enthalten. ApplyVariables setzt erst SelectedVariable=null und veröffentlicht danach neue Draft-Stempel; ein zwischenzeitlich ausgewähltes Objekt darf deshalb noch kein gültiges Handle erhalten.

[VariableViewModel:91][vm] trennt Original-Identifier/Origin von editierbaren Zeilenwerten. [InvalidateDraft:145][vm-invalidate] und [ApplyVariables:159][vm-publish] stützen F2s getrennte Regeln: reine Invalidierung behält historische Werte; tatsächliche Null-Auswahl verwirft sie. [BeforeDocumentChange:165][editor-change] erhöht WorkspaceGeneration bereits beim normalen Tippen. F2 verwendet diesen Zähler daher zu Recht nicht als Reset-Beweis. [ClearAdmittedState:124][clear] ist dagegen ein fachlicher Clear-Pfad für den semantischen Hook.

Historische vollständige Daten dürfen ohne Origin weiter kopiert werden. Fehlende historische Teile können nach einer Revision nicht nachgeladen werden. Das ist eine unvermeidbare zeitliche Grenze und keine neue Größenbeschränkung. Reopen nach echtem Tool-Close ist eine neue Aufnahme mit erneuter Admission; visuelles Detach beim Floating ist kein Tool-Close.

Der erhaltene Dockvertrag passt zum [vorhandenen Zwei-Spalten-Baum:42][dock-default], zum [Dock-Gate:99][dock-gate] und zur [atomaren Persistenz:67][dock-store]. Default Variables/Info/Inspector rechts, native Splits/Floating, Normalbaum bei Maximize und Reset auf den neuen Default widersprechen den neuen Snapshot-Regeln nicht. Schema 1 prüft weiter sechs Panes, Schema 2 sieben; vorhandenes ungültiges v2 fällt nicht still auf v1 zurück. Sieben Floating Hosts sind eine geplante v2-Erweiterung, keine Behauptung über die Baseline. Kein neuer Widerspruch oder Regression im begrenzten R4-Scope gefunden. Native Maus-/Fokus-/Accessibility-Nachweise bleiben A33–A38.

## 8. Schwerpunkt E — Alle 38 Abnahmekriterien

Die folgenden Gruppen decken A01–A38 lückenlos ab. „Konsistent“ bedeutet prüfbarer Vertrag, nicht bestandener Implementierungstest.

| Kriterien | Bewertung / besondere Abnahmegrenze |
|---|---|
| A01–A04 | Konsistent: Runtime-Inventar, geerbte/verdeckt/überschrieben, Indexer und fremde Unterbauten. Keine pauschale Getter-Freigabe. |
| A05–A08 | Konsistent: Read-only, PARAM, TR31H-Konfiguration, ausdrücklich angeforderte KCVs. A08 muss alle bestehenden Materialformate einschließlich NORMAL_STRING berücksichtigen. |
| **A09** | **Korrekturbedarf R4-L01:** Einer der normativen erwarteten Strings widerspricht dem tatsächlichen Scriptzustand. |
| A10–A15 | Konsistent: Codeeinheiten, Byte-/Integergrenzen, echte Datenkeys, Alias/Zyklus, Historie und native Clipboardfehler. Keine Garantie für nicht repräsentierbare native Textwerte. |
| A16–A18 | Konsistent: vollständiger großer Parent, kein funktionales Root-Limit, iterativer Zugriff, neuer Share-Range-Accessor. Die JS-Shares-Prüfung erfüllt diese nativen Kriterien noch nicht. |
| A19–A22 | Konsistent: Spill, Fehler, Peak, Latenz, Cancellation und Cleanup. Native Clipboard-Zeit wird getrennt gemessen und nicht aus der Capture-Messung verschwiegen. |
| A23–A24 | Konsistent: echter Observer-/Dispatcher-Wechsel und Verwerfen überholter Requests; keine Busy-Retry-Schleife. |
| A25–A27 | Konsistent, implementierungsseitig besonders kritisch: Admission-Fehler, Ressourcenfinalisierung, versteckte Recovery-Reprojektion, öffentliche IDs/Quoten, UI-/Core-Grenzen. |
| A28–A29 | Konsistent: Assignment/Mutation/Publikation und konkurrierender MCP-/GUI-Zugriff; Gate bis zum wirklichen Drain halten. |
| A30–A32 | Konsistent: Historie bei gewöhnlicher Invalidierung; Clear bei semantischem Reset/null; Layoutoperationen ohne neue Aufnahme. |
| A33–A38 | Konsistent mit beibehaltenem Dock-/Migrationsvertrag. Vollständig native Abnahme erforderlich; HTML-Layout ist kein Beleg für Dock.Admission oder atomare Migration. |

Der Corrections Report und validation.md grenzen ihre 38 bestandenen Artefaktchecks zutreffend von diesen 38 zukünftigen Kriterien ab. Die erneut grünen Checks ändern den Golden-Herkunftsbefund nicht. HTML und Markdown sind derzeit untereinander konsistent, an dieser einen Stelle aber gemeinsam inkonsistent mit C#.

## 9. Verbleibende Implementierungsrisiken und Freigabeempfehlung

| Risiko | Bewertung / vorhandene Begrenzung |
|---|---|
| Host-Zweckverzweigungen | Höchste Integrationspriorität: Recovery ebenso wie Finalize/Retention/Status ausnehmen; Admission und Drain gemeinsam behalten. Mit vorhandenen Strukturen lösbar, kein Beleg für nötigen Host-Neuentwurf. |
| Gleichzeitigkeit und Publikation | Dispatcher-Verzögerung, A→B→A, Dispose und Cleanupfehler gezielt injizieren. Kein Erfolg aus bloß beendetem Worker ableiten. |
| Große Daten und Clipboard | Technisch aufwendiger als das HTML-Modell. Endallokation und OS-Kopien können den Cache deutlich überschreiten; vollständig messen und Fehler ehrlich melden. |
| Temp-Payload und Crash | Lokale Ownership/Leases sind ausreichend als Architektur; Löschung nach Drain, IO-Fehler und erkennbare Crash-Reste bleiben nachzuweisen. Keine globale Snapshot-Verwaltung ergänzen. |
| Range-/KCV-Äquivalenz | Bounds, volle Coverage, NORMAL_STRING/Hex/Base64 und Null-Semantik gegen bestehende Getter prüfen. Optimierung darf die fachlichen Bytes/Strings nicht verändern. |
| BCL-/Formatterentwicklung | Neue Metadaten sichtbar, Werte erst nach Audit; bekannte Wrapperherkunft und Newtonsoft-Konfigurationsgrenze beibehalten. |
| Revision und native Dockintegration | Revisionsschutz setzt kooperierende Produktmutation voraus. Dock-/Layout-/Accessibility-Fälle müssen nativ geprüft werden. |

**Minimal erforderliche Architekturkorrekturen:** Keine Änderung an Host-Grundmodell, Copy-Wertformat, Accessor-Grenzen, Ereignismatrix oder Dockmigration erforderlich. **Minimal erforderliche Paketkorrektur:** R4-L01 an Fixture/Golden/HTML und Nachweisen schließen. Die genannten Host-Details konkretisieren vorhandene Invarianten und sind in der Implementierungsabnahme nachzuweisen.

**Empfehlung zur Eigentümerabnahme:** Der R4-Bericht ist bereit zur Owner-Prüfung. Das unveränderte F2-Paket sollte wegen des falschen normativen Golden noch nicht als abschließend ACCEPTED bestätigt werden. Nach der eng begrenzten Korrektur und unabhängigen Kontrolle des betroffenen Golden ist die Architektur aus diesem R4-Scope zur Eigentümerabnahme geeignet. Eine erneute breite Architekturiteration ist durch die Befunde nicht begründet.

**G2-04-02:** Erst nach ausdrücklicher Owner-Freigabe. R4 startet keine Implementierung. Produktcode, Tests, F2 und master blieben unverändert; keine Commits, Pushes oder Merges.

[fixture]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F2/prototype-model.js:8
[goldens]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F2/copy-golden-values.json:9
[build]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F2/build-artifacts.cjs:20
[lexer-ws]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Antlr/CryptoLexer.g4:177
[statement-map]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/AntlrToStatement.cs:14
[statement-eval]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/StatementEvaluator.cs:45
[statement-test]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScriptUnitTest/StatementExecutionTests.cs:43
[run-gui]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:391
[accept]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:264
[intent]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:553
[host]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs:45
[core]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/InterpreterExecution.cs:87
[run-owned]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:429
[recovery]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:685
[finalize]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:583
[public-status]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:126
[release]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:503
[acquire]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs:247
[epoch-close]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs:51
[secrets]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:6
[sharevalue]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:17
[header]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderDefinition.cs:28
[param]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/ParameterVariableDeclaration.cs:103
[kcv]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/KeyCheckValueCalculator.cs:13
[formats]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/FormatConversions.cs:36
[bindable]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/ViewModelBase.cs:13
[observe]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:347
[vm]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:91
[vm-invalidate]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:145
[vm-publish]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:159
[editor-change]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:165
[clear]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:124
[dock-default]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:42
[dock-gate]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs:99
[dock-store]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:67
