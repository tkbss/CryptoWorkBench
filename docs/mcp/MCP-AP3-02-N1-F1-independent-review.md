# MCP AP3-02-N1-F1 – Independent Review

Datum: 2026-10-29. Prüfgegenstand: einmalige native Enter-Fortsetzung, keine Implementierung weiterer AP3-Pakete.

**Entscheidung: READY FOR ACCEPTANCE für den begrenzten N1-F1-Patch.** Kein reproduzierbarer neuer F1-Berechtigungsdurchbruch. Die bestätigte, getrennte P8/AP5-Ausführungslücke IR-N1-01 und die vollständige native History-Abnahme bleiben offen; diese Entscheidung bescheinigt keine vollständige AP3-Autorität über asynchrone Ausführung.

## 1. Ausgangsstand und exakter Patchumfang

Worktree `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp-ap3`, Branch `feature/mcp-ap3`, HEAD `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Alle vorhandenen uncommitteten Änderungen und unversionierten Quellen/Berichte wurden berücksichtigt. Der HEAD allein enthält die geprüfte Implementierung nicht.

Verbindliche Grundlagen: N1-F1-Korrekturbericht, N1-D1-Diagnose, N1-Abnahmebericht, F2-Nachreview sowie R2/AP0/D01. Ältere IR-01, IR-02 und RR-01 bleiben ohne neue Gegenbeweise geschlossen.

Integriert: Avalonia 12.1.4, AvaloniaEdit 12.0.0, DataGrid 12.1.2, .NET SDK 10.0.401, Windows 10.0.26200. Genehmigter Prism-Quellstand `358118cd640d9a22ff8cf21c8ad197fa038b7990`; kein Paket-/Prismwechsel. Dauerhafte frühe IME-Deaktivierung unverändert.

F1 gegenüber seinem erhaltenen Vorherstand:

| Datei | F1-Änderung |
|---|---|
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | 55 zusätzliche Zeilen: private Einmalfortsetzung, Enter-Command-Abzweig, ExecuteEnter |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` | 7 zusätzliche Zeilen: rein prüfendes IsActiveGuiOperation |
| `CryptoWorkBenchGuiTest/NativeEnterContinuationTests.cs` | Neue Klasse mit 36 Fällen |
| `docs/mcp/MCP-AP3-02-N1-F1-enter-correction-review.md` | Neuer Implementierungsbericht |

Die weiteren Produktänderungen an WorkbenchTextEditor, HistoryService, ViewModel und Coordinator sowie vier History-Testdateien stammen aus den vorherigen AP3-Aufträgen. Sie sind Bestandteil des Gesamtpatches, keine F1-Neuerfindung. F1 hatte 122 von 124 vorhandenen manifestierten Dateien unverändert erhalten und genau die zwei obigen Produktdateien geändert. Diagnose-/Prüfprogramme liegen außerhalb der Produktprojekte in ignorierten Artefakten.

Eigene Evidenz liegt unter `artifacts/ap3-02-n1-f1-review/`: `status-before.txt`, `before.patch`, `before.xml`, `evidence-before.xml`, `review-gui.trx`, `trx-summary.json`, `gui.log`, `build.log`, `native-il.txt`, `probe/Program.cs`, `probe-final.log`, native JPEGs und `preservation-final.json`. Vor Testausführung wurden 126 Produkt-/Test-/Berichtsdateien und sämtliche 543 vorhandenen D1-/F1-Artefaktdateien SHA-256-manifestiert. Abschlussabgleich: jeweils null Änderungen oder fehlende Dateien. Nur dieser Bericht und neue isolierte Reviewartefakte kamen hinzu.

Prüfbinärdatei `CryptoWorkBenchAvalonia.dll` SHA-256: `CF5DB2D9B18138FD46EAD9F72B6EB2C427600740E704261B7415B53EF401D33B`. Tatsächlich inspizierte AvaloniaEdit-DLL: `2CE24E1C535EDD851D06651FD0EA8515CF49E994CBA264502FA50DB89779CEAD`.

## 2. Originalticket-Bindung

Prüfstellen: WorkbenchTextArea.cs:25, 77, 105–164; EditorCoordinator.cs:148 und bestehendes IsCurrent/Commit.

