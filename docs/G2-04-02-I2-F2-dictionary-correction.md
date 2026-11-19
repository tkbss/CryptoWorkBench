# G2-04-02-I2-F2 · Abschlussbericht zur Dictionary-Korrektur

**READY FOR OWNER REVIEW.** R1-M01 / R2-M01 ist im F2-Implementierungsstand geschlossen. Die unabhängige R3-Nachprüfung und die Eigentümerabnahme stehen aus.

## Prüfstand und Umfang

CryptoWorkBench, Worktree `artifacts/worktrees/gui-g2-04`, Branch `feature/gui-g2-04`. HEAD und lokaler Master wurden vor der Änderung gegen `32017a08c4771e73c12a50f6e1548476b9bb0a54` verifiziert. Grundlage war der uncommittierte I2-F1-Stand mit dem unabhängigen R2-Bericht. Alle 14 F3-Paketdateien entsprechen ihren manifestierten Hashes. Die 19 eingebetteten Golden-Erwartungen sind bytegleich mit der normativen Datei.

## Ursache und Korrektur

R2 maß bei einem vorhandenen Schlüssel von 1.200.000 UTF-16-Codeeinheiten 2.400.360 B in Debug und 2.400.336 B in Release für einen einzigen Navigationsschritt. Das Interpolieren des Zeilenlabels kopierte den kompletten Key in einen zweiten String. Die Prüfung der angrenzenden Pfade ergab weitere Stellen, an denen Länge und gemeinsamer Präfix unmittelbar die Arbeit eines Schritts bestimmten.

| Pfad | Änderung und Invariante |
|---|---|
| Zeile und Navigation | Derselbe DisplayName liefert bei langen Schlüsseln maximal 80 UTF-16-Einheiten Vorschau plus Ellipse und Klammern. Eine Schnittgrenze innerhalb eines gültigen Surrogatpaares wird um eine Einheit zurückgesetzt. Das Label für den leeren Key bleibt `[]`. |
| Identität | Jeder Map-Schritt trägt die originale Schlüsselreferenz und die ordinale Position innerhalb der konkreten Map. Die interne Pfadkennung speichert für diesen Schritt die kompakte Position. Elternpfad und CaptureStamp bestimmen die Map. Der historische Map-Index verifiziert den tatsächlichen Key an dieser Position. Die Vorschau fließt in keinen Lookup ein. |
| Live-Auflösung | Für lange Schlüssel wird die Map kooperativ enumeriert. Derselbe Schlüsselstring ist per Referenz erkennbar. Bei separaten Strings gleicher Länge wird die exakte Gleichheit in Blöcken bis 8.192 Codeeinheiten geprüft. Die Navigation muss einen langen Schlüssel hierfür weder kopieren noch in einem Zug hashen. |
| Ordinale Sortierung | Ein abbrechbarer Vorlauf unterscheidet Maps mit ausschließlich kurzen Keys (bis 32 Einheiten) von Maps mit langen Keys. Für kurze Keys bleibt die begrenzte Heap-Auswahl. Bei langen Keys arbeitet ein lokaler Merge-Sort mit zwei beschränkten Arrays aus Key/Value-Referenzen und behält bis zu 4.096 Einträge für den nächsten Ausgabelauf. Die Vergleichsblöcke sind maximal 4.096 Einheiten lang; spätestens nach einem Vergleichsbudget von 16.384 Einheiten wird kooperativ abgegeben. Auch Scans und Merge-Arbeit besitzen Eintrags-Checkpoints. Der Vergleich arbeitet ordinal in beiden Richtungen. |
| Fachliche Ausgabe | Die Formatierung übernimmt den vollständigen echten Key und den zugehörigen Value aus dem sortierten Eintrag. Der bestehende Encoder schreibt die JSON-Escapes stückweise. Sealing, Segmentintegrität und Clipboard-Commit laufen über die vorhandene I1/I2-Pipeline. |

### Geänderte Dateien

