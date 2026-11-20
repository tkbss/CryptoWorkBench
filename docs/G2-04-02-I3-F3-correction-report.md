# G2-04-02-I3-F3 · Korrektur des Auswahl-Copy-Pfads in Variables

19. November 2026 · Lokale Windows-Implementierung und Validierung im Worktree `artifacts/worktrees/gui-g2-04`

**READY FOR TARGETED INDEPENDENT R4 REVIEW.** Beide R3-Fehlerfälle bestanden auf dem korrigierten Stand in jeweils zwei frischen nativen Prozessen. Die I3-Eigentümerabnahme steht aus.

| Befund | R3-Original | F3 mit gleicher Eingabe |
|---|---|---|
| R3-H01 · HIGH | Die erweiterte Auswahlkopie beendete bei belegter Zwischenablage beide Prüfprozesse mit einer unbehandelten COMException. | Der sichtbare Shell-Status meldete den Schreibfehler. Sitzung und Sentinel blieben erhalten. Nach Freigabe lieferte ein erneutes Copy exakt 4.096 Einheiten. |
| R3-M01 · MEDIUM | Ein vollständig markiertes KDF-Label mit eingebettetem NUL ersetzte den Sentinel durch ein Präfix von 171 der 177 Einheiten. | Variables meldete den Windows-Formatfehler vor dem Schreiben. Beide Clipboard-Readbacks lieferten den unveränderten Sentinel. Darstellbare Teilmarkierungen blieben kopierbar. |

## Prüfstand und Änderungen

