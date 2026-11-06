# MCP-00-AP2.1-F1 – Correction Review

Stand: 26.10.2026. **Abschlussstatus: READY FOR INDEPENDENT RE-REVIEW.** Die vier verbindlichen Findings F01, F02, F04 und F03 sind im Coordinator korrigiert und durch zwölf neue permanente Testfälle abgesichert. Die vollständigen GUI-, Core- und MCP-Läufe bestehen. Dies ist der Korrekturbericht des ausführenden Agents, keine unabhängige Abnahme und keine Freigabe für MCP-Besitzwechsel, HTTP oder AP3–AP6.

## 1. Ausgangsbasis und Delta

Produkt-Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Mcp`. Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`, Upstream `origin/master`, Abstand +4/-10. Vorher: sechs modifizierte getrackte Dateien, 26 ungetrackte Dateien, Staging leer. Der tatsächliche Ausgangsstand enthält bereits uncommittete MCP-01.4-Arbeiten sowie AP2.1 und dessen unabhängigen Review.

Der bestehende Prüfbereich `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp00-ap21` mit `originals/`, `baseline.json` und `ap21-only.diff` wurde als Abgrenzung des ursprünglichen AP2.1-Deltas berücksichtigt und unverändert erhalten. Ein Git-Diff gegen HEAD allein würde die bereits ungetrackten Coordinatorquellen nicht erfassen.

Neuer separater Prüfbereich: `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp00-ap21-f1`.

- `baseline.json`: vor Änderungen erfasste SHA-256-Werte von 24.537 Originaldateien und Git-Ausgangszustand.
- `baseline.diff`: bestehender getrackter Diff vor F1.
- `originals/`: exakte Vorherkopien der beiden bearbeiteten Quelldateien.
- `f1-only.diff`: tatsächliche Korrektur gegenüber diesen Vorherkopien, einschliesslich ungetrackter Dateien.
- `results/*.trx` und `*.log`: neue Prüfevidenz; keine historischen Testresultate ersetzt.
- `verification.json`, `test-summary.json`: maschineller Erhaltungs- und Ergebnisausweis.

Das fachliche Produktdelta umfasst ausschliesslich:

| Datei | Änderung |
|---|---|
| `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` | Eigentümerschaft, Callbackabschluss, Commitprüfung und Drainidentität |
| `CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs` | Acht neue Testmethoden mit zwölf Fällen, nach dem unveränderten bisherigen Testteil angefügt |
| `docs/mcp/MCP-00-AP2.1-F1-correction-review.md` | Dieser neue Bericht |

EditorBridge, WorkbenchTextEditor und WorkbenchTextArea bleiben byteidentisch; deren vorhandene Bindungs-/Inputwege werden durch die Coordinator-Korrektur und Tests erfasst. Keine zweite Besitzregistry, kein zusätzlicher öffentlicher API- oder Testbypass.

## 2. Korrektur F01 – Eigentümerschaft

Referenz: `EditorCoordinator.cs:64`, insbesondere Attach/BeforeAttach.

Die gespeicherte alte Registration ist weiterhin die einzige lokale Bindung. Beim Wechsel auf einen anderen Editor wird ihr Coordinator-Verweis jetzt nur entfernt, wenn er noch exakt auf diesen Coordinator zeigt. Gehört der alte Editor inzwischen Service B, kann Service A dessen Verknüpfung nicht mehr löschen. Die Generation wird vor dem Lösen der alten Verknüpfung geprüft/fortgeschrieben. Eine mögliche Übernahme aus Unavailable berücksichtigt zusätzlich einen noch laufenden Observer.

Nachgewiesene Folge: A.attach(E) → A.detach(E) → B.attach(E) → B.BeginDrain → A.attach(E2). E bleibt an B gebunden. Echte geroutete Headless-Texteingabe, direktes PerformTextInput und native Ctrl+Z/Ctrl+Y werden während B.Releasing abgewiesen; Dokument, Revision und verfügbare Undo-/Redorichtungen bleiben erhalten. Nach B.ResumeGui funktioniert die ursprüngliche History weiter.

Der Test ruft zusätzlich einen veralteten Detach und zweimal DisposeAsync von A auf. Die Folgeprobe prüft fehlgeschlagenes Attach bei Pending, fehlgeschlagenes Attach/Documentwechsel unter IdentityLocked sowie den bis zum Ende der Identity-Lease aufgeschobenen Detach. Die anschliessende neue Servicebindung bleibt auch beim späteren Attach des alten Service erhalten.