- [InspectorProjector.cs](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs) enthält die Produktänderung. DisplayName und PathKey stehen ab Zeile 21, die Live-Auflösung ab Zeile 191, der Zugriff auf den historischen Index ab Zeile 457 und die kooperative Ordnung ab Zeile 572.
- [DictionaryKeyCorrectionTests.cs](../CryptoWorkBenchGuiTest/DictionaryKeyCorrectionTests.cs) ergänzt vier permanente Fälle. Ein Einzelschlüssel mit exakt 1.200.000 Einheiten und zwei lange Keys mit identischem Vorschaulabel prüfen Anzeige, kompakte Pfade, vollständigen Schlüssel, getrennte Values und unabhängiges kanonisches JSON. Ein echter VM-Fall öffnet beide Zeilen nach Editoränderung und kopiert Key und Value historisch. Der vierte Fall bricht während eines langen gemeinsamen Präfixvergleichs ab.
- `docs/G2-04-02-I2-F2-dictionary-correction.md` ist dieser Bericht. Laufskript, Prüfhostkopien, zusätzliche lokale Proben und Originalresultate dieser Phase liegen unter [F2-Prüfnachweise](../artifacts/test-results/g2-04-i2-f2/). 18 historische Harnessdateien wurden byteweise über SHA-256 gegen die jeweiligen Originale geprüft. [Prüfsummen](../artifacts/test-results/g2-04-i2-f2/harness-sha256.json)

## Arbeitsbudgets

Gemessen wurden die einzelnen Aufrufe der produktiven Schrittfolge für eine Seite bzw. des produktiven ReadChunk einschließlich Indexaufbau. Angegeben sind die höchste Thread-Allokation und die längste Dauer eines Schritts. Der Besuchszähler erfasst Eintragsarbeit. Die separate Blockgrenze im langen Vergleich begrenzt zusätzlich die Zeichenarbeit bei demselben Eintrag.

| Fall | Eintragsbesuche max. Debug / Release | Bytes max. Debug / Release | ms max. Debug / Release |
|---|---:|---:|---:|
| Ein langer Schlüssel, permanente Seitenprobe | 3 / 3 | 26.312 / 25.936 | 0,066 / 0,067 |
| Zwei lange Schlüssel mit identischem Vorschaulabel | 5 / 5 | 26.312 / 25.936 | 0,061 / 0,061 |
| 300.001 Schlüssel, vollständige kanonische Ausgabe | 97 / 97 | 98.704 / 98.688 | 15,490 / 12,751 |
| 65.536 Schlüssel, letzte Seite mit 36 Einträgen | 72 / 72 | 98.704 / 98.688 | 0,073 / 0,069 |

Bei 370.277 Schritten der vollständigen Ausgabe lagen zwei Debug-Schritte und ein Release-Schritt über dem 10-ms-Ziel. Die Daten belegen weder eine harte Echtzeitgrenze noch die Ursache einzelner Spitzen. Die Ausgabe arbeitet mit 64 KiB pro Puffer. Der Preis für begrenzten Sortierspeicher ist die wiederholte Enumeration großer Maps. Lange gemeinsame Präfixe können die Gesamtzeit erhöhen. Ein bereits aufgenommener Map-Index benötigt weiterhin O(n) Einträge und hält die originalen Schlüssel bis zur Snapshot-Freigabe erreichbar. Endgültige vollständige String- und Clipboard-Werte können fachlich entsprechend groß sein.

## Regressionen und Integrationsprüfung

| Bisher geschlossenes Finding | Erhaltener Nachweis |
|---|---|
| H01 · Bindung | Originalprobe mit historischem child-value, übereinstimmender Zeilen-/Toolbar-Copy und F1-Auswahlwechsel. |
| H02 · Coverage | Parent- und Seitenwerte nach Edit und unter MCP-Lease, fehlende oder widersprüchliche Bereiche, fremder Stamp und identische Namen unter verschiedenen Pfaden. Der neue kollidierende Langschlüsseltest nutzt dieselbe detached Copy-Pipeline. |
| M02 · KCV | Allgemeines Öffnen führt keine Erstberechnung aus. In der separat ausgeführten expliziten Variante bestehen die unveränderten Folgeassertionen für die Navigation nach Calculate. |
| M03 · Close | Festgehaltene View nach Dispose geleert, Inhalt über visuelles Reparenting erhalten. |
| L01 · Accessor-Identität | Indexer- und Override-Regressionen sowie Getterzähler bestehen. |
| L02 · Ordnung und Paging | Alle sieben Root-Typen, 99/100/101 Einträge und 19 unveränderte F3-Goldens. |

Die I1-Tests für Admission, ExecutionIntent, StateRevision, Reader-Leases, Segmentintegrität, Commit und Cleanup sowie die ursprünglichen 13 Payload-/Cleanup-Proben wurden erneut ausgeführt. Zusätzliche lokale Host-Proben prüfen die tatsächliche 300.001-Key-Aufnahme, die späte Seite sowie Abbruch während der Ausgabe. Bei Abbruch während eines langen Präfixvergleichs ging Pending auf null zurück und ein weiterer Read gelang. Ein separates Orakel prüft 9.001 lange Keys über mehrere begrenzte Sortierläufe, Vorwärts- und Rückwärtsseiten sowie den vollständigen JSON-Text.

