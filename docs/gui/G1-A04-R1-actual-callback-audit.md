# G1-A04-R1 – Actual Callback Reentrancy Audit

## Entscheidung

**NO REACHABLE B4 FOUND**

Im vollständig untersuchten, unten abgegrenzten aktuellen G1-Produkt- und gepinnten Bibliotheksstand wurde kein tatsächlich erreichbarer Callback gefunden, der während einer Dock-Mutation eine **unabhängige zweite Dock-Mutation** startet. Es gibt reale synchrone Rückrufe und verschachtelte strukturelle Aufrufe. Die erreichbaren strukturellen Folgeaufrufe gehören jedoch zur laufenden Operation; andere verdächtige Rückkopplungen sind durch konkrete Zustandsbedingungen unterbrochen.

Empfehlung an den Projekteigentümer: Die zusätzliche B4-Anforderung zum Schutz gegen beliebig injizierte unabhängige Callback-Mutationen **für den gegenwärtigen G1-Produktumfang verwerfen**. Dafür weder eine eigene Dock-Transaktionsarchitektur noch eine Dock.Avalonia-Modifikation einführen. Diese Entscheidung ersetzt die in A04 aus künstlichen Angreifer-Handlern abgeleitete produktive B4-Forderung; sie erklärt weder die roten Tests für bestanden noch erteilt sie ein allgemeines A05-GO.

## 1. Verbindlicher Stand und Methode

Audit am 2026-11-09, Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Maßgeblich sind die vorhandenen uncommitteten G1-/A03-Dateien einschließlich A04-Diagnosetests, nicht allein HEAD.

- Dock-Pakete: **12.1.0.13**; lokaler, sauberer Quellstand `78da173545501e796f37bc8528e9e55d9cbfd991`. Paket-Nuspecs für Dock.Avalonia, Dock.Model, Dock.Model.Mvvm, Fluent sowie Recycling, DeferredContentControl und ProportionalStackPanel stimmen damit überein.
- Avalonia: **12.1.4**; Paket-Repository-Commit `2d79ba55fae1b1e71c69f93a668080c9029e6a31`. Für die entscheidende Pointer-/Detach-Reihenfolge wurde dessen Originalquellcode gelesen, nicht die Reihenfolge einer anderen Avalonia-Version angenommen.
- Paketwahl: [CryptoWorkBenchAvalonia.csproj:24][s1]. Bestehende A04-Auswertung: [G1-A04-dock-operation-contract.md][s2] und [G1-A04-results.json][s3].
- SHA-256-Abgleich: **1.135 A04-Quelldateien, 1.631 A04-Artefakte, 7.654 historische Artefakte, jeweils 0 Abweichungen**. Referenzen: [final-source-manifest.json][s4], [final-artifact-manifest.json][s5], [historical-artifacts.json][s6].

Der Nachweis ist eine statische Erreichbarkeits- und Zustandsanalyse: Registrierung → Auslöser während einer Mutation → konkreter Callback → möglicher struktureller Aufruf. Rückwärts wurden auch Factory-/Manager-Mutationsaufrufe, Befehle, Host-Schließungen und Zustandssetter auf ihre Aufrufer geprüft. Ein fehlgeschlagener Suchtreffer allein wurde nicht als Negativnachweis benutzt: dynamische Avalonia-ClassHandler, Bindings, Templates, virtuelle Modellmethoden und interne Dock-Fortsetzungen wurden mitbetrachtet.

Keine neuen Tests oder Produktinstrumentierung, keine erneuten Native-/Testläufe. Vorhandene Testergebnisse dienen ausschließlich der Einordnung der A04-Injektionen, nicht als Beweis für die Nichterreichbarkeit.

### Geprüfter Produktumfang

G1-Dock-Shell einschließlich Layoutladen/-speichern, Reset, Maximize/Restore, Tool Show/Close/Float/Move, Einzel- und Gruppen-Drops, Tab-Reorder, Floating-Hosts, Hauptfenster-Schließen, Session-Presenter und optionalem produktivem `--dock-native-trace`. Untersucht wurden die tatsächlich verwendeten Mvvm-Modelle, DockControl/DockManager/DockService/Factory, native HostWindow-Implementierung, Fluent-Templates und ihre erreichbaren Control-/Lifecycle-Helfer.

Die Shell erzeugt konkrete, unveränderte Mvvm-`Tool`/`Document`-Objekte und einen `DocumentSafeHostWindow`-Locator ([DockShellLayout.cs:32][s7]). Wiederherstellung projiziert ebenfalls diese Modelle und Native-Roots ([DockLayoutStore.cs:265][s8]). Keine nutzerdefinierten Modell-Callbacks, Plugins oder zur Laufzeit geladenen Handler gehören zu diesem Stand.

