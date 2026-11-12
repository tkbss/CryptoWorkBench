# G1-A04-R3 – Partial Failure Reachability Audit

## 1. Entscheidung

**Ergebnis B – REACHABLE PARTIAL FAILURE FOUND**

**CHANGES REQUIRED. Keine Testentfernung, keine Produktkorrektur, kein A05-GO.**

Eine regulär zugelassene Float-Operation kann nach einer Strukturmutation durch eine vorhandene Produkt-/Bibliotheks-Exception abbrechen und ein ungültiges Layout mit weiterhin `LayoutFaulted == false` hinterlassen. Zwei konkrete Fehlerpfade sind im aktuellen Stand vorhanden:

1. **Auch ohne Diagnosemodus:** Native Host-Erzeugung schlägt fehl. Dock hat das neue Fenstermodell bereits registriert; dessen Owner-/Factory-/Host-Initialisierung ist noch nicht erfolgt.
2. **Mit dem vorhandenen produktiven `--dock-native-trace`:** Der synchrone Trace-Writer scheitert im vorhandenen DockableRemoved-Callback. Das Dokument ist bereits aus der Quelle entfernt und noch nicht im Ziel aufgenommen.

Beide Befunde stammen aus tatsächlichen Aufrufketten und realen Fehlerquellen. Sie benötigen keinen zusätzlich registrierten Exception-Handler. Die bedingte Entfernungserlaubnis für Ergebnis A greift deshalb nicht. `PartialMoveFailureMustLatchExistingLayoutFault` und die native Partial-Failure-Injektion bleiben unverändert aktiv.

Die Entscheidung aus R1 zu **unabhängigen zweiten Callback-Mutationen** wird dadurch nicht rückgängig gemacht. Ein vorhandener Callback kann eine Exception werfen, ohne selbst eine zweite Mutation anzustoßen. R1 hatte I/O-Exceptions des Trace-Pfads ausdrücklich von seiner B4-Frage abgegrenzt.

## 2. Stand, Methode und Beweisgrenzen

