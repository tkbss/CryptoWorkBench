# G2-04-01-F2 — Final Architecture Contract Corrections

**Ergebnisstatus: READY FOR OWNER REVIEW — nicht ACCEPTED**  
**Datum:** 17.11.2026  
**Repository/Baseline:** tkbss/CryptoWorkBench, `32017a08c4771e73c12a50f6e1548476b9bb0a54`

**F3-Nachtrag, 17.11.2026:** Abschnitte 1–8 dokumentieren den ursprünglichen F2-Stand vor R4. R4 ist inzwischen abgeschlossen; ausschließlich sein verbleibender Befund R4-L01 wurde mit der Fixture-Korrektur in Abschnitt 9 bearbeitet. Aktueller Artefaktprüfstand: 42/42 PASS. Der Status bleibt READY FOR OWNER REVIEW, nicht ACCEPTED.

## 1. Ergebnis und Aussagegrenze

Die sechs R3-Findings wurden gezielt in einer konsolidierten F2-Fassung adressiert. Die Korrekturen sind als überprüfbare Architekturverträge, Copy-Goldens, Accessor-Matrix, Ereignismatrix und Designinteraktionen festgelegt. **„Auf Vertragsebene geschlossen“ bedeutet hier weder ACCEPTED noch implementiert.** R4 muss die unten benannten Änderungen unabhängig nachprüfen; anschließend entscheidet der Owner über G2-04-02.

Die Produktprämisse bleibt unverändert: Schlüsselmaterial, Shares und Zwischenwerte sind vollständig sichtbar und kopierbar. Keine Maskierung, Reveal-Autorisierung, Passwortprüfung oder neue Geheimhaltungsschicht.

F2 enthält:

1. `G2-04-01-F2-architecture.md` — konsolidierte Zielarchitektur einschließlich großer Reads, Host-Ablauf, Lebenszyklus und Dockmigration.
2. `G2-04-01-F2-accessor-matrix.md` — vollständige Typ-/Property-/BCL-/Indexer-Matrix.
3. `G2-04-01-F2-copy-contract.md` und `copy-golden-values.json` — Wertformat, Ausnahmen und 19 Golden-Fälle.
4. `G2-04-01-F2-visual.html` — eigenständig öffnbarer HTML-Prototyp ohne externe Abhängigkeiten.
5. `G2-04-01-F2-acceptance.md` — 38 Implementierungs-Abnahmekriterien.
6. Dieser Bericht sowie `validation.md`/`validation-results.json` — Nachweis und verbleibende Risiken.

`prototype-model.js`, `prototype-ui.js`, `build-artifacts.cjs` und `validate-artifacts.cjs` sind ausschließlich Quellen/Prüfhilfen des Dokumentationsprototyps. Sie sind kein Produktcode und keine im Repository angelegten Tests. Das ausgelieferte HTML bettet die beiden Prototyp-Skripte ein; zum Öffnen sind keine Build-/Installationsschritte erforderlich.

## 2. Baseline und unveränderte Grundlagen

Vor Beginn war `HEAD == master == 32017a08c4771e73c12a50f6e1548476b9bb0a54`; der Worktree war sauber. Die Quellstellen wurden nur gelesen. Die F1-Artefakte und der R3-Bericht bleiben historische Eingaben; sie wurden nicht überschrieben. Alle F2-Dateien liegen außerhalb des Master-Worktrees unter dem gleichnamigen F2-Verzeichnis im vorhandenen Dokumentationsordner.

Keine Änderungen an Produktcode oder Tests, keine Commits/Pushes/Merges, keine Änderung an master, kein eigenständiger Start von G2-04-02 oder R4. Es wurde kein Produktbuild und kein C#-/Avalonia-/Native-Produkttest ausgeführt.

## 3. Nachweis je R3-Finding

