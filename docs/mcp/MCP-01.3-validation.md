# MCP-01.3 – Snapshots, Ergebnisregister und Cancellation

Stand: 23.10.2026. Implementierung und lokale Validierung abgeschlossen; bereit für den separat beauftragten unabhängigen Review. Dieser Bericht ist keine unabhängige Reviewfreigabe.

## 1. Basis und Umfang

- Zielworktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
- Branch: `feature/mcp-server`; HEAD unverändert `0a9eb3536ede1e24a070c7375b2852afb90d8ef4`.
- Der Zielworktree war vor Beginn sauber. Der Chat-Arbeitsordner unter `Documents/ChatGPT` ist ein anderes Repository und wurde nur für temporäre Bearbeitungsdateien benutzt.
- Grundlage: MCP-00-R1-Architektur und Implementierungsplan sowie MCP-01.1-/MCP-01.2-Validierung und MCP-01.2-Review. Die R1-/R2-Korrekturen aus MCP-01.2 waren bereits in HEAD enthalten und bleiben erhalten.
- Keine bestehenden Tests geändert, deaktiviert oder abgeschwächt. Keine Änderung an CryptoScript, Abstractions-Verträgen, MCP-Tools, Paketpins, Solutions oder Sprachsemantik.
- Kein Commit, Push, Merge, Rebase oder Reset. `.codex/config.toml` wurde weder gelesen noch verändert.

`origin/master` wurde nur lesend berücksichtigt. Am Ende lagen dort zusätzlich `4b78532`, `6943439`, `ca77a94` und `7974813` (Dateiverschlüsselung, macOS-ABI/Publish, Headless-Test-Races und Dateientschlüsselung). Kein Commit wurde übernommen. Der Tracking-Abstand änderte sich während der Arbeit von drei auf vier fremde Commits; der eigene HEAD blieb unverändert.

## 2. Komponenten und tatsächliche Aufrufketten

| Komponente | Änderung |
| --- | --- |
| `WorkbenchAutomationService.cs` | Bestehende GUI-Einstiege in das gemeinsame Auftragsmodell integriert; Mutation, Refresh und Clear unterscheiden ihre Zustandsgrenzen. |
| `WorkbenchAutomationRegistry.cs` | Implementiert `IWorkbenchAutomationService`, Auftragseigentum, atomare Annahme, Idempotenz, Rechteprüfung, Snapshots, Deadlines, Retention und Lifecycle-Hooks. |
| `AutomationProjection.cs` | Neue immutable, begrenzte Metadaten-/Diagnoseprojektion mit sicheren festen Meldungen und erlaubten Metadatenfeldern. |
| `WorkbenchExecutionPipeline.cs` | Bestehender Parse-/Runner-Kern bleibt erhalten; strukturierter Script-Outcome, Veröffentlichung abgeschlossener Einheiten, Passwortwartephase und beobachteter Benutzerabbruch ergänzt. |
| `InterpreterHostQueue.cs` | Bestehende fail-fast Admission wird vor Registrierung reserviert und vom Service bis zum Ende aller Abschlussarbeiten gehalten. Angenommene Arbeit wird ohne Request-Token und ohne übernommene Caller-AsyncLocals gestartet. |
| GUI / DI | Dieselbe `Shared`-Instanz wird konkret und als `IWorkbenchAutomationService` registriert. GUI-Ausführung verwendet den serviceeigenen Token; lokale Eingaben liefern einen gehashten Request-Schlüssel. `SetupVariables` ist ein Refresh ohne Versionsanstieg. |
| `WorkbenchRegistryTests.cs` | 35 zusätzliche Host-Vertrags-/Integrationstests im GUI-Testprojekt; keine Hostabhängigkeit im transportneutralen MCP-Testprojekt eingeführt. |

Produktive Aufrufketten:

```text
GUI-Line / ScriptBook / KeyMap
  -> bestehender WorkbenchAutomationService.RunAsync / ExecuteSubmissionAsync
  -> gemeinsame Admission + Registereintrag
  -> serviceeigener Worker / ein Interpreter-Host-Scope
  -> WorkbenchExecutionPipeline.ExecuteAsync je Parse-Einheit
  -> frischer Runner, bestehende Print-/Info-Collector und Passwort-Drain
  -> sichere Zwischen-Diagnosen + lokale GUI-Präsentation
  -> finale Snapshot-/Register-/Ressourcenveröffentlichung
  -> Admission freigeben, Completion-Signal setzen

IWorkbenchAutomationService.ExecuteTextAsync (nur vertrauenswürdiger GUI-Caller)
  -> Argumente / Instanz / Rechte / Fingerprint / Idempotenz / State-Version prüfen
  -> dieselbe Admission, derselbe Worker und dieselbe Pipeline
  -> callerabhängiger Response-Wait auf ein serviceeigenes Completion-Signal

GUI-Variablenmutation / Clear
  -> gemeinsame Admission / Core-Zustandszugriff
  -> Mutation + Versionsgrenze + Capture
  -> GUI-Präsentation unter weiter gehaltener Admission
```

