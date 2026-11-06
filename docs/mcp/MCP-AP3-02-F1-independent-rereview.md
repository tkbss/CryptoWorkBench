# MCP AP3-02-F1 – Unabhängiger adversarialer Nachreview

Stand: 29.10.2026. **CHANGES REQUIRED.** Die ursprünglichen Findings IR-01 und IR-02 sind in den erneut ausgeführten Gegenbeispielen korrigiert. Ein neuer **Medium**-Befund betrifft die ausdrücklich verlangte Bindung des internen Historyhelfers an seine eigene Zulassung: Ein beliebiges aktives Coordinator-Ticket genügt weiterhin. Die vollständige GUI-Suite besteht mit 540 Tests. Die native History-/physische Tastaturabnahme bleibt OPEN.

## 1. Exakter Ausgangs- und Patchstand

Worktree: `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`; Branch `feature/mcp-ap3`; HEAD unverändert `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Index leer. Der Review bewertet den vorhandenen **uncommitteten Gesamtpatch**, nicht einen separaten F1-Commit.

| Status bei Beginn | Datei / Umfang gegenüber HEAD |
|---|---|
| M | `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs`: +28 / −0 |
| M | `CryptoWorkBenchAvalonia/Services/HistoryService.cs`: +34 / −4 |
| M | `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`: +55 / −17 |
| ?? | `CryptoWorkBenchGuiTest/HistoryCoordinatorTests.cs` |
| ?? | `CryptoWorkBenchGuiTest/HistoryCorrectionTests.cs` |
| ?? | `docs/mcp/MCP-AP3-01-integration-assessment.md` |
| ?? | `docs/mcp/MCP-AP3-02-history-implementation-review.md` |
| ?? | `docs/mcp/MCP-AP3-02-independent-review.md` |
| ?? | `docs/mcp/MCP-AP3-02-F1-correction-review.md` |

SDK 10.0.401; net10.0; Avalonia 12.1.4, AvaloniaEdit 12.0.0, DataGrid 12.1.2. Prismquelle unverändert auf `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Keine Paket-/Prismänderung, keine erneute Migration.

Vor der Testausführung wurde [before.xml](../../artifacts/ap3-02-f1-review/before.xml) mit SHA-256 für 126 relevante Produkt-, Test-, Dokument- und Buildskriptdateien erstellt, einschließlich der unversionierten Tests und Bestandsberichte. Abschließender Vergleich: **126 geprüft, 0 verändert/fehlend**. Das erfasste Tracking-Diff liegt zusätzlich als [tracked.patch](../../artifacts/ap3-02-f1-review/tracked.patch) vor. Der neue Bericht gehört naturgemäß nicht zum Vorhermanifest.

| Zentrale Datei | SHA-256 vor/nach Review |
|---|---|
| WorkbenchTextEditor.cs | `940D7D26A59470D3C9C50C87656AAAC0BBBB5E5F97855D33314C8FA9B75E3DC5` |
| HistoryService.cs | `B00A45F19139C9DE968B12B14B0B5D1A89F820DE5975AA3926F2FA316024D135` |
| CryptoScriptEditViewModel.cs | `DF505C8C0854F7837BCA3C1EEA0E1962026F376E2DF4F8A580EDAC112F28015D` |
| HistoryCorrectionTests.cs | `7723BF1C864D9015E3E3D0F969AFD13DBB14A295237358C89692A1F9D11CBE4F` |
| HistoryCoordinatorTests.cs | `6F8F11D86FDB805D7F679E9FEFB258A183B2723670351244C2F45725225F5B54` |

Grundlagen: [ursprünglicher Review](MCP-AP3-02-independent-review.md), [F1-Korrekturbericht](MCP-AP3-02-F1-correction-review.md), [Implementierungsbericht](MCP-AP3-02-history-implementation-review.md), [AP3-01](MCP-AP3-01-integration-assessment.md), [R2](MCP-00-R2-consolidated-architecture-decision.md), [AP0/D01](MCP-00-AP0-contract-finalization.md). Historische Findings werden durch diesen Bericht nicht rückwirkend umgeschrieben.