| F2 / R3 | Korrektur | Überprüfbarer Nachweis | Verbleibender Implementierungsnachweis |
|---|---|---|---|
| **F2-01 / R3-H01** Complex Copy | Skalare exakt; Bytes uppercase Hex; komplexe Werte nur mit tatsächlichen fachlichen Membern. Arrays und Dictionaries ohne Inspector-Hüllen. Parent-Copy nur vollständig; Aliaswerte expandieren, echte Zyklen explizit nicht kopierbar. | Architektur §4; vollständiger Copy-Vertrag; 19 Goldens einschließlich ganzer KEY-/PARAM-/SECRET-/TR31H-Roots; Prototyp-Encoder liefert wertreines JSON. Echter Dictionary-Key `type` wird korrekt behalten. | A09–A15: C#-Encoder, ulong/UTF-16, Plattform-Clipboard und Fehler-/Historienfälle. |
| **F2-02 / R3-H02** Große Werte | Root-Hardlimits aufgehoben. Bedarfsgerechte Range-/Memberaufnahme, lückenlose Coverage unter gleichem CaptureStamp, UI-Paging getrennt. Voll-Copy lädt den ganzen verlangten Wert. Cachebudget wird durch lokalen Payload-Store ergänzt, nicht durch Abschneiden. | Architektur §5–6 definiert Schritte, Buffer, Admission, Cancellation, Staging/Commit und Clipboard-Endallokation. Prototyp hat 255×128-KiB-Fixture; der vollständige Shares-Payload wurde tatsächlich geprüft. | A16–A22, A25–A29: nativer Host/IO/Speicher, Range-Accessor, großer vollständiger Parent, Fehler/Drain und Performance. |
| **F2-03 / R3-M01** Inventar | Öffentliche Instanz-Properties jedes erreichten unterstützten Runtime-Typs; geerbte/überschriebene Slots, Collection-Metadaten, Einträge und Indexer getrennt. Bekannte BCL-Unterbauten eingegrenzt; unbekannte Getter sichtbar, nicht ausgeführt. | Normative Accessor-Matrix; .NET-10-Collection-Metadaten read-only abgefragt; Rootzahlen und Fixtures geprüft. Prototyp zeigt Properties getrennt vom Copy-Wert. | A01–A08: Reflection-Vollständigkeit, transitive Getter/Wrapper/Overrides und bekannte Berechnungen. |
| **F2-04 / R3-M02** Observer | Observer erfasst nur Absicht. Ein koaleszierter Dispatcher-Post erfolgt nach Rückkehr. UI-Admission wird erneut geprüft; Worker benutzt Core-State-Scope, nicht UI-affine Validierung. Kein Polling oder unbeschränkter Retry. | Architektur §6, genaue Abgrenzung gegen heutigen RunState/RunGuiAsync. Simulierte späte Capture-Ergebnisse werden im Prototyp atomar verworfen. | A23–A29: echte WorkbenchBindable-/Coordinator-Reentrancy und vorhandener Service-Lebenszyklus. |
| **F2-05 / R3-M03** Lifecycle | Normale Edits/MCP invalidieren Handles, erhalten detached historische Werte. Null-Auswahl, ApplyVariables-null, semantischer Reset, Layout-Reset und Dispose sind getrennt. | Verbindliche Ereignismatrix Architektur §7; Prototyp-Ereignisse edit/acquire/release/drain/publication/reset/layout/close/shutdown. Simulation bestätigt History-Copy und keine Autoritätswiederbelebung allein durch Release oder Auswahl. | A28–A32: reale Pub-/State-/Identity-Races, Clear-/Dokumentwechsel-Hooks und Service-Drain. |
| **F2-06 / R3-L01** Fixtures | KEY-Literal ist Secret(Unknown), ohne Attribute, KCV null nach Anforderung. Nicht-null HeaderReference und OriginalKeyMetadata. Gültige Headerdefinition ohne fehlendes Padding. Lange/bedarfsgeladene Werte und Full Copy aktualisiert. | Prototyp und Golden-Herkunftsdokumentation; tatsächliche Formatkonstanten `HEX_STRING`, `PARAM_STRING`, `NORMAL_STRING`, `PATH_STRING` und gequotete CryptoType-Ids übernommen. Default weiterhin zwei Spalten, Variables aktiv. | A33–A38 sowie native Visual-/Accessibility-Abnahme. Browser-Demo ersetzt keine Dockingprüfung. |

