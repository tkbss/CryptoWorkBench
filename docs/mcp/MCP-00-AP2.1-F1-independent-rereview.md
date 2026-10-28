# MCP-00-AP2.1-F1-R2 – Independent Correction Re-Review

Stand: 26.10.2026. **Gesamtbewertung: READY FOR AP2.1 ACCEPTANCE.**

F01, F02, F04 und F03 sind im geprüften F1-Stand korrigiert. Alle vier ursprünglichen Gegenbeispiele wurden gegen die korrigierten Produktbinaries erneut ausgeführt; zusätzlich bestehen acht ergänzende Übergangsproben sowie 35/35 Coordinator- und 469/469 GUI-Tests. Kein neuer blockierender Fehler ist im untersuchten Umfang nachgewiesen. Diese Bewertung gilt für den begrenzten AP2.1-Grundkern, nicht für globale Hostkoordination, MCP-Besitzwechsel oder HTTP.

## 1. Tatsächliche Reviewbasis und F1-Delta

Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Mcp`. Tatsächlicher Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`, Upstream `origin/master`, +4/-10. Reviewbeginn: sechs modifizierte getrackte Dateien, 27 ungetrackte Dateien, Staging leer.

Geprüft wurden die aktuellen Quellen, nicht nur die Berichte. Vertrags- und Evidenzbasis:

- ursprünglicher unabhängiger AP2.1-Review und dessen vollständige Gegenbeispielprobe;
- F1-Korrekturbericht und ursprünglicher AP2.1-Implementierungsbericht;
- aktueller AP0-Vertrag einschliesslich O14/D01 und datierter IME-Eigentümerergänzung;
- D02-S1, insbesondere die Vertrauensannahmen zur atomaren Admission/Commitprüfung und zum vollständigen Callbackabschluss;
- `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp00-ap21-f1`: `f1-only.diff`, Vorherkopien, Hashbaseline, Endverifikation, sämtliche 22 manifestierten Artefakte und Roh-TRX.

**Basisprüfung:** Keine Hashabweichung gegenüber dem dokumentierten F1-Endstand, einschliesslich Produktbinaries. Der neu aus aktuellen Quellen und Vorherkopien berechnete Diff ist exakt gleich `f1-only.diff`. Die ursprünglichen 23 Coordinatorfälle sind vollständig vorhanden; der ursprüngliche Quelldateiteil bis zur letzten Klassenklammer ist byteidentisch zur F1-Vorherkopie.

Das tatsächliche F1-Quellendelta bleibt auf EditorCoordinator.cs und ergänzte EditorCoordinatorTests.cs beschränkt. Der Bericht ist eine weitere neue Dokumentdatei. Die bereits uncommitteten MCP-01.4-Arbeiten sind Teil der Arbeitsbasis; ein Git-Diff allein würde die ungetrackten Quellen nicht ausreichend vergleichen.

Dieser Nachreview ist ein methodisch getrennter kritischer Prüfgang mit neuen Ausführungen und eigener Evidenz. Er wurde im selben Agent-/Arbeitskontext wie die Korrektur durchgeführt und ist **keine personell oder organisatorisch unabhängige Drittzertifizierung**.

Neue Reviewevidenz liegt getrennt unter `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp00-ap21-f1-r2`. Der F1-Prüfbereich wird nur gelesen. Vor Beginn wurden 24.561 Originaldateien einschliesslich der bisherigen Evidenzen und erfassten Git-Metadaten in einer neuen Baseline gesichert. Der abschliessende Erhaltungsnachweis steht am Berichtsende.

## 2. F01 – Ergebnis

**Bestätigt korrigiert.** Relevante Quellen: EditorCoordinator.cs:64–92, EditorBridge.Attach/Detach, WorkbenchTextEditor.Coordinator und WorkbenchTextArea.Allowed/CommandBindings.

