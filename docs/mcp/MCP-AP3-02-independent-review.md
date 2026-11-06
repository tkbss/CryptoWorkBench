# MCP AP3-02 – Adversarialer Review

Stand: 29.10.2026. **CHANGES REQUIRED.** Zwei reproduzierte Befunde verhindern die unveränderte Abnahme: eine nach Disposefehler zurückbleibende exklusive Historybindung und eine nicht erhaltene TextAnchor-Semantik. Die unveränderte GUI-Suite besteht trotzdem vollständig.

## 1. Reviewbasis und Unabhängigkeitsgrenze

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, Branch `feature/mcp-ap3`, HEAD `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Tatsächlicher Anfangsstatus:

- Modifiziert: `CryptoWorkBenchAvalonia/Services/HistoryService.cs`, `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`.
- Unversioniert: `CryptoWorkBenchGuiTest/HistoryCoordinatorTests.cs`, `docs/mcp/MCP-AP3-01-integration-assessment.md`, `docs/mcp/MCP-AP3-02-history-implementation-review.md`.
- Index leer. Das normale Git-Diff enthält nur die beiden Produktionsdateien; neue Tests und Berichte wurden ausdrücklich separat gelesen.

Grundlage: [AP3-01](MCP-AP3-01-integration-assessment.md), [Implementierungsbericht](MCP-AP3-02-history-implementation-review.md), [R2](MCP-00-R2-consolidated-architecture-decision.md), [AP0](MCP-00-AP0-contract-finalization.md), [F1-Nachreview](MCP-00-AP2.1-F1-independent-rereview.md). Alte Versions-/Migrationsaussagen werden nicht gegen die abgeschlossene M3-Basis ausgespielt.

Methodisch wurden Claims gegen den Quellcode, echte TRX-Ergebnisse und zusätzliche Gegenproben geprüft. Organisatorisch erfolgte dieser Review im selben Chat wie die Implementierung; es handelt sich nicht um die Begutachtung durch eine andere Person oder einen separat eingesetzten Reviewer. Die neuen Gegenproben sind unabhängig von den 23 implementierungsbegleitenden Assertions.

Produkt- und permanente Testdateien wurden nicht geändert. Die fünf oben bezeichneten Dateien wurden vor Reviewbeginn per SHA-256 erfasst und am Ende erneut verglichen: **null Änderungen**. Weitere vorhandene Dateien wurden nicht editiert. Keine Änderungen an anderen Worktrees, kein Zugriff auf `.codex/config.toml`, kein Commit/Push/Merge/Staging. Neu dauerhaft angelegt ist nur dieser nicht ignorierte Bericht; Testlogs/Manifest sind ignorierte Reviewevidenz.

## 2. R1 – Admission

Die produktiven MainViewModel-Commands delegieren LineBack/Forward an HistoryService. Beide Methoden führen auch bei direktem Aufruf über NavigateLine und den exklusiven VM-Delegate. Erst innerhalb von C.Commit wird die einmalige Indexfortsetzung aufgerufen. Vorher erfolgen IsExecuting-/Registrationprüfung, C.TryBegin und Q.Reserve. Keine doppelte Admission gefunden.

Phase, Active, ObserverDepth, Generation, Revision und Originalidentität werden im bestehenden Coordinator geprüft. Fehlende Bindung oder Registrierung verhindert Indexfortschritt. Native Update-/Identity-/Mutationlocks werden vor Commit erneut geprüft. Reentrante Historynavigation wird vor Indexänderung abgelehnt; Q verhindert weitere Service-/Stateannahme.

`using var ticket` schließt auch den frühen Rückgabepfad nach gescheitertem Q.Reserve. Ein tatsächliches Rennen zwischen IsExecuting und Reserve ist durch den Code abgesichert, aber von der Busy-Testvariante nicht dynamisch bewiesen: Diese hält Q bereits vor dem Aufruf, sodass gewöhnlich schon IsExecuting abweist. Kein bestätigter Ticketleckbefund; spezifischer Race-/Pendingnachweis bleibt OPEN.

## 3. R2 – Bindung und Lifecycle

Normalpfad positiv: ein interner Delegate pro konkretem HistoryService; zweite Bindung wirft, Dispose entfernt nur den eigenen Delegate. Die VM bindet ihre eigene Automation-Serviceinstanz. IdentityLock verhindert Dokumenttausch; Detach während der Mutation wird aufgeschoben und bleibt originalgebunden. Der permanente Detachfall besteht erneut.

**Nicht fehlerfest:** Dispose löst die neue exklusive Bindung erst nach DetachEditor, Lifetime.Cancel und PasswordHost.Dispose. Ein Fehler davor überspringt die Abmeldung und blockiert danach die neue VM-Konstruktion. Bestätigt als IR-01.

Auch die Bindung im Konstruktor erfolgt vor Abschluss weiterer Initialisierung. Ein Fehler danach könnte die Bindung behalten; mangels zusätzlicher realistischer Produktionsreproduktion wird dieser verwandte Fall hier nur als OPEN geführt, nicht als zweites bestätigtes Finding.

## 4. R3 – D01

Positiv: kein `historyCommand: true`; Indexfortschritt wird nicht unmittelbar als Undo-Wirkung ausgegeben. Bei identischem Zieltext werden RunUpdate, Replace und Dirtysetzung übersprungen; erfolgreiche No-op-Navigation liefert None/None/Complete. Ohne Editorbindung gibt es weder Indexbewegung noch eine angenommene Dokumentmutation.

Normale Ersetzung liefert Applied/Applied/Complete; kontrollierte Historyobserverfehler ergänzen CallbackFailed ohne den angewandten Effekt zu verschweigen. Entkommene native Exceptions werden konservativ Indeterminate/Indeterminate/Faulted behandelt. Das ist keine falsche None-/Busy-Antwort. Die Originalrevision stammt weiterhin von der Bridge; es wird keine Nachfolgerrevision geschätzt. Indexrollback und automatische Retries wurden nicht gefunden.

Testgrenze: Die Fehlerfälle prüfen überwiegend Finalization/HistoryEffect/Error und Sperre, nicht durchgängig TextEffect, Original-/ObservedRevision und reale Stackfolgen. Ein vollständiger Fehlervertragsnachweis folgt daher nicht allein aus 23 PASS. Beim Originalidentitätskern besteht zusätzliche unveränderte AP2.1-/F1-Evidenz; diese ersetzt keine History-spezifische Vollmatrix.

## 5. R4 – Native Dokumentsemantik

Die reale AvaloniaEdit-12.0.0-Assembly wurde in einer isolierten Probe verwendet. Ein Replace innerhalb RunUpdate erzeugt in den permanenten Positivfällen einen Undo-Schritt; Undo/Redo und mehrere Historyaktionen bestehen. Identischer Text und leere No-ops erzeugen keine zusätzliche Gruppe.

Die neue einzelne Replace-Operation hat jedoch **nicht dieselbe Anchorabbildung** wie die alte Remove-/Insert-Folge. IR-02 ist ein konkreter Unterschied am Anfang der ersetzten letzten Zeile. Die Behauptung, die native Implementierung beizubehalten, genügt nicht als Beweis der unveränderten Hostsemantik.

Geprüft sind leere Dokumente, leere letzte Zeile, mehrzeiliger Zieltext und Unicode in den permanenten Fällen. Nicht vollständig belegt: sämtliche Caret-/Selectionvarianten, CRLF-/CR-Einzelfälle, AnchorMovementType-Kombinationen, gelöschte statt überlebende Anchors. Diese bleiben OPEN, nicht zusätzliche reproduzierte Fehler. Die jetzt zusammengefasste Changed-Ereignisfolge ist eine bewusste Verhaltensänderung gegenüber zwei Dokumentänderungen; ihre Auswirkungen auf weitere Hostsubscriber müssen beim Korrekturreview berücksichtigt werden.

## 6. R5 – Observer und Finalisierung

Die Historypublikation erfolgt unter Active und Observe; weitere LineBack/Forward-Commands und Servicewrites sind in den permanenten Proben gesperrt. IdentityLock erlaubt genau die ursprüngliche Dokumentänderung. Kontrollierte Historyfehler bleiben im Receipt; die umgebenden using-/Coordinator-finally-Pfade schließen trotz dieser Fehler ab.

Observe umschließt die gesamte Multicastpublikation. Ein werfender Subscriber verhindert dadurch spätere Subscriber derselben Invocation List. Das folgt direkt aus `PublishLine` und C.Observe; es ist kein Nachweis, dass heute ein weiterer notwendiger produktiver Cleanupsubscriber existiert. Die frühere schreibende VM-Subscription wurde entfernt. Die Bedeutung übersprungener externer Publikationssubscriber bleibt deshalb OPEN; nicht pauschal als gefixte Multicast-Fehlerisolierung ausgeben.

Die explizite Dirtysetzung steht unter Observe; automatische IsModified-Callbacks können aber schon während der nativen Dokumentoperation auftreten. Die separate Propertyfehlerprobe deckt diesen früheren Pfad ab und führt konservativ zu Faulted. Registrierung/Entfernung von Subscribern während Dispatch und direkte Dokumentmutation aus Historyobservern sind durch die vorhandenen 23 Fälle nicht vollständig nachgewiesen. Allgemeine P6-Observerarbeit wurde nicht vorgezogen.

## 7. Bestätigte Findings

### IR-01 – High: Fehlgeschlagenes Dispose hält die exklusive Historybindung fest

**Position:** `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:339–346`, besonders Bindungsfreigabe Zeile 344; `Services/HistoryService.cs:17–21`.

**Reproduktion gegen aktuelle Produktassembly:** VM mit echtem WorkbenchTextEditor registrieren. Einen VM.PropertyChanged-Subscriber installieren, der beim Namen `TextEditor` wirft. VM.Dispose aufrufen. DetachEditor entfernt die Registration und setzt TextEditor=null; SetProperty publiziert und wirft. Danach mit demselben HistoryService eine reguläre neue VM konstruieren.

**Erwartet:** Alle eigenen Abmeldungen werden trotz vorheriger Cleanupfehler durchgeführt; die neue VM kann nach abgeschlossener Ablösung die History übernehmen. Der ursprüngliche Fehler darf weiterhin gemeldet werden.

**Tatsächlich, isolierte Probe:**

```text
dispose threw=Exception
successor bound=false; Line history already has an editor.
```

**Wirkung:** Der alte Delegate hält die VM referenziert und blockiert die neue Editorbindung. Die alte Registration ist bereits entfernt; es handelt sich um eine neue Verfügbarkeits-/Lifecyclefolge der exklusiven AP3-02-Bindung. Kein nachgewiesener falscher Statewrite oder Textverlust. Wiederholtes manuelles Dispose nach Entfernung des fehlernden Subscribers kann aufräumen, ist aber kein gesicherter regulärer Abschluss.

**Korrektur erforderlich:** Eigene Bindungs- und Outputabmeldungen in garantierten Cleanupabschnitten ausführen, Fehler sammeln/weiterreichen ohne spätere Pflichtabschlüsse zu überspringen; partielle Initialisierung ebenfalls berücksichtigen. Keine pauschale neue Shutdownarchitektur nötig.

**Regression:** Neue permanente Probe mit fehlerndem TextEditor-Propertysubscriber beim Dispose, nachfolgender VM-Konstruktion, wiederholtem Dispose und unveränderter Nachfolgerbindung. Der vorhandene `FailuresAndLifecycleRemainBoundToOriginal("detach")` wirft nicht beim VM-Dispose und findet diesen Fehler nicht.

### IR-02 – Medium: Replace verändert die bisherige TextAnchor-Semantik

**Position:** `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:150–154`, native Replace-Zeile 153; vorher separate Remove/Insert-Zeilen 133/135 im HEAD-Stand.

**Reproduktion:** `TextDocument("abcd")`; Anchor bei Offset 0 mit `SurviveDeletion=true`, Standard-MovementType. Referenzpfad `Remove(0,4); Insert(0,"WXYZ")`; neuer Pfad `Replace(0,4,"WXYZ")`. Dies entspricht der Ersetzung der einzigen/letzten Zeile durch die Historyaktion. Beide Pfade liefern denselben Text.

**Erwartet gemäß AP3-02:** Vorhandene Anchorsemantik bewahren oder konkrete Abweichung vor Abnahme ausdrücklich entscheiden und absichern.

**Tatsächlich, integrierte 12.0.0-Assembly:**

```text
anchor 0: removeInsert=4; replace=0
anchor 1: removeInsert=4; replace=4
anchor 2: removeInsert=4; replace=4
anchor 3: removeInsert=4; replace=4
anchor 4: removeInsert=4; replace=4
```

**Wirkung:** Ein überlebender Anchor am Ersetzungsanfang liegt nach dem neuen Historycommand vor statt hinter dem eingefügten Text. Textgleichheit und erfolgreiche Undo-Schritte erkennen das nicht. Ein konkreter Fehloutput der Returnpipeline wurde nicht nachgewiesen und wird nicht behauptet.

**Korrektur erforderlich:** Native Offset-/Anchormappingsemantik des alten Hostpfads unter einer Undo-Gruppe bewahren oder die geänderte Semantik explizit genehmigen lassen; danach echte Anchor-/Caret-/Selectionregressionen. Keine neue Undoengine.

**Regression:** `CommandsKeepOneUndoGroupAndSeparateNoOp` um Original-/Zielanfang/-ende, beide Anchorbewegungsrichtungen, Überleben/Löschen, Caret-/Selectionfälle ergänzen. Die bestehenden sechs Varianten prüfen keine Anchors.

## 8. R6 – Tests, Proben und Windows-Grenze

Erneut ausgeführt ohne Build/Restore oder Assertionsänderung:

```powershell
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --logger 'trx;LogFileName=gui-review.trx' --results-directory artifacts/ap3-02-review
```

**513 PASS / 0 FAIL / 0 SKIP**. Die TRX enthält 513 Passed-UnitTestResult-Einträge; über TestDefinitions sind 23 HistoryCoordinator-Fälle identifiziert. Die 35 bestehenden Coordinator-/F1-Fälle sind in derselben Suite enthalten, nicht zusätzlich addiert. Damit stimmen die gemeldeten finalen Implementierungsergebnisse mit der Wiederholung überein. [TRX](../../artifacts/ap3-02-review/gui-review.trx), [Runnerlog](../../artifacts/ap3-02-review/gui.log).

Zusätzliche Proben wurden als temporäres Konsolenprogramm unter `%TEMP%/cwb-ap302-independent-probe` gegen die vorhandenen GUI-Test-/Produktassemblies kompiliert; keine permanente Testdatei oder Produktassembly ersetzt. Die vorhandene Headless-Session wurde per Reflection erreicht, ebenso das interne AttachEditor; keine Guards/Felder wurden manipuliert. IR-01 nutzt einen normalen Propertysubscriber. IR-02 ruft öffentliche TextDocument-/Anchor-APIs auf. [Originalausgabe](../../artifacts/ap3-02-review/probe-native.log).

Der erste isolierte Start scheiterte vor der eigentlichen Probe am fehlenden nativen HarfBuzz-Suchpfad; [Fehlerlog](../../artifacts/ap3-02-review/probe.log) erhalten. Mit dem vorhandenen `runtimes/win-x64/native` im prozesslokalen PATH lief die Probe vollständig. Dieser Infrastrukturfehler ist kein Produktfinding. Der Name `probe-native.log` bezeichnet die nachgeladene native Abhängigkeit, **keinen Windows-GUI-/Tastaturtest**.

**Windows-Gate OPEN:** Keine reale MainView-Buttonbedienung, physische Ctrl+Z/Y-Folge, Fokus-/Schweizer-Layout-/Unicodeabnahme durchgeführt. Die verfügbaren Nachweise sind Headless-/Assemblyproben. Keine Aussage, dass native Automatisierung grundsätzlich unmöglich sei. Vor Abnahme echte Windowsfolgen separat protokollieren.

Core-/MCP-Tests nicht erneut ausgeführt; keine dortigen Änderungen oder neuer fachlicher Befund. Kein neuer Build des Produktbaums. SHA-256-Abgleich der fünf Reviewdateien: null Änderungen; `git diff --check` ohne Befund. Vorhermanifest: `artifacts/ap3-02-review/before.xml`.

## 9. Empfehlung und verbleibende Grenzen

**CHANGES REQUIRED** wegen IR-01 und IR-02. Die erfolgreichen 513 Tests widerlegen diese zusätzlichen Gegenbeispiele nicht. Lifecyclefehlerfestigkeit und native Anchorsemantik sind vor Abnahme zu korrigieren bzw. ausdrücklich zu entscheiden; neue permanente Regressionen und erneuter unabhängiger Korrekturreview erforderlich.

Keine Neubewertung historischer R2/R4/R6/R7-/H3-/IME-/EC21-Findings. Das akzeptierte CJK-Restrisiko und die dauerhafte IME-Deaktivierung bleiben unverändert. Offene Q-Race-/Multicast-/Caret-/Selection-/Windowsnachweise sind von bestätigten Fehlern getrennt ausgewiesen.

Review beendet. Ausschließlich dieser Bericht angelegt, keine automatischen Fixes, kein Commit/Push/Merge und keine AP3-03-Fortsetzung.