Ein ScriptBook behält genau einen serviceweiten Auftrag und Host-Scope; Diagnose-ExecutionIds gehören weiterhin den einzelnen Runner-Ausführungen. Syntaxpositionen berücksichtigen physische Buchzeilen. Es gibt weder einen zweiten Variablenbestand noch eine wartende Auftragsqueue oder parallele Interpreterausführungen.

## 3. Auftragseigentum und Annahme

Jeder angenommene Auftrag erhält eine serviceweite UUID, die vom Runner unabhängig ist. Die Serviceinstanz besitzt eine neue `instanceId`. Der Registereintrag enthält Principal, Quelle, Aktivierungsepoche, SHA-256-Fingerprint, UTC-Annahme-/Deadline-Zeiten, monotone Zeitmarken, sichere Ergebnissnapshots, CancellationSource und ein Ergebnis-freies Completion-Signal. Bei GUI-Einstiegen erzeugt der Service die ID; die öffentliche Textschnittstelle nimmt die vom vertrauenswürdigen Aufrufer vorgegebene ID an.

`ExecuteTextAsync` normalisiert ausgelassene Timeout-/Output-Optionen vor dem Fingerprint. Gleiche ID und gleiche Argumente liefern den vorhandenen Zwischen- oder Endstand, ohne neuen Lauf, neue Deadline oder erweiterte Ausgaberechte. Abweichende Argumente ergeben `InvalidArgument`, abgelaufene eigene IDs `ResultExpired`, fremde IDs `NotFound`. Vor Annahme abgewiesene Busy-/State-/Argumentfehler erzeugen keinen Eintrag.

Die Arbeit läuft nach Annahme unter Serviceeigentum. Der Registry-Task ist ein nicht generisches Completion-Signal und hält kein `Task<ExecutionPresentation>` mit lokalen GUI-Werten fest. Der eigentliche Worker wird einschließlich Fehlern beobachtet. Timer, Worker und lokale Cancellation-Registrierungen übernehmen keine Ambient-Request-Kontexte. Es werden keine HTTP-Requests, kurzlebigen DI-Scopes oder Transportobjekte gespeichert. Während der Ausführung notwendige lokale Callbacks und Scriptdaten bleiben bis zu ihrem sicheren Ende im Worker; sie sind kein Bestandteil des Ergebnisarchivs.

## 4. Snapshots, Redaktion und Zustandsversion

Alle veröffentlichten Sammlungen sind `ImmutableArray` mit unveränderlichen Vertragsrecords. Die Projektion kopiert ausdrücklich erlaubte Felder; weder Dictionary-Enumeration, veränderliche Variablendeklarationen, SemanticErrors, ASTs, Parserobjekte noch GUI-Collections werden zurückgegeben.

- Variablen: Identifier, bekannter Typ, Key-Flag, bekannte Algorithmus-/Materialmetadaten und gegebenenfalls Schlüssellänge; `valueIncluded=false`. Keine VAR-/PATH-/Parameterwerte oder Schlüsselbytes. Freie Mechanismusfelder werden konservativ nicht exportiert.
- Diagnosen: bekannte Codes, Severity, sichere strukturierte Positionen/Runnerkorrelation und feste katalogartige Meldungen. Keine freien Message-/Value-/FunctionCall-/Exception-Texte; freie Funktions-/Mechanismusfelder werden nicht exportiert.
- Ausgaben: vorhandener Print-/Info-Collector, ursprüngliche Reihenfolge, fortlaufende Sequenz und `redacted=true`; Text bleibt null. `includeText` wird bis zur lokalen Bestätigungsintegration vollständig mit `Forbidden` abgewiesen, auch wenn ein Caller `output.read` besitzt.
- Externe Grenzen: jeweils höchstens 1000 Variablen, Diagnosen und Ausgaben. Identifier werden zusätzlich auf 256 UTF-16-Codeeinheiten begrenzt und die Truncation wird sichtbar markiert. Lokale GUI-Inhalte und Print bleiben vollständig. Ohne Textfreigabe liegt der exportierte Ausgabetext bei null Bytes und damit unter 64 KiB.

`stateVersion` bezeichnet die abgeschlossene Host-Zustandsgrenze, nicht die Anzahl geänderter Variablen. Ein ausgeführter Auftrag erhöht sie einmal, auch bei Laufzeitfehler, Teilwirkung oder Abbruch. Ein Buch mit mehreren Runnern bleibt eine Laufgrenze. Syntaxfehler ohne vorherigen Runner erhöhen sie nicht. GUI-Mutation und Clear erhöhen die Version ebenfalls; ein reiner Refresh nicht. Auch eine werfende Mutation publiziert ihren möglichen Teilzustand beziehungsweise einen Capturefehler.

