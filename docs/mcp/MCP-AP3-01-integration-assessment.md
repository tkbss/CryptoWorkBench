# MCP AP3-01 – GUI-/Observer-Integrationsbewertung

Stand: 29. Oktober 2026. **Bestandsaufnahme und Implementierungsplan; keine produktive AP3-Implementierung und keine Freigabe von MCP-Besitz oder HTTP.**

Der integrierte AP2.1-/F1-Kern ist eine geeignete Grundlage für kleine AP3-Aufträge. Er ist **keine vollständige GUI-Vermittlung**: Search/Replace, Completion, History, Variablen-/Parameterbearbeitung, GUI-Ausführungsadmission und zahlreiche Observer bzw. asynchrone Producer verwenden seine Autorität noch nicht durchgehend. Empfohlen wird als erster begrenzter Auftrag **P1: History-Command einschließlich früher Admission und einer nativen Undo-Gruppe**. Die übrigen Pakete bleiben gesondert zu beauftragen.

## 1. Verifizierte Ausgangsbasis und Methode

### 1.1 Git und Erhaltung

| Merkmal | Verifizierter Stand |
|---|---|
| Autoritatives Quellrepository | `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench` |
| Lokaler `master` / HEAD vor Worktreeanlage | `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00` – `Merge PR #2: Avalonia 12, Prism and MCP integration` |
| Master-Working-Tree | `git status --short` mit Zugriff auf das Quellrepository: leer. Ein vorheriger sandboxbeschränkter Versuch konnte den Status nicht lesen und wurde nicht als sauber gewertet. |
| Neuer separater Worktree | `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3` |
| Neuer Branch | `feature/mcp-ap3`, direkt vom oben verifizierten lokalen `master`; kein abweichender Branchprefix |
| Neuer HEAD / Anfangsstatus | Derselbe volle Commit; `git status --short` leer, Index leer, vor Berichtanlage keine Quelldifferenz |
| AGENTS.md | Keine versionierte AGENTS.md im Quellbaum; in den geprüften übergeordneten Projekt-/Chatverzeichnissen ebenfalls keine gefunden |
| Git-Aktionen | Ausschließlich neuer Branch und neuer Worktree; keine vorhandenen Worktrees umgestellt, bereinigt, zurückgesetzt oder rebased; kein Commit, Push, Merge oder Staging |

Das anfängliche Chatverzeichnis ist ein separates Repository ohne ersten Commit und enthält historische Prüfartefakte. Es wurde nicht mit dem Produktrepository verwechselt oder bereinigt. Die Desktop-Worktreefunktion konnte das außerhalb ihrer Workspace-Liste liegende Quellrepository nicht verwenden; der ausdrücklich beauftragte Worktree wurde deshalb mit `git worktree add -b feature/mcp-ap3 … master` angelegt. Kein Remote-Fetch des Produktrepositories; „aktueller master“ bezeichnet den lokalen, mit der Vorgabe exakt übereinstimmenden Stand.

`.codex/config.toml` wurde weder gelesen, gehasht noch verändert. Builds/Tests laufen ausschließlich im neuen Worktree. Ignorierte `.deps`, `bin`, `obj` und `artifacts/ap3-01` sind Prüf-/Buildausgaben, keine produktiven Quelländerungen. Die bestehende Prism-Quelle und historische Evidenzen werden nicht editiert.

### 1.2 Tatsächlicher Frameworkstand

Die Projektdateien, `Directory.Build.props`, `global.json`, die genehmigte Prism-Konfiguration und nach Restore auch `CryptoWorkBenchAvalonia/obj/project.assets.json` wurden abgeglichen.