**MDI wurde nicht pauschal ausgeschlossen:** Die ausgelieferten Dokument-Menüs können die Dokumentdarstellung auf MDI umstellen. Deren Property-/Collection- und Aktivierungswege sind daher unten enthalten. Native Floating-Hosts und MDI-Dokumentdarstellung sind verschiedene Entscheidungen.

## 2. Abgrenzung von B4

Eine unabhängige zweite Operation wäre beispielsweise: Ein durch Move ausgelöstes Ereignis lässt einen anderen Tool-Float beginnen; ein Closing-Callback startet einen neuen Manager-Drop; ein Property-Callback setzt das gesamte Layout zurück.

Dagegen sind Remove → Collapse → Exit desselben leer gewordenen Quellhosts, Float → SplitToWindow, rekursive Gruppenkinder-Drops, Rückkehranker-Erstellung und notwendige Auswahl-/Fokusabstimmung Fortsetzungen der ursprünglichen Operation. Ihre Einordnung stützt sich auf den konkreten Aufrufer und Gegenstand, **nicht** nur auf gleiche Referenzen, Thread, ObserverDepth oder eine offenstehende Factory-Freigabe.

Benutzereingaben können selbstverständlich weitere Dock-Operationen auslösen. Für B4 müsste eine solche Operation durch einen vorhandenen Callback **innerhalb der noch laufenden Mutation** ausgelöst werden. Das bloße Vorhandensein eines Click-/Key-/Pointer-Befehls beweist diese Verbindung nicht.

## 3. Produkt-Callbacks und Factory-Ereignisse

| Pfad, Registrierung und Quelle | Erreichbare Kette und Einordnung | Layout-/B4-Bewertung |
|---|---|---|
| Factory-Ereignisse: [FactoryBase.Events.cs:159][s9] (Added/Removed), :173 (Closing), :197 (Moved), :280 (WindowClosing), :296 (WindowClosed). | Die Raiser sind real und synchron. DockableClosing ruft außerdem `dockable.OnClose()`, WindowClosing `window.OnClose()`. In den verwendeten Mvvm-Typen liefern diese true; `OnSelected` und Move-Drag-Hooks enthalten keine fremde Operation: [DockableBase.cs:385][s10], [DockWindow.cs:174][s11]. | Keine unabhängige Mutation aus den konkreten virtuellen Implementierungen. Die API erlaubt zusätzliche Subscriber; deren bloße Registrierbarkeit ist Kategorie 4. |
| Produktive Trace-Registrierung: [Program.cs:25][s12] und [NativeDockWindowTrace.cs:67][s13]. Factory-Abonnements :78–87; Host-Abonnements :95–99. | WindowAdded/Removed/Closing/Closed, MoveDrag sowie DockableRemoved/Moved/Docked → `Record` (:137). Host-Opened/Activated/Deactivated/Closing/Closed ebenfalls → Record. HostWindowLocator ruft den ursprünglichen Locator auf und ergänzt Tracking. | Tatsächliche Produkt-Handler, keine Testinjektionen. Sie lesen Modell-/Fensterzustand und schreiben Diagnoseausgabe; kein Move/Float/Close/Reset/Manager-Execute. Eine mögliche I/O-Exception wäre Fehlerbehandlung, keine zweite Dock-Mutation. |
| Trace-ClassHandler, Timer und WinEvent: [NativeDockWindowTrace.cs:44][s14], :51–60, :177–184. | Native Ereignisse, Pointer und LifecycleTrace-Sink → Record; zusätzlich geposteter Record nach MouseUp. DiagnosticsLogHandler protokolliert und leitet an den zuvor vorhandenen Handler weiter; im Produkt wird kein mutierender Vorgänger installiert. | Keine strukturelle Rückkopplung; auch ein synchroner nativer Callback macht aus dem Lese-/Logpfad keine zweite Operation. |
| Shell RootChanged und Fenster-Callbacks: [DockShellWindow.axaml.cs:38][s15]; [DockShellLayout.Persistence.cs:77][s16]. | Install → ExitWindows des alten Layouts unter `_replacing` → RootChanged → DockControl.Layout lösen/neu setzen. Opened → Root.ShowWindows; Position/Size → Bounds; Closed → vorbereiteten Snapshot speichern/ReleaseLayout. | Install-/Startup-/Shutdown-Fortsetzung. Kein Callback entscheidet, ein unabhängiges Reset/Maximize/Move zu starten. Menüaktionen sind ausdrücklich registrierte Eingaben. |
| Hauptfenster-Closing: [App.axaml.cs:122][s17]. | Closing → Capture/Shutdown/Drain; später gepostetes finales Close. Closed entsorgt Editor, Info und Presentation. | Keine synchrone unabhängige Factory-Mutation aus Closing. Das spätere Schließen gehört zum angeforderten Shutdown. Es ist kein während Move künstlich eingeschleustes Fremdkommando. |
| Editor/Presenter: [DockShellPresentation.cs:54][s18], :80; [DockEditorSession.cs][s19]; [CryptoScriptEditView.axaml.cs:48][s20]. | Attach/DataTemplate.Build → Presenter herstellen/Content umhängen. Session-eigene Editor-View überspringt normales AttachEditor/DetachEditor; Hostwechsel schließt Completion-/Insight-Popups. Popup-Closed verwendet Editor-Observation, keine Dock-Factory. | Visual-/Editor-Lifecycle ist während Dock-Mutationen erreichbar; keine zusätzliche strukturelle Dock-Operation. |