Branch `feature/gui-g2-04`, HEAD und lokaler Master `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Der Index enthielt zu Beginn keine vorgemerkten Änderungen. Erfasst wurden 1.259 versionierte bzw. unversionierte, von Git nicht ignorierte Dateien, davon 69 bereits geändert oder unversioniert. Alle 1.258 Dateien des R3-Eingangsinventars und alle 68 Einträge des F2-Abschlussinventars hatten die erwarteten SHA-256. Hinzu kam der R3-Bericht. Die R3-Absturzstacks, NUL-Readbacks, Kontrollversuche und ursprünglichen TRX waren zugänglich. [Eingangsinventar](../artifacts/test-results/g2-04-i3-f3/before.json) · [Git-Status](../artifacts/test-results/g2-04-i3-f3/status-before.txt) · [Originalnachweise und Hashes](../artifacts/test-results/g2-04-i3-f3/original-evidence.json) · [R3-Bericht](G2-04-02-I3-R3-independent-re-review.md)

| Geänderte Datei | Änderung gegenüber dem uncommittierten R3-Stand |
|---|---|
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | Fängt Fehler der erweiterten Auswahlkopie an der asynchronen Ereignisgrenze ab. Prüft den gewählten Text vor der Windows-Übergabe. |
| `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs` | Übergibt der Variables-Ansicht beim Start den vorhandenen Feedback-Kanal des Shell-Status. |
| `CryptoWorkBenchAvalonia/Services/Inspector/InspectorClipboard.cs` | Stellt die bestehende NUL-Prüfung auch Variables zur Verfügung. Der Inspector führt seine Prüfung weiterhin vor dem Schreiben abbrechbar im Hintergrund aus und verifiziert die Plattformübergabe. |
| `CryptoWorkBenchGuiTest/DockInspectorTests.cs` | Zwei permanente Regressionstests mit echten Controls, Strg+C, verzögertem Schreibfehler und Windows-NUL-Fällen. |
| `docs/G2-04-02-I3-F3-correction-report.md` | Dieser Bericht. |

Der Quell-Diff basiert auf bytegenau gesicherten Eingangsdateien. Das Vorher-/Nachher-Inventar unterscheidet die F3-Änderungen vom vorhandenen Arbeitsstand. Die lokalen Prüfbelege liegen unter `artifacts/test-results/g2-04-i3-f3/`. [Fokussierter Diff](../artifacts/test-results/g2-04-i3-f3/f3-only.diff) · [Exakte Pfade und SHA-256](../artifacts/test-results/g2-04-i3-f3/changed-files.json) · [Git-Endstand](../artifacts/test-results/g2-04-i3-f3/state-after.json)

## Verhalten am Copy-Ereignis

Bei einer Auswahl einschließlich der abschließenden Vorschauellipse stammt der zu kopierende Ausschnitt aus der Originalzeichenfolge des gebundenen Feldes. Vorwärts- und Rückwärtsauswahl verwenden dieselben geordneten Grenzen. Der Handler markiert die erweiterte Kopie vor dem asynchronen Schreiben als behandelt. Bei einer Exception meldet er `Copy failed.` mit dem Fehlergrund im vorhandenen Shell-Status. Erst ein abgeschlossener erfolgreicher Schreibvorgang erzeugt die Rückmeldung mit der kopierten Zeichenanzahl. [Copy-Handler](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs) · [Anschluss an Shell-Feedback](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs)

Die Windows-Prüfung bezieht sich auf den **tatsächlich markierten Ausschnitt**. Enthält er U+0000, wird der Copy-Versuch vor dem ersten Schreibzugriff behandelt und abgelehnt. Das gilt auch für ein NUL innerhalb eines sichtbaren Teilbereichs. Ein gültiger Präfix vor dem NUL und ein gültiger Originalsuffix hinter dem NUL sind weiterhin übertragbar. Für gültigen gewöhnlichen Text bleibt Avalonia für das Kopieren der sichtbaren Auswahl zuständig. [Gemeinsame Validierung](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorClipboard.cs)

## Native Windows-Nachweise

Die gesondert gebauten Release-Prüfhosts verwenden die Produktionsassembly und die echte Dock-Shell. Die F3-Assertions lesen den sichtbaren Shell-Status, den ausgewählten Modellzustand sowie die Zwischenablage. Vor jeder unabhängigen Copy-Gegenprobe wird ein Sentinel installiert und geprüft. Der direkte Win32-Readback verwendet `CF_UNICODETEXT`. [Probequelltext](../artifacts/test-results/g2-04-i3-f3/native/IndependentChecks.cs) · [Prozessstarts und Exits](../artifacts/test-results/g2-04-i3-f3/additional/commands.log) · [Binärhashes](../artifacts/test-results/g2-04-i3-f3/final/binaries.json)

- **Belegte Zwischenablage.** Ein separater Prozess bestätigte die erfolgreiche Öffnung und hielt die Windows-Zwischenablage 3,5 Sekunden. Das vollständige Markieren des gekürzten Identifier-Details und seine Produktionsaktion lösten `0x800401D0 (CLIPBRD_E_CANT_OPEN)` aus. Der Prüfer las `Copy failed. OpenClipboard Failed …` im sichtbaren Shell-Status. Der Sentinel blieb exakt erhalten. Ausgewählte Zeile, Inspector-Seite, Editordokument und Revision waren identisch. Nach Freigabe und neuem Sentinel lieferte derselbe Vorgang alle 4.096 Einheiten sowie die entsprechende Erfolgsmeldung. Beide Prozesse endeten regulär mit Exit 0. Die Kontrollversuche für kurze Standardauswahl und Inspector-Copy erlaubten ebenfalls den Retry. [Erster Lauf](../artifacts/test-results/g2-04-i3-f3/additional/contention.log) · [Wiederholung](../artifacts/test-results/g2-04-i3-f3/additional/contention-repeat.log) · [Kurze Auswahl](../artifacts/test-results/g2-04-i3-f3/additional/shortbusy.log) · [Inspector](../artifacts/test-results/g2-04-i3-f3/additional/inspectorbusy.log)
- **KDF-Label mit NUL.** Der GUI-Parser publizierte `KDF-SP800-108-COUNTER` mit `#PRF:HMAC-SHA256`, `#OUTLEN:128` und einem zitierten Label aus 170 Zeichen `q`, U+0000 und `tail`. Das Modell enthielt 177 UTF-16-Einheiten einschließlich der Anführungszeichen. In der auf 128 Einheiten gekürzten ParametersGrid-Zelle ließ sich ein Präfix von sieben Einheiten exakt kopieren. Die Vollauswahl erzeugte `Copy failed. Clipboard text cannot preserve embedded NUL.` Win32 und Avalonia lasen danach beide den vollständigen Sentinel. Originalreferenz und Inhalt, Auswahl und Inspector-Seite blieben erhalten. Der nächste gültige Teil-Copy gelang. „Copy Value“ im Inspector lehnte denselben Wert ebenfalls unter Erhalt seines Sentinels ab. Beide Prozesse endeten mit Exit 0. [Erster Lauf](../artifacts/test-results/g2-04-i3-f3/additional/nul.log) · [Wiederholung](../artifacts/test-results/g2-04-i3-f3/additional/nul-repeat.log)
- **Gültige Originale und Selektion.** Zwei 65.536 Einheiten lange IDs mit identischer Anzeige blieben auch nach Dokumentänderung dem richtigen Snapshot zugeordnet. Die Vollkopie eines normalen zitierten KDF-Labels ergab 1.048.578 Einheiten. Sechs unmittelbar aufeinanderfolgende Zeilenwechsel mit Copy ohne Dispatcher-Yield trafen jedes Mal die richtige Quelle. Im nativen Fenster ergaben die rückwärts gezogene Auswahl 128 → 13 und Strg+C exakt 4.083 Originaleinheiten. Strg+A, Strg+C lieferten alle 4.096. [Identität und historische Kopie](../artifacts/test-results/g2-04-i3-f3/additional/identity.log) · [Parameter](../artifacts/test-results/g2-04-i3-f3/additional/paramcopy.log) · [Sofortiger Wechsel](../artifacts/test-results/g2-04-i3-f3/additional/immediate.log) · [Native Maus und Tastatur](../artifacts/test-results/g2-04-i3-f3/additional/keyboard.log)

## Permanente Tests und Regressionen

