# G2-04-02-I3-R1 · Unabhängiges Integrationsreview

**Empfehlung am 18. November 2026 · CORRECTION REQUIRED.** Die produktive Shell erhält die Inspector-Sitzung beim Docken und beendet sie beim tatsächlichen Schließen. Zwei selbst reproduzierte Befunde stehen der Empfehlung zur Abnahme entgegen. Im Drei-Spalten-Layout blockiert die Anzeige eines ausgewählten Strings die Bedienung bereits bei 65.536 UTF-16-Codeeinheiten über die achtsekündige Prüffrist. Nach einem vorübergehenden Layout-Lesefehler kann normales Beenden eine gültige v2-Anordnung durch das Standardlayout ersetzen. Die Entscheidung über die Eigentümerabnahme liegt beim Owner.

## Prüfstand und Herkunft

Der lokale Windows-Worktree `artifacts/worktrees/gui-g2-04` befand sich auf `feature/gui-g2-04`. HEAD und lokaler master waren `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Der Index enthielt beim Einstieg keine gestagten Änderungen. Der uncommittierte Stand umfasste 63 geänderte bzw. unversionierte Einzeldateien außerhalb der Ignore-Regeln. Die Eingangsprüfsummen erfassen auch die akzeptierten Vorarbeiten. Die Gegenproben verwenden die Release-Produkt-DLL der GUI-Tests, SHA-256 `57EA6D3AA898CA0297B43B5915BEFF11EFB64204E7D40ED554ACEE3286855A58`, die echte Prism-App, eigene Layout- und Appearance-Pfade und einen zusätzlichen Prüfhost im lokalen Artefaktverzeichnis. [Eingangsinventar](../artifacts/test-results/g2-04-i3-r1/worktree-before.json) · [Prüfhost](../artifacts/test-results/g2-04-i3-r1/native/Program.cs)

Die eingebetteten Quelldokument-Prüfsummen der archivierten I2-R3-Debug-PDBs von CryptoScript und Avalonia stimmen in allen 329 Einträgen mit dem I3-Eingangsmanifest überein. Gegen den jetzigen Stand sind 319 identisch. Die zehn Abweichungen betreffen `App.axaml.cs`, `DockVariableInspectorView.cs` und die acht Shell-Dateien `DockLayoutStore`, `DockShellLayout`, `DockShellLayout.Persistence`, `DockShellPresentation`, `DockShellState`, `DockShellWindow.axaml.cs`, `DockVariablesView` und `EditorDockFactory`. Damit ist die Abgrenzung der kompilierten Produktquellen unabhängig nachvollzogen. Der aktuelle Renderpfad für Variable-Werte war bereits in HEAD vorhanden. Die I3-Änderung in der akzeptierten Inspector-View betrifft die Fokuswiederherstellung. [PDB-Vergleich je Datei](../artifacts/test-results/g2-04-i3-r1/pdb.json) · [Verifikation](../artifacts/test-results/g2-04-i3-r1/verification.log)

Die im I3-Bericht genannten Ergänzungen an den Dock-Tests und nativen Runnern sind im Diff bzw. als neue Dateien sichtbar. Es gibt keinen gesonderten I2-R3-Commit oder vollständigen eingefrorenen Worktree. Die geprüften archivierten PDBs mit dem Namen `CryptoWorkBenchGuiTest` gehören zusätzlichen Prüfhosts und enthalten keinen vollständigen Satz der regulären GUI-Testquellen. Für diese Tests, Runner, Projektdateien und die unversionierte Dokumentation lässt sich die Herkunft einzelner Zeilen deshalb nur über Git-Basis, Berichte und archivierte Prüfquellen eingrenzen. Aus dem aktuellen `git diff HEAD` allein lässt sich kein reiner I3-Patch ableiten. [I3-Dateiliste](G2-04-02-I3-completion-report.md) · [I2-R3](G2-04-02-I2-R3-final-dictionary-verification.md) · [I1-R3](G2-04-02-I1-R3-final-cleanup-verification.md)

Die F2-Originale, das F3-Korrekturmanifest, der F3-Bericht und die R4-Verifikation waren unter `C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4` verfügbar. Alle 14 F2-Dateien stimmen mit den F3-Nachher-Hashes überein. Geprüft wurden insbesondere Architektur, Accessor-Matrix, Copy-Vertrag und Abnahmekatalog. Das eingebettete Golden entspricht dem korrigierten Original, SHA-256 `0A4F17910F92BAF80F1628FB2FBA491BFC9015FCB7AA9ABE71FAE8479FC7DC40`. Ein entscheidendes normatives Original fehlt nicht. Für den früher abgebrochenen Großstring-Versuch lag im I3-Nachweissatz kein zuordenbares vollständiges Messprotokoll vor. [Prüfskript](../artifacts/test-results/g2-04-i3-r1/verify.ps1)

**Belegarten.** „Eigen“ bezeichnet eine R1-Ausführung oder einen eigenen Dateivergleich. „Original“ bezeichnet eingesehene Quellen, TRX-Einzelresultate, Protokolle oder native Aufnahmen aus früheren Läufen. „Berichtet“ bezeichnet eine Aussage ohne entsprechende Rohmessung. Ein positives Einzelresultat deckt nur die jeweils angegebene Bedingung ab.

## Findings

### R1-H01 · Hoch · Sichtbare Variables blockieren die Bedienung im Drei-Spalten-Layout

**Fundstelle.** Die Value-Spalte bindet den vollständigen `VariableModel.Value`. Das Summary-Template verwendet Ellipse, übergibt dem Text-Control aber den gesamten String. Auch die Details der ausgewählten Variable binden den vollständigen Wert an einen umbrochenen `SelectableTextBlock`. Die Inspector-Vollwertansicht besitzt dagegen eine eigene Textseitendarstellung. [Value-Spalte](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L44) · [Textbindung und Umbruch](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L93) · [Detailwert](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L123) · [Inspector-Textseite](../CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs#L96)

**Reproduktion und Soll.** Je Größe frischer Windows-Release-Prozess. Im echten Factory-Split sind Editor, Variables und Inspector gleichzeitig sichtbar. Die Variables-Tabelle ist rund 169 DIP, der Inspector 177 DIP breit. Der Prüfhost publiziert einen String mit Leerzeichen, Unicode und Zeilenenden und wählt die Zeile aus. Ein 50-ms-Dispatcher-Timer und im Bestätigungslauf ein vom Hintergrund eingereihter Auftrag mit Eingabepriorität messen, wann die Shell wieder Arbeit annimmt. Die Kontrollbedingung zeigt den Inspector in der gemeinsamen Tabgruppe, während Variables verdeckt ist. Erwartet werden eine bedienbare Shell und vollständige erreichbare Werte auch bei begrenztem Darstellungsraum.

**Eigenes Ergebnis.** Zeiten und Spitzenwerte der nachstehenden Serie gelten für eigene Prozesse. Der Monitor tastete den Working Set etwa alle 90 ms ab. CPU-Zeit umfasst Start und Ende des Prozesses. Die Prüfschranke war acht Sekunden ab beobachtetem START, zusätzlich 26 Sekunden Gesamtzeit oder 1.100 MiB Working Set. In den abgebrochenen Fällen griff die Zeitgrenze. Der Vollwertvergleich dieser Serie prüft die Übergabe an den Copy-Empfänger im Prozess.

| UTF-16-Einheiten | Variables sichtbar | Peak Working Set sichtbar | CPU sichtbar | Variables verdeckt |
|---:|---|---:|---:|---|
| 4.096 | Vollwert exakt, längstes Timerintervall 722 ms | 194,4 MiB | 2,83 s | Vollwert exakt, längstes Intervall 63 ms |
| 65.536 | Keine fertige Inspector-Seite vor Abbruch | 207,6 MiB | 9,64 s | Vollwert exakt, 62 ms |
| 262.144 | Keine fertige Inspector-Seite vor Abbruch | 226,1 MiB | 9,27 s | Vollwert exakt, 64 ms |
| 1.048.576 | Keine fertige Inspector-Seite vor Abbruch | 344,2 MiB | 9,67 s | Vollwert exakt, 64 ms |

Die Bestätigung mit Eingabepriorität ergab bei 4.096 Einheiten 546 ms Wartezeit gegenüber 1 ms im verdeckten Zustand. Bei 65.536 Einheiten wurde der Auftrag sichtbar eingereiht und bis zum Abbruch nicht ausgeführt. Verdeckt wurde er in 1 ms ausgeführt. Die Messung erlaubt keine Aussage über die spätere Fertigstellung eines abgebrochenen Laufs und keine Zuordnung der gesamten Wartezeit zu einem einzelnen Formatierungsschritt. [Messserie](../artifacts/test-results/g2-04-i3-r1/repeat/runs.json) · [Sichtbar, 4.096](../artifacts/test-results/g2-04-i3-r1/confirm/perf-4096.log) · [Sichtbar, 65.536](../artifacts/test-results/g2-04-i3-r1/confirm/perf-65536.log) · [Verdeckt, 65.536](../artifacts/test-results/g2-04-i3-r1/confirm/hidden-65536.log)

**Original und Einordnung.** Der ursprüngliche Funktionsprüfhost verglich im aktiven Inspector den aus der Windows-Zwischenablage zurückgelesenen String vollständig mit der Eingabe von 17.825.798 UTF-16-Einheiten. Beide Konfigurationen bestanden. Für den früheren Abbruch mit sichtbarer Tabelle ist der erhebliche Ressourcenverbrauch berichtet. Eine genaue Speicher- oder Zeitgrenze dieses früheren Laufs lässt sich daraus nicht belegen. **Vermutung:** Vermessung, Umbruch oder Zeichnung der ungekürzten Variables-Texte trägt zur UI-Blockade bei. Ohne Renderprofil lässt sich der Anteil von Summary, Detail, Layout und GC nicht trennen. [Ursprünglicher Prüfhost](../artifacts/test-results/g2-04-i3/functional/Program.cs) · [Release-Original](../artifacts/test-results/g2-04-i3/functional-Release.log) · [Debug-Original](../artifacts/test-results/g2-04-i3/functional-Debug.log)

**Abnahmewirkung.** Die vorhandene Variables-Darstellung ist älter als I3. Ihre Wirkung trifft unmittelbar die von I3 angebotene gleichzeitige Ansicht. Schon ein erheblich kleinerer Wert als der normative Großstring verhindert dort über Sekunden Eingaben und die Nutzung des Inspectors. Das blockiert die Abnahme. Für eine Nachprüfung müssen beide Tools gleichzeitig sichtbar sein, Eingaben bedient werden und der vollständige Originalwert weiterhin exakt kopierbar sein.

### R1-M01 · Mittel · Ein normaler Shutdown kann die gespeicherte Anordnung nach temporärem Startfehler verdrängen

**Fundstelle und Soll.** `Load` diagnostiziert einen Lesefehler und liefert den Default-Fallback. Die Shell merkt sich den Fehler für die Statusanzeige. Beim normalen `Closed` speichert sie den Shutdown-Snapshot unabhängig vom Grund des Fallbacks. Der sinnvolle Wiederherstellungsfall ist eine wieder lesbare, zuvor gültige Anordnung beim nächsten Start. Bei fehlgeschlagener Erstinstallation ist die v1-Quelle weiter verwendbar. [Load und Migration](../CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs#L67) · [Übernahme des Startup-Ergebnisses](../CryptoWorkBenchAvalonia/App.axaml.cs#L87) · [Shutdown-Speicherung](../CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs#L77)

**Eigen, gültiges v2.** Eine gültige v2-Datei mit eigenständigem Info-Fenster und geschlossenem Inspector wurde beim Start exklusiv gesperrt. Daneben lag eine gültige v1. Die App meldete `Layout load failed (IOException)` und zeigte erwartungsgemäß den neuen Standard. Anschließend wurde die Sperre gelöst und das Fenster ohne Layoutaktion normal geschlossen. v2 wechselte von SHA-256 `8436C449C74A70A1721E2EDA1C451BC5AE765C00CD6336A92F63CC6950969178` auf `FE6E359E4A726406F92D34A49F797E5CC8BC2703F98FE4AA950AF3556C56154D`. Der nächste Prozess öffnete den Inspector im Standardlayout mit null Floating Hosts. Die v1 blieb bytegleich. [Start unter Sperre](../artifacts/test-results/g2-04-i3-r1/confirm/lock-lockboot.log) · [v2 vorher](../artifacts/test-results/g2-04-i3-r1/confirm/lock-lockboot.before) · [v2 nach Shutdown](../artifacts/test-results/g2-04-i3-r1/confirm/lock-lockboot.after) · [Nächster Start](../artifacts/test-results/g2-04-i3-r1/confirm/lock-boot.log)

**Eigen, Erstinstallation.** Bei einer einmalig injizierten IOException vor dem ersten Move blieb v2 beim Start erwartungsgemäß aus. Danach gelang der normale Shutdown-Save und installierte den Standard als v2. Der nächste Prozess lud diesen Standard. Ein isolierter erneuter `Load` ohne dazwischenliegenden Shutdown migrierte dieselbe v1 dagegen erfolgreich. Der I3-Bericht beschreibt die weiter vorhandene Quelle zutreffend. Für die automatische Wiederholung nach regulärem Beenden reicht ihre Existenz nicht aus. [Fehlgeschlagener Erststart](../artifacts/test-results/g2-04-i3-r1/transient-faultboot.log) · [Folgestart](../artifacts/test-results/g2-04-i3-r1/transient-boot.log) · [Store-Gegenprobe](../artifacts/test-results/g2-04-i3-r1/disk.log)

**Abnahmewirkung.** Der atomare Dateiaustausch funktioniert. Der Fehler liegt in der Entscheidung, welchen Zustand die App nach einem vorübergehenden Startfehler persistiert. Der bedingungsarme Shutdown-Save stammt aus G1. Im v1→v2-Vertrag kann er den einzigen automatischen Migrationsversuch verdrängen. Eine bestehende gültige v2-Anpassung kann ohne Layoutaktion verloren gehen. Dieses Recovery-Verhalten sollte vor der Abnahme für beide Fälle korrigiert und über zwei tatsächliche Prozesse abgesichert werden. Die Wiederherstellung nach dauerhaft beschädigtem JSON bleibt ein eigener Fall.

## Lebensdauer, Auswahl und Fokus

| Eigenständige Windows-Probe | Beobachtung |
|---|---|
| Redock mit leer werdendem Quellhost | Floating Host verschwindet. Pane, View, VM, innere Sitzung mit demselben Snapshot, Seite, zweite Auswahl und StateRevision bleiben erhalten. Der gemessene Workspace-Listenerstand bleibt zwei. |
| Reset bei aktivem Floating-Inspector | Host schließt. Dieselben Referenzen, Listenerstand und Revision bleiben erhalten. Die wieder sichtbare Tabelle enthält dieselbe ausgewählte zweite Zeile. |
| Tatsächliches Schließen des Inspector-Fensters | View und VM aus der Präsentation entfernt, alter Snapshot und alte Seite leer, Listenerstand um eins reduziert. Ein weiterer Lauf hält eine schon begonnene Copy-Übergabe. Close setzt deren CancellationToken, während der externe Empfänger noch wartet. Nach Freigabe erfolgt keine neue Publikation. |
| Show, sofortiges Close vor Dispatcher-Publikation, Auswahlwechsel und erneutes Show | Die transiente VM hatte beim Close noch keine Seite und blieb leer. Die neue VM übernimmt die inzwischen gültige erste Zeile. Die Pane ist dieselbe, die View ist neu, der Listenerstand wieder zwei. |
| Reguläre Auswahl und Abwahl | Zweite Zeile per Mausklick und erste per Pfeil aufwärts ergaben jeweils dieselbe Zeile in Grid, VM und Inspector. Eine zusätzliche Probe setzte `grid.SelectedItem = null` am angehängten nativen Grid. Modell, Seite und Snapshot wurden leer. Strg+Leertaste löste in der interaktiven Gegenprobe keine Abwahl aus. |

Die Lifecycle-Serie bestand zweimal. Der erste Anlauf des Reviewhosts griff vor der visuellen Anheftung auf das Grid zu. Der endgültige Host wartet auf das benannte Grid. Für den expliziten Copy-Abbruch zählt nur der erfolgreich gebaute Lauf `cancel-actual`. [Lifecycle](../artifacts/test-results/g2-04-i3-r1/repeat/life.log) · [Wiederholung](../artifacts/test-results/g2-04-i3-r1/repeat/life2.log) · [Close bei gehaltener Copy](../artifacts/test-results/g2-04-i3-r1/cancel-actual.log) · [Interaktive Zeilenauswahl](../artifacts/test-results/g2-04-i3-r1/grid-ui.log)

**Tastatur im nativen Floating Window.** Der Prüfhost setzte den Startfokus auf „Inspect Value“. Ab dort erfolgte die gesamte Navigation mit echten Tasten. Enter öffnete `Value` und fokussierte „Inspect Length“. Rechts öffnete das skalare `Int32`-Blatt mit Wert 12 und fokussierte „Parent property“. Links kehrte zu `Value` zurück, Fokus weiterhin „Parent property“. Das nächste Links führte zur Wurzel, dort war „Inspect Text“ fokussiert. Das unmittelbar folgende Rechts öffnete nachweislich `Text`, einen leeren String, mit Fokus auf „Inspect Length“. UIA-Fokus und die vom Fenster protokollierten tatsächlichen Pfade stimmten überein. [Tastenfolge, Fokus und Ziel](../artifacts/test-results/g2-04-i3-r1/keyboard.log)

## Migration und Datenträger

Der unabhängige Vergleich konstruiert sein Soll direkt aus der historischen Pre-A02-Datei. Er ändert Schema und ClosedTools und lässt genau eine neu vergebene `dock:container:<GUID>`-Identität für die zuvor namenlose Floating-Root zu. Anschließend vergleicht er den **gesamten JSON-Baum** einschließlich aller Verweise. Er verwendet dafür keinen Produktdecoder. Die Gruppen enthalten unter anderem Variables/Output mit Output aktiv, die Proportionen 0,30 / 0,48 / 0,22, das Info-Fenster bei (160, 180) mit 500 × 420 und das Hauptfenster bei (80, 90) mit 1000 × 700. Status und Inspector sind im Ziel geschlossen. Die v1-Eingabebytes waren vor und nach jeder Probe identisch. [Vergleichsprogramm](../artifacts/test-results/g2-04-i3-r1/projection.cjs) · [Beide vollständigen Vergleiche](../artifacts/test-results/g2-04-i3-r1/projection.log)

| Startbedingung | Erster Zugriff und Datenträger | Folgeprüfung |
|---|---|---|
| Nur echte v1 | Vollständiges v2 installiert, v1 unverändert, kein Temp-Rest | Neuer Prozess stellt Info-Fenster und geschlossenen Inspector wieder her. |
| Ungültiges v2 neben gültigem v1 | Diagnose, Standard. Reines Load lässt defektes v2 stehen | Zweites Load diagnostiziert erneut. Nach regulärem Shell-Ende steht gültiger Standard auf Disk. Folgestart lädt ihn. |
| Vorhandenes exklusiv gesperrtes gültiges v2 | Ladefehler und Standard | Nach Entsperren kann ein neuer Store das Original lesen. Mit zwischenzeitlichem regulärem Shell-Ende tritt M01 ein. |
| Fehler vor erstem atomarem Move | v2 fehlt, kein temporärer Rest | Neuer Store migriert vollständig. Mit normalem Shutdown vor Neustart tritt M01 ein. |
| Andere Instanz installiert unterscheidbares gültiges v2 unmittelbar vor dem Move | Der Verlierer liest den Gewinner, LastError leer. Gewinnerbytes bleiben exakt erhalten | Neuer Store und frischer App-Prozess verwenden den Gewinner mit offenem Inspector, ohne Info-Fenster. |

Die zeitliche Konkurrenz wurde deterministisch am atomaren Übergang injiziert. Der G1-Fehlerpfad für Replace einer vorhandenen Datei sowie die historischen null bis sechs Hosts, sieben gültigen v2-Hosts und ungültigen Varianten waren im erneut ausgeführten fokussierten Prüfsatz enthalten. [Datenträgerfälle](../artifacts/test-results/g2-04-i3-r1/disk.log) · [Historischer Folgestart](../artifacts/test-results/g2-04-i3-r1/boot-success.log) · [Gewinner-Folgestart](../artifacts/test-results/g2-04-i3-r1/boot-winner.log) · [Ungültiges v2](../artifacts/test-results/g2-04-i3-r1/confirm/bad-boot.log) · [Folgestart mit repariertem Standard](../artifacts/test-results/g2-04-i3-r1/confirm/bad-boot-next.log)

## Testzahlen und KCV-Vertrag

Alle 16 Original-TRX wurden erneut gehasht und nach Einzelresultat ausgezählt. CLI-Skips stammen aus dem zugehörigen Konsolenprotokoll und wurden von TRX-NotExecuted abgezogen. Auch die 107 Test-IDs und Testnamen wurden in beiden vollständigen GUI-Läufen nachgewiesen. Die Originalmatrix stimmt. R1 wiederholte die 107 fokussierten Fälle in Debug und Release mit jeweils 107 Passed, 0 Failed, 0 Skipped und 0 weiteren NotExecuted. Die Gesamtsuiten wurden anhand ihrer Originalresultate geprüft. [Matrix](../artifacts/test-results/g2-04-i3/matrix.json) · [Eigener Debug-Lauf](../artifacts/test-results/g2-04-i3-r1/r1-focused-Debug.trx) · [Eigener Release-Lauf](../artifacts/test-results/g2-04-i3-r1/r1-focused-Release.trx)

| Originalsuite | Debug Passed | Release Passed | Failed je Build | Skipped je Build | Weitere NotExecuted je Build |
|---|---:|---:|---:|---:|---:|
| Core | 11.736 | 11.736 | 0 | 26 | 1 |
| GUI | 1.262 | 1.262 | 0 | 0 | 0 |
| MCP | 176 | 176 | 0 | 0 | 13 |
| I3 fokussiert, Teilmenge GUI | 107 | 107 | 0 | 0 | 0 |
| Historischer Hostprüfsatz | 34 | 34 | 0 | 0 | 0 |
| R3-Messproben | 2 | 2 | 0 | 0 | 0 |
| Explizites Calculate | 2 | 2 | 0 | 0 | 0 |
| Historischer R1-Host | 23 | 23 | 1 | 0 | 0 |

Der alte Test `CalculatedChildrenRemainNavigable` ruft auf der Zeile `NotCalculated` allgemeines `OpenAsync` auf und erwartet den KCV. Die Originalfehlermeldung zeigt den erwarteten Hexwert und tatsächlich `KeyVariableDeclaration`. A08 und I2-R3 verlangen eine ausdrückliche Berechnungsaktion. Die Vergleichsquelle ruft `CalculateAsync` auf und prüft danach auch die Navigation nach Length. Diese beiden Läufe sind grün. Der alte rote Fall ist transparent ausgewiesen und begründet keine I3-Korrektur. [Alte Assertion](../artifacts/test-results/g2-04-i3/probes/ComputedReview.cs#L18) · [Explizite Variante](../artifacts/test-results/g2-04-i3/explicit/ComputedReview.cs#L18) · [Fehlerprotokoll](../artifacts/test-results/g2-04-i3/last-probes-Debug.log)

## A01–A38 · Belege und Grenzen

Die Testnamen in dieser Tabelle sind in den geprüften Original-TRX oder den verlinkten Prüfquellen auffindbar. Übernommene Grenzen aus I1/I2 gelten weiterhin. [GUI-Original](../artifacts/test-results/g2-04-i3/owner-gui-Release.trx) · [Core-Original](../artifacts/test-results/g2-04-i3/core-Release.trx) · [I2-Korrekturprüfungen](../CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs) · [I1-Prüfungen](../CryptoWorkBenchGuiTest/InspectorReadTests.cs)

| ID | Belegart, geprüfter Umfang und verbleibende Grenze |
|---|---|
| A01 | Original. Inventarprüfung und sieben konkrete Roottypen in `BasePropertiesFirst`. |
| A02 | Original. Accessor-Matrix, Dictionary-Metadaten und Property-Goldens. Die I2-Grenze bei einzelnen geschlossenen BCL-Slots bleibt. |
| A03 | Original. `IndexersKeepSeparateSlots` und `UnknownSlotsAreVisibleWithoutDispatch` prüfen Slottrennung und ausbleibenden Getter-Dispatch. |
| A04 | Original. `ForeignCollectionIsNeverEnumerated` und Accessor-Matrix für bekannte Wrapper. |
| A05 | Eigen. Dockoperationen erhalten Snapshot und Revision. Original. Theme-Probe kontrolliert auch Dokument und Revision. Keine neue Instrumentierung sämtlicher Getter und Print-Ausgaben. |
| A06 | Original. `Golden("root-param")` und `Golden("param-adapter")` mit korrigiertem Golden, nativer vollständiger PARAM-Root. |
| A07 | Original. `GlobalSettingsNeverExecuteDuringHeaderProjection`, `HeaderOptionalBlocksPreserveFormatterAndCase` und vollständiger TR31H-Root. |
| A08 | Original. Explizite AES/TDEA-Fälle, ungültige Werte und getrennt ausgewiesene historische Gegenprobe. |
| A09 | Original und eigener Hashvergleich. 19 benannte Golden-Fälle einschließlich fünf fachlicher Roots. |
| A10 | Original. `Utf16Preserved`, Null/Empty, Whitespace und nativer Vergleich des 17.825.798 Einheiten langen Strings. |
| A11 | Original. Grenzen für Bytes und ulong sowie zeichenweises natives Share-Orakel für den gesamten Root. |
| A12 | Original. `FullDictionaryMatchesIndependentOrdinalEncoding` und Langschlüssel-Proben einschließlich identischer Vorschaupräfixe. |
| A13 | Original. Alias-, SyncRoot- und Unknown-Slot-Proben. End-to-End-Fixture für einen echten fachlichen Vorfahrenzyklus weiter offen. |
| A14 | Original. `DetailCopyUsesBoundPropertyAcrossSelectionChange` und vier Varianten `ExactHistoricalCoverage`. |
| A15 | Eigen. Close mit gehaltener Copy fordert Abbruch an und verhindert späte Publikation. Original. I2-Plattformgrenzen. Physische Speichererschöpfung offen. |
| A16 | Original. Unabhängiges natives Orakel prüft 255 × 131.072 Bytes bis zum Textende, 66.853.345 UTF-16-Einheiten in beiden Builds. |
| A17 | Original. Großstring-Vollwert im aktiven Inspector und große Map. Eigen. Gleichzeitige Sichtbarkeit beeinträchtigt den praktischen Zugang gemäß H01. Vollständige Tiefengrenze offen. |
| A18 | Original. 13 Core-Rangefälle im Gesamt-TRX. |
| A19 | Original. Spill-, beschädigte Segment-, Staging- und IO-Fehlerproben, darunter `ChangedSegmentCannotCommit`. |
| A20 | Original. SECRET-Lauf mit 133.706.690 gesampelten Payload-Bytes und 129 Dateien, null nach Ende. Beobachteter Prozesspeak 782.491.648 / 776.314.880 Bytes Debug/Release. Getrennte Heap-/Clipboard-Allokationsanteile offen. |
| A21 | Eigen. Gemessene UI-Verzögerung und unbeantworteter Eingabeauftrag gemäß H01. Original. Zwei R3-Messproben je Build. Allgemeiner Shell-p95 und isolierte native Clipboard-Handoff-Zeit bleiben offen. |
| A22 | Eigen. Snapshot/Listener-Freigabe und Handoff-Abschluss. Original. Spill/Cleanup und Shutdown. Bereinigung nach hartem Abbruch und dauerhafter OS-Löschverweigerung bleibt begrenzt. |
| A23 | Original. `ObserverCoalescesSelectionAndEditPreservesHistoryWhileResetClears` sowie Admission-Gegenproben. |
| A24 | Eigen. Close vor Dispatcher und anschließender Wechsel der gültigen Auswahl. Original. `DisposeBeforeDispatcherCannotPublishOrAcquire`. |
| A25 | Original. `ObserverCannotAcquireAndFailureReleasesIntent` und `WorkerStartAndStagingFailureAllowNextRead`. |
| A26 | Original. `ReadsDoNotAllocateExecutionTimersOrConsumeExecutionQuota`. Eigen. Keine Revisionsänderung bei visueller Verlagerung. |
| A27 | Original. `RejectedFinalValidationKeepsCleanupAcrossNextRead` sowie Host- und Dispatcher-Proben aus I1. |
| A28 | Original. `AssignmentIdentityIsCheckedEvenWithoutRevisionChange`, Draftwechsel und `SessionRetiresStaleHandle`. |
| A29 | Eigen. Held-Copy-Close und fokussierter Test zu Shutdown/Drain. Original. Host-Cancel und MCP-Konkurrenz. |
| A30 | Original. Nativer historischer Copy unter Lease und I2-Parent-/Seiten-Coverage. Eigen. Fokussierte Handoff-/Lease-Regression erneut bestanden. |
| A31 | Eigen. Regulärer Zeilenwechsel, programmgesteuerte Abwahl am angehängten Grid, Close vor Publikation, neues Show mit aktueller Auswahl. Original. Fachlicher Reset und Dokumentwechsel. |
| A32 | Eigen. Leerer Quellhost, aktives Floating-Reset und echtes Close getrennt, gleiche fachliche Identitäten bei Layoutoperationen. Maximize/Restore im fokussierten Wiederholungslauf. |
| A33 | Eigen. Echte Factory-Teilung und Home. Original. Windows-Aufnahmen zeigen drei Spalten und spätere gemeinsame rechte Gruppe bei 150 %. |
| A34 | Eigen. Fokussierte Layout-/Admission-Fälle. Original. Native Lease verweigert Float, vorhandener Copy bleibt nutzbar. |
| A35 | Eigen. Vollständiger Vergleich der historischen Projektion und ihrer Quellbytes, echte Wiederherstellung beim nächsten Prozess, Tests mit null bis sechs Hosts. |
| A36 | Eigen. Gültiges/ungültiges/gesperrtes v2, kein v1-Fallback, sieben Hosts und abgewiesene achte/leere/verschachtelte Varianten im fokussierten Lauf. Shutdown-Effekt unter M01. |
| A37 | Eigen. Atomare Fehler und konkurrierender Gewinner, Fokus-/Restore-Tests. Der produktive Folgestart nach vorübergehendem Fehler zeigt M01. |
| A38 | Eigen. Tastatur, UIA-Fokus und Folgeaktion im Floating Window sowie echte Zeilenauswahl. Original. System/Light/Dark und Rückkehr per Maus innerhalb des Hauptfensters bei 150 %. Native 100 %/125 %, manuelle Screenreader-Sprachausgabe und spezielle fensterübergreifende Maus-Redock-Sequenz bleiben OPEN. |

[Native Großwertprüfung](../artifacts/test-results/g2-04-i3/resource-Release.inspector.json) · [Ressourcenoriginal](../artifacts/test-results/g2-04-i3/resource-Release-metrics.json) · [Drei Spalten](../artifacts/test-results/g2-04-i3/windows-adjusted-columns.png) · [Rückkehr innerhalb des Hauptfensters](../artifacts/test-results/g2-04-i3/windows-regrouped.png)

## Übergabe

Die PNG-Fensteraufnahmen und UIA-Bäume dokumentieren den Layoutzustand. Die tatsächliche Eingabeabfolge der früheren Mausoperation stammt aus dem I3-Bericht. R1 hat dafür eine eigene Factory-Teilung und eigene native Tastatureingaben geprüft. Andere Desktop-Backends und die bekannte intermittierende Avalonia-Headless-Initialisierung bleiben offen. Die beiden grünen fokussierten Wiederholungen liefern keine Ursachenklärung. Die Review-Artefakte liegen im [lokalen Prüfverzeichnis](../artifacts/test-results/g2-04-i3-r1/).

**CORRECTION REQUIRED.** Für die erneute Vorlage sind die Bedienbarkeit mit gleichzeitig sichtbaren Variables und Inspector sowie der Datenträgerzustand nach temporärem Startfehler, regulärem Beenden und Neustart entscheidend. Produktkorrektur und Eigentümerabnahme sind gesonderte Schritte.
