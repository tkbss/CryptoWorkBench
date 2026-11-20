# G2-04-01-F2 — Konsolidierter Architekturvertrag

**Status: READY FOR OWNER REVIEW — nicht ACCEPTED**  
**Datum:** 17.11.2026 · **Repository:** tkbss/CryptoWorkBench  
**Baseline:** `32017a08c4771e73c12a50f6e1548476b9bb0a54`

F2 ersetzt F1 als vorgeschlagenen Architekturvertrag. Verbindliche Anhänge dieser Fassung sind die Accessor-Matrix, der Copy-Vertrag mit Golden Values und die Implementierungs-Abnahme im selben Paket. Die vorhandenen C#-Klassen werden als Baseline beschrieben; alle neuen APIs und Verhalten sind **geplante Implementierung**, nicht bereits verfügbar. R4 soll die sechs R3-Korrekturen begrenzt nachprüfen. G2-04-02 beginnt nicht automatisch.

## 1. Produktvertrag und Baseline

CryptoWorkBench ist eine kryptografische Entwicklungs- und Analyseumgebung für einen einzelnen Desktop-Benutzer. Schlüssel, Shares, Zwischenwerte, Text und Objektattribute sind vollständig sichtbar und kopierbar. Keine Maskierung, Reveal-Bedienung, Autorisierung, Passwortprüfung oder entsprechende Erweiterungsschnittstelle. Host-Admission verhindert konkurrierende Interpreterzugriffe; sie ist keine Zugriffsberechtigung auf Schlüsselmaterial.

Der Inspector verändert keine Variablen, PARAMs, History, Print-Ausgabe oder Interpreterrevision. Er liest keine noch nicht übernommenen GUI-Drafts als Interpreterwerte. Die bestehende CryptoScript-Print-Semantik bleibt unverändert. `SecretVariableDeclaration.Value` bleibt der tatsächliche String `SECRET`; die Shares sind unter `SecretSet` zugänglich. Der Inspector rekonstruiert keine Schlüssel durch `Restore`.

Vor Beginn wurden `HEAD == master == Baseline`, ein sauberer Worktree und die vorhandenen F1/R3-Artefakte verifiziert. Architektur-, Design- und Validierungsdateien liegen ausschließlich außerhalb des Repository-Worktrees. Kein Produktcode oder Produkttest wird in F2 geändert.

## 2. Kleine Zielarchitektur

| Baustein | Verantwortlichkeit |
|---|---|
| Bestehendes `VariableViewModel` | Auswahl, private Original-Identifier-/Publikations-/Draft-Zuordnungen. Schmaler interner Read-Zugriff auf diese Daten; kein zusätzliches `AttachView`. |
| Neues `VariableInspectorViewModel`, einmal je Shell | Auswahlabsicht, lokaler Request-Zähler, unveränderliche aufgenommene Teile, Lade-/Copy-Kommandos und UI-Lebensdauer. |
| Neuer zustandsloser `InspectorProjector` | Metadatenkatalog, Accessor-Matrix, typisierte Reads, kanonische Wertdarstellung. Keine beliebige Reflection-Wertauswertung. |
| Bestehender `WorkbenchAutomationService` | Read-Admission, Identität/Revisionsprüfung und serviceeigene Lebensdauer der Read-Operation. Verwendet bestehenden `EditorCoordinator`, `InterpreterHostQueue` und dessen asynchronen Core-Scope. |
| Neue `DockVariableInspectorView` | Ein reparentbares Read-only-Control; virtualisierte Properties, Detailansicht, rechts Copy. |
| Lokale Datenträger | `SelectionHandle`, `CaptureStamp`, `ReadSpec`, immutable `SnapshotPart`, `InspectorNode`; bei großen Werten ein snapshotlokaler Payload-Handle. Kein globales Objekt-/Revisionsregister. |

Interne Host-Methoden genügen; `IVariableInspectorReader` ist optional für die Austauschbarkeit in Tests. Es gibt keinen eigenen Copy-Service, Scheduler, Interpreter, Workspace-Manager oder globalen Snapshot-Manager. Snapshotlokale Payload-Segmente und temporäre Dateien gehören zum ausgewählten VM/Snapshot und sind keine zweite autoritative Variablenhaltung.

**Notwendige spätere Host-Erweiterung:** Ein interner Zweck `InspectorRead` im vorhandenen serviceeigenen Arbeitslebenszyklus. Dies ist keine neue Queue. Große Reads dürfen nicht einfach per `Task.Run` auf ein UI-geprüftes Handle losgelassen werden. Der genaue Ablauf steht in Abschnitt 6.

## 3. Inventar, Werte und Zugänglichkeit

Für **jeden erreichten unterstützten Runtime-Typ** werden alle öffentlichen Instanz-Properties einschließlich geerbter Properties inventarisiert. Reflection liest ausschließlich Metadaten, ohne `PropertyInfo.GetValue`, Attributinstanziierung oder Objekterzeugung. Überschriebene Accessor-Slots erscheinen einmal. Unabhängig mit `new` verdeckte Properties bleiben anhand deklarierendem Typ und Signatur unterscheidbar. Statische Properties, Felder und Methoden sind kein Property-Inventar.

