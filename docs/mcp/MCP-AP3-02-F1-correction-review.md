# MCP AP3-02-F1 – Korrekturreview

Stand: 29.10.2026. **READY FOR INDEPENDENT RE-REVIEW** für IR-01 und IR-02. Keine abschließende Produktfreigabe; native Windows-Button-/Tastaturabnahme bleibt OPEN.

## 1. Ausgangsstand und tatsächlicher Patchumfang

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, Branch `feature/mcp-ap3`, HEAD unverändert `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Vor Beginn lagen die beiden AP3-02-Produktänderungen in HistoryService und CryptoScriptEditViewModel sowie unversionierte HistoryCoordinatorTests und die drei AP3-Berichte vor. Sie wurden nicht zurückgesetzt.

F1 ändert ausschließlich:

- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`: fehlerfester Konstruktorabschluss/Dispose und Aufruf der begrenzten nativen Historyersetzung.
- `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs`: interner, nicht reentrant ausführbarer Remove-/Insert-Zweischritt unter bestehendem IdentityLock.
- Neu `CryptoWorkBenchGuiTest/HistoryCorrectionTests.cs`: 27 permanente Fälle.
- Neu dieser Bericht; ignorierte Logs/TRX unter `artifacts/ap3-02-f1`.

HistoryService und die 23 vorhandenen HistoryCoordinatorTests bleiben gegenüber AP3-02 byteidentisch; über das vorhandene Reviewmanifest geprüft. AP3-01 und AP3-02-Implementierungsbericht ebenfalls unverändert. Der unabhängige Reviewbericht wurde nicht umgeschrieben. Bestehende AP2.1-/F1-Assertions, Core, Pipeline, EC21, Pakete und Prism unverändert. Index leer, `git diff --check` ohne Befund. Keine anderen Worktrees, Branches, Commits, Pushes oder Merges. `.codex/config.toml` weder gelesen noch verändert.

Grundlagen: [unabhängiger Review](MCP-AP3-02-independent-review.md), [AP3-02-Implementierung](MCP-AP3-02-history-implementation-review.md), [AP3-01](MCP-AP3-01-integration-assessment.md), bestehende R2/AP0/D01-Verträge.

## 2. IR-01 – Ursache und Korrektur

Bisher konnte eine Exception aus DetachEditor/PropertyChanged, Lifetime.Cancel oder PasswordHost.Dispose die spätere Freigabe der exklusiven Historybindung überspringen. Eine neue VM mit demselben HistoryService scheiterte anschließend mit „Line history already has an editor“.

Dispose führt jetzt jede eigene Cleanupverpflichtung einzeln aus: Detach, Lifetime-Cancel, PasswordHost-Dispose, ursprüngliche Historybindung und beide Outputabmeldungen. Fehler werden gesammelt. Ein einzelner Fehler wird mit ursprünglicher ExceptionDispatchInfo erneut geworfen, mehrere als AggregateException. Keine stille Unterdrückung. Ein vor Beginn gesetzter Disposezustand verhindert doppelte Freigabe oder erneuten Zugriff auf eine inzwischen übergebene Bindung.

Die Historybindung bleibt ownergebunden durch das vorhandene NavigationBinding. Wiederholtes Dispose der alten VM entfernt keine Nachfolgerbindung. Bei einer während IdentityLock laufenden Originalaktion wird Detach weiterhin über AfterIdentity verschoben. Dispose entsorgt kein Coordinator-OperationTicket, keine native Updategruppe und keine Q-Reservation: Die ursprüngliche synchrone Aktion behält ihren Abschluss.

Konstruktorinitialisierung nach erfolgreicher Historybindung besitzt jetzt einen Fehlerpfad, der eigene Bindung und bereits registrierte Outputsubscriber entfernt und den ursprünglichen Fehler weitergibt. Die permanente Null-Dependencyprobe erzwingt einen tatsächlichen Fehler nach Bindung und belegt die anschließende Wiederverwendbarkeit desselben HistoryService. Sie ist eine gezielte Initialisierungsfehlerprobe, keine Behauptung, dass produktive DI normalerweise null liefert.