`ExpandedSelectionReportsAsyncWriteFailureAndCanRetry` verwendet den vorhandenen Clipboard-Proxy. Der Test lässt den asynchronen Schreibauftrag einer rückwärts markierten Auswahl gezielt mit einer COMException fehlschlagen. Er prüft sichtbares Feedback, Sentinel, Zeilenidentität und das exakte Ergebnis nach einem erneuten Strg+C. `NulRejectionPreservesClipboardAndAllowsRepresentableSelections` prüft unter Windows sowohl eine ganze Vorschau als auch einen sichtbaren Ausschnitt mit NUL. Anschließend kopiert der Test den gültigen Präfix und einen rückwärts markierten NUL-freien Originalsuffix. Die F2-Tests zu normalen Voll- und Teilmarkierungen laufen weiterhin mit. [Permanente Tests](../CryptoWorkBenchGuiTest/DockInspectorTests.cs)

Desktop und GUI-Testprojekt wurden jeweils in Debug und Release neu gebaut. Alle vier Builds und beide Release-Prüfhost-Builds endeten mit Exit 0. Die vier GUI-Läufe bestanden jeweils im ersten Versuch. Die 116 fokussierten Fälle sind per Testidentität in den 1.271 Fällen der Gesamtsuite enthalten. Beide neuen Tests liefen in allen vier Testläufen. [Befehle und Exits](../artifacts/test-results/g2-04-i3-f3/final/commands.log) · [Prüfhost-Builds](../artifacts/test-results/g2-04-i3-f3/additional/build-commands.log) · [TRX-Auswertung und Hashes](../artifacts/test-results/g2-04-i3-f3/final/matrix.json)

| Konfiguration | Fokussiert | Vollständige GUI-Suite | Fehler / übersprungen |
|---|---:|---:|---:|
| Debug | [116/116](../artifacts/test-results/g2-04-i3-f3/final/focused-Debug.trx) | [1.271/1.271](../artifacts/test-results/g2-04-i3-f3/final/gui-Debug.trx) | 0 / 0 |
| Release | [116/116](../artifacts/test-results/g2-04-i3-f3/final/focused-Release.trx) | [1.271/1.271](../artifacts/test-results/g2-04-i3-f3/final/gui-Release.trx) | 0 / 0 |

Der historische intermittierende Headless-Initialisierungsfehler aus R3 ist im ursprünglichen Debug-TRX und in der damaligen Wiederholung dokumentiert. Beide Originale wurden beim F3-Schlussvergleich anhand ihrer Hashes geprüft. [R3-Erstlauf](../artifacts/test-results/g2-04-i3-r3/final/gui-Debug.trx) · [R3-Wiederholung](../artifacts/test-results/g2-04-i3-r3/repeats/gui-Debug.trx)

Drei frische native Rendering-Smokes nutzten den sichtbaren Drei-Spalten-Dock, vor Veröffentlichung eingereihte und während des Aufbaus wiederholte Input-Aufträge sowie einen nativen Frame als Bereitschaftsmarke. Alle Prozesse erreichten die Seite und endeten mit Exit 0. Beim Value-Smoke enthielt die abschließende Clipboard-Kopie alle 1.048.576 Einheiten. [Rohzeitpaare](../artifacts/test-results/g2-04-i3-f3/smoke/analysis.json) · [Monitor](../artifacts/test-results/g2-04-i3-f3/measure.ps1)

| Produktionsfall | Bereit ab Auswahl | Höchste gemessene Input-Wartezeit | Bedient / eingereiht |
|---|---:|---:|---:|
| Identifier mit 65.536 Einheiten | 101 ms | 71 ms | 21/21 |
| KDF-Label mit 1.048.576 Zeichen, Wertzelle in den Viewport gescrollt | 116 ms | 66 ms | 23/23 |
| Value mit 1.048.576 Einheiten, zunächst unselektiert | 76 ms | 33 ms | 39/39 |

Die native Vollwert-Gegenprobe las **17.825.798 UTF-16-Einheiten** exakt zurück. 35 Layout- und Dateiprüfungen einschließlich gesperrtem gültigem v2, fehlgeschlagener Erstinstallation mit erfolgreicher Migration beim Folgestart und konkurrierendem Gewinner bestanden. Die vollständige v2-Projektion und die v1-Bytes stimmten mit dem historischen Original überein. Diese Gegenproben stützen die bestehenden R1-/R2-Schließungen. [Vollwert und Themes](../artifacts/test-results/g2-04-i3-f3/additional/fullvalue.log) · [Layoutchecks](../artifacts/test-results/g2-04-i3-f3/recovery/checks.log) · [Projektionsvergleich](../artifacts/test-results/g2-04-i3-f3/additional/projection.log)

## Empfehlung und Grenzen

Ich empfehle dem Owner ein gezieltes unabhängiges R4-Review des Auswahl-Copy-Pfads mit Clipboard-Belegung, Retry und NUL-Teilbereichen. Weiter offen bleiben die zuvor abgegrenzten nativen Prüfungen bei 100 % und 125 % DPI, manuell gehörte Screenreader-Sprachausgabe und der spezielle fensterübergreifende Maus-Redock. Die Entscheidung über die Abnahme von I3 liegt beim Owner.

**READY FOR TARGETED INDEPENDENT R4 REVIEW**
