# G2-04-02-I3-R5 – Unabhängige Abschlussverifikation

**PASS – READY FOR OWNER REVIEW. R4-M01 ist CLOSED.** In zwei frischen nativen Prozessen blieb die Fehlermeldung der jüngsten Parameterkopie sichtbar, auch nachdem der frühere Identifier vollständig in der Windows-Zwischenablage stand. Die Gegenrichtung, normale Teilkopien, sequenzielle Kopien, Retry und das Schließen der View bestanden die gezielten Gegenproben. Die Eigentümerabnahme steht aus.

## Prüfstand und eingesehene Originale

Geprüft wurde der lokale Windows-Worktree `artifacts/worktrees/gui-g2-04` auf `feature/gui-g2-04`. HEAD und lokaler Masterstand waren `32017a08c4771e73c12a50f6e1548476b9bb0a54`, der Index war leer. Die 72 bereits geänderten oder unversionierten Dateien stimmten nach Pfad und SHA-256 mit dem F4-Schlussinventar überein. Der Vergleich mit dem R4-Schlussinventar ergab genau zwei geänderte Quelldateien und den hinzugekommenen F4-Bericht. Aus den Vorher-Kopien und dem aktuellen Inhalt ließ sich der F4-Diff bytegenau rekonstruieren. [Eingangsinventar und Delta](../artifacts/test-results/g2-04-i3-r5/baseline.json) · [F4-Originaldiff](../artifacts/test-results/g2-04-i3-f4/f4-only.diff)

| Geprüfter Eingang | SHA-256 |
|---|---|
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | `E5F30A95C2E5AE0E5C1A6CF8D46760CAF0D2DFED99545DB2222EF19826625635` |
| `CryptoWorkBenchGuiTest/DockInspectorTests.cs` | `6D0DDD74E3B38400BE1DAF3FC315BCDEC89A345B32BEFFF33A07BEF1597540F9` |
| F4-Implementierungsdiff | `C47A26095AAD66564B5BF1632C3B9C79BC8C542CBD0AFA7F6D2FDBC8CE81C01C` |

Alle entscheidenden Originale waren lesbar. In den beiden R4-Protokollen verdrängte der Erfolg des Identifiers die jüngere NUL-Ablehnung nach 543 bzw. 556 ms. Die beiden F4-Korrekturläufe protokollierten jeweils nur die Ablehnung. R5 prüfte beide Berichte, die Vorher-TRX mit den vier erwarteten Fehlschlägen und die sechs F4-End-TRX. Die eigenen R5-Läufe sind unten separat ausgewiesen. [R4-Bericht](G2-04-02-I3-R4-targeted-final-review.md) · [R4-Lauf 1](../artifacts/test-results/g2-04-i3-r4/additional/order1.log) · [R4-Lauf 2](../artifacts/test-results/g2-04-i3-r4/additional/order2.log) · [F4-Bericht](G2-04-02-I3-F4-correction-report.md) · [Originale mit Hashes](../artifacts/test-results/g2-04-i3-r5/originals.json)

## Quellprüfung der Reihenfolge