Unabhängigkeit bedeutet hier eigenständige Gegenproben gegen die Produktassembly und getrennte Referenzimplementierung. Der Review findet im selben Chat wie die Implementierung statt; keine organisatorisch unabhängige Person und kein separater Reviewer wurden eingesetzt.

## 2. R1 – IR-01, Dispose und Konstruktorfehler

**IR-01 im ursprünglichen Gegenbeispiel geschlossen.** Die unabhängige Probe registriert eine VM mit Editor, wirft beim VM.PropertyChanged für `TextEditor` während Dispose und konstruiert danach einen Nachfolger mit demselben HistoryService. Resultat: dieselbe Exceptioninstanz wird erneut geworfen; der ursprüngliche Callbackstack ist erhalten; der Nachfolger bindet erfolgreich. Wiederholtes Dispose der alten VM entfernt dessen Bindung nicht; anschließendes LineBack schreibt `target` in den Nachfolger.

`CryptoScriptEditViewModel.cs:346–363` setzt den Disposezustand vor Cleanup. Detach, Cancellation, PasswordHost, Historybindung und beide Outputabmeldungen werden einzeln abgearbeitet. Ein Fehler nutzt ExceptionDispatchInfo, mehrere Fehler AggregateException. Die ownergebundene `NavigationBinding.Dispose` (`HistoryService.cs:24–29`) entfernt ausschließlich den ursprünglichen Delegate. Die VM ruft diese Freigabe im Disposepfad höchstens einmal auf; der Konstruktorfehlerpfad betrifft eine nicht zurückgegebene VM.

Zusätzliche unabhängige Probe: fehlerndes Detach plus fehlernder Lifetime-Cancellationcallback ergeben nach Flatten genau `detach-first`, `cancel-second`. Beide statischen Output-Subscriberzahlen kehren zum Ausgangswert zurück; Neubindung und wiederholtes altes Dispose funktionieren. Ein Null-VariableViewModel erzwingt einen Fehler nach Historybindung: keine zurückbleibende Bindung oder zusätzlichen Outputsubscriber. Dieser Fall wirft vor den Outputregistrierungen. Nach erfolgreicher Zuweisung des Executiondelegates folgen im aktuellen Konstruktor nur gewöhnliche Eventregistrierungen und Feldzuweisungen; ein künstlicher Fehler nach jeder einzelnen Registrierung wurde nicht injiziert.

Die Outputevents sind feldbasierte C#-Events ohne fremde Remove-Accessoren. Ein eigenständiger benutzerdefinierter Abmeldefehler ist hier nicht erreichbar; die einzelnen Cleanupblöcke würden dennoch nach einer solchen Exception weiterlaufen. Schwerwiegende Laufzeitfehler wie OutOfMemory sind kein bewiesener Recoveryfall. PasswordHostfehler wurden nicht separat injiziert.

Dispose/Detach aus dem ersten Changed-Callback: im Callback bleiben **Pending=1, Active=true, IsInUpdate=true, Q.IsBusy=true**. Der zweite Textschritt läuft zu Ende; danach Pending=0, Active=false, Update geschlossen und Phase Unavailable. Kein vorzeitiges Freigeben des OperationTickets, der Updategruppe oder Q beobachtet. Einzelheiten und D01 unten.

## 3. R2 – IR-02 und native Semantik

**IR-02 im ursprünglichen Gegenbeispiel geschlossen:** `abcd`, Anchor an Offset 0, Default, SurviveDeletion=true, Historyziel `WXYZ` → **Offset 4**, ebenso in der ursprünglichen ungruppierten Remove-/Insert-Referenz.