Die retained Registration des alten Coordinators reicht nicht mehr zum Lösen einer fremden Verknüpfung: Die Zuweisung null verlangt zusätzlich die exakte Referenzgleichheit `old.Coordinator == this`. Derselbe serviceeigene Coordinator hält weiterhin nur eine Registration; es entsteht keine zweite globale Besitzquelle. BeforeAttach verhindert die Übernahme eines noch verfügbaren, arbeitenden oder beobachtenden Fremdcoordinators. Die Fortentwicklung der Generation erfolgt vor dem Lösen der alten Assoziation.

Unverändert wiederholte ursprüngliche Probe:

```text
F01 B.phase=Releasing; E.Coordinator=null:False; text=base
```

A.attach(E) → A.detach(E) → B.attach(E) → B.BeginDrain → A.attach(E2) entfernt B nicht mehr. Der permanente Test erweitert diese Folge um veraltetes Detach, mehrfache Serviceentsorgung, geroutetes TextInput, direktes PerformTextInput, Ctrl+Z/Ctrl+Y, unveränderten Snapshot und nutzbare History nach gültigem Resume. Er besteht erneut.

Zusätzlich geprüft: fehlgeschlagenes Attach bei ausstehenden Tickets, direkte zweite Serviceinstanz, unter IdentityLocked abgewiesenes Attach/Documentwechsel sowie aufgeschobener Detach nach Ende der Identity-Lease. Die bestehenden Originaldokument-/Detach-/Reattachtests bestehen. Fehler werden nicht durch eine unbedingte Entkopplung oder eine neue Ersatzregistry behandelt.

## 3. F02 – Ergebnis

**Bestätigt korrigiert für registrierte synchrone Callbackwirkungen.** Relevante Quellen: EditorCoordinator.cs:321–368, OperationTicket.Dispose/SealProducer und Finish:249.

Dispose-Anforderung und tatsächlicher Abschluss sind getrennt. Der Ausführungszähler verhindert Complete, solange mindestens eine Observe-Ausführung dieses Tickets läuft. Das Callback bleibt währenddessen in den Pending-/Callbackzählern. Weitere Observe-Aufrufe nach Dispose-Anforderung werden nicht neu begonnen. Complete ist idempotent und ruft Finish erst nach der letzten Ausführung auf.

Die Exception des Callbackkörpers wird vor Complete dem Originalticket zugeordnet. Kontrollierte verschachtelte Observerfehler werden weiterhin erkannt. Applied-Wirkungen werden nicht auf None zurückgesetzt. Bei Self-dispose entstehen weder ein vorzeitig terminaler Beleg noch ein vorzeitig fertiggemeldeter lokaler Drain.

Unverändert wiederholte ursprüngliche Probe:

```text
F02 completionInsideCallback=False; receipt.error=CallbackFailed; observerFailures=1
```

Die permanenten Fälle mit/ohne angewandtem Edit und verschachtelter Observe-Ausführung bestehen. Die neuen separaten Proben ergänzen:

| Folge | Beobachtung |
|---|---|
| Self-dispose → BeginDrain → Throw innerhalb des Callbacks | Receipt und Drain im Callback noch offen; Pending=2; CallbackFailed vor endgültigem Abschluss |
| Zwei Callbacktickets, Dispose beider im laufenden ersten Callback, Fehler in eingeschlossenem Observer | Kein vorzeitiger Receipt; Applied/Applied/CallbackFailed; Pending danach 0 |
| Callbackfehler/Self-dispose vor BodyDone, anschliessend SealProducer | Body bleibt ausstehend; erst Body-Dispose schliesst mit CallbackFailed ab |

Das entspricht der lokalen Pflicht aus D02-S1 §3.2 Nr. 3: Ein erfasster Callback darf nicht vor Ende seiner Wirkungen verschwinden. Es ist **kein Refinement-Nachweis des gesamten D02-Modells**. Dispose des OperationTicket versiegelt hier zugleich den Producer; spätere allgemeine Hostproducer, Dispatcherposts und async-void-Fortsetzungen sind dadurch nicht automatisch erfasst. Registry-, Save-, Passwort- und Returnketten bleiben ausserhalb dieses lokalen Nachweises.

