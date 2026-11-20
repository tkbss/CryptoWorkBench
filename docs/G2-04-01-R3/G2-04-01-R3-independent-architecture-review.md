# G2-04-01-R3 — Independent Architecture Review

**Urteil: CHANGES REQUIRED**  
**Ergebnisstatus: READY FOR OWNER REVIEW**  
**Datum:** 17.11.2026  
**Repository:** tkbss/CryptoWorkBench  
**Verbindliche und geprüfte Baseline:** `32017a08c4771e73c12a50f6e1548476b9bb0a54`

## 1. Entscheidung und Prüfgrenze

F1 vereinfacht die Architektur sinnvoll und passt die Produktprämisse korrekt an. Die Architektur ist jedoch noch nicht zur unveränderten Umsetzung in G2-04-02 freigabefähig. Es verbleiben **zwei High-, drei Medium- und ein Low-Finding**. Die High-Bewertung bezeichnet hier die Verletzung einer zentralen Produktanforderung, keine Schlüsselgeheimhaltungs- oder Authentifizierungslücke.

Die Hauptprobleme sind der nicht wertreine Copy-Vertrag für komplexe Properties und das fehlende vollständige Zugriffskonzept für gültige Objekte oberhalb der Capture-Limits. Hinzu kommen eine unpräzise Grenze des verschachtelten Property-Inventars, die fehlende Übergabe aus Observer-Callbacks an einen zulässigen Capture und die Vermischung von Workspace-Invalidierung mit tatsächlichem Reset.

Schlüssel, Secret Shares, Zwischenwerte, Text und kryptografische Attribute dürfen vollständig sichtbar und kopierbar sein. **Maskierung, Reveal-Autorisierung, Passwortschutz und entsprechende Erweiterungspunkte sind keine R3-Anforderungen.** Die vorhandene Read-/Dock-Admission bleibt relevant für Konsistenz, Reentrancy und Lebenszyklus; sie ist kein Geheimhaltungssystem.

Geprüft wurden die drei F1-Artefakte und die tatsächlichen C#-Quellen. Aussagen aus F1 und früheren Reviews wurden gegen den Code geprüft, nicht als Nachweise übernommen. Der Bericht bewertet eine geplante Architektur: Die Findings behaupten keine bereits implementierten Inspector-Fehler.

### Baseline und Artefakte

`git rev-parse HEAD master` ergab für beide Referenzen den verbindlichen Hash. `git --no-optional-locks status --porcelain=v1` war leer. Origin ist `https://github.com/tkbss/CryptoWorkBench`. **Keine Baseline-Abweichung festgestellt.** Die Prüfstellen beziehen sich ausschließlich auf diesen Stand, nicht auf einen späteren Remote-Master.

| Grundlage | Verfügbarkeit und Verwendung |
|---|---|
| [F1-Architektur][f1] | Vollständig gelesen, eigentlicher Prüfgegenstand. |
| [F1-Corrections-Report][f1report] | Vollständig gelesen; acht Restrisiken und zehn R2-Neubewertungen einzeln beurteilt. |
| [F1-HTML-Prototyp][html] | Quelltext geprüft; vier reine JavaScript-Copy-Funktionen isoliert ausgeführt. |
| Ursprünglicher R2-Bericht | In F1 als frühere Chatantwort referenziert. Eine eigenständige R2-Datei ist in den bereitgestellten Artefakten nicht vorhanden. Die R2-Zuordnung in Abschnitt 8 folgt der dokumentierten F1-Tabelle; eine vollständige wortgetreue Gegenprüfung eines separaten Originaldokuments ist damit nicht belegt. |
| Tatsächlicher C#-Code | Read-only-Prüfung am angegebenen Commit, einschließlich Capture, VM, Interpreter-Host, Lease, Getter, Kryptohilfen und Dock-Persistenz. |

Keine Builds, Produkttestläufe oder native Avalonia-/Dock-Sitzung ausgeführt. Die dynamischen Nachweise beschränken sich auf den DOM-freien HTML-Copy-Pfad und öffentliche .NET-10-Collection-Metadaten. Keine Behauptung einer bestandenen nativen Drag-and-drop-, Accessibility- oder Performance-Abnahme. Produktcode, Tests und F1-Artefakte wurden nicht verändert; kein Commit, Push, Merge oder Beginn von G2-04-02. Nur dieser Bericht wurde außerhalb des Repository-Worktrees erstellt.

## 2. Findings

| ID | Schwere | Befund | Auswirkung |
|---|---|---|---|
| R3-H01 | High | Komplexes Copy liefert eine Snapshot-Repräsentation statt ausschließlich des Property-Wertes. | Zentrale Copy-Anforderung verletzt. |
| R3-H02 | High | Capture-Budgets werden funktionale Zugriffssperren; vollständiger Zugriff oberhalb der Grenzen bleibt vertagt. | Gültige kryptografische Objekte können nicht vollständig gelesen/kopiert werden. |
| R3-M01 | Medium | Vollständigkeitsvertrag für Collection-/Skalarobjekte und Getter-Unterstützung nicht geschlossen. | „Alle öffentlichen Properties“ ist für verschachtelte Runtime-Objekte nicht nachgewiesen. |
| R3-M02 | Medium | Capture aus der Auswahlbeobachtung hat keinen definierten zulässigen Scheduling-Punkt. | Naheliegende Integration wird durch die vorhandene Observer-Admission abgewiesen. |
| R3-M03 | Medium | Workspace-Invalidierung und tatsächlicher Reset sind nicht sauber getrennt. | Historische Snapshots können bei Lease-/Editorereignissen entgegen F1 verloren gehen. |
| R3-L01 | Low | Prototyp-Fixtures bilden wesentliche Runtime-Zustände nicht korrekt beziehungsweise nicht ab. | Visuelle Abnahme kann falsche fachliche Erwartungen bestätigen. |

### R3-H01 — Wertreines Copy fehlt für komplexe Properties und Referenzen

**Beleg:** [F1, Zeile 165][f1copy] definiert getaggtes JSON mit Typen, Membern, Referenzpfaden und Zuständen. Ein Referenzknoten kopiert seinen Snapshot-Pfad. Im [HTML, Zeile 79][htmlcopy] erzeugt `serializeNode` für Objekte `{type, members}` und für Blätter `{type, value}`; `payload` serialisiert diese Struktur.

**Reproduktion:** Die vier Funktionen `valueOf`, `complete`, `serializeNode`, `payload` wurden aus dem HTML extrahiert und ohne DOM ausgeführt. Für den vollständigen Wert `KeyType(Algorithm=Aes, MaterialKind=Secret)` entsteht beispielsweise:

```json
{
  "type": "KeyType",
  "members": {
    "Algorithm": { "type": "KeyAlgorithm", "value": "Aes" },
    "MaterialKind": { "type": "KeyMaterialKind", "value": "Secret" }
  }
}
```

`type`, `members` und die Blatt-Hüllen gehören zur Inspector-Projektion; sie sind keine Properties dieses `KeyType`. Ein snapshotlokaler Pfad ist ebenfalls nicht der referenzierte Wert. F1 nennt dies offen einen Exportvertrag, aber der R3-Auftrag verlangt Copy ohne zusätzliche Snapshot-Metadaten. Der Fehler besteht schon bei einem kleinen, vollständig erfassten Objekt und ist unabhängig von Limits oder historischer Anzeige.

