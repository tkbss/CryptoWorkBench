# MCP-01.4 – G-DESIGN: kritischer Designreview

Stand: 24.10.2026. **CHANGES REQUIRED / BLOCKED.**

Geprüft: [MCP-01.4-B2-design.md](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md), SHA-256 `FD92589558B3A32F68EFD5970A85EE5722BBD047D12367792A0F6B90C08F0FC2`. Das Dokument wurde vollständig gelesen und nicht korrigiert.

Der optionale Kernvertrag ist plausibel und durch B1 teilweise gestützt. Für eine Designabnahme fehlen jedoch eindeutige Verträge insbesondere für gekoppelte Dokumentbindungen, historische Caretaktionen, Providerpläne und die Move-Bestätigung bei Teilwirkung. Die nachfolgenden Gegenbeispiele betreffen vorgeschlagene unterstützte Pfade; sie werden nicht durch den Ausschluss beliebiger Raw-Stackmanipulation erledigt.

**Unabhängigkeitsgrenze:** Dieser Review wurde als neue kritische Prüfaufgabe durchgeführt, aber vom selben Chatassistenten, der B2 verfasst hat. Er ist kein personell unabhängiger Review durch einen anderen Reviewer, wie B2 §9.2 verlangt. Die Quellenprüfung und negativen Befunde sind nutzbar; die formale Unabhängigkeitsbedingung von G-DESIGN ist damit nicht erfüllt. Es wird weder eine unabhängige positive Abnahme noch eine Produktfreigabe behauptet.

## 1. Quellen, Methode und tatsächlicher Umfang