Keine pauschale neue Exception-Recovery für alle Hostpfade wird behauptet. Bestehende Dokument-/Observer-/Attachregressionen bleiben zusätzlich aktiv.

## 3. Korrektur F02 – Callback-Lebensdauer

Referenz: `EditorCoordinator.cs:321` (CallbackTicket), `:249` (Finish).

CallbackTicket führt die aktuelle Anzahl laufender Observe-Ausführungen und eine separate Dispose-Anforderung. Während einer Ausführung bleibt das Callbackticket samt Zähler erhalten. Dispose merkt den Abschluss nur vor; weitere Observe-Eintritte auf ein zum Abschluss vorgemerktes Ticket werden verworfen. Erst nach Rückkehr der letzten laufenden Ausführung reduziert Complete genau einmal Callback- und Pendingzähler und ruft Finish auf.

Die Exception des eigentlichen Callbackkörpers wird vor dieser Freigabe auf dem Originalticket als CallbackFailed erfasst. Das gilt auch bei gesättigtem diagnostischem Fehlerzähler. Die bisherige Erkennung eingeschlossener kontrollierter Observerfehler bleibt bestehen. Der Observerkontext verhindert weiterhin neue vermittelte Mutationen.

Neue Nachweise:

- Body beendet, Callback startet, Dispose zweimal im Callback, danach Throw: Completion bleibt im Callback unvollständig; Pending bleibt zwei (Operation plus laufender Callback).
- Erst nach Callbackende entsteht der terminale Beleg mit CallbackFailed und Complete.
- Derselbe Ablauf nach tatsächlichem Textedit behält TextEffect=Applied und HistoryEffect=Applied; natives Undo bleibt korrekt.
- Ohne Edit bleiben beide Wirkungen None.
- Verschachtelte Observe-Ausführung mit innerem Dispose finalisiert den äusseren Callback nicht vor dessen späterer Exception.
- Idempotentes Dispose, gesperrte neue Callbackausführung nach Dispose-Anforderung und Producer-Sealing werden geprüft.

Die Anzahl gleichzeitig ausstehender Operations-/Callbacktickets bleibt auf 64 begrenzt. Es handelt sich um lokale Abschlussverpflichtungen, nicht um einen Nachweis des vollständigen Registry-/Providerdrains.

## 4. Korrektur F04 – Erneute Commitzulassung

Referenz: `EditorCoordinator.cs:147`, `:249`.

Commit prüft vor der Action weiterhin Owner, Ticketbestand, BodyDone, Active und ObserverDepth. Danach muss sowohl die Originalbindung über IsCurrent als auch die aktuelle CanBegin-Bedingung gelten. Damit werden Kontrollgeneration, Gui-Phase, Bridge-/Editor-/Dokumentidentität, aktuelle Revision, MutationLocked, IdentityLocked und eine inzwischen offene Dokumentupdategruppe vor Wirkung geprüft.

Bei Ablehnung wird das Ticket mit Stale finalisiert; keine Action wird delegiert. Ein neues internes Merkmal MutationStarted unterscheidet den tatsächlich begonnenen Mutationskörper vom nur angenommenen Intent. Finish schreibt eine fremde offene Updategruppe einem abgewiesenen Intent nicht mehr als eigene unklare Finalisierung zu. Sein Beleg bleibt None/None/Complete mit Fehler. Eine tatsächlich gestartete, nicht sauber abgeschlossene Mutation faultet unverändert konservativ.

Die neue Regression nimmt bei geschlossenem Dokument ein Ticket an und öffnet danach jeweils eine fremde Updategruppe, Identity-Lease oder Mutation-Lease. Commit muss vor Actionausführung ablehnen. Geprüft werden unveränderte Textversion und Bridge-Snapshot, unveränderte Historyverfügbarkeit und null History-Propertysignals während Commit. Die fremde Gruppe bleibt offen, bis ausschliesslich deren Testbesitzer sie schliesst.

Die native TextArea kann beim Öffnen/Schliessen einer leeren fremden Updategruppe selbst optionale Historywirkungen verursachen. Daher vergleicht der abschliessende Undo-/Redonachweis mit einer separaten gleichen Hostfolge ohne Coordinator.Commit. Die erste neue Testfassung hatte pauschal den Redozustand vor der fremden Gruppe erwartet; diese unbelegte neue Erwartung wurde durch die strengere ursachentrennende Kontrollprobe ersetzt. Keine bestehende Assertion wurde geändert.

