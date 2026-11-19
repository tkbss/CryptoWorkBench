# G2-04-02-I3 · Docking, Persistenz und produktive Integration

**READY FOR OWNER REVIEW** · 18. November 2026 · `feature/gui-g2-04`

Der Variable Inspector ist in der produktiven Prism-Dock-Shell verfügbar. Frisch und nach Layout-Reset stehen Variables, Info und Inspector in einer gemeinsamen Tabgruppe rechts vom Editor. Die dritte Spalte entsteht durch eine Dock.Avalonia-Mausoperation. Auf Windows wurden auch das eigenständige Inspector-Fenster, ein Neustart mit diesem Fenster und die vollständige Clipboard-Übergabe von 66.853.345 UTF-16-Codeeinheiten geprüft. Die Eigentümerabnahme und das unabhängige I3-Review stehen aus. [Standardlayout](../artifacts/test-results/g2-04-i3/windows-default.png) · [Drei Spalten nach Maus-Split und Größenänderung](../artifacts/test-results/g2-04-i3/windows-adjusted-columns.png) · [Floating Window](../artifacts/test-results/g2-04-i3/windows-floating.png)

## Prüfgrundlage

Arbeitsort war der bestehende Worktree `artifacts/worktrees/gui-g2-04` des Repositorys `tkbss/CryptoWorkBench`. Feature-HEAD und lokaler master zeigten auf `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Der akzeptierte I1-/I2-Stand lag als uncommittierter Arbeitsstand vor. Vor der ersten Produktänderung stimmten alle 329 verglichenen Core- und Avalonia-Quelldokumente mit den Dokumentprüfsummen der aufbewahrten I2-R3-Debug-PDBs im Meter-Prüfhost überein. Diese Prüfung umfasst die kompilierten, vorhandenen Produktquellen. [Dateiweise Eingangsprüfung](../artifacts/test-results/g2-04-i3/baseline.json)

Die F2-, F3- und R4-Originale liegen außerhalb des Git-Worktrees unter `C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4`. Alle 14 F2-Dateien, darunter Architektur, Accessor-Matrix, Copy-Vertrag und A01–A38, stimmen mit den Nachher-Hashes aus `G2-04-01-F3/final-evidence.json` überein. Der F3-Korrekturbericht hat SHA-256 `F0556A1EF26BC319724439CB52F472BE11296D89CE3573D44E2CBBB9CCEC2D43`. Das eingebettete Golden hat den normativen Hash `0A4F17910F92BAF80F1628FB2FBA491BFC9015FCB7AA9ABE71FAE8479FC7DC40`. Der Originalbericht R4, beide Implementierungsberichte und ihre F1/F2-/R1–R3-Unterlagen waren verfügbar. [I1-R3](G2-04-02-I1-R3-final-cleanup-verification.md) · [I2-R3](G2-04-02-I2-R3-final-dictionary-verification.md)

## Shell und Lebensdauer

- `tool:variable-inspector` bezeichnet genau ein Pane-Objekt im Bestand der Shell. Das vorhandene View-Menü erzeugt Show, Float, Dock, Maximize und Close aus diesem Bestand. Die Pane-Menüs verwenden weiterhin die gemeinsamen Layout-Kommandos. „Inspect Variable“ im Variables-Tool aktiviert genau diese Pane über `DockShellLayout.Show`. Ein Auswahlwechsel allein lässt den aktiven Tab bestehen. [View-Menü](../artifacts/test-results/g2-04-i3/windows-view-actions.png)
- Die Präsentation startet den Inspector nach dem Editorstart anhand der tatsächlichen Pane-Mitgliedschaft. Eine migrierte oder geschlossen gespeicherte Pane hat zunächst keine Inspector-VM. Eine offene Pane besitzt eine View und eine VM mit der vorhandenen I2-`InspectorSession` am globalen `VariableViewModel`. Der Presenter hängt dieselbe View zwischen den Dock-Hosts um. Snapshots, fachlicher Host und Editor gehören weiterhin ihren bisherigen Besitzern.
- Beim Schließen über die Factory und beim Entfernen eines Inspector-Floating-Hosts wird der abgeschlossene Modellzustand ausgewertet. Ein geleerter Host nach einer Verschiebung, die Fensterschließung während des Layoutaustauschs und die ausgeblendeten Fokusgeschwister lösen keine fachliche Schließung aus. Bei tatsächlichem Close storniert I2 seine Anforderungen, entfernt Subscriptions, räumt Snapshot/Payload auf und leert eine noch gehaltene View. Die Präsentation trennt den Presenter. Das akzeptierte `Dispose` ist endgültig. Show erzeugt für dieselbe Pane eine neue View und VM und übernimmt die gültige Auswahl. Die Tests zählen die Workspace-Listener und prüfen die ausgebliebene Publikation bei Close vor dem Dispatcher-Read.
- Die Variables-Tabelle meldet Auswahländerungen während ihrer visuellen Initialisierung nicht an das Modell zurück. Avalonia setzt beim Wiederanheften vorübergehend die Auswahl auf null. Die Integrationsprobe hatte dabei trotz identischer finaler Zeile einen neuen Snapshot beobachtet. Das Grid stellt jetzt beim Attach die Modellauswahl wieder her und überträgt normale Änderungen nach dem Attach. Ein Wiederholungstest verwendet dafür die zweite Tabellenzeile.
- Maximize behält den Normalbaum und die ursprünglichen Panes. Restore stellt die zuvor gesicherte Anordnung wieder her. Der Reset setzt rechts Variables und unten Output aktiv. Links bleibt der Editor, Status ist geschlossen. Die oberen Breiten bleiben bei 70/30, die obere und untere Zeile bei 75/25. Tests vergleichen den serialisierten Normalbaum, die Seitenreferenz, die Auswahl, die StateRevision, das TextDocument und dessen UndoStack. [Permanente Lifecycle- und Migrationsproben](../CryptoWorkBenchGuiTest/DockInspectorTests.cs)

Die Inspector-Aktionen laufen über dieselben `CanRelocate`-, Factory- und `ChangeLayout`-Grenzen wie die bestehenden Tools. Geprüft wurden GUI-Betrieb, Host-Busy/aktive Operation, Pending, Observer, deaktiviertes Fenster, Editor-Locks, Dokument-Update, MCP-Übernahme/Lease/Drain, Stop, Fokusmodus und Layoutfault über die gemeinsamen Dock-/Koordinatorsuiten und ergänzende Inspector-Gegenproben. Historische Copy verwendet bereits gebundene Daten. Im nativen Lease-Test blieb Float gesperrt, während die vollständige Id kopierbar war. Beim Shutdown mit gehaltenem Copy-Handoff blieben Hauptfenster und Floating Host bis zur Freigabe offen. [Dock-Gates](../CryptoWorkBenchGuiTest/DockEditorTests.cs) · [Fokus und Fehlerschranke](../CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs) · [Natives Lease-Protokoll](../artifacts/test-results/g2-04-i3/delivery-native-Release.inspector.json)

## Dateiauswahl, Migration und atomare Speicherung

| Eingabe | Verarbeitung und geprüfte Wirkung |
|---|---|
| Gültiges `dock-layout-v2.json` | Schema 2 und sieben eindeutige IDs prüfen und laden. Eine danebenliegende ungültige v1 beeinflusst das Ergebnis nicht. |
| Nur gültiges `dock-layout-v1.json` | Unter dem historischen Schema 1 mit sechs IDs und höchstens sechs Hosts validieren. Zulässige alte namenlose Container erhalten wie bisher eine Struktur-ID. Die Projection übernimmt Gruppen, aktive Kinder, Proportionen und Geometrie. Nur die geschlossene Inspector-ID kommt hinzu. Das Ergebnis durchläuft die v2-Validierung und wird vor Rückgabe atomar installiert. |
| Vorhandenes ungültiges, nicht lesbares oder falsch versioniertes v2 | Diagnose und neuer Standard. Eine gültige v1 daneben bleibt unberücksichtigt. Die Windows-Dateisperre prüft den Lesefehler unabhängig von einem Syntaxfehler. |
| Ungültiges v1 oder fehlgeschlagene Erstspeicherung | Diagnose und neuer Standard. Die v1 bleibt als Quelle für einen späteren Start erhalten. |
| Zwei gleichzeitige Migrationen | Die Erstinstallation nutzt Move ohne Überschreiben. Gewinnt eine andere Instanz, wird deren v2 streng gelesen. Ein Test erzeugt die konkurrierende Datei genau am atomaren Übergang. |

Reguläres Save validiert vor dem Schreiben, schreibt eine temporäre Datei im selben Verzeichnis, flusht bis auf den Datenträger, validiert die geschriebene Datei und verwendet Replace bei vorhandenem Ziel. Die Projection enthält ausschließlich Dock-Struktur und Fenstergeometrie. Der Inspector-Name ist eine konstante Pane-Beschriftung. Auswahl, Titel mit Property-Inhalt, Origins, Interpreterobjekte, Snapshots, Dateien und Payloads fallen außerhalb des Formats. Beim Speichern im Fokusmodus liefert Capture den Normalbaum. [Layout-Store](../CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs) · [Fehlerinjektion und Datenumfang](../CryptoWorkBenchGuiTest/DockLayoutTests.cs)

Die Regression mit einem echten Factory-Split vergleicht den gesamten erwarteten v2-Text für null bis sechs historische Floating Hosts. Ein siebter Host ist in v2 zulässig. Achter, leerer und verschachtelter Host sowie fehlende oder doppelte Pane-IDs werden zurückgewiesen. Die echte historische Pre-A02-Fixture enthält ein eigenes Info-Fenster und benutzerdefinierte Gruppen. Original und für die native Migration eingesetzte v1-Kopie haben SHA-256 `7E54E250BA7F791968E4522C53BBB15771E1B9251A134CEA2504A086BE29F34F`. [Historischer Fixture](../CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json) · [Migration und Reset im Windows-Prozess](../artifacts/test-results/g2-04-i3/delivery-native-Release.inspector.json) · [Separater v2-Neustart](../artifacts/test-results/g2-04-i3/delivery-native-Release.inspector-restart.json)

## Native Windows-Abnahme

**Maus und Fenster.** Über „Inspect Variable“ wurde der zuvor ausgewählte KEY geöffnet. Der Inspector-Tab wurde innerhalb des Hauptfensters per echter Maus neben die rechte Gruppe gezogen. Anschließend wurden Variables in der mittleren Spalte aktiviert, beide Splitter verschoben und der Inspector zurück in die Tabgruppe gezogen. Die sichtbare Snapshot-Revision und die Aufnahmezeit blieben erhalten. Ein zweiter Lauf öffnete über View → Inspector → Float das Fenster „Inspector“. Die programmatische Windows-Probe prüfte HWND, native Caption, fehlenden Win32-Owner, Sichtbarkeit beim Minimieren des Hauptfensters, Home, Close/Reopen und den Wiederanlauf mit gespeichertem Host. Die ergänzende native Layoutsuite bestand in Debug und Release jeweils mit 53 Checks, die historische Host-Cleanup-Probe mit jeweils 29. [Drei Spalten](../artifacts/test-results/g2-04-i3/windows-adjusted-columns.png) · [Rückkehr zur Tabgruppe](../artifacts/test-results/g2-04-i3/windows-regrouped.png) · [Fensterprotokoll](../artifacts/test-results/g2-04-i3/resource-Debug.inspector.json)

**Theme und Daten.** System, Light und Dark durchliefen im produktiven Fenster Navigation, Copy, Fokus und Auf-/Zuklappen. Ein zusätzlicher Prüflauf verglich je Theme alle drei expliziten AES-KCV-Varianten mit den Referenzstrings, vier vollständige F3-Roots (PARAM, TR31H, SECRET_SHAMIR, SECRET_SPLIT), Vor-/Zurück-Paging und Vollwertansicht. Seitenidentität beim Themewechsel, Editor-Dokument und Revision wurden geprüft. Eine weitere Probe verglich einen vollständigen String mit 17.825.798 UTF-16-Codeeinheiten einschließlich Whitespace und Unicode direkt über den nativen Inspector. [Debug](../artifacts/test-results/g2-04-i3/functional-Debug.log) · [Release](../artifacts/test-results/g2-04-i3/functional-Release.log) · [Dark-Vollwert](../artifacts/test-results/g2-04-i3/windows-dark.png)

**Clipboard und Ressourcen.** Die produktive Shell übergab den vollständigen SECRET_SPLIT-Root mit 255 Shares zu je 131.072 Bytes. Der unabhängige Vergleich prüft jedes Zeichen einschließlich Metadaten, Reihenfolge und Wertende. Debug und Release lieferten exakt 66.853.345 UTF-16-Codeeinheiten. Im eigenen Prozess-Tempverzeichnis wurden jeweils maximal 129 Payload-Dateien mit zusammen 133.706.690 Bytes beobachtet, nach Prozessende jeweils null. Der beobachtete Prozess-Peak betrug 782.491.648 Bytes in Debug und 776.314.880 in Release. Die Gesamtläufe dauerten 4.337 bzw. 4.216 ms einschließlich Start und Shutdown. Die 100-ms-Abtastung erfasst den Prozess insgesamt. Sie liefert keine isolierte Clipboard-Latenz oder vollständige Heap-Aufteilung. Rechner mit Intel Core i7-14700F, rund 16 GiB RAM, Windows 11 Home Build 26200, .NET SDK 10.0.401. [Debug-Messung](../artifacts/test-results/g2-04-i3/resource-Debug-metrics.json) · [Release-Messung](../artifacts/test-results/g2-04-i3/resource-Release-metrics.json)

**DPI, Tastatur und Accessibility.** Windows meldete einen Bildschirm mit Faktor 1,5 und Arbeitsfläche 3840 × 2088 physischen Pixeln. Mehrere Fenster- und Spaltenbreiten wurden auf diesem Bildschirm geprüft. Native Durchgänge bei 100 % und 125 % bleiben OPEN. UI Automation fand „Inspect Value“, „Copy Value“, „Calculate Kcv“, den Vollwert-Editor sowie die Pane mit ihrer stabilen ID. Auf dem Desktop wurden Shift+Tab, Enter, Pfeil rechts/links und Strg+C betätigt. Auf „Inspect Value“ öffnete Enter den String, rechts öffnete Length. Der markierte Text „36“ wurde per Strg+C exakt in der Windows-Zwischenablage bestätigt. Nach Rücknavigation vom skalaren Wert war im Floating Window „Inspect Text“ fokussiert. Die manuelle Screenreader-Sprachausgabe ist **OPEN**. [UIA-Reihenfolge](../artifacts/test-results/g2-04-i3/windows-backtab.uia.txt) · [Fokus nach Rückkehr](../artifacts/test-results/g2-04-i3/windows-floating-return-focus.uia.txt) · [Strg+C-Abgleich](../artifacts/test-results/g2-04-i3/ctrl-c-matches.txt)

## Testmatrix

Die Werte gelten einzeln für die angegebene Konfiguration. CLI-Skips sind bereits in den TRX-NotExecuted enthalten. „Weitere NotExecuted“ zieht die CLI-Skips ab. Die fokussierte Suite ist in der GUI-Gesamtsuite enthalten.

| Suite | Debug Passed | Release Passed | Failed je Build | Skipped je Build | Weitere NotExecuted je Build |
|---|---:|---:|---:|---:|---:|
| Core gesamt | 11.736 | 11.736 | 0 | 26 | 1 |
| GUI gesamt | 1.262 | 1.262 | 0 | 0 | 0 |
| MCP gesamt | 176 | 176 | 0 | 0 | 13 |
| Fokussierte I3-, Standardlayout-, Persistenz- und Cleanup-Tests | 107 | 107 | 0 | 0 | 0 |
| Zusätzlicher archivierter Hostprüfsatz einschließlich Langschlüssel | 34 | 34 | 0 | 0 | 0 |
| Zusätzliche R3-Messproben | 2 | 2 | 0 | 0 | 0 |
| Archivierte explizite Calculate-Variante | 2 | 2 | 0 | 0 | 0 |
| Unveränderter historischer R1-Prüfhost | 23 | 23 | 1 | 0 | 0 |

Die Desktop-Builds Debug und Release waren erfolgreich. Alle 19 F3-Goldens, 38 I1-Inspectorfälle, 40 I2-Propertyfälle, 27 I2-F1-Korrekturtests, vier Dictionary-F2-Fälle und 13 Core-Rangefälle sind in den Gesamtsuiten enthalten. Der archivierte R1-Fall `CalculatedChildrenRemainNavigable` fordert über allgemeines Open die implizite Berechnung eines KCV. Er bleibt wie in I2-R3 rot. Die akzeptierte explizite Calculate-Variante besteht in beiden Builds. [Einzelresultate, Zählung und Hashes](../artifacts/test-results/g2-04-i3/matrix.json) · [Historischer Originalfehler](../artifacts/test-results/g2-04-i3/last-probes-Debug.log) · [Explizite Variante](../artifacts/test-results/g2-04-i3/last-explicit-Debug.log)

## Zuordnung zum F3-korrigierten Abnahmekatalog

| ID | Nachweis und Grenze |
|---|---|
| A01 | Die sieben Runtime-Inventare und ihre Reflection-Slots bestehen im permanenten I2-Prüfsatz. |
| A02 | Accessor-Familien, geerbte Properties, HeaderReference, OriginalKeyMetadata und Collection-Metadaten über Inventar-, Graph- und Golden-Tests. Die I2-Grenze für nicht einzeln getestete geschlossene BCL-Slots bleibt. |
| A03 | Override, Verdeckung, Indexer, nicht öffentlicher und unbekannter Getter einschließlich Aufrufzähler. |
| A04 | Fremde Implementierungen und die Zugriffsgrenzen der bekannten Collection-Wrapper sind durch I2-Negativproben und Accessor-Review abgedeckt. |
| A05 | Wiederholte Dockzyklen, Themewechsel und Kopien erhalten Revision und Dokument. Die Inspector-Funktionen verwenden die Read-only-Pipeline. |
| A06 | PARAM-Root und Methodenadapter stimmen mit dem Golden überein. Die bestehende Capture-Strecke bleibt unverändert. |
| A07 | Optional Blocks, Definition und fremde JsonConvert-Konfiguration im permanenten Test, vollständiger TR31H-Root zusätzlich nativ. |
| A08 | Referenztests für AES/TDEA, unbekannte und ungültige Werte, Whitespace sowie getrennte explizite Berechnung. Alle drei AES-Vektoren liefen nativ in allen Themes. |
| A09 | 19 einzeln ausgewiesene F3-Goldens, darunter die fünf fachlichen Roots. |
| A10 | Exakte Unicode-, Null-, Leer- und Whitespacewerte. Der native Großstring enthält CRLF, führenden Whitespace und abschließendes LF. |
| A11 | Hex-, Zahlengrenzen-, Range- und Mehrsegmentproben, dazu der native Vergleich jedes Share-Bytes über den kanonischen Root. |
| A12 | Ordinale Schlüsselreihenfolge, Escaping, Sonderkeys und lange kollidierende Key-Präfixe mit getrenntem historischem Originalwert. |
| A13 | Alias, Array.SyncRoot, unbekannter Getter und ausstehende Berechnung im permanenten Prüfsatz. Für einen fachlichen Vorfahrenzyklus bleibt die I2-End-to-End-Fixture offen. |
| A14 | Gebundene Herkunft, Seiten-Coverage und historische Copy nach Edit und während Lease, einschließlich gehaltenem Handoff. |
| A15 | I1-/I2-Fehler-, Cancel- und Handoff-Proben. NUL und Plattformgrenzen sind im I2-Nativevidenzsatz dokumentiert. Physische OS-Speichererschöpfung wurde in I3 nicht provoziert. |
| A16 | Vollständiges natives 255-Share-Orakel mit 66.853.345 UTF-16-Einheiten. |
| A17 | Nativer 17-MiB-String und große Maps, 300.001 Keys und späte Seiten im wiederholten Hostprüfsatz. Allgemeine Tiefen-/Lastgrenzen bleiben teilweise offen. |
| A18 | Alle 13 Core-Range-Regressionen bestehen. |
| A19 | Spill, Segmentschäden, unpubliziertes Staging, Abbruch und simulierte IO-Fehler in I1. Das native Sample erfasst den Spill oberhalb des Cachebudgets. |
| A20 | Peak Working Set, Payload-Dateien und vollständige native Ausgabe gemessen. Exakte Allokationsaufteilung pro Stufe bleibt offen. |
| A21 | Historische Render-/Handoff-Messungen und erneut ausgeführte R3-Schritt-/Cancel-Proben. Ein allgemeiner p95-Nachweis für die produktive Shell unter Last bleibt offen. |
| A22 | Close/Reopen, Workspace-Reset, Shutdown mit aktiver Copy, gesperrte Segmente und null eigene Dateien nach geordnetem Prozessende. Bereinigung nach hartem Prozessabbruch bleibt offen. |
| A23 | Coalescing und nachgelagerte Aufnahme aus Coordinator-Observation im I1-Test. |
| A24 | Auswahlwechsel, Neuaufnahme und Dispose vor Dispatcher-Publikation. Acht zusätzliche Close/Reopen-Zyklen zählen die tatsächlichen Listener. |
| A25 | Annahmefehler, Workerstart und Freigabe über den vorhandenen Hostowner in I1. |
| A26 | Quota-/Retention-Proben und unveränderte StateRevision während Navigation und Dockzyklen. |
| A27 | Dispatcher-Commit, spät invalidiertes Worker-Ergebnis und fehlgeschlagene Finalvalidierung im permanenten Hostprüfsatz. |
| A28 | Identität trotz gleichen Namens, Draft-Edit, Dokumentwechsel, Revisions- und historische Coverage-Proben. |
| A29 | Busy-/Cancellation-/Drain-Gegenproben in I1 sowie Shutdown bei gehaltener Copy im Floating-Host. |
| A30 | Vollständige historische Daten nach Edit und unter MCP-Lease einschließlich echter Windows-Zwischenablage. |
| A31 | Getrennte fachliche Reset- und Close-Pfade. Eine alte VM bleibt nach Wiederöffnung leer, die neue übernimmt die aktuelle Auswahl. |
| A32 | Gleiche View, VM, Seite, Auswahl und Revision über Split, Tabwechsel, Float, Home, Maximize/Restore und Layout-Reset. |
| A33 | Zwei-Spalten-Default, echte dritte Mausspalte, beide Splitter, Rückkehr zur Tabgruppe sowie natives Floating belegt. |
| A34 | Gemeinsame Admission und Fehlerschranke, einschließlich verweigerter Inspector-Aktionen und nativer Lease-Gegenprobe. |
| A35 | Historischer Validator, null bis sechs Hosts, Split und geschlossene Tools. Vergleich der ganzen v2-Projektion und der v1-Bytes. |
| A36 | Sieben Hosts zulässig. Achtes, leeres, verschachteltes, doppeltes oder unvollständiges Layout sowie unlesbares/vorhandenes falsches v2 werden zurückgewiesen. |
| A37 | Atomare Fehler, konkurrierende Erstinstallation, Normalbaum während Fokus, exaktes Restore und neuer Default nach Migration. Layout enthält nur Struktur und Geometrie. |
| A38 | Reale Maus-, Tastatur-, Theme-, UIA- und Floating-Nachweise bei 150 %. Spezielle fensterübergreifende Maus-Redock-Sequenz und manuelle Sprachausgabe bleiben OPEN. |

## Zwischenbefunde und Korrekturen

1. Der erste Debug-Build mit vorhandenen Restore-Artefakten fand `AttachDeveloperTools` nicht. Konfigurationsbezogener Restore mit der vorbereiteten Prism-Umgebung löste den Buildfehler. Ein paralleler Zwischenbuild traf auf die noch nicht vollständige neue Native-Probenklasse. Die abschließenden Builds nutzten den vollständigen Quellstand.
2. Im ersten breiten Debug-Docklauf bestanden 328 Fälle und drei scheiterten. Die Ganzgruppen-Probe führte nur die bisherigen Tabs zurück. Zwei Toolzyklen versuchten die nächste Mutation während der nach Auswahl tatsächlich laufenden Inspector-Aufnahme. Die Proben führen jetzt alle Gruppenmitglieder zurück und warten für die Ready-Zyklen auf das Ende der Auswahlaufnahme. Die Busy-Gegenproben bleiben aktiv.
3. Die erste I3-Lifecycleprobe fand einen echten Seitenidentitätswechsel. Der Stack zeigte `DataGrid.OnAttachedToVisualTree → InitializeElements → CoerceSelectedItem` mit null und anschließend derselben Variable. Die Korrektur sitzt im Dock-Grid, die Assertion vergleicht weiterhin die tatsächliche Seitenreferenz. [Originalstack](../artifacts/test-results/g2-04-i3/selection-trace.log)
4. Beim nativen Rücksprung vom skalaren Blatt zur Wurzel konnte der zuvor fokussierte Zurück-Button deaktiviert werden. Tab erreichte dann die globale Shell-Navigation. Die einzige Änderung an der akzeptierten I2-Inspector-View sichert diesen Fokus vor dem Aktualisieren der Buttons und Breadcrumbs. Der permanente Tastaturtest und die externe Windows-Fokusabfrage belegen die Rückkehr in die Inspector-Zeilen.
5. Die erste native Probe fragte den neuen Presenter unmittelbar nach dem Show-Ereignis ab. Sie wartet jetzt auf die angehängte Produkt-View. Ein früher Versuch mit einem bereits im sichtbaren Variables-Tool ausgewählten 17-MiB-String wurde nach erheblichem Ressourcenverbrauch beendet. Der vollständige String-Vergleich im aktiven Inspector bestand anschließend. Die Kosten der bestehenden Variables-Großtextanzeige sind ein gesonderter offener Befund.
6. Die zusätzliche native G1-Layoutprobe erreichte beim ersten Debug-Lauf den erwarteten Vordergrundzustand nicht und meldete einen Timeout. Der Wiederholungslauf und Release bestanden mit jeweils 53 Checks. Der bekannte Avalonia-Headless-Befund `Dispatcher.VerifyAccess / DefaultRenderLoop.Add` trat in den I3-Protokollen nicht erneut auf. Seine Ursache bleibt offen. [Erster Windows-Lauf](../artifacts/test-results/g2-04-i3/layout-Debug) · [Wiederholung](../artifacts/test-results/g2-04-i3/layout-repeat-Debug)

## Dateiumfang und Übergabe

| Bereich | Durch I3 geänderte oder neue Dateien |
|---|---|
| Produktstart und Layout | `CryptoWorkBenchAvalonia/App.axaml.cs`, unter `CryptoWorkBenchAvalonia/Shell/` die Dateien `DockShellState.cs`, `DockShellLayout.cs`, `DockShellLayout.Persistence.cs`, `DockShellPresentation.cs`, `DockShellWindow.axaml.cs`, `DockVariablesView.cs`, `EditorDockFactory.cs`, `DockLayoutStore.cs` |
| Akzeptierte I2-View | `CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs`, begrenzte Fokuskorrektur aus Befund 4 |
| Neue permanente Prüfungen | `CryptoWorkBenchGuiTest/DockInspectorTests.cs`, `CryptoWorkBenchGuiTest/DockInspectorNativeProbe.cs` |
| Weitergeführte GUI-Prüfungen | Unter `CryptoWorkBenchGuiTest/` die Dateien `DockStandardLayoutTests.cs`, `DockShellTests.cs`, `DockLayoutTests.cs`, `DockLayoutRoleTests.cs`, `DockFinalIntegrationTests.cs`, `DockToolTests.cs`, `DockPersistenceCleanupTests.cs`, `DockShellNativeProbe.cs`, `DockLayoutNativeProbe.cs` und `DockPersistenceNativeProbe.cs` |
| Native Einstiegspunkte | `eng/gui/native/Program.cs`, `eng/gui/Validate-G1LayoutRoles.ps1` |
| Bericht und lokale Evidenz | Dieser Bericht sowie Original-TRX, Build-/Probeprotokolle, Quellen der ergänzenden Prüfhosts, Screenshots und Prüfsummen unter `artifacts/test-results/g2-04-i3/` |

Beim unabhängigen Review zuerst die fachliche Schließung gegen die leere Quellgruppe nach Redock und gegen Reset eines aktiven Floating-Hosts prüfen. Danach die Datenrichtung des Variables-Grids und den Fokuswechsel vom skalaren Blatt untersuchen. Für die Persistenz bieten sich die Dateisperre, der konkurrierende v2-Gewinner und der Vergleich der gesamten migrierten Projektion an. Die Findings I1-H01/H02, M01–M06, L01 und I2-H01/H02, M01–M03, L01/L02 sind über die wiederholten Integritäts-, Cleanup-, historischen Copy-, Langschlüssel-, expliziten Berechnungs-, Inventar- und UI-Proben zugeordnet. Die archivierten Prüfhostläufe bleiben getrennt von den permanenten Suiten ausgewiesen.

Zur Reproduktion im Feature-Worktree `eng/prism/Enter-Prism.ps1` dot-sourcen und mit konfigurationsbezogenem Restore bauen. Der native Runner `eng/gui/native/G1ShellProbe.csproj` unterstützt `inspector`, `inspector-restart` und `inspector-review`. Für `inspector` einen frischen isolierten Ausgabeprefix verwenden, für den Neustart denselben Prefix. Eine historische Eingabe gehört nach `<prefix>.v1.json`. Layout und Appearance des Prüfhosts liegen unter diesem Prefix. Die Report- und Prüfarbeitsstände sind uncommittiert. Andere Desktop-Backends, die manuelle Screenreader-Ausgabe und die bekannte spezielle Cross-Window-Redock-Sequenz bleiben eigene Abnahmepunkte. [Prüfverzeichnis](../artifacts/test-results/g2-04-i3/)

