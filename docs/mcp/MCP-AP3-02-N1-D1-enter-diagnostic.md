# MCP AP3-02-N1-D1 – Diagnose des nativen Enter-Blockers

Datum: 2026-10-29. **ROOT CAUSE CONFIRMED.**

Der native `EnterParagraphBreak`-Command erhält bereits ein gültiges GUI-Ticket. Sein anschließender nativer `PerformTextInput("\n")` erreicht `WorkbenchTextArea.OnTextEntering`, wird dort aber wegen des bereits aktiven Originaltickets durch `Allowed == false` abgefangen. `Handled` wird gesetzt, bevor das TextEntering-Event und damit die GUI-Ausführung erreicht werden. Die Ursache liegt an der **AP2.1-TextArea-/Coordinator-Grenze**, nicht in der AP3-02-History-Capability. Physisches Enter bleibt als eigene Nachweisklasse OPEN.

## 1. Ausgangsstand und Diagnoseverfahren

Worktree `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp-ap3`, Branch `feature/mcp-ap3`, HEAD `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Windows `10.0.26200.0`, .NET SDK `10.0.401`, Debug/net10.0. Avalonia 12.1.4, AvaloniaEdit 12.0.0, DataGrid 12.1.2. Unveränderte Prism-Quellreferenzen; Build verifiziert Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` und 1180 gepinnte Dateien.

Grundlage: [N1-Abnahmebericht](MCP-AP3-02-N1-native-windows-acceptance.md). Vorher wurden Status, Patch und SHA-256-Manifest gesichert. Instrumentiert wurden ausschließlich **Quellkopien unter `artifacts/ap3-02-n1-d1/`**. Ein expliziter MSBuild-Import ersetzt nur für den Diagnosebuild vier Compile-Einträge durch diese Kopien und ergänzt `D1Trace.cs`. Keine Sperrbedingung, Capability oder Ausführungsentscheidung wurde geändert. Die zusätzlichen Handler beobachten auch bereits behandelte Ereignisse; Logs enthalten nur harmlose Testdaten und Zustandsmetadaten.

Zwei neue Desktopinstanzen aus dem Worktree wurden nacheinander gestartet und anschließend geschlossen. Kein vorhandenes Benutzerfenster, ScriptBook oder Schlüssel wurde verwendet. Der erste Lauf protokolliert Ereignisse/Stack/Admission, der zweite zusätzlich ViewModel, Queue, History und Registry. Der Diagnosebuild verändert erzeugte Binärdateien, keine Produktquellen. Abschließend wurden beide Lösungen ohne Diagnoseimport erfolgreich gebaut: GUI-Lösung 19 Warnungen/0 Fehler, zweite Lösung 0 Warnungen/0 Fehler. Die wiederhergestellte Produkt-DLL ist byteidentisch zum N1-Ausgangsbuild:

`CryptoWorkBenchAvalonia.dll`: SHA-256 `F41CD6742E63C2DFBC026C819107738D371FFDE12293AB44C467866631442F9D`.

## 2. Eingabeverfahren und Reproduzierbarkeit – D1

Native Windows-Eingabe über den Computer-Use-Skill (`@oai/sky`). Je Instanz: Editor anklicken, Fokus `26 custom` bestätigen, harmlosen Text einsetzen, sichtbaren Text/Caret prüfen, Return und danach KP_Enter senden. Instanz 1 verwendete `Print(0x(11))`, Instanz 2 `Print(0x(22))`. Vorher und nachher blieb jeweils derselbe Text in Zeile 1, Spalte 14.

| Verfahren | Zustellung und Ergebnis |
| --- | --- |
| OS-Automation `Return` | KeyDown/KeyUp `Return` nachgewiesen, Fokus true; nativer Enter-Command läuft an; TextEntering-Grenze lehnt ab. In beiden Instanzen reproduziert. |
| OS-Automation `KP_Enter` | Avalonia meldet ebenfalls `Key.Return`; identische Command-/Admissionfolge und Ablehnung. In beiden Instanzen reproduziert. |
| Weitere unabhängige native Windows-Methode | Nicht durchgeführt. Ein weiterer Alias desselben Automation-Backends wäre kein unabhängiger Zustellungsnachweis. |
| Physische Tastatur | **OPEN**, keine tatsächlich bediente physische Tastatur in diesem Durchlauf. Keine Gleichsetzung mit OS-Automation. |