Die Suche nach Produktregistrierungen wurde nicht auf die Shell beschränkt: App, Desktop, View-/Presenter-/Editor-Code und Diagnostik wurden einbezogen. Die mutierenden A04-Subscriber kommen dort nicht vor.

## 4. Reale interne Zustandsrückkopplung

### 4.1 DockingWindowStateSync: vorhandene Mutationsfähigkeit, unterbrochene Rückkopplung

Dies ist ein wichtiger tatsächlicher Framework-Callback, nicht lediglich ein externes Event:

1. `Tool/Document.IsOpen/IsActive/IsSelected` und `DockableBase.DockingState` ändern Properties und rufen `NotifyDockingWindowStateChanged` auf ([DockableBase.cs:394][s21]; [Tool.cs:47][s22]).
2. Das Interface führt nach [FactoryBase.DockingWindowStateSync.cs:49][s23]. Dessen Request-Zweige können tatsächlich Hide/Restore, Pin/Unpin, DockAsDocument/DockAsTool und Float auslösen (:105, :191).
3. Die bei einer Factory-Mutation entstehenden normalen Rückmeldungen nehmen jedoch den Weg Factory-Ereignis → SynchronizeDockingWindowState/Selection → `SetDockingWindowStateFlags` (:408).
4. **Alle vier Flag-Schreibzugriffe stehen innerhalb `EnterDockingWindowStateSyncScope` (:420).** Der dadurch synchron aufgerufene Property-Callback kehrt bei `IsDockingWindowStateSyncSuppressed` (:68) zurück. Auch ein expliziter Request wird unter diesem Scope ausgeführt; zusätzlich verhindert das Coalescing-Set denselben Request rekursiv.
5. Die aktuellen Shell-Modelle/Presenter und verwendeten Templates installieren keinen Handler, der aus einer anderen Notification einen neuen DockingState-/IsOpen-Request schreibt. Es existiert insbesondere kein entsprechendes TwoWay-Request-Binding im Produkt.

**Einordnung:** Zustandsabstimmung ist Kategorie 2. Ein künftig registrierter Property-Handler oder ein neues Request-Binding könnte einen unabhängigen Request erzeugen; das wäre Kategorie 4. Der vorhandene Sync-Pfad selbst produziert im untersuchten Stand keine unabhängige zweite Strukturmutation und damit keine B4-bedingte Layoutinkonsistenz. Die Suppression ist kein allgemeiner Schutz gegen beliebige fremde Callbacks.

### 4.2 Auswahl, Fokus und sichtbare Collections

| Registrierung/Quelle | Wirkung während einer Mutation | Einordnung |
|---|---|---|
| Mvvm `DockBase.ActiveDockable`, [DockBase.cs][s24]; Factory.InitActiveDockable, [FactoryBase.Init.cs][s25]; Active-/Focused-Raiser, [FactoryBase.Events.cs:103][s26]. | PropertyChanged → TwoWay-SelectedItem/InitActiveDockable → Init/OnSelected/Fokus/StateSync. Die konkreten OnSelected-Hooks sind leer. | Notwendige Auswahlfortsetzung. Keine Regel „bei Auswahl anderes Tool verschieben“. |
| DockControl registriert ActiveDockableChanged in [DockControl.axaml.cs:665][s27], Handler :701. | Aktivierungsreihenfolge für Selector merken. | Kein struktureller Factory-Aufruf. |
| DocumentControl: DataContext-Beobachtung :192, PropertyChanged :248, CollectionChanged :285, [DocumentControl.axaml.cs:248][s28]. | Wechsel der VisibleDockables-Liste neu abonnieren; HasVisibleDockables aktualisieren. | Anzeigezustand; keine zweite Move-/Close-Operation. |
| MdiDocumentControl: PropertyChanged :234, CollectionChanged :277, [MdiDocumentControl.axaml.cs:234][s29]; MdiDocumentWindow-Abonnements :315/:321, [MdiDocumentWindow.axaml.cs:315][s30]. | Sichtbarkeit, Active-Status, MdiBounds/State/ZIndex, Layoutinvalidierung; ClassicMdiLayoutManager ordnet Z-Indizes. | Erreichbar über mitgeliefertes MDI-Menü. Geometrie-/Aktivierungsfortsetzung, keine unabhängige Baum-/Hostmutation. |
| Tracker: [DockableControlTracker.cs:42][s31] (Attach/Detach, DataContext, Bounds), Register/Unregister :108 ff. | Visible-/Pinned-/Tab-Control-Zuordnung und Bounds aktualisieren; konkrete Modell-OnBounds-/OnPointer-Hooks sind leer. | Notwendiges Tracking; weder Auswahl eines neuen Dock-Ziels noch Foreign-Host-Close. |
| Fluent-Templates: [ToolControl.axaml:48][s32], [DocumentControl.axaml:23][s33]. | SelectedItem ↔ ActiveDockable; Fokus auf TabStripItem aktualisiert dieselbe Auswahl. | Keine strukturellen Commands aus PropertyChanged. Die im Menü vorhandenen Float/Close/Group-/MDI-Commands brauchen eine eigene Eingabe. |