Während einer laufenden Ausführung liefern `GetVariablesAsync` und die Variablenkomponente von Zwischen-Ergebnissen den letzten abgeschlossenen Snapshot mit `stale=true`. Noch nicht abgeschlossene Buchzustände werden ausschließlich privat als ausstehender Capture geführt. Es gibt keine parallele Live-Enumeration durch Leseaufrufe.

Bei Capturefehler bleiben Script-Outcome, bereits kopierte Diagnosen und Ausgaben erhalten. Der externe Hostabschluss ist `failed` mit sicherem `InternalError` und `CaptureFailed`-Diagnose; der bisherige Variablensnapshot bleibt stale und der Lifecycle wird Faulted. Die lokale GUI behält ihre bestehende getrennte Präsentation von Script- und Capture-Ergebnis.

Recovery führt ausschließlich eine Zustandsaufnahme aus. Vor einer weiteren Mutation/Laufausführung wird eine verlässliche Metadatenbasis unter dem gemeinsamen Gate hergestellt; dabei werden keine Value-/Passwort-/Parameterpayloads gelesen und keine Scriptoperationen wiederholt. Ein manueller GUI-Refresh kann ebenfalls wiederherstellen. Scheitert schon die Metadatenaufnahme, bleibt Faulted und die eigentliche Mutation unterbleibt. Eine erneute GUI-Wertaufnahme kann unabhängig davon erneut fehlschlagen und den Zustand wieder stale markieren.

## 5. Cancellation und Deadlines

Vor der atomaren Annahme verhindert ein abgebrochener Request den Auftrag. Danach wird sein Token ausschließlich für `WaitAsync` auf die Antwort verwendet. Der Auftrag, sein Passwortdialog, seine Admission und sein archiviertes Ergebnis überleben den verlorenen Response-Wait.

`CancelExecutionAsync` prüft Caller, Eigentum und Request-Cancellation und registriert dann einen separaten serviceeigenen Abbruch. Der erste registrierte Grund bleibt bestehen: `explicit`, `timeout`, `user`, `shutdown` oder `mcpDisabled`. Lokale GUI-/Lifetime-Tokens sind ausdrücklich lokale Benutzerabbruchsignale und werden nicht als Transport-Request-Tokens behandelt. Passwort-User-Cancellation wird bereits beim Beobachten des Runner-Outcomes registriert.

Deadline: 30 Sekunden ab Annahme, explizit 1000–120000 ms. Die Deadline benutzt `TimeProvider`-Timer und monotone Timestamps; UTC dient nur der Darstellung. Tests verwenden eine kontrollierbare TimeProvider-Implementierung. Retry und Ergebnisabfrage verlängern nichts.

Cancellation ist kooperativ. Während blockierender synchroner Operationen bleibt der Auftrag nichtterminal und die Workbench Busy. Passwortantworten werden wie bisher erst nach Runner-Ende und tatsächlichem Callback-Ende entsorgt. Admission bleibt auch über Callback-Cancellation, Passwort-Drain, GUI-Präsentation, Snapshot-/Registerfinalisierung und Ressourcendisposal belegt. Ein nie abgeschlossener Callback hält damit weiterhin sichtbar Busy. Ein regulär beendetes letztes Statement bleibt `completed`, selbst wenn kurz vor der Finalisierung noch ein Abbruchgrund registriert wurde; der Grund wird nicht mit dem Script-Outcome gleichgesetzt.

## 6. Retention und Kapazität

- Höchstens 64 abgeschlossene Ergebnisse, höchstens 15 Minuten ab terminalem Abschluss; Ablauf erfolgt durch Timer und zusätzlich bei Registry-Zugriff.
- Höchstens 8 MiB gespeicherte Transportprojektionen. Für aktives Ergebnis, ausstehenden Capture und veröffentlichten Variablensnapshot werden konservativ je 2 MiB reserviert; ältere abgeschlossene Ergebnisse werden bei Erreichen des verbleibenden Budgets verdrängt. Einzelprojektionen sind zusätzlich defensiv begrenzt.
- Höchstens 10 000 bekannte angenommene IDs pro Service-/Prozesslebensdauer. Nach Ablauf bleiben nur UUID und unveränderlicher Caller als Tombstone, keine Ergebnisse, Worker, Timer oder CancellationSources.
- Neue öffentliche Aufträge erhalten bei ausgeschöpftem ID-Budget `LimitExceeded`. Lokale bestehende GUI-Einstiege bleiben fail-fast; sie bieten keine vom Client wiederverwendbare Ausführungs-ID an.
- Aktive Aufträge werden nicht durch Retention verdrängt. Eine monotone Abschlusssequenz macht die Reihenfolge auch bei identischen Uhrzeitstempeln eindeutig.