Property-Reihenfolge im Baum: Basisklasse vor Unterklasse, innerhalb einer Ebene ordinal nach Name und Signatur. Copy-JSON besitzt unabhängig davon die in der Copy-Spezifikation definierte kanonische Reihenfolge.

Die sieben Deklarationsklassen besitzen nach Override-Deduplikation 6/5/5/7/14/6/6 Root-Properties für String/Path/Val/PARAM/KEY/SECRET/TR31H. Geerbtes `Text` gehört dazu. Diese Zahlen ersetzen nicht das Inventar der erreichten Unterobjekte.

### Drei gleichberechtigte Ansichten eines Knotens

1. **Wertansicht:** String, Bytes, Zahl oder fachliches Objekt; Grundlage von Wert-Copy.
2. **C#-Properties:** vollständiges öffentliches Inventar, auch `List.Capacity`, `Count`, String.Length und Array.SyncRoot. Dadurch wird kein eigener CLR-Wert mit diesen Metadaten erfunden.
3. **Einträge/Adapter:** Collection-Elemente, Dictionary-Key/Value-Paare, String-Chars/Bytes sowie ausdrücklich benanntes `PARAM.GetParameters()` als Methodenadapter. Einträge sind nicht als erfundene Properties auszugeben.

Skalaransicht darf standardmäßig kompakt sein; ihre C#-Properties bleiben über „Properties“ erreichbar. Bekannte Collections zeigen Metadaten und Elemente getrennt. `Nullable<T>` wird gemäß tatsächlichem Boxing behandelt: null ist null, ein vorhandener Wert besitzt Runtime-Typ T. Keine erfundenen `HasValue`-/`Value`-Properties auf einem geboxten int.

Bekannte Indexer werden ausschließlich mit geprüften Argumenten des dokumentierten Adapters aufgerufen: Listen/ReadOnlyCollection über gültige Indizes, Dictionary über vorhandene String-Keys, String.Chars über UTF-16-Indizes. Arrays haben keine reflektierte öffentliche `Item`-Property; ihre Elemente sind ein Adapter. Freie Eingabe eines beliebigen Indexers ist nicht Bestandteil von G2-04. Unbekannte Indexer bleiben mit Signatur und `RequiresArguments/UnsupportedAccessor` sichtbar.

Unbekannte Runtime-Typen, unbekannte Overrides und nicht registrierte BCL-Unterklassen erhalten ein vollständiges Metadateninventar, aber keine automatische Getter-Ausführung. Kein Basisklassen-Allowlist-Eintrag ermächtigt einen unbekannten Override oder dessen transitive Zugriffe. `WriteOnly`, `GetterNotPublic` und `UnsupportedAccessor` bleiben sichtbar. Vollständige Werte werden nur für die in der Matrix konkret unterstützten Typen zugesagt, nicht für beliebigen ausführbaren Fremdcode.

## 4. Copy-Vertrag

Die Datei **G2-04-01-F2-copy-contract.md** und `copy-golden-values.json` sind normativ. Der rechte Copy-Button verwendet ausschließlich den vollständigen Wert des gewählten Knotens. UI-Vorschau, ausgewählter Text, Revision, Status, Runtime-Typ und Referenzpfade ändern diesen Payload nicht.

Strings werden exakt als vorhandene UTF-16-Zeichenfolge kopiert: kein Trim, keine Newline-/Unicode-Normalisierung, keine neue Schlusszeile und keine Großschreibung eines stringförmigen Hex-Werts. Bytearrays sind davon verschieden: Sie werden als `0x(UPPERCASEHEX)` vollständig und reversibel formatiert. Zahlen invariant; bool `true/false`; null `null`; leerer String leer. Komplexe fachliche Werte verwenden ausschließlich tatsächliche Membernamen und Werte; Listen sind JSON-Arrays, String-Dictionaries JSON-Objekte. Keine `type`-/`members`-/`status`-/`$ref`-/Revision-Hüllen.

Eine Referenz auf bereits gesehenes Objekt wird im Baum als Navigation dargestellt. Wert-Copy expandiert denselben detached Wert erneut. Nur ein echter Vorfahrenzyklus im fachlichen Copy-Graph ergibt `CopyUnavailable(Cycle)`; kein Pfadstring als Ersatz. Array.SyncRoot kann zum Array selbst navigieren, verhindert aber nicht dessen Byte-Copy, weil Collection-Metadaten nicht Teil des Collection-Wertformats sind. Ein unbekanntes Objekt ohne vereinbartes Wertformat wird nicht als leeres Objekt oder `ToString()` ausgegeben.

