# G2-04-02-I1 — Inspector Core & Host Integration

Stand: 17.11.2026. **READY FOR OWNER REVIEW**. Übergabe zur unabhängigen Codeprüfung und Eigentümerprüfung; nicht automatisch ACCEPTED.

## Ausgangsbasis und Umfang

Repository `tkbss/CryptoWorkBench`, Branch `feature/gui-g2-04`, bestehender Worktree `artifacts/worktrees/gui-g2-04`. Feature-HEAD und Master-HEAD sind weiterhin `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Die Änderungen sind uncommitted. Master ist unverändert. Kein neuer Branch/Worktree, Commit, Push oder Merge.

Grundlage sind die akzeptierten, durch F3 korrigierten F2-Verträge und der ursprüngliche I1-Auftrag. Die vorhandene `SecretShare.CopyValueRange`-Implementierung samt 13 Regressionstests wurde erhalten. I1 implementiert den gemeinsamen Host-, Snapshot- und Payload-Unterbau. Property-Inventar, vollständige fachliche Projektion, JSON-/Clipboard-Encoder sowie Dock-Integration bleiben gemäß Auftrag I2/I3; diese Arbeitspakete wurden nicht begonnen. Keine Maskierung oder Autorisierung.

## Implementierung und Host-Lebenszyklus

`WorkbenchInspectorRead.RunInspectorReadAsync` ist ein interner Zweck des vorhandenen Dienstes. Die UI prüft Observer-Sperre, Editorregistrierung, `CanStartGui`, aktive Arbeit, Shutdown, MCP-Epoch-Close und Auswahlgültigkeit. Unter dem vorhandenen Service-Lock entstehen `ExecutionIntent` und Host-Reservation; der bestehende `Accept`-Pfad nimmt atomar an. Fehler bei Registrierung oder Worker-Annahme rollen Job, Subscription, Reservation und Intent zurück. Die Regression injiziert beide vorhandenen Admission-Checkpoints und lässt anschließend einen erfolgreichen Read folgen.

`RunOwnedAsync` benutzt weiterhin `InterpreterHostQueue.RunAdmittedAsync`. Es gibt keine zweite Queue, globale Snapshot-Registry oder neue Workspace-/Revisionsautorität. Nur der interne Capture-Code erhält einen CancellationToken; er erhält keine ausführbare Pipeline. Live-Zugriffe erfolgen ausschließlich synchron in `InterpreterExecution.AccessState`; IO und Await liegen außerhalb des State-Scopes, aber innerhalb derselben Host-Reservation. `Intent.Current` und Auswahlvalidierung laufen vor der Ergebnisrückgabe erneut auf dem UI-Dispatcher.

Für `InspectorRead` sind `EnsureRecovered`, Variablen-Reprojektion, Execution-Timer, `RecordUnit`, `CaptureMutation`, `AdvanceVersion`, normale Retention und Tombstones ausgeschlossen. `ClearAdmittedState` weist einen Inspector-Job vor der Mutation ab. Öffentliche Statusabfragen zeigen Busy, aber keine interne Execution-ID; Lookup liefert keinen Inspector-ExecutionResult. LastExecutionId, Variablenpublikation und bestehende öffentliche Execution-Quoten bleiben erhalten. Der Read erzeugt keine Print-/History-Publikation. Die internen Jobdaten dienen ausschließlich dem vorhandenen Cancellation-/Drain-Lebenszyklus.

Bei Erfolg, Ausnahme, Cancellation oder fehlgeschlagenem Workerstart läuft die bestehende Finalisierung: Cancellation-Registrierungen und Callbacks drainen; Workspace-Subscription und Intent auf UI freigeben; fehlgeschlagenes Payload-Staging bereinigen; Job ohne Execution-Retention entfernen; erst danach Reservation und Completion freigeben. Cleanup-Fehler verhindern eine erfolgreiche Rückgabe. Shutdown und MCP-Disable/Drain erfassen auch Reads ohne `InspectorSession`. Ein Cancel-Flag allein gibt den Host nicht frei.

## Identität, Observer und Historie

`CaptureStamp` enthält Original-Identifier, Weak-Origin, veröffentlichte Row-Identität, Publikation, Workspace-Generation, bestehende StateRevision und vorhandene Dokumentversion. Der ursprüngliche Identifier stammt aus dem privaten Publication-Draft, nicht aus dem editierbaren Row-Feld. Jeder Capture prüft Dictionary-Objektidentität und StateRevision; jeder weitere Arbeitsschritt prüft erneut. Assignment unter demselben Namen und In-place-Mutation können alte Teile nicht ergänzen.

Eine wichtige Implementierungspräzisierung: Normale Editoränderungen erhöhen nicht zwingend `WorkspaceGeneration`. Deshalb wird zusätzlich die bereits vorhandene `TextDocument.Version` gebunden und `EditorBridge` meldet Text-/Dokumentwechsel. Dafür wurde kein eigener globaler Zähler eingeführt. Eine noch dargestellte alte Variables-Zeile wird nach Textänderung nicht erneut als aktuelle Quelle zugelassen.

`InspectorSession` ist die minimale UI-Zustandskomponente. Im SelectedVariable-Observer werden nur Request-Absicht und Cancellation erfasst. Ein zusammengefasster Dispatcher-Aufruf wartet auf eigene vorherige Arbeit und startet genau einen zulässigen Aufnahmeversuch; Busy führt nicht zu Retry-Schleifen. Die initiale Auswahlaufnahme prüft Identität und Textlänge mit einer Range der Länge null. Sie lädt nicht ungefragt einen großen Wert.

Normale Textänderungen, Workspace-Invalidierung und MCP-Übergänge machen den Handle ungültig, erhalten aber detached Snapshot-Teile. Historische vollständige Werte sind ohne Live-Admission lesbar, auch während eines MCP-Lease. Tatsächliches `ClearAdmittedState`, Dokumentkontextwechsel, Auswahlaufhebung/-wechsel, Close/Dispose und Shutdown lösen Freigabe aus. Request-IDs verhindern späte Publikation. Layoutoperationen sind hier nicht angebunden und erzeugen keine Capture-Arbeit.

## Daten-, Range- und Ressourcenvertrag

`ReadSpec` adressiert in I1 bekannte Root-Text-/Value-Werte und einzelne SecretShare-Bytebereiche. I2 erweitert diese typisierte Adressierung um geprüfte Property-Pfade im selben Aufnahmeweg. Unbekannte Runtime-Untertypen werden vor virtuellen Getteraufrufen zurückgewiesen. TR31H-Value-Formatierung, KCV und vollständige komplexe Projektion werden nicht vorweggenommen.

`SnapshotPart` enthält einen versiegelten Payload und unveränderliche Bereichsdaten. Veröffentlichung ersetzt die `ImmutableArray` atomar in der UI-eigenen Session. Stamp-Konflikte, falsche Längen, widersprüchliche Totals und überlappende Ranges werden abgewiesen. Vollständigkeit verlangt lückenlose Coverage von null bis Total. Null ist separat von Empty markiert. Fehlgeschlagenes Staging wird niemals als vollständiger Teil veröffentlicht.

Strings werden als unveränderte UTF-16LE-Codeeinheiten gespeichert, einschließlich isolierter Surrogate, NUL, Leerzeichen und Newlines. Bytewerte bleiben Bytewerte; `SecretShare.CopyValueRange` kopiert ohne Gesamtclone und validiert Bounds vor dem ersten Schreibzugriff. `InspectorScalar` bewahrt unterstützte immutable CLR-Skalare einschließlich `ulong.MaxValue` und `long.MinValue` ohne Double-Konvertierung. Dies sind interne Datenformate, keine zusätzlichen Copy-Hüllen; die fachliche Ausgabe folgt in I2.

Technische Budgets: 64-KiB-Arbeitsbuffer, höchstens 32.768 UTF-16-Codeeinheiten bzw. 65.536 Share-Bytes je State-Schritt, maximal 256 KiB pro Payload-Append, physische Tempsegmente maximal 1 MiB. Der Payload unterstützt einen begrenzten RAM-Cache; der Capture-Standard nutzt konservativ sofortiges Spill (Cachebudget null), damit viele Teile nicht jeweils ein eigenes 32-MiB-Budget vervielfachen. Dies liegt unter dem freigegebenen RAM-Maximum und ist kein Größenlimit. Vor Schreiben werden standardmäßig 512 MiB freier Diskreserve verlangt. Keine fest eingebaute maximale Root-Größe; fehlender Speicher/IO wird als Fehler gemeldet, nicht als Teilwert-Erfolg.

Cancellation wird an jedem Arbeitsschritt und bei asynchronem IO berücksichtigt. Nach fünf Sekunden können gedrosselte Fortschrittsmeldungen erfolgen. Seal/Veröffentlichung erfolgt erst nach vollständigem Range-Write. Detached Copy-to-Stream hält einen lokalen Payload-Lease: Owner-Dispose verhindert neue Leser, laufende Leser dürfen drainen, erst danach werden Dateien gelöscht. Dateien tragen `cwb-inspector-<GUID>.payload`; nur eigens erzeugte Pfade werden gelöscht.

Die eigene Review-Korrektur stellt sicher, dass ein Löschfehler nicht die Bereinigung weiterer Segmente oder Snapshot-Teile verhindert. Fehler werden aggregiert; direkt gehaltene Payloads können die Freigabe nach Behebung wiederholen. Session-Dispose entfernt seine Subscriptions auch bei gemeldetem Payload-Löschfehler. Es gibt keinen unbegrenzten Cleanup-Retry oder globalen Sweeper.

## Permanente Tests und Nachweise

13 bestehende Core-Regressionen bleiben erhalten. 20 neue Inspector-Regressionen prüfen Host-Exklusivität, Reservation/Intent nach Fehlern, Recovery-Ausnahme, Revision und Identity, Observer/Dispatcher, späte Ergebnisse, MCP Acquire/Disable/Drain, Shutdown, historische Reads, Null/Empty, numerische Grenzen, Spill, IO-Fehler, Cancellation und Datei-/Reader-Lebensdauer.

Großdaten-Nachweise:

- 255 tatsächliche Shares à 131.072 Byte, zusammen 33.423.360 Byte (31,875 MiB), werden über den Host aufgenommen und während eines tatsächlichen MCP-Lease vollständig aus detached Payloads gelesen.
- Ein String mit über 9 Millionen UTF-16-Codeeinheiten erzeugt mehr als 18 MiB Payload; Codeeinheiten/Bytes, NUL, Unicode und CRLF werden exakt verglichen.
- Separater Spill-Test über 16 MiB prüft alle Bytes, Segmentobergrenze und vollständige Dateifreigabe.
- Range-Merge verifiziert lückenlose Coverage; doppelte/überlappende Bereiche und Revision-/Total-Konflikte werden abgewiesen. Der alte Payload bleibt nach Mutation exakt lesbar.
- Ein blockierender Copy-Zielstream beweist, dass Owner-Dispose eine laufende lokale Payload-Lease nicht zerstört.
- Ein Windows-Dateisperrtest beweist, dass ein nicht löschbares Segment nicht das Löschen anderer Segmente verhindert; nach Entsperren gelingt die erneute Freigabe.

### Tatsächliche Testmatrix

Die abschließende Matrix ist aus TRX-Einzelresultaten und CLI-Summaries erzeugt und liegt unter `artifacts/test-results/g2-04-i1/test-matrix.json`. Die zugehörigen `*-handoff.log`/`*.trx` bleiben im selben Verzeichnis. Nicht ausgeführte Fälle werden nicht als bestanden gezählt.

| Suite | Konfiguration | Passed | Failed | Skipped (CLI) | Zusätzlich NotExecuted | TRX-NotExecuted insgesamt |
|---|---|---:|---:|---:|---:|---:|
| Core | Debug | 11.736 | 0 | 26 | 1 | 27 |
| Core | Release | 11.736 | 0 | 26 | 1 | 27 |
| GUI | Debug | 1.166 | 0 | 0 | 0 | 0 |
| GUI | Release | 1.166 | 0 | 0 | 0 | 0 |
| MCP | Debug | 176 | 0 | 0 | 13 | 13 |
| MCP | Release | 176 | 0 | 0 | 13 | 13 |

Die TRX-Einzelresultate enthalten sowohl CLI-Skips als auch explizit ausgeschlossene Fälle unter `NotExecuted`; diese Spalten dürfen nicht doppelt addiert werden. Core: 26 Plattform-/Symlink-Skips (Darwin ABI bzw. fehlendes Windows-Symlinkprivileg), zusätzlich `MeasureLocalHardware` als expliziter manueller Hardwaretest. MCP: 13 explizite Installed-Codex-/gepaarte Desktop-/Identitäts- und Zertifikatsszenarien, die eine konfigurierte lokale Codex-Executable bzw. eine vorbereitete Desktop-Sitzung verlangen. Alle Namen und Gründe stehen in der JSON-Matrix. GUI: sämtliche 20 neuen Inspector-Tests bestehen in beiden Konfigurationen; Core enthält die erhaltenen 13 Range-Tests.

Desktop-Debug und Desktop-Release wurden aus dem endgültigen Code erfolgreich gebaut (je 0 Errors, 6 Warnings). Danach wurden `RealDesktopStopWithCaReadLockStillWaitsForOriginalDrainAndCloses` und `AdapterUsesStandardFramesFixedErrorsAndExitsOnInputEof` nochmals gegen diese Ausgaben geprüft: je 1 Passed, 0 Failed in beiden Konfigurationen (`*-confirmed.trx`). Das sind Wiederholungen bereits enthaltener Tests, keine zusätzlichen einzigartigen Testfälle.

Reproduktion im Feature-Worktree: `. ./eng/prism/Enter-Prism.ps1`, anschließend `dotnet build CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj` für Debug/Release und `dotnet test` für `CryptoScriptUnitTest`, `CryptoWorkBenchGuiTest` und `CryptoWorkBench.Mcp.Tests` jeweils mit `-c Debug` bzw. `-c Release` ausführen. TRX-Logger und separates Ergebnisverzeichnis verwenden. Verwendet: Windows 11 Home 10.0.26200 x64, Intel Core i7-14700F, 15,79 GiB physischer RAM, .NET SDK 10.0.401, offizieller gepinnter Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` (1.180 verifizierte Dateien), keine Dependency-Upgrades. Das ist die Testumgebung, kein Performance-Messprotokoll.

