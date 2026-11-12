# G1-07 – Layout Persistence, Maximization and Restore

Stand: 2026-11-06. Ergebnis: **CONDITIONAL GO für G1-08 nach ausdrücklicher Eigentümerabnahme**, im bisher nachgewiesenen Windows-Umfang. G1-08 wurde nicht begonnen. Kein Commit, Push oder Merge.

## 1. Ausgangsstand und Git-Status

Bearbeitet wurde ausschließlich `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Der Eingangsstand enthält 33 modifizierte getrackte und 53 ungetrackte Dateien aus G1-01 bis G1-06. Diese Änderungen sind kein G1-07-Diff gegen HEAD und wurden nicht zurückgesetzt.

Vor Produktänderungen wurden die 86 Arbeitsdateien unter `artifacts/g1-07/baseline/files` kopiert, der vollständige Status, ein getrackter Patch und SHA-256-Manifeste gesichert. Der Entwurf liegt in `artifacts/g1-07/layout-design.txt`. Grundlagen waren die sechs vorhandenen G1-Berichte samt Evidence, Layout/Factory/Identitäten, G1-01-Serializer sowie EditorSession, CapabilityPolicy und asynchroner Shutdown.

Abschluss: weiterhin 33 modifizierte getrackte Dateien, jetzt 60 ungetrackte Dateien. Gegenüber der gesicherten Baseline wurden acht vorhandene Dateien geändert und sieben Dateien ergänzt. Die Evidence enthält den vollständigen Anfangs-/Endstatus und die SHA-256-Werte aller versionierten und nicht ignorierten Arbeitsdateien. Der Eigenhash der Evidence steht separat in `artifacts/g1-07/final-deliverable-hashes.json`, um eine Selbstreferenz zu vermeiden. Die 88 geschützten Berichte, Evidence-Dateien, Prototypen und Paket-Locks sind bytegleich. Alle fremden Worktrees blieben unangetastet.

## 2. Architektur der Layoutverwaltung

`DockLayoutStore` kapselt das begrenzte Dateiformat und atomare Schreiben. Die Partial-Datei `DockShellLayout.Persistence.cs` ergänzt den vorhandenen Layoutbesitzer um Startup-Restore, normalen Snapshot, Fokusmodus und Reset. `DockWindowGeometry` übersetzt gespeicherte Präsentationsgeometrie auf die tatsächlich verfügbaren Avalonia-Screens. Es gibt keinen zusätzlichen Hintergrunddienst und keine neue Ausführungsautorität.

Vor jedem Serialisieren wird ein eigener, ausschließlich aus erlaubten Präsentationsfeldern bestehender Dock-Baum erzeugt. Nur dieser Baum wird dem vorhandenen `Dock.Serializer.SystemTextJson` übergeben. Live-Contexts, Views und Dienste gelangen nie in den Serializer. Nach Decode wird erneut projiziert und anschließend auf die sechs vorhandenen Pane-Instanzen des produktiven Layouts gebunden.

## 3. Format und Schema-Versionierung

Schema 1 besitzt vier Envelope-Felder: `SchemaVersion`, `Dock`, `ClosedTools`, `MainWindow`. Letzteres enthält X/Y, normale Breite/Höhe und `Maximized`. Der Dock-Teil enthält die erlaubte Dock-Typmetadaten-/Referenzstruktur, logische IDs, sichtbare Kinder, aktiven Tab, Proportion, Orientierung, Ausrichtung sowie Floating-Layout und -Geometrie.

Die festen Pane-IDs bleiben `document:cryptoscript:primary`, `tool:variables`, `tool:output`, `tool:info`, `tool:diagnostics`, `tool:status`. Jede muss genau einmal im sichtbaren Baum oder, ausschließlich für Tools, im geschlossenen Inventar vorkommen. `dock:root` und der eindeutige `dock:main` sichern die feste Shell-Struktur; doppelte bekannte Zielanker werden verworfen. Unbekannte Schema-Versionen führen heute zum Default. Eine spätere Migration kann explizit vor dem Schema-1-Decoder ergänzt werden; es gibt keine spekulative Vorwärtskompatibilität.

## 4. Speicherung, Startup-Restore und Default

Produktpfad: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoWorkBench", "dock-layout-v1.json")`. Der Pfad enthält weder einen festen Windows-Benutzernamen noch das Repository. Native Probes überschreiben nur die Store-Erzeugung und speichern neben ihren jeweiligen Ergebnisdateien; sie berühren keine echte Benutzerkonfiguration.

