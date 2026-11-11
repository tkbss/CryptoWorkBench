# G1-P01 – Cross-Platform MCP Lease Clock Feasibility

10.11.2026. Untersucht: `codex/g1-i01-composition`, HEAD `37b0a4b90e0413e7d53b4cff6eeb7b337e20ce1b` einschließlich I07. **Empfehlung B – PLATFORM CLOCK REQUIRED.** Keine Implementierung, keine Teständerung, kein Commit oder Push.

## Entscheidung

Keine untersuchte öffentliche, rein verwaltete .NET-10-Systemzeitquelle garantiert plattformübergreifend zugleich monotone, von UTC-Korrekturen unabhängige und während Suspend fortschreitende Lease-Zeit. Insbesondere sind `TimeProvider.System.GetTimestamp()` und `Environment.TickCount64` keine portable Lösung für den vorgegebenen Vertrag. Ein positiver Zeitabstand auf einem wachen Rechner beweist keine Suspend-Eignung.

Kleinste wartbare Lösung: eine interne gekapselte Zeitquelle hinter dem bestehenden `Func<ulong> LeaseMilliseconds`, mit Windows `GetTickCount64`, Linux `clock_gettime(CLOCK_BOOTTIME)` und macOS `mach_continuous_time`. Millisekunden, zehn Minuten, ReadTime-Latch, Admission, Epoch, Deduplication und Drain bleiben unverändert. Betriebssystemgrenzen und Konvertierungsfehler führen weiterhin zur Ablehnung. Dies ist eine Architekturfreigabeempfehlung, keine produktive Plattformabnahme. Eine Implementierung benötigt eine gesonderte Eigentümerfreigabe.

## Versionsbasis und Quellenqualität

`global.json` fixiert SDK 10.0.401 ohne Rollforward; die Anwendung zielt auf net10.0. Die vorhandene Desktop-runtimeconfig verlangt Microsoft.NETCore.App 10.0.0 als Frameworkbasis, fixiert aber nicht den tatsächlich ausgewählten Patch. Für die Quellprüfung wurde ausdrücklich der .NET-Runtime-Tag **v10.0.12** verwendet, passend zum zuvor lokal festgestellten installierten Runtime-Stand. Ein SDK-Pin ist kein Runtime-Pin; eine spätere Implementierungsabnahme muss die tatsächlich geladene Runtime pro Host aufzeichnen. Keine Schlussfolgerung stützt sich auf den veränderlichen Runtime-main-Branch.

