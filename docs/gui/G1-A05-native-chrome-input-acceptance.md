# G1-A05 – Native Chrome, Mouse Redock and Input Acceptance

Stand: 10.11.2026, Windows x64. Worktree `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. R4 ist durch den Auftrag abgenommen; A05 ist ausdrücklich freigegeben.

## 1. Ergebnis und Abnahmegrenze

**B7 ist korrigiert. Die automatische Debug-/Release-Regression und die externe Windows-Eingabeprüfung sind bestanden. Die vollständige Maus-Redock-Abnahme bleibt offen; A05 erhält deshalb keine uneingeschränkte Abnahme.**

Die korrigierte Chrome-Erkennung verwendet die tatsächliche Dokument-Pane dieser Session. Single-Tool und Tool-Gruppe erhalten Tool-Chrome; Dokument- und gemischte Hosts erhalten Dokument-Chrome. Ein bestehender Tool-Host wird auch beim späteren Eintritt und Austritt des Dokuments neu klassifiziert. Es entsteht keine zweite Editorinstanz.

Die historischen F03-Completion-Fehler reproduzierten sich in den aktuellen, extern per Windows-Tastatur bedienten Prüfschritten nicht. Completion und Insight funktionieren im Hauptfenster, im Floating-Host und nach Redock; veraltete Popups werden geschlossen und verspätete Completion-Insertion bleibt wirkungslos.

**Empfehlung:** technische A05-Korrektur zur Eigentümerprüfung bereit. Die unten aufgeführten fensterübergreifenden Mausgesten sind vor einer vollständigen A05-Abnahme am Desktop zu bestätigen. G1-A06 wurde nicht begonnen und benötigt weiterhin ausdrückliche Eigentümerabnahme. Linux/macOS wurden hier nicht abgenommen.

Maschinenlesbare Zusammenfassung: [G1-A05-results.json](evidence/G1-A05-results.json). Sämtliche neuen Rohdaten liegen ausschließlich unter [artifacts/g1-a05](../../artifacts/g1-a05).

## 2. Ausgangsstand und Integrität

Vor der ersten Produktänderung wurden alle 1.143 durch Git erfassten bzw. nicht ignorierten Arbeitsdateien einschließlich der uncommitteten G1-Arbeiten kopiert und mit SHA-256 inventarisiert. Dazu gehören Git-Status, HEAD und der vorhandene tracked Patch. Weitere 14.088 historische Artefaktdateien mit insgesamt 2.438.298.704 Bytes wurden separat inventarisiert; ihre vorhandenen historischen Sicherungen und Originale wurden erhalten. Die historische Artefaktmenge wurde gehasht, nicht nochmals vollständig dupliziert.

Sicherung: [baseline](../../artifacts/g1-a05/baseline). Abschlussvergleich: [integrity.json](../../artifacts/g1-a05/integrity.json). Von den 1.143 Ausgangsdateien bleiben 1.140 bytegleich; drei ändern sich gezielt, keine wird entfernt. Alle 14.088 historischen Artefakte und alle 253 besonders geschützten Bestandsdateien bleiben bytegleich. Letztere umfassen frühere Dokumentation/Evidence, Paket-Locks, die gesperrten Koordinationsklassen und bestehende A04-Prüfungen. Fünf neue Quell-/Berichtsdateien ergänzen den Stand.

Maßgebliche Grundlagen bleiben [A01](G1-A01-docking-architecture-review.md), [A02](G1-A02-maximize-layout-roles.md), [A03](G1-A03-persistence-cleanup-roles.md), R1/R2/R3/R4 sowie die F01/F2/F03-Untersuchungen. Die Dateinamen und vollständige Liste der tatsächlich vorhandenen Berichte sind im Baseline-Manifest enthalten. R4-Bericht: [Targeted Partial Failure Handling](G1-A04-R4-partial-failure-correction.md).

.NET 10, Avalonia 12.1.4, Dock.Avalonia 12.1.0.13 und AvaloniaEdit 12.0.0 bleiben unverändert. Dock-Quellcommit: `78da173545501e796f37bc8528e9e55d9cbfd991`. Die Build-Vorbereitung prüft weiterhin den offiziellen Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` mit 1.180 gepinnten Dateien. Kein Paketupgrade, kein Fork, keine Änderung an EditorCoordinator, EditorBridge, WorkbenchAutomationService oder InterpreterHostQueue.