Der Bytebudget-Test reproduzierte während der Implementierung eine falsche Verdrängungsreihenfolge bei identischen Zeitstempeln und wiederverwendeten Dictionary-Slots. Der Abschlusszähler behebt dies; der Regressionstest bleibt enthalten.

## 7. Rechte und Lifecycle-Grenzen

Alle Lese-/Abbruchoperationen prüfen das jeweilige Recht aus dem vertrauenswürdigen Host-Caller. Ergebniseigentum bindet Principal, Caller-Quelle und Aktivierungsepoche. Fremde Ausführungen werden nicht offengelegt. Rechte gelten nicht aufgrund clientseitiger Toolargumente. Ein Caller mit widerrufenem Recht beziehungsweise alter Epoche erhält keinen Zugriff.

Vorbereitete Hooks: `StopAccepting`, `BeginShutdown`, `DisableMcp`, `DrainAsync` und der interne Host-Hook `ActivateMcp`. Disable widerruft die Epoche sofort, fordert für MCP-Aufträge `mcpDisabled` an und verwirft deren Ergebnisse nach notwendiger Finalisierung. GUI-Aufträge bleiben bei MCP-Disable bestehen. Shutdown stoppt Annahmen und registriert `shutdown`, ohne einen bereits registrierten Grund zu überschreiben. Ein abgebrochener Drain-Wait beendet nicht die eigentliche Arbeit.

MCP-Ausführungen sind weiterhin nicht produktiv zugelassen: `ExecuteTextAsync` ist nur für den vertrauenswürdigen GUI-Kontext nutzbar. Editor-Lesen, -Ändern und -Ausführen liefern abhängig vom Recht `EditorUnavailable` oder `Forbidden`. Keine Fake-Editoridentität, kein unbestätigter Execute-Bypass, kein HTTP-Start, keine zusätzliche Toolregistrierung und keine Auth-/Pairingimplementierung.

Die vollständige Editor-/Revisionsbrücke und Save-Commit-Sperre bleiben MCP-01.4, lokale Bestätigung und Authentisierung MCP-01.5, Desktop-Closing und abschließendes Service-/HTTP-Disposal MCP-01.6. Die vorliegenden Lifecycle-Hooks ersetzen keine native Closing-Integration.

## 8. Automatisierte Nachweise

Die 35 neuen Fälle in `WorkbenchRegistryTests` ergänzen sämtliche unveränderten MCP-01.1-/MCP-01.2-Tests. Abdeckung der geforderten Themen:

| Themen | Nachweis |
| --- | --- |
| Tiefe Unveränderlichkeit und Geheimnisse | Detached GUI-/Core-Objekte, mutable Diagnosequellen, Marker in VAR, PATH, KEY, PARAM, Syntax, Exception und Passwortantwort. |
| Version / Clear / Teilfehler / Syntax | Versionsgrenzen einschließlich fehlgeschlagener Mutation, Refresh ohne Versionsanstieg und Clear über den Shared-Service. |
| Stale während Ausführung | Verlorener Response-Wait und kontrollierte Pause zwischen zwei Buchzeilen; auch ExecutionResult gibt keine halbfertigen Variablen als frisch aus. |
| Capturefehler / Recovery | Erfolg, Laufzeitfehler und Syntaxfehler mit erhaltener lokaler Präsentation; Recovery ohne Replay; fehlerhafte Metadatenaufnahme blockiert Mutationen. |
| Service-ID / mehrere Runner | Ein Buchauftrag, mehrere Diagnose-ExecutionIds, physische Syntaxzeilennummern. |
| Idempotenz / Konflikte / keine doppelten Prints | Identische gleichzeitig laufende Anforderungen, abweichende Argumente und terminaler Retry. |
| Malformed / Instance / Busy / State | Leere IDs, ungültige Enums/Versionen/Timeouts, fremde Instanz und abgewiesene Aufträge ohne Registereintrag. |
| Retention / Budget / Tombstones | 65 echte Abschlüsse, kontrollierter Zeitablauf, Bytebudget-Verdrängung, kein Replay. Die 10-000-ID-Grenze wird gezielt mit vorgefüllten minimalen Test-Tombstones geprüft. |
| Request-Cancellation vor/nach Annahme | Kein Auftrag vor Annahme; nach Annahme nur beendeter Antwort-Wait, weiterlaufende Effekte und spätere Ergebnisabfrage. |
| Expliziter Abbruch / erster Grund | Autorisierter Abbruch, bereits abgebrochener Cancel-Request, weitere Deadline-/Shutdown-Signale überschreiben den ersten Grund nicht. |
| Deadline / synchrone Operation / letzter Abschluss | Deterministische Uhr, kontrolliert blockierte Print-Operation, CancellationRequested ohne FinishedAt und regulär beendetes letztes Statement. |
| Passwortwartephase / Drain | Expliziter Abbruch und Deadline, tatsächlich verzögerte Antwort, Busy bis Drain, anschließendes Secret-Dispose. |
| Rechte / fremde Aufträge / Epoche | Principal- und Quellenbindung, fehlende Rechte, Disable/Reaktivierung und verworfene MCP-Ergebnisse. Der zukünftige MCP-Auftrag wird ausschließlich im Test über private Registry-Felder simuliert, nicht durch einen produktiven Execute-Bypass. |
| Output-/Diagnoselimits | Mehr als 1000 lokale Einträge, begrenzte externe Arrays, Truncation und unveränderte lokale Ausgaben. |
| Finalisierung / Shutdown | Kontrollierte Pause nach Scriptabschluss vor Präsentationsende; Admission und Drain bleiben belegt. |
| Task-/Request-Lebensdauer | Registry hält kein typisiertes GUI-Ergebnis-Task; Worker übernimmt keine Request-AsyncLocals; Host-Exceptions werden beobachtet und sicher projiziert. |

