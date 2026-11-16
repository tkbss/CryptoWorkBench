# G2-I01 – GUI/Master Integration Preparation & Validation

Datum: 12.11.2026
Status: **G2-I01 READY FOR OWNER MERGE APPROVAL**

Die Integration ist im isolierten Worktree baubar und validiert. Es wurden keine Produktkorrekturen benötigt. Vier neue kombinierte L1-/Dock-Integrationstests ergänzen die unverändert übernommenen Tests. Kein Commit, Push, Pull Request, Rebase oder Update von master beziehungsweise feature/gui-g1 wurde ausgeführt.

## 1. Verifizierter Git-Ausgangszustand

| Referenz | SHA |
|---|---|
| master / Integrations-HEAD | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| feature/gui-g1 / MERGE_HEAD | `d611da8d765cf3045ec344bdc7135dacf25a5928` |
| Gemeinsame Basis | `7ecac04483c10722c4cb4b459e3f6cb5157c6af1` |
| G1 | `cb5d0a6c8b3fd26bb199b45ef3e8887f8aca6e84` |
| G2-02 | `2f78ef97b13cd74ce6ff97b4cc6b7f8e135ce5f8` |
| C1/C2-Dokumentation | `d611da8d765cf3045ec344bdc7135dacf25a5928` |

Die drei GUI-Commits bilden die erwartete Parent-Kette ab der gemeinsamen Basis. Beide geschützten Arbeitskopien waren vor Beginn sauber. Branch und Zielverzeichnis der Integration existierten noch nicht.

Integrationsbranch: `integration/g2-i01`
Worktree: `artifacts/worktrees/g2-i01-integration` (relativ zum Hauptrepository).

Ausgeführt wurden ausschließlich im neuen Worktree:

```text
git worktree add -b integration/g2-i01 artifacts/worktrees/g2-i01-integration fbbadae2f68b015ccedfea92ce8bba4cd7737ecb
git merge --no-commit --no-ff d611da8d765cf3045ec344bdc7135dacf25a5928
```

Git meldete einen erfolgreichen automatischen Merge und hielt vor dem Commit an. HEAD bleibt auf master; MERGE_HEAD bezeichnet den GUI-Stand. Die 164 automatisch übernommenen Pfade sind im Integrationsindex vorgemerkt. Die neue Testdatei und dieser Bericht bleiben untracked, damit sie explizit geprüft und später gemeinsam hinzugefügt werden können.

## 2. Vollständige Konfliktliste und Herkunft

**Keine Git-Konflikte; keine Konfliktauflösungen.** `git ls-files -u` ist leer. Es wurde weder pauschal ours/theirs gewählt noch eine Quelldatei manuell zusammengeführt.

Der Vergleich von Basis, beiden Branch-Trees und Integrationsindex umfasst 1.181 Pfade einschließlich Dateimodus und Git-Blob-ID:

| Herkunft gegenüber der gemeinsamen Basis | Pfade | Übereinstimmung mit Integrationsindex |
|---|---:|---|
| Beide Seiten identisch | 982 | vollständig |
| Nur GUI-Seite geändert/neu | 164 | vollständig mit GUI |
| Nur master-Seite geändert/neu | 35 | vollständig mit master |
| Auf beiden Seiten unterschiedlich geändert | 0 | entfällt |
| Unerwartete Abweichungen | 0 | keine |

Gegen master ergeben sich 126 neue und 38 geänderte Pfade, keine Löschungen. Gegen GUI ergeben sich die 35 master-Pfade. Vollständige Listen stehen in Anhang A/B und in den maschinenlesbaren Nachweisen.

### Semantische Prüfung der Schnittstellen