Produktiver Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`; Branch `feature/mcp-server`; HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Bewertet wurde der vorhandene Arbeitsstand einschließlich nicht committeter Dateien. Die Eingangsinventur umfasst 752 vorhandene Git-gelistete Dateien außerhalb ausgeschlossener Konfigurationsverzeichnisse.

| Quelle | Tatsächlich verwendeter Prüfungsumfang |
| --- | --- |
| [MCP-00](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-00-architecture.md) | Editoridentität/Revision/No-op, §§4.4/4.5, gemeinsame Admission, Cancellation/Drain, Fehlerprojektion und Transportgrenze; keine erneute allgemeine MCP-Sicherheitsabnahme |
| [Editorentscheidung](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-editor-architecture-decision.md) | Variante B, interne Delegation ohne Callbackrecht, vollständige Composites, E01–E14, Raw-Ausschlüsse und keine zweite Engine |
| [U12](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-avalonia12-feasibility.md) | Feste 12.0.0-Quelle, Paket-/TFM-/Prismbasis, unveränderte ursprüngliche Fehler und eigenständige ExecutionContext-/Plattformgrenzen |
| [B1](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B1-feasibility.md) | API, Ausnahmeabschluss, native Grenzen, Zuordnung der Tests und historischen Beobachtungsproben |
| [MCP-01.4-Review](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-review.md) | Unabhängiger Final-Re-Review und BLOCKED-Nachtrag; R2/R4/R6/R7 bleiben Produktblocker, R1/R3/R5 werden nicht neu bewertet |
| [B1-Patch](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/final-library.patch) | Tatsächliche Gate-/Core-/Undo-/Event-/Editor-/Pasteänderungen; nicht nur B1-Zusammenfassung |
| [Bibliothekstests](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/library/test/MutationContractTests/Program.cs) | Assertions und Testformen, insbesondere Replay, frühe Ablehnung, Provider, Tracker, Native-Paste, Legacy und Gruppenabschluss; vollständige Quelle aus der vorangegangenen Prüfung, kritische Abschnitte erneut gelesen |
| [Boundarylog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/boundary-final.log), [Bibliothekslog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/library-tests-final.log), [Upstreamlog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/upstream-tests-final.log), [Hostlog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/host-contracts.log) | Gespeicherte Ergebnisse und tatsächliche PASS-Zeilen geprüft; Hosttestquelle und Fixturegrenzen berücksichtigt |
| Original AvaloniaEdit | `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0`; TextEditor, TextArea, TextView, TextDocument, UndoStack, UndoOperationGroup, DocumentChangeOperation, Selectionpfade, Clipboard/Drag, Search, Completion, Indentation sowie Build-/Paketdateien |
| Avalonia-Propertysystem | Ergänzende Primärquellenprüfung am Tag 12.0.0, EffectiveValue<T>; kein eigenständiger vollständiger Avalonia-Review |
| C# / Distribution | Microsoft-Dokumentation zu using/finally, Strong Naming und NuGet-Paketidentität zur Prüfung der jeweiligen B2-Annahmen |

Die lokale Originalquelle stimmt für **362 Manifestdateien** mit der vor B1 gesicherten Basis überein. Die vorhandene NuGet-Metadatei `avalonia.avaloniaedit-12.0.0.nuspec:13` nennt Commit `86fdebec4cff7affb0e14c7885df71d28edce777`. Alle 18 in B1s Änderungsmanifest verzeichneten Enddateien stimmen mit ihren Hashes überein. Patchhash und Testquellenhash entsprechen B2.

**Evidenzklassen dieses Reviews:**

- **V:** in dieser Runde gelesener Zustand, Hashvergleich oder gezählte Logeinträge.
- **B1:** früherer Laufzeitnachweis, jetzt geprüft, nicht neu ausgeführt.
- **S:** statischer Pfad bzw. aus Quellcode nachvollziehbares Gegenbeispiel.
- **D:** vorgeschlagener Vertrag, dessen Vollständigkeit bewertet wird.
- **O:** externe Entscheidung oder zukünftiger Nachweis.

V/B1: 46 Boundary-PASS-Zeilen, 243 Bibliotheks-PASS-Zeilen, 9 Host-PASS-Zeilen; Upstreamlog meldet 354 bestanden/0 fehlgeschlagen/0 übersprungen. Das sind **keine neuen unabhängigen Testläufe**. Es wurden keine B2-Implementierung, neue Binärprobe, Builds, Restores oder nativen Eingabetests ausgeführt. Die folgenden Fehlerszenarien sind konkrete statische Gegenbeispiele bzw. nicht entschiedene Designverzweigungen, keine vorgetäuschten Laufzeitresultate eines noch nicht implementierten B2.

## 2. Findings

Schweregrade: Critical = grundlegende unbeherrschte Gesamtgefährdung; High = Lücke in einer zugesagten Konsistenz-/Mutationsgrenze; Medium = relevante unbestimmte Semantik oder nicht erfüllbarer Teilvertrag; Low = begrenzte Planungs-/Reviewungenauigkeit. Ergebnis: **0 Critical, 4 High, 5 Medium, 1 Low.**

### GD-01 – High: Dokumentwechsel hat noch keinen vollständigen Bindungscommit

**Fundstelle:** B2 §2.3, Zeilen 93–97, und §3.2, Zeilen 159–161. [B2:93](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:93).

**Quellbeleg S:** Original [TextEditor.cs:137](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/TextEditor.cs:137) löst eigene Subscriptions und setzt TextArea.Document. [TextArea.cs:293](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/TextArea.cs:293) setzt seinerseits TextView.Document, registriert Handler und setzt Caret/Selection zurück. [TextView.cs:103](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Rendering/TextView.cs:103) exponiert eine dritte öffentliche StyledProperty; dessen Wechsel baut HeightTree/Caches um.

**Gegenbeispiel:** Eine Bindingaktualisierung wechselt TextEditor von A nach B. Beide Policies erlauben. Die Property besitzt bereits den neuen Wert; während der kaskadierten TextView-/TextArea-Nacharbeit wirft ein bibliothekseigener DocumentChanged-Empfänger oder Caretempfänger. Ohne konkret getrennten Bindungscommit bleiben äußere Property, innere Dokumente, Subscriptions und Generationen möglicherweise auf verschiedenen Ständen. Eine unabhängige Zuweisung an `editor.TextArea.TextView.Document` ist im vorgeschlagenen Eingangsinventar gar nicht benannt.

B2 fordert zwar gemeinsame frühe Zulassung und unabhängige Finalisierung, definiert aber nicht den Commitpunkt, die interne Delegation durch drei Propertyinstanzen, die Rolle des direkten TextView-Einstiegs, den finalen Bindungszustand nach Fehler oder die nötigen Generationserhöhungen. Ein pauschaler Coercion-Hook löst das nicht automatisch. Avalonia trennt Coercion, Wertübernahme und Benachrichtigung; daraus folgt kein mehrgliedriger Transaktionsvertrag. [Avalonia EffectiveValue<T>, 12.0.0](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.0/src/Avalonia.Base/PropertyStore/EffectiveValue%601.cs).

**Auswirkung:** Falsches Dokument bei späteren Eingaben/Replay, inkonsistente Darstellung und unzuverlässige Asyncinvalidierung. Die Beschränkung fremden Framework-Fanouts darf nicht die eigenen Bindungsinvarianten ausnehmen.

**Erforderliche Korrektur:** Alle drei Oberflächen inventarisieren; festlegen, ob innere direkte Zuweisungen synchronisiert oder vor Wirkung verweigert werden. Einen genau einmal zugelassenen Bindungsvorgang mit internem Kaskadenrecht, eindeutigem Commitpunkt, vollständiger interner Finalisierung und definierter Beobachtungsreihenfolge beschreiben. SetValue, SetCurrentValue, ClearValue, Bindingersatz/-ende und Recoercion prüfen. Kein Claim, dass bestehendes Avalonia dies bereits atomar erledigt.

### GD-02 – High: Native optionale Undoaktionen sind nicht automatisch dokumenttreu

**Fundstelle:** B2 §2.2/2.4, Zeilen 87/103; §3.3, Zeile 192; Matrix UND, Zeile 229. [B2:229](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:229).

**Quellbeleg S:** Original [TextArea.cs:398](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/TextArea.cs:398), RestoreCaretAndSelectionUndoAction, speichert eine WeakReference auf die TextArea, Caretposition und Selection. Undo/Redo schreibt auf die dann aktuelle TextArea. Weder Ursprungsdokument noch Bindungsgeneration werden geprüft. Diese native Klasse wird durch B1 nicht geändert.

**Gegenbeispiel:** E editiert A, wodurch eine solche optionale Aktion in A entsteht. Anschließend wird E legal an B gebunden. A bleibt durch einen zweiten Editor oder eine gehaltene Referenz zugänglich. Ein erlaubtes Undo/Redo auf A führt die alte native Aktion aus: Caret/Selection von E/B ändern sich. Deren Callback kann B ändern, sofern dort kein aktiver eigener Callbackschutz besteht; eine alte Selection kann außerdem geometrisch nicht mehr zu B passen. Ein freigegebener native-Typ allein verhindert dies nicht.

Zweites Gegenbeispiel: Beim Redo liegt eine optionale Caretaktion vor dem ersten DocumentChangeOperation. Sie ändert Caret/Selection; das folgende Changing wirft. B2 verlangt den Historieneintrag zurückzulegen und die vorgezogenen UI-Effekte „unter Kontrolle“ zu halten, legt aber nicht fest, ob diese bestehen bleiben oder restauriert werden und wie Fehler bei dieser Restaurierung behandelt werden.

**Auswirkung:** Nachfolgerwirkung außerhalb der Dokumentadmission sowie unbestimmter Zustand vor textwirkungslos gescheitertem Replay.

**Erforderliche Korrektur:** Native optionale Aktionen an Dokument und gültige Editorbindung binden; für stale Aktionen eine konkrete Skip-/Invalidierungsregel festlegen, ohne Textreplay zu verlieren. Vor-Text-Caretwirkung und deren Fehlerabschluss exakt spezifizieren. Tests: A→B, A→B→A, zwei Editoren, gesammelte/geschlossene TextArea, optional vor/zwischen/nach den Textteilen, erste und spätere Observerfehler. Die bisherigen acht reinen Dokument-Replaytests enthalten diese UI-Lifetimefälle nicht.

### GD-03 – High: Planvalidierung definiert die erlaubte Schreibfläche nicht

**Fundstelle:** B2 §5, Zeilen 238–242, Matrix PRV/SEL, Zeilen 225/228. [B2:240](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:240).

**Quellbeleg S:** [SimpleSelection.cs:56](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/SimpleSelection.cs:56) und RectangleSelection benutzen GetDeletableSegments/CanInsert vor UI-Edits. TextDocument.Replace erzwingt diese UI-Regel nicht selbst. Die Policy erhält nur Dokument, Kind und Reentrancy, keine konkreten Planbereiche.

**Gegenbeispiel:** Ein Completion-/Indentationprovider liefert einen syntaktisch gültigen, nicht überlappenden Plan, dessen zusätzlicher Edit in einen Readonlybereich oder außerhalb des vorgesehenen Indentationbereichs fällt. Die in §5 aufgezählte Validierung prüft Snapshot, Grenzen, Reihenfolge und Ergebnispositionen. Alle diese Prüfungen können bestehen. Die angekündigte Coredelegation könnte dann den UI-Readonlyvertrag umgehen. Ein Provider muss dazu kein fremdes Corerecht erhalten; gerade die Bibliothek vollzieht den unzureichend beschränkten Plan.

**Auswirkung:** Das Datenmodell trennt ausführbaren Fremdcode korrekt ab, begrenzt aber seine angeforderten Änderungen noch nicht eindeutig. Das ist ein Integritätsproblem, keine behauptete Sicherheitslücke gegen voll vertrauenswürdigen .NET-Code.

**Erforderliche Korrektur:** Pro Provider/Operation erlaubte Editflächen bestimmen, einschließlich bewusst erlaubter Zusatzedits etwa für Imports. Readonly-/CanInsert-Regeln für jeden geplanten Teil vor Commit auswerten. Festlegen, ob teilweise verbotene Pläne insgesamt abgewiesen oder deterministisch gekürzt werden; keine implizite Wahl. Für den virtuellen Post-Newline-Snapshot die Abbildung auf ursprüngliche Readonlybereiche definieren. Originale Raw-Document-APIs müssen dafür nicht global neue UI-Readonlyregeln erhalten.

### GD-04 – High: Drop-Teilwirkung und native Move-Bestätigung sind nicht gekoppelt

**Fundstelle:** B2 Matrix DEX/DIN/D2, Zeilen 218–220, sowie allgemeiner Teilwirkungsvertrag §3.3. [B2:219](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:219).

**Quellbeleg S:** Original [SelectionMouseHandler.cs:224](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/SelectionMouseHandler.cs:224) setzt e.DragEffects vor der Zielmutation. Der catch meldet Fehler verzögert. [StartDrag:381](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/SelectionMouseHandler.cs:381) löscht bei zurückgemeldetem Move die Quelle.

**Gegenbeispiel:** Rechteckiger Move von A nach B enthält zwei Zielzeilen. Die erste wird eingefügt, ihr Changed-Observer wirft; gemäß B2s normalem Compositevertrag wird die zweite nicht ausgeführt. Meldet der Zieladapter trotzdem Move, löscht die Quelle beide Zeilen. B2 beschreibt das ungefährliche Gegenrennen „Ziel erfolgreich, Quelle nicht gelöscht“, aber nicht diesen Verlustpfad „Ziel unvollständig, Quelle vollständig gelöscht“. Für den internen Move bleibt außerdem offen, wie der Dragdescriptor zwischen „begonnen“, „teilweise“ und „vollständig“ unterscheidet; ein einzelnes „bereits abgeschlossen“ genügt dafür nicht.

**Auswirkung:** Der vorgeschlagene Vertrag verhindert nicht eindeutig die Freigabe einer destruktiven Quelllöschung nach unvollständigem Zielcommit. Dass ein Benutzer möglicherweise Undo ausführen kann, ersetzt keinen korrekten Moveabschluss.

**Erforderliche Korrektur:** Definieren, wann Move überhaupt bestätigt wird: erst nach nachgewiesen vollständigem Zieltexttransfer und interner Finalisierung; zwischen erfolgreichem Transfer mit bloßem nachwirkendem Observerfehler und unvollständigem Transfer unterscheiden. Bei unvollständigem Drop keine Quelllöschung autorisieren. Interner Descriptor mit explizitem Ergebnis/Einmalverbrauch; Teilwirkung und nötige Benutzerdiagnose festlegen. Native Tests müssen das tatsächlich an die Quelle zurückgemeldete DragDropEffects prüfen, nicht nur Zieltext und geschlossene Gruppe.

### GD-05 – Medium: Teilpläne haben keine definierten Caret-/Selection-Nacharbeiten

**Fundstelle:** B2 §3.3, Zeile 187; Matrix RA/SEL, Zeilen 223/225; §5, Zeile 240. [B2:240](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:240).

**Gegenbeispiel D/S:** Ein Plan gegen „ab“ fügt zuerst rechts und danach links je 100 Zeichen ein; finale Caretposition 202 ist gegen den vollständigen Ergebnistext gültig. Nach dem ersten, absteigend ausgeführten Edit wirft Changed. Der Vertrag stoppt vor dem zweiten Edit; der tatsächliche Text hat Länge 102. Gleichzeitig gehören finale Caret-/Selectionwerte zum Plan und notwendige Nacharbeiten müssen weiterlaufen. Ob die Bibliothek 202 anwendet, klemmt, mappt oder die Nacharbeit unterlässt, ist nicht festgelegt.

Für ReplaceAll ist das Textpräfix klar beschrieben, die abschließende Suchauswahl/-index-Aktualisierung aber ebenfalls nicht vollständig bestimmt. Native Caretvalidierung könnte einen ungültigen Offset begrenzen; das wäre eine technische Nebenwirkung, kein spezifizierter Abschluss.

**Auswirkung:** Unterschiedliche korrekte Implementierer können verschiedene UI-/Fehlerresultate liefern; eine zusätzliche Finalisierungsexception kann den ursprünglichen Teilfehler überlagern.

**Erforderliche Korrektur:** Erfolgsabhängige Plan-Nacharbeiten von stets notwendigen Strukturabschlüssen trennen. Bei Teilwirkung tatsächliche Commitliste/Version benutzen; finale Planpositionen nur bei vollständig erfüllter Voraussetzung anwenden oder eine ausdrücklich definierte Mappingregel verwenden. Teilwirkung von ReplaceAll, Rechteck, Indentation und internem Move separat festlegen.

### GD-06 – Medium: Fehlererhaltung ist für fremde using-Rümpfe nicht erfüllbar

**Fundstelle:** B2 §2.4, Zeilen 107–109, §3.3 und T2, Zeile 317. [B2:107](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:107).

**Gegenbeispiel:**

```csharp
document.UpdateFinished += (_, _) => throw new FinishError();
using (document.RunUpdate())
{
    throw new CallerError();
}
```

Dispose sieht CallerError nicht. Wirft EndUpdate bei der Bereinigung wie spezifiziert MutationCallbackException, verlässt diese den finally-Pfad und verdeckt CallerError. Dass die Gruppe korrekt geschlossen ist, erhält nicht automatisch beide Fehler. Auch eine innerhalb des Bodys bereits publizierte MutationCallbackException kann durch einen weiteren Abschlussfehler verdeckt werden. [C# using/finally](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using).

**Auswirkung:** Die allgemeine Gateformulierung „werfender Hauptschritt plus werfender Abschluss verlieren keinen Fehler“ ist für das angebotene IDisposable-Muster zu weit. Das ist keine durch eine zusätzliche interne finally-Klausel lösbare Implementierungsaufgabe.

**Erforderliche Korrektur:** Die Erhaltungsgarantie auf bibliothekseigene Operationsrümpfe begrenzen und ein korrektes explizites Sammelmuster für manuelle Gruppen dokumentieren, oder eine anders spezifizierte Abschluss-/Ergebnis-API wählen. Kein öffentliches Callback-Allow-API als Abkürzung. Tests müssen genau beide Kategorien getrennt prüfen.

### GD-07 – Medium: Fehlerzustellung kann die vorgesehenen Ergebnisse nicht vollständig ausdrücken

**Fundstelle:** B2 Enum Zeile 168, MutationFailed Zeile 200, Stale-Regel Zeile 204 und D2 Zeile 220. [B2:168](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:168).

**Gegenbeispiele:**

1. Policy lehnt Paste vor Vorbereitung ab. MutationFailed soll die wirkliche Eintrittsphase berichten; MutationFailureStage enthält aber keine Admissionphase. Eine deterministische Zuordnung zu Preparation wird auch nicht festgelegt.
2. CopyAsync scheitert am Clipboard. Die Fehleroberfläche verlangt Kind; MutationKind kennt kein Copy. Ebenso fehlen explizite Abbildungsregeln für direkte öffentliche Selectionmutatoren.
3. Cut schreibt Clipboard erfolgreich, anschließend wird die lokale Löschung wegen neuer Version verworfen. §3.4 verwendet OperationCanceledException und behandelt Stale in nativen Adaptern ohne Fehlerdialog; §4/§10 verlangen verständliche Teilwirkungsdarstellung. Task ohne Resultat und TextChanged=false unterscheiden diesen Fall nicht von einem Abbruch vor jeder externen Wirkung.
4. Ein fremder Provider wirft selbst OperationCanceledException. Ohne interne Herkunftsmarkierung darf dies nicht als erwartete Veraltung still verschwinden.

**Auswirkung:** Unvollständige Phasendiagnose, verdeckte externe Teilwirkung und nicht eindeutig festgelegtes Fehlerfiltering.

**Erforderliche Korrektur:** Vollständige Operation-/Phasenabbildung und intern eindeutig identifizierte Cancellationursachen definieren. Externe Wirkung als unbekannt/möglich/erfolgreich und lokalen Commit getrennt transportieren – in lokalen Results oder klar spezifizierten Fehlerdaten, nicht zwingend als MCP-DTO. Auch ein fehlgeschlagener Clipboardtask kann außerhalb der Bibliothek schon Wirkung gehabt haben. Handled darf nur Zustellung beeinflussen, nicht den Ausgang umetikettieren.

### GD-08 – Medium: Der geforderte defekte Zustand fehlt im Lebenszyklusmodell

**Fundstelle:** B2 Zustandsdiagramm §3.1, Zeilen 117–128; §3.3, Zeile 189; Aktivierung/Deaktivierung §2.2. [B2:189](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:189).

**Gegenbeispiel D:** Ein unerwarteter interner Fehler unterbricht den Commit nach erster Wirkung. B2 verlangt, weitere Edits „bis expliziter Wiederherstellung“ zu sperren. Das Zustandsmodell führt nach Publishing jedoch immer nach Idle zurück. Es gibt weder definierte Faulted-Eigenschaft/-Exception, Wiederherstellungsoperation noch eine Regel, ob Policy=null, Text-Neusetzen, Undo, Save oder Dokumentwechsel den Zustand verlassen dürfen. Ein Besitzer könnte die Policy im vermeintlichen Ruhezustand abtrennen und die Sperre versehentlich umgehen.

**Auswirkung:** Eine ausdrücklich zugesagte Schutzmaßnahme hat keine prüfbare Lebensdauer. TextChanged als bool kann das zusätzlich beschriebene ungewisse Commitfenster nicht allein ausdrücken.

**Erforderliche Korrektur:** Faulted-Zustand samt erlaubter Lese-/Detach-/Diagnoseoperationen und nicht implizit durch Policywechsel aufhebbarer Sperre spezifizieren. Falls Reparatur ausdrücklich nicht geplant ist, Wiederherstellung als kontrollierten Austausch des defekten Dokuments definieren. Enginefehler von normalen Observerfehlern trennen; letztere sollen Folgeedits weiter erlauben. Keine Crash-/OOM-Garantie erfinden.

### GD-09 – Medium: Completion aus TextEntering verliert Eingabe oder veraltet immer

**Fundstelle:** B2 §5, Zeile 246, Matrix CMP Zeile 224 sowie R/A Zeile 208. [B2:246](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:246).

**Quellbeleg S:** [TextArea.cs:876](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/TextArea.cs:876) ruft TextEntering vor dem Textcommit auf; [CompletionWindow.cs:141](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/CodeCompletion/CompletionWindow.cs:141) besitzt bislang einen synchronen InsertionRequested/Complete-Pfad.

**Gegenbeispiel:** Text „pri“, aktiver Completionvorschlag, der Benutzer tippt einen Commitseparator. Der Handler merkt gemäß §5 nur einen an die aktuelle Version gebundenen Folgeauftrag vor. Lässt er die normale Eingabe passieren, erhöht diese Version und Auswahlgeneration; der Folgeauftrag ist schon durch seine eigene Auslösereingabe veraltet. Setzt er Handled=true, verhindert er zwar diese Veränderung, aber B2 sagt nicht, wer Separator und Completion zusammen einfügt, welche Eingabereihenfolge gilt und wie viele Undo-Gruppen entstehen. Popupclose kann die eigene Generation zusätzlich invalidieren.

**Auswirkung:** Der alte mutierende Callback wird korrekt nicht privilegiert, seine bisherige Benutzerfunktion erhält aber noch keinen determinierten Ersatz.

**Erforderliche Korrektur:** Für Tab/Enter/Separator/Maus konkrete Eingabekonsumierung festlegen. Entweder ein bibliothekseigener kombinierter, erneut zugelassener Plan mit festgelegter Basis oder eine ausdrücklich definierte Folgeoperation gegen den Zustand nach der Auslösereingabe. Keine pauschale Lockerung von Versionsprüfung oder Reentrancy. Positivtests müssen Separator genau einmal, Completion genau einmal, sinnvolle Undo-Grenze und Popup-Lifetime nachweisen.

### GD-10 – Low: U1s Baselineabhängigkeit ist zu eng beschrieben

**Fundstelle:** B2 §7, Zeilen 267–284. [B2:275](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:275).

**Gegenbeispiel V/D:** U1 soll vor U2/U3 bereits 46/243/354/9 reproduzieren. Der Text nennt dafür als vorgezogene D-Abhängigkeit mindestens die B1-Textsetter. Die 243 enthalten jedoch auch den strikten nativen Pastezweig, Style-/Clipboardfixture, native TextInput-/Newline-/Tabkontrollen, Load und SelectedText. Ohne diese B1-Inseln in U1 ist das genannte Gate nicht erreichbar.

**Auswirkung:** Reviewgrenzen und Exitkriterium können unterschiedliche Implementierungsstände meinen. Kein eigenständiger Produktfehler, aber unnötiger Druck, Tests umzudeuten.

**Erforderliche Korrektur:** U1 explizit als vollständigen B1-Funktionssockel plus Kernkorrekturen definieren; U2/U3 vervollständigen die noch offenen Composites. Die Baseline nicht kürzen. Alternativ ihren vollständigen Lauf erst an einer klar benannten kombinierten Seriengrenze verlangen.

## 3. Explizite Bewertung des öffentlichen API-Vertrags

„Plausibel“ bedeutet designseitig nachvollziehbar, nicht implementiert oder bestanden. „Teilweise“ bedeutet, dass eine genannte Präzisierung nötig bleibt.

| Vertrag aus §2 | Bewertung |
| --- | --- |
| IMutationPolicy, throw-or-return, frühe Prüfung | Plausibel und B1-gestützt; keine fachfremden Bibliotheksabhängigkeiten |
| MutationKind als äußerer Befehl, reserviertes EndUpdate | Im Prinzip sinnvoll; vollständige Zuordnung fehlt, GD-07. Wachstum/Unknown-Regel mit Maintainern entscheiden |
| Reentrancy bei Policy und Observern | Plausibel: keine weitere Policyrekursion, harte Bibliotheksablehnung trotz Policyzustimmung. B1 belegt definierte Dokumentpfade |
| Keine ambient Autorisierung / interne Coredelegation | Richtiger Grundsatz; interne Typidentität reicht für Lifetime nicht, GD-02; neue Provider bleiben ohne Corerecht |
| Aktivieren/ersetzen/deaktivieren nur idle | Eindeutig für normale Zustände; Faulted fehlt, GD-08. Policyreferenzwechsel und geänderter Entscheidungszustand derselben Policy unterscheiden |
| Besitzer, mehrere Dokumente, kein globales Gate | Saubere Grenze: globale Hostadmission ist extern. Reentrancygarantie ist dokument-/operationgebunden, keine Sandbox |
| SharedStack und historische Einträge | Designseitig richtige fail-closed-Regel; B1 weist sie nicht vollständig nach. Native optionale Aktionen benötigen zusätzlich GD-02 |
| Keine neue TextDocument-Disposalpflicht | Plausibel; pending Task, Subscription und Policyreferenz dürfen keine unbegrenzte Retention erzeugen |
| Dokumentwechsel / Policyübernahme | Kein automatisches Mitwandern ist richtig. Drei Controlbindungen und Fehlerzustände offen, GD-01 |
| Komplette Setter bis ClearAll/Nacharbeit | B1 belegt wichtige Fälle. TextChanged=false kann trotzdem Historywirkung bedeuten; B2 benennt dies korrekt |
| BeginUpdate/RunUpdate als Gruppierung ohne Autorisierung | Plausibel; jeder innere öffentliche Edit braucht neue Zulassung |
| EndUpdate ohne Policyveto | Richtige Lösung für Entscheidungswechsel derselben Policy; kein neues Begin/End beim abgewiesenen Eintritt |
| Gruppeneigentümer / Dispose höchstens einmal | Implementierbar mit internem Gruppentoken; direkte Begin/End bleiben vertraglich korrekt zu paaren. Keine Prüfung der Absicht beliebigen Fremdcodes versprechen |
| Manuelle Gruppen, Fehlerpublikation und finally | Gruppenbalance plausibel; Fehlererhaltung über fremden Rumpf nicht automatisch möglich, GD-06 |
| Async- und Bindungsgenerationen | A→B→A wird durch echte monotone Generation gelöst; praktische Abdeckung sämtlicher Bindungspfade hängt an GD-01 |

Ein bereits geteiltes UndoStack verlangt Prüfung **beider** Stapel und rekursiver nativer Gruppen, nicht nur derzeit angebundener Dokumente. Der Original-Stack hält private native Gruppenlisten; die Bibliothek kann ihre eigenen Strukturen ohne Reflection inventarisieren. Fremde IUndoableOperation müssen für Aktivierung nicht ausgeführt oder reflektiv entschlüsselt werden: unbekannte Einträge ablehnen ist implementierbar. Behandlung späterer öffentlicher Push-/Stackänderungen im strikten Modus sowie Ausschluss bewussten Raw-Missbrauchs müssen konsistent dokumentiert werden.

## 4. Explizite Bewertung des Event-/Fehlervertrags

| Fläche / Aussage aus §3 | Bewertung |
| --- | --- |
| Admission vor Update und fremden Gettern | Plausibel; frühe Argumentprüfungen ohne Fremdcode zulässig |
| Preparing → Updating → Finalizing | Plausibel für synchrone Composites; Faulted-Abzweig fehlt, GD-08 |
| UpdateStarted | Fehler darf eigene gerade eröffnete Gruppe nicht offenlassen; B1-belegt. Unabhängige spätere Empfänger und einmaliges Finish sind richtig |
| Changing / IDocument.TextChanging | Vor erster Textwirkung stoppen ist nachvollziehbar; nach begonnenem nativen Replay gilt die ausdrücklich andere Regel |
| Changed / IDocument.TextChanged | Engine bereits wirksam; Fehler sammeln und Finalisierung fortsetzen ist richtig |
| TextChanged / TextLength / LineCount / PropertyChanged | Partielle statt global erfundener Reihenfolge ist korrekt; konkrete Traces je Composite bleiben nötig |
| UpdateFinished | Nach geschlossener äußerer Gruppe, aber vor Ende mancher logischer Operationen: korrekt. Reentrancygrenze darf hier nicht fallen |
| Undo-/Redo-Notifications | Vor-/Nach-Text-Unterscheidung ist B1-gestützt; optionaler UI-Zustand und Lifetime offen, GD-02 |
| Caret/Selection | Vollständiger eigener Fanout und Callbackschutz plausibel; unabhängige Navigation und textgebundene Nacharbeit getrennt behandeln, GD-05 |
| Anchors | Native Bewegungs-/Löschsemantik beibehalten, verzögerte Ereignisse nach Konsistenz: richtig und teilweise B1-belegt |
| LineTracker | Fehlerisolierung B1-belegt. Bei An-/Abmeldung zusätzlich festlegen, ob Liste pro Einzelnachricht oder gesamtem Enginecommit gilt; neu beigetretene Tracker dürfen keinen unerklärten halben Änderungszyklus erhalten |
| Weak-Event-Fanout | Plausibel; Aufrufreihenfolge, Abmeldung/GC und Exceptionwrapping ohne unzulässige Retention testen |
| Fremde Provider / Iteratoren / Dispose | Fremdcodegrenze richtig; Planberechtigung und fremder using-Rumpf nicht dadurch erledigt, GD-03/GD-06 |
| TextEntering/-Entered/-Copied/-Pasted | Frühe/abschließende Callbackgrenzen nötig; Completionfolge und externe Teilwirkung noch unbestimmt, GD-07/GD-09 |
| Avalonia-Property-/Routed-Fanout | Ehrliche Beschränkung auf kontrollierbare Listen ist richtig; sie befreit nicht von eigenen Controlbindungsinvarianten, GD-01 |
| Exceptionhierarchie / TextChanged | Kein Rollback und kein effects=None: richtig. Die Stages decken nicht alle zugesagten Ergebnisse ab, GD-07; ungewisse Enginewirkung GD-08 |
| Fehler einmal nach Abschluss / MutationFailed | Grundsätzlich brauchbar. Direkte Taskfehler versus native Adapter müssen genau einmal berichten; Handled- und Fallbackfehler separat testen |
| Kein rekursives Finish bei früher Ablehnung | B1-gestützt und verbindlich; kein erneutes EndUpdate als Ablehnungsreparatur |
| Fehler während externer manueller Gruppe | Keine universelle Aggregation möglich, GD-06. Offen bleibende vom Aufrufer besessene äußere Gruppe ist nicht automatisch Bibliotheksleck |

Die Grenze „fremde Frameworksubscriber können nach Throw ausfallen“ wird akzeptiert als ehrliche Begrenzung des Bibliotheksversprechens. T3 muss deshalb kontrollierte Bibliothekslisten und Frameworklisten unterscheiden. Deren identische Abnahme als „späterer Handler erreicht“ wäre falsch.

## 5. Jede Composite-Matrixzeile

Zeilenangaben beziehen sich auf die unveränderte B2-Datei; **19/19 Zeilen** geprüft. In allen Fällen bleiben native Laufzeitbelege für B2 offen.

| B2-Zeile / Operation | Admission, Delegation, Reentrancy | Bindung, Neben-/Teilwirkung und Undo | Urteil |
| --- | --- | --- | --- |
| 212 Overstrike | Gate vor TextEntering/SelectRight und internes Replace sind richtig | Vor Ablehnung keine Selectionwirkung; Graphem/Readonly/Handlerpfade noch zu prüfen | Plausibles Design, kein B1-Vollnachweis |
| 213 Backspace/Delete | Gate vor Caret/Readonlyenumeration richtig | Präfixteilwirkung muss definierte Nacharbeit haben | Teilweise, GD-05 |
| 214 Tab/Shift-Tab/Newline | Planprovider statt mutierendem Callback sinnvoll | Koordinaten gegen Post-Newline und erlaubte Bereiche fehlen vollständig | GD-03/GD-05 |
| 215 Copy | Keine Textpolicy für reine Copy sinnvoll; Callbackschutz weiterhin nötig | Clipboardtask beobachten; Kind/Fehler-/Cancellationzuordnung fehlt | GD-07 |
| 216 Cut | Preflight und spätere erneute Zulassung richtig | Clipboarderfolg bindet nicht die spätere Löschung; Stale darf Quelle bewahren, Teilwirkung muss beobachtbar sein | GD-07; Sicherheitsrichtung grundsätzlich richtig |
| 217 Paste | Ein interner Commit nach neuer Admission statt offenem Update über await richtig | A→B→A anhand Generation, Version und Selection lösen; Policy-/Bindungswechsel erneut prüfen | Plausibel, abhängig GD-01/GD-07 |
| 218 Externer DragMove | Neue Quelladmission nach await richtig | Erfolgreiches externes Ziel plus verworfene Quelllöschung ist explizit erlaubt; Rückmeldung/Teilwirkung noch unvollständig | GD-04/GD-07 |
| 219 Drop/interner Move | Gate vor Zielcaret und eine native Gruppe richtig | Teiltransfer, Descriptorverbrauch und Moveacknowledgement fehlen | GD-04/GD-05 |
| 220 Move zwischen Dokumenten | Zwei lokale Admissions statt SharedStack richtig | Kein atomarer Zweidokumentvertrag; Quelle nur nach vollständigem Zieltransfer löschen | GD-04/GD-07 |
| 221 Search/FindNext/Previous | Navigation darf ohne Textadmission erfolgen; Callbackedit bleibt gesperrt | Kein Editorrevisionseffekt allein aus Navigation; async Auswahlbindung veraltet | Plausibel |
| 222 ReplaceNext | Frühe Admission vor sichtbarer Trefferauswahl korrekt gegen Originalpfad | Fehler in Refresh/Nacharbeit brauchen bestimmten Endindex/Selection | Kern plausibel, GD-05 |
| 223 ReplaceAll | Plan und absteigende native Ersetzungen geeignet | Geschlossenes Präfix mit echtem Undo ist konsistent; Folgeauswahl/-index nicht bestimmt | GD-05 |
| 224 Completion | Kein Corerecht für fremdes Complete; vor Hide prüfen richtig | Providerflächen, Inputkonsumierung, Popup-/Versionsbindung offen | GD-03/GD-09 |
| 225 Selection/Rechteck | Öffentliche Nativeinstiege zulassen; Fremdoverride nicht privilegieren richtig | Readonly-/virtuelle Spalten und Präfix-Nacharbeiten benötigen explizite Regeln | GD-03/GD-05 |
| 226 Navigation/Nacharbeiten | Keine Textpolicy für Navigation sinnvoll; Callbackschutz muss eigenständig existieren | Historische Aktionen dürfen keinen Nachfolger bearbeiten; Endposition bei Teilplan festlegen | GD-02/GD-05 |
| 227 Anchors/LineTracker | Kontrollierte Zustellung bei nativer Engine richtig | Trackerlistenänderung im Commit noch genauer festlegen; fremden privaten Zustand kann Bibliothek nicht reparieren | Teilweise, zusätzliche Gegenprobe |
| 228 Indentation/Readonly/Provider | Fremde Berechnung unter Callbackschutz richtig | Validierter Plan benötigt operativen Bereichs-/Readonlyvertrag | GD-03 |
| 229 Undo/Redo/Carethistorie | Frühe Admission und native Replaydelegation B1-gestützt | Ganze Gruppe nach erster Textwirkung plausibel; alte Editorbindung und optionale Vorwirkung fehlen | GD-02 |
| 230 Text/Clear/SelectedText/Load/Append/Document | Ganze Settergrenze und Basistypzugriffe durch B1 gestützt | Streamdispose plus Hauptfehler, Frameworkproperty und manueller Gruppenabschluss nicht pauschal bewiesen | Teilweise, GD-01/GD-06/GD-08 |

## 6. Obligatorische Gegenbeispiele und ihre Disposition

| Gegenbeispiel | Konkreter Ablauf | Löst B2 es vollständig? |
| --- | --- | --- |
| Policy zwischen Begin/End | Begin erlaubt; dieselbe Policy wechselt ihren Entscheidungszustand auf Denied; End/Dispose | **Ja, als Design für Gruppenbalance:** End darf nicht neu vetoierbar sein. Austausch der Policyreferenz selbst muss während offener Gruppe abgewiesen werden. Beides getrennt testen |
| Mehrteiliger nativer Replay | Gruppe aus zwei Inserts; Undo/Redo; erstes oder zweites Changing/Changed wirft | **Textkern ja:** vor erster Wirkung Eintrag zurück, danach vollständiger nativer Replay. B1 hat acht Kombinationen. **Gesamtzustand nein:** optionale Caretvorwirkung/Nachfolger, GD-02 |
| Fremde Completion/Indentation | Provider versucht während CreateEditPlan dokument.Insert oder liefert Edit in fremde Readonlyregion | Reentrancy ist eindeutig abzuweisen; ausführungsfreie Pläne sind machbar. Schreibfläche nicht vollständig definiert, GD-03 |
| StyledProperty/Bindingwechsel | SetValue/Binding A→B; Throw während kaskadiertem inneren Wechsel; separater TextView-Wechsel | Nein, GD-01 |
| Async-Paste A→B→A | Clipboardtask pending, zweimal legaler Wechsel, finale Referenz wieder A | **Ja auf abstrakter Ebene:** Bindungsgeneration muss veralten. Noch nicht durch B1 bewiesen; hängt an vollständiger Wechselabdeckung GD-01 |
| Cut/Move externe Wirkung, lokale Löschung verworfen | Clipboard/externes Ziel erfolgreich, danach Version-/Policywechsel | Textsicherheit explizit richtig: Quelle bleibt. Ergebnismeldung unvollständig, GD-07. Zusätzlich umgekehrter Dropfehlerpfad GD-04 |
| ReplaceAll + Observerfehler | Drei Ersetzungen, zweite Changed wirft | Textpräfix und eine geschlossene Gruppe sind festgelegt, sinnvoll undo-fähig. Nacharbeit/Ergebnispositionen fehlen, GD-05 |
| Finalisierung und Fehlerzustellung | Hauptfehler + Finishfehler + werfender MutationFailed-Empfänger | Für bibliothekseigenen Rumpf plausibel aggregierbar; fremder using-Rumpf GD-06. Fallback nicht rekursiv, aber Phasenmodell GD-07 und Faulted GD-08 ergänzen |
| Bereits geteilter Stack | A und B nutzen U ohne Strict; anschließend A.Policy setzen | **Ja als Design:** Aktivierung vor Wirkung verweigern, Stack/Policy/History unverändert. B1s StrictDocument-Marker reicht dafür nicht; historische Fremdeinträge nach Abtrennung ebenfalls prüfen |

Weitere gezielte Gegenproben: Copyfehler ohne MutationKind; Provider wirft OperationCanceledException statt echter Stale; erstmalige Aktivierung aus einem bereits laufenden Legacy-Caret-/Propertycallback; Fehler im letzten Dispose einer Providerenumeration; Trackerliste ändert sich zwischen BeforeRemoveLine und ChangeComplete; optionale Undoaktion greift auf anderen aktuell gebundenen Editor zu; zwei konkurrierende Clipboardaktionen in vertauschter Antwortreihenfolge; Clone/Mehrfachverwendung eines Plans; unmittelbarer öffentlicher Edit aus MutationFailed.

Diese Beispiele sind keine pauschale Behauptung, jede dieser Varianten sei in B1 defekt. Sie konkretisieren die noch benötigten Designentscheidungen und Abnahmesolls.

## 7. Provider-, Legacy- und Kompatibilitätsbewertung

Planbasierte Provider sind technisch mit der Bibliothek vereinbar: Die Bibliothek besitzt die Anwendung der Werte und kann bestehende Replace-/Undo-/Anchorpfade weiterverwenden. Es ist weder eine zweite Textengine noch eine allgemeine Callbackfreigabe nötig. Fremde Getter/Iteratoren müssen vor der Commitphase vollständig in native immutable Werte überführt sein; ein Interface mit nur lesenden Properties ist allein noch kein Nachweis nebenwirkungsfreier Implementierung.

Offen bleiben GD-03, GD-05 und GD-09. Außerdem müssen die endgültigen Requesttypen ausreichend Informationen für die bestehenden Indentationfunktionen enthalten: Zielzeilen, Einfüge-/Löschbereiche, Tab-/Newlineoptionen, Snapshotbasis und finale Positionssemantik. Nichtüberlappung allein ordnet zwei Zero-length-Inserts am selben Offset nicht eindeutig; Gleichstandsregel oder Ablehnung definieren. Bestehende OffsetChangeMappingType-/Anchorbewegung darf durch Plananwendung nicht still auf eine andere Semantik reduziert werden.

Legacyprovider unter Strict ausdrücklich abzulehnen ist als optionaler neuer Modus vertretbar, wenn der Umfang offen dokumentiert ist. Das ist keine Freigabe, produktive Completion oder Indentation ersatzlos auszuschalten. Der spätere Host braucht Adapter und Positivtests.

Ohne Policy dürfen sich dokumentierte Standardpfade nicht durch gemeinsame neue Commandadapter verändern. Insbesondere Cut/Paste müssen klar nach Legacy/Strict getrennt bleiben, wenn ihre alte synchrone/async-void-Semantik bewahrt werden soll. Die 354 Tests sind wertvolle Regressionen, aber kein vollständiger API-/Binarynachweis. Source-/Binary-Diff, beide ursprünglichen TFMs, XAML/avares, TextMate und Avalonia-Mindestversion bleiben erforderlich.

Die neue Exceptionbasisklasse bleibt eine AggregateException und kann B1s Typprüfungen erhalten; die Kompatibilität einer nie veröffentlichten PoC-API ist von der Kompatibilität des offiziellen Releases zu unterscheiden. Öffentliche Plan-/Task-/Eventtypen brauchen exakte Signaturen und Nullability vor API-Freeze, nicht zwingend bereits eine Veröffentlichung.

## 8. U0–U4 und Reihenfolge

| Einheit | Bewertung |
| --- | --- |
| U0 | Richtig: Vertrag und minimale Reproduktionen zuerst. Die Findings müssen vor Umsetzung als konkrete Sollresultate entschieden sein |
| U1 | A+B+C zusammenzuführen ist sachlich richtig; isolierte Policy ohne sichere Finalisierung wäre inkonsistent. Zusätzlich GD-02/GD-06/GD-08 und vollständigen B1-Sockel berücksichtigen, GD-10 |
| U2 | Größte Architekturrisiken: Bindungscommit und Planvalidierung. GD-01/GD-03/GD-05 müssen hier gelöst sein. Keine scheinbar vollständige öffentliche Strictfreigabe vor U3 |
| U3 | Baut sachlich auf U1/U2 auf; native Acknowledgements, Clipboardergebnisse und Completioninput sind eigene Verträge, nicht nur weitere Gatecallsites |
| U4 | Geeignet als Implementierungs-/Artefaktabnahme. Bibliotheksfreigabe, Upstreamannahme und Produktintegration bleiben getrennt |

Die Aufteilung ist als **Reviewserie** sinnvoll, nicht als Folge separat produktionsreifer Pakete. B2 benennt das bereits korrekt. Realistische Reihenfolge: zuerst diese Designkorrekturen und unabhängige Abnahme, dann unveröffentlichte Gesamtserie mit fortlaufender Baseline. Kein Gate darf nachträglich „bestehen“, indem ein bekannter Pfad bis U3 aus der Baseline entfernt wird. Eine Aufwandsschätzung ist ohne die Lösung des Property-/Planvertrags nicht belastbar.

## 9. Distribution und Reproduzierbarkeit

Die drei Optionen sind sauber getrennt. Offizielles Release bevorzugen; sonst eine ausdrücklich genehmigte befristete Linie oder weiter blockieren. Kein dauerhafter Fork, kein Überschreiben offizieller PackageId/Version und kein Mischgraph sind richtige Anforderungen.

Die lokale Originalprojektdatei bestätigt net8.0/net10.0, SignAssembly, öffentlich vorhandenen avalonia.snk-Bezug und TextMate-ProjectReference. Paketversion, Assemblyidentität und Vertrauensnachweis müssen getrennt bleiben. Ein eigenes NuGet-Paket ersetzt keine transitiven offiziellen References automatisch. Beibehaltener Assemblyname/Key plus eigene Version ist als nicht parallel geladener Ersatz prüfbar, garantiert aber allein weder Kompatibilität noch eindeutige Auflösung. Das entspricht den herangezogenen [NuGet-Hinweisen](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices) und der [Strong-Naming-Dokumentation](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/strong-naming).

| Gegenstand | Reviewurteil / noch erforderliche Entscheidung |
| --- | --- |
| Quellrevision/Patchhash | Ausreichend spezifiziert; echter Patchcommit und reproduzierbare Herkunft später erforderlich |
| Eigene PackageId/SemVer | Sinnvoll; Beispiel ist ausdrücklich kein Pin. Präfix und Feedbesitzer noch O |
| Assemblyname/Key/Version | Bedingt geeignet; konkreter Versionsbereich und tatsächliche Loader-/XAML-/TextMate-Prüfung vor Weg 2 |
| Abhängigkeitsgraph | B2 erkennt transitive Doppelreferenzen korrekt; finalen clean-cache-Restore und Publishgraph überprüfen |
| Reproduzierbarkeit | Deterministische DLLs, Pfadnormalisierung, SourceLink und Zweitbuild sinnvoll. Exakt festlegen, welche unsigned Artefaktbytes gleich sein müssen und welche Signatur-/Archivmetadaten separat verifiziert werden |
| Lizenz/Notices | MIT-/Herkunftshinweise nicht nur im nuspec nennen; tatsächlichen Paketinhalt prüfen. Keine pauschale Aussage zu allen zukünftigen Abhängigkeiten |
| Feed/Verifikation | Unveränderliche Ablage, getrennte Schreibrechte und unabhängig verifizierte Provenienz sinnvoll; Hash aus demselben kompromittierten Feed allein nicht ausreichend |
| Pflege | Owner/Budget/Frist/Sicherheitsupdates fehlen als externe Entscheidungen, nicht als erfundene Zusage |
| Exit | Richtigerweise erst offizielles Release mit dem tatsächlich angenommenen Vertrag und bestandenen Gates; Teilannahme genügt nicht |

Kein eigenständiges High-Finding zur Distribution: Die verbleibenden konkreten Namen/Versionen/Owner können bis zur ausdrücklich getrennten Distributionsentscheidung offenbleiben. Die Laufzeitsemantik aus GD-01–GD-09 darf dagegen nicht auf eine solche Entscheidung verschoben werden.

## 10. T1–T10 und zusätzliche Abnahmesolls

| Gate | Bewertung und notwendige Ergänzung |
| --- | --- |
| T1 Ownership/Lifetime | Richtige Richtung; beide Historienstapel/verschachtelte Gruppen, bisher geteilte und später getrennte Stacks, Legacycallback-Aktivierung und historische optionale UI-Aktionen ergänzen |
| T2 Abschluss | Policyreferenzwechsel vs. Entscheidungszustandswechsel unterscheiden. GD-06 als getrennten APIvertrag behandeln; Faulted-Ausnahme zu „Folgeedit möglich“ ergänzen |
| T3 Events | Gute Flächenmatrix, aber bibliothekseigenen Fanout von Avalonia-Fanout trennen. Mehrere gleichzeitige Fehler, Subscriptionänderung pro Phase und nichtrekursiven Fallback testen |
| T4 Historie | Echte Rückfolgen statt Flags richtig. GD-02 mit Nachfolgereditor, optionalen Vorwirkungen und beiden Replayrichtungen zwingend |
| T5 Composites | Alle 19 Zeilen bleiben verpflichtend. GD-03 Readonlyplan, GD-05 Teilplannacharbeit und GD-09 Completionseparator positiv testen |
| T6 Async | Generationen und vertauschte Antworten gut. Externes Wirkungsergebnis, Provider-OCE vs. Stale, Movebestätigung am Quellende und Teil-Rechteckdrop ergänzen |
| T7 Delegation | Kein geerbtes Recht richtig. Gemischte Strict-/Legacy-Dokumente, direkte TextView.Property und Providerpläne ohne zulässigen Schreibbereich ergänzen |
| T8 Legacy | Ausgangsversion als Differentialkontrolle gut; gemeinsam neu verdrahtete void-/Task-/Routedadapter auf unbeabsichtigte Semantikänderung prüfen |
| T9 Plattform | Native Eingabe-/Clipboard-/Moveabnahme unverzichtbar. Pro unterstütztem OS/Backend ein unterschriebenes Ergebnisprotokoll bzw. überprüfbarer CI-Nachweis; fehlende Umgebung bleibt offen |
| T10 Paket/Performance | TFMs und Graph gut. Workloads und akzeptierte Grenzwerte für Allokation/Latenz/Retention vor Messung festlegen; „Kosten messen“ allein ist kein Passkriterium |

Baseline 46/243/354/9 bleibt verbindlich, aber nicht hinreichend. B1 prüft beim gruppierten Replay echte Textfolgen, jedoch in den acht betreffenden Fällen keinen TextArea-basierten optionalen Caretstack. Die Standalone-Caret-/Selectionfälle prüfen Reentrancy, nicht die vollständige Fehlerzustellung außerhalb einer Mutation. Die Native-Pastefälle prüfen weder A→B→A noch externen Clipboardverzug. Diese fehlenden Nachweise werden ausdrücklich **nicht als bestanden** bewertet.

G-DESIGN benötigt nach Korrektur einen anderen Reviewer und eine neue Prüfung des unveränderten Sollumfangs. G-LIBRARY muss später die tatsächlich gebauten Quellen/Artefakte und eigene Gegenproben prüfen. Beide Gates dürfen die vollständigen B3/B4-Hostregressionen nicht ersetzen.

## 11. Hostgrenze, Eigentümer- und Maintainerentscheidungen

B2 hält die wesentliche MCP-00-Trennung ein: editorId/Revision, gemeinsame Interpreteradmission, serviceeigene Cancellation, SaveIntent/Save-Completion, lokale Bestätigung/Berechtigungen, GUI-Diagnosen und Authentisierung/Transport bleiben beim Host. Library TextChanged wird nicht zu MCP effects=None erklärt. Keine neue CryptoScript-/MCP-/Prismreferenz ist vorgesehen. ExecutionContext-Schutz und Prismdistribution bleiben zurecht separate Migrationsaufgaben.

Wichtig bleibt die Hostoperation über **mehrere** Bibliotheksaufrufe: eine zulässige Rückkehr aus einer Libraryoperation beendet nicht die fachliche Admission. Ebenso darf ein „Copy statt Move“ bzw. „Clipboard geschrieben, Cut nicht gelöscht“ nicht durch einen generischen Busy-/Cancelled-Text verschleiert werden. Die dafür fehlenden lokalen Ergebnisdaten gehören zu GD-07, ihre redigierte GUI-/MCP-Projektion weiterhin zum Host.

Offenbleiben dürfen: konkrete Paketidentität und Feed, zeitlich befristetes Pflegebudget/Owner, Upstreamkommunikation und Maintainerannahme, native Testumgebungen, Namenskonventionen/Enumstrategie und spätere B3/B4-Freigabe.

Vor Designannahme zu entscheiden sind dagegen: GD-01 Bindungscommit, GD-02 historische UI-Aktionen, GD-03 erlaubte Planflächen, GD-04 Moveacknowledgement, GD-05 Präfix-Nacharbeiten, GD-06 manuelle Fehlererhaltung, GD-07 Ergebnis-/Cancellationmodell, GD-08 defekter Zustand und GD-09 Completionkonsumierung. Diese sind keine bloßen Eigentümerpräferenzen ohne Laufzeitfolgen.

## 12. Schlussurteil und Arbeitsabschluss

**CHANGES REQUIRED / BLOCKED.** B1 belegt einen tragfähigen Kern, aber B2 ist noch nicht vollständig und widerspruchsfrei genug für DESIGN ACCEPTED. Die zehn Findings sind getrennt zu bearbeiten; die Matrixbewertungen dürfen dabei nicht als bereits implementierte oder bestandene Abnahmen übernommen werden.

Dieser Bericht ändert weder B2 noch die bisherige MCP-01.4-Reviewhistorie. R2/R4/R6/R7 bleiben im Produkt offen. Selbst ein späteres DESIGN ACCEPTED wäre nur eine Designabnahme, keine Implementierungs-, Paket-, Migrations- oder Integrationsfreigabe.

Einzige neue Produktdatei: `docs/mcp/MCP-01.4-B2-independent-review.md`. Bestehende Produktdateien, B1-Patch, Bibliotheks-/Solltests und Logs wurden gegen die Eingangshashes geprüft. Branch/HEAD und vorhandener Dirty-Status bleiben erhalten; Staging bleibt leer. Keine Produktcodeänderung, keine automatische B2-Korrektur, kein Build/Restore/Publish, keine GUI-Migration/MCP-01.5, kein Upstream-PR und kein Commit/Push/Merge/Rebase. `.codex/config.toml` wurde nicht gelesen oder verändert. Nach Ablage und Integritätsprüfung wird angehalten.

