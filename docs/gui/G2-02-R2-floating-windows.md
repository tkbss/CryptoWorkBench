# G2-02-R2 – Unabhängige Floating Windows

Status: **READY FOR VISUAL REVIEW**, vorbehaltlich der unten dokumentierten Prüfgrenzen und der Eigentümerabnahme.

## Arbeitsort und Umfang

Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1`, Branch `feature/gui-g1`. Die vorhandenen uncommitteten G1/G2/R1-Änderungen bleiben erhalten. R2-Ausgangsstatus, getrackter Diff und Quellensicherung liegen unter `artifacts/g2-02-r2/baseline/`. Kein Commit, Push, Merge oder Eingriff in `master`.

Produktiv werden nur die Floating-Host-Präsentation und deren Initialisierung geändert. Keine Multi-Dokument-Erweiterung; keine Änderung an CryptoScript, MCP-Verträgen, Editor-Koordination oder Layoutschema.

## Ursache und Entscheidung

Untersucht wurde der lokal vorhandene Quellstand der eingesetzten Dock-Version unter `artifacts/g1/dock-source/src/`.

| Einstellung / Mechanismus | Befund und R2-Entscheidung |
| --- | --- |
| `DockSettings.FloatingWindowOwnerPolicy` | Standard `Default`; `NeverOwned` würde eine globale Richtlinie setzen. R2 verändert keine prozessweiten Defaults. |
| `DockSettings.UseOwnerForFloatingWindows` | Standard `true`. Im Default-Pfad verwendet Dock ein Besitzerfenster; Windows behandelt dessen Floating Hosts dann gemeinsam mit dem Besitzer. Ein blosses `false` schützt zudem nicht vor explizitem ParentWindow. |
| `DockWindowOwnerMode` | R2 setzt **`None` pro IDockWindow** in der dreiparametrigen `InitDockWindow`-Überladung. Dock beendet damit die Owner-Auflösung vor Parent-/Root-/Fallback-Suche. Gilt für neue und wiederhergestellte Fenster. Die logische Layout-Zugehörigkeit (`IDockWindow.Owner`) bleibt erhalten; nur die native Window-Ownership entfällt. |
| `ShowInTaskbar` | Pro Modell und Host `true`. Die native Prüfung kontrolliert zusätzlich: kein HWND-Owner und kein `WS_EX_TOOLWINDOW`. Windows darf die Einträge weiterhin unter einem App-Symbol gruppieren; jedes Fenster bleibt ein eigener Eintrag. |
| `WindowDecorations` | Für jeden Floating Host `Full`, `ExtendClientAreaToDecorationsHint=false`, `ToolChromeControlsWholeWindow=false`, `DocumentChromeControlsWholeWindow=false`. Lokale Werte verhindern, dass Dock-Theme-Pseudoklassen die native Titelleiste wieder ausblenden. |
| Bisherige Editor-Titelleiste | `DockDocumentWindowChrome` ersetzte den OS-Caption durch einen Dock-Drag-Griff mit eigenen Fensterbuttons. Diese Ersatzimplementierung entfällt. |
| Floating-Templates / Drag | Die bestehenden Host-Content-Templates, DockControl-Overlays und Tab-Drag-Flächen bleiben bestehen. Die native Titelleiste verschiebt das Fenster; Docking erfolgt über den Tab. |
| `WindowState` | Keine gemeinsame Bindung, kein Broadcast und kein neuer Synchronisationshandler. Jedes native Window behält seinen eigenen Zustand. |
| `DockSettings.CloseFloatingWindowsOnMainWindowClose` | Der globale Standard `false` bleibt erhalten. Die vorhandene Shell erfasst vor dem Shutdown das Layout und ruft nach tatsächlich erfolgtem `Closed` `ReleaseLayout`/`Dispose`/`Root.ExitWindows` auf. Das schliesst auch besitzerlose Hosts. Eine zweite globale Close-Kette wäre hier unnötig. |

Die Host-Initialisierung setzt die Präsentationswerte vor dem Öffnen. Die vorhandene Schutzregel bleibt bestehen: Ein Benutzer kann den einzigen CryptoScript-Editor nicht durch Schliessen seines Floating Hosts verwerfen. Hauptfenster-Shutdown, Reset und zulässige Dock-Operationen dürfen ihn weiterhin freigeben. Tool-Fenster behalten ihr bisheriges Close-Verhalten und die vorhandenen Admission-Gates.

Beim nativen Neustart fiel der Framework-Standardtitel `IDockWindow` auf. Der Host leitet seinen Caption-/Taskbar-Titel deshalb aus den tatsächlich enthaltenen Sitzungspanes ab, auch nach Gruppenänderungen. Ein einzelner Host heisst `CryptoScript`, `Variables` oder `Info`; Gruppen nennen ihre enthaltenen Ansichten. Der LayoutUpdated-Handler wird bei `Closed` abgemeldet. Es entsteht kein neues persistiertes Feld.

## Persistenz, Editor und MCP

Schema 1, Pane-Identitäten, Fenstergeometrie, Reset und die bisherige Behandlung von WindowState bleiben unverändert. `OwnerMode` und Taskbar-Präsentation werden beim Initialisieren erneut angewendet und nicht ins Whitelist-Schema aufgenommen. Es gibt keine Migration oder Löschung von Benutzerlayouts. Wie bisher werden die bestehenden Layoutdaten wiederhergestellt; R2 führt keine neue Persistenz von minimierten/maximierten Floating-Zuständen ein.

Die Tests prüfen Editor-, Document- und UndoStack-Identität bei Redock, Reset, Close und gemischten Gruppen. MCP-Lease- und Observer-Sperren werden nun am Dock-Tab statt an der entfernten künstlichen Titelleiste geprüft. Native Titelleistenbewegung ist eine OS-Fensteroperation und führt selbst keine Dock-Relocation aus.

Alle nativen Proben verwenden ausschliesslich isolierte Layoutdateien neben ihren Ergebnisdateien. Der normale Benutzer-Layoutpfad wurde dafür nicht verändert.

## Geänderte Dateien

Produktiv:

- `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs`: Ownership/Taskbar-Richtlinie je Floating-Modell.
- `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs`: native Host-Chrome und aussagekräftige Fenstertitel; bestehende Close-Regeln beibehalten.
- `CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs`: nicht mehr benötigte Ersatz-Titelleiste entfernt.

Tests und native Werkzeuge:

- `CryptoWorkBenchGuiTest/DockIndependentWindowTests.cs` (neu): drei parallele Hosts, unabhängige Zustände, Redock/Shutdown, Restore/Reset und Titel.
- `CryptoWorkBenchGuiTest/DockIndependentWindowNativeProbe.cs` (neu): native Matrix, Prozessneustart und interaktives Zustandsjournal.
- `CryptoWorkBenchGuiTest/DockNativeChromeTests.cs`: native Chrome auch bei gemischten Gruppen, unveränderte Identitäts- und Close-Prüfungen.
- `CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs`: echte geroutete Tab-Eingabe statt der entfernten Caption, inklusive Observer-/Coordinator-/MCP-Sperren.
- `CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs`: neue Chrome-/Owner-Erwartung.
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`, `DockEditorNativeProbe.cs`, `DockToolNativeProbe.cs`, `DockOperationNativeProbe.cs`, `DockPartialFailureNativeProbe.cs`, `DockPersistenceNativeProbe.cs`: native Owner-Assertions auf die neue Anforderung aktualisiert; zusätzliche Probe-Modi eingebunden.
- `eng/gui/native/Program.cs`: Modi `independent-windows`, `independent-restart`, `independent-review` zugelassen.
- Dieser Bericht.