Bei `--dock-shell` werden Container und globale Dienste regulär aufgebaut, die Datei geladen und vollständig validiert, Geometrien begrenzt und die Pane-Contexts gebunden. `Factory.InitDockable` initialisiert Owner/Hosts, ohne Floating-Fenster vorzeitig zu zeigen. Erst danach startet die eine produktive `DockEditorSession`; dies ist keine Folge interaktiver Detach-/Attach-Operationen. Startup-Restore verweigert Aufrufe, sobald eine Session oder ein DockControl gebunden ist.

Fehlende Datei ergibt das Standardlayout. Leeres, kaputtes oder unzulässiges JSON und Lesefehler ergeben nachvollziehbar protokollierten Default-Fallback. Eine ungültige Layoutdatei wird nicht als erfolgreicher Restore behandelt und führt auch nicht zu einem zweiten Legacy-Editor. Die Legacy-Shell bleibt Standardstart und nutzt den Layout-Store nicht.

Ein neuer Prozess erhält neue Objektinstanzen, bindet aber dieselben logischen Identitäten an die für diesen Prozess vorgesehenen Prism-Singletons und genau eine autoritative Editorregistrierung. Innerhalb einer Sitzung bleiben Pane-, View-, ViewModel-, TextDocument- und History-Instanzen bei Fokus/Restore/Reset erhalten. Es wird keine Objektidentität über Prozessgrenzen behauptet; Editorinhalt wird bewusst nicht wiederhergestellt.

## 5. Maximieren, Wiederherstellen und Reset

Alle sechs Bereiche unterstützen Fokusmodus. Dafür werden die ursprünglichen Kinderlisten, aktiven Kinder und Proportionen entlang des Owner-Pfades aufbewahrt und vorübergehend auf den ausgewählten Pfad reduziert. Restore setzt dieselben Listen und Referenzen zurück. Der aktuelle Floating-Host bleibt dabei derselbe. Zehn Zyklen je Pane prüfen exakten Baum-/JSON-Erhalt sowie Undo/Redo und Registrierung.

Das Hauptmenü bietet `Maximize Active View`, `Restore Layout`, `Reset to Default Layout` und pane-spezifisches Maximieren. Jedes Pane besitzt außerdem ein `Layout`-Menü, sodass die Aktionen im Floating-Fenster erreichbar bleiben. Es wurden keine bestehenden Tastengesten überschrieben. Abgewiesene oder nicht anwendbare Hauptmenüaktionen erhalten einen Statushinweis.

Während Fokusmodus sind normale strukturelle Dockoperationen gesperrt. Restore/Reset bleiben über die ursprüngliche Admission-Prüfung möglich. Reset erstellt den validierten Default-Baum und bindet die existierenden Panes neu; alte Floating-Hosts werden innerhalb der bereits zugelassenen Mutation geschlossen. Der eng begrenzte interne Austauschzustand ist kein allgemeiner Busy-/MCP-Bypass.

Der normale Dock-Snapshot wird vor Fokusbeginn erfasst. Speichern während Fokus verwendet diesen Snapshot, einschließlich ursprünglicher Sichtbarkeit und Floating-Geometrie, zusammen mit der aktuellen normalen Hauptfenstergeometrie. Der temporäre Fokuszustand überschreibt das normale Layout nicht. Ein Neustart beginnt im normalen Dock-Layout.

## 6. Floating-Windows und Bildschirmgeometrie

