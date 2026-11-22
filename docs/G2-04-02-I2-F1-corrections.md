# G2-04-02-I2-F1 · Property Inspector Implementation Corrections

**READY FOR OWNER REVIEW.** Alle sieben R1-Findings sind im F1-Implementierungsstand korrigiert. Empfohlen ist die gezielte unabhängige R2-Nachprüfung. Die Eigentümerabnahme steht aus.

## Prüfstand

Repository `tkbss/CryptoWorkBench`, bestehender Worktree `artifacts/worktrees/gui-g2-04`, Branch `feature/gui-g2-04`, 18. November 2026. HEAD und der lokale Master zeigen auf `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Die I1- und I2-Vorleistungen lagen uncommittiert im Worktree. Die Dateiliste unten beschreibt das Delta dieser F1-Phase.

14 Dateien des F3-korrigierten Architekturpakets stimmen mit den SHA-256-Werten im F3-Original überein. Das eingebettete Golden-Inventar ist mit dem Original bytegleich. Alle acht Quell- und Projektdateien für die wiederholten R1-Proben und den nativen Prüfhost wurden gegen ihre historischen Originale gehasht. [Eingangsnachweis](../artifacts/test-results/g2-04-i2-f1/input-hashes.json)

## Entscheidungen zu den sieben Findings

| R1 | Ursache und Korrektur | Abnahmebeleg |
|---|---|---|
| H01 · korrigiert | Der historische Schnellpfad setzte zuvor nur den Detailtext. Die Navigation veröffentlicht jetzt den verifizierten Pfad, die Seite und den Detailtext zusammen. Beide Copy-Befehle binden Snapshot und Wertquelle vor der asynchronen Übergabe. | Bei `child-value` stimmen Anzeige, Zeilen-Copy und Vollwert-Copy überein. Der Wechsel von Id auf Value und eine Auswahlaufhebung bei bereits wartendem Clipboard-Callback vertauschen den Wert nicht. |
| H02 · korrigiert | Dem kanonischen Parent-Payload fehlte die Zuordnung seiner Kinder. Der Encoder hält nun typisierte Pfade, UTF-16-Offsets, Längen und Typen fest. Das Seitenmodell enthält die tatsächliche Stringlänge. Eine lückenlose Folge gültiger Seiten ergibt eine vollständige Quelle unter demselben Stamp. Beim Lesen eines Ausschnitts prüft der bestehende Payload-Reader auch die übrigen Segmente des Quellteils vor dem Commit. | Jeweils Editoränderung und echte MCP-Lease mit 400 Zeichen aus dem Parent bzw. 180 Zeichen aus den erfassten Seiten. Die Werte enthalten Anführungszeichen, Backslash, Zeilenumbrüche, NUL und isolierte Surrogate. Zusätzlich geprüft wurden fehlende Mittelseite, überlappende Seite, abweichende Länge, falsche Revision, Teil-Payload, gleichnamige Properties in verschiedenen Objekten und Dictionary-Paare mit leerem Schlüssel. |
| M01 · korrigiert | Die bisherige Vollsortierung und wiederholtes `ElementAt` lagen innerhalb eines Host-Schritts. Die ordinale Auswahl behält jetzt höchstens 4.096 Schlüssel pro Sortierlauf. Sie gibt zwischen Scan, Heap-Entnahme und Ausgabe kooperativ ab. Späte Seiten können vom oberen Ende der Ordnung aufgebaut werden. Der Index für die historische Map entsteht in unveränderlichen Blöcken zu 128 Einträgen. | Vollständiger unabhängiger SHA-256-Vergleich des UTF-16-Ausgabewerts für 300.001 Schlüssel. Seite 655 eines Dictionaries mit 65.536 Einträgen stimmt mit den letzten 36 ordinal sortierten Originalschlüsseln überein. Abbruch während Sortierung und Seitennavigation gibt die Reservation frei. |
| M02 · korrigiert | Die allgemeine Navigation setzte zuvor das Berechnungsflag und löste beim späteren Traversieren des Pfades erneut den KCV-Zugriff aus. Die View bietet eine benannte Calculate-Aktion. Rechtspfeil und allgemeines Öffnen führen die erstmalige Berechnung nicht aus. Der berechnete Skalar stellt Länge und Zeicheneinträge bereit. | Der Zähler am Getter-Dispatch zeigt null Aufrufe vor der expliziten Aktion und einen danach. In der echten VM gelingen Länge und Zeichenzugriff auch bei reserviertem Host, anschließend nochmals historisch. `Chars` erscheint als Indexer mit einzelnen Zeichen als Einträgen. |
| M03 · korrigiert | Die bisherige Dispose-Reihenfolge ließ die gehaltene View mit altem Inhalt zurück. Das VM leert seinen fachlichen Zustand und liefert eine letzte Änderungsbenachrichtigung vor dem Abhängen der Empfänger. Die View leert Textbox, lokale Volltextreferenz, Zeilen und Navigation. | 8.192 Zeichen über visuelles Detach und erneutes Einhängen erhalten. Zweifaches fachliches Dispose bei weiter gehaltener View leert Text und Zeilen. Die originale R1-Probe liest die private Volltextreferenz unmittelbar aus. |
| L01 · korrigiert | Die bisherigen Property-Pfade unterschieden überladene Indexer nicht. Ein Property-Schritt enthält deklarierenden Typ, Memberidentität und Basis-Accessor-Slot. Die Getterfreigabe vergleicht den konkreten öffentlichen Getter mit den registrierten Modul-/Metadatenidentitäten des exakten Runtime-Typs. | Zwei überladene Indexer unterscheiden sich in Signatur und Pfad. Ihr Getterzähler bleibt null. Die bestehenden Tests für verborgene Properties, Overrides und nicht zugelassene Collections bestehen. |
| L02 · korrigiert | Der Baum wurde global nach Namen sortiert und zeigte 64 Collection-Einträge. Die Darstellung sortiert nach Deklarationsebene und innerhalb der Ebene ordinal. Eine Seite enthält bis zu 100 Einträge. Die Copy-Sortierung bleibt separat im kanonischen Encoder. | Vergleich der geordneten Memberidentitäten für alle sieben Root-Typen. Tests mit 99, 100 und 101 Einträgen prüfen Anzahl, More und Folgeindex. Alle 19 F3-Goldens bestehen. |

Bei Busy-Erstadmission bleibt die ausgewählte Identität gebunden. Ein sichtbarer Reload-Befehl startet nach Freigabe eine neue reguläre Admission. Der Test hält die echte Host-Reservation, prüft den Busy-Status, gibt sie frei und lädt per Befehl den erwarteten Wert.

## Geänderte Dateien

| Worktree-relativer Pfad | F1-Änderung |
|---|---|
| [CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs) | Slot- und Baumordnung, getrennte Berechnung, kooperative Traversierung, typisierter Index und detached Metadaten |
| [CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs) | Seite und Encoder schrittweise unter dem vorhandenen Host-Read ausführen, Stamp erneut prüfen, Index nach Seal übertragen |
| [CryptoWorkBenchAvalonia/Services/Inspector/InspectorSnapshot.cs](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorSnapshot.cs) | Geprüfte Property-Fenster, unveränderliche Indexseiten und gemeinsame Reader-/Commit-Implementierung |
| [CryptoWorkBenchAvalonia/Services/Inspector/InspectorValueSource.cs](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorValueSource.cs) | Neu. Vollständige Quelle innerhalb genau eines Snapshots bestimmen, Ausschnitte und Zeichenfenster über die gemeinsamen Reader stagen und verlustfrei lesen |
| [CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs](../CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs) | Navigation und Copy binden, historische Seiten nutzen, explizite Berechnung, Reload und fachliches Close |
| [CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs](../CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs) | Benannte Bedienelemente für Berechnung und Reload, Navigation per Tastatur und abschließendes Leeren |
| [CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs](../CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs) | Neu. 27 permanente NUnit-Fälle einschließlich parametrisierter Matrix |
| [docs/G2-04-02-I2-F1-corrections.md](G2-04-02-I2-F1-corrections.md) | Dieser Abschlussbericht |

Ausführungsprotokolle, Original-TRX, Prüfsummen, lokale Kopien der Prüfhosts und der Aufrufadapter für die neue Calculate-Aktion liegen separat unter [F1-Prüfnachweise](../artifacts/test-results/g2-04-i2-f1/). Das vorhandene I1-Host- und Cleanup-Ownership-Modell sowie der native Clipboard-Adapter wurden im Rahmen von F1 über ihre regulären Eintrittspunkte geprüft.

## Testmatrix des Übergabestands

Je Zelle stehen die Resultate als Debug / Release. Die fokussierten Suiten sind Teilmengen oder separate Kontrollläufe und werden nicht zur Gesamtzahl addiert. Skipped stammt aus dem Test-Runner. Weitere NotExecuted ist die Differenz zwischen TRX-Einzelresultaten mit NotExecuted und den bereits als Skipped gezählten Fällen.

| Lauf | Passed | Failed | Skipped | Weitere NotExecuted |
|---|---:|---:|---:|---:|
| Core gesamt | 11.736 / 11.736 | 0 / 0 | 26 / 26 | 1 / 1 |
| GUI gesamt | 1.251 / 1.251 | 0 / 0 | 0 / 0 | 0 / 0 |
| MCP gesamt | 176 / 176 | 0 / 0 | 0 / 0 | 13 / 13 |
| Neue permanente F1-Regressionen | 27 / 27 | 0 / 0 | 0 / 0 | 0 / 0 |
| Bestehende I2-Tests, darin 19 F3-Goldens | 40 / 40 | 0 / 0 | 0 / 0 | 0 / 0 |
| I1 Inspector | 38 / 38 | 0 / 0 | 0 / 0 | 0 / 0 |
| I1 SecretShare-Range | 13 / 13 | 0 / 0 | 0 / 0 | 0 / 0 |
| Unveränderter externer R1-Prüfhost einschließlich R2-Payload-Proben | 23 / 23 | **1 / 1** | 0 / 0 | 0 / 0 |
| Davon ursprüngliche R2-Payload-/Cleanup-Proben | 13 / 13 | 0 / 0 | 0 / 0 | 0 / 0 |
| ComputedReview mit expliziter KCV-Aktion | 2 / 2 | 0 / 0 | 0 / 0 | 0 / 0 |

Beide Desktop-Builds waren erfolgreich. Für Debug wurden nach dem letzten Review die abhängigen Builds, Gesamtsuiten, fokussierten GUI-Tests und Prüfhosts nochmals ausgeführt. Die Core-/Range-Läufe verwenden denselben fachlichen Core-Stand. Die Produkt-DLL in GUI-Tests, Desktop und allen vier unabhängigen Prüfausgaben hat pro Konfiguration denselben SHA-256. Alle 13 Testnamen der ursprünglichen R2-Suite wurden gegen die Einzelresultate der Wiederholung abgeglichen. [TRX-Dateinamen, Ergebnisse und SHA-256](../artifacts/test-results/g2-04-i2-f1/final-test-matrix.json) · [Assembly-Abgleich Debug](../artifacts/test-results/g2-04-i2-f1/assemblies-Debug.json) · [Release](../artifacts/test-results/g2-04-i2-f1/assemblies-Release.json)

**Der eine rote R1-Test ist reproduzierbar.** CalculatedChildrenRemainNavigable öffnet Kcv per allgemeinem OpenAsync und erwartet direkt den berechneten String. Dieser Aufruf muss nach F1-M02 am Root bleiben. Die separate Kopie ersetzt ausschließlich den Aufruf durch CalculateAsync. Dieselben Folgeassertionen bestehen. Beide Original-TRX mit der roten Assertion bleiben erhalten. Die originale späte Seitenprobe verlangt Seite 1.023, die bei der neuen Seitengröße außerhalb der Daten liegt. Den tatsächlichen Grenzfall belegt die neue Probe für Seite 655. [Original Debug](../artifacts/test-results/g2-04-i2-f1/verified-original-Debug.trx) · [Original Release](../artifacts/test-results/g2-04-i2-f1/original-Release.trx) · [Angepasster Aufruf](../artifacts/test-results/g2-04-i2-f1/explicit/ComputedReview.cs)

## Dictionary und Ressourcen

Die Messung umfasst den produktiven ReadChunk einschließlich Aufbau des typisierten Map-Indexes. Die Seitenprobe misst jeweils einen Advance der produktiven Schrittfolge. Gezählt werden besuchte Dictionary-Einträge bei Auswahl, Heap-Entnahme, Ausgabe und Seitennavigation. Vergleiche innerhalb des Heaps und einzelne CPU-Instruktionen sind keine separaten Einträge. Die Allokationswerte erfassen Thread-Allokationen während des jeweiligen Schritts. Die unabhängige Sollsortierung und der abschließende Testvergleich erfolgen außerhalb der Messung.

| Fall | Höchste Zahl Eintragsbesuche pro Schritt D / R | Höchste Allokation pro Schritt D / R | Längster Schritt D / R |
|---|---:|---:|---:|
| 300.001 Schlüssel, vollständiger Copy-Stream | 97 / 97 | 104.952 / 104.752 B | 13,888 / 11,410 ms |
| 65.536 Schlüssel, Seite 655 | 72 / 72 | 112.912 / 112.584 B | 0,188 / 0,169 ms |

In der großen Probe lagen je Konfiguration zwei von 365.589 Schritten über dem 10-ms-Ziel. Der Encoder verwendet einen 64-KiB-Ausgabepuffer. Die Sortierläufe behalten begrenzte Schlüsselreferenzen. Weitere Durchläufe über die Map tauschen Gesamtlaufzeit gegen begrenzten Sortierspeicher. Der veröffentlichte historische Index wächst mit der Eintragszahl. CPU-Planung, GC, außergewöhnlich lange gemeinsame Schlüsselpräfixe und beliebige Objektgrößen erlauben aus diesen Messungen keine allgemeine Latenz- oder RAM-Garantie. Die Cancellation-Proben unter echter Host-Admission unterbrechen die laufende Navigation und Sortierung, prüfen Pending null und führen danach erfolgreich einen weiteren Host-Read aus. [F1-Messwerte Debug](../artifacts/test-results/g2-04-i2-f1/verified-f1-Debug.trx) · [Release](../artifacts/test-results/g2-04-i2-f1/f1-Release.trx)

## Windows-Clipboard und GUI

Windows 10.0.26200, .NET 10.0.12. Der unveränderte native R1-Prüfhost kopierte in beiden Konfigurationen 255 Shares. Das unabhängige vollständige Root-Orakel bestätigte **66.853.345 UTF-16-Codeeinheiten**. Die Prüfung las zusätzlich CF_UNICODETEXT samt Terminator. Debug benötigte beim großen Auftrag 980 ms, Release 902 ms. Die höchste abgetastete temporäre Dateigröße insgesamt betrug jeweils 134.230.986 Bytes. Nach dem Drain waren in beiden Läufen null eigene Payload-Dateien vorhanden. Der Host prüfte Light/Dark/System bei 360 Pixeln, Tab/Enter, Fokus, Textseiten, leeren Text und isolierte Surrogate. Eingebettetes NUL wird beim Windows-Endschritt explizit abgewiesen. Die VM-Copy-Regression prüft die verlustfreie Aufnahme dieses Zeichens davor. [Nativ Debug](../artifacts/test-results/g2-04-i2-f1/verified-native-Debug.log) · [Nativ Release](../artifacts/test-results/g2-04-i2-f1/native-Release.log)

Ein zusätzlicher nativer Prüfhost betätigte die benannten Copy-Buttons an einer echten Window-Instanz. Für historische Parent-Werte mit 400 sowie Zeichenseiten mit 180 Zeichen prüfte er jeweils Editoränderung und aktive MCP-Lease. Erwartung war der ursprüngliche Eingabestring mit Unicode, Zeilenumbrüchen und Escapes. Alle vier Fälle je Konfiguration bestanden im direkten Win32-Vergleich. Die Prüfung verwendete die tatsächliche Windows-Zwischenablage. [Historische Copy Debug](../artifacts/test-results/g2-04-i2-f1/history-native-Debug.log) · [Release](../artifacts/test-results/g2-04-i2-f1/history-native-Release.log)

## Zwischenfehler und internes Review

- In der ersten Wiederholung der Originalproben bestanden 22 von 24. Neben dem geänderten KCV-Bedienvertrag verhinderte ein zusätzlich angefragtes, leeres Zeichenfenster hinter dem Stringende die Erkennung der bereits vollständigen Coverage. Das Fenster trägt jetzt keine Daten zum Bereich bei. Die unveränderte R1-Seitenprobe besteht. [Erster Probenlauf](../artifacts/test-results/g2-04-i2-f1/r1-initial.trx)
- Im ersten permanenten F1-Lauf scheiterten sieben Ordnungsprüfungen. Sie verglichen Reflection-Objekte aus unterschiedlichen ReflectedType-Kontexten. Der korrigierte Test vergleicht die geordnete Sequenz aus Modul und Membertoken mit der unabhängig ermittelten Reihenfolge. Die erwartete Reihenfolge blieb gleich. [Erster F1-Lauf](../artifacts/test-results/g2-04-i2-f1/first-f1.trx)
- Ein lokaler Schreibvorgang mit falscher Textkodierung unterbrach die Bearbeitung der View. Ihr vorheriger vollständiger Quelltext wurde aus der bereits gelesenen Fassung wiederhergestellt. Die F1-Änderungen wurden in UTF-8 angewendet und über beide Builds sowie die nativen UI-Läufe geprüft.
- Im internen Review wurden das Veröffentlichen eines großen Map-Indexes auf begrenzte Blöcke umgestellt, der Fall eines als vollständigen Property-Wert angebotenen Teil-Payloads an der Snapshot-Grenze zurückgewiesen und leere Dictionary-Schlüssel, Pair-Copy und getrennte Pfade für Key und Value geprüft. Weder Host-Admission noch öffentliche Execution-Quoten benötigen hierfür eine Erweiterung.
- Die wiederholten Gesamtläufe liefern keinen Ursachenbeleg zur früher beobachteten intermittierenden Avalonia-Headless-Initialisierung. Diese Einschränkung bleibt für R2 dokumentiert.

## Übergabe an R2

Die unabhängige Nachprüfung kann anhand der gelisteten TRX zuerst historische Copy unter MCP-Lease, den einzelnen absichtlich weiter roten Originalaufruf und den tatsächlichen Host-Schritt beim Dictionary nachvollziehen. Für die I1-Lebensdauer decken die wiederholten Tests unter anderem Reader-Leases, beschädigte bzw. fehlende Segmente, Close vor Veröffentlichung, gesperrte Löschung und Cleanup-Retry ab. Die nativen Prüfhosts decken Windows ab. Manuelle Screenreader-Nutzung, andere Clipboard-Backends, ein Prozessabbruch während des Stagings und produktives Cross-Window-Docking waren keine nachgewiesenen Abnahmen dieser Phase. Produktive Dock-Integration und Layoutmigration verbleiben bei I3. Es gab keine Commits, Pushes oder Merges.