ExecuteEnter erzeugt die private Closure erst innerhalb des Commit-Bodys eines erfolgreich erhaltenen TryBegin-Tickets. Kein Eintritt bei aktiver fremder Operation, Releasing, Faulted oder gesetztem Mutation-/IdentityLock. Die Fortsetzung ist weder public noch internal zugänglich; IsActiveGuiOperation erteilt keine Berechtigung, sondern prüft das konkrete aktive Ticket.

IsOriginal kombiniert Referenzidentität von Host, Coordinator, registriertem Editor und Originaldokument mit IsActiveGuiOperation. Dessen IsCurrent kontrolliert Ticketbesitzer, Mitgliedschaft, BodyDone, Generation, Gui-Phase, lebende Bridge-Registrierung/editorId, Dokument und Originalrevision; zusätzlich muss ObserverDepth null sein. Ein fremdes allgemeines GUI-Ticket genügt nicht.

Das konkrete TextInputEventArgs-Objekt muss identisch, Text exakt Newline und Handled false sein. OnTextEntering entfernt zuerst das Feld. Die Closure setzt valid vor base.OnTextEntering auf false. Fremde Subscriber sehen damit bereits verbrauchte Autorität. Nach Rückkehr werden Text und Originalität nochmals geprüft. finally entwertet auch unbenutzte oder durch Exception abgebrochene Fortsetzungen.

Zwischen Installation und erstem Verbrauch führt der tatsächliche lokale PerformTextInput(EventArgs)-IL nur Argumentprüfung und Document-Nullprüfung aus. get_Document ruft AvaloniaObject.GetValue(DocumentProperty) auf; WorkbenchTextArea ist sealed, das registrierte Originaldokument wurde vorher bereits gelesen. In der integrierten konkreten Konfiguration liegt hier kein fremder Text-/Dokumentobserver-Aufruf. Die Aussage gilt für diese Assembly und Registrierung, nicht als allgemeine Garantie für beliebige zukünftige Avalonia-Propertyimplementierungen. IL-Reproduktion: `inspect/Program.cs`, Ausgabe `native-il.txt`.

## 3. Native AvaloniaEdit-Enter-Semantik

Beide nativen Commands, EnterParagraphBreak und EnterLineBreak, sind in der tatsächlichen 12.0.0-Assembly an denselben OnEnter-Handler gebunden. Dieser holt die TextArea, prüft IsFocused, ruft PerformTextInput("\n") auf und setzt das Commandereignis auf Handled.

F1 bildet diese Schritte ab: vorhandene native Registrierung/CanExecute bleiben, der Wrapper setzt Handled, ExecuteEnter prüft den Fokus und liefert genau das Newline-Event an die originale native PerformTextInput(EventArgs)-Implementierung. Die Stringüberladung erzeugt in der Originalassembly gerade ein solches Event mit TextInputEvent. Der native OnEnter-Delegate wird im speziellen Zweig nicht nochmals aufgerufen; dadurch entfällt eine zweite allgemeine Command-Redispatchrunde. Dies ist die bewusste enge Integration, nicht eine neue Newline-/Undo-Engine.

TextEntering, dessen Handled-Abbruch, native Newline-Ersetzung, TextEntered und Caret.BringCaretToView verbleiben im nativen PerformTextInput. Normaler Routed TextInput und direktes PerformTextInput behalten ihren bisherigen eigenen Admissionpfad. Shift+Enter, Nummernblock, beide Commands, Perform und Routed Newline wurden in den permanenten Tests erneut ausgeführt; CanExecute-Exception vor Admission, Fokus/Locks und gewöhnliche Unicode-Eingabe sind mit abgedeckt.

Zusätzlich 16 unabhängige Vergleiche gegen ein unverändertes AvaloniaEdit.TextEditor-Control: beide Entercommands × acht Texte/Selections (leer, ASCII, CRLF, LF, ä/Surrogatpaar, vollständige/teilweise Auswahl). Verglichen wurden Text, Caret, Selection, Before-/AfterInsertion-Anchors an jedem UTF-16-Offset sowie CanUndo/CanRedo und Zustände nach Undo und Redo. Alle 16 identisch. Eine native Entereinfügung bildet eine Undo-Gruppe; die spätere Serviceausgabe ist eine gesonderte Wirkung.

