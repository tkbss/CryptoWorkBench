# G1-I01 – Isolated G1 + L1 Integration Feasibility

Stand: 10.11.2026. **CONDITIONAL GO** für die Vorbereitung einer späteren Integration. Die isolierte Windows-Komposition wird nachfolgend anhand neuer Läufe bewertet. Dies ist keine endgültige Master-Integration und keine Linux-/macOS-Freigabe.

Ergebnisdatei: [G1-I01-results.json](evidence/G1-I01-results.json). Alle neuen Rohbelege liegen unter [artifacts/g1-i01](../../artifacts/g1-i01). Der ursprüngliche G1-Worktree und der Mastercheckout bleiben unverändert.

## 1. Herkunft und gesicherte Ausgangsstände

| Stand | Identität und Herkunft |
|---|---|
| Aktueller Master/L1 | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`, Tree `6a67e560e05d8b472fac8fe5ebae8be24dbd916b`. Lokaler Master und lesende verbundene GitHub-Abfrage von `master` stimmen überein. |
| G1 | Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`, **vollständige uncommittete Arbeitskopie einschließlich A05/M1 und A06**. Der HEAD allein ist keine G1-Lieferung. |
| Neue Arbeitskopie | `artifacts/worktrees/g1-i01-composition`, Branch `codex/g1-i01-composition`, HEAD bleibt auf obigem Master. Mit `git worktree add -b` angelegt; keine Commits oder Mergeoperationen. |

Vor der Worktreeanlage wurden die A06-Manifeste vollständig gegen die Originaldateien geprüft: 1.149 ursprüngliche Dateien, 15.831 historische Raw-Dateien und 2.004 A06-Artefakte einschließlich seiner beiden Berichte, ohne Hashabweichung. Auch der vollständige G1-Git-Status entsprach A06. Hash des A06-Artefaktmanifests: `E0EF9ABDE03B0EE362F078F7617E2A6D8378BCB932A699F071552FFAC19CAD41`.

Im neuen Worktree wurden anschließend **1.151 G1-Quelldateien** und **1.055 unveränderte Masterdateien** byteweise gesichert und SHA-256-inventarisiert. [Preflight](../../artifacts/g1-i01/baseline/preflight.json), [G1-Manifest](../../artifacts/g1-i01/baseline/g1-source-manifest.json), [Master-Manifest](../../artifacts/g1-i01/baseline/master-source-manifest.json) und [Kompositionsnachweis](../../artifacts/g1-i01/baseline/composition.json) halten Herkunft und Inhalte fest. Die maschinenlesbare Evidence referenziert zusätzlich die Hashes dieser Manifeste.

## 2. Vollständige Übernahme und Konfliktprüfung

**152 akzeptierte G1-Dateien übernommen: 37 Änderungen an getrackten Dateien und 115 neue Dateien.** Die [vollständige Übernahmeliste](../../artifacts/g1-i01/baseline/transfer-plan.json) enthält jeden Pfad, Herkunfts-SHA-256, vorherigen Master-SHA-256 beziehungsweise fehlendes Ziel und die geprüfte Aktion. Sie umfasst Produktcode, Tests, SDK-Projekte, Locks, Ressourcen, Treiber, Prototyp und akzeptierte Berichte/Evidence einschließlich A06. Ignorierte historische Testausgaben, Binaries, alte Probeläufe und Buildverzeichnisse wurden nicht als Produktänderung kopiert.

Die 35 seit der gemeinsamen Basis durch L1 geänderten/ergänzten Pfade haben **keine Schnittmenge** mit den 152 G1-Pfaden. Vor dem ersten Kopieren wurde für jede der 37 Ersetzungen zusätzlich geprüft, dass der Masterblob mit dem ursprünglichen G1-Basisblob übereinstimmt und die Zieldatei unverändert ist. Jeder neue G1-Pfad musste frei sein. Erst der vollständig erfolgreiche Vorabvergleich erlaubte das Kopieren aus dem gesicherten Snapshot. Jeder Zielhash wurde danach erneut verglichen.

Ergebnis: **keine tatsächlichen Pfad-/Inhaltskonflikte, keine Produktanpassung notwendig**. Alle 35 L1-Dateien bleiben bytegleich ([L1-Erhaltungsmanifest](../../artifacts/g1-i01/baseline/l1-preserved.json)). Grammatik, generierter Parser, Assignment-/Initializer-Evaluatoren, L1-Tests und Diagnostik wurden nicht ersetzt. Es wurde weder `git merge` noch Rebase, Cherry-Pick oder automatische Konfliktauflösung verwendet.

