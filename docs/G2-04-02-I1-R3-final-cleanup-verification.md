# G2-04-02-I1-R3 – Final Cleanup Ownership Verification

**PASS · R1-M03 / R2-M03 CLOSED · READY FOR OWNER REVIEW**

Am 18. November 2026 geprüft am uncommitteten Stand von `feature/gui-g2-04` im Worktree `artifacts/worktrees/gui-g2-04`, Repository `tkbss/CryptoWorkBench`. Feature-HEAD und lokaler master entsprechen der vorgegebenen Baseline `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Gegenstand ist die Cleanup-Garantie des akzeptierten F3-Vertrags, insbesondere A22. R1, F1, R2 und F2 wurden mit den tatsächlichen Quellpfaden, den ursprünglichen R2-Fehlerergebnissen und den aktuellen Läufen abgeglichen.

Die beiden zuvor reproduzierbaren Dateireste lassen sich unter denselben Windows-Sperrbedingungen nach Entsperren und Retry vollständig entfernen. Auch wenn der Host erfolgreich zurückkehrt und erst die Session den Teil verwirft, bleibt der konkrete Payload für Cleanup erreichbar. Im geprüften Umfang gibt es kein neues Finding. Ich empfehle I1 zur Eigentümerabnahme.

## Eigentümerwechsel und Freigabe

| Übergang | Feststellung am Produktcode |
|---|---|
| Privates Staging, Seal, Zurückweisung | `InspectorCapture.cs:23–30,104–108`. Failure-Callback und äußerer Catch teilen die lokale Referenz. Nur der erste Aufruf von Abandon erhält über Interlocked.Exchange den Payload zur Freigabe. Seal allein veröffentlicht nichts. Scheitert die finale UI-Prüfung, bleibt der ursprüngliche Read-Fehler vom Cleanup-Fehler unterscheidbar. |
| Erfolgreicher Host-Read | Erst nach abgeschlossener Host-Finalisierung gibt Capture den versiegelten SnapshotPart zurück. Bis zur Übernahme hält ihn die lokale Variable in `InspectorSession.cs:131–151`. Auswahl und Revision werden erneut geprüft. Erst nach erfolgreichem Add wird die lokale Referenz geleert. Bei überholtem Request oder Add-Ausnahme ruft finally RetireStaging auf, solange das Transfer-Ticket noch gilt. |
| Veröffentlichter Teil, Close und Reset | `InspectorSnapshot.cs:39–59,98–110`, `InspectorSession.cs:22–35,42–57,164–177`. Der Snapshot besitzt seine aufgenommenen Teile. Close und Reset ziehen ihn zurück. Aktive Copies verschieben die endgültige Freigabe. Scheitert Dispose, hält die Session den Snapshot über ihre Cleanup-Liste und den registrierten Retry erreichbar. |
| Fehlende Löschfreigabe | `InspectorPayload.cs:134–152` entfernt jeden erfolgreich gelöschten Pfad einzeln. Verweigerte Pfade bleiben im Payload. Für verworfene Teile hält `WorkbenchInspectorRead.cs:16–36` den **Payload selbst** und die letzte Fehlermeldung im Dictionary der Dienstinstanz. Der nächste erfolgreiche Read leert diesen Bestand nicht. Session.RetryCleanup erreicht sowohl diese Einträge als auch die zurückbehaltenen veröffentlichten Snapshots. |
| Transfer und Shutdown | `WorkbenchInspectorRead.cs:37–68`, `WorkbenchAutomationRegistry.cs:755–778`. Transferbeginn und Stop sind durch denselben Registry-Lock geordnet. Das letzte Ticket schließt die Completion ab. Doppeltes Ticket-Dispose kann dank Interlocked.Exchange nicht nochmals dekrementieren. Shutdown wartet auf Host-/Editorarbeit und ausstehende Transfers. Anschließend versucht er im finally die verbliebenen Staging-Freigaben einmal. Drain umfasst diesen letzten Versuch. |

**Host und Locks.** Der Host beendet den Inspector-Job über seinen vorhandenen Finalisierungspfad (`WorkbenchAutomationRegistry.cs:562 ff.`). Datei-Retry reserviert keinen Interpreter und erwirbt weder ExecutionIntent noch Admission. Der erste Dispose-Versuch verlässt den Payload-Lock, bevor sein Fehler im Cleanup-Dictionary gespeichert wird. Der Retry unter dem Dictionary-Lock ruft Payload.Dispose auf, aber keinen Host-/Session-Callback. Die Transfer-Completion führt Continuations asynchron aus. In diesen Pfaden ergab sich weder eine zyklische Lock-Abhängigkeit noch eine offene Completion nach Abschluss. Die Laufzeitprüfungen belegen freien Host, erfolgreiche Folge-Admission und Pending null. Sie sind kein allgemeiner Beweis für alle Thread-Interleavings.

**Reader und atomare Übergabe.** `InspectorSnapshot.cs:70–96` erwirbt alle Reader vor dem ersten asynchronen Write, prüft vollständige Coverage und ruft Commit erst nach erfolgreichem Staging auf. Bei Rückzug hält die aktive Copy den Snapshot bis zum finally. Dort werden alle Reader freigegeben und die ausstehende Snapshot-Freigabe ausgeführt. Die R2-Mehrteilproben prüfen Shutdown während angehaltener Copy, beschädigte oder fehlende spätere Segmente, Commit-Fehler und eine gesperrte Datei. Das Read-Ticket deckt analog die Session-Übernahme bis nach Add bzw. RetireStaging ab. Die Shutdown-Probe mit angehaltener Copy und die Add-Fehlerprobe mit anschließendem Shutdown prüfen unterschiedliche Zeitpunkte.

## Unabhängige Reproduktionen

Die vorhandenen unveränderten R2-Proben, die permanenten Inspector-Tests und die Range-Tests liefen erneut mit dotnet vstest gegen die aktuellen Debug- und Release-Assemblies. Die Produkt-DLL im Probe-Output stimmte je Konfiguration per SHA-256 mit der entsprechenden GUI-Produkt-DLL überein. Die sechs neuen TRX wurden anhand ihrer Einzelresultate ausgewertet.

| Probe | Beobachteter Nachweis in beiden Konfigurationen |
|---|---|
| RealSessionCloseBeforePublish | Ursprünglich Failed. Eine reale Session schließt nach Seal bei extern gehaltener Datei. Nach Entsperren, explizitem Retry und Drain ist ihr isoliertes Verzeichnis leer. Jetzt Passed. |
| RejectedStagingWithLockedFile | Ursprünglich Failed. Die finale UI-Prüfung lehnt das gesperrte Staging ab. Folge-Read und Cleanup funktionieren, das Testverzeichnis wird leer. Jetzt Passed. |
| LockedReturnedOrUnpublishedPartRetainsOwner(true) | Permanenter Close-Fall. Ein weiterer Retry unter Sperre lässt den Fehler sichtbar und den Host frei. Nach Entsperren gelingt der explizite Retry. Nach Drain sind Fehler und eigene Dateien verschwunden, Pending ist null. |
| RejectedFinalValidationKeepsCleanupAcrossNextRead | Permanenter Validierungsfall. Auf die Cancellation folgt nach Entsperren ein erfolgreicher neuer Read **vor** dem Retry. Der frühere Cleanup-Fehler bleibt bis zum Retry sichtbar. Verzeichnis leer und Pending null nach Drain. |
| LockedReturnedOrUnpublishedPartRetainsOwner(false) | Permanenter Add-Fehler. Der zweite Read liefert den Teil erfolgreich zurück, Add lehnt die Überlappung ab. Retry unter Sperre erhält den Cleanup-Fehler. Nach Entsperren übernimmt der abschließende Shutdown-Retry. Verzeichnis leer und Pending null. |

Die drei permanenten Fälle stehen in `CryptoWorkBenchGuiTest/InspectorReadTests.cs:386,439`. PublishedCleanupAcrossFreshCapture prüft wiederholte verweigerte Löschungen. FullCoverageSurvivesResetAndShutdownDrainsCopy und LaterSegmentDuringShutdown prüfen, dass ein ausstehender Transfer den Drain offen hält und anschließend abschließt. CancellationAndShutdownDrainAcceptedRead prüft nach Drain explizit IsBusy gleich false und Pending gleich null.

## Testmatrix und Originalnachweise

Alle 16 Einträge der F2-Matrix wurden über den SHA-256 des jeweiligen Original-TRX, die einzelnen Outcomes und die CLI-Skipzahlen abgeglichen. Die folgende Tabelle gibt Passed pro Konfiguration an. In allen aufgeführten Ergebnisläufen gab es null Failed.

| Suite | F2 Debug | F2 Release | R3 Debug | R3 Release | Ausgelassene Fälle je Konfiguration |
|---|---:|---:|---:|---:|---|
| Core gesamt | 11.736 | 11.736 | — | — | 26 CLI-Skips und 1 weiterer NotExecuted |
| GUI gesamt | 1.184 | 1.184 | — | — | 0 |
| MCP gesamt | 176 | 176 | — | — | 13 NotExecuted |
| Inspector | 38 | 38 | 38 | 38 | 0 |
| Share-Range | 13 | 13 | 13 | 13 | 0 |
| Unveränderte R2-Proben | 13 | 13 | 13 | 13 | 0 |
| Desktop-Stop separat | 1 | 1 | — | — | 0 |
| Desktop-Adapter separat | 1 | 1 | — | — | 0 |

Die sechs R3-Läufe ergeben 128 bestandene Ausführungen. Fokustests und Gesamtsuiten überschneiden sich. Striche kennzeichnen die ausschließlich am F2-Original ausgewerteten Läufe. Für eine Wiederholung der Gesamtsuiten ergab sich bei dieser Nachprüfung kein Anlass.

Der separate F2-Debug-GUI-Lauf `gui-Debug-reviewed.trx` enthält 1.183 Passed und einen Fehler in ShortPrintIsVisibleAndSourceButtonNavigates(911,512) bei Dispatcher.VerifyAccess / DefaultRenderLoop.Add während der Avalonia-Headless-Initialisierung. Der vollständige Wiederholungslauf bestand. Die Ursache des intermittierenden Fehlers bleibt offen.

**Prüfartefakte im Worktree**

- [Frühere R2-Proben und ursprüngliche Fehlerergebnisse](../artifacts/test-results/g2-04-i1-r2/)
- [F2-Matrix mit Original-TRX-Hashes](../artifacts/test-results/g2-04-i1-f2/matrix.json)
- [R3-Einzelresultate und CLI-Logs](../artifacts/test-results/g2-04-i1-r3/) für r2, inspector und range, jeweils Debug und Release.

## Übrige R1-Findings und Grenzen

Im von F2 berührten Umfang bleiben acht Findings **CLOSED**. Für H01/H02 bestätigen die erneut ausgeführten Segment- und Mehrteilproben die Integritätsprüfung, die Lebensdauer aller Reader und den Commitpunkt. Für M01/M02 bestehen die Kontext-, Assignment- und Revisionstests mit der erweiterten Session-Übergabe. Für M04 besteht die Cancellation-Probe am finalen UI-Entscheid. Für M05 besteht der fokussierte Test gegen Volltextallokation bei Admission. Für M06 bestehen die Coverage-Permutationen und die Ablehnung tatsächlicher Überlappungen. Für L01 bleibt die getrennte Dokumentation fehlgeschlagener und wiederholter Läufe erhalten.

Bei dauerhafter OS-Löschverweigerung können Dateien und Cleanup-Einträge über Drain hinaus bestehen bleiben. Pro explizitem Aufruf wird jeder offene Eintrag einmal versucht. Weiterhin verweigerte Freigaben bleiben samt Fehler über den lebenden Owner erreichbar. Viele verschiedene verweigerte Löschungen können diesen Bestand vergrößern. Eine Garantie über Prozessende hinaus gibt es nicht. Die ausgeführten Dateisperrproben belegen Windows-Verhalten. Native Clipboard-Übergabe einschließlich Fehler nach externer Übergabe, Dateifreigaben auf anderen Plattformen sowie p95-Latenz und Peak-Speicher großer Transfers bleiben gesonderte Abnahmen.

**Empfehlung:** I1 kann dem Eigentümer zur Abnahme vorgelegt werden. R3 hat ausschließlich Reviewdokumentation und lokale Ausführungsnachweise erzeugt.