| Bestandteil | Stand / Quelle |
|---|---|
| SDK / Zielframework | `10.0.401`, `rollForward=disable`, `allowPrerelease=false`; tatsächlich `dotnet --version = 10.0.401`; Anwendung `net10.0` |
| Avalonia, Fluent, Fonts, XAML Loader | `12.1.4` |
| Avalonia.AvaloniaEdit | `12.0.0`; Paket-Nuspec nennt Quellcommit `86fdebec4cff7affb0e14c7885df71d28edce777` |
| DataGrid / Headless | `12.1.2` / `12.1.4` |
| Prism | Vier offizielle Quellprojekte aus Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`, vorbereitet und unverändert geprüft durch `eng/prism/Prepare-Prism.ps1` |
| Prism-Paketidentität | Restore zeigt die Quellprojekte als `Prism.Avalonia`, `Prism.DryIoc.Avalonia`, `Prism.Core`, `Prism.Events` jeweils `1.0.0` unter abgeschaltetem GitVersioning; das ist **keine** veröffentlichte kompatible NuGet-Releaseversion. Container-Abhängigkeiten `9.0.114`. |
| Prism-eigene Avalonia-Abhängigkeit | Laut unveränderter Integration `12.1.1` beim Kompilieren der Upstreamprojekte; Anwendungsauflösung `12.1.4` |
| UI-Handoff / Testlifecycle | Vorhandenes `UiDispatch` unterdrückt ExecutionContext-Erfassung beim Enqueue; `GuiTestSession` verwendet einen gemeinsamen Headless-Dispatchthread wegen statischer threadgebundener AvaloniaEdit-KeyBindings. |

M3 ist abgeschlossen. Historische R2-/AP0-/F1-Aussagen „Avalonia 11 beibehalten“, „Migration vorgeschaltet“ und „AP3 bis Frameworkentscheidung zurückgestellt“ beschreiben ihren damaligen Stand; sie werden durch diesen Auftrag und die integrierte Masterbasis abgelöst, nicht rückwirkend umgeschrieben. Keine erneute Migration, Paketwahl oder Prism-Quelländerung gehört zu AP3.

### 1.3 Verbindliche Referenzen und Evidenzarten

- [R2-Architekturentscheidung](MCP-00-R2-consolidated-architecture-decision.md), einschließlich später Eigentümerergänzung zum akzeptierten IME-Risiko.
- [AP0-Vertragsfinalisierung](MCP-00-AP0-contract-finalization.md), besonders M01–M51, OBA01–OBA31 und D01–D08.
- [AP2.1-Implementierungsreview](MCP-00-AP2.1-implementation-review.md).
- [AP2.1-F1-Nachreview](MCP-00-AP2.1-F1-independent-rereview.md): F01–F04 korrigiert im dortigen begrenzten Umfang; keine pauschale Inventurabnahme.
- [Migrations-Baselineabgleich](../migration/A12-06-D1-mcp-baseline-reconciliation.md) und [Prism-Integrationsvorgaben](../../eng/prism/README.md).

**Q** bedeutet aktueller Quellbefund auf diesem HEAD; **E** vorhandene historische Evidenz; **V** in AP3-01 erneut ausgeführte Verifikation; **P** zukünftige Arbeit. Eine bestehende positive Funktionsassertion beweist weder vollständige Coordinator-Vermittlung noch native Windows-Sicherheit. Der Bericht bewertet jeden AP0-Eintrag einzeln, einschließlich nicht angebotener Funktionen und späterer Verträge. Ein fehlender Hostadapter ist keine Fremd-API-Ausnahme.

Geprüft wurden die C#-/AXAML-Mutationsstellen, Commandregistrierungen, Subscriptions, Property-/Collectionpublikationen, Dispatcher- und Cancellationfortsetzungen sowie die zugehörigen unveränderten Tests. Methodenanker in den Tabellen gelten für diesen Commit; die Kürzel verweisen auf die folgenden Produktionsdateien.

| Kürzel | Datei / Komponenten |
|---|---|
| C / A / B / E | [EditorCoordinator](../../CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs), [WorkbenchTextArea](../../CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs), [EditorBridge](../../CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs), [WorkbenchTextEditor](../../CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs) |
| V / VM | [CryptoScriptEditView](../../CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs), [Editor-ViewModel](../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs) und zugehörige AXAML |
| CC / RP | [InfoCompletionController einschließlich InfoCompletionData](../../CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs), [InfoCompletionReopenPolicy](../../CryptoWorkBenchAvalonia/Models/InfoCompletionReopenPolicy.cs) |
| S / R / EO / SC | [AutomationService](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs), [Registry](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs), [EditorOperations](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs), [SaveCommit](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs) – Partial-Dateien derselben Serviceinstanz |
| QG / WP / UD | [InterpreterHostQueue](../../CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs), [ExecutionPipeline](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs), [UiDispatch](../../CryptoWorkBenchAvalonia/Services/UiDispatch.cs) |
| H / MV / SB | [HistoryService](../../CryptoWorkBenchAvalonia/Services/HistoryService.cs), [MainViewModel](../../CryptoWorkBenchAvalonia/ViewModels/MainViewModel.cs), [SidebarViewModel](../../CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs) |
| VV / VC / VG | [VariableViewModel](../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs), [VariablePresentationCapture](../../CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs), [VariableView.axaml](../../CryptoWorkBenchAvalonia/Views/VariableView.axaml) |
| KM / IV / ST / VB | [KeyMapViewModel](../../CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs), [InfoViewModel](../../CryptoWorkBenchAvalonia/ViewModels/InfoViewModel.cs), [StatusViewModel](../../CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs), [ViewModelBase](../../CryptoWorkBenchAvalonia/ViewModels/ViewModelBase.cs) |
| PH / PC / RC / APP | [PasswordDialogHost](../../CryptoWorkBenchAvalonia/Services/PasswordDialogHost.cs), [PasswordDialog](../../CryptoWorkBenchAvalonia/Views/PasswordDialog.cs), [EditorReplaceConfirmation](../../CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs), [App](../../CryptoWorkBenchAvalonia/App.axaml.cs) |

### 1.4 Bereits vorhandene Autoritäten und Grenzen

Eine produktive `WorkbenchTextEditor`-Instanz im Editor-AXAML, eine Bridge je Automation-Service, ein von dieser Serviceinstanz erzeugter Coordinator. APP registriert dieselbe `Shared`-Instanz auch über das Serviceinterface. C besitzt `Unavailable/Gui/Releasing/Faulted`, Generation, Active, ObserverDepth und maximal 64 ausstehende Operations-/Callbackverpflichtungen. Es gibt keine aktive MCP-Phase oder Erwerbsmethode.

`TryBegin/Commit` prüfen Originalregistration, Dokument, editorId, Revision, Generation, Update-/Identity-/Mutationlocks und Observerreentranz. `RunGui` erfasst D01-Text-/Historywirkung getrennt. `CallbackTicket` verhindert seit F1 vorzeitigen Abschluss bei Self-dispose. `BeginDrain/ResumeGui` binden den konkreten Übergang; Fokusinvalidierung erteilt keinem alten Ticket neue Rechte. `Observe` allein zählt keine zukünftigen Posts. `RegisterCallback` wird außerhalb des Coordinators im Produkt gegenwärtig nicht verwendet; die umfassenden Producerketten sind deshalb nicht integriert.

QG reserviert prozessweit mit `Wait(0)`. Das serialisiert Interpreter-/Statearbeit, ist aber keine Editorbesitzprüfung. R.RunGuiAsync und S.Mutate/Refresh konsultieren C bisher nicht. Ebenso stehen die eigentlichen EO.UpdateEditorAsync-/ExecuteEditorAsync-Methoden außerhalb der C-Operationsadmission. Die schützenden Identity-/Mutationlocks und `IsExecuting` bleiben nützlich, ersetzen C aber nicht.

## 2. Vollständige Mutationszuordnung M01–M51

Status: **B** = produktiv umgesetzt und im ausdrücklich genannten Umfang nachgewiesen; **T** = teilweise umgesetzt, Integration und/oder Variantenbeleg fehlt; **I** = AP3-Integration fehlt; **S** = der bezeichnete Rest gehört verbindlich erst AP4/AP5/AP6. Kombinationen benennen Teilgrenzen, keinen stillen Abschluss. P1–P8 sind die konkreten Pakete in Abschnitt 5. Native Restprüfungen stehen zusätzlich in Abschnitt 6.

| ID | Aktuelle produktive Callchain / Befund Q | Status und verbindliche Zuordnung |
|---|---|---|
| M01 | A.OnTextInput/OnTextEntering → C.RunGui → einmaliges PerformTextInput; V.TextEntering startet ggf. Ausführung. | **T**, Kern B durch Coordinator-Unicode-/PerformTextInput-Tests; P2/P6/P8 für alle native Varianten und Hostcallbacks. |
| M02 | Mehrzeichiger Unicode läuft durch denselben frühen Eingang; keine Inhaltsklassifikation als IME. | **T**, Unicodekern belegt; P2 native Unicode-/Dead-Key-/AltGr-Matrix. Kein pauschaler Zeichenfilter. |
| M03 | E und A erzwingen InputMethod=false schon per Metadaten/Konstruktor. | **T**, dauerhafte Konfiguration belegt; P2 Regression. Echte CJK-Unterdrückung weiterhin OPEN, U2 INCONCLUSIVE, akzeptiertes Restrisiko; kein neuer AP1-Blocker. |
| M04 | Enter/native Command → V.TextEntering → ExecuteInputAsync → VM.ParseLineAsync → S.RunAsync/R.RunGuiAsync. | **T**: native Admission vorhanden; P8 GUI-Run-/Returnbindung vor erstem await. AP5 vollständiges Return-/Joboutputticket, AP4 Providerdrain. |
| M05 | Shift-Enter/EnterLineBreak erreicht native Handler; ggf. dieselbe Returnkette. | **T**, P2 Aliase/Einmaldelegation; P8/AP5 wie M04, keine zweite Returnausführung. |
| M06 | A.CommandBindings vermittelt Editing-Commands Delete/Backspace/Word-Delete. | **T**, P2 vollständige native Key-/Selection-/ReadOnly-Varianten und Fehlerabschluss. |
| M07 | Geroutete ApplicationCommands.Delete/Editing-Fallback über A-Bindings. | **T**, P2 auch direkte Command-/Context-/Fallbackroute separat nachweisen. Kein Schluss aus KeyDown allein. |
| M08 | A.OnKeyDown umschließt Tab bei AcceptsTab und Fokus mit RunGui. | **T**, P2 Shift-Tab, Mehrzeilen-/Rechteckauswahl, Strategiecallbacks; P6 Observer. |
| M09 | Native Undo-/Redo-Bindings bleiben erhalten; A vermittelt vor nativer Delegation, C erfasst History getrennt. | **T**, produktiver Kern B durch Registration/Gesten/History-only/Fault-Tests; P2 Windows- und alternative Routen. Historischer U1-Befund nicht neu als Gesamtnachweis ausgeben. |
| M10 | A.ClipboardAsync: TryBegin → Export-await → IsCurrent/Selection/Caret → Commit → Cut. | **T**, Basis B durch Clipboardtests; P4 Exportfehler, Cancel/Drain und echte Provider. Keine Löschung nach fehlgeschlagenem Export. |
| M11 | WholeLine-Cut berücksichtigt deletable Segments; sonst native Selection.ReplaceSelectionWithText. | **T**, P4 Rechteck/Teil-ReadOnly/WholeLine und Ereignisäquivalenz fehlen. |
| M12 | Paste nutzt Avalonia-12-TryGetTextAsync, prüft nach await Originalticket und Auswahl. | **T**, verspäteter Paste bei Swap/Drain belegt; P4 übrige Invalidierungen und Providerfälle. |
| M13 | Paste normalisiert Newlines/Tabs; eigener Hostadapter statt vollständigem nativen Clipboardhandler. | **T**, P4 native Format-/WholeLine-/Rectangle-/Null-/ReadOnly-Äquivalenz beweisen, nicht unterstellen. |
| M14 | Natives SearchPanel hat eigene ReplaceNext-/ReplaceAll-Bindings; im Host kein Adapter. | **I**, P2 frühe Grenze am Panel vor Find/Selection/RunUpdate/Replace. |
| M15 | Nativer SearchInputHandler/NestedInputHandler-Fallback ist nicht in A's Editing-Commandmenge nachgewiesen. | **I**, P2 separate TextArea-/globale Suchroute vermitteln und testen. |
| M16 | AvaloniaEdit.TextEditor.OnApplyTemplate installiert SearchPanel; Host hat keinen Installations-/Erneuerungsadapter. | **I**, P2 jede Panelinstanz früh einbinden, P6 Lifecycle. Reine Suche bleibt erlaubt. |
| M17 | CompletionWindow → InfoCompletionData.Complete → Document.Replace → `_completed`; keine C-Admission. | **I**, P3 separate Commitgrenze mit ursprünglicher Session/Dokument/editorId/Generation; D01 nach Replace. |
| M18 | CC Caret/KeyDown/Document/Closed und RP.Changing steuern Refresh/Reopen; Dokument-/Anchorbindung, keine C-Generation/Observe. | **T**, P3/P6; spätes Schließen und Wiederöffnen dürfen keine neue Session beeinflussen. |
| M19 | MV.LineHistory… → H.LineBack/Forward (Index vor Event) → VM.OnHistoryChanged → Remove/Insert/IsModified. | **I**, P1 Admission vor Historyindexänderung und ganzer Mutation; derzeit nur IsExecuting, keine gemeinsame Updategruppe. |
| M20 | Text-Copy-Drop durch frühes dauerhaftes EnableTextDragDrop=false unterbunden. | **T**, Konfigurationskern B; P2 native eingehende Dropregression. Kein neuer Dropadapter. |
| M21 | A.OnOptionChanged setzt Drag-Aus vor base; Selection-/Pointerrouting bleibt. | **T**, P2/P6 Options-/Template-/Kindcontrolfälle. Datei-Drop außerhalb MVP. |
| M22 | Nativer StartDrag soll durch dieselbe frühe Konfiguration nie beginnen. | **T**, P2 Windows-Startversuch; keine spätere Flagänderung als Drainbeweis. |
| M23 | Interner/externer Move ist nicht angeboten; Source-/Targeteffekt soll ausbleiben. | **T**, P2 native Gegenproben; historische H2/H2R-Targetfehler bleiben erhalten. |
| M24 | DeleteLine gehört zum nativen Editing-Commandpfad. | **T**, P2 einzelne Commandprobe, Undo-Gruppe und Reentranz. |
| M25 | RemoveLeading/TrailingWhitespace über Editing-Commands. | **T**, P2 Composite-/Teilfehler- und Auswahltests, keine neue Textalgorithmik. |
| M26 | Upper/Lower/InvertCase über Editing-Commands. | **T**, P2 Unicode/Selection/History und fehlernde Observer. |
| M27 | Keine eigene TitleCase-UI; erreichbarer nativer Editing-Command bleibt relevant. | **T**, P2 frühe Ablehnung/konservativer Fehlerabschluss; D07 bleibt, kein Menü/Shortcut ergänzen, H1-FAIL nicht umwerten. |
| M28 | TabsToSpaces/SpacesToTabs einschließlich Leading-Varianten. | **T**, P2 komplette native Commandmenge/Gruppierung, auch ohne Keygesture. |
| M29 | Tab/IndentationStrategy/Selection im nativen Ablauf. | **T**, P2 Strategie-/Fallbackfälle; P6 Callbackgrenze ohne ambient Schreibrecht. |
| M30 | ToggleOverstrike/anschließender TextInput verwenden native Zustands-/Eingaberouten. | **T**, P2 Toggle, virtuelle Auswahl, nachfolgender Commit und Rückmeldung. |
| M31 | Navigation/Selection/Find bleiben lesend; Copy wird in A über `Allowed` und TryBegin wie Schreibarbeit behandelt. | **T**, P4 Copy von Mutationsadmission trennen; P2/P6 Pointer/Find-Observer. Lesen/Copy dürfen durch zukünftigen Besitz nicht pauschal gesperrt werden. |
| M32 | Nur V.TextEntered/Caret verwenden produktiv Observe; viele Hostsubscriber ungeschützt. | **I**, P6/P7 plus paketlokale Observer. Unvermittelte Callbacks können während Active direkt schreiben; C ist keine automatische Eventhülle. |
| M33 | V-Konstruktor Clear und VM.TextEditor vor Attach; Attached setzt Registration/Completion. | **T**, P6 unverfügbarer Initialisierungszustand, einmalige Registrierung und Ausnahme-/Reattachfolge; Clear hat Historywirkung. |
| M34 | E.Document-Coerce → C.BeforeDocumentChange; B.Attach/Detach und VM.AfterIdentity. | **T**, AP2.1/F1-Originalbindung B; P6 GUI-Lifecycle/Properties; **AP4** globale Shutdown-/Dialog-/Saveabschlüsse. |
| M35 | SB.OpenScriptBook: IsExecuting vor Picker; nach Picker/Clear-Dialog aktueller VM-Aufruf ohne früheren Intent. Close/Escape/Cancel bereits als Abbruch korrigiert. | **T**, P8 AP3-Aufrufgrenze; **AP4** Picker-/Zustimmungsintent mit Generation/Zielbindung. Bestehenden F1-Cancel-Fix erhalten. |
| M36 | VM.OpenScriptBookAsync → ReadAllLinesAsync → statisches ClearAdmitted → UI Clear/Append/Present; Lifecyclecheck schützt nur Teile. | **T**, P8 GUI-/Originalbindung und P5 Instanzverdrahtung; **AP5** instanzgebundenes bereits-admittiertes Clear, Stateversion-/Jobintegration. Keine neue Q-Reservation innerhalb des Runs. |
| M37 | EO.UpdateEditorAsync hat Q/Identity-/Revisionguards, jedoch kein C-Ticket; alter v1-EndUpdate-/Antwortpfad bleibt. | **T**, P8 frühe hostinterne Admission/Receiptgrenze; **AP4** Bestätigungsproducer, **AP6** Lease/v2/D01-Transport. Vorher nicht als D01-komplett oder extern freigegeben werten. |
| M38 | EO.ExecuteEditorAsync nimmt GUI-Jobs an und fügt Präsentationsoutput weiterhin in Quelltext ein. | **S AP5** EC21 und Job-/Outputabschluss; P8 AP3-GUI-Admission vorbereiten; **AP6** externe Lease/v2/Zustimmung. Historischer Test erwartet noch out:-Mutation. |
| M39 | V.ExecuteInputAsync wartet auf ParseLine und fügt PrintMessage am Anchor ein; Lifecycle/Dokumentprüfung vorhanden, kein Outputticket. | **T**, P8 AP3-Erfassung/Invalidierung vor await; **AP5** kompatible Anchorfortsetzung und vollständiger Abschluss nach Runnerende. Nicht mit strict-revision-Paste gleichsetzen. |
| M40 | Print/Info → VM-/KM-Posts → Status/Variablen/Navigation; WP sammelt separat. | **T**, P7 rein präsentierende Observer/Posts binden; P8 Originalauftrag übergeben; **AP5** vollständige Job-/Outputkorrelation, **AP4** Gesamtdrain. |
| M41 | SB.Save → CaptureSaveIntent → Ordneranlage/Picker/Overwrite → SC.Delete/Write/Unlock. | **S AP4**, vorhandene Snapshot-/Q-/Savecompletionbasis; Generation fehlt, auch CreateFolder ist Dateiwirkung. Dateisemantik unverändert lassen. |
| M42 | Synchrones SC.SaveScriptBook reserviert Q und MutationLock, aber keine C-Admisson. | **S AP4**, synchrone Savegrenze/Propertycallbacks/Receipt und Originalfinalisierung. |
| M43 | R.Shutdown/Drain/Dispose + APP Closed/Exit; Registry-/Save-Tasks sind kein vollständiger Editordrain. | **S AP4**, Gesamtabschluss/GUI-Freigabe; **AP6** Reclaim/Expiry/Credentials/Epochzertifikat/Zeitquelle. Kein automatischer Reset. |
| M44 | E.BeforeUpdate/BeforeChange/Identity-/Mutationlocks ergänzen frühe C-Grenze; alter EndUpdate-Ausgleich vorhanden. | **T**, AP2.1-Grundkern B; P2/P6/P8 schließen frühe Hostpfade. R2/R4/R6/R7 nicht als allgemeine Libraryfehler behoben deklarieren. |
| M45 | VG-Button → VV.OnModifyValue → FindSelected → Shared.Mutate → globaler State. | **T**, Q/Capture vorhanden; P5 C-Admission, instanzgleicher Service, Entwurf-/Auswahl-/Stateversionsbindung. |
| M46 | Parameterbutton → VV.OnModifyVariable → SetParameter/Dictionary.Update. | **T**, P5 wie M45; alter Parametergegenstand darf nicht auf eine neu selektierte Variable angewandt werden. |
| M47 | WP.PasswordRequested → PH/PC; Request-/Cancellationdrain im Pipelinepfad, kein C-Producer. | **S AP4** Dialog/Provider und **AP5** Jobbindung; keine bloße GUI-Observerhülle als vollständiger Nachweis. |
| M48 | Vorhandener StatusTransport/Abstractions/Service; kein neues Lease-v2-Tool. | **S AP6**, striktes v2/Rechte/Epochfence/CAS/Projektion; HTTP erst spätere separate Hostfreigabe. |
| M49 | KM-Konstruktor → Shared.ExecuteSubmissionAsync("Info(keymap)") → R.RunGuiAsync → WP → UD. | **T**, P5 gleiche Serviceinstanz, P8 gemeinsame GUI-Run-Admisson, P7 Post-Lifetime; **AP5** durchgängige Job-/Stateversion. Kein privilegierter Info-Run. |
| M50 | VV.SetupVariables → Shared.Refresh → QG.RunState(CaptureMutation, ApplyVariables). | **T**, Kopie unter Q vorhanden, kein Versionsinkrement beim Lesen; P5 Busy/stale-Snapshot und Observer-/Instanzbindung. |
| M51 | VG.Value-TwoWay und editierbares Parametergrid → detached VariableModel/ParameterModel.SetProperty. | **T**, Kopien B durch VC; P5 Editcontrols, Entwurfversion/Selection und Commitprüfung, P6 Modelproperties. UI-Editing ist noch nicht besitzgebunden. |

## 3. Vollständige Observerzuordnung OBA01–OBA31

„Observer“ ist keine pauschale Erlaubnis zum Schreiben. Autoritätsinterne Identitätsbuchhaltung, schreibende Commands, native Teilalgorithmen und reine Präsentation benötigen unterschiedliche Grenzen. Insbesondere darf ein RunGui-Commit nicht aus einem Observe-Callback gestartet werden.

| ID | Aktueller Befund Q / vorhandener Beleg | Status, Grenze und Paket |
|---|---|---|
| OBA01 | E.TextEntering prüft MutationLocked; A vermittelt TextInput früh. | **T**, Kern B; P2 native Delegation, P6 eigene Subscriber; kein nachträglicher allgemeiner Eventguard. |
| OBA02 | E.UpdateStarted/Changing schützen spät; BeforeUpdate balanciert den abgewiesenen BeginUpdate. | **T**, P2/P6 frühe Admission vor diesen Events; R6/R7 bleiben. Kein Reparatur-EndUpdate ergänzen. |
| OBA03 | V.TextEntered ist Observe; TextEntering enthält Completion-/Returnstart ohne eigene Trennung. | **T**, P3/P8: Präsentation O, angenommene native Aktion bzw. explizite Returnfortsetzung separat. |
| OBA04 | V.Caret.PositionChanged → ObserveEditor → StatusText.Text. | **B** für diesen konkreten Subscriber; P6 prüft weitere Caret-/Selectionsubscriber, kein Schluss auf alle Fokuscallbacks. |
| OBA05 | V.Attached/Detached verbindet VM/B und disposed CC; Fehler kann nachfolgende Cleanupschritte verhindern. | **T**, P6 lokale Lifecyclegrenze/Unsubscribe, AP4 Gesamtdrain. |
| OBA06 | `_insightWindow.Closed` löscht Feld ohne Senderprüfung; MyOverloadProvider.PropertyChanged direkt. | **I**, P3 instanzgebundener Closed-Callback und Observe. Alter Popupabschluss darf neuen nicht löschen. |
| OBA07 | CC.Caret/KeyDown/DocumentChanged direkt registriert. | **I**, P3 O-/Sessiongrenze; Commit separat, kein Recht aus Refresh. |
| OBA08 | CC.Document.UpdateStarted/Changed/UpdateFinished ohne Observe; lokale Pending-Changefelder. | **I**, P3 O an Originaldokument; Fehler-/Dispose-/Reopenfolge und native Reentranz. |
| OBA09 | CC.Window_Closed prüft Sender bereits, aber keine C-/Generationsbindung. | **T**, P3 ursprüngliche Session schließen, keine Nachfolgewirkung. |
| OBA10 | InfoCompletionData._completed läuft direkt nach Replace. | **I**, P3 kontrollierter Abschluss desselben Commits; Throw nach Wirkung muss applied erhalten. |
| OBA11 | RP.Document.Changing unsubscribt am alten Dokument, bleibt unkontrollierter Callback. | **T**, P3 Observe/Lifetime und Swap-/Disposeprobe. |
| OBA12 | B.DocumentChanged/TextChanged prüfen Originalsender; Current/Snapshot beobachten vor reentrantem Read nach. | **B** für AP2.1-/F1-Identitäts-/Revisionskern; P6 Kompositionsregression. Kritische Buchhaltung nicht durch generisches Exception-Schlucken ersetzen. |
| OBA13 | H.LineHistoryChanged → VM.OnHistoryChanged schreibt Dokument. | **I**, P1 expliziter Mutationsauftrag vor H-Indexwechsel, kein reiner Observe-Callback. |
| OBA14 | VM.OnPrintEvent bindet off-UI-Post an EditorLifecycle, kein Callbackticket/Job. | **T**, P7 Post zählen/Originalbindung; AP5 Output-/Jobabschluss. |
| OBA15 | VM.OnInfoEvent postet ohne Lifecyclecheck und navigiert. | **I**, P7 Lifetime/Generation/Jobursprung vor Navigation prüfen, O. |
| OBA16 | KM.OnInfoEvent und LoadInfoAsync-Erfolg/Fehler posten ohne Prüfung nach Dispose. | **I**, P7 Lifetime und kontrollierte Publikation; P8 separater Run. |
| OBA17 | VB/Status/Sidebar/VV nutzen Prism SetProperty/RaisePropertyChanged ohne Hostgrenze. | **I**, P6 eigene Publikations-/Subscribergrenzen; CanExecute nur Anzeige. Keine Änderung an Prism. |
| OBA18 | MV/IV/KM erben direkt BindableBase und umgehen einen eventuellen VB-Adapter. | **I**, P6 ausdrücklich einbeziehen; Navigation/Propertydispatch ebenfalls O. |
| OBA19 | ST.Diagnostics.CollectionChanged → RaisePropertyChanged(HasDiagnostics) direkt. | **T**, Diagnosekopien vorhanden; P6 Collection- und Folgepropertygrenze, Capture-/Präsentationsfehler nicht als Erfolg ausgeben. |
| OBA20 | VV.ApplyVariables Clear/Add/SelectedVariable und Modellproperties ohne Generation/Version. | **T**, P5/P6: detached Entwürfe, publizierter Snapshot, kontrollierte Collection-/Propertycallbacks. |
| OBA21 | H.InfoHistoryChanged/IV und InfoView.DataContextChanged/Markdowncommand direkt. | **I**, P6/P7 viewgebundene O-/Navigationgrenze; alte DataContexts invalidieren. |
| OBA22 | E.IsModified/IsReadOnly/Document können Properties/History/Identitycallbacks auslösen. | **T**, Identity/Save-Lockbasis vorhanden; P1/P2/P6/P8 lokale Wirkung/Observergrenzen, AP4 Save-Unlock. |
| OBA23 | RC yes/no und Cancellation.Post schließen gebundenes Dialogobjekt; noch keine C-Generation/Producerzählung. | **S AP4**, AP6 ursprüngliche Lease-/Epochprüfung; späte Antwort ist keine neue Zulassung. |
| OBA24 | PC Confirm/Cancel/Closed/Opened inklusive Passwortleeren/Fokus. | **S AP4**, O und Requestbindung; keine Secretprotokollierung. |
| OBA25 | PH.OwnerClosed/Cancellation.Register/Post und Antwortentsorgung. | **S AP4**, registrierter Callback-/Providerdrain; AP5 Executionbindung. |
| OBA26 | R Timer/UserCancellation/Retention/Publish haben serviceeigene Ressourcenfinalisierung. | **S AP4/AP5**, Editor-/Callbackabschluss ergänzen; **AP6** Epochfence/Timer-/Kapazitätsnachweis. Kein UI-Warten unter `sync`. |
| OBA27 | WP Password/Print/Info werden im finally entfernt; Passworttasks abgewartet, Diagnostik kopiert. | **S AP5**, vorhandene Pipeline erhalten; AP4 Provider-, AP6 Gesamtzertifikat. AP3-P7 nur GUI-Empfängerseite. |
| OBA28 | GUI beforeLine/afterLine/Present/appendLine per OnUiAsync; teilweise Lifecyclechecks. | **T**, P8 explizite ursprüngliche GUI-Auftragsbindung; **AP5** Job-/Book-/Returnabschluss. |
| OBA29 | Save Picker/Overwrite/Delete/Write/Lock/Unlock/finally separat erfasst. | **S AP4**, Generation und Provider-/Dateiwirkungsgrenze einschließlich Ordneranlage. |
| OBA30 | APP Closed → VM.Dispose, Desktop.Exit → Service-Shutdown; lokaler Detach vorhanden. | **S AP4**, vollständiger asynchroner Drain/idempotentes Dispose; P6 nur lokale Subscriberlebensdauer. |
| OBA31 | A.LostFocus invalidiert Generation; Drag-/IME-Metadaten/Optionsgrenze vorhanden. Übrige Fokus/Selection/Anchor/LineTracker/Templatecallbacks nicht pauschal unter O. | **T**, P2/P3/P6. F03 lokaler Drainfehler korrigiert; H3 später Fokusobserver bleibt gesonderte offene Integrationsklasse. |

## 4. Konkrete Lücken und Prioritäten

Priorität bedeutet Reihenfolge vor AP3-Abnahme, keine Behauptung einer heute aktiven externen Angriffsfläche. **P0** blockiert die vollständige AP3-Autoritätsaussage; **P1** betrifft wesentliche Varianten-/Lifecyclebelege. Die Tabelle nennt Quellbefunde; nur ausdrücklich bezeichnete Tests sind dynamische Reproduktionen.

| Finding | Prio | Konkreter Befund / Konsequenz | Bearbeitung |
|---|---|---|---|
| G01 | P0 | MV → HistoryService verschiebt Index bereits vor VM.Remove/Insert. Ohne frühe C-Grenze kann Ablehnung erst nach Historynavigation erfolgen; Remove und Insert sind getrennt. | P1 |
| G02 | P0 | Panel-Replace läuft außerhalb A's Editing-Liste. Später Dokumentguard beweist nicht Wirkungslosigkeit vor RunUpdate/Selection. | P2 |
| G03 | P0 | InfoCompletionData.Replace besitzt weder Sessiongeneration noch Ticket. Closed/Refresh sind weitere reentrante Einfallspunkte. | P3 |
| G04 | P0 | R.RunGuiAsync und S.Mutate reservieren nur Q; Releasing/Faulted/ObserverDepth des Editors werden nicht geprüft. `IsExecuting` ist keine Besitzautorität. | P5/P8 |
| G05 | P0 | VV-Kommandoparameter und SelectedVariable können auseinanderfallen; FindSelected bindet nur Identifier/Typ. Bei Refresh bleibt SelectedVariable unversioniert. Alte Parameter-/Wertentwürfe können einen neuen State treffen. | P5 |
| G06 | P0 | Produkt verwendet CallbackTicket nicht für Posts; Info-/KeyMap-Callbacks können nach Dispose/Generationwechsel publizieren. Hintergrundpriorität beweist keinen Drain. | P6/P7; AP4/AP5 Gesamtabschluss |
| G07 | P0 | Return hat kein Outputticket nach Runnerende. Strikte IsCurrent-Revisionsprüfung eines normalen Clipboardtickets wäre wegen erlaubten Weitertippens falsch. | P8/AP5 |
| G08 | P0 | `ClearAdmitted` schreibt globale Variablen, erhöht aber ausdrücklich `Shared` statt die aufrufende Serviceversion. VV/KM umgehen ebenfalls injizierte Instanzen. | P5 Verdrahtung; AP5 bereits-admittiertes Clear/Ergebnisversion |
| G09 | P1 | Copy nutzt Allowed/TryBegin und wird dadurch bei Releasing/Faulted wie Mutation blockiert. Read-only-Besitzmodell noch nicht getrennt. | P4 |
| G10 | P1 | Clipboardadapter hat eigene Auswahl-/Normalisierungslogik; native Ereignis-/Formatäquivalenz für Rectangle/WholeLine/ReadOnly nicht vollständig belegt. | P4 |
| G11 | P0 | EO.UpdateEditorAsync umgeht C und enthält weiterhin eigenen EndUpdate-Abschluss sowie v1-Antworten. Kein vollständiger D01-Nachweis aus positiven Editortests ableitbar. | P8/AP4/AP6, klare Schnittstelle statt stiller DTO-Umdeutung |
| G12 | P1 | Frühe Init-Clear-, Options-/Template-, unkontrollierte Properties und Overload-Closed-Pfade verbleiben. | P2/P3/P6 |

### StateVersion und Revision

B bleibt die einzige Quelle für editorId/Revision. Weder Count der TextChanges noch ein vermutetes `+1` pro Befehl ist ein Ersatz. History-only-Mutationen können die Revision unverändert lassen und müssen trotzdem im D01-Beleg erscheinen. Alte Ergebnisse dürfen nie die Revision eines Nachfolgedokuments erhalten.

C.Generation invalidiert auch bei unverändertem Text. Normale Inputintents werden durch Fokusverlust verworfen. Ein absichtlich modal wartender Picker-/Bestätigungsintent braucht eine ausdrücklich typisierte Regel: Der erwartete Fokuswechsel zum eigenen Dialog darf nicht mit einer Besitz-/Dokumententwertung gleichgesetzt werden. Die heutige einfache Generation darf dafür nicht global abgeschwächt werden; AP4 muss die konkrete Bindung im selben Coordinator festlegen.

R/S.Version beschreibt Variablen-/Interpreterzustand. `Mutate` erhöht konservativ im finally; `Refresh` erfasst ohne Inkremement. Die detached VariableModel-Kopien tragen heute keine StateVersion/Entwurfbindung. AP3-P5 muss Serviceidentität und Draftbindung vorbereiten und GUI-Mutationen konsistent publizieren. Die durchgängige Book-/Executionversion einschließlich `ClearAdmitted` ist AP5. Weder Editorrevision noch UI-Selection ersetzen diese Version.

### Historische Findings bleiben sichtbar

- H1 TitleCase-FAIL und H2/H2R-Target-/Movefehler werden nicht als behoben umetikettiert; TitleCase/Textdrag bleiben gemäß Produktentscheid nicht angebotene Funktionen, erreichbare native Pfade sind trotzdem abzusichern.
- R2/R4/R6/R7 gelten weiter für die breitere direkte Library-API. Positive Hosttests belegen einzelne vermittelte Aufrufketten, keine globale Reparatur von Setter-/History-/EndUpdate-Verhalten.
- H3s später ungeschützter Fokusobserver ist nicht mit F03 gleichzusetzen: F03 repariert die Identität eines lokalen Drains, nicht alle Fokusobserver.
- F01–F04 aus AP2.1 sind gemäß F1-Nachreview geschlossen, und ihre unveränderten Tests gehören zur neuen Regression. Das schließt keine weitere OBA-Gruppe automatisch.
- U2 bleibt INCONCLUSIVE; CJK-Negativtests technisch OPEN bei akzeptiertem Restrisiko. Dauerhafte frühe IME-Deaktivierung bleibt zwingend, auch nach Reinitialisierung und künftigem Release.
- Der historische Headless-Session-Disposefehler bleibt Ursache INCONCLUSIVE. Die Migrations-Testsession und ein neuer erfolgreicher Lauf beweisen nicht rückwirkend seine Ursache.
- EC21: Der aktuelle Produktcode und `WholeEditorPrintIsInsertedOnceWithoutDuplicatingSource` erwarten weiterhin zusätzliche out:-Zeilen. Ein PASS dieses Tests erfüllt D05 nicht; Änderung ausschließlich im gesonderten AP5-Auftrag.
- D02-S1 und lokaler C.Drain sind kein produktives L2-Epochenzertifikat. Keine Tombstonefreigabe oder Leaseaktivierung aus diesem Bericht.

## 5. Kleine, getrennt abnehmbare Implementierungspakete

Alle Pakete sind **Planung**, keine Umsetzungserlaubnis dieses Auftrags. Gemeinsame Regeln: dieselbe Service-/Bridge-/Coordinatorinstanz; UI-affine Admission vor fremdem Callback; Q zusätzlich für State-/Run-/Serviceedit; kein Lock/RunUpdate über await; keine ambient oder öffentliche Callbackberechtigung. Fehler nach Wirkung dürfen nicht als Busy/none erscheinen. Unsicherer nativer Abschluss bleibt faulted; keine automatischen Retries oder Historyreparatur. Cancellation entwertet neue Arbeit, beendet aber keine laufende Originalfinalisierung. Eine Reentranzprobe muss sowohl einen neuen Hostcommand als auch einen direkten Dokument-/Stateversuch prüfen.

### P1 – History als ein synchroner GUI-Auftrag (erster Auftrag)

**Dateien:** MV, H/IHistoryService, VM.OnHistoryChanged, eng begrenzter serviceinterner Adapter in S/EO; bestehende C-/B-APIs verwenden. Tests in einer neuen History-Integrationsklasse, vorhandene Assertions unverändert.

**Lücke:** MainView-LineBack/Forward → H ändert Index → Event → VM Remove/Insert/Document/IsModified. `IsExecuting` verhindert keinen C-Observer-/Drainfall. Ein bloßer Wrapper um OnHistoryChanged wäre zu spät für den Historyindex.

**Grenze:** Frühe synchrone C-Admission am tatsächlichen History-Command vor Indexänderung; bindet Editor/Registration. Die akzeptierte Historyaktion delegiert genau einmal zur bisherigen Historynavigation und führt die zusammengehörige Textänderung in einer nativen Updategruppe aus. Das schreibende Historyevent ist eine ausdrücklich gebundene Fortsetzung dieses Auftrags, kein beliebiger Observer. Direkte weitere produktive H-Aufrufer müssen denselben Eingang verwenden; neue Aufrufe aus Subscriber erhalten Busy. IsModified-/Statusfolgerungen einzeln kontrolliert beobachten. Kein pauschales Observerrecht und keine zweite Historyengine.

**Fehler/Reentranz/Cancellation/spät:** Vor Admission keinerlei Index-/Text-/Historywirkung. Nach begonnenem Auftrag Teilwirkung im Originalreceipt, kein Rücksetzen des Indexes als angebliches Dokumentrollback. Bei Update-/Dirtycallbackfehler Wirkung erhalten, bei unklarem Historyabschluss faulted. Synchroner Auftrag besitzt kein await; ein später angeliefertes fremdes Historyevent ist keine gültige Fortsetzung. Detach während Wirkung finalisiert nur das Original.

**Tests:** Unit: Back/Forward-Auswahl, keine Navigation bei Ablehnung, genau eine Delegation. Headless: echte MainView-Commandbindung, zwei native Undo-/Redo-Schritte, leere/mehrzeilige letzte Zeile, Unicode, Releasing/Faulted, dokument-/propertyseitige Reentranz, Throw nach Remove bzw. Insert, Detach und neue Registrierung. Windows: Historybuttons und Tastaturfolge mit nachfolgendem Ctrl+Z/Y, Fokuswechsel und Fehlermeldung.

**Abnahme:** Je erfolgreicher Historyersetzung genau eine native Undo-Gruppe und korrekter Originalreceipt; abgewiesene Aktion verändert weder Navigationsindex noch Text/Undo/Revision. Kein zweiter Insert, keine Nachfolgerwirkung, 35 Coordinatorfälle und vorhandene History-/GUI-Regressionen unverändert grün. Abhängig nur von AP2.1; keine AP4-Datei-/Dialog-, AP5-Ausführungs- oder AP6-Leasearbeit.

### P2 – Native Commands, SearchPanel und Templateinstallation

**Dateien:** A/E, ggf. neuer hostinterner Search-Adapter unter Controls/Services; V nur zur lokalen Verdrahtung. Keine AvaloniaEdit-/Prism-Quelländerung.

**Lücke/Grenze:** Bestehende Editing-/Undo-Delegation bewahren und die tatsächlich registrierte Commandmenge auf 12.0.0 inventarisieren. Nach base.OnApplyTemplate das konkrete öffentliche SearchPanel vor Nutzbarkeit anbinden; ReplaceNext/All-Bindings am Panel und SearchInputHandler-Fallback vermitteln, native Implementierung genau einmal aufrufen. Hosteigene direkte Replace-Aufrufe müssen denselben Adapter nutzen. Alte Panelbindungen sauber lösen; Templatewiederholung darf keinen zweiten wirksamen Handler behalten. Suchfeld-Undo/Clipboard ist nicht Dokument-Undo. Reine Suche/Navigation bleibt O und ohne Schreibrecht.

**Fehler/Reentranz/Cancellation/spät:** Ungültige Regex ohne native Mutation; Fehler nach einem ReplaceAll-Teilabschnitt anhand tatsächlicher Wirkung, nicht pauschal none. Keine neue Algorithmik für Regex-Ersetzung oder Undo. Veraltete Panelinstanz nach Dokument-/Templatewechsel darf nicht schreiben. Synchroner Commit ohne await; Reentranz vor nativer Delegation ablehnen. Native Fehlerroute einschließlich TitleCase konservativ abschließen.

**Tests:** Unit: vollständige Command-/Aliaszuordnung und idempotentes Binden/Entbinden. Headless: Panelbuttons und TextArea-/direkte Commandroute, ReplaceNext/All/Regex/Mehrzeile/invalid/ReadOnly, Tab/Shift-Tab, Deletevarianten, Whitespace/Case/Tabkonvertierung/Overstrike; Historysignale bei Ablehnung, erneutes Template/Attach, fehlernde Ergebnis-/Selectionobserver. Windows: Ctrl+F/H, Panelbutton/Shortcut/Contextpfad, Ctrl+Z/Y, Tab/Rechteck/Schweizer Unicode, Dragversuch vor Erstfokus und nach Optionswechsel.

**Abnahme:** Alle produktiv erreichbaren Replaceeingänge erreichen dieselbe frühe C-Grenze; Lesesuche funktioniert im gesperrten Kontrollzustand, null Text-/Historywirkung bei Ablehnung; native erfolgreiche Algorithmen/Undo erhalten. Kein angebotener TitleCase und kein Textdrag. P6 ergänzt übrige Observer; keine AP4–AP6-Funktion vorziehen. Falls eine benötigte native Route mit öffentlichen Hostgrenzen nicht kontrollierbar ist: konkreter Gegenbeweis und Architekturentscheidung, kein stiller Bibliothekspatch.

### P3 – Completion-/Overloadsession und Commit

**Dateien:** CC, RP, V einschließlich MyOverloadProvider; kleine interne Session-/Commitbindung, C/B weiterverwenden.

**Lücke/Grenze:** CompletionWindow schließt vor `ICompletionData.Complete`; ein Closed-Callback kann daher bereits vor dem Replace laufen. Commitbindung muss ursprüngliche Session, Registration/Dokument/editorId und Generation vor dem Popupabschluss besitzen und unmittelbar vor Replace geprüft werden. Normales Tippen darf eine aktuelle Session aktualisieren; die Revision wird für den tatsächlichen Commit gebunden, nicht dauerhaft auf die Öffnungsrevision eingefroren. O um Refresh/Changing/Caret/Closed/Providerproperties, eigene Commitadmission außerhalb O. `_completed` wird kontrollierter Abschluss derselben Operation.

**Fehler/Reentranz/Cancellation/spät:** Escape/Dispose/Owner-/Dokumentwechsel invalidiert Session; alte Closed-Ereignisse löschen nur eigene Instanz. Kein Wiederöffnen aus entwertetem RP-Callback. Throw nach Replace bleibt applied mit CallbackFailed; unklarer nativer Abschluss faulted. Keine automatische Wiederholung und kein generelles Schreiben im TextEntered-Observer. Keine Cancellationwarteschleife.

**Tests:** Unit: Sessiongültigkeit, Reopenpolicy und alte Closed-Abfolge. Headless: tatsächliches Popup und RequestInsertion per Tab/Enter/Mausroute, Fensterclose vor Commit, laufendes Tippen/Backspace-Reopen, fehlernder `_completed`, Caret-/Dokument-/Generationwechsel, Releasing/Faulted, Dispose während Refresh. Windows: Popupfokus, Annahmevarianten, Overloadfenster und wiederholtes Öffnen/Schließen.

**Abnahme:** Ein Replace/Undo-Schritt pro gültiger Annahme; stale Session null Dokumentwirkung und kein Nachfolger-Popupverlust; Observer können keine zweite Mutation starten. P2/P6 koordinieren native Key- und Lifecyclegrenzen, ohne doppeltes TryBegin innerhalb bereits Active. AP4–AP6 nur spätere Integration, kein neues Lease-/Dialogprotokoll.

### P4 – Clipboardvarianten und erlaubtes Lesen

**Dateien:** A.ClipboardAsync/CommandBindings, ggf. hostinterner Intentadapter; C nur für notwendige typisierte Bindung, kein zusätzlicher Rechtezustand.

**Lücke/Grenze:** Copy aus der Schreibzulassung lösen: Text/Auswahl synchron kopieren, anschließend Clipboardexport, kein späterer Dokumentwrite und keine Q-Reservation. Cut/Paste bleiben an ursprüngliches Ticket, Dokument, Generation, Revision, Caret und Selection gebunden; vor Commit unmittelbar revalidieren. Native Format-/Selectionsemantik gegen die integrierte 12.0.0-Version abgleichen. Ein reiner Intent darf später durch Besitzwechsel entwertbar bleiben; er ist keine angenommene lange Mutation.

**Fehler/Reentranz/Cancellation/spät:** Null/Providerfehler vor Commit ohne Löschung; Clipboardexport kann bereits extern sichtbar sein, auch wenn der nachfolgende Cut verworfen wird – D01 beschreibt Text/History, nicht einen Rückbau des Clipboardinhalts. Await niemals mit offener Updategruppe. Nach Generationwechsel Callbackwirkung verwerfen und Verpflichtung abschließen; nicht auf aktuellen Editor umbiegen. Bereits gestarteter native Commit schließt normal ab.

**Tests:** Unit: Copy-/Cut-/Pasteentscheidung und stale Intent. Headless mit verzögertem/fehlerndem Provider: Lesen, Null/leer, Auswahl/Caret-/Revisions-/Generationswechsel, WholeLine/Rechteck/teilweise ReadOnly/Tabs/CRLF/Unicode, reentrante Copied/Pasted-/Propertycallbacks, Dispose nach Callbackstart. Windows: echtes OS-Clipboard zwischen Anwendungen, Cutexportfehler soweit reproduzierbar, Mehrzeilen-/Unicode-/Rechteckfolgen und Undo/Redo.

**Abnahme:** Copy ohne Dokument-/Historyänderung auch bei gesperrter Mutationszulassung; keine verspätete Cut-/Pastewirkung; erfolgreiches Clipboardverhalten entspricht der dokumentierten nativen Semantik. Erreichbare Abweichungen ausdrücklich entscheiden statt stillschweigend übernehmen. Kein neuer Save-/Run-/Leasepfad; voller Provider-Shutdowndrain AP4.

### P5 – Variablen-/Parameterentwürfe und eine Serviceinstanz

**Dateien:** VV, VariableModel/ParameterModel, VG, VC, S/R für hostinterne GUI-Stateadmission/Snapshotbindung, VM/KM/APP für konsistente DI.

**Lücke/Grenze:** Button/TwoWay-Entwurf → SelectedVariable/Parameter → Shared.Mutate. Serviceinstanz aus derselben DI wie Editor verwenden; GUI-Commit prüft C vor Q und vor Statewrite. Entwurf bindet Snapshot-StateVersion, Selection-/Variablenidentität und Lebensdauer/Generation. UIcontrols spiegeln Zulassung, serverseitige Commitprüfung bleibt entscheidend. Parameter muss Mitglied des ursprünglichen Entwurfs sein. Refresh verwendet Kopien und bei Busy einen als stale gekennzeichneten veröffentlichten Snapshot, keine lebende Dictionaryenumeration und keinen Retry. Reine Modellbearbeitung verleiht keine Stateberechtigung.

**Fehler/Reentranz/Cancellation/spät:** Mutation kann vor einem Parse-/Capturefehler teilweise wirken; Version konservativ erhöhen, stale markieren, keine Wiederholung. Refresh/Selectionwechsel invalidiert alte Entwürfe; keine stille Übertragung auf gleichnamige neue Variable. Collection-/Propertypublikation unter O, kein Statewrite aus Bindingcallback. Synchrone Commitphase hat keine Cancellationlücke; spätere UIpublikation muss ihren Originalsnapshot prüfen.

**Tests:** Unit: Entwurf-/Parameterzuordnung, StateConflict, Refresh ohne Versionsinkrement, konservative Teilfehlerversion. Headless: echtes Value-TextBox-/Parameter-DataGridbinding, Commit/CancelEdit, SelectedItem-Wechsel während Buttonaufruf, Refresh/Detach/Generation, reentrante Collection-/Propertyhandler, injizierter Service ungleich Shared. Windows: Zellcommit per Tab/Enter/Maus, Auswahlwechsel, Ablehnung bei laufender Mutation, sichtbarer stale-Zustand.

**Abnahme:** Alle M45/46/50/51 verwenden eine Serviceinstanz und C+Q; alte Entwürfe können keinen neuen State ändern; Revision bleibt bei reinem Statewrite unverändert. P6 liefert Observablegrenzen, P8 Runzulassung für KM. **AP5 behält** `ClearAdmitted` innerhalb eines laufenden Bookjobs und die gesamte Job-Stateversionfinalisierung; P5 darf dafür keine zweite Reservation, Pipeline oder Coreänderung einführen. Dieser Rest wird als AP5-Gate getestet/ausgewiesen, nicht als P5-erledigt deklariert.

### P6 – Synchrone Observer, Properties und lokaler Viewlifecycle

**Dateien:** V/VM, E/A/B (nur notwendige Grenzverdrahtung), VB/ST/SB/VV/MV/IV/KM, VariableModel/ParameterModel, InfoView.axaml.cs. Keine Änderung an Prism.BindableBase.

**Lücke/Grenze:** Eigene Property-/Collectionpublikation und Subscriber einzeln über den servicegebundenen O-Adapter führen; direkt von BindableBase abgeleitete VMs ausdrücklich erfassen. Ein alleiniger VB-Override reicht nicht. Autoritätsinterne B-Revisions-/Identitybuchhaltung bleibt früh und zuverlässig; kein generischer catch darf einen fehlgeschlagenen Identitätswechsel als Erfolg ausgeben. Viewattach/detach ist eine eigene Lifecycleaktion, kein pauschaler O-Block, der Attach selbst verbietet. Initiales Clear nur vor Nutzbarkeit/unavailable oder als ausdrücklich zugelassene Initialisierung. Unsubscribe und Sessioninvalidierung an Originalinstanzen.

**Fehler/Reentranz/Cancellation/spät:** In O keine neue Editor-/History-/State-/Runzulassung. Observerfehler dem aktiven Originalauftrag zuordnen; außerhalb eines Auftrags sicheren Präsentationsfehler/stale-Zustand erfassen, keine erfundene Erfolgsquittung. Fehler in einem Cleanupschritt dürfen andere nötige Unsubscribes nicht überspringen. Späte Ereignisse des alten Dokuments/DataContext bleiben wirkungslos. Synchron keine await-Cancellation; asynchrone Subscriber gehören P7/AP4.

**Tests:** Unit: Scope-Nesting/Fehlerzuordnung und idempotente Abmeldung. Headless: PropertyChanged/CollectionChanged/Caret/Fokus/IsModified/IsReadOnly/Document/Selection, alle direkten BindableBase-Klassen, Infohistory/Markdownnavigation, Detach/Reattach mit Callbackfehler. Jeder Trigger versucht reentranten Servicecommand und direkten Hostwrite; Originaleffekt bleibt erkennbar. Windows: Navigation zwischen Editor/Variablen/Info, tatsächlicher Fokus, wiederholte Regionwechsel und Erstfokus.

**Abnahme:** OBA01–22/31 haben je konkretem Produktcallback eine dokumentierte Grenze oder begründete autoritätsinterne Rolle; kein „alles durch Basiswrapper“-Nachweis. P1/P2/P3/P5 behalten ihre schreibenden Commandgrenzen. App-/Provider-Gesamtdrain, Recovery und Shutdownzertifikate bleiben AP4/AP6.

### P7 – Späte GUI-Posts und Präsentationsproducer

**Dateien:** VM.OnPrintEvent/OnInfoEvent/OnUiAsync, KM.OnInfoEvent/LoadInfoAsync, UD, IV/ST und ein kleiner serviceinterner Produceradapter unter Automation; C.CallbackTicket nutzen/gezielt ergänzen.

**Lücke/Grenze:** Hintergrundevent → UD.Post → neuer UI-/Navigationszustand ohne originale Auftrags-/Lifetimebindung. Vor Scheduling Producer/Callback registrieren, vor tatsächlicher Publikation auf UI ursprüngliche Registrierung, Generation und Empfängerlifetime prüfen, Callback im finally abschließen. Producerabschluss erst wenn kein weiteres Scheduling möglich ist. Gui-/Jobidentität explizit übergeben; UD.SuppressFlow bleibt, er ist keine Ticketregistrierung. Rein lokale Navigation kann eine View-Lifetimebindung ohne Schreibrecht verwenden; keinen künstlichen Textedit eröffnen, nur um einen Observer zu zählen.

**Fehler/Reentranz/Cancellation/spät:** Schedulingfehler löst die gerade registrierte Verpflichtung; laufender Callback verschwindet nicht durch Self-dispose. Cancellation/Detach verwirft Publikation, nicht die Abschlussverpflichtung. Alte Info-/KeyMap-Fehlerposts dürfen keine neue View überschreiben. Fehler im Callback vor terminalem Receipt erfassen. Keine Dispatcherwarteoperation unter R.sync und kein await innerhalb Observe(Action).

**Tests:** Unit: Producer-Sealing, Zählen-vor-Post, Schedulingthrow, Double-dispose, maximale Verpflichtungen. Headless: verzögerte Posts vor/nach Bodyabschluss, Generation-/Dokument-/Service-/DataContextwechsel, Dispose/Cancel während Callback, verschachtelte Fehler, blockierter Callback hält Drain. Windows: reales Dispatcher-/Fokus-/Navigationstiming, langsamer Run und Viewwechsel, keine alte Status-/Infoüberschreibung.

**Abnahme:** Für jeden AP3-GUI-Producer ist Publish/Discard/Finally belegt; Pending wird nicht vor Ende einer Callbackwirkung null. Keine allgemeine „RunOnUi darf schreiben“-Capability. Abhängig von P6 und P8-Auftragsbindung. **AP4/AP5** integrieren Registry-/Passwort-/Save-/Outputproducer vollständig; AP3-Teilabschluss ist kein L2-Zertifikat.

### P8 – Gemeinsame GUI-Admisson und Übergaben an AP4/AP5/AP6

**Dateien:** S.RunAsync/Mutate-Anbindung, R.RunGuiAsync, EO, VM.ParseLine/OpenScriptBook/ExecuteBookAdmitted, V.ExecuteInputAsync, SB, KM; minimale interne typisierte Auftragsbindung im bestehenden C.

**Lücke/Grenze:** GUI-Aufträge gehen vor/aus/nach nativen Eingaben direkt an Q/R; Pickerantwort bindet erst nach await, Returnoutput erst nach Runnerende. Gemeinsame UI-affine GUI-Admisson vor Registryannahme/Statuscallbacks, danach Q fail-fast, Originalauftrag und Completion vor Veröffentlichung. Unter laufendem nativen Enter darf kein zweites generisches TryBegin scheitern oder Active als ambient Berechtigung gelten: eine eng definierte einmalige Return-Übergabe vom angenommenen Input zum eigenen Run-/Outputintent ist erforderlich. KeyMap bekommt denselben GUI-Eingang. Open erhält vor Picker einen entwertbaren Intent; eigentliche Datei-/Dialogcommitregeln werden AP4 übergeben. Bookzeilen/Present erhalten Originalbindung, keine Nachfolgersuche.

**Serviceeditgrenze:** Auch vorhandene hostinterne EO.UpdateEditorAsync-Aufrufe müssen C-Admission und Original-D01-Erfassung erhalten, bevor sie innerhalb H als geschützt gelten können. Alte v1-DTOs werden nicht still als v2 deklariert. Bestätigung/Callbackfinalisierung ist AP4; strukturierte öffentliche D01-/v2-Projektion AP6. Der alte EndUpdate-Fallback darf nicht als neue gemeinsame Finalisierungsstrategie übernommen werden. Ein Paket darf diesen Weg nur vollständig für seinen benannten lokalen Umfang abnehmen oder als konkreten AP4/AP6-Rest ausweisen.

**Fehler/Reentranz/Cancellation/spät:** Ablehnung vor Jobannahme/Statewirkung ohne Diagnose-/Statuslöschung; angenommener Job bleibt serviceeigen. Cancel vor Runnerstart revalidieren, nach Wirkung geordnet abschließen. Keine offene native Gruppe über await. Returnoutput braucht in AP5 Originalanchor und explizit kompatibles Weitertippen; normaler Inputgenerationwechsel und endgültiger Lifecycleentzug sind zu unterscheiden. Alte Picker-/Bestätigungsantwort wirkt nie auf aktuellen Nachfolger. Capturefehler kein Retry.

**Tests:** Unit: GUI-Admission vor Q/Job/Publish und Rückgabe bei Q-Busy, genaue einmalige Übergabe, gleiche Serviceidentität. Headless: native Enter/Shift-Enter, KM.Initialization, direkte GUI-Serviceaufrufe, History-/Propertyreentranz, Open vor/nach Picker/Clearzustimmung, Detach vor erster/nächster Bookzeile, Return mit Weitertippen, spät zugestellte Präsentation. Windows: Enter bleibt bedienbar, keine doppelte Ausführung, Picker-Cancel/Escape, Fokuswechsel bei Dialog und Rückkehr. Die vollständigen Passwort-/Save-/Outputdrainorakel werden unter AP4/AP5 separat abgenommen.

**Abnahme:** Kein neuer GUI-Run/State-/hostinterner Editorwrite ohne passende C-Zulassung; keine unkontrollierten Fortsetzungsdelegates. D01 im benannten lokalen Umfang und kompatible Returnsemantik klar belegt. Abhängig von P5/P6/P7-Schnittstellen, in kleine Unteraufträge teilbar: zuerst Runadmission/KM, danach Open-Übergabe, danach Return-/Serviceedit-Übergabe. **Kein EC21-Fix, keine Core-/Dateisemantikänderung, kein externer Tool-/Lease-/HTTP-Zugang.** Ohne spätere AP4/AP5/AP6-Endabnahme bleibt die betroffene Gesamtkette ausdrücklich offen.

## 6. Avalonia-12-Nachweise und Testplan

### 6.1 Versionsgebundene Frameworkprüfung

Die Prüfung verwendet den **Paketcommit** `86fdebec4cff7affb0e14c7885df71d28edce777`, nicht einen aktuellen ungebundenen Frameworkbranch:

- [TextEditor.cs](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/TextEditor.cs): OnApplyTemplate installiert das öffentlich erreichbare SearchPanel; Textsetter kann History löschen; IsModified beeinflusst Historymarker. Daraus folgen Installations-/Propertygrenzen, keine erneute Migration.
- [SearchPanel.cs](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Search/SearchPanel.cs): eigene Replacebindings und öffentliche ReplaceNext/All; Dokument-/TextChanged- und Templatecallbacks. Hostadapter fehlt im Produkt. Ersatztext-/Regexsemantik der konkreten Version erhalten; keine stillschweigende Erweiterung auf andere Regex-Replacementregeln.
- [CompletionWindow.cs](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/CodeCompletion/CompletionWindow.cs): Popup wird vor Complete verborgen; Caret-/TextInput-/Selection-/Size-Subscriptions. P3 muss diese Reihenfolge berücksichtigen.

A's nativer Commandadapter erhält DefaultInputHandler-Undo/Redo und verbraucht seine private Delegationsmarke vor nativen Callbacks. Die vorhandenen produktbezogenen Tests prüfen reale Registrierungen, Gesten, TextInput und Clipboard. Das beweist nicht, dass Such- und Completionpfade dieselbe Liste verwenden. `TryGetTextAsync` ist bereits der integrierte Avalonia-12-Pfad; keine Rückportierung auf obsolete Clipboard-APIs. P2/P4 prüfen Native-Parität, ohne Paketwechsel.

### 6.2 Ausführung in AP3-01

Build-/Testbefunde sind im Abschlussabschnitt mit den tatsächlich vorliegenden Resultaten festgehalten. Befehle:

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Debug --no-build --no-restore --logger 'trx;LogFileName=gui.trx' --results-directory artifacts/ap3-01
```