Paketwechsel sind ein echter Wartungspunkt: OnEnter, Bindings, Eventerzeugung und Reihenfolge vor OnTextEntering müssen dann erneut gegen die tatsächlich integrierte Assembly geprüft werden. Kein Update in diesem Review.

## 4. Berechtigungsverbrauch und Reentrancy

Unabhängiges Prüfprogramm mit echter View/ViewModel/Service-Pipeline, Headless-Window, angewandtem Template und geprüftem Fokus. Es verändert keine Produktdatei oder permanente Assertion. Ausführung über `dotnet run --project artifacts/ap3-02-n1-f1-review/probe/probe.csproj -c Debug`; endgültiger Lauf: **72 abgeschlossene Fälle**, keine Assertion fehlgeschlagen.

40 davon kombinieren acht echte Callbackgrenzen (TextEntering, TextEntered, UpdateStarted, Changing, Changed, UpdateFinished, Caret.PositionChanged, IsModified-PropertyChanged) mit fünf Angriffen: FOREIGN, Newline, erneutes EnterLineBreak, Replay desselben Events und anderes Event mit identischem Text. Jeder Callback wurde tatsächlich erreicht; geprüft wurde auch das bereits leere Fortsetzungsfeld.

Ergebnis: genau ein ursprüngliches TextEntering, ein PendingExecution-Wechsel, ein Historyeintrag, eine Print-Ausgabe, kein FOREIGN, kein hängenbleibendes Ticket/Q. Kein Callback erhielt ein zweites Schreibrecht. Same-event-Replay kann das originale Event auf Handled setzen und dadurch dessen Newline verhindern; die bereits angenommene Serviceausführung ist davon getrennt (Abschnitte 5/6). Das ist keine erfolgreiche zweite Delegation.

Verbrauchte/abgelaufene Delegation unter neuem Ticket, Identitätswechsel, Observer-Schreibversuch, Locks und nicht verfügbare Kontrollphasen sind zusätzlich durch die unverändert erneut ausgeführten 36 permanenten Fälle belegt. Die unabhängige Fremdticketprobe erreicht kein TextEntering und erzeugt weder Text noch History noch Ausführungsauftrag.

## 5. Fehler-, D01- und Lifecyclematrix

Die übrigen unabhängigen Fälle bestehen aus 16 Lifecycle-/Fehler-/Asyncfällen und den 16 Semantikvergleichen. Zustände wurden unmittelbar nach dem nativen Key und nach Await von PendingExecution getrennt protokolliert.

| Eingriff | Synchrones Enter | Serviceabschluss / tatsächliche Wirkung |
|---|---|---|
| Später TextEntering-Subscriber setzt Handled oder Text=FOREIGN | Kein Newline, kein TextEntered, Receipt None/None/Complete | Ein ursprünglicher Auftrag läuft weiter, ein Historyeintrag und eine Ausgabe; kein FOREIGN |
| Generationinvalidierung in TextEntering | Originalnewline verworfen, Revision synchron unverändert | Ein angenommener Auftrag und Ausgabe laufen weiter: IR-N1-01 |
| Detach in TextEntering | Kein Newline, Phase Unavailable | History kann entstehen; kein späterer Dokumentinsert |
| BeginDrain in TextEntering | Kein Newline, Releasing | Service kann rechnen/History schreiben; Ausgabeinsert abgewiesen, Status Script execution failed |
| Exception in TextEntering oder Changing | Im beobachteten Lauf keine Textänderung | Conservative Indeterminate/Indeterminate/Faulted, FinalizationFailed; bereits angenommener Serviceauftrag kann History/PrintMessage erzeugen, Ausgabeinsert gesperrt |
| Exception in Changed oder TextEntered | Newline bereits vorhanden | Gleiches konservatives Faulted-Ergebnis; keine Behauptung vollständiger Wirkungslosigkeit |
| Dokumentersetzung nach synchronem Key | Originalnewline bleibt | Nach Await weder Ausgabe in altes Dokument noch in SUCCESSOR; Nachfolgedokument unverändert |
| Detach nach synchronem Key | Originalnewline bleibt | Kein verspäteter Dokumentinsert |
| Dispose in TextEntering | Newline verworfen, Unavailable | Deterministisch Script execution cancelled; Historyeintrag möglich, kein Print und kein Dokumentinsert |
| Dispose unmittelbar nach Key | Originalnewline möglich | Erfolgsabschluss kann Cancel überholen; kein deterministischer Cancel-Nachweis allein aus diesem Fall |
| Parserfehler | Ein nativer Newline | Ein Auftrag/Historyeintrag, Diagnosestatus, keine Print-Ausgabe; Q frei |
| Fremdes allgemeines GUI-Ticket | Kein Eintritt | Keine Ausführung/History/Textwirkung |
| Bereits Handled direktes Event | View überspringt Ausführung | Kein Auftrag, kein Historyeintrag, keine Textänderung |