Produktive Kette: `MainViewModel.LineHistoryBackCommand/ForwardCommand` → `HistoryService.NavigateLine` → exklusiver VM-Delegate → IsExecuting/Registrationprüfung → C.TryBegin → Q.Reserve → C.Commit → IdentityLock → Indexfortsetzung → `ReplaceHistoryLine` → kontrollierte Dirty-/Historypublikation. Die bestehenden 23 Fälle benutzen auch den MainViewModel-Command; die zusätzlichen Proben rufen den gleichen HistoryServicepfad direkt auf.

Die unabhängige Referenz führt **Remove und Insert ohne äußeres RunUpdate** aus. Damit wird die ursprüngliche Anchorsemantik verglichen; die neue Undoanforderung wird ausschließlich am Produkt separat geprüft.

270 Kombinationen: 5 Ausgangstexte (`abcd`, leer, `p\n`, `p\r\nabcd`, `ä😀e\u0301\r\nü😀`) × 3 Zieltexte (`WXYZ`, leer, `ä😀\r\nè`) × 3 MovementTypes × 2 SurviveDeletion-Werte × 3 Caret-/Selectionkonfigurationen. Jeder UTF-16-Offset von 0 bis Dokumentende erhält einen Anchor, einschließlich Surrogat-Innenoffsets. Geprüft: IsDeleted, lebender Offset, Text, Caret, SelectionStart/-Length, Changing-/Changed-Reihenfolge mit Offset/RemovalLength/InsertionLength. **270 Fälle, 0 Abweichungen.**

Caret-/Selectionkonfigurationen: keine Auswahl/Caret 0; gesamte Auswahl; Auswahl ab Dokumentmitte. Kein Anspruch auf sämtliche möglichen Auswahlen oder Layoutzustände. Hinter der letzten ersetzten Zeile gibt es produktiv keinen Suffix; das Dokumentende ist enthalten.

Für jede wirksame Produktnavigation stellt genau ein Undo den gesamten Originaltext her, danach CanUndo=false; Redo stellt den Zieltext her. No-op erzeugt keine neue Undoaktion. Die Zahl der UpdateStarted/UpdateFinished-Ereignisse des ungruppierten Originals wird bewusst nicht mit der neuen Gruppe gleichgesetzt. Die permanenten 18 Matrixfälle vergleichen zusätzlich die gruppierte Ereignisfolge, sind aber allein kein unabhängiger Vergleich mit dem alten ungruppierten Vertrag.

## 4. R3 – Zweistufige Schreibberechtigung

Innerhalb eines bereits betretenen Historyhelfers ist die Sperrfolge nachvollziehbar: `historyStepsActive` wird vor RunUpdate gesetzt; UpdateStarted erhält noch kein `editWriteAllowed`. Vor Remove wird einmal freigegeben; BeforeChange verbraucht vor nachfolgenden Changing-Observern. Erst nach vollständig zurückgekehrtem Remove werden die beiden privaten Flags für Insert zurückgesetzt. Das innere finally entzieht die Berechtigung; äußeres finally und IdentityLease räumen auch beim Fehler auf. Ein UpdateStarted-Fehler vor Eintritt in das using erhält keine erste Freigabe.

Unabhängige **direkte öffentliche Document.Insert-Angriffe** aus UpdateStarted, Changing beider Schritte, Changed beider Schritte, UpdateFinished, UndoStack.PropertyChanged und IsModified wurden alle abgewiesen: 9 abgewiesene Aufrufe über sechs frische Fixtures, 0 erlaubte; Originalziel `WXYZ`, Applied/Applied/Complete, geschlossene Gruppe. Bei Changing/Changed kann zusätzlich die native Reentranzprüfung abweisen; das Ergebnis beweist nicht allein, welcher Guard zuerst wirft. Die vorhandene permanente Callbackprobe ruft dagegen hauptsächlich den internen Helfer erneut auf und deckt diese direkte Schreibfläche nicht selbst ab.