Der erste vorbereitende Buildversuch scheiterte beim Netzwerkzugriff auf die gepinnte Prism-Quelle, vor Kompilierung. Nach erlaubt ausgeführtem Fetch exakt dieses Commits und detached Checkout lief das unveränderte Buildskript; kein Reset/Patch einer bestehenden Prism-Quelle. `build-debug.log` und `build-debug-retry.log` bleiben getrennt. Ein Restorezugriff ist kein automatischer Retry einer Editor-/Stateoperation.

Eine neue native Windows-/physische Tastatur-/OS-Clipboard-/IME-Abnahme wurde in AP3-01 **nicht** durchgeführt. Dieser Bericht ist deshalb keine native Vollfreigabe. Die pro Paket bezeichneten Windows-Fälle sind verbindliche spätere Abnahmearbeit. Historische native Belege bleiben E und werden nicht als neue V gezählt. Keine neuen Produktproben oder Testassertionen wurden für diesen Bericht implementiert.

### 6.3 Gemeinsame Abnahmematrix für Folgeaufträge

| Dimension | Erforderliches Orakel |
|---|---|
| Vor Wirkung abgewiesen | Keine Text-/Undo-/Redo-/Statewirkung; kein neuer Run/Dateicommit; keine leere native Gruppe als Nebeneffekt; Prüfung tatsächlicher Stackfolgen zusätzlich zu CanExecute |
| Erfolgreiche native Aktion | Genau eine Delegation; ursprüngliche Dokumentinstanz/Identität; native Auswahl-/Caret-/Anchor-/Historysemantik, keine Ersatzengine |
| Reentranz | Property/Collection/Document/Caret/Fokus/Popupcallback startet keinen zweiten Write/Run; auch ohne äußere aktive Operation keine versteckte spätere Mutation |
| Fehler nach Wirkung | Originalreceipt erhält applied/partial/indeterminate nach Evidenz, History separat; Callbackfehler nicht in falsches none/Busy umwandeln; unsicherer Abschluss faulted |
| Späte Antwort | Generation, Dokument, Registration, Service/Owner oder Draftversion geändert: kein Nachfolgerwrite/-unlock/-popupclose/-statuspublish; eigene Verpflichtung trotzdem finalisieren |
| Drain | Bodyende, Producersealing, laufende Callbackwirkung und Q-/Registry-/Providerabschluss getrennt; Cancellation/Timeout allein kein Freigabebeweis |
| Versionskonsistenz | Revision ausschließlich B; StateVersion ausschließlich derselbe Service; Read erhöht StateVersion nicht; History-only darf textneutral sein |
| Native Windows | Echte Tastatur/Clipboard/Popup/Template/Dialog-/Fokusvarianten der tatsächlichen 12er-Version; keine Headlesssimulation als OS-Beweis |