Grundlage sind `Window.Screens.All`, deren `WorkingArea` und `Scaling`. Positionen sind native Pixel, Clientgrößen DIP. Nicht erreichbare Positionen werden auf einen vorhandenen Bildschirm begrenzt; ungültige/überdimensionierte Größen werden auf eine positive erreichbare Größe reduziert. Für den Fensterrand wird vertikaler Platz reserviert. Normale Hauptfensterbounds werden nur im normalen Fensterzustand verfolgt; der Maximiert-Zustand wird separat gespeichert.

Nativ geprüft: Windows 10.0.26200, .NET 10.0.12, ein Bildschirm, WorkingArea 3840×2088, Skalierung 1,5; Hauptfenster bis 640×480 verkleinert. Der Geometrie-Neustart erhält absichtlich X/Y-Werte von ±800000 bzw. ±900000 und einen maximierten Hauptfensterzustand. Er bestätigt sichtbaren Inhalt, erreichbare normale Position, 1000×700 Normalgröße und erreichbaren Floating-Host. Beide Konfigurationen bestehen.

Synthetische Tests prüfen 1920×1080, 320×240, Skalierung 1/2, ungültige Größen, entfernten Monitor und eine vorhandene zweite Arbeitsfläche mit negativen Koordinaten. Das ist **kein nativer Multi-Monitor-, Monitor-Abzieh-, Auflösungswechsel- oder gemischter-DPI-Nachweis**. Linux/macOS wurden nicht nativ geprüft.

## 7. Sicherheits- und Datenschutzgrenzen

Dateigröße maximal 256 KiB, JSON-Tiefe 64, Baumtiefe 20 und begrenzte Knotenzahl. Vor Dock-Deserialisierung werden doppelte JSON-Eigenschaften, unbekannte Felder und alle nicht explizit erlaubten Typnamen abgewiesen. Erlaubt sind acht konkrete Dock-Präsentationstypen; keine frei aktivierbaren Anwendungstypen. Nach Decode folgen Prüfungen auf Identitäten, Referenzzyklen, Eltern/Kind-Struktur, Splitfolge/-werte, aktive Tabs und Floating-Konfiguration. Verschachtelte Floating-Fenster, leere Floating-Inhalte und Pinned-/Hidden-Bäume sind nicht Teil von Schema 1.

Nicht enthalten: Quelltext, Undo/Redo, Variablen/Schlüsselmaterial, ScriptBooks, Interpreterzustand, Output, Diagnostics, InfoDoc, MCP-Credentials/Lease/Transport, Dialoge oder Completion-Popups. Auch Titel und Context werden vor Serialisierung entfernt. Tests verwenden Geheimnis-Sentinels und kontrollieren die erzeugte Datei. Fehlerdiagnosen enthalten Operation und Ausnahmetyp, keinen Dateiinhalt und keine beliebige Exception-Nachricht. Layoutdateien sind Präsentationskonfiguration, kein fachliches Workspace-Backup.

## 8. Editor-/MCP-Lebenszyklus

Interaktive Maximize-/Restore-/Reset-Aktionen laufen durch `EditorDockFactory.ChangeLayout` und die vorhandene `Session.Relocate`-/Coordinator-Beobachtung. GUI-Ausführung, aktive MCP-Lease, Pending, Host-Busy, Modalität, Observer und Shutdown verweigern Mutationen ohne späteres Replay. Hide/Restore-Dockoperationen werden zusätzlich an derselben Factory-Grenze erfasst. Drag/Drop, Float, Redock und Tool-Close behalten die vorhandene Policy. Reine native Fenstergrößenänderung und Tab-Auswahl sind Präsentationsvorgänge und keine neue Execution-Admission.

Keine Änderungen an EditorCoordinator, InterpreterHostQueue, MCP-Protokoll, Lease-/Ticket-Autorität oder erzwungenes Release. Die bisherigen sicheren produktiven Startup-Failure-/Shutdown-Verträge bleiben bestehen. Neue Tests prüfen unveränderte EditorId/WorkspaceGeneration, lebende Registrierung, eindeutige Freigabe, abgewiesene Aktionen nach Dispose und Erhalt von Hosts bei fehlgeschlagenem Shutdown.