**Abgrenzung:** Der geprüfte String-Copy-Pfad bewahrt Leerzeichen, Unicode, CRLF und abschließendes LF unverändert. Auch die Herkunftsbeschriftung außerhalb des Payloads ist richtig. Es gibt keinen Beleg, dass der HTML-Copy-Button Strings abschneidet oder normalisiert.

**Minimale Korrektur:** Copy-Daten von der Node-/Statusdarstellung trennen, innerhalb des bestehenden Projectors. Für bekannte komplexe Typen eine dokumentierte, ausschließlich wertbezogene Darstellung festlegen, beispielsweise JSON mit den tatsächlichen fachlichen Membernamen und Werten. Keine automatisch hinzugefügten Typ-, Revision-, Status- oder Pfad-Hüllen. „Referenzpfad kopieren“ wäre eine ausdrücklich anders benannte Zusatzaktion; „Wert kopieren“ muss den vollständigen Zielwert verwenden. Ein nicht berechneter oder fehlerhafter Wert darf nicht durch einen Statusstring als scheinbarer Wert ersetzt werden. Ein beliebiges CLR-Objekt hat keine naturgegebene Clipboard-Textdarstellung; der konkrete Vertrag muss daher vor der Umsetzung feststehen.

**Akzeptanz:** Golden-Values für `KeyType`, `KeyAttributes`, `SecretSet`, `HeaderReference` und `Definition`; Copy enthält ausschließlich den vereinbarten Wert. Historisch/aktuell, expandiert/eingeklappt und Auswahlwechsel nach Command-Start verändern denselben Copy-Payload nicht. Referenznavigation und Wert-Copy sind unterscheidbar. Eine zyklische Darstellung darf weder endlos serialisieren noch einen Pfad als eigentlichen Wert ausgeben.

### R3-H02 — Limits ersetzen den fehlenden vollständigen Zugriff

**Beleg:** [F1, Zeilen 169–173][f1limits] begrenzt einen Root auf Tiefe 16, 10.000 Nodes und 16 MiB detached Payload. Bei Überschreitung folgt `LimitReached`; die Lösung durch Teilaufnahme oder größere Budgets wird G2-04-02 überlassen. Die 100er-UI-Seiten arbeiten laut [F1, Zeile 103][f1graph] ausschließlich auf zuvor aufgenommenen Daten. Sie lösen die Capture-Grenze nicht.

Das ist kein rein hypothetischer zukünftiger Typ: [SecretSet.cs:25][secretdescriptor] erlaubt bis zu 255 Shares und jede positive durch acht teilbare `int`-Bitlänge. [SecretSet.cs:77][secretset] prüft Anzahl, eindeutige X-Werte und passende Länge, setzt aber kein 16-MiB-Gesamtlimit. Ein Shamir-Descriptor mit `Parts=255`, `MinParts=2`, `KeySizeBits=1_048_576` und 255 verschiedenen Shares zu je 131.072 Bytes erfüllt diese Konstruktorbedingungen. Allein seine Nutzdaten umfassen **33.423.360 Bytes = 31,875 MiB**. Auch der tatsächliche [Share-Operationspfad:11][shareoperation] übernimmt die Materiallänge; [SecretSharingEngine.Share:11][shareengine] produziert alle angeforderten Shares ohne eine solche Gesamtgrenze. Das ist eine aus dem Code ableitbare gültige Eingabe, keine hier ausgeführte große Allokation.

**Zusätzlicher Kostenbeleg:** [SecretShare.Value:17][sharevalue] klont bereits beim Getter. [Tr31HeaderDefinition.OptionalBlocks:13][tr31] klont ebenfalls. [KeyCheckValueCalculator:25][kcv] dekodiert das gesamte Material, bevor die AES-/TDEA-Länge geprüft wird. Ein erlaubter Getter ist deshalb nicht automatisch konstant teuer. Ein Payload-Limit allein begrenzt weder temporäre Kopien noch Hex-/JSON-Strings, DOM-/Control-Kosten oder eine einzige synchrone Getter-Ausführung.

**Folge:** Explizites `LimitReached` ist ehrlicher als Abschneiden, erfüllt aber noch nicht vollständige Zugänglichkeit. Auch die Anforderung, `RunState` kurz zu halten, ist mit einem vollständigen großen Root-Capture noch nicht operationalisiert. Die Zahlen sind in F1 korrekt als ungemessen bezeichnet; sie können deshalb nicht zugleich die abschließende Architekturentscheidung darstellen.

**Minimale Korrektur:** UI- und Arbeitsbudgets von einer Produktgrenze unterscheiden. Vorschau mit 160 Zeichen und 100 sichtbaren Einträgen darf bleiben. Für große unterstützte Werte einen konkreten vollständigen Weg festlegen: bedarfsgerechte, budgetierte Property-/Collection-Aufnahme unter demselben State-/Identity-Token und vollständiges Copy des angeforderten Wertes; Verarbeitung bereits detached Daten außerhalb der UI. Teilaufnahmen verschiedener Revisionen niemals zusammenfügen. Bei zwischenzeitlicher Mutation bleiben bereits erfasste Teile historische Werte; noch nicht erfasste Teile sind ausdrücklich nicht verfügbar. Ein vollständiger historischer Gesamtwert kann nur kopiert werden, wenn er vorher vollständig aufgenommen wurde.

Dies erfordert keinen neuen Streaming-/Workspace-Manager. Es erfordert jedoch eine Entscheidung über Aufnahmeumfang, Haltezeit, Peak-Speicher und den vorhandenen Host-Pfad für längere Arbeit. Nur den Root-Grenzwert zu erhöhen verschiebt das Problem. Reale Speicher-/Clipboardfehler dürfen als technische Fehler auftreten; eine stille Kürzung oder dauerhafte unbegründete 16-MiB-Produktgrenze darf nicht als vollständige Unterstützung gelten.

**Akzeptanz:** Der beschriebene Share-Satz sowie ein langer String sind vollständig erreichbar. Jedes Share und jeder angeforderte vollständige Wert lässt sich ohne Abschneiden kopieren. Eine abgesenkte Test-Budgetgrenze erzwingt denselben Fortsetzungsweg. Mutationen zwischen Teilaufnahmen führen zu einem klaren Revisionskonflikt, niemals zu einem gemischten Objekt. Peak-Speicher und längster UI-Block werden gemessen; die vereinbarten Leistungsgrenzen gelten auch für Clone, Formatierung und Clipboard-Payload, nicht nur für gespeicherte Nodes. KCV mit übergroßem/ungültigem Material führt nicht erst eine unbeschränkte Dekodierung auf dem UI-Thread aus.

### R3-M01 — Die Grenze von „alle öffentlichen Properties“ bleibt offen

**Beleg:** [F1, Zeilen 71–87][f1inventory] beschreibt ein vollständiges Metadateninventar. Die Root-Zahlen stimmen mit den aktuellen Klassen überein. Die [Graph-Tabelle ab Zeile 91][f1graph] beschreibt Collections jedoch über Einträge, nicht über deren vollständige öffentliche Runtime-Properties. Die Baseline exponiert tatsächlich `List<OptionalBlock>` ([KeyVariableDeclaration:73][keyattrs]), `ReadOnlyCollection<SecretShare>` und `byte[]` ([SecretSet.cs][secretset]) sowie ein `Dictionary<string,string>` aus PARAM ([ParameterVariableDeclaration:112][paramdict]).

