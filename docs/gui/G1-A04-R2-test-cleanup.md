# G1-A04-R2 – Test Cleanup

## Ergebnis und Eigentümerentscheidung

**R2-Bereinigung abgeschlossen; der separate Partial-Failure-Befund bleibt offen und rot.**

Der Projekteigentümer hat nach G1-A04-R1 (`NO REACHABLE B4 FOUND`) die zusätzliche B4-Anforderung für den aktuellen G1-Produktumfang verworfen. Die sechs ausdrücklich benannten künstlichen Callback-Mutationsfälle wurden deshalb aus der aktiven Testsuite entfernt. Sie werden weder als bestanden noch als behobene Produktfehler gezählt.

R2 grenzt die Entscheidung ausdrücklich vom siebten A04-Fehlerfall ab: `PartialMoveFailureMustLatchExistingLayoutFault` bleibt eine aktive, unveränderte Anforderung. Die frühere R1-Empfehlung wird hier entsprechend der verbindlichen Eigentümerentscheidung präzisiert; die historischen A04-/R1-Berichte werden nicht umgeschrieben.

Repository `tkbss/CryptoWorkBench`; Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1`; Branch `feature/gui-g1`; HEAD unverändert `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Ausführung am 2026-11-09. Keine Produktänderung, kein Commit, Push oder Merge. A05 wurde nicht begonnen.

## 1. Tatsächliche Testinventar-Differenz

Quelle: [DockOperationContractTests.cs][r1]. Vergleichsbasis sind die tatsächlichen Debug-/Release-TRX-Ergebnisse des letzten A04-Regressionslaufs; neue Ergebnisse stammen aus vollständigen, ungefilterten R2-Regressionsläufen. Zusätzlich wurde die neue Assembly beider Konfigurationen mit `--list-tests` geprüft.

### Genau sechs entfernte Testfälle

| Testfall | A04 Debug / Release | R2 Debug / Release |
|---|---|---|
| `MovedMustRejectIndependentFloat` | Failed / Failed | Nicht entdeckt, nicht ausgeführt |
| `AddedMustRejectIndependentMove` | Failed / Failed | Nicht entdeckt, nicht ausgeführt |
| `ClosingMustRejectIndependentOtherHostClose(True)` | Failed / Failed | Nicht entdeckt, nicht ausgeführt |
| `WindowClosingMustRejectNestedManagerDrop` | Failed / Failed | Nicht entdeckt, nicht ausgeführt |
| `SameSourceTargetMustNotAuthorizeSecondMove` | Failed / Failed | Nicht entdeckt, nicht ausgeführt |
| `MovedMustNotMutateAnotherFloatingHost` | Failed / Failed | Nicht entdeckt, nicht ausgeführt |

Fünf Testmethoden wurden gelöscht. Beim parametrisierten Close-Test wurde **ausschließlich `[TestCase(true)]`** entfernt. Signatur, Methodenrumpf, Assertions und `[TestCase(false)]` bleiben unverändert; der False-Fall wurde in beiden Konfigurationen entdeckt und bestanden.

### Alle sieben verbleibenden A04-Testfälle

| Testfall | Debug | Release | Quellvergleich |
|---|---|---|---|
| `ClosingMustRejectIndependentOtherHostClose(False)` | Passed | Passed | Methodenrumpf unverändert |
| `PropertyChangedRejectsReset` | Passed | Passed | Unverändert |
| `LegitimateSourceCleanupCompletesInsideObserver` | Passed | Passed | Unverändert |
| `LeaseRejectsIndependentFactoryAndManagerBeforeMutation` | Passed | Passed | Unverändert |
| `PublicHookNullScopeDoesNotVetoManagerMutation` | Passed | Passed | Unverändert |
| `PublicHookDisposeExceptionMasksOriginalManagerException` | Passed | Passed | Unverändert |
| `PartialMoveFailureMustLatchExistingLayoutFault` | **Failed** | **Failed** | **Unverändert** |

Kein Ignore/Explicit, kein umgekehrtes Expected-Failure-Ergebnis, keine abgeschwächte Assertion und kein Ausführungsfilter zum Ausblenden des roten Falls. Die verbleibenden künstlichen Prüfungen wie `PropertyChangedRejectsReset` wurden nicht zusätzlich entfernt: Der Auftrag benennt genau sechs zu löschende Fälle.