- **AST/Evaluatoren:** L1-Grammatik, Assignment-Knoten, AssignmentEvaluator, Initializer und StatementEvaluator stammen unverändert aus master. Die GUI-Seite ergänzt nullable SourcePosition-Metadaten am bestehenden FunctionCallExpressionNode; der bestehende Konstruktor bleibt erhalten. Die Positionsweitergabe über FunctionCallEvaluator, FunctionCall und OperationInvocation verändert die Zuweisungsregeln nicht.
- **Print/Output:** Die GUI-Seite transportiert die Parserposition zum Print-Ergebnis. Der neue Kombinationstest bestätigt Print des tatsächlich ersetzten Werts und Navigation zur zweiten Skriptzeile.
- **Editor/ExecutionPipeline:** Die Dock-Sitzung nutzt weiterhin den gemeinsamen Ausführungspfad und dieselbe History-/Undo-Infrastruktur. Direkte semantische Diagnosen ohne verlässliche Position behalten den bestehenden Unknown-Navigationsvertrag; es werden keine Positionen erfunden.
- **MCP:** Keine Änderung an Automation-Verträgen oder MCP-Funktionen. Authentifizierter Editor-Lease, Zustandsversion, idempotente Execution-ID, Redaction und Freigabe werden auch mit L1-Zuweisungen im Dock-Editor geprüft. V2-Ergebnisse werden weiterhin nicht ungefragt in GUI-Output publiziert; Variables aktualisiert sich nach Lease-Freigabe über Refresh.
- **Abhängigkeiten:** Projektdateien und sämtliche übernommenen Lockdateien sind blobidentisch zum jeweiligen Ausgangsbranch. Keine Paketmigration. Offizieller Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`, einschließlich 1.180 gepinnter Dateien, verifiziert. SDK `10.0.401`, unveränderte Avalonia-/Dock-Konfiguration.
- **Tests:** 244 gemeinsame Testprojektpfade, 39 GUI-Pfade und 8 master-Pfade sind unverändert übernommen. Kein vorhandener Test wurde entfernt oder abgeschwächt.

## 3. Neue Integrationsdateien und gemeinsame Funktionsprüfung

Zusätzlich zum automatischen Merge entstehen ausschließlich:

1. `CryptoWorkBenchGuiTest/DockAssignmentIntegrationTests.cs`
2. `docs/gui/G2-I01-integration-validation.md`

Die Testdatei enthält vier Fälle:

| Test | Varianten | Nachweis |
|---|---|---|
| AssignmentPublishesAtomicReplacementAndNavigationWithoutUiReplay | docked / floating | Deklaration, atomarer Ersatz, unveränderter alter Deklarationswert, neuer Variables-Wert, Print, Output-Quellnavigation, typwidrige Zuweisung mit Diagnose ohne Mutation, Undo/Redo, Zeilenhistory und Redock ohne erneute Ausführung |
| DockMcpAssignmentsPreserveLeasePublicationAndPartialFailure | gültig / Fehler nach gültiger Zuweisung | authentifizierter MCP-Lease, gemeinsamer Runner, Partial-Failure-Erhalt, redigierte Variable-Snapshots, idempotente Wiederholung, GUI-Publikationsgrenze, Refresh nach Release, anschließende GUI-Print-Navigation |

Die GUI-Manipulationen müssen StateRevision und die ersetzte Deklarationsinstanz unverändert lassen. Jeder Fall stellt die zuvor vorhandenen globalen Variablen im finally wieder her und prüft den wiederhergestellten Bestand. Globale VariableState-Implementierung und Produktcode wurden nicht verändert. L1-Typen sowie synchrone/asynchrone Pfade bleiben zusätzlich durch die unveränderten Core-/L1-Tests abgedeckt.

SHA-256 der endgültig getesteten neuen Testdatei:
`E94193B7573EC28FE1D3FB40CF8D334322AE188F95882D3A2F9E4102473F941E`.

### Entwicklung der neuen Tests, einschließlich Fehlern

Die ersten Läufe sind ausdrücklich **nicht** vollständig bestanden:

| Stand/Lauf | Debug GUI | Release GUI | Ursache |
|---|---|---|---|
| `i01-validation` | 1.129 PASS, 2 Failed | 1.129 PASS, 2 Failed | Zwei falsche Erwartungen in der neu hinzugefügten Testdatei |
| Endgültiger Stand `i01-final` | 1.131 PASS, 0 Failed | 1.130 PASS, 1 Failed | Release: bekannter Headless-Initialisierungsfehler vor Testkörper |
| Unveränderter Release-Wiederholungslauf | nicht wiederholt | 1.131 PASS, 0 Failed | vollständige GUI-Suite, kein Neubuild/keine Quelländerung |

Die erste neue Erwartung nahm für eine direkte semantische Diagnose eine navigierbare Position an. StatusViewModel transportiert diese nur bei einem verlässlichen ScriptBook-Zeilenbezug; der korrigierte neue Test prüft deshalb ausdrücklich IsError, SourceLine=null und Unknown. Die zweite Erwartung behandelte LineHistory wie einen vollständigen Dokumentersatz; korrigiert wurde der neue Test auf zwei einzelne History-Zeilen. Bestehende Tests und Produktverhalten wurden dafür nicht geändert.

Der spätere Release-Fehler betraf `ShortPrintIsVisibleAndSourceButtonNavigates(911,512)` mit `Dispatcher.VerifyAccess → DefaultRenderLoop.Add → AvaloniaHeadlessPlatform.Initialize → HeadlessUnitTestSession.EnsureIsolatedApplication`. Der Testkörper wurde nicht erreicht. Original-TRX, Stacktrace und Exitcode 1 bleiben erhalten. Der erfolgreiche vollständige Wiederholungslauf beseitigt den bekannten intermittierenden Infrastrukturfehler nicht.

## 4. Locked Restore, Builds und Regressionen

Neue Ausführungen in diesem Worktree, getrennt von historischen Baseline-Nachweisen:

- `eng/prism/Build.ps1 -Configuration Debug` und `Release`: beide Lösungen mit Locked Restore, erfolgreich.
- Alle drei Testprojekte je Konfiguration vollständig mit `dotnet test --no-build --no-restore`, TRX-Ausgabe und `NUnit.NumberOfTestWorkers=0`.
- Nativer Runner je Konfiguration zusätzlich mit `dotnet restore --locked-mode` und `dotnet build --no-restore`, erfolgreich.
- Nach den Korrekturen ausschließlich der neuen Testdatei wurden beide Konfigurationen und alle Suiten erneut ausgeführt (`i01-final`).

| Konfiguration | Lösungen Buildfehler | Warnungen erste / zweite Lösung | Native Restore/Build |
|---|---:|---:|---|
| Debug | 0 | 85 / 0 | PASS / PASS |
| Release | 0 | 91 / 0 | PASS / PASS |

Die vollständigen, nach Repository-Pfad normalisierten Warnungsmengen entsprechen exakt C2/G2: Debug 85 eindeutige Warnungen, Release 91, jeweils **0 Unterschiede**. Keine neue projektspezifische Warnung.

| Suite | Debug PASS | Debug NotExecuted | Release PASS | Release NotExecuted |
|---|---:|---:|---:|---:|
| Core | 11.723 | 27 | 11.723 | 27 |
| GUI | 1.131 | 0 | 1.131 nach Wiederholung | 0 |
| MCP | 176 | 13 | 176 | 13 |

Die Tabelle gibt die erfolgreichen vollständigen Läufe an; die Fehlerhistorie darüber ist Bestandteil des Ergebnisses. Alle vier neuen Kombinationstests sind in beiden Konfigurationen Passed, auch bereits im Release-Lauf mit dem unabhängigen Headless-Startfehler.

### Tatsächlicher Testinventarvergleich

Verglichen wurden TRX-Testdefinitionen und Einzelresultate der historischen lokalen L1-I1-master-Läufe, der C2/G2-Läufe und der neuen Integrationsläufe. Historische Läufe wurden **nicht neu ausgeführt** und werden nur als Baseline bezeichnet. Eingabepfade und SHA-256 stehen in `test-evidence-hashes.json`.

Vergleichsschlüssel: Klassenname und vollständiger parametrisierter Anzeigename, einschließlich Mehrfachvorkommen. NUnit vergibt bei manchen gleich benannten Datenfällen dieselbe TRX-Test-ID; diese Resultate werden als Multimenge erhalten, nicht zusammengezählt oder dedupliziert. Bei zehn MCP-Contract-Datenfällen je Lauf enthalten Anzeigenamen Laufzeitzeitpunkte aus dem unveränderten FakeAutomationService. Ausschließlich die vier belegten Felder AcceptedAt, DeadlineAt, FinishedAt und CapturedAt wurden für diese ContractTests normalisiert. Rohname und Normalisierung bleiben nachvollziehbar in `runtime-name-normalization.json` erhalten. Andere Parameter bleiben unverändert.

| Suite | master Fälle inkl. NotExecuted | GUI Fälle inkl. NotExecuted | Vereinigung mit Multiplizitäten | Integration | Fehlend | Neu |
|---|---:|---:|---:|---:|---:|---:|
| Core | 11.746 | 11.539 | 11.750 | 11.750 | 0 | 0 |
| GUI | 800 | 1.124 | 1.127 | 1.131 | 0 | 4 |
| MCP | 189 | 189 | 189 | 189 | 0 | 0 |

Identisches Ergebnis in Debug und Release. **Keine verlorenen Tests, keine neuen NotExecuted-Fälle.** Die erwarteten Mengen wurden nicht durch Addition der Branch-Gesamtzahlen gebildet.

NotExecuted bleiben einzeln mit Namen, Ergebnis und Begründung im Inventarnachweis erhalten:

- Core 27: fehlende lokale Symlink-Privilegien, Darwin-spezifische ABI-/ACL-Fälle und explizite manuelle Hardwaremessung.
- MCP 13: explizite Installed-Codex-/Desktop-Pairing-Prüfungen ohne die dafür vorgesehene externe Testumgebung beziehungsweise lokale Freigabe.
- GUI 0. Die teilweise abweichende CLI-Skipanzeige wird nicht anstelle der TRX-Einzelresultate verwendet.

## 5. Native Windows-Validierung

Der bestehende Runner bootet die echte Prism-Anwendung mit nativem Windows-Backend. Jede Probe läuft in einem eigenen Prozess mit separatem Layoutpfad unter den Integrationsartefakten. Initiale Aktivierung erfolgte bei Bedarf per Computer Use; anschließend laufen die unveränderten programmgesteuerten Assertions. Die beiden Independent-Modi wurden jeweils durch reales F8 im beobachteten Hauptfenster gestartet.

| Modus | Checks Debug | Checks Release | Endergebnis |
|---|---:|---:|---|
| dock (zusätzliche historische Foundation-Fixture) | 67 | 67 | PASS nach Aktivierung/Wiederholung |
| legacy / fallback / startup-failure | 10 / 13 / 5 | 10 / 13 / 5 | PASS |
| native-acceptance | 68 | 68 | PASS nach Wiederholung |
| editor / tools | 54 / 32 | 54 / 32 | PASS |
| output / diagnostics | 29 / 35 | 29 / 35 | PASS |
| layout-roles | 194 | 194 | PASS |
| layout / layout-restart / layout-invalid | 49 / 56 / 50 | 49 / 56 / 50 | PASS |
| integration / redock | 28 / 134 | 28 / 134 | PASS |
| operation-contract / partial-failure | 21 / 22 | 21 / 22 | PASS |
| independent-windows / independent-restart | 48 / 49 | 48 / 49 | PASS |
| standard-layout / corrections-layout | 9 / 9 Layout-Captures | 9 / 9 Layout-Captures | keine Layoutfehler |

Damit sind je Konfiguration 21 verschiedene native Szenarien mit **964 bestandenen Checks und 18 Layout-Captures** abgedeckt; Captures werden nicht als zusätzliche Assertions ausgegeben. Geprüft sind Editor/Variables/Info/Copy Example, Output-/Diagnostiknavigation, native Fenster und Aktivierung, Maximieren/Wiederherstellen, command-basiertes Redock, Reset, Persistenz einschließlich Neustart/ungültigem Layout, Editoridentität und Undo, MCP-Lifecycle einschließlich realem STDIO/TLS im vorhandenen Editor-Probe sowie vollständiger Host-/Registrierungs-/HWND-Abbau.

### Native Erstläufe und Wiederholungen

Insgesamt 53 Probeläufe für die 42 verschiedenen Szenarien, davon 11 fehlgeschlagene Versuche und 42 erfolgreiche Versuche. Kein fehlgeschlagenes JSON oder Log wurde überschrieben:

- Erster Debug-Lauf: `dock` (13 Fokusassertions), `native-acceptance`, `editor`, `layout-roles`, `layout`, `layout-restart`, `layout-invalid` scheiterten an fehlendem Vordergrund/Editorfokus beziehungsweise entsprechendem Timeout.
- Erster Release-Lauf: `dock` und `layout-invalid` scheiterten ebenfalls an der Aktivierung. `native-acceptance` meldete zusätzlich drei fehlgeschlagene Checks: `main-no-orphan-popup`, `floating-no-orphan-popup`, `redocked-no-orphan-popup`.
- Unter gezielter Anfangsaktivierung bestanden alle wiederholten Produktproben. Die zusätzliche Debug-Foundation-Fixture verfehlte auch im ersten Wiederholungsversuch die sehr frühe Fokusprüfung; ein zweiter, mit vorab bereiter Aktivierung gestarteter Versuch bestand mit 67/67. Keine Assertions oder Testquellen wurden dafür geändert.
- Die drei Release-Popup-Befunde traten im Wiederholungslauf mit 68/68 nicht erneut auf. Ihre Ursache ist **nicht abschließend bewiesen**; sie werden nicht pauschal als Headless-Fehler oder behobener Produktfehler bezeichnet. Bei der Eigentümerprüfung sind Popup-Cleanup und Fokuswechsel gezielt zu beobachten.

Die erfolgreichen Wiederholungen sind zusätzliche Nachweise, kein Ersatz für die Fehlerhistorie. `native-all-attempts.json`, `native-initial-failures.json`, die ursprünglichen Ergebnisdateien sowie die Wiederholungsordner dokumentieren die Grenzen.

### Tatsächlicher Desktop-Einstieg und Shutdown

Zusätzlich wurde in Debug und Release die gebaute `CryptoWorkBenchAvalonia.Desktop.exe --dock-shell --dock-native-trace <Nachweisdatei>` gestartet. Die sichtbare Dock-Shell mit Editor, rechter Variables-/Info-Gruppe und unterer Output-/Diagnostics-Gruppe wurde geprüft und jeweils per Alt+F4 regulär geschlossen. Beide Prozesse endeten mit Exitcode 0. Das vorhandene lokale Layoutprofil wurde vorab gesichert und anschließend in Inhalt und LastWriteTime wiederhergestellt; Hashvergleich jeweils erfolgreich. Diese zwei CLI-Smokes sind getrennt von den 53 Runner-Versuchen ausgewiesen.

Abschließend: keine sichtbaren Integrations-Prüffenster und keine verbliebenen Prozesse mit ausführbarer Datei unter diesem Worktree. Native Independent-Proben bestätigen zusätzlich zerstörte HWNDs und freigegebene Editor-/Hostregistrierung.

Gemessene Layout-Skalierung: **150 %**. 125 % wurde nicht geprüft.

## 6. Git-, Quell- und Artefaktintegrität

- `master` und `feature/gui-g1`: ursprüngliche HEADs, weiterhin sauberer Git-Status, byteidentische Git-Indexdateien.
- Alle zuvor vorhandenen Branch- und Remote-Tracking-Referenzen unverändert. Nur der neue Integrationsbranch ist hinzugekommen und zeigt weiterhin auf den ursprünglichen master-Commit.
- Fremde Worktrees wurden nicht rekursiv untersucht oder bearbeitet. Die zentrale Worktree-Registry dient ausschließlich zur Prüfung ihrer registrierten Pfade/HEADs/Branchzuordnungen.
- Keine unstaged Änderungen an übernommenen getrackten Dateien, keine unmerged Einträge. Die 1.181 Indexpfade stimmen vollständig mit der beschriebenen Quellvereinigung überein.
- Neue Testdatei und Bericht sind die einzigen vorgesehenen untracked Quelldateien. `.deps`, bin/obj, Logs, TRX, Layoutdateien und lokale Hilfsskripte bleiben ignorierte Artefakte und gehören nicht in den Merge-Commit.
- `git diff --cached --check`: **Exitcode 2, exakt 20 historische EOF-Leerzeilenbefunde**. Die betreffenden Blobs stammen unverändert aus dem genehmigten GUI-Stand. Dies ist kein warnungsfreier Check; die bereits erteilte Eigentümerausnahme wird angewandt. Keine Formatierungsbereinigung und keine Git-Konfigurationsänderung.
- `git diff --check` für den unstaged getrackten Diff: Exitcode 0. Die beiden neuen Dateien wurden zusätzlich separat gegen NUL mit `--no-index --check` geprüft, da untracked Dateien sonst nicht enthalten wären: jeweils Exitcode 1 wegen des vorhandenen No-Index-Dateiunterschieds, ohne Whitespace-Befund. Zwei zunächst im neu verfassten Bericht verwendete Markdown-Zeilenumbrüche mit nachgestellten Leerzeichen wurden nur in diesem neuen Bericht entfernt; historische Quellen bleiben unverändert.

## 7. Verbleibende Grenzen

1. Spezieller Cross-Window-Mouse-Redock-Fall bleibt offen. Erfolgreiches command-basiertes Redock und die vorhandenen nativen Szenarien belegen keine generelle Behebung dieses Spezialfalls.
2. Intermittierender Headless-Teststartfehler ist erneut dokumentiert; unveränderter vollständiger Release-Wiederholungslauf besteht.
3. Native Tests benötigen eine echte anfängliche Vordergrundaktivierung. Die protokollierten Fokusfehler dürfen nicht als stillschweigend bestandene Erstläufe gelesen werden.
4. Drei einmalige native Release-Popup-Cleanup-Befunde, im Wiederholungslauf nicht reproduziert; Ursache offen und bei Owner Review gezielt beobachten.
5. Windows-Skalierung 125 % nicht verifiziert; tatsächlich geprüfte Skalierung 150 %.
6. Linux/macOS/RID-Publish und neue GitHub-CI für diesen Integrationsstand wurden hier nicht ausgeführt. Historische CI-Ergebnisse werden nicht auf diesen Stand übertragen.
7. Die 40 expliziten/plattformabhängigen NotExecuted je Konfiguration bleiben offen; keine pauschale PASS-Deklaration dafür.

Es besteht nach den vollständigen erfolgreichen Wiederholungsläufen kein reproduzierter technischer Integrationsfehler. Die genannten Timing-/Umgebungsbefunde und bisherigen Produktgrenzen bleiben transparent Teil der Entscheidung.

## 8. Empfehlung für die spätere master-Integration

**Freigabeentscheidung ist noch erforderlich.** Der vorbereitete Merge kann nach Eigentümerprüfung im Integrationsbranch abgeschlossen werden. Die drei GUI-Commits werden weder neu erstellt noch gesquasht/cherry-picked.

Nach neuer Freigabe, nicht in diesem Auftrag ausgeführt:

1. HEAD von master, GUI und Integrationsbranch sowie MERGE_HEAD und Quellintegrität erneut prüfen. Bei inzwischen veränderten Eingangsständen nicht blind fortfahren.
2. Nur die neue Testdatei und diesen Bericht zum bereits vorbereiteten Integrationsindex hinzufügen; keine Artefakte stagen.
3. Merge-Commit auf `integration/g2-i01` erstellen, beispielsweise `Integrate G1/G2 dock GUI with L1 assignments`. Erwartete Eltern: ursprünglicher master und GUI-HEAD. Commit-Inhalt, Eltern und Tree prüfen.
4. Nach weiterer dafür erteilter Freigabe kann der unveränderte master per `--ff-only` auf den geprüften Integrations-Merge-Commit fortgeführt werden. Keinen zweiten abweichenden Merge erzeugen. Alternativ eine separat freigegebene PR-/CI-Abwicklung desselben Merge-Inhalts.
5. Vor Veröffentlichung beziehungsweise nach der tatsächlichen Integration die vorhandene `.NET`-CI-Matrix für win-x64, linux-x64, osx-arm64 und osx-x64 ausführen: Debug-/Release-Locked-Builds, native Dateipublikations-Gates, vollständige Release-Core-/GUI-/MCP-Suiten, Coordinator/F1/C1, RID-Desktop-Publish und Layout-/Native-Asset-/InfoDocs-Prüfung. Zusätzlich Windows-Native-Smoketest am finalen Commit. Keine Auslassung der neuen vier GUI-Fälle.

Push, PR und master-Update sind damit nicht automatisch autorisiert. Der aktuelle Zustand bleibt ein **uncommitteter Merge im isolierten Integrations-Worktree**.

## 9. Nachweise

Die folgenden Pfade sind relativ zum Hauptrepository; Rohlogs bleiben lokal und werden nicht in den Commit aufgenommen:

- `artifacts/g2-i01/`: Merge-Log, Vorher-/Nachher-Referenzen und Worktree-Registry, geschützte Indexhashes, Source-Provenance, vollständige Diffinventare, Warnungsvergleich, Testinventar samt Rohname-Normalisierung und Eingabehashes, alle nativen Versuche, Whitespace-Exitcodes, Prozess-Cleanup, SHA-256-Nachweisinventar und verwendete Hilfsskripte.
- `artifacts/worktrees/g2-i01-integration/artifacts/i01-validation/`: erste Builds und vollständige Suiten, einschließlich der zwei neuen Test-Erwartungsfehler je Konfiguration.
- `.../artifacts/i01-final/`: endgültige Locked-Builds und vollständige Suiten; unverändertes fehlgeschlagenes Release-GUI-TRX erhalten.
- `.../artifacts/i01-gui-Release-retry-1/`: vollständiger Release-GUI-Wiederholungslauf ohne Neubuild.
- `.../artifacts/i01-native/`, `i01-native-retry/`, `i01-native-dock-retry-2/`: sämtliche nativen Ergebnisdateien, Layouts, Traces, stdout/stderr und Prozess-Exitcodes.
- `.../artifacts/i01-independent/`: je Debug/Release Floating- und Neustartprüfung, 48 beziehungsweise 49 Checks.
- `.../artifacts/i01-desktop-cli/Debug/` und `Release/`: tatsächlicher CLI-Start, nativer Trace, regulärer Prozessabschluss und Profilwiederherstellung.

## Anhang A – Vollständiger Merge-Diff gegenüber master (164 Pfade)

`A` = neu, `M` = geändert. Dies sind unverändert übernommene GUI-Inhalte; zusätzlich kommen die beiden in Abschnitt 3 genannten neuen Dateien hinzu.

```text
M	CryptoScript/Model/AntlrToFunctionCallExpression.cs
M	CryptoScript/Model/Ast/FunctionCallExpressionNode.cs
M	CryptoScript/Model/FunctionCall.cs
M	CryptoScript/Model/FunctionCallEvaluator.cs
M	CryptoScript/Model/OperationInvocation.cs
M	CryptoScript/Operations/OutputOperations.cs
A	CryptoScriptUnitTest/PrintSourceTests.cs
M	CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs
A	CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs
M	CryptoWorkBenchAvalonia.Desktop/Program.cs
M	CryptoWorkBenchAvalonia/App.axaml.cs
M	CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs
M	CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs
M	CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj
M	CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs
M	CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs
M	CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs
M	CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs
M	CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs
A	CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs
A	CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs
A	CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs
A	CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs
A	CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs
A	CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs
A	CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs
A	CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs
A	CryptoWorkBenchAvalonia/Shell/DockOutputService.cs
A	CryptoWorkBenchAvalonia/Shell/DockOutputView.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellState.cs
A	CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml
A	CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml
A	CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs
A	CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs
A	CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs
A	CryptoWorkBenchAvalonia/Shell/DockWindowGeometry.cs
A	CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs
A	CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs
M	CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs
M	CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs
M	CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml
M	CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs
M	CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj
A	CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs
A	CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockEditorTests.cs
A	CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs
A	CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs
A	CryptoWorkBenchGuiTest/DockIndependentWindowNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockIndependentWindowTests.cs
A	CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs
A	CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs
A	CryptoWorkBenchGuiTest/DockLayoutTests.cs
A	CryptoWorkBenchGuiTest/DockMouseRedockTests.cs
A	CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs
A	CryptoWorkBenchGuiTest/DockNativeChromeTests.cs
A	CryptoWorkBenchGuiTest/DockOperationContractTests.cs
A	CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockOutputTests.cs
A	CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockPartialFailureTests.cs
A	CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs
A	CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockRedockNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockShellNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockShellTests.cs
A	CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockStandardLayoutTests.cs
A	CryptoWorkBenchGuiTest/DockToolNativeProbe.cs
A	CryptoWorkBenchGuiTest/DockToolTests.cs
A	CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs
A	CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs
M	CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs
A	CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json
M	CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs
A	docs/gui/G1-01-dock-feasibility.md
A	docs/gui/G1-02-shell-implementation.md
A	docs/gui/G1-03-editor-mcp-integration.md
A	docs/gui/G1-04-variables-info-integration.md
A	docs/gui/G1-05-output-pipeline.md
A	docs/gui/G1-06-diagnostics-navigation.md
A	docs/gui/G1-07-layout-persistence.md
A	docs/gui/G1-08-final-validation.md
A	docs/gui/G1-08-native-acceptance-checklist.md
A	docs/gui/G1-A01-docking-architecture-review.md
A	docs/gui/G1-A02-maximize-layout-roles.md
A	docs/gui/G1-A03-persistence-cleanup-roles.md
A	docs/gui/G1-A04-R1-actual-callback-audit.md
A	docs/gui/G1-A04-R2-test-cleanup.md
A	docs/gui/G1-A04-R3-partial-failure-audit.md
A	docs/gui/G1-A04-R4-partial-failure-correction.md
A	docs/gui/G1-A04-dock-operation-contract.md
A	docs/gui/G1-A05-M1-final-owner-acceptance.md
A	docs/gui/G1-A05-native-chrome-input-acceptance.md
A	docs/gui/G1-A06-cross-platform-final-validation.md
A	docs/gui/G1-N1-F01-F2-native-window-investigation.md
A	docs/gui/G1-N1-F01-mouse-redock-correction.md
A	docs/gui/G1-N1-F03-group-document-redock.md
A	docs/gui/G2-02-C1-git-dependency-audit.md
A	docs/gui/G2-02-C2-reconstruction-validation.md
A	docs/gui/G2-02-R1-visual-corrections.md
A	docs/gui/G2-02-R2-floating-windows.md
A	docs/gui/G2-02-final-acceptance.md
A	docs/gui/G2-02-standard-layout.md
A	docs/gui/evidence/G1-01-results.json
A	docs/gui/evidence/G1-02-results.json
A	docs/gui/evidence/G1-03-results.json
A	docs/gui/evidence/G1-04-results.json
A	docs/gui/evidence/G1-05-results.json
A	docs/gui/evidence/G1-06-results.json
A	docs/gui/evidence/G1-07-results.json
A	docs/gui/evidence/G1-08-results.json
A	docs/gui/evidence/G1-A02-results.json
A	docs/gui/evidence/G1-A03-results.json
A	docs/gui/evidence/G1-A04-R4-results.json
A	docs/gui/evidence/G1-A04-results.json
A	docs/gui/evidence/G1-A05-results.json
A	docs/gui/evidence/G1-A06-results.json
A	docs/gui/evidence/G1-N1-F01-F2-results.json
A	docs/gui/evidence/G1-N1-F01-results.json
A	docs/gui/evidence/G1-N1-F03-results.json
A	eng/gui/Test-DockNativeTrace.ps1
A	eng/gui/Validate-G1LayoutRoles.ps1
A	eng/gui/Validate-G1NativeAcceptance.ps1
A	eng/gui/Validate-G1Platform.ps1
A	eng/gui/Validate-G1Shell.ps1
A	eng/gui/native/G1ShellProbe.csproj
A	eng/gui/native/Program.cs
A	eng/gui/native/packages.lock.json
M	eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json
M	eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json
M	eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.lock.json
M	eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
M	eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.lock.json
A	prototypes/G1DockFeasibility/G1DockFeasibility.csproj
A	prototypes/G1DockFeasibility/NativeProbe.cs
A	prototypes/G1DockFeasibility/ProbeWorkspace.cs
A	prototypes/G1DockFeasibility/Program.cs
A	prototypes/G1DockFeasibility/README.md
A	prototypes/G1DockFeasibility/Session.cs
A	prototypes/G1DockFeasibility/Tests/DockTests.cs
A	prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj
A	prototypes/G1DockFeasibility/Tests/packages.lock.json
A	prototypes/G1DockFeasibility/Validate.ps1
A	prototypes/G1DockFeasibility/Views.cs
A	prototypes/G1DockFeasibility/packages.lock.json