## 3. B7 und produktive Änderung

Der Befund bestand vor A05 tatsächlich: `DocumentSafeHostWindow.OnOpened` verwendete `VisibleTree(...).Any(d => d is IDocument)`. Die eingesetzten Tool-Typen erfüllen ebenfalls `IDocument`; der Test unterschied deshalb echte CryptoScript-Dokumente nicht zuverlässig von Tools.

Die einzige Produktänderung liegt in [DockShellLayout.cs](../../CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs): Der sichtbare lokale Floating-Baum muss jetzt **die konkrete `owner.Pane(DockShellState.DocumentId)`-Instanz enthalten**. Es wird weder ein Container-ID-Präfix ausgewertet noch bloß auf `IDocument` geprüft. `includeFloating: false` verhindert, dass ein anderes Floating-Fenster die Chrome dieses Hosts bestimmt.

| Hostinhalt | Erwartete Chrome und Lebensdauer |
|---|---|
| Einzelnes Tool | Dock-Tool-Chrome; Benutzer-Close schließt den Host. |
| Tool-Gruppe | Dock-Tool-Chrome; Gruppeninhalt bleibt im normalen Dock-Lifecycle. |
| CryptoScript-Dokument | Vorhandene Dokument-Chrome mit explizitem Dock-Grip; Dokument-Close bleibt geschützt. |
| Gemischter Host | Dokument-Chrome, solange die tatsächliche Dokument-Pane enthalten ist. |
| Tool-Host erhält/verliert Dokument | Derselbe Tool-HWND bleibt erhalten; Chrome wird angepasst, nach Dokumentaustritt wieder schließbar. |

Ein idempotenter `LayoutUpdated`-Handler aktualisiert ausschließlich die Darstellung beim Wechsel der Hostzusammensetzung. Beim Austritt des Dokuments werden die lokalen Werte von ContentTemplate und WindowDecorations entfernt, sodass wieder der bestehende Dock-Style gilt. `OnClosed` entfernt den Handler. Nicht-Windows, fehlende Session, Dispose und LayoutFaulted werden früh ausgeschlossen. Die bestehende `OnClosing`-Logik wurde nicht geändert.

Die fünf neuen permanenten Tests in [DockNativeChromeTests.cs](../../CryptoWorkBenchGuiTest/DockNativeChromeTests.cs) prüfen alle vier Hostarten sowie den Eintritt/Austritt des Dokuments in einem bestehenden Tool-Host einschließlich Editor-/Document-/UndoStack-Identität und Close-Verhalten. Die native A05-Probe ergänzt echte HWND-Owner, Sichtbarkeit, Aktivierung, Positionsänderung, Close und Reset.

### R1-/R4-Nachprüfung

Der neue Lifecycle-Handler liest den Modellbaum und setzt Chrome-Eigenschaften. Er ruft weder Float, Dock, MoveDockable, Show, Save noch eine Factory-Layoutoperation auf und vergibt keine Mutationserlaubnis. Die Änderung des Templates kann bestehende visuelle Presenter-Lifecycle-Schritte auslösen; `DockEditorSession.CreatePresenter` reicht dabei dieselbe View weiter und löst den alten Presenter. Der vorhandene Observer-/Admission-Vertrag bleibt bestehen. Der Handler fügt keinen unabhängigen B4-Mutationspfad hinzu.