**Überprüfbare Beispiele:** .NET-10-Metadaten liefern für `List<T>` `Capacity`, `Count`, `Item`; für `ReadOnlyCollection<T>` `Count`, `Item`; für `byte[]` unter anderem `Length`, `LongLength`, `Rank`, `SyncRoot`. Ein Eintragsadapter zeigt diese Properties nicht automatisch. String-/Byte-Darstellung als Skalar und Collection-Darstellung als Einträge sind sinnvolle Ansichten, beweisen aber allein keine wörtliche Vollständigkeit aller öffentlichen C#-Properties. Der HTML-Prototyp zeigt ausschließlich die fachlichen Einträge.

Unbekannte Overrides erhalten nach F1 nur Metadaten. Das ist eine richtige Grenze gegen unbekannten Code, muss aber als **nicht ausgewertete Property** sichtbar bleiben. Ein unbekannter Typ ist durch eine vollständige Namensliste noch nicht vollständig inspizierbar. Das betrifft auch transitiv verwendete Getter: Ein bekannter KCV-Accessor liest beispielsweise das virtuelle `Value` einer nicht versiegelten Key-Klasse. Die Prüfung nur des Namens `Kcv` wäre unzureichend.

**Minimale Korrektur:** Den Inventarvertrag ausdrücklich auf jeden erreichten Runtime-Knoten anwenden; ergänzende Collection-Einträge als eigenen Adapterzweig ausweisen. Für die vorhandenen Typen eine endliche Accessor-Liste einschließlich benötigter BCL-Properties festhalten. Skalaransicht und Property-Ansicht dürfen dieselben Daten unterschiedlich darstellen, ohne Metadaten zu verstecken. Indexer bekannter Collections sind durch Index-/Key-Einträge erreichbar; beliebige Indexer bleiben mit Signatur und Argumentbedarf sichtbar. Unbekannte Getter/Overrides nicht automatisch ausführen und nicht als erfolgreich unterstützt zählen. Sollte „Vollständigkeit“ stattdessen nur fachliche Modell-Properties bedeuten, wäre das eine ausdrückliche Änderung der derzeit wörtlichen Anforderung, keine stillschweigende Adapterentscheidung.

**Akzeptanz:** Für jeden unterstützten Runtime-Typ wird das erwartete öffentliche Inventar mit der tatsächlichen Reflection-Metadatenmenge abgeglichen, einschließlich geerbtem `Text`, `new`-Verdeckung und Overrides. Collection-Properties und Einträge sind beide nachvollziehbar. Kein bekanntes fachliches Property bleibt allein wegen einer fehlenden Allowlist-Registrierung ohne Wert. Ein Test-Override mit Mutations-/Aufrufzähler wird nie ausgeführt; die Zeile bleibt mit eindeutiger Begründung vorhanden. Dies ist eine Testanforderung, keine jetzt vorgenommene Teständerung.

### R3-M02 — Auswahl-Callback liegt innerhalb der Observer-Sperre

**Beleg:** [VariableViewModel.SelectedVariable:206][vmselection] verwendet `SetProperty`. [WorkbenchBindable:13][bindable] umschließt `OnPropertyChanged` mit `ObservePresentation`; der [Automation-Service:68][serviceobserver] leitet dies für den registrierten Editor an `EditorCoordinator.Observe`. Dieser erhöht [ObserverDepth:347][observe] und aktiviert `InterpreterHostQueue.EnterObserver`.

Währenddessen verweigert [InterpreterHostQueue.Reserve:24][hostreserve] die Aufnahme. Auch [ValidateStateAccess:88][stateaccess] verbietet Observer-Zugriffe und verlangt für den registrierten GUI-Editor den UI-Thread. F1 beschreibt „Auswahl beobachten → Aufnahme anfordern“, definiert aber den notwendigen Übergang nicht. Der F1-Corrections-Report nennt Reentrancy selbst als offenes Risiko.

**Reproduktion als Aufrufkette:** Benutzer wählt eine Zeile → `SelectedVariable.SetProperty` → `OnPropertyChanged` → `Observe` → synchroner Inspector-Capture → `Reserve` wirft `ExecutionBusyException`. Ein blindes `Task.Run(Capture)` löst dies nicht: Es verletzt anschließend die UI-Thread-Bedingung. Während `ApplyVariables` sind zudem Publikation und Draft-Stempel noch im Aufbau; ein dort reentrant gestarteter Read darf nicht teilweise veröffentlichte Daten als gültig behandeln.

**Minimale Korrektur:** Der PropertyChanged-Handler erfasst ausschließlich Auswahlabsicht und lokalen Request-Zähler. Capture wird nach Verlassen der Benachrichtigung auf dem UI-Dispatcher angestoßen und dort unter der bestehenden Host-Admission vollständig neu validiert. Schnelle Auswahlwechsel dürfen zusammengefasst werden. Ein echter Busy-/Lease-Zustand wird sichtbar gemeldet; keine unbegrenzte Retry-Schleife und kein neuer konkurrierender Scheduler. Ein direkt ausgelöster Refresh-Command verwendet denselben Host-Vertrag.

**Akzeptanz:** Auswahl bei registriertem Editor führt nach dem Observer-Callback zu genau einem erfolgreichen Capture. Reentrant provozierter Zugriff wird weiter abgewiesen. A→B→A, `ApplyVariables`, Close und Dispose vor dem Dispatcher-Callback publizieren kein überholtes Ergebnis. Keine Arbeit liest live außerhalb des Hosts oder auf einem unzulässigen Thread. Ein Inspector-Read erhöht die StateRevision nicht und erzeugt keinen Script-/History-Eintrag.

### R3-M03 — WorkspaceChanged bedeutet nicht Workspace-Reset

**Beleg:** [F1, Zeile 139][f1history] erlaubt historische Anzeige und Copy ausdrücklich während eines MCP-Lease. [F1, Zeile 149][f1lifecycle] verlangt Löschen bei Workspace-Wechsel/-Reset, ohne die Ereignisse auf die vorhandene Semantik abzubilden.

[EditorCoordinator.BeforeDocumentChange:165][documentchange] erhöht bereits bei normaler Editoränderung die Generation und sendet `WorkspaceChanged`. [TakeControl:116][takecontrol] erhöht die Generation beim MCP-Kontrollwechsel. [WorkbenchLeaseV2:139][leasepublish] veröffentlicht die Benachrichtigung; beim Erwerb wird sie [ab Zeile 268][leaseacquire] ausdrücklich markiert. Release/Drain tun dies ebenfalls. Diese Ereignisse belegen eine ungültige Live-Auswahl, nicht zwingend einen gelöschten Interpreterzustand. [VariableViewModel.InvalidateDraft:145][vminvalidate] leert die privaten Publikationszuordnungen, lässt aber die bestehende Auswahl und die Zeilen stehen.

**Reproduktion:** Snapshot aufnehmen → MCP-Lease erwerben → `WorkspaceChanged`. Wird F1 „Workspace-Wechsel löscht“ direkt an dieses Ereignis gebunden, verschwindet gerade der Snapshot, dessen historische Copy-Nutzung während des Lease zugesagt wurde. Dasselbe passiert beim Tippen ohne Script-Ausführung. Dies ist eine konkrete Integrationsmehrdeutigkeit des Entwurfs, kein Beleg für einen bereits vorhandenen Inspector-Bug.

