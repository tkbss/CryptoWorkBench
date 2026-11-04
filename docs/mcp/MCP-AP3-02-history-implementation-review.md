# MCP AP3-02 – History-Implementierungsreview

Stand: 29.10.2026. **READY FOR INDEPENDENT REVIEW** für den begrenzten P1-Produktpatch. Die native Windows-Abnahme bleibt ausdrücklich OPEN; keine vollständige AP3-Freigabe.

## 1. Ausgangsstand und Dateien

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, Branch `feature/mcp-ap3`, HEAD `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Vor Beginn ausschließlich unversionierter, akzeptierter [AP3-01-Bericht](MCP-AP3-01-integration-assessment.md); dieser wurde nicht geändert. Kein neuer Worktree, Commit, Push, Merge oder Staging. `.codex/config.toml` weder gelesen noch geändert.

Geändert:

- `CryptoWorkBenchAvalonia/Services/HistoryService.cs`: exklusive lokale Editorbindung und frühe vermittelte Line-Navigation, einmalige Indexfortsetzung, reine Eventpublikation.
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`: Bindung/Dispose, synchrone Admission, Q-Reservation, originale Dokumentwirkung und kontrollierte History-/Dirtypublikation.
- Neu `CryptoWorkBenchGuiTest/HistoryCoordinatorTests.cs`: 23 permanente Fälle.
- Neu dieser Bericht. Ignorierte lokale Build-/TRX-Artefakte unter `artifacts/ap3-02`.

MainViewModel und seine AXAML-Commands bleiben unverändert: Sie erreichen die neue Grenze über den produktiven HistoryService. Keine Änderung an bestehenden Tests, Coordinator, Bridge, Core, EC21, Paketen oder Prism. Keine Umsetzung anderer AP3-Pakete.

## 2. Endgültige Callchain

MainViewModel.LineHistoryBackCommand/ForwardCommand → IHistoryService.LineBack/Forward → HistoryService.NavigateLine → exklusiv gebundener VM.NavigateHistory → IsExecuting/Registration/Editorprüfung → C.TryBegin → Q.Reserve → C.Commit → IdentityLock → einmalige Indexfortsetzung → gegebenenfalls native RunUpdate/Replace → kontrollierte Dirtypublikation → kontrollierte LineHistoryChanged-Publikation → Originalfinalisierung/Q-Freigabe.

Produktive direkte LineBack/Forward-Aufrufer wurden gesucht: MainViewModel; zusätzlich vorhandene direkte Testaufrufe. Auch direkte Serviceaufrufe erreichen NavigateLine. Ohne Editorbindung bewegt sich kein Index. Das bisher schreibende VM-Historyevent wurde entfernt: Die Dokumentwirkung ist jetzt expliziter Bestandteil der gebundenen Aktion; LineHistoryChanged ist anschließend ein nichtschreibender Observer. Somit kann ein beliebiger Subscriber nicht die ursprüngliche Schreibfortsetzung auslösen. Es gibt keine zweite Admission und kein allgemeines Event-Schreibrecht. InfoBack/Forward bleiben unverändert.

Die konkrete HistoryService-Implementierung besitzt die interne Bindung. Alternative IHistoryService-Implementierungen in Tests erhalten dadurch keine produktive Editorberechtigung. Mehrere gleichzeitig gebundene Editor-VMs werden explizit abgewiesen; Dispose entfernt nur die eigene Bindung.

## 3. Admission und Reentranz

C prüft Phase, Active, ObserverDepth, Generation, originale Bridge-/Dokument-/Editoridentität, Revision und Locks erneut vor Wirkung. IsExecuting und gültige VM-Registration werden davor geprüft; Q wird vor Indexänderung fail-fast reserviert. Bei fehlender Registrierung, Busy, Releasing, Faulted, Active oder ObserverDepth bleiben Index, Text und native History unverändert.

IdentityLock und AllowSingleEditChange schützen den einzelnen nativen Replace vor zusätzlichen Dokumentänderungen und Identitätswechseln. Q blockiert reentrante Service-/State-/Runannahme. History- und explizite Dirtyobserver laufen unter C.Observe. Neue LineBack/Forward-Aufrufe aus Document-, Property- und Historysubscriber verändern den Index nicht. Keine Reservation über await: Der gesamte neue Pfad ist synchron.

Detach unter IdentityLock wird gebunden aufgeschoben. Nach Ende der Originalaktion kann eine neue Registrierung entstehen, ohne eine alte Fortsetzung zu erben. Fehler werden durch vorhandene Coordinatorfinalisierung behandelt; keine zusätzliche Undo-/Historyreparatur.