Alle endgültigen Probezustände: Active false, Pending 0, Q frei, IsExecuting false, keine offene native Updategruppe. Die Fortsetzung wird auch bei Exceptions endgültig entfernt. Vor Textänderung kann eine native Updategruppe bereits begonnen haben; deshalb wird aus unverändertem Text ausdrücklich nicht auf fehlende Undo-/Historywirkung geschlossen. D01 bleibt konservativ.

Die synchronen Receipts beziehen sich auf das Enterticket. Ein None/None/Complete-Receipt ist keine Aussage, dass später auch der serviceeigene Auftrag ohne Wirkung geblieben sei. Nach asynchroner Ausgabe kann die aktuelle Revision weiterlaufen, während das unveränderte Receipt weiterhin seine ursprüngliche beobachtete Revision nennt.

## 6. Asynchrone Ausführungsübergabe

Tatsächliche Kette: Return → nativer Command → ursprüngliches Ticket → TextEntering → View.cs:104 PendingExecution=ExecuteInputAsync → ParseLineAsync → Automation.RunAsync → BeginLine/AddLine → Interpreter → Präsentation. Der Viewhandler erstellt den Return-Anchor mit BeforeInsertion am Ende der ursprünglichen Zeile. F1 verändert weder diesen Anchor noch die gemeinsame Interpreterpipeline/globalen Variablen.

Der Handler startet den Task bereits während TextEntering, also bevor spätere Subscriber und die nachgelagerte IsOriginal-Prüfung abgeschlossen sind. Der Serviceauftrag besitzt seine eigene Queue-/Execution-Behandlung. Die private Enterclosure ist dann schon verbraucht und wird weder an Task noch Service weitergegeben. Der ursprüngliche native Ticketabschluss ist synchron; Q kann danach noch bis zum Serviceabschluss belegt sein.

Normale vollständige Viewfälle belegen jeweils einen beobachteten PendingExecution-Wechsel, einen Historyeintrag und eine Ausgabe. Der Quellpfad und die Ticketassertionen zeigen kein zweites natives Enterticket. Bei Dokumentersetzung/Lifecyclewechsel verhindert der bestehende Viewvergleich den getesteten verspäteten Insert. Er prüft aber keine Coordinator-Generation und macht den Serviceauftrag nicht rückwirkend abhängig von späterem Handled. Diese getrennte Lücke ist unten ausdrücklich dokumentiert.

## 7. Red-/Green-Integrität

Erhaltenes F1 `red.trx`: 1 ausgeführt, 1 fehlgeschlagen, erwartetes TextEntering=1, tatsächlich 0. `green.trx`: derselbe Fall erfolgreich. SHA-256 des gesicherten ursprünglichen Testquelltexts: `B993D7F11D87FB5395E74C863826E2D823E1C7295C8CE9031AF721F9399A7BBF`.

Unabhängiger Quellvergleich: ursprünglicher Klassenpräfix bis vor die späteren Zusatzfälle, ohne deren drei zusätzliche using-Zeilen, stimmt exakt mit `red-original-test.cs.txt` überein (`red-integrity.txt`: True). Assertions für genau ein TextEntering, PendingExecution, History, Ausgabe und freien Abschluss wurden nicht abgeschwächt. Der Originalfall ist in den erneut grünen 36 Fällen enthalten. Die historische Rotphase wurde aus erhaltenem TRX und Quellstand geprüft; kein Produktrollback zur Wiederholung des alten Fehlers vorgenommen.