**Minimale Korrektur:** Eine Ereignismatrix festlegen: normale Dokumentänderung und Lease-Übergänge entwerten Live-Handles/Requests, behalten bereits detached Werte und markieren sie konservativ historisch. Tatsächlicher Kontextwechsel, fachlicher Reset und Shutdown dürfen gemäß Produktvertrag löschen. Bestehendes `SelectedVariable=null` bei [ApplyVariables:174][vmpublish] bleibt ein expliziter Leerzustand. Layout-Reset berührt fachliche Snapshot-Daten nicht. Für echte Reset-/Kontextwechsel einen vorhandenen semantischen Aufrufer anbinden; kein zweiter Workspace-Manager oder globaler Epoch-Zähler.

**Akzeptanz:** Tippen und Lease acquire/release erhalten den historischen Copy-Payload unverändert; neue Reads/Berechnungen bleiben gesperrt beziehungsweise benötigen neue gültige Handles. Tatsächlicher Reset löscht die Auswahl und verwirft späte Ergebnisse. Layout-Reset, Tabwechsel, Float und Restore verändern weder Aufnahmezeit noch StateRevision. Normales `ApplyVariables` kann die Auswahl wie bisher löschen; historische Unterstützung bedeutet damit keine automatische unbegrenzte Snapshot-Historie.

### R3-L01 — Mock-Fixtures sind kein vollständiger fachlicher Abnahmenachweis

**Beleg:** Im [KEY-Fixture, HTML:63][htmlkey] steht ein bloßes `KEY demoKey=0x(...)`, während der Property-Baum `KeyType.Algorithm=Aes` und nichtleere KeyAttributes zeigt. Der tatsächliche [Literal-Builder:120][literalkey] setzt nur Identifier, Typ, Value, Format und Größe; der Kommentar und die [Key-Defaults:21][keydefaults] bestätigen `Secret(Unknown)`, unspezifizierte Usage und leere Attribute. Die Darstellung entsteht daher nicht aus dem angezeigten Script allein.

`OriginalKeyMetadata` und `PARAM.HeaderReference` sind in den Fixtures jeweils null. Damit werden gerade zwei relevante verschachtelte Nicht-null-Graphen nicht demonstriert. KCV ist ausdrücklich als Demo gekennzeichnet; daraus wird kein falscher Kryptonachweis abgeleitet. Der HTML-Prototyp behauptet selbst keine native Dock-Implementierung.

**Minimale Korrektur:** Literal-Fixture fachlich angleichen oder ergänzende synthetische Zustandsänderungen ausdrücklich beschreiben. Nicht-null-Fixtures für `OriginalKeyMetadata` und `HeaderReference`, einen nicht ausgewerteten Getter sowie Limit-/Fortsetzungszustände ergänzen. Das ist eine Dokumentationskorrektur und kein Anlass, Produktlogik in den Mock einzubauen.

**Akzeptanz:** Jedes Fixture bezeichnet seine Herkunft und stimmt mit ihr überein. Die wichtigsten verschachtelten Wege können visuell geprüft werden. Das spätere C#-Ergebnis wird separat gegen echte Runtime-Objekte geprüft.

## 3. Getter, Typen und bestehender Capture

### Nachgeprüfte Inventare und Verantwortlichkeiten

| Runtime-Klasse | Root-Properties nach Override-Deduplikation | Bewertung |
|---|---:|---|
| `StringVariableDeclaration` | 6 | F1 korrekt: Basis fünf plus GMAC. |
| `PathVariableDeclaration` | 5 | F1 korrekt. |
| `ValVariableDeclaration` | 5 | Type/Value sind Overrides, keine zusätzlichen Properties. |
| `ParameterVariableDeclaration` | 7 | Basis plus Mechanism und HeaderReference. |
| `KeyVariableDeclaration` | 14 | F1 korrekt, einschließlich aller drei KCVs und Text. |
| `SecretVariableDeclaration` | 6 | SecretSet muss als eigener Graph zugänglich sein. |
| `BlockHeaderVariableDeclaration` | 6 | Definition sowie tatsächlich berechnetes Value beachten. |

Grundlage sind [VariableDeclaration.cs][declaration], [Statement.Text:12][statement] und die jeweiligen Klassen im Baseline-Quellcode. Die Aufzählung der sieben CryptoScript-Deklarationsklassen ist keine Behauptung, jedes beliebige CLR-Unterobjekt sei damit bereits vollständig abgedeckt; dazu R3-M01.

[VariablePresentationCapture:11][capture] erstellt `VariableModel`-Zeilen und speichert Weak-Origin sowie ausgewählte Anzeigeattribute. Es bildet weder sämtliche Runtime-Properties noch den vollständigen Secret-/TR31H-Graphen ab. `VariableModel` ist veränderlicher GUI-Zustand. F1s Entscheidung, daraus keinen autoritativen Inspector-Snapshot zu machen, ist **bestätigt**. Kein zweites `AttachView`; keine Verwendung editierter Identifier-/Value-Felder als Live-Identität.

### Nebenwirkungen: Ergebnis der Quellprüfung

| Pfad | Ergebnis | Konsequenz |
|---|---|---|
| PARAM-Property-Getter | Mechanism liest `_mechanism`; HeaderReference liest die Referenz; Value ist der geerbte Wert. Keine Mutation in diesen bekannten Gettern gefunden. | Geprüfte Getter zulassen. |
| `PARAM.GetParameters()` | Gibt das lebende Dictionary zurück, nicht eine Kopie. | Als ausdrücklich benannten Adapter unter Admission aufnehmen; keine Referenz oder Enumerator in die UI geben. |
| `PARAM.PrintOutput()` | Ruft `BuildValue`; dieses setzt Value neu und kann über `ParameterMechanismen` Mechanism/#MECH verändern. | Als Inspector-Formatter ausgeschlossen. F1-Begründung bestätigt. Quellen: [PARAM:121][parambuild] und [PARAM:184][paramprint]. |
| `TR31H.Value` | Bekannte Implementierung ruft `Definition.ToString()` auf. Die versiegelte Definition arbeitet mit eigenen kopierten Daten; der geprüfte Formatter liest Attribute und serialisiert die stringförmige OptionalBlock-Data. | Für den aktuellen Anwendungspfad keine Interpretermutation gefunden. Das pauschale Verbot beliebiger `ToString`-Aufrufe schließt diesen gezielt geprüften Getter nicht aus. Die globale Serializer-Abhängigkeit ist unten abgegrenzt. Quellen: [BlockHeaderVariableDeclaration:12][blockheader], [Tr31HeaderDefinition:118][tr31format]. |
| `Kcv`, `Kcvc`, `Kcvk` | Aktuelle Implementierung berechnet auf lokal dekodierten Bytearrays; AES-/DES3-Analysehilfen verwenden lokale Kryptoinstanzen. Keine Dictionary-/PARAM-/History-Mutation im geprüften Pfad gefunden. | Explizite Berechnung ist plausibel. Größenkosten vor Dekodierung berücksichtigen. KCV ist vollständiger Analysewert, nicht automatisch der oft kürzere konventionelle Anzeigecode. Quelle: [KeyCheckValueCalculator:13][kcv]. |
| Unbekannter Override / Formatter | Nicht durch das Verhalten der Basisklasse bewiesen. Getter können beliebigen Code ausführen; Reflection zeigt nur Metadaten. | F1s genaue Runtime-/Accessor-Prüfung beibehalten. Kein generisches `GetValue`, Live-JSON, PrintOutput oder Timeout als Ersatz für den Audit. |