Alle Hilfsmethoden/Fixtures in dieser Datei bleiben benötigt: Run, Target und Move von verbleibenden Integrationsprüfungen; HookFactory, ErrorScope und HookLayout von den beiden erhaltenen Hook-Charakterisierungen. Es gab daher keine ausschließlich den entfernten Fällen dienende Hilfsmethode oder Fixture zu löschen.

Nachweise:

- [Tatsächliche Testentdeckung Debug][r2] und [Release][r3]: jeweils genau die sieben oben aufgeführten Fälle.
- [Vollständige TRX-Inventardifferenz][r4]: GUI jeweils genau sechs entfernt, null hinzugefügt, keine Ergebnisänderung eines erhaltenen Falls.
- [Inventar vorher][r5] und [Inventar nachher][r6].
- [Ordinaler Quellvergleich][r7]: alle sieben erhaltenen Methoden sowie der native Partial-Failure-Abschnitt bis einschließlich Shutdown identisch.
- [Exakter R2-Testcode-Patch][r8] gegen die vor der Änderung gesicherten Dateien.

## 2. Native A04-Probe

In [DockOperationNativeProbe.cs][r9] wurden der zusätzliche `DockableMoved`-Subscriber und dessen unabhängiger `FloatDockable(info)`-Aufruf entfernt. Der ursprüngliche Move wird jetzt direkt ausgeführt.

Entfallen sind ausschließlich:

1. `B4-independent-float-rejected`: die verworfene aktive B4-Anforderung.
2. `callback-observer-active`: die nur für diesen künstlichen Angreifer benötigte Nachweisprüfung einschließlich fired-/observer-Instrumentierung und Subscribe/Unsubscribe.

Der Journalmarker heißt nun `A04-after-source-cleanup` statt `A04-after-reentrant-float`; der Kopfkommentar benennt die verbleibende Partial-Failure-Anforderung. Der Native-Runner, sein Exit-Verhalten und andere Prüfmodi wurden nicht verändert.

Das native Inventar sinkt von **23 auf 21 Prüfungen**. Alle übrigen Prüfnamen sind erhalten; keine neue Prüfung wurde hinzugefügt. Quelle: [native-inventory.json][r10].

Erhalten und in allen vier Läufen bestanden sind insbesondere HWND-/Owner-/Modellzuordnung, legitimer Source-Cleanup, Fortbestand des anderen Hosts, Capture/Decode, Maximize/Restore samt identischem Capture, Generation, Lease-Sperre und Editor-/Document-/Undo-Identität. Im separaten Partial-Failure-Abschnitt bestehen Originalexception, Ablehnung der Persistierung des unvollständigen Dokumentbaums, Observer-Cleanup, Save-Intent-Erhalt und finaler Shutdown. **Nur die unveränderte Fault-Latch-Prüfung bleibt rot.**

| Nativer Lauf | Prozess-ID | Bestanden | Fehlgeschlagen | Timeout / Exit |
|---|---:|---:|---:|---|
| Debug, Prozess 1 | 30940 | 20 | 1 | Nein / 1 |
| Debug, Prozess 2 | 35688 | 20 | 1 | Nein / 1 |
| Release, Prozess 1 | 24100 | 20 | 1 | Nein / 1 |
| Release, Prozess 2 | 29772 | 20 | 1 | Nein / 1 |

Einziger Fehler in jedem Lauf: `partial-failure-latches-A02-fault`. Vier frische Prozesse, keine automatische Wiederholung bis zum Bestehen. Der Runner endet wegen dieses aktiven Kriteriums weiterhin mit Exit 1; die native Suite wird nicht als vollständig bestanden ausgewiesen.

Rohdaten einschließlich Journal/Trace: [Native Aufrufe und Exitcodes][r11], [Debug 1][r12], [Debug 2][r13], [Release 1][r14], [Release 2][r15].

## 3. Vollständige Debug-/Release-Regression

| Suite | Debug: bestanden / fehlgeschlagen / nicht ausgeführt | Release: bestanden / fehlgeschlagen / nicht ausgeführt |
|---|---:|---:|
| GUI gesamt | **1.093 / 1 / 0** | **1.093 / 1 / 0** |
| Davon ursprüngliche A03-GUI-Baseline | **1.087 / 0 / 0** | **1.087 / 0 / 0** |
| Davon erhaltene A04-Fälle | **6 / 1 / 0** | **6 / 1 / 0** |
| MCP | **176 / 0 / 13** | **176 / 0 / 13** |
| Core (CryptoScriptUnitTest) | **11.512 / 0 / 27** | **11.512 / 0 / 27** |
| Headless (G1DockFeasibility.Tests) | **12 / 0 / 0** | **12 / 0 / 0** |

