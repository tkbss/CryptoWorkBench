# G1-02 – MainWindow Shell and Dock Architecture

## Ergebnis und Freigabegrenze

Die neue Dock-Shell ist als ausdrücklich auswählbare Platzhalter-Shell in die bestehende Avalonia-/Prism-Anwendung integriert. Die bisherige produktive GUI bleibt der Standard. **CONDITIONAL GO für G1-03**, nach Eigentümerabnahme; G1-03 wurde nicht begonnen.

Der Vorbehalt betrifft insbesondere den noch zu entwerfenden Editor-Detach/Attach- und MCP-Lebenszyklus, fehlende Linux-/macOS-Nativnachweise, vollständiges mausgesteuertes Cross-Window-Redock sowie die weiterhin auftretenden Dock-Bindingmeldungen. G1-02 enthält keine produktive Editor-Migration.

## Ausgangszustand und Erhaltung

- Worktree: `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`.
- HEAD vor und nach der Arbeit: `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.
- Die 14 bereits ungetrackten G1-01-Dateien wurden vor Änderungen per SHA-256 erfasst und abschließend unverändert nachgewiesen. Dazu gehören Bericht, Ergebnisevidenz, Prototyp, Tests und deren Lockdateien.
- Keine Änderungen in den anderen Worktrees, kein Commit, Push oder Merge.
- Prüfprotokolle liegen unter `artifacts/g1-02/`; dauerhafte zusammengefasste Evidenz unter `docs/gui/evidence/G1-02-results.json`.

## Architektur und Zuständigkeiten

`DockShellState` hält ausschließlich Präsentationszustand: ein Dokument und fünf Tool-Zustände mit unveränderlichen IDs und einem editierbaren Platzhalter-Notiztext. Er ist kein fachlicher Workspace und besitzt weder Interpreter noch Editorregistrierung oder MCP-Transport.

| Bereich | Identität | Inhalt |
|---|---|---|
| DocumentDock | `dock:documents` | `document:cryptoscript:primary` |
| ToolDock | `dock:tools` | `tool:variables`, `tool:output`, `tool:info` |
| DiagnosticsDock | `dock:diagnostics` | `tool:diagnostics`, `tool:status` |

`DockShellLayout` besitzt Factory, RootDock und die sechs stabilen Dockable-Instanzen. Es führt UI-threadgebundene Layoutoperationen aus. Der Notiztext hängt am stabilen Zustand, nicht an der erzeugten View. Floating, Verschieben, Schließen und Wiederöffnen erzeugen keine neuen logischen Panels. Die Dock-Modellstruktur bleibt für spätere mehrere Dokumente geeignet; G1-02 erzeugt jedoch genau einen Dokumentplatzhalter und schaltet die Dokument-Neuanlage ab.

`DockShellPresentation` installiert Dock-Fluent-Styles und das Platzhalter-DataTemplate ausschließlich für den Preview-Start. `DockPlaceholderView` enthält Titel und TextBox; es gibt keinen `CryptoScriptEditView`, `TextEditor`, `AttachEditor` oder `DetachEditor` in diesem Pfad.

`DockShellWindow` enthält DockControl, ein begrenztes File-/View-Menü und den Preview-Hinweis. „Show“, „Float“, „Dock“ und für Tools „Close“ arbeiten auf denselben Instanzen. „Show“ aktiviert auch einen bestehenden nativen Floating-Host. Tabs und Dock-Drag-Verhalten kommen aus Dock.Avalonia.

`DockShellStartup.Register` ergänzt den vorhandenen Prism-Container um genau drei Singleton-Registrierungen: State, Layout und Window. Es gibt keinen zusätzlichen Produktcontainer. Die bestehenden drei Automation-Service-Registrierungen bleiben auf `WorkbenchAutomationService.Shared` gerichtet. In der Preview werden weder die produktiven Regionen noch die Desktop-MCP-Anbindung initialisiert, weil es noch keinen produktiven Editor gibt. Der Standardstart und der bestehende `--mcp-stdio`-Pfad bleiben erhalten.

## Start, Fehler-Rückfall und Lebensdauer

Nach einem Build in der vorhandenen Prism-Umgebung:

```powershell
# Bestandsshell (Standard)
./CryptoWorkBenchAvalonia.Desktop/bin/Debug/net10.0/CryptoWorkBenchAvalonia.Desktop.exe