Der TR31H-Formatter verwendet `JsonConvert.SerializeObject(b.Data)` und damit die globale Newtonsoft-Konfiguration. Im geprüften Produktcode wurde keine Zuweisung an `JsonConvert.DefaultSettings` gefunden; eine universelle Reinheitsgarantie bei künftig installierten Convertern/DefaultSettings folgt daraus nicht. Der vorhandene [SnapshotJson-Serializer:10][snapshotjson] isoliert sich ausdrücklich von dieser globalen Konfiguration. Der spätere Accessor-Audit muss solche transitiven Abhängigkeiten berücksichtigen, statt allein den unmittelbaren Getter als „einfach“ zu registrieren. Das ist ein abgegrenztes Integrationsrisiko, kein nachgewiesener aktueller TR31H-Mutationsfehler.

**Keine neue Anforderung an Geheimhaltung:** Die vorhandene lokale Bereinigung temporärer Kryptopuffer wird lediglich als Codeverhalten beschrieben. Daraus entsteht weder ein Inspector-Maskierungsgebot noch eine Authentifizierungspflicht.

## 4. Snapshot-Konsistenz und historische Werte

Der Grundvertrag von F1 ist tragfähig: Original-Identifier aus der veröffentlichten Zeile, Weak-Origin, VM-Publikation, Workspace-/Draft-Stempel, StateRevision und tatsächliche Dictionary-Objektidentität werden **innerhalb derselben Aufnahme** validiert. Ein Inspector-VM-lokaler Request-Zähler ist zusätzlich nötig, um verspätete UI-Ergebnisse auszusortieren; er ersetzt keine Interpreterrevision.

| Fall | Geprüftes Baseline-Verhalten | Erforderliche Inspector-Semantik |
|---|---|---|
| Assignment gleicher Name | [AssignmentEvaluator:103][assignment] ersetzt den Dictionary-Eintrag durch das vorbereitete Objekt. | Objektidentität prüfen; alten Handle nicht auf neuen gleichen Namen umbiegen. |
| In-place-Mutation | GUI-Mutate erhöht in `finally` die Version und Prozessrevision ([AutomationService:111][mutation]). Jobs sammeln `EffectsPossible` und erhöhen beim Abschluss die Revision ([Registry:544][effects], [Registry:583][finalize]). | Identität allein reicht nicht; StateRevision mitprüfen. Read-Gate während laufender Jobs verhindert den Zugriff vor Abschluss der Revisionsfortschreibung. |
| Draft-Änderung | Editierbare Zeile ist kein aktueller Interpreterwert. Private Draft-Zuordnungen schützen die ursprüngliche Identität ([VariableViewModel:91][vmdraft]). | Capture niemals nur aus `VariableModel.Identifier/Value` ableiten. |
| GUI-Ausführung / paralleler Host | Prozessweiter nichtwartender Host-Gate plus EditorCoordinator. | Live-Capture und KCV brauchen beides; ein bloßes `VerifyAccess` reicht nicht. |
| MCP-Lease | Kontrollphase und Generation ändern sich; neue GUI-State-Reads nicht zulässig. | Bereits detached Werte bleiben historisch kopierbar; Copy öffnet keinen Host-Zugriff. R3-M03 korrigieren. |
| Workspace-Reset / neue Publikation | Clear/ApplyVariables und Kontextwechsel können Auswahl sowie Identität entwerten. | Späte Ergebnisse verwerfen; bestehendes Null-Selection-Verhalten respektieren. |
| Keine StateRevision-Events | `StateRevision` ist ein Zähler ohne allgemeines Änderungsereignis ([HostQueue:19][revision]). | „Snapshot R, aufgenommen um …“ ist korrekt; „aktuell“ ohne Prüfung nicht. Bekannte Invalidierung darf konservativ „historisch“ anzeigen. |

Die Revision ist kein Hash des Objektgraphen. Sie schützt kooperierende, über den vorhandenen Host laufende Änderungen. F1 darf daraus keine Erkennung beliebiger unsynchronisierter Mutationen durch fremden Code ableiten. Zusätzliche globale Revisionsregister lösen dieses Problem nicht.

**Historische Snapshots: fachlich befürwortet.** Sie passen zur Analyseumgebung, benötigen keine Copy-Autorisierung und vereinfachen den Zugriff während eines Lease. Herkunft, Aufnahmezeit und Revision stehen im UI beziehungsweise in der Copy-Rückmeldung. Sie gehören nicht in den eigentlichen Clipboard-Wert. Ein nicht bei R aufgenommener oder berechneter Wert darf später nicht aus R+1 in einen als R bezeichneten Snapshot nachgetragen werden. F1s verlangter Token-Check für KCV ist hierfür richtig. Eine neue separate Funktion „KCV aus historischem Material berechnen“ ist durch F1 nicht vorgesehen und für diese Freigabe nicht erforderlich.

## 5. Docking und vollständiger Migrationsvertrag

**Architekturprüfung: grundsätzlich PASS, Implementierungsabnahme offen.** Der Stand enthält Dock.Avalonia `12.1.0.13` und bereits den benötigten Zwei-Spalten-Aufbau. [DockShellLayout:42][dockdefault] gruppiert Variables und Info rechts, Variables aktiv; Output und Diagnostics bilden unten eine Gruppe. Die neue Inspector-Pane kann dort als weiterer Tab ergänzt werden. Status existiert weiterhin im Inventar und ist im aktuellen Default geschlossen. F1 beschreibt das korrekt.

Optionale dritte Spalte benötigt den tatsächlichen `SplitToDock`-Pfad, nicht lediglich ein anderes Home-/Dock-Ziel. [EditorDockFactory:196][docksplit] und native Floating Hosts sind vorhanden. Die bestehende View/VM darf beim Reparenting erhalten bleiben; kein erneutes Editor-Attach, kein Capture allein aufgrund des Dockens.

### Admission und Fokusmodus

[DockEditorSession.CanRelocate:99][dockadmission] verlangt unter anderem gestartete, nicht disponierte Sitzung, keinen Busy-/Executing-/Stopping-Zustand, GUI-Phase, keine Pending-/Active-/Observer-Operation, keine Dokument-Updates oder Editor-Locks und aktivierte Fenster. [EditorDockFactory.Allowed:106][dockallowed] ergänzt Layoutfault/Fokusmodus. Inspector-Maßnahmen dürfen diese Grenzen nicht umgehen. Historisches Copy kann während MCP funktionieren, obwohl Float/Dock/Close gerade abgewiesen werden.

Maximize bewahrt den Normalbaum; [Capture:30][dockcapture] serialisiert während Fokus diesen Normalbaum. Restore/Reset nutzen den gesonderten `ChangeLayout`-Pfad, bleiben aber an die übrige Admission gebunden. [RestoreAtStartup:56][dockstartup] erfasst den frischen Default vor Installation des gespeicherten Layouts; dadurch kann Reset nach Migration weiterhin den neuen Default liefern. [Reset:214][dockreset] arbeitet mit diesem Default und hat einen Rollback-Pfad. Diese Struktur muss um die neue Pane erweitert, nicht ersetzt werden.

