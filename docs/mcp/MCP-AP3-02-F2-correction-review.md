# MCP AP3-02-F2 – History-Capability-Bindung

Stand: 29.10.2026. **READY FOR INDEPENDENT RE-REVIEW** für RR-01. Beide originalen Gegenproben zuerst rot, nach der Korrektur mit byteidentischen Testmethoden grün. Native Windows-History-/physische Tastaturabnahme bleibt OPEN.

## 1. Ausgangsstand und exakter Patchumfang

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, Branch `feature/mcp-ap3`, HEAD unverändert `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Kein Commit, Staging, Push oder Merge; keine anderen Worktrees verändert; `.codex/config.toml` weder gelesen noch geändert.

Bei Beginn bereits modifiziert: WorkbenchTextEditor.cs, HistoryService.cs, CryptoScriptEditViewModel.cs. Bereits unversioniert: HistoryCoordinatorTests.cs, HistoryCorrectionTests.cs sowie die Berichte AP3-01, AP3-02-Implementierung, AP3-02-Independent-Review, F1-Korrektur und F1-Nachreview. Vollständiger Anfangsstatus: [status-before.txt](../../artifacts/ap3-02-f2/status-before.txt); vorhandenes Tracking-Diff: [before.patch](../../artifacts/ap3-02-f2/before.patch); SHA-256 der relevanten Bestandsdateien einschließlich unversionierter Tests/Berichte: [before.xml](../../artifacts/ap3-02-f2/before.xml).

**Ausschließlich durch F2 geändert/ergänzt:**

| Datei | F2-Änderung |
|---|---|
| `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` | 43 zusätzliche Zeilen: enger RunHistory-Eingang und verschachtelte operationsgebundene Capability; vorhandenes Commit/RunGui/Drain unverändert |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | Capabilityparameter, Ablehnung ohne Capability und einmaliger Verbrauch vor nativer Gruppe; bestehender Remove-/Insert-Körper unverändert |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | Navigation verwendet RunHistory und reicht dessen Capability an den Helfer weiter; vorherige Ticket-/Q-Klammer liegt jetzt im engen RunHistory-Eingang |
| `CryptoWorkBenchGuiTest/HistoryCapabilityTests.cs` | Neu: zwei originale RR-01-Rot-/Grünfälle |
| `CryptoWorkBenchGuiTest/HistoryCapabilityAdversarialTests.cs` | Neu: elf zusätzliche Capability-/Fehler-/Lifecyclefälle |
| Dieser Bericht | Neu |

HistoryService, alle bisherigen Tests und alle Bestandsberichte bleiben gegenüber F2-Beginn byteidentisch. Der abschließende Vergleich des Vorhermanifests weist ausschließlich die drei oben genannten Produktionsdateien als geändert aus. Die neuen RR-01-Tests sind zusätzlich mit ihrem vor der Produktkorrektur erfassten Hash und ihrer archivierten Rotfassung verglichen: unverändert.

Frameworkbasis unverändert: SDK 10.0.401, net10.0, Avalonia 12.1.4, AvaloniaEdit 12.0.0 und genehmigte Prismquelle. Keine Paket-/Prism-, Core-, EC21-, Datei-, Lease- oder HTTP-Änderungen.

## 2. Ursache und neue operationsgebundene Berechtigung

Grundlage ist [RR-01 im F1-Nachreview](MCP-AP3-02-F1-independent-rereview.md). Der bisherige Helfer prüfte nur IdentityLock, Active, ObserverDepth, Dokumentgleichheit und lokalen Reentranzzustand. Das bezeichnete keine konkrete Historyzulassung. Ein gewöhnliches RunGui-Ticket konnte deshalb den nativen Zweischritt ausführen.

Die neue `EditorCoordinator.HistoryCapability` besitzt einen **privaten Konstruktor** und eine readonly Referenz auf das konkrete Original-OperationTicket. Sie wird ausschließlich im dedizierten RunHistory-Pfad erzeugt:

1. Vorhandenes TryBegin nimmt ein frisches Ticket an; Active/ObserverDepth/Generation/Registrierung/Locks bleiben unter den vorhandenen Regeln.
2. Die bestehende globale Q-Reservation muss erfolgreich sein. Bei Busy wird das Ticket entsorgt, ohne eine Capability zu erzeugen oder den Actiondelegate aufzurufen.
3. Vorhandenes Commit prüft das Originalticket erneut und setzt es aktiv.
4. Erst innerhalb dieser synchronen Commitaktion wird die Capability erzeugt und explizit an den Historydelegate übergeben.
5. Ein finally entwertet sie unwiderruflich beim Verlassen der Aktion, auch bei No-op oder Exception. Danach folgen unveränderte Commitfinalisierung und Q-Freigabe.

`Consume` prüft das **identische aktive Ticket**, dessen Aktualität über IsCurrent (Generation, ursprüngliche Registrierung/EditorId/Revision und Originaldokument), ObserverDepth=0, den identischen Coordinator am Editor, den ursprünglichen Editor der Registrierung sowie Dokumentgleichheit. Das Objekt ist einmalig verwendbar und wird **vor RunUpdate** verbraucht. Es kann sich weder zurücksetzen noch ein Nachfolgerticket übernehmen.

Der Helfer behält sämtliche bisherigen Guards und fordert zusätzlich diese Capability. Ein Aufruf ohne fünften Parameter verwendet den Default null und wird vor jeder nativen Wirkung mit ExecutionBusyException abgewiesen. Dieser Default erhält die bisherigen negativen Testaufrufe, vermittelt aber keine Kompatibilitätsberechtigung.

Keine AsyncLocal-Capability, kein globaler Current-Owner, keine allgemein aktivierbare Flagge und kein neues Besitzmodell. Die verschachtelte Factory kann kein bereits aktives Ticket aufwerten: Auch ihr direkter Aufruf muss durch TryBegin und Q; aus einem aktiven oder kontrollierten Observerkontext erreicht sie keinen Actiondelegate. Das bestehende `historyCommand`-Argument von RunGui bleibt ausschließlich Wirkungsmetadatum und erzeugt keine Capability.

## 3. Originale RR-01-Red-/Green-Gegenproben

Die beiden Tests wurden **vor jeder Produktkorrektur** hinzugefügt und gegen den vorhandenen F1-Stand ausgeführt. Ergebnis: **2 Failed, 0 Passed**. Anschließend wurden diese Testdatei und alle Assertions unverändert gelassen.

| Probe | Rot vor Korrektur | Grün nach Korrektur |
|---|---|---|
| A: allgemeines RunGui + IdentityLock + Helfer ohne Historynavigation | Fremder Text `FOREIGN`, keine Ablehnung, unerlaubte Text-/Historywirkung | ExecutionBusy vor nativer Wirkung; Text `abcd`, CanUndo=false, Historyindex unverändert; None/None; Pending=0; nachfolgende echte Historynavigation erfolgreich |
| B: fremder UpdateStarted-Callback ruft Helfer auf | `STOLEN` wird eingesetzt; die ursprüngliche Freigabe scheitert danach | Callback fängt ExecutionBusy; ausschließlich der ursprüngliche Auftrag schreibt `intended`; Complete; Historyindex unverändert; genau ein Undo stellt `abcd` her, danach CanUndo=false |

Originalbelege: [red.trx](../../artifacts/ap3-02-f2/red.trx), [red.log](../../artifacts/ap3-02-f2/red.log), [Rotfassung der Tests](../../artifacts/ap3-02-f2/HistoryCapabilityTests.red.cs), [Rot-Hash](../../artifacts/ap3-02-f2/red-tests.xml). Grün: [targeted.trx](../../artifacts/ap3-02-f2/targeted.trx) und [gui-final.trx](../../artifacts/ap3-02-f2/gui-final.trx).

## 4. Weshalb Active und IdentityLock keine fremde Historywirkung mehr erlauben

RunGui ruft weiterhin das allgemeine TryBegin/Commit auf und erhält keine Capability. Das neue RunHistory darf während dieses Tickets nicht beginnen. Der Helfer lehnt null ab, bevor RunUpdate oder eine einmalige native Schreibfreigabe erreicht werden. Auch die Kombination Active + IdentityLock + ObserverDepth=0 + gleiches Dokument genügt daher nicht mehr.

Ein aufbewahrtes Capabilityobjekt scheitert nach Aktionende an seiner permanenten Invalidierung und zusätzlich am abweichenden aktiven Ticket. Das gilt auch für eine zuvor **unbenutzte** Capability aus einer erfolgreichen No-op-Aktion. Bei regulärer Historyersetzung ist sie schon vor UpdateStarted verbraucht. Native Callbackketten können sie deshalb weder vor Remove noch nach Remove noch während Insert erneut verwenden; sie können auch keine zweite Historyaktion beginnen.

Der einzig reguläre Produktaufruf des Helfers bleibt VM.NavigateHistory. Dessen Indexfortsetzung bleibt innerhalb der ursprünglichen angenommenen Aktion. Der VM-Delegate erhält das Capabilityobjekt direkt; native Observer bekommen es nicht publiziert. Die adversarialen Tests geben ihnen absichtlich sogar eine gespeicherte Referenz und prüfen die Zurückweisung nach Verbrauch.

## 5. Reentrancy, D01, Undo und Lifecycle

Elf zusätzliche Fälle in HistoryCapabilityAdversarialTests:

| Gruppe | Fälle / Ergebnis |
|---|---|
| Abgelaufene Capability | 2: zuvor benutzt/unbenutzt; unter neuem RunGui-Ticket abgewiesen, Textversion unverändert, None/None; verschachteltes RunHistory ebenfalls abgewiesen |
| Alle nativen Grenzen | 1: UpdateStarted, Changing/Changed für Remove und Insert, UpdateFinished; sechs Wiederverwendungen und sechs neue Historyeintritte abgewiesen, ursprüngliches Ziel `WXYZ`, Complete; zweiter Helferaufruf nach der Gruppe ebenfalls abgewiesen |
| Exception/Faulted | 3: vor Verwendung, nach Remove, nach Insert; Capability unbrauchbar, tatsächlicher Text jeweils `abcd`, leer, `WXYZ`; Indeterminate/Indeterminate/Faulted, Pending=0, Active=false |
| Q-Reservation gescheitert | 1: TryBegin kann erfolgen, Q ist belegt; kein Capabilitydelegate erreicht, Ticket endet, Text/Undostack unverändert; nach Q-Freigabe echte Navigation erfolgreich |
| Originaleditor/-dokument | 1: Consume mit fremdem Editor bzw. fremdem Dokument abgewiesen; originale Operation bleibt ausführbar |
| Detach/Dispose im Originalcallback | 2: während Changed bleiben Q, Active, Pending=1 und native Updategruppe belegt; am Ende `WXYZ`, Pending=0, Q frei, Complete |
| Entkommene Callbackablehnung | 1: fremder UpdateStarted-Aufruf ohne Capability wirft aus dem Callback heraus; Originaltext und Textversion unverändert, Faulted/Indeterminate, weitere Admission gesperrt |

**Wichtige Fehlerunterscheidung:** Im letzten Fall hat das äußere BeginUpdate bereits die native TextArea-/Historyzustellung begonnen. Es bleiben CanUndo=true und IsInUpdate=true. Das ist keine bewiesene Wirkungslosigkeit der gesamten äußeren Operation, obwohl der Historyhelfer selbst nichts schreibt. Es erfolgt keine Reparatur-EndUpdate-Schleife und kein Retry. Die bestehende konservative Finalisierung bleibt erhalten.

Der erste Gesamtlauf meldete deshalb 552 Passed / 1 Failed: Die neu hinzugefügte zusätzliche Callbackfehlerprobe hatte fälschlich CanUndo=false erwartet. Ausschließlich diese neue Probe wurde auf den beobachteten nativen Fehlervertrag präzisiert (CanUndo=true, offene Gruppe, unveränderte Textversion und Faulted). Der fehlgeschlagene Lauf bleibt in [gui.trx](../../artifacts/ap3-02-f2/gui.trx) erhalten. Weder vorhandene Tests noch die beiden verbindlichen RR-01-Assertions wurden geändert.

IR-01-Cleanup-/Konstruktorbehandlung bleibt unverändert. IR-02 bleibt Remove gefolgt von Insert unter einer nativen Gruppe, mit derselben einmaligen Schreibfreigabe je Teilschritt. No-op überspringt weiterhin den Helfer und erzeugt keine Gruppe. Keine neue Undoengine und keine Änderung an D01 oder S-01.

## 6. Regressionen und originale Testbelege

| Prüfung | Tatsächliches Ergebnis | Beleg |
|---|---|---|
| Debug-Build beider Lösungen über unverändertes `eng/prism/Build.ps1 -Configuration Debug` | beide erfolgreich, 0 Fehler; Avalonia-Lösung 1 bestehende CS8619-Warnung in Avalonia12DispatcherTests.cs:68, Root-Lösung 0 Warnungen | [build-final.log](../../artifacts/ap3-02-f2/build-final.log) |
| Vollständige GUI-Suite | **553 Passed, 0 Failed, 0 Skipped** | [gui-final.trx](../../artifacts/ap3-02-f2/gui-final.trx), [Log](../../artifacts/ap3-02-f2/gui-final.log) |
| Separater Klassenlauf | **98 Passed**: 2 RR-01 + 11 adversarial + 27 HistoryCorrection + 23 HistoryCoordinator + 35 EditorCoordinator | [targeted.trx](../../artifacts/ap3-02-f2/targeted.trx), [Log](../../artifacts/ap3-02-f2/targeted.log) |
| Unveränderte unabhängige Semantikmatrix | **270 Fälle, 0 Abweichungen**, originales Anchorgegenbeispiel Offset 4 | [independent-probe.log](../../artifacts/ap3-02-f2/independent-probe.log) |

Die 98 Klassenfälle sind **Teilmengen der 553**, nicht zusätzlich zu addieren. TRX als XML geprüft: 553 Passed-Ergebnisse, 553 verschiedene ExecutionIds, 544 TestDefinitions; alle TestIds aufgelöst. Die bekannte Differenz durch mehrfach ausgeführte OpenInfoLink-Testdefinitionen bleibt bestehen. Bestehende F1-/Nachreviewbelege wurden nicht überschrieben.

Für die 270 Vergleiche wurde das verfügbare [unveränderte unabhängige Prüfprogramm](../../artifacts/ap3-02-f1-review/probe/Program.cs) erneut gegen die frisch gebauten Assemblies ausgeführt. Seine Matrix erreicht den produktiven Historypfad über die VM und vergleicht weiterhin mit der ungruppierten Remove-/Insert-Referenz. Sie prüft dieselben Texte, UTF-16-Offsets, MovementTypes, SurviveDeletion, Caret/Selection, Changing/Changed sowie separate Undo-/Redo-Rückkehr. Die ebenfalls erneut ausgeführten direkten Document.Insert-Angriffe wurden an allen sechs Callbackarten abgewiesen.

**Grenze des alten Programms:** Seine späteren `foreignTicket`-/`foreignCallback`-Abschnitte rufen den Helfer per Reflection mit vier Argumenten auf. Die neue optionale fünfte Signatur wird von MethodInfo.Invoke nicht automatisch ergänzt; diese Abschnitte erreichen nun einen Parameteranzahlfehler. Sie gelten ausdrücklich **nicht** als Capability-Grünnachweis. Diesen liefern die neu kompilierten, unveränderten permanenten RR-01-Tests mit tatsächlichem null-Capabilityaufruf. Das alte Prüfprogramm wurde nicht angepasst, um ein besseres Ergebnis zu erzeugen.

## 7. Verbleibende Findings und native Gates

RR-01 ist durch die beschriebenen Tests korrigiert; ein unabhängiger Nachreview muss insbesondere private Erzeugung, aktives Originalticket, einmaligen Verbrauch vor nativen Callbacks und Invalidierung im finally bestätigen. Kein weiterer bestätigter neuer Befund in diesem begrenzten Auftrag.

Native Windows-Historybuttons, wiederholte Back-/Forwardnavigation mit anschließendem Ctrl+Z/Y und physische Schweizer AltGr-/Dead-Keyabnahme bleiben **OPEN**. In F2 wurde kein neuer nativer Abnahmelauf behauptet. Der begrenzte Windows-Nachweis aus dem F1-Nachreview wird nicht ausgeweitet. Frühe IME-Deaktivierung und akzeptiertes CJK-Restrisiko bleiben unverändert. Allgemeine AP3-Observer-/Datei-/Leasearbeiten sowie frühere offene Race-/Multicastfragen sind nicht Gegenstand dieser Korrektur.

## 8. Schlussbewertung und Stop-Gate

**READY FOR INDEPENDENT RE-REVIEW.** Die operationsgebundene Capability ergänzt die bestehende native Mechanik. Beide originalen RR-01-Gegenproben zuerst rot und anschließend unverändert grün; alle bisherigen 540 GUI-Fälle plus 13 neue Fälle bestanden; 270 unabhängige Semantikvergleiche erneut ohne Abweichung.

Am Stop-Gate angehalten. Keine automatische Fortsetzung mit AP3-03, keine weiteren produktiven Pakete, kein Commit, Push oder Merge.