## 4. F04 – Ergebnis

**Bestätigt korrigiert.** Relevante Quellen: EditorCoordinator.cs:147–186, CanBegin/IsCurrent sowie Finish:249–270.

Commit prüft vor Actiondelegation Ticketowner/-bestand, BodyDone, Active und ObserverDepth. IsCurrent prüft die ursprüngliche Generation, Gui-Phase, Bridge-/Registration-/Editoridentität, Originaldokument und Revision. Die zusätzliche CanBegin-Prüfung verlangt die aktuelle gebundene Registration, geschlossene Updategruppe und freie Mutation-/Identity-Locks. Zwischen letzter Prüfung und Beginn der Action liegt kein await oder fremder Callback.

Unverändert wiederholte ursprüngliche Probe:

```text
F04 openUpdate commitRan=False; commitReturn=False; text=base; phase=Gui
```

| Bedingung nach ursprünglicher Admission | Nachweis |
|---|---|
| Fremdes BeginUpdate | Permanente Probe und ursprüngliches Gegenbeispiel: keine Delegation, keine Text-/Historywirkung; Gruppe bleibt offen |
| IdentityLocked / MutationLocked | Beide permanenten Varianten: frühe Ablehnung, None/None/Complete mit Stale |
| Veraltete Revision | Zusätzliche Probe nach realem Dokumentedit: keine Action, kein zusätzlicher Text-/Historyeffekt, Ticketabschluss |
| Veraltete Generation | Zusätzliche Probe nach Intentinvalidierung: Ablehnung und Abschluss ohne Wirkung |
| Dokumentwechsel | Bestehende Originaldokumentprobe: keine Mutation des Nachfolgers, alter Receipt bleibt an Original gebunden |
| Registrationwechsel | Bestehende Detach-/Reattachprobe: altes Ticket erhält keine Berechtigung auf neuen Editor |
| Observerreentranz | Zusätzliche Probe: keine Action im Observer; gültiger noch offener Intent kann danach regulär angenommen werden |
| Eigene atomare Mutation | Neuer positiver Test mit LockIdentity/RunUpdate/AllowSingleEditChange/Insert besteht, einschliesslich Undo/Redo |

MutationStarted bedeutet „native Action wurde begonnen“, nicht „Text wurde nachweislich geändert“. Die eigentliche D01-Text-/Historyerfassung bleibt separat. Bei einem vor Actionbeginn verworfenen Intent ist eine fremde offene Gruppe kein eigener Finalisierungsfehler; Finish liefert None/None ohne sie zu schliessen. Sobald eine Action begonnen hat, bleiben unklare Abschlüsse konservativ Faulted.

Zusätzliche Prüfung des anderen Zweigs: tatsächlicher Edit → noch offenes Callbackticket → fremde Updategruppe → Callbackabschluss. Ergebnis Applied/Indeterminate/Faulted; die offene Gruppe wird nicht vom Produkt geschlossen. Die neue Unterscheidung versteckt damit keine nachgewiesene Textwirkung.

Die F04-Testkorrektur im F1-Paket ist sachlich gerechtfertigt: Eine separate native Kontrollgruppe ohne Commit zeigt die eigenen optionalen Historywirkungen von BeginUpdate/EndUpdate. Der zu prüfende Commit wird durch null Actiondelegation, unveränderte Version/Snapshot und null Historysignale isoliert; die spätere History wird mit der Kontrollfolge verglichen. Das ist keine Lockerung einer historischen Assertion.

## 5. F03 – Ergebnis

**Bestätigt korrigiert.** Relevante Quellen: EditorCoordinator.cs:205–236 und DrainTicket:273; tatsächlicher LostFocus-Eingang WorkbenchTextArea.cs:36.