Die übernommenen historischen Berichte sind unveränderte Provenienz. Ihre ursprünglichen relativen Raw-Verweise beziehen sich fachlich auf die Belege im Original-G1-Worktree; diese Raw-Daten wurden bewusst nicht in die neue Arbeitskopie dupliziert. Neue I01-Nachweise sind ausschließlich die hier referenzierten Läufe.

## 3. Neue permanente Kombinationstests

Die vorhandenen L1-Tests verwenden den bisherigen Editorhost; die G1-Tests vor dieser Komposition kennen keine Assignment-Grammatik. Deshalb ergänzt allein [DockAssignmentCompositionTests.cs](../../CryptoWorkBenchGuiTest/DockAssignmentCompositionTests.cs) **sieben GUI-Fälle** auf dem vorhandenen DockToolTests-Host. Keine vorhandene Assertion und keine Testdatei wurde verändert. Das SDK-Projekt bindet die neue Datei automatisch ein; keine Projekt-/Lockänderung nötig.

| Fälle | Tatsächlich zusätzlich geprüfter Vertrag |
|---:|---|
| 2 | Enter mit Deklaration, Assignment und anschließendem Print im Haupt-/Floating-Editor; exakter Output und Quellspalte, globale Variables, ein Historyeintrag, identischer Editor/TextDocument/UndoStack, SaveIntent und keine Wiederholung durch Redock/Undo/Redo. |
| 2 | Direkter beziehungsweise verschachtelter Print-Aufruf innerhalb eines Assignment-Ausdrucks: Parser-Quellposition erhalten, aber L1 lehnt vor Ausführung ab; kein Print, keine Mutation, keine Folgeanweisung, redigierte semantische Diagnose, keine erfundenen Navigationskoordinaten. |
| 1 | S-01: gemeldeter KeyCheck-Fehler erlaubt die bestehende Fortsetzung einschließlich Assignment/Print; geworfener Assignment-Typfehler bricht ab, erhält das alte Variablenobjekt und erzeugt keine zusätzliche Ausgabe. |
| 2 | Erfolgreiches beziehungsweise fehlschlagendes Assignment über authentifizierte MCP-Lease im gefloateten G1-Editor; identische Session, redigierte Ergebnisse, Request-Deduplikation, Dock-Sperre während Lease und identitätserhaltendes Redock nach Freigabe. |

Wesentliche Klarstellung: **Print innerhalb eines Assignment-RHS ist in L1 kein erlaubter Ausgabepfad.** `AssignmentEvaluator.ValidateCallTree` verwirft Print/Info und weitere Void-Operationen einschließlich verschachtelter Aufrufe vor der Auswertung. Die Parserposition des Print-Tokens wird trotzdem korrekt durch den G1-erweiterten AST erhalten (Zeile 2, Spalte 12 beziehungsweise 28). Für die tatsächlich erlaubte Ausgabe nach einem Assignment prüft die Enter-Integration Zeile 1/Spalte 37 und den Navigationsoffset 36. Weder L1 noch der G1-Outputvertrag wurden zur Ermöglichung einer verbotenen Ausgabe erweitert.

Die neuen Tests hatten zwei erhaltene Entwicklungsstände: `targeted-1` scheiterte beim Kompilieren am fehlenden AvaloniaEdit-Import; außerdem wurde die händisch gezählte Print-Spalte vor dem ersten ausführbaren Lauf korrigiert. `targeted-2` hatte 3 PASS/4 FAIL, weil die neue Fixture History-Navigationsevents als Eintragszähler verwendete und die WorkspaceGeneration über MCP-Epochwechsel hinweg unverändert erwartete. `HistoryService.AddLine` schreibt die Liste; `PublishLine` gehört zur Navigation. `EditorCoordinator.Advance` erhöht die Epoch bei Lease-Wechseln absichtlich. Die Korrektur prüft den exakten Historylisteninhalt und vergleicht die Generation unmittelbar vor/nach Docking nach Release; zusätzlich verlangt sie den erwarteten Fortschritt durch die Lease. Das sind korrigierte neue Testannahmen, kein Produktfix und keine Abschwächung bestehender Tests. Quellen dieser Entwicklungsstände und rote Logs bleiben erhalten. `targeted-3` besteht mit 7/7; endgültige Debug-/Release-Ergebnisse folgen unten.