Wichtige Präzisierung zur Texteingabe: `sky.type_text` führte in der gemessenen Umgebung über **Ctrl+V/Paste** zum Testtext. Das Trace weist diesen Weg ausdrücklich aus. Die anschließenden Return-/KP_Enter-Ereignisse wurden separat als Tastendrücke zugestellt. Der erfolgreiche Aufbau des Testtextes ist daher kein Nachweis physischer Zeichenkomposition.

Die Ursache ist kein bloß vermuteter Zustellungsfehler: Die Windows-Stackspur führt über `WindowImpl.AppWndProc`, `KeyboardDevice.ProcessRawEvent`, den nativen KeyBinding-Handler und `EditingCommandHandler.OnEnter` bis zur konkreten ablehnenden Produktbedingung. Welche zusätzlichen Unterschiede physisches Enter auf diesem Rechner zeigen könnte, bleibt trotzdem OPEN.

## 3. Tatsächliche Ereignis- und Admissionfolge – D2

Beleg: [native-trace.log](../../artifacts/ap3-02-n1-d1/native-trace.log), insbesondere erster Return ab Zeile 17 und KP_Enter ab Zeile 84. Zeitpunkte erster Lauf: 17:08:22 und 17:08:32 UTC.

| Schritt | Beobachtung |
| --- | --- |
| KeyDown Tunnel | `key=Return`, `Handled=false`, Editorfokus true; Phase Gui, Active=false, Pending=0, ObserverDepth=0, Generation=1, CanStartGui=true. |
| OnKeyDown | Vor/nach `base.OnKeyDown`: Handled=false. Der anschließende registrierte InputHandler verarbeitet den Command. |
| Command CanExecute | `EnterParagraphBreak`, true; zwei Abfragen sind keine doppelte Ausführung. |
| Command Execute | Ein Aufruf, Allowed=true; Routed-Command-Argument wird als behandelt markiert. |
| RunGui / TryBegin | Eintritt mit CanStartGui=true; TryBegin erfolgreich, Pending=1. |
| Commit | Active=true, Pending=1, ObserverDepth=0, Generation=1; CanStartGui wird false. |
| Nativer OnEnter | `EditingCommandHandler.OnEnter` → `TextArea.PerformTextInput(string)` → `PerformTextInput(TextInputEventArgs)`. |
| Workbench OnTextEntering | Text `\n`, Handled=false, `nativeTextDispatch=false`, Allowed=false; Originalticket weiterhin aktiv. |
| Ablehnung | `Handled=true`, Rückkehr vor `base.OnTextEntering`. Kein zweites TryBegin und kein zweites Ticket. |
| Abschluss | Revision 1→1, TextEffect=None, HistoryEffect=None, Finalization=Complete, Error=null; Active=false, Pending=0, ObserverDepth=0, Generation=1, CanStartGui=true. |
| KeyDown Bubble / KeyUp | KeyDown Handled=true; KeyUp Handled=false. Zwei KeyUp-Logzeilen im ersten Lauf entsprechen Tunnel/Bubble desselben Ereignisses, nicht zwei Eingaben. |

**Routed TextInput und `OnTextInput` werden für diese native Enterroute nicht beobachtet.** Der native Editing-Command ruft direkt PerformTextInput auf. Ein fehlendes Routed-TextInput-Ereignis ist daher hier nicht die Zustellungsursache. TextEntering/TextEntered-Events und der View-Handler werden wegen der vorgelagerten Ablehnung überhaupt nicht ausgelöst.

Die im Auftrag skizzierte Folge `OnTextInput → OnTextEntering → RunGui` ist für normales TextInput relevant, beschreibt aber nicht den hier beobachteten Enter-Command. Tatsächlich liegt `RunGui` bereits **vor** `OnTextEntering`.

## 4. Exakte Blockierstelle und Bestandsabgleich

`Controls/WorkbenchTextArea.cs`:

- Zeile 47: `Allowed` fordert unter anderem `Coordinator.CanStartGui`.
- Zeile 81: Native Editing-Commands werden mit `coordinator.RunGui(Execute, ...)` ausgeführt.
- Zeilen 100–101: `OnTextEntering` akzeptiert ausschließlich den zuvor gesetzten einmaligen `nativeTextDispatch`-Weg oder fordert erneut `Allowed`.
- Zeilen 105–107: `nativeTextDispatch` wird erst beim eigenen TextInput-Redispatch gesetzt. Die native Commandroute setzt es nicht.

