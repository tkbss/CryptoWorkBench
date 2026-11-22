# G2-04-02-I1-F2 · Cleanup Ownership

18. November 2026. Branch `feature/gui-g2-04`, bestehender Worktree `artifacts/worktrees/gui-g2-04`, Ausgangscommit `32017a08c4771e73c12a50f6e1548476b9bb0a54`.

## Ursache und Korrektur von R1-M03 / R2-M03

R2 konnte eine Datei nach Seal, aber vor Veröffentlichung sperren. Der Read versuchte beim Abbruch, den Payload freizugeben. Auch der anschließende Catch versuchte Dispose noch während derselben Sperre. Danach besaß weder der Job noch ein veröffentlichter Snapshot den Payload. Der vorhandene Session-Retry erreichte nur fehlgeschlagene Snapshot-Freigaben. Der gleiche Verlust war beim Verwerfen eines zurückgegebenen SnapshotPart möglich.

| Zeitpunkt | Owner und überprüfbarer Übergang |
|---|---|
| Aufbau bis zur Host-Finalisierung | Der einzelne Read besitzt sein privates Staging. Der Failure-Callback und der äußere Catch teilen eine einmalige Übergabe über Interlocked.Exchange. Der erste Fehlerpfad überträgt das Staging zur Freigabe. Der zweite findet keinen eigenen Payload mehr. Der ursprüngliche Read-Fehler bleibt für den Aufrufer erhalten. |
| Erfolgreiche Rückgabe | Nach abgeschlossener Host-Finalisierung gehört der versiegelte Payload dem SnapshotPart. Bis zum erfolgreichen Add besitzt die Session diesen lokalen Teil. |
| Erfolgreiches Add | Der veröffentlichte Snapshot übernimmt den Teil. Seine bisherigen Reader-Leases und seine Freigabe bei Close gelten weiter. |
| Stale Request, Cancellation oder fehlgeschlagenes Add | Ein noch lokal gehaltener Teil geht im finally an denselben Freigabepfad wie verworfenes Staging. |
| Betriebssystem verweigert Delete | Der vorhandene Automation-Service behält pro betroffener Instanz den konkreten Payload samt letzter Fehlermeldung. Die Sammlung enthält ausschließlich fehlgeschlagene Freigaben. Sie verwendet einen eigenen Lock und benötigt keine Interpreter-Reservation. Auch nach Close kann der Cleanup-Owner über den Dienst oder Session.RetryCleanup erreicht werden. |
| Entsperren und Retry | Ein Aufruf versucht jeden ausstehenden Payload einmal und entfernt erfolgreiche Einträge. Eine erneute OS-Verweigerung aktualisiert die Fehlermeldung. Ein normaler erfolgreicher Read verändert diese Meldung nicht. |
| Shutdown | Der vorhandene Transferzähler deckt neben der detached Copy auch die UI-Übernahme bzw. Verwerfung eines zurückgegebenen Teils ab. Shutdown schließt neue Transfers und wartet auf Host- und Transferabschluss. Bei ausstehenden Staging-Freigaben erfolgt anschließend ein letzter begrenzter Versuch außerhalb des Host-Locks. Ein weiterhin gesperrter Pfad bleibt als Cleanup-Fehler abfragbar. |

Der Host gibt Reservation und ExecutionIntent im bisherigen Finalisierungspfad frei. Die Copy-Commit-Grenze, Digestprüfung, Coverage und die Lebensdauer veröffentlichter Payload-Reader verwenden den bisherigen F1-Code.

## Änderungen gegenüber dem R2-Prüfstand

- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs` regelt die einmalige Aufgabe des privaten Staging-Besitzes und die Übergabe eines erfolgreich versiegelten Teils.
- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorSession.cs` integriert den Staging-Retry und den sichtbaren Cleanup-Fehler. Ein lokales Transfer-Ticket gilt bis zum Add oder zur Rückgabe des verworfenen Teils an den Cleanup-Owner.
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchInspectorRead.cs` enthält die instanzgebundene Aufbewahrung fehlgeschlagener Staging-Freigaben, ihren Retry und den für Read-Übergabe und Copy gemeinsam verwendeten Transferzähler.
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` bindet den abschließenden Retry nach Quieszenz in das bestehende Shutdown-Task ein.
- `CryptoWorkBenchGuiTest/InspectorReadTests.cs` ergänzt drei permanente Windows-Fälle in zwei Testmethoden. Dieser Bericht dokumentiert die Übergabe an R3.

## Permanente Regressionen und unabhängige Proben

**A · Close vor Veröffentlichung.** `LockedReturnedOrUnpublishedPartRetainsOwner(true)` hält den UI-Thread an, bis die neue Datei ohne aktiven Writer geöffnet werden kann. Die Session wird bei weiter gehaltener Dateisperre geschlossen. Ein Retry unter Sperre lässt den Fehler sichtbar. Nach Entsperren räumt der definierte Retry auf. Nach Shutdown ist das Verzeichnis leer und Pending null.

**B · Finale Auswahlprüfung verweigert.** `RejectedFinalValidationKeepsCleanupAcrossNextRead` sperrt den versiegelten Payload im finalen Auswahlprädikat. Der Read endet mit Cancellation und freiem Host. Nach Entsperren wird ein neuer Wert erfolgreich in der Session aufgenommen. Bis zum Retry bleibt der ursprüngliche Cleanup-Fehler sichtbar. Retry und Shutdown hinterlassen ein leeres Verzeichnis.

