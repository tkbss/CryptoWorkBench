# MCP-01.4-U12 – Avalonia 12 Migration Feasibility

**Stand: 24.10.2026. Entscheidungsvorlage, keine Integrationsfreigabe.**

Avalonia 12 bleibt das verbindliche zukünftige Upgrade-Ziel. Die Migration ist technisch grundsätzlich möglich: Ein isolierter Host mit den tatsächlichen CryptoWorkBench-Komponenten startet mit Avalonia 12.1.4/AvaloniaEdit 12.0.0, lokal gebautem Prism, Regions, Navigation, Dialog und Editor unter Windows. **Die Migration behebt R2/R4/R6/R7 nicht.** Die erhaltene BoundaryProbe liefert weiterhin **42 PASS / 4 FAIL**; die unabhängigen R6-/R7-Proben reproduzieren die bisherigen Fehler.

**Empfehlung: Strategie 2 – zuerst B1 als isolierter Bibliotheksnachweis gegen AvaloniaEdit 12.0.0, anschließend eine getrennt überprüfbare GUI-Migration.** Vor der Integration sind außerdem Prism-Paketbezug/Lizenz, der Dispatcher-Context-Vertrag und die Plattformabnahme zu klären. MCP-01.4 bleibt **BLOCKED**. Weder B1 noch eine produktive Migration wurden hier implementiert.

## 1. Evidenz, Arbeitsstand und Untersuchungsgrenzen

Kennzeichnung im Bericht:

- **V – verifiziert:** unmittelbar geprüfte Datei, Paketmetadaten, GitHub-Zustand oder tatsächliches Testergebnis.
- **S – statische Analyse:** aus konkreten Quellen abgeleitete Wirkung; kein vollständiger Laufzeitbeweis.
- **P – isolierter Prototyp:** tatsächlich ausgeführt, mit den unten offengelegten Versuchsanpassungen.
- **O – offen:** insbesondere nicht ausgeführte Plattform-/Distributionsabnahme.

### 1.1 Tatsächliche Ausgangsbasis

**V:** Original: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`; Branch `feature/mcp-server`; HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Die Untersuchung verwendet den Arbeitsstand einschließlich unversionierter Dateien, nicht nur HEAD. Keine repositoryinternen `AGENTS.md` oder Vorgaben in den geprüften übergeordneten `AGENTS.md`-Pfaden gefunden.

Vorher vorhandene Änderungen:

| Status | Dateien relativ zum Originalrepository |
| --- | --- |
| Geändert, unstaged | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`, `WorkbenchAutomationService.cs`; `ViewModels/CryptoScriptEditViewModel.cs`, `SidebarViewModel.cs`; `Views/CryptoScriptEditView.axaml`, `CryptoScriptEditView.axaml.cs` |
| Unversionierter Produktcode | `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs`; `Services/Automation/EditorBridge.cs`, `EditorReplaceConfirmation.cs`, `WorkbenchEditorOperations.cs`, `WorkbenchSaveCommit.cs` |
| Unversionierte Tests | `CryptoWorkBenchGuiTest/EditorAutomationTests.cs`, `EditorReviewCorrectionTests.cs` |
| Unversionierte Dokumente | `docs/mcp/MCP-01.4-review.md`, `MCP-01.4-validation.md`, `MCP-01.4-editor-architecture-decision.md` |

Die relevanten bestehenden MCP-00-/MCP-01.4-Dokumente und der abschließende BLOCKED-Stand wurden berücksichtigt. Historische READY-Aussagen aus früheren Korrekturrunden werden nicht wieder als gültige Freigabe verwendet.

### 1.2 Isolation und Integrität

Versuchsbereich **außerhalb** des Originalrepositories:

`C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12`

658 zugelassene Quellen-, Projekt-, Ressourcen- und Dokumentdateien wurden einschließlich der unversionierten Implementierung kopiert und mit SHA-256 inventarisiert. Die Kopie ist eine gefilterte Quellenbasis, kein vollständiges Repositoryabbild. Keine Git-Konfiguration, Zugangsdaten, Original-Buildartefakte oder `.codex/config.toml` wurden geöffnet oder kopiert. Dateien der SecretSharing-Implementierung sind reguläre `.cs`-Quellen und wurden einbezogen; gespeicherte Geheimnisse nicht. Öffentliche Upstream-Archive und neu erzeugte Versuchsausgaben liegen ebenfalls nur im Versuchsbereich.

Das Original wurde nicht gebaut, restored, gestaged, committed, gepusht, gemergt oder rebased. Keine Übernahme von `master`, kein produktiver HTTP-Start, kein Beginn von MCP-01.5. CryptoScript-Core und MCP-00-R1 bleiben unverändert. Einziges neues Ergebnis im Original ist dieses Dokument. Die abschließende Prüfung vergleicht die 658 Originaldateien, HEAD, vollständigen Arbeitsstatus und Staging-Diff mit der Eingangsinventur.

## 2. Verifizierte Paketmatrix

**V, Abruf am 24.10.2026:** öffentliche NuGet-V3-Flatcontainer-Listen, `.nuspec`-Dateien und tatsächlich aufgelöste `project.assets.json`. Versionsangaben sind Untersuchungskandidaten, **keine genehmigten Produktionspins**. Alle Restores verwendeten eine eigene öffentliche NuGet-Konfiguration ohne fremde Credential-Konfiguration. NuGet-Mindestversionen belegen für sich keine binäre Kompatibilität über einen Framework-Major-Wechsel.

| Paket | Original | Verifizierter Kandidat / Versuch | Wesentliche Abhängigkeiten |
| --- | --- | --- | --- |
| `Avalonia` | 11.3.20 | **12.1.4**, aktuell stabil | `Avalonia.BuildServices >=11.3.2`, `Avalonia.Remote.Protocol >=12.1.4`, `MicroCom.Runtime >=0.11.6`; net8.0/net10.0 |
| `Avalonia.AvaloniaEdit` | 11.4.1 | **12.0.0**, aktuell stabil | `Avalonia >=12.0.0`; net8.0/net10.0 |
| `Avalonia.Headless` | 11.3.20 | **12.1.4** | Avalonia, Fonts.Inter, HarfBuzz jeweils 12.1.4 |
| `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter` | 11.3.20 | **12.1.4** | Avalonia 12.1.4 |
| `Avalonia.Desktop` | 11.3.20 | **12.1.4** | Avalonia, Native, Win32, X11, Skia, HarfBuzz jeweils 12.1.4 |
| `Avalonia.Native` | transitiv | **12.1.4** | Avalonia 12.1.4; macOS-Native-Library im Publish nachgewiesen |
| `Avalonia.Controls.DataGrid` | 11.3.12 | **12.1.2**, aktuell stabil | Avalonia >=12.1.0; nicht blind auf eine nicht geprüfte 12.1.4 vereinheitlichen |
| `AvaloniaUI.DiagnosticsSupport` | 2.1.1 | **2.2.3** | Logging.Abstractions >=8.0.0, RecyclableMemoryStream >=3.0.1 |
| `FluentIcons.Avalonia` | 2.0.319 | **2.1.344** | Avalonia >=12.0.0; Common und Resources.Avalonia exakt 2.1.344 |
| `Markdown.Avalonia` | 11.0.3 | **12.0.2** | Html, Svg, SyntaxHigh, Tight 12.0.2; SyntaxHigh zieht AvaloniaEdit >=12.0.0 |
| `MessageBox.Avalonia` | 3.3.1.1 | **12.0.0** | Avalonia >=12.0.1; **DialogHost.Avalonia >=0.12.1-nightly.0.1** |
| `UVtools.AvaloniaControls` | 4.1.0 | **5.2.0** | Avalonia >=12.1.4 |
| `Prism.Avalonia`, `Prism.DryIoc.Avalonia` | 9.0.537.11130 | Öffentlich weiterhin **9.0.537.11130** stabil; kein verifiziertes öffentliches Avalonia-12-Paket unter diesen IDs | Avalonia und Markup.Xaml.Loader >=11.1.3; Container.DryIoc >=9.0.107; Core >=9.0.537 über Prism.Avalonia |
| `Prism.Container.DryIoc`, `Prism.Container.Abstractions` | transitiv 9.0.107 | **9.0.114**, öffentlich stabil, im Prism-Quellnachweis | DryIoc.dll >=5.4.3; Abstractions >=9.0.114; Microsoft.Extensions.DependencyInjection.Abstractions >=8.0.1 |
| .NET | net10.0; SDK 10.0.401 | **unverändert**, SDK 10.0.401 tatsächlich ausgeführt | Kein neues TFM erforderlich; Avalonia 12 unterstützt diese Basis |