# G1-02-Platzhalter-Shell
./CryptoWorkBenchAvalonia.Desktop/bin/Debug/net10.0/CryptoWorkBenchAvalonia.Desktop.exe --dock-shell
```

Die bestehende `MainWindow`-XAML, ihre ViewModels, Prism-Regionsregistrierungen und der vorhandene sichere Editor-Shutdown wurden nicht verändert. Der Selektor ist auf den Programmstart begrenzt; es gibt kein Umschalten während einer laufenden Sitzung und keine dauerhaft gespeicherte Auswahl.

Wenn der Preview-Aufbau scheitert, werden der teilweise erzeugte Dock-Host, Floating-Hosts und Preview-Ressourcen freigegeben, die Ausnahme in `DockShellStartupError`/Trace festgehalten und anschließend die Bestandsshell aufgebaut. Der native Fehler-Test wirft absichtlich **nach** dem vollständigen Aufbau der Dock-Shell und prüft danach den realen produktiven Editor samt Registrierung und Textbearbeitung.

Tool-Schließen entfernt die sichtbare Dockable-Verknüpfung; der Singleton-Zustand bleibt erhalten. Beim Wiederöffnen wird dasselbe Dockable in seinen Heimatbereich eingesetzt. Auch native Host-Schließvorgänge werden geprüft. Das einzige Dokument ist nicht schließbar; ein kleiner HostWindow-Ableger schützt zusätzlich dessen Floating-Host, da Dock den nativen Fensterknopf nicht über `Document.CanClose` absichert. Owner-/Anwendungs-/OS-Shutdown darf passieren: Avalonia schließt besessene Fenster bereits vor dem `Closed`-Ereignis des Hauptfensters. Der Schutz unterscheidet deshalb den nativen `CloseReason`. Beim anschließenden Layout-Dispose schließt `Root.ExitWindows` alle übrigen Hosts.

Ein abgebrochener MainWindow-Schließvorgang räumt das Layout nicht ab. Erst `Closed` löst das idempotente Freigeben aus. DockControl verliert dabei Layout und Factory; Factory-Host- und DockControl-Registrierungen werden leer. Leere Floating-Gruppen werden beim Redock entfernt. Wenn ein ganzer Tool-Bereich ausgekoppelt wurde, wird der fehlende Hauptfenster-Anker bei Bedarf neu erstellt; die Suche ignoriert gleichnamige Floating-Anker.

## Abhängigkeiten

| Abhängigkeit | Festgelegte Version |
|---|---|
| Dock.Avalonia | 12.1.0.13 |
| Dock.Model.Mvvm | 12.1.0.13 |
| Dock.Serializer.SystemTextJson | 12.1.0.13 |
| Dock.Avalonia.Themes.Fluent | 12.1.0.13 |
| Avalonia | 12.1.4, unverändert |
| AvaloniaEdit | 12.0.0, unverändert |
| .NET SDK / Runtime | 10.0.401 / 10.0.12 |
| Lokale offizielle Prism-Quellen | `358118cd640d9a22ff8cf21c8ad197fa038b7990`, unverändert |

Die Dock-Transitiven haben ebenfalls Version 12.1.0.13; hinzu kommt CommunityToolkit.Mvvm 8.4.0. Versionen und Content-Hashes stimmen mit dem G1-01-Prototyp überein. Keine bestehende Paketversion und kein bestehender Pakethash wurde geändert. Der Serializer ist wie beauftragt referenziert; G1-02 verwendet noch keine Layout-Persistenz.

Aktualisiert wurden ausschließlich die betroffenen Produkt-Locks: App, Desktop und GUI-Tests jeweils für Debug/Release, außerdem App/Desktop in den vier vorhandenen Release-RID-Sätzen. Alle vorhandenen Runtime-Abschnitte bleiben erhalten. Prism-Lockdateien, Quellen, Allowlist, Build-Hooks und Integrationsskripte bleiben unverändert. Die vier RID-Restores wurden mit `CwbPublishRid` **und** passender `RuntimeIdentifier` erneut gesperrt validiert; dies ist kein nativer Plattformtest.

## Prüfungen und Reproduktion

```powershell
./eng/gui/Validate-G1Shell.ps1 -Configuration Both -Native `
  -EvidenceDirectory artifacts/g1-02/review
```

Das Skript verlangt ein neues Evidenzverzeichnis. Es verwendet die unveränderte lokale Prism-Integration, baut beide Lösungen, führt vollständige GUI-/MCP-/Core-Suiten aus und startet anschließend die drei nativen Modi in getrennten Prozessen. Der native Probe hat einen eigenen lokalen Lock und benötigt keine Erweiterung der Prism-Allowlist.

Die permanenten Shell-Tests prüfen die tatsächlichen Produktregistrierungen, Dokument-/Tool-IDs, wiederholtes Verschieben und Floating, Wiederöffnen, fehlende Gruppenanker, den Schutz des Dokumentfensters, abgebrochenen Shutdown, Host-Cleanup, fehlende Editor-Views und unveränderte Coordinator-Generationen. Die vorhandenen Editor-/Prism-/MCP-Tests bleiben Bestandteil der vollständigen Regression.

