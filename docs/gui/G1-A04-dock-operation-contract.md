# G1-A04 – Dock Operation Admission, Continuation and Completion Contract

## Status und Phase-1-Entscheidung

**CHANGES REQUIRED; kein A05-GO.** Dies ist ein Machbarkeits- und Rotnachweis, keine implementierte B4-Korrektur. Der geforderte Gesamtvertrag ist mit den untersuchten öffentlichen Hooks unter Erhalt sämtlicher F01/F03-Pfade nicht nachgewiesen. Deshalb wird die produktive Factory nicht spekulativ umgebaut. Diese Einschränkung folgt ausdrücklich aus Auftrag §4/§14. Die bestehende B4-Lücke bleibt offen; die hinzugefügten Solltests dürfen deshalb rot sein und werden nicht ausgeblendet.

Diese Entscheidung wurde vor dem Hinzufügen der A04-Tests und vor jeder möglichen Produktkorrektur festgehalten. Eine unveränderliche Fassung liegt unter `artifacts/g1-a04/phase1-hook-proof.md`. Kein Upstream-Fork, keine Paketänderung, kein selbstgebauter Docking-Kern. Es wird auch nicht pauschal Group-Docking deaktiviert, da dies der Erhaltungsvorgabe widerspräche.

## Ausgangsstand