Avalonias Auswahl-/Fokuswege wurden an [ListBoxItem](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/ListBoxItem.cs), [TabStripItem](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/Primitives/TabStripItem.cs) und [SelectingItemsControl](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/Primitives/SelectingItemsControl.cs) bis zur Auswahländerung verfolgt. Diese führt in die oben genannten konkreten Dock-Callbacks; es wird dabei kein Click oder unabhängiger Dock-Befehl erzeugt.

## 5. Native Host-Callbacks und notwendiger Cleanup

Registrierung: HostWindow-Konstruktor ab [HostWindow.axaml.cs:129][s34] (PositionChanged, LayoutUpdated, Activated, Deactivated); virtuelle OnOpened/OnClosing/OnClosed ab :492/:562/:503. Der Produkt-Locator und `DocumentSafeHostWindow.OnClosing` sind in [DockShellLayout.cs:200][s35] implementiert.

**Konkrete erreichbare Kette:**

Move/Drop → Remove/Collapse des Quell-Docks → EditorDockFactory.RemoveWindow → FactoryBase.RemoveWindow → DockWindow.Exit → nativer Host.Close → DocumentSafeHostWindow.OnClosing → HostWindow.OnClosing → Factory.OnWindowClosing → Save/root.Close → OnClosedCore → Visual-/Model-Cleanup → Factory.CloseWindow/OnWindowClosed/gegebenenfalls RemoveWindow.

Quellen: [EditorDockFactory.cs:76][s36], [FactoryBase.Window.cs][s37], [NavigateAdapter.cs:243][s38], [HostWindowContentCleanup.cs:14][s39].

- Dies sind echte verschachtelte strukturelle Aufrufe. Ihr Gegenstand ist der von derselben Operation entleerte/geschlossene Host samt dessen Root/Registrierung.
- EditorDockFactory markiert nur den exakt zugehörigen leeren Quellhost; DocumentSafeHostWindow lässt dessen Abschluss zu. Ein nichtleerer anderer Host erbt diese Ausnahme nicht.
- Floating-Roots haben im Produkt keine eigene Sammlung fremder Floating-Hosts; die Fensterregistrierung liegt am Hauptroot. `root.Close` des Floating-Hosts ist daher keine Aufforderung, ein fremdes Fenster zu schließen.
- Die vorhandenen externen Closing-/Closed-Subscriber sind die zuvor geprüften Trace-/Lifecycle-Pfade, nicht die A04-Angreifer.
- WindowLifecycleServices werden von FactoryBase.Lifecycle benachrichtigt ([FactoryBase.Lifecycle.cs:18][s40]), aber im Produkt ist kein Service registriert.
- Position/LayoutUpdated/PropertyChanged speichern Bounds bzw. aktualisieren Chrome; Activation/Deactivation synchronisieren Fokus. Sie starten keinen neuen Drop. Positionsänderungen während Drag aktualisieren die Vorschau, nicht den finalen Drop.
- Hauptfenster-Closing in DockControl (:1267/:1295) postet ExitWindows nach dem Closing-Ergebnis; ein noch sichtbares, wegen Cancel nicht geschlossenes Hauptfenster führt nicht zu diesem Cleanup.

**Bewertung:** Kategorie 2, keine unabhängige Zweitoperation. Ein temporär leerer Source-Dock während Remove/Collapse ist Teil dieses Ablaufs, kein Nachweis eines durch einen fremden Callback inkonsistent gewordenen Endlayouts. Die Aussage ersetzt keine allgemeine Rollback-/Exception-Garantie.