**Nicht erfüllt ist die Begrenzung auf das eigene Historyticket.** Die Eintrittsbedingung in `WorkbenchTextEditor.cs:111–112` verlangt Active und ObserverDepth=0, aber keine Historyoperation/gebundene Capability. `historyStepsActive` schützt nur vor Rekursion desselben bereits laufenden Helfers. Zwei eigenständige Proben bestätigen fremden Eintritt; siehe Finding RR-01.

Kein übrig gebliebenes `editWriteAllowed` oder IdentityLock in den protokollierten Fehlerfällen. Dokumentwechsel bleibt durch den bestehenden Identityguard geschützt. Allgemeine AP3-Observerintegration wird dadurch nicht als abgeschlossen gewertet.

## 5. Neue Gegenproben und Reproduzierbarkeit

Vollständiger separater Quellcode: [probe/Program.cs](../../artifacts/ap3-02-f1-review/probe/Program.cs), [Projekt](../../artifacts/ap3-02-f1-review/probe/probe.csproj). Maßgeblicher erfolgreicher Lauf: [probes-evidence.log](../../artifacts/ap3-02-f1-review/probes-evidence.log). Diese Dateien liegen ausschließlich in ignorierter Reviewevidenz, außerhalb von Produkt- und permanenten Testquellen.

Die Probe lädt die gebauten echten AvaloniaEdit-/Produktassemblies und dispatcht über die vorhandene HeadlessUnitTestSession. Reflection dient dem Aufruf interner Methoden und der lesenden Inspektion von Flags/Receipt/Subscriberlisten; kein privates Berechtigungsflag wird gesetzt und kein Guard ersetzt. Die fremde Callbackprobe ist eine künstlich installierte interne Observerprobe, kein nachgewiesener externer Angreifer.

Ausführung aus dem Worktree nach Debug-Build: `dotnet run --project artifacts/ap3-02-f1-review/probe/probe.csproj`; für die native Bibliotheksauflösung wurde ausschließlich im Probeprozess `CryptoWorkBenchGuiTest/bin/Debug/net10.0/runtimes/win-x64/native` dem PATH vorangestellt.

Frühe eigene Probeausgaben bleiben erhalten, sind jedoch nicht Abnahmebeleg: In `probes.log` war der Detach-Reflectionaufruf wegen seines optionalen Parameters falsch; in `probes-complete.log` scheiterte eine neu ergänzte Subscriberprobe an einem falschen Namespace. Beides ausschließlich im Reviewharness korrigiert. Der finale Lauf enthält korrekt aufgerufenes Detach, Subscriberprüfung und Vergleich der Receipt-ID mit der vor Beginn erfassten Originalregistrierung. Die ursprünglichen F1-Rot-/Grünbelege wurden nicht überschrieben.

## 6. R4 – D01-, Undo- und Lifecyclefehler

Jeweils frische VM, Originaltext `abcd`, Ziel `WXYZ`. Die Receipt-EditorId stimmt in allen neun finalen Fehler-/Lifecycleproben mit der vor der Aktion erfassten Originalregistrierung überein. OriginalRevision ist jeweils 0; ObservedRevision ist tatsächlich aus dem Original gelesen, nicht als +1 erwartet. Nachfolgender Schreibversuch erfolgt erst nach Erfassung des Receipts.