Keine bestehenden Testfälle entfernt oder deaktiviert. Veraltete Erwartungen an Owner-HWND und künstlichen Caption wurden durch die explizit geforderte native/unowned-Konfiguration ersetzt; Redock-, Admission-, Identitäts- und Lifecycle-Assertions bleiben bestehen.

## Automatisierte Prüfungen

Gezielte Regressionen: **26 bestanden** (`targeted-r3/chrome.trx`). Darunter zwei neue Tests; die vorhandene GUI-Suite wächst von 1122 auf 1124 Fälle.

Vollständiger R2-Lauf (`validation-final/`), jeweils Debug und Release:

| Suite | Bestanden je Konfiguration | Nicht ausgeführt |
| --- | ---: | ---: |
| GUI, seriell | 1124 | 0 |
| MCP | 176 | 13 |
| CryptoScript Core | 11512 | 27 |

Debug- und Release-Builds auch nach der letzten Ergänzung der Restore-Fenstertitel erfolgreich (`final-build-Debug.log`, `final-build-Release.log`). Die finalen GUI-Suiten bestehen jeweils mit **1124/1124** (`final-Debug/gui.trx`, `final-r2-Release/gui.trx`). MCP/Core-Code blieb unverändert.

Der erste normale Debug-GUI-Lauf (`validation/`) hatte 1123 bestandene Fälle und einen Fehler beim Start der isolierten Headless-App vor dem Testkörper `ShortPrintIsVisibleAndSourceButtonNavigates(911,512)`: `Dispatcher.VerifyAccess` / `DefaultRenderLoop.Add`. Das entspricht dem bereits in R1 beobachteten intermittierenden Headless-Initialisierungsfehler. Die vollständigen seriellen Läufe verwenden nur `NUnit.NumberOfTestWorkers=0`, ohne Filter, Test-Retries oder abgeschwächte Assertions. Der Infrastrukturfehler wird durch R2 nicht behoben.