### v1 → v2: Vertrag für die spätere Umsetzung

| Aspekt | R3-Bewertung und konkrete Bedingung |
|---|---|
| Pane-Inventar | Genau sieben eindeutige IDs: `document:cryptoscript:primary`, `tool:variables`, `tool:output`, `tool:info`, `tool:diagnostics`, `tool:status`, `tool:variable-inspector`. Jede Pane genau einmal sichtbar oder als geschlossenes Tool registriert; Dokument nicht geschlossen. Aktuelle IDs: [DockShellState:9][dockids]. |
| Auswahl der Datei | Vorhandenes gültiges v2 gewinnt. Nur **fehlendes** v2 erlaubt v1-Migration. Vorhandenes ungültiges/unlesbares v2 führt zur Diagnose und zum Default, nicht zum stillen alten v1. |
| Unterscheidung fehlend/ungültig | Das aktuelle [Load:51][dockload] liefert für beides null. Die spätere Orchestrierung muss diese Zustände unterscheiden; `Load(v2) ?? Load(v1)` erfüllt F1 nicht. Ein kleiner Ergebnisstatus beziehungsweise präziser Fehlerpfad genügt. |
| Historische v1-Prüfung | Schema 1 mit altem Sechserinventar und bisherigen Grenzen validieren. Nicht zuerst die globale PaneIds-Liste auf sieben umstellen und v1 damit prüfen. Aktuelle Prüfstellen: [Decode:118][dockdecode], [Validate:230][dockvalidate]. |
| Transformation | Validierten Strukturbaum übernehmen; Inspector zunächst geschlossen ergänzen. Gruppen, aktive Tabs, Proportionen, Hauptfenster und Floating-Geometrie erhalten. Kein automatisches Umsortieren alter Benutzerlayouts in den neuen Default. |
| v2-Prüfung | Neues Siebenerinventar; bis zu sieben Floating Hosts, mindestens eine Pane pro Host, keine verschachtelten Hosts. Bestehende Identitäts-, Referenz-, Struktur- und Geometrieprüfung beibehalten. Nicht nur die PaneIds, sondern auch die bisherige harte Sechser-Hostgrenze ändern. |
| Persistenz | Schema 2 nach `dock-layout-v2.json`; Strukturprojektion beibehalten. [Save:67][docksave] validiert, schreibt Temp im selben Dateisystem, flusht, validiert erneut und ersetzt atomar. Kein Delete-first. Migration benutzt denselben Mechanismus mit v2-Validierung. |
| Fehler und Wiederholung | Scheitert Save, v1 unverändert erhalten, Fehler melden; der migrierte In-Memory-Baum darf weiter benutzt werden. Beim nächsten Start ohne v2 wiederholbar migrieren. Beschädigtes vorhandenes v2 ist kein Anlass zum automatischen v1-Downgrade. |
| Rückwärtskompatibilität | Neue App liest alte v1-Daten. Alte App liest weiterhin die unveränderte alte v1-Datei. Spätere v2-Änderungen werden nicht zurückgeschrieben; bidirektionale Downgrade-Synchronisation wird nicht zugesagt. |
| Maximize/Restore/Reset | Normalbaum inklusive neuer Pane persistieren; Restore identisch; Reset neuer Zwei-Spalten-Default. Layout-Reset ist kein Interpreter-/Workspace-Reset. |
| Fachliche Daten | Nur Layoutstruktur persistieren. Keine VM, Origin, Snapshotwerte oder variable Pane-Titel. Das ist eine Zuständigkeitsgrenze, kein Geheimhaltungskonzept. |

F1 enthält damit den wesentlichen Migrationsvertrag. Noch ausstehende native Nachweise werden nicht als bereits erfüllte Implementierung ausgegeben. Insbesondere der im F1-Bericht erwähnte spezielle Cross-Window-Mouse-Redock-Fall bleibt eine Regression-/Abnahmeaufgabe; in R3 wurde er nicht nativ reproduziert oder als behoben bestätigt.

## 6. Minimale Zielarchitektur und Komplexität

Die drei vorgesehenen Bausteine reichen weiterhin: **Inspector-VM, zustandsloser Projector, Dock-View**. Snapshot-/Node-/Selection-Datenträger sind gerechtfertigt. Die optionale `IVariableInspectorReader`-Schnittstelle ist nur bei tatsächlichem Bedarf für die Read-Grenze/Tests nötig; zwei interne Methoden am bestehenden Host sind ebenfalls angemessen.

Der minimale Ablauf nach Korrektur:

1. Auswahlbenachrichtigung erfasst die Absicht; ein lokaler Request-Zähler entwertet ältere UI-Arbeit.
2. Ein nachgelagerter Dispatcher-Aufruf fordert den Read beim vorhandenen Automation-Service an.
3. Bestehende Host-/Editor-Admission validiert Original-Identifier, Publikation, Workspace-Stempel, StateRevision und Objektidentität gemeinsam; der Capture-Token entsteht dort.
4. Der Projector nutzt Metadaten und geprüfte typisierte Accessors. Collection-/Skalaransicht und vollständiges Property-Inventar sind ausdrücklich definiert. Größere Zugriffe folgen dem vorab beschlossenen budgetierten Aufnahmevertrag.
5. Das VM erhält detached Ergebnisse und bindet ausschließlich diese. Berechnungen durchlaufen denselben Host-Vertrag; Expand/Collapse und bereits erfasstes Copy tun dies nicht.
6. Clipboard bekommt ausschließlich den vollständigen vereinbarten Wert. Historie/Fehler/Provenienz bleiben UI-Information. Derselbe View-/VM-Satz überlebt Dock-Reparenting; semantisches Close und Dispose entwerten Requests.

**Unnötige Komplexität vermeiden:** Kein globaler Snapshot-Manager, kein zweiter Workspace-/Revisionsdienst, keine zweite VariableDictionary, kein eigener Copy-Service, keine generische Serialisierung beliebiger Live-Objekte. Kein parallel gepflegter globaler „IsCurrent“-Zustand. Lokaler Request-Zähler und StateRevision sind keine überflüssige Duplizierung: Sie schützen unterschiedliche Dinge, UI-Ergebniszuordnung beziehungsweise Interpreterzustand. Ein detached historischer Snapshot ist ebenso keine zweite autoritative Variablenhaltung.

Die getaggte generische Graph-Exportstruktur ist für den verlangten Wert-Copy unnötig und verursacht R3-H01. Die pauschale Root-Vollaufnahme erzeugt den größten Komplexitätsdruck bei Ressourcen; sie sollte auf den tatsächlich geforderten Zugriff zugeschnitten werden, bevor hierfür weitere Dienste eingeführt werden.

## 7. Bewertung sämtlicher acht F1-Restrisiken