Der positive Gegenfall führt LockIdentity, RunUpdate, AllowSingleEditChange und Insert erst innerhalb der angenommenen Action aus. Dieser eigene atomare Edit bleibt zulässig, wird Applied/Applied belegt und lässt sich nativ rückgängig machen und wiederholen. Keine EndUpdate-Reparaturschleife, kein fremdes Gruppenende und kein kompensierendes Undo im Produkt.

## 5. Korrektur F03 – Kontrollübergang und Fokusgeneration

Referenz: `EditorCoordinator.cs:205` (BeginDrain), `:216` (ResumeGui), `:273` (DrainTicket).

Generation bleibt der Zaun für alte Eingabe-/Callbackintents und wird durch Fokusinvalidierung weiterhin erhöht. Die Drainidentität ist dagegen die konkrete, vom Coordinator aktuell gehaltene DrainTicket-Instanz samt Owner. Sie wird nicht mehr mit der veränderlichen Eingabegeneration verglichen.

ResumeGui verlangt zusätzlich zum exakten aktuellen Zertifikat: erfolgreich abgeschlossene Completion, Phase Releasing, null Pending, keine aktive Operation, keinen Observer, die weiterhin aktuelle angehängte Registration samt Dokument, keine offene Updategruppe und keine Identity-/Mutation-Locks. Nach erfolgreichem Resume wird die Eingabegeneration erneut erhöht und die aktuelle Drainreferenz entfernt.

BeginDrain ist während desselben Übergangs idempotent. Wiederholtes Resume mit dem verbrauchten Zertifikat liefert false und hat keine neue Wirkung, entsprechend dem unveränderten bisherigen Testvertrag. Eine alte oder fremde Instanz kann einen folgenden Drain nicht beenden. Faulted und noch laufende Verpflichtungen verhindern die Freigabe.

Zwei neue Fälle erzeugen echten LostFocus durch Fokusübernahme einer zweiten vorhandenen Headless-Hostfixture: nach bereits abgeschlossenem Drain und während noch offenem Callbackdrain. Die Generation steigt tatsächlich. Der offene Drain bleibt gesperrt, bis sein Callback disposed ist; der abgeschlossene eigene Übergang lässt sich danach sicher ohne Detach-/Reattach beenden. Alte Callbacks und Operationstickets erhalten dadurch keine neue Berechtigung. Observer-, Fremdzertifikat-, verbrauchtes Zertifikat-, Nachfolgerdrain- und Faulted-Ablehnung sind zusätzlich geprüft.

## 6. Neue dauerhafte Regressionstests

Alle Positionen beziehen sich auf `CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs`.

| Methode / Position | Fälle | Nachweis |
|---|---:|---|
| StaleServiceCannotUnbindSuccessorOrEnableInputAndHistory, Zeile 438 | 1 | F01, Stale-Detach, mehrfaches Dispose, TextInput/PerformTextInput, Ctrl+Z/Y und History |
| FailedAttachAndDocumentChangePreserveCurrentBindingAndDeferredDetach, Zeile 467 | 1 | Attach-/Document-Abweisung, Identity-gebundener Detach, Nachfolgerbindung |
| CallbackSelfDisposeWaitsForExitAndPreservesActualEffects, Zeile 497 | 2 | F02 ohne/mit Edit, D01, Zähler, Observermutation, Sealing |
| NestedCallbackSelfDisposeCannotCompleteOuterExecution, Zeile 534 | 1 | F02 bei verschachteltem Callback und späterem äusserem Fehler |
| CommitRechecksCurrentUpdateAndLocksBeforeAnyEffect, Zeile 555 | 3 | F04 für Update, IdentityLocked und MutationLocked |
| OwnAtomicEditCanAcquireIdentityAndUpdateAfterCommitAdmission, Zeile 615 | 1 | Eigene zulässige Atomarität und natives Undo/Redo |
| ActualFocusLossPreservesOwnDrainIdentityButInvalidatesInput, Zeile 639 | 2 | F03 bei beendetem/laufendem Drain, echte Fokusereignisse |
| ForeignOldObserverAndFaultedDrainsCannotReleaseSuccessor, Zeile 670 | 1 | Fremde/alte Zertifikate, Observer, Folgedrain und Faulted |

Summe: zwölf neue Fälle, insgesamt 35 Coordinatorfälle. Der gesamte bisherige Testinhalt bis unmittelbar vor der schliessenden Klassenklammer wurde am Ende bytegenau gegen die Vorherkopie geprüft; alle ursprünglichen 23 Fälle und ihre Assertions bleiben erhalten.