## 6. PointerCaptureLost: der stärkste interne Verdacht

### 6.1 Tab-Reorder

[ItemDragHelper.cs:64][s41] registriert PointerPressed/Moved/Released/**CaptureLost**. CaptureLost (:128) ruft `Released` (:135) auf; bei aktivem Reorder und verschiedenen Indizes führt `MoveDraggedItem` (:243) zu Factory.MoveDockable. Die Flags werden erst nach dem Move zurückgesetzt. **Isoliert betrachtet ist das ein echter reentranzfähiger Callback und musste weiter verfolgt werden.**

Die konkrete Auslösungskette wird im aktuellen Ablauf jedoch unterbrochen:

1. Beim gewöhnlichen MouseUp liefert Avalonia zuerst PointerReleased aus und verliert die implizite Capture im `finally` **nach Rückkehr des Release-Handlers**. Die Reorder-Flags sind dann zurückgesetzt. Quelle: [MouseDevice.cs:207](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Base/Input/MouseDevice.cs#L207).
2. Entfernt die Mutation das Tab-Item, ruft dessen OnDetachedFromVisualTree `ItemDragHelper.Detach` auf und entfernt auch den CaptureLost-Handler: [ToolTabStripItem.axaml.cs:103][s42], [DocumentTabStripItem.axaml.cs][s43], ItemDragHelper:73.
3. Avalonia ruft beim Detach zunächst den virtuellen OnDetachedFromVisualTree-Pfad, dann das DetachedFromVisualTree-Ereignis und danach die Kinder auf. Die Capture-Retargetierung hängt an diesem Ereignis; der Dock-Handler ist zu diesem Zeitpunkt entfernt. Quellen: [Visual.cs:581](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Base/Visual.cs#L581), [Pointer.cs:179](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Base/Input/Pointer.cs#L179). Die Base-Detach-/Fokuspfade in InputElement/Control führen hier nur zur Fokusabstimmung aus Abschnitt 4.
4. Beim Übergang aus dem Tab-Reorder in einen Dock-Drag setzt ItemDragHelper vor dem `dragOutside`-Callback `_dragStarted=false` und führt Released zur Zustandsbereinigung aus (:490 ff.). Erst danach startet Tool-/DocumentTabStripItem den DockControl-Drag und wechselt die Capture.
5. AutoScroll verändert Scroll-/Dragpositionen. Es fügt keinen synchronen zweiten Factory-Aufruf in Move ein.

Damit wurde nicht nur „kein Fehler beobachtet“, sondern für den konkreten verdächtigen Sink die Auslöser-/Registrierungsreihenfolge geprüft. Kein aktueller unabhängiger zweiter Move ist dadurch erreichbar.

### 6.2 DockControl- und Fenster-Drag

- [DockControlState.cs:1062][s44]: CaptureLost räumt Preview/Dragzustand auf; kein Execute-Drop.
- [HostWindowState.cs:483][s45]: CaptureLost beendet/verlässt den Drag; kein Execute-Drop.
- [WindowDragHelper.cs:139][s46]: PointerReleased entfernt zuerst das Release-Abonnement und löscht die aktiven Dragflags, **bevor** EndExternalWindowDrag den finalen Drop ausführt. Detach/Cancel (:71/:87) entfernt Handler, löscht Zustand und meldet nur Cancel/CaptureLost.
- [HostWindow.axaml.cs:194][s47]: EndWindowDrag setzt eigene MouseDown-/Dragging-Flags vor der Released-Verarbeitung zurück.

Drop und gegebenenfalls Float-Fallback im normalen Released-Zweig bleiben Teile derselben ursprünglichen Geste. Der parallel im Code vorhandene CaptureLost-Zweig erzeugt keine zweite Geste. Ergebnis für diese Familie: Kategorie 2 bzw. abgebrochener Pfad, keine B4-bedingte Layoutinkonsistenz.

## 7. Templates, Selector, Presenter und übrige Framework-Helfer

| Quelle und Registrierung | Verfolgter Effekt / Abschluss des Pfades |
|---|---|
| [DockControl.axaml.cs:520][s48]: LayoutProperty, Initialize/DeInitialize (:539/:604), Attach/Detach (:1172/:1209). | Layoutwechsel registriert/entfernt DockControl, Factory-Events, Recycling und Root-Window-Tracking. `DeInitialize → layout.Close` ist durch InitializeLayout bedingt; dieser Bool ist im Produkt-DockControl und Host-Template nicht aktiviert. Root-WindowClosed meldet Tracking/Ereignis, keine unabhängige Close-Entscheidung. |
| DockControl Selector :752–912/:1115, [DockControl.axaml.cs:894][s49]; [DockSelectorOverlay.axaml.cs:88][s50]. | Aktivieren könnte RestoreDockable/Preview und Fokus auslösen. Commit wird aber ausschließlich aus KeyUp bzw. Enter aufgerufen (:806/:849). HideSelector setzt nur IsOpen/Items/SelectedItem zurück. Keine automatische Commit-Registrierung bei Closed, SelectedItem-Änderung oder Detach. Daher kein versteckter Restore während einer Mutation. |
| [ToolChromeControl.axaml.cs:240][s51] und [ToolMenuLifetime.cs:17][s52]; [DockDocumentWindowChrome.cs:31][s53]. | Templatewechsel löst alte Handler, baut Chrome/Grip auf und hängt Fenster-Drag an/ab. MenuLifetime trennt/erneuert nur die Flyout-Referenz samt Binding. Float/Close/Pin/Maximize werden durch Click/Pointer-Eingaben ausgeführt, nicht durch Menü-Detach/PropertyChanged. |
| [VisualTreeLifecycleBehavior.cs:21][s54]. | ClassHandler/DataContext und Attach/Detach rufen IVisualTreeLifecycle nur bei implementierendem konkreten Objekt. Aktuelle Pane-Kontexte, Mvvm-Modelle und Produktviews implementieren diesen Dock-Hook nicht. Keine verborgene Produktoperation. |
| [DockCommandBarManager.cs:61][s55]. | ActiveDockableChanged → UpdateBars; Provider suchen, Menüs zuordnen. Produkt enthält keinen IDockCommandBarProvider. Auch die Aktualisierung führt keinen Command aus. |
| [ControlRecycling.cs:53][s56]; [DeferredContentControl.cs][s57]; [ProportionalStackPanel.cs:140][s58]. | Template/Content wiederverwenden oder lösen, verzögert präsentieren, Proportion/CollapsedProportion und Layout aktualisieren. Diese Komponenten haben keine Dock.Model-Factory-Abhängigkeit. Die von ihnen ausgelösten konkreten Build/Attach/Detach-/Binding-Rückwege sind die oben geprüften Presenter-, Auswahl- und Pointer-Pfade. |
| [PinnedDockControl.axaml.cs:417][s59] (Modell-Abonnements), :475 (UpdateWindow); RootDockControl-Pointerhandler. | DisplayMode-/Geometrie-/Preview-Verwaltung. Aktuelle Panes haben CanPin=false; der Layoutstore verwirft persistierte Pinned-/Hidden-Bäume. Kein produzierter Pin-Request aus Notifications. Ein generischer Pointer-Eintritt zum Ausblenden einer Preview ist keine durch Move erzeugte Eingabe. |
| [OverlayHost.cs:130][s60], :156/:192/:344; [OverlayLayerRegistry.cs][s61]. | Provider-/Collection-/Layer-Property-Callbacks bauen Visual-Layer neu auf. Kein Produkt-Overlayprovider oder mutierender Layer-Callback registriert; die generischen Overlay-Controls führen keine Dock-Factory-Mutation aus. |
| [DockWorkspaceManager.cs:297][s62]; AvalonDock LayoutRoot; Dock.Avalonia.Diagnostics RootDockDebug; ManagedHostWindow/ManagedWindowLayer/ManagedDockWindowDocument. | In der Bibliothek vorhandene zusätzliche Subscriber wurden berücksichtigt, aber diese Integrationen werden von G1 nicht instanziiert/registriert. G1 verwendet den expliziten nativen Locator und Mvvm, nicht AvalonDock oder den WorkspaceManager. Ihre Aktivierung würde eine andere Produktkonfiguration/Registrierung erfordern. Dies schließt die tatsächlich erreichbaren MDI-Controls aus Abschnitt 4 ausdrücklich nicht aus. |

## 8. DockManager- und Factory-Fortsetzungen

[DockManager.cs:631][s63] öffnet den Operation-Scope und führt nach der Preview die Mutation aus; ValidateChildDocks (:594) ruft für die Kinder denselben Manager rekursiv auf. [DockService.cs][s64] zerlegt einen Drop in mehrere Factory-Aufrufe.

Der tatsächliche Produkt-Observer [EditorDockFactory.cs:44][s65] erfasst Composite-Quellen, verschiebt deren Empty-Cleanup und schließt bei Dispose nur die noch passenden leeren Quellen ab. InitDockable, ReturnAnchor, CollapseDock/RemoveWindow, Float→Split und die Auswahl-/Hostschritte sind damit konkrete Fortsetzungen des initialen Befehls. Es gibt keinen registrierten Factory-/Manager-Callback, der dabei eine neue Quelle/Ziel-Operation nach einer unabhängigen Produktentscheidung startet.

Die von A04 gezeigte Schwäche der öffentlichen Hooks bleibt wahr: Ein künstlicher Callback kann dieselben APIs benutzen; Null-Scope ist kein Veto und ObserverDepth beweist keine Herkunft. **Das ist eine fehlende allgemeine Abschirmung gegen zusätzliche Handler, kein im jetzigen Produkt nachgewiesener B4-Aufrufpfad.**

## 9. Vier Kategorien und die historischen roten Solltests

| Kategorie | Befund |
|---|---|
| 1. Vorhanden, erreichbar, unabhängig mutierend | **Kein Pfad gefunden.** Auch StateSync, CaptureLost, Selector, native Close-Rückläufe und MDI wurden nicht allein wegen ihres internen Ursprungs ausgenommen. |
| 2. Vorhanden, erreichbar, notwendige Fortsetzung | Factory-/Manager-Rekursion, Source-Cleanup/Host-Close, Install/Shutdown, Zustands-/Auswahlabstimmung, Visual-/Presenter-Tracking; konkrete Begründungen in Abschnitten 3–8. |
| 3. Künstliche A04-Registrierungen | Die nachstehenden Eventhandler werden in den Tests ausdrücklich hinzugefügt und anschließend entfernt. Gleiches gilt für die Native-Probe-Injektionen. |
| 4. Theoretisch / zusätzliche Registrierung nötig | Fremde mutierende Eventsubscriber, neue State-Request-Bindings, eigene OnClose/OnSelected-/Lifecycle-Modelle, Workspace-/WindowLifecycle-/Overlay-/CommandBar-Services oder alternative Hostintegration. Vor ihrer Einführung ist dieser Audit neu zu bewerten. |

Die **sieben roten A04-Solltests bleiben unverändert und rot**. Weder Ignore/Explicit noch Assertion-/Ergebnisänderungen wurden vorgenommen:

| Roter Test / Injektionsstelle in [DockOperationContractTests.cs][s66] | Was er tatsächlich belegt |
|---|---|
| MovedMustRejectIndependentFloat (:29, Registrierung :39) | Test-Handler startet aus DockableMoved einen unabhängigen Float. |
| AddedMustRejectIndependentMove (:48, Registrierung :58) | Test-Handler verschiebt ein anderes Tool aus DockableAdded. |
| ClosingMustRejectIndependentOtherHostClose(true) (:67, Registrierung :78) | Test-Handler schließt den anderen Host über Factory.CloseWindow. Der native Close-Fall false ist ein separater grüner Fall. |
| WindowClosingMustRejectNestedManagerDrop (:87, Registrierung :98) | Test-Handler startet einen neuen Manager-Drop während Quellhost-Cleanup. |
| SameSourceTargetMustNotAuthorizeSecondMove (:127, Registrierung :137) | Test-Handler wiederholt Move mit gleichen Objekten; demonstriert die fehlende Herkunftsprüfung, nicht eine reale Produktregistrierung. |
| MovedMustNotMutateAnotherFloatingHost (:144, Registrierung :155) | Test-Handler entleert einen anderen Floating-Host. |
| PartialMoveFailureMustLatchExistingLayoutFault (:187, Registrierung :192) | Eingeschleuste Exception aus DockableRemoved lässt eine Teilmutation zurück, ohne LayoutFaulted zu setzen. **Dies ist kein unabhängiger zweiter Mutationspfad** und daher kein positiver B4-Erreichbarkeitsnachweis. Es bleibt ein historischer Fault-Contract-Rotnachweis. |

Die Native-Probe fügt dieselben Fremdaktionen selbst hinzu: [DockOperationNativeProbe.cs:56][s67] (Moved→Float) und :96 (Removed→Exception). Ein nativ reproduzierter Fehler mit diesem Zusatzhandler ist weiterhin eine Testinjektion.

**Künftiger Anforderungsstatus:** Für den hier untersuchten gegenwärtigen G1-Umfang werden diese sieben roten A04-Solltests als historische, **nicht mehr verpflichtende Anforderungen** zur Entscheidung vorgelegt. Sie dürfen weder als bestanden noch als sieben behobene Fehler gezählt werden. Die Empfehlung umfasst keine Behauptung, dass beliebige Exceptions oder künftig hinzugefügte Callbacks sicher wären. Die Übernahme dieser geänderten Anforderungsbaseline obliegt dem Projekteigentümer; Dateien und bisherige A04-Statusberichte werden nicht rückwirkend umgeschrieben.

## 10. Tragweite, Integrität und Abschluss

Der Negativnachweis beruht auf der geschlossenen aktuellen Registrierung und den konkret geprüften Rückkopplungsbarrieren. Er ist keine allgemeine Thread-Safety-, Transaktions-, Rollback- oder Bibliotheksfehlerfreiheitszusage. Ausgeschlossen sind nicht vorhandene Erweiterungen, externe Test-/Debugger-Injektionen und künftige Änderungen; **kein für den beschriebenen aktuellen Produktumfang relevanter verdächtiger Callback-Pfad bleibt in diesem Audit ungeklärt**.

Es wurde ausschließlich dieses R1-Dokument erstellt. Produktcode, Tests, Pakete, Dock-Quellstand, alte Reports und Evidenz sind unverändert. Kein Workaround, Fork, Commit, Push oder Merge. Die SHA-256-Abgleiche der vorhandenen Quellen und Evidenz ergaben die in Abschnitt 1 dokumentierten Nullabweichungen.

**Vorlage an den Projekteigentümer:** B4-Zusatzanforderung für den aktuellen G1-Umfang verwerfen; historische rote Solltests mit dem oben beschriebenen nicht verpflichtenden Status erhalten. Keine Implementierung einer neuen Dock-Transaktionsarchitektur aus diesem Audit ableiten. Nach Erstellung und Integritätskontrolle dieses Berichts endet die Arbeit; weitere Projektfreigaben oder Änderungen werden hier nicht vorgenommen.

[s1]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj:24>
[s2]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-dock-operation-contract.md>
[s3]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/evidence/G1-A04-results.json>
[s4]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04/final-source-manifest.json>
[s5]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04/final-artifact-manifest.json>
[s6]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04/baseline/historical-artifacts.json>
[s7]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:32>
[s8]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:265>
[s9]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Events.cs:159>
[s10]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Core/DockableBase.cs:385>
[s11]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Core/DockWindow.cs:174>
[s12]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia.Desktop/Program.cs:25>
[s13]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs:67>
[s14]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs:44>
[s15]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs:38>
[s16]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs:77>
[s17]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/App.axaml.cs:122>
[s18]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs:54>
[s19]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs>
[s20]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs:48>
[s21]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Core/DockableBase.cs:394>
[s22]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Controls/Tool.cs:47>
[s23]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.DockingWindowStateSync.cs:49>
[s24]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model.Mvvm/Core/DockBase.cs>
[s25]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Init.cs>
[s26]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Events.cs:103>
[s27]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DockControl.axaml.cs:665>
[s28]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DocumentControl.axaml.cs:248>
[s29]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/MdiDocumentControl.axaml.cs:234>
[s30]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/MdiDocumentWindow.axaml.cs:315>
[s31]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DockableControlTracker.cs:42>
[s32]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia.Themes.Fluent/Controls/ToolControl.axaml:48>
[s33]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia.Themes.Fluent/Controls/DocumentControl.axaml:23>
[s34]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/HostWindow.axaml.cs:129>
[s35]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:200>
[s36]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:76>
[s37]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Window.cs>
[s38]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/Adapters/NavigateAdapter.cs:243>
[s39]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Internal/HostWindowContentCleanup.cs:14>
[s40]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.Lifecycle.cs:18>
[s41]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Internal/ItemDragHelper.cs:64>
[s42]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/ToolTabStripItem.axaml.cs:103>
[s43]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DocumentTabStripItem.axaml.cs>
[s44]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Internal/DockControlState.cs:1062>
[s45]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Internal/HostWindowState.cs:483>
[s46]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Internal/WindowDragHelper.cs:139>
[s47]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/HostWindow.axaml.cs:194>
[s48]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DockControl.axaml.cs:520>
[s49]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DockControl.axaml.cs:894>
[s50]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DockSelectorOverlay.axaml.cs:88>
[s51]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/ToolChromeControl.axaml.cs:240>
[s52]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Internal/ToolMenuLifetime.cs:17>
[s53]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs:31>
[s54]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Behaviors/VisualTreeLifecycleBehavior.cs:21>
[s55]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/CommandBars/DockCommandBarManager.cs:61>
[s56]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Controls.Recycling/ControlRecycling.cs:53>
[s57]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Controls.DeferredContentControl/DeferredContentControl.cs>
[s58]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Controls.ProportionalStackPanel/ProportionalStackPanel.cs:140>
[s59]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/PinnedDockControl.axaml.cs:417>
[s60]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/Overlays/OverlayHost.cs:130>
[s61]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Avalonia/Controls/Overlays/OverlayLayerRegistry.cs>
[s62]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/DockWorkspaceManager.cs:297>
[s63]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/DockManager.cs:631>
[s64]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/DockService.cs>
[s65]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:44>
[s66]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchGuiTest/DockOperationContractTests.cs>
[s67]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs:56>