Primärbelege: [Avalonia 12.1.4](https://www.nuget.org/packages/Avalonia/12.1.4), [AvaloniaEdit 12.0.0](https://www.nuget.org/packages/Avalonia.AvaloniaEdit/12.0.0), [Desktop](https://www.nuget.org/packages/Avalonia.Desktop/12.1.4), [Headless](https://www.nuget.org/packages/Avalonia.Headless/12.1.4), [DataGrid](https://www.nuget.org/packages/Avalonia.Controls.DataGrid/12.1.2). Die lokal gespeicherten Manifeste enthalten die überprüften Gruppen und Untergrenzen vollständig.

Weitere **tatsächlich aufgelöste** Transitivkanten des Testhosts:

- Skia 12.1.4 → `SkiaSharp 3.119.4` und NativeAssets für Windows, Linux, macOS sowie WebAssembly; HarfBuzz → `HarfBuzzSharp 8.3.1.3` mit entsprechenden NativeAssets.
- Win32 → `Avalonia.Angle.Windows.Natives 2.1.27548.20260419`; X11 → FreeDesktop/FreeDesktop.AtSpi 12.1.4, FreeDesktop → `Tmds.DBus.Protocol 0.94.1`.
- Markdown 12.0.2 → ColorDocument/ColorTextBlock 12.0.2, HtmlAgilityPack 1.11.42, Svg.Controls.Avalonia 12.0.0.1 → Svg.Model/SceneGraph/Custom und ShimSkiaSharp 4.1.0; ExCSS 4.3.1.
- Im ausgeführten Graph wurde tatsächlich `DialogHost.Avalonia 0.12.1-nightly.0.1` gewählt. **Stabile 0.12.3 ist verfügbar**, MIT, Avalonia >=12.0.0. Ein ausdrücklicher stabiler Override ist ein sinnvoller zusätzlicher Kandidat, wurde hier aber nicht als getesteter Graph ausgegeben. Die stabile MessageBox-Top-Level-Version allein garantiert keinen vollständig stabilen Graph. [MessageBox](https://www.nuget.org/packages/MessageBox.Avalonia/12.0.0), [DialogHost 0.12.3](https://www.nuget.org/packages/DialogHost.Avalonia/0.12.3).

`Directory.Build.props` enthält zusätzlich die alte `AvaloniaVersion` 11.0.2. Die Projektdateien setzen derzeit überwiegend direkte Versionen. Bei einer späteren Migration diese irreführende zweite Versionsquelle bereinigen; aus ihr wurde hier kein Restoreergebnis abgeleitet.

## 3. Prism: Merge, Veröffentlichung, Funktion und Lizenz getrennt betrachten

### 3.1 Upstream-Zustand

**V:** [PrismLibrary/Prism PR #3410](https://github.com/PrismLibrary/Prism/pull/3410) ist seit **07.09.2026** gemergt. Merge-Commit: `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Der PR hebt Avalonia auf 12.1.1, korrigiert die Dialog-Title-Bindung und verwendet in `DelayedRegionCreationBehavior` die Zugriffsprüfung des Zielelements statt einer globalen UIThread-Prüfung. [Issue #3391](https://github.com/PrismLibrary/Prism/issues/3391) ist seit **09.09.2026** mit `completed` geschlossen. Das dokumentierte anfängliche Ziel ist Desktop-Support.

**V:** Öffentliches NuGet liefert unter den im Projekt verwendeten IDs weiterhin das Community-Paket 9.0.537.11130 aus `AvaloniaCommunity/Prism.Avalonia`, Quellcommit `4f19d1428264eef0b54fcf03e5cdcac4a36d758a`. Die jüngere Vorabversion 9.0.537.11300-pre verlangt weiterhin Avalonia >=11.3.0. Sie ist weder stabil noch ein Avalonia-12-Veröffentlichungsbeleg. [Paketidentität und Versionshistorie](https://www.nuget.org/packages/Prism.Avalonia), [Vorabversion](https://www.nuget.org/packages/Prism.Avalonia/9.0.537.11300-pre).

Ein privater Commercial-Plus-Feed ist offiziell dokumentiert. Dessen Inhalt und ein dort eventuell verfügbares Avalonia-12-Paket wurden **nicht** authentifiziert geprüft; keine vorhandenen Zugangsdaten wurden gelesen. Ein geschlossener Work Item oder erfolgreicher Quellbuild ist kein Nachweis eines dort veröffentlichten, bezugsberechtigten Produktionspakets. [Offizielle Feedbeschreibung](https://docs.prismlibrary.com/docs/current/pipelines/commercial-plus/).

### 3.2 Tatsächlich verwendete Prism-Funktionen

Im Projekt vorhanden: `PrismApplication`, `RegisterSingleton`, `RegisterInstance`, `RegisterForNavigation`, `ViewModelLocator.AutoWireViewModel`, `IRegionManager`, `RegisterViewWithRegion`, `RequestNavigate`, `BindableBase`, Delegate-/AsyncDelegateCommands und `INavigationAware` in `ViewModelBase`.

**P:** Mit der echten `App`, `MainWindow`, den tatsächlichen ViewModels und Views wurden Containerauflösung, die fünf Regions `InfoFooterRegion`, `FooterRegion`, `InfoRegion`, `ContentRegion`, `SidebarRegion` und Navigation nach `InfoView`/`VariableView` nachgewiesen. Das gelang in einem ersten begrenzten Headless-Smoke sogar mit den veröffentlichten alten Prism-Binaries. Das ist ein positiver Befund für diese Pfade, **keine allgemeine Avalonia-12-Supportzusage** für ein gegen Avalonia 11 gebautes Paket.

Für den weitergehenden Nachweis wurde der exakte gemergte Prism-Quellstand lokal für net10.0 gebaut. Nur Versuchs-Buildparameter: Avalonia-Familie auf 12.1.4 angeglichen, `TargetFrameworks=net10.0`, `DISABLE_GITVERSIONING=true`, `SignAssembly=false`, `IsPackable=false`. Vier lokale Assemblies (`Prism`, `Prism.Events`, `Prism.Avalonia`, `Prism.DryIoc.Avalonia`) wurden explizit referenziert; Container.DryIoc 9.0.114 und DryIoc.dll 5.4.3 stammen von nuget.org. **Kein veröffentlichtes oder produktiv freigegebenes Prism-12-NuGet wurde dadurch erzeugt.**

**P:** Ein zusätzlich registrierter echter Prism-Dialog prüft `IDialogService`, `IDialogAware`, Owner, Open/Close und `DialogCloseListener` mit Ergebnis OK. Er ist eine Testfixture. Die produktiven Passwort- und Replace-Bestätigungsdialoge verwenden hingegen Avalonia `Window.ShowDialog<T>` direkt; deren bestehende GUI-Tests wurden ebenfalls ausgeführt. Diese beiden Integrationswege nicht verwechseln.

### 3.3 Lizenz- und Distributionsrisiken

**V/S:** Das alte Community-Paket deklariert MIT. Daraus folgt kein MIT-Gesamtstatus für Prism.Core, Containerpakete oder den neuen PrismLibrary-Quellstand. Die lokal verwendeten Containerpakete verweisen auf eine mitgelieferte `LICENSE`; der neue Prism-Quellstand verlangt Community- oder Commercial-Lizenz. DryIoc.dll 5.4.3 deklariert dagegen MIT. [Prism-Lizenzdatei am untersuchten Commit](https://github.com/PrismLibrary/Prism/blob/358118cd640d9a22ff8cf21c8ad197fa038b7990/LICENSE).

**O, Eigentümerentscheidung vor Distribution:** Berechtigung, Entwicklerzahl, erlaubte Nutzung/Weitergabe, modifizierte Binaries, Source-Zugang und CI-Bezug müssen am konkret gewählten Vertrag geklärt werden. Der verlinkte Lizenztext Version 1.0.0 enthält in §§4.2, 4.2(j), 6 entsprechende Einschränkungen; die kurze Website-/Repository-Zusammenfassung und der ausführliche Vertrag sind nicht gleichlautend. Insbesondere die Community-Zusammenfassung genügt nicht als Freigabe eigener Prism-Pakete. Das ist eine technische Risikoinventur, keine Feststellung der individuellen Lizenzberechtigung. [Vertrag](https://cdn.prismlibrary.com/downloads/prism_license.pdf), [offizielle Evaluations-/Lieferpolitik](https://prismlibrary.com/fulfillment).

Avalonia/AvaloniaEdit und die geprüften übrigen UI-Manifeste deklarieren überwiegend MIT; Notices und transitive Lizenzen bleiben Bestandteil einer Releaseprüfung. `AvaloniaUI.DiagnosticsSupport 2.2.3` enthält im geprüften Nuspec kein Lizenzfeld; daraus weder MIT noch ein Distributionsrecht ableiten. Zudem ist die Referenz derzeit nicht auf Debug begrenzt, obwohl das Attach im Code unter `#if DEBUG` steht. Release-Entfernung oder ausdrücklich geklärte Distribution gehört in den Migrationsauftrag; Developer-Tools-Lizenz und Support-Bridge nicht gleichsetzen.

Ein erster Prism-Packaging-Build meldete `NU1902` für `Microsoft.Build.Tasks.Git 10.0.300` (GHSA-23fw-v26w-5fgq) und ein SourceRoot-Problem. Für den reinen lokalen Assemblynachweis wurden Packaging/SourceLink deaktiviert. Das beseitigt keine Schwachstelle einer späteren produktiven Buildkette; deren Abhängigkeiten sind gesondert zu aktualisieren/prüfen.

## 4. Avalonia-12-Breaking-Changes gegen tatsächlichen Projektcode

Referenz ist die [offizielle Avalonia-12-Migrationsempfehlung](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes). Sie bestätigt insbesondere den neuen Binding-Default, entfernte BindingPlugins, geänderte Transfer-/Windowing-APIs und Context-Capture. Die folgenden Projektwirkungen wurden zusätzlich durch lokale Quellen und Builds geprüft.

| Bereich | Konkreter Befund | Erforderliche Behandlung / Evidenz |
| --- | --- | --- |
| .NET | GUI, Desktop und Tests bereits net10.0; SDK 10.0.401 vorhanden | Kein TFM-Wechsel nötig. **V/P** |
| Compiled Bindings / `x:DataType` | Keine globale bisherige Aktivierung; tatsächlich **50 XAML-Fehler** nach Entfernen des ersten C#-Blockers | Betroffen: MainWindow, SidebarView, InfoView, KeyMapView, StatusView, VariableView. View-DataTypes, Template-Datentypen und typisierte Parent-DataContexts ergänzen. **V** |
| DataTemplates | `VariableView` enthält untypisierte Templates und `$parent[DataGrid].DataContext`-Commandzugriffe; `StatusView` hat ein DataType am Template, aber keinen ausreichenden gesamten View-Vertrag | Nicht nur einen Root-DataType ergänzen und alle Templatefehler für behoben erklären. Für den Versuch global Reflection-Bindings beibehalten; keine Compiled-Binding-Abnahme behauptet. **S/P/O** |
| Binding / Validation | `App.axaml.cs:30`: `BindingPlugins.DataValidators.RemoveAt(0)` führt zu CS0122 | Entfernen der veralteten Pluginmanipulation in der Kopie ermöglicht nächsten Build. Validation-Anzeige/Fehlerpfade später gezielt abnehmen. **V/P** |
| Converters / Binding-Typen | Keine eigenen `IValueConverter`-/`IMultiValueConverter`-/`IBinding`-Implementierungen in den untersuchten GUI-CS-Dateien gefunden | Kein belegter eigener Converter-Umbau. Framework-/Paketbindings trotzdem prüfen; erfolgreiches Reflection-XAML beweist keinen Compiled-Vertrag. **S** |
| Window / TopLevel / Dialog | `PasswordDialogHost`, `EditorReplaceConfirmation` verwenden Owner und `ShowDialog`; Lebensdauer und Cancellation bleiben relevant | Bestehende Dialogtests grün; echter Prism-Dialog zusätzlich grün. AvaloniaEdit 12 nutzt intern bereits `TopLevel.GetTopLevel` für Completion. **P/S** |
| Fensterdekoration | MainWindow nutzt Acrylic und `ExtendClientAreaToDecorationsHint`, außerdem festes oberes Grid-Margin | Nativer Kurzstart erfolgreich; Maximieren, DPI, Drag der Titelleiste, Theme und Linux/macOS-Dekoration nicht abgenommen. Optische Anpassung möglichst im Redesign. **P/O** |
| Prism / DryIoc | APIs des tatsächlichen Bootstrap-/Region-/Navigationseinsatzes mit lokalem Quellbuild funktionsfähig | Distribution unabhängig davon offen; Lifecycle-/Navigation-Abnahmen beibehalten. **P/O** |
| Clipboard / Transfer | Kein eigener produktiver Clipboard-Adapter gefunden; reale Nutzung über AvaloniaEdit und Tests | Originalprobe benötigt `using Avalonia.Input.Platform` für die neue Clipboard-Extension. Bibliothek benutzt DataTransfer; Async-Lebensdauer siehe §5. **V/P/S** |
| Drag-and-drop | Backendpfad in AvaloniaEdit `SelectionMouseHandler` | Neuer Transfer-/DoDragDropAsync-Pfad vorhanden; native externe Ziele und Move-Abschluss offen. **S/O** |
| Textinput / IME | Reales `PerformTextInput`, Enter und Unicode in Gegenproben | Headless-Eingabe funktioniert. Native Komposition/Preedit ist kein Ergebnis dieser Proben. **P/O** |
| Dispatcher | `OnEditorUiAsync`, `PasswordDialogHost`, VM-Präsentation und `Post`-Aufrufe vorhanden | Context-Vertrag explizit an den Übergängen behandeln; neue Gegenprobe in §6. **V/P/S** |
| Headless | Projekt verwendet direkt `HeadlessUnitTestSession`, NUnit bereits 4.6.1, Adapter 6.3.0 | Kein xUnit-2→3-Auftrag. Probe-Tastenoverloads portiert; synchrone Session-Disposal-Grenze separat belegt, `await using` in Versuchsharness. **P** |
| Diagnostics | Bereits `AvaloniaUI.DiagnosticsSupport`, kein produktiver direkter alter `Avalonia.Diagnostics`-Pin | Kandidat 2.2.3; Attach nur Debug, Paketreferenz noch unbeschränkt. Release-/Lizenzprüfung offen. **S** |
| Native Assets / Publish | Windows x64, Linux x64 und macOS arm64 als framework-dependent Publish erzeugt | Native Dateien vorhanden; nur Windows nativ gestartet. Kein AOT-, Trimming-, Single-File-, Installer- oder Signierungsnachweis. **P/O** |

Die Versuchskopie änderte **keine** Editor-, Save-, Registry-, Admission- oder Core-Implementierungslogik. Die einzig notwendige GUI-CS-Anpassung für diese Buildbasis war das Entfernen der veralteten BindingPlugins-Verwendung. Der Binding-Opt-out ist eine bewusst begrenzte Kompatibilitätsbrücke, kein vollständiger Migrationsabschluss.

## 5. AvaloniaEdit 11.4.1 → 12.0.0: veröffentlichter Quellvertrag

### 5.1 Identität und Vergleichsgrundlage

**V:** Nuspec 11.4.1 nennt Commit `b56a35a5d4838f3c9a9adeba90dd80952f70e774`; Nuspec 12.0.0 nennt `86fdebec4cff7affb0e14c7885df71d28edce777`. Der annotierte Git-Tag 12.0.0 wurde bis zu genau diesem Commit aufgelöst. Die geladene Assembly bestätigt `AvaloniaEdit, Version=12.0.0.0` und InformationalVersion `12.0.0+86fdebec4cff7affb0e14c7885df71d28edce777`.

Quellwurzeln: [11.4.1](https://github.com/AvaloniaUI/AvaloniaEdit/tree/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit), [12.0.0](https://github.com/AvaloniaUI/AvaloniaEdit/tree/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit). Vergleich erfolgte zwischen diesen Quellen, nicht gegen `master`. Zeilen in der folgenden Tabelle beziehen sich auf 12.0.0; die zugehörigen Dateien beider Versionen wurden direkt verglichen.

### 5.2 API-/Verhaltensmatrix

| Vertrag | 11.4.1 gegenüber 12.0.0 | Konsequenz und konkrete Quelle |
| --- | --- | --- |
| `TextEditor.Text`, `Clear` | Mutationsablauf gleich: Dokumenttext setzen, Caret zurücksetzen, Historie löschen; Clear delegiert an Text | Basistypzugang bleibt relevant. Nichtvirtueller Setter; kein neues frühzeitiges Gate. [TextEditor:256, 1070](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/TextEditor.cs#L256) |
| `BeginUpdate`, `RunUpdate` | Zähler und Undo-Gruppe bereits verändert, bevor UpdateStarted ausgeliefert wird; RunUpdate erzeugt das Disposable erst danach | Exception im Callback ist weiterhin keine zustandsfreie Ablehnung. [TextDocument:437–461](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextDocument.cs#L437) |
| `EndUpdate`, `UpdateFinished` | Reihenfolge unverändert: Change-Notifications, Undo-Ende, Zähler 0, Abschlusscallback | Ausgleichendes EndUpdate im Host-Guard erzeugt echte neue Callbackzustellung; R7 bleibt. [TextDocument:468](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextDocument.cs#L468) |
| `Changing`, `Changed`, Replace | BeginUpdate weiterhin vor try/finally; leere Ersetzung kehrt vor Changing zurück; Changed nach Text-/Anker-/Versions-/Undo-Wirkung | Changing erfasst nicht die vollständige Setteroperation, Changed ist zu spät für wirkungslose Ablehnung. [TextDocument:810–932](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextDocument.cs#L810) |
| `UndoStack.ClearAll` | `UndoStack.cs` inhaltlich unverändert; Fehlervorprüfung setzt bei offener Gruppe deren Tiefe auf 0 und wirft | ClearAll-Fehlschlag bleibt nicht zustandsneutral; R6-Ursache fortbestehend. [UndoStack:252, 436](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/UndoStack.cs#L252) |
| Erweiterbarkeit | TextDocument/UndoStack weiterhin sealed; Text-Setter, BeginUpdate und ClearAll nicht virtuell | Reflectionprobe bestätigt diese Grenzen am geladenen Paket. Host-Unterklasse kann den Rohvertrag nicht vollständig ersetzen. |
| TextAnchors | TextAnchor-Diff betrifft ToString/InvariantCulture; keine neue Anker-Mutationssemantik | Nativer Positions-/Undo-/Redo-Smoke erfolgreich. [TextAnchor](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextAnchor.cs), [CreateAnchor:1064](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextDocument.cs#L1064) |
| Documentwechsel | Styled DocumentProperty und TextArea-Neuverbindung bleiben; keine allgemeine Widerrufbarkeit bereits ausgegebener Dokumentreferenzen | EditorBridge-Identität und Attach/Detach bleiben erforderlich. [TextEditor](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/TextEditor.cs), [TextArea:265–316](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs#L265) |
| CommandBindings / Inputhandler | `TextAreaDefaultInputHandlers.cs` inhaltlich gleich; TextArea-CommandBindings bleiben öffentliche Liste | Workbench-Umbindung von Undo/Redo kompiliert und funktioniert in Gegenproben. CanExecute bleibt kein umfassendes Mutationsgate. [DefaultInputHandlers](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextAreaDefaultInputHandlers.cs), [TextArea:1136](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs#L1136) |
| TextArea / Fokus / IME | Fokus-Overrides auf FocusChangedEventArgs, Scrollinterfaces ergänzt; Preedit weiterhin false und SetPreeditText leer | Kein neues vollständiges IME-Versprechen. Komposition, Kandidatenfenster und OS-Eingaben separat prüfen. [TextArea:781, 1241, 1312](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs#L1241) |
| Paste | DataTransfer-Clipboard statt alter Text-API; BeginUpdate weiterhin **vor** asynchronem Clipboardlesen, Abschluss über das dann aktuelle TextArea.Document | Dokumentbindung/Update über await bleiben eigenständige B1-Vertragsaufgaben. Neue Transfer-API ist keine Transaktionskorrektur. [EditingCommandHandler:505](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/EditingCommandHandler.cs#L505) |
| Cut / Copy | Erzeugt nun DataTransfer/Item; SetDataAsync-Aufgabe wird nicht awaited; Cut löscht weiterhin nach Copy-Pfad | Asynchrone Clipboardfehler und Copycallbacks sind keine nachgewiesene atomare Cut-Operation. [EditingCommandHandler:384–489](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/EditingCommandHandler.cs#L384) |
| Drag-and-drop | `DataTransfer`, typisierte Formate, `DoDragDropAsync`, gespeicherte PointerPressed-Args | Quellanker werden vor await gebildet, spätere Move-Löschung greift weiterhin über TextArea.Document zu. Wechsel-/Detach-Revalidierung bleibt nötig. [SelectionMouseHandler:327–391](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/SelectionMouseHandler.cs#L327) |
| SearchPanel | Diff nur Fokus-Argumenttyp; ReplaceNext/ReplaceAll-Logik gleich | Direkte Document-/Selection-Mutationen weiterhin von B1 abzudecken. [SearchPanel:401–429](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Search/SearchPanel.cs#L401) |
| Completion | CompletionWindowBase verwendet TopLevel statt VisualRoot, MinHeight-Reset entfällt; CompletionWindow blendet bei leerer Trefferliste aus | Commit weiterhin über ICompletionData.Complete/AnchorSegment; Host-Replace in `InfoCompletionController.cs:405` bleibt. [CompletionWindowBase:71](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/CodeCompletion/CompletionWindowBase.cs#L71), [CompletionWindow:145](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/CodeCompletion/CompletionWindow.cs#L145) |

Die Unterschiede in TextDocument außerhalb des Mutationskerns betreffen unter anderem C#-Property-Backing-Fields für ServiceProvider/FileName. Sie liefern keine neue Zulassungs- oder Exception-Sicherheitsgrenze. Die Tabelle bewertet die genannten Verträge; sie ist keine Behauptung, die gesamte Bibliothek sei unverändert.

## 6. MCP-Sonderprüfung: ExecutionContext und Ownership

### 6.1 Request → UI → angenommener Worker

**S/V:** `WorkbenchEditorOperations.cs:19` führt `OnEditorUiAsync` direkt oder per InvokeAsync aus. Avalonia 12 übernimmt dabei Caller-AsyncLocals, Kultur und gegebenenfalls Impersonation. Die Operation kann diese Objekte zumindest bis zu ihrer Ausführung halten. Ein späteres `ConfigureAwait(false)` entfernt den ExecutionContext nicht. Die vorhandenen Permissions kommen aus dem expliziten `AutomationCaller`; ein automatischer Wechsel der Berechtigungsprüfung zu AsyncLocals wurde nicht gefunden.

**P:** Ein Sentinel-AsyncLocal erreicht in Avalonia 12 den Dispatcher-Callback, in der 11.3.20-Kontrolle nicht. Von diesem Callback aus wird der wirkliche `WorkbenchAutomationService.RunAsync` aufgerufen. Im angenommenen Worker ist der Sentinel **null**. Der bestehende `InterpreterHostQueue.RunAdmittedAsync:36–48` unterdrückt den Flow beim Task.Run-Handoff. Timer unterdrücken ebenfalls den Flow, Cancellation-Registrierung verwendet UnsafeRegister. Dieser Schutz darf bei der Migration nicht entfernt werden.

**P, zusätzliche Lebensdauerprobe:** Worker-Sichtbarkeit ist nicht gleich Retentionsfreiheit. Ein künstliches Request-Objekt wird nur über ein AsyncLocal transportiert; ein bereits abgeschlossener aufrufender Task gibt lediglich WeakReference und Owned-Task zurück. Während der angenommene Worker kontrolliert wartet, bleibt das Objekt nach wiederholtem GC **lebendig**, obwohl der Worker es nicht sieht. Nach Abschluss der serviceeigenen Arbeit ist es **nicht mehr lebendig**. Unterdrückung des Flows bereits am eingehenden Dispatcher-Enqueue macht es schon während des blockierten Workers sammelbar. Die Probe verwendet den tatsächlichen Service und kein HTTP-System, keine sensitiven Daten.

**S:** Die umgebenden asynchronen Hostmethoden/Continuations können den ursprünglichen Context bis zum Arbeitsabschluss halten; das SuppressFlow ausschließlich um Task.Run schützt den gestarteten Worker, nicht automatisch diese äußere Kette. Die Messung lokalisiert keine einzelne GC-Root per Heapdump. Sie belegt aber eine unerwünschte Lebensdauerbindung im ausgeführten UI→Service-Pfad. Nach Abschluss wurde kein dauerhafter Leak beobachtet. Direkt unter einem Request-Context gestartete Hostmethoden verdienen dieselbe Prüfung auch unabhängig von einem Frameworkwechsel; Avalonia 12 transportiert den Context nun zusätzlich über den UI-Handoff.

Request-Wait und Execution-Ownership bleiben hinsichtlich Cancellation getrennt: serviceeigene CTS/Job-Completion, kein Requesttoken als Schedulingtoken für angenommene Arbeit, WaitAsync nur für den anfragenden Wartenden. Die bestehende Probe `AcceptedWorkerDoesNotInheritRequestScopedAmbientObjects` und die Request-Wait-/Result-Recovery-Tests gehören zu den 434 bestandenen GUI-Tests. Sie beweisen **nicht** die gerade beschriebene Retentionsfreiheit. Explizit eingefangene Objekte entfernt SuppressFlow außerdem nicht.

### 6.2 Angenommener Worker → UI: zusätzliche Migrationsgrenze

**P, neue reproduzierte Beobachtung:** Innerhalb einer angenommenen Serviceausführung wird ein UI-Callback mit InvokeAsync ausgeführt. Dort gelingt `InterpreterExecution.AccessState`. Beim entsprechenden 11.3.20-Dispatcher-Vergleich wird derselbe Zugriff mit ExecutionBusyException abgewiesen. Wird der Flow ausschließlich beim Enqueue des UI-Callbacks unterdrückt, ist der Zugriff auch unter 12 wieder abgewiesen.

| Probe | Avalonia 11.3.20 | Avalonia 12.1.4 |
| --- | --- | --- |
| Caller-Sentinel im InvokeAsync-Callback sichtbar | False | True |
| Caller-Sentinel im tatsächlichen angenommenen Serviceworker | Bestehende Schutzintention; hier nicht nochmals als kompletter 11-Servicegraph getestet | **False** |
| Caller-Objekt lebt während blockierter serviceeigener Arbeit nach abgeschlossenem Aufrufer weiter | Keine vollständige 11-Service-Retentionsprobe ausgeführt | **True**, nach Abschluss False |
| Caller-Objekt lebt bei bereits unterdrücktem eingehendem UI-Handoff weiter | – | **False**, bereits während des blockierten Workers |
| Core-Ambient-Scope erlaubt AccessState im Worker→UI-Callback | False | **True** |
| Derselbe 12-UI-Hop mit unterdrücktem Flow | – | False |

**S:** `InterpreterExecution` hält seinen asynchronen Scope in AsyncLocal; der Service hält `current` als AsyncLocal<Job?>. Dispatcher-Capture kann somit serviceeigene Capability-/Jobreferenzen in UI-/Präsentationscallbacks übertragen und durch weiter eingeplante Arbeit verlängert halten. Der Versuch beweist die veränderte Zugriffsgrenze, **keinen nachgewiesenen Produktionsdatenabfluss und keinen Authentisierungsbypass**. Scope-Owner-/RunnerActive-Prüfungen bleiben bestehen. Zusammen mit der gemessenen Request-Objektbindung ist das ein eigenständiges Integrations-Exit-Gate.

Zu prüfen sind insbesondere `PasswordDialogHost.RequestAsync`, die UI-Präsentation in `CryptoScriptEditViewModel`, `EditorReplaceConfirmation`, `OnEditorUiAsync` und die Print-/Info-Posts. Auch `Dispatcher.Post` erzeugt im untersuchten 12.1.4-Quellstand eine contextcapturende DispatcherOperation; pauschales Ersetzen von InvokeAsync durch Post löst die Frage nicht. [DispatcherOperation:44–58](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Threading/DispatcherOperation.cs#L44), [Dispatcher.Invoke](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Threading/Dispatcher.Invoke.cs).

**Vorgeschlagener Host-Vertrag, noch nicht implementiert:** Request-Kontext am Übergang zur angenommenen Arbeit weiterhin unterdrücken und den gesamten serviceeigenen Start einschließlich äußerer Continuations vom Request-Context trennen; ausgehende UI-Aufträge nur mit ausdrücklich benötigten immutable Daten, Lifecycle-/Editoridentität und Token ausstatten. Ambient-Capabilities nicht versehentlich delegieren. Falls SuppressFlow gewählt wird, nur synchron um das Enqueue legen, vor dem await wiederherstellen und den CheckAccess-Direktpfad gesondert behandeln. Queue-Closure und Cancellationregistrierungen zusätzlich prüfen. Die durch den neuen UI-Handoff entstehende Integration gehört zu **A (Avalonia-12-Anpassung)**, nicht zur AvaloniaEdit-B1-Korrektur; keine Änderung am CryptoScript-Core vorgesehen. Keine pauschale Produktionslösung allein aus der erfolgreichen unterdrückten Probe ableiten.

Save-Drain bleibt in den ausgeführten Tests erhalten. `WorkbenchSaveCommit` besitzt aber einen eigenen Task.Run-Dateischreibpfad ohne dieselbe Worker-SuppressFlow-Grenze. Das ist eine separate statische Context-Inventurstelle, keine hier nachgewiesene neue Drain-Regressionsbehauptung. Keine zusätzliche Abbruchstelle zwischen bestätigtem Delete und Write einführen.

## 7. Isolierter Prototyp und Gegenproben

### 7.1 Offengelegte Anpassungen

1. Direkte UI-Paketversionen ausschließlich in der Quellenkopie auf die Matrix angehoben; Core-Pakete unverändert.
2. Veralteten BindingPlugins-Aufruf entfernt; Compiled-Binding-Default nur im Versuch auf false gesetzt. Keine XAML-View neu entworfen.
3. Erst alte öffentliche Prism-Binaries als begrenzte Kontrolle; anschließend lokal gebaute Assemblies des gemergten Prism-Commits für die Hauptnachweise.
4. Originalproben kopiert; Assertions, Callbacklogik, R6-/R7-Bedingungen und Rekursionslimits beibehalten. Nur Headless-API-Portierung: vier Tastenaufrufe um PhysicalKey/Symbol ergänzt, Clipboard-Extension-Namespace ergänzt, Session-Disposal auf `await using` umgestellt.
5. Ebenso nur Session-Disposal in betroffenen bestehenden GUI-Testquellen der Kopie angepasst. NUnit-/Testassertions nicht abgeschwächt.
6. Testhost ergänzt eigene Prism-Dialogfixture, Contextprobe und explizite Icon-Auslieferung. Keine dieser Fixtures wurde dem Original hinzugefügt.

**Headless-Abbau:** Die anfangs bytegleiche BoundaryProbe lieferte alle 46 Ergebnisse, hing danach aber im synchronen Session-Abbau und wurde als eigener Probeprozess beendet. Der 12.1.4-Quelltext erzeugt ein TCS ohne asynchrone Continuationoption und wartet in Dispose synchron auf die Dispatchtask. Eine inline auf dem Dispatchthread fortgesetzte Main-Methode kann dadurch auf sich selbst warten. Mit ausschließlich asynchroner Disposal endet dieselbe rote Matrix sauber mit Exit 1. Das erste Hängen ist kein Test-PASS und wurde nicht durch Entfernen roter Assertions kaschiert. [HeadlessUnitTestSession](https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs).

### 7.2 Ergebnisse

| Versuch | Tatsächliches Ergebnis | Aussagegrenze |
| --- | --- | --- |
| Build ohne Codeanpassung | CS0122 bei BindingPlugins | Kein bloßer Paketwechsel möglich |
| Danach mit neuem Binding-Default | 50 XAML-Fehler, sechs Views | Compiled Bindings nicht migriert |
| Reflection-Kompatibilitätsbasis | Build erfolgreich | Nur Versuchsbasis |
| Echte App, zunächst öffentliches Prism-11-Paket | Headless-Start, Auflösung, fünf Regions, zwei Navigationen erfolgreich | Keine offizielle Prism-12-Distribution daraus ableiten |
| Echte App mit Prism-Quellbuild | Headless und später Windows nativ erfolgreich; Dialog OK; Anchor/Undo/Redo/Textinput erfolgreich | Begrenzter Smoke, kein vollständiges UI-Akzeptanztestset |
| Bestehende GUI-Suite | **434 bestanden / 0 fehlgeschlagen / 0 übersprungen**, ca. 3 s | Kein Ersatz für die separat rote BoundaryProbe; Core-/MCP-Vollsuiten hier nicht erneut ausgeführt |
| Ursprüngliche unabhängige Gegenproben | Alle Beobachtungen ausgeführt, Exit 0 | Exit 0 bedeutet erfolgreichen Beobachtungslauf, keine allgemeine Vertragsfreigabe |
| Zusätzliche Originalgegenproben, R6 isoliert | Text `edit`, Revision 2→3, `InternalError`, Update offen, Folgeedit Busy | **R6 und zugehöriger R4-Vertrag weiter verletzt** |
| R7 isoliert | Save- und Edit-Normalisierer erreichen Limits 8 und 32; ungesperrte Kontrolle terminiert bei Tiefe 2 | **R7 und zugehöriger R2-Vertrag weiter verletzt**; kein tatsächlicher Stackoverflow provoziert |
| BoundaryProbe | **42 PASS / 4 FAIL**, sauberer Exit 1 nach Harness-Portierung | Exakt die gleichen vier Sollvertragsverletzungen |
| Context-Proben | Worker sieht Request-Sentinel nicht; äußere Hostkette hält dessen Objekt bis zum Abschluss; Worker→UI erbt Core-Scope | Eigenständiges Integrationsrisiko trotz grüner Cancellation-/Standardtests |
| Publish | win-x64, linux-x64, osx-arm64 erfolgreich | Framework-dependent; kein Laufzeitnachweis für Linux/macOS |

Die vier roten Matrixfälle:

| Fall | Ergebnis unter AvaloniaEdit 12 | Blockerzuordnung |
| --- | --- | --- |
| `callback/Started/True/same` | Leerer Basistyp-Setter im UpdateStarted beschädigt Undo-Gruppenzustand; äußerer Text bereits geändert, Update offen, Folgeedit Busy | R6 / R4 |
| `callback/Started/True/clear` | Gleicher Fehler über Clear | R6 / R4 |
| `normalizer/save=False` | 16 Callbackaufrufe, Tiefe 16 bis künstliches Limit | R7 / R2 |
| `normalizer/save=True` | 16 Callbackaufrufe, Tiefe 16 bis künstliches Limit | R7 / R2 |

**Einordnung R2/R4:** Die gewöhnlichen früheren R2-/R4-Korrekturpfade sind weiterhin grün, etwa einzelne Basistyp-Setter während Save und dokumentgebundene Reentrancyablehnung. Dennoch bleiben ihre umfassenden Garantien wegen R6/R7 offen. Nicht behaupten, jeder einzelne historische R2-/R4-Fall scheitere; ebenso wenig aus den grünen Teilfällen einen Abschluss dieser Findings ableiten.

Weitere erhaltene Kontrollen: Save-/Shutdown-Reentrancy wartet auf Drain; nach bestätigtem Delete erfolgt der Writeversuch trotz Shutdown/Detach; widerrufene Freigaben bleiben Forbidden; externer ExecuteEditor bleibt gesperrt; Undo/Redo nach Save funktioniert; direkte rohe UndoStack-Aufrufe bleiben die dokumentierte Negativgrenze. Reale Unicode-Texteingabe in der Originalprobe ist **kein** nativer IME-Kompositionstest.

### 7.3 Abdeckung der zehn Prototypziele

| Ziel | Bewertung |
| --- | --- |
| 1. Start mit Avalonia 12 | **P:** echte App im Headless- und Windows-Native-Host |
| 2. Prism/DryIoc initialisieren | **P:** echte Registrierungen mit isolierten Prism-Assemblies |
| 3. ViewModel-/View-Auflösung | **P:** reale Views und Editor-VM, kein Ersatzmodell |
| 4. Navigation und Dialoge | **P:** zwei reale Navigationsziele, zusätzlicher echter Prism-Dialog, bestehende produktive Dialogtests |
| 5. CryptoScript-Dokument anzeigen/bearbeiten | **P:** WorkbenchTextEditor mit `Print(0x(AB))`, reale Texteingabe; Windows-Message-Loop läuft |
| 6. TextDocument/Undo/Redo/Anchors | **P:** Smoke und bestehende Tests; Grenzen der roten Fälle separat |
| 7. Reale Headless-Editoroperationen | **P:** Original-, Zusatz-, Boundary- und GUI-Tests |
| 8. EditorBridge/Revisionen portierbar | **P:** identische Produktquellen und Registry-/Revisionstests; kein Freibrief für R4/R6 |
| 9. Gemeinsame Admission/Save-Drain | **P:** vorhandene Tests/Originalkontrollen grün; Context-Rückfluss zusätzlich offen |
| 10. Eigenständige R6/R7-Vertragsproben | **P:** erhalten und rot; nicht als erwartete Produktkorrektur umgeschrieben |

## 8. Plattform- und Publish-Bewertung

**Windows x64 – P:** Framework-dependent Publish und nativer Start des Testhosts erfolgreich. Die reale App erzeugt Shell und Regions, navigiert, öffnet/schließt den Prism-Dialog, editiert das Dokument und durchläuft den nativen Message-Loop. Gelieferte Assets umfassen `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll`.

Der erste Native-Start scheiterte an `SidebarViewModel.cs:50`: `WindowIcon("./Assets/avalonia-logo.ico")` wird relativ zum Arbeitsverzeichnis geladen. Headless hatte dieses Problem nicht sichtbar gemacht. Auch im ersten Publish fehlte das lose Icon. Nach expliziter Content-/Publish-Aufnahme **im Testhost** und Start aus dessen Verzeichnis funktioniert der Versuch. Dies ist eine nachgewiesene Ressourcen-/CWD-Grenze, keine bewiesene neue Avalonia-12-Regressionsursache. Für das Produkt später Resource-URI/Stream oder explizite Deploymentregel festlegen.

**Linux x64 – P/O:** Publish erfolgreich; `libSkiaSharp.so` und `libHarfBuzzSharp.so` vorhanden. Kein Linux-Prozess gestartet. Offen: X11/Wayland-Umgebung, DBus/Portale, Schriftfallback, Clipboardownership und Lifetime, DnD-Move, IME, GPU/Fallback, Dekoration, tatsächliche unterstützte Distribution und Systembibliotheken.

**macOS arm64 – P/O:** Publish erfolgreich; `libAvaloniaNative.dylib`, `libSkiaSharp.dylib`, `libHarfBuzzSharp.dylib` vorhanden. Kein macOS-Prozess gestartet. Offen: App-Bundle, Signierung/Notarisierung, Intel-Variante, Cmd-Shortcuts, Owner-/Modalverhalten, DPI, Fonts, Clipboard/DnD, IME und Beenden mit laufender Ausführung/Save.

Alle Plattformen benötigen zusätzlich: Auswahl/Caret/Scroll, Completion-Popup samt Fokusverlust, Search/Replace, Cut/Paste mit asynchronem Clipboard, native Komposition, Windowclose/Detach während Dialog/Job/Save sowie reale Publish-Installation. Weder erfolgreiche Restores noch diese drei Publish-Verzeichnisse belegen all diese Eigenschaften. Kein AOT-/Trim-/Single-File-Support zugesagt; reflectionbasierte XAML-/Prism-Auflösung wäre dafür gesondert zu bearbeiten.

## 9. Auswirkungen auf MCP-01.4: A/B/C/D getrennt

**A:** ausschließlich Avalonia-12-Anpassung. **B:** ausschließlich AvaloniaEdit-12-Anpassung. **C:** Vertragskorrektur R2/R4/R6/R7 durch Variante B/B1. **D:** sinnvoller Bestandteil des späteren GUI-Redesigns.

| Komponente | A | B | C | D |
| --- | --- | --- | --- | --- |
| `WorkbenchTextEditor` | Property-/Commandintegration im neuen Framework abnehmen | Kompiliert ohne Produktquelländerung; Backend-Input/Transfer prüfen | Späte BeforeUpdate/BeforeChange-Guards durch früh zugelassenen Bibliotheksvertrag integrieren; Setter/Clear/Undo-/Compositeoperationen vollständig erfassen | Schmale Hostschnittstelle für neues UI, keine Schattenkopie |
| `EditorBridge` | Dispatcher-/Lifecycle-Grenze, Contextpolicy | Dokument-/Versions-/Anker-API grundsätzlich gleich | Identität/Revision müssen auch bei Callbackfehlern an ursprünglichem Dokument bleiben | Präsentation entkoppeln, native Dokumentidentität intern halten |
| `WorkbenchEditorOperations` | `OnEditorUiAsync`, Context-Fluss und direkter CheckAccess-Pfad | Kein neuer Editor-DTO-Vertrag erforderlich | Keine InternalError-/Busy-Antwort als wirkungslose Ablehnung nach bereits erfolgter Wirkung; B1-Abschluss integrieren | Keine fachliche ExecuteEditor-Ausgabesemantik im Redesign nebenbei ändern |
| `WorkbenchSaveCommit` | UI-Context/Completion/Drain weiter nachweisen; Dateiarbeiter separat inventarisieren | Kein notwendiger neuer Dateiformatpfad | Lease muss vor allen Editor-/Dokumenteffekten ablehnen; R7-Terminierung statt auslösendem EndUpdate-Workaround | Dateiauswahl-/Dialoggestaltung später; ASCII/Filter/LF/Append/Delete→Write erhalten |
| `CryptoScriptEditViewModel` | InvokeAsync/Post, Attach/Detach, Lifecyclegeneration und XAML-Verträge | TextDocument/Anchor-Aufrufe portierbar | History/Import/Textsetter/Printpfade in einheitlichen Mutationsvertrag aufnehmen | UI-Struktur und Verantwortlichkeiten erst nach technischem Vertrag gestalten |
| `InfoCompletionController` | Popup-/Fokus-/Dispatcher-Abnahme | Verändertes Leerlistenverhalten und TopLevel-Integration prüfen; direkter Replace bleibt | Completion-Commit als ganze Operation zulassen, Callbackfehler begrenzen, an alte Identität binden | Completion-UX später |
| Prism-Views/Dialoge | Neue Distribution, Bootstrap, Regions, typed XAML, entfernte ValidationAPI | Kein AvaloniaEdit-spezifischer Prism-Wechsel | Kein Ersatz für Bibliotheksgate | Shell, Navigation und Dialogdesign später |
| Headless-Tests | Session-Lifetime, Clipboardnamespace und Tasten-API portieren | Reales 12.0.0-Paket/Sourceidentität dokumentieren | R6/R7 und rote Boundary-Assertions erhalten, erst durch echte Korrektur grün | Native UI-Akzeptanztests ergänzen |
| CryptoScript-Core / Interpreterpipeline | **Keine Änderung vorgesehen** | **Keine Änderung vorgesehen** | Host-/Bibliotheksgrenze lösen; keine zweite Pipeline | Fachkern unabhängig halten |

Unverändert nutzbar erscheinen DTOs, Registry-/Revisiongrundmodell, fail-fast Admission, serviceeigene Cancellation, Save-Drain und die bestehende Interpreterpipeline. Das ist durch die ausgeführten Tests gestützt, nicht als pauschale Fehlerfreiheit formuliert. Die neue Dispatcher-Capabilityübertragung bleibt trotz grüner Standardtests ein gesonderter A-Punkt.

## 10. Vergleich der Migrationsstrategien

| Kriterium | 1. Ganze GUI zuerst, dann B1 auf 12 | 2. B1 isoliert auf 12, dann GUI | 3. Migration und Bibliothekskorrektur gemeinsam |
| --- | --- | --- | --- |
| Abhängigkeiten | Früh Prism-Distribution, vollständige Paketmatrix, XAML, Context und Plattformen nötig | B1 benötigt zunächst AvaloniaEdit/Avalonia/Headless 12; Prism für Bibliothekskern nicht nötig, echter Hostadapter kann separat getestet werden | Alle Abhängigkeiten gleichzeitig |
| Testbarkeit | Viele Frameworkänderungen vor Klärung des entscheidenden Mutationsvertrags; bekannte rote Tests begleiten große Migration | Bibliotheksfehler mit erhaltenen Rohproben isolierbar; danach Host- und Plattformtests mit festem Vertrag | Ursachen grüner/roter Ergebnisse schwerer zuzuordnen |
| Risiko | Technisch tragfähig, aber große Vorleistung ohne Behebung der MCP-Blocker | Niedrigstes Ursachen-/Integrationsrisiko; späterer Frameworkaufwand bleibt sichtbar | Höchstes Regressions-/Reviewrisiko und größere Rücknahmefläche |
| Lieferbarkeit | GUI-Meilenstein möglich, MCP-01.4 bleibt bis B1 blockiert; öffentliche Prism-12-Distribution derzeit nicht belegt | Konkreter B1-Meilenstein trotz offener Prism-Distribution; danach getrennte Integrationslieferung | Gesamtergebnis hängt am langsamsten Teil; kein früher sauberer Bibliotheksnachweis |
| Uncommittete MCP-01.4-Arbeit | Muss zuerst verlustfrei in separater Migrationsbasis erhalten werden; viele parallele Änderungen an denselben Dateien | Vorhandene Arbeit zunächst schreibgeschützte Host-Testbasis; weniger frühe Konflikte | Höchstes Risiko, Hostkorrektur und Frameworkport untrennbar zu vermischen |
| Redesign | Kann verführen, View-Neugestaltung vor Vertragsklärung mitzunehmen | Redesign folgt stabiler Host-/Bibliotheksgrenze | Drei Vorhaben drohen zusammenzufallen |

**Empfehlung 2** folgt nicht nur aus erfolgreichen Builds: Der unveränderte Mutationskern, vier reproduzierte Vertragsfehler, der positive echte Host-/Windowsnachweis und die separate Context-Regressionsprobe bestimmen die Reihenfolge. Strategie 1 ist vertretbar, wenn ein unabhängig priorisierter GUI-Meilenstein die Vorleistung rechtfertigt und die roten MCP-Verträge ausdrücklich offen bleiben. Strategie 3 ist hier nicht zu empfehlen.

## 11. Begrenzter konkreter Folgeplan und überprüfbare Einheiten

Dieser Plan ist eine Entscheidungsvorlage, **kein Beginn der Umsetzung**.

1. **B1-Auftrag separat freigeben.** AvaloniaEdit 12.0.0 am genannten Commit als Ausgangsbasis; generisches frühes Mutationsgate und ausnahmesicherer Abschluss. Original-/Basistyp-Setter, Clear, Update-/Undo-Gruppen, Callbacks, Dokumentwechsel, Paste/Drop/Completion als klar benannte Verträge. Keine Abschwächung R2/R4/R6/R7, kein bloßes Verstecken öffentlicher Setter. Exit: unveränderte rote Sollproben tatsächlich grün, kein offenes Update, korrekte Historie/Identität/Revision und terminierende Ablehnung.
2. **Prism-Distribution parallel als Entscheidungsfrage klären.** Offizielles Avalonia-12-Paket, Feed, exakte Identität/Commit, Container-/DryIoc-Versionen und Lizenzrechte festlegen. Lokaler Quellbuild ist nur Nachweis. Eigene produktive Pakete erst nach ausdrücklicher Eigentümerfreigabe und reproduzierbarer Build-/Pflegestrategie.
3. **Separate Migrationsbasis vorbereiten.** Später neue `codex/…`-Branch/Worktree-Struktur für Hostmigration, getrennt vom Original `feature/mcp-server`; AvaloniaEdit-B1 in separater Bibliotheksbasis. Da HEAD die MCP-01.4-Dateien nicht enthält, genügt ein frischer Worktree von HEAD nicht: uncommittete und unversionierte Quellen nach freigegebenem Snapshot-/Übertragungsverfahren samt Hashmanifest übernehmen. Keine versteckte Commit-/Stash-Aktion am Original.
4. **Frameworkport in begrenzten Revieweinheiten.** Paketgraph einschließlich stabiler DialogHost-Auswahl; App-/Validation-/Prism-Integration; typed XAML/Template-/Parentbindings oder ausdrücklich befristeter Reflection-Zwischenschritt; Tests/Harness. Contextpolicy für eingehende Requests und ausgehende UI-Callbacks als eigene überprüfbare Einheit mit 11/12-Differenzproben. Core unverändert.
5. **B1 im echten Host integrieren.** EditorBridge/WorkbenchEditorOperations/SaveLease/Completion an den bewiesenen Bibliotheksvertrag anschließen. Gemeinsame Admission, Request-Wait-Trennung, Snapshot-Dateisemantik und MCP-01.5-Sperre behalten. Neue Bibliotheks- und Hosttests getrennt bewerten.
6. **Abnahme und danach Redesign.** Bestehende Core-, GUI- und MCP-Suiten plus Originalgegenproben, Boundarymatrix und Contextproben; native Windows-/Linux-/macOS-Tests einschließlich Publish/Installer/Lifecycle/Clipboard/DnD/IME. Erst danach Integrationsentscheidung. Layout, Shellgestaltung und UX nicht in die B1-Korrektur hineinziehen.

Empfohlene spätere Commitgrenzen: **Bibliotheksvertrag + eigene Tests**, **Host-Paket-/Prism-Integration**, **Binding-/Validationport**, **Dispatcher-/Lifecycle-Context-Vertrag + Tests**, **B1-Hostadapterintegration**, **Plattform-/Publishkorrekturen**. Jede Einheit soll separat nachvollziehbar sein; Zwischenstände dürfen bekannte rote Proben nicht als grün umetikettieren. Reihenfolge der Integration richtet sich nach dem B1-Exit und verfügbarer Prism-Distribution. In diesem Auftrag wurde kein Commit erstellt.

## 12. Entscheidungsvorlage für den Projekteigentümer

| Entscheidung | Vorlage |
| --- | --- |
| Avalonia 12 als Ziel | Bereits verbindlich; hier bestätigt, nicht erneut zur Disposition gestellt |
| Technische Reihenfolge | **Strategie 2 empfehlen:** isolierter B1-Nachweis auf AvaloniaEdit 12, danach getrennte GUI-Migration |
| B1-Vertrag | R2/R4/R6/R7 unverändert erhalten; keine Freigabe von Variante A durch Hintertür |
| Prism-Produktionsbezug | **Offen:** konkretes offizielles/privates Paket oder ausdrücklich genehmigte eigene Distribution samt Lizenz/Pflegebudget |
| GUI-Migrationsumfang | Framework/Pakete, Binding-/Validationport, Context-/Lifecycle-Schutz, Testharness und Plattformabnahme; Redesign separat |
| Produktionspins | Erst nach Graph-/Lizenz-/Plattformabnahme genehmigen; heutige Kandidaten sind keine Pins |
| MCP-01.4-Freigabe | **Nicht erteilen:** vier rote Vertragsfälle und offene A12-Context-Integration |
| Nächster ausführbarer Auftrag | B1-Bibliotheksnachweis, ausdrücklich begrenzt und ohne Produktmigration; alternativ Strategie 1 nur als bewusste Priorisierungsentscheidung |

**Stopppunkt:** Untersuchungsbericht erstellt. Die endgültige Strategie, jede Bibliotheksänderung und die produktive Integration werden separat entschieden.

## Anhang: Nachvollziehbare lokale Evidenz

Alle folgenden Pfade liegen unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/`, nicht im Originalrepository. Der Bericht enthält die wesentlichen Resultate selbst; die Versuchsausgaben dienen der Nachprüfung und sind keine zusätzlichen Produktlieferungen.

| Evidenz | Datei/Verzeichnis |
| --- | --- |
| Eingangsstand / Hashes | `status-before.txt`, `staged-before.txt`, `source-manifest.json` |
| Öffentliche Paketlisten / Manifeste | `research/*-versions.json`, `research/*.nuspec`, `research/package-index.json` |
| Gepinnte Bibliotheksquellen | `upstream/AvaloniaEdit-12.0.0`, `upstream/AvaloniaEdit-b56a35a5d4838f3c9a9adeba90dd80952f70e774`, `upstream/Prism-358118cd640d9a22ff8cf21c8ad197fa038b7990` |
| API-/XAML-Buildfehler | `build-public.log`, `build-xaml.log`; erster unvollständiger Kopierlauf nicht als Frameworkfehler gewertet |
| Lokaler Prism-Build | `build-prism-source-local.log`, `restore-prism-source-local.log` |
| Erste unveränderte Boundary-Ausgabe | `boundary-public.log` – vollständige 42/4-Ausgabe, anschließend synchroner Abbauhang |
| Portierte rote BoundaryProbe | `boundary/BoundaryProbe.cs`, `boundary-source.log`, `.exit` = 1 |
| Unabhängige Originalkontrollen | `original/Program.cs`, `original-source.log`, `.exit` = 0 |
| R6/R7 und zusätzliche Kontrollen | `additional/Program.cs`, `r6-source.log`, `r7-source.log`, `additional-source.log`, jeweils Exit 0 als Beobachtungslauf |
| Bestehende GUI-Suite | `test-gui.log`, `test-results/gui-u12.trx` – 434/0/0 |
| Context-Nachweis | `context/Program.cs`, `context.log`, `context-retention.log`; `context11/Program.cs`, `context11-final.log` |
| Reale App / Navigation / Dialog / Editor | `bootstrap/Program.cs`, `bootstrap-public.log`, `bootstrap-final.log`, `publish-windows-final.log` |
| Publish / Native Assets | `publish-win-x64-final.log`, `publish-linux-x64-final.log`, `publish-osx-arm64-final.log`; `publish/<rid>/` |
| Abschließende Integritätsprüfung | `integrity-final.json` |

Originalproben vor Portierung (SHA-256):

- `mcp014-corrections2/BoundaryProbe.cs`: `174706C331B9D5F50CDE1A78FF468F20B662BF3029921CCAFE7184983C7DE857`
- `mcp014-independent-review/Program.cs`: `A893703D94A57EE4CA032433B85E86F7342FF49AB94F87AA4E921ED65C775294`
- `mcp014-final-independent-review/Program.cs`: `84B11377EC1729F4573A6A12A456E41B068E78D97E192694F6B93CFA60D7A382`

Reproduktion auf der vorhandenen isolierten Basis: mit SDK 10.0.401 die jeweiligen `probe.csproj` in Release und eigener `NuGet.Config` bauen; die resultierende `CryptoWorkBenchGuiTest.dll` ausführen. `additional` akzeptiert `started-empty` und `normalizer`; `boundary` liefert absichtlich Exit 1. Die GUI-Suite wird über `dotnet test source/CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release` ausgeführt. Der Bootstrap akzeptiert `native`; Publish erfolgte mit `-r win-x64|linux-x64|osx-arm64 --self-contained false`. `prepare.ps1`, `use-prism-source.ps1` und die lokalen Projektdateien dokumentieren die Versuchskonfiguration; sie sind keine Produktmigrationsskripte.