Die ersten GUI-/MCP-Gesamtläufe meldeten je einen Fehler wegen fehlender Desktop-Build-Ausgaben. Nach Debug-/Release-Desktopbuild wurden diese Tests erfolgreich ausgeführt; die ursprünglichen Logs bleiben erhalten. Ein weiterer GUI-Debug-Lauf (`-verified`) meldete zwei Fehler bei der Avalonia-Headless-Appinitialisierung (`DefaultRenderLoop.Add`, falscher Dispatcherthread), bevor der jeweilige Testkörper lief. Das wird als beobachtete Testinfrastruktur-Instabilität dokumentiert, nicht als bestandener Lauf oder nachgewiesen vorbestehender Fehler ausgegeben. Der abschließende vollständige Debug-/Release-Lauf besteht ohne diese Fehler. Ein erneuter Desktop-Build-Aufruf verwendete zunächst versehentlich den nicht existierenden Projektpfad `CryptoWorkBenchDesktop`; diese beiden MSB1009-Logs (`Desktop-*-handoff.log`) bleiben ebenfalls erhalten. Der korrigierte tatsächliche Desktopbuild und beide abhängigen Native-Tests bestehen (`*-confirmed`). Kein bestehender Test wurde abgeschwächt.

## Abgleich A01–A38 und Integrationspunkte

