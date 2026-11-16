# G2-02-C1 – Git Dependency and Commit Boundary Audit

**READY FOR OWNER DECISION**

Stand: 12.11.2026. Empfehlung: **Variante A – vollständige G1-Baseline, danach G2-02 einschliesslich R1/R2/F1**. Die vollständige lokale G1-A06-Quellensicherung macht die Grenze rekonstruierbar. Die Freigabe dieser Strategie und ihrer Ausführung liegt beim Projekteigentümer. Dieses Audit führt keine Commits, Indexänderungen oder Rekonstruktion aus.

## 1. Verifizierter Git-Ausgangszustand

| Merkmal | Ergebnis |
| --- | --- |
| Worktree | `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1` |
| Branch | `feature/gui-g1` |
| HEAD | `7ecac04483c10722c4cb4b459e3f6cb5157c6af1` |
| Merge-Base mit master | `7ecac04483c10722c4cb4b459e3f6cb5157c6af1` |
| master | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| HEAD...master | 0 GUI-Commits exklusiv, 4 master-Commits exklusiv |
| Index | Keine gestagten Änderungen |
| Arbeitskopie vor C1 | 38 getrackte Änderungen, 124 untracked Dateien, keine getrackten Löschungen; 162 Pfade |
| Diff der getrackten Dateien | 1.809 Einfügungen, 58 Löschungen; untracked Inhalte darin nicht enthalten |
| Vollständiges nicht ignoriertes Inventar | 1.160 Dateien vor C1 |
| Hauptcheckout | master, sauber; nicht bearbeitet |

