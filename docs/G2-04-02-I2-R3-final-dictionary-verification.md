# G2-04-02-I2-R3 · Abschließende Dictionary-Nachprüfung

**PASS · R1-M01 / R2-M01 CLOSED · READY FOR OWNER REVIEW.** Die zuvor fehlgeschlagene unabhängige Probe benötigt für den 1.200.000 Einheiten langen Schlüssel maximal 26.600 Bytes in Debug und 25.936 Bytes in Release je Navigationsschritt. R2 hatte für dieselbe Probe über 2,4 MB gemessen. Die vollständigen Originalschlüssel und die zugehörigen Werte blieben auch bei identischer Vorschau nach einem Editor-Edit korrekt kopierbar. Ich empfehle I2 zur Eigentümerabnahme. Im geprüften Umfang ergab sich kein neuer Befund.

## Prüfstand und Herkunft

Im bestehenden Worktree `artifacts/worktrees/gui-g2-04` wurden Branch `feature/gui-g2-04`, Remote `tkbss/CryptoWorkBench` sowie HEAD und lokaler master vor der Prüfung verifiziert. Beide Refs ergaben `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Prüfgegenstand war der darauf aufbauende uncommittierte F2-Stand. Die 14 Dateien des F3-korrigierten Architekturpakets stimmen mit dem F3-Manifest überein, die eingebettete Golden-Datei byteweise mit dem normativen Original. Die Review-Ergänzungen bestehen aus diesem Bericht und lokalen Prüfartefakten. [R3-Prüfhost und Ergebnisse](../artifacts/test-results/g2-04-i2-r3/)

## Quellprüfung und Gegenproben

- **Anzeige und Identität.** DisplayName begrenzt die Vorschau auf 80 UTF-16-Einheiten und rückt bei einer Schnittgrenze innerhalb eines gültigen Surrogatpaares um eine Einheit zurück. Die Zeile übernimmt dieses Label; der Map-Schritt enthält weiterhin den Originalstring und den ordinalen Index. Die kompakte Pfadkomponente verwendet die Position innerhalb des gestempelten Elternpfads. Der historische Lookup prüft den Originalkey an dieser Position. Zwei lange Keys mit identischem Label wurden live und historisch separat geöffnet. Die Tests vergleichen die vollständigen Key- und Value-Inhalte sowie das gesamte kanonische JSON mit eigenen Erwartungen. Der ergänzende Grenzfall verwendet 79 ASCII-Zeichen vor einem Surrogatpaar. Beide Labels waren exakt auf die 79 Zeichen plus Ellipse und Klammern begrenzt. Die Sonderkey-Probe deckt zusätzlich leeren Key, Leerstring, null, Klammern, Unicode, Escapezeichen und Zeilenumbrüche ab. [Identifier und Vorschau](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L21) · [Historischer Lookup](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L457)
- **Live-Auflösung.** Kurze Keys werden direkt aufgelöst. Lange Keys werden bei gleicher Referenz sofort erkannt; andernfalls prüft der Host gleich lange Kandidaten in Abschnitten von bis zu 8.192 Einheiten. Zwischen den Abschnitten kann er abbrechen. Der Originalkey bleibt für Copy erhalten. In den berührten UI- und Capture-Pfaden fand sich keine zweite vollständige Stringkopie zur bloßen Bildung eines Labels oder eines Pfadschlüssels. [Auflösung](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L179)
- **Sortierung.** Der Vorlauf prüft die Schlüssellängen kooperativ. Für kurze Keys wählt der Heap bis zu 4.096 Einträge aus. Der Langschlüsselpfad nutzt zwei Arrays von je maximal 8.192 Key-/Value-Paaren aus Stringreferenzen. Die Wiederaufnahme nach Yield setzt den Vergleich am gespeicherten Offset fort. Sortierläufe verwenden einen Cursor und können für späte Seiten rückwärts laufen. Die Orakel prüften den kompletten Wert mit 300.001 Keys, die letzten 36 Einträge von 65.536 Keys sowie bei 9.001 langen Keys die Seiten 1, 45, 52, 89 und 90 und den kompletten JSON-Text. Die Proben nutzen eine separate ordinale Sollsortierung. [Sortierentscheidung](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L572) · [Merge-Schritte](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs#L592)
- **Admission und Abbruch.** Der Host hält die Iteratoren innerhalb des aufgenommenen Reads. Die umgebenden using-Blöcke geben sie auch bei Cancellation frei; SnapshotParts übernehmen versiegelte Daten und einen Index. Die Hostproben deckten Sortierung, späte Navigation und begonnene Ausgabe ab. Die zusätzliche R3-Probe löste Cancellation bei einem beobachteten Vergleichsoffset von mindestens 32.768 Einheiten aus. Pending fiel auf null, ein weiterer regulärer Read gelang. [Host-Schrittfolge](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs#L52) · [R3-Messprobe](../artifacts/test-results/g2-04-i2-r3/meter/Meter.cs)

## Eigene Messungen

Werte jeweils Debug / Release. Die Allokation misst den ausführenden Thread um einen produktiven Schritt. Der Besuchszähler erfasst Eintragsarbeit; die Zeichenarbeit des langen Vergleichs wurde separat überprüft.

| Probe | Max. Eintragsbesuche | Max. Bytes | Max. Dauer in ms |
|---|---:|---:|---:|
| Permanente Probe, ein langer Key | 3 / 3 | 26.312 / 11.096 | 0,067 / 0,031 |
| Permanente Probe, langes Kollisionspaar | 5 / 5 | 26.312 / 25.936 | 0,058 / 0,060 |
| R3-Paar mit Surrogatgrenze | 5 / 5 | 14.440 / 14.064 | 0,3190 / 0,2267 |
| 300.001 Keys, vollständiger Encoder mit Index | 97 / 97 | 98.704 / 98.688 | 22,542 / 29,484 |
| 65.536 Keys, Seite 655 | 72 / 72 | 98.704 / 98.688 | 0,119 / 0,070 |

Beim R3-Paar unterscheiden sich die Keys erst an der letzten von 1.200.001 Positionen. Der Prüfhost las zwischen den Zeitmessungen den Fortsetzungsoffset des suspendierten produktiven Iterators. In beiden Builds wurden 73 ansteigende Offsets bis 1.196.032 beobachtet. Bis zum ersten Unterschied wurden alle 1.200.001 Zeichenpaare verglichen. Die größte Differenz je Schritt einschließlich des Reststücks betrug **16.384 Zeichenpaare**, entsprechend 65.536 Bytes logischem UTF-16-Eingang aus beiden Strings. Diese Messung erfasst den Fortschritt des Vergleichs auf Anwendungsebene; CPU-Vektorzugriffe und Cache-Verkehr wurden nicht gemessen. Über 76 Schritte betrug der Median **0,0052 / 0,0049 ms**. Vom Abbruchsignal bis zur Rückkehr der finalisierten UI-Task vergingen **9,3507 / 9,4983 ms**, jeweils eine Einzelmessung einschließlich Host-Finalisierung. [Debug](../artifacts/test-results/g2-04-i2-r3/meter-Debug.trx) · [Release](../artifacts/test-results/g2-04-i2-r3/meter-Release.trx)

Bei der großen Map überschritten 2 / 1 von jeweils 370.277 Schritten das 10-ms-Ziel. Die gemessenen Mengen blieben unter 256 Eintragsbesuchen; der produktive Encoder begrenzt die Ausgabe pro Schritt mit einem 64-KiB-Puffer. Im Langvergleich ist zusätzlich der Zeichenfortschritt begrenzt. Die Zeitspitzen allein belegen keine mit der Schlüssellänge unbeschränkt wachsende Einzeloperation. Ohne Scheduler-/GC-Trace ist die konkrete Ursache dieser Spitzen offen. Wiederholte Durchläufe können bei großen Maps die Gesamtzeit erhöhen. Der historische Index beansprucht weiterhin O(n) Metadaten und hält tatsächliche Schlüsselstrings bis zur Freigabe erreichbar.

## Matrix und Regressionen

Alle 24 F2-Original-TRX wurden gehasht, ihre Einzelresultate gezählt und die CLI-Skips vom zusätzlichen NotExecuted-Anteil getrennt. Alle 18 Harness-Prüfsummen stimmen; die verwendete R2-Probenquelle ist auch direkt mit dem R2-Original bytegleich. In beiden Konfigurationen entsprachen die Produkt-DLLs der fünf zusätzlichen F2-Harnessausgaben byteweise der GUI-Testausgabe. Zahlen in der Tabelle gelten pro Konfiguration. Fokussierte Suiten sind Teilmengen. [Verifizierte F2-Matrix](../artifacts/test-results/g2-04-i2-f2/matrix.json)

| Suite | Passed | Failed | Skipped | Weitere NotExecuted | Art des Nachweises |
|---|---:|---:|---:|---:|---|
| Core gesamt | 11.736 | 0 | 26 | 1 | F2-Original |
| GUI gesamt | 1.255 | 0 | 0 | 0 | F2-Original |
| MCP gesamt | 176 | 0 | 0 | 13 | F2-Original |
| F2 / F1 / I2 / I1 Inspector | 4 / 27 / 40 / 38 | 0 | 0 | 0 | In R3 zusammen 109 erneut bestanden |
| I1 Range | 13 | 0 | 0 | 0 | F2-Original |
| R2-Langschlüssel, Host-, Mehrlauf-, Abbruch- und Sonderkeyfälle | 7 | 0 | 0 | 0 | R3-Wiederholung |
| Zusätzliche R3-Messproben | 2 | 0 | 0 | 0 | Eigener Prüfhost |
| Unveränderter R1-Host einschließlich 13 I1-R2-Proben | 23 | 1 | 0 | 0 | R3-Wiederholung |
| Explizite Calculate-Variante | 2 | 0 | 0 | 0 | R3-Wiederholung |

**Die sechs übrigen I2-Findings bleiben im berührten Umfang CLOSED.** H01/H02 sind über historische Detail-/Zeilen-Copy, Parent- und Seiten-Coverage nach Edit und unter Lease abgedeckt. M02 ist durch berechnungsfreie allgemeine Navigation und die explizite Calculate-Variante belegt. Nur die historische R1-Assertion CalculatedChildrenRemainNavigable verlangt weiterhin implizite Berechnung und schlägt erwartungsgemäß fehl. M03 ist durch Dispose bei gehaltener View und die Detach-Gegenprobe, L01 durch unterschiedliche Indexerslots und unveränderte Getterzähler, L02 durch alle sieben Root-Ordnungen, Seitengrenzen und 19 Goldens gestützt. Die erneuten I1-Inspector- und Payload-Proben stützen Admission, Revision und Objektidentität, mehrteilige Reader-Leases, Segment-Digests vor Commit sowie Cleanup bei Abbruch, Shutdown und gesperrter Datei. [Fokussiert Debug](../artifacts/test-results/g2-04-i2-r3/focused-Debug.trx) · [Fokussiert Release](../artifacts/test-results/g2-04-i2-r3/focused-Release.trx) · [Hostproben](../artifacts/test-results/g2-04-i2-r3/host-Release.trx)

## Native Nachweise und Eigentümerentscheidung

Die vorhandenen Windows-Prüfprotokolle und die Orakellogik wurden geprüft. Für Debug und Release dokumentieren sie den unabhängigen vollständigen Vergleich von 255 Shares mit 66.853.345 UTF-16-Codeeinheiten, einschließlich Win32-Clipboard-Prüfung. Historische Copy aus Parent und Seiten bestand jeweils nach Edit und unter Lease; die Prüfhosts meldeten null eigene Payload-Dateien nach Drain. Diese Nachweise stammen aus F2. R3 wiederholte die fachlichen Host- und Copy-Proben; ein zusätzlicher nativer Clipboard-Lauf war für die unveränderte Plattformübergabe nicht erforderlich. [Natives Root-Orakel](../artifacts/test-results/g2-04-i2-f2/native/Program.cs#L203) · [Windows Debug](../artifacts/test-results/g2-04-i2-f2/native-Debug.log) · [Windows Release](../artifacts/test-results/g2-04-i2-f2/native-Release.log) · [Historische Übergabe](../artifacts/test-results/g2-04-i2-f2/history-native-Release.log)

Andere Desktop-Clipboard-Backends, manuelle Screenreader-Nutzung und produktives Cross-Window-Docking bleiben offen. Die bekannten intermittierenden Headless-Initialisierungsfehler gelten durch diese erfolgreichen Läufe nicht als ursächlich behoben. Für den beauftragten I2-Umfang steht der Eigentümerabnahme aus dieser Prüfung kein offenes Finding entgegen. I3 wurde nicht begonnen.
