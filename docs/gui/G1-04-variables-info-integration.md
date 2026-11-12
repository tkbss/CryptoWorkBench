# G1-04 – Variables and Info Dock Integration

Stand: 6. November 2026. **CONDITIONAL GO für G1-05 nach Eigentümerabnahme.**

Variables und Info sind produktiv in die opt-in Dock-Shell integriert. Beide globalen Views behalten ihre vorhandenen ViewModels über Float/Redock und Close/Show. Variables ist schreibgeschützt und selektierbar; Info verwendet den bestehenden Markdown-Renderer einschließlich Mausauswahl, Ctrl+C, Copy Example, internen Links und History. Debug und Release bestehen vollständig einschließlich erneuter G1-03-Baseline und externer Windows-Eingaben.

## 1. Ausgangsstand und Arbeitsgrenze

Repository `tkbss/CryptoWorkBench`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`, ausschließlich Worktree `artifacts/worktrees/gui-g1`. G1-03 ist gemäß Auftrag als Grundlage akzeptiert. Zu Beginn waren 21 getrackte Dateien modifiziert und 35 Dateien ungetrackt. Alle 56 wurden vor Änderungen nach `artifacts/g1-04/baseline/` kopiert und in `baseline.json` mit SHA-256 erfasst. `initial-status.txt` und `lifecycle-design.md` dokumentieren Ausgangsstatus und Entwurf vor Produktänderungen.

G1-01-/G1-02-/G1-03-Berichte und Evidence-JSONs bleiben bytegleich, ebenso Prototyp, Paket-Locks und bestehende permanente Tests. Der bestehende native Runner wird erweitert. Andere Worktrees werden nicht bearbeitet. .NET 10, SDK 10.0.401, Runtime 10.0.12, Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock 12.1.0.13, Markdown.Avalonia 12.0.2 und Prism-Quellstand `358118cd640d9a22ff8cf21c8ad197fa038b7990` bleiben unverändert.

## 2. Globale Views und Lebensdauer

`DockShellPresentation` erhält dieselben Prism-Singletons `VariableViewModel` und `InfoViewModel`, die bereits der Editor verwendet. Sie besitzt genau einen `DockVariablesView` und einen bestehenden `InfoView`. Die IDs `tool:variables` und `tool:info` bleiben stabil. Neue Dock-Presenter lösen den bisherigen Content-Parent, bevor sie dieselbe View übernehmen. Dokumentwechsel sind dafür keine Voraussetzung; eine zweite Interpreter-, Workspace-, Coordinator- oder Editorinstanz entsteht nicht. Dynamische Prism-Regionen und zusätzliche Services sind nicht erforderlich.

Die Legacy-Variables-View bindet ihren Lebenszyklus an visuelles Attach/Detach. Das retiert Commands und verhindert spätere Snapshot-Publikation. Für ein globales Dock-Tool wäre dieses Verhalten beim Reparenting falsch. `DockVariablesView.Start()` bindet daher einmal nach `DockEditorSession.Start()` an das vorhandene ViewModel und abonniert einmal `WorkspaceChanged → InvalidateDraft`. Close/Show, Float/Redock und Tabwechsel führen weder AttachView noch DetachView erneut aus. GUI-Ergebnisse erreichen das ViewModel auch bei geschlossenem Tool. Beim endgültigen Dispose endet die Subscription und `DetachView` retiert die Commands. Vor produktivem Start entsorgt ein struktureller Fallback nur die noch ungebundene Darstellung.

`InfoViewModel` behält seinen vorhandenen Eigentümer: `App` entsorgt es nach dem produktiven Shutdown-Drain. Ein geschlossenes Info-Tool entsorgt es nicht. Der bestehende InfoView-Detach invalidiert lediglich den alten Link-Kontext; beim erneuten Attach wird ein aktueller Handler verwendet. Die History-Subscription bleibt genau einmal bestehen und endet erst beim App-Shutdown. Der vorhandene Editor-/MCP-Sessionvertrag bleibt unverändert.

## 3. Schreibgeschützte Variables-Darstellung

Die Liste zeigt Type, Identifier und Value aus den bestehenden `VariableModel`-Snapshots. Die Detailansicht zeigt Identifier, Type, Value, ValueFormat, Mechanism, Algorithm, Material, KeySize, Derivation und GMAC sowie `ParameterModel.Parameter` und `Value`. Sichtbarkeiten für Parameter und Schlüssel verwenden dieselben Modellflags wie Legacy. Die Darstellung liest weder die rohe VariableDictionary noch zusätzliche Schlüsselbytes; bestehende Capture-/Offenlegungsregeln bleiben maßgeblich.

Beide DataGrids und sämtliche Spalten sind read-only. Textwerte werden mit `SelectableTextBlock` und expliziten OneWay-Bindings dargestellt. Es gibt keine Modify-Buttons, editierbaren TextBoxen oder editierbaren Parameterzellen. Lediglich `SelectedItem` ist TwoWay, um die angezeigte Zeile auszuwählen; dies verändert keinen Variablenwert. Die internen Mutationscommands und die Legacy-Ansicht bleiben unverändert.

Identifier, Werte und Details unterstützen normale Textauswahl und Ctrl+C. Parameterzellen verwenden ebenfalls selektierbare Texte. Copy löst keine fachliche Mutation aus. Keine Produktlogik schreibt beim Anzeigen, Ausführen oder Docken automatisch in die Zwischenablage. Die Test-Fixtures setzen ausschließlich künstliche Sentinel-Werte, um diese Eigenschaft zu prüfen.

## 4. Refresh und MCP

Die neue Ansicht verwendet unverändert `RefreshCommand`, `SetupVariables`, Snapshot-Erfassung und die bestehenden Admission-Prüfungen. `WorkspaceChanged` kennzeichnet die dargestellten Werte als stale. Der Hinweis und „Refresh stale values“ bleiben sichtbar, bis eine zulässige Aktualisierung gelingt. Layoutaktionen und Show führen selbst keine Ausführung oder automatische Wiederholung eines Refresh aus.

GUI-Ausführung publiziert weiter über `CryptoScriptEditViewModel.Present`: Variables und Info kommen auch bei geschlossenen Tools an; Diagnostics bleiben im bestehenden Statusmodell. Fehlerfälle werden mit bereits ausgeführten Statements und anschließendem semantischem Fehler geprüft.

**MCP V2 behält seine vorhandene Präsentationsgrenze.** `ExecuteEditorV2Async` publiziert Outputs in das MCP-Ergebnis und führt nicht den GUI-Present-Pfad aus. Die GUI-Snapshotansicht wird invalidiert; während der Lease bleibt Refresh gesperrt. Nach Release übernimmt der vorhandene explizite Refresh den tatsächlichen gemeinsamen Interpreterzustand. Die bisherige Info-Ansicht wird durch V2-Output nicht überschrieben. G1-04 führt keine neue Output-Pipeline oder Info-History für MCP-Ausgaben ein. Die neuen Tests prüfen diesen Unterschied ausdrücklich, statt eine automatische Veröffentlichung anzunehmen.

Observer-, Host-Busy-, Pending- und MCP-Lease-Zustände bleiben den bestehenden Gates unterworfen. Die G1-03-DockCapabilityPolicy und Factory-Grenzen werden nicht geändert. Keine zusätzliche Ausführungsautorität und keine zweite Registrierung werden eingeführt.

## 5. InfoDocs, Auswahl und Copy Example

Der bestehende `InfoView`, sein Markdown-Engine-Objekt, internes Linkrouting, Back-Command, History und die spezielle Mechanism-Titelformatierung bleiben unverändert. Dokumente wie HASH-SHA256, HMAC-SHA256, Padding-Übersicht und AES-CBC werden direkt über den vorhandenen FileInfoDocumentationProvider geprüft. Kein Markdown-Inhalt wird geändert.

Die einzige Renderer-Anpassung im Produkt ist `MarkdownScrollViewer.SelectionEnabled = true` für die Dock-Info-View. Diese vorhandene Eigenschaft aktiviert Mausauswahl, Fokus und Ctrl+C. Die Legacy-Konfiguration bleibt unverändert. Das wurde am gepinnten [MarkdownScrollViewer-Quelltext](https://github.com/whistyun/Markdown.Avalonia/blob/f3f65f3f82b44f569958f0116c188e7a8e60686f/Markdown.Avalonia.Tight/MarkdownScrollViewer.cs) geprüft und mit dem tatsächlich geladenen Renderer getestet.

Der bestehende [CodeBlockElement-Copy-Handler](https://github.com/whistyun/Markdown.Avalonia/blob/f3f65f3f82b44f569958f0116c188e7a8e60686f/Markdown.Avalonia.SyntaxHigh/CodeBlockElement.cs) kopiert `TextEditor.Text` des gesamten read-only Codeblocks in die Zwischenablage. Er verändert den Workbench-Editor nicht und benötigt keine Editor-Admission. Der Copy-Button erscheint im vorhandenen CodePad-Menü beim Überfahren des Blocks. Auswahl plus Ctrl+C kopiert dagegen nur den markierten Ausschnitt. Prosa-Auswahl verwendet die Renderer-Selektion; Code-Auswahl verwendet dessen read-only AvaloniaEdit.

Tests verwenden mehrzeilige `text`-/`cryptoscript`-Blöcke mit HEX, BASE64, Unicode `ä€😀`, Leerzeilen und einer eigenen abschließenden LF. Der Zeilenumbruch unmittelbar vor der schließenden Markdown-Fence ist Syntax und gehört gemäß bestehendem Parser nicht zum Code. Soll der Beispieltext selbst eine abschließende LF enthalten, steht eine zusätzliche Leerzeile vor der Fence. Die resultierende Codezeichenfolge wird exakt geprüft; es gibt keine nachträgliche Normalisierung des Copy-Payloads. Bei Prosa kann der Renderer einen Absatzabschluss anfügen; der Test dokumentiert den tatsächlichen Clipboard-Inhalt und ignoriert nur diesen Abschluss beim Vergleich der ausgewählten Prosa.

## 6. Permanente und native Prüfungen

`DockToolTests` ergänzt 20 permanente Fälle: Prism-Singletons; sechs Float/Redock/Close/Show-Zyklen je Tool; unveränderte ViewModels, Commands, Auswahl, Registrierung, TextDocument, UndoStack, WorkspaceGeneration und StateRevision; Publikation bei geschlossenen Tools einschließlich Diagnostics; MCP-V2-Stale/Lease/Refresh; read-only Werte und Parameter; Copy ohne Mutation; vier vorhandene InfoDocs; interne Links, Back und inaktive alte Callbacks; vollständige Codekopie gegenüber ausgewähltem Text; Renderer-Mausauswahl mit Ctrl+C angedockt und floating; finaler Shutdown mit zwei Tool-Hosts und entfernten Subscriptions.

Der bestehende G1ShellProbe erhält `tools` und `tools-input`. `tools` verwendet native Windows, produktive Prism-Registrierungen und programmgesteuerte UI-Aktionen. `tools-input` wartet an benannten Checkpoints auf externe Windows-Maus-/Tastatureingabe. Vorbereitende Skripte, View-Auswahl und Float/Redock erfolgen durch die Probe. Externe Eingaben werden über `computer-use` / `@oai/sky` ausgeführt. Das ist keine vollständige Cross-Window-Maus-Redock-Matrix.

Die neuen nativen Checks erfassen Owner-HWND, View-/VM-Identität, read-only Controls, Parameterkopie, Clipboard-Payloads, Link/Back, Fokuswechsel, unveränderten Editor-/Interpreterzustand, MCP-Servicepfad und finalen Shutdown. Die unveränderten G1-03-Modi prüfen zusätzlich echten Desktop-MCP-Transport über die produktive STDIO-Bridge und TLS. Die neuen Tool-MCP-Checks sind ausdrücklich Service-API-Tests, kein zusätzlicher Transportclient.

## 7. Debug-/Release-Matrix

Alle Angaben gelten **je Konfiguration**; Debug und Release wurden separat ausgeführt.

| Suite / Prüfgruppe | Debug Passed | Release Passed | Failed je Konfiguration | Skipped / Explicit nicht ausgeführt |
|---|---:|---:|---:|---|
| GUI vollständig | 850 | 850 | 0 | 0 / 0 |
| davon neue G1-04-Tests | 20 | 20 | 0 | 0 / 0 |
| erhaltene GUI-Baseline | 830 | 830 | 0 | 0 / 0 |
| MCP vollständig | 176 | 176 | 0 | 0 / 13 |
| Core vollständig | 11.508 | 11.508 | 0 | 26 / 1 |
| G1-01 Headless | 12 | 12 | 0 | 0 / 0 |
| G1-01 Windows nativ | 60 | 60 | 0 | 0 / 0 |
| G1-02 native Dock-Fixture / Legacy / Fallback | 67 / 10 / 13 | 67 / 10 / 13 | 0 | 0 / 0 |
| G1-03 nativer Editor + echter MCP-Transport | 54 | 54 | 0 | 0 / 0 |
| G1-03 produktiver Startup-Fehler | 5 | 5 | 0 | 0 / 0 |
| G1-03 externe Tastatureingabe + Transport | 54 | 54 | 0 | 0 / 0 |
| neue G1-04 native Tool-Prüfungen, programmgesteuert | 32 | 32 | 0 | 0 / 0 |
| neue G1-04 Tool-Prüfungen mit externen Eingabepunkten | 34 | 34 | 0 | 0 / 0 |
| **Native Summe: 263 Baseline + 66 G1-04** | **329** | **329** | **0** | **0 / 0** |

Die beiden vollständigen Lösungen wurden in beiden Konfigurationen gebaut. Die 26 Core-Skips und der Explicit-Hardwaretest entsprechen der Baseline; 13 MCP-Explicit-Codex-Fälle sind nicht ausgeführt. TRX führt Skips und Explicit gemeinsam als NotExecuted; die Auswertung trennt sie anhand der NUnit-Zusammenfassung. Kein Explicit-Fall wird als Passed gezählt.

Externe G1-04-Eingaben: Identifier per Maus-Doppelklick und Ctrl+C; vollständiger Floating-Variablenwert per Mauszug und Ctrl+C; Unicode-Prosa per Mauszug und Ctrl+C in beiden Hosts; Klick auf den sichtbaren Copy-Button in beiden Hosts; tatsächlicher AES-CBC-Link, Back-Button und Klick zurück in den Editor. Die Probe bestätigt den jeweiligen Clipboard-Inhalt sowie den tatsächlich ausgelösten Copy-Button. Parameterkopie und die vorbereitenden Layoutwechsel sind programmgesteuert. Prosa lieferte in beiden Konfigurationen `Native selection ä€😀\n`; Codeblöcke einschließlich innerer Leerzeile und abschließender LF wurden exakt kopiert.

Die externe G1-03-Baseline wurde mit Unicode, Ctrl+Z/Ctrl+Y, Return, KP_Enter, Alt+PageUp und der einzelnen `(`-Taste erneut durchlaufen. Enter-Ausführung, History, Completion sowie nachfolgende Dialog-, TLS/STDIO- und Shutdown-Prüfungen bleiben grün.

Maßgebliche Rohdaten: `artifacts/g1-04/verified/` für vollständige Builds/Suiten und native Debug-Prüfungen, `native-release-final/` für die abschließenden nativen Release-Prüfungen, `foundation/` für G1-01 sowie `native-input-{Debug,Release}.json` und `native-tools-input-{Debug,Release}.json` für externe Eingaben. SHA-256, Einzeltests, Prüfpunkte, Eingabeart und vollständiger Status stehen in [G1-04-results.json](evidence/G1-04-results.json). Frühere Evidenz wurde nicht überschrieben.

Reproduktion:

```powershell
./eng/gui/Validate-G1Shell.ps1 -Configuration Both -Native -EditorIntegration -ToolIntegration `
  -EvidenceDirectory artifacts/g1-04/review
./prototypes/G1DockFeasibility/Validate.ps1 -Configuration Both -Native `
  -EvidenceDirectory artifacts/g1-04/review-foundation
# Danach jeweils mit neuen Dateien, separat für Debug und Release:
./eng/gui/native/bin/Debug/net10.0/G1ShellProbe.exe editor-input artifacts/g1-04/review-editor-Debug.json
./eng/gui/native/bin/Debug/net10.0/G1ShellProbe.exe tools-input artifacts/g1-04/review-tools-Debug.json
```

Die Input-Modi benötigen tatsächliche Eingabe am aktiven Testfenster. Sie sind keine unbeaufsichtigten CI-Modi.

## 8. Befunde während der Entwicklung

Die ersten neuen Headless-Ergebnisse sind **kein Abschlussnachweis**: Der zunächst verwendete Dispatch-Helfer ließ ein verschachteltes Task unbeobachtet. Er wurde an das bestehende G1-03-Muster mit generischem asynchronem Rückgabewert angepasst. Dadurch werden Assertions und Dispose zuverlässig abgewartet. Die neue Fixture wurde anschließend vollständig erneut geprüft. Bestehende Tests wurden nicht geändert.

Der native Lauf zeigte ungültige Testbeispiele `Info(HASH)` und `Info(HMAC)`: Diese Bezeichner sind keine konkreten Mechanismen und werden als unbekannte Variablen zurückgewiesen. Die neuen Beispiele verwenden HASH-SHA256 und HMAC-SHA256; Padding wird über die vorhandene `paddings`-Übersicht geprüft. Weitere Fixture-Korrekturen betreffen NameScope-freie codebasierte Controls, noch nicht erzeugte leere Dock-Windowlisten, die Hover-Lebensdauer des Copy-Buttons und die oben dokumentierte unveränderte MCP-V2-Präsentationsgrenze. Zwischenläufe bleiben erhalten; keine Produktfunktion wurde für grüne Tests abgeschwächt.

Der erste native Release-Dock-Lauf bestand 54 von 67 Checks; ausschließlich Vordergrund-/Fokusprüfungen scheiterten bei einem Hintergrundstart. Die Wiederholung mit aktivierter Probe besteht unverändert mit 67/67. Auch die folgenden nativen Release-Modi bestehen. Bei externer Eingabe wurde nach gleichzeitiger Benutzerbedienung das Fenster neu erfasst; die letzten Release-Prüfungen wurden nach ausdrücklicher Freigabe einer kurzen ungestörten Mausbedienung abgeschlossen. Nicht vollständige Textmarkierungen wurden nicht als bestandene Copy-Prüfung gezählt. Es gab keine OS-Einstellungsänderung und keine Abschwächung von Fokus- oder Clipboard-Erwartungen.

## 9. Dateien und Abschlussstatus

G1-04 ändert gegenüber dem gesicherten G1-03-Stand ausschließlich:

- `CryptoWorkBenchAvalonia/App.axaml.cs`: bestehende Tool-Singletons an die Presentation übergeben, Tools nach Editorstart binden.
- `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs`: dieselben globalen Views in neuen Presentern hosten, Renderer-Selektion einschalten, finale Ressourcenfreigabe.
- Neu `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs`: schreibgeschützte selektierbare Snapshot-Darstellung mit globalem Lebenszyklus.
- Neu `CryptoWorkBenchGuiTest/DockToolTests.cs` und `DockToolNativeProbe.cs`: permanente und native Tool-Prüfungen.
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`, `eng/gui/native/Program.cs`, `eng/gui/Validate-G1Shell.ps1`: neue Modi im bestehenden Runner.
- Dieser Bericht und `docs/gui/evidence/G1-04-results.json`.

Keine Änderungen an Parser, AST, Evaluator, InterpreterHostQueue, EditorCoordinator, EditorBridge, MCP-Verträgen, bestehenden VMs, Legacy-Views, InfoDocs, G1-03-Docking-Gates oder Paketen. Output/Diagnostics/Status bleiben Platzhalter. Keine Mehrdokumente, Persistenz oder G1-05-Arbeit.

Abschlussstatus einschließlich der erhaltenen Vorarbeiten: **21 modifizierte getrackte und 40 ungetrackte Dateien**, kein Staging, unveränderter G1-HEAD. Gegenüber der G1-03-Sicherung werden fünf vorhandene Dateien gezielt weiterentwickelt; die anderen 51 initialen Dateien bleiben bytegleich. Fünf Dateien kommen hinzu. `git diff --check` besteht; alle 40 ungetrackten Dateien werden zusätzlich auf Inhalt, Whitespace und JSON-Parsebarkeit geprüft. Die drei übernommenen Paket-Locks ohne abschließende LF bleiben unverändert. Der vollständige Status und Einzelhashes stehen in der Ergebnisevidenz. Kein Commit, Push oder Merge; keine verbleibenden Testfenster oder Probeprozesse.

## 10. Offene Grenzen und Übergabe

Die übernommenen Grenzen bleiben bestehen: Linux/macOS nativ, gemischte DPI und Multi-Monitor sind ungeprüft; ebenso vollständiges Cross-Window-Maus-Redock und die Adorner-/Drop-Zielmatrix. Bekannte optionale Dock-Overlay-Casts und Null-Bindings beim Teardown sind keine durch G1-04 behobene Overlay-Architektur. Windows-Foreground-Aktivierung kann verweigert werden. Zyklen, Handler, Hostregistrierung und Shutdown sind prüfbar, ersetzen aber keinen stundenlangen Ressourcenstresstest. Die 13 Installed-Codex-Explicit-Fälle und native File-Picker-Bedienung bleiben gemäß G1-03 offen.

**CONDITIONAL GO für G1-05**, vorbehaltlich Eigentümerabnahme und unter Beibehaltung dieser Plattform-/Interaktionsgrenzen. Dies ist keine allgemeine plattformübergreifende Veröffentlichungsfreigabe. Die Arbeit endet mit G1-04; G1-05 wurde nicht begonnen.
