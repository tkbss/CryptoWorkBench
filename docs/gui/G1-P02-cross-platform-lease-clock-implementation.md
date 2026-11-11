# G1-P02 – Cross-Platform MCP Lease Clock Implementation

11.11.2026. Lokale Umsetzung mit ausdrücklicher Eigentümerfreigabe für Implementierung, erforderliche Tests und lokale Validierung. Branch `codex/g1-i01-composition`, unveränderte versionierte Basis `37b0a4b90e0413e7d53b4cff6eeb7b337e20ce1b` einschließlich I07. Kein Commit, Push oder Eingriff in Draft PR #6.

## Umsetzung und Sicherheitsreview

Die Empfehlung B aus [P01](G1-P01-cross-platform-lease-clock-feasibility.md) ist als kleine interne `LeaseClock` umgesetzt. `WorkbenchLeaseV2` ersetzt ausschließlich den bisherigen Inline-P/Invoke-Default durch `LeaseClock.Create()`. Die init-setzbare `Func<ulong> LeaseMilliseconds` bleibt für Tests erhalten. Der neue Adapter wählt genau eine Funktion anhand von OS und **Prozessarchitektur**. Die Auswahl führt noch keinen nativen Aufruf aus. Dadurch gelangen auch fehlende Bibliotheken/Symbole, falsche ABI, native Fehler und Konvertierungsfehler beim ersten Lesen in den vorhandenen ReadTime-Fehlerpfad; der Servicekonstruktor öffnet keinen abweichenden Zulassungspfad.

| Freigegebener Implementierungsumfang | Native Quelle | Validierung / Umrechnung |
|---|---|---|
| Windows x64 | `kernel32.dll!GetTickCount64` | Bestehende Quelle und unsigned Millisekunden beibehalten; die Funktion besitzt keinen separaten Fehler-Rückgabecode |
| Linux x64 | `libc!clock_gettime`, `CLOCK_BOOTTIME = 7` | Rückgabe muss 0 sein; errno wird unmittelbar gesichert. `timespec` nur LP64: zwei signed 64-bit-Felder, Größe 16, Offsets 0/8. Negative Sekunden und Nanosekunden außerhalb [0, 1e9) werden verworfen. `checked(seconds * 1000 + nanos / 1000000)` |
| macOS x64 / ARM64 | `libSystem.B.dylib!mach_continuous_time`, `mach_timebase_info` | Timebase-Rückgabe muss 0 sein; Numerator und Denominator müssen positiv sein. Zwei uint32-Felder, Größe 8, Offsets 0/4. Integerrechnung `UInt128(ticks) * numer / denom / 1000000`, danach checked-Konvertierung nach ulong |
| Andere OS-/Prozess-ABIs | Keine | Lesedelegate wirft PlatformNotSupportedException; keine native Funktion mit geratenem Layout |

Linux-Layoutprüfungen schützen die verwaltete Deklaration; die native LP64-Zuordnung stammt aus dem dokumentierten x64-ABI. Das ist keine Laufzeiterkennung beliebiger libc-ABIs. Insbesondere sind Linux ARM64, x86 und time64/32-bit nicht freigegeben. Die Quelle wird nicht bei Fehlern gewechselt. macOS-Timebase wird beim ersten Lesen in einer threadsicheren Lazy-Instanz geprüft und danach wiederverwendet; auch ein Initialisierungsfehler bleibt dort gespeichert. Fehlende native Funktionen führen sicher zur Ablehnung. Das Laden einer anderen Bibliothek oder eines Ersatz-Zählers wird nicht versucht.

Die Division rundet auf ganze Millisekunden ab. Das 64×32-bit-Produkt auf macOS passt vollständig in UInt128; ein zu großer Millisekundenwert wird dennoch beim Narrowing abgewiesen. Auf Linux werden sowohl Multiplikation als auch Addition geprüft. Die Adapter dürfen einen numerisch gültigen ulong-Wert liefern; die zusätzliche Deadline-Reserve von 600000 bleibt Aufgabe des unveränderten ReadTime. Gleiche Werte sind erlaubt. Null ist ein gültiger Uhrwert und kein Fehler-Sentinel.