## 9. Atomare Speicherung und Fehlerverhalten

Beim kontrollierten Schließen wird vor Drain und Host-Abbau der normale Snapshot erfasst. Erst das tatsächliche `Closed` schreibt ihn; anschließend wird das Layout freigegeben. Ein wartender/fehlgeschlagener Shutdown schreibt nichts und räumt keine lebenden Floating-Hosts weg. Der bereits erfasste Snapshot benötigt beim späteren Commit keine entsorgten Hosts.

Save validiert den Snapshot, schreibt eine neue temporäre Datei im Zielverzeichnis, führt `Flush(true)` aus, validiert deren Inhalt erneut und ersetzt dann mit `File.Replace` bzw. veröffentlicht erstmalig mit `File.Move`. Es gibt keinen Delete-first-Fallback. Fehlgeschlagene Ersetzung lässt die bisherige gültige Datei bestehen. Tests prüfen gesperrtes Ziel sowie injizierten Abbruch vor Replace bytegenau. Ein harter Prozess-/Stromausfall und die Dauerhaftigkeit der Verzeichnis-Metadaten wurden nicht simuliert; eine dann zurückbleibende temporäre Datei wird nicht als Layout geladen.

## 10. Automatisierte Tests und Review

47 neue GUI-Testfälle in `DockLayoutTests`: fünf Save/Restore-Zyklen; Tabs/Reihenfolge/Proportionen/Closed-Tools; stabile Contexts; Dateifehler und Formatangriffe; atomarer Fehlpfad; sechs Panes mit je zehn Fokuszyklen; schwebender Editor/Tool und Reset; Singleton-Views/-Modelle; Pending/Active/Host-Busy/Dialog/Observer/MCP-Lease; Drain, fehlgeschlagener Shutdown und verspätete Aktionen; reale Host-Snapshots und synthetische Bildschirmgeometrie.

Die bestehenden Suiten decken weiterhin Return/KP_Enter, Undo/Redo, Line History, Completion, Save/Open, Variables/Info, Output/Clear, Diagnostics/Filter, Quellnavigation und Desktop-MCP ab. `git diff --check`, zusätzliche Whitespace-/Konfliktmarkerprüfung aller neuen ungetrackten Dateien, JSON-Parsing und Baseline-Hashvergleich wurden ausgeführt. Keine bestehende Assertion wurde abgeschwächt.

Zwischenläufe bleiben erhalten: In zwei früheren vollständigen GUI-Läufen scheiterte der unveränderte Bestandsfall `R3OwnershipPrecedesLeaseCallbacksAndEveryRejectedInitializationUnwinds("shutdown-throw")` bei `Document.Insert` mit `ExecutionBusyException`. Die unveränderte Review-Fixture bestand isoliert 49/49; spätere vollständige Läufe und die abschließende Matrix bestanden. Eine Ursache wurde nicht bewiesen und der Fall nicht als behoben ausgegeben. Die Intermittenz bleibt ein offenes Bestandsrisiko.

Weitere erhaltene Zwischenbefunde: frühe Implementierungs-/Compilerfehler wurden korrigiert; paralleler Testhost blockierte einmal einen Build; konfigurationsfremde Restore-Assets wurden durch gesperrten Restore pro Konfiguration korrigiert. Ein Debug-Inputlauf scheiterte am App-Freigabe-Timeout, ein Release-Outputlauf am ausbleibenden ersten externen Eingabesignal. Die gesondert benannten Wiederholungen bestanden. Der neue Geometrietest prüfte zunächst fälschlich den Ursprung eines maximierten Windows-Rahmens auf Zugehörigkeit zur WorkingArea. Er prüft jetzt sichtbare Inhaltsmitte und zusätzlich die erreichbare Normalposition; die alte Fehlprobe ist erhalten. Dies änderte keine Bestandsassertion und keinen Produktcode.

## 11. Native Windows-Ergebnisse