Die aktuelle DrainTicket-Referenz mit ihrem Owner bezeichnet den Kontrollübergang. Die Eingabegeneration invalidiert weiterhin alte Operations-/Callbackintents, entscheidet aber nicht mehr über die Identität dieses bereits begonnenen Übergangs. ResumeGui verlangt das exakte aktuell gehaltene Ticket und prüft erfolgreiches Ende, Releasing, leere Verpflichtungen, fehlende Active-/Observerausführung, aktuelle Registration, Dokument sowie freie Update-/Locksituation erneut.

Die unveränderte ursprüngliche Probe meldet:

```text
F03 actualFocusLoss generation=2->3; drainComplete=True; resume=True; sameDrain=False; phase=Releasing
```

**Ausgabeinterpretation:** Die ursprüngliche Diagnose ruft nach erfolgreichem Resume innerhalb der Berechnung von sameDrain erneut BeginDrain auf. Deshalb zeigt die letzte Ausgabe bereits den neu gestarteten Folgedrain als Releasing. Entscheidend sind resume=True und die neue, nicht identische Draininstanz; dies ist kein Wiederauftreten des alten Hängers.

Nachweise:

- Echter LostFocus durch zweiten Headless-Host sowohl während offenem als auch nach abgeschlossenem Drain: alte Intents bleiben ungültig; gültiger eigener Drain wird nach tatsächlichem Abschluss wiederaufnehmbar.
- Wiederholtes BeginDrain liefert innerhalb desselben Übergangs dieselbe Instanz.
- Wiederholtes Resume mit verbrauchtem Ticket bleibt wirkungslos/false.
- Fremde und alte Zertifikate können einen neuen Übergang nicht freigeben.
- Noch offene Callbacktickets, Observerkontext und Faulted verhindern Resume.
- Dokumentwechsel während Releasing wird abgewiesen.
- Separat geprüft: Detach/Attach eines Nachfolgers verwirft das alte Drainrecht; nur das neue eigene Zertifikat beendet dessen Übergang.
- Alte Operationstickets werden durch die Wiederaufnahme nicht reaktiviert.

Die logische Blockade durch normalen Fokusverlust ist behoben; ein tatsächlich nicht terminierender Provider bleibt zulässigerweise ein Verfügbarkeitsblocker.

## 6. Neue Findings nach Severity

**Keine neuen bestätigten Critical-, High-, Medium- oder Low-Findings im untersuchten F1-Korrekturumfang.** Insbesondere wurde kein konkreter neuer AP2.1-Blocker nachgewiesen. Die vier ursprünglichen Findings sind durch Codeanalyse, unveränderte Gegenbeispielwiederholung und Regressionen geschlossen.

Das ist keine Behauptung vollständiger Fehlerfreiheit aller GUI- oder Bibliothekspfade. Historische offene Integrations- und Plattformnachweise bleiben in Abschnitten 8–10 ausgewiesen. Sie werden weder als erledigt noch ohne konkreten lokalen Gegenbeweis als neue F1-Fehler umklassifiziert.

## 7. Regressionstests und Rohprotokolle

Neue Ausführung auf den hashverifizierten F1-Binaries mit `--no-build --no-restore`:

| Nachweis | PASS | FAIL | Nicht ausgeführt |
|---|---:|---:|---:|
| coordinator-r2.trx | 35 | 0 | 0 |
| gui-r2.trx | 469 | 0 | 0 |
| F1 core-final.trx, erneut inhaltlich/hashgeprüft | 7.667 | 0 | 5 |
| F1 mcp-final.trx, erneut inhaltlich/hashgeprüft | 89 | 0 | 1 |

Core und MCP wurden nicht nochmals ausgeführt: Quell-, Build- und Evidenzstand stimmen vollständig mit der dokumentierten F1-Basis überein; der Nachreview änderte nichts und ergab keine neue konkrete Auswirkung auf diese Suites. Der im Auftrag genannte Anlass für ihre Wiederholung liegt damit nicht vor.

