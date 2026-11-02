# MCP AP3-02-F2 – Unabhängiger adversarialer Nachreview

Stand: 29.10.2026. **READY FOR ACCEPTANCE für die begrenzte RR-01-Korrektur.** Die beiden ursprünglichen Umgehungen sind mit korrekt kompilierten Aufrufen unabhängig abgewiesen. Kein neuer bestätigter Blocker im aktuellen produktiven Historypfad. Dies ist keine vollständige AP3-/Hostfreigabe; native History-/physische Tastaturabnahme bleibt OPEN. Die zusätzliche interne API-Erreichbarkeit und ein synthetisch reproduzierter Rekursionsfall werden unten ausdrücklich eingegrenzt.

## 1. Auftrag, Ausgangsstand und Erhaltung

Die zunächst abgeschnittene Auftragsdatei wurde durch den nachgereichten vollständigen Auftrag ersetzt. Der abschließende Abgleich deckt R1–R5, die native Bediengrenze, alle geforderten Berichtsinhalte und das Stop-Gate ab. Die bereits vorliegenden Läufe enthalten sämtliche verlangten Klassenfälle; eine erneute Testausführung war für die redaktionelle Ergänzung nicht erforderlich.

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, Branch `feature/mcp-ap3`, HEAD `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Index leer. Bei Beginn modifiziert: WorkbenchTextEditor.cs, EditorCoordinator.cs, HistoryService.cs, CryptoScriptEditViewModel.cs. Unversioniert: die vier History-Testdateien HistoryCapabilityTests, HistoryCapabilityAdversarialTests, HistoryCoordinatorTests und HistoryCorrectionTests sowie sechs AP3-Berichte (AP3-01, AP3-02-Implementierung/-Review, F1-Korrektur/-Nachreview und F2-Korrektur).

Der vollständige Anfangsstatus liegt in [status-before.txt](../../artifacts/ap3-02-f2-review/status-before.txt), das Tracking-Diff in [before.patch](../../artifacts/ap3-02-f2-review/before.patch). **Vor Tests** erstelltes SHA-256-Erhaltungsmanifest für 112 relevante Produkt-, Test- und Bestandsberichtdateien: [before.xml](../../artifacts/ap3-02-f2-review/before.xml). Abschluss: **112 geprüft, 0 Änderungen**. Auch die vorab gehashten F2-Altbelege sind unverändert. [verification.json](../../artifacts/ap3-02-f2-review/verification.json) enthält die maschinenlesbaren Ergebnisse.

Quellbasis: [F2-Korrekturbericht](MCP-AP3-02-F2-correction-review.md), [F1-Nachreview](MCP-AP3-02-F1-independent-rereview.md), [F1-Korrektur](MCP-AP3-02-F1-correction-review.md), [ursprünglicher Review](MCP-AP3-02-independent-review.md), [R2](MCP-00-R2-consolidated-architecture-decision.md), [AP0/D01](MCP-00-AP0-contract-finalization.md). Avalonia 12.1.4, AvaloniaEdit 12.0.0 und die genehmigte Prismquelle bleiben unverändert.

Methodisch eigenständige Proben und Quellabgleich; organisatorisch derselbe Chat wie die Implementierung, kein anderer menschlicher Reviewer. Keine Produkt-/permanenten Teständerungen. Neu sind ausschließlich dieser Bericht und ignorierte Reviewevidenz. Keine anderen Worktrees verändert, kein Zugriff auf `.codex/config.toml`, kein Commit/Push/Merge/Staging.

## 2. R1 – Erzeugung, Identität und Verbrauch

Geprüfte Grenzen: `EditorCoordinator.cs:148–187`, `WorkbenchTextEditor.cs:108–135`, `CryptoScriptEditViewModel.cs:140–160`, `HistoryService.cs:32–45`.

| Anforderung | Tatsächlicher Nachweis |
|---|---|
| Ursprüngliche Admission | RunHistory benutzt TryBegin und das unveränderte Commit. Kein Aufwerten des bereits aktiven RunGui-Tickets. CanBegin sperrt Active, ObserverDepth, unpassende Phase/Registration und native Locks/Updates. |
| Q vor Erzeugung | Reserve folgt auf TryBegin. Bei Busy wird das Ticket durch using freigegeben; der Delegate wird nicht erreicht und kein Capabilityobjekt erzeugt. Unabhängig mit belegtem Q geprüft. |
| Konkretes Ticket | Privater Capabilitykonstruktor, readonly OperationTicket; Consume verlangt Referenzgleichheit mit owner.active. |
| Coordinator/Editor/Registrierung/Id/Dokument | editor.Coordinator muss identisch sein; Registration.Editor muss der Editor sein; beide Dokumentreferenzen müssen stimmen. IsCurrent prüft Ticketmenge, Originalregistrierung/EditorId, Originaldokument, Generation, Phase und Originalrevision. |
| Einmaliger Verbrauch | Consume setzt consumed=true vor historyStepsActive/RunUpdate. In allen sechs nativen Callbackgrenzen scheitert auch ein direkter erneuter Consume-Aufruf. |
| Ende der Gültigkeit | Das finally um den synchronen Actiondelegate setzt valid=false, auch bei unbenutztem No-op und Exception. Zusätzlich verhindern anderes Active-Ticket und BodyDone eine Wiederverwendung. |
| Kein ambient Recht | Kein AsyncLocal, kein globaler Current-Owner, keine öffentliche Aktivierungsflagge. Die Capability wird als explizites Argument übergeben. |

Die aktuelle produktive Kette ist MainViewModel → HistoryService.NavigateLine → exklusiver VM-Delegate → RunHistory → IdentityLock → einmalige Indexfortsetzung → gegebenenfalls Helper mit Capability → kontrollierte Dirty-/Historypublikation. Zwischen Capabilityübergabe und ihrem Verbrauch besitzt diese VM-Aktion keinen externen Callback: LockIdentity setzt lokale Felder, advanceIndex weist lediglich den privaten Index zu, anschließend werden Zeile/Text gelesen. Die erste native Veröffentlichung erfolgt erst im bereits verbrauchten Helfer. Bei No-op bleibt die Capability unbenutzt; die Publikation läuft unter Observe und danach wird sie invalidiert.

Keine Capability wird an LineHistoryChanged, native Dokumentevents oder PropertyChanged weitergegeben. Die unabhängigen Proben geben den nativen Angreifer-Callbacks absichtlich sogar die originale Capabilityreferenz; auch damit gelingt kein weiterer Verbrauch. Ein vom echten LineHistoryChanged aus versuchtes RunHistory wird ebenfalls abgelehnt.

## 3. Zusätzlicher Schwerpunkt: direkter interner RunHistory-Eingang

**Tatsächlich erreichbar:** Ein vertrauenswürdiger interner Aufrufer kann bei freiem Editor und freiem Q RunHistory unmittelbar verwenden, ohne vorher HistoryService.NavigateLine aufzurufen. Es existiert keine kryptographische oder typbasierte Bindung an den privaten VM-Delegate und keine Prüfung, dass der Bereich die letzte Zeile ist.

Korrekt kompilierte Probe:

```csharp
coordinator.RunHistory(cap => {
    using var identity = editor.LockIdentity();
    editor.ReplaceHistoryLine(cap.Document, 1, 1, "ARBITRARY", cap);
});
```

Ergebnis: `abcd` → **`aARBITRARYcd`**, Revision 0 → 2, Applied/Applied/Complete, Historyindex unverändert, Q/Ticket/Locks am Ende frei. Das ist ein dynamischer Nachweis der internen API-Erreichbarkeit, keine bloße Vermutung.

**Einordnung:** Dieser Aufruf nimmt eine neue Operation über den ausdrücklich geschaffenen Historyeingang einschließlich Q und Originalticket an. Er umgeht keine laufende Operation, kein fremdes Ticket und keinen kontrollierten Observer. Bereits das allgemeine RunGui ist ein vertrauenswürdiger interner Mutationszugang; der Hostvertrag schützt nicht vor beliebigem absichtlich schreibendem Produktcode außerhalb seiner vorgesehenen Aufrufketten. Die Suche im aktuellen Produktionscode findet als einzigen RunHistory-/Helperaufrufer VM.NavigateHistory. Kein bestehender fremder Produktaufrufer oder Callback, der diesen Eingang missbraucht, wurde gefunden. Deshalb **kein bestätigtes neues RR-01-Hostfinding** allein aus dieser Probe.

Der Nachweis lautet folglich: Der **aktuelle integrierte Historypfad** ist an sein Originalticket gebunden; nicht: „Auch neu hinzugefügter beliebiger interner Code kann niemals Historyrechte anfordern.“ Falls eine strukturelle Exklusivität gegenüber allen künftigen internen Aufrufern verlangt wird, wäre eine enger gebundene Eintrittsschnittstelle erforderlich. Diese stärkere Garantie wird hier nicht behauptet. Neue Produktaufrufer von RunHistory bleiben reviewpflichtig.

## 4. R2 – Originale RR-01-Gegenproben und Rot-/Grünintegrität

Beide unveränderten permanenten Tests erneut bestanden. Der aktuelle Hash von HistoryCapabilityTests.cs stimmt mit `artifacts/ap3-02-f2/red-tests.xml` und damit der vor der Produktkorrektur erfassten Rotfassung überein. Die originale red.trx enthält genau beide erwarteten Failed-Testnamen; die finale F2-TRX und der aktuelle Lauf enthalten Passed. Altbelege wurden nicht überschrieben.

Zusätzlich separate direkte, neu kompilierte Aufrufe gegen die aktuelle fünfparametrige Signatur:

| Probe | Unabhängiges Ergebnis |
|---|---|
| A: RunGui + IdentityLock, Helper mit explizitem null | einmal ExecutionBusy; `abcd`, Revision 0 → 0, None/None/Complete, CanUndo=false, Index unverändert, Pending=0, Active=false |
| B: UpdateStarted einer allgemeinen Aktion, Helper mit null, Fehler lokal gefangen | einmal ExecutionBusy; nur die ursprüngliche Aktion erzeugt `abcd legitimate`, Revision 0 → 1, Applied/Applied/Complete; genau ein Undo zurück zu `abcd`, danach CanUndo=false; Index unverändert |
| B mit entkommener Callbackexception | Text `abcd`; Indeterminate/Indeterminate/Faulted, Phase Faulted, Pending=0, Active=false; IsInUpdate=true und CanUndo=true durch bereits begonnene äußere native Gruppe |

Der letzte Fall ist ausdrücklich keine vollständig wirkungslose Ablehnung der **äußeren** Operation. Der Helper schreibt keinen fremden Text; vorherige native Gruppen-/Historywirkung ist trotzdem möglich. Kein Rollback oder Reparatur-EndUpdate wurde durchgeführt.

Die unabhängige Probe liegt unter [probe/Program.cs](../../artifacts/ap3-02-f2-review/probe/Program.cs) mit [Projekt](../../artifacts/ap3-02-f2-review/probe/probe.csproj). Sie benutzt den bereits bestehenden Friend-Assembly-Namen `CryptoWorkBenchGuiTest`, um interne Methoden direkt zu kompilieren; kein Produktassembly-Attribut wurde geändert. Eigenständige HeadlessSession, keine permanenten NUnit-Tests kopiert. Maßgeblicher vollständiger Lauf: [probes-verified.log](../../artifacts/ap3-02-f2-review/probes-verified.log).

## 5. R3 – Eigenständige adversariale Ergebnisse

| Probe | Resultat |
|---|---|
| Verbrauchte Capability nach Erfolg | Consume außerhalb der Aktion abgewiesen; auch neues aktives Ticket kann keinen Replaywrite erzeugen; unveränderte Revision beim Replayversuch |
| Unbenutzte Capability nach erfolgreichem No-op | dauerhaft unbrauchbar; neues RunGui mit IdentityLock ändert nichts, None/None |
| Anderer Editor oder Dokument | Consume abgewiesen, kein Textedit |
| Invalidierte Generation | Consume abgewiesen, kein Textedit |
| Kontrollierter Observer vor Verbrauch | Observe-Kontext kann Consume/Helper nicht nutzen; kein Textedit |
| UpdateStarted, beide Changing, beide Changed, UpdateFinished | 6 direkte Consumeversuche abgewiesen; 6 neue RunHistory-Aufträge abgewiesen; 6 Helperwiederverwendungen plus zweiter Helperaufruf nach der Gruppe abgewiesen; Ziel `WXYZ`, Complete |
| Direkte Document.Insert-Versuche an denselben Grenzen | Mit begrenzter einmaliger Zustellung jeweils abgewiesen; zusätzliche Rekursionsbeobachtung siehe Abschnitt 6 |
| Exception vor Verwendung | `abcd`, Revision 0; Indeterminate/Indeterminate/Faulted; Capability ungültig |
| Exception nach Remove | leer, Revision 1; Indeterminate/Indeterminate/Faulted; Capability ungültig |
| Exception nach Insert | `WXYZ`, Revision 2; Indeterminate/Indeterminate/Faulted; Capability ungültig |
| Q belegt | Delegate nicht aufgerufen; Pending=0, None/None, `abcd`, keine Undoaktion; der fremde Q-Besitzer bleibt belegt; nach dessen Freigabe echte Navigation erfolgreich |
| Detach/Dispose während Changed | Währenddessen Pending=1, Active/IdentityLock/Q/Update=true; danach `WXYZ`, Applied/Applied/Complete, Unavailable, Pending=0, Active/IdentityLock/Q/Update=false |

In den drei Exceptionfällen vor/nach Textschritten sind am Ende Q, Ticket und IdentityLock frei, native Gruppe geschlossen und Phase Faulted. Das ist kein Erfolgssignal: weitere Admission bleibt gesperrt. Native UpdateStarted-/IsModified-Fehler können dagegen eine offene Gruppe hinterlassen, wie die unveränderten bisherigen Tests und der separate entkommene Callbackfall zeigen; konservative Finalisierung wird nicht mit garantierter nativer Reparatur verwechselt.

## 6. Zusätzliche Beobachtung: rekursive Ablehnungszustellung

Die erste eigene Probe kombinierte alle Callbackangriffe ohne Selbstbegrenzung des künstlichen Angreiferhandlers. Dabei trat ein tatsächlicher **Stack overflow** auf (Prozesscode `-1073741571`). Originalquellfassung [Program.initial.cs](../../artifacts/ap3-02-f2-review/Program.initial.cs), vollständiger Stack [probes.log](../../artifacts/ap3-02-f2-review/probes.log). Dieses Ergebnis wird nicht als erfolgreicher Gesamtlauf ausgegeben.

Isolierte, begrenzte Wiederholung benutzt nur einen synthetischen nativen Observer und den öffentlichen Dokumentwrite:

```csharp
editor.Document.UpdateFinished += (_, _) => {
    if (++depth < 16) {
        try { editor.Document.Insert(0, "BAD"); }
        catch (ExecutionBusyException) { }
    }
    depth--;
};
history.LineBack();
```

Tatsächlich 16 verschachtelte UpdateFinished-Zustellungen und 15 abgewiesene Inserts, Originalziel `WXYZ`, kein `BAD`. Ursache: `WorkbenchTextEditor.BeforeUpdate` (`Controls/WorkbenchTextEditor.cs:71–79`) ruft zum Ausgleichen der abgewiesenen Gruppe EndUpdate auf; dieses publiziert erneut UpdateFinished. Ein unbeschränkt erneut schreibender Handler rekursiert vor der Rückkehr des ExecutionBusy-Fehlers.

**Abgrenzung:** Die betreffende Guardimplementierung ist bereits im HEAD-Baselinecode vorhanden, unverändert durch F1/F2. Die Reproduktion benötigt keine History-Capability und erzeugt keinen zulässigen Fremdwrite. Im Produktionsquellbaum wurde als expliziter UpdateFinished-Subscriber InfoCompletionController gefunden; dessen Handler (`Services/InfoCompletionController.cs:202–249`) setzt `_hasPendingChange=false` vor weiterer Verarbeitung und enthält nicht den reproduzierten wiederholten Document.Insert-Aufruf. Eine entsprechende benötigte produktive Callbackkette wurde nicht nachgewiesen. Deshalb **bestätigte synthetische Reentranzbeobachtung, Produktrelevanz OPEN**, kein als neu eingeführter F2-Fehler ausgegebener Blocker. Bei späterer P6-/Observerintegration gezielt berücksichtigen; nicht als „durch F2 erledigt“ markieren.

Für die isolierten Capabilitygrenzen wurde der eigene Angreiferhandler anschließend gegen Selbstrekursion begrenzt. Die gesonderte Tiefenprobe bleibt im finalen Harness erhalten. Kein Produktguard oder bestehender Test wurde dafür geändert.

## 7. R4 – IR-01, IR-02 und Semantikmatrix

Quellabgleich mit dem F2-Vorhermanifest: HistoryService und bisherige HistoryCorrection-/HistoryCoordinator-Tests unverändert; Dispose-/Konstruktorfehlerpfad nicht durch F2 verändert. Der Helper führt weiterhin Remove und Insert in genau einem RunUpdate aus; ausschließlich Parameter und Capabilityverbrauch kamen hinzu. No-op wird weiterhin im VM vor dem Helper abgefangen.

Das verfügbare alte unabhängige Prüfprogramm wurde unverändert gegen die neu gebauten Assemblies erneut ausgeführt: [semantics.log](../../artifacts/ap3-02-f2-review/semantics.log). **270 Semantikfälle, 0 Abweichungen**, ursprünglicher Anchor wieder Offset 4. Gleiche ungruppierte Remove-/Insert-Referenz; alle UTF-16-Offsets, MovementTypes, SurviveDeletion/IsDeleted, die dokumentierten Caret-/Selectionvarianten, Unicode/CRLF/leere Texte, Changing/Changed sowie getrennte Ein-Gruppen-Undo-/Redo-Prüfung.

IR-01-Probe erneut: dieselbe Cleanup-Exceptioninstanz mit erhaltenem Callbackstack; Nachfolgerbindung funktioniert. Kombinierte Detach-/Cancellationfehler hinterlassen keine zusätzlichen Outputsubscriber. Originale Dispose-/Konstruktortests ebenfalls bestanden.

**Ungültige Altproben getrennt:** Die späteren vierparametrigen Reflectionaufrufe `foreignTicket`/`foreignCallback` dieses alten Programms scheitern am Signaturwechsel und gelten weiterhin nicht als RR-01-Grünbeleg. Ihr Output wird nicht zur Bewertung benutzt. Abschnitt 4 liefert stattdessen tatsächlich kompilierte fünfparametrige Aufrufe.

## 8. Build, Tests und TRX-Integrität

| Lauf | Ergebnis | Originalbeleg dieses Reviews |
|---|---|---|
| Debug beider Lösungen über unverändertes eng/prism/Build.ps1 | beide erfolgreich; jeweils 0 Fehler/0 Warnungen im aktuellen inkrementellen Lauf | [build.log](../../artifacts/ap3-02-f2-review/build.log) |
| Vollständige GUI-Suite | **553 Passed, 0 Failed, 0 Skipped** | [gui.trx](../../artifacts/ap3-02-f2-review/gui.trx), [Log](../../artifacts/ap3-02-f2-review/gui.log) |
| Gezielter Lauf | **98 Passed**: 13 F2, 27 HistoryCorrection, 23 HistoryCoordinator, 35 EditorCoordinator | [targeted.trx](../../artifacts/ap3-02-f2-review/targeted.trx) |
| Unabhängiger direkter Capabilityharness | alle finalen Checks abgeschlossen | [probes-verified.log](../../artifacts/ap3-02-f2-review/probes-verified.log) |
| Unabhängige alte Semantikmatrix | 270 Fälle / 0 Abweichungen | [semantics.log](../../artifacts/ap3-02-f2-review/semantics.log) |

TRX als XML geprüft: 553 Passed-Resultate, 553 verschiedene ExecutionIds, 544 TestDefinitions, keine unaufgelöste TestId. Die bekannte Differenz der OpenInfoLink-Mehrfachausführungen bleibt erhalten. Der gezielte Lauf hat 98 Resultate und 98 Definitionen. Die 98 sind Teilmengen der 553, keine zusätzlich zu addierenden Tests. Historische F2-Rot-/Grünbelege stimmen mit ihren dokumentierten Resultaten überein und bleiben hashidentisch.

Testgrenze: Permanente Callbacktests prüfen Wiederverwendung der Capability im bereits aktiven Historypfad. Sie beweisen keine Exklusivität des RunHistory-Eingangs gegenüber allen internen Aufrufern. Die unabhängige Idleprobe und die zusätzliche Rekursionsdiagnose machen diese Grenzen sichtbar, ohne bisherige Assertions abzuschwächen.

## 9. Native und sonstige offene Gates

Die aktuelle GUI bietet für LineHistory **KeyBindings, keine gefundenen Historybuttons**. Die erneute Suche nach allen vier Historycommands in Produktions-C# und AXAML findet die Bedienbindungen ausschließlich in `Views/MainWindow.axaml:22–25`: **Alt+PageUp/Alt+PageDown** für LineHistoryBack/Forward, Ctrl+PageUp/Ctrl+PageDown für InfoHistoryBack/Forward. Die DelegateCommands stehen in MainViewModel. Nicht vorhandene Buttons sind daher keine Abnahmevoraussetzung; die reale LineHistorynavigation muss über ihre vorhandenen KeyBindings geprüft werden.

Kein neuer nativer Windows-Abnahmelauf in diesem Review. Mehrere reale LineHistory-Back-/Forwardaktionen über diese KeyBindings, anschließendes Ctrl+Z/Y, Fokuswechsel mit anschließender Historynavigation sowie physische Schweizer AltGr-/Dead-Keyeingabe bleiben **OPEN**. Frühere allgemeine Ctrl+Z/Y- und Unicode-Smoke-Ergebnisse sind keine vollständige History-Abnahme. Frühe IME-Deaktivierung und akzeptiertes CJK-Restrisiko unverändert.

Weitere Grenzen: allgemeine AP3-P6-Observer-/Completionintegration; Produktrelevanz der synthetischen UpdateFinished-Rekursion; reviewpflichtige zukünftige interne RunHistory-Aufrufer. Keine neuen Lease-/HTTP-Rechte und keine Änderung an S-01/D01. Keine neuen bestätigten Hostfindings mit Severity im begrenzten F2-Pfad. Der reproduzierte synthetische Prozessabbruch aus Abschnitt 6 bleibt eine Verfügbarkeitsbeobachtung mit offener produktiver Erreichbarkeit; er wird weder verschwiegen noch ohne diese Erreichbarkeit als neuer F2-Blocker eingestuft.

## 10. Empfehlung und Stop-Gate

**READY FOR ACCEPTANCE für RR-01 im bestehenden produktiven Historypfad.** Fremde allgemeine Tickets, deren UpdateStarted-Callbacks und abgelaufene/verbrauchte Capabilities erhalten keine Historyschreibberechtigung. Originale Historynavigation, Cleanup und Anchor-/Undosemantik bleiben nachgewiesen erhalten.

Keine pauschale Garantie gegen beliebigen neuen vertrauenswürdigen internen Code; keine vollständige AP3-/Host-/Nativefreigabe. Die in Abschnitt 6 dokumentierte Beobachtung bleibt als eigenständige offene Integrationsfrage erhalten. Keine produktiven Korrekturen vorgenommen. Nach Berichtabschluss angehalten; keine automatische Fortsetzung.