Der bestehende `G1ShellProbe` wurde um `layout`, `layout-restart`, `layout-invalid`, `layout-input` erweitert. Keine zweite Native-Infrastruktur. Die Geometrieprobe verwendet `layout-restart` mit absichtlich veränderter Eingabedatei. Die endgültigen programmatischen Ergebnisse heißen `complete-native-{Debug|Release}-*.json`.

| G1-07-Lauf | Debug | Release | Art |
|---|---:|---:|---|
| Default, sechs Panes, Fokus/Restore, Close/Save | 48 | 48 | programmatisch im nativen Prozess |
| echter zweiter Prozess mit gespeicherter Datei | 56 | 56 | programmatisch, tatsächlicher Startup-Restore |
| ungültiges Schema → Default | 49 | 49 | programmatisch |
| Offscreen + maximiertes Hauptfenster | 56 | 56 | programmatisch mit tatsächlichen Screen-Daten |
| Layout-Menüs im Haupt- und Floating-Fenster | 53 | 53 | 48 programmatische Prüfungen + 5 externe Menü-Checkpoints |
| **G1-07 zusätzlich** | **262** | **262** | keine Fehler in den ausgewählten Abschlussläufen |

Externe Windows-Eingaben über Computer Use: Hauptfenster `View > Maximize Active View`, `Restore Layout`, Floating-Info `Layout > Maximize Active View`, `Restore Layout`, Hauptfenster `Reset to Default Layout`. Ergebnisdateien: `layout-input-Debug-retry.json` und `layout-input-Release.json`. Die sechs Einzel-Panes werden darüber hinaus programmatisch maximiert; hierfür wird kein vollständiger externer Sechs-Pane-Mausnachweis behauptet.

Die unveränderten externen Bestandsprobes wurden ebenfalls in beiden Konfigurationen bedient: Unicode-Eingabe, Undo/Redo, Return/KP_Enter, History/Completion, Variables-Selektion/Clipboard, Info-Selektion/Copy Example/Navigation, Output-Clipboard/Quellnavigation/Clear, Diagnosefilter/-Clipboard und Quellnavigation einschließlich schwebendem Editor und Stale-Hinweis. `output-input-Release-retry.json` ersetzt für die Abschlusswertung den erhaltenen Timeoutlauf. Programmatisches `PerformTextInput` und direkte Layoutbefehle werden nicht als externe Eingaben gezählt. Der native Runner bestätigt die Ergebnisse durch Clipboard-/Caret-/Fokus-/Modellvergleiche.

## 12. Debug-/Release-Testmatrix

| Suite | Debug bestanden | Release bestanden | Unveränderte Ausnahmen je Konfiguration |
|---|---:|---:|---|
| GUI gesamt | 944 | 944 | 0 |
| davon G1-06-Bestand | 897 | 897 | 0 |
| davon G1-07 neu | 47 | 47 | 0 |
| MCP | 176 | 176 | 13 Explicit |
| Core | 11.512 | 11.512 | 26 Skipped, 1 Explicit |
| G1-01 Headless | 12 | 12 | 0 |
| Native Windows G1-01–G1-06 | 457 | 457 | 0 |
| Native Windows G1-07 zusätzlich | 262 | 262 | 0 |

Die vollständige Suite-Matrix liegt unter `artifacts/g1-07/verified`, G1-01 unter `foundation`. .NET-Restore/Build verwendeten die vorhandenen gesperrten Abhängigkeiten und die verifizierte Prism-Integration. Nach der vollständigen Matrix wurde ausschließlich die neue Native-Geometrieassertion präzisiert; beide Native-Builds und alle neuen programmatischen Native-Läufe wurden danach erneut ausgeführt. Produktcode und 47 GUI-Testfälle der bestandenen Matrix blieben unverändert. Paketversionen: Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock und Dock-Serializer 12.1.0.13.

## 13. Änderungen an vorhandenen Tests

`DockShellNativeProbe.cs`: genau eine Modusverzweigung und ein Store-Factory-Override. Die Verzweigung ruft die neue Partial-Prüfung auf; der Override isoliert native Prüfungen von Benutzerlayoutdateien. Entfernt man diese beiden Ergänzungen, entspricht die Datei bytegenau der gesicherten Baseline. Alle übrigen bestehenden Testdateien und Assertion-Bodies blieben bytegleich.