Der bereits vorhandene Dokument-Grip prüft weiterhin `owner.CanChange`, bevor er die öffentliche `HostWindow.TryBeginMoveDrag(..., WindowDragDockScope.FullWindow)`-Integration aufruft. Er wurde in A05 nicht neu eingeführt. Keine Transaktionsarchitektur, keine künstlichen Callback-Tests, keine Dock-Modifikation und keine Änderung am R4-Partial-Failure-Schutz. Die sechs in R2 entfernten Tests bleiben entfernt. Bestehende A04-Testdateien bleiben bytegleich; Vollregression und unabhängiger kritischer Lauf bleiben grün.

## 4. Redock und die vier Evidenzarten

| Art | In A05 verwendeter Nachweis | Aussagegrenze |
|---|---|---|
| 1. Direkte Factory-/DockManager-Aufrufe | Unveränderte Redock-, Layoutrollen-, Persistenz- und A04-Probes; neue Chrome-Tests | Modell-/Cleanup-Vertrag, keine bestandene Mausgeste. |
| 2. Programmgesteuerte native Hosts | Echte Windows-Fenster; Activate, Position, Close, Float, Redock, Maximize/Restore, Restart; HWND-Census | Reale native Ressourcen, aber programmgesteuerte Layoutaktion. |
| 3. Simulierte Avalonia-Eingabe | `PerformTextInput`, ApplicationCommands und bestehende Tests | Routed Editor-/Command-Vertrag, kein externer Windows-Key. |
| 4. Externe Windows-Maus/-Tastatur | Computer Use/`@oai/sky`: tatsächliche Auswahl-Drags, Klicks, Ctrl+C/V/Z/Y, Return, KP_Enter, `(` | Die beschriebenen Eingabeschritte wurden extern bedient. Vorbereitende Daten und Float/Redock der Probes bleiben programmgesteuert. |

Die native `redock`-Probe prüft Single, Gruppe, verschachtelte Struktur, Dokumenttab und DocumentDock mit Fill/Right/Bottom, eine zweite belegte Floating-Instanz, zerstörte Quell-HWNDs, entfernte Registrierungen und identischen Editor. A02 ergänzt Maximize/Restore und Hauptanker, A03 Save/Restart einschließlich aktueller, Legacy- und echter historischer Layouts. Diese Nachweise werden **nicht** als externe Maus-Redock-Abnahme verbucht.

Zusätzliche externe Mausbeobachtung im eigenen Debug-Prozess PID 34744: Variables wurde per Tab von links nach rechts und zurück gezogen. Der Tool-Grip führte die linke Dreiergruppe mit Diagnostics/Status zusammen; der Grip bewegte anschließend alle fünf Tools gemeinsam. Ein Drag vom Gruppengriff auf den Statusbereich außerhalb der Dockfläche erzeugte den sichtbaren Tool-Host HWND `6752788` mit exakt den fünf Tool-IDs. Danach erzeugte ein Dokumenttab-Drag den separaten Dokument-HWND `8128702`; der Tool-HWND blieb bestehen. Haupt-HWND: `7145826`. Der angeklickte Produktbefehl **Reset to Default Layout** stellte die Ausgangsansicht wieder her; beide Floating-Hosts waren anschließend geschlossen, ohne HWND und ohne Factory-Registrierung. Das ist erfolgreiche externe Float-/Tab-/Grip-Evidenz und ein Menü-Reset, **kein Drag-Redock zwischen Fenstern**. [Journal](../../artifacts/g1-a05/mouse-observation-Debug.json.windows.jsonl) und [Aktionsprotokoll](../../artifacts/g1-a05/external-input-actions.json) erhalten diese Unterscheidung. Das unveränderte alte `redock-manual`-Ergebnis enthält den statischen Text „manual human mouse only“; tatsächlich bediente hier externe Windows-Automation den Desktop, kein bestätigender menschlicher Prüfer.

### Tab, Dock-Grip und Betriebssystem-Caption

