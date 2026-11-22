# G2-04-02-I2-R1 – Independent Implementation Code Review

**CHANGES REQUIRED · READY FOR OWNER REVIEW**

**Empfehlung:** I2 vor der Eigentümerabnahme korrigieren. In beiden Konfigurationen konnte der Inspector beim Anzeigen einer historischen String-Property erfolgreich den JSON-Wert der gesamten Variable kopieren. Außerdem konnte er bereits vollständig aufgenommene Daten nach einem Editor-Edit nicht mehr als Property kopieren. Beide Fehler betreffen die fachliche Bedeutung einer erfolgreichen bzw. verweigerten Copy. Die 19 Wortlaut-Goldens und die native Großkopie bestanden unabhängig davon.

## Prüfstand und Herkunft

18. November 2026, bestehender Worktree `artifacts/worktrees/gui-g2-04`, Branch `feature/gui-g2-04`, Remote `https://github.com/tkbss/CryptoWorkBench`. HEAD und lokaler master entsprechen `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Gegenstand ist der darauf aufbauende uncommittierte I2-Stand. Produktcode und permanente Tests wurden im Review ausschließlich gelesen.

Geprüft wurden die externen F2-Originale aus dem F3-korrigierten Paket (14 von 14 SHA-256 gegen F3-final-evidence), insbesondere Architektur, Accessor-Matrix, Copy-Vertrag, 19 Goldens, A01–A38 und Prototyp. Die eingebettete GUI-Testressource ist mit der normativen Golden-Datei bytegleich. I1-Korrekturunterlagen, I1-R3 und I2-Abschlussbericht sind vorhanden. I1 ist kein eigener Git-Commit. Als unabhängige Quellreferenz dienten die Dokumentprüfsummen aus dem aufbewahrten I1-Produkt-PDB im ursprünglichen R2-Prüfoutput. Von 22 verglichenen Dateien der Automation-/Inspector-Dienste und des VariableViewModel stimmen 19 unmittelbar überein. Die drei Abweichungen sind InspectorCapture, InspectorSnapshot und InspectorSession. Für diese drei liegt damit eine historische Binär-/Checksumreferenz vor; eine vollständige separat versionierte I1-Quellkopie wurde nicht identifiziert. Ihre jetzigen Übergaben wurden am Quellcode und mit den ursprünglichen R2-Proben geprüft.

## High

### H01 · Historische Vollwertansicht und erfolgreicher Copy-Auftrag können verschiedene Objekte meinen

**Reproduktion.** Eine Stringvariable mit Value `child-value` auswählen, ihren Root vollständig kopieren und den Editortext ändern. Jetzt auf der historischen Root-Seite die zuvor nicht geöffnete Value-Zeile öffnen. Die Vollwertansicht zeigt `child-value`. „Copy full value“ übergibt das Root-JSON an seinen Commit-Callback und meldet Erfolg. Gleicher Befund in Debug und Release mit echter Session. Der Test verwendet den VM-Befehl, den der tatsächliche Button aufruft; der Callback zeichnet den angebotenen Clipboard-Inhalt auf.

**Ursache.** Der historische Schnellpfad setzt nur Detail und kehrt vor der Aktualisierung von path und pageNumber zurück. Der Toolbar-Befehl löst sein Ziel anschließend aus dem alten path auf. Der rechte Copy-Button direkt an der Zeile hat einen anderen Aufrufpfad. [VariableInspectorViewModel.cs, Zeile 75](../CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs#L75) · [DockVariableInspectorView.cs, Zeile 42](../CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs#L42)

**Korrektur und Abnahme.** Den dargestellten Detailwert gemeinsam mit Stamp, Property-Pfad und Copy-Quelle binden. Anzeige und zugehörige Vollwertaktionen müssen diesen gebundenen Knoten verwenden. Nach obiger Folge müssen sowohl Zeilen-Copy als auch Copy der geöffneten Detailansicht exakt `child-value` liefern. Für einen ausstehenden Clipboard-Aufruf bleibt seine ursprüngliche Herkunft gebunden.

### H02 · Vollständig vorhandene historische Daten verlieren ihre Copy-Fähigkeit

**Zwei Reproduktionen in Debug und Release.** (1) Eine 400-Zeichen-Property im vollständigen Root-JSON aufnehmen. Die Probe vergleicht den enthaltenen String vor dem Edit. Nach dem Edit liefert die Zeilen-Copy keinen Wert. (2) Eine 180-Zeichen-Property öffnen und alle drei Zeichenseiten laden. Die unabhängig zusammengesetzten Einträge entsprechen exakt dem Original. Nach dem Edit meldet Full Copy „Historical value was not fully captured“. Es wurde weder ein fehlendes Zeichen noch ein Revisionskonflikt gemessen.

**Ursache.** Der Property-Zweig speichert die bereits formatierte Tokenfolge als einen opaken Teil bei Offset 0. Seiten transportieren eigene Zeilenmetadaten. ValueAsync erkennt einen vollständigen Einzelteil, einen kurzen ExactScalar oder einen eigenen Seiten-Skalar. Es gibt keine Zuordnung von aufgenommenen Kindwerten im Parent oder von vollständig vorliegenden Seiten zur Range-Coverage der Property. Die historische Sperre verweigert dann den Zugriff auf vorhandene Daten. Das verletzt insbesondere A13/A14/A30 und den F2-Vertrag, nach dem JSON Ausgabeformat des Projectors ist. [InspectorCapture.cs, Zeile 44](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs#L44) · [VariableInspectorViewModel.cs, Zeile 89](../CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs#L89) · [InspectorSnapshot.cs, Zeile 65](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorSnapshot.cs#L65)

**Korrektur und Abnahme.** Erfasste fachliche Kinder und Bereiche unter demselben Stamp im vorhandenen Snapshot adressierbar machen. Parent-Copy und Seitenaufnahme müssen die tatsächlich erworbene Coverage verfügbar machen. Beide Reproduktionen müssen nach Editor-Edit und unter MCP-Lease ohne Live-Admission exakt kopieren können. Eine bewusst ausgelassene Seite muss weiterhin zur transparenten Ablehnung führen. Reader-Leases, Digestprüfung und Cleanup der I1-Teile beibehalten.

## Medium

### M01 · Ein einzelner Host-Schritt kann ein ganzes Dictionary sortieren oder Millionen Einträge durchlaufen

Die äußere Schleife begrenzt erzeugte Tokens und Ausgabebytes. Innerhalb eines MoveNext sortiert `map.Keys.Order(StringComparer.Ordinal)` die gesamte Schlüsselmenge. In der Aufnahme einer späten Dictionary-Seite startet ElementAt für jeden sichtbaren Index erneut beim Anfang. Die gesamte Describe-Operation läuft in **einem** AccessState. Das Gate bleibt während dieser Arbeit belegt. Bei der Enumeration zum Index wird das Token nicht zwischen den übersprungenen Einträgen geprüft. [InspectorProjector.cs, Zeile 135](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L135) · [Zeile 198](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L198) · [Zeile 272](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L272) · [InspectorCapture.cs, Zeile 65](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs#L65)

**Messung.** Nach Aufbau von 300.001 Schlüsseln benötigte allein der Übergang vom öffnenden JSON-Zeichen zum ersten Schlüssel 3.600.744 / 3.600.696 neu allokierte Bytes und 54,069 / 56,150 ms (Debug / Release). Für die letzte 64er-Seite von 65.536 Einträgen wurden 24,445 / 22,908 ms gemessen. Die Seitenprobe blieb unter ihrem Allokationsziel. Der Quellpfad überschreitet dort jedoch die erlaubten 256 Einträge pro kooperativem Schritt. Das sind Worker-Messungen; der Dispatcher-p95 des SECRET-Fixtures misst diesen Pfad nicht.

**Korrektur und Abnahme.** Revisionsgebundene Eintragsnavigation in begrenzten Schritten und eine budgetierte ordinale Sortierung vorsehen. Für beide Fixtures tatsächliche Einträge je AccessState, Arbeitsbuffer, Cancel-Latenz und vollständigen Ausgabetext prüfen. Die ordinale Reihenfolge und echte fachliche Dictionary-Keys müssen erhalten bleiben.

### M02 · Nach Berechnung von KCV sind seine String-Unterproperties nicht zuverlässig navigierbar

Mit einem gültigen AES-Schlüssel die Kcv-Zeile öffnen. Ihr Wert entspricht dem Core-Getter. Danach „Length“ öffnen: Der Inspector bleibt bei System.String und meldet NotCalculated. Erwartet wird System.Int32. Der Resolver durchläuft den Kcv-Vorfahren nochmals. Die Berechnungsfreigabe stammt allein aus dem State der zuletzt angeklickten Zeile. Bei Length fehlt sie. Umgekehrt kann die allgemeine Rechts-Pfeil-Navigation auf „Inspect Kcv“ über denselben Pfad unmittelbar die Berechnung starten. Die sichtbare State-Spalte nennt „Calculate“, der zugängliche Name dieser Navigationsaktion benennt die Berechnung nicht. [VariableInspectorViewModel.cs, Zeile 80](../CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs#L80) · [InspectorProjector.cs, Zeile 137](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L137) · [DockVariableInspectorView.cs, Zeile 60](../CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs#L60)

**Korrektur und Abnahme.** Den ausdrücklich berechneten Skalar unter seinem Stamp weiterverwenden und seine BCL-Properties daraus öffnen. Berechnen als eigene benannte Benutzeraktion führen. Length und Chars müssen nach einmaliger Berechnung auch historisch ohne weiteren KCV-Getter zugänglich sein. Allgemeines Expandieren darf keine KCV-Berechnung auslösen.

### M03 · Eine weiterlebende View behält nach fachlichem Close den alten Volltext

Die Probe öffnete einen Vollwert mit 8.192 Zeichen, hielt die tatsächliche DockVariableInspectorView fest und rief Dispose der VM auf. VM.Detail war anschließend null. Die View hielt alle 8.192 Zeichen weiter in shownText. Der TextBox-Inhalt wird ebenfalls nur in Render/ShowTextPage aktualisiert. Dispose trennt zunächst die Benachrichtigung und löscht am Ende die Changed-Abonnenten. Eine letzte Leeranzeige erreicht die View nicht. Bei einem gehaltenen Shell-Control bleibt damit ein potenziell großer String unabhängig vom freigegebenen Snapshot erreichbar. Für eine gleichzeitig verworfene, unerreichbare View wurde kein dauerhaftes GC-Leck nachgewiesen. [VariableInspectorViewModel.cs, Zeile 164](../CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs#L164) · [DockVariableInspectorView.cs, Zeile 115](../CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs#L115)

**Korrektur und Abnahme.** Beim semantischen Close die gebundene Ansicht einmal vollständig leeren; visuelles Detach muss den bisherigen Zustand erhalten. Nach Dispose bei weiterhin gehaltener View müssen Volltext, Detailfeld und alte Zeilen freigegeben sein. Für Reparenting muss der gegenteilige Test denselben Inhalt erhalten.

## Low

### L01 · Überladene Indexer haben denselben Inspector-Pfad

Für einen unbekannten Untertyp mit `this[int]` und `this[string]` liefert Reflection zwei Member. Beide erhalten denselben Step aus deklarierendem FullName und „Item“, denselben Anzeigenamen und keine Indexerparametersignatur. Die Probe fand zwei Zeilen mit nur einem eindeutigen Step. Die Getter blieben gesperrt. Außerdem registriert die Implementierung bekannte Getter über Runtime-Typ und Property-Name; die normative Matrix nennt zusätzlich Signatur und konkreten Getter-Slot. [InspectorProjector.cs, Zeile 72](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L72) · [Zeile 176](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L176)

**Korrektur und Abnahme.** Deklaration, Property-/Indexersignatur und Slot als eindeutige Metadatenidentität führen, für bekannte Zugriffe gegen die auditierte Signatur prüfen. Beide synthetischen Indexer müssen unterscheidbar und unausgeführt bleiben. Overrides weiterhin deduplizieren und verdeckte Properties erhalten.

### L02 · Standardreihenfolge und Seitengröße weichen vom angenommenen Design ab

F2-Architektur Abschnitt 3 verlangt Basis-vor-Unterklasse und danach ordinale Reihenfolge innerhalb der Ebene. Abschnitte zu Paging und Ressourcen legen 100 sichtbare Collection-Einträge pro Standardseite fest. Describe sortiert das gemischte Inventar global nach Name und verwendet 64 Einträge. Bei String steht dadurch GMAC vor geerbtem Text; bei 100 Einträgen ist eine zweite Seite erforderlich. Das ändert den kanonischen JSON-Inhalt nicht. [InspectorProjector.cs, Zeile 157](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L157) · [Zeile 182](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L182)

**Korrektur und Abnahme.** Baumreihenfolge und Standardseitengröße gemäß F2 umsetzen. Für den String-Root die tatsächliche Zeilenreihenfolge prüfen, für 99/100/101 Einträge die Seitengrenze. Das separate JSON-Golden muss byte- bzw. zeichengleich bleiben.

## Positive Nachweise und Grenzen

**Properties und Fachtypen.** Die produktiven Inventartests bestätigen 6/5/5/7/14/6/6 öffentliche Root-Properties einschließlich Statement.Text. Die Matrix enthält die bekannten fachlichen Untertypen und BCL-Familien. Read prüft den exakten Runtime-Typ; die eingeschränkten ReadOnly-Wrapper sind über die kopierenden SecretSet-/Header-Konstruktoren erreichbar. Im I2-Projector fand sich kein pauschaler Getteraufruf für unbekannte Unterklassen. Der Negativtest mit fremder Collection enumeriert sie nicht. Der bestehende allgemeine Variables-Publikationspfad liest dagegen selbst v.Value und v.Type.Name; die Getter-Aussage gilt für InspectorRead. Gleiche Referenzen in unterstützten Listen und Bytearray.SyncRoot sind abgedeckt. Der Vorfahren-Guard beim fachlichen Copy ist vorhanden. Ein echter zyklischer Graph unter den geschlossenen fachlichen Schemata wurde nicht End-to-End erzeugt.

**KCV und Nebenwirkungen.** Die eigene Vergleichsprobe prüfte alle drei Methoden für AES und TDEA mit je HEX, NORMAL_STRING und langem, whitespacehaltigem BASE64 gegen die echten Getter; alle 18 Vergleiche bestanden je Build. Hinzu kommen die permanenten Null-/Malformed-Fälle. Capture benutzt den vorhandenen InspectorRead, prüft Ursprung, Revision und die abschließende UI-Admission. Die KCV-Berechnung publiziert weder Execution, Print noch History. PARAM verwendet den expliziten GetParameters-Adapter; SECRET.Value bleibt SECRET, Shares werden über CopyValueRange gelesen. TR31H weist einen bereits gesetzten fremden DefaultSettings-Delegate zurück. Eine gleichzeitig von fremdem Code umgestellte globale Newtonsoft-Konfiguration bleibt eine zeitliche Grenze dieser Vorprüfung; dafür wurde kein Produktfehler reproduziert.

**Copy-Goldens.** Alle 19 Fälle wurden in beiden Konfigurationen neu ausgeführt. Die Erwartungen sind aus der eingefrorenen F3-Datei eingebettet und werden separat mit dem produktiven Encoderergebnis verglichen. Der KEY entsteht über Parser und Runner; sein Text beginnt mit KEYdemoKey=. PARAM mit HeaderReference, Split/Shamir mit OriginalKeyMetadata und TR31H verwenden echte C#-Objekte; TR31H mit Optional Blocks hat zusätzlich einen separaten Regressionstest. Die Fälle decken die fünf Roots, sechs Adapter/Unterobjekte sowie acht Skalar-/Dictionary-Grenzen ab. Strings, Zeilenenden, isolierte UTF-16-Surrogate, exakte Zahlen, Bytes und JSON-Namen werden im Encoder geprüft. Mehrere Skalarfälle nehmen ihren Eingabewert aus einem anderen Feld derselben Originaldatei. Diese Tests sind Wertformatvergleiche. Sie durchlaufen nicht 19-mal die GUI, Historisierung und Windows-Zwischenablage.

**Windows.** Neben den unveränderten Native-Proben wurde ein Reviewhost gegen dieselbe Produkt-DLL ausgeführt. Er ergänzt den nativen CF_UNICODETEXT-Vergleich um einen unabhängigen Vergleich der **gesamten** kanonischen Root-Zeichenfolge: festgelegte Header-/Descriptor-/Typmember, jede Zahl und jeder Separator sowie 255 algorithmisch erwartete Share-Hexwerte. Ergebnis in Debug und Release: 33.423.360 Rohbytes, genau 66.853.345 übereinstimmende UTF-16-Einheiten, korrekter Terminator und 0 eigene Payload-Dateien nach Drain. Damit ist auch die Lücke zwischen einem Readbackvergleich zweier Clipboard-Leser und dem fachlich erwarteten kompletten Parent für dieses Fixture geschlossen.

Die unveränderten nativen Wiederholungen maßen 1.122 / 752 ms von der großen Aufnahme bis zum geprüften Handoff, Render-p95 2,400 / 1,460 ms, Rendermaximum 26,799 / 18,816 ms und Prozesspeaks 743.305.216 / 741.105.664 Byte. Der Sampler sah 134.230.986 Byte benannte Dateien. Die gleichzeitig mögliche DeleteOnClose-Datei und Spitzen zwischen zwei Samples begrenzen die Disk-Aussage. Die isoliert gemessene warme SetTextAsync-Zeit (12 / 14 ms) umfasst nicht zwingend verzögertes OS-Rendering. Der Native-Test beschreibt die reale Windows-Zwischenablage mit synthetischen Testdaten.

CF_UNICODETEXT ist nullterminiert. Die vorgelagerte Ablehnung eines internen NUL ist sachgerecht: kein still abgeschnittener Erfolg, der interne UTF-16-Wert bleibt erhalten. Leerer Text, CRLF/LF und isolierter Surrogatwert wurden im aktuellen Windows-Lauf exakt zurückgelesen. Ein simulierter Fehler des Clipboard-Callbacks ergab eine Fehlermeldung; der nächste Versuch lieferte den Originalwert. Ein Readbackfehler nach bereits erfolgter OS-Übergabe kann die externe Wirkung nicht zurücknehmen. Die reguläre Textauswahl per Ctrl+C ist in der permanenten UI-Probe separat von der Property-Kopie geprüft.

**Lebensdauer und I1.** Für die drei gegenüber I1 geänderten Dateien wurden die Property-Verzweigung vor dem bisherigen Range-Pfad, die zusätzlichen Felder von ReadSpec/SnapshotPart und der optionale Session-Einstieg einschließlich Reset-/Transfer-Weitergabe geprüft. Aufnahme und Veröffentlichung laufen über denselben Host und dieselbe Reservation. Failure-Cleanup für unveröffentlichte Teile bleibt am einmaligen Abandon, die Session behält den transferierten Teil bis Add/finally. Snapshot.CopyAsync erwirbt alle relevanten Reader vor dem ersten Await und erreicht Commit erst nach validiertem Staging. Die unveränderten 13 ursprünglichen R2-Proben bestanden auch gegen die I2-DLL, darunter später beschädigtes/fehlendes Segment, verweigerte Veröffentlichung und vorübergehend gesperrte Datei. Die eigene Shutdown-Probe stoppte eine im vorbereitenden Clipboard-Callback wartende Copy vor ihrer simulierten Übergabe; Drain endete mit Pending null. M03 oben betrifft den UI-Volltext nach Close. Ein erneuter Verlust des I1-Cleanup-Owners wurde nicht nachgewiesen.

Die I1-Findings H01/H02 (Integrität/Leases), M01/M02 (Kontext/Revision), M03 (Cleanup), M04 (Cancellation), M05 (Admission-Allokation), M06 (Coverage im I1-Teilmodell) und L01 (Testhistorie) sind auf diesen ursprünglichen Pfaden ohne nachgewiesene Regression. H02 dieses Reviews betrifft die fehlende **Zuordnung der neuen I2-Property-/Seitendaten** zu diesem Teilmodell.

## Testmatrix und Evidenz

Alle **57** ursprünglichen I2-TRX wurden auf Einzelresultate/Fehlerhistorie gelesen. Für alle **12** Delivery-Einträge stimmen SHA-256, Passed, Failed, TRX-NotExecuted und CLI-Skips mit der Liefermatrix überein. Debug- und Release-Desktop-Buildprotokolle und native Nachweise sind vorhanden. Die drei Gesamtsuiten wurden in R1 anhand der Originale ausgewertet.

| Suite | Lieferung Passed Debug / Release | Eigene Wiederholung Debug / Release | Failed im bestätigten Lieferlauf je Konfiguration | CLI Skipped / zusätzliche NotExecuted |
|---|---:|---:|---:|---:|
| Core gesamt | 11.736 / 11.736 | Original-TRX | 0 | 26 / 1 |
| GUI gesamt | 1.224 / 1.224 | Original-TRX | 0 | 0 / 0 |
| MCP gesamt | 176 / 176 | Original-TRX | 0 | 0 / 13 |
| I2 fokussiert | 40 / 40 | 40 / 40 | 0 | 0 / 0 |
| I1 Inspector | 38 / 38 | 38 / 38 | 0 | 0 / 0 |
| I1 Range | 13 / 13 | 13 / 13 | 0 | 0 / 0 |

Die Review-Suite extended enthält zusätzlich zu den 13 originalen R2-Fällen neun R1-Proben. Je Konfiguration ergaben sich **16 Passed / 6 Failed**. Die zusätzliche Suite compute ergab **1 Passed / 1 Failed** je Konfiguration. Die sieben fehlgeschlagenen Soll-Assertions verteilen sich auf H01, H02 (zwei Proben), M01, M02, M03 und L01. Mehrfach ausgeführte Iterationen sind nicht addiert. Die Produkt-DLLs der beiden lokalen Prüfharnesses stimmen je Konfiguration per SHA-256 mit den GUI-Test-DLL-Ausgaben des Produkts überein.

Die sieben Reflection-Vergleichsfehler des ersten Property-Laufs sind in den Originalen sichtbar. Die separaten Zwischenläufe review-GUI-Debug.trx und owner-GUI-Debug.trx enthalten jeweils ShortPrintIsVisibleAndSourceButtonNavigates(911,512) mit Dispatcher.VerifyAccess / DefaultRenderLoop.Add. Ein späterer grüner Lauf belegt keine Behebung. Ein ursächlicher Bezug zu den hier reproduzierten I2-Fehlern wurde nicht festgestellt.

[Verifizierte Liefermatrix](../artifacts/test-results/g2-04-i2-r1/verified-matrix.json) · [I1-PDB-Abgleich](../artifacts/test-results/g2-04-i2-r1/i1-pdb-checksums.json) · [Proben, native Orakel, Logs und Original-TRX der Nachprüfung](../artifacts/test-results/g2-04-i2-r1/)

## Abnahmeabgleich und offene Grenzen

| Kriterien | R1-Bewertung |
|---|---|
| A01–A04 | Root-Anzahlen und geprüfte Zugriffssperren bestätigt. Indexeridentität und Metadatenvertrag siehe L01/L02. Die ReadOnly-Wrapper-Herkunft ist durch Quellprüfung gedeckt; nicht jeder geschlossene BCL-Slot hat eine eigene Laufzeitprobe. |
| A05–A08 | Read-only-Pfade, PARAM, TR31H und KCV-Werte gestützt. Die explizite Berechnung und anschließende Property-Navigation sind gemäß M02 zu korrigieren. |
| A09–A12 | Golden- und ergänzende Grenzwerttests bestanden. Für historische Vollwert-Copy ist die Knotenbindung zusätzlich erforderlich (H01). |
| A13–A15 | Windows-Endschritt und getestete Fehlerbehandlung bestätigt. H01/H02 verhindern die Abnahme der historischen und vollständigen Copy-Semantik. |
| A16–A18 | Vollständiger nativer 255-Share-Parent und einzelne Ranges bestätigt. Kein fixes Root-Limit in der geprüften Pipeline gefunden. Extrem tiefe synthetische Graphen sind kein gemessener Nachweis. |
| A19–A22 | Integrität und Datei-Cleanup auf den getesteten Pfaden bestätigt. M01 verletzt die kooperativen Arbeitsgrenzen, M03 betrifft die UI-Lebensdauer. Eine Detailanforderung materialisiert trotz 4.096er-Textseiten zuerst den ganzen String. Freiplatzprüfung und FileStream-Eröffnung erfolgen vor CopyAsync auf dem UI-Pfad. Langsamer Datenträger, echte Quota-Erschöpfung, Crash und alle Cancel-Latenzen sind hier nicht vollständig vermessen. |
| A23–A31 | Bestehende Observer-/Host-/MCP-Pfade durch Originalvergleich und Tests gestützt. I2-Historie und semantischer Close bleiben wegen H01/H02/M03 offen. Für Busy-Erstadmission ist kein eigener sichtbarer „Erneut laden“-Button im Fenster vorhanden; das Verhalten nach einer solchen Ablehnung sollte in der Korrekturabnahme ausdrücklich bedient werden. |
| A32–A37 | View und VM können getrennt vom visuellen Detach weiterleben. Tatsächlicher Close verlangt M03. Produktive Pane-Integration, Dock-Admission, Floating und Layoutmigration gehören zu I3. |
| A38 | Native Windows-Proben für 360 Pixel, Light/Dark/System, Fokus, Tab/Enter und Textseiten bestanden. Vorhandener externer UIA-Nachweis umfasst benannte Buttons und Invoke. **Manuelle Screenreader-Abnahme offen.** Automationsnamen allein beweisen weder Sprachführung noch Kontrast oder vollständige visuelle Qualität. Andere Desktop-Clipboard-Backends und produktives Cross-Window-Reparenting wurden nicht abgenommen. |

**Eigentümerentscheidung:** Die belegten Korrekturen insbesondere an Wertbindung und historischer Coverage vor einer Abnahme verlangen. Eine gezielte Nachprüfung kann die lokalen Soll-Proben und das unabhängige native Root-Orakel wiederverwenden. I3 wurde nicht begonnen.