Alle zwölf neuen Fälle wurden inhaltlich geprüft und erneut ausgeführt:

| Neue Testmethode | Fälle | Schwerpunkt |
|---|---:|---|
| StaleServiceCannotUnbindSuccessorOrEnableInputAndHistory | 1 | F01, native Eingänge/History, veralteter Detach/Dispose |
| FailedAttachAndDocumentChangePreserveCurrentBindingAndDeferredDetach | 1 | Attachfehler, IdentityLocked und Nachfolger |
| CallbackSelfDisposeWaitsForExitAndPreservesActualEffects | 2 | F02, None/Applied, Zähler/Fehler/Sealing |
| NestedCallbackSelfDisposeCannotCompleteOuterExecution | 1 | Verschachtelte laufende Callbackverpflichtungen |
| CommitRechecksCurrentUpdateAndLocksBeforeAnyEffect | 3 | F04 Update/Identity/Mutation |
| OwnAtomicEditCanAcquireIdentityAndUpdateAfterCommitAdmission | 1 | Zulässige eigene atomare Operation |
| ActualFocusLossPreservesOwnDrainIdentityButInvalidatesInput | 2 | F03 vor/nach Drainabschluss |
| ForeignOldObserverAndFaultedDrainsCannotReleaseSuccessor | 1 | Fremde/alte Drainrechte und Ablehnungszustände |

Der Vergleich der tatsächlich ausgeführten Testnamen mit der ursprünglichen 23er-TRX ergibt null fehlende alte Fälle und exakt zwölf neue Fälle. Zusätzlich ist der alte Quelltextteil byteidentisch. Keine Assertion wurde im Review geändert.

Nicht ausgeführte Fälle werden getrennt ausgewiesen:

| Suite / Test | Einordnung |
|---|---|
| Core: NativePublicationDoesNotFollowLateSymlink | Symlink-Erstellungsprivileg fehlt |
| Core: TargetSymlinksIncludingDanglingLinksAreRejected(False) | Symlink-Erstellungsprivileg fehlt |
| Core: TargetSymlinksIncludingDanglingLinksAreRejected(True) | Symlink-Erstellungsprivileg fehlt |
| Core: MacOsInheritedExtendedAclIsRemovedBeforePublication | Darwin-Plattformfall auf Windows |
| Core: MeasureLocalHardware | Explicit, manuelle Hardwaremessung |
| MCP: InstalledCodexDiscoversAndCallsStatus | Explicit, benötigt CRYPTOWORKBENCH_CODEX_PATH |

Die Roh-TRX enthalten 7.672 beziehungsweise 90 UnitTestResult-Einträge. Die Konsolenangaben 4 beziehungsweise 0 Skips und der aggregierte notExecuted-Zähler reichen zur Zählung der Explicit-Fälle nicht aus. Keiner dieser sechs Fälle wird als bestanden ausgegeben.

Buildwarnungen: Die F1-Logs enthalten CS0618 an WorkbenchTextArea.cs:140 sowie EditorCoordinatorTests.cs:51/281 für bestehende Clipboard-/Headless-Key-APIs. CS8618/CS8602/CS8625 betreffen bestehende MainViewModel-/Sidebar-/Viewstellen. Keine Warnung zeigt auf eine neue F1-Coordinatorzeile oder einen neuen Regressionstestkörper. Die betreffenden alten Testteile und anderen Quellen sind unverändert; ein neuer F1-Regressionsgrund ist daraus nicht erkennbar. Der Nachreview hat keinen Build durchgeführt.

Reproduktion im Produkt-Worktree, mit einem neuen separaten Ergebnisverzeichnis:

```powershell
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --filter FullyQualifiedName~EditorCoordinatorTests --logger 'trx;LogFileName=coordinator-r2.trx' --results-directory <Pruefverzeichnis>
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --logger 'trx;LogFileName=gui-r2.trx' --results-directory <Pruefverzeichnis>
```