* Ein Dock-Tab startet den Dock-Tab-Drag für die ausgewählte Pane.
* Der vorgesehene Dock-Grip startet die öffentliche Dock-Gruppen-/Fenster-Drag-Pipeline. Die Dokument-Chrome verwendet ausdrücklich `TryBeginMoveDrag` mit `FullWindow` und erhält die vorhandenen Dock-Overlays.
* Eine echte Betriebssystem-Titelleiste führt zunächst einen Betriebssystem-Fenstermove aus. Das ist kein Nachweis eines Dock-Drops. Es wurde kein Win32-Hook eingeführt, der Caption-Moves in Dock-Operationen umdeutet.

Die verfügbare externe Drag-API ist an ein ausgewähltes Fenster mit Start-/Endkoordinaten gebunden. Sie stellt keine getrennten Mouse-down/Move/Mouse-up-Schritte mit einer gehaltenen Taste über zwei ausgewählte native Fenster und eine beobachtbare Zielwahl bereit. Ein vollständig bestätigter fensterübergreifender Tab-/Grip-Redock wurde damit nicht nachgewiesen. Die Eigentümermatrix in Abschnitt 10 bleibt offen; ein programmgesteuerter Drop ersetzt sie nicht.

## 5. Completion, Insight und Eingabe

Der unveränderte F03-Pfad `editor-input` besteht mit **54/54** Prüfungen. Darin bestehen ausdrücklich `completion-in-floating-host` und `completion-retired-on-relocation`. Extern bedient wurden Unicode-Eingabe `ä€😀`, Undo/Redo, Return, KP_Enter, History-Taste und die öffnende Klammer. Nach Alt+PageUp wurde ein Menufokus beobachtet; vor der Klammer wurde der tatsächliche Editor erneut angeklickt. Auf der vorhandenen Tastaturbelegung erzeugte `Shift_L+8` die Klammer. Die historischen roten Ergebnisse bleiben erhalten; aus diesem erfolgreichen Lauf wird keine nachträgliche eindeutige Ursache der alten F03-Fehler abgeleitet.

Die neue [DockNativeAcceptanceProbe.cs](../../CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs) prüft in jeder Phase Hauptfenster/Floating/Redocked:

1. Unicode-Auswahl und exakten Clipboard-Inhalt nach Copy; Paste und Undo/Redo.
2. Completion für `Info(`, offen und mit PlacementTarget im richtigen nativen Editor-Host.
3. Hostwechsel, geschlossenes altes Popup und wirkungslose verspätete Insertion.
4. Insight für `Print(`, richtiger Host und Schließen beim nächsten Hostwechsel.
5. Sichtbarer HWND-Gesamtbestand nach Rückkehr, Editor-/TextDocument-/UndoStack-/EditorId-/WorkspaceGeneration-Identität.
6. Shutdown ohne sichtbares Prozessfenster, registrierte Hosts/DockControls oder registrierten Editor.

Die externe Wiederholung `native-acceptance-input-hidden-Debug.json` besteht mit **49/49** Prüfungen. Die Floating-Auswahl erfolgte durch einen echten Text-Drag; im Hauptfenster und nach Redock durch Ctrl+A. Copy/Paste/Undo/Redo und beide Popup-Auslöser erfolgten per externem Windows-Key. Testtext, Sentinel und Hostwechsel stammen aus der Probe. Die Probe aktiviert und fokussiert an benannten Checkpoints; zusätzliche explizite Editor-Klicks nach Reparenting sind im Interaktionsprotokoll ausgewiesen. Dies ist kein Nachweis, dass jede beliebige Hoständerung ohne erneuten Benutzerklick bereits Tippeingaben erhält.

UI Automation lieferte nach einzelnen Reparentings kein `focused_element`, obwohl Screenshot, reale Auswahl und exakter Clipboard-/Textvergleich die Eingabe bestätigten. Dieser UIA-Befund wurde nicht mit einem Produkt-Fokusverlust gleichgesetzt. Enter-Semantik, History und MCP wurden nicht geändert.

## 6. Output, Diagnostics, Variables und Info