„Vollständig laden & kopieren“ fordert die noch fehlenden bekannten Werte an. Enthält der gewählte fachliche Graph berechnete Properties, heißt die Aktion **„Laden, berechnen & kopieren“** und benennt die Berechnungen vor dem Klick. Dieser Klick ist die explizite Berechnungsanforderung, keine Autorisierung. Ohne gültigen Token können fehlende historische Teile nicht nachgeladen werden. Fehler, nicht unterstützte Getter oder Zyklen verhindern ausschließlich das betroffene vollständige Parent-Copy; vorhandene vollständige Kinder bleiben kopierbar. Ein tatsächlicher null-Getterwert, auch KCV-NotApplicable, bleibt JSON-null.

Copy bindet vor dem Clipboard-Aufruf einen festen Payload und Herkunftstext. Herkunft/Revision erscheinen im UI und in der Rückmeldung, niemals im Payload. Ein Auswahlwechsel nach Übergabe an das OS tauscht den Copy-Inhalt nicht aus. Kein automatisches Copy bei Auswahl, Refresh oder Docking. Ctrl+C kopiert nach Desktop-Konvention markierten Text; es ist nicht identisch mit dem Vollwert-Button.

## 5. Bedarfsgerechte Aufnahme und große Werte

### 5.1 Snapshot als unveränderliche Teile eines einzigen Tokens

Ein `CaptureStamp` enthält den ursprünglichen Identifier, schwachen Origin, VM-Publikation, Workspace-/Draft-Stempel, die bei der Aufnahme festgestellte prozessweite StateRevision und die ausgewählte veröffentlichte Zeilenidentität. Die wechselnde lokale Request-Nummer gehört **zur Operation, nicht zur Gleichheit des CaptureStamp**; mehrere erfolgreiche Teilaufnahmen derselben Auswahl können so zusammengeführt werden. Die UI hält keine lebende Declaration oder Collection. Metadaten des Roots können früh aufgenommen werden; Werte beginnen mit `NotLoaded`. Ein Snapshot ist keine Behauptung, der gesamte Graph sei schon aufgenommen.

Jeder erfolgreiche `SnapshotPart` trägt denselben Stamp und einen eindeutigen Property-/Elementpfad. Das VM ersetzt seine immutable Map von Teilen; bereits aufgenommene Werte werden nicht in-place überschrieben. Ergänzung ist nur bei exakt gleichem Stamp erlaubt. Der ursprüngliche Aufnahmezeitpunkt bleibt, zusätzlich kann die UI den Zeitpunkt der letzten Ergänzung zeigen. Ein später beobachteter Revisionszähler darf nicht auf alte Werte umetikettiert werden.

Ein Pfad besteht intern aus typisierten Segmenten `Property(AccessorSlot)`, `Index(int)` oder `Key(string)`, nicht aus erneut geparsten Anzeigezeichenfolgen. Dictionary-Keys mit Punkten/Klammern bleiben eindeutig. Range-Teile tragen Offset, Anzahl und bei R erfasste Gesamtlänge. `Complete` verlangt lückenlose Abdeckung von 0 bis Gesamtlänge und alle fachlichen Schemamember; gleiche Länge allein genügt nicht. Überlappende widersprüchliche Teile werden verworfen, nicht nach Last-Write-Wins gemischt. Jede neue Teilaufnahme löst den ursprünglichen Pfad unter gültigem Root-/State-Token erneut auf.

Vor **jeder** ergänzenden Live-Aufnahme werden Publikation/Workspace/StateRevision und Root-Identität erneut geprüft. Assignment unter gleichem Namen, In-place-Mutation, ungültige Publikation oder verschwundener Weak-Origin ergibt `Stale`, ohne Wechsel zum gleichnamigen Nachfolger. Ein Reference-Pfad ist ein snapshotlokaler Navigationspfad, keine Befugnis für spätere Reads.

### 5.2 Bedienung und Aufnahme sind getrennt

Die UI zeigt standardmäßig 100 Collection-Einträge pro Seite und höchstens 160 Vorschauzeichen. Ein UI-Seitenwechsel auf vorhandene Daten liest nur den Snapshot. Fehlen Daten, fordert „Seite laden“ die konkrete Range an. Expandieren erzeugt keine unkontrollierte rekursive Komplettaufnahme. Für Objekt-Member, Stringbereiche, Bytebereiche und Collection-Ranges sind `ReadSpec`-Varianten festgelegt. Eine explizite Vollwert-/Copy-Aktion fordert sämtliche benötigten Teile an, unabhängig von der sichtbaren Seite.

**Kein Root-Hardlimit von 16 MiB, Tiefe 16 oder 10.000 Nodes mehr.** Diese bisherigen Werte sind keine Produktgrenzen. Ein Tiefenfenster ist ausschließlich ein Renderfenster: „Weiter öffnen“ erreicht den nächsten Abschnitt. Iterative Traversierung und Vorfahrenidentität verhindern Stackoverflow/Zyklen. Verzweigungen werden nach Bedarf erzeugt; eine volle Copy-Traversierung arbeitet iterativ bis zum Ende des endlichen unterstützten Graphen.

### 5.3 Konkrete Speicher- und Arbeitsbudgets