| Injektionspunkt | Tatsächlicher Text nach Navigation | ObservedRevision | Text / History / Finalization | Phase | IsInUpdate | Folge-Insert |
|---|---|---:|---|---|---|---|
| UpdateStarted wirft | `abcd` | 0 | Indeterminate / Indeterminate / Faulted | Faulted | true | abgewiesen |
| Changing vor Remove wirft | `abcd` | 0 | Indeterminate / Indeterminate / Faulted | Faulted | false | abgewiesen |
| Changed nach Remove wirft | leer | 1 | Indeterminate / Indeterminate / Faulted | Faulted | false | abgewiesen |
| Changing vor Insert wirft | leer | 1 | Indeterminate / Indeterminate / Faulted | Faulted | false | abgewiesen |
| Changed nach Insert wirft | `WXYZ` | 2 | Indeterminate / Indeterminate / Faulted | Faulted | false | abgewiesen |
| UpdateFinished wirft | `WXYZ` | 2 | Indeterminate / Indeterminate / Faulted | Faulted | false | abgewiesen |
| natives IsModified/PropertyChanged wirft | `WXYZ` | 2 | Indeterminate / Indeterminate / Faulted | Faulted | true | abgewiesen |
| Dispose im ersten Changed | `WXYZ` | 2 | Applied / Applied / Complete | Unavailable | false | am abgelösten Original erlaubt |
| Detach im ersten Changed | `WXYZ` | 2 | Applied / Applied / Complete | Unavailable | false | am abgelösten Original erlaubt |

In allen Zeilen nach Abschluss Pending=0, Active=false, IdentityLocked=false und `editWriteAllowed=false`. Die sieben Faulted-Fälle melden FinalizationFailed. Die offene native Gruppe bei UpdateStarted/IsModified wird **nicht** repariert oder als Complete ausgegeben. Pending=0 ist hier kein Erfolgssignal; Phase Faulted verhindert neue Mutationen und sichere Wiederverwendung.

Die erlaubte Folgemutation nach Detach/Dispose betrifft das vollständig abgelöste Originaldokument (`NEXTWXYZ`), keinen neu registrierten Editor. Das entspricht dem bestehenden Coordinatorvertrag für freigegebene Orphans. Im aktiven Callback blieben Q, Ticket und Updategruppe belegt (Abschnitt 2).

Indeterminate ist konservativ und verschweigt keine Wirkung als None/Busy. Insbesondere leer nach Remove und Zieltext nach Insert werden ausdrücklich dokumentiert. Der Review behauptet keinen Rollback, keine fehlerfreie native Undohistory nach einer Exception und keinen automatischen Retry. Erfolgreiche Undo/Redo-Rückkehr wird in der getrennten Positivmatrix bewiesen. S-01, EC21, Dateisemantik und Lease-v2 bleiben unverändert.

## 7. R5 – Builds, Regressionen und TRX-Integrität

| Lauf | Resultat | Beleg |
|---|---|---|
| Debug beider Lösungen über unverändertes `eng/prism/Build.ps1 -Configuration Debug` | beide erfolgreich; jeweils 0 Warnungen / 0 Fehler im aktuellen inkrementellen Lauf | [build.log](../../artifacts/ap3-02-f1-review/build.log) |
| Vollständige GUI-Suite, ohne Filter | **540 Passed, 0 Failed, 0 Skipped** | [gui.trx](../../artifacts/ap3-02-f1-review/gui.trx), [Log](../../artifacts/ap3-02-f1-review/gui.log) |
| Separater Filterlauf der drei geforderten Klassen | **85 Passed**, davon 27 HistoryCorrection, 23 HistoryCoordinator, 35 EditorCoordinator | [targeted.trx](../../artifacts/ap3-02-f1-review/targeted.trx) |
| Originaler F1-Rotbeleg | 2 Failed, 0 Passed | [red.trx](../../artifacts/ap3-02-f1/red.trx) |
| Originaler erster Grünbeleg | 539 Passed | [gui-green.trx](../../artifacts/ap3-02-f1/gui-green.trx) |
| Originaler finaler F1-Beleg | 27 gezielt und 540 vollständig Passed | [corrections-final.trx](../../artifacts/ap3-02-f1/corrections-final.trx), [gui-final.trx](../../artifacts/ap3-02-f1/gui-final.trx) |