| Externer Debug-Lauf | Ergebnis | Tatsächlich extern bediente Schritte |
|---|---:|---|
| `tools-input` | 34/34 | Variables-Identifier/Value auswählen und kopieren; Info-Prosa auswählen/kopieren; Copy Example floating und docked; Info-Link/Back; Editor wieder anklicken. |
| `output-input` | 29/29 | Return/KP_Enter, Undo/Redo; Output-Text docked/floating auswählen/kopieren; Quellnavigation; Clear Output. |
| `diagnostics-input` | 35/35 | Warnings/Errors/All; Diagnose-Text docked/floating kopieren; Quellnavigation zum docked und floating Editor; veraltete Navigation auslösen. |

Die vorhandenen Produktmechanismen erzeugen die Testdaten und kontrollieren die Ergebnisse. Read-only-Verhalten, unveränderte globale Variables-Instanz, Parameter-Copy, reine Print-Ausgabe, korrekte Zeile/Spalte und Clear ohne Editor-Textmutation sind Assertions dieser Probes. Das ist keine Behauptung, dass jede einzelne Assertion einen separaten manuellen Eingabeschritt besitzt. Bei Diagnostics verdeckte der Floating-Editor zunächst den Navigationsbutton; Maximieren des Hauptfensters legte ihn frei. Die anschließende Navigation und der korrekte Editor-Vordergrund bestanden.

Rohdateien und aktuelle Stage-Dateien: [g1-a05](../../artifacts/g1-a05). Die Stage-Datei zeigt jeweils nur den letzten Checkpoint; Ergebnis-JSON und begleitendes [external-input-actions.json](../../artifacts/g1-a05/external-input-actions.json) dokumentieren die gesamte abgeschlossene Folge.

## 7. Vordergrund, Reproduzierbarkeit und Fehlversuche

Es wurde kein Fokus-Assert abgeschwächt. Die Probes vergleichen weiterhin den tatsächlichen Vordergrund-HWND und den Editorfokus. Der freie Desktop wurde während der Arbeiten ausdrücklich vom Eigentümer bestätigt: „Desktop frei, keine parallele Bedienung“. Diese Zusicherung lag bei den frühen Entwicklungsversuchen noch nicht vor; ein systemseitig exklusiver Vordergrund-Lock wurde weder eingerichtet noch behauptet.

Ein früher A05-Entwicklungslauf scheiterte bereits vor dem ersten fachlichen Check mit `foreground/editor focus` und HWND `67158`; die Desktop-Inventur ordnete diesen HWND Chrome zu. Die Wiederholung nach gezielter Aktivierung des eigenen Probe-Fensters bestand mit 68/68. Das belegt einen konkreten Vordergrundkonflikt im Teststart, nicht die Abwesenheit aller denkbaren Produkt-Fokusfehler. Die zehn historischen A04-Timeouts werden deshalb nicht rückwirkend als aktuelle Produktfehler oder als vollständig ursächlich geklärt ausgegeben.

Windows beschränkt das programmgesteuerte Setzen des Vordergrundfensters unter anderem anhand des aktiven Prozesses und der letzten Eingabe; selbst bei erfüllten Voraussetzungen kann die Aktivierung abgelehnt werden. Siehe [Microsoft: SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow). Hier gibt es keinen Foreground-Hack, keine globale Tastatur-/Maus-Sperre und keinen Assertion-Retry innerhalb einer fehlgeschlagenen Probe.

Ein erster externer A05-Lauf bestand 45 Checks, scheiterte aber bei vier vollständigen Sichtbarkeits-Censuses. Das Journal zeigte ein sichtbares **PseudoConsoleWindow** des Teststarts; die Dock-Hosts waren geschlossen. Dieser Lauf bleibt rot erhalten. Derselbe Probe-Code wurde mit `Start-Process -WindowStyle Hidden` erneut gestartet: 49/49, einschließlich aller vier unveränderten Censuses. Die Korrektur betrifft den Teststart; es wurde keine Fensterklasse aus dem Assert herausgefiltert.