Die bestehende Suite prüft weiterhin Unicode, IME/Textdrag, Clipboard/Cut/Paste und verzögerte Paste, Originaldokument-/Detachbindung, kontrollierte Observer, Producer-Sealing, Fehlerbelege, native History, direkte PerformTextInput-Aufrufe und die Ticketgrenze. Keine Tests deaktiviert oder native Bindings ersetzt.

## 7. GUI-/Core-/MCP-Ergebnisse

Ausführung im angegebenen Produkt-Worktree mit vorhandenen Abhängigkeiten, `dotnet test --no-restore`; kein Paketupgrade. Neue Ergebnisse liegen ausschliesslich im F1-Prüfbereich.

| Lauf / Roh-TRX | PASS | FAIL | Nicht ausgeführt | Einordnung |
|---|---:|---:|---:|---|
| coordinator-before-fix.trx | 25 | 10 | 0 | Neue Gegenbeispiele gegen ursprünglichen Coordinator reproduziert |
| coordinator-after-fix.trx | 34 | 1 | 0 | Fehler der neuen pauschalen History-Erwartung, siehe Abschnitt 4 |
| coordinator-final.trx | 35 | 0 | 0 | Gezielter Lauf vor abschliessendem Encodingabgleich |
| gui-verified-final.trx | 469 | 0 | 0 | Massgeblicher vollständiger GUI-Lauf; enthält alle 35 Coordinatorfälle |
| core-final.trx | 7.667 | 0 | 5 | 7.672 Roh-TRX-Einträge, vier reguläre Skips plus ein Explicit-Test |
| mcp-final.trx | 89 | 0 | 1 | 90 Roh-TRX-Einträge, ein Explicit-Test |

Transparenz zum Ablauf: Der abschliessende Delta-Abgleich erkannte eine durch die Windows-Standardkodierung eines Bearbeitungsskripts verursachte temporäre Umlaut-/Unicodeveränderung im ursprünglichen Testteil. Dieser Teil wurde bytegenau aus der Vorherkopie wiederhergestellt, ohne alte Assertions neu zu formulieren. Danach wurde die gesamte GUI-Suite erneut gebaut und ausgeführt: `gui-verified-final.trx` ist der massgebliche Endnachweis. Die früheren Ergebnisse bleiben als Zwischenevidenz erhalten; die neuen Gegenbeispielkörper waren ASCII und reproduzierten dort bereits die beschriebenen Zustandsfehler. Core/MCP waren von der Testdateikodierung nicht betroffen.

Vollständige Liste nicht ausgeführter Fälle:

| Suite / Test | Grund |
|---|---|
| Core: NativePublicationDoesNotFollowLateSymlink | Symlink-Erstellungsprivileg fehlt |
| Core: TargetSymlinksIncludingDanglingLinksAreRejected(False) | Symlink-Erstellungsprivileg fehlt |
| Core: TargetSymlinksIncludingDanglingLinksAreRejected(True) | Symlink-Erstellungsprivileg fehlt |
| Core: MacOsInheritedExtendedAclIsRemovedBeforePublication | Darwin-ACL-Plattformfall auf Windows |
| Core: MeasureLocalHardware | Explicit, manuelle Hardwaremessung |
| MCP: InstalledCodexDiscoversAndCallsStatus | Explicit, erfordert CRYPTOWORKBENCH_CODEX_PATH |

Die Konsolenangaben „Core 4 Skips“ und „MCP 0 Skips“ enthalten die Explicit-Fälle nicht. Massgeblich sind die einzelnen UnitTestResult-Einträge mit Outcome=NotExecuted; der aggregierte TRX-Zähler notExecuted ist hier trotz dieser Einträge null. Diese Fälle werden nicht als PASS gewertet.

Reproduktion, Ergebnisse jeweils in ein neues separates Ergebnisverzeichnis schreiben:

```powershell
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --filter FullyQualifiedName~EditorCoordinatorTests --logger 'trx;LogFileName=coordinator.trx' --results-directory <Pruefverzeichnis>
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --logger 'trx;LogFileName=gui.trx' --results-directory <Pruefverzeichnis>
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --logger 'trx;LogFileName=core.trx' --results-directory <Pruefverzeichnis>
dotnet test CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj --no-restore --logger 'trx;LogFileName=mcp.trx' --results-directory <Pruefverzeichnis>
```

Die vorhandenen CS0618-Warnungen betreffen weiterhin Clipboard.GetTextAsync und Headless-KeyPress/KeyRelease. Weitere im Build protokollierte Nullable-Warnungen betreffen bestehenden GUI-Code. Die Korrektur führt weder neue Paketversionen noch neue obsolete API-Aufrufe ein. Buildlogs bleiben vollständig verfügbar.

