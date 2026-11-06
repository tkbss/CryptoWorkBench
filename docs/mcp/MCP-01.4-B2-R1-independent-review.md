# MCP-01.4 – G-DESIGN-R1: kritischer Re-Review

Stand: 24.10.2026. **CHANGES REQUIRED / BLOCKED.**

Geprüft wurde die unveränderte [B2-R1-Fassung](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md), SHA-256 `E7D1AC8E7D4475ED3DC8F1EE7F23F0BF317C41333AD66AD908A39D920ECBF175`, 492 Textzeilen. Alle B2-Zeilenangaben dieses Berichts beziehen sich auf diese Fassung.

R1 verbessert die Verträge deutlich: historische Aktionen erhalten eine Bindung, Provider eine begrenzte Schreibfläche, Move eine Transferbestätigung und Fehler ein differenziertes Ergebnis. GD-06 und GD-10 sind auf Designebene erledigt; mehrere andere ursprüngliche Gegenbeispiele sind entschieden. Eine Gesamtannahme ist trotzdem nicht gerechtfertigt. Die zentrale Propertyintegration ist mit den beschriebenen öffentlichen Hooks noch nicht hinreichend begründet. Hinzu kommen konkrete Lücken bei historischem UI-Replay, Planübersetzung und Faulted-Nacharbeiten.

**Keine Implementierungs-, Paket-, Migrations- oder Integrationsfreigabe.** Fehlende Laufzeittests allein werden nicht als Designfehler gezählt. Wo ein eindeutiger Vertrag mit bekannten internen Mitteln implementierbar erscheint, ist dies ausdrücklich als plausibles Design bewertet.

## 1. Identität, Quellen und Nachweisgrenzen

### 1.1 Tatsächliche Reviewer-Unabhängigkeit

Reviewer ist der Codex-Chatassistent in **demselben Chat**, der zuvor B2 und B2-R1 verfasst bzw. überarbeitet hat. Es wurde kein anderer menschlicher oder separat beauftragter Reviewer eingesetzt. Eine neue Aufgabenstellung schafft keine personelle Unabhängigkeit. Das formale Kriterium „anderer Reviewer“ aus B2 §9.3 ist **nicht erfüllt**.

Dieser Bericht ist eine erneute kritische Quellenprüfung mit eigenen Gegenbeispielen; frühere Urteile werden nicht als Beweis übernommen. Seine negativen Befunde und überprüfbaren Quellenpfade sind nutzbar. Er ist keine unabhängige positive Abnahme. Die technischen Findings bestehen unabhängig von diesem formalen Mangel.

### 1.2 Arbeitsstand und tatsächlich geprüfter Umfang

Produktiver Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Der Chat-Arbeitsordner `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp` enthält die isolierten historischen Untersuchungen; die Produktdokumente liegen im genannten Worktree. Vorhandene uncommittete/unversionierte Arbeit wurde berücksichtigt und vorab mit 753 Dateihashes inventarisiert; Konfigurationsverzeichnisse wurden ausgeschlossen.

| Quelle | Prüfung und Bedeutung |
| --- | --- |
| [B2-R1](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md) | Vollständiger Vertrag §§1–12, alle zehn Dispositionen, alle 19 Compositezeilen, U0–U4, T1–T10; keine Änderungen |
| [G-DESIGN](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-independent-review.md) | GD-01–GD-10 samt Gegenbeispielen und Zusatzanforderungen; historische Zeilenangaben gelten nicht für R1 |
| [B1](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B1-feasibility.md) | Kern-API, Vor-/Nachwirkungsfehler, Replay, Sollmatrix, native Grenzen, Patch-/Test-/Logzuordnung |
| [U12](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-avalonia12-feasibility.md) | Releaseidentität, unveränderter Mutationskern, Framework-/Prism-/ExecutionContextgrenzen, native Nachweisgrenzen und getrennte Migration |
| [Editorentscheidung](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-editor-architecture-decision.md) | Variante B §§5–8, frühe Admission, vollständige Setter, kein geerbtes Callbackrecht, native Engine, E01–E14 und Raw-Ausschlüsse |
| [MCP-01.4-Review](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-review.md) | Entwicklung der R2/R4-Befunde, R6/R7 und abschließender BLOCKED-Nachtrag; kein Überschreiben der Produktentscheidung |
| [MCP-00](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-00-architecture.md) | Insbesondere §§4.3–4.5 und 5: Identität/Revision, No-op, Save, gemeinsame Admission, Fehlerprojektion, serviceeigene Cancellation/Drain |
| Original AvaloniaEdit 12.0.0 | TextEditor/TextArea/TextView, Caret/Selection, TextDocument/UndoStack/UndoOperationGroup, Completion, Search, Indentation, Clipboard/Drag und LineManager; gezielte statische Callgraphprüfung |
| B1-Patch, Testquellen und Logs | Kritische Gate-/Setter-/Replay-/Pasteabschnitte und Assertions erneut gelesen; gespeicherte Ergebniszahlen geprüft, keine Ausführung |
| Original Avalonia 12.0.0 | AvaloniaObject, AvaloniaProperty, StyledProperty, ValueStore, EffectiveValue, EffectiveValue<T>, PropertyNotifying und BindingEntryBase; vertiefte Property-/Bindinganalyse |

Die 362 Dateien der lokalen AvaloniaEdit-Originalbasis stimmen mit dem vorhandenen B1-Ausgangsmanifest überein (0 Abweichungen). Der feste AvaloniaEdit-Commit bleibt `86fdebec4cff7affb0e14c7885df71d28edce777`. Avalonia-Tag 12.0.0 wurde lesend auf **`220dea3f5e18002ff20a6603e70eb633eaa73b4c`** aufgelöst. Die untersuchten Frameworkquellen wurden unverändert in einen temporären Reviewordner geladen; die Links unten verwenden diesen festen Commit, nicht master. Das ist keine Paketinstallation und keine Machbarkeitsprobe.

B1-Patchhash: `2265133825B9ED3364EA6F2646FDCBF1D8DA7C384D6218C0E4F81A671AF394B9`. B1-Bibliothekstestquelle: `F6B2914678FC20D0DA6AD7209E146295129426FA73CF24D5416E4D9455A82390`. Die gespeicherten Logs melden 46/46 Boundary, 243/243 Bibliothek, 354/354 Upstream (0 Skips) und 9/9 Host. B1 lief unter Avalonia 12.1.4/net10.0; dies ist kein Laufzeitbeleg für das Propertysystem 12.0.0 oder für R1.

**Evidenzbegriffe:** S = konkreter statischer Quellenpfad; D = prüfbare Designregel; H = historischer B1-Lauf; O = offen. „Designseitig adressiert“ bedeutet weder implementiert noch getestet. Es gab keine Builds, Restores, Laufzeittests, GUI-Migration oder Ausführung der unten beschriebenen Probe. Keine vollständige Neuprüfung sämtlicher Avalonia-Backends oder aller historischen Hosttests wird behauptet.

## 2. GD-01–GD-10: Disposition