| Kriterien | I1-Nachweis und Grenze |
|---|---|
| A01–A04 | Vollständiges Inventar/Accessor-Matrix gehören I2. I1 weist unbekannte Runtime-Getter vor Auswertung ab. |
| A05–A08 | Host-Read ohne Execution-Publikation/Revision/Recovery nachgewiesen; I2 muss sämtliche Properties, PARAM-Adapter, TR31H-Formatter und KCV getrennt abnehmen. |
| A09, A12–A13 | 19 JSON-Goldens, Zyklen und komplexer Encoder nicht als bestanden behauptet; I2. |
| A10–A11 | Verlustfreie String-/Byte-/Null-/Scalar-Grundlage geprüft; UI-Differenzierung und endgültige Copy-Formatierung I2. |
| A14–A15 | Historische Stream-Werte und lokale Payload-Leases geprüft; native Clipboard-Übergabe und Plattformgrenzen I2. |
| A16–A18 | Vollständiger 255-Share-Datenweg, großer String und Range-API geprüft. Vollständiger Parent-JSON-Copy, tiefe Graphen/Nodeinventar und Renderfenster I2; A16 nicht pauschal als vollständige Copy-Abnahme behauptet. |
| A19 | Spill, Diskreserve-/IO-Fehler, Cancellation, vollständige Payload-Publikation und Freigabe geprüft. |
| A20–A21 | Bounded IO-/State-Schritte implementiert. Peak-RAM, native Clipboard-Handoff und p95-UI-/Cancel-Messungen nicht abgenommen. |
| A22 | Eigene Pfade, Close/Dispose, Reset/Shutdown und aktive Reader-Leases geprüft. Crashreste dokumentiert; kein Prozesskill-Abnahmetest durchgeführt. |
| A23–A29 | Permanente Host-/Observer-/Revision-/Drain-Regressionen; vorhandene gesamte GUI-/MCP-Suiten zusätzlich ausgeführt. Kein Anspruch, jede denkbare Scheduler-Interleaving erschöpfend bewiesen zu haben. |
| A30–A31 | Historie bei normalen Änderungen/MCP, echte Reset-/Auswahl-/Dispose-Grenzen implementiert und getestet. |
| A32–A38 | Docking, Layoutmigration und native GUI-Abnahme unverändert außerhalb I1. |

