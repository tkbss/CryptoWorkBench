# G1-01 – Dock.Avalonia Foundation Feasibility

Stand: 5. November 2026, Windows x64. **Entscheidung: CONDITIONAL GO.**

Dock.Avalonia ist mit .NET 10, Avalonia 12.1.4 und der unveränderten lokalen Prism-Integration
praktisch einsetzbar. Ein isolierter Prototyp, zwölf Headless-Tests je Konfiguration und
60 bestandene native Windows-Prüfungen je Konfiguration belegen Layout, Identität, DI,
Editor-Reparenting, native Floating Windows, Fokus, minimalen Layout-Roundtrip und Freigabe.
Die vollständigen bestehenden Standardtests zeigen keine Regression gegenüber der Baseline.

Ein uneingeschränktes GO wäre wegen fehlender Linux-/macOS-Desktopnachweise, nicht vollständig
geprüfter mausbasierter Cross-Window-Drop-Ziele und der noch ausstehenden produktiven
Editor-/MCP-Lebenszyklusanbindung nicht gerechtfertigt. G1-02 wurde nicht begonnen.

## 1. Ausgangsstand und Isolation

- Ausgangscommit: `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`,
  `Remove obsolete asymmetric CryptoScript infrastructure`.
- Hauptworktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench`, Branch `master`,
  zu Beginn sauber. `HEAD`, lokales `master`, `origin/master` und die lesend abgefragte
  Remote-Referenz `refs/heads/master` stimmten überein. Kein Fetch/Reset/Branchwechsel am Hauptworktree.
- Bereits vorhanden: `artifacts/worktrees/script-assignment`, Branch `feature/script-assignment`,
  gleicher HEAD. Dieser Worktree wurde nicht verändert oder bereinigt.
  Bei der abschließenden lesenden Worktree-Abfrage stand dieser fremde Branch inzwischen auf
  `9a71065`; laufende Arbeit außerhalb von G1 wurde weder übernommen noch zurückgesetzt.
- Neu: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1`,
  Branch `feature/gui-g1`, durch `git worktree add -b feature/gui-g1 artifacts/worktrees/gui-g1 <SHA>`.
  Alle Arbeitsdateien und Build-/Forschungsartefakte liegen in diesem Worktree.
- Kein Commit, Push, Merge oder destruktiver Git-Befehl. Gemeinsame Git-Metadaten wurden lediglich
  für die ausdrücklich angeforderte Branch-/Worktree-Anlage ergänzt.
- SDK `10.0.401`, Runtime `.NET 10.0.12`, Windows-Kernelbuild `10.0.26200`.
  `dotnet --info` scheiterte innerhalb der Sandbox teilweise an Service-Control-Manager-Rechten;
  die freigegebenen Builds und ausführbaren Tests liefen anschließend erfolgreich.