| Prüffrage | Befund am aktuellen Produktcode |
|---|---|
| Gemeinsamer Zustand und Beginn der Aktion | Alle Tabellen-, Detail- und Parameterfelder entstehen über dieselbe Instanzmethode `Text(...)`. Der Zähler gehört der Variables-View. Jeder nicht leere Copy erhält seine Nummer beim Eingang des UI-Ereignisses, vor Validierung und Schreiben. Eine leere Auswahl startet keinen Vorgang. |
| Später Erfolg und später Fehler | Nach dem asynchronen Schreiben und im Fehlerpfad vergleicht der Handler die eingefangene Nummer mit der aktuellen. Nur der jüngste Vorgang erreicht den bestehenden Shell-Callback. Die synchrone NUL-Ablehnung ist dabei unmittelbar wirksam. |
| Normale Teilkopie, sequenzielle Kopien und Retry | Auch die gewöhnliche Teilkopie erhöht den Zähler. Nach erfolgreicher Validierung übernimmt das Control die unveränderte Standardkopie. Ein später bewusst gestarteter Retry erhält seine eigene Nummer und kann seinen Erfolg melden. |
| Bindung der Daten und tatsächliches Schreiben | Min/Max der Markierung und bei Expansion der Teilstring des jeweiligen Originals stehen vor dem `await` fest. F4 prüft die Nummer erst für die Rückmeldung. Die bereits übergebene Datenübertragung läuft auch bei einer jüngeren Aktion zu Ende. |
| Close, Detach und Dispose | Beim Trennen vom visuellen Baum erhöht die View den Zähler. Eine spätere Wiederverwendung macht die alte Nummer nicht erneut gültig. Dispose entfernt den Callback, die Shell entsorgt die Variables-View beim Abbau. |
| Ausnahmebehandlung | Aufruf und abgewarteter Task der erweiterten Kopie liegen im selben `try/catch`. Ein älterer Fehler wird auch dann behandelt, wenn seine Meldung überholt ist. Im geprüften Pfad ergab sich kein neuer Ausnahme- oder Lebensdauerbefund. |