Es gibt keinen UTC-, Stopwatch- oder Uptime-Fallback. Die Lease-Dauer, ReadTime einschließlich `lastLeaseTime` und dauerhaftem `clockBroken`, Deadlinevergleich, Acquire/Renew/Release, Replay/CAS, Epoch/Credentials, InterpreterHostQueue und Coordinator-Drain wurden nicht verändert. DispatcherTimer bleibt ein Wecksignal. Fehler vor Erwerb ergeben weiterhin TimerUnavailable; Fehler bei aktiver Lease beenden deren Autorisierung und starten den bestehenden Cancel-/Drain-Pfad. Eine scheinbare Erholung der Quelle setzt den Latch nicht zurück.

Quellen für die nativen Verträge: [GetTickCount64](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64), [Linux clock_gettime](https://man7.org/linux/man-pages/man2/clock_gettime.2.html), [Linux Clock-IDs](https://github.com/torvalds/linux/blob/master/include/uapi/linux/time.h), [glibc timespec-Deklaration](https://github.com/bminor/glibc/blob/master/time/bits/types/struct_timespec.h), [glibc x64-Typgrößen](https://github.com/bminor/glibc/blob/master/sysdeps/unix/sysv/linux/x86/bits/typesizes.h), [Apple mach_time.h](https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/mach_time.h). Konkrete reale Suspend-/Hibernate-Eigenschaften werden durch diese Quellprüfung nicht als lokal gemessen ausgewiesen.

## Geänderte Dateien und permanente Tests

Produkt: neue `CryptoWorkBenchAvalonia/Services/Automation/LeaseClock.cs` und eng begrenzte Default-Änderung in `WorkbenchLeaseV2.cs`. Keine andere Produktdatei, kein Projekt, Lock oder Paket wurde geändert.

Tests:

- `LeaseClockTests.cs`: Normalwerte, Millisekundenabrundung, Linux-Returncodes und errno, ungültige Felder, Multiplikations-/Additionsüberlauf, macOS-Returncodes und Timebase, breite Zwischenrechnung gegen BigInteger-Referenz, Narrowing-Überlauf, Strukturgrößen/-Offsets und abgelehnte Plattform-/Architekturkombinationen.
- `LeaseClockContractTests.cs`: Normal-/Gleichstände inklusive Null und maximaler Deadline-Reserve; Grenze bei 599999/600000/600001 ms; Renew vor und nach Ablauf; simulierte zwei Stunden Suspend nach 30 Sekunden ohne Dispatcher-Yield; UTC-Sprünge ±24 Stunden; Acquire-/Renew-Replays ohne Verlängerung; native Fehler, fehlende Funktionen, Rollback und Overflow mit dauerhaftem Fault-Latch; neue Serviceinstanz/neue Epoch und Ablehnung alter Credentials; Cancellation eines offenen Edits, ausstehender Drain und erst danach GUI-Freigabe.
- `LeaseV2Tests.cs`: nur `ProductionLeaseClockRetainsPlatformBoundaryAndGuiAdmission` an den neuen OS-/Architekturvertrag angepasst. Unterstützte Hosts müssen mit unverändertem Produktionsdefault eine Lease erteilen, monotone native Werte liefern und nach Release GUI-Eingabe erlauben. Nicht unterstützte Hosts behalten sämtliche TimerUnavailable-/Keine-Lease-/GUI-Assertions. OS-, Prozessarchitektur, Runtime und gemessene Millisekunden werden ausgegeben.
- `ManualLeaseClockPowerTests.cs`: eine neue **Explicit**-Probe für spätere physische Suspend-/Hibernate-Abnahme. Sie ist kein automatisierter CI-Nachweis und wurde in P02 nicht ausgeführt.

Die vorhandenen Fälle zu gleichzeitigem Renew, Versions-CAS, Replays, Revoke, verbotener Reaktivierung derselben Serviceinstanz, Edit-/Execution-Fences und Drain bleiben vollständig bestehen. Keine Sicherheitsassertion wurde ersatzlos entfernt. Die neue manuelle Probe fügt einen bewusst nicht automatisch ausgeführten Fall hinzu; bestehende Fälle werden nicht auf Explicit/Skip umgestellt.

## Lokale Validierung und Evidence

Die finalen Zahlen, Einzelinventare, Exitcodes, Hashes und Produktionsuhr-Ausgaben stehen in [G1-P02-results.json](evidence/G1-P02-results.json). Neue Rohdaten liegen ausschließlich unter `artifacts/g1-p02/`; frühere P01-/I01–I07-Evidence bleibt erhalten.

Finale vollständige Windows-Regressionsmatrix (jeweils tatsächliche TRX-Inventare):

| Suite | Debug PASS / FAIL / NotExecuted | Release PASS / FAIL / NotExecuted |
|---|---|---|
| GUI | 1164 / 0 / 1 | 1164 / 0 / 1 |
| MCP | 176 / 0 / 13 | 176 / 0 / 13 |
| Core | 11723 / 0 / 27 | 11723 / 0 / 27 |
| G1 Headless Feasibility | 12 / 0 / 0 | 12 / 0 / 0 |

Beide Solutions wurden in beiden Konfigurationen gebaut; alle 18 protokollierten Build-/Restore-/Testkommandos endeten mit Exit 0. Enthalten sind jeweils **214 L1-Fälle**, **7 G1/L1-Kompositionsfälle** und **63 Lease-/Uhrfälle** mit PASS. Die 46 neuen automatisierten Fälle erhöhen GUI von 1118 auf 1164; dazu kommt genau eine neue Explicit-Power-Probe mit NotExecuted. Gegenüber dem vollständigen I06-Einzelinventar fehlen keine bisherigen Fälle und kein bisheriges Outcome wurde verändert. Der Auswertungscode musste für groß-/kleinschreibungsverschiedene Testnamen auf ordinalen Schlüsselvergleich korrigiert werden; keine Test- oder Produktänderung war dafür nötig.

MCPs 13 NotExecuted-Fälle betreffen die bestehenden installierten-/gepaarten-Codex-Voraussetzungen. Die 27 Core-Fälle umfassen Darwin-spezifische Prüfungen, fehlendes Windows-Symlink-Privileg und einen expliziten Hardwaretest. Alle Gründe sind im vollständigen Inventar erhalten. VSTests kurze Konsolensumme zählt Explicit-Ausschlüsse teils nicht mit; deshalb sind oben die TRX-Zahlen maßgeblich, nicht „Skipped: 0“ aus der Kurzzeile. Der neue manuelle Test wird niemals als PASS gezählt.

Die Produktionsuhr wurde in beiden Konfigurationen tatsächlich auf `Microsoft Windows 10.0.26200`, OS und Prozess `X64`, Runtime `.NET 10.0.12` aufgerufen. Debug lieferte 344786515/344786515 ms, Release 345005265/345005265 ms. Gleiche aufeinanderfolgende Werte sind bei dieser Auflösung zulässig. Produktive Lease-Erteilung, MCP-Sperre der GUI sowie GUI-Rückgabe nach Release bestanden. Native Fehlercodes und Nicht-Windows-Konvertierungen wurden deterministisch injiziert/geprüft; es wurde kein realer Linux-/macOS-Aufruf vorgetäuscht.

Zwei vorbereitende Fehlversuche werden ausdrücklich nicht als bestanden gezählt:

1. `targeted-Debug.log`: Exit 1, Build vor Teststart gescheitert (`AttachDeveloperTools`), weil der erste `--no-restore`-Versuch noch konfigurativ unpassende Restore-Artefakte verwendete. Danach wurde Debug mit `--locked-mode -p:Configuration=Debug` wiederhergestellt. Keine Produktkorrektur außerhalb des Zeitquellenumfangs.
2. `targeted-restored-Debug.log` und zugehörige TRX: Exit 1, VSTest konnte innerhalb seiner unveränderten 90 Sekunden keine Verbindung zum Testhost herstellen; Lauf abgebrochen, keine fachlichen Tests bestanden. Derselbe gebaute Teststand bestand außerhalb der Sandbox in `targeted-host-Debug.log`: 63/63, Exit 0. Keine Timeout- oder Assertionänderung.

Die vollständige Validierung verwendet anschließend die reguläre Hostumgebung, die vorhandenen Prism-Buildskripte, Locked Restores und getrennte Debug-/Release-Ergebnisse. Der erste Sandbox-Restore meldete zusätzlich NU1900 wegen nicht erreichbarer NuGet-Auditdaten; dies ist kein erfolgreicher Vulnerability-Audit. Normale Compiler-/Prism-/Avalonia-Warnungen werden in den Originalprotokollen erhalten.

Auf diesem Host ist Windows x64 verfügbar. WSL meldet „not installed“; es wurde nichts installiert. Linux x64, macOS x64 und macOS ARM64 wurden hier weder nativ ausgeführt noch durch simulierte Konvertierungstests als abgenommen erklärt. Die spätere Vier-Plattform-CI benötigt gesondert genehmigten Commit und Push. Native G1-Fensterabnahmen und die zwei gepaarten installierten-Codex-Desktop-Prüfungen bleiben getrennte offene Nachweise.

## Reproduzierbare reale Suspend-/Hibernate-Abnahme

Status für Windows, Linux und beide macOS-Architekturen: **NOT VERIFIED**. Dieser Abschnitt bereitet eine spätere, beaufsichtigte Abnahme vor; P02 versetzt den Arbeitsplatz nicht selbst in Suspend.

Die neue Explicit-Probe verwendet die produktive native Uhr und den echten WorkbenchAutomationService mit aktiviertem MCP-Vertrag. Der Editor läuft in der vorhandenen Avalonia-Headless-Testsession. Das prüft realen OS-Zeitfortschritt und produktive Lease-/Cancellation-/Drain-Logik, aber weder MCP-Transport noch native Dock-HWNDs. Dafür bleiben die gesonderten Integrations-/Desktopabnahmen erforderlich.

1. Auf dem jeweiligen physischen Testhost oder einer ausdrücklich dokumentierten VM den exakt zu prüfenden Stand bauen. SDK/Runtime, OS-Version, OS-/Prozessarchitektur, Produkt-/Testassembly-SHA-256 und aktivierten Schlafmodus festhalten. Debug und Release getrennt abnehmen. Keine Änderungen an Uhr, Assertions oder Zeitlimits. Eigenen neuen Evidence-Ordner je Lauf verwenden.
2. Nur diesen Test selektieren. Vorher `CWB_LEASE_POWER_MODE` auf `suspend` setzen (PowerShell: `$env:CWB_LEASE_POWER_MODE='suspend'`; POSIX-Shell: `export CWB_LEASE_POWER_MODE=suspend`). Beispiel nach erfolgreichem Debug-Build:

   ```text
   dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName=CryptoWorkBenchGuiTest.ManualLeaseClockPowerTests.RealPowerTransitionExpiresBeforeDispatcherCanTick" --logger "console;verbosity=detailed" --logger "trx;LogFileName=power-Debug.trx" --results-directory artifacts/g1-p02-power/unique-run
   ```

3. Auf `READY` warten. Zu diesem Zeitpunkt sind die produktive Lease erteilt und ein bestätigungspflichtiger Edit offen. Die Probe hält **denselben Dispatcher-Turn** synchron mit kurzen Sleeps besetzt. Das ist ausschließlich Testkoordination: keine Änderung am Produkttimer. Keine Dispatcher-Nachricht und kein regulärer Lease-Tick kann zwischen Schlafphase und erster Renewal-Prüfung verarbeitet werden.
4. Innerhalb von 90 Sekunden über die reguläre OS-Bedienung suspendieren: Windows „Energie sparen“, Linux den tatsächlich konfigurierten System-Suspend, macOS „Ruhezustand“. Nach **mehr als elf Minuten** extern gemessener Schlafzeit wecken; optional den P01-Fall mit zwei Stunden wiederholen. Bildschirmabschaltung, Sperrbildschirm und bloßer Leerlauf genügen nicht. Keine fremden Prozesse schließen und keine Fokusmanipulation.
5. Die Probe verlangt einen Clock-Sprung von über 660000 ms zwischen zwei Proben. Direkt danach, ohne Dispatcher-Yield, muss der alte Renew synchron `LeaseExpired` liefern. Der Editor bleibt Releasing/gesperrt. Die Probe wartet auf tatsächliche Cancellation, gibt erst dann den gehaltenen Dialog frei, wartet auf Drain/GUI-Transition, prüft unveränderten Text und anschließend zulässige GUI-Eingabe. Sie ändert keine Zeitquelle und injiziert bei diesem Test keine Fake-Zeit.
6. Die Original-TRX und Konsolenausgabe sowie die korrespondierenden OS-Power-Ereignisse sichern. Windows: System-Ereignisprotokoll mit Kernel-Power/Power-Troubleshooter; Linux: Boot-/Sleep-Journal einschließlich tatsächlichem Schlafmodus; macOS: Power-Management-Schlaf-/Wake-Protokoll. Eintritt, Austritt und Dauer müssen zum Testlauf passen. Ein großer Messsprung allein kann auch von Prozess-/VM-Pause stammen und reicht nicht als realer OS-Suspend-Nachweis.
7. Hibernate separat mit `CWB_LEASE_POWER_MODE=hibernate` durchführen, **nur** wenn OS, Hardware, Speicherabbild und Konfiguration diesen Modus tatsächlich unterstützen. Windows-Ruhezustand ist von Standby zu trennen; Linux-Hibernation muss konfiguriert sein; macOS-Safe-Sleep/Standby darf nicht ohne Nachweis als klassisches Hibernate bezeichnet werden. Nicht verfügbare oder nicht eindeutig belegte Modi: **NOT VERIFIED**, keine globale Energie-Konfiguration für diese Abnahme ändern.

Fehlt der große Sprung, schlägt die Probe nach begrenztem aktivem Polling fehl; sie markiert eine uptimebasierte Uhr nicht als bestanden. Fehler oder unvollständige OS-Evidence werden bewahrt. Keine Wiederholung bis zu einem grünen Ergebnis ohne Ursachenklärung. Eine VM-Snapshot-Wiederherstellung ist kein gewöhnlicher Suspend und bleibt außerhalb dieser Zusage.

## Ergebnis und Grenzen

P02 liefert die lokale Implementierung und permanente Sicherheitsregressionen zur Eigentümerabnahme. Die plattformspezifischen Zweige sind implementiert; eine produktive Nicht-Windows- oder reale Suspend-/Hibernate-Freigabe wird damit noch nicht erteilt. Vollständige Vier-Plattform-CI und reale Power-Abnahme bleiben offen.

Die sechs genehmigten historischen EOF-Befunde bleiben unverändert. Der Standard-Whitespace-Check über die historische Commitfolge bleibt **FAIL (Exit 2)** mit genau diesen sechs Ausnahmen; neue Dateien erhalten keine zusätzliche Ausnahme. Master, Original-G1, L1, Dock/Prism und Paket-Pins werden gegen die Ausgangsmanifeste geprüft. Nach P02 stoppen; kein Commit, Push, Merge, PR-Update oder Ready-for-Review-Schritt.

Abschlussprüfung bestanden: 1173 der 1175 akzeptierten I07-Dateien bytegleich; ausschließlich `WorkbenchLeaseV2.cs` und `LeaseV2Tests.cs` als bereits versionierte Dateien geändert. Der vollständige Lease-Quelltext entspricht nach Entfernung des alten Default-Imports/P/Invoke und Einsetzen von `LeaseClock.Create()` exakt dem Ausgangstext. Alle 35 L1-Pfade und 4452 historische Evidence-Einträge einschließlich beider P01-Dateien sind hashgleich. Master (1055 Manifestdateien) und Original-G1 (1151 Manifestdateien) behalten HEAD, Dateihashes und vorherigen Status. Genau vier bestehende Commits nach der Master-Basis, null neue Commits; Index sauber. Der Worktree ist wegen der beauftragten lokalen Änderungen absichtlich nicht sauber.

`git diff --check` und `git diff --cached --check`: jeweils Exit 0. Neue Dateien separat gegen NUL geprüft: keine Whitespace-Diagnose; Exit 1 bezeichnet dort den vorhandenen neuen Inhalt, keinen bestandenen Standard-Diff-Check. Die historische Gesamtprüfung liefert unverändert ausschließlich:

- `CryptoWorkBenchGuiTest/DockOperationContractTests.cs:177`
- `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs:112`
- `docs/gui/G1-A04-R1-actual-callback-audit.md:247`
- `docs/gui/G1-A04-R2-test-cleanup.md:183`
- `docs/gui/G1-A04-R3-partial-failure-audit.md:332`
- `docs/gui/G1-A04-dock-operation-contract.md:168`

Jeweils `new blank line at EOF`; genehmigte historische Ausnahme, nicht behoben. Die Entscheidung **NO REACHABLE B4 FOUND** bleibt bestehen.
