# A12-01 – Implementierungs- und Migrationsreview

Stand: 26. Oktober 2026. **Gesamtbewertung: BLOCKED durch nachgewiesene Abhängigkeit.**

Die Anwendung und GUI-Tests sind auf Avalonia 12.1.4 / AvaloniaEdit 12.0.0 portiert. Release- und Debug-Builds funktionieren; alle 472 GUI-Tests, darunter die unveränderte Menge von 35 Coordinator-Testfällen, bestehen. Der veröffentlichte Prism-Avalonia-11-Binärstand scheitert jedoch beim Erzeugen eines Prism-Dialogfensters unter Avalonia 12 mit MissingMethodException. Der vorliegende Arbeitsstand ist zur unabhängigen Prüfung vorbereitet, aber weder abgenommen noch produktionsfreigegeben.

**Dokumentationsnachtrag vom 27. Oktober 2026:** Die [aktualisierte Lizenz-/Distributionsentscheidung in A12-02](A12-02-prism-compatibility-review.md) dokumentiert die vom Eigentümer mitgeteilte befristete Supportausnahme für CryptoWorkBench: unveränderte offizielle Prism-Quellen selbst bauen, daraus erzeugte Assemblies mit CryptoWorkBench ausliefern und in öffentlichen GitHub-Actions-CI-Builds verwenden, ohne Commercial Plus. Dies gilt bis zur für November 2026 angekündigten offiziellen Veröffentlichung; danach ist der Wechsel auf offizielle Prism-NuGet-Pakete verpflichtend, anschließend gilt laut Support die Community-Lizenz. Die Ausnahme klärt die beschriebenen Rechte, beseitigt aber nicht den technischen Dialog-ABI-Blocker des weiterhin verwendeten Paketstands. Keine Integration oder Produktfreigabe erfolgt durch diesen Nachtrag. Die nachfolgenden Testzahlen und Befunde bleiben historische Ergebnisse vom 26. Oktober 2026. A12-03 bleibt separat; ein Linux-Bericht liegt hier derzeit nicht vor und wird nicht rekonstruiert.

## 1. Exakte Ausgangsbasis

| Merkmal | Wert |
| --- | --- |
| Ursprünglicher Worktree | C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Mcp |
| Ursprünglicher Branch | feature/mcp-server |
| Verifizierter HEAD | bd48c57fa4cae9dc733da7d4521eb6f79a9017f0 |
| Neuer Worktree | C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12 |
| Neuer Branch | feature/avalonia12-migration |
| Erfassung | 2026-10-26T17:06:28.651166Z |
| Inventar | 767 versionierte bzw. nicht ignorierte Projekt-/Ressourcen-/Dokumentationsdateien |
| Uncommitteter Ausgangsstand | 6 modifizierte versionierte Dateien, 28 unversionierte Dateien; Staging leer |

Der neue Worktree wurde vom angegebenen HEAD angelegt. Anschliessend wurden die **34 uncommitteten Dateien einzeln anhand des Inventars** übertragen. Alle 767 Inventardateien liegen zusätzlich bytegetreu unter artifacts/a12-01/baseline/source. Es wurden keine fremden Git-Metadaten oder unversionierten Konfigurationen pauschal kopiert. Ignorierte Buildprodukte und Paket-Caches sind nicht Bestandteil dieses Quellmanifests.

Die vollständige Liste der übertragenen Dateien samt Hash, Länge und Untracked-Kennzeichnung steht in [transferred-changes.json](../../artifacts/a12-01/baseline/transferred-changes.json). Darunter befinden sich EditorCoordinator, EditorBridge, beide Workbench-Editorcontrols, die AP2.1-Tests sowie sämtliche inventarisierten MCP-01.4- und AP2.1-Reviews. Der Vergleich erfolgt gegen diesen tatsächlichen Quellstand, nicht lediglich gegen Git HEAD.

Nachweise:

- [Quellmanifest](../../artifacts/a12-01/baseline/source-manifest.json), SHA-256: **8F0D62DB6797CF7C1FB41176A2754EAF7ACFD3F2D28D4F1FD05F3F4F9ED780AA**.
- [Identität](../../artifacts/a12-01/baseline/identity.json), [Git-Status vor Beginn](../../artifacts/a12-01/baseline/status-before.txt), [Unstaged-Patch](../../artifacts/a12-01/baseline/unstaged.patch); Staged-Patch und Staging-Liste ebenfalls gesichert.
- [Abschlussprüfung](../../artifacts/a12-01/source-preservation-final.json): alle 767 Originaldateien hashgleich, Porcelain-v2-Status identisch, Originalbranch und HEAD unverändert, beide Staging-Bereiche leer. Eine zwischenzeitliche Prüfabweichung entstand nur durch Vergleich von Porcelain v1 mit v2; der korrigierte Vergleich verwendet durchgehend v2.