[Copy-Handler](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L109) · [Zell- und Detailfelder](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L143) · [Detach und Dispose](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L187) · [Verbindung zur Shell](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs#L57)

## Eigene native Reproduktion

Zwei separat gestartete Release-Prozesse öffneten die produktive Shell. Die frisch gebaute Produkt-DLL in Desktop, GUI-Testausgabe und isoliertem Prüfhost hatte denselben Hash. R5 nutzte die bestehende Host-Infrastruktur und ersetzte die feste Sperrdauer durch ein Freigabesignal. Der externe Win32-Prozess hielt das Clipboard bis zur ausdrücklich bestätigten Fehlermeldung offen. [Binärvergleich](../artifacts/test-results/g2-04-i3-r5/additional/binaries.json) · [Prüfablauf](../artifacts/test-results/g2-04-i3-r5/native/IndependentChecks.cs) · [Sperrprozess](../artifacts/test-results/g2-04-i3-r5/native/Program.cs)

Der GUI-Parser erzeugte in jedem Lauf eine KDF-SP800-108-COUNTER-Variable mit 4.096 UTF-16-Einheiten im Identifier und 177 Einheiten im zitierten `#LABEL`, einschließlich NUL an Index 171. Der Host verifizierte einen pro Lauf eindeutigen Sentinel per Win32, markierte den gesamten verkürzten Identifier und löste die produktive Copy-Aktion aus. Bei weiter aktiver Sperre kopierte er die voll markierte, sichtbare ParametersGrid-Zelle. Die Clipboard-Sequenznummer war beim NUL-Fehler noch unverändert. Die folgende Tabelle verwendet Millisekunden seit Start des jeweiligen Prüfprozesses.

| Ereignis | Lauf 1 · PID 42996 | Lauf 2 · PID 39712 |
|---|---:|---:|
| Sperre des anderen Prozesses bestätigt | 2.202 | 2.222 |
| Identifier-Copy ausgelöst | 2.203 | 2.224 |
| Jüngerer Parameter-Copy ausgelöst | 2.329 | 2.352 |
| NUL-Ablehnung gemeldet / am Shell-Status geprüft | 2.330 / 2.332 | 2.353 / 2.354 |
| Freigabe angefordert | 2.333 | 2.355 |
| Geänderte Windows-Clipboard-Sequenz beobachtet | 2.436 | 2.457 |
| Vollständiger Identifier per Win32 und Avalonia bestätigt | 2.443 | 2.464 |
| Letzte Prüfung des sichtbaren Shell-Status | 2.761 | 2.810 |

In beiden Prozessen blieb `Copy failed. Clipboard text cannot preserve embedded NUL.` die einzige Meldung. Die Zwischenablage enthielt nach Freigabe exakt den früheren Identifier. Zeile, beide Originalreferenzen, beide Markierungen 0–128 und die Inspector-Seite waren erhalten. Anschließend nahm das Parameterfeld Fokus und eine neue Teilmarkierung an. Nach Wiederherstellung der Markierung las der Host erneut den Shell-Status und den Clipboard-Inhalt. Beide Prozesse endeten mit Exit 0. [Lauf 1 mit Zeitfolge](../artifacts/test-results/g2-04-i3-r5/additional/order1.log) · [Lauf 2 mit Zeitfolge](../artifacts/test-results/g2-04-i3-r5/additional/order2.log) · [Exitcodes](../artifacts/test-results/g2-04-i3-r5/additional/commands.log)

## Direkte Gegenproben und Aussagekraft der Tests

Die sechs neuen permanenten F4-Fälle senden Strg+C an echte fokussierte, realisierte Felder. Der Testadapter hält die erste Datenübertragung gezielt zurück und lässt spätere Kopien direkt zum Headless-Clipboard durch. Nach kontrollierter Freigabe oder Ausnahme lässt der Test die UI-Fortsetzung laufen und prüft den sichtbaren Status und die tatsächlichen Daten. R5 hat diese Fälle und die beiden bestehenden Tests für Retry und NUL ausgeführt. [Neue Tests und Adapter](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L237) · [Bestehende Retry- und NUL-Tests](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L170)

| Permanenter F4-Fall | Geprüftes Ergebnis | Originaler Rotlauf gegen R4 |
|---|---|---|
| `NewerNulErrorSurvivesOlderSuccessfulCopy` | NUL-Fehler bleibt sichtbar. Zunächst steht der Sentinel im Clipboard, nach der Freigabe der ursprüngliche Id-Suffix. | Fehlgeschlagen, alter Erfolg mit 253 Zeichen |
| `NewerSelectionSupersedesOlderFailedCopy(True)` | Erfolg der neueren Expansion und ihre 299 Zeichen bleiben nach dem älteren Fehler erhalten. | Fehlgeschlagen, alter Fehler sichtbar |
| `NewerSelectionSupersedesOlderFailedCopy(False)` | Die gewöhnliche Teilkopie schreibt vier Einheiten. Der bestehende Status bleibt auch nach dem älteren Fehler erhalten. | Fehlgeschlagen, alter Fehler sichtbar |
| `SequentialSuccessfulSelectionCopiesReportEachResult` | Beide Kopien liefern der Reihe nach den richtigen Text und die dazugehörige Zeichenzahl. | Bestanden |
| `InactiveVariablesViewDoesNotReportPendingCopy(False)` | Nach Pane-Close bleibt der Status erhalten. Der ursprünglich gestartete Schreibvorgang schließt erfolgreich ab. | Fehlgeschlagen, verspäteter Erfolg mit 256 Zeichen |
| `InactiveVariablesViewDoesNotReportPendingCopy(True)` | Nach Dispose bleibt die alte Rückmeldung ebenfalls aus und der ursprüngliche Text wird geschrieben. | Bestanden |

Die Original-TRX enthält acht Fälle, darunter die zwei bestehenden Absicherungen. Die vier roten Fälle belegen, dass die Tests die ursprüngliche Rückmelderegression und ihre Varianten erkennen. Sequenzielle Kopien und Dispose sichern bereits korrektes Verhalten. Beim expliziten Retry prüft der bestehende Test zuerst Ablehnung, Sentinel und Zeilenreferenz, danach Erfolg und exakten Suffix. Beim NUL-Fall bleiben für zwei unzulässige Markierungen Status und Sentinel korrekt. Gültige Präfix- und Suffixkopien gelingen. [Originale Vorher-TRX](../artifacts/test-results/g2-04-i3-f4/red/before.trx) · [R5-Auswertung des Rotlaufs](../artifacts/test-results/g2-04-i3-r5/red-original.json)

## Debug und Release

Desktop- und GUI-Testprojekt wurden in beiden Konfigurationen neu gebaut. Alle vier Builds und alle sechs Testläufe bestanden im ersten Versuch. Der Vergleich der F4- und R5-TRX nach Testklasse und parametrisiertem Fallnamen ergab je Satz dieselben Identitäten. Alle sechs neuen Fälle sind in jedem Satz enthalten. Die beiden kleineren Sätze sind jeweils Teil der vollständigen Suite. In R5 trat kein Headless-Initialisierungsfehler auf.

| Konfiguration | Clipboard und neue Regressionen | Fokussierte GUI-Suite | Vollständige GUI-Suite |
|---|---:|---:|---:|
| Debug | 21/21 | 122/122 | 1.277/1.277 |
| Release | 21/21 | 122/122 | 1.277/1.277 |

[TRX-Vergleich, Zähler und Hashes](../artifacts/test-results/g2-04-i3-r5/final/matrix.json) · [Originale R5-TRX und Buildprotokolle](../artifacts/test-results/g2-04-i3-r5/final/) · [Befehle und Exitcodes](../artifacts/test-results/g2-04-i3-r5/final/commands.log)

## Frühere Befunde und Grenzen der Eigentümerabnahme

| Befunde | Status | Bezug zum R5-Ergebnis |
|---|---|---|
| R1-H01 · großer Value, R2-H01 · langer Identifier, R2-M01 · großer KDF-Parameter | **CLOSED** | F4 ändert den Rückmeldepfad der Auswahlkopie. Vorschauerzeugung, Bindungen und Quellenprojektion stimmen mit dem vorherigen Stand überein. Die vorhandenen Fälle für begrenzte Value-Anzeige, identische Identifier-Vorschauen, historische Werte und Parameterzellen sind im fokussierten R5-Satz enthalten. |
| R1-M01 · geschütztes v2 und Migration | **CLOSED** | Der F4-Diff enthält keine Persistenzänderung. Die Fälle für temporäres Startversagen, einen konkurrierenden v2-Gewinner und unlesbares v2 liefen im fokussierten R5-Satz. |
| R3-H01 · Clipboard-Belegung, R3-M01 · eingebettetes NUL | **CLOSED** | Fehlerbehandlung, Retry, NUL-Validierung und Auswahlquelle blieben im direkt betroffenen Pfad wirksam. Die neuen nativen Läufe bestätigen den Erhalt des NUL-Fehlers beim Abschluss eines bereits laufenden Schreibvorgangs. |

Die nativen Leistungs- und Migrationsnachweise sowie die früheren 3,5-s-Sperrversuche stammen aus den Vorstufen. R5 hat die dauerhaften Regressionstests und die zwei gezielten nativen Überlappungen ausgeführt. Die breiten Core-/MCP-Nachweise und der native 255-Share-Clipboard-Vergleich bleiben ebenfalls übernommen. [Unabhängige Vorbefunde und Originalverweise](G2-04-02-I3-R4-targeted-final-review.md) · [F4-Nachweise](G2-04-02-I3-F4-correction-report.md)

**Offen für den Owner** bleiben native Prüfungen bei **100 % und 125 % DPI**, die **manuell gehörte Screenreader-Sprachausgabe** und der **spezielle fensterübergreifende Maus-Redock**. Die früher bei 150 % beobachtete Rückkehr eines Tabs innerhalb des Hauptfensters ist ein eigener Prüffall.

**PASS – READY FOR OWNER REVIEW.** Ich empfehle dem Owner, I3 zur Abnahme zu prüfen. R4-M01 ist geschlossen. Im vereinbarten Prüfumfang trat kein neuer materieller Produktfehler auf. Der Owner entscheidet über die Abnahme mit den aufgeführten Grenzen oder eine gesonderte Validierung. Der abschließende Hashvergleich bestätigt den vorgefundenen Arbeitsstand und die eingesehenen Originale. Als neuer sichtbarer Arbeitspfad kam ausschließlich dieser Bericht hinzu. [Schlussabgleich](../artifacts/test-results/g2-04-i3-r5/end.json)