## 3. IR-02 – Native Mechanik und Berechtigungsgrenze

Die verworfene einzelne Document.Replace-Operation ist aus dem Historypfad entfernt. `ReplaceHistoryLine` führt die ursprüngliche Reihenfolge **Remove, danach Insert** innerhalb eines einzigen nativen RunUpdate aus. Identischer Zieltext wird weiterhin bereits im VM als No-op behandelt, ohne Gruppe und ohne Text-/Historywirkung.

Der interne Helfer nimmt Originaldokument, Offset, Länge und Text entgegen, keinen frei ausführbaren Delegate. Eintritt verlangt IdentityLock, unbenutzten Editzustand, dasselbe Dokument, Coordinator.Active und ObserverDepth=0. Der VM ruft ihn ausschließlich synchron innerhalb seines ursprünglichen C.Commit-Tickets auf; keine zweite Admission.

Vor RunUpdate wird eine private Reentranzsperre gesetzt. Damit kann auch UpdateStarted, bevor eine Textänderung begonnen hat, den Helfer nicht verschachtelt aufrufen. Nach Ende von UpdateStarted wird genau eine Schreibberechtigung für Remove erteilt. BeforeChange verbraucht sie vor nachfolgenden nativen Observern. Erst wenn Remove einschließlich seiner synchronen Callbacks regulär zurückkehrt, wird die Berechtigung für Insert erteilt. Nach Insert oder Exception wird sie im finally entzogen. Ein Fehler im ersten Schritt führt niemals zum zweiten Schritt. Die allgemeine AllowSingleEditChange-Schnittstelle und die alten Serviceeditregeln bleiben unverändert.

Es gibt kein allgemeines Schreibrecht für Active-Callbacks, keine Reparatur-EndUpdate-Schleife und keinen Librarypatch. RunUpdate besitzt den normalen Gruppenabschluss; ein dennoch unklarer/offener Abschluss wird vom bestehenden Coordinator faulted behandelt. IdentityLock wird erst in der bestehenden Original-finally-Kette gelöst.

## 4. Tatsächliche Anchor-/Caret-/Selectionmatrix

Die neue permanente `NativeReferenceMatrix` vergleicht den echten produktiven Historycommand mit einer separaten nativen Remove-/Insert-Referenz auf AvaloniaEdit 12.0.0. Beide verwenden eine Gruppe; die Referenz kopiert keinen neuen Hostguard.

| Dimension | Tatsächlich geprüft |
|---|---|
| Ausgangstext | `p\nabcd`, `p\r\nabcd`, `p\n` |
| Zieltext | Mehrzeilig `WX\r\nYZ` |
| Anchoroffsets | Jeder Offset des Originaldokuments, einschließlich Präfix vor der Region, Regionsanfang, Innenpositionen und Regionsende |
| MovementType | Default, BeforeInsertion, AfterInsertion |
| SurviveDeletion | false und true |
| Resultat | IsDeleted und für lebende Anchors genauer Offset, jeweils identisch zur nativen Referenz |
| Caret/Selection | Caret am Dokumentende, anschließende gesamte Dokumentauswahl; finale Caretposition, SelectionStart und SelectionLength identisch zur Referenz |
| Ereignisse | Reihenfolge UpdateStarted, Changing/Changed mit Offset/RemovalLength/InsertionLength und UpdateFinished identisch zur gruppierten Remove-/Insert-Referenz |
| Undo/Redo | Ein Undo stellt Originaltext her, danach CanUndo=false; Redo stellt Zieltext her |

Das ergibt 18 parametrisierte Matrixfälle mit mehreren Anchorassertions je Fall. Das originale Gegenbeispiel besitzt zusätzlich eine eigenständige unveränderte Regression: `abcd`, Anchor 0, SurviveDeletion=true, Standardbewegung → `WXYZ` → **Offset 4**.