Auch `final-Release/gui.trx` meldete einmal diesen Startfehler vor demselben unveränderten R1-Testkörper (1123 bestanden / 1 Fehler), trotz serieller Ausführung. Serialisierung garantiert damit keine Behebung. Die identische vollständige Release-Wiederholung unter `final-r2-Release/` besteht mit 1124/1124; sämtliche Fehlversuche bleiben erhalten.

Weitere Zwischenfehler bleiben in den Logs: zwei korrigierte Test-Compilefehler und ein Debug-Build mit zuvor auf Release umgestellten Restore-Assets (`AttachDeveloperTools`). Ein passender locked Debug-Restore behebt Letzteres ohne Produktänderung.

## Native Windows-Abnahme

Prüfumgebung: Windows, nativer Avalonia-Backend, Monitor 3840×2160 bei 150 %. Gleichzeitig ein Hauptfenster und drei native Floating Hosts. Beispiel: globale Hex-Variable, `Print(result)` und `Info(HASH-SHA256)`.

Ergebnisse:

- `native-input.json`: **47/47** Prüfungen bestanden, vor der abschliessenden Titelkorrektur.
- `native-restart.json`: **48/48** Prüfungen bestanden, erster echter Prozessneustart; dabei Platzhaltertitel entdeckt.
- `restart-final.json`: **49/49** Prüfungen bestanden auf dem Stand mit korrigierten Titeln, inklusive echter Wiederherstellung, nativer Ownership/Caption/Taskbar-Eignung, getrennter Minimize/Normal/Maximize-Zustände, Vordergrundwechsel, Show eines minimierten Hosts, Command-Redock, Reset, Editor-Identität, gemeinsamer Schliessung, HWND-Freigabe und keiner verbleibenden sichtbaren Prozessfenster. Prozess mit Exitcode 0 beendet.
- `review.json`: interaktives Journal; nativer Hauptfenster-Close entfernt alle drei Floating Hosts. Anschliessend sind weder Probe-Fenster noch Probe-Prozess vorhanden.

