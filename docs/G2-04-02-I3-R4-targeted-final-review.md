# G2-04-02-I3-R4 – Gezieltes unabhängiges Abschlussreview

**CORRECTION REQUIRED.** R3-H01 und R3-M01 sind **CLOSED**. In je zwei frischen Windows-Prozessen blieb die Anwendung bei belegter Zwischenablage bedienbar und wies nicht darstellbaren Text ohne Änderung der Zwischenablage zurück. Ein neuer, zweimal nativ reproduzierter Befund blockiert die Empfehlung zur Abnahme. Bei zwei überlappenden Kopieraktionen überschreibt der Erfolg der älteren Aktion die sichtbare Fehlermeldung der jüngeren. Wer sich auf den abschließenden Shell-Status verlässt, kann den Identifier einfügen, obwohl zuletzt der Parameterwert kopiert wurde. Die Entscheidung über I3 bleibt beim Owner.

## Neuer Befund R4-M01 · MEDIUM · offen

**Fundstelle.** In `DockVariablesView.cs` erfasst der Handler ab Zeile 108 die Auswahl bei Aufruf. Zeilen 124–132 validieren die Auswahl, warten auf den Windows-Schreibvorgang und melden das Ergebnis. Während des Wartens kann ein anderes Feld derselben View bereits einen neueren Fehler melden. Beide Handler schreiben über denselben Callback in den Shell-Status. [Handler](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L108) · [Anschluss an die Shell](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs#L57)

**Reproduktion.** Eine vom GUI-Parser erzeugte KDF-Zeile mit einem gültigen Identifier von 4.096 UTF-16-Einheiten und dem unten beschriebenen 177-Einheiten-NUL-Label auswählen. Einen eindeutigen Sentinel verifizieren. Ein zweiter Prozess öffnet das Windows-Clipboard für 550 ms und bestätigt die Sperre. Die vollständige Identifier-Vorschau kopieren. Nach Rückkehr des Ereignisaufrufs, während der Schreibvorgang noch aussteht, rund 100 ms später die vollständige Vorschau von `#LABEL` über die realisierte Parameterzelle kopieren. Beide Aufrufe verwenden die produktive `SelectableTextBlock.Copy()`-Aktion auf dem UI-Thread. Nach Freigabe den Status und den nativen Clipboard-Inhalt lesen. Die Zeitangaben laufen ab Prozessstart.

| R4-Prozess | Identifier-Copy gestartet | Jüngerer NUL-Fehler sichtbar | Älterer Erfolg sichtbar | Letzter Status und Win32-Inhalt |
|---|---:|---:|---:|---|
| 1 | 1.586 ms | 1.700 ms | 2.243 ms | `Copied 4'096 characters.` · exakt der frühere Identifier |
| 2 | 1.530 ms | 1.642 ms | 2.198 ms | `Copied 4'096 characters.` · exakt der frühere Identifier |

**Soll.** Nach der jüngsten, abgelehnten Benutzeraktion bleibt erkennbar, dass ihr Parameterwert nicht kopiert wurde. **Ist.** Nach 543 bzw. 556 ms verschwand die korrekte Ablehnung. Der ältere Identifierschreibvorgang war tatsächlich erfolgreich. Die jüngere Aktion selbst schrieb keine NUL-Daten. Es gab je eine Meldung pro Aktion, doch der einzelne sichtbare Status ordnet das letzte Ergebnis für den Benutzer falsch zu. Beide Prozesse endeten regulär. [Lauf 1](../artifacts/test-results/g2-04-i3-r4/additional/order1.log) · [Lauf 2](../artifacts/test-results/g2-04-i3-r4/additional/order2.log) · [Prüfablauf](../artifacts/test-results/g2-04-i3-r4/ordering/IndependentChecks.cs)

**Abnahmewirkung und kleinster Korrekturumfang.** Die falsche Erfolgsanzeige betrifft den von F3 neu eingeführten Shell-Rückmeldepfad. Ein Benutzer kann unmittelbar danach den falschen Wert weiterverwenden. Es trat weder ein Absturz noch eine Änderung des Quellmodells auf. Für die Freigabe muss die View das Feedback überlappender Auswahlkopien feldübergreifend der jüngsten Aktion zuordnen. Der Abschluss einer älteren Aktion darf deren Fehler nicht verdrängen. Dafür genügt eine gezielte Wiederholung dieser Folge zusammen mit dem bisherigen Einzel-Retry. Die Beobachtung begründet keinen Umbau der Clipboard- oder Docking-Architektur.

## Prüfstand und Herkunft

Lokaler Windows-Worktree `artifacts/worktrees/gui-g2-04`, Branch `feature/gui-g2-04`. HEAD und lokaler `master` standen auf `32017a08c4771e73c12a50f6e1548476b9bb0a54`, der Index war leer. Die 70 bereits geänderten oder unversionierten Arbeitspfad-Dateien stimmten mit der F3-Ausgangsliste und dem F3-Delta überein. Der aus den vier Vorher-Kopien und dem aktuellen Quellstand erneut erzeugte F3-Diff war bytegleich, SHA-256 `8541BBB514C88028F4EA51A04A3836634B0268F190693A48F9B08D831EE4462F`. Zum Delta kam seinerzeit der F3-Bericht hinzu. [R4-Eingangsinventar](../artifacts/test-results/g2-04-i3-r4/status-before.json) · [Isolierter Diff](../artifacts/test-results/g2-04-i3-r4/f3-only.diff) · [Vorher-/Nachher-Hashes](../artifacts/test-results/g2-04-i3-f3/changed-files.json)

| Tatsächlicher F3-Quelleingang | SHA-256 des geprüften aktuellen Inhalts |
|---|---|
| CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs | `16230FACEC31331BADE3E2AE8A0CD8C86C8B5A072CDA64AE442A4DFD30D08EF9` |
| CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs | `0A216FFECAB8C2377797D676AE6EEC1D99DDEF1472868B1869AA409B4736593C` |
| CryptoWorkBenchAvalonia/Services/Inspector/InspectorClipboard.cs | `352572762CF3234E9D0A7FAB239A599D26C6E659D7DD5F22B2EC2175A91DF98B` |
| CryptoWorkBenchGuiTest/DockInspectorTests.cs | `1368D2B6EC9C78919FD4B42F3EE1A0AA040F6A052F0F41E8CD50951786EF793B` |

**Eingesehene Originale.** Die zwei R3-Absturzprotokolle zeigen die unbehandelte COMException aus Avalonia Win32. Beim R3-NUL-Fall enthielten Avalonia und Win32 anschließend nur das 171-Einheiten-Präfix. Die F3-Einzelproben, vier F3-TRX, deren Matrix, beide Berichte und der ältere Vertrag zur gebundenen Copy-Quelle waren zugänglich. Der Vertrag bindet einen laufenden Copy an seinen Ursprung. R4-M01 betrifft die spätere Zuordnung der sichtbaren Rückmeldung. [Quellen und Hashes](../artifacts/test-results/g2-04-i3-r4/originals.json) · [R3](G2-04-02-I3-R3-independent-re-review.md) · [F3](G2-04-02-I3-F3-correction-report.md) · [Historische Copy-Quelle](G2-04-02-I2-R1-independent-review.md)

**Eigene Nachweise.** Separate Release-Prüfprozesse öffneten die produktive Dock-Shell mit echten Avalonia-Windows-Controls und dem System-Clipboard. Der isolierte Host steuerte Auswahl und Aktionen und registrierte die Shell-Meldungen. Produkt-DLL in Desktop, Testausgabe und beiden nativen Hosts war hashgleich. Beim zusätzlichen Host für die Reihenfolge wurde eine lokale Variablenkollision im Prüfcode vor dem ersten Lauf behoben. Beide Buildprotokolle liegen im Nachweisordner. [Build- und Binärnachweise](../artifacts/test-results/g2-04-i3-r4/final/) · [Native Prozesse und Exitcodes](../artifacts/test-results/g2-04-i3-r4/additional/commands.log)

## R3-H01 · CLOSED

Bei einer erweiterten Auswahl setzt der Handler `Handled` vor dem Schreiben, prüft das gewählte Original und umfasst sowohl den Aufruf als auch dessen `await` mit demselben `try/catch`. Ein synchroner Fehler vor Rückgabe des Tasks und eine spätere Task-Ausnahme gelangen so zur bestehenden Shell-Fehleranzeige. Erst das erfolgreiche `await` führt zur Erfolgsmeldung. Die View löst den Feedback-Callback bei `Dispose`. Gültige gewöhnliche Teilmarkierungen nutzen weiterhin den normalen Control-Copy. [Produktpfad](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L108)

Zwei bestätigte externe Win32-Sperren von jeweils 3,5 s, zwei frische Prozesse, je eine Vollselektion 0–128 aus einem 4.096-Einheiten-Original. Beide meldeten `Copy failed. OpenClipboard Failed (0x800401D0 (CLIPBRD_E_CANT_OPEN))`. Sentinel und Windows-Clipboard-Sequenznummer blieben gleich. Selektierte Variable, Inspector-Seite, Editordokument und Revision blieben erhalten. Nach Freigabe und neu gesetztem Kontrollwert kopierte derselbe Benutzerpfad jedes Mal alle 4.096 Einheiten ordinal exakt. Je ein Fehler und ein Erfolg nach dem bewussten Retry, keine weitere Meldung im Beobachtungsfenster, beide Exit 0. [Lauf 1](../artifacts/test-results/g2-04-i3-r4/additional/contention1.log) · [Lauf 2](../artifacts/test-results/g2-04-i3-r4/additional/contention2.log)

## R3-M01 · CLOSED

Der Windows-Validator prüft bei Expansion `source[start..]` vor dem ersten Schreibzugriff. Bei gewöhnlicher Auswahl prüft er `SelectedText`, beim Inspector den vollständigen gewählten Wert. Die Fehlermeldung bezeichnet das eingebettete NUL ausdrücklich. [Auswahl](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L115) · [Gemeinsamer Validator](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorClipboard.cs#L13)

Zwei frische Prozesse parsten `KDF-SP800-108-COUNTER` mit `#PRF:HMAC-SHA256`, `#OUTLEN:128` und einem zitierten Label aus 170 q, U+0000 und `tail`. Mit Anführungszeichen ergaben sich 177 UTF-16-Einheiten, NUL bei Index 171. In der realisierten ParametersGrid-Zelle kopierte ein gültiges Präfix exakt sieben Einheiten. Die volle 128-Einheiten-Vorschau einschließlich Ellipse meldete einmal `Copy failed. Clipboard text cannot preserve embedded NUL.` Win32 und Avalonia lasen denselben vorab verifizierten Sentinel, die Clipboard-Sequenznummer blieb gleich. Referenz und Inhalt des Originals, Zeile und Inspector-Seite blieben erhalten. Der nächste gültige Teil-Copy gelang. „Copy Value“ im Inspector lehnte denselben Vollwert ebenfalls unter Erhalt seines Sentinels ab.

Das Suffix hinter Index 171 ist in der verkürzten Zelle nicht separat markierbar. Auf der Volltextseite desselben Werts ergab die markierte Spanne [172, 177) exakt die fünf Einheiten `tail"`. Als Gegenprobe in der ParametersGrid-Zelle enthielt ein zweites parsererzeugtes Label derselben Länge das NUL bei Index 4. Die rückwärtige Markierung 128→5 kopierte alle 172 nachfolgenden Einheiten exakt. Beide Prozesse Exit 0. [Lauf 1](../artifacts/test-results/g2-04-i3-r4/additional/nul1.log) · [Lauf 2](../artifacts/test-results/g2-04-i3-r4/additional/nul2.log)

## Auswahl, Quellenbindung und permanente Tests

- Die echte Mausmarkierung vom rechten Identifier-Ende nach links ergab Anker 128 und Ende 13. Strg+C lieferte 4.083 Einheiten exakt. Strg+A und Strg+C im selben Feld lieferten alle 4.096. Auch Ellipse allein, überlappender Suffix, gewöhnliches Präfix, Leerwahl, CRLF, kurze Texte sowie Parameter-Schlüssel und zitierte Werte stimmten mit den Originalen bzw. dem Sentinel überein. [Maus und Tastatur](../artifacts/test-results/g2-04-i3-r4/additional/gesture.log) · [Einzelvergleiche](../artifacts/test-results/g2-04-i3-r4/additional/selections.log)
- Zwei Identifier mit gleichen sichtbaren Präfixen und 65.536 Einheiten blieben in Tabellen- und Inspector-Aktionen unterscheidbar. Nach einer Dokumentänderung kopierte die historische Vollwertseite die ursprüngliche Id. Sechs unmittelbare Zeilenwechsel mit Copy im selben UI-Durchlauf, die aktualisierte Quelle bei identischem Vorschautext im selben Control sowie eine separate Inspector-Textmarkierung lieferten jeweils den erwarteten Wert. Der KDF-Vollwert einschließlich Anführungszeichen umfasste 1.048.578 Einheiten. [Identität und Historie](../artifacts/test-results/g2-04-i3-r4/additional/identity.log) · [Sofortwechsel](../artifacts/test-results/g2-04-i3-r4/additional/immediate.log) · [KDF](../artifacts/test-results/g2-04-i3-r4/additional/parameter.log)
- `ExpandedSelectionReportsAsyncWriteFailureAndCanRetry` sendet Strg+C an das fokussierte echte Detail-Control, lässt den vom Proxy gelieferten Schreib-Task fehlschlagen und prüft die sichtbare Meldung, den Sentinel, die Zeilenreferenz sowie den exakten Suffix beim Retry. R3 hätte die Ausnahme aus dem UI-Ereignis entweichen lassen. [Test ab Zeile 170](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L170)
- `NulRejectionPreservesClipboardAndAllowsRepresentableSelections` sendet Strg+C an ein echtes Feld, prüft die Ablehnung für die Vollauswahl und einen kurzen NUL-haltigen Ausschnitt, anschließend gültiges Präfix, rückwärtigen Suffix und Originalreferenz. Gegen R3 würden die erwartete explizite Ablehnung und der Sentinel-Erhalt scheitern. Die separaten Win32-Läufe ergänzen den Headless-Test um Parser, reale Zelle und zweite Lese-API. Beide permanenten Tests verwenden aufeinanderfolgende Aktionen mit abgeschlossenem Ergebnis. Die Reihenfolge zweier noch überlappender Aktionen bleibt darin ungeprüft. [Test ab Zeile 209](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L209)

## Frühere R1-/R2-Befunde

Native Stichproben bei 150 % im Drei-Spalten-Layout mit sichtbaren Variables und Inspector. Die beiden Zeitspalten messen von der Auswahl bis zur nutzbaren Seite und den größten beobachteten Input-Verzug. Speicher ist das abgetastete maximale Prozess-Working-Set. Die F3-Werte sind Vergleichsdaten aus dem früheren Lauf.

| Befund | Eingabe in UTF-16-Einheiten | R4 Auswahl / Input | F3 Auswahl / Input | R4 Speicher | Bediente Input-Aufträge |
|---|---:|---:|---:|---:|---:|
| R1-H01 · CLOSED | Value 1.048.576 | 75 / 35 ms | 76 / 33 ms | 209,8 MiB | 36/36 |
| R2-H01 · CLOSED | Identifier 65.536 | 86 / 66 ms | 101 / 71 ms | 185,6 MiB | 20/20 |
| R2-M01 · CLOSED | KDF-Label 1.048.576 | 103 / 70 ms | 116 / 66 ms | 218,3 MiB | 21/21 |

Alle drei erreichten einen nutzbaren Frame und endeten regulär. Der Inspector kopierte den vollständigen Value mit 1.048.576 Einheiten exakt. Die große Parameterzelle war auf dem Bildschirm realisiert. [R4-Messdaten](../artifacts/test-results/g2-04-i3-r4/smoke/analysis.json) · [Value-Copy](../artifacts/test-results/g2-04-i3-r4/smoke/unselected-1048576.log) · [F3-Vergleich](../artifacts/test-results/g2-04-i3-f3/smoke/analysis.json)

**R1-M01 · CLOSED.** Die permanenten Fälle für temporäres Startversagen, unlesbares v2 und einen konkurrierenden v2-Gewinner liefen im fokussierten Satz. Im nativen Migrationsfall meldete Prozess 1 den simulierten Schreibfehler. Nach normalem Schließen fehlte v2 weiterhin. Prozess 2 installierte v2 und stellte unter anderem das eigenständige Info-Fenster, den aktiven Output-Tab und den geschlossenen Inspector wieder her. Die installierte Projektion stimmte einschließlich Gruppierung, aktiver Tabs, Proportionen und Fenstergeometrie mit der Referenz überein. v1 war nach beiden Starts bytegleich. [Kontrollen](../artifacts/test-results/g2-04-i3-r4/additional/recovery.log) · [Projektionsvergleich](../artifacts/test-results/g2-04-i3-r4/additional/projection.log) · [Dateien beider Starts](../artifacts/test-results/g2-04-i3-r4/recovery/)

## Debug- und Release-Validierung

Desktop- und GUI-Testprojekt wurden in Debug und Release neu gebaut. Alle vier Builds endeten mit Exit 0. Der Abgleich von Testklasse und vollständig parametrisiertem Testnamen zwischen R4-TRX und den jeweiligen F3-Originalen ergab in jedem Satz dieselben Testidentitäten. Beide permanenten F3-Fälle bestanden in allen vier Erstläufen. Die 116 fokussierten Fälle sind in den 1.271 GUI-Fällen enthalten. [Befehle und Exitcodes](../artifacts/test-results/g2-04-i3-r4/final/commands.log) · [Matrix der Erstläufe](../artifacts/test-results/g2-04-i3-r4/final/matrix.json) · [Identitätsvergleich mit TRX-Hashes](../artifacts/test-results/g2-04-i3-r4/final/identity-comparison.json)

| Konfiguration | Fokussiert | GUI gesamt, erster Lauf | GUI gesamt, Wiederholung |
|---|---:|---:|---:|
| Debug | 116/116 | 1.271/1.271 | entfiel |
| Release | 116/116 | 1.270/1.271 | 1.271/1.271 |

Im ersten Release-Gesamtlauf scheiterte `ShortPrintIsVisibleAndSourceButtonNavigates(911,512)` bereits in `HeadlessUnitTestSession.EnsureIsolatedApplication → AvaloniaHeadlessPlatform.Initialize → Dispatcher.VerifyAccess`. Die Fehlermeldung weist einen Zugriff vom falschen Thread beim Aufbau der Headless-Anwendung aus. In zwei frischen Release-Wiederholungen der Testmethode bestanden jeweils alle drei Parameterfälle. Anschließend bestand die vollständige Release-Wiederholung. Der Erstfehler bleibt dokumentiert. Sein Stack liefert keinen Anhaltspunkt für einen Fehler im F3-Copy-Handler. [Erster Release-Lauf](../artifacts/test-results/g2-04-i3-r4/final/gui-Release.trx) · [Wiederholung 1](../artifacts/test-results/g2-04-i3-r4/repeat/shortprint1.trx) · [Wiederholung 2](../artifacts/test-results/g2-04-i3-r4/repeat/shortprint2.trx) · [Release gesamt wiederholt](../artifacts/test-results/g2-04-i3-r4/repeat/gui-Release.trx)

## Grenzen und Empfehlung an den Owner

Für die Eigentümerabnahme bleiben native 100 % und 125 % DPI, die manuell gehörte Screenreader-Sprachausgabe und die spezielle fensterübergreifende Maus-Redock-Sequenz offen. Die früher bei 150 % dokumentierte Rückkehr eines Tabs innerhalb des Hauptfensters ist ein eigener Prüffall. Breite Core-/MCP-Regressionen und der native 255-Share-Clipboard-Nachweis sind aus früheren Reviews übernommen. [Übernommene Grenzen](G2-04-02-I3-R3-independent-re-review.md)

Der Schlussabgleich umfasst den bestehenden uncommittierten Arbeitsstand, die geprüften Originale, HEAD, Index und den lokalen Masterstand. Im sichtbaren Arbeitsstand kam allein dieser Bericht hinzu. R4-Belege einschließlich temporärer Hosts liegen im lokalen Nachweisverzeichnis. [Schlussinventar](../artifacts/test-results/g2-04-i3-r4/status-end.json) · [R4-Nachweise](../artifacts/test-results/g2-04-i3-r4/)

**CORRECTION REQUIRED.** Ich empfehle dem Owner, I3 bis zur gezielten Korrektur von R4-M01 zurückzustellen. Das Freigabekriterium ist, dass die sichtbare Ablehnung der jüngsten Auswahlkopie auch nach Abschluss eines älteren Schreibvorgangs korrekt bleibt. Die bisherigen R1-, R2- und R3-Befunde bleiben geschlossen.