## 4. D01-Effektmatrix

`historyCommand: true` wird **nicht** verwendet. Der HistoryService-Index ist kein nativer Undo-Stack. Commit ermittelt native Historywirkung aus tatsächlicher Dokumentversion/Historybenachrichtigung; die Indexverschiebung allein zählt nicht.

| Fall | Index | TextEffect | HistoryEffect | Finalization |
|---|---|---|---|---|
| Keine Bindung/Registration, kein navigierbarer Eintrag, Busy/Phase/Observerablehnung | unverändert | keine eigene Wirkung | keine eigene Wirkung, keine Updategruppe | keine angenommene Mutation; ggf. abgeschlossener unbenutzter Intent |
| Identischer Zieltext, auch leeres Dokument | verschoben | None | None | Complete |
| Tatsächliche Ersetzung | verschoben | Applied | Applied | Complete |
| Kein externer Historysubscriber, Editorbindung vorhanden | verschoben | abhängig von Textgleichheit | abhängig von tatsächlicher Ersetzung | Complete |
| Historysubscriber wirft nach erfolgreicher Ersetzung | verschoben | Applied | Applied | Complete, CallbackFailed |
| Historysubscriber wirft bei No-op | verschoben | None | None | Complete, CallbackFailed |
| Entkommener Fehler in UpdateStarted/Changing | kann bereits verschoben sein | konservativ Indeterminate | Indeterminate | Faulted |
| Entkommener Fehler in Changed/UpdateFinished/automatischer Dirtyproperty oder abgewiesener Dokumentwechsel aus Callback | verschoben, kein Rollback | konservativ Indeterminate bei entkommener nativer Exception | Indeterminate | Faulted |
| Gebundener Detach nach erfolgreicher Ersetzung | verschoben | Applied | Applied | Complete am Original |

Indeterminate verschweigt keinen möglichen Effekt und behauptet insbesondere nicht None. Ein unsicherer nativer Abschluss sperrt Folgeeingabe. Die Indexänderung wird weder als native Historywirkung ausgegeben noch zurückgerollt. Der bestehende D01-Vertrag bleibt unverändert; keine automatische Wiederholung.

## 5. Undo-/Redo-Gruppierung

Die frühere getrennte Remove/Insert-Folge wurde durch **einen nativen Document.Replace innerhalb einer RunUpdate-Gruppe** ersetzt. Dadurch entfällt ein eigener Host-Zwischenzustand nach Remove und vor Insert. Tests injizieren Fehler vor Wirkung (UpdateStarted/Changing), nach der kombinierten Textwirkung (Changed), beim Gruppenabschluss (UpdateFinished) und bei Dirty-Propertywirkung. Es gibt bewusst keinen künstlich eingeführten separaten Remove-/Insert-Testhook.

Bei identischem Text wird weder RunUpdate noch Dirtyänderung gestartet. Positive Tests prüfen echte native UndoStack-Schritte, nicht nur CanExecute: Ersetzung zurück auf Original, kein weiterer Undo-Schritt, Redo zurück auf Ziel; weitere Forwardaktion und deren Undo. Fälle umfassen leere Dokumente/letzte Zeilen, mehrzeiligen Zieltext und Unicode/Sonderzeichen. Keine Dokumentneuzuweisung, Ersatz-Undoengine oder neue Caret-/Selectionimplementierung.

## 6. Tests und Evidenz

Alle Läufe mit SDK 10.0.401, Avalonia 12.1.4, AvaloniaEdit 12.0.0 und unverändertem genehmigten Prismadapter im bestehenden Worktree.

| Lauf | Ergebnis | Beleg |
|---|---|---|
| Erster gezielter Historylauf | 12 PASS | [history.trx](../../artifacts/ap3-02/history.trx) |
| Erste vollständige GUI-Suite | 502 PASS | [gui.trx](../../artifacts/ap3-02/gui.trx) |
| Erweiterte Fehlerproben, Zwischenlauf | 18 PASS, 1 FAIL | [history-final.trx](../../artifacts/ap3-02/history-final.trx) |
| Nach korrigierter neuer Testvorbedingung | 509 PASS | [gui-verified.trx](../../artifacts/ap3-02/gui-verified.trx) |
| Finaler Debug-Build beider Lösungen | 0 Fehler; Avalonia-Lösung 1 Warnung, Root-Lösung 0 | [build-complete.log](../../artifacts/ap3-02/build-complete.log) |
| Finale vollständige GUI-Suite | **513 PASS, 0 FAIL, 0 SKIP**, einschließlich **23 neuer Historyfälle** und unveränderter 35 Coordinator-/F1-Fälle | [gui-complete.trx](../../artifacts/ap3-02/gui-complete.trx), [Log](../../artifacts/ap3-02/gui-complete.log) |