Die ersten nativen Hintergrundläufe meldeten fünf Fehler bei `GetForegroundWindow`, obwohl die verwalteten `IsActive`-Flags wechselten. Ein blosses programmatisches Aktivieren ist kein Ersatz für echte Benutzereingabe: Windows verhindert Fokusübernahme durch Hintergrundprozesse. Die finale Probe wartet daher auf **F8 im Test-Hauptfenster**, gesendet über Computer Use. Danach bleiben dieselben Foreground-Assertions unverändert und bestehen. Kein Fokus-Workaround, Foreground-Lock-Tuning oder Win32-Input-Code wurde ins Produkt eingebaut.

Direkt per Computer Use beobachtet:

| Aktion | Ergebnis |
| --- | --- |
| CryptoScript, Variables und Info jeweils an der nativen Titelleiste ziehen | Alle drei verschieben sich; jeweils nur ihre Position ändert sich. |
| Jedes Floating Window einzeln minimieren und aktivieren/wiederherstellen | Andere Fenster bleiben Normal. |
| Variables über OS-Button maximieren und wiederherstellen | Nur Variables ändert WindowState. |
| Hauptfenster minimieren | CryptoScript, Variables und Info bleiben Normal und sichtbar. |
| Hauptfenster wiederherstellen / Floating aktivieren | Einzelnes Ziel wird bedienbar; keine gemeinsame Zustandsänderung. |
| Hauptfenster über nativen Close-Button schliessen | Alle vier Fenster und der Prozess verschwinden. |

Screenshots (native Titelleisten mit sichtbaren Dock-Tabs):

- `artifacts/g2-02-r2/cryptoscript-native-title.png`
- `artifacts/g2-02-r2/variables-native-title.png`
- `artifacts/g2-02-r2/info-native-title.png`

## Prüfgrenzen und bekannter Redock-Fehler

**NOT VERIFIED – tatsächliche Bedienung der Windows-Taskleiste:** Native Fenster sind separat taskbar-fähig (`ShowInTaskbar=true`, kein Owner, kein `WS_EX_TOOLWINDOW`) und separat aktivierbar. Ein Klick auf jeden konkreten Taskleisten-/Thumbnail-Eintrag konnte mit der verfügbaren fenstergebundenen Computer-Use-Schnittstelle nicht geprüft werden. Windows kann die Einträge gruppieren; die manuelle Eigentümerabnahme soll die Erreichbarkeit aller vier Fenster bestätigen.

**NOT VERIFIED – echter Cross-Window-Mouse-Redock in R2:** Der reale Info-Tab-Drag wurde vom Computer-Use-Werkzeug vor der Ausführung abgelehnt: Ziel ausserhalb der Bounds des Quellfensters. Das ist eine Grenze des Testwerkzeugs und keine neue Produktfehlermeldung. Gerouteter Tab-Drag wird headless geprüft; Command-Redock und DockManager-Relocation inklusive Host-Bereinigung werden automatisiert und nativ geprüft. Diese Nachweise ersetzen keinen echten Maus-Drop zwischen zwei HWNDs.

Der bereits bekannte Cross-Window-Mouse-Redock-Fehler wird **nicht als behoben** behauptet. R2 ändert weder DockManager noch Overlay-/Drag-Lifecycle. Ein bei der Eigentümerprüfung weiterhin auftretender Cross-Window-Drop-Fehler muss von den hier geprüften nativen Ownership-/Chrome-/WindowState-Funktionen getrennt bewertet werden. Im geprüften Umfang kein neuer Redock-/Shutdown-Fehler beobachtet.

## Startkommando

```powershell
& 'C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1\CryptoWorkBenchAvalonia.Desktop\bin\Debug\net10.0\CryptoWorkBenchAvalonia.Desktop.exe' --dock-shell
```

Über **View → CryptoScript / Variables / Info → Float** die drei Fenster öffnen. Bereits gespeicherte Layouts erhalten die neue Host-Präsentation automatisch. Reset ist dafür nicht erforderlich. Die manuelle Eigentümerabnahme bleibt offen.