Eine Position strikt hinter der ersetzten Region existiert im produktiven Pfad nicht: Ersetzt wird die letzte Dokumentzeile bis zum Dokumentende. Das Ende ist enthalten; ein künstlicher Suffix wurde nicht als produktive Historyvariante ausgegeben. Andere Caret-/Selectionkonfigurationen sind nicht vollständig enumeriert und bleiben zusätzliche Review-/Nativeprüfungen. Bestehende Unicode-/Sonderzeichen-, leere Dokument-/Zeilen- und No-op-Fälle aus den 23 Historytests laufen unverändert mit.

Gegenüber AP3-02-Replace gibt es wieder zwei Changing/Changed-Teilschritte statt eines. Dies ist die ausdrücklich angeordnete Rückkehr zur bisherigen Remove-/Insert-Semantik. Gegenüber der ursprünglichen ungruppierten Folge bleibt die bewusst neue einzelne Update-/Undo-Gruppe. Diese Unterschiede werden nicht als vollständige Ereignisgleichheit aller drei Varianten dargestellt.

## 5. Undo, D01 und Fehlerabschluss

| Fall | Nachweis / Verhalten |
|---|---|
| Normale wirksame Ersetzung | Zwei native Textschritte, eine Undo-Gruppe; Originalticket/IdentityLock/Q bis Abschluss |
| Identischer Zieltext / leerer No-op | Kein RunUpdate, keine neue Gruppe; Index allein bleibt keine native Historywirkung |
| Exception nach Remove, vor Insert | Text bleibt nach Remove; Insert läuft nicht; HistoryEffect Indeterminate, Finalization Faulted |
| Exception nach Insert | Zieltext bereits sichtbar; kein Rollback; HistoryEffect Indeterminate, Finalization Faulted |
| Exception in UpdateFinished | Unsicherer Abschluss bleibt Faulted, kein Erfolg trotz möglicher Textwirkung |
| Dispose während erster Changed-Zustellung | Pending bleibt während Callback positiv; ursprüngliche Aktion beendet Insert/Finalisierung, erst danach Pending=0 |
| Reentranz aus UpdateStarted, Changed und IsModified | Interner Zweischritteintritt abgewiesen; genau ursprünglicher Zieltext, Complete |

TextEffect bleibt bei entkommener nativer Exception nach bestehendem D01-Code konservativ Indeterminate statt falschem None/Busy. Kontrollierte Callbackfehler und Applied-Fälle werden durch unveränderte History-/Coordinatorfälle mitgeprüft. Kein historyCommand=true als erfundener Beleg für Indexnavigation; kein automatischer Retry oder Rollbackversprechen.

## 6. Neue permanente Regressionen und Rot-/Grünnachweis

Vor jeder Produktkorrektur wurden die beiden ursprünglichen Gegenbeispiele als permanente Tests angelegt und ausgeführt: **2 FAIL / 0 PASS**. OriginalAnchorCounterexample erwartete Offset 4, DisposeFailureReleasesOnlyOriginalBinding erwartete eine konstruktierbare Nachfolger-VM. Beide Tests blieben anschließend unverändert und bestehen nach der Korrektur.

Zusätzlich:

- 18 Anchor-/Caret-/Selection-/Ereignisvergleichsfälle.
- Fehler nach erfolgreicher Konstruktorbindung und erfolgreiche Neubindung.
- Zwei fehlernde Cleanupquellen (Property und Lifetime-Cancellation), AggregateException mit beiden Ursachen, erfolgreiche Nachfolgerbindung und wiederholtes altes Dispose.
- Vier Remove-/Insert-/UpdateFinished-/Active-Dispose-Fälle.
- NativeCallbacksCannotBorrowTwoStepPermission für frühe und späte native Callbackreentranz.

Insgesamt **27 neue F1-Fälle**. Reflection wird nur in der Cleanupfehlerprobe verwendet, um einen werfenden Cancellationcallback an die vorhandene VM-Lifetime zu registrieren. Produktflags, Guards und Assertions werden dabei nicht geändert.

## 7. Vollständige Validierung