I2 konstruiert eine `InspectorSession` zur vorhandenen `VariableViewModel`, ohne einen zweiten View-Attach oder Host zu erzeugen. `StateChanged`, `Snapshot`, `Historical`, `Error`, `Progress`, `ReadAsync`, `CancelRead` und `Dispose` bilden die Integration. Vollständige Werte nur bei erfolgreicher Coverage zum Encoder weiterreichen. Session/Parts bleiben UI-owned; Payload-Copy darf mit eigenem lokalen Reader-Lease laufen. Der zukünftige Encoder nutzt denselben Payloadweg; kein erneutes Lesen historischer Werte aus dem Interpreter. Visuelles Detach bei Docking darf nicht als Dispose behandelt werden.

## Dateiliste

Produktcode geändert:

- `CryptoScript/SecretSharing/SecretSet.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs`
- `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs`

Produktcode neu:

- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchInspectorRead.cs`
- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs`
- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorPayload.cs`
- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorScalar.cs`
- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorSession.cs`
- `CryptoWorkBenchAvalonia/Services/Inspector/InspectorSnapshot.cs`

Permanente Tests neu (einschließlich erhaltenem Vorarbeitsstand): `CryptoScriptUnitTest/SecretShareRangeTests.cs`, `CryptoWorkBenchGuiTest/InspectorReadTests.cs`. Dokumentation neu: dieser Bericht. Ignorierte lokale Nachweise: Logs, TRX, Testmatrix und Review-Manifest unter `artifacts/test-results/g2-04-i1`.

## Verbleibende Risiken und unabhängiges Review

1. Native Clipboard-, Render- und Performance-Abnahme stehen aus; kein I1-Test ersetzt diese I2-/I3-Nachweise. Insbesondere löscht Session-Freigabe Dateien synchron; sehr viele Segmente oder langsames Storage können UI-Latenz erzeugen. Vor nativer Abnahme messen und gegebenenfalls asynchrones, sessionlokal gedraintes Cleanup ergänzen.
2. Nach Prozessabbruch oder dauerhaft verweigertem Delete können erkennbare eigene Tempdateien verbleiben. Es gibt absichtlich keinen globalen Sweeper und kein Versprechen erfolgreicher Löschung trotz OS-Fehler. Kein funktionales Rootlimit, aber natürliche Disk-/Adressraumgrenzen bleiben.
3. I2 muss ausschließlich geprüfte Runtime-Accessorpfade ergänzen, Cancellation/AccessState-Grenzen erhalten und unbekannte Getter nicht auswerten. Die interne Read-Delegate ist ein Trusted-Code-Vertrag, keine Sandbox gegen beliebigen neuen Assembly-Code.
4. Die Tests laufen auf Windows. Linux/macOS sowie explizite Installed-Codex-/gepaarte Desktop-Szenarien sind nicht ausgeführt. Die beobachtete Headless-Initialisierungsinstabilität bleibt im Review sichtbar.
5. Streaming-/Cachebudgets sind funktional geprüft; eine Referenzhardware-Messreihe für Peak-RAM, Disk und Tail-Latenzen ist offen. Sofortiges Spill minimiert RAM, kann aber mehr IO verursachen.

Internes Code-Review durchgeführt: Admission-Rollback, Host-Finalisierung, unerwünschte Recovery-/Execution-Nebenwirkungen, Threadgrenzen, Auswahlidentität, historische Daten, vollständige Coverage und Cleanup wurden gegen den Code kontrolliert. Dabei wurden Dokumentversion, bedarfsgerechte initiale Null-Range und Cleanup bei Dateisperren nachgeschärft. Dies ist kein unabhängiges Review.

Für die unabhängige Prüfung liegen uncommitted Diff, neue Dateien, permanente Tests, sämtliche erfolgreichen und fehlgeschlagenen Prüfläufe sowie ein SHA-256-Manifest bereit. Priorität: `Accept`/`RunOwnedAsync`/`ReleaseResourcesAsync` inklusive MCP-Drain und Acceptance-Rollback; danach Session-Invalidierung und Payload-Leases. Der Reviewer soll keine neue Execution-Publikation, keinen Revisionsfortschritt und keine Gatefreigabe vor Drain feststellen. Fehlgeschlagene Native-/Performance-Nachweise dürfen nicht durch die Artefakt- oder Headless-Prüfungen ersetzt werden.

Ergänzung F1-L01: CryptoWorkBenchGuiTest-Release.trx enthält neben RealDesktopStopWithCaReadLockStillWaitsForOriginalDrainAndCloses (fehlende Desktop-DLL) auch ShortPrintIsVisibleAndSourceButtonNavigates(640,420) (Thread-Affinitätsfehler). Ein gemeinsamer Auslöser mit den beiden Debug-Headless-Fehlern ist nicht nachgewiesen.