`EditorCoordinator.CanBegin` fordert korrekt `!Active`. Deshalb kann der innere Neueintritt nicht beginnen, obwohl er zur ursprünglichen zugelassenen nativen Aktion gehört. Der vorhandene `nativeDispatch`-Mechanismus betrifft die CommandBindings und autorisiert diesen TextEntering-Übergang nicht.

Es handelt sich um eine **erneute Admissionprüfung innerhalb derselben nativen Aktion**, nicht um ein tatsächlich erstelltes verschachteltes Ticket. Die Ablehnung wirft keine Exception und setzt keinen Busy-/Stale-/Callbackfehler am Originalticket; dadurch sieht der Abschluss wie ein erfolgreicher No-op aus.

Die betreffende `WorkbenchTextArea.cs` ist gegenüber HEAD unverändert; `git show HEAD:...` enthält dieselben Bedingungen und Routen. Die Ursache ist somit bereits im Ausgangsstand vorhanden. Ein separater historischer Desktoplauf wurde nicht durchgeführt. Dieser Quellabgleich und die laufenden Gegenproben erlauben keine pauschale Aussage über jede frühere Konfiguration.

## 5. Ausführung, Queue, History und Diagnostik – D3

Der zweite native Lauf misst vor KeyDown und nach KeyUp für beide Entervarianten:

`IsExecuting=false; Q.IsBusy=false; History.Count=0; Registry.jobs.Count=1; StatusString="bla"; PrintMessage.Length=0`.

Die Registry enthält bereits vor Enter einen Eintrag. Es entsteht **kein zusätzlicher Job**; „Registry leer“ wäre daher falsch. Der Ursprung des bereits vorhandenen Eintrags wurde nicht separat untersucht. Beleg: [native-state-trace.log](../../artifacts/ap3-02-n1-d1/native-state-trace.log), vollständige gemeinsame Spur in `trace.log`.

| Übergang | Nativer Enterlauf |
| --- | --- |
| View.TextEntering | Nicht erreicht. IsExecuting ist nicht die ablehnende Bedingung. |
| ExecuteInputAsync / ParseLineAsync | Kein Eintritt; somit keine Ausführungsanforderung. |
| ExecuteLineAsync / WorkbenchAutomationService.RunAsync | Nicht erreicht. |
| InterpreterHostQueue.Reserve / Registry.Accept | Für Enter nicht erreicht; keine Queue-Abweisung und kein neuer Job. |
| Interpreterpipeline / BeginLine / AddLine | Nicht erreicht; History bleibt leer. |
| Parser-/Interpreter-/Präsentations-/Lifecyclefehler | Für diese Enteranforderung kein solcher Lauf begonnen. |
| Status / Diagnostik | Status unverändert; ParseLineAsync und dessen ClearDiagnostics/Präsentation werden nicht erreicht. Keine neue Diagnose aus dieser Ausführung. Kein vollständiger nativer Diagnostiklisten-Dump erhoben. |

Eine verdeckt erfolgreiche Ausführung mit lediglich fehlender Ausgabe ist für die untersuchten Entergesten ausgeschlossen: Der GUI-Handler wird nicht erreicht, die Ausführungsmarker fehlen, History/Jobanzahl bleiben unverändert. Die kontrollierten Positivproben zeigen dagegen die Marker bis zum erfolgreichen Interpreterabschluss.

## 6. Kontrollierte Gegenproben – D4

Separates Prüfprogramm: [probe/Program.cs](../../artifacts/ap3-02-n1-d1/probe/Program.cs). Keine permanenten Tests oder Assertions geändert. Sechs Fälle wurden gegen die instrumentierte und anschließend gegen die wiederhergestellte **uninstrumentierte** Produkt-DLL erfolgreich geprüft. Die Kontrollfenster verwenden Fluent-/AvaloniaEdit-Templates und verifizieren TextArea-Fokus. Frühere Fixtureversuche ohne vollständiges Template/Fokus waren nicht aussagekräftig und werden nicht als Nachweis gewertet.