Die **S-01-Diagnostikregel bleibt unverändert**. AP3 darf aus DiagnosticSeverity oder einer nichtleeren Diagnoseliste keine neue Abbruchentscheidung ableiten. Der aktuelle Ausführungs-/Exceptionvertrag bleibt maßgeblich; diagnostizierte Checkfehler können die Fortsetzung erlauben. Reihenfolge, Severity, Formatierung, Quell-/Bookkontext und identitätsbasierte Vermeidung eines doppelt transportierten Exceptiondiagnostikums bleiben erhalten. Vorhandene Orakel u. a. `CheckErrorsContinueAndKeyPrintReachesExistingGuiOutput`, `SuccessfulWarningsRemainOrderedAndDoNotSetErrorFlag`, `WarningBeforeErrorUsesRunnerSnapshotWithoutDuplicateAndRefreshesPartialState` und `SemanticDiagnosticPresentationTests` dürfen nicht abgeschwächt werden. Kein Parser-/CryptoScript-Core-Umbau zur Vereinfachung der GUI-Grenzen.

## 7. Abhängigkeiten, echte Entscheidungspunkte und Risiken

**Reihenfolge:** P1 unabhängig als erster Produktauftrag; anschließend P2/P3/P4 jeweils mit paketlokalen Observern. P5/P6 schaffen konsistente Instanz-/Präsentationsgrenzen. P7/P8 werden an expliziten Auftrags-/Producerübergaben miteinander abgestimmt und in kleine Unteraufträge getrennt. AP4 übernimmt Datei-/Dialog-/Provider-/Shutdownfinalisierung; AP5 EC21 sowie vollständige Book-/Return-/Job-/Stateversion; AP6 v2/Lease/Credentials/Epoche/Uhr. Die AP0-Mischzeilen sind deshalb oben aufgeteilt statt vollständig in eine spätere Phase verschoben.