Die GUI-Gesamtzahl sinkt von 1.100 auf **1.094**; die Zahl bestandener Tests bleibt **1.093**. Der Abbau von sieben auf einen Fehler entsteht ausschließlich durch Entfernung der sechs verworfenen Anforderungen, nicht durch deren Bestehen. Die A03-Baseline wird über die erhaltenen tatsächlichen Testfälle außerhalb DockOperationContractTests abgeglichen, nicht nur über eine zufällig gleiche Gesamtzahl.

Die nicht ausgeführten MCP-/Core-Fälle sind dieselben wie in A04, einschließlich vorhandener Explicit-/Plattform-/Symlink-Voraussetzungen; sie zählen nicht als bestanden. Die Tabelle verwendet die einzelnen TRX-Ergebnisse. Die knappe VSTest-Konsolensumme lässt teils Explicit-Fälle aus und ist deshalb nicht die Inventargrundlage.

**Dynamische MCP-Anzeigenamen:** Der rohe Namensvergleich zeigt je zehn alte/neue Anzeigenamen. Es wurden keine MCP-Tests entfernt oder ergänzt. ContractTests.Samples verwendet FakeAutomationService.Variables/Completed, die zur Laufzeit `DateTimeOffset.UtcNow` in die dargestellten Argumentobjekte aufnehmen ([FakeAutomationService.cs][r16], Zeilen 25–28). Nach ausschließlich auf diese Zeitstempeldarstellung begrenzter Normalisierung stimmen alle **189 eindeutigen Fälle samt Ergebnissen** überein. Rohdifferenz und Normalisierungsnachweis bleiben getrennt erhalten: [mcp-runtime-name-comparison.json][r17]. Core und Headless haben bereits ohne Normalisierung null Inventar- oder Ergebnisänderungen.

### Ausführung und Infrastrukturversuch

Der erste Sandbox-Versuch unter `artifacts/g1-a04-r2/regression/` scheiterte vor GUI-Testausführung an der VSTest/Testhost-Verbindung nach 90 Sekunden. Der Lauf wurde beendet; Rohlog und abgebrochenes TRX wurden erhalten. Er wird weder als Produktfehler noch als bestandene Testausführung gezählt.

Die vollständige Regression wurde danach außerhalb der Sandbox unter `regression-host/` ausgeführt. Alle acht Restore-Schritte im Locked Mode sind erfolgreich. GUI hat je Exit 1 wegen des oben ausgewiesenen Fault-Tests; MCP, Core und Headless haben je Exit 0. Das Gesamtskript meldet folgerichtig weiterhin einen Regressionsfehler. Native Restore/Build sind in beiden Konfigurationen erfolgreich. Vorhandene Compiler-/Avalonia-Warnungen und Warnungen zur nicht erreichbaren NuGet-Vulnerability-Abfrage sind in den Logs erhalten.

Verwendete Aufrufe, jeweils aus dem angegebenen G1-Worktree:

```powershell
# Historisches Skript unverändert gelesen/ausgeführt, ausschließlich neues Ausgabeziel:
./artifacts/g1-a04/run-regression.ps1 -EvidenceDirectory artifacts/g1-a04-r2/regression-host

# Für Debug und Release, im bestehenden vorbereiteten Prism-Environment:
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c <Configuration> --no-build --no-restore --list-tests --filter FullyQualifiedName~DockOperationContractTests

./eng/gui/Validate-G1LayoutRoles.ps1 -Configuration Both -OperationContractOnly -EvidenceDirectory artifacts/g1-a04-r2/native
```

Die vollständigen Testläufe selbst waren **ungefiltert**. Einzelargumente/Exitcodes: [Regression][r18] und [Discovery][r19]. TRX-Dateien liegen für jede Suite/Konfiguration neben den Regressionslogs in diesem neuen Verzeichnis.

## 4. Weiterhin offener Partial-Failure-Befund

`PartialMoveFailureMustLatchExistingLayoutFault` injiziert eine Exception aus DockableRemoved nach Entfernung des verpflichtenden Dokuments. In Debug und Release bestehen die vorangehenden Assertions:

- Die ursprüngliche Exception bleibt erhalten.
- ObserverDepth kehrt auf null zurück.
- Das entfernte Dokument fehlt tatsächlich im sichtbaren Baum.
- Capture lehnt den ungültigen Layoutzustand mit InvalidDataException ab.

Die letzte Assertion bleibt unverändert rot:

```text
An invalid partial tree must latch the existing A02 fault state.
Assert.That(h.Layout.LayoutFaulted, Is.True)
  Expected: True
  But was:  False
```

Der native Nachweis bestätigt denselben getrennten Fehler. Dies ist keine unabhängige B4-Callback-Mutation, sondern die bestehende Anforderung an den Fault-Zustand nach einer partiell fehlgeschlagenen Mutation. Sie bleibt aktiv und muss später fachlich bewertet werden. R2 repariert, deaktiviert oder akzeptiert diesen Befund nicht.

## 5. Vollständige Änderungsliste und Integrität

Änderungen gegenüber dem unmittelbar vor R2 gesicherten Arbeitsstand:

| Datei | Änderung |
|---|---|
| `CryptoWorkBenchGuiTest/DockOperationContractTests.cs` | Fünf benannte Methoden und ausschließlich TestCase(true) entfernt; alle sieben verbleibenden Methoden unverändert. |
| `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs` | Künstlicher B4-Float-Handler, dessen Zustandsvariablen und zwei zugehörige Checks entfernt; normalen Move erhalten; Journalmarker/Kopfkommentar angepasst. |
| `docs/gui/G1-A04-R2-test-cleanup.md` | Dieser neue Bericht. |

Zusätzlich wurden ausschließlich neue, ignorierte Test-/Audit-Artefakte unter `artifacts/g1-a04-r2/` erzeugt: Baselinekopien und Hashmanifest, Logs/TRX, Discovery, Native-JSON/Trace/Journal, Inventare/Differenzen, Patch, Quell-/Integritäts- und Git-Status-Nachweise. Build-Ausgaben wurden regulär neu erzeugt; sie sind keine Produktquelländerungen. Kein bestehender Evidenzpfad wurde als Ausgabeziel wiederverwendet.

Abschlusskontrolle:

- **1.136 vorhandene Quelldateien** gegen R2-Startmanifest geprüft: ausschließlich die beiden benannten Testdateien geändert; **1.134 bytegleich**, darunter Produktcode, Paketsperren, EditorCoordinator, MCP und alte A04-/R1-Dokumentation. Dazu kommt ausschließlich der neue R2-Bericht. [Quellintegrität][r20].
- **1.631 A04-Artefakte und 7.654 frühere Artefakte: 0 Hashabweichungen.** [Historische Integrität][r21].
- Dock-Quellcheckout unverändert/sauber; Prism-Pin wurde bei den Build-Einstiegen mit **1.180 gepinnten Dateien** verifiziert. Keine Änderungen an diesen Bibliotheken.
- `git diff --check`: **Exit 0**, keine Befunde. Zusätzlich beide bereits vor R2 unversionierten Testdateien per No-Index-Vergleich geprüft: keine Whitespace-Befunde; No-Index-Exit 1 bezeichnet hier lediglich den vorhandenen Inhaltsunterschied. [test-diff-check.json][r22].
- Der G1-Worktree war bereits vor R2 umfangreich geändert/unversioniert. Er wird nicht als sauber ausgegeben. Vollständiger abschließender Status mit einzelnen unversionierten Dateien: [final-git-status.txt][r23]; R2-Delta und Check-Exitcode: [final-checks.json][r24]. Hauptcheckout unverändert/sauber, HEAD/Branch unverändert.

**Nach R2 gestoppt. Keine A05-Arbeit, keine weitergehende Fehlerkorrektur und keine Freigabe des offenen Partial-Failure-Befunds.**

[r1]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchGuiTest/DockOperationContractTests.cs>
[r2]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/discovery/Debug.log>
[r3]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/discovery/Release.log>
[r4]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/test-inventory-diff.json>
[r5]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/baseline/test-inventory.json>
[r6]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/test-inventory-after.json>
[r7]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/retained-source-check.json>
[r8]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/test-cleanup.patch>
[r9]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs>
[r10]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/native-inventory.json>
[r11]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/native/commands.json>
[r12]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/native/Debug-operation-contract-1.json>
[r13]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/native/Debug-operation-contract-2.json>
[r14]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/native/Release-operation-contract-1.json>
[r15]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/native/Release-operation-contract-2.json>
[r16]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBench.Mcp.Tests/FakeAutomationService.cs>
[r17]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/mcp-runtime-name-comparison.json>
[r18]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/regression-host/commands.json>
[r19]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/discovery/commands.json>
[r20]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/source-integrity.json>
[r21]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/historical-integrity.json>
[r22]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/test-diff-check.json>
[r23]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/final-git-status.txt>
[r24]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r2/final-checks.json>