Der Zwischenfehler betraf ausschließlich die neue Propertyprobe: Der initiale Textsetter hatte IsModified bereits gesetzt, sodass kein weiterer Propertywechsel und damit kein injizierter Fehler auftrat. Die Probe setzt jetzt vor Subscriberregistrierung IsModified=false. Die Faulted-Assertion blieb unverändert; keine bestehende Assertion wurde geändert. Alle Zwischenprotokolle bleiben erhalten.

Neue Fälle: sechs Text-/No-op-Varianten, sechs Ablehnungszustände, zwei Historyobserverfehlerfälle, sieben Fehler-/Lifecyclevarianten, fehlende Editorbindung und kombinierte Dokument-/Property-/Service-Reentranz. Assertions nach den Callbacks prüfen tatsächliche Endeffekte, damit abgefangene Callbackexceptions keinen falschen Test-PASS erzeugen.

Befehle: `pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug`; gezielt `dotnet test ... --no-build --no-restore --filter FullyQualifiedName~HistoryCoordinatorTests`; finale GUI-Suite ohne Filter, TRX jeweils separat. Core-/MCP-Projekte wurden gebaut, deren Tests nicht erneut ausgeführt: keine geänderten Core-/MCP-Abhängigkeiten, Verträge oder Ausführungspipeline. S-01-Diagnostik und EC21 unverändert, vorhandene GUI-Regressionen eingeschlossen.

## 7. Native Windows-Abnahme

**OPEN / nicht durchgeführt.** Headless-Commands und native UndoStack-Aufrufe sind keine physische Windows-Button-/Ctrl+Z/Y-Abnahme. Vor Produktabnahme sind echte MainView-Historybuttons, mehrere Back/Forwardaktionen, anschließend Ctrl+Z/Y, Fokuswechsel und Schweizer Unicodeeingabe zu prüfen. Keine Änderung der dauerhaften IME-/Textdragsperre; CJK-Restrisiko unverändert akzeptiert.

## 8. Verbleibende Findings und Grenzen

Keine bestätigten kritischen oder hohen offenen Fehler in den ausgeführten P1-Proben. Folgende Nachweisgrenzen bleiben explizit:

- **Medium, Verifikationslücke:** Native Windowsfolge fehlt; Reproduktion/Abnahme siehe Abschnitt 7.
- **Low, konservative Einschränkung:** Ein entkommener nativer Callbackfehler nach Indexverschiebung sperrt den Editor als Faulted, auch wenn der sichtbare Zieltext vollständig erscheint. Reproduktion: `FailuresAndLifecycleRemainBoundToOriginal("changed")`. Kein Rollback/Retry; beabsichtigte bestehende D01-Sicherheitsentscheidung.
- **Reviewpunkt:** Die Verwendung eines einzelnen Replace bewahrt die native Ersetzungssemantik, ersetzt aber die frühere zweistufige Remove/Insert-Ereignisfolge. Mehrzeilentext und Undo/Redo sind geprüft; zusätzliche produktspezifische Anchor-/Selectionbeobachter sind beim unabhängigen Review zu berücksichtigen. Allgemeine Observerintegration bleibt P6.

Historische R2/R4/R6/R7, EC21, Return-/Save-/Dialogdrain und sonstige AP3-01-Lücken werden durch diese lokalen Tests nicht geschlossen. Keine weitere Architekturarbeit zur Behandlung dieser Grenzen begonnen.

## 9. Empfehlung und Stop-Gate

**READY FOR INDEPENDENT REVIEW**, mit weiterhin offenem nativen Windows-Abnahme-Gate. Prüfschwerpunkte: Bindungslebensdauer, frühe Indexadmission auch bei direkten Aufrufen, einmaliger Replace, Index-/Text-/Historytrennung und originale Finalisierung bei Detach/Exception.

Git-Diffprüfung ohne Whitespacefehler; ursprüngliche AP2.1-/F1-Testdateien unverändert. Ausschließlich die oben genannten zwei Produktionsdateien, neue Tests und dieser Bericht gehören zum Patch. AP3-01 bleibt erhalten. Kein Commit, Push oder Merge. Am Stop-Gate angehalten; keine automatische AP3-03-Fortsetzung.