| Finding | Ursprüngliches Problem / R1-Lösung | Laufzeitsemantik, Machbarkeit, Testbarkeit | Disposition |
| --- | --- | --- | --- |
| GD-01 | Alle drei Documentoberflächen, Commitpunkt, Generation und Verbund-Faulted jetzt benannt | Reine Wertübernahme teilweise statisch abfangbar. Erhalt/Konsumierung von Bindingzuständen und Eintritt/Abschluss für alle Pfade nicht nachgewiesen; R1-01. Konkrete Probe in §4 | **OFFEN / BLOCKED** |
| GD-02 | Dokument + Bindungsgeneration, stale Skip und Zurückstellen vor Textwirkung lösen Nachfolger-/Vorwirkungsidee | A→B→A eindeutig. Die Koordinaten des am Replayende angewandten Kandidaten und sein Erfassungszeitpunkt sind noch nicht ausreichend festgelegt; R1-02 | **TEILWEISE ADRESSIERT** |
| GD-03 | AllowedEditAreas, CanInsert/Readonly, vollständige Materialisierung und Ablehnung des ganzen ungültigen Plans | Ursprünglicher Bereichsbypass geschlossen als D; statisch mit internen Replacepfaden vereinbar. Übersetzung zusammengesetzter Pläne und native Mappingtypen fehlen; R1-03 | **BEREICHSVERTRAG ADRESSIERT; PLANVERTRAG NOCH OFFEN** |
| GD-04 | Vollständiger Payload vor Move-Ack; interner Descriptor auch bei Teilwirkung konsumiert | Sicherheitsrichtung eindeutig und in den kontrollierten Drop-/Sourcepfaden umsetzbar. Fremdes Ziel liefert nur Transportbeleg, ausdrücklich keine Transaktion. Plattformnachweis offen, kein eigener verbleibender Designblocker | **DESIGNSEITIG ADRESSIERT** |
| GD-05 | Präfixabbildung des Anfangscarets, EmptySelection, kein vollständiger Endoffset bei Partial, Searchindex invalidieren | Ursprünglicher 202/102-Fehler entschieden. Zeitpunkt des Historybookkeepings und ClearAll bei Vorwirkungsfehlern widersprechen noch dem Gesamtabschluss; R1-02/R1-05 | **TEILWEISE ADRESSIERT** |
| GD-06 | Fremder using-Rumpf aus Aggregationsgarantie ausgenommen; explizites Sammelmuster beschrieben | Erfüllbarer Vertrag, keine fiktive Kenntnis des Callerfehlers. Bibliothekseigene Rümpfe weiter aggregiert; separat testbar | **DESIGNSEITIG ADRESSIERT** |
| GD-07 | Copy/Selection/Navigation, alle Phasen, MutationResult, externe/ungewisse Wirkung, eigener Cancellationgrund | Die ursprünglichen fehlenden Kategorien sind vorhanden. Direkter Task liefert Result, native Adapter melden einmal; Provider-OCE ist nicht automatisch Cancellation. Implementierung offen, kein bloß aus fehlendem Lauf abgeleiteter Fehler | **DESIGNSEITIG ADRESSIERT**, Präzisierungen §6 |
| GD-08 | Terminales Dokument-Faulted, kein Reset/Policybypass, Ersatz durch frisches Dokument | Grundrichtung korrekt. Quarantäne der angebundenen Views und Ausnahme zum Gruppenprüfpfad beim Ersatz fehlen; R1-04 | **TEILWEISE ADRESSIERT** |
| GD-09 | Synchroner Koordinator statt stets veraltendem Folgeauftrag; Tab/Enter konsumiert, Separator einmal | Eigener Phasenwechsel ohne Fremdrecht plausibel. Separator im Ergebnisraum passt noch nicht zum einheitlichen Snapshotplan; R1-03. Popupreaktionen während Commit zusätzlich testen | **TEILWEISE ADRESSIERT** |
| GD-10 | U1 enthält ausdrücklich vollständige funktionale B1-Basis einschließlich Input/Paste/Load/SelectedText | Baseline vor U2/U3 jetzt erreichbar beschrieben; keine isolierte Releasefähigkeit behauptet. U0-Abhängigkeit separat präzisieren, R1-06 | **DESIGNSEITIG ADRESSIERT** |

Keine Zeile erhält VERIFIED FIXED. Offene Implementierungsbelege werden nicht mit offenen Designentscheidungen gleichgesetzt.

## 3. Neue bzw. verbleibende Findings

Ergebnis: **0 Critical, 1 High, 4 Medium, 1 Low.** R1-01 blockiert die tragende Bibliotheksarchitektur. R1-02–R1-05 verlangen normative Präzisierung vor Gesamtannahme. R1-06 betrifft konsistente Gates und Reihenfolge.

### R1-01 — High: Bindingereignis und Propertyzustand sind am Coercion-Hook nicht vollständig kontrollierbar

**Fundstelle:** B2 §2.3 [Zeilen 96–106](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:96), insbesondere Punkt 5; §6 Zeile 346; U0 Zeile 366.

**S:** Avalonia ValueStore ersetzt ein lokales Binding durch Dispose des bestehenden Eintrags **vor** dem Start des neuen Bindings. Bei nichtlokalen Bindings speichert BindingEntryBase den angebotenen Wert, bevor ValueStore den effektiven Wert berechnet; identische weitere Werte werden dort unterdrückt. Der öffentliche Coercioncallback erhält nur Objekt und Wert, keinen Emissionszähler, keine Bindungsidentität und keinen Eintrittsgrund. Der notifying-Hook liegt erst nach der effektiven Übernahme.

**Konkrete Gegenbeispiele:**

1. Style-Binding emittiert B bei gesperrtem A. Coercion gibt A zurück. Nach Entsperren wird CoerceValue erneut gerufen: der gespeicherte ungecoercte B-Wert steht erneut zur Verfügung. Bloß erneute Policyprüfung würde den verbotenen verspäteten Wechsel erlauben. Eine Sperrliste für Wert B kann dagegen neue absichtliche B-Emissionen nicht zuverlässig erkennen: das unveränderte BindingEntryBase unterdrückt gerade eine erneute identische B-Emission vor diesem Hook.
2. Aus einem geschützten Callback wird ein bestehendes lokales Binding durch ein anderes ersetzt, das zunächst ebenfalls A liefert. Der Frameworkeintrag des alten Bindings kann bereits disposed sein, bevor die Bibliothek den ersten neuen Wert prüfen kann. Zustandsneutrale Abweisung des **Bindingersatzes** und Erhalt des bisherigen Bindings folgen nicht aus einem Documentwert-Veto.
3. Coercion kann ohne folgende effektive Wertänderung laufen (identischer Wert, niedrigere Priorität oder direkte StyledProperty.CoerceValue-Auswertung). Eine in Coercion reservierte Operation kann deshalb nicht pauschal erst in notifying(false) freigegeben werden; dieser Abschluss muss dann gar nicht kommen. Eine spätere Rekonfiguration darf den Verbund nicht dauerhaft Busy hinterlassen.

**Auswirkung:** Automatische Übernahme eines zuvor verworfenen Ziels, Verlust der bisherigen Bindingbeziehung oder hängenbleibende Reservation. Die geforderte Generation ist erst zuverlässig, wenn alle tatsächlichen Wechsel und Abbrüche diesen Ablauf korrekt durchlaufen.

**Erforderliche Korrektur:** Verbindlich festlegen, welche Propertyzustände ein Veto erhält und welcher konkrete Frameworkpfad Eintrittsgrund/Emissionsidentität sowie Abschluss liefert. Für den jetzigen Bibliothek-allein-Ansatz reichen die benannten öffentlichen Hooks nicht als Begründung. Ein eingeschränktes Bindingmodell, eine ausdrücklich entworfene Avalonia-Erweiterung oder eine andere Propertyoberfläche wären neue Architekturentscheidungen; sie dürfen nicht still als U2-Detail eingeführt werden. U0-Probe nach §4, keine pauschale Behauptung, sämtliche denkbaren Lösungen seien unmöglich.

### R1-02 — Medium: Zurückgestellte historische UI-Werte haben keine vollständige Endversionsabbildung

**Fundstelle:** B2 §2.5 [Zeilen 124–130](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:124), §3.1 Schritte 7/8 (164–165), §3.5 Zeile 258.

**S:** Original [TextArea:388–430](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/TextArea.cs:388) zeichnet bei UpdateStarted die damalige UI auf; dieselbe Aktion dient Undo und Redo. [Caret:174–198](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/Caret.cs:174) bildet die damalige Position durch folgende Dokumentänderungen ab. [UndoOperationGroup:48–82](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Document/UndoOperationGroup.cs:48) kehrt für Redo die Reihenfolge um. [UndoStack:385](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Document/UndoStack.cs:385) erlaubt PushOptional nur in einer offenen Gruppe.

**Gegenbeispiel:** Leeres Dokument, Caret 0, native Eingabe „x“, Undo, Redo. Die ursprüngliche optionale Aktion im Redo liegt vor dem Insert und enthält 0. Wird dieser Kandidat gemäß R1 erst **nach** dem Insert als bloßer Wert angewandt, landet der Caret bei 0; im ursprünglichen Ablauf wird er durch den Insert auf 1 fortgeschrieben. Dokument- und Bindungsgeneration sind in beiden Fällen gültig. Bei fortgesetzten Gruppen kann der letzte Kandidat außerdem zu einer Zwischenversion gehören. „Vor-/Nachversion“ benennt das Problem, definiert aber weder die Abbildung verbleibender Textteile noch getrennte finale Vor-/Nachzustände.

R1 verlangt zugleich, die tatsächlichen UI-Nacharbeiten in der Historie zu speichern, setzt diese jedoch hinter Undo-Gruppenabschluss. Ein späteres PushOptional ist dort nicht zulässig; eine neue Gruppe würde das Ein-Gruppen-Ziel verfehlen.