## 4. Builds, Locks und vollständige Regression

Windows x64; .NET SDK 10.0.401. Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock 12.1.0.13 bleiben gepinnt. Prism wird im eigenen Dependencycheckout vom offiziellen Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` bereitgestellt; die bestehende Vorbereitung prüft 1.180 Dateien. Keine Fremd-Worktree-Buildausgabe wird als Kompositionsbinary verwendet.

Beide Solutions bauen in Debug und Release mit Locked Restore erfolgreich (alle Exitcodes 0). Die erste Solution meldet 85 Debug- beziehungsweise 91 Release-Warnungen, die zweite jeweils null; überall null Buildfehler. Die Meldungen bleiben vollständig erhalten. Jede der vier Testsuiten verwendet die vorhandenen passenden Locks. Der zusätzliche unabhängige kritische G1-Testprozess besteht je Konfiguration mit 16/16; diese Wiederholung erhöht das Testinventar nicht.

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|
| GUI | 1.118 | 1.118 | 0 | 0 |
| MCP | 176 | 176 | 0 | 13 Explicit |
| Core | 11.723 | 11.723 | 0 | 26 Skip + 1 Explicit |
| G1-Headless | 12 | 12 | 0 | 0 |

Die Zahlen stammen aus den neuen TRX-Einzelresultaten. [Vollständiges Inventar](../../artifacts/g1-i01/test-inventory.json), [Vergleich gegen beide Ausgangsstände](../../artifacts/g1-i01/regression-summary.json), [214 L1-Fälle](../../artifacts/g1-i01/l1-regressions.json) und [sieben Kombinationstests](../../artifacts/g1-i01/composition-tests.json) enthalten Namen, Ergebnisse und Skipgründe. Der Vergleich bildet die Vereinigung der tatsächlichen A06- und lokalen L1-I1-Testidentitäten; nur bekannte dynamische UTC-Anzeigenamen in MCP-Contract-Argumenten werden normalisiert.

Der tatsächliche Vergleich bestätigt **keine entfernten Fälle, keine geänderten bisherigen Ergebnisse und keine neuen Skips**. Alle **214 L1-Fälle pro Konfiguration** bestehen: 211 Core- und drei GUI-Fälle. Die sieben neuen Kombinationstests bestehen ebenfalls in beiden Konfigurationen. Die vereinigte GUI-Baseline umfasst 1.111 Fälle, zuzüglich dieser sieben ergibt sie 1.118. Die Core-Vereinigung enthält tatsächlich 11.723 bestandene Fälle; diese Übereinstimmung mit dem A06-Planwert wird aus den TRX belegt und nicht aus der Rechnung als PASS abgeleitet. Die unveränderten 40 NotExecuted-Fälle werden nicht als bestanden gezählt.

Reproduktion mit neuen Outputverzeichnissen: [run-regression.ps1](../../artifacts/g1-i01/run-regression.ps1), vorhandener `Validate-G1NativeAcceptance.ps1` und [run-additional.ps1](../../artifacts/g1-i01/run-additional.ps1). Jeder Locked-Restore und Testprozess hat seinen eigenen erhaltenen Log-/TRX-Nachweis. Provozierte Dateisperr-/Startfehler innerhalb erfolgreicher Vertragstests sind erwartete Assertions, keine unterdrückten Fehler.

## 5. Native G1-Windows-Matrix am kombinierten Stand

**72/72 G1-Bestandsprozesse bestehen erneut** (36 je Konfiguration), Exit 0, kein Timeout, insgesamt 2.738 bestandene Checks. Zusätzlich bestehen sechs Legacy-/Startfehlerprozesse mit 56 Checks. Damit umfasst die native Vertragsmatrix **78 bestandene Prozesse und 2.794 Checks**, ohne fehlgeschlagene Assertion. Die Haupt-Ergebnisdateien und ihre PID-/Exitcode-Zuordnung sind in [native-results.json](../../artifacts/g1-i01/native-results.json) inventarisiert. Die zusätzlichen Produkt-CLI-Startsmokes in Abschnitt 6 werden separat gezählt.

Die unveränderte Matrix umfasst Dokument-/Tool-/Gruppen-Redock, verschachtelte Strukturen, zweite Floating-Hosts, Rootrollen, Maximize/Restore, native Editor-/MCP-Integration, Layoutsave/Restart/Invalid sowie aktuelle, Legacy- und echte historische Schema-1-Dateien. A04-Operation-/Partial-Failure-Prozesse prüfen LayoutFaulted, gesperrte Persistenz, unabhängiges fachliches Speichern und bereinigte Quellen. Die A05-Acceptance-Probe prüft reale HWNDs, Fenster-Chrome, Selection/Clipboard, Completion/Insight, Popup-Retirement und identische Editor-/Dokument-/Undoobjekte.

Alle Ergebnisse unterscheiden reale native Hosts mit programmgesteuerten Layoutaktionen von externer Maus-/Tastatureingabe. Es wurde in I01 keine vollständige externe Mausabnahme durchgeführt. Die sieben früheren Eigentümer-PASS gelten ausschließlich für den G1-Ausgangsstand. Sie sind kein automatischer PASS des kombinierten Releases.

## 6. Tatsächlicher Desktop-Einstieg, Legacy und Publish

Die tatsächliche `CryptoWorkBenchAvalonia.Desktop.exe` wurde in Debug und Release je einmal ohne Argumente und mit `--dock-shell --dock-native-trace <lokaler Pfad>` gestartet. Alle **vier sichtbaren Starts** bestehen: Legacy-Titel `CryptoWorkBenchAvalonia`, Dock-Titel `CryptoWorkBench — Dock preview`, jeweils ein tatsächlicher Haupt-HWND. Die Dock-Traces zeigen den nativen sichtbaren DockShellWindow und `canRelocate: true`. [Prozess-/Argumentnachweis](../../artifacts/g1-i01/desktop-start-visible/processes.json) und [Hashes von Apphost und verwalteten Produktassemblies](../../artifacts/g1-i01/desktop-binary-hashes.json) binden die Starts an den gebauten Stand. Diese begrenzten Startsmokes beenden ausschließlich ihre eigenen Prozesse nach der Fensterbeobachtung; sie sind ausdrücklich **kein kontrollierter Shutdown-Nachweis**. Letzterer stammt aus der getrennten nativen Vertragsmatrix. Die Benutzerlayoutdatei blieb dabei bytegleich. Ein anfänglicher Legacy-Debug-Start mit `WindowStyle Hidden` erhielt innerhalb von 30 Sekunden keinen MainWindowHandle; sein Befund bleibt unter `desktop-start` erhalten. Derselbe unveränderte Fenster-Assert besteht nach sichtbarem GUI-Start in allen vier Fällen. Die Änderung betrifft den Startwrapper, nicht das Produkt.

Der kombinierte Release-Stand wurde zusätzlich mit den bestehenden `win-x64`-RID-Locks veröffentlicht. Restore, Publish und vorhandener Publish-Validator enden mit Exit 0; Apphost, verwaltete Abhängigkeiten, fünf native Assets und 133 InfoDocs-Dateien sind bestätigt. Ausgabe: `artifacts/g1-i01/publish/win-x64`. Die interaktiven Produktstarts oben stammen aus `bin/<Configuration>/net10.0`; ein Start aus dem Publishverzeichnis wird nicht behauptet.

Legacy bleibt ohne Schalter Standard. Die vorhandenen nativen `legacy`, `fallback` und `startup-failure`-Probes prüfen zusätzlich strukturellen Fallback vor Editorstart und kontrolliertes Beenden eines produktiven Startfehlers ohne Ersatzeditor. Es wurde kein zweiter Startup- oder Dockingmechanismus implementiert.

## 7. L1-, Editor- und MCP-Verträge

Alle bisherigen L1-Fälle werden anhand ihrer neuen Einzelergebnisse nachgewiesen, einschließlich Deklaration/Zuweisung, Typverträglichkeit, atomarer Übernahme, PARAM-Serialisierungsgrenze, Fehlerredaktion und S-01-Fortsetzung. Bei fehlgeschlagener RHS bleibt das bisherige Zielobjekt unverändert; erfolgreiche Zuweisungen erreichen die globale VariableDictionary-Instanz. G1 stellt den bestehenden Zustand dar und führt keinen parallelen Sprachzustand ein.

Die Kombination nutzt denselben `AntlrToFunctionCallExpression`-Konverter mit optionalem SourcePosition-Feld; vorhandene L1-AST-Konstruktoren bleiben kompatibel. `WorkbenchExecutionPipeline` sammelt strukturierte Print-Ereignisse über den bestehenden Runner/InterpreterHostQueue. G1 veröffentlicht nur Print-Ausgaben, während Info und Diagnostics ihre eigenen Darstellungen behalten. Die Diagnostics-Kategorie für L1-Fehler ist SemanticError; fehlende Tokenkoordinaten werden als unbekannt behandelt. Vorhandene Syntax-/Output-Navigation, Stale-Abwehr und native Quellnavigation bleiben Bestandteil der vollständigen G1-Suiten.

`DockEditorSession.CreatePresenter` verwendet dieselbe View. Coordinator, EditorBridge, WorkbenchLeaseV2 und InterpreterHostQueue wurden nicht angepasst. Pending/Busy/Modal-/Lease-Gates, Editor-ID, Dokument-/UndoStack-Identität, SaveIntent und Request-Deduplikation bleiben geprüft. Layoutwechsel erzeugen keine zweite Editorregistrierung und keine zusätzliche Script-Ausführung. Kontrollierter Shutdown und Drain werden in den nativen Probes mit offenen Hosts geprüft; ihre erfolgreiche Ressourcenfreigabe ist von den unten ausgewiesenen Startsmokes getrennt.

Die vorhandenen installierten-Codex-Explicit-Tests wurden zusätzlich gegen **codex-cli 0.167.0-alpha.3.2** und den kombinierten Desktopadapter ausgeführt: **11/11 PASS je Debug und Release**, null Skip/Fehler. Discovery/Status, TLS-/Pin-/Portübernahme-, Identitätswechsel- und Replay-Verträge bleiben bestanden. [Einzelfälle](../../artifacts/g1-i01/codex-results.json). Diese Ergebnisse werden separat von den regulären 176 MCP-Fällen gezählt. Zwei gepaarte Desktop-AES-Fälle bleiben mangels vollständiger Wegwerf-Desktop-/lokaler Freigabeumgebung NOT VERIFIED.

## 8. Erneute Eigentümer-Mausmatrix für Release

**Alle Zeilen derzeit NOT VERIFIED für den kombinierten Stand.** Auszuführen am Release-Binary dieser Arbeitskopie beziehungsweise dem hieraus erzeugten geprüften Publish, mit `--dock-shell`. Vorher Datum, Prüfer, Windows-/Bildschirm-/Skalierungsdaten und Binary-/Quellmanifest erfassen. Hauptfenster normal bedienen, Testdaten verwenden und am Ende kontrolliert schließen.

Start aus dem isolierten Worktree:

```powershell
& .\CryptoWorkBenchAvalonia.Desktop\bin\Release\net10.0\CryptoWorkBenchAvalonia.Desktop.exe --dock-shell
```

| Nr. | Erneuter Ablauf | Erwartetes Ergebnis | Eigentümer |
|---|---|---|---|
| M1 | Einzelnes Tool per Dock-Tab/-Grip floaten und zurückziehen. | Zulässige Ziele sichtbar, Inhalt erhalten, leerer Quellhost vollständig verschwunden. | NOT VERIFIED |
| M2 | Vollständige Tool-Gruppe floaten und redocken. | Alle Pane-Identitäten/Tabreihenfolge erhalten; reine Tool-Chrome; keine leeren Hosts. | NOT VERIFIED |
| M3 | CryptoScript-Dokument floaten/redocken, Maximize Active View und Restore Layout. | Derselbe Text/UndoStack; Docking im Fokusmodus gesperrt, nach Restore wieder möglich; Completion/Insight funktionieren. | NOT VERIFIED |
| M4 | DocumentDock seitlich andocken. | Richtige Dokumentrolle, Editor weiterhin nutzbar, keine doppelte Editoransicht. | NOT VERIFIED |
| M5 | Verschachtelte Gruppen erstellen, floaten und zurückdocken. | Vollständiger Gruppeninhalt und gültige Auswahl; leere dynamische Container bereinigt. | NOT VERIFIED |
| M6 | Zwei Floating Windows gleichzeitig verwenden und eines redocken. | Das andere bleibt sichtbar, belegbar und unverändert; richtige Chrome beider Hosts. | NOT VERIFIED |
| M7 | Layout kontrolliert speichern/schließen, denselben Release-Desktop neu starten. | Normaler Layoutzustand einschließlich geschlossener Tools wiederhergestellt; kein fachlicher Variablen-/Editorinhalt aus Layout; alle Hosts nach Shutdown beendet. | NOT VERIFIED |

Zusätzlich im Haupt- und Floating-Editor `VAR ownerValue=0x(11) ownerValue=0x(22) Print(ownerValue)` mit Enter ausführen: globale Variables und genau `out: 0x(22)`, richtige Quellnavigation, Undo/Redo und History ohne erneute Ausführung. Die bekannten Regeln für Fehlerfortsetzung bleiben unverändert. **Zum Zurückdocken einer maximierten View zuerst `Restore Layout` ausführen.** Diese kompakte Matrix ist vorbereitet, nicht vom Agenten als Eigentümer-PASS ausgefüllt.

## 9. Risiken, Befunde und Plattformgrenzen

| Befund | Klassifikation / Ergebnis |
|---|---|
| Textuelle G1/L1-Komposition | Keine Konflikte; alle akzeptierten Produkt-/Testdateien übertragen und L1 vollständig erhalten. |
| Neue Testentwicklung | Ein Compilefehler und vier fehlerhafte neue Fixture-Erwartungen nachvollziehbar korrigiert; rote Evidence bleibt erhalten. Kein daraus abgeleiteter Produktfehler. |
| Kombinierte Mausabnahme | Offen; Matrix in Abschnitt 8. Alte G1-PASS werden nicht übertragen. |
| Linux x64, macOS x64/ARM64 | NOT VERIFIED für diesen kombinierten Stand. Keine fremden CI-/Windows-Ergebnisse als Plattformfreigabe. |
| Produktive Nicht-Windows-MCP-Lease | Unveränderte bekannte Windows-GetTickCount64-Grenze; gesonderte Plattformuhr-/Sicherheitsfreigabe erforderlich. `WorkbenchLeaseV2` bleibt bytegleich. |
| Gepaarte Codex-Desktop-AES-Prüfungen | Zwei Explicit-Fälle benötigen einen geeigneten gepaarten Wegwerf-Desktop mit lokaler Ausführungsfreigabe und Disconnect; keine künstliche PASS-Aussage. |
| Langzeit-/Plattform-Lifecycle | Native Censuses belegen die ausgeführten Windows-Zyklen; keine allgemeine Leakfreiheit oder OS-übergreifende Layouttransfergarantie. |

Kein neuer relevanter Produktfehler oder notwendiger Integrationspatch nachgewiesen. Zwei Infrastrukturannahmen werden ausdrücklich erhalten: Rohhashvergleich unterschiedlicher Git-EOL-Checkouts (451 ausschließlich LF/CRLF-Abweichungen) und der anfänglich versteckte GUI-Start. Beide wurden durch passende Prüfvoraussetzungen geklärt; Assertions und Produktzustände wurden nicht zum Erreichen eines PASS verändert.

## 10. Integrität und vollständiger Git-Status

Der abschließende Vergleich bestätigt: **1.151/1.151 Original-G1-Dateien bytegleich**, dessen vollständiger Git-Status unverändert; **15.831/15.831 historische Raw-Dateien und 2.004/2.004 A06-Artefakte bytegleich**. Alle **152 übertragenen G1-Dateien** sind weiterhin exakt wie im gesicherten Snapshot. Alle **1.018 übrigen Masterdateien**, darunter sämtliche **35 L1-Pfade**, sind in der neuen Arbeitskopie unverändert. Der originale Master bleibt sauber und auf demselben Commit. Seine 1.055 Dateien wurden gegen einen eigenen Rohbyte-Hashbestand geprüft: Der erste Vergleich mit dem frischen Checkout hatte 451 ausschließlich LF/CRLF-bedingte Unterschiede gezeigt; [EOL-Nachweis](../../artifacts/g1-i01/master-checkout-eol-comparison.json) und der erhaltene erste Vergleich dokumentieren dies. Es wurde keine Zeilenendenkorrektur im Original vorgenommen. [integrity.json](../../artifacts/g1-i01/integrity.json) enthält die vollständige Prüfung. `git diff --check` besteht; die drei neuen I01-Dateien werden zusätzlich per `--no-index --check` ohne Whitespace-Diagnose geprüft. Der finale Status hat 155 Zeilen: 37 modifizierte und 118 ungetrackte Dateien (152 übernommene G1-Pfade + drei I01-Ergänzungen). Die Referenzen bleiben seit Anlage des einzigen neuen Zweigs unverändert.

Die einzige neue permanente Codeergänzung gegenüber den unverändert zusammengesetzten Ausgangsständen ist `CryptoWorkBenchGuiTest/DockAssignmentCompositionTests.cs`. Dazu kommen dieser Bericht und seine Ergebnisdatei. Neue Scripts/Logs/Baselinekopien/TRX/Publishes befinden sich ausschließlich im ignorierten I01-Artefaktbereich. Kein Produktcodefix, Paketupgrade, Dock-Patch, neuer Plattformclock-Vertrag oder neue Dock-Architekturfunktion. **NO REACHABLE B4 FOUND** bleibt unberührt.

Vollständiger finaler Status der neuen Arbeitskopie:

```text
 M CryptoScript/Model/AntlrToFunctionCallExpression.cs
 M CryptoScript/Model/Ast/FunctionCallExpressionNode.cs
 M CryptoScript/Model/FunctionCall.cs
 M CryptoScript/Model/FunctionCallEvaluator.cs
 M CryptoScript/Model/OperationInvocation.cs
 M CryptoScript/Operations/OutputOperations.cs
 M CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs
 M CryptoWorkBenchAvalonia.Desktop/Program.cs
 M CryptoWorkBenchAvalonia/App.axaml.cs
 M CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs
 M CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs
 M CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj
 M CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs
 M CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs
 M CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs
 M CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs
 M CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs
 M CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj
 M CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs
 M CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs
 M eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json
 M eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json
 M eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.lock.json