| Ressource | Vertrag |
|---|---|
| Sichtbare Einträge | 100 pro UI-Seite, virtualisiert; unabhängig von Capture-/Copy-Länge. |
| UI-Aufwand | Ziel ≤8 ms je Dispatcher-Arbeitsschritt; Render-/Publikationsblöcke zerlegen. Keine große Clone-/Formatierungsarbeit auf UI. Messziel, keine Behauptung eines präemptiven Zeitlimits. |
| Core-Read-Schritt | Höchstens 256 Einträge oder 256 KiB Rohdaten pro kooperativem Schritt; Ziel ≤10 ms. AccessState immer vor await verlassen. Metadaten/O(1)-Getter separat. |
| Gepufferte Payload-Daten | Ziel 32 MiB pro aktiver Inspector-Snapshot; große Werte/Nodes in append-only snapshotlokale Segmente auslagern. Ein einzelner bereits immutable String darf als Referenz gehalten werden; seine Länge zählt, erzeugt aber keine erzwungene Kopie. |
| Zusätzlicher Arbeitsbuffer | Ziel ≤2 MiB pro Aufnahme/Formatierung neben Cache, bestehenden Werten und abschließender Clipboard-Allokation; Buffer wiederverwenden. |
| Vollwertspeicher | Temporärer, auf diesen Snapshot begrenzter Payload-Store im OS-Temp-Verzeichnis. 1-MiB-Segmente, vollständige Indizes/Metadaten getrennt von Copy-Inhalt. Kein fachliches Größenlimit und keine automatische Löschung historischer vollständiger Werte durch Cache-Eviction. |
| Freier Speicher | Vor Wachstum prüfen: mindestens 512 MiB freier Diskreserve angestrebt. Unterschreitung/Quota/IO-Fehler ergibt `ResourceUnavailable`, keine verkürzte erfolgreiche Aufnahme. Arbeitsort/Reserve konfigurierbar als technische Einstellung; keine Geheimhaltungsoption. |
| Laufzeit | Nach 5 s sichtbarer Fortschritt mit Abbrechen. Kein starres Gesamtzeitlimit, das legitime große Objekte dauerhaft unzugänglich macht. Abbruchprüfung mindestens pro Schritt/IO-Block. Kein Timeout zum vermeintlichen Abbrechen unbekannter Getter. |
| Clipboard | Am Ende muss die Plattform gegebenenfalls einen vollständigen zusammenhängenden String annehmen. Diese Allokation ist vom 32-MiB-Cachebudget ausgenommen, vorab mit Länge/Peak-Schätzung geprüft und separat gemessen. Bei OS-/RAM-Grenze kein falscher Erfolg. |

„32 MiB Cache“ ist ein Arbeitsbudget, keine maximal unterstützte Wertgröße. Rohbytes werden erst für die angeforderte Ausgabe in Hex umgewandelt. Strings werden für den temporären Store verlustfrei als UTF-16-Codeeinheiten abgelegt; keine Newline-/Unicode-Konvertierung. JSON ist eine Ausgabe des Projectors, nicht das interne Snapshot-Format. Der Payload-Store ist eine lokale Ressource des Snapshots, kein globaler Dienst. Nur eigene, eindeutig benannte Dateien dürfen gelöscht werden. Dispose/Reset/Close schließen Handles und löschen eigene temporäre Dateien; Abbruch räumt unvollständige Staging-Daten auf. Dateifehler werden sichtbar, ohne bereits vollständige Werte als erfolgreich neu aufgenommen auszugeben. Verwaiste eigene Temp-Dateien nach Prozessabbruch sind ein dokumentiertes Betriebsrisiko, keine Layoutdaten.

Formatierungsblöcke sind nötigenfalls kleiner als ein Core-Read-Schritt, etwa 64 KiB Rohbytes für Hex oder 16 Ki UTF-16-Codeeinheiten für JSON-Escaping, damit Eingabe-, Ausgabe- und IO-Buffer zusammen das Arbeitsbudget einhalten. Eine aktive detached Copy hält einen lokalen Payload-Lease bis zur Übergabe; Snapshot-Freigabe löscht keine noch gelesene eigene Datei. Nach dem letzten Lease werden Ressourcen freigegeben. Bereits voll erfasste Inhalte dürfen aus RAM verdrängt, aber nicht still aus dem historischen Snapshot gelöscht werden.

Das OS-Clipboard bietet keinen universellen Streaming-Textvertrag. Vollständiges Copy großer Werte wird deshalb nicht durch „Datei exportieren“ ersetzt. Soweit das Clipboard den Wert physisch aufnehmen kann, wird exakt der ganze Wert übergeben; andernfalls erscheint ein technischer Copy-Fehler und der vollständige Snapshot bleibt erreichbar. Embedded-NUL/ungültige UTF-16-Folgen und native Längenlimits müssen in der Plattformabnahme explizit geprüft werden; kein stilles Normalisieren zur Erzeugung eines vermeintlichen Erfolgs.

### 5.4 Bekannte teure Getter

