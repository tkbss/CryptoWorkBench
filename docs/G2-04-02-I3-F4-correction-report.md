# G2-04-02-I3-F4 – Reihenfolge der Clipboard-Rückmeldungen

**R4-M01 ist im F4-Stand korrigiert.** In zwei frischen Windows-Prozessen blieb die Fehlermeldung der jüngsten Parameterkopie sichtbar, nachdem eine ältere Identifier-Kopie erfolgreich abgeschlossen war. Win32 und Avalonia lasen in beiden Fällen den vollständigen früheren Identifier. Auch die umgekehrte Reihenfolge ist abgedeckt. Ein verspäteter Fehler kann die Erfolgsmeldung einer neueren Auswahlkopie nicht mehr verdrängen. [Native Gegenprobe 1](../artifacts/test-results/g2-04-i3-f4/additional/order1.log) · [Native Gegenprobe 2](../artifacts/test-results/g2-04-i3-f4/additional/order2.log)

## Prüfstand und Änderungsumfang

Worktree `artifacts/worktrees/gui-g2-04`, Branch `feature/gui-g2-04`, HEAD und lokaler Masterstand `32017a08c4771e73c12a50f6e1548476b9bb0a54`, Index leer. Alle 71 bereits geänderten oder unversionierten Dateien stimmten zu Beginn nach Pfad und SHA-256 mit dem R4-Schlussinventar überein. Der vollständige R4-Bericht, die beiden ursprünglichen Fehlerprotokolle, der F3-Diff und der Anschluss des Shell-Callbacks waren verfügbar. Die beiden ursprünglichen Protokolle zeigen, wie der ältere Erfolg den neueren NUL-Fehler nach 543 bzw. 556 ms verdrängte. [R4-Bericht](G2-04-02-I3-R4-targeted-final-review.md) · [Eingangsinventar](../artifacts/test-results/g2-04-i3-f4/baseline.json) · [Hashes der Originalnachweise](../artifacts/test-results/g2-04-i3-f4/original-evidence.json)

| Geänderte Quelle | SHA-256 vor F4 | SHA-256 nach F4 |
|---|---|---|
| DockVariablesView.cs | `16230FACEC31331BADE3E2AE8A0CD8C86C8B5A072CDA64AE442A4DFD30D08EF9` | `E5F30A95C2E5AE0E5C1A6CF8D46760CAF0D2DFED99545DB2222EF19826625635` |
| DockInspectorTests.cs | `1368D2B6EC9C78919FD4B42F3EE1A0AA040F6A052F0F41E8CD50951786EF793B` | `6D0DDD74E3B38400BE1DAF3FC315BCDEC89A345B32BEFFF33A07BEF1597540F9` |

Der berichtete Diff vergleicht beide Dateien mit ihren Kopien unmittelbar vor F4. Das Schlussinventar enthält zusätzlich diesen Bericht. Der Hash des Berichts steht im separaten Änderungsnachweis. [F4-Diff](../artifacts/test-results/g2-04-i3-f4/f4-only.diff) · [Geänderte Dateien](../artifacts/test-results/g2-04-i3-f4/changed-files.json) · [Schlussinventar](../artifacts/test-results/g2-04-i3-f4/status-end.json)

## Verhalten der Variables-View

Jede nicht leere Auswahlkopie erhält beim Eingang des UI-Ereignisses die nächste laufende Nummer der View. Identifier, Value und Parameterzellen teilen sich diesen Zähler. Sowohl der Erfolgs- als auch der Fehlerpfad vergleichen die eigene Nummer unmittelbar vor der Rückmeldung mit dem aktuellen Stand. Ein leerer Auswahlbereich startet keinen Kopiervorgang. Eine gewöhnliche Teilmarkierung macht älteres Feedback ungültig und nutzt anschließend wie bisher den normalen Copy-Pfad des Controls. Auswahlgrenzen und expandierter Originaltext werden vor dem asynchronen Schreiben erfasst. Ein bereits gestarteter Schreibvorgang darf mit seinem ursprünglichen Text fertig werden. [Copy-Ereignis](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L109)

Bei visueller Trennung erhöht die View den Zähler. So kann ein vor dem Schließen gestarteter Vorgang auch nach einem späteren Wiederanzeigen keine alte Meldung veröffentlichen. Beim Entsorgen entfernt die View den bestehenden Feedback-Callback. Die dauerhaften Tests decken das Schließen des Variables-Panes und das direkte Entsorgen der View getrennt ab. [Lebensdauer der View](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L187)

## Dauerhafte Regressionstests