Zusätzliche Reviewartefakte im neuen R2-Prüfbereich:

- `basis-check.json`: F1-Hash-, Diff- und Originaltestvergleich.
- `original-probes.log`: Ausgabe des unveränderten C#-Blocks aus Anhang A des ursprünglichen unabhängigen Reviews.
- `extra-probe-body.cs.txt` und `run-extra-probes.ps1`: acht ergänzende Szenarien; der Runner übernimmt unverändert Initialisierung/Reflectionhelper aus jenem Anhang und ersetzt nur den isolierten Prüfkörper.
- `extra-probes.log`: acht erfolgreiche Prüfmeldungen; alle Bedingungen werden ausserhalb abgefangener Produktobserver ausgewertet.
- `test-and-warning-summary.json`: Roh-TRX-Auswertung, alte/neue Testnamen und Warnungsstellen.
- `verification.json`: abschliessender Dateivergleich.

Die zusätzlichen Proben laufen mit PowerShell 7.6/.NET 10 per Add-Type im Speicher auf den echten vorhandenen Produktassemblies und der vorhandenen Headless-Fixture. Reflection überbrückt interne Sichtbarkeit; keine Produktfelder, Guards oder Assemblies werden verändert. Die Ergebnisse sind gezielte Übergangsprüfungen, kein vollständiges State-Space-Modell und kein globaler L2-Beweis.

## 8. Headless-Session-Befund

Der historische Rohbefund aus `mcp00-ap21/results/gui-third.trx` wurde erneut gelesen: OpenNativeUpdateFaultsWithoutEndUpdateRepairLoop schlägt mit NullReferenceException in Avalonia.Headless.HeadlessUnitTestSession.Dispose fehl; der nächste Frame ist die äussere Ui-Sessiongrenze in EditorCoordinatorTests.cs:33.

Der identische alte Test und die unveränderte Fixture bestehen im aktuellen 35er- und 469er-Lauf. Auch die separaten Proben beenden ihre Headless-Sessions erfolgreich. Damit ist keine Ursache bewiesen. Der Befund bleibt **OPEN / Ursache INCONCLUSIVE**; weder als sicherer Produktfehler noch als sicher behobener Frameworkfehler einstufen.

Keine Testabschaltung, Assertionlockerung oder neue Fixturebehandlung. Die frühere F1-Encodingkorrektur ist durch byteidentische Originaltests und neue Ausführung verifiziert und erklärt den historischen Disposefehler nicht.

## 9. Noch offene AP3–AP6-Grenzen

Bestätigt für das geprüfte Delta und den aktuellen Coordinator:

- Keine MCP-Besitzphase oder neue externe Editor-Acquire-Schnittstelle; nur Unavailable/Gui/Releasing/Faulted.
- Kein neuer HTTP-Zugang und keine Lease-v2-/Credential-/D02-Rotation.
- Keine EC21-, Interpreterpipeline- oder CryptoScript-Semantikänderung.
- Kein AvaloniaEdit-Eingriff, kein Paket-/Projektupgrade.
- Registry.DrainAsync/DisposeAsync wartet weiterhin auf die bisherigen Registry-/Save-Tasks; es ist kein vollständiger Coordinator-/Provider-/Callbackdrain.
- Die bestehende interne Registry-Aktivierung ist keine neue externe Editorbesitzfreigabe.

Die vollständige Vermittlung der 51 Mutationspfade und 31 Observergruppen bleibt entsprechend dem unveränderten ursprünglichen Review offen. F1 beansprucht keine neue Vollintegration dieser Inventur. Save-/Dialog-/Passwort-/Return-/Dispatcherproducer, v2-Transport, Credentialwiderruf, Epochenwiederverwendung und Plattformzeit benötigen spätere gesonderte Nachweise. AP3–AP6 bleiben gemäss aktuellem Auftrag bis zur Frameworkentscheidung zurückgestellt.