| Entscheidung / Risiko | Was konkret vor Implementierung zu klären bzw. nachzuweisen ist |
|---|---|
| Native Search-/Completiongrenze | Öffentliche Hostadapter und echte Commandrouten müssen alle benötigten Varianten vor Wirkung erreichen. Bei belegter Unkontrollierbarkeit separater Architekturentscheid; kein stiller Fork/Patch/Upgrade. |
| Return-/Bookfortsetzung | Normales C.OperationTicket ist revisionstreng und wird bei Commit disposed; es modelliert noch keine mehrstufige, kompatible Anchor-/Jobfortsetzung. Typisierte Übergabe/Producerdauer im selben C reviewen; kein Active-/AsyncLocal-Schreibrecht. |
| Fokus versus modaler Intent | Paste-/Completionintent darf beim Fokuswechsel entwertet werden; ein eigener Picker/Dialog muss trotzdem regulär abschließbar sein. AP4 entscheidet die genaue typisierte Bindung; keine globale Abschaffung der Fokusinvalidierung. |
| Producer ohne Textmutation | Reine Info-/KeyMap-/Navigationstasks brauchen begrenzte Lifetime-/Callbackpflichten, ohne einen fiktiven Editorwrite oder ungebundenes OperationTicket zu erfinden. P7/P8-Schnittstelle zuerst testen. |
| StateVersion mit injiziertem Service | APPs heutige Shared-DI verdeckt den Fehler im statischen Clearpfad. Nicht-Shared-Tests sind zwingend; ein globales Dictionary darf nicht zu mehreren unabhängigen Versionswahrheiten führen. Keine zweite Serviceautorität zulassen. |
| Fehler in Propertypublikation | Ein catch um gesamten Multicastdispatch kann spätere notwendige Subscriber überspringen. Kritische Buchhaltung muss unabhängig fertig werden oder konservativ als unvollständig gelten. Keine Prism-Quelländerung. |
| Legacy-v1-Serviceedit | Frühere positive Guards sind kein D01-v2-Receipt. Schnittstelle AP3/AP4/AP6 muss Fehler nach Wirkung wahrheitsgemäß behandeln; keine zusätzliche externe Schreibfreigabe zur Erprobung. |
| Tatsächlich blockierter Provider | Drain darf offen bleiben; Timeout beendet kein Originalrecht. AP4/AP6 müssen Verfügbarkeit und sichere Sperre dokumentieren, nicht automatisch freigeben. |
| IME / native Umgebung | Akzeptiertes CJK-Restrisiko bleibt, sonstige Unicode-/Schweizer-/AltGr-/Dead-Keyeingabe muss funktionieren. Frühe dauerhafte IME-/Dragsperre unverändert. |
| Prism-Ausnahmerecht | Gepinnte offizielle Quelle und bestehende Adapter bleiben. Umstellung nach Ende der genehmigten Ausnahme ist ein anderer Auftrag, kein AP3-Nebenfix. |