Damit bleiben keine der sechs **bekannten Vertragslücken** absichtlich bis zur Implementierung unentschieden. Der Entwurf enthält jedoch konkrete neue technische Pflichten, die R4 bewerten muss; diese werden nicht als bereits vorhandene APIs ausgegeben.

## 4. Wesentliche Architekturentscheidungen

### Wertformat statt generischem Snapshot-Export

Der Projector besitzt einen kleinen expliziten Wertencoder. Er serialisiert keine lebende Declaration. Status, Typinformation und historische Herkunft bleiben UI-Daten. Collection-Metadaten sind vollständig zugänglich, beeinflussen aber den Collection-Wert nicht. Ein PARAM-Methodenadapter bleibt eine Methode; Root-Copy enthält kein erfundenes Parameters-Feld.

Null, Empty, NotLoaded, NotCalculated und Getterfehler sind verschieden. Actual-null-KCV kann kopiert werden. Ein unvollständiger historischer Wert kann nicht aus einem späteren Zustand vervollständigt werden. Diese Grenze gilt unabhängig von seiner Größe.

### Konkreter Großwertpfad

Ein einzelner Benutzerauftrag läuft als interner `InspectorRead` im vorhandenen Service-Lebenszyklus. Er hält die gemeinsame Admission über seine schrittweise Aufnahme, gibt AccessState vor await frei und finalisiert geordnet. Das ist bewusst kein direktes `Task.Run` und kein ungeprüfter Aufruf des heutigen RunGuiAsync: dessen Variablen-/Execution-Result-Nebenwirkungen werden für den Read-Zweck ausgeschlossen.

Die UI bleibt bedienbar; konkurrierende Interpreterjobs erhalten Busy. Zwischen getrennten Reads wird das Gate freigegeben; Stamp-/Identity-Prüfung vor jeder Ergänzung verhindert Revisionsmischung. Eine große Full-Copy-Aktion ist endliche bedarfsgerechte Arbeit, keine Hintergrund-Daueraufnahme.

32 MiB RAM-Cache, kleine Schritte und snapshotlokale Tempsegmente ersetzen das bisherige funktionale Rootlimit. Das Clipboard kann abschließend einen größeren String benötigen. Physische Grenzen werden als Fehler ausgewiesen, niemals durch erfolgreiche Teilwerte verdeckt. Eine alternative Exportfunktion ist kein Ersatz für das verlangte Copy.

### Notwendige kleine Core-/Host-Ergänzungen in der späteren Implementierung

- Interner Read-Zweck und entsprechende Finalisierungs-/Retentionsabgrenzung im vorhandenen Automation-Service; keine neue globale Verwaltung.
- `SecretShare.CopyValueRange` als rein lesender Bounds-geprüfter Weg zu den bestehenden privaten Bytes; keine Gesamtclone-Schleife je Seite.
- Schmaler Zugriff auf veröffentlichte Original-Identifier-/VM-Stempel, ohne eine zweite Identity-Datenbank.
- Semantischer Reset-/Dokumentwechsel-Hook am bestehenden Ablauf, statt WorkspaceChanged als Reset zu interpretieren.

Diese Ergänzungen sind jetzt nur beschrieben. Tempsegmente/Payload-Leases sind Ressourcen eines Snapshots; sie dürfen nicht zu einer zweiten persistenten Workspace- oder globalen Snapshot-Verwaltung wachsen.

## 5. Verbleibende technische Risiken

