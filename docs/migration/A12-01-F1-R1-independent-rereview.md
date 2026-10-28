# A12-01-F1-R1 – Independent Import Cancellation Re-Review

Stand: 27.10.2026. Bewertung: **F1 ACCEPTED**.

Die F1-Korrektur ist im geprüften Umfang vollständig und minimal. Es wurde kein blockierendes Produktfinding nachgewiesen. Der frühe Rücksprung verhindert bei jedem Nicht-Yes-/Nicht-No-Ergebnis bereits den Zugriff auf den ausgewählten Dateipfad und damit den Start der Importpipeline. Die acht vorhandenen Regressionen bestehen erneut. Eigene Gegenproben bestätigen den Erhalt bereits vorhandenen Zustands; eine gezielte Negativkontrolle zeigt, dass die Abbruchtests beim Entfernen der Korrektur tatsächlich scheitern.

Diese Abnahme betrifft ausschließlich F1. Sie gibt weder die gesamte Avalonia-12-Migration noch Prism-Integration, Redistribution oder eine Produktveröffentlichung frei.

## 1. Ausgangsbasis, Methodik und Integrität

Geprüft wurde der tatsächliche uncommittete Worktree `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

Vor der Prüfung wurden am 27.10.2026 um 10:01:52 UTC Git-Status, unstaged/staged Diff und SHA-256 von 775 Quelldateien aufgenommen. Der Vergleich mit dem finalen F1-Quellmanifest ergab **keine Abweichung**. Der vorhandene Migrationsstand wurde als Ausgangsbasis behandelt. Nachweise: [baseline](../../artifacts/a12-01-f1-r1/baseline/).

Der Re-Review ist methodisch separat: neue Bestandsaufnahme, direkte Quellprüfung, neue Builds/Testprozesse, ein eigenständig geschriebener Harness und eine eigene Negativkontrolle. Der ausführende Agent hat allerdings auch den bisherigen Arbeitskontext; es handelt sich nicht um personelle oder organisatorische Unabhängigkeit und nicht um eine externe Zertifizierung.

Die neue Arbeitskopie liegt unter `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-A12-01-F1-R1-Review`. Der unveränderte Prüfling liegt in `app/`, der eigene Harness außerhalb davon in `probes/review/`. Eine getrennte `mutant/`-Kopie dient ausschließlich der absichtlichen Fehlerinjektion. Es wurde kein anderer Git-Worktree verändert.

[integrity-final.json](../../artifacts/a12-01-f1-r1/integrity-final.json) belegt:

- Alle **775 ursprünglichen Quelldateien** einschließlich Produktcode, vorhandener Tests und Berichte sind unverändert.
- Die unveränderte Validierungskopie stimmt mit diesen 775 Dateien überein.
- Alle **136 zu Beginn erfassten Evidenzdateien** unter A12-01-R1 und A12-01-F1 sind unverändert.
- Die historischen Evidenzmanifeste wurden zusätzlich geprüft: 69 R1-Dateien und 65 F1-Dateien stimmen mit ihren dort gespeicherten Hashes überein.
- HEAD und Branch sind unverändert; die Staging Area bleibt leer.

Im Original entstanden ausschließlich dieser Bericht und `artifacts/a12-01-f1-r1/`. Keine produktiven Änderungen, keine Testkorrekturen, keine neuen Prism-Pakete, kein Commit, Push, Merge, Reset, Rebase oder Stash. `.codex/config.toml` wurde weder gelesen noch verändert.

## 2. Produkt- und Testdelta

Der gesicherte F1-Produktdiff wurde mit dem tatsächlichen Sidebar-Code und dem hashverifizierten Ausgangstext abgeglichen. Er enthält genau zwei Änderungen in derselben Importfunktion:

1. `SidebarViewModel.cs:160`: zusätzlicher sichtbarer Button `Cancel` mit `IsCancel = true`.
2. `SidebarViewModel.cs:172–174`: frühe Ablehnung jedes Ergebnisses außer den exakten Strings `Yes` und `No`; danach `clear = r == "Yes"`.

Der Aufruf von `OpenScriptBookAsync(result[0].Path.LocalPath, clear)` bleibt in Zeile 178 unverändert. Die Save-/Overwrite-Funktion und benachbarte Komponenten wurden durch F1 nicht verändert.

Der neue Testdelta besteht aus `ScriptBookImportCancellationTests.cs` mit acht parametrisierten Fällen. Bestehende Tests, Projektdateien und Paketversionen sind unverändert. Die vollständige GUI-Suite enthält weiterhin die vorherigen 472 Fälle plus diese acht Fälle.

**Sichtbarer Cancel-Button:** Das ist eine beabsichtigte, lokal begrenzte Änderung der Bedienoberfläche. Sie stellt einen ausdrücklich abbrechenden Button und die geforderte Escape-Zuordnung bereit. No bleibt die ausdrückliche Zustimmung zum Anhängen; seine Bedeutung wurde nicht auf Abbruch umgestellt. Der historische Vorher-Lauf zeigt, dass Escape ohne diesen zusätzlichen Button den Dialog offen ließ. Die Erweiterung ist für den geforderten Bedienvertrag sachgerecht und keine unnötige Refaktorierung.

## 3. Direkte Code- und Sicherheitsprüfung

Die Ergebnisauswertung lautet:

```csharp
if (r is not ("Yes" or "No")) return;
clear = r == "Yes";
```

Die Konstantenmuster erlauben nur die beiden exakten Werte. Null, leerer String, Cancel, abweichende Groß-/Kleinschreibung und beliebige andere Strings erreichen den Importaufruf nicht. Eine spätere Default-Verzweigung, die diese Ergebnisse doch noch als No interpretiert, existiert nicht.

Zwischen dem Dialog-await und dieser Prüfung liegt keine weitere asynchrone Operation. Ein ungültiges Ergebnis wird unmittelbar zurückgewiesen. Der Rücksprung erfolgt vor:

- `result[0].Path.LocalPath`;
- `CryptoScriptEditViewModel.OpenScriptBookAsync`;
- dessen Zurücksetzen des Erfolgsflags und Leeren von Diagnosen;
- Admission/Queue-Annahme und File.ReadAllLinesAsync;
- Parser-/Runnerausführung und den anschließenden Editorwirkungen.

GuardAsync fängt zwar Fehler des Commands ab, kann den frühen Rücksprung aber nicht in eine Importoperation umwandeln. Die Tests prüfen zusätzlich den unveränderten Status, sodass ein versehentlicher Eintritt mit anschließend abgefangenem Fehler nicht allein deshalb als erfolgreicher Abbruch gewertet würde.

Bei geschlossenem Bestätigungsfenster oder geschlossenem Owner entsteht kein Yes-/No-Ergebnis. Die vorhandenen Tests und die eigene Probe beobachten keinen späteren Import. Die eigene Probe liefert die Dateiauswahl ausdrücklich asynchron von einem Worker und prüft anschließend die verschiedenen Abbrüche. Zusätzlich wurde das Schließen des Bestätigungsfensters unmittelbar gefolgt vom Owner-Schließen geprüft. Das ist kein Beweis jeder denkbaren Betriebssystemreihenfolge; ein Umgehen des Rücksprungs wurde in den ausgeführten Abläufen nicht nachgewiesen.

Yes und No erreichen unverändert dieselbe vorhandene Pipeline. Nur Yes setzt clear=true. Der leere Editor überspringt unverändert die Bestätigung und importiert direkt mit clear=false. Keine Änderung an Interpretations- oder Ausführungssemantik wurde festgestellt.

## 4. Bewertung der acht Dialogszenarien

| Szenario | Erneute Prüfung / Ergebnis |
|---|---|
| Yes | PASS: vorhandener Text wird ersetzt; tatsächlicher Runner erzeugt f1Imported; bestehende Variable wird wie bisher beim Clear entfernt. |
| No | PASS: vorheriger Inhalt bleibt erhalten, Buchtext wird angehängt und ausgeführt; bestehende Variable bleibt erhalten. |
| Close/X | PASS: realer Window.Close-Pfad, keine Pfadauswertung und keine neue Ausführung. X wird durch denselben Window.Close-Vertrag abgedeckt, nicht durch einen manuellen Titelleistenklick. |
| Escape | PASS: Yes ist fokussiert; Escape aktiviert den Cancel-Pfad. Kein Import. Zusätzlich unabhängig headless und nativ geprüft. |
| Owner-Schließen | PASS: Owner und Dialog werden geschlossen; keine nachträgliche Importoperation. Die wegfallende Viewregistrierung wird nicht fälschlich als Importmutation bewertet. |
| Bereits leerer Editor | PASS: keine Bestätigung, direkter Import und tatsächliche Ausführung wie zuvor. |
| Cancel | PASS: echter zusätzlicher MessageBox-Button und dessen Command; vollständiger Abbruch. |
| Unknown | PASS: ungültiger Parameter über den tatsächlichen MessageBox-Command. Die eigene Probe prüft ergänzend den nicht erlaubten Wert `YES`. |

Die Yes-/No-/Leer-Fälle waren bereits im historischen Vorher-Lauf erfolgreich und sind im aktuellen Stand weiterhin erfolgreich. Der Vergleich wird durch die neue Negativkontrolle ergänzt.

## 5. Bewertung der vorhandenen Tests

### Unabhängigkeit der Fälle

Jeder Fall erzeugt einen eigenen AutomationService, Status, HistoryService, ViewModel, View, Editor, Owner, Pickerproxy und ein eigenes temporäres Verzeichnis. Der vorherige globale Variablenbestand wird aufgenommen und nach Abschluss wiederhergestellt. Die Klasse ist NonParallelizable. Die drei positiven Fälle stellen ihre Voraussetzungen selbst her; kein späterer Fall ist auf ihre Ergebnisse angewiesen.

Die Fälle sind **nicht prozessisoliert**: Sie teilen den bewusst gemeinsamen Headless-Dispatcher und statische Infrastruktur. Das entspricht der bereits geprüften Testarchitektur wegen AvaloniaEdit-KeyBinding-Threadbindung. Im gezielten Lauf, im vollständigen GUI-Lauf und in den eigenen Proben wurde keine voraussetzungswidrige Kopplung beobachtet. Eine Aussage über sämtliche denkbaren Testreihenfolgen wird daraus nicht abgeleitet.

### Tatsächliche Integrationspfade

Der Test ruft den öffentlichen OpenScriptBookCommand über IAsyncCommand auf, einschließlich GuardAsync. Er ersetzt keine Produktionsfunktion für die Ergebnisauswertung. MessageBox-Fenster und Button-Commands sind echt; Button.OnClick wird per Reflection ausgelöst, um auch den Command auszuführen. Close und Owner.Close sind tatsächliche Fensterpfade. Headless-Escape läuft durch die Tastatureingabe bei fokussiertem Yes.

Der Unknown-Fall ist gezielte Fehlerinjektion: Er ändert den CommandParameter des tatsächlichen No-Buttons. Er ist kein normaler Benutzerpfad, aber geeignet, die Ablehnung eines unerwarteten Bibliotheksergebnisses zu prüfen.

### Aussagekraft des Dateipicker-Proxys

Der Proxy liefert einen ausgewählten IStorageFile mit einem echten lokalen Dateipfad. Das entspricht dem für die Sidebar verwendeten Teil des Providervertrags; produktiv wird die Datei anschließend über File.ReadAllLinesAsync anhand dieses Pfads gelesen. Der Zähler am Path-Getter liegt damit vor der tatsächlichen Lese-/Importgrenze.

Die F1-Tests liefern das Pickerergebnis synchron und sperren die Datei in Abbruchfällen gegen Lesen. Das ist kein nativer Picker-End-to-End-Test. Die Sperre könnte eine fehlerhafte Ausführung blockieren, **kann hier aber keinen grünen Abbruchtest vortäuschen**, weil bereits der Path-Zähler, die Ausführungsidentität und der Status geprüft werden. Die Negativkontrolle bestätigt diese Fehlererkennung.

### Nichtausführung und Zustand

Unveränderter Text allein wäre unzureichend. Die Tests kombinieren jedoch PathReads=0, unveränderte LastExecutionId und StateVersion nach Drain, keine neue Variable, unveränderte Dokumentinstanz/-version, null aktive Ausführung und fehlende MutationBusy. Beide ursprünglichen Undo-/Redo-Richtungen werden anschließend wirklich ausgeführt und auf ihren ursprünglichen Inhalt geprüft.

Die Abbruchassertions prüfen im bestehenden Test nur einen **anfangs leeren Diagnosebestand**, einen **anfangs falschen Erfolgsstatus** und eine **noch nicht belegte LastExecutionId**. Außerdem wird der bestehende Variablenwert dort nicht separat auf Identität und Wert verglichen. Das sind begrenzte Testlücken, keine nachgewiesenen Produktfehler. Die eigene Probe erweitert genau diese Ausgangszustände und besteht; die unveränderte frühe Rückkehr stützt das Ergebnis zusätzlich.

Die erwartete Operation wird vollständig abgewartet, anschließend wird der Service geleert. Es gibt keinen bloßen Sleep als Ersatz für die Ausführungsfinalisierung. Begrenztes Polling wird nur zum Finden des geöffneten Dialogs verwendet. Im überprüften Ablauf wurde kein False Positive durch eine noch ausstehende Importoperation festgestellt.

## 6. Eigene zusätzliche Gegenproben

Der neue Harness wurde eigenständig geschrieben und übernimmt nicht die Assertions des F1-RunScenario-Helfers. Er liegt unter [probe](../../artifacts/a12-01-f1-r1/probe/); er referenziert unveränderte Produktprojekte und nutzt den bereits vorhandenen Friend-Assembly-Namen für interne Statusabfragen. Produktionsattribute wurden nicht verändert.

Für jeden der sechs Fälle Close, Escape, Cancel, Owner, Unknown und CloseThenOwner:

1. Tatsächliche erfolgreiche Seed-Importausführung mit einer harmlosen Variablendefinition.
2. Vorhandenen Erfolgsstatus true, eine konkrete frühere LastExecutionId, eine bestehende Diagnose und einen Statusstring erhalten.
3. Editordokument mit beiden Undo-/Redo-Richtungen vorbereiten.
4. Neue Auswahl von einem Worker über eine TaskCompletionSource liefern; der Command muss davor tatsächlich suspendiert sein.
5. Ein **lesbares, ungesperrtes** harmloses Buch auswählen und die echte Bestätigung abbrechen.
6. Unveränderte Pfadzugriffszahl null, frühere Ausführungsidentität/StateVersion, Erfolgsflag true, dieselbe Diagnoseinstanz, denselben Variablenwert/dieselbe Variableninstanz sowie Dokument und ursprüngliche History prüfen.

Alle sechs Fälle bestehen headless und unter Windows. Der native Lauf verwendet UsePlatformDetect, echte Fensterhandles und eine Desktop-Message-Loop. Escape wird als tatsächliches geroutetes KeyDown vom fokussierten Yes-Button ausgelöst und muss behandelt werden. Es ist kein manueller Hardwaretastatur- oder Dateipickertest.

### Eigene negative Kontrolle

In einer **separaten isolierten Kopie** wurde ausschließlich der neue frühe Rücksprung entfernt. Der Cancel-Button und alle vorhandenen Tests blieben unverändert. Die normalen Prüflingsquellen und der Originalworktree wurden dabei nicht verändert.

Ergebnis: **3 PASS, 5 FAIL, Exit 1**. Yes, No und leerer Editor bleiben erfolgreich. Close, Escape, Owner, Cancel und Unknown scheitern jeweils an PathReads=1 statt 0. Das ist der erwartete Erfolg der Negativkontrolle, kein Fehler des tatsächlich geprüften F1-Produkts.

Belege: `tests/mutant.trx`, `tests/mutant.log`, `probe/SidebarViewModel-mutant.cs`, `run-probes.ps1`. Die kaputte Kopie ist ausdrücklich als Review-Mutant gekennzeichnet und darf nicht als Produktstand verwendet werden.

## 7. Ausgeführte Tests und historische Evidenz

Neue Umgebung: Windows 10.0.26200 win-x64, .NET SDK 10.0.401, MSBuild 18.9.11, Hostruntime 10.0.12. Separate NuGet-/CLI-Verzeichnisse. Ablauf und Einzelbelege liegen vollständig in [artifacts/a12-01-f1-r1](../../artifacts/a12-01-f1-r1/).

| Neuer Re-Review-Lauf | Tatsächliches Ergebnis |
|---|---|
| Release-Restore / Build der GUI-Solution | Exit 0 |
| Debug-Restore / Build der GUI-Solution | Exit 0 |
| Gezielte F1-Regressionen | 8/8 PASS |
| Vollständige GUI-Suite | 480/480 PASS |
| Coordinator separat | 35/35 PASS |
| Eigenständige Headless-Probe | 6/6 PASS |
| Eigenständige Windows-Probe | 6/6 PASS |
| Absichtliche Negativkontrolle | 3 PASS, 5 erwartete FAIL; Exit 1 |

Die 35 Coordinator-Fälle und acht F1-Fälle sind Teil der GUI-Suite und keine zusätzlichen einzigartigen GUI-Testfälle. Der Vergleich von GUI-/Coordinator-Fallnamen und Outcomes mit F1 zeigt keine Abweichung. Debug- und Release-Paketgraphen enthalten gegenüber F1 keine Paket-/Versionsänderung.

Die historischen Vorher-/Nachher-TRX wurden selbst aus den Einzelfällen ausgewertet und gegen die gesicherten Manifeste geprüft: Vorher 3 PASS/5 FAIL; nachher 8 PASS. Die ursprünglichen Fehlschläge sind plausibel differenziert: Close/Owner/Unknown erreichen die Pfadauswertung, Escape wartet bis zum Timeout, Cancel existiert noch nicht. Diese historischen Ursachen sind nicht identisch mit allen Ursachen des neuen Mutantenlaufs, der den Cancel-Button absichtlich behält.

**Core/MCP wurden in diesem Re-Review nicht erneut ausgeführt.** Gemäß Auftrag wurden die unveränderten F1-Rohdaten als historische Evidenz ausgewertet:

| Historischer F1-Lauf | Ergebnis |
|---|---|
| Core | 7.672 Fälle: 7.665 PASS, 2 FAIL, 5 NotExecuted |
| MCP | 90 Fälle: 89 PASS, 1 NotExecuted |

Die beiden Core-Fehler RejectsInvalidPsnWithoutNormalizing("1") und ("A1") bleiben die dokumentierten pfadabhängigen Assertions gegen Exception.ToString einschließlich Quellpfad. Keine Umdeutung zu PASS. Core-Skips: drei Symlinkfälle ohne Berechtigung, Darwin-ACLs und manueller Hardwaretest. MCP-Skip: InstalledCodexDiscoversAndCallsStatus ohne konfigurierte lokale Codex-Ausführung.

`tests/summary.json` zählt die UnitTestResult-Einträge, nicht die bei Skips unvollständigen TRX-Summary-/Konsolenzähler. Frühere Windows-Smokes wurden nicht als neu ausgeführt ausgegeben; die hier neu ausgeführte native Probe ist separat beschrieben.

## 8. Architekturverträglichkeit und Findings

Die Produktänderung beendet eine abgebrochene Sidebar-Aktion **vor** der bestehenden Importpipeline. Bei Zustimmung bleibt die bisherige Pipeline unverändert. Die Hash-/Deltaprüfung bestätigt keine F1-Änderung an:

- EditorCoordinator, EditorBridge, Admission oder exklusiven Leases;
- Dokumentidentität, Kontrollgeneration und nativer Undo-/Redo-Delegation;
- UiDispatch oder ExecutionContext-Grenzen;
- Core, Parser oder Runner;
- Prism-Abhängigkeiten und Avalonia-Versionen.

Die aktuelle Prüfung verändert ihrerseits keine dieser Dateien. Vollständige GUI-/Coordinator-Läufe und die engeren Gegenproben unterstützen die Kompatibilität im geprüften Umfang.

**Neue Produktfindings: keine.** Insbesondere wurde kein Rückfall von Close/Escape/Owner-Schließen in eine Importausführung nachgewiesen. Die beschriebenen Begrenzungen der ursprünglichen Tests sind von Produktfehlern getrennt und durch ergänzende Proben teilweise geschlossen. Hypothetische Abläufe ohne konkreten Nachweis wurden nicht als Findings klassifiziert.

## 9. Verbleibende Grenzen und Abnahmeempfehlung

Linux und macOS bleiben **OPEN**. Die ausdrückliche CJK-IME-Risikoakzeptanz bleibt unverändert. Der native Dateipicker und dessen eigener Shutdown-/Fehlervertrag sind durch den Proxy nicht end-to-end validiert; geprüft ist hier die F1-Entscheidung nach erfolgter Auswahl einschließlich asynchroner Auswahlfortsetzung und Schließen der Importbestätigung. Ebenso wurde keine vollständige OS-Tastaturlayout- oder visuelle Designmatrix geprüft.

Bekannte Core-Pfadtests, Codex-Integrationsskip, Prism-Binärblocker sowie Lizenz-/Distributionsfragen bleiben außerhalb dieser F1-Abnahme offen. Die eigene Prüfung behauptet keine generelle Race-Freiheit aller GUI- und Fremdbibliothekspfade.

F1 kann im nachgewiesenen Umfang abgenommen werden. Die Entscheidung schließt keine automatische weitere Implementierung, Migration, Prism-Integration oder Veröffentlichung ein.

