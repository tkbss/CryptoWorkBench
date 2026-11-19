# G2-04-02-I3-F1 · Bedienbarkeit großer Werte und Layout-Wiederherstellung

**18. November 2026 · feature/gui-g2-04 · READY FOR INDEPENDENT RE-REVIEW**

Beide Windows-Reproduktionen aus R1 bestehen am korrigierten Stand. Bei gleichzeitig sichtbaren Variables und Inspector ließen sich alle vier Messwerte bis 1.048.576 UTF-16-Codeeinheiten vollständig über die native Zwischenablage kopieren. Nach einem vorübergehenden Layoutfehler blieben die gültigen v2-Bytes bzw. das Fehlen von v2 bis zum regulären Prozessende erhalten. Der Folgeprozess stellte das Info-Fenster und den geschlossenen Inspector wieder her. [R1-Ausgangspunkt](G2-04-02-I3-R1-independent-integration-review.md)

## Prüfstand und Änderungen

Vor der ersten Änderung wurden Branch, HEAD, lokaler master, Git-Status und SHA-256 der 64 bereits geänderten oder unversionierten Einzeldateien geprüft. HEAD und master standen auf `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Gearbeitet wurde im bestehenden Worktree `artifacts/worktrees/gui-g2-04`. Vier vorhandene Dateien unterscheiden sich vom R1-Arbeitsstand. Hinzu kommt dieser Bericht. Kopien der vier Ausgangsdateien, native Prüfhosts und Rohprotokolle liegen im lokalen F1-Prüfverzeichnis. [Eingangsinventar](../artifacts/test-results/g2-04-i3-f1/before.json) · [Vorher-/Nachher-Prüfsummen](../artifacts/test-results/g2-04-i3-f1/changed.json) · [Reines F1-Diff](../artifacts/test-results/g2-04-i3-f1/f1-only.diff)

| Geänderte Datei | F1-Änderung |
|---|---|
| [DockVariablesView.cs](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L94) | Begrenzt die an Value-Zelle und ausgewählten Detailwert gebundenen Anzeigestrings. |
| [DockLayoutStore.cs](../CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs#L67) | Kennzeichnet einen potenziell wiederherstellbaren Lade- oder Erstinstallationsfehler für den späteren Shutdown. |
| [DockShellWindow.axaml.cs](../CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs#L93) | Vergleicht beim Shutdown in diesem Fehlerfall die normale Dock-Anordnung mit der beim Start gezeigten Ersatzanordnung. |
| [DockInspectorTests.cs](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L24) | Ein permanenter Test des tatsächlichen Renderpfads und drei Fälle mit produktivem Fenster-Shutdown, anschließender Datei- und Ladeprüfung. |

## R1-H01 · Ursache und Korrektur

Die Value-Zelle band den ganzen String an einen `SelectableTextBlock` mit Ellipse. Die Detailansicht band ihn ebenfalls vollständig, dort mit Zeilenumbruch in einer schmalen Spalte. Zur Eingrenzung lief die archivierte R1-Produkt-DLL in einem echten Drei-Spalten-Split. Der Prüfhost ersetzte wahlweise nur die Textbindung der Value-Zelle, nur die des Detailwerts oder beide durch sechs feste Zeichen. Die Original-Controls, Auswahl und Inspector-Daten blieben im Prüfpfad. Die Protokolle weisen die tatsächlich gebundenen Textlängen aus. [Isolationsprobe](../artifacts/test-results/g2-04-i3-f1/ablation/Program.cs) · [Rohdaten](../artifacts/test-results/g2-04-i3-f1/r1-ablation/series.json)

| R1-Produkt, sichtbare Variables | 4.096 Einheiten, Eingabewartezeit | 65.536 Einheiten, Eingabewartezeit |
|---|---:|---:|
| Beide Textbindungen unverändert | 634 ms | Abbruch nach acht Sekunden |
| Nur Tabellenzelle kurz | 371 ms | Abbruch nach acht Sekunden |
| Nur Detailwert kurz | 4 ms | 156 ms |
| Beide kurz | 1 ms | 1 ms |

Der vollständige umbrochene Detailtext reicht unter diesen Bedingungen für die mehrsekündige Blockade aus. Auch bei kurzem Detailwert trägt die ungekürzte Tabellenzelle messbar zur Verzögerung bei. Die Messung erlaubt keine genauere Aufteilung auf Textlayout, Zeichnen und GC. Zwei vorbereitende Versuche haben die Detailbindung nicht zuverlässig ersetzt. Für die Zuordnung gilt die Serie `r1-ablation` mit expliziter Ersatzbindung und protokollierten Zeichenlängen. [Nur Detail kurz](../artifacts/test-results/g2-04-i3-f1/r1-ablation/detail-65536.log) · [Beide kurz](../artifacts/test-results/g2-04-i3-f1/r1-ablation/both-65536.log)

**Die Korrektur greift vor der Textmessung.** Bis einschließlich 128 UTF-16-Codeeinheiten erhält das Text-Control den Originalstring. Darüber zeigt es ein Präfix mit abschließender Ellipse, insgesamt höchstens 128 Einheiten. An der Schnittstelle bleibt ein gültiges Surrogatpaar zusammen. Die OneWay-Bindungen lassen `VariableModel.Value` und den Originalwert im Inspector vollständig. Der permanente Test prüft die echte Tabellenzelle einer noch nicht ausgewählten langen Zeile, den ausgewählten Detailwert, kurze Werte mit Leerzeichen, Zeilenwechsel, Abwahl und die unveränderte Originalreferenz. [Permanenter Rendertest](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L24)

### F1-Messreihe am nativen Windows-Desktop

Je Bedingung wurde ein frischer Release-Prozess der produktiven Prism-Shell geöffnet. Der Factory-Split ergab rund 169 DIP für die Tabelle und 177 DIP für den Inspector. Die verdeckte Kontrolle verwendete die gemeinsame Tabgruppe mit aktivem Inspector. Die Eingaben enthielten Leerzeichen, CRLF und Unicode. Wie in R1 wurde ein Auftrag nach 150 ms aus dem Hintergrund mit Eingabepriorität eingereiht. Der Dispatcher-Timer hatte ein Sollintervall von 50 ms. Der Prozessmonitor tastete den Working Set etwa alle 90 ms ab. Dieselben Abbruchgrenzen galten bei acht Sekunden ab START, 26 Sekunden insgesamt oder 1.100 MiB. Alle acht Prozesse endeten regulär. [Prüfhost](../artifacts/test-results/g2-04-i3-f1/native/Program.cs) · [Messskript](../artifacts/test-results/g2-04-i3-f1/measure.ps1) · [Einzelergebnisse in Bytes und Millisekunden](../artifacts/test-results/g2-04-i3-f1/corrected/series.json)

| UTF-16-Einheiten | Variables | Eingabewartezeit | Längstes Timerintervall | Beobachteter Peak Working Set | Inspector-Seite / native Copy |
|---:|---|---:|---:|---:|---|
| 4.096 | sichtbar | 13 ms | 101 ms | 191,5 MiB | fertig / exakt |
| 4.096 | verdeckt | 6 ms | 63 ms | 188,5 MiB | fertig / exakt |
| 65.536 | sichtbar | 4 ms | 94 ms | 198,7 MiB | fertig / exakt |
| 65.536 | verdeckt | 2 ms | 77 ms | 193,0 MiB | fertig / exakt |
| 262.144 | sichtbar | 6 ms | 98 ms | 201,3 MiB | fertig / exakt |
| 262.144 | verdeckt | 1 ms | 68 ms | 194,9 MiB | fertig / exakt |
| 1.048.576 | sichtbar | 5 ms | 94 ms | 207,3 MiB | fertig / exakt |
| 1.048.576 | verdeckt | 7 ms | 64 ms | 203,1 MiB | fertig / exakt |

Der Prüfhost setzte zuerst einen Sentinel in die Windows-Zwischenablage, löste dann die vorhandene Aktion „Copy Value“ aus und verglich die zurückgelesene Gesamtzeichenfolge mit dem Original. In der zusätzlichen Probe stand der Wert mit 1.048.576 Einheiten zuerst bei explizit leerer Auswahl in der sichtbaren Tabelle. Die Zelle enthielt bereits die 128 Einheiten lange Vorschau. Nach Auswahl wurden Inspector-Seite und vollständige native Copy geprüft. Dort betrugen Eingabewartezeit, maximales Timerintervall und Prozessspitze 1 ms, 83 ms und 204,5 MiB. Der bestehende Großstring-Vergleich mit 17.825.798 Einheiten bestand zusätzlich in Debug und Release, jeweils im aktiven Inspector. [Unselektierte große Zeile](../artifacts/test-results/g2-04-i3-f1/corrected/unselected-1048576.log) · [Großstring Debug](../artifacts/test-results/g2-04-i3-f1/full-Debug.log) · [Großstring Release](../artifacts/test-results/g2-04-i3-f1/full-Release.log)

**Bewertung der Restlatenz.** Im ursprünglichen R1-Lauf war bei 65.536 Einheiten ein Eingabeauftrag beim Abbruch noch unbeantwortet. In F1 lagen alle vier sichtbaren Eingabewartezeiten bei 4–13 ms. Die Timerintervalle von 94–101 ms zeigen weiterhin eine mögliche kurze Stockung beim Aufbau der sichtbaren Ansicht, bis zu 51 ms über dem Sollintervall. Der Working Set stieg mit der Payloadgröße. Die Messungen umfassen den Gesamtprozess, und der einzelne Eingabeauftrag kann bei schneller Aufnahme erst nach Fertigstellung der Seite eintreffen. Daraus folgt weder ein p95 über Nutzerereignisse noch eine Obergrenze für einzelne Render- oder Clipboard-Schritte. Die konkrete Sekundenblockade aus R1 wurde in keiner korrigierten Größe beobachtet. [R1-Vergleichslauf](../artifacts/test-results/g2-04-i3-r1/confirm/perf-65536.log) · [F1-Vergleichslauf](../artifacts/test-results/g2-04-i3-f1/corrected/perf-65536.log)

## R1-M01 · Ladeergebnis und Shutdown-Save

R1 hatte nach einem Ladefehler korrekt das Standardlayout geöffnet. Beim regulären `Closed` ersetzte dessen Snapshot anschließend ein zuvor gültiges v2 oder installierte bei fehlgeschlagener Erstmigration ein neues v2. F1 unterscheidet Zugriffs-/IO-Fehler und fehlgeschlagene Erstinstallation von einem normalen Erststart oder nachweislich ungültigen Inhalt. Der Fehlerstatus für die Persistenz ist unabhängig vom Text der Diagnose. Bei einer konkurrierenden Installation bleibt das gültige v2 des Gewinners maßgeblich. [Store](../CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs#L67) · [Fenster](../CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs#L93)

**Regel bei bewusster Änderung.** G1 sichert beim kontrollierten Beenden den Normalbaum einschließlich geschlossener Tools, Tabwahl und Floating Hosts. Nach temporärem Startfehler erfasst die Shell die angezeigte Ersatzanordnung mit konstanten Hauptfensterkoordinaten. Vor dem Drain vergleicht sie dieselbe Projektion erneut. Eine beim Beenden fortbestehende Änderung der Dock-Anordnung, etwa ein ausdrücklich ausgeführtes Float, wird regulär und atomar einschließlich aktueller Geometrie gespeichert. Entspricht die normale Dock-Anordnung wieder dem Ausgangszustand, unterbleibt die Speicherung. Das gilt auch nach Float, Home und anschließendem Reset. Alleinige Hauptfensterbewegung, Größenänderung oder Maximierung begründet in dieser Fehlersitzung kein Ersetzen der schützenswerten Datei. Diese Regel benötigt keine Zuordnung von Plattform-Größenereignissen zu Benutzereingaben. Die Prüfung erfolgt vor dem Abbau der Hosts. Scheitert bereits die Erfassung der Vergleichsbasis, bleibt die Datei geschützt. [G1-Persistenzvertrag](gui/G1-07-layout-persistence.md) · [Regel im Shutdown-Pfad](../CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs#L104)

### Native Prozessfolgen

Jeder Start in der Tabelle ist ein eigener Windows-App-Prozess mit isolierten Dateipfaden. Der Prüfhost gibt beim Start Inspector-Mitgliedschaft, Hostanzahl, native Fenstertitel und Sichtbarkeit aus. Das Skript sichert vorhandene v2-Dateien vor dem Start und nach Prozessende. Alle **35 Prüfungen** der Prozessreihe bestanden. [Prozessskript](../artifacts/test-results/g2-04-i3-f1/recovery.ps1) · [Prüfprotokoll](../artifacts/test-results/g2-04-i3-f1/process-check/checks.log)

| Fall | Erster Prozess und Datei beim Beenden | Zweiter Prozess |
|---|---|---|
| Exklusive v2-Lesesperre, gültige v1 daneben | Diagnose, Inspector im Default, kein Floating Host. Sperre gelöst, Hauptfenstergröße geändert, normal beendet. v2 vorher und nachher bytegleich. | Eigenständiges sichtbares Info-Fenster bei (160, 180), 500 × 420, Inspector geschlossen. |
| Einmaliger Fehler vor erstem atomarem Move | Diagnose, Default, keine Floating Hosts. v1 bytegleich, v2 auch nach regulärem Ende weiterhin abwesend. | Automatische Migration. Sichtbares Info-Fenster und geschlossener Inspector. Gesamte installierte v2-Projektion geprüft. |
| Gesperrtes v2, danach bewusstes Float | Nach Entsperren Inspector aus Default heraus gefloatet. Geänderte Anordnung gespeichert. | Eigenständiges sichtbares Inspector-Fenster. |
| Gesperrtes v2, Änderung rückgängig gemacht | Float, Home und Reset. Beim Beenden ursprüngliche v2-Bytes erhalten. | Wieder Info-Fenster und geschlossener Inspector. |
| Dauerhaft ungültiger v2-Inhalt neben gültiger v1 | JSON-Diagnose und Default. Reguläres Ende schreibt gültiges Standard-v2. | Standard ohne Floating Hosts und ohne Diagnose. |
| Normaler Erststart ohne Dateien | Default, reguläres Ende installiert v2. | Gültiges v2 geladen, bewusstes Float gespeichert. Ein dritter Prozess stellt das Inspector-Fenster wieder her. |

SHA-256 der gesperrten v2-Datei war vor und nach dem ersten Prozess `8436C449C74A70A1721E2EDA1C451BC5AE765C00CD6336A92F63CC6950969178`. Die historische v1-Datei behielt über beide Prozessgrenzen `7E54E250BA7F791968E4522C53BBB15771E1B9251A134CEA2504A086BE29F34F`. Ein vom Produktdecoder unabhängiges Programm vergleicht den vollständigen migrierten JSON-Baum einschließlich aktiver Tabs, Gruppen, Anteile, Geometrie und Referenzen. Es berücksichtigt die neue reguläre ID der historisch namenlosen Floating-Root. Der deterministische Konkurrent unmittelbar vor dem Move blieb bytegleich und konnte erneut geladen werden. [v2 vor Sperrstart](../artifacts/test-results/g2-04-i3-f1/process-check/locked-1.before) · [v2 nach Ende](../artifacts/test-results/g2-04-i3-f1/process-check/locked-1.after) · [Wiederhergestellter Host](../artifacts/test-results/g2-04-i3-f1/process-check/locked-2.log) · [Fehlgeschlagener Erststart](../artifacts/test-results/g2-04-i3-f1/process-check/migration-1.log) · [Erfolgreicher Folgestart](../artifacts/test-results/g2-04-i3-f1/process-check/migration-2.log) · [Vollständiger Projektionsvergleich](../artifacts/test-results/g2-04-i3-f1/process-check/projection.log) · [Konkurrenzprobe](../artifacts/test-results/g2-04-i3-f1/process-check/competing-1.log)

## Regressionen und Nachprüfung

Die neuen nativen Lifecycle-Proben bestätigten in beiden Builds dieselbe ausgewählte zweite Zeile, dieselbe Seite und Listenerzahl nach Redock und Reset eines aktiven Floating-Inspectors. Normale Auswahländerung und Abwahl funktionierten. Tatsächliches Close gab Snapshot und Listener frei. Show nach Close vor Dispatcher-Publikation verwendete die neue gültige Auswahl. Beim Schließen während einer gehaltenen Copy-Übergabe wurde Cancellation angefordert und nach Freigabe kein spätes Ergebnis veröffentlicht. [Debug](../artifacts/test-results/g2-04-i3-f1/life-Debug.log) · [Release](../artifacts/test-results/g2-04-i3-f1/life-Release.log) · [Gehaltene Übergabe](../artifacts/test-results/g2-04-i3-f1/cancel-Release.log)

Im korrigierten Release-Floating-Fenster öffnete Enter auf „Inspect Value“ den String, Rechts das skalare Length-Blatt mit Wert 12. Links kehrte zur Value-Seite zurück, ein weiteres Links zur Wurzel mit Fokus „Inspect Text“. Das nächste Rechts öffnete tatsächlich Text. Der separat ausgeführte Funktionsprüfhost prüfte System, Light und Dark, explizite KCV-Varianten, vier F3-Roots, Paging, Detailansicht und Dokument-/Revisionsidentität. [Neue native Tastaturfolge](../artifacts/test-results/g2-04-i3-f1/keyboard.log) · [Neue Funktionsprobe](../artifacts/test-results/g2-04-i3-f1/full-Release.log)

Desktop- und GUI-Testprojekte bauten in Debug und Release jeweils mit null Fehlern. Die Desktop-Builds meldeten je sechs Warnungen, die GUI-Testbuilds je zwei. Die 111 fokussierten Fälle sind Teil der 1.266 GUI-Fälle. [Desktop Debug](../artifacts/test-results/g2-04-i3-f1/build-CryptoWorkBenchAvalonia.Desktop-Debug.log) · [Desktop Release](../artifacts/test-results/g2-04-i3-f1/build-CryptoWorkBenchAvalonia.Desktop-Release.log) · [GUI-Build Debug](../artifacts/test-results/g2-04-i3-f1/build-CryptoWorkBenchGuiTest-Debug.log) · [GUI-Build Release](../artifacts/test-results/g2-04-i3-f1/build-CryptoWorkBenchGuiTest-Release.log)

| Neuer Lauf | Passed | Failed | Skipped | Weitere NotExecuted |
|---|---:|---:|---:|---:|
| I3/Layout Debug | 111 | 0 | 0 | 0 |
| I3/Layout Release | 111 | 0 | 0 | 0 |
| GUI Debug, erster Gesamtlauf | 1.265 | 1 | 0 | 0 |
| Debug, betroffene parametrisierte Testmethode erneut | 3 | 0 | 0 | 0 |
| GUI Debug, vollständige Wiederholung | 1.266 | 0 | 0 | 0 |
| GUI Release | 1.266 | 0 | 0 | 0 |

Der erste Debug-Gesamtlauf traf bei **ShortPrintIsVisibleAndSourceButtonNavigates(911,512)** auf den bekannten intermittierenden Avalonia-Headless-Initialisierungsfehler. Der Originalstack zeigt Dispatcher.VerifyAccess → DefaultRenderLoop.Add → AvaloniaHeadlessPlatform.Initialize. Alle drei Varianten der Methode und anschließend die gesamte Debug-Suite bestanden auf demselben Build. Die Ursache des intermittierenden Fehlers bleibt offen. [Erster Lauf und Stack](../artifacts/test-results/g2-04-i3-f1/gui-Debug.log) · [Einzelwiederholung](../artifacts/test-results/g2-04-i3-f1/shortprint-Debug.trx) · [Vollständige Debug-Wiederholung](../artifacts/test-results/g2-04-i3-f1/gui-repeat-Debug.trx) · [GUI Release](../artifacts/test-results/g2-04-i3-f1/gui-Release.trx) · [Alle TRX-Zählungen und Hashes](../artifacts/test-results/g2-04-i3-f1/matrix.json)

**Übernommene Nachweise und offene Prüfungen.** Die vollständigen Core- und MCP-Suiten, das native 255-Share-Orakel sowie die dokumentierte Maus-Rückkehr in die gemeinsame Gruppe bei 150 % stammen aus I3/R1. Die F1-Mess-, Prozess-, Lifecycle-, Tastatur-, Großstring- und GUI-Ergebnisse oben sind neue Läufe. Native Prüfungen bei 100 % und 125 % DPI, manuelle Screenreader-Sprachausgabe und die spezielle fensterübergreifende Maus-Redock-Sequenz bleiben **OPEN**. [R1-Nachweise und Abgrenzung](G2-04-02-I3-R1-independent-integration-review.md)

Für die unabhängige Nachprüfung stehen die Einzelmessungen, Dateizustände an den Prozessgrenzen, Prüfsummen und TRX unter dem [lokalen F1-Prüfverzeichnis](../artifacts/test-results/g2-04-i3-f1/) bereit. **READY FOR INDEPENDENT RE-REVIEW.**