## 8. Build-/GUI-/TRX-Ergebnisse

Erneut ausgeführt nach Laden des genehmigten Prism-Environments:

- `dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Debug --logger "trx;LogFileName=review-gui.trx" --results-directory artifacts/ap3-02-n1-f1-review`
- `pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug`

| Umfang | Tatsächliche Ergebnisse |
|---|---:|
| NativeEnterContinuationTests | 36 bestanden |
| EditorCoordinatorTests | 35 bestanden |
| EditorAutomationTests | 47 bestanden |
| HistoryCoordinatorTests | 23 bestanden |
| HistoryCorrectionTests | 27 bestanden |
| HistoryCapabilityTests / AdversarialTests | 2 + 11 bestanden |
| Übrige GUI-Fälle | 408 bestanden |
| Gesamtsuite | **589 bestanden, 0 Fehler, 0 Skips** |

Die Teilmengen werden nicht zusätzlich zur Gesamtsuite gezählt. TRX enthält 589 tatsächliche UnitTestResults mit 589 eindeutigen executionIds, 580 Definitionen und null nicht auflösbaren testIds. Die Differenz sind wiederholte OpenInfoLink_Padding_ShowsDocument (9 Ergebnisse) und OpenInfoLink_Mechanism_ShowsDocument (2 Ergebnisse), zusammen neun zusätzliche Resultate gegenüber den Definitionen. Keine verdeckten fehlenden Tests. Vollständige Zuordnung in `trx-summary.json`.

Beide Debug-Lösungen erfolgreich: GUI-Lösung 87 Warnungen/0 Fehler, zweite Lösung 0 Warnungen/0 Fehler. Warnungen werden nicht als warnungsfreier Build ausgegeben. Keine Änderung am SDK, den Paketen oder Prism-Quellen.

## 9. Native Windows-Ergebnisse

Eigene frische Desktopinstanz aus dem erneut gebauten Debug-Verzeichnis gestartet; vor Start keine CryptoWorkBench-Fenster vorhanden. Kein Benutzerfenster übernommen und kein ScriptBook geöffnet oder gespeichert. Instanz nach Prüfung geschlossen; Fensterliste bestätigt deren Ende.

Computer-Use über Windows-Tasteneingabe, kein direkter Headless-Commandaufruf:

1. Print(0x(11)) eingegeben; Return: genau eine sichtbare Ausgabe out: 0x(11), Caret nächste freie Zeile.
2. Print(0x(22)) eingegeben; KP_Enter: genau eine sichtbare Ausgabe out: 0x(22).
3. Alt+PageUp: freie Zeile wird Print(0x(11)); Alt+PageDown: Print(0x(22)). Keine zusätzliche Ausgabe.
4. Ctrl+Z: Historyersetzung zurück auf Print(0x(11)); Ctrl+Y: wieder Print(0x(22)). Beide bisherigen Ausgaben unverändert jeweils einmal.

Belege: `native-return.jpg`, `native-kp-enter.jpg`, `native-history-back.jpg`, `native-history-forward.jpg`, `native-undo.jpg`, `native-redo.jpg`. Sichtbar angeboten werden KeyBindings; keine erfundenen Historybuttons. Der bestehende Fußzeilentext beschreibt die Gesten unzutreffend gegenüber der tatsächlich beobachteten Alt-LineHistory-Bindung; er war nicht Testoracle.

Texteingabe erfolgte durch die Computer-Use-Texteingabe, die in dieser Umgebung Clipboard/Paste verwendet; Return/KP_Enter/History/Undo/Redo wurden gesondert als Windows-Keygesten ausgelöst. Kein Nachweis physischer Tastenbetätigung. Schweizer AltGr/Dead Keys, physisches Enter, CJK-Restgrenze und vollständige N1–N4-Abnahme bleiben OPEN/separat. Der Native-Smoke weist sichtbare Ausgaben nach; die genaue Taskzahl stammt aus den instrumentierungsfreien Headless-Proben, nicht aus einem Screenshot.

## 10. Findings und verbleibende OPEN-Punkte