`SecretShare.Value` klont heute das gesamte Bytearray. Für eine Range-Aufnahme ist daher in G2-04-02 eine **kleine typisierte Core-Ergänzung** vorgesehen: `SecretShare.CopyValueRange(int offset, Span<byte> destination)`, mit Bounds-Prüfung, ausschließlich lesender Kopie aus den vorhandenen privaten Bytes. Keine neue Property, kein globaler Zustand, kein Schlüssel-Restore. Der Adapter liefert damit exakt dieselben Bytes wie der Getter, ohne ihn pro Seite vollständig aufzurufen. Die vollständige `Value`-Zeile bleibt eine Bytearray-Property und behält deren Metadaten; Range und Vollwert müssen gegen den tatsächlichen Getter auf kleinen Fixtures übereinstimmen.

`Tr31HeaderDefinition.OptionalBlocks` wird für die bekannte validierte versiegelte Definition einmal je Read-Operation kopiert; die Core-Klasse begrenzt diese auf 99 Blöcke und den Wire-Header. Weitere Bereiche werden aus dem detached Ergebnis gelesen. `KEY.KeyAttributes` und andere mutable Listen dagegen werden unter Admission direkt über geprüfte Indizes segmentiert.

KCV wird nur ausdrücklich angefordert. Vor dem eigentlichen Algorithmus wird auf einem Worker die effektive Materiallänge ohne unbegrenzte Decode-Allokation geprüft. Für bekannte AES-/TDEA-Formate wird höchstens das benötigte 16/24/32-Byte-Material dekodiert; ungültiges/zu langes Material hat denselben null-Wert wie der bestehende Calculator. Base64-Whitespace wird bei der Längenprüfung berücksichtigt. Eine kleine rein typisierte Analysehilfe kann den bestehenden Calculator auf einer privaten normalisierten Materialkopie verwenden; sie darf das Original-Value nicht normalisieren. Äquivalenz zum bestehenden Getter, einschließlich aktueller Kleinschreibung seiner Hex-Stringausgabe, ist Abnahmepflicht. Kein Blockieren der UI durch einen riesigen String.

`TR31H.Value` darf nur für den exakten bekannten Pfad ausgewertet werden. Sein `JsonConvert.SerializeObject(string)` besitzt eine globale Konfigurationsabhängigkeit: Im Baseline-Produkt ist `JsonConvert.DefaultSettings` nicht gesetzt. F2 registriert diesen Getter nur bei der auditierten Standardkonfiguration; bei fremden DefaultSettings/Convertern bleibt er mit `UnsupportedFormatter` sichtbar, statt unbekannten Code auszuführen. Keine Änderung globaler Serializer-Einstellungen zur Umgehung dieser Grenze. Definition/Member bleiben einzeln zugänglich. Ein späterer isolierter Core-Formatter wäre eine gesondert auf Semantikgleichheit geprüfte Härtung, keine Voraussetzung für fremde Konfigurationen in G2-04.

## 6. Observer → Dispatcher → vorhandener Host

### 6.1 Auswahl-Scheduling

`SelectedVariable.PropertyChanged` liegt über `WorkbenchBindable` bereits in `EditorCoordinator.Observe`. Der Handler darf nur die neue Auswahlreferenz, den lokalen Request-Zähler und gewünschte Aktion erfassen, eine bestehende eigene Operation zur Cancellation markieren und höchstens **einen** `Dispatcher.UIThread.Post` vormerken. Kein Getter, Dictionary-Zugriff, `TryBegin`, `RunState`, await/Wait oder UI-Nested-Loop im Observer.

Der nachgelagerte Callback liest die jüngste Absicht. Passt sein Request nicht mehr, ist die View geschlossen/disponiert oder die Auswahl null, beendet er sich. Erst jetzt liest er den schmalen internen Original-Identifier-/Publikationsvertrag des Variables-VM. Unvollständig laufendes `ApplyVariables` darf kein Handle liefern. Nach Ende der Publication muss der aktuelle Stempel erneut geprüft werden.

Nur rasche Auswahlabsichten vor demselben Dispatcher-Callback werden zusammengefasst. Bei Busy wird kein Timer, Polling oder unbegrenzter Retry gestartet. Nach Abschluss der Cancellation einer **eigenen** älteren Aufnahme darf genau der bereits vorgemerkte jüngste Benutzerwunsch einmal auf dem Dispatcher ausgeführt werden; das ist kein Wiederholen fremd abgewiesener Hostzugriffe. Ist der Host dann anderweitig Busy, bleibt „Erneut laden“ als Benutzeraktion.

### 6.2 Zulässige Read-Admission und lange Arbeit

Der derzeitige `RunState`/`ValidateStateAccess`-Pfad ist UI-affin und synchron. F2 behauptet nicht, er könne unverändert große Worker-Reads leisten. Die spätere Methode `RunInspectorReadAsync(handle, spec, cancellation)` wird im **bestehenden Automation-Service** wie folgt integriert:

1. **UI-Vorprüfung:** Kein Observer, registrierter ursprünglicher Editor, GUI-Phase, kein Stop/Epoch-Close/Drain/konkurrierender Job. Aktuelle Auswahl, Publikation und Workspace prüfen. Request-Identität festhalten.
2. **Atomare Annahme:** Unter vorhandener Service-Synchronisation die nichtwartende `InterpreterHostQueue.Reserve()` und ein vorhandenes `EditorCoordinator.ExecutionIntent` erwerben; bei irgendeinem Fehlschritt alle bereits erworbenen Ressourcen freigeben. Der bestehende `Accept`-Lebenszyklus prüft die UI-Intention unmittelbar vor Veröffentlichung erneut. Das Read wird vor Workerstart als serviceeigene aktive Arbeit sichtbar.
3. **Read-Zweck im bestehenden Job-Datenträger:** Interner Zweck `InspectorRead`, keine öffentliche MCP-Execution-API und kein Scripttext. Bestehende Cancellation, Stop-/Epoch-Drain und Resource-Finalization wiederverwenden. Keine `ExecuteAsync`, `RecordUnit`, `CaptureMutation`, `ApplyVariables` oder Print-/History-Aufrufe. `Effects=false` bleibt invariant. Die Inspector-Ergebnisse werden nicht in `ExecutionResult.Variables` projiziert. Öffentliche Execution-Listen/Lookup und `last execution` lassen diesen internen Zweck aus. Die Execution-ID-Quota gilt nicht als kumulatives Seitenlimit. Der heutige automatische Execution-Deadline-Timer wird für diesen internen Zweck nicht gestartet; Fortschritt und kooperative Benutzer-/Lifecycle-Cancellation ersetzen das starre Gesamtzeitlimit. Andere Jobarten bleiben unverändert.
4. **Worker/Core:** `RunAdmittedAsync` stellt den bestehenden asynchronen Interpreter-Scope bereit. Innerhalb kurzer synchroner `InterpreterExecution.AccessState`-Schritte werden Root-Identity und `StateRevision` geprüft sowie geprüfte Getter/Ranges gelesen. UI-affine Prüfungen werden **nicht** vom Worker aufgerufen. Zwischen Schritten darf formatiert/geschrieben/auf IO gewartet werden; niemals await innerhalb AccessState. Die globale Host-Reservation bleibt für die ganze angeforderte Aufnahme bestehen. Live-Referenzen und Enumerator verbleiben ausschließlich im privaten Worker-Operationskontext bis zu dessen Ende.
5. **UI-Änderung während Read:** Normales Tippen/Selection/Close setzt den lokalen Request ungültig und cancelt die Operation. MCP-/weitere Interpreterzugriffe werden durch vorhandene aktive Arbeit/Intent/Reservation abgehalten. Keine lang lebende GUI-Mutationssperre und kein offenes Dokument-Update nur zum Lesen. Vor einer Veröffentlichung wird die UI-Intention erneut auf dem Dispatcher geprüft.
6. **Staging → Commit:** Eine Range oder der vollständig angeforderte Wert wird zunächst privat aufgebaut. Nach erfolgreichem Read und Finalisierung wird das Ergebnis unter Stamp-/Request-Prüfung atomar an das VM übergeben. Kein halber Parentwert als vollständig markieren. Vorher abgeschlossene Teile bleiben bei Abbruch unverändert historisch; unfertiges Staging wird verworfen. Fortschritt ist ausschließlich detached Zählerinformation.
7. **Finalisierung:** Intent auf dem UI-Dispatcher disponieren; Cancel-/IO-Arbeit drainen; Worker/Core-Scope verlassen; Reservation genau einmal freigeben. Kein Freigeben des Gates, während ein synchroner Getter noch läuft. Der Read-Zweck überspringt Variablen-Reprojektion, `AdvanceVersion`, öffentliches `last execution`, Ergebnisretention und Execution-ID-Tombstones. Temporäre Registry-Mitgliedschaft wird am Abschluss entfernt, damit viele Seitenreads keine Execution-ID-Quota verbrauchen. Service-Stop muss aktive Reads trotzdem vollständig sehen und drainen.

Das ist eine zweckgebundene Erweiterung des vorhandenen Workflows, **kein unveränderter Aufruf des heutigen `RunGuiAsync`**: Dessen PendingVariables-/Finalize-/Retention-Verhalten wäre für Inspector-Reads ungeeignet. Diese wenigen zweckabhängigen Stellen sind Teil der Implementierungsabnahme. Eine parallele Inspector-Queue oder ein separater globaler Active-Read-Owner ist nicht zulässig.

Während einer vollständigen großen Aufnahme hält die Operation die Host-Admission; GUI-Scriptausführung/MCP-Erwerb sind Busy, die UI bleibt bedienbar und Abbrechen verfügbar. Diese bewusst gewählte Konsistenzregel vermeidet Revisionsmischungen. Zwischen getrennten Benutzeraufnahmen wird das Gate freigegeben und der Stamp erneut geprüft. Dictionary-Enumeration darf innerhalb einer solchen gehaltenen Operation in Schritten fortgesetzt werden; nach Ende darf kein Live-Enumerator erhalten bleiben. Dictionary-Copy sortiert detached Keys ordinal; große Sortierung darf segmentiert extern erfolgen.