TRX als XML gelesen: Counters, UnitTestResult-Anzahl/Outcomes und Zuordnung über TestDefinitions/TestMethod.className geprüft. 540 Ergebnisse besitzen 540 verschiedene ExecutionIds; alle TestIds sind zugeordnet. Es gibt 531 TestDefinitions: `OpenInfoLink_Padding_ShowsDocument` teilt eine TestId über 9 Ausführungen, `OpenInfoLink_Mechanism_ShowsDocument` über 2. Diese Differenz besteht auch im alten finalen Grünbeleg; sie wird nicht als 540 verschiedene Testdefinitionen ausgegeben. Die 27/23/35 sind **Teilmengen der 540**, keine zusätzlich zu addierenden Produkttests. Die separate Probe ist keine Erweiterung dieser Suite. Alte Ergebnisse bleiben historische Belege, keine erneut behauptete Rot-Ausführung. Bestehende TRX/Logs wurden nur gelesen; ergänzendes Hashmanifest `old-evidence.xml` erfasst sie nach den neuen Läufen und ergab am Abschluss **0 Änderungen**. Es ersetzt keinen vor Testbeginn erhobenen Altloghash. Maschinenlesbare Abschlusszahlen: [verification.json](../../artifacts/ap3-02-f1-review/verification.json).

Bewertung der Testqualität: Die beiden ursprünglichen Gegenbeispiele sind aussagekräftig. Die F1-Matrix prüft viele Anchors, benutzt aber auf beiden Seiten eine Gruppe; der separate ungruppierte Vergleich dieses Reviews schließt diese Referenzlücke. Die vorhandene Helper-Reentranzprobe prüft Eintritt **während einer Historyoperation**, nicht unter einem fremden Ticket. Alle 540 Tests können deshalb trotz RR-01 bestehen. Kein Test wurde entfernt oder abgeschwächt.

## 8. R6 – Native Windows-Ergebnisse und offenes Gate

Der gebaute `CryptoWorkBenchAvalonia.Desktop.exe` wurde über den Computer-Use-Skill als realer Windows-Prozess gestartet und nach dem Versuch geschlossen. Keine bestehende Benutzerinstanz wurde übernommen; kein ScriptBook gespeichert.

Beobachtet: Editor fokussiert; über OS-Automation eingegebener Text `äöü € é` im Editor sichtbar; natives Ctrl+Z entfernt ihn, Ctrl+Y stellt ihn sichtbar wieder her. Wechsel über „Show Variables“ zeigt Variable View und verlagert den Fokus auf den Sidebarbutton. Die zunächst verzögerte Accessibility-Fokusmeldung wurde durch erneutes Beobachten aufgelöst.

**Weiterhin OPEN:** mehrere History-Back-/Forwardaktionen, MainView-Historybuttons und anschließendes Undo/Redo genau dieser Navigation, Rückfokus-/Historykombination sowie physische Schweizer AltGr-/Dead-Keyeingabe. In der gestarteten Shell waren keine Historybuttons sichtbar/als solche zugänglich; die geprüfte MainWindow-AXAML bietet KeyBindings. Der allgemeine Ctrl+Z/Y-Versuch wird nicht als History-Undo-Abnahme ausgegeben. Die Unicodeinjektion verwendet keine physische Schweizer Tastatur und belegt keine AltGr-/Dead-Keyverarbeitung. Das akzeptierte CJK-IME-Restrisiko und die dauerhafte frühe IME-Deaktivierung bleiben unverändert.

## 9. Bestätigtes neues Finding

### RR-01 – Medium: Historyhelfer akzeptiert ein fremdes aktives Ticket

**Position:** `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs:108–130`, Eintrittsprüfung insbesondere **111–112**. Ergänzende Grenze: `Services/Automation/EditorCoordinator.cs:137–145` (allgemeines RunGui).

**Verletzte Anforderung:** R3 verlangt ausdrücklich, dass kein beliebiges Active-Ticket den internen Historyhelfer missbrauchen kann. Aktuell reicht IdentityLocked + unbenutzter Editzustand + gleiches Dokument + Active/ObserverDepth=0. Weder der Helfer noch sein Parameter identifizieren die zugelassene Historyoperation.