?? CryptoScriptUnitTest/PrintSourceTests.cs
?? CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs
?? CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs
?? CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs
?? CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs
?? CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs
?? CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs
?? CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs
?? CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs
?? CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs
?? CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs
?? CryptoWorkBenchAvalonia/Shell/DockOutputService.cs
?? CryptoWorkBenchAvalonia/Shell/DockOutputView.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellState.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml
?? CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs
?? CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs
?? CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs
?? CryptoWorkBenchAvalonia/Shell/DockWindowGeometry.cs
?? CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs
?? CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs
?? CryptoWorkBenchGuiTest/DockAssignmentCompositionTests.cs
?? CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs
?? CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockEditorTests.cs
?? CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs
?? CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs
?? CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs
?? CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs
?? CryptoWorkBenchGuiTest/DockLayoutTests.cs
?? CryptoWorkBenchGuiTest/DockMouseRedockTests.cs
?? CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs
?? CryptoWorkBenchGuiTest/DockNativeChromeTests.cs
?? CryptoWorkBenchGuiTest/DockOperationContractTests.cs
?? CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockOutputTests.cs
?? CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockPartialFailureTests.cs
?? CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs
?? CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockRedockNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockShellNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockShellTests.cs
?? CryptoWorkBenchGuiTest/DockToolNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockToolTests.cs
?? CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs
?? CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json
?? docs/gui/G1-01-dock-feasibility.md
?? docs/gui/G1-02-shell-implementation.md
?? docs/gui/G1-03-editor-mcp-integration.md
?? docs/gui/G1-04-variables-info-integration.md
?? docs/gui/G1-05-output-pipeline.md
?? docs/gui/G1-06-diagnostics-navigation.md
?? docs/gui/G1-07-layout-persistence.md
?? docs/gui/G1-08-final-validation.md
?? docs/gui/G1-08-native-acceptance-checklist.md
?? docs/gui/G1-A01-docking-architecture-review.md
?? docs/gui/G1-A02-maximize-layout-roles.md
?? docs/gui/G1-A03-persistence-cleanup-roles.md
?? docs/gui/G1-A04-R1-actual-callback-audit.md
?? docs/gui/G1-A04-R2-test-cleanup.md
?? docs/gui/G1-A04-R3-partial-failure-audit.md
?? docs/gui/G1-A04-R4-partial-failure-correction.md
?? docs/gui/G1-A04-dock-operation-contract.md
?? docs/gui/G1-A05-M1-final-owner-acceptance.md
?? docs/gui/G1-A05-native-chrome-input-acceptance.md
?? docs/gui/G1-A06-cross-platform-final-validation.md
?? docs/gui/G1-I01-master-L1-composition.md
?? docs/gui/G1-N1-F01-F2-native-window-investigation.md
?? docs/gui/G1-N1-F01-mouse-redock-correction.md
?? docs/gui/G1-N1-F03-group-document-redock.md
?? docs/gui/evidence/G1-01-results.json
?? docs/gui/evidence/G1-02-results.json
?? docs/gui/evidence/G1-03-results.json
?? docs/gui/evidence/G1-04-results.json
?? docs/gui/evidence/G1-05-results.json
?? docs/gui/evidence/G1-06-results.json
?? docs/gui/evidence/G1-07-results.json
?? docs/gui/evidence/G1-08-results.json
?? docs/gui/evidence/G1-A02-results.json
?? docs/gui/evidence/G1-A03-results.json
?? docs/gui/evidence/G1-A04-R4-results.json
?? docs/gui/evidence/G1-A04-results.json
?? docs/gui/evidence/G1-A05-results.json
?? docs/gui/evidence/G1-A06-results.json
?? docs/gui/evidence/G1-I01-results.json
?? docs/gui/evidence/G1-N1-F01-F2-results.json
?? docs/gui/evidence/G1-N1-F01-results.json
?? docs/gui/evidence/G1-N1-F03-results.json
?? eng/gui/Test-DockNativeTrace.ps1
?? eng/gui/Validate-G1LayoutRoles.ps1
?? eng/gui/Validate-G1NativeAcceptance.ps1
?? eng/gui/Validate-G1Platform.ps1
?? eng/gui/Validate-G1Shell.ps1
?? eng/gui/native/G1ShellProbe.csproj
?? eng/gui/native/Program.cs
?? eng/gui/native/packages.lock.json
?? prototypes/G1DockFeasibility/G1DockFeasibility.csproj
?? prototypes/G1DockFeasibility/NativeProbe.cs
?? prototypes/G1DockFeasibility/ProbeWorkspace.cs
?? prototypes/G1DockFeasibility/Program.cs
?? prototypes/G1DockFeasibility/README.md
?? prototypes/G1DockFeasibility/Session.cs
?? prototypes/G1DockFeasibility/Tests/DockTests.cs
?? prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj
?? prototypes/G1DockFeasibility/Tests/packages.lock.json
?? prototypes/G1DockFeasibility/Validate.ps1
?? prototypes/G1DockFeasibility/Views.cs
?? prototypes/G1DockFeasibility/packages.lock.json
```

## 11. Empfohlene spätere Versionierungs-/Integrationsreihenfolge

1. I01 zur Eigentümerabnahme vorlegen und die sieben Release-Mausabläufe am exakt inventarisierten kombinierten Stand wiederholen. Plattform-/MCP-Gates separat offen führen.
2. Nach gesonderter Freigabe aktuellen Master erneut auflösen und beide akzeptierten Ausgangsmanifeste sowie I01-Testerweiterung prüfen. Bei geändertem Master die Komposition und ihre Inventarprüfung neu ausführen.
3. Die vollständige G1-Änderungsmenge einschließlich bisher ungetrackter Produktdateien, Tests, Projekte, Ressourcen und Locks regulär versionieren. Für eine gut prüfbare Historie bietet sich ein eigener G1-Kompositionscommit auf Master/L1 und ein getrennter Commit der sieben neuen Integrationstests mit Bericht an. Historische Raw-Artefakte gehören nicht in den Produktcommit.
4. Den tatsächlich versionierten kombinierten Stand erneut durch Debug-/Release- und native Windows-Gates sowie die ausstehenden tatsächlichen Zielplattformprüfungen führen. Zu jedem CI-Nachweis Commit/Manifest und Prozessarchitektur erfassen. Die jetzigen uncommitteten Belege zertifizieren keinen später abweichenden Commit.
5. Erst nach ausdrücklicher Freigabe einen normalen PR-/Integrationsschritt vorbereiten; L1 vollständig erhalten, Legacy weiterhin Standard und G1 über `--dock-shell`. Kein automatischer Merge aus diesem Bericht.

## 12. Abschlussentscheidung

**CONDITIONAL GO**. Die Windows-Komposition ist technisch bestanden: konfliktfreie und hashgesicherte Übernahme, beide Solutions, vollständige Regression einschließlich aller L1-Fälle und sieben neuer Kombinationstests, native G1-Verträge, tatsächliche Desktopstarts und Windows-Publish. Offen bleiben die erneute Eigentümer-Mausabnahme des kombinierten Releases, Linux-/macOS-Nativnachweise und die unveränderten gesonderten MCP-Plattform-/Explicit-Voraussetzungen. Diese Grenzen verhindern eine uneingeschränkte endgültige Integrationserlaubnis.

I01 endet hier. Keine Commits, Pushes, Merges, Rebases oder endgültige Master-Integration durchgeführt. Der neue Worktree bleibt für Review und die erneute Eigentümerabnahme erhalten.