- Prism bleibt auf dem offiziellen Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`.
  `eng/prism/Enter-Prism.ps1` / `Build.ps1` prüften jeweils 1.180 Quellfiles; keine Änderungen
  an Prism, dessen Hooks, Allowlist, Lockfiles oder der NuGet-Konfiguration.

Die Baseline-Builds beider Konfigurationen und alle unten genannten Bestandstests liefen im
frischen Worktree gegen den unveränderten Produktstand. Neue Prototypdateien gehören nicht
zu den bestehenden Solutions oder Tests und beeinflussten die Baseline nicht.

## 2. Offizielle Pakete und Versionsentscheidung

Die live gelesenen offiziellen NuGet-v3-Indizes lieferten `12.1.0.13` als jüngste stabile
Version der untersuchten Dock-Pakete. Veröffentlichung von Dock.Avalonia:
`2026-10-28T14:05:45.003Z`, `listed=true`. Paket-Nuspec und Release-Tag verweisen auf
Dock-Commit `78da173545501e796f37bc8528e9e55d9cbfd991` (`v12.1.0.13`).
Suchmaschinen hatten teilweise ältere Stände; maßgebend sind die NuGet-v3-Metadaten und
der tatsächlich erfolgreich wiederhergestellte, gesperrte Paketgraph.

| Paket | Version | Verwendung / Befund |
|---|---|---|
| Dock.Avalonia | 12.1.0.13 | DockControl, Drag/Drop, native HostWindow; net8.0/net10.0, Avalonia >= 12.1.4 |
| Dock.Avalonia.Themes.Fluent | 12.1.0.13 | Theme; gleiche Avalonia-/Dock-Linie |
| Dock.Model.Mvvm | 12.1.0.13 | Gewähltes Layoutmodell; CommunityToolkit.Mvvm 8.4.0 |
| Dock.Serializer.SystemTextJson | 12.1.0.13 | Gewählter Layoutserializer mit generierten Typmetadaten |
| Dock.Model.Prism | 12.1.0.13 | Untersucht, nicht im Prototyp; Dock.Model + Prism.Core >= 9.0.537 |
| Dock.Serializer.Newtonsoft | 12.1.0.13 | Untersucht, nicht benötigt; Kompatibilitäts-API über SystemTextJson, zusätzlich Newtonsoft.Json 13.0.4 |
| Avalonia.Desktop, Themes.Fluent, Markup.Xaml.Loader | 12.1.4 | Direkte Referenzen, passend zur Produktkonfiguration |
| Avalonia.AvaloniaEdit | 12.0.0 | Identische Editorversion wie im Produkt |
| Avalonia.Headless | 12.1.4 | Nur Testprojekt |
| Microsoft.NET.Test.Sdk / NUnit / NUnit3TestAdapter | 18.9.0 / 4.6.1 / 6.3.0 | Gleiche Testplattform wie im Repository |

Die transitiven Dock-Pakete `Dock.Model`, `Dock.Settings`, `Dock.MarkupExtension`,
`Dock.Controls.DeferredContentControl`, `Dock.Controls.ProportionalStackPanel`,
`Dock.Controls.Recycling` und `Dock.Controls.Recycling.Model` lösen einheitlich auf
`12.1.0.13` auf. Avalonia-, Win32-, X11-, Native-, Skia-, HarfBuzz-, FreeDesktop- und
Remote.Protocol-Runtimepakete lösen auf `12.1.4` auf. Keine `.v11`-Dock-Pakete,
keine Avalonia-11-Runtime, keine Nightlies. `Avalonia.BuildServices 11.3.2` ist die
transitive Buildwerkzeugversion und keine Avalonia-11-Dock-/UI-Runtime.

Die bestehenden Prism-Projektreferenzen ziehen unverändert `Prism.Container.Abstractions`
und `Prism.Container.DryIoc 9.0.114` sowie `DryIoc.dll 5.4.3` ein. Prism selbst kompiliert
gemäß seinem unveränderten Quellstand mit Avalonia 12.1.1; im Prototyp-Runtimegraph gewinnt
die explizite 12.1.4-Linie. Vollständige transitive Versionen und Content-Hashes stehen in
den beiden neuen `packages.lock.json`; eine kompakte Paketliste steht in der Evidence-JSON.

### Dock.Model.Prism

Das Paket stellt Prism-basierte Dock-Modelle bereit (`ReactiveBase : Prism.Mvvm.BindableBase`),
ersetzt aber keinen RegionAdapter und installiert keine Prism-Shell. Auch das offizielle
DockPrismSample ist eine normale Avalonia-Application und kein Nachweis für unsere lokale
PrismApplication-/Regionkonfiguration.

Ein separates Restore-Experiment unter `artifacts/g1/prism-package-probe` mit
`Dock.Model.Prism 12.1.0.13` und lokalem Prism.Core war erfolgreich: Der Graph enthielt
`Prism.Core/1.0.0` und `Prism.Events/1.0.0` als **Project**, nicht eine zweite Prism.Core-DLL
aus NuGet. Die anfängliche Vermutung eines zwingenden Restore-Konflikts bestätigte sich
also nicht. Laufzeit-/ABI-Kompatibilität dieses alternativen Modellpakets wurde nicht
nachgewiesen. Es wird nicht benötigt: Dock.Model.Mvvm plus vorhandene Prism-DI hat den
vollständigen hier beschriebenen Laufzeitnachweis und vermeidet die zusätzliche Kopplung.

### Quellen

- [Offizielle Dock.Avalonia-Versionen](https://api.nuget.org/v3-flatcontainer/dock.avalonia/index.json),
  [Release-Registrierung](https://api.nuget.org/v3/registration5-semver1/dock.avalonia/12.1.0.13.json).
- [Dock.Avalonia-Nuspec](https://api.nuget.org/v3-flatcontainer/dock.avalonia/12.1.0.13/dock.avalonia.nuspec),
  [Dock.Model.Prism-Nuspec](https://api.nuget.org/v3-flatcontainer/dock.model.prism/12.1.0.13/dock.model.prism.nuspec).
- [Verifizierter Release-Quellstand](https://github.com/wieslawsoltes/Dock/tree/78da173545501e796f37bc8528e9e55d9cbfd991).
- [Native Fensterbesitzer](https://wieslawsoltes.github.io/Dock/articles/dock-window-owner.html),
  [Managed Windows](https://wieslawsoltes.github.io/Dock/articles/dock-managed-windows-guide.html),
  [Layout-Serialisierung](https://wieslawsoltes.github.io/Dock/articles/dock-serialization.html).

Die lokal zur Prüfung geladene Dock-Quelle und Nuspecs bleiben unter `artifacts/g1/`;
sie sind Forschungsartefakte, keine produktive Quellintegration.

## 3. Prototyp und Eigentümerschaft

`prototypes/G1DockFeasibility/` ist ein eigenes ausführbares net10.0-Projekt mit separatem
NUnit-Projekt. Keine Referenz auf Produkt-MainWindow, Produkt-Views, CryptoScript oder MCP.

```text
Lokale PrismApplication / DryIoc
  ├─ WorkspaceState (Singleton: gemeinsam für beide Tools)
  ├─ DocumentSession (ein Demo-Dokument: TextDocument + Caret + Lebenszykluszähler)
  ├─ VariablesViewModel / DiagnosticsViewModel (Singletons)
  └─ ProbeWorkspace / ProbeFactory
       └─ ContentControl-Region "ProbeDockHost" → ein DockControl
            ├─ DocumentDock → document:cryptoscript:sample
            ├─ ToolDock → tool:variables
            └─ ToolDock → tool:diagnostics