D01–D08 werden nicht erneut zur fachlichen Entscheidung gestellt. Offen sind konkrete Mechanismen und Nachweise. Kein zusätzlicher Bedarf für neue Undo-Engine, zweite Bridge/Koordination, andere Dateiformate, eine leaseweite Q-Reservation oder einen zweiten Interpreter.

## 8. Empfehlung und Stop-Gate

**Erster produktiver Folgeauftrag: „AP3-02 – History-Command unter EditorCoordinator, einschließlich MainView-Eingang und nativer Undo-Gruppe“**, exakt im Umfang P1. Er bietet einen kleinen synchronen Ende-zu-Ende-Pfad mit klaren Ablehnungs-, Reentranz-, History- und D01-Orakeln; er benötigt weder modale Provider noch eine vorgezogene EC21-/Leaseänderung.

Vor seiner Abnahme: tatsächlicher MainViewcommand statt nur direkter VM-Aufruf, frühe Ablehnung auch vor Historyindexverschiebung, eine Undo-Gruppe, originale Revision/Receipt bei Fehler, native Windows-Button-/Ctrl+Z/Y-Folge und unveränderte Coordinator-/GUI-Regression. Jede zusätzliche Ausführungs-, State-, Save- oder Dialogänderung wäre eine gesonderte Umfangserweiterung.

