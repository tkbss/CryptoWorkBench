# MCP AP3-02-N1-F1 – Native Enter-Command-/TextEntering-Integration

Datum: 2026-10-29. **READY FOR INDEPENDENT REVIEW.**

Die legitime native Enter-Fortsetzung erreicht jetzt unter dem ursprünglichen Coordinator-Ticket genau einmal den bestehenden View-/Ausführungspfad. Der permanente native Key.Return-Solltest wurde vor der Produktkorrektur rot und danach mit unveränderten Assertions grün ausgeführt. Die abschließende GUI-Suite besteht mit **589/589 Fällen**, darunter 36 neue Enterfälle. Return und KP_Enter wurden zusätzlich im aktuellen Windows-Debug-Build erfolgreich geprüft. Dies ist keine vollständige N1–N4-Historyabnahme.

## 1. Ausgangsstand und exakter neuer Patchumfang

Worktree `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp-ap3`; Branch `feature/mcp-ap3`; HEAD unverändert `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Ausgangspunkt ist der bestehende uncommittete AP3-02/F1/F2-Patch und der [D1-Diagnosebericht](MCP-AP3-02-N1-D1-enter-diagnostic.md).

Windows `10.0.26200.0`, SDK `10.0.401`, Debug/net10.0, Avalonia 12.1.4, AvaloniaEdit 12.0.0. Prism bleibt als genehmigte Quellreferenz am verifizierten Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` eingebunden. Keine Paket-, Prism-, Core-, IME- oder Drag-Drop-Änderung.

Nur folgende Änderungen gehören zu **diesem** Auftrag:

| Datei | Neuer Umfang |
| --- | --- |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | Private, ereignisgebundene Enter-Delegation; spezifischer Dispatch für die zwei nativen Entercommands; Verbrauch und Nachvalidierung vor nativer Einfügung. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` | Zusätzliche reine Validierungsfunktion `IsActiveGuiOperation(ticket)`; keine Änderung von CanBegin, Commit, Observerregeln oder HistoryCapability. |
| `CryptoWorkBenchGuiTest/NativeEnterContinuationTests.cs` | Neue permanente Regression mit 36 Fällen. |
| Dieser Bericht | Implementierungs-, Test- und Erhaltungsnachweis. |

Die Änderungen an WorkbenchTextEditor, HistoryService und CryptoScriptEditViewModel sowie die bisherigen Historytests/-berichte stammen aus den vorherigen Aufträgen und wurden erhalten.

Vorhermanifest: 124 relevante Bestandsdateien. Abschließend sind davon nur die beiden oben genannten Produktdateien verändert, 122 unverändert. Alle 270 Dateien des zusätzlich gesicherten D1-Artefaktmanifests sind unverändert, einschließlich Original-Traces und Diagnoseprogramme. Die HistoryCapability wurde zusätzlich als Quellabschnitt mit der erhaltenen D1-Kopie identisch abgeglichen. Keine vorhandene Testassertion wurde geändert.

## 2. Tatsächliche AvaloniaEdit-12-Route

Untersucht wurde die lokal integrierte net10.0-DLL, SHA-256 `2CE24E1C535EDD851D06651FD0EA8515CF49E994CBA264502FA50DB89779CEAD`. Der IL-Nachweis unter [native-il.txt](../../artifacts/ap3-02-n1-f1/native-il.txt) zeigt:

- `EditingCommandHandler` registriert sowohl `EnterParagraphBreak` als auch `EnterLineBreak` auf denselben `OnEnter`-Handler.
- Dieser ermittelt die TextArea, prüft `IsFocused`, ruft `PerformTextInput("\n")` auf und markiert das Commandargument als behandelt.
- `PerformTextInput(string)` erzeugt ein TextInputEventArgs mit Text und RoutedEvent und ruft die EventArgs-Überladung auf.
- Diese prüft Argument/Dokument und ruft als ersten erweiterbaren Übergang `OnTextEntering` auf. Erst danach folgen Handled-Prüfung, native Newline-Ersetzung, TextEntered und Caret-Präsentation.

Die Korrektur delegiert innerhalb der bereits behandelten äußeren Command-Klammer diesen konkreten nativen PerformTextInput-Schritt. Sie **redispatcht Enter nicht erneut als RoutedCommand**: Ein solcher Redispatch würde frühe Commandcallbacks zwischen Berechtigungserzeugung und Verbrauch zulassen. Native KeyBindings, Commandobjekte, CanExecute-Registrierungen und Undo-/Redo-Routen bleiben bestehen. Der Fokuscheck und der Textwert entsprechen dem untersuchten OnEnter. Diese enge Delegation ist an AvaloniaEdit 12.0.0 gebunden und bei einer späteren Änderung dieses Handlers erneut zu prüfen.

## 3. Originalticketbindung, Verbrauch und Invalidierung

1. Die vorhandene Commandgrenze prüft `Allowed`. Nur EnterParagraphBreak/EnterLineBreak gelangen in die private `ExecuteEnter`-Methode.
2. `TryBegin` und `Commit` führen die bisherigen Admission-/Revalidierungsprüfungen aus. Erst im erfolgreich zugelassenen Commit-Body entsteht die Delegation. Es wird kein zweites Ticket angefordert und keine Q-Reservierung neu eingeführt.
3. Eine private Closure bindet das konkrete OperationTicket, Coordinator, Editor, Originaldokument sowie ein frisch erzeugtes **konkretes TextInputEventArgs-Objekt** mit Text `\n`.
4. Unmittelbar danach wird `base.PerformTextInput(input)` aufgerufen. Zwischen Installation und diesem Aufruf liegen weder routed Commanddispatch noch fremde Observer, Mutation oder asynchrone Fortsetzung. Die native Argument-/Dokumentprüfung vor OnTextEntering enthält gemäß IL keinen fremden Callback.
5. OnTextEntering nimmt die private Delegation aus dem Feld und setzt das Feld **vor** ihrem Aufruf auf null. Die Closure prüft Referenzidentität des Ereignisses, Text, Handled, aktives Originalticket, ObserverDepth=0, IsCurrent einschließlich Revision/Generation/Registrierung, Editor-/Dokumentidentität und Locks. Danach wird ihre lokale Gültigkeit endgültig verbraucht, bevor `base.OnTextEntering` einen Subscriber aufrufen kann.
6. Nach den TextEntering-Observern werden Textwert und Originalzustand erneut geprüft. Änderungen des Ereignistextes, Generationinvalidierung, Detach oder Releasing verhindern die anschließende native Einfügung durch Handled=true.
7. Das äußere finally entwertet auch eine unbenutzte oder fehlgeschlagene Delegation und leert das Feld. Exceptions bleiben im vorhandenen Commit-/D01-Abschluss. Es gibt keinen Retry.

`IsActiveGuiOperation` ist ausschließlich eine Prüfung auf `ReferenceEquals(active, ticket)`, ObserverDepth=0 und IsCurrent; sie erzeugt und erweitert keine Autorität. Es existiert keine neue AsyncLocal-/ThreadStatic-/globale Berechtigung. Das lokale Gültigkeitsbit ist kein allgemeiner Bypass: Ohne das gebundene Ticket **und dasselbe Ereignisobjekt** kann es keinen Aufruf zulassen. Fremde Ereignisse würden die Delegation zudem verbrauchen und abgewiesen werden.

## 4. Permanenter Red-/Green-Nachweis

Test: `NativeReturnExecutesExactlyOnceThroughTheCompleteView` in der neuen Testdatei. Vollständige CryptoScriptEditView mit echtem ViewModel/Service/History, Fluent-/AvaloniaEdit-Template, Layout und geprüftem TextArea-Fokus. Script: `Print(0x(11))`. Eingang: Headless `KeyPress/KeyRelease(Key.Return, PhysicalKey.Enter)`, anschließend Await von `View.PendingExecution`.

Unveränderte positive Sollassertions: TextEntering=1, TextEntered=1, genau ein neuer PendingExecution-Task im View-Event, History.Count=1, genau eine sichtbare Ausgabe `out: 0x(11)`, PrintMessage und regulärer Erfolgsstatus, geschlossene Coordinator-/Queue-/Lockzustände.

| Lauf | Tatsächliches Ergebnis |
| --- | --- |
| Vor Produktkorrektur: `red.trx` | 1 ausgeführt, 1 fehlgeschlagen, 0 übersprungen. Erste Sollassertion: Expected 1 / But was 0 für TextEntering. |
| Nach Produktkorrektur: `green.trx` | Derselbe Test: 1 ausgeführt, 1 bestanden. |
| Endgültige Suite | Derselbe Test erneut bestanden; die ursprünglichen Assertions unverändert. |

Beim ersten Green-Lauf war die gesamte neue Testdatei noch byteidentisch zum Red-Lauf (SHA-256 `B993D7F11D87FB5395E74C863826E2D823E1C7295C8CE9031AF721F9399A7BBF`). Danach wurden weitere Tests/Imports ergänzt. Das unveränderte ursprüngliche Testdatei-Präfix wurde als `red-original-test.cs.txt` rekonstruiert und gegen diesen Hash erfolgreich geprüft. Keine alten Tests wurden zur Herstellung eines Green-Ergebnisses angepasst.

## 5. Varianten und Einmaligkeit

Die 36 neuen Fälle decken ab:

| Gruppe | Fälle / Wirkung |
| --- | --- |
| Vollständiger View-E2E | Key.Return; physischer Headless-Keycode NumPadEnter; Shift+Enter; beide Entercommandobjekte; direktes PerformTextInput-Newline; Routed TextInput-Newline. Je genau ein TextEntering-/TextEntered-Zyklus, ein Ausführungstask, ein Historyeintrag und eine Print-Ausgabe. |
| Gewöhnlicher Text | Routed Unicode `äöü € ê`: genau eine Änderung; keine History-/Scriptwirkung. |
| Originalticket | Derselbe Ticketverweis in TextEntering, Document.Changing und TextEntered; jeweils Pending=1; genau ein äußerer Entercommand. Delegationsfeld bereits beim ersten Observer null. Abgeschlossenes Ticket autorisiert auch unter einer späteren fremden GUI-Operation keine weitere Texteingabe. |
| Native Textwirkung ohne Interpreter | Ein Enter erzeugt genau einen Newline-Commit. Ein Undo stellt den Ausgangstext her und leert die Undo-Möglichkeit; Redo stellt exakt den Newline wieder her. |

NumPadEnter/Shift sind hier Headless-Eingabevarianten mit getrennten physischen Codes beziehungsweise Modifiers. Sie werden nicht als physische Tastaturbetätigung bezeichnet. Return und KP_Enter erhielten zusätzlich den Windows-Nachweis in Abschnitt 9.

## 6. Adversariale Callbacks und Fehler

Die früheste erreichbare fremde TextEntering-Callbackstelle liegt **nach** dem Delegationsverbrauch. Von dort sowie aus TextEntered, UpdateStarted, Changing, Changed, UpdateFinished, Caret.PositionChanged und IsModified-PropertyChanged werden fremdes `PerformTextInput("FOREIGN")`, weiteres Newline und ein EnterLineBreak-Command versucht. Ergebnis jeweils: nur die ursprüngliche Newline, genau ein Ereigniszyklus, keine weitere Admission/Historywirkung. Die Callbacks müssen tatsächlich erreicht worden sein; ihre Aufrufzahlen werden geprüft.

Weitere Fälle:

- Reentrante Wiederverwendung desselben TextInputEventArgs wird abgewiesen; dessen Handled-Markierung bricht auch die ursprüngliche Einfügung konservativ ab.
- Änderung des delegierten Textwertes in einem Observer erlaubt keine fremde Textersetzung.
- Exception in der nativen CanExecute-Callbackkette vor Admission lässt Text und History unverändert, erzeugt kein Ticket/Receipt und hinterlässt keine Delegation; ein anschließender normaler Enter funktioniert.
- Exceptions in TextEntering, Changing, Changed und TextEntered prüfen Fehler vor nativer Textwirkung, während der Mutation und nach dem berechtigten Übergang. Nach Fehler ist die Delegation null; zusätzliche Eingabe bleibt gesperrt.
- Dokument-/Editorwechsel sowie direkter Dokumentwrite innerhalb eines Coordinator-Observers werden abgewiesen; ursprünglicher Editor und Nachfolgertext bleiben identifizierbar, CallbackFailed wird festgehalten.

Abgrenzung: Diese Tests prüfen fremde **TextInput-/Commandfortsetzungen** auch aus rohen nativen Events und direkte Dokumentwrites innerhalb der bestehenden `Observe`-Grenze. Sie begründen keine allgemeine Absicherung beliebiger unmediierter Dokumentwrites in allen Produktcallbacks. Diese allgemeine Observerintegration einschließlich des bekannten synthetischen Rekursionspunkts bleibt AP3-P6.

## 7. D01, Lifecycle, Undo und Ausführungspipeline

Generationinvalidierung, Detach und Releasing aus TextEntering verhindern die noch ausstehende native Einfügung. Ohne gültige Autorität (aktive fremde Operation, Releasing, Faulted, MutationLock oder IdentityLock) erreicht Enter keinen Textzyklus und keine Dokument-/Historywirkung. Die Lifecycle-/Fehlerproben ohne View-Ausführungshandler isolieren bewusst den synchronen Editorpfad.

Alle ausgeführten Fehlerfälle schließen das ursprüngliche Ticket und ihre tatsächlich geöffneten nativen Gruppen: Active=false, Pending=0, ObserverDepth=0, IsInUpdate=false, keine eigenen offenen Locks. Bei entwichenen nativen Callbackexceptions bleibt die Phase Faulted; TextEffect/HistoryEffect werden konservativ Indeterminate und Finalization=Faulted. Nach Changed/TextEntered kann der Newline bereits vorhanden sein; vollständige Wirkungslosigkeit wird dort ausdrücklich nicht behauptet. Keine Recoverygruppe, Undo-Engine oder Wiederholung hinzugefügt.

Der erfolgreiche vollständige View-Pfad bleibt unverändert: View.TextEntering → ExecuteInputAsync → ParseLineAsync → ExecuteLineAsync → bestehendes RunAsync/Registry-/Queue-Gate → BeginLine/AddLine → Interpreter/Print-Präsentation. Der Test zählt den neuen PendingExecution-Task, wartet ihn ab und prüft History, Ausgabe und freigegebene Queue. Der originale Return-Anchor und die asynchrone Ausgabeimplementierung wurden nicht verändert. Im erfolgreichen nativen E2E-Lauf wurde kein zusätzlicher P8-/AP5-Blocker beobachtet.

## 8. Regressionen, Builds und TRX-Prüfung

Abschließende Evidenz: [gui-full-final.trx](../../artifacts/ap3-02-n1-f1/gui-full-final.trx), `gui-full-final.log`, `build-final.log`. Counters und tatsächliche UnitTestResults wurden abgeglichen: **589 Results, alle Passed; total/executed/passed=589**, failed/error/timeout/aborted/inconclusive/notExecuted/notRunnable=0. Keine Skips. Die TestDefinitions ordnen die Fälle den Klassen zu.

| Teilmenge der Gesamtsuite | Bestanden |
| --- | ---: |
| NativeEnterContinuationTests | 36 |
| EditorCoordinatorTests | 35 |
| EditorAutomationTests | 47 |
| HistoryCoordinatorTests | 23 |
| HistoryCorrectionTests | 27 |
| HistoryCapabilityTests | 2 |
| HistoryCapabilityAdversarialTests | 11 |
| Weitere bisherige GUI-Fälle | 408 |
| Gesamt | 589 |

Damit bestehen alle bisherigen 553 Fälle plus 36 neue Fälle. Der separate erste Grenztestlauf enthielt 35 Fälle; der explizite Verbrauchs-/Fremdticketnachweis wurde anschließend ergänzt und in der abschließenden Gesamtsuite geprüft. Frühere Fehler während des Aufbaus neuer Testfixtures wurden korrigiert; der verbindliche ursprüngliche Rotfall blieb unverändert.

Beide Lösungen wurden über das bestehende `eng/prism/Build.ps1 -Configuration Debug` erfolgreich gebaut. Sowohl der erste als auch der abschließende vollständige Build meldeten 87 Warnungen/0 Fehler für die GUI-Lösung und 0/0 für die zweite Lösung; die Protokolle sind als `build.log` und `build-final.log` erhalten. Keine Paket- oder Quelländerung zur Warnungsbereinigung vorgenommen.

Die früheren 270 unabhängigen History-Semantikvergleiche wurden nicht erneut ausgeführt. Stattdessen wurde entsprechend Auftrag nachgewiesen, dass HistoryService, Historypfad im ViewModel, WorkbenchTextEditor einschließlich Remove-/Insert-Helfer und die F2-HistoryCapability unverändert sind. Die permanenten 63 History-/Capabilityfälle sind zusätzlich in der Gesamtsuite erfolgreich.

## 9. Native Windows-Gegenprüfung

Frische, nicht instrumentierte Desktopinstanz aus dem aktuellen Debug-Verzeichnis. Produkt-DLL-SHA-256: `CF5DB2D9B18138FD46EAD9F72B6EB2C427600740E704261B7415B53EF401D33B`. Keine vorhandene Benutzerinstanz oder Datei verwendet. Der Computer-Use-Skill lieferte OS-Automation; `type_text` setzt den harmlosen Testtext ein, Return/KP_Enter werden getrennt als Tasten gesendet. Nach einem Eingabehinweis wurde das Fenster neu beobachtet und vor Fortsetzung wieder eindeutig aktiviert/fokussiert.

| Schritt | Sichtbarer Vorher-/Nachherzustand |
| --- | --- |
| `Print(0x(11))` + Return | Vorher eine Scriptzeile; danach genau eine `out: 0x(11)`-Zeile und leere Zeile 3, Caret 3:1, Erfolgsstatus. |
| Ein Alt+PageUp nach erstem Enter | Leere Zeile 3 bleibt leer; kein zweiter identischer Historyeintrag erreichbar. |
| `Print(0x(22))` in Zeile 3 + KP_Enter | Genau eine zusätzliche `out: 0x(22)`-Zeile, Caret auf leerer Zeile 5; erste Ausgabe weiterhin einmal vorhanden. |
| Alt+PageUp | Zeile 5 wird `Print(0x(11))`, nicht erneut `Print(0x(22))`; beide Ausgaben bleiben unverändert. |
| Alt+PageDown | Zeile 5 wird `Print(0x(22))`, ohne weitere Ausgabe. |

Diese begrenzte Gegenprobe belegt die erwarteten zwei Historyeinträge und unterscheidet die hier relevanten Doppelregistrierungen. Interne native Zähler wurden nicht instrumentiert; die exakten Zähler sind separat im Headless-E2E gemessen. Es wurden keine Serviceaufrufe als Ersatz für die nativen Gesten verwendet.

Screenshots: [Return](../../artifacts/ap3-02-n1-f1/native-return.jpg), [erste Historygrenze](../../artifacts/ap3-02-n1-f1/native-first-history-boundary.jpg), [KP_Enter](../../artifacts/ap3-02-n1-f1/native-kp-enter.jpg), [erster Historytext](../../artifacts/ap3-02-n1-f1/native-history-first.jpg), [zweiter Historytext](../../artifacts/ap3-02-n1-f1/native-history-second.jpg). Die Testinstanz wurde geschlossen und ihre Abwesenheit bestätigt.

**Physische Tastaturprüfung bleibt OPEN.** Ebenso bleibt die vollständige N1–N4-Abnahme einschließlich History-Undo/Redo, Fokusfolgen und Schweizer AltGr-/Dead-Keyeingaben dem separaten Auftrag nach F1-Review vorbehalten.

## 10. Verbleibende Grenzen und Stop-Gate

Keine neue allgemeine P8-Ausführungsadmission oder AP5-Return-Abschlusslogik implementiert. Die synchronen Lifecycle-Gegenproben sind keine Freigabe beliebiger später asynchroner Präsentationscallbacks. Die allgemeine AP3-P6-Observer-/Rekursionsgrenze, der irreführende bestehende History-Hilfetext und das akzeptierte CJK-IME-Restrisiko bleiben unverändert.

Erhaltungsartefakte unter `artifacts/ap3-02-n1-f1/`: `before.xml`, `d1-before.xml`, `before.patch`, `status-before.txt`, `preservation-after.txt`, `status-after.txt`. D1-Originalartefakte wurden weder neu ausgeführt noch überschrieben. `.codex/config.toml` wurde weder gelesen noch verändert; keine anderen Worktrees geändert.

**READY FOR INDEPENDENT REVIEW** für die begrenzte Enter-Fortsetzungskorrektur. Kein Commit, Push oder Merge; keine weiteren AP3-Pakete und keine automatische vollständige Historyabnahme begonnen.