**C · Zurückgegebener Teil wird verworfen.** `LockedReturnedOrUnpublishedPartRetainsOwner(false)` führt einen zweiten Read für einen bereits vollständig erfassten Bereich aus. Die endgültige UI-Prüfung und die Worker-Rückgabe gelingen. Erst Add lehnt die Überlappung ab. Der Test prüft diesen konkreten Fehler und den separaten Cleanup-Fehler. Nach Entsperren übernimmt hier der Shutdown-Drain den letzten Retry. Er kehrt mit leerem Verzeichnis und Pending null zurück.

Das unabhängige R2-Projekt wird als eigene Testsuite ausgeführt. Seine beiden früheren Fehler `RealSessionCloseBeforePublish` und `RejectedStagingWithLockedFile` verlangen weiterhin die ursprünglichen strengen Assertions. Die R2-Originale mit den tatsächlichen Fehlschlägen liegen weiterhin unter `artifacts/test-results/g2-04-i1-r2`. Alle F2-Logs und neuen TRX liegen separat unter `artifacts/test-results/g2-04-i1-f2`.

## Architekturabgleich und R3-Handoff

R1-H01 und H02 bleiben im F2-Umfang geschlossen. Der R2-Prüfsatz kontrolliert weiterhin beschädigte und fehlende Segmente, einen späteren Fehler nach privatem Teil-Staging, den ausbleibenden Commit und die Freigabe aller Teile bei aktiver Copy und Shutdown. R1-M01, M02, M04, M05 und M06 bleiben durch die wiederholten Dokumentkontext-, Revisions-, Cancellation-, Admission- und Coverage-Regressionen gedeckt. Die Ergänzung zur Testhistorie für R1-L01 bleibt erhalten. Die F1-Produktdateien außerhalb der vier oben benannten geänderten Produktdateien wurden mit dem Review-Manifest verglichen.

Für R3 sind drei Übergänge nachzustellen: Abbruch nach Seal vor Veröffentlichung, Abschluss eines erfolgreichen Host-Reads mit anschließendem Add-Fehler und Entsperren vor dem letzten Shutdown-Retry. Das Prüfprojekt unter artifacts/test-results/g2-04-i1-r2/probes und die neuen permanenten Fälle prüfen diese Übergänge mit echten Dateihandles. Ein dauerhaftes OS-Delete-Verbot kann weiterhin einen Tempdateirest hinterlassen. Solange die Dienstinstanz lebt, bleiben der betroffene Payload und sein letzter Cleanup-Fehler für einen expliziten erneuten Versuch erreichbar. Es gibt keine Wiederholungsschleife.

Native Clipboard-Übergabe, p95-/Peak-Messungen und die plattformspezifische Dateifreigabe außerhalb von Windows bleiben separate Abnahmen. Für F2 wurden Windows-Dateisperren und der bestehende Desktop-Stop-/Adapter-Pfad geprüft.

## Testergebnisse und Status

**R1-M03 / R2-M03: im F2-Korrekturumfang geschlossen. READY FOR OWNER REVIEW.** Die unabhängige R3-Nachprüfung und die Eigentümerentscheidung stehen aus.

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | Skipped je Konfiguration | weitere NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|---:|
| Core gesamt | 11.736 | 11.736 | 0 | 26 | 1 |
| GUI gesamt | 1.184 | 1.184 | 0 | 0 | 0 |
| MCP gesamt | 176 | 176 | 0 | 0 | 13 |
| Inspector fokussiert | 38 | 38 | 0 | 0 | 0 |
| Share-Range fokussiert | 13 | 13 | 0 | 0 | 0 |
| Unverändertes R2-Prüfprojekt | 13 | 13 | 0 | 0 | 0 |
| Desktop-Stop separat | 1 | 1 | 0 | 0 | 0 |
| Desktop-Adapter separat | 1 | 1 | 0 | 0 | 0 |

Gegenüber F1 kommen drei permanente GUI-Fälle hinzu. Im R2-Prüfprojekt wechselten RealSessionCloseBeforePublish und RejectedStagingWithLockedFile in beiden Konfigurationen von Failed auf Passed. Die 26 CLI-Core-Skips und der zusätzliche manuelle Hardwarefall ergeben zusammen 27 NotExecuted-Einzelresultate im Core-TRX. Die 13 expliziten MCP-Szenarien wurden je Konfiguration nicht ausgeführt. Desktop-Debug und -Release bauten mit je sechs Warnungen und null Fehlern. Fokussierte und native Wiederholungen zählen nicht zusätzlich zu den Gesamtsuiten.

Ein Debug-GUI-Zwischenlauf auf dem endgültigen Stand meldete 1.183 Passed und einen Fehler in ShortPrintIsVisibleAndSourceButtonNavigates(911,512) während der Avalonia-Headless-Initialisierung (Dispatcher.VerifyAccess / DefaultRenderLoop.Add). Der vollständige Wiederholungslauf mit derselben Debug-Assembly bestand mit 1.184 Passed. Ursache und Zusammenhang mit früheren intermittierenden Fehlern sind nicht geklärt. Beide Ergebnisse sind separat als gui-Debug-reviewed.trx und gui-Debug-repeat.trx aufbewahrt. Die maschinenlesbare Matrix mit TRX-Hashes und einzelnen NotExecuted-Gründen liegt unter artifacts/test-results/g2-04-i1-f2/matrix.json.
