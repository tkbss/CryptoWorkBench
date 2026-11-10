# G1-06 – Diagnostics and Source Navigation

Stand: 6. November 2026. G1-06 implementiert und in Debug sowie Release geprüft. Ergebnis: **CONDITIONAL GO für G1-07**, vorbehaltlich ausdrücklicher Eigentümerabnahme und der unten aufgeführten übernommenen Plattformgrenzen.

## Ausgangsstand

Ausschließlich `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Die uncommittierten G1-01–G1-05-Vorarbeiten wurden vor Produktänderungen einschließlich aller getrackten und ungetrackten Quelldateien nach `artifacts/g1-06/baseline/` kopiert. SHA-256: `baseline.json`; vollständiger Ausgangsstatus: `initial-status.txt`. Hauptworktree und `feature/script-assignment` wurden nicht verändert. Kein Commit, Push oder Merge. Paketversionen, Locks und lokale Prism-Integration bleiben unverändert.

Der vor Produktänderungen gespeicherte Datenfluss-/Quellenentwurf liegt unter `artifacts/g1-06/dataflow-design.md`.

## Analyse und Datenfluss

`ParserBuilder.StringBuild(...).program()` füllt unter bestehender Hostzulassung die `SyntaxErrorList`. `SyntaxErrorListner` und `LexerErrorListener` liefern strukturierte Zeilen-/Spaltenstrings, Symbol und Meldung. Zeilen sind einsbasiert, Spalten nullbasiert. Bereits bestehende Import-Redaktion und spezielle Tokenzuordnung bleiben unverändert. `WorkbenchExecutionPipeline` kopiert diese Liste in `SyntaxDiagnostics`; Parserfehler verhindern schon bisher den Runnerstart. Die bisherige GUI zeigte sie im aggregierten Statusstring, nicht in der semantischen Liste.

Semantische Meldungen stammen aus dem ausführungseigenen `SemanticDiagnosticExecution`/Sink und den bestehenden Legacy-/Exception-Brücken. Die Pipeline kopiert sie und erhält die Referenzgleichheit einer bereits enthaltenen Exception-Diagnostic. Severity wird unverändert aus `SemanticDiagnosticSeverity` übernommen. Fehler und Warnungen bestimmen in S-01 nicht allein den Abbruch: Ein berichteter Fehler kann weiterhin mit erfolgreich abgeschlossener Ausführung zusammenfallen. `SemanticErrorException` ergibt `Aborted`; andere Ausnahmen ergeben `Failed`. Rohe Exceptiontexte werden schon bisher nicht behalten. Die neue Darstellung verwendet bei `Failed` ausschließlich den vorhandenen sicheren Status und bei `CaptureFailed` die bestehende sichere Capture-Meldung. Abbruch/Cancellation ist Status und wird nicht künstlich als Fehler klassifiziert.

`DiagnosticLocation` enthält ExecutionId, StatementIndex, OperationId, ParentOperationId und fachliche Namen, jedoch keine Textkoordinaten. Ein StatementIndex ist keine Quellzeile. Deshalb erfolgt weder Textsuche noch Zuordnung nach Schlüsselwörtern oder Fehlermeldungstext. Keine Änderung an Core, Parser, Grammatik, Receipt oder fachlicher Severity war erforderlich.

Der zugelassene GUI-Pfad bleibt `CryptoScriptEditViewModel.Present`: ursprüngliche Destination und EditorIntent prüfen, Print und Info in ihre bisherigen Ziele, dann Diagnostics und Status, zuletzt Variables. Diagnostics werden weiterhin an den vorhandenen Zulassungsgrenzen geleert: angenommene lokale Line-Ausführung, Beginn der GUI-Gesamteditor-Ausführung und bisheriger Open-Book-Pfad. Der interne Book-Helfer behält seine bisherige Aufrufersemantik. Docking, Filtern, Copy und Navigation leeren keine Diagnostics. Es entsteht kein neues längerfristiges Meldungsprotokoll.

## Kategorien, Zustand und Views

`DiagnosticDisplayItem` bleibt unveränderlich und erhält `DiagnosticCategory`, ursprüngliche Severity/Text, nullable DocumentId/ExecutionId/SourceLine/SourceColumn/SourceRevision, Sequence und die bestehende ScriptBookLine. Kategorien werden ausschließlich aus `SyntaxDiagnostics`, semantischer Severity und `ExecutionState.Failed`/`CaptureFailed` gebildet. Unbekannte Metadaten bleiben null. Wiederholte Meldungen bleiben getrennte Einträge; nur dieselbe bereits transportierte Exception wird nicht nochmals angehängt. Sequence gibt die ursprüngliche Batch-/Book-Reihenfolge wieder.

Der bestehende Singleton `StatusViewModel` ist der einzige Diagnostics- und Statuszustand. Seine schreibgeschützte Collection bleibt erhalten; `VisibleDiagnostics` ist die All/Errors/Warnings-Projektion. PresentationId verhindert doppelte Batch-Publikation innerhalb des aktuellen Diagnostics-Zyklus; Current-/Epoch-Prüfungen begrenzen die Veröffentlichung bei Widerruf bzw. Clear. Die bestehenden Pipeline-Gates bleiben für verspätete Ergebnisse maßgebend.

`DockShellPresentation` hält genau eine `DockDiagnosticsView` und eine `DockStatusView`. Beim Reparenting wird der bisherige Presenter gelöst; weder Float/Redock noch Close/Show erzeugt einen neuen globalen Zustand oder entsorgt das StatusViewModel. Die Diagnostics-Liste verwendet Avalonia ListBox, farbige Fehler-/Warnungsränder, Kategoriebezeichnungen, explizite Quellenbuttons und SelectableTextBlock mit Ctrl+C. Der gewählte Filter wird angezeigt. Status zeigt ausschließlich den bestehenden StatusString in einer selektierbaren, scrollbaren Ansicht; initial steht `Ready` anstelle des bisherigen Platzhaltertexts `bla`. Save/Open-, Busy-, Ausführungs- und Shutdowntexte bleiben an ihren bisherigen Produzenten.

Die Legacy-Shell verwendet weiterhin `StatusView` und den bisherigen semantischen Append-Pfad. Syntax bleibt dort im bestehenden Statusstring. Dadurch bleiben ihre Assertions und ihr Präsentationsvertrag erhalten; die erweiterte Kategorisierung ist die freigegebene Dock-Integration.

## Quellen und gemeinsame Navigation

G1-05 `OutputSource` wird unverändert als gemeinsamer angenommener GUI-Quellkontext wiederverwendet: logische Editorregistrierung, exakte TextDocument-Version und validierte erste physische Zeile. Native Enter erfasst weiterhin nach der nativen Fortsetzung und überprüft den ausgeführten Text. Fremdtext erhält keine erfundene Dokumentposition.

Syntaxkoordinaten werden einmal numerisch geprüft und von nullbasierter Parser-Spalte zu einsbasierter Editor-Spalte konvertiert. Der Meldungstext einschließlich historischer Koordinatendarstellung bleibt unverändert. ScriptBook addiert den physischen Zeilenoffset einschließlich übersprungener historischer `out:`-Zeilen. Semantische Meldungen haben im allgemeinen Line-/Mehrzeilentext keine bekannte Position. Nur eine verlässlich zugeordnete einzelne physische ScriptBook-Einheit erlaubt Zeilennavigation; deren Spalte bleibt unbekannt. Insbesondere wird keine AST-Position aus StatementIndex erfunden.

`DockEditorSession.NavigateSource` ist der einzige Validator für Output und Diagnostics. Er prüft CanRelocate, gültige Metadaten, Editoridentität, Referenzgleichheit der TextDocument-Version sowie Zeilen-/Spaltengrenzen. Nur eine validierte Zeile mit unbekannter Spalte darf am Zeilenanfang aktiviert werden; dies ergänzt keine erfundene SourceColumn im Modell. Innerhalb des bestehenden Coordinator-Observers werden Caret, Scrollposition, natives Besitzerfenster und Fokus geändert. Keine Mutation, Undo-Operation, History, Ausführung, Admission oder Retry entsteht.

Veraltete Identität/Revision, unbekannte Quelle, ungültige Grenzen und Busy/MCP werden unterscheidbar zurückgemeldet. Bei einem abgewiesenen Quellenbutton erscheint ein sichtbarer Hinweis. Keine automatische Verschiebung, kein Replay. Auch Undo zum gleichen sichtbaren Text liefert eine andere Version und bleibt deshalb konservativ veraltet.

## GUI-/MCP-Grenze und Nebenläufigkeit

Lokale GUI-Line-, lokale Book- und bestehende GUI-Gesamteditor-Ausführung publizieren über ihren ursprünglichen GUI-Präsentationspfad. MCP V2 behält ausschließlich seine bisherigen Ergebnisse/Receipts; keine zusätzliche Diagnostics-Publikation wurde eingeführt. MCP-Editoränderungen erzeugen keine Diagnostics und entwerten alte Quellrevisionen. EditorCoordinator, EditorBridge, InterpreterHostQueue, deren Observer-/Pending-/Lease-/Shutdown-Gates und der Desktop-Transport bleiben unverändert.

Teilprints, semantische Meldungen und bereits erfasste Variablen bleiben gemäß bestehendem Ergebnisvertrag vor späterem Fehler/Abbruch sichtbar. Eine allgemeine Ausführungsausnahme hat einen sicheren Text und keine erfundene Quelle. Geschlossene Tools erhalten weiterhin Modellupdates. Shutdown widerruft die ursprüngliche Destination vor Drain/Dispose; verspätete Resultate dürfen keine neue Session befüllen.

## Prüfungen und Review

Die bestehenden Testfälle und Assertions werden nicht verändert. Der vorhandene G1ShellProbe wird über eine zusätzliche Modusverzweigung in seiner Dispatch-Methode und eine neue Partial-Datei erweitert; seine bisherigen Prüfroutinen bleiben bytegleich. Neue Modi: `diagnostics` und `diagnostics-input`, zuschaltbar über `Validate-G1Shell.ps1 -DiagnosticsIntegration`.

Die neuen `DockDiagnosticsTests` prüfen Parserkoordinaten und mehrere Fehler derselben Zeile, S-01-Fortsetzung, Warning/Error/Print-Kombinationen, Teilresultate vor Abbruch, sichere Infrastrukturfehler, CaptureFailed, reine Filterprojektion, Status-Trennung, Close/Float/Redock, Ctrl+C in beiden Hosts, Quellbutton/Fokus, Stale/Bounds/Identity/unknown column, beide Book-Pfade, Fremdtext, Dedup/Widerruf, Cancellation bei geschlossenem Tool, Pending/Observer/Busy/MCP sowie Shutdown mit spätem Fehler. Die vollständigen Bestandsuiten decken Return/KP_Enter, Undo/Redo, History, Completion, Variables, Info/Copy Example, Output, Save/Open, MCP-Transport/Admission und Shutdown weiterhin ab.

Erster Review-Nachweis: 871/871 bestehende GUI-Fälle und 26/26 neue Fälle bestanden. Der Sandbox-Restore blieb bei Projektermittlung stehen; der abgebrochene Zwischenlauf bleibt dokumentiert. Gesperrter Restore und Tests außerhalb dieser Sandboxgrenze liefen erfolgreich. Keine Paketdatei wurde dafür geändert.

## Abschließende Testmatrix und native Nachweise

| Suite | Debug bestanden | Release bestanden | Nicht ausgeführt je Konfiguration |
|---|---:|---:|---|
| GUI | 897 | 897 | 0 |
| MCP | 176 | 176 | 13 Explicit |
| Core | 11.512 | 11.512 | 26 Skipped, 1 Explicit |
| G1-01 Headless | 12 | 12 | 0 |
| Native Windows | 457 | 457 | 0 |

Alle abschließenden Läufe haben **0 Fehler**. Gegenüber G1-05 kommen je Konfiguration 26 GUI-Fälle und 70 native Checks hinzu. Explicit/Skipped sind nicht als bestanden gezählt. Beide Solutions wurden je Konfiguration über die bestehende Prism-Buildintegration gebaut; Restore erfolgte mit vorhandenen Locks. Bestehende Buildwarnungen bleiben in den Rohlogs sichtbar.

Native Aufteilung je Konfiguration: Foundation 60; Dock 67; Legacy 10; Fallback 13; Editor 54; Startup-Failure 5; Variables/Info 32; Output 29; neue Diagnostics 35. Zusätzlich echte externe Eingaben: Editor 54; Variables/Info 34; Output 29; neue Diagnostics 35. Damit entfallen 305 Checks auf programmgestützte Szenarien und 152 auf Szenarien mit externen Eingaben. Die externen Szenarien enthalten weiterhin programmgesteuerte Fixture-/Layoutvorbereitung und Ergebnisassertions; sie werden nicht als vollständig manuelle Abläufe bezeichnet.

Externe Eingaben wurden mit dem Windows-Computer-Use-Werkzeug `@oai/sky` an den benannten G1ShellProbe-Prüfpunkten ausgeführt. Tatsächliche Mausauswahl, Ctrl+A/Ctrl+C mit exaktem Clipboardvergleich, All/Errors/Warnings, Quellbuttons, Fokus, Unicode-Eingabe, Return/KP_Enter, Undo/Redo, Alt+PageUp, Completion-Tasteneingabe, Info-Link/Back und Copy Example sind getrennt von den programmgesteuerten Läufen belegt. Diagnostics wurden in beiden Hosts visuell geprüft; Fehler/Warnungen und der Hinweis bei abgelehnter Navigation sind lesbar. Es wurden weder UI-Automation über Test-Interna als externe Eingabe ausgegeben noch Bestandsassertions abgeschwächt.

Primäre Rohbelege liegen unter `artifacts/g1-06/verified-Debug`, `verified-Release` und `foundation`; der letzte Debug-GUI-Lauf liegt in `GUI-Debug-review.trx`. Native Release-Endläufe sind `native-Release-review-{dock,legacy,fallback}.json` und `native-Release-active-{editor,startup-failure,tools,output,diagnostics}.json`. Externe Debug-Endläufe sind `editor-input-Debug-retry.json`, `tools-input-Debug.json`, `output-input-Debug.json`, `diagnostics-input-Debug-retry.json`; Release verwendet entsprechend `*-input-Release.json`. `docs/gui/evidence/G1-06-results.json` enthält die Einzelchecks, Pfade, Hashes, ausgeschlossenen Fälle und vollständigen Git-Status.

Zwischenläufe bleiben unverändert erhalten: Der erste externe Debug-Diagnostics-Lauf lief wegen abgelaufener Computer-Use-App-Freigabe in einen Timeout (5 bestanden, 1 fehlgeschlagen). Der erste externe Debug-Editorlauf und der erste Release-Editor-Reviewlauf erreichten den initialen Vordergrund nicht (je 0/1). Der erste native Release-Docklauf bestand 54 Checks und verfehlte 13 Vordergrundchecks. Nach bestätigter App-Freigabe bzw. expliziter Fensteraktivierung bestanden die vollständigen Wiederholungen. Zeitgrenzen und Assertions wurden hierfür nicht verändert. Temporär nicht verfügbare UIA-Geometrie wurde durch eine neue Fensteraufnahme und sichtbare Koordinaten ersetzt; erst der erfolgreiche Clipboard-/Caretvergleich zählt als Nachweis.

## Änderungsumfang und Bestandsschutz

Ausgangsstatus: 31 modifizierte getrackte Dateien und 47 ungetrackte Quelldateien aus G1-01–G1-05. Abschluss: 33 modifizierte getrackte Dateien und 53 ungetrackte Quelldateien. Der vollständige Status wird mit `--untracked-files=all` in der neuen Evidence-Datei festgehalten; `artifacts/g1-06` bleibt ein ignoriertes Rohbelegverzeichnis. HEAD und Branch bleiben unverändert.

Acht bereits vorhandene Dateien wurden gegenüber dem gesicherten Arbeitsstand geändert:

- `CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs`: unveränderliche Kategorie-/Quellmetadaten.
- `CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs`: strukturierte Dock-Diagnostics und Filterprojektion.
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`: vorhandenen GUI-Präsentationspunkt anschließen.
- `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs`: gemeinsame validierte Navigation.
- `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs`: Singleton-Views reparenten.
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`: genau eine Dispatch-Zeile für die neuen Modi.
- `eng/gui/native/Program.cs`: neue Modi akzeptieren.
- `eng/gui/Validate-G1Shell.ps1`: optionaler Diagnostics-Lauf.

Sechs neue Dateien: `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs`, `CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs`, `CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs`, `CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs`, dieser Bericht und `docs/gui/evidence/G1-06-results.json`.

Die einzige Ergänzung innerhalb einer bisherigen Testdatei ist die Dispatch-Zeile im ausdrücklich zu erweiternden G1ShellProbe. Entfernt man diese neue Zeile für den Vergleich, ist die alte Datei bytegleich. Alle bisherigen Test-/Assertion-Bodies, übrigen Testdateien, G1-Berichte, Evidence-Dateien, Prototypen und Paket-Locks sind gegenüber dem Eingangsmanifest bytegleich. Neue Prüflogik liegt ausschließlich in den neuen Testdateien. `git diff --check`, zusätzliche Whitespace-/Konfliktmarkerprüfung der neuen Dateien und JSON-Validierung sind Bestandteil des Abschlusschecks.

## Risiken und Freigabe

Übernommene Grenzen: Linux/macOS nativ, gemischte DPI/Multi-Monitor, vollständige externe Cross-Window-Maus-Redock-/Overlay-Matrix, native File-Picker und Installed-Codex-Explicit-Fälle sind weiterhin nicht belegt. Semantische Operationen besitzen außerhalb der verlässlich zugeordneten Book-Zeile keine navigierbaren Tokenkoordinaten; die UI kennzeichnet dies ausdrücklich. Konservative Revisionserkennung verwirft auch Positionen nach Undo zum gleichen Text.

Bei sehr schmalem Diagnostics-Tool kann der zusätzliche Text `Showing: …` abgeschnitten werden; die drei Filterbuttons bleiben im geprüften Standardlayout bedienbar. Dies verhindert weder Filterung noch Meldungsselektion und wird als begrenztes Layoutthema offengehalten. Keine neue Architektur- oder Admission-Ausnahme wurde eingeführt.

**CONDITIONAL GO**: Die geforderte Windows-Debug-/Release-Matrix ist erfüllt, die bestehenden MCP-/Coordinator-Garantien bleiben erhalten. G1-07 kann nach Eigentümerabnahme im bestehenden Plattformumfang beginnen; eine plattformübergreifende Produktionsfreigabe wird aus diesen Windows-Nachweisen nicht abgeleitet.

Die Arbeit endet nach G1-06. G1-07 wird erst nach ausdrücklicher Eigentümerabnahme begonnen.