**Auswirkung:** Falscher Caret/Selection beim Redo oder unklarer Historyabschluss trotz korrektem Text. Stale-Skips lösen diese Fälle nicht.

**Erforderliche Korrektur:** Entweder Kandidaten mit konkreter Replayversion und nativer Abbildung der nachfolgenden Textschritte versehen, oder ausdrücklich getrennte finale Vor-/Nach-UI-Snapshots innerhalb des vorhandenen nativen Gruppeneintrags erfassen. Festlegen, wann dessen Nachzustand finalisiert wird und wie nachwirkende UI-Observerfehler erfasst werden, ohne zweite Gruppe/Redo-Verlust. Tests mit einfacher Eingabe, fortgesetzter Gruppe, Rechteck und mehreren TextAreas; Vorwirkungsabbruch weiterhin ohne UI-Anwendung.

### R1-03 — Medium: Gemeinsamer Plan vermischt Original- und Ergebniskoordinaten und lässt Mappingsemantik offen

**Fundstelle:** B2 §5.1 [Zeilen 320–326](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:320), §5.3 Zeile 338, Matrix IND/CMP/PRV.

**Gegenbeispiel D:** Snapshot „pri“ (Länge 3), Completion ersetzt [0,3) durch „print“, Endcaret 5, Trigger „(“. §5.3 verlangt einen zusätzlichen Insert bei 5 als Teil des gemeinsamen Plans; §5.1 verlangt gegen den ursprünglichen Snapshot gültige Bereiche und absteigende Anwendung. Offset 5 ist darin ungültig. Einfaches Umordnen löst weder Überlappung noch Readonlyprüfung. Analog benötigen die nach virtueller Newline berechneten Indentationedits eine verbindliche Übersetzung in den ausführbaren Plan; die Zuordnung ihrer Berechtigung allein reicht nicht.

**S:** Native [DefaultIndentationStrategy:38–44](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Indentation/DefaultIndentationStrategy.cs:38) verwendet RemoveAndInsert ausdrücklich für die Caretbewegung. [TextDocument:714–765](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Document/TextDocument.cs:714) unterscheidet Mappingarten. Ein reiner Offset/Length/Text-Plan ohne festgelegten Mappingtyp kann native Ankersemantik verändern, obwohl alle Schreibbereiche korrekt geprüft wurden.

**Auswirkung:** Der vorgeschriebene erfolgreiche Completionpfad kann durch die eigene Validierung abgewiesen werden oder verlangt eine nicht spezifizierte Sonderübersetzung. Unterschiedliche Implementierer können abweichende Anchor-/Caretresultate erzeugen.

**Erforderliche Korrektur:** Eine bibliothekseigene Normalisierung vor Gesamtvalidierung definieren: Separator in den betreffenden Ersatztext einarbeiten oder einen ausdrücklich phasenbezogenen internen Plan mit getrennten Basen und Abbildungen festlegen. Die resultierenden Schreibrechte erneut vollständig prüfen. Mappingart pro nativer Operation und die Nachposition nach Separator genau bestimmen; keine fremden ausführbaren Maps. Tests erwarten „print(“, Caret 6, einen Undo und exakte Anchorpositionen; ergänzend Newline plus Einrückung mit Readonlygrenzen.

### R1-04 — Medium: Faulted-Dokument ist gesperrt, seine aktiven Verbraucher und der Ersatzpfad sind es noch nicht vollständig

**Fundstelle:** B2 §3.6 [Zeilen 264–272](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:264), §2.3 Punkt 1, §3.1 Faulted-Zustand.

**Gegenbeispiel:** Ein Enginefehler hinterlässt unbekannte Gruppeninvarianten, die Bibliothek setzt IsFaulted. Der Fehlerempfänger protokolliert und behandelt den Fehler; ein frisches Ersatzdokument wird erst später bereitgestellt. Die weiterhin angehängte TextView/Caret-/Completioninfrastruktur erhält Measure-/Position-/Renderingarbeit und versucht Dokumentzeilen zu lesen. R1 verbietet diese Abfragen, definiert aber keine Stilllegung dieser automatischen Verbraucher. Original [TextView:957](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Rendering/TextView.cs:957), [TextView:1730](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Rendering/TextView.cs:1730) und [Caret:235](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/Caret.cs:235) setzen einen verwendbaren Dokument-/Zeilenzustand voraus.

Zweites Gegenbeispiel: Der Ersatz aus §3.6 soll ohne alte Engineabfragen möglich sein; §2.3 fordert vorher Prüfung offener Gruppen. Bei gerade nicht gesicherten Gruppenzählern bleibt unklar, ob dieser konkrete Ersatz dennoch zulässig ist oder dauerhaft an der allgemeinen Vorbedingung scheitert.

**Auswirkung:** Wiederholte spätere UI-Exceptions oder kein spezifikationskonformer kontrollierter Ersatz. Das ist kein Anspruch auf Reparatur einer defekten Engine, sondern die notwendige Grenze ihrer weiteren Verwendung.

**Erforderliche Korrektur:** Quarantäne aller angebundenen Views im selben Faultübergang festlegen, sichere Anzeige/Diagnose und zulässige Subscriptionbereinigung benennen. Aktiven Operationsbesitz von unbekannter alter Enginetiefe trennen; Ersatz nach Ende des logischen Vorgangs explizit erlauben, ohne die defekte Tiefe als gesund zu erklären. Aufbewahrte Dokument-/Line-/Anchorreferenzen und Verbund-Faulted getrennt behandeln. Tests müssen zwischen Fehler und Ersatz einen UI-Zyklus zulassen und unbekannte alte Gruppentiefe injizieren.

### R1-05 — Medium: Unbedingte Textsetter-Nacharbeit widerspricht Vorwirkungsabbruch und B1-Baseline

**Fundstelle:** B2 §3.5 [Zeile 260](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:260), §3.1 Schritte 4/8, §7 U1-Baseline.

**Gegenbeispiel:** Editor besitzt Undo-Historie; zulässiger Textsetter öffnet eine Gruppe, aber UpdateStarted oder erstes Changing wirft vor Textwirkung. §3.5 bezeichnet Textsetter-ClearAll auch nach Observerfehlern als verpflichtend. Bei wörtlicher Anwendung wird die bestehende Historie gelöscht, obwohl der Textschritt nicht begonnen hat. Der historische B1-Test [setter-finalization](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/library/test/MutationContractTests/Program.cs:145) verlangt in genau diesen Fällen unveränderten Text und vorhandenes Undo.

**Auswirkung:** Entweder Verletzung der zugesagten B1-Baseline oder stillschweigende zusätzliche Voraussetzung in der Implementierung. Zulässiger identischer Textsetter ist davon zu unterscheiden: Bei regulär abgeschlossenem No-op darf er weiterhin ClearAll ausführen.

**Erforderliche Korrektur:** ClearAll/Caretreset an „zugelassener und regulär abgeschlossener Textschritt einschließlich regulärem No-op“ binden; bei Vorwirkungsfehler unterlassen. Bei nachwirkendem Observerfehler weiter ausführen. Dasselbe Kriterium für Load/SelectedText-Erfolgsnacharbeiten explizit prüfen. Keine B1-Assertions ändern.

### R1-06 — Low: Gateformulierungen sind noch nicht vollständig mit den neuen Grenzen vereinbar

**Fundstelle:** B2 T3/T10 und allgemeines Fehlersoll [Zeilen 416–427](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:416), U0 Zeile 366, §9.3/§11.

**Gegenbeispiele:** T3 verlangt für „jede §3-Fläche“ einen später erreichten Handler, obwohl §3.2 den fremden Avalonia-Fanout ausdrücklich ausnimmt. Das allgemeine Fehlersoll verlangt Folgeedit und Text-/Versionabfrage, obwohl §3.6 sie bei Faulted verbietet. T10 nennt Kostenmessung ohne Workloads/Grenzwerte; jede Messzahl könnte so als bestanden gelten. U0 fordert positive Propertymachbarkeit und positiven R1-Re-Review, während die nächste Implementierungsarbeit erst nach Designannahme stattfinden soll; eine für diese Annahme nötige isolierte Probe braucht eine getrennte Freigabegrenze.

**Erforderliche Korrektur:** T3 in kontrollierten und Frameworkfanout teilen; Faulted-Gegenproben auf Quarantäne/Ersatz statt Folgeedit am selben Objekt ausrichten. Messmaßstab vor T10 festlegen. U0 in spezifizierte, gesondert beauftragte Machbarkeitsprobe und nachfolgende Serienarbeit aufteilen. Diese Runde beschreibt die Probe nur und führt sie nicht aus.

## 4. GD-01: technische Prüfung am Avalonia-Propertysystem

### 4.1 Tatsächliche Architektur und verfügbare Hooks

Alle drei Documentproperties verwenden dieselbe StyledProperty-Identität, aber **verschiedene objektlokale effektive Werte**. AddOwner registriert die Property auf einem weiteren Typ; es bildet keinen Mehr-Objekt-Commit. Original TextEditor:106 und TextArea:265 verwenden AddOwner, TextView:103 registriert die Property. Ihre eigenen Changed-Subscriptions führen die Bindung kaskadiert weiter. Der künftig geplante Coordinator müsste diese interne Verdrahtung ersetzen, einschließlich der TextView-Ressourcen; ein CLR-Setterguard deckt direkte AvaloniaObject-Zugriffe nicht ab. [StyledProperty.AddOwner](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/StyledProperty.cs#L64).

Vorhanden sind öffentlich registrierbare Coercion und beim entsprechenden Register-Overload ein notifying-Callback. ValidateValue erhält dagegen nur den Wert und eignet sich nicht als dokument-/ownerabhängige Policy. Notifying ist eine Registrierungsoption, keine nachträglich beliebig austauschbare Transaktions-API. Die Bibliothek könnte die eigene ursprüngliche Registrierung ändern; der bestehende Propertytyp darf dabei für Binär-/XAMLkompatibilität nicht still ersetzt werden. [AvaloniaProperty.Register](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/AvaloniaProperty.cs#L300).

Der relevante effektive Wertpfad führt Coercion vor Value-Zuweisung aus. Ungecoercte Werte werden zusätzlich aufbewahrt und bei Recoercion erneut angeboten. Coercion erhält keine Information darüber, ob gerade eine Emission, eine aktuelle Wertüberschreibung oder eine Neubewertung stattfindet. Derselbe Aufruf kann auch ohne effektive Änderung enden. **S:** Ein Veto gegen einen einzelnen normalen effektiven Wertwechsel ist möglich. **Nicht belegt:** der komplette Emissions-/Reservationvertrag aus R1. [EffectiveValue<T>](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/EffectiveValue%601.cs#L204).

Im nichtlokalen Bindingpfad speichert BindingEntryBase einen geänderten Quellwert vor der Weitergabe und filtert gleiche Werte. Damit ist eine beobachtete Quellemission nicht gleich einem Aufruf der Propertycoercion. Ein Emissionsmerkmal, das nur der Bibliothekscoordinator zählt, kann diese unterdrückten Emissionen nicht rekonstruieren. [BindingEntryBase.SetValue](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/BindingEntryBase.cs#L125).

ValueStore verändert bei Bindingersatz bzw. Neubewertung außerdem vorgelagerte Binding-/Prioritätsstrukturen. ClearValue setzt Überschreibungszustand zurück; ReevaluateEffectiveValue beginnt eine Neubewertung und kann bei Throw deren weitere Arbeit verlassen. Deshalb sind „alter effektiver Wert blieb sichtbar“ und „bisheriger Property-/Bindingzustand ist vollständig erhalten“ verschiedene Nachweise. [ValueStore](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/ValueStore.cs#L47), [EffectiveValue.BeginReevaluation](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/EffectiveValue.cs#L77).

Nach Value-Zuweisung umschließt PropertyNotifying.Start die anschließende Benachrichtigung. Sein Dispose kann einen internen Abschluss auch nach einer Exception im RaisePropertyChanged-Pfad ermöglichen. Wirft aber bereits notifying(true), ist die verwendete Disposable-Instanz noch nicht erfolgreich entstanden; notifying(false) ist dann nicht als Cleanup garantiert. Der interne Einstieg muss deshalb selbst nichtwerfend bzw. eigenständig ausnahmesicher gestaltet werden. **Das ist ein plausibler Baustein für Commitfinalisierung, kein universeller Binding-Transaktionsbeweis.** [PropertyNotifying](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/PropertyNotifying.cs#L17).

RaisePropertyChanged ruft zuerst OnPropertyChangedCore, dann Property-Changed-Fanout, Objekt-PropertyChanged und INPC. Ein früher Throw kann spätere Empfänger verhindern. Die geforderte interne Finalisierung darf deshalb nicht als späterer Empfänger dieser Kette implementiert werden. R1s Zulassung vorübergehend gemischter Frameworksicht ist ehrlich; sie ist mit einem expliziten gesperrten Commitfenster prinzipiell vereinbar. [AvaloniaObject.RaisePropertyChanged](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/AvaloniaObject.cs#L783).

### 4.2 Bewertung der sechs Pflichtfragen

| Frage | Statische Antwort | Grenze |
| --- | --- | --- |
| 1. Gemeinsame Admission vor erster effektiver Übernahme? | **Teilweise machbar:** gemeinsame Metadaten-/Coercionlogik für die drei Owner kann normale effektive Wechsel prüfen; Coordinator kann die beteiligten Dokumente reservieren | Vollständiger Eintritt/Abschluss auch ohne EffectiveChange sowie ClearValue-/Prioritäts-/Bindingersatzfälle nicht durch den benannten Hook allein erklärt. Frühere Bindingnebenwirkungen gesondert behandeln |
| 2. Abgewiesene Bindingwerte ohne späteres Nachholen? | **Mit bloßem Coercion-Veto nicht erfüllt.** Gespeicherte Quell-/ungecoercte Werte bleiben verfügbar | Keine hinreichend begründete Bibliothek-allein-Lösung für Konsumierung plus neue identische Emission. Hier liegt eine wesentliche offene Implementierbarkeitsannahme, R1-01 |
| 3. Interne Bindung trotz Frameworkthrow finalisieren? | **Plausibel:** eigener Commitcode und nichtwerfender notifying-Einstieg, Abschluss im garantierten Disposepfad des effektiven Notify | Sämtliche effektiven Übergänge/Defaultwerte und verschachtelten Memberwechsel müssen erfasst sein. Direktes Subscriber-Finalizing genügt nicht. Originale Ressourcen-/Subscriptionpflege muss aus der alten Changed-Kaskade herausgelöst werden |
| 4. Reentrancy über alle drei Oberflächen verhindern? | **Für effektive Wertwechsel plausibel**, wenn Prüfung an gemeinsamer Propertyintegration sitzt | Bindingersatz/-entfernung und nicht wirksame Wertangebote sind nicht identisch mit einem effektiven Wechsel; nachträgliche Guardwirkung reicht dort nicht für behauptete Zustandsneutralität. Keine beliebigen fremden Overrides als Core privilegieren |
| 5. Bindungsgeneration einschließlich A→B→A? | **Zähler selbst eindeutig implementierbar:** pro logischem Commit einmal; A→B→A ergibt zwei Erhöhungen; Detach/Reattach zusätzlich | Zählung darf nicht pro drei kaskadierten Propertymeldungen erfolgen. Voraussetzung ist lückenloser Commit-/Abbruchpfad. Ein abgewiesener Wechsel darf nicht als erfolgreicher Commit zählen |
| 6. Legacy ohne Policy erhalten? | **Plausibel, nicht automatisch:** Strictzweig nur bei alter/neuer Policy oder aktivem striktem Übergang, sonst ursprünglicher Ablauf | Gemeinsame Registrierung, Exceptions, Coercionnebenwirkungen und Bindingprioritäten benötigen Differentialtests. 354 B1-Tests ohne neue Integration beweisen dies nicht |

**Einordnung:** Keine Behauptung, der ganze Verbund sei technisch unmöglich. Statisch belegt sind konkrete Hooks und ihre Grenzen. Plausibel sind interne Wert-/Ressourcenfinalisierung und monotone Generation. Nicht hinreichend belegt und mit einer einfachen Hooklösung nicht erfüllbar ist der vollständige Vertrag zur Konsumierung von Bindingwerten und zur zustandsneutralen Abweisung sämtlicher Propertyzugriffe. Eine ungeklärte Frameworkerweiterung wäre eine neue Abhängigkeit für U0–U4 und Distribution, keine bloße spätere Optimierung.

### 4.3 Minimale isolierte Machbarkeitsprobe — nur spezifiziert, nicht ausgeführt

Eine gesondert beauftragte Probe soll zunächst **nur Avalonia 12.0.0** am festen Commit verwenden: drei kleine AvaloniaObject-Owner derselben StyledProperty, einen minimalen Coordinator, zwei dokumentartige Identitätsobjekte, steuerbare Observablequellen und eine Ereignisspur. Keine CryptoWorkBench-/Prism-/MCP-Abhängigkeit, keine B1-Änderung. Erst nach erfolgreichem Hooknachweis wird die identische Probenmatrix gegen die tatsächlichen TextEditor/TextArea/TextView-Ressourcen geführt. Phase 1 beweist keine TextView-Integration.

Die Instrumentierung protokolliert Angebot, Admission, effektive Werte aller drei Owner, Bindingaktivität, Commitgeneration, Coordinatorphase, notifying(true/false), Observerfehler und Reservationfreigabe. Interne Testinstrumentierung darf den Ablauf beobachten; sie darf fehlende Produkt-Hooks nicht durch private Reflection implementieren.

| Fall | Konkrete Assertions |
| --- | --- |
| P01 normale Zuweisungen | Je Mitglied CLR, SetValue und SetCurrentValue A→B: eine logische Zulassung je unterschiedlichem Dokument, eine Generation, danach alle drei B; Denied: alle A, Generation unverändert, Folgeversuch möglich |
| P02 No-op / nicht wirksames Angebot | Identischer Wert, niedriger priorisierter Stylewert, direkte StyledProperty.CoerceValue-Auswertung: keine offene Reservation ohne folgende Notifyphase; effektiver Verbund unverändert |
| P03 ClearValue | Lokales A über Style-B entfernen, sowie A→Default/null: frühes Veto hält effektive Bindung; auch Priorität/CurrentValue-Verhalten des Folgezugriffs ist festzuhalten, nicht nur GetValue |
| P04 gleiche Emission | Stylequelle B unter Denied, danach Unlock, Recoerce, danach **erneute B-Emission ohne Zwischenwert**: Recoerce darf B nicht nachholen; neue Emission muss gemäß gewähltem Vertrag eindeutig behandelt werden. Prüfen, ob sie den Hook überhaupt erreicht |
| P05 neue Emission nach Zwischenwert | Nach abgewiesenem B emittiert Quelle A, dann B: neuer expliziter Versuch muss von stiller Neubewertung unterschieden werden |
| P06 Bindingersatz und Dispose | Lokales Binding A durch neue Quelle B bzw. gleiche Quelle A ersetzen; Subscriptionzähler vor/nach Veto vergleichen. Bindingdispose bei gesperrtem Dokument darf keinen heimlichen Wechsel erzeugen; verbleibende Bindingsemantik ausdrücklich prüfen |
| P07 Prioritäten | Style/Local/Animation und CurrentValue kombinieren; verdecktes Angebot, Entfernen höherer Priorität, Recoercion; kein ungeprüfter späterer Commit, keine doppelte Generation |
| P08 Frameworkthrow | Erster Property-/Objekt-/INPC-Empfänger wirft jeweils: eigene Finalisierung läuft, alle internen Mitglieder neu oder explizit Verbund-Faulted; kein erfolgversprechender halb gebundener Zustand. Spätere fremde Empfänger sind nicht generell zu verlangen |
| P09 Reentrancy | Jeder Callback versucht Wechsel über jeden der drei Owner und jede P01/P03/P06-Operation: Ablehnung vor zugesagter Wirkung; kein neuer Commit/keine zusätzliche Generation; äußerer Abschluss bleibt möglich |
| P10 ABA / Attach | Pending-Marker auf A, A→B→A und Detach/Reattach: alter Marker ungültig; normaler identischer No-op ohne Detach erhöht nicht |
| P11 Postcommit-Mitgliedsfehler | Interne Ressource für zweites/drittes Mitglied scheitert: kein Rückfall in Stable, keine Bearbeitung über Verbund, kontrolliertes Verwerfen möglich |
| P12 Legacy | Gleiche Propertyfolge ohne Policy gegen unverändertes Original: effektive Werte, Subscriptionverhalten, Reihenfolge und Exceptions gleich; keine neue globale strikte Fehlerzustellung |

Abbruchkriterium: Liefert kein verfügbarer Hook die zur Umsetzung von P04/P06 notwendige Information bzw. Kontrolle, wird die Bibliothek-allein-Variante **nicht** durch Testlockerung akzeptiert. Dann ist eine konkrete API-/Frameworkalternative zu entwerfen und erneut zu reviewen. Diese Probe wurde nicht angelegt, gebaut oder ausgeführt.

## 5. Öffentlicher API-, Zustands- und Eventvertrag

| Vertragsfläche | Bewertung dieses Re-Reviews |
| --- | --- |
| IMutationPolicy / frühe throw-or-return-Admission | Generisch und mit B1-Kern vereinbar. Keine Host-/MCP-Typen. Direkter synchroner Policyfehler bleibt ursprüngliche Exception |
| MutationKind 0–22 / EndUpdate reserviert | Copy, Selection und Navigation schließen alte Lücken; Diagnosekind darf ohne Textpolicy existieren. Completion nach TextInput benötigt im späteren APItext ausdrücklich den endgültigen Result-Kind Completion |
| Reentrancy / Policyrekursion | Harte Abweisung trotz zustimmender Policy, keine rekursive VerifyMutation-Schleife, kein ambient Recht: eindeutig. Fremdcallback kann andere unbeteiligte Dokumente nicht durch diese lokale Grenze global sperren; Host koordiniert dies |
| Policyaktivierung/-wechsel/-deaktivierung | Nur idle; offene Gruppen und Callback verhindern Wechsel. Ein veränderter Entscheidungszustand derselben Policy ist vom Policysetter getrennt. EndUpdate bleibt auch dann verpflichtend |
| Stackownership / Historienaktivierung | Vollständige Dokumentzuordnung und Prüfung beider Stapel einschließlich rekursiver nativer Gruppen sind machbar. Shared vorher/nachher und unbekannte historische Aktionen fail-closed. Keine implizite Historienlöschung; Bindungsstempel in alten optionalen Einträgen sind nicht rückwirkend zu erfinden |
| Lifetime / fehlendes Document.Dispose | Vertretbar. Weak-Ziel allein genügt nicht, aber Generation ist jetzt vorgesehen. Pending Tasks und Weak-Subscriptions benötigen Retentiontests; keine Hostshutdown-Autorität in der Library |
| Documentwechsel aller drei Owner | Spezifizierter Verbund ist klarer, technische vollständige Abdeckung bleibt R1-01. Derselbe Policytyp an zwei Dokumenten muss zweimal mit passendem Document geprüft werden |
| Begin/RunUpdate/End/Dispose | Gruppierung statt Autorisierung korrekt. Public End aus Callback/fremdem Thread bleibt falsch; Dispose höchstens einmal. Raw-Pairing kann keine Absicht fremden Codes erkennen; unterstützte Paare müssen deterministisch bleiben |
| Historische UI-Aktionen | Dokument-/Generationsbindung und stale Skip korrekt; Endversionsabbildung und Speicherung des Nachzustands R1-02 |
| Idle/Admission/Preparing/Updating/Finalizing/Publishing | Für synchrone Operationen plausibel. Async hält nur Daten, keine Gruppe oder Coreautorität. Faulted ist terminal; Recoverypfad R1-04 |
| UpdateStarted / Changing / IDocument.TextChanging | Aktuelles kontrolliertes Fanout abschließen; vor erstem Textcommit stoppen; eigene Gruppe dennoch schließen. Normales Präfixstoppen und nativer Replay nach erster Wirkung bewusst verschieden |
| Changed / IDocument.TextChanged / TextChanged / Properties | Notwendige interne Enginefortschreibung vor Fehlerpublikation; partielle Eventordnung sinnvoll. Keine globale Sortierung fremder Frameworkmeldungen versprochen |
| UpdateFinished / logische Nacharbeit | Reentrancygrenze bleibt über IsInUpdate=false hinaus. Voraussetzungen für ClearAll sind noch R1-05; UI-Historytail R1-02 |
| Undo-/Redo-Notifications | Native Liste/State finalisieren, keine unbekannten IUndoableOperation unter Corerecht. Observerthrow vor erstem Textteil zurücklegen, danach native Gruppe fertig; Enginefehler anders behandeln |
| Eigenständige Caret-/Selectionevents | Eigener Callbackschutz und eigener kontrollierter Fanout plausibel; keine Textpolicy für Navigation nötig. Navigation invalidiert pending Auswahlbindung |
| Anchors | Native Offset-/Deletion-/Biassemantik beibehalten, verzögerte Events nach konsistenter Engine. Planmapping muss dazu ausdrücklich passen, R1-03 |
| LineTracker | „Snapshot je Zustellung, Listenänderung ab nächster Zustellung“ ist eine konkrete Regel. Ein neu registrierter Tracker kann dadurch mitten im Änderungszyklus beginnen; dies ist keine vollständige Zustandsinitialisierung. Eigene Tracker müssen vorher initialisiert sein bzw. erst an sicherer Grenze registriert werden. Gegenprobe für BeforeRemoveLine→ChangeComplete erforderlich |
| Weak-Events | Eigene Empfänger einzeln, Abmeldung/GC und Reihenfolge bei Snapshot beachten; keine permanente starke Retention. B1 belegt nur bestimmte Fälle |
| Provider/TextSource/Iterator/Dispose | Fremdcode wird vor Commit materialisiert und erhält keine Coreautorität. Endgültige versiegelte Planwerte statt ausführbarer Fremdgetter ist technisch geeignet |
| TextEntering/Entered/Copied/Pasted | Eigene Callbackgrenze vorhanden. Erfolgsnachrichten erst bei erfülltem Schritt; Completioneingabe §5.3 ist bewusst additive Strictsemantik, Planlücke R1-03 |
| Framework-/Routed-Fanout | Ehrliche Grenze: spätere fremde Empfänger können ausfallen. Eigene Invarianten müssen trotzdem finalisieren; T3 darf nicht mehr verlangen |
| MutationOperationException / Callbackunterklasse | B1-Typ bleibt AggregateException-abgeleitet. TextChanged bedeutet nur bestätigten Textcommit; TextEffect=Possible verlangt Resultauswertung und darf nicht auf wirkungsloses Busy projiziert werden |
| Manuelle Gruppen / fremde using-Rümpfe | GD-06 erfüllt: keine überzogene Fehleraggregation. Explizites Callersammeln möglich, interne Compositeaggregate weiterhin vollständig |
| Faulted / Ersatz | Terminale Sperre ist klar, fehlende Quarantäne-/Gruppenausnahme R1-04. Kein neues Dokument als heimlicher Textrollback |

Die strikte Start-/Abschlussgrenze darf auch nicht durch erstmalige Policyaktivierung in einem laufenden Legacycallback umgangen werden. B2 §2.2 verbietet dies bereits; die nötige Beobachtung solcher Phasen muss auch ohne aktive Policy existieren, ohne Legacycallbackmutationen generell zu verbieten. Dies ist implementierbar, aber durch B1s derzeitigen Callbackzähler allein nicht belegt.

## 6. Ergebnis-, Cancellation- und Providerbewertung

Die neuen Resultkategorien beantworten die früheren GD-07-Beispiele:

- Paste bereits vor Vorbereitung abgelehnt: Rejected, Admissionfehler, keine eigenen Text-/Historyeffekte.
- Copywrite fehlgeschlagen: Kind Copy, ExternalTransferfehler, ExternalEffect je nach bekanntem Transportausgang; TextEffect=None behauptet keine vollständige Wirkungslosigkeit.
- Cutwrite bestätigt, Source inzwischen stale: Cancelled mit eigenem Stalegrund, ExternalEffect=Confirmed, keine lokale Löschung. Der native Adapter darf dies nicht still als wirkungslosen Cancel verstecken.
- Provider wirft OperationCanceledException: Failed, solange der Coordinator keinen eigenen kontrollierten Abbruch festgestellt hat. Gleicher Token allein genügt nicht.
- Interner Fehler vor bekanntem Textcommit: Possible/Unknown/Faulted; nach einem nachgewiesenen Präfix Applied/Unknown/Faulted. Kein frischer Snapshot einer unsicheren Engine als Diagnosepflicht.
- Fehlerzustellungs-Observer wirft: ursprüngliche Ursachen plus Publicationfehler einmal an Fallback; Handled kann diese zusätzlichen Fehler nicht verschlucken. Keine zweite MutationFailed-Schleife.

Das Result ist eine lokale Diagnoseoberfläche, kein öffentliches Transport-DTO. Defensive Fehlerlisten sind strukturell unveränderlich; referenzierte Exceptionobjekte und TextDocument sind keine tief unveränderlichen Fachsnapshots. Das ist für lokale Diagnose vertretbar, muss aber beim API-Freeze so benannt werden.

Noch zu präzisieren, ohne daraus einen nicht belegten Laufzeitfehler zu erfinden: Result bei Publicationfehler als neuer finaler Datensatz statt nachträglicher Mutation bereits zugestellter EventArgs; Fehlerpriorität bei gleichzeitig beobachtetem Transportfehler und eigener Cancellation; endgültiger Completion-Kind nach TextInput-Preflight. Ein direkter Taskaufrufer muss Failed/Rejected/Cancelled tatsächlich auswerten; ein erfolgreich beendeter Task allein ist kein Operationserfolg.

Providerflächen sind nun wirksam begrenzt. Unzulässiger letzter Edit verwirft das ganze Fremdplanangebot; Readonly-Iteratorfehler und Dispose sind noch Vorbereitungsfehler. Native partielle Deletegeometrie ist davon getrennt. Freie Raw-TextDocument-APIs bleiben bewusst außerhalb der Editor-Readonlysemantik. Eine einzelne Policyprüfung vor den Providercallbacks ist weiterhin keine Erlaubnis für deren öffentliche Mutationen. Offene Punkte sind die Ausführungsübersetzung und native Mappingarten aus R1-03, nicht das Grundprinzip eines Datenplans.

## 7. Sämtliche 19 Composite-Matrixzeilen

Gemeinsame Bewertung: frühe Admission vor eigener sichtbarer Vorbereitung, geschützte Fremdcallbacks, festgehaltene ursprüngliche Dokumentreferenz und ein interner synchroner Commit sind angemessene Grundregeln. Für Async gilt A einschließlich Version und Generation, nicht nur Referenzvergleich. Die nachfolgende Einzelprüfung ersetzt diese gemeinsamen Anforderungen nicht.

| B2-Zeile / Operation | Admission und Reentrancy | Bindung, Neben-/Teilwirkung, History / Urteil |
| --- | --- | --- |
| 290 Overstrike | TextInput vor TextEntering und Auswahlvorbereitung; internes Ersetzen statt öffentlichem SelectRight ist richtig | Abweisung bewahrt UI. Reguläre Positivgeometrie/Unicode prüfen; Nachwirkungshistorie R1-02. Grunddesign plausibel |
| 291 Backspace/Delete | Gate vor Readonlyenumeration und Live-Auswahl; fremde Iteratoren geschützt | Native erlaubte Segmente, Präfixstoppen und EmptySelection bei Teilwirkung klar. Tatsächlicher UI-Historytail noch R1-02 |
| 292 Tab/Shift-Tab/Newline | Indent/TextInput vor Provider und Newline; kein öffentlich privilegierter Indent-Aufruf | Vorvalidierung aller Teile sinnvoll; Post-Newline-Koordinaten und RemoveAndInsert-Mapping R1-03. Noch nicht vollständig |
| 293 Copy | Keine Textpolicy, aber eigener Callbackschutz; Taskabschluss beobachten | Kind/ExternalEffect jetzt darstellbar, keine Textgruppe. Keine Garantie über fremde Callback-I/O; designseitig brauchbar |
| 294 Cut | Cut vor Kopiercallbacks/Write, erneut vor Löschung | Keine Gruppe über await; erfolgreiches Clipboard plus verworfene Löschung explizit sichtbar. A→B→A hängt an GD-01. Kein eigener verbleibender Cut-Vertragsfehler |
| 295 Paste | Preflight, Materialisierung, A, erneute Admission, TextPasted noch geschützt | Ursprüngliches Ziel und Präfixabschluss richtig; Result verständlich. Vollständige Bindinginvalidierung R1-01, Historytail R1-02; B1 drei Fälle reichen nicht |
| 296 Externer DragMove | Vor Dragvorbereitung, Sourcecommit später erneut zulassen | SourceDeleteAttempted und bestätigter Transfer verhindern doppelte/falsche Löschung; Source-stale hinterlässt Kopie. Fremder Movebeleg hat ausdrücklich begrenzte Verlässlichkeit |
| 297 Drop/interner Move | Vor Zielcaret/History; interner Plan ohne Callbackrecht | Zieleinfügung vor umgerechneter Source-Löschung, konsumierter Descriptor bei jedem Ausgang: ursprüngliche GD-04-Lücke geschlossen. Teilpräfix bleibt, genau eine native Gruppe; Mapping-/UI-Historytests erforderlich |
| 298 Move zwischen Dokumenten | Zwei lokale Admissions; kein SharedStack als Scheintransaktion | Teilziel bestätigt kein Move, vollständiges Ziel mit Source-Denied bleibt Kopie. Richtiger lokaler Vertrag, Plattformbeleg offen |
| 299 Search/FindNext/Previous | Navigation ohne Textpolicy; Auswahlcallbackedit verboten | Darf pending Auswahl veralten lassen; kein Text-/Undo-Effekt versprochen. Plausibel |
| 300 ReplaceNext | Gate vor FindNext-/Index-/Selectionwirkung; Treffer immutable planen | Kein Treffer ohne vorbereitende sichtbare Änderung; Refreshfehler invalidiert Index. Präfix-UI sinnvoll, Historytail R1-02 |
| 301 ReplaceAll | Ein kompletter validierter Trefferplan, keine Neusuche zwischen Schritten | Nach Changedfehler angewandtes Präfix, geschlossene native Gruppe, tatsächlicher Caret und leerer Suchindex bei Refreshfehler. Klarer Teilwirkungsvertrag; R1-02 bleibt |
| 302 Completion | Eigener synchroner Koordinator; Proposal kein Corerecht, Completionprüfung nach TextInput | Kein selbstveraltender Dispatcherauftrag mehr; Separator-/Planübersetzung R1-03. Eigenes Caret-/Popupauto-close während Commit separat prüfen; noch nicht vollständig |
| 303 Simple/Empty/RectangleSelection | Direkter Kind Selection, Nativecore und Fremdoverrides getrennt | Readonly/virtuelle Geometrie vorab, Partial kollabiert Auswahl. Richtige Bereichsgrenze; Mapping und Historytail R1-02/R1-03 |
| 304 Navigation und Nacharbeiten | Navigation eigener Callbackschutz, Edittail hält äußere Grenze | Neue Bindung darf alte Nacharbeit nicht übernehmen. Historisches Replay und Endzustand noch R1-02; Faulted-Verbraucher R1-04 |
| 305 Anchors/LineTracker | Native Algorithmen, keine neuen öffentlichen inneren Edits | Bestehende Anchorsemantik setzt festgelegtes Planmapping voraus, R1-03. Trackerwechsel mitten im Zyklus ausdrücklich als Teilzustellung testen; keine Reparatur fremder Tracker versprochen |
| 306 Indentation/Readonly/Provider | Fremdcode vollständig vorbereitet, Gesamtvalidierung vor Gruppe | GD-03-Bereichsbypass adressiert. Post-Newline-Ausführung und Mapping noch R1-03; kein live Getter im Commit |
| 307 Undo/Redo/optionale Historie | Vor Stackbewegung zulassen, nur native Replayteile | Vor Text Eintrag zurück, danach bei Observerfehlern vollständiger Replay. Stale-Aktion skip korrekt; nichtstale Koordinaten/Historytail R1-02 |
| 308 Text/Clear/SelectedText/Load/Append/Document | B1-Kern und vollständige Settergrenze bewahren; Stream Fremdcode | Raw-Readonlygrenze bewusst getrennt. Vorwirkungs-ClearAll R1-05; Faulted R1-04; keine Behauptung globaler I/O-Rücknahme |

Damit sind **19/19 Zeilen** in diesem Re-Review bewertet; keine Zeile wird als neuer ausgeführter Konformitätstest ausgewiesen.

## 8. U0–U4, T1–T10 und realistische Reihenfolge

### 8.1 Implementierungseinheiten

| Einheit | Bewertung / notwendige Grenze |
| --- | --- |
| U0 | Richtige erste Risikogrenze. Erst R1-01-Alternativen und P01–P12 als gesonderten Machbarkeitsauftrag freigeben; Ergebnis danach in erneuten Designreview geben. Kein öffentlicher Strictmodus und keine Produktmigration. U0 darf nicht eine schon positive Gesamtannahme voraussetzen, die gerade diesen Nachweis benötigt |
| U1 | GD-10 korrekt: vollständige funktionale B1-Basis, nicht nur Textsetter. Zusätzlich R1-Kern, Ownership, EndUpdate, Result/Faulted und historische UI-Aktionen. R1-02/R1-04/R1-05 müssen davor entschieden sein. Alle 46/243/354/9 behalten ihre Aussagen |
| U2 | Sinnvolle Reviewgrenze für Bindungscommit, Selection/Search und Provider. Hängt am positiven U0-Pfad und einem konkret normalisierten Planmodell aus R1-03. Selbst eine grüne Einzelmatrix ist noch kein öffentliches Release |
| U3 | Native/async Integration auf U1+U2: Clipboard, Dragacknowledgement, Input/IME/Completion und Fehleradapter. Keine neuen B1-Paste-/Inputabhängigkeiten erst hier nachliefern. Eigene Lifecycle-/Racedaten und tatsächliche native Rückmeldung prüfen |
| U4 | Artefakt-, API-/Binary-, TFM-, Paketgraph- und Plattformabnahme nach vollständiger Serie. Unabhängiger Implementierungsreview und konkrete Distribution bleiben separate Bedingungen; keine Freigabe durch dieses Dokument |

R1 nennt die Einheiten zutreffend Revieweinheiten einer unveröffentlichten Serie. Es ist kein zweiter Text-/Undoalgorithmus erforderlich; ein koordiniertes UI-Historienmodell kann im nativen Stack bleiben. Eine zusätzliche Avalonia-Änderung für GD-01 würde dagegen Umfang, Abhängigkeitsgraph, Pflege und U4 erweitern und erfordert einen neuen Entscheid. Eine Aufwandsschätzung vor diesem Entscheid wäre nicht belastbar.

### 8.2 Gates

| Gate | Bewertung / zusätzliche Abnahmekriterien |
| --- | --- |
| T1 Ownership/Lifetime | Alle Stackbesitzer auch vor Strict, beide Stapel und verschachtelte Gruppen, alte native UI-Einträge ohne Stempel, Legacycallback-Aktivierung, A→B→A. Propertyfälle P01–P07/P10 und explizite Detachgeneration ergänzen |
| T2 Abschluss | Policyzustand nach Begin verweigert: End bleibt möglich. Policyreferenzsetter bei offener Gruppe verweigert. Fremder using-Rumpf versus eigener Compositekörper jetzt korrekt getrennt. Faulted mit unbekannter Gruppentiefe benötigt Ersatzprobe statt gesunden Folgeedit |
| T3 Events/Fehler | Eigene Listen und fremde Frameworklisten trennen, R1-06. Fehler in notifying-Einstieg und Commitressource separat von Observerthrow behandeln. MutationFailed plus Fallbackthrow genau einmal; Result nach Publicationfehler spezifizieren |
| T4 Historie | Nicht nur Text/CanUndo: ursprüngliche und finale Caret-/Selectionwerte in beiden Richtungen, fortgesetzte Gruppen, A→B→A, stale Skip. R1-02 mit konkret erwarteten Koordinaten. Teilpräfix bleibt eine Gruppe; ClearAll vor Wirkung erhält alte Rückfolge |
| T5 19 Composites | Vollständige Zeilenmatrix bleibt sinnvoll. Zusätzlich Completion „pri“→„print(“/Caret 6, Newline-/Indentation-Mapping, interner Teilmove und wirklich gespeicherte Partial-UI. Regulär wirkungsloser Setter von vorwirkungslos gescheitertem Setter unterscheiden |
| T6 Async | TCS-gesteuerte Clipboard-/Dragantworten, kein offenes Update während await, A-Generationen, vertauschte Antworten. Transportfehler plus gleichzeitiger Cancel, bestätigter Write plus Stale, Possible ohne bestätigten Erfolg, einmalige Source-Löschung |
| T7 Delegation | Fremde Getter/Iteratoren/Dispose und alle Callbacktypen, gemischte Strict-/Legacy-Dokumente, direkte TextView-Property; ProposeCompletion ist ausschließlich Dateneingang. Normalisierte zusätzliche Planedits dürfen AllowedEditAreas nicht erweitern |
| T8 Legacy | Originaldifferenz für tatsächliche null-Policy-Pfade einschließlich Bindingprioritäten und alten Input-/Clipboardadaptern. Nach deaktivierter Strictpolicy darf neue alte Historie nicht unbemerkt als generationstreu reaktiviert werden. Keine globale Normalisierungs-/Fanoutänderung |
| T9 Plattform | Native IME/Deadkeys, Tab/Enter/Maus/Separator, Fokus/Popup, Clipboardownership, tatsächliches DragEffects am Sourceende, Window-/Visualdetach. Fehlende OS-/Backendumgebung bleibt offen; Headless oder Publish reicht nicht |
| T10 Paket/Performance | Beide TFMs und Mindest-/Ziel-Avalonia, XAML/TextMate, API-/Binarydiff, Mischgraphverbot und reproduzierbarer Zweitbuild beibehalten. Workloads sowie Grenzwerte für Latenz/Allokation/Retention vor Messung festlegen. Neue Avalonia-Patchabhängigkeit gegebenenfalls ausdrücklich aufnehmen |

Die 243 Bibliotheksfälle enthalten acht `grouped-replay`-Kombinationen auf **TextDocument ohne TextArea**. Sie bestätigen die historische Textkernprüfung, nicht R1s historisches UI-Modell. Die drei Pastefälle prüfen weder ABA-Bindung noch einen vollständig kontrollierten verzögerten Clipboardtransfer. Die ursprünglichen Solltests bleiben unverändert; neue Proben ergänzen sie. Kein Gate wurde in dieser Runde ausgeführt oder als bestanden markiert.

## 9. Legacy, Distribution und Hostgrenze

### 9.1 API- und Binärkompatibilität

Die vorgesehenen Result-, Policy-, Provider- und Diagnose-APIs sind additive Vorschläge. PoC-Kompatibilität mit B1 ist von offizieller API-/Binarykompatibilität zu trennen. Der neue Task<Result>-Vertrag ersetzt keine bereits veröffentlichte Task-Signatur, solange er tatsächlich neu eingeführt wird; vor API-Freeze sind Überladungen, Exceptionkonstruktoren, EventArgstypen und Nullability vollständig zu prüfen.

Ein Wechsel von StyledProperty auf DirectProperty oder auf nur vermittelte Documentzugriffe wäre kein unsichtbares Implementierungsdetail. Bestehende öffentliche statische Felder, SetValue-/Binding-/XAMLpfade und AddOwner-Verwendung wären betroffen. Sollte GD-01 diese Alternative benötigen, müssen Vertrag und Kompatibilitätsentscheid ausdrücklich geändert werden. Legacy-zu-Legacy muss ohne Policy weiterhin den bisherigen Ablauf ermöglichen; neue gemeinsame Registrierungen benötigen entsprechende Differenzkontrollen.

### 9.2 Distribution

B2 §8 ist im unveränderten Stand als Entscheidungsrahmen tragfähig: offizielles passendes Release bevorzugen; andernfalls nur ausdrücklich genehmigtes zeitlich begrenztes Patchartefakt oder weitere Blockade. Eigene PackageId, Assemblyidentität, transitive Referenzen, TextMate/XAML, Quellenpin, getrennte saubere Builds und Exit auf einen wirklich vertragsgleichen offiziellen Stand sind berücksichtigt.

Keine neue Paketverfügbarkeit oder rechtliche Freigabe wird hier behauptet. Die Microsoft-Links im B2-Bericht werden nicht als aktuell neu geprüfte Distributionsentscheidung ausgegeben. Die offenen Werte — Präfix, konkrete AssemblyVersion, Feed, Owner/Budget/Frist und Plattformverantwortliche — dürfen externe Entscheidungen bleiben. Sie verdecken keine Laufzeitsemantik. Anders wäre eine zusätzliche Avalonia-Patchlinie: diese müsste ausdrücklich denselben Reproduzierbarkeits-/Graph-/Pflegeanforderungen unterstellt werden; das bestehende AvaloniaEdit-allein-Konzept deckt sie nicht automatisch ab.

### 9.3 MCP-00 und Hostverantwortung

Die wesentliche Trennung bleibt erhalten: editorId, monotone Revision, fachlicher No-op, SaveIntent/Bestätigung, Delete→Write, serviceeigene Cancellation/Drain, gemeinsame Interpreter-/Serviceadmission, Rechte und redigierte externe Fehler bleiben beim Host. Die Library besitzt lokale Text-/Binding-/Historykonsistenz und liefert wahrheitsgemäße lokale Wirkungsdiagnosen. Sie wird nicht zu einer zweiten Registry oder MCP-Ausführungspipeline.

Ein Libraryresult Completed ist keine fachliche Hostfreigabe; eine fachliche Operation kann mehrere Libraryaufrufe umfassen. Possible/Confirmed bei Clipboard/Move darf nicht als wirkungsloses Busy projiziert werden. Faulted-Ersatz erzeugt einen neuen Dokumentzustand, dessen Hostidentität/Revision der Host neu registrieren muss; er darf nicht den alten SaveIntent wieder gültig machen.

U12s Dispatcher-/ExecutionContext- und Prismgrenzen bleiben eigenständige spätere Migrationsaufgaben. R2/R4/R6/R7 im Produkt werden durch diesen Designreview nicht freigegeben. MCP-01.5 bleibt außerhalb des Auftrags.

## 10. Offene Entscheidungen und Schlussurteil

Vor Designannahme erforderlich:

1. Konkreter erfüllbarer GD-01-Pfad samt nachprüfbarer Quelle für Emissions-/Abschlusskontrolle; bei Bedarf gesonderte minimale Machbarkeitsprobe und erneuter Architekturentscheid.
2. Versions-/Mappingregel für zurückgestellte historische UI-Aktionen und genauer Zeitpunkt der Speicherung ihres Nachzustands.
3. Normalisierung zusammengesetzter Provider-/Completionpläne einschließlich Separator, Newline und nativer Mappingarten.
4. Quarantäne aktiver Verbraucher eines Faulted-Dokuments und ausdrücklich zulässiger Ersatz trotz unbekannter alter Enginegruppentiefe.
5. Erfolgsabhängige Textsettertails bei Vor-/Nachwirkungsfehlern sowie zu diesen Grenzen passende Gates.
6. Ein tatsächlich anderer Reviewer für die formale Abnahme.

Als externe Entscheidungen offenbleiben dürfen: Maintainerannahme/APIbenennung, konkrete Distributionsidentität und Pflegeweg, native Abnahmeumgebungen sowie spätere Implementierungs-, B3-/B4- und Migrationsfreigaben. Diese sind keine automatische Ermächtigung zum Beginn solcher Arbeiten.

**Abschließendes Urteil: CHANGES REQUIRED / BLOCKED.** R1 löst wesentliche ursprüngliche Probleme auf Designebene, doch GD-01 ist weiterhin eine tragende unbelegte Machbarkeitsannahme; R1-02–R1-05 enthalten konkrete semantische Lücken bzw. Widersprüche. Fehlende Laufzeitnachweise allein begründen dieses Urteil nicht. Die fehlende formale Reviewer-Unabhängigkeit ist zusätzlich ausdrücklich ausgewiesen.

Ein späteres **DESIGN ACCEPTED** wäre ausschließlich eine Designabnahme, keine Implementierungs-, Paket-, Migrations- oder Integrationsfreigabe.

## 11. Arbeitsabschluss

Einzige neue Produktdatei dieses Auftrags: `docs/mcp/MCP-01.4-B2-R1-independent-review.md`. B2-R1, der ursprüngliche Review, bestehender Produktcode und Tests bleiben unverändert. B1-Patch, Testquellen und historische Logs wurden nur gelesen. Die Abschlussprüfung vergleicht die 753 Eingangshashes, Branch/HEAD und Staging; die einzige erwartete Statusergänzung ist dieser neue unversionierte Bericht.

Keine Produktcodeänderung, keine automatische B2-Korrektur, keine Probeausführung, kein Build/Restore, keine Paketveröffentlichung, keine GUI-Migration, kein MCP-01.5, kein Commit/Push/Merge/Rebase. `.codex/config.toml` wurde weder gelesen noch geändert. Nach Ablage und Integritätsprüfung endet dieser Auftrag.