## 8. Noch offene Risiken und Erhaltung

Der historische intermittierende NullReferenceException-Befund in HeadlessUnitTestSession.Dispose bleibt **OPEN / Ursache INCONCLUSIVE**. Er trat in den F1-Läufen nicht auf; daraus folgt kein Ursachennachweis oder Fix. Weder Fixture noch alte Assertions wurden dafür geändert.

Native Ctrl+Z/Y wurden über die echte Avalonia-Headless-Routing-/Commandkette geprüft. Ein eigener vollständiger physischer Windows-Tastatur-/OS-Clipboarddurchlauf ist weiterhin nicht durchgeführt. Es wurde kein natives Windows-Prüffenster verwendet. Historische U1-/U2-Befunde und die genehmigte IME-Risikoakzeptanz bleiben unverändert.

AP3–AP6 bleiben offen: vollständige GUI-/Observerinventur, Registry-/Save-/Dialog-/Passwortdrain, AP5/EC21 und D02-Epochenintegration. Die vier Korrekturen beweisen weder globales L2 noch MCP-v2-Autorisierung. Kritische Bridge-Snapshotfehler und vollständige Host-/Providerintegration behalten die im unabhängigen Bericht genannten Aussagegrenzen.

Erhaltungsprüfung gegen 24.537 Originaldateien:

- Genau zwei geänderte Quelldateien: Coordinator und die um neue Fälle ergänzte Testdatei.
- 16 geänderte Dateien unter bin/obj; vollständige Einzelpfade und Vorher-/Nachherhashes in `verification.json`.
- Davon 15 Buildprodukte: GUI-/Produkt-DLLs, PDBs, Referenzassemblies und AssemblyReference.cache; ein testgeneriertes `CryptoScriptUnitTest/bin/Debug/net10.0/g03-csharp-vectors.txt`.
- Alle übrigen Originaldateien, bestehende uncommittete Arbeiten, historischen Evidenzen und erfassten Git-Metadaten byteidentisch.
- Der ursprüngliche unabhängige Reviewbericht und sämtliche Architekturberichte unverändert.
- HEAD und Staging unverändert; keine Git-Mutation. Die zwei bearbeiteten Quellen waren bereits ungetrackt, weshalb der Porcelain-Status ihre Inhaltsänderung nicht zusätzlich anzeigt; `f1-only.diff` und Hashvergleich weisen sie aus.
- `.codex/config.toml` wurde vor Inhaltszugriff ausgeschlossen, weder gelesen noch verändert.

Der neue Bericht wird zusätzlich als einzige neue Produktdatei geführt. Prüfbereich, Logs und Baseline liegen separat und ersetzen keine historische Evidenz.

## 9. Auswirkungen auf die Avalonia-12-Migrationsvorbereitung

Die Korrekturen stabilisieren interne Lebensdauer- und Zulassungsregeln unabhängig von einer Bibliotheksmigration. Die zwölf Regressionen sind zusätzliche wiederverwendbare Prüffälle für einen später ausdrücklich beauftragten Migrationslauf. Alle Aussagen dieses Berichts gelten für die aktuell verwendete Avalonia-11-/AvaloniaEdit-Konfiguration.

Keine Änderung von AvaloniaEdit, Projekt-/Paketdateien, nativen Commandregistrierungen oder CryptoScript-Semantik. Insbesondere wurden keine API-Anpassungen für Avalonia 12 vorgezogen. Headless-/Native-Äquivalenz und neue Frameworkwarnungen müssen bei einer späteren Migration erneut geprüft werden. Keine automatische Fortsetzung.

## 10. Abschliessende Empfehlung für den unabhängigen Nachreview

**READY FOR INDEPENDENT RE-REVIEW.**

Alle vier verbindlichen Blocker sind im begrenzten F1-Delta korrigiert; zwölf dauerhafte Regressionen und vollständige GUI-/Core-/MCP-Läufe stützen dies. Empfohlen wird die unabhängige Prüfung des konkreten F1-Diffs, besonders der Eigentümerbedingung, der Reihenfolge Callbackfehler → Callbackende → Finish, der Unterscheidung angenommener Intent/tatsächlich gestartete Mutation und der aktuellen Drainreferenz als Übergangsidentität.

Die Empfehlung ist ausdrücklich keine AP2.1-Abnahme und keine Produktfreigabe. Keine Umsetzung von AP3–AP6, kein Commit, Push, Merge, Reset, Rebase oder Stash erfolgte.