## 9. Validierungsergebnisse

Windows x64, .NET SDK **10.0.401**, Release:

| Prüfung | Endergebnis |
| --- | --- |
| Restore `CryptoWorkBench.sln` | erfolgreich |
| Restore `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | erfolgreich |
| Release-Build beider Solutions | erfolgreich, 0 Fehler; im GUI-Neubau die 8 bereits dokumentierten Nullable-Warnungen |
| Vollständige Core-Suite | **7 667 bestanden, 4 übersprungen**, 0 fehlgeschlagen |
| Vollständige GUI-Suite | **308 bestanden**, 0 übersprungen/fehlgeschlagen; 273 bestehende + 35 neue Fälle |
| Vollständige reguläre MCP-Suite | **89 bestanden**, 0 fehlgeschlagen |
| Gezielte finale Registry-/PinTranslation-Nachprüfung | 61 bestanden |
| Projektabhängigkeitskontrolle | einzige neue Produktionsreferenz: Avalonia → Automation.Abstractions; Core und MCP-Grenzen unverändert |
| `git diff --check` | erfolgreich |

Core-Skips getrennt: drei Symlink-Fälle mangels Windows-Symlinkprivileg (`NativePublicationDoesNotFollowLateSymlink`, beide `TargetSymlinksIncludingDanglingLinksAreRejected`-Varianten) und `MacOsInheritedExtendedAclIsRemovedBeforePublication` als Darwin-Fall. Der explizite Hardwarebenchmark `MeasureLocalHardware` und der explizite Installed-Codex-Test sind keine regulären Suiteläufe und wurden nicht aktiviert. Keine neuen Skips eingeführt.

Zwei Zwischenläufe enthielten je eine `NullReferenceException` in `Avalonia.Headless.HeadlessUnitTestSession.Dispose`: einmal beim unveränderten Passwort-GUI-Fall `ProductiveSecureLoadDialogUpdatesVariablesAndStopsBookOnFailure("wrong")`, einmal bei `RenderedMatrixLinkAndBackNavigateThroughActualView(1,1)`. Gezielte Nachprüfungen (13 Passwortfälle beziehungsweise 61 Registry-/PinTranslation-Fälle) und anschließende vollständige Läufe bestanden. Der vorhandene master-Commit `ca77a94` behandelt Headless-Lifecycle-Races durch einen separaten Backport; er wurde nur gelesen und ausdrücklich nicht übernommen. Der Zwischenfehler ist deshalb hier dokumentiert und nicht durch Teständerungen oder eine unaufgeforderte Integration verborgen. Kein Nachweis nativer Desktop-Dialoge oder anderer Betriebssysteme wird daraus abgeleitet.

Lokale, ignorierte Nachweise:

- `artifacts/mcp013/restore-main.log`, `restore-gui.log`, `build-main-final.log`, `build-gui-final.log`.
- `CryptoScriptUnitTest/TestResults/mcp013-core.trx`.
- `CryptoWorkBenchGuiTest/TestResults/mcp013-gui.trx`, `mcp013-final-targeted.trx`, `mcp013-password-recheck.trx`.
- `CryptoWorkBench.Mcp.Tests/TestResults/mcp013-mcp.trx`.
- `artifacts/mcp013/gui-headless-dispose.trx` bewahrt den zweiten fehlgeschlagenen Zwischenlauf.

## 10. Offene Integrationspunkte für MCP-01.4 und danach

- Echte Editoridentität, Revisionen und gebundene Editorinhalte fehlen bewusst. Die zukünftige Bridge muss die vorhandene gemeinsame Admission und dieselbe Serviceinstanz verwenden.
- Rechte-/Epochenkontext muss aus der späteren authentisierten Hostbindung stammen. Der interne Aktivierungshook startet keinen Server und authentisiert keinen Client.
- Lokale MCP-Bestätigungen einschließlich bewusster Print-Offenlegung fehlen; `includeText` bleibt bis dahin gesperrt.
- Save-Commit-/Closing-Integration und native Plattform-Smokes bleiben Folgeumfang. Ein nie beendeter Callback hält weiterhin Busy, auch nach Deadline.
- Die bekannte Multicast-Grenze bleibt: Ein vor dem Collector werfender Print-/Info-Abonnent kann dessen Zustellung verhindern. Keine allgemeine Änderung des Eventdispatchers.
- master-Änderungen, insbesondere Headless-Backport und neue Dateioperationen, benötigen eine gesondert autorisierte Integration und erneute Validierung.

Nach MCP-01.3 wird gestoppt. Der unabhängige Review ist ein separater Auftrag.

## 11. Korrekturstatus R1–R4 nach unabhängigem Review (23.10.2026)

**READY FOR RE-REVIEW**, keine unabhängige Freigabe. Die ursprünglichen Implementierungsnachweise oben und die vier Findings mit CHANGES REQUIRED im Reviewbericht bleiben als Historie bestehen. Dieser Abschnitt beschreibt die anschließenden Korrekturen auf `feature/mcp-server`, weiterhin HEAD `0a9eb3536ede1e24a070c7375b2852afb90d8ef4`.

### Tatsächliche Änderungen

| Finding | Korrektur / Codebezug |
| --- | --- |
| R1 | `WorkbenchAutomationRegistry.cs:185–214`: statische Timercallbacks mit explizitem State aus Service, executionId und einer internen RegistrationId. Kein Job-, Worker-, Request- oder GUI-Delegate im Timerstate. Separater `StartRetention`-Helfer verhindert die gemeinsame Compilerclosure. `DisposeAsync` ab Zeile 585 stoppt Annahmen, dränt aktive Arbeit und verwirft Retentiontimer/-resultate. Dies ist Servicebereinigung, keine vorgezogene Desktop-Closing-Integration. |
| R2 | `WorkbenchAutomationRegistry.cs:218–263`: gemeinsame `Accept<T>`-Transaktion für GUI und öffentliche Textausführung. Vor Übergabe an `RunOwnedAsync` werden bei Fehlern Eintrag, active, Timer, Registration, CTS und Reservation bereinigt; Completion wird signalisiert, keine Tombstone-ID angelegt. Nach Übergabe bleibt die Bereinigung ausschließlich beim Worker. Eine nicht wartende Abmeldung vermeidet Warten auf einen Callback unter `sync`; ein bereits laufender Callback sieht nach Entfernung keinen passenden Eintrag. Die zusätzliche RegistrationId verhindert, dass verspätete Callbacks eines fehlgeschlagenen Versuchs eine erneut verwendete executionId treffen. Die Fehlernaht `AdmissionCheckpoint` ist internal, instanzlokal und kein Transport-/Produktionskonfigurationszugang. |
| R3 | `WorkbenchAutomationRegistry.cs:364–365`, `:406–438`: Timer-/Registration-Disposal und Execution-Cancellation-Callbacks werden vor `FinalizeJob` abgewartet. `CancellationClosed` schließt unter `sync` die letzte Cancel-/Drain-Lücke; ein konkurrierender Cancel wird entweder noch vollständig drainiert oder liegt bereits hinter dieser Abschlussgrenze. Cleanupfehler werden beobachtet und als Failed/InternalError ohne Rohtext publiziert. `active`-Freigabe, Retentionwartung, Reservationfreigabe und Completion liegen im abschließenden Lock/Finally, ohne verlorenes Signal. |
| R4 | `CryptoScriptRunner.cs:15–17`: rein lesbarer `ExecutionId`-Marker aus dem bereits vorhandenen aktiven/letzten Diagnosekontext. Keine Änderung von Evaluierung, AST-Mapping, Cancellation-Prüfung oder Sprachsemantik. `WorkbenchExecutionPipeline.cs:43–46`, `:73`, `:144`: bereits abgebrochene Arbeit liefert vor Parsing eine Cancelled-Präsentation ohne Effekte; nach Runnererzeugung beweist nur eine neue tatsächliche ExecutionId den Eintritt. Leere, tatsächlich gestartete Runner bleiben eine Ausführungsgrenze; nach begonnenem Runner bleiben Possible und Teilwirkungsregeln erhalten. `BeforeRunnerStart` ist ausschließlich eine interne Testnaht. |

Die vier betroffenen Quelldateien dieses Korrekturschritts sind Registry, Pipeline, `CryptoScriptRunner.cs` und die neue `CryptoWorkBenchGuiTest/WorkbenchRegistryCorrectionTests.cs`. Zusätzlich wurden die beiden MCP-01.3-Dokumente ergänzt. Die übrigen schon vorhandenen MCP-01.3-Änderungen und alle bisherigen Tests wurden nicht verändert. Abstractions, JSON-Verträge und MCP-01.1-Transport bleiben unverändert. Der kleine Core-Marker ist eine zusätzliche C#-Leseeigenschaft, keine neue CryptoScript-Funktion und keine zweite Ausführungspipeline.

### Neue Regressionen und Gegenproben

**30 neue Fälle** in 16 Methoden der `WorkbenchRegistryCorrectionTests`:

- R1: 16-MiB-GUI-Payload und echter TextExecution-Request werden bei noch abrufbarem Ergebnis gesammelt; nicht-inlinende Hilfen begrenzen lokale Referenzen. Zusätzlich wird der reale Callbacktarget auf fehlende Instanzfelder und der State auf genau Service plus zwei GUIDs geprüft. Jobfreigabe bei Ablauf, Tombstone/Replay und wiederholtes Service-Disposal sind abgedeckt.
- R2: sechs Timer-/Registrierungs-/Workergrenzfehler mit und ohne gleichzeitig abgebrochenen Request; zwei GUI-Registrierungs-/Workerfehler; Request-Cancellation und expliziter Execution-Cancel an der Übergabe; verspäteter Callback nach Rollback und Wiederverwendung derselben ID. Jeweils freie Admission, kein Phantomauftrag, abschließbarer Drain und funktionierender Folgeauftrag. Die Registrierungsnaht wirft nach Anlage der echten Registration, sodass deren Bereinigung tatsächlich geprüft wird. Die Workerfehlernaht simuliert einen Fehler unmittelbar vor Ownership-Übergabe; sie behauptet keinen realen Threadpool-Ausfall.
- Zwei gleiche IDs starten über eine gemeinsame Barrier wirklich konkurrierend; Print erfolgt genau einmal. Ergebnisabfragen konkurrieren mit Completion und spätere Retries führen nicht erneut aus.
- R3: vier Varianten mit blockiertem Cancellation-Callback (Explicit/Timeout/Shutdown sowie werfender Callback), wiederholtem Cancel, abgebrochenem Drain-Wait und einer Stunde Fake-Zeit. Solange blockiert: nichtterminal, FinishedAt null, IsFinal false, Busy und kein abgeschlossener Drain. TTL startet erst nach Freigabe. Zwei weitere Fälle halten die Ressourcenbereinigung offen, lösen dabei Deadline aus und prüfen auch einen werfenden Cleanup. Ein separater Fall kombiniert verlorenen Execute-Antwortwait, Shutdown, Drain und Ergebnis-Recovery.
- R4: vier Kombinationen aus Explicit/Deadline und Pause vor Pipeline-/nach Runnererzeugung; alle ohne Variable, Effekte und Versionsanstieg. Zwei ScriptBook-Fälle brechen vor der ersten bzw. nach der ersten ausgeführten Zeile ab. Die bisherigen vier Tests zur nichtkooperativen synchronen Operation bleiben der Kontrollnachweis für bereits begonnene Ausführung und späte Completion.
- Integration: Retentionablauf parallel zu GetExecution liefert nur sicheren terminalen Snapshot oder ResultExpired; Capturefehler nach tatsächlicher Teilwirkung bleibt bis nach Ressourcenabschluss nichtterminal und publiziert anschließend Failed, stale und genau eine Versionsgrenze.

Die ursprüngliche isolierte Speicherprobe aus dem Review wurde ohne Quelländerung erneut gegen die korrigierten Release-Assemblies ausgeführt:

```text
AFTER_COMPLETE payloadAlive=False; busy=False; activeTimers=1
Retention.Callback.Target: WorkbenchAutomationService+<>c
AFTER_EXPIRY payloadAlive=False
PUBLIC_TEXT_AFTER_COMPLETE requestAlive=False; busy=False
PUBLIC_TEXT_AFTER_EXPIRY requestAlive=False
```

Der Compiler-Target `<>c` ist eine zustandslose Singletoninstanz des statischen Lambdas; der neue Regressionstest prüft ausdrücklich, dass sie keine Instanzfelder besitzt. Die reale Timerstate-Struktur wird separat geprüft. Damit beruht der Nachweis nicht allein auf zufälligem GC-Verhalten.

### Vollständige Validierung dieses Korrekturschritts

| Prüfung | Ergebnis |
| --- | --- |
| Restore beider Solutions | erfolgreich |
| Finale Release-Builds beider Solutions | erfolgreich, 0 Fehler; finale inkrementelle Builds 0 Warnungen |
| Vollständige GUI-Suite | **338 bestanden**, 0 fehlgeschlagen/übersprungen: bisherige 308 einschließlich aller 35 Registry- und MCP-01.2-Fälle plus 30 neue Regressionen |
| Vollständige Core-Suite | **7 667 bestanden, 4 übersprungen**, 0 fehlgeschlagen |
| Vollständige reguläre MCP-01.1-Suite | **89 bestanden**, 0 fehlgeschlagen |
| Ursprüngliche Review-Speichergegenprobe | Payload und Textrequest bereits während Retention sammelbar |
| Projekt-/Architekturabhängigkeiten | keine neue Projektreferenz in diesem Korrekturschritt; Core bleibt ohne Hostreferenz, Avalonia nutzt Abstractions, MCP nutzt nur Abstractions |
| `git diff --check` | erfolgreich |

Beim vorausgehenden tatsächlichen Neubau erschienen die sechs vorhandenen Core-Nullable-Warnungen und acht vorhandenen GUI-Nullable-Warnungen. Die warnungsfreien inkrementellen Endbuilds werden nicht als Beseitigung dieser bestehenden Warnungen ausgegeben. Core-Skips, expliziter Hardwarebenchmark und expliziter Installed-Codex-Test bleiben wie oben dokumentiert; keine Tests entfernt, deaktiviert oder abgeschwächt.

Der erste neue Testlauf hatte **26 bestandene und zwei fehlgeschlagene neue Proben**, gespeichert als `fix-new-first.trx`:

1. `PartialEffectCaptureFailureStillWaitsForResourceCleanup`: `ExecutionBusyException` mit Stack `InterpreterExecution.VerifyAccess:120 → VariableDictionary.Contains:54 → WorkbenchRegistryCorrectionTests:311`. Die neue Probe las während aktiver Hostreservation direkt den Core. Die Teilwirkungsprüfung wurde hinter den Abschluss gelegt; während Cleanup werden weiterhin nichtterminaler Snapshot und Admission geprüft.
2. `RetentionKeepsSafeResultButNotSixteenMiBGuiPayload`: Assertion `job.IsAlive == false` in der neuen Testzeile 83. Der JIT durfte das temporäre Ergebnis des direkten `Job(id)`-Aufrufs noch lebendig halten. Ein eigener nicht-inlinender WeakReference-Helfer begrenzt diese lokale Referenz. Der Payload-GC- und Callbackstrukturtest wurden unverändert beibehalten. Danach bestanden alle damaligen 28 neuen Fälle; mit zwei zusätzlichen Integrationsfällen bestehen nun 30.

Dies waren nachvollziehbare Fehler in neu geschriebenen Proben, keine sporadischen Headless-Ausnahmen. Im vollständigen Korrekturlauf wurde keine `HeadlessUnitTestSession.Dispose`-NRE beobachtet. Die Ursache der beiden früheren Zwischenfehler bleibt trotzdem ungeklärt; der Master-Backport wurde nicht übernommen. Statische Shared-Service-Lebensdauer und native Closing-Integration bleiben gesondert zu prüfen. Die neuen Tests entsorgen ihren lokalen Service und prüfen Timerdisposal; ein notwendiger nie zurückkehrender Callback bleibt absichtlich Busy.

Nachweise: `artifacts/mcp013-fixes/restore-main.log`, `restore-gui.log`, `build-main-final.log`, `build-gui-final.log`; `CryptoWorkBenchGuiTest/TestResults/fix-baseline.trx`, `fix-new-first.trx`, `fix-new-second.trx`, `fix-full-gui.trx`, `fix-final-gui.trx`; `CryptoScriptUnitTest/TestResults/fix-full-core.trx`; `CryptoWorkBench.Mcp.Tests/TestResults/fix-full-mcp.trx`. Die ursprüngliche Speicherprobe liegt weiterhin außerhalb des Zielworktrees im Chat-Arbeitsordner `mcp013-review-main`.

Offen bleiben unabhängiger Re-Review, native Plattform-/Desktop-Smokes und die bereits abgegrenzten Folgeschritte. MCP-01.4, Editorrevisionen, Save-Commit-Sperre, HTTP-Host, Pairing/Auth, neue Tools und persistente Jobs wurden nicht begonnen. Die Print-/Info-Multicast-Grenze bleibt unverändert. Kein Commit, Push, Merge oder Rebase; `.codex/config.toml` weder gelesen noch verändert.