```

## Anhang B – Gegenüber GUI zusätzlich erhaltene master-Pfade (35)

```text
M	CryptoScript/Antlr/CryptoScript.g4
M	CryptoScript/Antlr/CryptoScript.interp
M	CryptoScript/Antlr/CryptoScriptBaseListener.cs
M	CryptoScript/Antlr/CryptoScriptBaseVisitor.cs
M	CryptoScript/Antlr/CryptoScriptListener.cs
M	CryptoScript/Antlr/CryptoScriptParser.cs
M	CryptoScript/Antlr/CryptoScriptVisitor.cs
M	CryptoScript/ErrorListner/SemanticDiagnosticCode.cs
M	CryptoScript/Expressions/Expression.cs
M	CryptoScript/InfoDocs/Info.Types.md
A	CryptoScript/Model/AntlrToAssignment.cs
M	CryptoScript/Model/AntlrToStatement.cs
A	CryptoScript/Model/AssignmentEvaluator.cs
A	CryptoScript/Model/Ast/AssignmentStatementNode.cs
A	CryptoScript/Model/ParameterInitializerEvaluator.cs
A	CryptoScript/Model/PathInitializerEvaluator.cs
M	CryptoScript/Model/StatementEvaluator.cs
M	CryptoScript/Model/VariableDeclarationEvaluator.cs
A	CryptoScriptUnitTest/AssignmentCorrectionTests.cs
A	CryptoScriptUnitTest/AssignmentTests.cs
M	CryptoScriptUnitTest/CwmTests.cs
A	CryptoScriptUnitTest/ParameterAssignmentConsistencyTests.cs
A	CryptoScriptUnitTest/ParameterAssignmentSerializationTests.cs
M	CryptoScriptUnitTest/SemanticDiagnosticSinkTests.cs
A	CryptoWorkBenchGuiTest/AssignmentMcpIntegrationTests.cs
M	CryptoWorkBenchGuiTest/NativeEnterContinuationTests.cs
A	docs/architecture/L1-B1-independent-review.md
A	docs/architecture/L1-B2-corrections-report.md
A	docs/architecture/L1-B3-independent-rereview.md
A	docs/architecture/L1-B4-mechanism-consistency-fix.md
A	docs/architecture/L1-B5-final-independent-rereview.md
A	docs/architecture/L1-B6-param-serialization-boundary-fix.md
A	docs/architecture/L1-B7-final-independent-review.md
A	docs/architecture/L1-I1-master-integration.md
A	docs/architecture/L1-assignment-report.md

```