Der GUI-HEAD ist der Commit „Remove obsolete asymmetric CryptoScript infrastructure“. Auf master folgen `9a71065` (atomare Zuweisungen), `a34ffbd` (Assignment-Abnahme), `50e8612` (Merge PR #5) und `fbbadae` (verifizierte L1-Integration). Diese L1-Änderungen gehören **nicht** zur G1/G2-Baseline. Eine spätere Integration mit master ist eine eigene Aufgabe mit neuen Integrationstests; weder A noch B soll sie stillschweigend einschliessen. Weitere registrierte Worktrees wurden nur über die Git-Worktree-Liste identifiziert, nicht bearbeitet.

Die vier angeforderten G2-Berichte wurden als historische Quellen herangezogen. F1 ist die abschliessende Eigentümerabnahme; die früheren Statusangaben werden nicht rückwirkend umgeschrieben.

## 2. Sicherungen und belastbare Commit-Grenze

### Vollständige G1-Sicherung

`artifacts/g1-a06/baseline/source-manifest.json` enthält 1.149 Pfade. **Alle 1.149 gesicherten Dateien** unter `artifacts/g1-a06/baseline/source/` wurden gegen ihre SHA-256-Werte geprüft: keine fehlende Datei, keine Abweichung.

Manifest-SHA-256:

`B69E8940396E0E7BADC9768419A9305D237EB1BDF42C71D8F95F985EE158D0A7`

Das A06-Ergebnis nennt denselben Hash und HEAD. Der damalige Ausgangszustand enthielt 37 getrackte Änderungen und 113 untracked Dateien. A06 ergänzte anschliessend ausschliesslich:

- `docs/gui/G1-A06-cross-platform-final-validation.md`
- `docs/gui/evidence/G1-A06-results.json`

Diese zwei aktuellen Dateien gehören daher ebenfalls zum vollständigen G1-Abschlussstand, obwohl sie noch nicht im vor A06 angelegten Manifest stehen. Ihre Herkunft wird durch die A06-Dokumentation und deren `newDeliverables`-Liste belegt; hierfür besteht kein Hashnachweis aus dem früheren Manifest.

### G2-Ausgangssicherung

`artifacts/g2-02/baseline/gui.zip` ist keine vollständige Repository-Sicherung. Sie enthält Shell, Editor-View und GUI-Tests, ausserdem lokale bin/obj-Ausgaben. Nach Ausschluss der Build-Verzeichnisse wurden **101 Dateien** mit der vollständigen A06-Sicherung verglichen: alle identisch. ZIP-Pfade `Shell/...` wurden auf `CryptoWorkBenchAvalonia/Shell/...`, die zwei einzelnen Editor-View-Dateien auf `CryptoWorkBenchAvalonia/Views/...` abgebildet.

`artifacts/g2-02/baseline/tracked.diff` und `artifacts/g1-a06/baseline/tracked.patch` sind bytegleich, SHA-256:

`48C56A7D2B3FA4B7B8C9F3D10846757DA7B58CBC6E0BFE37B9084B42939BCE11`

Die G2-Statusdatei mit zusammengefassten untracked Verzeichnissen reicht allein nicht für eine Rekonstruktion. Die A06-Sicherung schliesst diese Lücke, insbesondere für `eng/gui/native/Program.cs`, sämtliche Core-Ergänzungen, Skripte, Locks und Prototypdateien.

### Vergleich mit dem aktuellen Stand

Der Hashvergleich aller A06-Manifestpfade mit der aktuellen Arbeitskopie ergibt exakt **29 geänderte und eine entfernte Datei**. Ausser den zwei A06-Abschlussdateien gibt es **zehn neue G2-Dateien**. Dies bestätigt das F1-Inventar mit 40 G2-Pfaden unabhängig durch den vollständigen Quellvergleich.

Von den 29 geänderten Dateien sind **28 echte G1/G2-Überlappungen**: Sie wurden in G1 neu angelegt und in G2 weiterentwickelt, sind aber bis heute untracked. Die übrige Datei `CryptoScriptEditView.axaml` existiert bereits im HEAD und war in der G1-Baseline unverändert; ihre Git-Änderung gehört vollständig zu G2. Der zugehörige Code-behind wurde dagegen bereits in G1 geändert und in G2 nicht weiter geändert.

Die entfernte `DockDocumentWindowChrome.cs` war ebenfalls nur untracked. Deshalb zeigt Git heute kein `D`. Für Variante A muss sie im G1-Commit enthalten sein und im G2-Commit gelöscht werden. In Variante B erscheint sie in keinem neuen Git-Tree; ihre historische Existenz bleibt nur über Sicherung und Dokumentation nachvollziehbar.

## 3. Inhaltliche Zuordnung und Abhängigkeiten

| Art | Befund und Konsequenz |
| --- | --- |
| Git-Abhängigkeit | HEAD enthält die G1-Shell nicht. Ein Commit nur aus den momentan getrackten Änderungen oder nur aus den zehn neuen G2-Dateien wäre unvollständig. Untracked ist kein Synonym für G2. |
| Build-Abhängigkeit | G1 ergänzt Dock-Paketreferenzen, Shell-Klassen, Desktop-Startintegration und konfigurations-/RID-spezifische Locks. G2 verwendet und verändert diese Klassen. Ohne vollständige G1-Dateien ist G2 nicht eigenständig kompilierbar. |
| Funktionale Abhängigkeit | G1 liefert Editor-Sitzung, Dock-Identitäten, Operation-/Admission-Regeln, Layoutpersistenz, Output, Diagnostics, Variables/Info und MCP-Koordination. G2 verändert Layout und Präsentation auf dieser Grundlage. |
| Core-/Output-Abhängigkeit | G1 führt Print-Quellenpositionen durch AST, FunctionCall und OperationInvocation bis OutputOperations. Die ExecutionPipeline verarbeitet PrintResult mit Quelle und Ausführungsidentität; Output/Navigation setzt dies voraus. Diese Core-Dateien sind G1-Bestandteile, keine fremde L1-Assignment-Übernahme. |
| Test-Abhängigkeit | G1-Tests prüfen die damalige Anordnung und Fenster-Chrome. G2 aktualisiert entsprechende Erwartungen und native Proben. G2-Tests dürfen nicht mit G1-Produktdateien kombiniert werden. |
| Native Probe | `eng/gui/native/G1ShellProbe.csproj` referenziert Desktop und GUI-Testprojekt. Der G1-Launcher ist vollständig gesichert. Die G2-Modi für Standardlayout, Korrekturen und unabhängige Fenster gehören zusammen mit den jeweiligen Probe-Dateien in Commit 2. |
| Dokumentation | G1-Berichte/Evidenz einschliesslich A06 dokumentieren den G1-Stand; die vier G2-Berichte dokumentieren den weiterentwickelten Stand. C1 beschreibt nur die Commit-Grenze und sollte separat dokumentiert werden. |

**Kann G1 ohne G2 gebaut werden?** Die gesicherte G1-Version ist vollständig rekonstruierbar und besitzt erfolgreiche historische Windows-Debug-/Release-Nachweise. Der Vergleich zeigt keine notwendige G2-Datei in dieser Version. Ein neuer Build einer aus der vorgeschlagenen Commit-Grenze erzeugten sauberen Arbeitskopie wurde in C1 bewusst nicht ausgeführt. Historisch baubar ist belegt; die frische Rekonstruktionsprüfung ist noch offen.

**Build-Voraussetzungen:** SDK 10.0.401 aus `global.json`, PowerShell 7, NuGet-Restore aus dem konfigurierten nuget.org-Feed und die vorhandene offizielle Prism-Quellintegration. `eng/prism/Build.ps1` bereitet die Umgebung vor und baut beide Solutions mit Locked Restore. Prism wird auf `358118cd640d9a22ff8cf21c8ad197fa038b7990` vorbereitet; `.deps/prism` ist bewusst ignoriert und kein mitzucommittender lokaler Quellstand. Die Dock-Pakete sind 12.1.0.13, Avalonia 12.1.4, AvaloniaEdit 12.0.0. Der lokale Dock-Reviewcheckout unter artifacts ist Analysegrundlage, kein ProjectReference. Keine Versionsänderung ist für die Commit-Aufteilung erforderlich.

Die 14 geänderten Prism-Lockdateien, die nativen/prototypischen `packages.lock.json` und das alte Schema-1-Testfixture sind notwendige beziehungsweise bewusst versionierbare Eingaben. Sie dürfen nicht zusammen mit Build-Artefakten ausgefiltert werden. Prototypen sind keine Produktionsabhängigkeit, gehören aber zur dokumentierten G1-Entwicklungs- und Prüfgeschichte.

## 4. Bewertung der Varianten

| Kriterium | A – G1, dann G2 | B – gemeinsamer Stand |
| --- | --- | --- |
| Vollständigkeit | Mit verifiziertem A06-Snapshot plus zwei A06-Berichten herstellbar | Mit vollständigem aktuellem Inventar unmittelbar herstellbar |
| Historie | G1-Architektur und G2-Präsentationsänderungen getrennt reviewbar | Nur der gemeinsame Endzustand ist als Commit verfügbar |
| Überlappungen | 28 Dateien benötigen alte und neue Fassungen; alte Chrome-Klasse zunächst hinzufügen, danach entfernen | Nur aktuelle Fassungen, alte Chrome-Klasse fehlt |
| Tests | G1-Nachweise gehören zu Commit 1, G2-Nachweise zu Commit 2 | G2-Nachweise gelten für den Gesamtstand; G1 nur historisch |
| Technisches Risiko | Rekonstruktions-/Zuordnungsfehler; durch Vollmanifest und Tree-Vergleich kontrollierbar | Geringeres Rekonstruktionsrisiko, aber schlechtere Änderungszuordnung |
| Revert | G2 kann nach geprüfter Reihenfolge auf die G1-Baseline zurückgeführt werden | Revert entfernt G1 und G2 gemeinsam |
| Freigabe | Beide resultierenden Stände frisch prüfen | Gemeinsamen resultierenden Stand frisch prüfen |

**Empfehlung A.** Die anfängliche Unsicherheit einer unvollständigen G2-Sicherung ist durch die verifizierte vollständige A06-Sicherung ausgeräumt. Es gibt keinen belegten technischen Zwang für B. B bleibt eine vertretbare Eigentümerentscheidung, falls eine kompakte gemeinsame Historie ausdrücklich wichtiger ist als getrennte Review- und Rücknahmemöglichkeiten. Variante C ist nicht erforderlich; insbesondere kein ungeprüfter Import des neueren master und kein nachträgliches Vermischen von G1-Quellen mit G2-Tests.

## 5. Geplante Commit-Reihenfolge – erst nach Freigabe

1. **G1: vollständige Dock-Shell-Baseline einschliesslich A06.** Auf dem jetzigen HEAD aufbauen. 37 bereits in G1 geänderte getrackte Dateien und 115 G1-Neudateien einschliesslich der heute entfernten Chrome-Klasse: insgesamt 152 geänderte/neue Pfade. Für G1/G2-Überlappungen die A06-Version verwenden. Die zwei nach dem Snapshot entstandenen A06-Abschlussdateien aus dem aktuellen Stand ergänzen. G2-Neudateien ausschliessen; Editor-XAML bleibt auf HEAD-Stand.
2. **G2-02: Standardlayout, R1 und R2 mit Final Acceptance.** Die exakt 40 F1-Pfade anwenden: 29 Änderungen, zehn Ergänzungen, eine Löschung. Der resultierende Quellstand muss dem vor C1 gehashten aktuellen Arbeitsstand entsprechen.
3. **Dokumentation: C1-Entscheidungsgrundlage.** Diesen Auditbericht separat hinzufügen; eine spätere Eigentümerentscheidung ausdrücklich ergänzen, ohne das Audit nachträglich als Ausführungsfreigabe darzustellen.

Vor einer Umsetzung nach Freigabe den aktuellen vollständigen Stand einschliesslich untracked Dateien erneut sichern und den Hashbestand prüfen. Die Rekonstruktion sollte isoliert erfolgen, ohne die bestehende GUI-Arbeitskopie zurückzusetzen. Für jeden Kandidaten: vollständigen Tree gegen das zugehörige Manifest prüfen, Locked Restore/Build Debug und Release sowie GUI/MCP/Core und passende native Proben ausführen. Erst diese Nachweise bestätigen die tatsächlich erzeugten Commit-Stände. Keine Befehle zur Umsetzung wurden in C1 ausgeführt.

Für Variante B würden alle 162 aktuell geänderten/untracked Pfade in ihrer aktuellen Fassung gemeinsam aufgenommen; die entfernte, nie getrackte Chrome-Datei bleibt ausgeschlossen. C1 kann auch dort ein separater Dokumentationscommit sein.

## 6. Vollständige Dateizuordnung

Inventar vor Erstellung dieses Berichts, ergänzt um die historische Löschung und C1. `M` = getrackt geändert, `??` = derzeit untracked. „1/2/3“ bezeichnet die oben vorgeschlagenen Commits. Bei Variante B werden alle vorhandenen Inventar-Dateien im aktuellen Zustand gemeinsam übernommen, mit C1 optional separat. Unveränderte HEAD-Dateien bleiben selbstverständlich Teil jedes Git-Trees.

| Datei | Git-Status | Zuordnung | Inhalt bei Variante A |
| --- | --- | --- | --- |
| `CryptoScript/Model/AntlrToFunctionCallExpression.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoScript/Model/Ast/FunctionCallExpressionNode.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoScript/Model/FunctionCall.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoScript/Model/FunctionCallEvaluator.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoScript/Model/OperationInvocation.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoScript/Operations/OutputOperations.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia.Desktop/Program.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/App.axaml.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml` | M | G2 auf HEAD-Datei | 1: HEAD-Inhalt; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.Desktop.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.Desktop.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.Desktop.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.Desktop.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.lock.json` | M | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoScriptUnitTest/PrintSourceTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockOutputService.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellState.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/DockWindowGeometry.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockEditorTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockIndependentWindowNativeProbe.cs` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `CryptoWorkBenchGuiTest/DockIndependentWindowTests.cs` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockLayoutTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockMouseRedockTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockNativeChromeTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockOperationContractTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockOutputTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockPartialFailureTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockRedockNativeProbe.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockShellTests.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `CryptoWorkBenchGuiTest/DockStandardLayoutTests.cs` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `CryptoWorkBenchGuiTest/DockToolNativeProbe.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `CryptoWorkBenchGuiTest/DockToolTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-01-dock-feasibility.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-02-shell-implementation.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-03-editor-mcp-integration.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-04-variables-info-integration.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-05-output-pipeline.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-06-diagnostics-navigation.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-07-layout-persistence.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-08-final-validation.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-08-native-acceptance-checklist.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A01-docking-architecture-review.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A02-maximize-layout-roles.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A03-persistence-cleanup-roles.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A04-R1-actual-callback-audit.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A04-R2-test-cleanup.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A04-R3-partial-failure-audit.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A04-R4-partial-failure-correction.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A04-dock-operation-contract.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A05-M1-final-owner-acceptance.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A05-native-chrome-input-acceptance.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-A06-cross-platform-final-validation.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-N1-F01-F2-native-window-investigation.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-N1-F01-mouse-redock-correction.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G1-N1-F03-group-document-redock.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/G2-02-R1-visual-corrections.md` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `docs/gui/G2-02-R2-floating-windows.md` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `docs/gui/G2-02-final-acceptance.md` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `docs/gui/G2-02-standard-layout.md` | ?? | G2 neu | 1: fehlt; 2: hinzufügen |
| `docs/gui/evidence/G1-01-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-02-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-03-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-04-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-05-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-06-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-07-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-08-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-A02-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-A03-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-A04-R4-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-A04-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-A05-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-A06-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-N1-F01-F2-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-N1-F01-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `docs/gui/evidence/G1-N1-F03-results.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/Test-DockNativeTrace.ps1` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/Validate-G1LayoutRoles.ps1` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/Validate-G1NativeAcceptance.ps1` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/Validate-G1Platform.ps1` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/Validate-G1Shell.ps1` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/native/G1ShellProbe.csproj` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `eng/gui/native/Program.cs` | ?? | G1 + G2 | 1: A06-Sicherung; 2: aktueller Inhalt |
| `eng/gui/native/packages.lock.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/G1DockFeasibility.csproj` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/NativeProbe.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/ProbeWorkspace.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Program.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/README.md` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Session.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Tests/DockTests.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Tests/packages.lock.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Validate.ps1` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/Views.cs` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `prototypes/G1DockFeasibility/packages.lock.json` | ?? | G1 | 1: aktueller Inhalt; 2: unverändert |
| `CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs` | fehlt, nie getrackt | G1 → G2 entfernt | 1: A06-Sicherung hinzufügen; 2: löschen |
| `docs/gui/G2-02-C1-git-dependency-audit.md` | neu durch C1 | Audit | 3: separat dokumentieren |

## 7. Test- und Abnahmestand: vorhandene Nachweise, keine neuen Läufe

Die vorhandenen TRX-Einzelresultate wurden in C1 eingelesen und nach outcome gezählt. Es wurden keine Tests gestartet.

| Stand | GUI Debug / Release | MCP Debug / Release | Core Debug / Release |
| --- | --- | --- | --- |
| G1-A06 | jeweils 1.108 Passed | jeweils 176 Passed, 13 NotExecuted | jeweils 11.512 Passed, 27 NotExecuted |
| G2-R2 final | jeweils 1.124 Passed | jeweils 176 Passed, 13 NotExecuted | jeweils 11.512 Passed, 27 NotExecuted |

G1-Rohdaten: `artifacts/g1-a06/regression/{CryptoWorkBenchGuiTest,CryptoWorkBench.Mcp.Tests,CryptoScriptUnitTest}-{Debug,Release}.trx`. A06 dokumentiert erfolgreiche Windows-Builds und einen CONDITIONAL GO für Integrationsvorbereitung, keine Linux-/macOS-Produktfreigabe.

G2-Rohdaten:

- `artifacts/g2-02-r2/final-Debug/gui.trx`
- `artifacts/g2-02-r2/final-r2-Release/gui.trx`
- `artifacts/g2-02-r2/validation-final/CryptoWorkBench.Mcp.Tests-{Debug,Release}.trx`
- `artifacts/g2-02-r2/validation-final/CryptoScriptUnitTest-{Debug,Release}.trx`

`artifacts/g2-02-r2/restart-final.json` wurde ebenfalls eingelesen: 49 Checks, 49 mit passed=true, failed=0. Kein separater nativer Release-Lauf wird daraus abgeleitet. Die sechs manuellen Windows-PASS-Angaben vom 12.11.2026 stammen vom Eigentümer und sind in F1 dokumentiert; C1 hat keine neue manuelle Prüfung vorgenommen.

Diese Ergebnisse gehören zum jeweiligen damaligen Arbeitsstand, nicht zu bereits existierenden G1/G2-Commits. Rohdaten unter artifacts sind lokal und werden durch die vorgeschlagenen Quellcommits nicht transportiert. Für eine übertragbare Nachweiskette sind sie vor einer späteren Bereinigung separat mit Manifest aufzubewahren; die Repository-Berichte und JSON-Zusammenfassungen ersetzen keine mitgelieferten Rohlogs.

## 8. Integrität, Risiken und offene Nachweise

- Das Inventar enthält alle nicht ignorierten, gegenüber HEAD geänderten/neuen Dateien. Die G1-Zuordnung ist durch Sicherung und A06-Dokumentation belegt; keine zusätzliche unbekannte Codeänderung wurde gegenüber dieser Grenze festgestellt. Dies ist keine pauschale Aussage zur Urheberschaft jeder historischen G1-Änderung.
- Keine Builds, bin/obj-Ausgaben, lokalen ZIPs, TRX-Laufverzeichnisse, Fensterzustände, Benutzerlayouts, .deps-Checkouts oder artifacts-Reviewquellen neu aufnehmen. `.gitignore` ignoriert artifacts und .deps. Eine Rekonstruktion aus der vollständigen Quellenliste ist sicherer als Entpacken des GUI-ZIPs in einen Commit-Kandidaten.
- Die Quellensicherung ist lokal, noch kein Git-Commit. Bis zur freigegebenen Umsetzung nicht löschen. Ein frischer Restore/Build/Test der rekonstruierten Stände bleibt offen; Netzwerk-/SDK-Verfügbarkeit und die bestehende Prism-Integration sind reale Reproduktionsvoraussetzungen.
- Der neuere master enthält L1-Assignment-Arbeit. Deren spätere Zusammenführung kann trotz anderer Dateipfade funktionale Wechselwirkungen in Core/GUI/MCP erzeugen. Bestehende G1/G2-Testergebnisse sind keine Integrationsabnahme dieses neueren master.
- Bekannte Restpunkte bleiben: spezieller Cross-Window-Mouse-Redock-Fall, intermittierender Headless-Teststartfehler, nicht vollständig verifizierte Windows-Skalierung bei 125 %. Erfolgreiche Wiederholungsläufe heben diese Grenzen nicht auf.
- Keine Multi-Dokument-Erweiterung und keine neue Funktionsänderung durch C1.
- Abschlusskontrolle: Git-Status und `git diff --check`; Inhalts-/Indexvergleich gegen den vor C1 erfassten SHA-256-Bestand. Einzige beabsichtigte neue Datei ist dieser Bericht. HEAD, master und Index bleiben unverändert.

**Entscheidung ausstehend:** Variante A freigeben oder ausdrücklich Variante B wählen. Weder der F1-Status „READY FOR LOCAL COMMIT“ noch dieses Audit ersetzt die Freigabe zur Commit-Erstellung.