Die genehmigte IME-Risikoakzeptanz gilt weiter; historische U2-Evidenz wird nicht umgewertet. Die Headless-Proben sind kein neuer physischer Windows-Tastatur-/CJK-/OS-Clipboarddurchlauf.

## 10. Bewertung der Avalonia-12-Migrationsbereitschaft

**Der korrigierte AP2.1-Grundkern ist als abgegrenzter Ausgangsstand für eine separat beauftragte Avalonia-12-Migrationsplanung geeignet.** Die vier bekannten lokalen Blocker sind geschlossen; der Quellstand, sein Delta und die 35 Coordinator-/469 GUI-Testfälle sind reproduzierbar dokumentiert.

Diese Eignung ist keine Aussage, dass der Code bereits mit Avalonia 12 kompatibel sei. Die Nachweise gelten für die gegenwärtige Avalonia-11-/AvaloniaEdit-Konfiguration. Ein späterer Auftrag muss die Framework-/Bibliothekskombination, native Commandregistrierungen, Fokus-/TextInput-/Clipboardverhalten, Property-/Template-Lifecycle und Headless-Testinfrastruktur auf dem konkreten Zielstand erneut bewerten. Die vorhandenen Obsolete-Warnungen sind dafür Eingaben, kein in diesem Review erledigter Migrationsschritt.

Strategische Reihenfolge: zuerst separate Frameworkentscheidung/Migrationsplanung; AP3–AP6 bleiben zurückgestellt. Keine automatische Migration oder Paketänderung ausgelöst.

## 11. Abschliessende Empfehlung

**READY FOR AP2.1 ACCEPTANCE** für den hier begrenzten, korrigierten AP2.1-Grundkern. F01, F02, F04 und F03 sind bestätigt korrigiert; es gibt keinen neuen nachgewiesenen blockierenden Befund im Reviewumfang.

Die Entscheidung über die tatsächliche Abnahme verbleibt beim Projekteigentümer. Die Bewertung aktiviert weder MCP-Editorbesitz noch HTTP und gibt AP3–AP6 nicht automatisch frei. Der historische Headless-Abschlussfehler und die späteren Integrations-/Plattformnachweise bleiben offen. Als nächster gesonderter Arbeitsgegenstand ist die Avalonia-12-Migrationsplanung geeignet; sie wird hier nicht begonnen.
### Abschliessender Erhaltungsnachweis

Der vollständige SHA-256-Abgleich der 24.561 vor Reviewbeginn erfassten Originaldateien ergibt **null Änderungen und null fehlende Dateien**. Das umfasst auch Produkt-bin/obj, bisherige F1-Protokolle/Vorherkopien, historische Berichte und die erfassten Git-Metadaten. Es gibt keine testbedingten bin/obj-Änderungen dieses Reviews.

Einzige neue Datei im Produktbaum ist dieser Bericht. HEAD, Branch-/Upstreamdaten und Staging sind unverändert. Der Git-Status unterscheidet sich ausschliesslich um:

```text
? docs/mcp/MCP-00-AP2.1-F1-independent-rereview.md
```

Damit verbleiben sechs zuvor modifizierte getrackte Dateien und jetzt 28 ungetrackte Dateien, Staging leer. Bestehende uncommittete Arbeiten und alle Originalassertions sind erhalten. Die elf geforderten Hauptabschnitte sind vollständig vorhanden.

Maschineller Nachweis: `mcp00-ap21-f1-r2/verification.json` im oben genannten separaten Prüfbereich. Dieser Erhaltungsabschnitt wurde anschliessend ausschliesslich an den neuen Bericht angefügt. Die verbotene `.codex/config.toml` war vor Inhaltszugriff ausgeschlossen und wurde weder gelesen noch verändert. Keine Git-Mutation, Produkt-/Testkorrektur oder automatische Folgearbeit.