Die historischen Reviews sind auch im neuen Worktree unverändert. Die anderen bestehenden Worktrees wurden nicht bearbeitet. Die notwendige Registrierung des zusätzlichen Worktrees/Branches ist die einzige beabsichtigte gemeinsame Git-Verwaltungsänderung. Kein Commit, Push, Merge, Reset, Rebase oder Stash wurde ausgeführt. .codex/config.toml wurde weder gelesen noch verändert.

## 2. Zielversionen und Abhängigkeitskompatibilität

Die Versionen wurden am Ausführungstag über die öffentlichen NuGet-Flatcontainer-Indizes und Paket-Nuspecs geprüft. Die gespeicherten Antworten liegen unter [research](../../artifacts/a12-01/research/package-index.json). Keine private Quelle und kein selbst gebautes Prism-/AvaloniaEdit-Paket wurden verwendet.

| Paket | Avalonia-11-Basis | Migrationsstand |
| --- | --- | --- |
| Avalonia, Desktop, Headless, Themes.Fluent, Fonts.Inter | 11.3.20 | 12.1.4 |
| Avalonia.Markup.Xaml.Loader | transitiv 11.1.3 | explizit 12.1.4 |
| Avalonia.AvaloniaEdit | 11.4.1 | 12.0.0 |
| Avalonia.Controls.DataGrid | 11.3.12 | 12.1.2 |
| AvaloniaUI.DiagnosticsSupport | 2.1.1 | 2.2.3, nur Debug |
| FluentIcons.Avalonia | 2.0.319 | 2.1.344 |
| Markdown.Avalonia und Unterpakete | 11.0.3 | 12.0.2 |
| MessageBox.Avalonia | 3.3.1.1 | 12.0.0 |
| DialogHost.Avalonia | transitiv 0.8.1 | explizit 0.12.3 |
| UVtools.AvaloniaControls | 4.1.0 | 5.2.0 |
| Prism.Avalonia, Prism.DryIoc.Avalonia | 9.0.537.11130 | unverändert; BLOCKER |
| Prism.Core / Prism.Events | 9.0.537 | unverändert |
| Prism.Container.Abstractions / DryIoc | 9.0.107 | unverändert |

Die neuere Prism.Container-Version 9.0.114 wurde ermittelt, aber nicht als unbelegter Reparaturversuch eingesetzt: Der konkret fehlende Aufruf stammt aus dem kompilierten Prism.Avalonia-AXAML. Auch die verfügbare Prism-Vorabversion 9.0.537.11300-pre ist noch auf Avalonia 11 ausgerichtet; sie wurde nicht verwendet. Es gibt somit **keinen vollständig kompatiblen, hier nachgewiesenen öffentlichen Gesamtpaketsatz**.

DialogHost ist explizit stabil gepinnt, weil MessageBox sonst eine Nightly-Mindestversion zulässt. Der XAML-Loader wurde explizit aktualisiert, weil Prism sonst weiterhin die 11.1.3-Assembly auflöst. Avalonia.BuildServices 11.3.2 ist ein Buildwerkzeug im offiziellen Avalonia-Paketgraphen, keine verbliebene Avalonia-11-UI-Laufzeit. Svg.Controls.Avalonia 12.0.0.1 und ColorDocument/ColorTextBlock 12.0.2 werden transitiv aufgelöst.

[Finaler Release-Paketgraph](../../artifacts/a12-01/package-graph-final.json) und [vollständige Assets](../../artifacts/a12-01/avalonia12/gui-release-assets-validated.json) enthalten keine Vorabversion. DiagnosticsSupport fehlt im finalen Release-Graphen und ist im [Debug-Graphen](../../artifacts/a12-01/avalonia12/gui-debug-assets-final.json) vorhanden. Restore wurde abschliessend explizit mit Configuration=Release bzw. Debug durchgeführt, um einen irreführenden Debug-Assetgraphen bei --no-restore zu vermeiden.