| Fall | Ergebnis im gültigen Durchlauf |
| --- | --- |
| Headless-Key.Return mit vollständiger CryptoScriptEditView | Gleiche Blockade; Text unverändert, TextEntering=0, TextEntered=0, Document.Changed=0; vollständiger wirkungsloser Receipt. History=0, Q frei. |
| Headless-Key.Return ohne View-Ausführungshandler | Gleiche Blockade am koordinierten WorkbenchTextEditor. Keine tatsächliche CryptoScript-Ausführung angeschlossen. |
| `PerformTextInput("\n")` mit vollständiger View | TextEntering=1, TextEntered=1, Changed=2; genau eine Ausgabe `out: 0x(11)`, History=1. |
| Gezieltes Routed TextInput `"\n"` mit vollständiger View | Dasselbe erfolgreiche Ergebnis über OnTextInput/OnTextEntering/ExecuteInputAsync. Kein Windows-Zustellungsnachweis. |
| Direkter isolierter `Vm.ParseLineAsync("Print(0x(11))")` | Pipeline erfolgreich, History=1, PrintMessage=`out: 0x(11)`, Status=`Line successfull parsed`; Editortext unverändert, weil der View-Ausgabeabschluss bei diesem Direktaufruf nicht beteiligt ist. |
| Unveränderter AvaloniaEdit-TextEditor ohne Workbench-Grenze, Headless-Key.Return | `plain` → `plain\r\n`, TextEntering=1. Native AvaloniaEdit-12-Enterverarbeitung funktioniert in dieser Kontrolle. |

Belege: [probe-instrumented.log](../../artifacts/ap3-02-n1-d1/probe-instrumented.log), [probe-clean.log](../../artifacts/ap3-02-n1-d1/probe-clean.log). Beide enden mit `ALL SIX DIAGNOSTIC CASES PASSED`. Dies bedeutet bestätigte Diagnoseerwartungen einschließlich der reproduzierten Blockade, **keine bestandene native Historyabnahme**.

Die positiven instrumentierten Fälle protokollieren ExecuteInputAsync → ParseLineAsync (IsExecuting=false) → ExecuteLineAsync → RunAsync → angenommenen Pipelinecallback → BeginLine/AddLine → Pipeline.Succeeded=true → Ausgabeabschluss. Danach Active=false, Pending=0, ObserverDepth=0, Q frei, IsInUpdate=false.

Der permanente Test `EditorAutomationTests.ActualReturnKeepsPendingExecutionAnchorOutputAndRevision` verwendet tatsächlich `TextArea.PerformTextInput("\n")`. Er deckt die erfolgreiche direkte Textverarbeitung ab, **nicht** Key.Return → EnterParagraphBreak. Diese Differenz erklärt, warum ein positiver Test dieses Namens den nativen Blocker nicht ausschließt.

## 7. Architekturabgrenzung – D5

| Fläche | Bewertung |
| --- | --- |
| Windows-/OS-Automation | Zustellung belegt; kein notwendiger Erklärungsansatz für die gemessene Blockade. Physischer Vergleich bleibt OPEN. |
| AvaloniaEdit 12 | Native OnEnter-Route belegt; unveränderter Editor besteht Kontrollfall. Kein Frameworkfix aus dieser Diagnose abgeleitet. |
| AP2.1 Coordinator/TextArea | **Bestätigte Ursache:** legitimer nativer Command/TextEntering-Übergang scheitert an erneuter Startprüfung unter Originalticket. |
| AP3-02 History | Capability/NavigateHistory nicht beteiligt. Geschlossene IR-/RR-Findings bleiben in ihrem Reviewumfang erhalten; natives History-Gate bleibt blockiert. |
| AP3-P8 GUI-Ausführungsadmission | Nachgelagerte Fläche; wurde im nativen Fehlerlauf nicht erreicht. Positive Direktproben geben keine allgemeine P8-Freigabe. |
| AP5 Return-/Ausführungsabschluss | Der Abschluss wird im nativen Fehlerlauf nicht erreicht. Kein Auftrag, dessen Semantik vorzuziehen. |

Der offene allgemeine Observer-Rekursionspunkt aus dem F2-Review bleibt unverändert AP3-P6 zugeordnet. Kein unbeschränkter Rekursionstest und keine Freigabe dieser Grenze.

## 8. Minimaler Korrekturvorschlag und nächster Auftrag