Audit am 2026-11-09 in `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Maßgeblich ist der vorhandene uncommittete Produktstand einschließlich A03 und R2.

Gelesene Grundlagen: [G1-A04-dock-operation-contract.md][s1], [G1-A04-R1-actual-callback-audit.md][s2], [G1-A04-R2-test-cleanup.md][s3].

Dock **12.1.0.13**, Quellcommit `78da173545501e796f37bc8528e9e55d9cbfd991`, sauberer lokaler Checkout. Avalonia **12.1.4**, vom lokalen Paket-Nuspec bestätigter Commit `2d79ba55fae1b1e71c69f93a668080c9029e6a31`. Die maßgeblichen Avalonia-Dateien wurden an diesem Commit gelesen; belegende Auszüge und unveränderliche Quell-URLs stehen in [avalonia-source-excerpts.json][s4].

Untersucht wurden zugelassene Shell- und Factory-Aufrufe, Manager-/Service-Drops, Move/Split/Float/Redock/Collapse, Native-Closing/Closed, Composite-Cleanup, vorhandene Produkt-Subscriber, Property-/Collection-Notifications, Presenter-Lifecycle sowie A02- und Editor-Fehlergrenzen. Die entscheidende Frage ist jeweils: **Was wurde bereits verändert, wenn die Exception entsteht, und wer stellt anschließend einen gültigen Zustand oder den Fault-Zustand sicher?**

Beweisarten werden getrennt:

- **Statischer positiver Erreichbarkeitsnachweis:** Abschnitte 3 und 4. Der Aufrufpfad und sein Zustand beim Abbruch ergeben sich aus dem unveränderten Code. Das Auftreten einer OS-/I/O-Fehlerbedingung setzt keine Codeänderung voraus.
- **Ausgeführte Diagnose:** Eine tatsächliche Windows-Dateisperre erzeugt eine IOException mit derselben StreamWriter/FileStream-Konfiguration wie der Produkt-Trace. Das bestätigt die I/O-Fehlerquelle, ist aber **kein vollständiger Dock-/Desktop-Reproduktionslauf**.
- **Unveränderter künstlicher Solltest:** Je ein gezielter Debug-/Release-Lauf bestätigt weiterhin den fehlenden Fault-Latch nach der künstlich ausgelösten Teilmutation. Dieser Test beweist für sich allein weiterhin keine produktive Erreichbarkeit.
- Eine native Ressourcenerschöpfung oder ein kompletter Float unter absichtlich fehlschlagender Host-Erzeugung wurde **nicht** auf dem Arbeitsdesktop provoziert. Es gibt keine Behauptung einer gemessenen Fehlerhäufigkeit oder eines vollständig dynamisch reproduzierten Win32-Pfads.

Für Ergebnis B genügt ein positiver konkreter Pfad. Es wird kein vollständiger Negativnachweis für alle übrigen möglichen Framework-Exceptions behauptet.

## 3. Positiver Pfad P1: Native Host-Erzeugung nach Registrierung

### 3.1 Regulärer Einstieg und Mutation

Aus einem gültigen, nicht maximierten Layout mit betriebsbereiter GUI-Session kann der Benutzer im vorhandenen Menü **View → CryptoScript → Float** auslösen. Die Menüregistrierung enthält auch das Dokument; kein Testcode ist dafür erforderlich.

| Schritt | Quellstelle | Zustand / Wirkung |
|---|---|---|
| Menü-Float → Layout.Float(document) | [DockShellWindow.axaml.cs:60][s5], [DockShellLayout.cs:98][s6] | CanChange/CanRelocate müssen erfüllt sein. Show aktiviert die vorhandene Pane; Float delegiert an die Factory. |
| EditorDockFactory.Change → Session.Relocate → Observe | [EditorDockFactory.cs:122][s7], [DockEditorSession.cs:100][s8] | Regulär zugelassene visuelle Mutation; kein MCP-/Editor-Ticket wird erzeugt. |
| FloatDockable → SplitToWindow | [FactoryBase.Dockable.cs:1080][s9], [FactoryBase.cs:811][s10] | Capability/Owner werden geprüft; anschließend beginnt die strukturelle Arbeit. |
| RemoveDockable(document, true) | FactoryBase.cs:822; [FactoryBase.Dockable.cs:41][s11] | Das Dokument wird aus der Quell-VisibleDockables-Liste entfernt. |
| CreateWindowFrom(document) | [FactoryBase.cs:636][s12]; Fensteranlage :783 | Neues DocumentDock/RootDock mit dem Dokument, neues DockWindow; window.Layout/root.Window werden gesetzt. Das Fenster hat noch keinen nativen Host und noch keine Fenster-Owner-/Factory-Zuordnung. |
| AddWindow(mainRoot, window) | [FactoryBase.Window.cs:38][s13] | **mainRoot.Windows.Add(window) erfolgt vor InitDockWindow.** |
| InitializeAddedWindow → InitDockWindow | FactoryBase.Window.cs:57; [FactoryBase.Init.cs:241][s14] | OnWindowAdded ist ein Ereignisraiser, keine Initialisierung. Die Host-Erzeugung wird als Argument für die zweite InitDockWindow-Überladung ausgewertet. |
| GetHostWindow → Produkt-Locator → new DocumentSafeHostWindow | [FactoryBase.Locator.cs:53][s15], [DockShellLayout.cs:32][s16] und :190 | Der produktive Locator erzeugt einen echten nativen Host. Keine ausgetauschte Factory, kein Test-Delegate. |

Die Reihenfolge in InitDockWindow ist entscheidend:

```text
InitDockWindow(window, owner,
    GetHostWindow(window.Id));         // kann hier abbrechen