Die öffentliche TickCount64-Dokumentation enthält mittlerweile .NET-11-Verhalten: Auf Windows zählt .NET 10 und früher Nichtwachzeit mit; die Änderung auf Ausschluss dieser Zeit gehört zu .NET 11. Das darf nicht rückwirkend auf .NET 10 übertragen werden. [API-Dokumentation](https://learn.microsoft.com/en-us/dotnet/api/system.environment.tickcount64?view=net-10.0), [.NET-11-Änderung](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/11/environment-tickcount-windows-behavior).

## Bestehender Vertrag im Repository

Maßgeblich: `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs`, `WorkbenchBindingV2.cs`, `WorkbenchExecutionV2.cs`, `WorkbenchAutomationRegistry.cs`, `ProcessAcquisitions.cs`, `EditorCoordinator.cs` und `CryptoWorkBenchGuiTest/LeaseV2Tests.cs`.

`LeaseMilliseconds` ist eine injizierbare `Func<ulong>`; der Produktionsdefault verwendet nur auf Windows GetTickCount64. Nicht-Windows wirft derzeit PlatformNotSupportedException. `Duration = 600_000` bleibt verbindlich. `ActiveLease.Deadline` ist eine absolute Grenze in dieser Zeitdomäne, kein UTC-Zeitpunkt.

`ReadTime` akzeptiert Gleichstand, aber keinen Rückgang gegenüber `lastLeaseTime`; außerdem muss `now <= ulong.MaxValue - Duration` gelten. Bei Ausnahme, Rückgang oder fehlendem Additionsspielraum setzt es `clockBroken` dauerhaft für die Serviceinstanz. Es liefert danach null, auch wenn spätere Werte wieder plausibel erscheinen. Weder Release noch neue Requests setzen diesen Latch zurück. Ein Nullwert der Uhr ist dagegen ein gültiger Anfangswert; Fehler dürfen nicht als künstlicher Nullwert maskiert werden.

Warum nicht rückwärts: Ein zurückgesetzter Zähler würde eine bestehende absolute Deadline künstlich in die Zukunft verschieben und könnte abgelaufene Autorisierung verlängern. Ein Wrap, eine neue Zeitbasis oder eine unkontrollierte Quellenumschaltung sind aus Sicht dieses Vertrags ebenso unzulässig. Gleichstand durch Millisekundenquantisierung ist korrekt.

### Acquire, Renew und Ablauf

Acquire läuft über OnEditorUiAsync und unter `sync`. Es prüft Authentisierung, Vertrag, Instance, Rechte, aktuelle Lease und registrierten Editor, Busy/Pending sowie Revision. ProcessAcquisitions reserviert den deduplizierten Requestslot. Erst nach Interpreterreservierung und erfolgreichem ReadTime wird die Lease vorbereitet. Uhrfehler ergibt TimerUnavailable ohne TakeControl; auch ein Timerstartfehler wird so abgewiesen. Registrierung und Rechte werden vor Übernahme nochmals geprüft. Die Deadline wird `now + Duration`.

Acquire-Replays erzeugen keine neue Lease und verlängern keine Deadline. Ein noch vorhandener beendeter Slot kann eine historische Receipt mit Releasing liefern; das ist keine neue aktive Autorisierung. Retirierte Erwerbungen werden nicht wiederbelebt. ProcessAcquisitions bindet Instance, Epoch, Principal, AcquisitionId, Editor und Revision; abweichende Wiederholung ergibt IdempotencyConflict. Kapazitätsgrenzen und terminale Slots bleiben erhalten.

Renew prüft CheckBinding vor einem Replay, verlangt passende Version und verweigert VersionExhausted. Nach UTC-Erfassung prüft es die Bindung nochmals; erst dann erhöht es die Version und setzt `Deadline = lastLeaseTime + Duration`. Ein gleicher Renew-Input liefert die gespeicherte Receipt, ohne erneut zu verlängern. Ein Renew bei oder nach Deadline scheitert bereits in CheckBinding.

CheckLease beendet die Autorisierung bei Shutdown, Epochwechsel, Detach, Reclaim/Fehlerzustand oder `ReadTime == null` beziehungsweise `now >= Deadline`. Die Reihenfolge bestimmt den sichtbaren ReleaseReason; ein Uhrfehler einer ansonsten aktiven Lease wird als Expired behandelt. Neue Acquire-Aufrufe treffen danach auf den TimerUnavailable-Latch.

CheckBinding ruft CheckLease auf und kontrolliert Epoch, Principal, Editor und Lease-ID. Editor-/Ausführungszulassung verwendet diese Bindung; der Ausführungspfad enthält zusätzlich eine ReadTime/Deadline-Fence vor Runnerstart sowie Prüfungen um Bestätigungsdialoge. Historische Job-Resultatreplays sind von neuer Ausführungszulassung zu unterscheiden: Das Lesen eines alten Resultats führt keinen Auftrag erneut aus.

### Release, Revoke, Coordinator und Epoch

Release prüft Zugriff und Slotidentität, stößt EndLease an und ist idempotent. Es kann Releasing zurückgeben, solange die Freigabe noch nicht zertifiziert ist. Revoke setzt den terminalen Grund zuerst, startet CancelAsync und fordert Abbruch aktiver MCP-Ausführung an. EndLease beginnt den bestehenden Coordinator-Drain. Ein abgelaufener Timer allein darf den Editor nicht entsperren.

CheckLease kann erst nach erfolgreichen Cancellation-Callbacks, erfolgreichem Drain und Ende von Ausführung, Edit, Dialog-, Dateischritt- und Save-Arbeit ResumeGui aufrufen. EditorCoordinator verlangt das richtige Drain-Zertifikat, keine Pending-/Active-/Observer-Arbeit und einen konsistenten nativen Dokumentzustand. Faulted oder unvollständige Cleanup-Zustände bleiben gesperrt. FinishEpochCloseAsync wartet zusätzlich Serviceproducer, Callback-Quieszenz und native Finalisierung ab; Fehler behalten die Admission-Sperre.

RevokeMcpCore löscht Epoch und Credentials und beginnt Epoch-Close. ActivateMcp erzeugt eine neue Guid, erlaubt jedoch wegen `activationUsed` keine Wiederaktivierung derselben Serviceinstanz. Ein Test für eine neue Aktivierungsepoche muss diesen bestehenden konservativen Lebenszyklus respektieren. Prozessneustart erzeugt neue Service-/Instanzidentität; absolute Leasedeadlines und alte Credentials dürfen nicht persistiert oder übernommen werden.

### Drei verschiedene Zeitrollen

| Rolle | Bedeutung |
|---|---|
| `clock.GetUtcNow()` | Receipt AcceptedAt/ExpiresAt; sichtbare UTC-Projektion mit AddMinutes(10), kann Systemuhrkorrekturen widerspiegeln |
| `LeaseMilliseconds` / Deadline | Tatsächliche Lease-Autorisierung; nur diese Domäne wird gegen die Ablaufgrenze geprüft |
| `DispatcherTimer`, eine Sekunde | Wecksignal für CheckLease, kein Ablaufzähler und keine Autorisierungsquelle |

Während Suspend läuft kein Dispatcher-Tick. Ein sicherer erster Zugriff nach Resume darf deshalb nicht von einem bereits gelieferten Tick abhängen. CheckBinding muss mit der suspendfähigen Uhr unmittelbar die alte Lease verwerfen. UTC-Receipt-Zeit und tatsächliche Deadline können nach einer Systemuhrkorrektur auseinanderliegen; ein Wechsel zur UTC-Receipt als Autorisierungsgrundlage wäre eine Sicherheitsvertragsänderung.

## .NET-10-Alternativen

| API | Plattform / Auflösung | Monotonie und Suspend | Testbarkeit / Ergebnis |
|---|---|---|---|
| TimeProvider.System.GetTimestamp | Windows/Linux/macOS; Stopwatch-Frequenz | Übernimmt die zugrunde liegende Stopwatch; keine zusätzliche portable Suspendgarantie | Eigener Provider gut injizierbar; Systemprovider erfüllt den Gesamtvertrag nicht |
| Stopwatch.GetTimestamp | Windows QPC; Unix hochauflösender Zähler, Frequenz abfragen | Windows QPC zählt nach Microsoft auch Sleep; Linux CLOCK_MONOTONIC und macOS CLOCK_UPTIME_RAW zählen Sleep nicht mit | Delegate/Adapter testbar; nicht portabel suspendfähig |
| Environment.TickCount64 | Öffentliche Millisekunden-API auf allen Zielplattformen; Auflösung kann gröber als 1 ms sein | .NET 10 Windows zählt Nichtwachzeit mit; Unix nutzt nicht suspendfähige Uptimequellen | Über Func testbar; kein sicherer einheitlicher Ersatz |
| DateTimeOffset.UtcNow / TimeProvider.GetUtcNow | Kalenderzeit, plattformübergreifend | Nicht monoton; manuelles Stellen/NTP können Ablauf verschieben | Fakes einfach, aber nicht als Autorisierungsuhr geeignet |
| TimeProvider.GetElapsedTime | Umrechnung zweier Provider-Timestamps | Erzeugt keine fehlende Suspendzeit | Hilfsfunktion, kein anderer Clock-Vertrag |
| Timer / PeriodicTimer / Task.Delay | Scheduling-Abstraktionen | Verzögerte Callbacks beweisen weder reale Dauer noch sichere Admission nach Resume | Für Wecksignale geeignet, nicht Ersatz der Deadlineprüfung |
| Process CPU-Zeit / TickCount32 | CPU-Arbeit beziehungsweise kurzer signed Wrap | Keine geeignete reale suspendfähige Zeitdomäne | Nicht verwenden |

TimeProvider.System delegiert Timestamp und Frequency an Stopwatch; GetElapsedTime rechnet eine Differenz um und ergänzt keine OS-Eigenschaften. Ein FakeTimeProvider weist nur die Anwendung gegen eine Simulation nach. [TimeProvider v10.0.12](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/Common/src/System/TimeProvider.cs).

Im geprüften Runtime-Tag führt Stopwatch.Unix über System.Native zu minipal_hires_ticks; TickCount64.Unix führt zu minipal_lowres_ticks. Linux verwendet MONOTONIC beziehungsweise dessen COARSE-Variante; der Apple-Zweig verwendet CLOCK_UPTIME_RAW. Die Native-BootTime-Funktion ist eine interne Berechnung und keine öffentliche suspendfähige .NET-Leaseabstraktion. Diese konkreten Mappings sind Implementierungsdetails des geprüften Tags, kein unveränderliches API-Versprechen. [Stopwatch Unix](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.Unix.cs), [Environment Unix](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Environment.UnixOrBrowser.cs), [Native Brücke](https://github.com/dotnet/runtime/blob/v10.0.12/src/native/libs/System.Native/pal_time.c), [minipal](https://github.com/dotnet/runtime/blob/v10.0.12/src/native/minipal/time.c).

Windows QPC ist nicht UTC-synchronisiert, seine Frequenz ist innerhalb eines Boots fest; Microsoft dokumentiert Sleep-/Hibernate-Einschluss. Das macht Stopwatch unter Windows allein brauchbar, nicht plattformübergreifend. Keine Annahme gleicher QPC-Frequenz zwischen Geräten oder Boots. [Microsoft QPC-Leitfaden](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps).

Alle Boot-/Performancewerte haben nur innerhalb derselben Clock-Epoche Bedeutung. Ein Prozessneustart setzt den Systemzähler nicht notwendig zurück, beseitigt aber Serviceidentität und Lease; ein Systemneustart beginnt eine neue Boot-Zeitbasis. Hochauflösende signed Nanosekundenwerte haben viel weniger Zahlenraum als ulong-Millisekunden. Ungeprüftes `ticks * 1000`, signed→unsigned-Casts, negative Differenzen und Gleitkommarundung an der Deadline sind zu vermeiden. GetElapsedTime ersetzt keine explizite Rollback-/Overflowprüfung. Für eine zukünftige Umsetzung checked-Arithmetik beziehungsweise UInt128-Zwischenprodukte und geprüfte Frequenz verwenden; gleiche Werte erlauben, echte Rückgänge fail-closed behandeln. Uhr-APIs liefern nicht durchgehend kontrollierbare Fehlercodes; die .NET-Unix-Implementierung enthält native Assertions statt eines allgemein zugesicherten verwalteten Fehlersignals.

## Suspend-Beispiel und ausgeschlossene Abkürzungen

Start bei T, Deadline T+600000; nach 30000 ms zwei Stunden Suspend. Ein suspendfähiger Zähler liefert nach Resume ungefähr T+7230000: der erste autorisierende Zugriff muss Expired erkennen, Cancellation/Drain anstoßen und jede neue Mutation verweigern. Eine Uptime-Uhr liefert ungefähr T+30000 und würde die alte Lease fälschlich für weitere 570000 ms zulassen. Ein später Timer-Tick repariert diese falsche Zeitbasis nicht.

UTC zusätzlich zu Stopwatch zu maximieren ist kein Beweis: Die UTC kann während Suspend zurückgestellt werden, ohne dass die nächste beobachtete Probe unter dem letzten UTC-Wert liegt. Resume-Events allein garantieren keine Reihenfolge vor dem ersten MCP-Zugriff und wären außerdem eine weitere Plattformintegration. Eine generell immer abgelehnte Lease ist fail-closed, aber keine funktionsfähige Erfüllung des Auftrags. Eine auf reine Wachzeit geänderte Lease wäre Option C und bedürfte einer ausdrücklichen Vertragsänderung; sie wird hier nicht empfohlen.

## Kleinste Plattformlösung

| Plattform | Quelle | Umsetzung und Fehlergrenzen |
|---|---|---|
| Windows | GetTickCount64 | Bestehenden Pfad behalten; Millisekunden, typischerweise 10–16 ms Auflösung, keine UTC-Korrekturabhängigkeit |
| Linux x64 | clock_gettime(CLOCK_BOOTTIME) | Seit Kernel 2.6.39 suspendeinschließend; Rückgabecode prüfen, timespec-Felder validieren; checked Sekunden×1000 + Nanosekunden/1000000 |
| macOS x64/ARM64 | mach_continuous_time | Ab macOS 10.12; mit geprüfter mach_timebase_info numer/denom in Millisekunden umrechnen; UInt128 für Multiplikation, denom nie null |

Linux BOOTTIME ist nicht durch diskontinuierliches Stellen der Kalenderuhr veränderbar; Frequenzkorrekturen bleiben möglich. Auflösung per clock_getres ermitteln, Nanosekundenfeld nicht mit tatsächlich erreichter Nanosekundenpräzision gleichsetzen. [Linux-man-pages](https://man7.org/linux/man-pages/man2/clock_gettime.2.html). Apple bezeichnet continuous_time ausdrücklich als während Sleep fortschreitend; die Timebase-Konvertierung ist architekturabhängig. [Apple-Systemheader](https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/mach_time.h). Windows-Vertrag und Auflösung: [GetTickCount64](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64).

Empfohlen ist eine einzige interne Factory/Adapterklasse mit drei kleinen OS-Zweigen, ohne Paket, native Zusatzbibliothek oder Änderung der MCP-Architektur. Linux-x64-timespec-ABI und macOS-Timebase müssen explizit geprüft werden; keine ungeprüfte Ausweitung auf 32-bit oder andere RIDs. Fehlende Symbole, nicht unterstützte Clock-ID, ungültige Zeitdaten oder Konvertierungsüberlauf werfen kontrolliert in den bestehenden ReadTime-Fehlerpfad. Kein Fallback auf UTC, Stopwatch oder einen konstanten Wert. Die Quelle während einer Serviceinstanz nicht wechseln.

Suspend-/Hibernate-Unterstützung der konkreten Hardware, OS-Version und Virtualisierung muss real abgenommen werden. Apple garantiert im zitierten Header Sleep-Fortschritt, nicht jede mögliche VM-Snapshot-/Rollback-Situation. VM-Snapshot-Rollback ist kein gewöhnlicher Suspend und benötigt bei entsprechender Bedrohungsannahme eine eigene Vertragsentscheidung. Dies ändert nicht die Empfehlung B für die angegebenen Desktopplattformen; unbestätigte reale Betriebsmodi bleiben bis zur Abnahme gesperrt beziehungsweise nicht freigegeben.

## Geplante Sicherheits- und Testmatrix – nicht ausgeführt

| Fall | Deterministischer Nachweis / erwartetes Ergebnis |
|---|---|
| Acquire normal | T=1000, gültige Rechte/Revision, Deadline 601000, eine Lease, ein TakeControl |
| Grenze | T+599999 gültig; T+600000 und darüber Expired; kein Dispatcher-Tick vorausgesetzt |
| Renew vorher/nachher | Vor Grenze genau eine neue Version und neue zehn Minuten; bei/nach Grenze Ablehnung ohne Wiederbelebung |
| Gleichstand / Rollback | Gleiche Samples zulässig; kleineres Sample verriegelt clockBroken dauerhaft, auch nach scheinbarer Erholung |
| Uhrfehler | Ausnahme bei Acquire: TimerUnavailable, keine Übernahme; bei aktiver Lease: Revoke/Expired und Drain; Folge-Acquire gesperrt |
| Overflow | MaxValue-Duration zulässig; +1 abgewiesen; Multiplikationsüberlauf, negative/native ungültige Werte und denom=0 im Adapter prüfen |
| Suspend simuliert | Uhr springt um zwei Stunden, Timer bleibt ungetickt; erster Edit/Execute/Renew darf keine alte Autorisierung nutzen |
| UTC-Sprung | Receipt-UTC vor/zurück ändern; monotone Deadline unverändert; keine erneute Zulassung durch Kalenderzeit |
| Neustart/Epoch | Neue Serviceinstanz und neue IDs; alte Credentials/Lease/Acquisition nicht übernehmbar; Wiederaktivierung derselben Instanz bleibt verboten |
| Deduplication | Acquire-Replay verlängert nicht; divergenter Request Konflikt; Renew-Replay nur identische gespeicherte Receipt, nach Expiry kein gültiger Replay |
| Konkurrenz | Gleichzeitiges Renew gleicher Version genau eine Versionsänderung; Release/Expiry/Revoke-Rennen terminal und ohne doppelte Freigabe |
| Drain | Aktive Arbeit, Callback, offenes natives Update und Save halten Releasing; GUI erst nach echtem Zertifikat; Cleanupfehler bleiben Faulted/gesperrt |
| Ausführungs-Fence | Suspend/Ablauf während Bestätigung und vor Runnerstart; kein neuer Interpreterlauf nach Deadline |
| Timer | Startfehler, verspäteter Tick, Tick nach Revocation; Timer ersetzt weder Uhr noch Admission-Prüfung |

Vorhandene Anker sind `ClockRollbackOrOverflowClosesAdmission`, `LostRepliesCasAndDelayedTimerCannotResurrectLease` und `ProductionLeaseClockRetainsPlatformBoundaryAndGuiAdmission`. Letzterer bestätigt heute bewusst die Nicht-Windows-Ablehnung und muss erst im gesonderten Implementierungsauftrag gezielt angepasst werden; nicht jetzt und nicht pauschal entfernt. Ergänzende Fake-Tests müssen die echte LeaseMilliseconds-Naht verwenden, nicht lediglich Receipt-TimeProvider verstellen.

Reale Matrix: Windows x64, Linux x64, macOS x64 und ARM64, jeweils Debug/Release; OS-/Runtimeversion, Uhrwerte und Frequenz/Timebase dokumentieren. Lease erteilen, nach 30 Sekunden tatsächlich suspendieren, nach mindestens zehn Minuten (zusätzlich Beispiel zwei Stunden) fortsetzen; sofortigen MCP-Edit/Execute/Renew vor einem Timer-Tick prüfen. Suspend-to-RAM und tatsächlich unterstütztes Hibernate getrennt nachweisen. Systemuhrkorrektur und Prozessneustart separat testen. Nicht unterstütztes Hibernate als NOT VERIFIED ausweisen. Solche Tests benötigen kontrollierte Hosts und eigene Freigabe; GitHub-Headless-CI und FakeTimeProvider beweisen dies nicht.

## Abschluss und Grenzen

**B – PLATFORM CLOCK REQUIRED.** Reine .NET-10-APIs erfüllen den verlangten plattformübergreifenden Suspend-Vertrag nicht. Kleinster Vorschlag ist der gekapselte OS-Adapter unter Beibehaltung der bestehenden Sicherheitslogik. Keine Änderung der zehn Minuten, kein UTC-Fallback, kein neues Lease-Protokoll. Kein produktiver Go vor realer Plattformabnahme.

P01 besteht ausschließlich aus Quellenanalyse und Testentwurf; keine Tests, Suspend-/Hibernate-Experimente oder neue CI ausgeführt. Produkt, bestehende Tests, Pakete, Dock/Prism und historische Evidence bleiben unverändert. PR #6 wurde nicht verändert. Maschinenlesbare Zusammenfassung: [G1-P01-feasibility.json](evidence/G1-P01-feasibility.json). Nach P01 stoppen; Implementierung nur mit gesonderter Eigentümerfreigabe.