## Abschlussmatrix

Die unveränderte, zuvor rote R2-Langschlüsselprobe bestand in Debug mit **27.648 B** und in Release mit **26.984 B** maximaler Allokation in einem Schritt. Die rote R2-Historie bleibt im ursprünglichen Ergebnisverzeichnis erhalten. [Debug-TRX](../artifacts/test-results/g2-04-i2-f2/r2-Debug.trx) · [Release-TRX](../artifacts/test-results/g2-04-i2-f2/r2-Release.trx)

Alle Zahlen gelten jeweils für Debug und Release. Fokussierte Suiten sind Teilmengen oder zusätzliche Kontrollläufe. Skipped stammt aus dem CLI-Ergebnis; weitere NotExecuted sind die verbleibenden TRX-Einzelfälle.

| Suite | Passed | Failed | Skipped | weitere NotExecuted |
|---|---:|---:|---:|---:|
| Core gesamt | 11.736 | 0 | 26 | 1 |
| GUI gesamt | 1.255 | 0 | 0 | 0 |
| MCP gesamt | 176 | 0 | 0 | 13 |
| F2 permanent | 4 | 0 | 0 | 0 |
| F1 permanent | 27 | 0 | 0 | 0 |
| Bestehende I2-Tests inkl. 19 Goldens | 40 | 0 | 0 | 0 |
| I1 Inspector / Range, separate Läufe | 38 / 13 | 0 | 0 | 0 |
| R2-Langschlüsselprobe und drei Hostfälle | 4 | 0 | 0 | 0 |
| Zusätzliche Langschlüssel-Integrationsproben | 2 | 0 | 0 | 0 |
| Unveränderter R1-Host inkl. ursprünglicher I1-R2-Proben | 23 | 1 | 0 | 0 |
| Separate explizite Calculate-Variante | 2 | 0 | 0 | 0 |

Beide Desktop-Builds bestanden. Die Produkt-DLL der fünf zusätzlichen Harnessausgaben war je Konfiguration bytegleich mit der im GUI-Testlauf. Die Ergebnisse wurden anhand aller 24 ausgewiesenen TRX-Dateien mit CLI-Skips und SHA-256 erfasst. [Vollständige Testmatrix](../artifacts/test-results/g2-04-i2-f2/matrix.json)

**Fehlerhistorie.** Die unveränderte R1-Erwartung CalculatedChildrenRemainNavigable verlangt weiterhin implizite KCV-Berechnung beim allgemeinen Öffnen und ist in beiden Konfigurationen rot. Sie gehört zur historischen Negativkontrolle. Die explizite Variante ist separat grün. Für die F2-Produkt-, Host- und Langschlüsselprüfungen gab es keinen fehlgeschlagenen Testlauf. Der zusätzliche fokussierte Debug-Lauf wurde separat aufbewahrt. Die früher beobachtete intermittierende Avalonia-Headless-Initialisierung bleibt eine bekannte Testumgebungsgrenze.

**Native Windows-Prüfung.** Der unveränderte unabhängige Root-Vergleich bestätigte in beiden Konfigurationen 255 Shares mit exakt 66.853.345 UTF-16-Codeeinheiten. Die vier historischen Win32-Vergleiche (Parent oder Seiten, nach Edit oder unter Lease) bestanden jeweils. Nach Drain waren in beiden nativen Prüfhosts null eigene Payload-Dateien vorhanden. Manuelle Screenreader-Abnahme und Clipboard-Verhalten anderer Desktop-Plattformen bleiben offen. [Debug](../artifacts/test-results/g2-04-i2-f2/native-Debug.log) · [Release](../artifacts/test-results/g2-04-i2-f2/native-Release.log) · [Historische Werte](../artifacts/test-results/g2-04-i2-f2/history-native-Release.log)

**Übergabe an R3.** Empfohlen ist die begrenzte unabhängige Wiederholung der ursprünglichen Langschlüsselprobe, der historischen Kollisionsprobe, der Sortierlaufgrenzen und der Abbruch-/Freigabeprüfung. Die akzeptierten I1-Host-, Snapshot- und Cleanup-Verträge sowie die sechs übrigen I2-Befunde bleiben nach den ausgeführten Regressionen erhalten. I3 wurde nicht begonnen.