| Risiko | Einordnung | Festgelegte Begrenzung / Abnahme |
|---|---|---|
| Read-Zweck im bestehenden Host | **Wichtig für R4.** Der vorhandene Lifecycle hat Variablenprojektion, öffentliches LastResult, Timer und Retention; ein unvollständiger Read-Sonderpfad könnte Nebenwirkungen oder Leaks erzeugen. | Architektur §6 nennt jede abzugrenzende Stelle. Ein Gate/active owner, Stop-/Epoch-Drain wiederverwenden; A25–A29. Keine Behauptung, RunGuiAsync sei heute bereits ein Inspector-Read. |
| Große Clipboard-Endallokation | Nativer Text-Clipboardvertrag kann trotz gestreamter Aufnahme einen großen zusammenhängenden String/OS-Kopie verlangen. | Exakte Längen-/Peak-Prüfung, kein Teil-Erfolg; A15/A20/A21. Plattformgrenzen bleiben messbarer technischer Befund, kein verborgenes Datenlimit. |
| Temporärer Speicher / Crash-Reste | Vollständige historische Werte oberhalb RAM benötigen lokalen Speicher. IO-Fehler oder Prozessabbruch können Ressourcen zurücklassen. | Snapshotlokale Ownership, segmentiertes Staging, lokale Leases, geordnete Löschung; Reserve/IO-Fehler sichtbar. A19/A22. Keine Layoutpersistenz, keine fremden Pfade löschen. |
| Laufzeit langer Live-Reads | Konsistenter großer Read hält Admission und kann GUI-Ausführung/MCP vorübergehend abweisen. Synchrone Getter sind nicht präemptiv abbrechbar. | Nur auditierte/rangefähige Getter, Worker-Schritte, Fortschritt/Cancel; keine harte Timeout-Freigabe des Gates. A18/A21/A29. |
| TR31H-Serializer-Konfiguration | Fremde Newtonsoft DefaultSettings könnten beliebigen Formattercode ausführen. | Bekannter Standardpfad zugelassen; fremde Konfiguration UnsupportedFormatter, nicht automatisch ausführen. Andere Member zugänglich. A07. Kein universelles Fremdcode-Supportversprechen. |
| BCL-/Typentwicklung | Neue Properties oder interne Wrapper-Unterbauten können Inventar/Audit verändern. | Metadata-Differenz sichtbar; Werte erst nach Matrix-Erweiterung. Runtime-Metadatenprobe und A01–A04. |
| Revision erfasst nur kooperierende Mutationen | StateRevision ist kein Hash beliebiger Objektzustände. | Alle Produktmutation über bestehenden Host; Identität plus Revision und UI-Stempel. Unsynchronisierter Fremdcode ist kein unterstützter Nebenpfad. A28. |
| Native Docking-/Accessibility-Regressionsfälle | HTML kann weder Dock.Admission noch Cross-Window-Mouse-Redock nachweisen. | Bestehende Gates/Migration unverändert konsolidiert; A33–A38 nativ. |

Keines dieser Risiken eröffnet erneut Maskierung, Autorisierung oder Passwortschutz. Sie betreffen Korrektheit, Ressourcen und Integration.

## 6. Artefaktvalidierung

Die dokumentierten Prüfhilfen ergaben **38 bestandene Dokumentations-/Prototypprüfungen**, darunter alle **19 Golden-Fälle**. Getestet wurden exakter String-/JSON-Payload, echte Dictionary-Keys statt Blacklist, Rootzahlen, korrigierte Fixturemetadaten, lange Strings, historische Werte während MCP, stale Reads, Null-/Reset-/Shutdown-Zustände, verspätete Capture-Ergebnisse, nicht ausgeführte unbekannte Getter, Zyklen und Alias-Copy.

Der große synthetische Shares-Payload wurde vollständig erzeugt und gegen unabhängig byteweise erzeugte Vergleichswerte geprüft:

- 255 Shares × 131.072 Bytes = **33.423.360 Rohbytes**.
- Vollständiges kanonisches JSON der Shares: **66.852.988 Clipboard-Zeichen**.
- SHA-256 dieses UTF-8-kodierten JSON-Strings: `b1b6e0e777b6678f113ce4e59fc8f7d3cf282a21828828f8b2799f49d061ed3a`.
- Historisches Copy desselben vollständigen Werts nach simuliertem MCP Acquire bleibt identisch.

Dies ist ein JavaScript-Datenpfadnachweis. Es wurden weder 38 Implementierungskriterien als bestanden erklärt noch echte C#-Getter, Tempdatei-Streaming oder natives Clipboard damit abgenommen. HTML-Skripte/Element-IDs und der deklarierte Zwei-Spalten-Default wurden statisch geprüft. Eine Browser-/Screenshot-/Native-Visual-Abnahme wurde in F2 nicht ausgeführt.

## 7. Begrenzter R4-Prüfauftrag — noch nicht gestartet

R4 sollte sich auf diese überprüfbaren Fragen konzentrieren:

1. Erfüllen die definierten JSON-Schemata/Goldens und Fehler-/Zyklusregeln R3-H01 vollständig, ohne fachliche Member zu verlieren?
2. Ist der serviceeigene Read-Zweck einschließlich Finalisierung, Quoten, Timer, UI-/Core-Grenzen und Shutdown mit dem fixierten Code vereinbar?
3. Reicht der beschriebene große Capture-/Payload-/Clipboard-Pfad ohne funktionale Rootlimits; sind technische Fehler ehrlich abgegrenzt?
4. Ist die Accessor-Matrix vollständig für die unterstützten Baseline-Typen einschließlich BCL-/Indexer-Unterobjekten?
5. Trennen Request-Nummer, CaptureStamp und Ereignismatrix reale Reset-/Publikationsfälle korrekt von normalen Editor-/Lease-Invalidierungen?
6. Sind Fixtures, Wertformat und 38 spätere Abnahmekriterien konsistent?

Kein neuer Gesamtentwurf und keine Wiederaufnahme verworfener Schlüsselgeheimhaltungsanforderungen. Owner-Freigabe und Implementierung bleiben getrennte Schritte.

**Ergebnisstatus: READY FOR OWNER REVIEW — nicht ACCEPTED.**

## 8. Kritische Baseline-Quellstellen

- [Observer-Umfassung](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/ViewModelBase.cs:13), [Observer-Gate](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:347).
- [UI-affines ValidateStateAccess](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:88), [prozessweite Reservation/RunAdmitted](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs:24).
- [Accept/Worker-Lebenszyklus](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:264), [bestehendes RunGuiAsync](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:391), [Finalize mit Variablen/LastResult](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs:583).
- [Private Draft-/Identity-Prüfung](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:91), [ApplyVariables löscht Auswahl](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs:174).
- [Edit sendet WorkspaceChanged](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs:165), [Lease-Publikation](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs:139), [tatsächliches Clear](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs:124).
- [SecretShare.Value klont](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:17), [SecretDescriptor ohne 16-MiB-Limit](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:25), [Literal-Key-Defaults](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Expressions/Expression.cs:120).
- [Default-Dockgruppe](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs:42), [Dock-Gate](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs:99), [atomare Layoutpersistenz](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs:67).

## 9. F3 — Ausschließliche Korrektur von R4-L01

Die KEY-Fixture hatte den menschenlesbaren Scripttext in `Statement.Text` übernommen. Tatsächlich verwirft der Lexer Whitespace zwischen Tokens; `context.GetText()` gelangt über AntlrToStatement und StatementEvaluator in diese Property. Das Script bleibt `KEY demoKey=0x(...)`; der Property-Wert lautet `KEYdemoKey=0x(...)`. Modell, KEY-Root-Golden und generiertes HTML wurden entsprechend korrigiert. Der Copy-Vertrag ergänzt ausschließlich den Herkunftsnachweis dieses Beispiels. Parser und Copy-Encoder wurden nicht verändert.

`validate-artifacts.cjs` enthält jetzt einen vollständigen, unabhängig von Fixture und Golden festgelegten Erwartungsstring. Vor Neugenerierung wies dieser den alten Golden nachweislich ab. Ein zusätzlicher Negativtest weist auch denselben falschen Text in Fixture und Golden zurück. Weitere Prüfungen gleichen Script-/Property-Dokumentation und die exakten eingebetteten Modell-/UI-Quellen im HTML ab.

**Ergebnis:** 19/19 Copy-Goldens und 42/42 Artefaktprüfungen bestanden: alle ursprünglichen 38 plus vier gezielte F3-Prüfungen. Dies schließt R4-L01 im beauftragten Korrekturumfang; es ist keine neue umfassende Architekturprüfung oder native Implementierungsabnahme. Architektur, Accessor-Matrix, A01–A38, UI-/Copy-Algorithmen und Buildskript bleiben unverändert. Vorher-/Nachher-Hashes und die vollständige Dateiliste stehen im separaten F3-Abschlussbericht und Prüfprotokoll. Der frühere R4-Bericht bleibt unverändert als historische Reviewgrundlage erhalten.