**Reproduktion A**, ausgeführt gegen die Produktassembly (interne Aufrufe im separaten Harness per Reflection):

```csharp
coordinator.RunGui(() => {
    using var identity = editor.LockIdentity();
    editor.ReplaceHistoryLine(editor.Document, 0, 4, "FOREIGN");
}); // kein Historydelegate, keine Historyindexfortsetzung, keine Q-Reservation
```

Tatsächlich: `abcd` → `FOREIGN`, Revision 0 → 2, **Applied/Applied/Complete**. Dies ist kein wirkungslos zurückgewiesener Versuch.

**Reproduktion B**, fremder früher Callback unter einem allgemeinen Ticket:

```csharp
editor.Document.UpdateStarted += (_, _) =>
    editor.ReplaceHistoryLine(editor.Document, 0, 4, "STOLEN");
coordinator.RunGui(() => {
    using var identity = editor.LockIdentity();
    using (editor.Document.RunUpdate()) {
        editor.AllowSingleEditChange();
        editor.Document.Replace(0, 4, "intended");
    }
});
```

Aufrufkette: allgemeines C.RunGui → IdentityLock → äußeres RunUpdate → fremdes UpdateStarted → noch inaktiver Historyhelfer → zwei native Schreibschritte. Tatsächlich: Callback einmal erfolgreich; Text **STOLEN**; Revision 0 → 2. Die ursprünglich geplante Freigabe scheitert anschließend am verbrauchten Editzustand; Receipt **Indeterminate/Indeterminate/Faulted**, Gruppe geschlossen. Der späte Fault verhindert die vorherige fremde Text-/Historywirkung nicht.

**Reichweite/Severity:** Bestätigte Verletzung der internen Capabilityanforderung, deshalb Medium und Korrekturbedarf. Kein nachgewiesener öffentlicher oder externer MCP-Exploit: Der Helper ist internal, die Probe installiert absichtlich einen internen Observer. Die Suche findet als regulären Produktaufrufer derzeit nur VM.NavigateHistory. Ein bestehender produktiver Fremdsubscriber mit diesem Helperaufruf wurde nicht gefunden. Diese Grenze wird ausdrücklich nicht als High-Datenverlust oder aktiv erreichbare externe Sicherheitslücke dargestellt.

**Erforderliche Abnahme für eine begrenzte Korrektur:** Eintritt an die konkrete ursprüngliche Historyoperation und deren Editor/Dokument binden; ein allgemeines Active-Ticket und dessen UpdateStarted dürfen keinen Zweischritt freischalten. Beide obigen Gegenproben müssen ohne fremde Text-/Historywirkung abgewiesen werden, während die ursprünglichen Anchor-/Undo-/Disposefälle bestehen bleiben. Keine zweite Koordinationsinstanz oder neue Undoengine nötig. Keine Korrektur im Review durchgeführt.

## 10. Empfehlung und Stop-Gate

**CHANGES REQUIRED wegen RR-01.** IR-01 und IR-02 sind für ihre konkreten ursprünglichen Fehler sowie die dokumentierte Erweiterungsmatrix bestätigt korrigiert. Vor Abnahme ist die Historyberechtigung enger zu binden und unabhängig nachzuprüfen; das native History-/physische Tastaturgate bleibt zusätzlich offen.

Kein weiterer bestätigter neuer Produktionsfehler aus den ausgeführten Proben. Nicht injizierte Cleanupfehler, allgemeine P6-Observerpfade, die frühere Q-Race-/Multicastfrage und die verbleibenden nativen Fälle werden nicht pauschal als geschlossen erklärt.

Produktionscode, permanente Tests und Bestandsberichte unverändert; `.codex/config.toml` weder gelesen noch geändert; keine anderen Worktrees verändert; kein Commit, Staging, Push oder Merge. Neu nicht ignoriert ausschließlich dieser Bericht. Am Stop-Gate angehalten; keine automatische Korrektur und keine Fortsetzung mit AP3-03.