// Erst in der aufgerufenen Überladung:
window.Host = hostWindow;
window.Host.Window = window;
window.Owner = owner;
window.Factory = this;
InitDockable(window.Layout, ...);
```

Der konkrete Mvvm-Factory-Code liefert `new DockWindow()`; Owner/Factory/Host sind zunächst null ([Factory.cs:113][s17], [DockWindow.cs:24][s18]). Kein vorheriger Schritt in diesem Pfad setzt diese Fensterfelder.

### 3.2 Tatsächliche Exception-Quelle

Die unveränderte native Kette lautet:

```text
DocumentSafeHostWindow → Dock.Avalonia.HostWindow → Avalonia.Window()
→ PlatformManager.CreateWindow()
→ Win32Platform.CreateWindow()
→ new WindowImpl()
→ WindowImpl.CreateWindow()
→ RegisterClassEx / CreateWindowEx
→ Win32Exception bei fehlgeschlagener nativer Erzeugung
```

Gepinnte Quellen:

- [Window.cs:243](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/Window.cs#L243): Basiskonstruktor ruft PlatformManager.CreateWindow auf.
- [PlatformManager.cs:27](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/Platform/PlatformManager.cs#L27) und [Win32Platform.cs:258](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Windows/Avalonia.Win32/Win32Platform.cs#L258): Weiterleitung an die tatsächliche Windows-Implementierung.
- [WindowImpl.cs:157](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Windows/Avalonia.Win32/WindowImpl.cs#L157), [:963](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Windows/Avalonia.Win32/WindowImpl.cs#L963) und [:1004](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Windows/Avalonia.Win32/WindowImpl.cs#L1004): native Erzeugung; explizite Win32Exception bei RegisterClassEx == 0 oder HWND == 0.

Das ist ein regulärer OS-Fehlerzweig. CreateWindowEx dokumentiert einen Null-Rückgabewert bei Fehler; Microsoft beschreibt fehlgeschlagene Fenstererzeugung bei erschöpften Desktop-Ressourcen. Das belegt die reale Fehlerklasse, nicht deren Auftreten in diesem konkreten Lauf. [CreateWindowExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw), [Desktop-Heap-Fehler](https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/desktop-heap-limitation-out-of-memory).

Der Nachweis stützt sich somit nicht auf ein beliebiges „jede Allokation könnte OutOfMemory werfen“, sondern auf einen vorhandenen nativen Fehlerzweig **hinter einer bereits erfolgten Dock-Mutation**. Er gilt ohne `--dock-native-trace`.

### 3.3 Zustand nach Abbruch und Fault-Verhalten

Wenn die Host-Erzeugung hier scheitert:

- Die Quellliste enthält das Dokument nicht mehr.
- Das neue Fenster steht bereits in mainRoot.Windows.
- Sein Layout und dessen Window-Rückreferenz existieren, aber **window.Owner, window.Factory und window.Host sind noch null**.
- Die anschließende Initialisierung der neuen Layout-Owner-/Factory-Beziehungen wurde nicht ausgeführt.
- Position/Größe/Present und erfolgreicher Abschluss von SplitToWindow werden nicht erreicht.
- Es gibt in dieser Kette keinen Rückbau der Fensterregistrierung, keine Rückführung der Pane und keinen Aufruf von MutationFailed.

Das ist nicht nur ein noch unsichtbares, aber gültig vorbereitetes Fenster: Der Live-Validator verlangt eine zur Hauptroot passende Fenster-Owner-/Factory-Verknüpfung. [DockLayoutStore.cs:235][s19] verwirft die zurückbleibende Registrierung mit `Invalid live floating ownership.`

EditorDockFactory.Change besitzt nur Depth-/Trace-finally-Blöcke, keinen Fault-Latch. Session.Relocate propagiert Fehler. Der tatsächlich verwendete Shell-ActionItem-Handler fängt die Exception und zeigt eine allgemeine Fehlermeldung ([DockShellWindow.axaml.cs:109][s20]); er korrigiert das Modell nicht. Damit kann die Anwendung mit dem ungültigen Zustand weiterlaufen. **LayoutFaulted bleibt false.**

## 4. Positiver Pfad P2: Produktiver Trace-Callback nach Remove

### 4.1 Vorhandene Registrierung

Der Desktop-Einstieg unterstützt bereits `--dock-shell --dock-native-trace <path>`: [Program.cs:25][s21]. NativeDockWindowTrace wird über den vorhandenen Diagnose-Initialisierer angelegt und am Shell-Fenster registriert.

[NativeDockWindowTrace.cs:39][s22] öffnet einen FileStream mit CreateNew/Write/FileShare.Read und einen StreamWriter mit **AutoFlush=true**. Attach registriert unter anderem:

```text
factory.DockableRemoved += (_, e) => Record("factory-dockable-removed", e);
```

Quelle: NativeDockWindowTrace.cs:85. Record serialisiert den Zustand und ruft synchron `writer.WriteLine(...)` auf (:137/:150). Weder dieser Handler noch Record fängt Schreibfehler ab. Zusätzlich ruft [DockLifecycleTrace.cs:9][s23] seinen produktiven Sink direkt auf.

### 4.2 Konkrete Fehlerfolge

Für denselben zugelassenen Dokument-Float:

```text
Layout.Float(document)
→ EditorDockFactory / FloatDockable / SplitToWindow
→ RemoveDockable
→ RemoveVisibleDockable: Quelle.VisibleDockables.Remove(document)
→ OnDockableRemoved
→ vorhandener NativeDockWindowTrace-Subscriber
→ Record → StreamWriter.WriteLine → AutoFlush → IOException
```

Entscheidende Reihenfolge: [FactoryBase.Dockable.cs:79][s24] entfernt zuerst und ruft danach OnDockableRemoved auf. [FactoryBase.Events.cs:166][s25] invokiert die Subscriber ohne Exception-Barriere. Erst nach Rückkehr aus RemoveDockable würde SplitToWindow das Zielmodell anlegen.

**Fehlerbedingung:** Der Trace war erfolgreich geöffnet und frühere Records konnten geschrieben werden; der spätere Record trifft beispielsweise auf einen Dateibereichs-Lock oder einen Laufwerks-/Schreibfehler. Die früheren Trace-Aufrufe vor Mutation müssen erfolgreich gewesen sein. Ein schon beim Öffnen oder beim ersten Preflight-Record auftretender Fehler wäre dagegen kein Partial-Failure-Nachweis.

Ein externer Leser darf wegen FileShare.Read geöffnet werden. Eine Windows-Bereichssperre kann einen späteren Schreibbereich betreffen, ohne frühere Writes zu verhindern. Es bedarf keines neuen Dock-Subscribers und keiner Produktänderung. Ein verlorener Schreibzugriff auf den Datenträger ist ebenfalls keine Eigenschaft, die die Dock-Admission vorab dauerhaft garantieren könnte.

**Zustand beim Abbruch:** Die verpflichtende Dokument-Pane ist aus der sichtbaren Quelle entfernt; das Ziel existiert noch nicht. Eine mögliche Auswahlkorrektur durch UI-Bindings behebt das fehlende Dokument nicht. Capture/Validate kann diesen Zustand nicht als zulässiges geschlossenes Tool akzeptieren: Das Dokument darf nicht in ClosedTools stehen ([DockLayoutStore.cs:250][s26]).

Die Change-/Observe-Grenzen stellen keinen Layout-Fault her. Ein weiterer Trace-Fehler im finally kann zusätzlich die ursprüngliche Exception verdecken; er repariert den Baum ebenfalls nicht. **LayoutFaulted bleibt false.**

### 4.3 Ausgeführte Windows-I/O-Diagnose

Mit derselben Writer-/Share-/AutoFlush-Konfiguration wurde in einer neuen R3-Datei zunächst erfolgreich geschrieben. Ein zweiter echter FileStream mit Lesezugriff sperrte danach einen Bytebereich am nächsten Schreiboffset; der folgende WriteLine warf auf Windows/.NET 10.0.14:

```text
System.IO.IOException
HRESULT 0x80070021 (gesperrter Dateibereich)
RandomAccess.WriteAtOffset
→ OSFileStreamStrategy.Write
→ BufferedFileStreamStrategy.Flush
→ StreamWriter.Flush
→ StreamWriter.WriteLine
```

Die Sperre wurde anschließend wieder gelöst und beide Streams entsorgt. Keine absichtlich werfende Dock-Ereignisroutine, kein Stream-Mock, kein ausgetauschter Writer. Nachweis: [trace-io-diagnostic.json][s27].

**Grenze dieser Diagnose:** Der Lock wurde kontrolliert erzeugt; die Diagnose prüft die echte I/O-Fehlerquelle isoliert. Sie synchronisiert keinen Lock mit einem laufenden Produkt-Float und wird nicht als dynamischer End-to-End-Beweis ausgegeben. Die Platzierung dieses Fehlers nach Remove und sein fehlender Fault-Latch sind der oben belegte statische Produktpfad.

## 5. Weitere untersuchte Ketten und Fehlergrenzen

| Bereich / Quellen | Mutation, Exception-Quelle und Behandlung | Bewertung |
|---|---|---|
| Cross-Dock Move / Redock, [FactoryBase.Dockable.cs:274][s28] | RemoveDockable erfolgt vor InsertVisibleDockable/InitDockable/ActiveDockable im Ziel. Derselbe produktive Removed-Trace kann dazwischen werfen. Kein Catch/Rollback in Move. | P2 gilt auch hier, sofern die konkrete Move-Zielkombination zugelassen ist. Der positive Dokumentnachweis oben benutzt bewusst einen tatsächlich angebotenen Float statt einen möglicherweise ungültigen Dokument→Tool-Drop. |
| Split, [FactoryBase.cs:391][s29] und :510; [DockService.cs:169][s30] | Manager/Service zerlegen den Drop in mehrere Factory-Aufrufe; Listen, Proportionen und Owner werden schrittweise geändert. Notifications/Init laufen innerhalb dieser Schritte. Ungültige Split-Enums besitzen NotSupportedException-Zweige. | Keine allgemeine atomare Grenze. Die regulären UI-Ziele liefern gültige Operationen; ein allein mit falschem Enum konstruierter Aufruf wird nicht als weiterer Produktnachweis gezählt. Reale Trace-/Host-Fehler bleiben relevant. |
| Factory-Init und Property-/CollectionChanged, [FactoryBase.Init.cs:63][s31], [DockBase.cs][s32], [FactoryBase.Dockable.cs:1718][s33] | Collection/Property-Änderungen lösen UI- und Modellbenachrichtigungen aus. Zuweisungen/Listenmutationen sind teilweise bereits erfolgt, wenn Benachrichtigungen laufen. InitActiveDockable und StateSync sind Fortsetzungen. | R1 fand keine unabhängige zweite Mutation. Daraus folgt keine Exception-Freiheit. Nicht jeder Avalonia-Bindingfehler propagiert; der P2-Eventpfad braucht aber kein Binding und hat nachweislich keine Catch-Barriere. |
| Produkt-Presenter, [DockShellPresentation.cs:54][s34] und :80; [DockEditorSession.cs:113][s35]; [CryptoScriptEditView.axaml.cs:48][s36] | Content lösen/neu zuordnen, BuildContent auf Attach. Editor-Attach bereinigt bei Fehler und wirft weiter; CreatePresenter prüft Thread/Disposed. PrepareForHostChange liegt vor action(), also kann ein dortiger Fehler vor der Dock-Mutation liegen. | Ein möglicher Lifecycle-Fehler muss zeitlich eingeordnet werden. Ein Disposed-/falscher-Thread-Start ist im regulär zugelassenen Pfad nicht automatisch ein positiver Fall. Keine globale Dock-Kompensation in diesen Helfern. |
| Native Present / Chrome, [HostAdapter.cs:42][s37], [HostWindow.axaml.cs:715][s38], [DockDocumentWindowChrome.cs:23][s39] | Show/Show(owner), Template/Chrome und Lifecycle folgen auf Modellmutationen. Avalonia prüft geschlossene Hosts/Owner und unsichtbare Owner; Chrome wirft bei fehlendem ContentTemplate. | Die Guard-Exceptions sind vorhanden. Ein unabhängiger Start aus gültigem Produktzustand mit gerade diesen ungültigen Vorbedingungen wurde hier nicht zusätzlich nachgewiesen. Sie werden nicht als Beweis für P1 benötigt; P1 betrifft die frühere reale native Erzeugung. |
| Collapse / RemoveWindow, [FactoryBase.cs:274][s40], [FactoryBase.Window.cs:74][s41] | Pane-/Splitter-/Dock-Entfernung kann bereits erfolgt sein, bevor window.Exit und anschließend Registry-/Owner-Bereinigung stattfinden. | Unter P2 kann ein Trace-Fehler Cleanup abbrechen. Ein zurückbleibender leerer Floating-Root wird vom Validator verworfen. Die exakte Empty-Host-Ausnahme ist eine Admission-Regel, kein Exception-Handler. |
| Native Closing/Closed, [HostWindow.axaml.cs:503][s42] und :562; [HostAdapter.cs:77][s43] | Closing ruft vorhandene Subscriber, Factory.OnWindowClosing und root.Close auf. Closed setzt zunächst _isClosed, löst Präsentation, ruft base.OnClosed und erst danach Factory-/Registry-Cleanup. HostAdapter.Exit setzt Tracking/Exiting zurück und wirft erneut. | Ein produktiver Trace-Subscriber kann zwischen diesen Schritten werfen. Teilweise native/visuelle Schließung ist keine Garantie abgeschlossener Modellbereinigung. HostAdapter schützt seinen lokalen Status, setzt aber weder LayoutFaulted noch stellt er den Gesamtbaum wieder her. |
| DockManager und Composite-Cleanup, [DockManager.cs:594][s44] und :631; [EditorDockFactory.cs:44][s45] | Mehrere Kinderoperationen, using-Scope, Complete und Dispose. Dispose setzt _compositeDrop=null und kollabiert die gesammelten Quellen über Change. | Kein Abort-/Rollback-Vertrag. Trace-/Hostfehler im Dispose können Cleanup nach bereits verschobenen Kindern beenden; ein Dispose-Fehler kann einen ursprünglichen Fehler überdecken. Ein erfolgreicher vorheriger Kind-Drop macht diesen Abschluss nicht exception-sicher. |
| Ungültige Modelle, [DockShellLayout.cs:66][s46], [DockContainerRoles.cs:21][s47], [DockLayoutStore.cs:173][s48] | VisibleTree wirft bei Zyklen/Mehrfachbesuch/Bounds; Rollenfindung verwendet SingleOrDefault; Validate prüft Ownership, Auswahl, Splitsequenz, Pflicht-Pane und Floating-Konfiguration. | Ein vorab künstlich korruptes Modell allein wäre kein gültiger Startnachweis. P1/P2 erzeugen jedoch aus einem gültigen Start einen nach diesen Regeln ungültigen Zustand. Spätere Validation erkennt ihn, setzt selbst aber keinen Fault-Latch. |

Für die im letzten Teil genannten Guard-/Template-/Modellfehler wird keine umfassende unabhängige Erreichbarkeit oder Nichterreichbarkeit behauptet. Diese Restunsicherheit ist **nicht** die Grundlage der Entscheidung: Die beiden positiven Fehlerpfade genügen bereits für B.

## 6. A02-Fehlerzustand und Editor-/MCP-Sicherheit

### A02 ist kein allgemeiner Dock-Exception-Schutz

[DockShellLayout.Persistence.cs:92][s49] kapselt Maximize/Restore und Teile von Reset mit spezifischer Kompensation und Verifikation. [DockLayoutAction.cs:63][s50] setzt LayoutFaulted ausschließlich, wenn diese Kompensation/Verifikation nicht erfolgreich ist.

Die normalen Move-/Float-/Split-/Collapse-Aufrufe benutzen diese MutationFailed-Grenze nicht. Change/ChangeLayout sind Admission-/Depth-Hüllen, keine allgemeine Zustandsverifikation. Capture erkennt ungültige Strukturen, setzt aber nicht automatisch LayoutFaulted.

Zusätzliche Begrenzung: [DockLayoutStore.cs:104][s51] nennt IOException, InvalidDataException, UnauthorizedAccessException, JsonException, NotSupportedException, ArgumentException und InvalidOperationException als Recoverable. **Win32Exception gehört nicht zu diesem Filter.** Auch ein vorhandener A02-Catch darf daher nicht pauschal als Schutz gegen native Fehler ausgegeben werden. Ein späteres Reset kann bereits am Capture des ungültigen Ausgangszustands scheitern, bevor seine Install-Kompensation erreicht wird.

### Editor und MCP

[EditorCoordinator.cs:347][s52] erhöht ObserverFailures, trägt gegebenenfalls einen Fehler in ein bereits vorhandenes Ticket ein, propagiert bei `propagate:true` und reduziert ObserverDepth im finally. DockEditorSession.Relocate erzeugt gerade kein neues Business-Ticket.

Im zugelassenen GUI-Dock-Pfad gilt daher:

- Während der Mutation bleiben die vorhandenen Observer-/MCP-/Editor-Sperren wirksam.
- Nach der Exception wird der Observer beendet; diese lokale Bereinigung ist keine Layoutreparatur.
- Der Session-eigene Editor wird durch den Visual-Detach nicht neu registriert oder automatisch als Business-Fault markiert. Seine Document-/Undo-Objekte können erhalten bleiben, obwohl die Präsentation fehlt.
- Da LayoutFaulted false bleibt, sperrt die Dock-Admission nicht wegen des beschädigten Layouts. Auch CanRelocate validiert nicht den Dock-Baum.
- Es ist **kein Autorisierungs-Bypass, keine geänderte Revision und kein Verlust von Editorinhalten nachgewiesen**. Das belegte Risiko ist der inkonsistente visuelle Zustand bei fehlendem Layout-Fault sowie nachfolgende fehlschlagende Layoutaktionen/Persistierung.
- Aus der fehlenden Layout-Sperre folgt nicht automatisch ein MCP-Sicherheitsdefekt: LayoutFaulted und EditorCoordinator-Fault sind getrennte Zustände. Eine spätere Korrektur darf diese Zuständigkeiten nicht ohne eigenen Nachweis vermischen.

## 7. Testinventar und ausgeführte Diagnose

### Keine Entfernung

| Gegenstand | Vor R3 | Nach R3 |
|---|---:|---:|
| Aktive DockOperationContractTests-Fälle | 7 | 7 |
| PartialMoveFailureMustLatchExistingLayoutFault | Aktiv, unverändert | Aktiv, unverändert |
| Native A04-Checks | 21 | 21 |
| Native Partial-Failure-Handler und Fault-Assertion | Aktiv | Aktiv, unverändert |
| Entfernte / hinzugefügte Testfälle | – | **0 / 0** |

Beide Testdateien und sämtliche anderen vorhandenen Quelldateien stimmen bytegleich mit dem R3-Startmanifest überein. Es wurden keine Assertions, Testattribute, Hilfsmethoden, Fixtures oder Testinstrumentierungen geändert.

### Neue R3-Läufe

Gezielt ausgeführt, jeweils `--no-build --no-restore`, Filter ausschließlich `PartialMoveFailureMustLatchExistingLayoutFault`:

| Konfiguration | Ausgeführt | Bestanden | Fehlgeschlagen | Nicht ausgeführt im gefilterten Lauf | Testprozess-Exit |
|---|---:|---:|---:|---:|---:|
| Debug | 1 | 0 | **1** | 0 | 1 |
| Release | 1 | 0 | **1** | 0 | 1 |

Beide Läufe scheitern an derselben unveränderten letzten Assertion: **LayoutFaulted erwartet true, tatsächlich false**. Die davorliegenden Prüfungen auf Originalexception, Observer-Cleanup, entfernte Pflicht-Pane und abgelehntes Capture bestehen. Der künstliche Handler bleibt eine kontrollierte Sollprüfung der Fehlergrenze; der Erreichbarkeitsbeleg kommt aus P1/P2.

Aufrufe/Exitcodes: [commands.json][s53]; ausgewertete Resultate: [targeted-results.json][s54]; [Debug-TRX][s55] und [Release-TRX][s56].

### Vollregressionen und native Windows-Prüfungen

Bei Ergebnis B verlangt der Auftrag nur notwendige Audit-/Diagnoseprüfungen. Es fand **keine erneute Vollregression** und keine neue native A04-/F03-Fenstermatrix statt; es gab keine Codeänderung, die deren Wiederholung begründet hätte. Die neue Windows-Prüfung in R3 ist die isolierte reale I/O-Fehlerdiagnose aus Abschnitt 4.3, keine neue native Dock-Abnahme.

Letzter unveränderter Vollregressionsstand aus **R2, nicht neu in R3 ausgeführt**:

| Suite | Debug | Release |
|---|---|---|
| GUI | 1.093 bestanden, 1 Fehler | 1.093 bestanden, 1 Fehler |
| Davon A03-GUI-Baseline | 1.087 bestanden | 1.087 bestanden |
| MCP | 176 bestanden, 13 nicht ausgeführt | 176 bestanden, 13 nicht ausgeführt |
| Core | 11.512 bestanden, 27 nicht ausgeführt | 11.512 bestanden, 27 nicht ausgeführt |
| Headless | 12 bestanden | 12 bestanden |
| Native A04 | Zwei Prozesse, jeweils 20 bestanden / 1 Fehler | Zwei Prozesse, jeweils 20 bestanden / 1 Fehler |

Die älteren zehn nativen Fokus-Timeouts aus der A04-Matrix bleiben historisch offen bzw. nicht abschließend geklärt. R3 bestätigt die frühere vollständige Native-/F03-Abnahme nicht erneut und erklärt diese Fehler nicht für bestanden. Ihre Details verbleiben unverändert im A04-Bericht, Abschnitt „Native Windows-Matrix“.

## 8. Empfehlung für eine gezielte Korrektur und G1-A05

Keine automatische Umsetzung in R3. Zur gesonderten Entscheidung empfohlen:

1. **Native Host-Anlage gezielt absichern:** Prüfen, ob Host-Erzeugung vor dem destruktiven Schritt vorbereitet werden kann oder ob der konkrete Fehlerpfad Registrierung/Pane-Beziehungen zuverlässig zurückführt. Wenn kein gültiger Abschluss verifiziert werden kann, muss der bestehende Layout-Fault ausgelöst werden. Ein bloßer generischer Menü-Catch reicht nicht.
2. **Produktive Diagnose von Mutationen entkoppeln bzw. ihre erwartbaren Schreibfehler abfangen:** Ein fehlgeschlagener Trace darf nicht die Dock-Operation abbrechen. Dabei Diagnoseverlust sichtbar und begrenzt behandeln; keine neuen Business-/MCP-Rechte schaffen.
3. **Composite-/Host-Cleanup in die gezielte Fehlerentscheidung einbeziehen:** Originalfehler erhalten und einen unvollständigen Abschluss erkennen; nicht nur ObserverDepth wieder auf null setzen.
4. Eine spätere Korrektur gegen reale Host-/I/O-Fehlerpfade und den bestehenden Partial-Failure-Vertrag prüfen. Die verworfene allgemeine B4-Fremdcallback-Anforderung wird dafür nicht wieder eingeführt.

**Empfehlung für G1-A05: noch keine Freigabe.** Zunächst gesonderte Eigentümerentscheidung zum gezielten Partial-Failure-Befund und seiner Behandlung; anschließend erforderliche Abnahme. Weder ein Dock-Fork noch eine neue allgemeine Transaktionsarchitektur folgt automatisch aus diesem Audit.

## 9. Änderungen, Evidenzintegrität und Abschluss

Einzige neue nicht ignorierte Datei: dieser Bericht `docs/gui/G1-A04-R3-partial-failure-audit.md`. Keine vorhandene Produkt-, Test-, Paket-Lock- oder Berichtdatei geändert. Neue Audit-Artefakte liegen ausschließlich unter `artifacts/g1-a04-r3/`.

Abschlussnachweise:

- **1.137 vorhandene Quelldateien bytegleich** zum R3-Startmanifest; null Änderungen, null Löschungen, nur der neue Bericht hinzugefügt.
- **1.631 A04-Artefakte, 7.654 frühere Artefakte und 77 R2-Artefakte:** jeweils null Hashabweichungen.
- Dock-Quellcheckout sauber, unveränderter Commit; Prism-Pin bei der Diagnoseausführung mit 1.180 gepinnten Dateien verifiziert.
- `git diff --check`: Exit 0, keine Befunde. Bericht zusätzlich auf Whitespace und lokale Quellenlinks geprüft.
- Der bereits zuvor geänderte G1-Worktree wird nicht als sauber ausgegeben. Abschließender vollständiger Git-Status: [final-git-status.txt][s57]; Delta/Hashes/Checks: [final-checks.json][s58] und [historical-integrity.json][s59]. Hauptcheckout bleibt sauber.

**Nach R3 gestoppt. Tests erhalten; CHANGES REQUIRED dem Projekteigentümer vorgelegt. Keine A05-Arbeit, kein Commit, Push oder Merge.**

[s1]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-dock-operation-contract.md>
[s2]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-R1-actual-callback-audit.md>
[s3]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-R2-test-cleanup.md>
[s4]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/avalonia-source-excerpts.json>
[s5]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs:60>
[s6]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:98>
[s7]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:122>
[s8]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs:100>
[s9]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Dockable.cs:1080>
[s10]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.cs:811>
[s11]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Dockable.cs:41>
[s12]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.cs:636>
[s13]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Window.cs:38>
[s14]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Init.cs:241>
[s15]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Locator.cs:53>
[s16]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:32>
[s17]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Factory.cs:113>
[s18]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Core/DockWindow.cs:24>
[s19]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:235>
[s20]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs:109>
[s21]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia.Desktop/Program.cs:25>
[s22]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs:39>
[s23]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs:9>
[s24]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Dockable.cs:79>
[s25]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Events.cs:166>
[s26]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:250>
[s27]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/trace-io-diagnostic.json>
[s28]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Dockable.cs:274>
[s29]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.cs:391>
[s30]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/DockService.cs:169>
[s31]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Init.cs:63>
[s32]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Core/DockBase.cs>
[s33]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Dockable.cs:1718>
[s34]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs:54>
[s35]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs:113>
[s36]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs:48>
[s37]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/Adapters/HostAdapter.cs:42>
[s38]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/HostWindow.axaml.cs:715>
[s39]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs:23>
[s40]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.cs:274>
[s41]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Window.cs:74>
[s42]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/HostWindow.axaml.cs:503>
[s43]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/Adapters/HostAdapter.cs:77>
[s44]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/DockManager.cs:594>
[s45]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:44>
[s46]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:66>
[s47]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs:21>
[s48]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:173>
[s49]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs:92>
[s50]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs:63>
[s51]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:104>
[s52]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:347>
[s53]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/targeted/commands.json>
[s54]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/targeted-results.json>
[s55]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/targeted/PartialFailure-Debug.trx>
[s56]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/targeted/PartialFailure-Release.trx>
[s57]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/final-git-status.txt>
[s58]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/final-checks.json>
[s59]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/historical-integrity.json>