## 7. Verbindliche Lebenszyklusmatrix

Der Header lautet „Snapshot · R · Aufnahmezeit“, nicht ungeprüft „aktuell“. Jeder Knoten unterscheidet `NotLoaded`, `Loading`, vollständigen Wert/Null/Empty, `NotCalculated`, Getter-/Ressourcenfehler und Unsupported. „Historisch“ ist ein Snapshotstatus, kein anderer Wert. `WorkspaceChanged` ist eine Invalidierungsbenachrichtigung und niemals allein ein Reset-Beweis.

| Ereignis | Bereits vollständige detached Werte | Live-Handle / laufende Requests | Auswahl / nächste Aktion |
|---|---|---|---|
| Normale Editortextänderung | Behalten, konservativ historisch markieren. | Handle ungültig; eigene laufende Reads abbrechen/Ergebnisse verwerfen. | Auswahl darf stehen bleiben; Copy vorhandener Werte weiter möglich. Für weitere Reads Variables aktualisieren/erneut auswählen. |
| GUI-Ausführung beginnt | Behalten, „Ausführung; Snapshot R“ anzeigen. | Kein neuer Read; laufender eigener Read muss zuvor beendet sein oder Ausführung erhält Busy. | Keine implizite Neuaufnahme. |
| GUI-Ausführung endet ohne Publikation | Behalten; bei bekannter Änderung historisch. | StateRevision/Stamp bei nächstem Read prüfen; kein automatisches Hochstufen. | Benutzer fordert Refresh an. |
| Assignment oder In-place-Mutation | Alte Werte bleiben historisch, solange keine folgende Auswahlaufhebung erfolgt. | Identity beziehungsweise Revision macht alte Handles stale. | Kein Umhängen auf gleichnamiges neues Objekt. |
| MCP Acquire | Behalten; historisches Copy erlaubt. | Live-Reads gesperrt; Workspace-Invalidierung entwertet Handle. | Kein Löschen nur aufgrund Acquire. |
| MCP Release / Drain / ResumeGui | Behalten; Status historisch bleibt. | Keine Wiederbelebung alter Handles. Während Drain weiter Busy. | Frische Variables-Publikation und neue Auswahl für Live-Reads. |
| `InvalidateDraft` | Behalten, historisch. | Publikation/Handle ungültig; Requests abbrechen. | Bestehende ausgewählte Zeile kann als Ursprung stehen bleiben. |
| Variables-Aktualisierung / `ApplyVariables` | Bei dessen bestehendem `SelectedVariable=null` Snapshot freigeben. | Alle alten Requests verwerfen. | Leerzustand; erneute Auswahl erforderlich. Kein automatisches Namens-Reselect. |
| Explizite Auswahlaufhebung | Snapshot/Payload freigeben. | Request++ und Cancellation. | Leerzustand; keine implizite Neuaufnahme. |
| Auswahl A→B | A freigeben, B zunächst Metadaten/NotLoaded. | Request++ und Cancellation von A; Scheduling nach Abschnitt 6. | B nur mit eigener gültiger Publikation lesen. |
| Tatsächlicher Workspace-Reset / anderes fachliches Editor-Dokument | Snapshot/Payload freigeben. | Request++ und Cancellation; ursprüngliche Identität nie übertragen. | Expliziter semantischer Reset-/Kontextwechsel-Hook; neue Auswahl. |
| Fehlgeschlagener Refresh | Alten vollständig aufgenommenen Wert historisch behalten, Fehler daneben. | Fehlgeschlagenen Request beenden; keine Teilmischung. | Manueller Retry nur bei weiterhin gültigem Handle; sonst Variables-Aktualisierung. |
| Layout-Reset | Unverändert. | Keine Capture-/Revisionseffekte. | Neuer Zwei-Spalten-Default, Variables aktiv. |
| Tabwechsel, Docking, Floating, Maximize/Restore | Unverändert; dieselbe VM/View. | Keine neuen Reads; visuelles Detach ist kein Close. | Bestehende Dock-Admission gilt. |
| Semantisches Tool-Close | Snapshot/Payload freigeben; Verhalten festgelegt, nicht optional. | Request++ und Cancellation. | Reopen nimmt gültige bestehende Auswahl neu auf; bei stale Auswahl Leer-/Historiehinweis ohne Wertbehauptung. |
| Shutdown / Dispose | UI sofort beenden, Handles anschließend geordnet freigeben. | Keine neue Arbeit; bestehender Service-Drain wartet auf Worker/IO, Gate bleibt bis dahin belegt. | Abonnements genau einmal lösen; keine späte Publikation. |

