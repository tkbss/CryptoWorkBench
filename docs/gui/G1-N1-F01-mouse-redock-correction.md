# G1-N1-F01 – Mouse-Redock: leere native Hosts

Stand: 7. November 2026. **CONDITIONAL GO** für die gezielte Integrationskorrektur nach bestandener vollständiger Windows-Regression; die erneute manuelle Eigentümerprüfung steht aus. **M2 ist nicht PASS.** M1 bleibt der vom Eigentümer gemeldete PASS; M2 war FAIL und wartet auf Nachprüfung. M3–M8 bleiben offen. Legacy bleibt Standard; kein Commit, Push, Merge oder L1-Import.

Maschinenlesbare Ergebnisse, ausgeschlossene Tests, vollständiger Git-Status und SHA-256-Manifeste: [G1-N1-F01-results.json](evidence/G1-N1-F01-results.json). Rohbelege: `artifacts/g1-n1-f01`. Fehlgeschlagene Zwischenläufe bleiben erhalten.

## Umfang und Nachweisgrenze

Gearbeitet wurde ausschließlich in `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Die 1.103 anfänglich getrackten beziehungsweise nicht ignorierten Dateien wurden gehasht, die 103 bereits uncommitteten Dateien gesichert, Git-Status und binärer Diff abgelegt: `artifacts/g1-n1-f01/baseline`. Die 1.102 G1-08-Quelldateihashes stimmen mit dem Anfangsstand überein.

Die verfügbare Windows-Computer-Use-API `@oai/sky` bietet `drag` mit genau einem Zielfenster und fensterrelativen Start-/Endkoordinaten. Ein vollständiger Cross-Window-Drag mit getrennten Quell-/Zielfenstern beziehungsweise gehaltenem Mausbutton ist nicht dokumentiert. Deshalb wurde der tatsächliche Mausablauf hier **nicht automatisch reproduziert**. Es wurden weder private Eingabeinjektion noch erfundene Koordinaten außerhalb des Quellfensters verwendet. Die nachfolgenden direkten DockManager- und nativen HWND-Tests sind ergänzende Nachweise, keine bestandene Mausabnahme. Der im Auftrag erwähnte Screenshot wird als Eigentümerbeobachtung behandelt; eine eigene Sichtprüfung dieser Bilddatei liegt nicht vor.

## Nachgewiesene Ursache

Die Analyse verwendet den lokal vorhandenen, unveränderten Dock-Quellstand `78da173545501e796f37bc8528e9e55d9cbfd991` (v12.1.0.13). Die Maus-Pipeline führt nach Hit-Testing über `DockManagerState.Execute` zu `DockManager.ValidateDockable(..., bExecute: true)`, `DockService.MoveDockable` und der produktiven `EditorDockFactory.MoveDockable`-Übersteuerung.

1. `EditorDockFactory.Change` lässt die Layoutmutation durch `DockEditorSession.Relocate` zu und führt sie in einer `EditorCoordinator.Observe`-Grenze aus. `_mutationDepth > 0`, `ObserverDepth = 1`, neue Relokationen sind gesperrt.
2. Dock entfernt das letzte Tool aus dem Floating-Quell-Dock. `CollapseDock` ruft `RemoveWindow`, dieses `window.Exit()` und damit `HostWindow.Close()` auf.
3. Das produktive `DocumentSafeHostWindow.OnClosing` prüfte erneut `owner.CanChange`. Dieser Wert ist während der bereits zugelassenen Mutation wegen der Observer-Grenze false. Deshalb wurde das Schließen des **inzwischen leeren** Quellfensters vor `base.OnClosing` abgebrochen.
4. Dock respektiert `IHostWindowCloseState.IsClosed == false` und erhält Fenster-, Modell- und Factory-Registrierungen. Nach Verlassen der Mutation wird das Schließen nicht erneut versucht.
5. `DockShellLayout.Move/Dock` besitzt zusätzlich `CloseEmptyHosts()` nach Rückkehr aus der Factory. Die bisherigen menü-/API-basierten Zyklen verdeckten dadurch den Fehler. Der direkte DockManager-Pfad hat diese zusätzliche Bereinigung nicht.

Damit ist ein **Integrationsfehler**, kein nachgewiesener Dock-Upstream-Defekt, belegt. Technisch sind die zurückbleibenden Fenster weiterhin registrierte, leere Hosts, keine bereits aus dem Modell entfernten HWND-Waisen. Die Zuordnung dieses Mechanismus zu allen Fenstern des ursprünglichen Eigentümer-Mausablaufs bleibt bis zur instrumentierten manuellen Nachprüfung offen.

## Nativer Rotnachweis

`native-red-Debug.json` und das zugehörige `.windows.jsonl` protokollieren sechs direkte DockManager-Redocks: drei einzelne Variables-Tools und drei Gruppen mit Variables/Output. Diagnostics bleibt als legitimes zweites Floating-Fenster geöffnet. Ergebnis: **19 Checks bestanden, 13 fehlgeschlagen**. Nach jedem Redock wächst die Zahl leerer sichtbarer Hosts um eins, bis sechs leere Hosts neben Diagnostics bestehen. Jeder Drop wird angenommen; Editor und anderes Fenster bleiben korrekt.

Konkreter erster Nachzustand: Hauptfenster HWND `854784`, legitimes Diagnostics HWND `2755322`, leerer Quellhost HWND `4065974`, Owner `854784`, Titel `Window`, `IsVisible=true`, `ShowInTaskbar=true`, `WindowState=Normal`, Factory-registriert. Hostidentität `54288170` ist dem leeren Floating-Modell `9924058` zugeordnet. Zwei Hosts und drei DockControls bleiben registriert. Die unsichtbaren Avalonia-/IME-Hilfsfenster werden separat erfasst. Die sichtbaren leeren Fenster sind damit eindeutig `DocumentSafeHostWindow`-Instanzen, keine Drag-Preview-/Overlay-Fenster. Eine visuelle Taskleistenprüfung ersetzt diese Eigenschafts-/HWND-Messung nicht.

Die zehn ursprünglichen neuen NUnit-Fälle scheiterten vor der Korrektur an der fehlenden Hostfreigabe (`red-2/red.trx`). `red.log` enthält den vorangegangenen Compilerfehler einer Test-Stringinterpolation und ist kein Rotnachweis des Produktfehlers.

Der native Grünlauf besteht **32/32 in Debug und Release**, einschließlich jeweils sechs Zyklen. Nach jedem Drop verbleiben ausschließlich Diagnostics, ein Floating-Host und zwei DockControls; kein leerer Host. Das konkrete Quell-HWND ist jeweils zerstört. Nach Hauptfenstershutdown bleiben null Hosts/DockControls und keine Editorregistrierung. Rohbelege: `native-green-Debug.json`, `native-green-Release.json` und die jeweiligen `.windows.jsonl`. Die zwölf gezielten NUnit-Fälle bestehen (`green/green.trx`) und sind zusätzlich in beiden vollständigen GUI-Suiten enthalten.

## Korrektur

`EditorDockFactory.RemoveWindow` merkt ausschließlich während eines bereits zugelassenen Factory-Mutationsbereichs das konkret zu entfernende leere Fenster. Die Ausnahme gilt nur, wenn Factory, aktueller Layout-Root, Root-Fensterregistrierung und Fensteridentität übereinstimmen und der gesamte Quellbaum weder Tool noch Dokument enthält. Der vorherige Marker wird in `finally` wiederhergestellt. Die Bedingungen werden unmittelbar in `OnClosing` erneut geprüft.

`DocumentSafeHostWindow.OnClosing` erlaubt damit die synchrone Bereinigung dieses einen leeren Hosts, obwohl die Observer-Grenze neue Benutzeraktionen sperrt. Der Dokument- und Anwendungsschließschutz bleibt erhalten. Es werden keine weiteren Floating-Fenster gesucht oder pauschal geschlossen, keine abgelehnten Dock-Aktionen wiederholt, keine Lease freigegeben und keine Gate-/Queue-Regeln geändert. Es gibt keinen asynchronen Cleanup-Callback und keinen neuen Timer im Produkt.

Produktdateien: `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs`, `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs`. Testdateien: neu `CryptoWorkBenchGuiTest/DockMouseRedockTests.cs` und `DockRedockNativeProbe.cs`; Modusregistrierung in `DockShellNativeProbe.cs` und `eng/gui/native/Program.cs`. Keine Prism-/Dock-Upstream- oder Paket-Lock-Änderungen.

## Permanente Regressionen und Instrumentierung

Zwölf neue NUnit-Fälle prüfen vier Zyklen je Kombination aus Einzeltool/Gruppe und mit/ohne anderem Floating-Host; jeweils genau ein Closed-Ereignis, keine verbleibenden leeren Hosts/Modelle/DockControls, keine duplizierten Pane-Instanzen, identische Variables-/Info-Views und globale ViewModels, Editor/Dokument/Undo-Stack/SaveIntent/WorkspaceGeneration sowie sauberen, erneut aufrufbaren Dispose. Fünf Gatefälle decken Pending, Active, Observer, Busy und Modalität ab. Der Leasefall bestätigt eine echte erlaubte MCP-Änderung bei gleichzeitig abgewiesenem Drop und Host-Close; erst nach regulärem Release wird Redock zugelassen. Zwei Callbackfälle versuchen während des leeren Quellhost-Close ein anderes Tool-/Editorfenster zu schließen: beide bleiben geschützt.

Der neue native Modus `redock` verwendet den vorhandenen G1ShellProbe und dieselbe produktive Prism-/Shell-/Editorinstanz. Er prüft zusätzlich das tatsächliche Verschwinden des Quell-HWND mit `IsWindow`, die Erhaltung des anderen HWND, Modell-/Factory-Freigabe und den anschließenden Shutdown. Das JSON kennzeichnet die Eingabe ausdrücklich als programmatischen DockManager-Pfad. Je Lauf sechs Zyklen und 32 Checks.

`redock-manual` bewegt selbst keine Pane. Das Journal beginnt vor manuellen Aktionen, erfasst Factory-/Dockable-/Window-Drag-/Opened-/Closing-/Closed-Ereignisse sowie geänderte Zustände im 150-ms-Takt. Erfasst werden alle Top-Level-HWNDs des eigenen Prozesses mit Owner, Titel, Klasse, Extended Style, Sichtbarkeit und Zuordnung zu verwalteten Hosts; außerdem WindowState/ShowInTaskbar, Host-/Modellidentitäten, aktive Floating-Pane-IDs, Host-/DockControl-Registrierungen, ObserverDepth/CanRelocate und Layout-Dispose. Kurzlebige Overlayfenster zwischen Samples können fehlen; nicht zuordenbare native Fenster werden ausdrücklich als solche erfasst. Das Journal nutzt ausschließlich lesende Win32-Abfragen. Sein Timer und Writer enden mit dem Probe-Lebenszyklus. Das manuelle Ergebnis vergibt niemals automatisch M2-PASS.

## Vollständige Regression

Windows 11, OS-Build `10.0.26200`, x64, .NET 10.0.12. Locked Restore und Builds wurden mit der vorhandenen Prism-Integration ausgeführt. Reproduzierbarer vollständiger Build-/Testaufruf aus dem G1-Worktree (neuen Evidence-Pfad verwenden):

```powershell
./eng/gui/Validate-G1Platform.ps1 -Configuration Both -EvidenceDirectory artifacts/g1-n1-f01/recheck
```

| Suite | Debug bestanden | Release bestanden | Abgrenzung |
|---|---:|---:|---|
| GUI | 965 | 965 | 953 erhalten + 12 neue Fälle |
| MCP | 176 | 176 | jeweils 13 Explicit nicht ausgeführt |
| Core | 11.512 | 11.512 | jeweils 26 Skipped und 1 Explicit nicht ausgeführt |
| Prototyp Headless | 12 | 12 | unverändert |
| Native Baseline, programmatisch | 514 | 514 | unveränderte Checks |
| Native Baseline, externe Checkpoints | 205 | 205 | unveränderte Checks |
| Native Baseline gesamt | **719** | **719** | G1-08 vollständig erhalten |
| Neue native Redock-Checks | **32** | **32** | ergänzend, kein Mausabnahme-PASS |

Keine Fehlschläge in den ausgewiesenen Abschlussläufen. Die 26 Core-Skips betreffen unveränderte Plattform-/Symlinkvoraussetzungen, der Core-Explicit-Fall `MeasureLocalHardware`. Namen und Gründe sämtlicher Skips/Explicit-Fälle stehen getrennt in der Evidence. Die Gesamt-TRX-Zahlen sind daher Core 11.539 und MCP 189; nicht ausgeführte Fälle werden nicht als bestanden gerechnet. Linux/macOS wurden in diesem Korrekturpaket nicht validiert.

Rohpfade für die vollständigen NUnit-Suiten: `verified/product` und `verified/prototype`. Native Debug-Zuordnung: `native-Debug/dock.json` (67), elf Modi aus `native-Debug-2` (alle außer `dock` und `layout-geometry`), Geometrie separat `geometry/Debug.json` (56). Native Release: zwölf Modi aus `native-Release`, Geometrie aus `geometry/Release.json` (56). Die Evidence listet jeden tatsächlich gezählten Einzelpfad; Wiederholungen werden nicht doppelt gezählt. Externe Modi unter `input-Debug` und `input-Release`: Editor/MCP 54, Tools 34, Output 29, Diagnostics 35 und Layout 53, jeweils 205.

Die externen Checkpoints wurden mit dem Computer-Use-Skill (`@oai/sky`) bedient: Unicode `ä€😀` als Literal, Return und KP_Enter, Ctrl+Z/Y, Alt+PageUp, `Shift_L+8` für Completion, Mausauswahl/Clipboard, Copy-Example-Buttons, Info-History, Diagnostics-Filter/Navigation einschließlich Stale-Ablehnung, Haupt-/Floating-Maximize/Restore und Layout-Reset. Fixture-Aufbau und nachgelagerte Assertions bleiben programmatisch. Die nativen Editor-Modi prüfen weiterhin echten MCP-Transport und Lease-Lebenszyklen. Keine dieser Bedienprüfungen gilt als Cross-Window-Mausredock.

Erhaltene Zwischenläufe: Die erste Debug-Matrix hat Vordergrund-Timeouts bei Editor/Diagnostics/Layout/Invalid sowie sechs Prototyp-Fokusfehler. Das fehlgeschlagene Layout lieferte folglich kein korrektes vorgesehenes Restart-Fixture; vier Restart- und fünf Geometrieprüfungen schlugen fehl. Die zweite Debug-Matrix bestand diese Abläufe; während zusätzlicher externer Aktivierung verfehlte `dock` zwei Fokuschecks und `layout-geometry` den anfänglichen Maximiert-Zustand. Der vollständige frühere Docklauf besteht 67/67; frische Geometrieläufe ohne eingreifende Aktivierung bestehen 56/56 in beiden Konfigurationen. Alle Fehlchecks und Rohdateien bleiben in der Evidence. Es wurden weder Assertions geschwächt noch diese Versuche als grüne Läufe ausgegeben. Der erste gezielte Grünbefehl meldete nach erfolgreichen Tests/Build noch einen Shell-Aufräumfehler (`Leave-Prism` irrtümlich als Funktion); der Prozess endete, spätere Aufrufe verwenden korrekt das vorhandene Skript.

Der Recorder erhielt zusätzlich einen Release-Smokecheck: sichtbarer Start und externes Schließen per Titelleisten-X; `manual-recorder-smoke-Release.json` und Journal enden mit `manual-main-closed`/`journal-dispose`, `disposedLayout=true`, null Hosts/DockControls. Dabei fand kein Drag/Redock statt; die Modusbeschreibung bezeichnet die vorgesehene manuelle Eingabe, keinen erfolgten Eigentümer-Mausnachweis. Das Ergebnis bleibt ausdrücklich `NOT CERTIFIED`.

## Git- und Dateiintegrität

Alle **93** geschützten historischen Berichte/Evidence-Dateien, Prototypdateien und Paket-Locks stimmen per SHA-256 mit dem Anfangsstand überein. Die vier geänderten vorhandenen Dateien sind ausschließlich die oben genannten zwei Produktdateien und zwei Modusregistrierungen. Neu hinzugekommen sind zwei Test-/Probe-Dateien, dieser Bericht und die neue Evidence. Alle bestehenden Testdatei-Assertions bleiben erhalten. Der lokale Dock-Checkout und `.deps/prism` sind sauber; die geprüften Worktrees `master` und `script-assignment` sind sauber und auf ihren unveränderten HEADs. Kein anderer Worktree wurde bearbeitet.

Der vollständige Abschlussstatus enthält **107** uncommittete Dateien: 36 modifiziert und 71 ungetrackt, einschließlich der bereits vorhandenen G1-Arbeiten. Er steht in `artifacts/g1-n1-f01/final-status.txt` und in der Evidence. Das Quellmanifest umfasst 1.106 Dateien ohne die Evidence-Selbstreferenz; deren eigener Hash steht in `final-evidence.sha256`. `git diff --check` ist erfolgreich; neue Dateien wurden zusätzlich auf nachgestellte Leerzeichen und gegen die erlaubte Dateiliste geprüft. Rohbelege, Ausgangsmanifeste und finaler Worktree-Status sind ebenfalls gehasht.

## Manuelle Nachprüfung durch den Eigentümer

Aus dem G1-Worktree die neu gebaute Release-Anwendung starten:

```powershell
& ./CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.exe --dock-shell
```

1. Variables per Tab-Mausdrag aus dem Hauptfenster ziehen. Nutzbaren Inhalt und genau ein neues Floating-Fenster bestätigen.
2. Variables per Maus auf den gewünschten Tool-Dock-Zielbereich im Hauptfenster zurückziehen. Nach abgeschlossenem Drop darf kein leeres weißes Zusatzfenster und kein zugehöriger Taskleisteneintrag verbleiben.
3. Mindestens fünfmal wiederholen. Anzahl realer Fenster und Taskleisten-Vorschauen jeweils vor/nach dem Drop vergleichen.
4. Diagnostics separat floating lassen und Variables erneut floaten/redocken. Diagnostics muss sichtbar und bedienbar erhalten bleiben.
5. Variables und Output in einem Floating-Host gruppieren; die ganze Gruppe per Maus zurückdocken. Alle Tools müssen genau einmal vorhanden und bedienbar sein.
6. Editorinhalt, Undo/Redo und vorhandene Toolzustände prüfen. Anwendung regulär schließen; keine Probe-/Produktfenster dürfen zurückbleiben. Neustart und Layoutzustand prüfen.

Für ein zusätzliches instrumentiertes Protokoll denselben Ablauf im isolierten Probe durchführen (neuen Dateinamen verwenden; dieses Fenster speichert nicht in die Benutzerlayoutdatei):

```powershell
$evidence = [IO.Path]::GetFullPath('artifacts/g1-n1-f01/owner-mouse-Release.json')
& ./eng/gui/native/bin/Release/net10.0/G1ShellProbe.exe redock-manual $evidence
```

Variables und Gruppen ausschließlich mit der Maus bewegen; am Ende das Hauptfenster regulär schließen. Ergebnis und `.windows.jsonl` zusammen mit Windows-/App-Version, Einzeltool-/Gruppenergebnis und Taskleistenbeobachtung aufbewahren. Für jeden geschlossenen leeren Quellhost muss das ursprüngliche HWND verschwinden und die zugehörige Factory-/Root-Registrierung entfallen. Andere legitime Fenster bleiben erhalten. Erst die ausdrückliche erneute Eigentümerbestätigung setzt M2 auf PASS; dieser Bericht oder ein grüner programmatischer Lauf tut dies nicht. Nach G1-N1-F01 wird gestoppt, M3–M8 werden nicht bearbeitet.