| Lauf | Ergebnis | Originalbeleg |
|---|---|---|
| Ursprüngliche IR-01-/IR-02-Gegenbeispiele vor Produktfix | 2 FAIL, 0 PASS | [red.trx](../../artifacts/ap3-02-f1/red.trx), [Log](../../artifacts/ap3-02-f1/red.log) |
| Erster vollständiger korrigierter Stand | 539 PASS, 0 FAIL, 0 SKIP | [gui-green.trx](../../artifacts/ap3-02-f1/gui-green.trx) |
| Finaler gezielter F1-Lauf | 27 PASS, 0 FAIL, 0 SKIP | [corrections-final.trx](../../artifacts/ap3-02-f1/corrections-final.trx) |
| Finale vollständige GUI-Suite | **540 PASS, 0 FAIL, 0 SKIP** | [gui-final.trx](../../artifacts/ap3-02-f1/gui-final.trx), [Log](../../artifacts/ap3-02-f1/gui-final.log) |
| Darin unveränderte HistoryCoordinatorTests | **23 PASS** | Dieselbe finale TRX, nicht zusätzlich addieren |
| Darin bisherige EditorCoordinator-/F1-Fälle | **35 PASS** | Dieselbe finale TRX, nicht zusätzlich addieren |
| Debug-Build beider Lösungen | Erfolg, 0 Fehler; Avalonia-Lösung 9 Warnungen, Root-Lösung 0 | [build-final.log](../../artifacts/ap3-02-f1/build-final.log) |

TRX unabhängig vom Konsolentext eingelesen: 540 UnitTestResult-Einträge mit Passed; TestDefinitions ergeben 27/23/35 für die genannten Klassen. Kein Test entfernt oder abgeschwächt. Bestehende Core-/MCP-Projekte gebaut, Tests dort nicht erneut ausgeführt, da keine entsprechenden Abhängigkeiten/Verträge geändert wurden.

Ausführung über unverändertes `eng/prism/Build.ps1 -Configuration Debug`, danach `dotnet test ... --no-build --no-restore`, gezielt mit HistoryCorrectionTests-Filter und vollständig ohne Filter. Alle Ergebnisse in getrennten Dateien; der Rotnachweis wurde nicht überschrieben. SDK 10.0.401, Avalonia 12.1.4, AvaloniaEdit 12.0.0 und genehmigte Prismquelle unverändert.

## 8. Verbleibende Grenzen

**Native Windows OPEN:** Keine echte MainView-Button-/Ctrl+Z/Y-/Schweizer-Layout-/Fokusabnahme durchgeführt. Headless-/native Dokument-APIs sind kein physischer OS-Nachweis. IME-/Textdragsperre und akzeptiertes CJK-Restrisiko bleiben unverändert.

Keine vollständige Enumeration aller Caret-/Selectionzustände; tatsächlicher Umfang siehe Matrix. Die im unabhängigen Review bezeichneten allgemeinen Q-Race-/Multicast-/P6-Observergrenzen werden nicht durch F1 pauschal geschlossen. Historische Findings bleiben unverändert. Keine neuen bestätigten offenen IR-01-/IR-02-Blocker in den ausgeführten Proben; ein unabhängiger Nachreview muss insbesondere den privaten Zweischritt-Berechtigungsverbrauch und Cleanup während aktiver Originalwirkung prüfen.

## 9. Schlussbewertung und Stop-Gate

**READY FOR INDEPENDENT RE-REVIEW.** Beide ursprünglichen Gegenbeispiele zuerst rot, anschließend grün; zusätzliche Fehler-/Anchor-/Lifecyclefälle und vollständige GUI-Suite bestehen. Die verbindliche alte Anchorsemantik wird wiederhergestellt, nicht neu genehmigt oder umdefiniert.

Keine weiteren AP3-Pakete implementiert. Keine Änderungen an Core, EC21, Save-/Dialog-/Shutdownarchitektur, Lease-v2 oder HTTP. Kein Commit, Push oder Merge. Am Stop-Gate angehalten, keine AP3-03-Fortsetzung.