Weitere erhaltene Entwicklungsbefunde: erster sandboxierter .NET-Lauf hing und wurde nur für den eigenen Prozess beendet; die erforderlichen Builds/Tests liefen danach mit Sandbox-Freigabe. Der erste neue native Probe-Build hatte lokale Namenskollisionen und einen fehlenden Clipboard-Extension-Import; der korrigierte Build und alle Endläufe bestanden. NU1900-Warnungen zum nicht erreichbaren Vulnerability-Feed und vorhandene Compilerwarnungen werden in den Rohlogs bewahrt; Paket-Locks bleiben unverändert.

## 8. Vollständige automatische Regression und unabhängige Wiederholung

| Suite | R4 Passed je Konfiguration | Debug Passed | Release Passed | Nicht ausgeführt je Konfiguration |
|---|---:|---:|---:|---:|
| GUI | 1.103 | 1.108 | 1.108 | 0 |
| MCP | 176 | 176 | 176 | 13 Explicit |
| Core | 11.512 | 11.512 | 11.512 | 26 Skips + 1 Explicit |
| Headless | 12 | 12 | 12 | 0 |

Alle acht vollständigen Suites haben null Fehler. Die fünf zusätzlichen GUI-Fälle sind ausschließlich die neuen Chrome-Regressionen. Die Inventur vergleicht jeden TRX-Fall mit R4: **kein entfernter Fall, kein geändertes bisheriges Ergebnis**. Nur dynamische UTC-Anzeigenamen der bereits bekannten MCP-Contract-Testargumente werden für den Vergleich normalisiert; keine Testquelle wird geändert. Die 40 nicht ausgeführten Fälle sind keine bestandenen Tests. Namen und Gründe stehen vollständig in [test-inventory-difference.json](../../artifacts/g1-a05/test-inventory-difference.json).

Die unabhängigen neuen Testprozesse `independent-Debug.trx` und `independent-Release.trx` bestehen jeweils **16/16**: fünf Chrome-Fälle, HostChangeRetiresPopup, neun R4-Partial-Failure-Fälle und der erhaltene partielle Move-Fehlerfall. Zusätzlich startet der native A05-Treiber `native-acceptance` zweimal pro Konfiguration in getrennten Prozessen. Es handelt sich um neue Prozessläufe, nicht um einen zweiten unabhängigen Reviewer.

Reproduzierbarer neuer Treiber: [Validate-G1NativeAcceptance.ps1](../../eng/gui/Validate-G1NativeAcceptance.ps1). Beispiel aus dem G1-Worktree:

```powershell
./eng/gui/Validate-G1NativeAcceptance.ps1 -Configuration Both `
  -EvidenceDirectory artifacts/g1-a05-owner-repeat -IncludeHistorical