| F1-Risiko | R3-Ergebnis | Erforderlicher Abschluss |
|---|---|---|
| Atomare Erreichbarkeit privater VM-Stempel | **Offen, R3-M02.** Identitätsansatz korrekt; Callback-/Admission-Reihenfolge fehlt. | Interner Host/VM-Vertrag plus nachgelagerter Dispatcher-Capture. |
| Kein universelles StateRevision-Ereignis | **Akzeptabel mit Snapshot-Semantik.** Kein zusätzlicher globaler Eventbus notwendig. | Keine unbewiesene Aktuell-Anzeige; Live-Aktionen immer neu prüfen. |
| Historische Kopien | **Fachlich befürwortet; R3-H01/M03 korrigieren.** | UI-Herkunft getrennt vom Wert, Ereignismatrix und keine Revisionsmischung. |
| Große Graphen/Bytearrays | **Nicht gelöst, R3-H02.** Messung allein ersetzt den fehlenden Zugriffsweg nicht. | Vollständiger Zugriff über budgetierte Arbeit; Speicher-/UI-Messungen. |
| Getter-Nebenwirkungen | **Bekannte Pfade konkret geprüft; Grundansatz bestätigt.** R3-M01/H02 beachten. | Vollständiger Accessor-Vertrag, transitive Override-Prüfung, Kostenkontrolle. |
| Collection-Zyklen und vollständiges Objekt-Copy | **Teilweise offen, R3-H01/M01.** Snapshotlokale Referenzen verhindern Rekursion, sind aber kein exakter Copy-Wert. | Inventar, Referenznavigation und wertbezogene Copy-Repräsentation trennen. |
| Dock-Fensterzahl/Migration | **Vertrag grundsätzlich tragfähig.** | Sieben Hosts/IDs, historische v1-Prüfung, Atomizitäts-/Normalbaumtests. |
| Native Drag-and-drop/Accessibility | **Nicht nachgewiesen, spätere Abnahme.** R3-L01 betrifft Mock-Aussagekraft. | Native Windows-Matrix einschließlich Cross-Window-Mausoperationen, Fokus und Tastatur. |

## 8. Abgleich mit den in F1 dokumentierten R2-Findings

Die folgende Zuordnung bestätigt keine Befunde allein deshalb, weil R2 oder F1 sie aufführt. Die Bewertung folgt der aktuellen Produktprämisse und den oben belegten Quellpfaden.

| R2-ID laut F1 | R3-Beurteilung |
|---|---|
| H01 — geerbtes Text | Geheimhaltungsforderung verworfen und nicht wieder aufgenommen. Öffentliches `Text` ist tatsächlich vorhanden und gehört ins Inventar. Root-Korrektur in F1 bestätigt. |
| H02 — KEY-Klartext in Capture/Variables | Klartext entspricht dem Produktziel. Keine neue Maskierung nötig. Capture bleibt funktional unvollständig für den Inspector. |
| H03 — Alias-/verschachtelte Inhalte | Keine Geheimhaltungsklassifikation nötig. Verschachtelte Werte müssen sichtbar sein; Getter-/Clone-/Copy-Verträge bleiben relevant. |
| M01 — Invalidierung/Lebenszyklus | Historische Semantik sinnvoll. F1 noch nicht vollständig: R3-M03 und Reentrancy aus R3-M02. |
| M02 — VariableModel nicht autoritativ | Bestätigt. F1s Original-Identifier, Weak-Origin, Revision und Publikation sind passend; interne atomare Anbindung noch konkretisieren. |
| M03 — Copy-Gateway | Autorisierungsteil ausdrücklich verworfen. Exaktheit bleibt relevant; komplexes Copy ist mit R3-H01 noch nicht korrekt. |
| M04 — Default/Reset | Im F1-Design korrekt adressiert; Baseline-Struktur unterstützt die Lösung. Native Umsetzung später abnehmen. |
| M05 — Layoutmigration | In F1 im Wesentlichen korrekt geschlossen; Abschnitt 5 präzisiert die tatsächlichen Anpassungsstellen. |
| M06 — Getter/Ressourcen | Getter-Konzept verbessert; Vollständigkeits- und Ressourcenvertrag bleiben mit R3-M01/H02 offen. |
| L01 — Mock/Accessibility | Reveal-Aspekte entfallen. Echte Accessibility nicht durch HTML behaupten; neue fachliche Fixture-Lücke R3-L01. |

## 9. Verbindliche Implementierungsabnahme nach Architekturkorrektur

Diese Kriterien ergänzen/präzisieren F1 A01–A16. Es wurden dafür keine Tests angelegt oder verändert.

| ID | Prüffall und erwartetes Ergebnis |
|---|---|
| R3-A01 | Inventar aller sieben Deklarationsklassen: 6/5/5/7/14/6/6 Root-Properties; tatsächlicher Runtime-Typ und geerbtes Text. Inventar auch für nicht-null HeaderReference, OriginalKeyMetadata und Collections. |
| R3-A02 | Getter-/Formatter-Probe: Öffnen, Expandieren, Copy und Docking ändern weder PARAM.Value/Mechanism/Dictionary noch History, Print-Ausgabe oder StateRevision. Unbekannter Override wird nicht aufgerufen. Bekannte TR31H-Value-Darstellung vollständig. |
| R3-A03 | KCV/KCVC/KCVK mit vorhandenen AES-/TDEA-Referenzwerten, unpassendem Algorithmus, ungültigem/übergroßem Material und alter Revision. Vollständiger Wert oder eindeutiger NotApplicable-/Fehlerzustand, keine Mutation. |
| R3-A04 | Auswahl im registrierten GUI-Editor: Capture erst nach Observer-Ende. Schnelle Wechsel, Publish-Reentrancy, Busy und Dispose liefern kein falsches Ergebnis. Keine Endlos-Retries. |
| R3-A05 | Assignment gleicher Name/neues Objekt und In-place-Mutation gleiches Objekt getrennt prüfen. Alte Handles werden jeweils abgelehnt. Draft-Edit allein ändert keinen Inspector-Livewert. |
| R3-A06 | GUI-Job, paralleler Hostzugriff, MCP acquire/release und Drain: neue Reads korrekt gesperrt; bereits detached historische Werte bleiben gemäß Ereignismatrix verfügbar. Keine Mischung zwischen R und R+1. |
| R3-A07 | Echter Reset, neue Auswahl, ApplyVariables-null, semantisches Close und Shutdown: späte Ergebnisse verworfen. Layout-Reset/Float/Restore dagegen ohne fachliche Mutation und ohne unnötige Neuaufnahme. |
| R3-A08 | Scalar-Copy: Unicode, führende/nachlaufende Leerzeichen, CRLF, LF, abschließende Newline, leerer String, Null, lange Strings; Byte-Copy vollständig reversibel. Clipboard-Payload wird vor asynchroner Übergabe an einen festen Snapshot gebunden. |
| R3-A09 | Complex-Copy-Golden-Values ohne Inspector-Hüllen/Metadaten; Referenzwert versus Pfadaktion unterscheidbar. Historische Herkunft sichtbar, aber nicht im Payload. Fehlende Kinder nicht als vollständigen Wert ausgeben. |
| R3-A10 | Gültiger Share-Satz >16 MiB und abgesenktes Budget: alle Werte über den festgelegten Fortsetzungsweg erreichbar. Tiefen-/Node-Grenze verhindert Endlosschleife, schneidet jedoch nicht still Werte ab. Peak-Speicher, größter UI-Block und Abbruch gemessen. |
| R3-A11 | Frischer Start und Reset: zwei obere Spalten; Variables/Info/Inspector in einer rechten Gruppe, Variables aktiv; Output/Diagnostics unten; Status wie bisher geschlossen. |
| R3-A12 | Tatsächliches Maus-Drag-and-drop in dritte Spalte, native Floating Windows, Home/Dock, Close/Reopen, Maximize/Restore und Mehrmonitor-Geometrie; View-/VM-Identität und Snapshot bleiben bei Reparenting erhalten. |
| R3-A13 | Dock-Admission-Matrix: GUI bereit, executing, pending, observer, MCP, draining, stopping, Editor-Lock, Document.IsInUpdate, Fokusmodus, Layoutfault. Bestehende verweigerte Operationen bleiben verweigert, ohne versteckte Wiederholung. |
| R3-A14 | v1-Fixtures: Tabs, geschlossene Tools, bis sechs Hosts, dynamische Container/Geometrie. Ergebnis v2 mit sieben IDs, Inspector geschlossen; v1 bytegleich. Fehlende/duplizierte IDs und unzulässige Strukturen werden zurückgewiesen. |
| R3-A15 | v2 mit sieben Floating Hosts akzeptieren; achtes/verschachteltes/leeres Hostlayout ablehnen. Gültiges v2 bevorzugen; ungültiges vorhandenes v2 nicht aus v1 ersetzen. Neuer Default nach Migration per Reset erreichbar. |
| R3-A16 | Persistenzfehler vor Replace: bestehende Datei unverändert, Diagnose, wiederholbarer Start. Save während Maximize erhält Normalbaum. Keine VM-/Snapshot-Daten in Layout-JSON. |
| R3-A17 | Native Tastatur-/Fokus-/Accessibility-Prüfung: Property-Namen, Werte und Copy-/Berechnen-Aktionen verständlich; ein- und ausgeklappte sowie historische/fehlerhafte Zustände unterscheidbar. Fachlich korrekte Mock-Fixtures als Designgrundlage. |