`eng/gui/native/Program.cs` akzeptiert die neuen Modi. `Validate-G1Shell.ps1` erhält den optionalen Schalter `-LayoutIntegration` und bereitet Neustart-/Invalid-Fixtures vor. Bestehende Modusprüfungen wurden nicht ersetzt. Neue Tests liegen ausschließlich in `DockLayoutTests.cs` und `DockLayoutNativeProbe.cs`.

## 14. Geänderte Dateien gegenüber Arbeitsbaseline

| Vorhandene Datei | G1-07-Änderung |
|---|---|
| `CryptoWorkBenchAvalonia/App.axaml.cs` | Store/Restore vor produktivem Start; Snapshot vor Drain |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs` | Partial, Root-Austausch, Fokus-/Dispose-/Host-Verknüpfung |
| `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs` | zugelassene Layoutmutation und Fokus-Sperre |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs` | Geometrie, Menüs, Startup-Bindung, Snapshot/Commit |
| `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs` | Layout-Menü in Pane-Presentern |
| `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs` | isolierter Store und neue Dispatch-Zeile |
| `eng/gui/native/Program.cs` | Modusliste erweitern |
| `eng/gui/Validate-G1Shell.ps1` | optionaler Layout-Nachweis |

Neu: `CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs`, `DockShellLayout.Persistence.cs`, `DockWindowGeometry.cs`; `CryptoWorkBenchGuiTest/DockLayoutTests.cs`, `DockLayoutNativeProbe.cs`; dieser Bericht und `docs/gui/evidence/G1-07-results.json`.

Die vollständigen Pfade und Hashes stehen in der Evidence. Parser, AST, Evaluator, fachliche Ausführung, MCP-Semantik und ScriptBook-Format wurden gegenüber dem Eingangsstand nicht verändert. Sichtbare ältere Änderungen an solchen Dateien gehören zur gesicherten G1-Baseline.

## 15. Offene Risiken und Nachweisgrenzen

Nativ offen bleiben Linux/macOS, mehrere Monitore, Entfernen eines realen Monitors, Auflösungs-/DPI-Wechsel, vollständige externe Cross-Window-Maus-Redock-/Overlay-Matrix und native File-Picker. Die bestehenden Installed-Codex-Explicit-Fälle wurden nicht zusätzlich ausgeführt. Die kleine Geometrie-Heuristik berücksichtigt keinen plattformspezifischen vollständigen Fensterrahmen auf jedem Window Manager. Die atomare Ersetzung wurde auf dem vorhandenen Windows-Dateisystem, nicht bei Stromverlust oder auf fremden Dateisystemen geprüft.

Die oben dokumentierte intermittierende Bestandsassertion bleibt beobachtungsbedürftig. Vorhandene Avalonia/Dock-Binding-Diagnosen beim Reparenten/Abbau stehen in den Rohlogs; sie werden nicht als fehlerfreie Trace-Ausgabe verschwiegen. Der Nachweis stützt sich auf die bestandenen Identitäts-, Host-, Fokus-, Admission- und Shutdown-Assertions. Schema 1 unterstützt das feste G1-Modell und lehnt nicht unterstützte Pinned-/verschachtelte Floating-Strukturen kontrolliert ab.

## 16. Entscheidung und Abschluss

**CONDITIONAL GO** für den nächsten G1-Schritt innerhalb des geprüften Windows-Umfangs: Layout-Persistenz, sechs Fokusbereiche, normales Restore, Reset, sichere Dateigrenze und Editor-/MCP-Lebenszyklus sind implementiert; Debug und Release bestehen die vollständige geforderte Baseline sowie die neuen Tests. Eine plattformübergreifende Produktionsfreigabe oder vollständige Maus-Redock-Abdeckung folgt daraus nicht.

G1-07 ist zur Eigentümerabnahme bereit. Die Arbeit stoppt hier. G1-08 beginnt erst nach ausdrücklicher Eigentümerabnahme.