Empfehlung: separater, begrenzter Auftrag zur **nativen Enter-Command-/TextEntering-Integration innerhalb des bereits zugelassenen Originaltickets**. Ziel ist, genau den legitimen synchronen Übergang vor dem ersten Observer zu erlauben, ohne allgemeine Reentrancyberechtigung zu schaffen. Das konkrete Design muss diesen Nachweis erbringen; eine pauschale Freigabe bei `Active=true`, ein ambient Bypass oder Abschwächen von CanStartGui wäre kein zulässiger Fix. Die History-Capability sollte dafür nicht zweckentfremdet werden.

Abnahmekriterien für diesen Folgeauftrag:

1. Echter Headless-Key.Return/KeyUp mit vollständigem Template/Fokus erreicht genau einmal den View-Ausführungspfad; direkte PerformTextInput- und Routed-TextInput-Fälle bleiben erhalten.
2. Return und KP_Enter erreichen in der nativen Desktopinstanz genau eine Ausführung und einen Historyeintrag, ohne doppelte Ausgabe. Physisches Enter separat prüfen oder weiter OPEN ausweisen.
3. Fremde TextEntering-/TextEntered-/Dokumentcallbacks erhalten keine zusätzliche Schreib- oder Admissionberechtigung. Wiederverwendung, fremde Tickets, Dokument-/Generationswechsel und Callbackfehler gezielt regressieren.
4. Erfolgs-, Ablehnungs- und Fehlerpfade schließen Originalticket, Locks und native Gruppen korrekt; D01 bleibt konservativ. Keine automatische Wiederholung.
5. Bestehende Coordinator-/History-/Capabilityregressionen unverändert bestehen lassen. Danach N1–N4 des nativen Historyauftrags erneut ausführen; keine AP3-02-Freigabe allein aus den Diagnosegegenproben.

Nicht im vorgeschlagenen Auftrag: allgemeine P8-Ausführungsadmission, AP5-Abschlusssemantik, neue Undo-Engine, Core-/Prism-/Paketänderungen oder IME-Änderungen. Falls die eng begrenzte Reparatur eine dieser Flächen voraussetzt, ist diese Abhängigkeit zuerst als Entscheidungspunkt vorzulegen.

## 9. Reproduktion, Erhaltung und Stop-Gate

Lokale Diagnoseartefakte liegen unter `artifacts/ap3-02-n1-d1/` und sind ignoriert. `instrument.ps1` erzeugt Quellkopien; `instrument.targets` bindet sie nur bei explizitem `-p:CustomAfterMicrosoftCommonTargets=<absoluter Pfad>/instrument.targets` ein. Für einen reproduzierten Diagnosebuild zuerst das vorhandene `eng/prism/Enter-Prism.ps1` laden, dann das Desktopprojekt Debug mit diesem Import bauen. Reguläre Wiederherstellung erfolgt mit `eng/prism/Build.ps1 -Configuration Debug` ohne Import. Die ursprüngliche Instrumentierungs-DLL für die sechs Gegenproben ist unter `instrumented/` gesichert.

`probe.csproj` referenziert vorhandene GUI-Testabhängigkeiten; das unabhängige Programm benötigt deren Windows-x64-Nativbibliotheken im Ausgabeverzeichnis. Über `-p:D1Product=<Pfad zur regulären CryptoWorkBenchAvalonia.dll>` lassen sich dieselben Fälle gegen den Produktbuild ausführen. Der gültige Clean-Durchlauf wurde genau so durchgeführt. Die Logs sind Diagnoseprotokolle, keine TRX-Gesamtsuite.

Nach Abschluss: **123/123 Dateien des Vorhermanifests unverändert**, keine fehlend, keine SHA-256-Abweichung. Gesichert sind `before.xml`, `before.patch`, `status-before.txt` und `preservation-after.txt`/`status-after.txt`. Der einzige neue nicht ignorierte Eintrag ist dieser Bericht. Der gesamte bestehende uncommittete AP3-Patch bleibt erhalten. Produktquellen und permanente Tests wurden zu keinem Zeitpunkt instrumentiert oder geändert; nur erzeugte Diagnosebinärdateien wurden anschließend durch den regulären Build ersetzt. Andere Worktrees und `.codex/config.toml` blieben unberührt; die Konfigurationsdatei wurde nicht gelesen.

**ROOT CAUSE CONFIRMED** für den untersuchten nativen Enter-Blocker. Keine Produktkorrektur, kein Commit/Push/Merge, keine automatische Fortsetzung mit der Korrektur oder AP3-03. Die native Historyabnahme und der physische Tastaturvergleich bleiben offen.