Worktree: `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Die uncommittete A03-Implementierung ist die Baseline, nicht HEAD. Vor Änderungen wurden 1.131 versionierte bzw. nicht ignorierte Dateien vollständig kopiert und gehasht (`artifacts/g1-a04/baseline/files.json`, Kopien in `baseline/files/`). Gegen das akzeptierte A03-Abschlussmanifest gab es keine Abweichung. Vollständiger Status und Patch liegen daneben.

Weitere Baseline: 7.654 historische Artefakte in allen bisherigen g1-Verzeichnissen; A03-Binärmanifest; tatsächliches Benutzerlayout. Dock-Quellstand `78da173545501e796f37bc8528e9e55d9cbfd991`, sauber, passend zum gepinnten 12.1.0.13. Historische Reports/Evidence und Paket-Locks bleiben unverändert. Ergebnisse und abschließende Hashprüfung werden unten ergänzt.

## Hook- und Sicherheitsnachweis

Alle folgenden Pfade beziehen sich auf `artifacts/g1/dock-source/src/`:

| Hook / Ort | Beobachtung | Konsequenz |
|---|---|---|
| Dock.Model/DockManager.cs, ValidateDockable (631 ff.) | Nicht virtuell. Preview-Core; BeginDockOperation; Execute-Core; optional Complete; Dispose im finally. | Null-Scope ist kein Veto. Der Scope erhält weder Ergebnis false noch die ursprüngliche Exception. |
| Dock.Model/Core/IDockOperationObserver.cs und IDockOperationScope.cs | Begin(source,target,operation), Complete(), Dispose(). | Keine unübertragbare Fortsetzungsmarke, kein Callback-Beginn/-Ende, kein Abort(error). |
| IDockDropPolicy / DockManager.IsDockingAllowed | Kann aktuellen Drop verneinen; wird bei Preview und Ausführung erneut konsultiert. | Geeignet für finale unabhängige Admission; bezeichnet aber nicht, wer den Aufruf verursacht hat. |
| DockCapabilityPolicy | Capability-Entscheidung für konkrete Modelle. | Keine allgemeine Eintritts-/Commit-Grenze für rohe Factory-Methoden, Properties oder Collections. |
| DockManager.ValidateChildDocks (594 ff.) | Rückwärtsiteration über lebende Kinderliste, rekursiv dieselbe öffentliche ValidateDockable-Methode. | Interne Rekursion und fremder Callback verwenden denselben Hook. F03-Deferral bleibt erforderlich. |
| Dock.Model/DockService.cs | Ein Drop ruft mehrere Factory-Methoden auf, u.a. Move und anschließend Split; einige bool-Erfolge folgen auf void-Factory-Aufrufe. | Eine Factory-Methode ist keine Benutzertransaktion. Stilles Verweigern eines inneren void-Aufrufs beweist keinen abgelehnten Gesamt-Drop. |
| Dock.Avalonia/Controls/DockControl.axaml.cs (130, 249 ff.) | DockManager getter-only; eigener new DockManager(new DockService(), options). | Eine eigene IDockManager-Hülle für programmatische Aufrufe ersetzt den produktiven Drag-Pfad nicht. |
| Dock.Avalonia/Internal/DockManagerState.cs (18, 189 ff.) | Interner Typ; führt finales ValidateDockable aus, Vorschau getrennt. | Kein öffentlicher synchroner Execute(Action)-Adapter um den ganzen nativen Drop. |
| FactoryBase.Dockable.cs | Move/Swap/Remove verändern Collections und Properties; benachrichtigen synchron; rufen danach weitere virtuelle Methoden auf. | Factory-Event-Abschirmung allein erfasst PropertyChanged/CollectionChanged nicht. |
| Dock.Model.Mvvm/Core/DockBase.cs | ActiveDockable benachrichtigt, ruft InitActiveDockable und benachrichtigt danach erneut. | Auch nach einem Factory-Hook bleibt unmarkierter Callback-Code vor dem nächsten internen Schritt möglich. |
| FactoryBase.cs / CollapseDock | Entfernt Splitter/Docks, kollabiert Eltern, schließt Fenster; initiiert weitere Properties/Callbacks. | Ein pauschales Cleanup-Permit autorisiert fremde Aufrufe. Gleiche Referenzen sind kein Herkunftsnachweis. |
| FactoryBase.Window.cs / RemoveWindow | window.Exit vor Entfernen der Registrierung; danach Owner/Layout/Factory-Bereinigung. | Native Closing/Closed können zwischen Anfang und Ende laufen; Entfernung ist kein atomarer Aufruf. |
| Dock.Avalonia/Controls/HostWindow.axaml.cs | OnClosing ruft zuerst base (externe Closing-Subscriber), danach Factory.OnWindowClosing und root.Close. OnClosedCore ruft externe Closed-Subscriber vor weiterer Factory-Bereinigung auf. | Eine durchgehende Host-Close-Berechtigung umfasst zugleich fremde Callbacks. Eine pauschale Sperre verhindert notwendige eigene Fortsetzung. |

### Konkretes Unterscheidungsproblem

Ein erlaubtes Move erreicht nach Collection-/Owner-/ActiveDockable-Änderungen noch CollapseDock(source). Während der vorherigen synchronen Notifications kann ein fremder Handler bereits CollapseDock mit exakt diesem Objekt aufrufen. Der öffentliche Methodeneintritt liefert in beiden Fällen dieselbe Factory, Session, Layoutgeneration, Thread-ID und Objektidentität. Ein gespeicherter erwarteter Schritt kann vom Handler verbraucht werden. Ein Token, das nur im Factory-Feld liegt, wird durch diesen Aufruf implizit ebenso zugänglich.

Analog ruft der Composite-Manager rekursiv ValidateDockable(child,target,...) auf. Vor dem nächsten Kind kann ein Callback diesen Aufruf selbst auslösen. BeginDockOperation kennzeichnet weder interne Rekursion noch den Übergang aus fremdem Code. Die Existenz einer äußeren Operation beweist keine innere Autorität.

Das ist **kein mathematischer Unmöglichkeitsbeweis für jede denkbare Integration**. Es widerlegt die vorgeschlagene reine Hook-/Referenz-/Depth-Freigabe. Eine weitergehende Lösung müsste sämtliche Notifications und native Fensterübergänge kontrollieren oder die interne Orchestrierung ersetzen. Dies ist in diesem Auftrag nicht als schmaler Adapter bewiesen. Ein StackTrace-basierter Herkunftstest wäre keine stabile API-Garantie (Inlining/Optimierung) und wird verworfen.

Untersuchte eingeschränkte Alternativen:
- Nur Factory-Eventraiser abschirmen: PropertyChanged und CollectionChanged bleiben offen.
- Alle inneren Factory-Eintritte sperren: legitimes Float→Split, Move→Cleanup und F03 scheitern.
- Private Engine plus öffentliche Fassade: Dock.InitDockable veröffentlicht die Engine als dockable.Factory; Modell und Host verwenden diese für legitime Fortsetzungen. Ohne durchgehende Umleitung keine Grenze.
- RemoveDockable(collapse:false) plus eigener späterer Cleanup: kann einen einzelnen Pfad kontrollieren, löst aber weder Manager-Rekursion noch die übrigen Collapse-/Host-/Property-Übergänge.
- Gesamten Drop in Coordinator.Observe halten: vorhandenes Observe akzeptiert Action; Observer.Begin/Dispose liefert diese Ausführungshülle nicht. Coordinator darf nicht verändert oder durch ein neues Ticket ersetzt werden.
- Custom-Modelltypen und umhüllte Collections: technisch weiter untersuchbar, aber vollständige Notification-Abdeckung, Serializer-Kompatibilität und native Closing-/Closed-Abgrenzung wären erst zu beweisen; keine Freigabe in A04.

## Eintritts- und Fortsetzungsmatrix

„Letzte Prüfung“ beschreibt den **Iststand**, nicht eine neue Sicherheitsgarantie.

| Pfad / unabhängiger Eintritt | Letzte Prüfung | Interne Schritte und Callbacks | Nötige Fortsetzung / Erkennung / Ergebnis |
|---|---|---|---|
| Shell Show/Move/Float/Close | CanChange, danach teilweise Factory.Change | Add/selection/focus; Move/Float/Close; Modell- und Factoryevents | Shell lehnt unter Observer ab. Direkter Factory-Eintritt kann durch depth passieren. void bedeutet nicht Commit. |
| Factory Move, beide Overloads | Change→Allowed→Session.Relocate | Remove, Unpin, Insert, Init, Active, Added/Moved/Docked, Collapse | Source-Cleanup muss unter Observer fertigwerden. Fremder Move/Float durch depth erlaubt; ursprüngliche Exception propagiert ohne universelle Endprüfung. |
| Swap, beide Overloads | Change | Zwei Collection-Ersetzungen, Init, Swapped, Active, Splittercleanup | Keine allgemeine Callback-Herkunftsmarkierung; Teilmutation bei Fehler möglich. |
| SplitToDock | Change | CreateSplitLayout, Proportion/Collection/Active, Init, Docked | Gleiches public Ziel für interne und fremde Schritte; keine Rollbackzusage. |
| SplitToWindow / Float | Change; Float prüft Capability | Unpin, Remove, CreateWindowFrom, AddWindow, Bounds, Present, Docked, Focus, Rückkehranker | Float→Split ist legitimer innerer Aufruf. Event-/Property-/Host-Callbacks bleiben fremder Code; Rückkehranker muss erhalten bleiben. |
| FloatAll / neue Dokumentgruppen | Change | Mehrere Move-/Split-/Window-Schritte | Operation über mehrere Methoden; keine unabhängige Zweitautorität aus gleichem Quellobjekt ableitbar. |
| Manager Fill/Side/Group | Preview und Execute CanDrop/Capabilities | DockService, mehrere Factory-Aufrufe, rekursive Child-Drops, Composite-Deferral | Ablehnung über Policy möglich, null-Scope nicht. Interne Rekursion ist nicht eindeutig von neuem Drop unterscheidbar. |
| CollapseDock / RemoveWindow | Keine allgemeine eigene Admission; exakte F01-Hostausnahme | Splitter/Elternpromotion, window.Exit, Closing/Closed, Registry-/Owner-Bereinigung | Nur genau zugehörigen leeren Host abschließen; Callback kann gleiche Methode/Objekte verwenden. |
| Native Host-Close | DocumentSafeHostWindow.CanChange bzw. exakte leere Sourceausnahme | Externe Closing, Factory.WindowClosing, Save, root.Close, Closed, CloseWindow/RemoveWindow | Nichtleerer fremder Host unter Observer wird bereits blockiert. Freigegebener eigener Closing-Callback kann dennoch Factory-/Manager-Aktionen starten. |
| Maximize/Restore/Reset | ExecuteLayout, CanRelocate, ChangeLayout | Focus-Projektion bzw. Install, Propertyevents, Host-Schließen/Öffnen | Reentrantes Reset wird bereits abgelehnt; A02-Kompensation/Fault gilt nur für diese Layoutkommandos. |
| Add/Insert/Remove/Init/CloseWindow, Pin/Unpin, DockAsDocument etc. | Teilweise keine EditorDockFactory-Override-Grenze | Direkte Modell- und Strukturänderungen | Kein vollständiger öffentlicher Mutationsvertrag. Pin ist für Shell-Panes deaktiviert; rohe Factory-APIs dürfen deshalb nicht als sicher abgedeckt gelten. |

EditorDockFactory-Overrides wurden vollständig aufgenommen: InitDockable, RemoveWindow, CollapseDock, CreateRootDock, Hide/Restore, Move×2, Swap×2, SplitToDock, SplitToWindow(options), Float(options), FloatAll(options), NewHorizontal/NewVerticalDocumentDock, CloseDockable; zusätzlich CanDrop, BeginDockOperation und ChangeLayout. Alle bleiben unverändert.

## Unterstützter Umfang und nicht implementierter Zielvertrag

Neu nachgewiesener sicherer Operationsumfang: **keiner über die vorhandenen A03-Gates hinaus**. Die bestehenden Shell-Guards, Layoutkommandos und fachlichen Observer-Grenzen bleiben erhalten, werden aber nicht zu einem vollständigen A04-Vertrag umetikettiert.

Für eine weitere Implementierung wären notwendig: eindeutiger externer Eintritt vor erster Mutation, aktuelle autoritative Session plus Workspace-/Layoutgeneration, UI-Thread, enges synchrones Execute(Action), unveröffentlichte Fortsetzungseigentümerschaft und abschließende Ergebnis-/Fehlerübergabe. Ein neues bool-Feld oder erlaubte Objektliste genügt nicht. Es wird kein Context über await gehalten.

CanRelocate prüft derzeit Started/disposed, service stopping/busy, executing, Haupt-/Editorfenster enabled, Phase Gui/Active/Pending/ObserverDepth, Editor MutationLocked/IdentityLocked und Document.IsInUpdate. Start prüft autoritative Registrierung und Targets(Editor); laufendes CanRelocate hat keine explizite erneute Targets-/Layoutgeneration-Prüfung. Diese Lücke wird nicht als erfüllt bewertet. Pointer-Preview bleibt unverbindlich.

## Composite und Fehler

F03-Deferral über Sources/Empty bleibt unverändert nötig. Complete protokolliert derzeit nur; Dispose führt Cleanup unabhängig von Complete über Change und damit erneute allgemeine Admission aus. Es gibt keinen Abort(error)-Parameter. Ein Cleanup-Fehler aus Dispose kann die ursprüngliche Exception beim Unwinding ersetzen. Ein false nach partiellen Kinderoperationen ist möglich; kein Rollback wird zugesagt.

Der geforderte Vertrag würde RejectedBeforeMutation, Committed, PartialFailure, CleanupFailure und Aborted unterscheiden. Sichere Restbereinigung müsste an genau die begonnene Operation gebunden sein, originale Exception erhalten und bei unbestätigtem Endzustand A02.LayoutFaulted setzen. **Das ist in A04 nicht implementiert.** A02.LayoutFaulted schützt weiterhin seine bisherigen Layoutkommandos/Capture, nicht automatisch jede Factory-Ausnahme. Keine fremden Hostscans, Timer oder allgemeine Observer-Ausnahme werden ergänzt.

## Fachliche Grenzen

EditorCoordinator, EditorBridge, WorkbenchAutomationService, InterpreterHostQueue, MCP-Lease, SaveIntent und Shutdown-/Drain bleiben bytegleich. Keine Tickets, Interpreterreservierungen, neuen Singletons oder Workspacegenerationen. Der B4-Nachweis betrifft Layoutintegrität; er beweist keine Umgehung fachlicher Editor-/Interpreterautorität. Mehrere zukünftige Dokumente benötigen je Session/Editor eine eindeutige Zuordnung; eine globale Mutationserlaubnis wäre dafür ungeeignet.

## Prüfungen und Ergebnis

### Permanente Rotfälle und Gegenproben

Alle 13 neuen NUnit-Fälle sind gewöhnliche Tests in `DockOperationContractTests.cs`: kein Ignore/Explicit, keine Umkehrung der Sollassertion, keine Sleeps oder Retries. Dadurch ist die GUI-Suite bewusst nicht grün, solange der Vertrag offen ist. Die beiden ausdrücklich als PublicHook benannten Charakterisierungstests bestätigen API-Verhalten, nicht die Erfüllung des Zielvertrags.

| Fall | Art | Debug / Release / unabhängige Wiederholung |
|---|---|---|
| Moved → Info-Float | Unabhängige neue Aktion, Observer nachweislich aktiv | ROT: Owner ändert sich |
| Added → anderes Tool-Move | Unabhängige neue Aktion | ROT: Owner ändert sich |
| DockableClosing → fremdes natives Host.Close | Unabhängige neue Aktion | GRÜN: Host bleibt |
| DockableClosing → fremdes Factory.CloseWindow | Unabhängige neue Aktion über öffentlichen Factory-Eintritt | ROT: Host/Registrierung bleibt nicht erhalten |
| WindowClosing → neuer Manager-Drop | Unabhängige Aktion innerhalb eigener Source-Bereinigung | ROT: Manager liefert true |
| PropertyChanged(Owner) → Reset | Unabhängige neue Aktion | GRÜN: Reset false, ursprüngliches Move fertig |
| Moved → identisches Move mit denselben drei Argumenten | Zweite unabhängige Aktion, keine neue Objektidentität nötig | ROT: zwei Moved-Ereignisse statt eines |
| Moved → anderes Floating Window leeren | Unabhängige neue Aktion | ROT: fremder Host/Owner wird verändert |
| Zugehörigen leeren Sourcehost entfernen | Legitime interne Fortsetzung | GRÜN: Observer > 0, nur Source geschlossen, Editor/Text/Undo/Generation/Capture erhalten |
| Exception nach Entfernung des Pflichtdokuments | Partieller Fehler | ROT: originale Exception und Observer-Cleanup korrekt; Capture lehnt fehlendes Dokument ab, aber LayoutFaulted bleibt false |
| Echte MCP-Lease → Factory und Manager | Unabhängige Aktion vor Mutation verweigern | GRÜN: beide verweigert, Capture unverändert |
| PublicHook: null-Scope | API-Charakterisierung | GRÜN: Manager mutiert trotzdem |
| PublicHook: Dispose wirft nach Move-Exception | API-Charakterisierung | GRÜN: Cleanup-Exception ersetzt ursprüngliche Exception |

Der erste Build hatte einen mehrdeutigen EventArgs-Import; er wurde korrigiert und zählt nicht als Rotnachweis. Der erste ausführbare Lauf hatte 8 rote/5 grüne Tests: zusätzlich war der neue Lease-Test fehlerhaft, weil ActivateMcp allein noch keine Lease erwirbt. Nach echter AcquireEditorAsync-/ReleaseEditorAsync-Verwendung bestehen alle finalen Läufe mit genau 7 roten/6 grünen Fällen. Die ursprünglichen Logs bleiben unverändert unter `artifacts/g1-a04/red*` erhalten. Keine Produktdatei wurde zwischen Rotnachweis und finalen Läufen korrigiert.

### Vollständige Regressionen

| Suite | Debug | Release | Nicht ausgeführt je Konfiguration |
|---|---:|---:|---|
| Bestehende GUI | 1.087 bestanden | 1.087 bestanden | 0 |
| Neue A04-Solltests und API-Probes | 6 bestanden / 7 fehlgeschlagen | 6 bestanden / 7 fehlgeschlagen | 0 |
| GUI insgesamt | 1.093 bestanden / 7 fehlgeschlagen | 1.093 bestanden / 7 fehlgeschlagen | 0 |
| MCP | 176 bestanden | 176 bestanden | 13 Explicit |
| Core | 11.512 bestanden | 11.512 bestanden | 26 Skip + 1 Explicit |
| G1-01 Headless | 12 bestanden | 12 bestanden | 0 |
| Unabhängige A04-Wiederholung | 6 bestanden / 7 fehlgeschlagen | 6 bestanden / 7 fehlgeschlagen | 0 |

Alle bestehenden Assertions sind erhalten. Locked-Restore wurde pro Konfiguration durchgeführt; Builds sind Teil der vollständigen Test- und nativen Build-Läufe. TRX, Log und tatsächliche Exitcodes liegen unter `artifacts/g1-a04/regression` und `independent`. Exitcode 1 der GUI-/Wiederholungsbefehle wird nicht unterdrückt. Die Core-Skips betreffen Darwin-/Symlink-Voraussetzungen; der Explicit-Fall ist eine manuelle Hardwaremessung. MCP-Explicit-Fälle setzen eine installierte Codex-/Adapter-Integration voraus. Einzelne Namen/Gründe stehen vollständig in der JSON-Evidence.

### Native Windows-Matrix

Der bestehende G1ShellProbe und `Validate-G1LayoutRoles.ps1` sind um `operation-contract` bzw. `-OperationContract` ergänzt. Der neue Modus wird in zwei separaten Prozessen je Konfiguration ausgeführt. Er verwendet das bestehende RedockJournal, echte HWND-/Owner-Abfragen, die tatsächliche produktive Session und den regulären Shutdown. Sein Journal-Timer ist ausschließlich bestehende Testbeobachtung; kein produktiver Cleanup-Timer wurde eingeführt.

Der neue Modus prüft unabhängigen Callback-Float, exakte Source-HWND-Zerstörung, Erhalt eines fremden HWND, Factory/Root/Window-Zuordnung, Maximize/Restore/Capture, echte MCP-Lease, Editor/Text/Undo/Generation, eine Exception nach Pflichtdokument-Entfernung, LayoutFaulted, SaveIntent und Shutdown. Er sendet **keine externen Maus-/Tastatureingaben**. Die bestehende programmatische A03-Matrix wird zusätzlich vollständig angefordert (27 Prozesse/1.051 Checks je Konfiguration als Vergleich).

| Native Gruppe | Debug | Release |
|---|---|---|
| Bestehende A03-Matrix, je 27 Prozesse | 23 Prozesse erfolgreich; 707 Checks bestanden, 4 Fehler | 21 Prozesse erfolgreich; 625 Checks bestanden, 6 Fehler |
| A04, ursprünglicher Probe, je 2 Prozesse | je 19 bestanden / 3 fehlgeschlagen | je 19 bestanden / 3 fehlgeschlagen |
| A04, korrigierter Probe, je 2 neue Prozesse | je 21 bestanden / 2 fehlgeschlagen | je 21 bestanden / 2 fehlgeschlagen |

In der alten Matrix scheitern `layout-roles`, `layout`, `layout-restart` und `layout-invalid` in beiden Konfigurationen an der geforderten Vordergrundaktivierung. In Release scheitern zusätzlich `editor` und `integration` an derselben Fokus-Voraussetzung. Das sind **10 interne Fokus-Timeouts**, keine überschrittenen 180-Sekunden-Prozesslimits. Die Ursache wurde nicht abschließend bestimmt; es erfolgte keine externe Eingabe-/Fokuskorrektur und keine Abschwächung der Assertions. Die frühere Abnahme mit 1.051 Checks je Konfiguration ist damit **nicht erneut vollständig bestätigt**. Die tatsächlichen 27 Prozesse je Konfiguration wurden dennoch alle ausgeführt. Redock und sämtliche 20 Save-/Restart-/Historical-Prozesse je Konfiguration bestehen; Debug-Editor und Debug-Integration ebenfalls.

Die ursprüngliche A04-Probe enthielt einen zusätzlichen Messfehler: Sie verglich WorkspaceGeneration vor und nach einem **autorisierten MCP-Kontrollwechsel**. `EditorCoordinator.TakeControl`, `BeginDrain` und `ResumeGui` erhöhen diese Generation absichtlich. Dieser Test wurde korrigiert: Generationserhalt wird direkt nach der visuellen Relokation geprüft; Editor/TextDocument/UndoStack-Identität zusätzlich über den Lease-Wechsel hinweg. Beide Assertions bleiben erhalten, nun mit korrekter Messgrenze. Die ursprünglichen vier Prozesse, Buildlogs und Binärhashes bleiben unter `native` erhalten. Nur der korrigierte A04-Modus wurde unter `native-final` erneut gebaut und in vier neuen Prozessen ausgeführt; die unveränderten alten Fokusprüfungen wurden nicht pauschal wiederholt.

Alle vier finalen A04-Prozesse bestätigen exakt dieselben zwei Sollverletzungen: `B4-independent-float-rejected` und `partial-failure-latches-A02-fault`. Die übrigen 21 Checks je Prozess bestehen, insbesondere tatsächliche Source-HWND-Zerstörung, legitimer anderer Host, Capture/Decode, Maximize/Restore, echte Lease-Ablehnung vor Mutation, Editor/Text/Undo-Identitäten, unveränderte visuelle WorkspaceGeneration, ursprüngliche Exception, abgelehnter Capture des ungültigen Dokumentbaums, SaveIntent und finaler Shutdown. Dies ist ein **nativer Rotnachweis**, kein bestandener A04-Vertrag und keine Mausabnahme. Insgesamt wurden 62 native Prozesse ausgeführt; alle PID-/Exitcode-/HWND-Daten sind erhalten.

### Reproduktion und verbleibende Abdeckung

Im G1-Worktree: `artifacts/g1-a04/run-regression.ps1`, danach `artifacts/g1-a04/repeat.ps1`, danach `eng/gui/Validate-G1LayoutRoles.ps1 -Configuration Both -EvidenceDirectory <neues Verzeichnis im Worktree> -IncludeExistingNative -PersistenceCleanup -OperationContract`. Nur A04: denselben nativen Runner mit `-OperationContractOnly` statt der drei Matrixoptionen verwenden. Für einen einzelnen Solltest nach Enter-Prism und konfigurationspassendem Locked-Restore: `dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Debug --no-restore -m:1 --filter FullyQualifiedName~DockOperationContractTests`. Die reinen NUnit-Testkörper waren von der späteren Korrektur des separaten Native-Probe-Helpers nicht betroffen.

Die umfassenden neuen Admission-/Continuation-/Abort-Gruppen aus Auftrag §11 sind **nicht vollständig implementiert**: insbesondere ungültige Session/Layoutgeneration, kontrollierter Split-/Window-Close-Fehler, Composite-Abort mit erhaltener Originalexception und kontrollierte Fortsetzung nach Fehler. Bestehende A02/A03-Tests ersetzen diesen fehlenden A04-Vertrag nicht. Keine Behauptung einer vollständigen A04-Abnahme.

## Änderungen und Integrität

Neue permanente Dateien: `CryptoWorkBenchGuiTest/DockOperationContractTests.cs`, `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs`, dieser Bericht und `docs/gui/evidence/G1-A04-results.json`. Drei vorhandene Harness-Dateien erhalten ausschließlich die Modusregistrierung/Runner-Option: `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`, `eng/gui/native/Program.cs`, `eng/gui/Validate-G1LayoutRoles.ps1`. Alle produktiven Quellen bleiben bytegleich zur A03-Baseline.

Vollständige Baselinekopien, Abschlussstatus, A04-Diff, Dateihashes, historische Raw-Evidence-Prüfung und zusätzliche Whitespace-/Konfliktprüfung neuer Dateien liegen unter `artifacts/g1-a04`. Abschlussumfang: 1.135 Quelldateien, davon 3 gegenüber der A03-Baseline geändert und 4 neu. Alle 94 bisherigen Reports/Evidence-/Lock-Dateien, 7.654 historischen Artefakte und 451 referenzierten Raw-Evidence-Dateien bleiben bytegleich. `git diff --check`, zusätzliche Whitespace-/Konfliktprüfung, Root-Worktree-Status, Dock-Upstream-Status und Benutzerlayout-Hash sind sauber/unverändert. Das neue Ergebnis-JSON wird nur im externen Abschlussmanifest selbst gehasht, um zyklische Selbsthashes zu vermeiden. Kein Commit, Push, Merge, L1-Import oder A05-Beginn.

## Architekturreview und Freigabe

- Hoch: B4 weiterhin offen; depth als allgemeine Freigabe nicht beseitigt.
- Hoch: kein bewiesener Fortsetzungsnachweis über Manager-/Property-/Host-Grenzen.
- Hoch: Composite-Abort, Fehlererhaltung und allgemeine LayoutFaulted-Überleitung fehlen.
- Mittel: rohe Factory-Eintritte und laufende Session-/Layoutgeneration nicht vollständig geschützt.
- Upgrade-Risiko: alle genannten Reihenfolgen sind für Dock 12.1.0.13 untersucht; keine versionsunabhängige Zusage.
- F01/F03/A02/A03 werden nicht absichtlich verändert. Testresultate entscheiden über erhaltene Regressionen.
- Ein vollständig grüner programmatischer Lauf wäre keine externe Cross-Window-Mausabnahme. F03-Completion-/Eingabeabnahme bleibt offen, sofern nicht tatsächlich neu geprüft.

**CHANGES REQUIRED.** Erforderlich ist eine Eigentümerentscheidung über einen separat beweisbaren Integrationsumfang bzw. eine zusätzliche Upstream-Ausführungs-/Fortsetzungsgrenze. Ein Fork ist nicht vorgeschlagen oder begonnen. A05 wird nicht begonnen.