```

Das Zielverzeichnis muss neu und innerhalb dieses Worktrees liegen. Der Treiber verwendet die vorhandene Prism-Vorbereitung, konfigurationsspezifische Locked-Restores, native Builds, begrenzte Prozesse und rohe Exit-/Timeout-Daten. Er ergänzt den bestehenden historischen Treiber, statt dessen Assertions zu verändern. Externe Checkpoint-Modi müssen separat interaktiv bedient werden; sie sind kein unbeaufsichtigter CI-Lauf.

## 9. Native Prozesse, HWNDs und Registrierungen

Die finale Prozessmatrix und alle PIDs, Exitcodes, Checkzahlen sowie Fehlerdetails stehen in [native-results.json](../../artifacts/g1-a05/native-results.json). Historische Prozesse: `native/historical/commands.json`; neue Chrome-/Completion-/Tool-Prozesse: `native/commands.json`.

Die vorhandenen F01/F03-, A02-, A03- und A04-R4-Prüfungen bestehen je Konfiguration in 31 nativen Prozessen. Neue A05-Läufe ergänzen je Konfiguration zwei Chrome-/Completion-Prozesse mit jeweils 68/68 sowie Tools 32/32, Output 29/29 und Diagnostics 35/35. **Alle 72 finalen nativen Prozesse: Exit 0, kein Timeout, kein fehlgeschlagener Check.** Hinzu kommen die fünf erfolgreichen externen Eingabeläufe aus Abschnitt 5/6 und die separate Mausbeobachtung. Fehlgeschlagene Entwicklungsversuche werden getrennt erhalten und nicht aus der Evidenz gelöscht.

Die Product-, CryptoScript- und MCP-Assemblies in Desktop-, GUI-Test- und Native-Probe-Ausgabe sind je Debug/Release SHA-256-identisch: [assembly-parity.json](../../artifacts/g1-a05/assembly-parity.json). Die Endläufe testen damit dieselbe Produktimplementierung; für die interaktiv gestarteten Prozesse wurde der Exitcode nicht separat aufbewahrt, wohl aber das abgeschlossene Prüfergebnis und das Ende des Prozesses beobachtet. Die 72 durch den Treiber gestarteten Prozesse besitzen hingegen explizit gespeicherte Exitcodes und Timeout-Ergebnisse.

Die HWND-Prüfung unterscheidet Owner, IsWindow, IsWindowVisible, Root.Windows, Factory.HostWindows und DockControls. Leere Redock-Quellhosts müssen als HWND zerstört und aus den Registrierungen entfernt sein; ein zweiter belegter Host muss seinen HWND behalten. Popup- und A05-Journale enumerieren auch unsichtbare native Fenster und deren Klassen. Nach dem externen A05-Abschluss sind alle sechs während der Eingabephasen erzeugten Dock-Hosts geschlossen, ohne HWND; Root.Windows, HostWindows und DockControls sind leer. Sichtbare Prozessfenster: null.

Unsichtbare Avalonia-Hilfs-/Message-/Popup-Ressourcen, ConsoleWindowClass und IME-Fenster können bis zum Prozessende existieren. Sie werden im Journal nicht als verschwundene HWNDs ausgegeben. Entscheidend sind die geschlossenen Popups, die zerstörten Dock-Quell-HWNDs, keine verbleibenden Dock-Registrierungen/Taskleistenhosts und das reguläre Prozessende. Der Abschluss beansprucht nicht, dass vor Prozessende jedes interne Windows-Hilfsfenster bereits zerstört ist.

## 10. Offene Eigentümer-Mausmatrix

**Status aller nachstehenden vollständigen Mausabläufe: PENDING OWNER, keine Bestätigung erfunden.** Frischen Probe-Prozess im Modus `redock-manual` mit neuer JSON-Ausgabedatei starten; vorhandenes Journal zeichnet HWNDs und Dock-Lifecycle auf. Der historische Modus enthält die Kennzeichnung „NOT CERTIFIED“; diese ist kein automatischer Pass. Für Save/Restart zusätzlich die echte Shell bzw. den bestehenden Layout-Testweg mit eigener Testdatei verwenden. Pro Schritt Tab und vorgesehenen Grip getrennt protokollieren; die OS-Caption als separate reine Fensterbewegung prüfen.

1. **Variables:** Tab ausdocken und per Tab wieder auf ein sichtbares Dock-Ziel ziehen. PASS: genau eine Variables-Pane, Quell-HWND zerstört, keine leere Taskleisteninstanz.
2. **Fünf Tools:** Variables, Output, Info, Diagnostics, Status in einer Gruppe sammeln und über den Gruppengriff ausdocken. PASS: alle fünf genau einmal im selben Floating-Host, Inhalte bedienbar.
3. **Tool-Gruppe redocken:** Über den vorgesehenen Grip auf ein sichtbares Hauptfensterziel ziehen. PASS: fünf Tools erhalten, Quellmodell und HWND entfernt.
4. **Dokument ausdocken:** CryptoScript-Tab ziehen. PASS: ein Dokument/Editor mit unverändertem Text, Undo/Redo und MCP-Identität.
5. **Dokumenttab redocken:** Aus dem Floating-Host per Tab ins Hauptfenster. PASS: Dokument genau einmal, alter HWND weg; Copy und Completion erneut funktionsfähig.
6. **DocumentDock seitlich:** Dokument-Gruppengriff auf ein seitliches Ziel ziehen. PASS: genau ein Haupt-Dokumentanker, gültiges Capture, kein leerer zusätzlicher Host.
7. **Verschachtelte Gruppen:** Tool- und DocumentDock per Tab/Grip seitlich und unten kombinieren, wieder auflösen. PASS: gültiges Capture, alle sechs Panes genau einmal, keine leeren Fenster.
8. **Zwei Floating-Hosts:** Einen Host belegt stehen lassen, den anderen redocken. PASS: nur Quell-HWND verschwindet; der andere bleibt sichtbar, fokussierbar und inhaltlich unverändert.
9. **Maximize nach Redock:** Über vorhandene Layoutaktion maximieren. PASS: kein Absturz, Editor-/Toolinhalt unverändert, Fokus und Eingabe möglich.
10. **Restore:** Layout wiederherstellen. PASS: ursprüngliche Struktur und belegte Floating-Hosts sichtbar, keine Duplikate oder zusätzlichen Hauptanker.
11. **Save/Restart:** Layout mit Floating-Fenstern speichern, regulär beenden und neu starten. PASS: gespeicherte belegte Fenster tatsächlich sichtbar und aktivierbar; keine unsichtbaren Ersatzhosts.

Für jeden Schritt FAIL bei Exception, verschwundener belegter Pane, doppeltem Editor, falscher Dokument-Chrome, leerem/unsichtbarem Quellhost, defektem Fokus oder fehlendem Dock-Ziel. Einen OS-Caption-Move ohne Dock-Ziel nicht als Tab-/Grip-Fehler oder erfolgreichen Redock zählen. Nach jeder Dokumentbewegung `Info(` und `Print(` prüfen; während eines weiteren Hostwechsels müssen alte Popups geschlossen werden. Diese Matrix schließt die verbleibende tatsächliche Mausabnahme, nicht die bereits bestandenen automatischen Verträge.

## 11. Dateien, Git und Abschluss

Gegen die vor A05 gesicherte Arbeitskopie geändert:

* `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs`: tatsächliche Dokumentidentität und dynamische Chrome.
* `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`: Dispatch der zwei neuen A05-Modi.
* `eng/gui/native/Program.cs`: Zulassung dieser Modi.

Neu:

* `CryptoWorkBenchGuiTest/DockNativeChromeTests.cs`
* `CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs`
* `eng/gui/Validate-G1NativeAcceptance.ps1`
* `docs/gui/G1-A05-native-chrome-input-acceptance.md`
* `docs/gui/evidence/G1-A05-results.json`

Neue ignorierte Rohartefakte/Arbeitshelfer bleiben unter `artifacts/g1-a05`. [a05.patch](../../artifacts/g1-a05/a05.patch) isoliert die A05-Differenz vom umfangreichen bereits vorhandenen G1-Stand. [final-git-status.txt](../../artifacts/g1-a05/final-git-status.txt) enthält den vollständigen Status gegen HEAD, [final-checks.json](../../artifacts/g1-a05/final-checks.json) die Prüfresultate. `git diff --check` besteht. Für sämtliche neuen und zuvor ungetrackten geänderten Dateien wurde zusätzlich `git diff --no-index --check` ausgeführt; Exit 1 ohne Diagnose bedeutet dort vorhandene Inhaltsdifferenz, kein Whitespace-Fehler.

Kein Commit, Push, Merge oder automatische L1-Integration. Hauptcheckout und gepinnter Dock-Quellcheckout bleiben sauber. Keine Änderung der CryptoScript-Semantik, keine zusätzliche ScriptBook-/Multi-Document-Implementierung. Abschluss nach A05; A06 bleibt gesperrt bis zur ausdrücklichen Eigentümerabnahme.