**Semantischer Reset-Hook:** An tatsächlichen Aufrufern wie `ClearAdmittedState` plus zugehöriger UI-Publikation und dem Austausch des fachlichen Editor-Dokuments anbinden; nicht aus `WorkspaceGeneration++` ableiten. Bei unbekannter Ereignisursache gilt konservativ „historisch/Handle ungültig“, nicht „Reset“. Kein neuer globaler Reset-Zähler. Liefert ein nachfolgendes `ApplyVariables` null, greift ausdrücklich die separate Null-Auswahlregel. Historische Unterstützung ist keine unbegrenzte History-Liste.

Vollständige historische Teile bleiben auch ohne lebendes Origin-Objekt kopierbar. Nicht aufgenommene historische Teile können nicht rekonstruiert werden. Ein vollständiger historischer Parent ist nur kopierbar, wenn sein fachlicher Wert zuvor vollständig aufgenommen wurde. Das ist eine zeitliche Tatsachengrenze, keine Größen- oder Geheimhaltungsgrenze.

## 8. Docking und Migration — unverändert konsolidierter Vertrag

Frisch/Reset: Editor links; Variables, Info und Inspector als Tabs rechts, Variables aktiv; Output/Diagnostics unten, Output aktiv; Status wie bisher standardmäßig geschlossen. Inspector ist `tool:variable-inspector`. Dritte obere Spalte durch vorhandenen echten `SplitToDock`-Pfad, Floating durch native Hosts. Home/Dock führt in die rechte Tool-Gruppe zurück. Eine VM/View je Inspector, kein zusätzliches Editor-Attach.

Vorhandene Grenzen bleiben: GUI-Phase, keine Busy-/Executing-/Stopping-/Pending-/Observer-Operation, kein Editor-Lock/Document-Update, aktivierte Fenster; außerdem Fokusmodus und Layoutfault. Historisches Copy ist von einer verweigerten Dockmutation unabhängig. Normale Mutationen im Fokusmodus gesperrt; Restore/Reset verwenden bestehenden `ChangeLayout`-Pfad mit den übrigen Gates.

| Migrationsschritt | Verbindlicher Vertrag |
|---|---|
| Dateiauswahl | `dock-layout-v2.json` bevorzugen. Nur wenn v2 fehlt, v1 lesen. Vorhandenes ungültiges/unlesbares v2: Diagnose + Default, kein stilles v1-Fallback. `Load(v2) ?? Load(v1)` genügt nicht; fehlend/ungültig unterscheiden. |
| v1 lesen | Historisches Schema-1-Inventar mit exakt sechs Panes und historischen Strukturgrenzen verwenden. |
| Transformieren | Struktur projizieren, Inspector als siebtes geschlossenes Tool ergänzen. Gruppen, aktive Tabs, Proportionen, Fenster und Geometrie erhalten. |
| v2 prüfen | Exakt sieben IDs: `document:cryptoscript:primary`, `tool:variables`, `tool:info`, `tool:output`, `tool:diagnostics`, `tool:status`, `tool:variable-inspector`. Jede ID genau einmal sichtbar oder geschlossen; Dokument nicht geschlossen. |
| Floating | Bis sieben Hosts; jeder enthält mindestens eine Pane; keine verschachtelten Hosts. Bestehende Eigentums-/Identitäts-/Struktur-/Geometrieprüfung beibehalten. |
| Speichern | Schema 2, gleiche Temp-/Flush-/Validierungs-/Replace-Strategie; kein Delete-first. v1 unverändert lassen. Bei Save-Fehler Diagnose; v1 bleibt verwendbar, erneute Migration beim nächsten Start ohne v2 möglich. |
| Rückwärtskompatibilität | Neue Version liest v1; alte Version liest weiterhin unverändertes v1. Keine Rückübersetzung späterer v2-Änderungen zugesagt. |
| Fokus/Reset | Normalbaum während Maximize persistieren; Restore exakt; Reset immer neuer Default, auch nach Migration. Default vor Installation gespeicherter Layouts erfassen. |
| Datenumfang | Nur Struktur. Keine VM, Origin, Payload-Dateien, gewählte Variable oder variable Pane-Titel in Layoutpersistenz. |

## 9. Dokumentationsabschluss und verbleibende Nachweise

F2 schließt die sechs R3-Vertragslücken auf Entwurfsebene; es behauptet keine bestandene Implementierungsabnahme. Die gesonderte Abnahmedatei ist Teil der Architektur. R4 soll insbesondere den zweckgebundenen Host-Lebenszyklus, vollständige große Copy-Werte, die Accessor-/BCL-Grenzen und die Ereignismatrix gegenprüfen.

Verbleibende technische Risiken sind messbare Performance/Peak-Speicher, Plattform-Clipboardgrenzen, temporärer Speicher/Crash-Cleanup, korrekte gemeinsame Host-Finalisierung, Getter-Konfigurationsabhängigkeiten und natives Dock-/Accessibility-Verhalten. Sie sind im Korrekturbericht mit Maßnahmen und Abnahmepunkten bewertet. Kein Risiko wird durch neue Geheimhaltungsanforderungen ersetzt.

**Ergebnisstatus: READY FOR OWNER REVIEW — nicht ACCEPTED.**