```

Prism löst den Workspace und seine Konstruktorabhängigkeiten auf. Dock-`ContextLocator`
bindet stabile Dockable-IDs an diese bereits vorhandenen Objekte. Die ViewTemplates erzeugen
visuelle Controls, aber keine neuen logischen Dokumente oder globalen Zustände.
Der Workspace besitzt die Session; Detach räumt nur die visuelle Bindung auf, nicht die Session.
Shutdown ist idempotent und beendet alle Floating-Hosts.

Die DocumentSession hält das `TextDocument`. Ein Editor setzt es bei Attach und löst es
bei Detach wieder, nachdem die Caretposition gesichert wurde. Fokus wird nach dem Einhängen
auf `TextArea` gesetzt; ein Generationstoken verhindert veraltete gepostete Fokusaktionen.
Dock darf Controls recyceln oder neu erzeugen. Die Tests verlangen dieselbe **logische**
Session und an den geprüften Float-/Redock-Grenzen genau eine angehängte Editoransicht, keine
unrealistische Garantie einer einzigen visuellen Controlinstanz über alle Layoutwechsel.

Die Demo registriert genau eine Session als Singleton. Eine spätere DocumentRegistry mit
einer Session je Dokument-ID kann dieselbe Factory/ContextLocator-Grenze verwenden. Die
globale Tool-/Workspace-Lebensdauer bleibt davon unabhängig; neue Produkt-Tabs wurden nicht gebaut.

## 4. Reproduzierbare Builds und Tests

Siehe [Start- und Testanleitung](../../prototypes/G1DockFeasibility/README.md).

```powershell
pwsh -NoProfile ./prototypes/G1DockFeasibility/Validate.ps1 -Native -IncludeRegression -EvidenceDirectory artifacts/g1/new-run
```

Der Wrapper verwendet neue Evidence-Verzeichnisse, protokolliert exakte Argumente und Exitcodes,
verwendet die bestehenden Prism-Hooks und prüft die neuen Lockfiles mit `--locked-mode`.
Ohne `-Native` laufen keine OS-Fenstertests. Die bestehenden Solutions wurden zusätzlich
mit `eng/prism/Build.ps1 -Configuration Debug` beziehungsweise `Release` gebaut.

| Suite | Konfiguration | Baseline Passed / Failed / Skipped / nicht ausgeführt | Danach Passed / Failed / Skipped / nicht ausgeführt |
|---|---|---|---|
| GUI | Debug | 797 / 0 / 0 / 0 | 797 / 0 / 0 / 0 |
| GUI | Release | 797 / 0 / 0 / 0 | 797 / 0 / 0 / 0 |
| MCP | Debug | 176 / 0 / 0 / 13 | 176 / 0 / 0 / 13 |
| MCP | Release | 176 / 0 / 0 / 13 | 176 / 0 / 0 / 13 |
| Core | Debug | 11.508 / 0 / 26 / 1 | 11.508 / 0 / 26 / 1 |
| Core | Release | 11.508 / 0 / 26 / 1 | 11.508 / 0 / 26 / 1 |
| G1-Prototyp | Debug | neu | 12 / 0 / 0 / 0 |
| G1-Prototyp | Release | neu | 12 / 0 / 0 / 0 |

TRX bezeichnet sowohl Skips als auch explizit nicht gestartete Tests als `NotExecuted`.
Die 13 MCP-Fälle sind `[Explicit]`-Codex-/Desktop-Integrationstests mit zusätzlichen
Installations-/Pairing-/Ausführungsbedingungen, keine ausgeführten Erfolge. Im Core sind
26 Fälle wegen fehlender Symlink-Rechte beziehungsweise POSIX-/macOS-Voraussetzungen
übersprungen und `MeasureLocalHardware` explizit nicht gestartet. Vollständige Namen und
Gründe sind in der Evidence-JSON enthalten. Es wurden weder OS-Rechte noch Sicherheitseinstellungen geändert.

Beide vorhandenen Solutions: Baseline und Regression jeweils Build erfolgreich, null Fehler.
Die Avalonia-Solution meldete in der Baseline 84 Warnungen (Debug) und 90 (Release), bei
der Regression jeweils 84. Die sechs zusätzlich in der Release-Baseline ausgegebenen
CS8603-Warnungen betreffen unveränderte Core-Dateien (`Tr31Header`, `BlockHeaderVariableDeclaration`,
`VariableDictionary`); beim inkrementellen Folgebuild wurden sie nicht erneut ausgegeben.
Die Root-Solution meldete jeweils null Warnungen.
Die finalen Prototyp-/Testbuilds und gesperrten Restores bestanden in beiden Konfigurationen;
kein neuer C#-Compilerfehler oder NU1605-/Avalonia-11-Mischgraph.

Die zwölf gezielten Tests prüfen Layoutstruktur und Identitäten; Prism-Singletons;
20 Tool-Move-Zyklen; Layout-Roundtrip mit Context-Rebind; zehn Editor-Float/Redock-Zyklen;
Aktivierung; Floating-Layout-Roundtrip; Zurückweisung doppelter Layout-IDs ohne Verlust des
aktuellen Layouts; Garbage Collection einer abgelösten Ansicht; Tool-Schließen ohne Verlust
des Singletons; Rückholen einer vollständig abgedockten Toolgruppe; Shutdown/Detach/Freigabe.

## 5. Native Windows-Nachweise und Plattformgrenzen

Finale Binärdateien: Debug und Release jeweils **60 Checks bestanden, 0 fehlgeschlagen**,
`UsePlatformDetect`, `headless=false`. Ergebnisse unter `artifacts/g1/verified/native-*.json`
und vollständig eingebettet in [G1-01-results.json](evidence/G1-01-results.json).

- Haupt- und Floating-Fenster besitzen unterschiedliche echte Plattformhandles.
- Avalonia-Owner und Win32-`GetWindow(..., GW_OWNER)` entsprechen dem Hauptfenster.
- Floating-Fenster lassen sich rechts außerhalb der Hauptfenstergrenze positionieren.
- Fünf Zyklen prüfen Text, Caret, unverändertes Dockable/Document, Avalonia-TextArea-Fokus,
  Fensteraktivierung und tatsächlichen Win32-Foreground-Handle.
- Redock schließt das native Floating-Fenster; Fokus kehrt zum Editor im Hauptfenster zurück.
- Tool-Schließen entfernt den Host, während Prism-ViewModel und globaler Zustand bestehen bleiben.
- Dateibasierter JSON-Layout-Roundtrip stellt dieselben logischen Context-Objekte wieder her.
- Hauptfenster-Schließen mit noch offenem Editor-Floating-Fenster schließt alle Hosts.
  Je Lauf: 14 Attach, 14 Detach, 13 erstellte visuelle Views, null verbleibende Attachments,
  null registrierte DockControls/HostWindows, genau eine Session-Freigabe.

Zusätzlich per Computer Use sichtbar geprüft: drei Bereiche, dargestellter AvaloniaEdit-Text,
gemeinsame Workspace-ID beider Tools, Herausziehen des Variables-Kopfes per Maus in ein
echtes natives Toolfenster sowie Schließen des Hauptfensters samt Floating-Fenster.
**Nicht nachgewiesen:** vollständiger mausbasierter Redock auf sämtliche Adorner-/Drop-Ziele.
Der versuchte Cross-Window-Drag wurde vom Computer-Use-Werkzeug wegen eines Zielpunkts
außerhalb des ausgewählten Fensters abgewiesen. API-Redock und echter nativer Host-Lebenszyklus
sind getestet; diese Grenze wird nicht als bestandener Maus-Redock umgedeutet.

Frühere Native-Läufe sind ebenfalls erhalten. Ein unvorbereiteter Prism-Regionzugriff musste
auf `RegisterViewWithRegion` umgestellt werden; feste kurze Wartezeiten waren für deferred
Dock-Inhalte unzureichend. Die endgültige Probe wartet begrenzt auf den realisierten Editor.
Bei Hintergrundstarts bestanden später 54 Checks, fünf Win32-Foreground-Checks schlugen fehl,
weil eine andere Anwendung im Vordergrund blieb, obwohl Avalonia `IsActive=true` meldete.
Der finale Test wartet maximal 30 Sekunden auf echte Hauptfensteraktivierung und protokolliert
bei ausbleibender Aktivierung weiterhin einen Fehler. Die abschließenden aktiven Desktopläufe
bestanden vollständig; beliebiges Stehlen des Foregrounds aus einem Hintergrundprozess wird nicht zugesichert.

Native Diagnoseausgabe enthält je finalem Lauf 90 Binding-Meldungen: hauptsächlich Dock-Host-
Overlays (`IRootDock` lässt sich nicht als `IHostOverlayServices` binden) sowie temporär null
`Document.Text`/`Window.Topmost` bei Detach/Close. Die geprüften Funktionen bestehen trotzdem.
Dock-eigene Dialog-/Confirmation-Overlays sind nicht Teil des Prototyps; ihre Integration
darf daraus nicht als geprüft gelten.

| Plattform | Ergebnis |
|---|---|
| Windows x64, Build 26200 | Headless, Debug/Release, native Handles/Owner/Position/Fokus/Shutdown geprüft |
| Linux | Nicht ausgeführt: `wsl --list --quiet` meldet WSL nicht installiert; kein anderer Linux-Runner verfügbar |
| macOS | Nur Abhängigkeitsprüfung: Avalonia.Native/Skia-Runtimeassets vorhanden; kein macOS-Runner, kein nativer Erfolg behauptet |
| Weitere Monitore / DPI / Wayland / X11 / macOS WindowServer | Nicht nativ geprüft; keine Multi-Monitor- oder plattformübergreifende Fokusgarantie |

Managed Floating Windows sind laut offizieller Implementierung In-App-MDI-Controls,
ohne unabhängige OS-Fenster, Taskbar-/Owner-Beziehungen oder Positionierung außerhalb des
Hauptfensters. Im Prototyp sind sie deaktiviert; `HostWindowLocator` erzeugt `HostWindow`.
Sie ersetzen den hier nachgewiesenen nativen Pfad nicht und wurden nicht separat getestet.

## 6. Architekturbeurteilung und Empfehlung für G1-02

**Prism bleibt DI- und Navigationsframework.** Ein gewöhnlicher ContentControl-Regionhost
kann einen DockControl aufnehmen. Innerhalb dieses Hosts verwaltet Dock Layout, Auswahl,
Fenster und Drag/Drop; Prism verwaltet Services, Anwendungslaufzeiten und Navigation außerhalb
des Dockbaums. Ein Dock-Modell ist kein View, ein DockControl kein automatisch adaptierter
Prism-Regionmanager und `Dock.Model.Prism` kein Regionadapter.

Die einfachste belastbare G1-02-Integration ist deshalb die geprüfte Kombination aus
**Dock.Model.Mvvm + lokaler Prism-DI + einem dauerhaften Dock-Host**. Kein eigenes
Prism-Regionobjekt pro umhängbarem Dockable und kein Navigationsaufruf auf jeder Drag-Bewegung.
Region-Navigation innerhalb verschiebbarer Tool-Views und deren ScopedRegionManager-Lebensdauer
sind gesondert zu prüfen, bevor solche verschachtelten Regions eingebaut werden.

Tool-Views können globale Singletonzustände verwenden, ohne sie bei Reparenting neu anzulegen.
Ein Dokument kann dieselbe logische ID und dasselbe TextDocument behalten, selbst wenn Dock
neue Controls oder beim Restore neue Layoutmodelle erzeugt. Layout-JSON enthält weder den
Editorinhalt noch die Workspace-/Interpreterobjekte; die Bindung erfolgt über bekannte IDs.
Der verwendete Serializer unterstützt die benötigten Referenzen und Floating-Layouts.
Andere verfügbare Formate sind XML, YAML und Protobuf; diese wurden nicht getestet.
Formatversionierung, Migration, Fehlerrecovery und persistente Dokumentwiedereröffnung gehören zu G1-07.

**Wesentliches Produktintegrationsrisiko:** `CryptoScriptEditView` ruft heute bei Visual-Attach
`AttachEditor()` und bei Detach `DetachEditor()` auf. `EditorCoordinator.Detached` beginnt
Drain/Unavailable; ein neues Attach kann Generationen ändern oder bei aktiven/pending/MCP-
Operationen abgewiesen werden. Ungeprüftes Reparenting der bestehenden View kann damit
ausstehende Ausführungen, MCP-Identität, Intents oder den kontrollierten Shutdown beeinflussen.
Die hier grünen Regressionen sind kein Nachweis für bereits migrierte Produkt-Views.

G1-02 benötigt vor dem Umhängen produktiver Editoren eine bewusste Grenze zwischen
Dokument-/Editorregistrierung und visueller Präsentation, einschließlich Zulässigkeit von
Dock-Operationen bei laufenden Jobs, alten Callbacks, Completion-Popups und Fokuswechseln.
Dieser Adapter muss bestehende Admission-/Drain-Garantien erhalten. In G1-01 wurden
EditorCoordinator, MCP-Admission und InterpreterHostQueue weder geändert noch umgangen.

Dock hat im Prototyp keinerlei Autorität über Interpreter, Workspace-Inhalt oder Ausführung.
Es darf auch später nur Präsentationszustand besitzen. Mehrdokumentunterstützung passt zur
ID-/Session-Grenze, benötigt aber eigene Admission-/Auswahlregeln und wird nicht durch eine
bloße zusätzliche Tab-Instanz automatisch sicher. Kein grundlegender Dock-/DI-Wechsel ist dafür notwendig.

## 7. Lebenszyklusdetails und offene Punkte nach Schweregrad

| Priorität | Befund / erforderliche Folgemaßnahme |
|---|---|
| Hoch – vor Produkt-Editor-Migration | Visual-Detach ist heute fachlich wirksam für Coordinator/MCP. Adapter und Tests für laufende Jobs/Drain/Registrierung erforderlich; keine produktive Migration durch G1-01 freigegeben. |
| Mittel – fehlender Plattformnachweis | Linux/macOS native Window-/Focus-/Close-Tests sowie Multi-Monitor/DPI fehlen. |
| Mittel – Interaktion | Maus-Redock über Fenstergrenzen und vollständige Adorner-/Drop-Zielmatrix noch manuell/nativ prüfen. |
| Mittel – Theme/Overlay | Host-Overlay-Bindingmeldungen untersuchen, bevor Dock-Dialog-/Confirmationservices verwendet werden; derzeit Prism-Dialogzuständigkeit beibehalten. |
| Niedrig – OS-Aktivierung | Hintergrundstart kann Win32-Foreground-Wechsel verweigern. Tests brauchen ein wirklich aktives Testfenster; `IsActive` allein genügt nicht als Nachweis. |
| Niedrig – Layoutmodelle | Tool implementiert auch IDocument; fachliche Dokumente nicht allein per `is IDocument` zählen. |
| Niedrig – Gruppen/IDs | Floating-Container können Ursprungs-IDs übernehmen. Rückkehranker im Hauptbaum suchen; nach Whole-Group-Drag gegebenenfalls neu anlegen und leere, nicht kollabierbare Floating-Hosts explizit entfernen. Im Prototyp getestet. |
| Niedrig – Persistenz | Restore erzeugt Layoutmodelle neu. Contexts erst über bekannte IDs anbinden; kein DI-/Dokumentzustand in Layout-JSON. Vollständige Persistenz bleibt G1-07. |

Die Ressourcenprüfungen umfassen tatsächliche Host-/Control-Abmeldung, getrennte
TextDocument-Bindungen und einen WeakReference-/GC-Test nach Layoutablösung. Sie sind kein
Langzeit-Leak- oder Stressnachweis über Stunden. Ein beim Redock noch benutztes recyceltes
Control ist erwartungsgemäß nicht sammelbar; der GC-Test prüft ausdrücklich eine durch
Layoutwechsel abgelöste Ansicht, während ihre logische Session weiterlebt.

## 8. Evidenz und vollständige Dateiliste

Versionierbare, maschinenlesbare Zusammenfassung inklusive Testnamen, Nichtausführungsgründen,
nativen Einzelchecks, Paketgraph und SHA-256 der Prototypquellen:
[evidence/G1-01-results.json](evidence/G1-01-results.json).

Lokale Rohdaten (ignorierte Arbeitsartefakte, nicht Teil eines künftigen Commits):
`artifacts/g1/baseline/`, `regression/`, `verified/`, frühere Probeläufe `final*/`,
`research/`, `prism-package-probe/`, `dock-source/`. `verified/commands.json` erfasst
die finalen Locked-Restores und Headless-Läufe; die finalen nativen EXE-Läufe wurden anschließend
separat mit `--native-probe artifacts/g1/verified/native-<Configuration>.json` ausgeführt.

Alle versionierbaren Änderungen sind **neue Dateien**:

1. `docs/gui/G1-01-dock-feasibility.md`
2. `docs/gui/evidence/G1-01-results.json`
3. `prototypes/G1DockFeasibility/G1DockFeasibility.csproj`
4. `prototypes/G1DockFeasibility/Program.cs`
5. `prototypes/G1DockFeasibility/Session.cs`
6. `prototypes/G1DockFeasibility/ProbeWorkspace.cs`
7. `prototypes/G1DockFeasibility/Views.cs`
8. `prototypes/G1DockFeasibility/NativeProbe.cs`
9. `prototypes/G1DockFeasibility/Validate.ps1`
10. `prototypes/G1DockFeasibility/README.md`
11. `prototypes/G1DockFeasibility/packages.lock.json`
12. `prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj`
13. `prototypes/G1DockFeasibility/Tests/DockTests.cs`
14. `prototypes/G1DockFeasibility/Tests/packages.lock.json`

Abschlussprüfung: `git diff --check` erfolgreich; zusätzlich Whitespaceprüfung der neuen,
noch ungetrackten Textdateien, da normales `git diff` diese nicht einschließt. `git diff
--name-only` leer. `git status --short` zeigt ausschließlich neue `docs/gui/`- und
`prototypes/`-Dateien; Branch/HEAD weiterhin `feature/gui-g1` / Ausgangscommit.
Keine bestehenden Produkt-, Prism-, NuGet-, Parser-/Evaluator-, VariableState-,
ScriptBook-, Assignment-, Output-/Diagnostics- oder MCP-Dateien geändert.

**Übergabe:** Prototyp, Tests, Lockfiles, Bericht und Evidenz verbleiben uncommitted im
isolierten G1-Worktree. CONDITIONAL GO für den Kandidaten und die beschriebene Integrationsrichtung;
G1-02 erst nach ausdrücklicher Eigentümerabnahme.