Der native Probe verwendet das echte Produkt-`App` mit Prism und Windows-Backend. Er prüft echte HWNDs, Avalonia- und Win32-Owner, unabhängige Fensterpositionen, TextBox-Fokus **und** `GetForegroundWindow`, Redock, Textzustand, native Schließvorgänge und Shutdown. Zusätzlich prüft er die normale Bestandsshell und den echten Initialisierungsfehler-Rückfall. Die Ausführungs-/Editor-Generationen und die State-Revision dürfen sich während Docking und Preview-Shutdown nicht ändern.

Die Trennung zwischen Skipped und nicht ausgeführten Explicit-Tests folgt der NUnit-Ausgabe; TRX kodiert beide als `NotExecuted`, weshalb der TRX-Summary-Zähler allein ungeeignet ist. Alle Testnamen und Einzelchecks sind in der JSON-Evidenz beziehungsweise den dort referenzierten TRX-Dateien nachprüfbar.

### Abschließende Ergebnisse vom 6. November 2026

Beide Lösungen bauen in Debug und Release erfolgreich. Die folgenden Zahlen gelten **je Konfiguration**; die vollständigen Debug- und Release-Läufe wurden separat ausgeführt:

| Suite | Passed | Failed | Skipped | Nicht ausgeführt (Explicit) | Vergleich G1-01 |
|---|---:|---:|---:|---:|---|
| GUI gesamt | 810 | 0 | 0 | 0 | 797 → 810, +13 Shell-Tests |
| davon neue Shell-Tests | 13 | 0 | 0 | 0 | neu; in GUI-Gesamtzahl enthalten |
| MCP | 176 | 0 | 0 | 13 | unverändert |
| Core vollständig | 11.508 | 0 | 26 | 1 | unverändert |
| Native Windows: Dock-Shell | 67 | 0 | 0 | 0 | neue Produktprüfungen; G1-01 hatte 60 Prototypchecks |
| Native Windows: Bestandsshell | 10 | 0 | 0 | 0 | neu |
| Native Windows: Fehler-Rückfall | 13 | 0 | 0 | 0 | neu |

Die 26 Core-Skips betreffen plattformspezifische beziehungsweise nicht verfügbare Symlink-/Native-Voraussetzungen. `MeasureLocalHardware` bleibt ein expliziter, nicht ausgeführter Hardware-Test. Die 13 expliziten MCP-Integrationsfälle mit zusätzlichen externen Voraussetzungen bleiben wie in G1-01 unausgeführt. Sie werden nicht als bestanden gezählt. Für den finalen Stand wurden keine Regressionen festgestellt.

Zusätzlich wurde der echte Desktop-Einstieg mit `--dock-shell` visuell geprüft: drei Bereiche, ein Dokument-Tab, drei linke und zwei rechte Tool-Tabs, View-Menü mit den erwarteten Aktionen. „Output → Float“ wurde per UI-Menü ausgelöst, das separate Fenster geprüft und über dessen Schließen-Knopf geschlossen. Der anschließende App-Shutdown beendete den gestarteten Prozess. Dies ist kein Cross-Window-Maus-Redock-Nachweis.

Ein Zwischenlauf deckte einen Fehler des zunächst auf Factory-Ebene eingebauten Dokument-Schließschutzes auf: Er blockierte auch das Schließen des besitzenden Hauptfensters. Der Lauf unter `artifacts/g1-02/final/` meldete deshalb zwei fehlgeschlagene Cleanup-Checks und einen Timeout. Die endgültige Implementierung unterscheidet `CloseReason` im HostWindow; maßgeblich sind die anschließenden Prüfungen unter `artifacts/g1-02/verified/`. Der fehlgeschlagene Zwischenlauf wurde erhalten.

## Offene Punkte und Grenzen