SDK 10.0.401 und net10.0 blieben unverändert. Primärquellen: [Avalonia 12.1.4](https://www.nuget.org/packages/Avalonia/12.1.4), [AvaloniaEdit 12.0.0](https://www.nuget.org/packages/Avalonia.AvaloniaEdit/12.0.0), [Prism-Paket](https://www.nuget.org/packages/Prism.Avalonia/9.0.537.11130), [Avalonia-12-Migrationshinweise](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes).

## 3. Migrierte Dateien und notwendige API-Änderungen

Gegenüber dem gesicherten tatsächlichen Ausgangsstand wurden **20 vorhandene Dateien** geändert; hinzu kommen **3 neue C#-Dateien** und dieser Bericht. Die vollständigen Namen und Vorher-/Nachher-Hashes stehen in [migration-delta-final.json](../../artifacts/a12-01/migration-delta-final.json). [migration-only.diff](../../artifacts/a12-01/migration-only.diff) zeigt die Änderungen der vorhandenen Dateien; die drei neuen Dateien sind zusätzlich direkt zu prüfen. Ein gewöhnlicher Git-Diff gegen HEAD enthält auch geerbte AP2.1-Arbeit und bildet diese Trennung nicht ab.

| Dateien | Änderung und Zweck |
| --- | --- |
| Directory.Build.props; GUI-, Desktop- und GUI-Test-csproj | Versionen, stabiler DialogHost, XAML-Loader, Debug-Diagnosepaket; explizite Beibehaltung von Reflection-Bindings |
| App.axaml.cs | Entfernen des bisherigen DataValidators.RemoveAt(0); Avalonia 12 aktiviert die betreffende DataAnnotations-Validierung nicht mehr standardmässig |
| Controls/WorkbenchTextArea.cs | IClipboard.GetTextAsync durch ClipboardExtensions.TryGetTextAsync ersetzt; unveränderte Ticket-/Identitätskontrollen nach await |
| Services/UiDispatch.cs – neu | ExecutionContext-Fluss nur während des Enqueue unterdrücken |
| Services/Automation/EditorReplaceConfirmation.cs, WorkbenchEditorOperations.cs; Services/PasswordDialogHost.cs | UI-Handoffs über UiDispatch |
| ViewModels/CryptoScriptEditViewModel.cs, KeyMapViewModel.cs | Gleiche Dispatcher-Anpassung; keine Änderung der fachlichen Ausführung |
| ViewModels/SidebarViewModel.cs | Icon aus avares-Ressource statt aktuellem Arbeitsverzeichnis; Desktop-Bestätigungsdialoge mit ShowWindowDialogAsync(owner), anderer TopLevel-Typ behält ShowAsync |
| CryptoWorkBenchGuiTest/GuiTestSession.cs – neu | Gemeinsamer Headless-Dispatcherthread bei weiter bestehender App-Isolation pro Dispatch |
| CryptoWorkBenchGuiTest/Avalonia12DispatcherTests.cs – neu | Drei gezielte Context-/Capability-Regressionstests |
| AsyncPasswordGuiTests, CryptoScriptExecutionPresentationTests, EditorAutomationTests, EditorCoordinatorTests, EditorReviewCorrectionTests, PinTranslationInfoTests, WorkbenchAutomationTests, WorkbenchReviewRegressionTests | Gemeinsame Session nutzen; Coordinator-Tastatur-/Clipboard-Testadapter auf Avalonia-12-API portieren |

AXAML und Styles wurden nicht neu gestaltet. Die Projektoption AvaloniaUseCompiledBindingsByDefault=false erhält die vorherige Reflection-Binding-Semantik. Eine vollständige Umstellung auf typisierte Compiled Bindings ist damit ausdrücklich **nicht** nachgewiesen und war für den kompatiblen Port nicht erforderlich.

Core, Parser, Runner, Kryptomechanismen, Ausführungspipeline, Variablenhaltung und historische Dokumente sind gegenüber dem Manifest unverändert. EC21 und der historische Gesamteditor-Ausgabetest bleiben unverändert. Keine neuen MCP-Funktionen, keine Lease-v2- oder HTTP-Aktivierung.

## 4. AvaloniaEdit-12-Kompatibilität

Die bestehenden CommandBindings werden weiter benutzt. Weder native Undo-/Redo-Registrierungen noch der einmalige Native-Delegationsmechanismus wurden ersetzt. WorkbenchTextEditor, EditorBridge und EditorCoordinator selbst sind hashgleich zur übertragenen AP2.1-Basis.

Die Clipboard-Migration betrifft die neue [IClipboard-Schnittstelle](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Input/Platform/IClipboard.cs) und [Text-Extensions](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Input/Platform/ClipboardExtensions.cs). Der Testproxy liefert nun über TryGetDataAsync einen DataTransfer mit DataFormat.Text; die verzögerte ursprüngliche Task und alle fachlichen Assertions bleiben erhalten. Headless-Tastenereignisse verwenden Key, Modifiers, PhysicalKey und Symbol.

AvaloniaEdit 12 hält statische KeyBindings mit Threadbindung. Die bisherigen wechselnden Headless-Sessionthreads führten im Clipboard-Test zu einem threadfremden Zugriff auf KeyBinding.Gesture. Ein gemeinsamer Testdispatcher behebt diese konkrete Testinfrastrukturinkompatibilität. Jeder Dispatch erzeugt weiterhin eine isolierte Application; Produktobjekte und Fenster werden von den jeweiligen Tests beendet. Siehe [HeadlessUnitTestSession](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs). Kein Bibliothekspatch und keine neue produktive Editorarchitektur wurden eingeführt.

Native Windows-Smokes bestätigen Undo/Redo, Clipboard, Info-Completion einschliesslich Tab-Übernahme und SearchPanel mit sichtbarem Treffer. Die permanente Textdrag-/IME-Deaktivierung bleibt erhalten. Die bestehende CJK-IME-Risikoakzeptanz wurde nicht geändert.

## 5. Prism-Kompatibilität

**Nachgewiesener Blocker A12-F01:** Das öffentliche Prism.Avalonia 9.0.537.11130 enthält vorkompiliertes AXAML, das unter Avalonia 12 folgende entfernte Signatur aufruft:

~~~text
Avalonia.Data.Binding
Avalonia.Markup.Xaml.MarkupExtensions.ReflectionBindingExtension
    .ProvideValue(System.IServiceProvider)
~~~

Der isolierte Bootstrap referenziert das tatsächliche Desktopprojekt und startet dessen App und MainWindow. DI löst CryptoScriptEditViewModel samt Editor auf; die fünf erwarteten Regionen existieren. Navigation zu InfoView und VariableView funktioniert. Erst IDialogService.ShowDialog scheitert beim Auflösen von IDialogWindow:

~~~text
Prism.Ioc.ContainerResolutionException
  -> System.MissingMethodException
  at Prism.Dialogs.DialogWindow.!XamlIlPopulate(...)
~~~

Nachweis: [Bootstrap-Quelltext](../../artifacts/a12-01/bootstrap/Program.cs), [finales Laufprotokoll](../../artifacts/a12-01/bootstrap/headless-validated.log), Exitcode 1. Der Fehler wurde auch mit explizitem XAML-Loader 12.1.4 erneut bestätigt. Die Editorprüfungen, die im Bootstrap hinter dem Dialogaufruf stehen, werden deshalb **nicht erreicht** und nicht als bestanden gezählt.

Die frühere MCP-01.4-/B1-Untersuchung bleibt relevant für die Editorgrenzen. Ein früherer positiver Versuch mit lokal aus Quellcode gebautem Prism ersetzt jedoch keinen Nachweis für das hier verwendete veröffentlichte Paket. Es wurde kein solcher Eigenbuild übernommen.

Die Sidebar-MessageBox und der eigene Passwortdialog sind separate Implementierungen. Ihre positiven Ergebnisse beseitigen diesen Prism-Dialogblocker nicht. Zur Freigabe braucht es ein geprüftes Avalonia-12-kompatibles Prism-Artefakt oder einen gesonderten Architektur-/Distributionsentscheid; eine improvisierte Bibliotheksänderung ist nicht Teil dieses Standes.

## 6. Auswirkungen auf den EditorCoordinator

Alle **35 bestehenden Coordinator-Testfälle** wurden zunächst unter Avalonia 11 und danach unter Avalonia 12 ausgeführt; die Namenmengen sind identisch, kein Fall wurde entfernt oder abgeschwächt. Nachweis: [coordinator-comparison.json](../../artifacts/a12-01/coordinator-comparison.json) und [finales TRX](../../artifacts/a12-01/avalonia12/validated-results/coordinator.trx).

Dies umfasst die korrigierten Verträge F01, F02, F04 und F03: frühe Mutationsablehnung, Original-Undo/Redo, einmalige Delegation, Callback-/Receiptabschluss, Kontrollgeneration/Drainidentität, Dokument-/Servicewechsel, verzögertes Clipboard und die permanenten Eingabesperren. Ein grüner Lauf ist Regressionsevidenz, kein Ersatz für den geforderten unabhängigen Review.

Avalonia 12 erfasst ExecutionContext beim Enqueue. Ein positiver Kontrolltest weist nach, dass der rohe Dispatcher sowohl AsyncLocal-Daten als auch den InterpreterExecution-Zugriffsscope aus einem Worker zur UI transportiert. **UiDispatch unterdrückt ausschliesslich diesen Capture-Vorgang** und stellt den Fluss vor jedem await wieder her. Action, Func, asynchrone Func, Post, bereits unterdrückter Fluss, UI-Ursprung und Fehlerweitergabe werden getestet. Der Worker behält seine Capability; die UI erhält sie nicht.

Alle produktiven expliziten Dispatcher.Post/InvokeAsync-Aufrufe dieses GUI-Projekts laufen nun durch diese Grenze. Bestehende direkte Same-Thread-Pfade bleiben erhalten. Die Änderung behauptet weder eine allgemeine Sanitisierung expliziter Callbackargumente noch eine neue Drain-/Reentrancy-Architektur. Bibliotheksinterne Dispatcher-Aufrufe sind damit ebenfalls nicht pauschal geprüft.

## 7. GUI-/Core-/MCP-Testergebnisse

Die vollständige Avalonia-11-Baseline wurde **vor** den Produktänderungen im neuen Worktree gebaut und getestet. CryptoWorkBench.sln enthält nicht die gesamte GUI; deshalb wurden Lösung, Desktopprojekt und GUI-Testprojekt separat gebaut.

| Suite | Avalonia 11: bestanden / fehlgeschlagen / nicht ausgeführt | Avalonia 12: bestanden / fehlgeschlagen / nicht ausgeführt |
| --- | --- | --- |
| Core, 7672 Fälle | 7666 / 1 / 5 | 7666 / 1 / 5 |
| GUI | 468 / 1 / 0 von 469 | 472 / 0 / 0 von 472 |
| MCP, 90 Fälle | 89 / 0 / 1 | 89 / 0 / 1 |
| Coordinator, Teilmenge der GUI | 35 / 0 / 0 | 35 / 0 / 0 |
| Neue Dispatcherfälle, Teilmenge der GUI | nicht vorhanden | 3 / 0 / 0 |

Massgeblich sind einzelne UnitTestResult-Einträge im TRX, einschliesslich expliziter nicht ausgeführter Fälle. Die Teilmengen werden nicht zur GUI-Gesamtzahl addiert. [Maschinenlesbare Zusammenfassung mit allen Abweichungen](../../artifacts/a12-01/test-summary-final.json).

**Core-Abweichung:** RejectsInvalidPsnWithoutNormalizing("1") scheitert in beiden Frameworkständen. Die Assertion durchsucht exception.ToString() nach der Ziffer 1. Der neue absolute Quellpfad enthält bereits „Avalonia12“; damit schlägt die Prüfung am Stacktracepfad an. Der Test und die Produktionsvalidierung wurden nicht verändert. Das ist keine grüne Core-Suite und keine neue nachgewiesene Avalonia-12-Fachregression.

**Nicht ausgeführte Core-Fälle, in beiden Läufen:**

- NativePublicationDoesNotFollowLateSymlink sowie TargetSymlinksIncludingDanglingLinksAreRejected(True/False): fehlendes Symlink-Erstellungsprivileg.
- MacOsInheritedExtendedAclIsRemovedBeforePublication: Darwin-spezifisch.
- MeasureLocalHardware: explizite manuelle Hardwaremessung.

**Nicht ausgeführter MCP-Fall:** InstalledCodexDiscoversAndCallsStatus benötigt CRYPTOWORKBENCH_CODEX_PATH. Die installierte Codex-Integration wurde nicht ausgeführt; insbesondere wurde dafür keine Codex-Konfiguration geöffnet.

**Historischer Headless-Abschlussfehler:** In der Baseline scheitert SecretImportSyntaxErrorsReachGuiWithoutShareMaterial("0x(01AABBCC) 0x(02DDEEFF)") mit NullReferenceException in HeadlessUnitTestSession.Dispose. Der finale Avalonia-12-Lauf mit gemeinsamer Session ist grün. Das beweist keine unabhängige Ursachenbehebung des historischen Fehlers; er bleibt getrennt nachzuverfolgen und darf nicht aus historischer Evidenz entfernt werden.

**Builds:** Alle drei Baseline-Release-Builds erfolgreich. Finale Avalonia-12-Release-Builds von Lösung, GUI-Tests und Desktop erfolgreich; Desktop-Debug ebenfalls erfolgreich. Die unveränderten Core-/MCP-Projekte wurden nach den abschliessenden ausschliesslichen GUI-/Paketkorrekturen nicht nochmals ausgeführt. Die volle GUI-Suite und die 35 Coordinator-Fälle wurden auf dem endgültigen Release-Graphen erneut ausgeführt.

**Warnungen:** Keine Warnung wurde unterdrückt. Die Baseline enthält sechs CS8603-Core-Warnstellen (VariableDictionary, BlockHeaderVariableDeclaration, vier Stellen in Tr31Header), acht bestehende GUI-Nullable-Warnstellen sowie die obsoleten Clipboard-/Headless-Tastaturaufrufe. Die API-Obsolete-Warnungen entfallen mit dem Port. Die acht GUI-Nullable-Warnstellen bleiben: vier History-Command-Properties in MainViewModel, _insightWindow, zwei null-Zuweisungen in CryptoScriptEditView sowie die mögliche null-Window-Dereferenz in SidebarViewModel. Inkrementelle Null-Warnungs-Builds sind kein Beweis ihrer Beseitigung. Alle vollständigen Meldungen, Pfade und Zeilen stehen in [build-diagnostics-final.json](../../artifacts/a12-01/build-diagnostics-final.json).

**Nicht erfolgreiche Zwischenstände bleiben dokumentiert:**

- attempt-1-invalid-project.log: fehlerhafter PackageReference-Edit, MSB4067; korrigiert.
- attempt-2-clipboard-api.log: fehlende Clipboard-Extension-Namespace, CS1929/CS1061; korrigiert.
- attempt-3-headless-key-api.log: Key statt PhysicalKey, CS1503; korrigiert.
- attempt-4: ein neu geschriebener Test blockierte den Dispatcher durch NUnit Assert.ThrowsAsync; nach 3 Minuten abgebrochen, kein vollständiger Erfolg. Danach echte asynchrone Fehlerprüfung. Dump/Sequenz verbleiben im Evidenzordner.
- Versuch mit altem Clipboard-Proxy und anschliessend attempt-5: jeweils 34/35 Coordinator; erst API-Proxy, dann statische KeyBinding-Threadbindung korrigiert.
- build-desktop-loader12.log / bootstrap/headless-loader12.log: MSB3026, MSB3027, MSB3021 durch den eigenen noch laufenden nativen Testprozess. Nur dieser exakt identifizierte Prozess wurde beendet; anschliessende Builds erfolgreich.
- Die finale Prism-Bootstrap-Ausnahme bleibt ungelöst und ist kein Buildfehler.

Die abschliessenden GUI-/Desktop-Builds und Tests tragen den Suffix **validated**. Frühere „final“-Dateien sind Zwischenstände. Primäre Logs: [GUI](../../artifacts/a12-01/avalonia12/test-gui-validated.log), [Coordinator](../../artifacts/a12-01/avalonia12/test-coordinator-validated.log), [Core](../../artifacts/a12-01/avalonia12/test-core.log), [MCP](../../artifacts/a12-01/avalonia12/test-mcp.log), [Desktop Release](../../artifacts/a12-01/avalonia12/build-desktop-validated.log), [Desktop Debug](../../artifacts/a12-01/avalonia12/build-debug-final.log). Baseline-Logs/TRX liegen unter artifacts/a12-01/baseline.

**Reproduktion:** [validate.ps1](../../artifacts/a12-01/validate.ps1) mit -Phase avalonia12 baut Lösung, Desktop und GUI-Testprojekt und führt alle vier Suiten aus. -Phase baseline prüft zuerst die SHA-256-Werte und verwendet ausschliesslich die gesicherte Baseline-Kopie. Neue Läufe erhalten eigene Zeitstempelordner unter artifacts/a12-01/runs; vorhandene Evidenz wird nicht überschrieben. Die ursprüngliche Baseline wurde direkt am Migrationspfad ausgeführt; ein Wiederholungslauf aus der Sicherung hat entsprechend andere Stacktracepfade. Öffentliche Paketquelle, lokaler Cache, SDK und ursprüngliche Assetgraphen sind dokumentiert.

## 8. Native Windows-Ergebnisse

Ausgeführt im echten Windows-Desktopprozess aus dem neuen Worktree mit Computer Use; kein Headless-Ergebnis wird hier als nativer Nachweis gezählt. Screenshots und UI-Interaktionen sind im Chatprotokoll sichtbar. [Smoke-Journal](../../artifacts/a12-01/native-smoke-log.md) und [gespeicherte Testdatei](../../artifacts/a12-01/native-smoke.sbk) ergänzen die Beobachtungen.

| Vorgang | Beobachtetes Ergebnis |
| --- | --- |
| App-Start / Icon / Hauptfenster | PASS; Start aus fremdem Arbeitsverzeichnis mit eingebetteter Icon-Ressource |
| Texteingabe und Navigation | PASS; Testtext sichtbar, Caret-/Zeilenanzeige aktualisiert |
| Undo / Redo | PASS; Ctrl+Z entfernt Eingabe, Ctrl+Y stellt sie wieder her |
| Windows-Clipboard | PASS; Ctrl+A/C, End, Ctrl+V dupliziert den Text; Ctrl+Z entfernt die Einfügung als Undo-Schritt |
| Info-Completion | PASS; „Info“ plus echte Shift+8-Taste erzeugt „(“ und Completionliste; Tab übernimmt „tr31“ |
| SearchPanel | PASS; Ctrl+F, Suche „tr31“, sichtbare Markierung und „1 of 1“ |
| Save-Picker | PASS; native-smoke.sbk im neuen Worktree gespeichert, Inhalt nachgelesen |
| Open-Picker / Import | PASS im finalen Dialogstand; Datei in leeren Editor geladen und geparst |
| Bestätigungsdialog / Anhängen | PASS nach Owner-Korrektur; „No“ lässt vorherigen Text stehen, importiert zweite Zeile, Open Book wird wieder aktiviert |
| Variablenansicht / DataGrid | PASS; „migration“, Typ VAR, Wert 0x(AB) sichtbar |
| Prism-Regionnavigation | PASS; Wechsel InfoView → VariableView → InfoView |
| Beenden | PASS im finalen Owner-Stand; kein verbliebener Testprozess |
| Prism-IDialogService | BLOCKED; separater reproduzierter Binärfehler aus Abschnitt 5 |

Die ersten Importversuche mit besitzerlosem ShowAsync blieben ausstehend, nachdem das Dialogfenster hinter dem Hauptfenster verschwunden war. Die UI-Steuerung konnte den unzugeordneten Dialog nicht zuverlässig adressieren; dies ist **kein Beweis für einen MessageBox-Taskdeadlock**. Die Zuordnung über ShowWindowDialogAsync(owner) stellt die erwartete Desktop-Modalität her; danach wurden Auswahl, „No“, Importabschluss und erneut aktiver Befehl tatsächlich beobachtet. Derselbe Owner-Pfad gilt für die Überschreibbestätigung.

Die Smoke-Abdeckung ist begrenzt: keine vollständige manuelle Tastaturlayout-/DPI-/Accessibility-Matrix, kein CJK-IME-Nachweis, kein nativer Ja-/Abbruch-/Überschreibfall und kein vollständiger Dialogkatalog. Textübertragung per type_text allein wurde nicht als Completion-Tastenereignis gewertet; hierfür erfolgte eine separate echte Key-Injektion. Der letzte reine Release-Restore entfernt Debug-Diagnoseabhängigkeiten; die nativen Interaktionsläufe verwendeten denselben Produktquellstand und UI-Paketstand davor, kein nachträglicher kompletter nativer Wiederholungslauf auf dem bereinigten Diagnosegraphen.

## 9. Verbleibende Linux-/macOS-Nachweise

**Linux: OPEN. macOS: OPEN.** Auf diesen Betriebssystemen wurde weder ein nativer Prozess noch ein Testlauf ausgeführt. Erfolgreiche Windows-Headless-Tests sind kein Ersatz.

Nach Auflösung des Prism-Blockers und vor Abnahme erforderlich:

| Plattform | Geplanter echter Nachweis |
| --- | --- |
| Windows | Finale vollständige Release-Smoke-Wiederholung, Prism-Dialoglebenszyklus, Ja/Nein/Abbruch/Überschreiben, Fokuswechsel, Layout-/DPI-Prüfung |
| Linux | .NET-10-Restore/Build und alle Suiten auf Linux; X11/Wayland-Desktop, systemeigenes Clipboard, Auswahl/Paste, Undo/Redo, Completion, Search, Dateipicker und Dialoge |
| macOS | Native App auf macOS, alle Suiten einschliesslich Darwin-ACL-Test; Cmd-Tastaturgesten, Clipboard, Fokus, Completion/Search, Picker und Dialoge |
| Alle | Exakte Paketgraphen, Test-/Buildlogs, OS/Architektur und Reviewer dokumentieren; keine automatische Releasefreigabe |

Es wurde keine CI-Ausführung auf diesen Plattformen simuliert oder als erfolgreich eingetragen. Hardwaremessung und Symlinktests benötigen ihre jeweiligen realen Voraussetzungen.

## 10. Neue Risiken und Findings

| Kennung | Status | Einordnung |
| --- | --- | --- |
| A12-F01 Prism-Dialog-ABI | BLOCKED | Konkrete MissingMethodException aus veröffentlichtem A11-Prism-AXAML; Freigabestopper |
| A12-F02 ExecutionContext | Implementiert, Review erforderlich | Neue Capture-Semantik durch explizite Enqueue-Grenze abgesichert; drei positive/negative Regressionstests |
| A12-F03 Headless-KeyBindings | Korrigiert | Ein Dispatcherthread bei App-Isolation pro Test; grüne volle Suite |
| A12-F04 Sidebar-Fensterbesitzer | Korrigiert, native Teilabdeckung | Owner-gebundene Dialoge; Import/No abgeschlossen, weitere Dialogzweige offen |
| A12-F05 Core-Pfadassertion | Bekannte Baseline-Abweichung | 1 Core-Failure in beiden Ständen; unverändert, kein stilles Grünrechnen |
| A12-F06 Historischer Dispose-Fehler | OPEN, getrennte Nachverfolgung | Baseline reproduziert; neuer grüner Lebenszyklus beweist nicht die historische Ursache |
| A12-F07 Plattform-/Dialogmatrix | OPEN | Linux/macOS und restliche native Zweige noch nicht belegt |

Die bestehende CJK-IME-Risikoakzeptanz und die Begrenzungen der AP2.1-/B1-Architektur gelten weiter. Kein B2-Bibliothekspatch. Keine neue fachliche Semantik als Nebenwirkung der Migration. Die Entscheidung für Reflection-Bindings ist bewusst und im Projekt sichtbar.

## 11. Vergleich mit Avalonia 11

| Kriterium | Gesicherte Avalonia-11-Basis | Avalonia-12-Arbeitsstand |
| --- | --- | --- |
| Exakte Quellbasis | 767 Dateien plus Status-/Hashnachweis | gleiche Basis + dokumentiertes Migrationsdelta |
| Build | erfolgreich, bestehende Warnungen | erfolgreich, bestehende Nullable-Warnungen |
| Core / MCP | dokumentierte 1 / 0 Fehler | identische Ergebnisse und Nichtausführungen |
| GUI | 468/469; historischer Session-Dispose-Fehler | 472/472 mit drei neuen Context-Tests |
| AP2.1 | 35/35 | 35/35; gleiche Testnamen, Coordinator unverändert |
| Dispatcher | bisherige Handoff-Semantik | neuer Capture standardmässig; Host unterdrückt ihn gezielt |
| Clipboard / Testinput | veraltete APIs | aktuelle Text-/DataTransfer-/PhysicalKey-APIs |
| Prism | bestehender A11-Paketstand | Start/Regionen funktionieren; Dialog-ABI inkompatibel |
| Native Plattformen | hier keine neue A11-Smoke-Matrix | Windows teilweise positiv; Linux/macOS offen |
| Abnahme | ursprünglicher Referenzstand | blockierter Migrationsstand, separates Review erforderlich |

## 12. Empfehlung zur Migrationsabnahme

**BLOCKED durch nachgewiesene Abhängigkeit. Keine Migrationsabnahme und keine Produktfreigabe.**

Der tatsächliche Port und die lokale Evidenz können jetzt unabhängig geprüft werden. Voraussetzungen für eine spätere Abnahme:

1. Prism-Avalonia-12-Abhängigkeit mit nachvollziehbarer Herkunft und kompatiblem Dialoglebenszyklus bereitstellen bzw. gesondert entscheiden.
2. Unabhängiger Review der Migration und insbesondere der ExecutionContext-/Admission-Grenze; F01/F02/F04/F03 erneut bestätigen.
3. Dokumentierte Core-Abweichung und historischen Headless-Fehler separat behandeln, ohne ihre Tests oder Evidenz rückwirkend umzuschreiben.
4. Offene Windows-Dialogzweige sowie reale Linux-/macOS-Nachweise abschliessen.

Der neue Branch bleibt uncommittet. Der ursprüngliche MCP-Stand bleibt unverändert. Keine automatische Fortsetzung mit AP3–AP6. Die lokalen Nachweise unter artifacts/a12-01 gehören zum Reviewpaket; sie sind nicht allein durch einen späteren Git-Diff rekonstruierbar.