## 10. Owner-Entscheidungen und Freigabeempfehlung

Die Produktprämisse selbst ist entschieden und wird nicht erneut zur Diskussion gestellt. Vor Freigabe der Umsetzung sind diese konkreten Vertragslücken zu schließen:

1. **Komplexe Copy-Repräsentation:** wertbezogene Form je bekanntem Typ festlegen; Snapshot-Export ist nicht der verlangte Property-Copy. Referenznavigation separat behandeln.
2. **Große Werte:** budgetierten vollständigen Zugriffsweg sowie messbare Leistungsziele festlegen. Die konkreten Startwerte können anschließend gemessen werden; der Funktionsweg darf nicht erst während der Umsetzung erfunden werden.
3. **Wörtliche Property-Vollständigkeit:** Collection-/Skalar-Metadaten und bekannte Indexer im Vertrag explizit abdecken. Eine Beschränkung auf fachliche Properties wäre eine erkennbare Anforderungsänderung.
4. **Ereignismatrix:** historische Snapshots bei Editor-/Lease-Invalidierung behalten; echte Reset-/Kontextwechsel benennen. Das bestehende Null-Selection-Verhalten bewusst beibehalten.

R3-M02 ist eine konkrete technische Korrektur, keine zusätzliche Produktentscheidung. R3-L01 kann zusammen mit der korrigierten Dokumentation erledigt werden. Ein neuer Architekturapparat ist für keinen dieser Punkte nötig.

**Empfehlung für G2-04-02:** Noch keine Freigabe der unveränderten F1-Architektur. Nach dokumentiertem Abschluss von R3-H01/H02 und R3-M01/M02/M03 sowie Übernahme der Abnahmekriterien ist eine begrenzte Nachprüfung dieser Verträge ausreichend; der übrige Dock-/Host-Grundansatz muss nicht neu entworfen werden. R3 erteilt keine automatische Owner-Abnahme und startet keine Implementierung.

**Ergebnis: READY FOR OWNER REVIEW.**

## Anhang — Prüfbarkeit der Artefakte

SHA-256 der geprüften F1-Eingaben:

| Datei | SHA-256 |
|---|---|
| Architektur | `E34BB3437C37C46740EEF561F9228DD6A6B3382038E3E8F8E023544CDE1092AF` |
| Corrections-Report | `916C2634F03B83ED5776359AB53E278B5AEFE29CC60DD43AEA56EB266EB8684D` |
| HTML-Prototyp | `AA90F17692280310118B89690683FEB5D83AFA48782DBDADC72C4DE2D43AAF0E` |

DOM-freie Reproduktion von R3-H01: aus dem HTML ausschließlich die vier Zeilen mit Funktionsdeklarationen `valueOf`, `complete`, `serializeNode`, `payload` in einen leeren Node-VM-Kontext mit `calculations = new Map()` übernehmen; `payload` auf den oben dargestellten vollständigen KeyType-Knoten anwenden. Ergebnis: `type`-/`members`-Hüllen. Vergleich einer String-Leaf mit `" A\r\nΩ\n"`: unverändert. Dies prüft Payload-Erzeugung, nicht Betriebssystem-Clipboard oder Browserlayout.

Die .NET-Metadatenprüfung von R3-M01 nutzte `GetProperties(Public | Instance)` auf `List<string>`, `Dictionary<string,string>`, `ReadOnlyCollection<string>` und `byte[]`; keine Getter wurden ausgewertet. Das Projekt zielt ebenfalls auf .NET 10. Die Größenreproduktion in R3-H02 ist ein Konstruktor-/Arithmetiknachweis aus dem Quellcode; es wurde kein großer Share-Satz erzeugt.

[f1]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md
[f1report]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-F1-architecture-corrections-report.md
[html]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-visual.html
[f1copy]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md:165
[f1limits]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md:169
[f1inventory]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md:71
[f1graph]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md:91
[f1history]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md:139
[f1lifecycle]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-architecture.md:149
[htmlcopy]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-visual.html:79
[htmlkey]: C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F1/G2-04-01-variable-inspector-visual.html:63
[secretdescriptor]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:25
[secretset]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:77
[shareoperation]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Operations/SecretOperations.cs:11
[shareengine]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSharingEngine.cs:11
[sharevalue]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:17
[keyattrs]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/KeyVariableDeclaration.cs:73
[keydefaults]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/KeyVariableDeclaration.cs:21
[paramdict]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/ParameterVariableDeclaration.cs:112
[parambuild]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/ParameterVariableDeclaration.cs:121
[paramprint]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/ParameterVariableDeclaration.cs:184
[snapshotjson]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Serialization/SnapshotJson.cs:10
[tr31]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderDefinition.cs:13
[tr31format]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderDefinition.cs:118
[blockheader]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/BlockHeaderVariableDeclaration.cs:12
[kcv]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/KeyCheckValueCalculator.cs:13
[literalkey]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Expressions/Expression.cs:120
[declaration]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/VariableDeclaration.cs:6
[statement]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/Statement.cs:12
[capture]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs:11
[vmselection]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:206
[vminvalidate]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:145
[vmpublish]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:174
[vmdraft]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:91
[bindable]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/ViewModelBase.cs:13
[serviceobserver]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:68
[stateaccess]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:88
[mutation]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:111
[observe]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:347
[documentchange]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:165
[takecontrol]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:116
[hostreserve]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs:24
[revision]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs:19
[leasepublish]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs:139
[leaseacquire]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs:268
[assignment]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/AssignmentEvaluator.cs:103
[effects]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:544
[finalize]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:583
[dockdefault]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:42
[docksplit]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:196
[dockallowed]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:106
[dockadmission]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs:99
[dockcapture]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs:30
[dockstartup]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs:56
[dockreset]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs:214
[dockids]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellState.cs:9
[dockload]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:51
[dockdecode]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:118
[dockvalidate]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:230
[docksave]: C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:67