### IR-N1-01 – P8/AP5: verworfene Newline beendet bereits angenommene Ausführung nicht

**Severity: Medium; bestätigt; getrennte bestehende P8/AP5-Lücke, kein F1-Blocker.**

Positionen: `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs:104` (früher Taskstart), :109–116 (Präsentationsprüfung ohne Coordinator-Generation), `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:201–203` und :227ff (History/Serviceausführung). F1-Abgrenzung: WorkbenchTextArea.cs:155ff prüft erst nach den Subscribern erneut.

Reproduktion: vollständige View mit Print(0x(11)); nach ihrem TextEntering-Handler einen Subscriber registrieren, der entweder e.Handled=true setzt oder Coordinator.InvalidateInputIntents aufruft; nativen Return auslösen; synchronen Zustand erfassen; PendingExecution abwarten. Eigenes Prüfprogramm, Modi handled/generation, liefert die protokollierten Wirkungen.

Erwartung für vollständige spätere P8/AP5-Koordination: ausdrücklicher Vertrag, ob die bereits angenommene Ausführung trotz später verworfener Eingabe gültig bleibt; generation-/lifecyclegebundene Präsentation und getrennte strukturierte Wirkungsevidenz. Keine Ableitung vollständiger Wirkungslosigkeit allein aus dem Enterreceipt.

Beobachtet: synchron kein Newline, keine Revisionserhöhung, Receipt None/None/Complete und kein aktives Ticket mehr; danach trotzdem genau ein Historyeintrag und out: 0x(11) im selben Dokument. Generation allein verhindert den späteren Insert nicht. Kein zweiter Task, keine übertragene Enterberechtigung und kein FOREIGN. Detach/Dokumenttausch verhindern dagegen den getesteten Dokumentinsert; Cancel kann einen Historyeintrag zurücklassen. Bei Faulted/Releasing kann der Service weiterrechnen, während die Präsentation scheitert.

Korrekturanforderung an einen separat freizugebenden P8/AP5-Auftrag: Ausführungsannahme, spätere Invalidierung und Präsentationsberechtigung explizit zusammenführen bzw. ihre Unabhängigkeit vertraglich festlegen; Generation/Lifecycle und bereits entstandene History-/Interpreterwirkung in separaten Receipts nachweisen. Die unveränderten View-/Servicepfade zeigen, dass diese Verantwortung nicht durch F1 neu eingeführt wurde. Keine Korrektur oder Vorwegnahme dieser Architektur in diesem Review.

### OPEN / Grenzen

- AP3-P6: bekannter synthetischer UpdateFinished-Rekursionspunkt bleibt offen. Die begrenzten Eingabe-Reentrancyproben beweisen nicht die Sicherheit beliebiger direkter observerseitiger Dokumentmutationen. Keine neue unbeschränkte Rekursionsprobe und keine stillschweigende Schließung.
- Native vollständige N1–N4-Abnahme und physische Schweizer Tastaturprüfung offen.
- Zukünftiger Paketwechsel: enge OnEnter-/PerformTextInput-Integration erneut prüfen.
- Keine neuen Gegenbeweise zu IR-01, IR-02 oder RR-01; History-/Capabilityregressionen sämtlich grün.

## 11. Schlussbewertung und Stop-Gate

**READY FOR ACCEPTANCE** für die begrenzte native Enter-Command-/TextInput-Korrektur N1-F1. Einmalige Originalticket-Bindung, Verbrauch vor fremden TextEntering-Callbacks, native Semantik, Abschluss und Regression sind unabhängig belegt. Keine bestätigte F1-Berechtigungsumgehung oder doppelte Interpreterausführung.

Diese Bewertung umfasst nicht die offene P8/AP5-Lücke IR-N1-01, AP3-P6 oder die vollständige native History-Abnahme. Sie sind sichtbar weiterzuführen. Produktquellen, permanente Tests/Assertions und vorhandene Evidenz bleiben unverändert. Keine Fortsetzung mit AP3-03, keine vollständige N1–N4-Abnahme, kein Commit, Push oder Merge. `.codex/config.toml` wurde weder gelesen noch verändert. Ende am verlangten Review-Stop-Gate.