Sechs neue Testfälle verwenden reale Variables-Felder, Strg+C und den sichtbaren Shell-Status. Für überlappende Aktionen hält ein Testadapter genau die erste Schreibanforderung mit ihrer ursprünglichen Datenübertragung zurück. Weitere Kopien können währenddessen auf das Headless-Clipboard schreiben. Der Test gibt den ersten Vorgang gezielt frei oder lässt ihn fehlschlagen und prüft den Endzustand nach der UI-Fortsetzung. Der bereits vorhandene F3-Test deckt den geforderten Retry ab. [Neue Testfälle](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L237) · [Bestehender Retry-Test](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L170)

| Fall | Beobachtete Rückmeldung und Clipboard-Inhalt |
|---|---|
| A · Älterer Erfolg nach neuerem NUL | Die Ablehnung bleibt sichtbar. Vor der Freigabe steht der Sentinel im Clipboard, danach der ursprünglich gewählte Identifier-Suffix. |
| B · Älterer Fehler nach neuerem Erfolg | Die neuere expandierte Kopie behält ihre Erfolgsmeldung und ihren Inhalt. Ein zweiter Fall prüft die normale Teilkopie ohne eigene Erfolgsmeldung. Auch dort bleibt die verspätete Fehlermeldung aus. |
| C · Zwei erfolgreiche Kopien in Folge | Identifier und Value liefern nacheinander jeweils die richtige Zeichenzahl und den vollständigen erwarteten Text. |
| D · Fehler und anschließender Retry | Der vorhandene Test prüft sichtbaren Fehler, Sentinel, Zeilenidentität, anschließende Erfolgsmeldung und exakten Inhalt des erneut kopierten Suffixes. |
| E · Abschluss nach Dispose oder Pane-Close | Beide Varianten lassen den ursprünglichen Clipboard-Schreibvorgang fertig werden. Der sichtbare Shell-Status bleibt auf seinem Stand nach der jeweiligen Aktion. |

**Gegenprobe vor der Produktänderung.** Acht Fälle liefen mit den neuen Tests gegen den unveränderten R4-Produktstand. Vier scheiterten genau an den veralteten Shell-Meldungen in A, beiden B-Varianten und nach dem Schließen des Panes. Die sequenziellen Fälle, die bestehende NUL- und Retry-Absicherung sowie das direkte Dispose zeigten das erwartete Verhalten. Die Original-TRX dieses absichtlichen Rotlaufs ist erhalten. [Vorher-TRX](../artifacts/test-results/g2-04-i3-f4/red/before.trx)

## Native Reproduktion und Gegenproben

Zwei frisch gestartete Release-Prozesse öffneten die produktive Dock-Shell. Die Produkt-DLL in Desktop, GUI-Testausgabe und Prüfhost war hashgleich. Der GUI-Parser erzeugte eine KDF-SP800-108-COUNTER-Variable mit 4.096 UTF-16-Einheiten im Identifier und einem zitierten `#LABEL` von 177 Einheiten mit NUL an Index 171. Nach verifiziertem Sentinel bestätigte ein anderer Windows-Prozess seine 550-ms-Clipboard-Sperre. Der Host löste die normale Copy-Aktion an der realisierten Identifier-Auswahl und danach an der ParametersGrid-Zelle aus. Vor der zweiten Aktion waren die Sperre noch aktiv und die Rückmeldung des ersten Schreibens ausstehend. [Prüfcode](../artifacts/test-results/g2-04-i3-f4/native/IndependentChecks.cs) · [Produkt-DLLs](../artifacts/test-results/g2-04-i3-f4/additional/native-product.json)

| Prozess | Beginn Identifier-Copy | NUL-Fehler sichtbar | Endkontrolle nach Freigabe | Shell-Meldungen / Exit |
|---|---:|---:|---|---|
| 1 | 3.038 ms | 3.175 ms | 4.703 ms, NUL-Fehler sichtbar, Clipboard exakt früherer Identifier | 1 / 0 |
| 2 | 2.144 ms | 2.268 ms | 3.792 ms, NUL-Fehler sichtbar, Clipboard exakt früherer Identifier | 1 / 0 |

Die Zeiten messen ab Prozessstart. Der letzte Shell-Text lautete beide Male `Copy failed. Clipboard text cannot preserve embedded NUL.` Die ältere Aktion ersetzte den Sentinel durch den vollständigen Identifier. Selektierte Variable, Quellreferenzen, beide Markierungen 0–128 und Inspector-Seite blieben erhalten. [Lauf 1](../artifacts/test-results/g2-04-i3-f4/additional/order1.log) · [Lauf 2](../artifacts/test-results/g2-04-i3-f4/additional/order2.log) · [Exitcodes](../artifacts/test-results/g2-04-i3-f4/additional/commands.log)