**AP3-01 endet mit diesem Bericht.** Keine automatische Umsetzung von P1 oder P2–P8, keine Änderung produktiver C#-/AXAML-/Projekt-/Prismdateien, keine Testassertionänderung, kein Commit/Push/Merge. Der Bericht ist zur Prüfung vorzulegen; vollständige AP3- und MCP-Hostfreigabe wird ausdrücklich nicht behauptet.

## 9. Abschlussverifikation vom 29. Oktober 2026

| Prüfung V | Ergebnis | Lokale Evidenz |
|---|---|---|
| Debug-Restore/Build beider Lösungen über vorhandenen Prismadapter | Erfolgreich, null Fehler; Avalonia-Lösung 87 Warnungen, Root-Lösung 0 Warnungen. Vorhandene Nullable-/Dokumentations-/APIwarnungen wurden nicht korrigiert. Kein Releasebuild in AP3-01. | [Buildlog](../../artifacts/ap3-01/build-debug-retry.log) |
| Erster GUI-Lauf innerhalb Sandbox | Vor Testausführung abgebrochen: Testhost-Verbindungsaufbau nach 90 Sekunden fehlgeschlagen. Kein fachlicher Test-PASS/FAIL daraus abgeleitet. | [Erstes GUI-Log](../../artifacts/ap3-01/gui.log), [erste TRX](../../artifacts/ap3-01/gui.trx) |
| Unveränderte vollständige GUI-Suite außerhalb Sandbox, Debug/no-build/no-restore | **490 PASS, 0 FAIL, 0 SKIP**, Laufzeit laut Runner 7 Sekunden. Alle 490 UnitTestResult-Einträge der TRX als Passed geprüft. | [GUI-Log](../../artifacts/ap3-01/gui-unrestricted.log), [GUI-TRX](../../artifacts/ap3-01/gui-unrestricted.trx) |
| Darin enthaltene EditorCoordinator-Fälle | **35 PASS** einschließlich F1-Regressionen; Teilmenge der 490, nicht zusätzlich addieren. | Dieselbe TRX; [Verifikation](../../artifacts/ap3-01/verification.json) |
| Neue native Windows-/CJK-/physische Tastaturtests | Nicht ausgeführt; P1–P8/native Matrix weiterhin ausstehend. | Keine PASS-Behauptung |
| Core-/MCP-Testausführung | Nicht erneut ausgeführt; beide Projekte gebaut. Keine Core-/MCP-Quell- oder Vertragsänderung und kein neuer Befund mit zusätzlichem Ausführungsbedarf in diesem Dokumentauftrag. | Buildlog; historische Ergebnisse nicht als heutige Läufe zählen |
| Inventurprüfung | Genau **51 eindeutige M-Zeilen** und **31 eindeutige OBA-Zeilen**, vollständige fortlaufende IDs. | Dokumentprüfung |
| Lokale Dokumentlinks | Alle vorhandenen relativen Dokument-/Quell-/Evidenzlinks auflösbar. | Dokumentprüfung |
| Produkt-/Test-/Projektquellen und Index | `git diff` und `git diff --cached` leer; `git diff --check` ohne Befund. | Git-Abschlussprüfung |
| HEAD / Branch | Unverändert `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00` / `feature/mcp-ap3`. | Verifikation |
| Gepinnte Prismquelle | HEAD exakt `358118cd640d9a22ff8cf21c8ad197fa038b7990`, Status leer, keine Quelländerung. | Verifikation und Buildprüfung |

Einzige neue nicht ignorierte Datei im Produktworktree ist `docs/mcp/MCP-AP3-01-integration-assessment.md`. Die aufgeführten Logs/TRX/Verifikationsdaten und Buildausgaben sind ignorierte lokale Prüfartefakte dieses neuen Worktrees. Sie gehören nicht zu einem stillschweigend vorbereiteten Commit. Die alten Worktrees, ihre Quellen und historischen Assertions wurden nicht bearbeitet. Die erfolgreiche Testwiederholung belegt keinen bestimmten technischen Grund des ersten Testhost-Verbindungsfehlers.

Die Vollinventur ist damit als **Planungs- und Quellenabgleich** abgeschlossen; die in den Tabellen benannten Integrations- und nativen Nachweise sind weiterhin offen. Stop-Gate erreicht: Bericht zur Prüfung vorlegen und keine Folgeimplementierung beginnen.