1. **G1-03-Blocker:** Die bestehende produktive Editor-View koppelt Attach/Detach an Coordinator-, Drain- und Generationswechsel. Vor einer beweglichen produktiven Editor-View ist ein eigener abgenommener Lebenszyklusentwurf nötig. G1-02 ändert diese Verträge nicht.
2. **Linux/macOS:** Kein nativer Nachweis auf diesen Betriebssystemen. Die gesperrten RID-Restores ersetzen ihn nicht.
3. **Maus-Cross-Window-Redock:** Vollständiges Herausziehen und Wiederandocken zwischen getrennten OS-Fenstern ist weiterhin offen. Die nativen automatisierten Nachweise benutzen die produktiven Dock-Kommandos, nicht synthetische Cross-Window-Mausgesten. Die Beschränkung des verfügbaren UI-Werkzeugs auf ein Zielfenster bleibt bestehen.
4. **Bindingmeldungen:** Der instrumentierte native Lauf reproduziert die G1-01-Meldungen: RootDock wird für optionale Busy-/Dialog-/Confirmation-Overlays als `IHostOverlayServices` interpretiert; beim Freigeben treten außerdem Null-Bindings auf `FocusedDockable.Title`, `Window.Topmost` und `DockControl.Layout` auf. Keine gescheiterten geprüften Dock-/Fokus-/Cleanup-Operationen; dennoch kein warnungsfreier Lauf und kein Nachweis für diese unbenutzten Overlay-Funktionen. Kein Upstream-Fork und keine zusätzliche Overlay-Service-Architektur eingeführt.
5. **Hintergrundstart/Fokus:** Windows darf Vordergrundaktivierung verweigern. Der native Probe prüft den tatsächlichen Vordergrundbesitz und wartet initial höchstens 30 Sekunden auf Aktivierung. Ein verweigerter Fokus zählt als Fehler, nicht als bestanden. Keine globale Änderung der Windows-Aktivierungspolitik.
6. **XAML-Warnung:** `AVLN3001` für `DockShellWindow` ist erwartbar: Die DI-Shell besitzt absichtlich nur den Konstruktor mit Layout-Parameter und wird durch Prism gebaut. Ein parameterloser Konstruktor mit eigener State-Erzeugung wurde vermieden. Die kompilierten XAML-Pfade werden headless und nativ geprüft. Bestehende Nullability-/ViewModel-Warnungen bleiben erhalten.

Keine Änderungen an Parser, AST, Evaluator, Variables, Assignment L1, ScriptBook-Serialisierung, Admission, EditorCoordinator, InterpreterHostQueue, Enter-Ausführung oder Editor-Attach-/Detach-Verträgen. Keine Mehrdokumentfunktion, funktionale Tool-Migration oder Layout-Persistenz.

## Geänderte und neue Dateien

- `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj`: vier Dock-Paketreferenzen.
- `CryptoWorkBenchAvalonia/App.axaml.cs`: Preview-Auswahl, Ressourcen-/Fehler-Cleanup, Singleton-Registrierung; Bestandspfad erhalten.
- `CryptoWorkBenchAvalonia.Desktop/Program.cs`: begrenzter Startschalter; parameterloser Designer-Builder bleibt erhalten.
- `CryptoWorkBenchAvalonia/Shell/DockShellState.cs`: stabile Präsentationszustände.
- `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs`: Layout, Identitäten, Operationen, Lebensdauer.
- `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs`: Preview-Theme und Platzhalter-Views.
- `CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs`: Registrierung und Startauswahl.
- `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml` und `.axaml.cs`: Shell, Menü und Host-Cleanup.
- `CryptoWorkBenchGuiTest/DockShellTests.cs`: permanente Headless-Regressionen.
- `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`: echte Prism-Start-/Fenster-/Fallback-Prüfungen.
- `eng/gui/Validate-G1Shell.ps1`: reproduzierbarer Prüfablauf.
- `eng/gui/native/Program.cs`, `G1ShellProbe.csproj`, `packages.lock.json`: nativer Windows-Einstieg und eigener Lock.
- 14 betroffene Produkt-Lockdateien unter `eng/prism/locks/`, wie oben abgegrenzt.
- Dieser Bericht und `docs/gui/evidence/G1-02-results.json`.

G1-01-Dateien bleiben ungetrackt und unverändert erhalten. Neue G1-02-Dateien werden ebenfalls nicht gestaged. Abschlussstatus: **17 modifizierte getrackte Dateien** (drei Produktdateien, 14 Produkt-Locks), **28 ungetrackte Dateien** (14 erhaltene G1-01-Dateien, 14 neue G1-02-Dateien), kein Staging und unveränderter HEAD. Der vollständige Git-Status und die SHA-256-Prüfungen stehen in der Ergebnisevidenz.

`git diff --check`: Exit 0. Alle 28 ungetrackten Dateien zusätzlich gelesen und geprüft: keine leeren Dateien, keine nachgestellten Leerzeichen oder Tabs. Die drei NuGet-generierten lokalen Lockdateien haben formatbedingt keinen abschließenden Zeilenumbruch; zwei davon gehören zu den ausdrücklich unverändert erhaltenen G1-01-Dateien. JSON-Locks und Ergebnisevidenz sind parsebar; Produktcode, Tests, Skripte und Bericht enden mit Zeilenumbruch.

Die Umsetzung endet hier. G1-03 erfordert die ausdrückliche Eigentümerabnahme und einen gesonderten Auftrag.