Eine getrennte 3,5-s-Sperre erzeugte den erwarteten COM-Fehler und erhielt den Sentinel. Nach Freigabe meldete der bewusste Retry Erfolg und schrieb alle 4.096 Einheiten. Ein weiterer Prozess kopierte nacheinander einen 4.096-Einheiten-Identifier und einen 4.406-Einheiten-Value-Suffix, jeweils mit korrekter Erfolgsmeldung. Die folgende normale Präfixkopie schrieb exakt acht Einheiten. [Sperre und Retry](../artifacts/test-results/g2-04-i3-f4/additional/contention.log) · [Sequenzielle Kopien](../artifacts/test-results/g2-04-i3-f4/additional/sequential.log)

## Bestätigung früherer Korrekturen

Die drei nativen Rendering-Stichproben bei 150 % verwendeten das Drei-Spalten-Layout mit sichtbaren Variables und Inspector. Alle Prozesse erreichten einen nutzbaren Frame, bedienten sämtliche eingereihten Input-Aufträge und endeten mit Exit 0.

| Pfad | Eingabe in UTF-16-Einheiten | Auswahl bis nutzbarer Frame | Max. Input-Verzug | Abgetastetes maximales Working-Set |
|---|---:|---:|---:|---:|
| Identifier | 65.536 | 87 ms | 62 ms | 197,9 MiB |
| KDF-Label | 1.048.576 | 107 ms | 67 ms | 222,4 MiB |
| Value | 1.048.576 | 92 ms | 35 ms | 219,8 MiB |

Der Inspector kopierte den gesamten großen Value exakt. Beim Migrationsfehler blieb v2 nach normalem Schließen zunächst aus. Der nächste Start installierte die vollständige erwartete v2-Projektion mit aktivem Output, eigenständigem Info-Fenster und geschlossenem Inspector. Die v1-Datei war nach beiden Starts bytegleich. Damit bleiben R1-H01/M01, R2-H01/M01 und mit den Clipboard-Gegenproben auch R3-H01/M01 **CLOSED**. [Messdaten](../artifacts/test-results/g2-04-i3-f4/smoke/analysis.json) · [Vollwertkopie](../artifacts/test-results/g2-04-i3-f4/smoke/unselected-1048576.log) · [Migration](../artifacts/test-results/g2-04-i3-f4/additional/recovery.log) · [Projektionsvergleich](../artifacts/test-results/g2-04-i3-f4/additional/projection.log)

## Debug- und Release-Ergebnisse

Desktop- und GUI-Testprojekt wurden für Debug und Release jeweils neu gebaut. Alle vier Builds und alle nachfolgenden Testläufe bestanden beim ersten Versuch. Im F4-Endstand trat kein intermittierender Headless-Startfehler auf. Der absichtliche Vorher-Lauf gegen R4 ist separat ausgewiesen.

| Konfiguration | Clipboard und neue Regressionen | Fokussierte GUI-Suite | Vollständige GUI-Suite |
|---|---:|---:|---:|
| Debug | 21/21 | 122/122 | 1.277/1.277 |
| Release | 21/21 | 122/122 | 1.277/1.277 |

Die TRX-Identitäten wurden nach Testklasse und parametrisiertem Fallnamen mit R4 abgeglichen. Alle 116 bisherigen fokussierten und alle 1.271 bisherigen GUI-Fälle sind enthalten. Jeweils sechs neue Fälle erklären die Differenz. Die Teilmengen sind bereits in den Gesamtsummen enthalten. [Befehle und Exitcodes](../artifacts/test-results/g2-04-i3-f4/final/commands.log) · [TRX und Buildprotokolle](../artifacts/test-results/g2-04-i3-f4/final/) · [Zählung](../artifacts/test-results/g2-04-i3-f4/final/test-counts.json) · [Identitätsvergleich](../artifacts/test-results/g2-04-i3-f4/final/inventory.json)

## Abnahmegrenzen und Übergabe

Offen bleiben die nativen Prüfungen bei 100 % und 125 % DPI, die manuell gehörte Screenreader-Sprachausgabe und die spezielle fensterübergreifende Maus-Redock-Sequenz. Die früher bei 150 % beobachtete Tabrückkehr innerhalb des Hauptfensters ist ein eigener Prüffall. Die breiten Core-/MCP-Läufe und der native 255-Share-Clipboard-Nachweis sind aus früheren Stufen übernommen. [Übernommene Prüfgrenzen](G2-04-02-I3-R4-targeted-final-review.md)

**READY FOR FINAL R5 VERIFICATION.** F4 erfüllt das R4-M01-Kriterium in beiden nativen Wiederholungen und deckt den umgekehrten Fehlerverlauf, sequenzielle Kopien, Retry und den Lebenszyklus mit dauerhaften Tests ab. Die Eigentümerabnahme von I3 steht aus.
