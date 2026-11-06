# L1-B1 – Unabhängiger Architektur-, Code- und Regressionstest-Review

Datum: 2026-11-05. Reviewgegenstand: L1-A, nicht die parallele GUI-Entwicklung G1.

## 1. Prüfumfang und Nachweisbasis

- Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\script-assignment`.
- Branch: `feature/script-assignment`.
- Vollständig verifizierter Commit: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Verifizierter direkter Eltern-/Basiscommit: `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.
- Geprüft wurden der Diff aller 24 geänderten Dateien sowie die relevanten unveränderten Parser-, Runner-, Funktions-, Variablen-, Diagnose- und Hostpfade.
- Der Implementierungsbericht wurde als Orientierung verwendet. Die folgenden Testergebnisse und Gegenbeispiele wurden im Review selbst ausgeführt.
- Produktivcode und permanente Tests wurden nicht verändert. Keine Commits, kein Push, kein Merge. Dieser Bericht ist die einzige zusätzliche nicht ignorierte Datei. Ausführungslogs, TRX und ein separater Probeprozess liegen in ignorierten Artefakt-/Buildverzeichnissen.

**Ergebnis:** Zwei High-Findings und ein Medium-Finding. Die grundsätzlich passende Prepare/Commit-Architektur lässt in den nachgewiesenen Fällen ungültige beziehungsweise verfälschte Werte bis zur Veröffentlichung durch.

## 2. Architekturbeurteilung

### Parser und AST

Die zusätzliche `assignment`-Regel unterscheidet sich durch `ID '='` von Deklarationen mit vorangestelltem Typ und Aufrufen mit `FN '('`. Die bestehende leere Deklarationsalternative bleibt erhalten; das Assignment verlangt dagegen einen Initialisierer. Es gibt keine neue automatische Deklaration: `VAR target=` erzeugt weiterhin keinen gespeicherten Wert, weshalb ein anschließendes `target=1` eine unbekannte Zielvariable meldet.

`AntlrToAssignment` erstellt ausschließlich Syntaxknoten und schreibgeschützte Parameter-/Headerlisten. Es führt keine Funktion aus und greift nicht auf das Dictionary zu. Gemischte Anweisungen behalten Reihenfolge und `GetText()`-Textzuordnung; letzteres enthält entsprechend der bisherigen Konvention keine ursprünglichen Zwischenräume.

Die Lexer-Priorität bleibt unverändert: beispielsweise ist `Foo` ein FN-Token, `foo` ein ID-Token und `VAR` ein Typbezeichner. `VAR Foo=1 Foo=2` und `VAR VAR=1` sind damit weiterhin keine zulässigen Namensformen. Es wurde keine neue Zusage beliebiger Bezeichner eingeführt.

Die zehn generierten Dateien wurden aus Kopien aller drei Grammatikdateien mit dem lokal vorhandenen ANTLR 4.13.2 und `-Dlanguage=CSharp -visitor CryptoScript.g4` neu erzeugt: Parser, Lexer, beide Listener, beide Visitor, zwei `.interp` und zwei `.tokens`. Alle stimmen nach Normalisierung von Zeilenenden und nachgestellten Leerzeichen mit dem Commit überein. Die Generierung erfolgte ausschließlich unter `artifacts/l1-b1-antlr`.

Fehler-Recovery wurde zusätzlich mit `target=`, `target= VAR after=1`, `target=Compare(`, `target={KBVID:;}` und `target=#MECH:` geprüft. Alle erzeugen Syntaxfehler, keine Zustandsänderung beim Mapping. Einige beschädigte Recovery-Kontexte werfen beim direkten Mapping allerdings `NullReferenceException`. Der produktive `WorkbenchExecutionPipeline` beendet Syntaxfehler vor dem Runner; GUI und MCP führen diese Kontexte nicht aus. Dies ist eine dokumentierte API-Voraussetzung/Risikogrenze, kein nachgewiesener produktiver Ausführungsfehler.

### Vorbereitung, Veröffentlichung und Atomarität

Beide Runner verwenden denselben `StatementEvaluator`-Zweig. Der Evaluator sucht das Ziel vor jeder RHS-Ausführung, bereitet ein separates Objekt vor und ruft erst nach Typ-/Ergebnisprüfung `VariableDictionary.Update` auf. `Expression.PrepareVariable`, PARAM- und PATH-Helfer veröffentlichen nicht selbst. Die bisherigen Deklarationsaufrufer übernehmen weiterhin ihre eigene Veröffentlichung.

Der Typvergleich erfolgt über den CryptoScript-Typnamen, nicht über identische C#-Klassen. Das neue Objekt erhält Zielnamen, Zieltyp und Assignment-Text. Ein zurückgegebenes Objekt, das bereits direkt im Dictionary liegt, wird vor diesen Änderungen abgewiesen.

Diese Struktur schützt den alten Zustand bei tatsächlich erkannten Fehlern. Die Findings betreffen fehlende Erkennung vor dem Commit: Der fehlerhafte RHS-Wert wird als Erfolg behandelt und ersetzt das Ziel. Es wurde kein zusätzlicher scriptseitig erreichbarer Fall gefunden, in dem ein bereits als fehlgeschlagen erkanntes Assignment das alte Zielobjekt mutiert.

Die Referenzprüfung ist keine allgemeine Prüfung des gesamten Objektgraphen. Deshalb wurden insbesondere KEY-Attribute, PARAM-Headerreferenzen und SECRET-Daten geprüft: `Check` erzeugt einen neuen KEY samt kopierter Attributliste; strukturierte Headerdefinitionen sind unveränderlich; SECRET-Operationen erzeugen eigene Ergebnisobjekte. Bereits existierende PARAM-Referenzen auf einen ersetzten Header bleiben Referenzen auf das alte Objekt. Das entspricht Objektersetzung; ein nachträgliches Umbinden aller Referenzen ist kein L1-Vertrag.

### Serialisierung, Reentranz und fremde Effekte

`InterpreterExecution.Enter` und `RunRunnerAsync` bleiben erhalten. Synchrone Reentranz desselben Threads ist weiterhin zulässig; asynchrone parallele Runner, unberechtigter Zustandszugriff und Host-Callback-Reentranz werden blockiert. Die zugelassenen Assignment-Funktionen eröffnen keinen nachgewiesenen öffentlichen Print-/Info-/Passwort-Callback, mit dem das Ziel während der Vorbereitung ersetzt werden könnte. Interne Testinjektionsstellen sind keine registrierten Scriptfunktionen.

PARAM-Ziele dürfen im gesamten Aufrufbaum nur `Parameters` verwenden. Damit werden mutierende PIN-Operationen auch bei Verschachtelung ausgeschlossen. Andere Zieltypen dürfen fremde PARAM-Objekte oder Dateien beeinflussen. Das bestehende Gegenbeispiel mit abgeschlossenem `Wrap` und anschließend scheiterndem `Compare` wurde erfolgreich getestet: Das Assignment-Ziel bleibt alt, die fremde PARAM-Änderung bleibt sichtbar. Eine globale Transaktion wird zu Recht nicht behauptet.

## 3. Bestätigte, priorisierte Findings

### B1-01 — High — Leerer TR31H-Funktionswert wird als gültiger Ersatz veröffentlicht

**Ort:** [CryptoScript/Model/AssignmentEvaluator.cs:125](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:125) (Zeilen 118–127, insbesondere 125); Prüfung/Commit Zeilen 47–60 und 81–85. Auslöser: [CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31Header.cs:13](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31Header.cs:13) (Zeilen 13–16).

**Reproduktion über Parser und beide Runner:**

```cryptoscript
TR31H target={KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;}
target=BlockHeader(WRAP-AES-TR31,0x(01))
```

**Ist-Verhalten:** Keine Syntaxfehler, keine Exception, keine Diagnostic. Das Dictionary enthält danach ein neues `BlockHeaderVariableDeclaration` mit `Value == ""` und `Definition == null`. Der vorher validierte Header wurde ersetzt. Das separat gehaltene alte Objekt ist zwar unverändert, aber nicht mehr der Zielzustand im Dictionary. Die anschließende Verwendung als `#BLKH:target` scheitert am bestehenden Vertrag in `Arguments/Argument.cs:69–75`, der eine validierte Definition verlangt.

**Ursache:** Die Zulassung enthält pauschal `blockheader`. Dessen Mehrargument-Überladung liefert bereits im Bestand einen leeren typisierten Platzhalter. `ReturnVariable.Type` und die Rückgabeklasse reichen aus, um alle neuen Guards zu passieren; `ValidRepresentation` prüft weder Headerdefinition noch verwertbaren Inhalt.

**Betroffene Anforderungen:** L1-A2 Schritte 4–6, L1-A3 verwendbarer Rückgabewert/TR31H-Vertrag, L1-A4 fehlender Rückgabewert beziehungsweise fehlerhafter Funktionsvertrag und L1-A5 Veröffentlichung erst nach erfolgreicher Validierung.

**Minimale Korrektur für L1-B2:** Im Assignment-Pfad leere/unbrauchbare Header-Rückgaben vor `Update` mit einer strukturierten Diagnostic abweisen. Für strukturierte TR31H-Ziele die validierte Definition als erforderlichen Ergebnisvertrag prüfen. Das alte einargumentige `BlockHeader` ist ausdrücklich eine Legacy-Anzeigefabrik und ebenfalls keine validierte Transportdefinition; dessen bestehende Deklarationssemantik darf nicht beiläufig geändert werden. Die Zulassung dieses Legacy-Ergebnisses für neue Assignments muss ausdrücklich begrenzt werden. Eine zentrale Funktionsmetadatenarchitektur ist hierfür nicht erforderlich.

**Erforderlicher Regressionstest:** Obiges Script synchron und asynchron; Zielreferenz, `Definition`, Wert und Metadaten müssen nach dem abgewiesenen Assignment identisch bleiben. Diagnostic samt ExecutionId/StatementIndex prüfen. Positive Gegenfälle mit Headerliteral und `CreateHeader` sowie den einargumentigen Legacy-Fall separat abdecken.

**Herkunft:** Der leere Factory-Rückgabewert ist älter. Neu ist seine ungeprüfte Zulassung als vollständiger Ersatz eines bereits gültigen Ziels.

### B1-02 — High — PARAM ohne #MECH umgeht die Pflichtprüfung durch Textinhalt

**Ort:** [CryptoScript/Model/ParameterInitializerEvaluator.cs:14](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:14) (Zeilen 14–23); Assignment-Aufruf in [CryptoScript/Model/AssignmentEvaluator.cs:62](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:62) (Zeilen 62–66).

**Reproduktion über Parser und beide Runner:**

```cryptoscript
PARAM target=#MECH:AES-CBC
target=#IV:"MECH"
```

**Ist-Verhalten:** Erfolgreiche Ausführung ohne Diagnostic. Das ursprüngliche PARAM-Objekt wird ersetzt; der neue Mechanismus ist `"MECH"`, der serialisierte Wert `#MECH:"MECH"#IV:"MECH"`. Der Initialisierer enthält überhaupt keinen `#MECH`-Parameter.

**Ursache:** Die Suche verwendet `RawText.Contains("MECH")` statt des bereits vorhandenen strukturierten Parameternamens. Ein beliebiger Parameterwert kann dadurch als Mechanismusdeklaration gelten. `Parameter.Mechanism = mech.RawText` und die nachfolgende Normalisierung erfinden daraus einen `MECH`-Eintrag. Die nachgeschalteten spezialisierten Validatoren erkennen diesen Fall nicht.

**Betroffene Anforderungen:** L1-A3 PARAM-Struktur/-Validierung, L1-A4 ungültiger Initialisierer und L1-A5 Erhalt des Zielzustands bei einem abzulehnenden Initialisierer. Der vorhandene Test für fehlendes `#MECH` beweist nur den Fall ohne die Zeichenfolge im Fremdwert.

**Minimale Korrektur für L1-B2:** Vor der Assignment-Vorbereitung einen tatsächlichen Parameter mit `TypeName == "#MECH"` verlangen und den Mechanismus aus dessen strukturiertem Wert beziehen. Die Korrektur lässt sich zunächst im Assignment-Pfad begrenzen, um die ausdrücklich verlangte Deklarationskompatibilität nicht unbemerkt zu verändern. Kein allgemeiner Umbau aller Mechanismusvalidatoren nötig.

**Erforderlicher Regressionstest:** Obiges Script in beiden Runnern muss mit `InvalidAssignmentInitializer` scheitern und dasselbe Zielobjekt einschließlich Mechanismus, Parameterwerten und Metadaten behalten. Positiver Fall mit echtem `#MECH`; weiterer negativer Fall mit `MECH` in einem anderen Fremdparameterwert.

**Herkunft:** Die fehlerhafte Suche wurde aus der alten Deklarationsroutine übernommen. Auch die Kontrollprobe `PARAM target=#IV:"MECH"` zeigt das Altverhalten. Dies ist keine behauptete Deklarationsregression, sondern eine nachgewiesene Lücke im neu zugesicherten Assignment-Vertrag.

### B1-03 — Medium — Großes Ganzzahlliteral überschreibt VAR still mit leerem String

**Ort:** [CryptoScript/Model/AssignmentEvaluator.cs:73](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:73) (Zeilen 73–76); verwendeter Fallback in [CryptoScript/Expressions/Expression.cs:145](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Expressions/Expression.cs:145) (Zeilen 145–151).

**Reproduktion über Parser und beide Runner:**

```cryptoscript
VAR target="old"
target=2147483648
```

**Ist-Verhalten:** Null Syntaxfehler und null Diagnostics. Das Ziel wird durch ein neues VAR mit dem Wert `""` ersetzt. Die ursprüngliche Zahl geht verloren.

**Ursache:** Die Grammatik akzeptiert `INT: [0-9]+`. `Expression.Create` erkennt Zahlen jedoch ausschließlich mit `int.TryParse`; beim Überschreiten von Int32 wird der allgemeine Fallback auf einen leeren String genommen. Der Assignment-Pfad unterscheidet diesen Fallback nicht von einem erfolgreich vorbereiteten Literal.

**Betroffene Anforderungen:** L1-A1 Literalinitialisierung und L1-A4 strukturierter Fehler bei ungültigem Initialisierer; die verpflichtende Validierung vor Ersetzung aus L1-A2 wird bei dieser stillen Wertverfälschung nicht erfüllt. Der CryptoScript-Typ bleibt zwar VAR, der Literalinhalt wird dennoch unangefordert verändert.

**Minimale Korrektur für L1-B2:** Im neuen Assignment-Pfad ein numerisches Literal außerhalb des unterstützten Wertebereichs ausdrücklich mit `InvalidAssignmentInitializer` ablehnen. Keine allgemeine Erweiterung der Ausdruckssprache und keine Änderung der alten Deklarationssemantik erforderlich. Eine etwaige Unterstützung größerer Zahlen wäre eine gesonderte Produktentscheidung.

**Erforderlicher Regressionstest:** Grenzfälle `2147483647`, `2147483648` und eine deutlich längere Ziffernfolge über Parser und beide Runner. Der zulässige Wert bleibt exakt erhalten; die abgewiesenen Fälle erhalten Zielreferenz und Metadaten und liefern die korrekte Statement-Diagnostic.

**Herkunft:** Auch `VAR target=2147483648` zeigt im unverändert verwendeten Deklarationspfad denselben Fallback. Der Befund verlangt deshalb eine begrenzte Assignment-Validierung, keine ungefragte Änderung des Bestandsverhaltens.

## 4. Typfamilien und Funktionsverträge

### Typfamilien

| Familie | Geprüfter Vertrag und Bewertung |
|---|---|
| VAR | Verschiedene C#-Ergebnisklassen zulässig; Script-Typ muss VAR bleiben. Literal-/Funktionsresultate und Folgeoperationen getestet. Stiller numerischer Fallback: B1-03. |
| KEY | Literale werden tatsächlich dekodiert, nichtleeres Material und Bitlänge geprüft. Neuer Literal-KEY erhält Unknown/Unspecified-Defaults. GenerateKey/Check-Ergebnisse behalten ihre Ergebnis-Metadaten; Check kopiert Attribute. Ein inkompatibler oder fehlerhafter RHS erhält das alte Objekt. |
| VAL | Nur passende `ValVariableDeclaration`; deren Setter erzwingen echte Boolean-Werte. Reale MacVerify-Ergebnisse und Fehlererhalt über CWM-Tests geprüft. Compare liefert dagegen VAR-Text, kein VAL. |
| PARAM | Neues Parameterobjekt vor Veröffentlichung; nur Parameters im gesamten Funktionsbaum. Headerreferenz und explizite Parameter bleiben Teil der Vorbereitung. Fehlende strukturelle Mechanismusprüfung: B1-02. |
| PATH | Gemeinsamer PATH-Helfer übernimmt die vorhandene Entschlüsselung von String-Escapes und lehnt numerische Initialisierer ab. Reale MacPack-/PATH-Rückgaben getestet. Dateioperationen besitzen eigene Vertragsprüfungen. |
| TR31H | Headerliterale werden über CreateHeader validiert. Reine Klassenprüfung für Funktionsresultate reicht nicht: B1-01. |
| SECRET | Ergebnis muss `SecretVariableDeclaration` sein; dessen SecretSet ist erforderlich. Allgemeiner Value-Zugriff zeigt nur SECRET. Share/Restore-Pfad getestet. Explizites Print darf gemäß bestehendem Vertrag Shares ausgeben, ist aber als Assignment-RHS gesperrt. |

Bei KEY/VAL/PATH/SECRET wurden keine zusätzlichen produktiv erreichbaren Vertragsverletzungen nachgewiesen. Das bedeutet nicht, dass die Rückgabeklassenprüfung beliebige zukünftige oder per C# eingeschleuste Objekte vollständig validieren würde.

### Systematischer Abgleich mit OperationFactory

Die aktuelle explizite Liste deckt die registrierten Funktionen ab und lehnt neue/unbekannte Namen standardmäßig ab. Sie ist als begrenzte Zulassungsstrategie geeignet, ist aber kein Beweis für jeden Ergebniszweig einer zugelassenen Funktion.

| Funktion(en) | Verwertbare Rückgabe / relevante Wirkung | Assignment-Beurteilung |
|---|---|---|
| Parameters | Neues PARAM; mechanismusspezifische Vorbereitung/Validierung | Für PARAM-Ziele alleinige zulässige Funktionsfamilie, auch verschachtelt. |
| GenerateKey, Derive | KEY mit Ergebnis-Metadaten | Zieltypen werden nach Rückgabe geprüft; kein nachgewiesener Zielalias. |
| Hash, Mac | VAR; bei Dateipfaden Lesen von Dateien | Bestehende Datei-/Mechanismusverträge gelten vor bzw. während der Ausführung. |
| Encrypt, Decrypt | VAR oder im Dateipfad PATH; Datei-Veröffentlichung möglich | FileEncryptionContract vor RHS; fremde Datei-Effekte sind nicht global transaktional. |
| Wrap, Unwrap | VAR bzw. mechanismusabhängig KEY/VAR; PIN-Mechanismen können PARAM-Eingaben verändern | Bei PARAM-Ziel auch verschachtelt gesperrt; fremde PARAM-Effekte bleiben zulässig. |
| BlockHeader | Legacy-TR31H-Anzeige oder leerer typisierter Platzhalter | Unzureichend geprüft: B1-01. |
| CreateHeader | Neues TR31H mit validierter unveränderlicher Definition | Headerattribute werden von der bestehenden Factory geprüft. |
| Check | Neuer KEY als Kopie eines gefundenen Schlüssels | Kein direkter Live-Alias; Suchfehler behalten ihre Weiterlaufsemantik. |
| Compare | VAR mit Vergleichstext | Kein Boolean; vorhandene verschachtelte Textkonvertierung ist eine Bestandsgrenze, siehe unten. |
| Share, Secret | Neues SECRET | Keine Veröffentlichung des Zielnamens innerhalb der Operation. |
| Restore | Neuer KEY | Bestehende SECRET-/Schlüsselverträge gelten. |
| PinBlockTranslate | VAR; mutiert beteiligte PARAM-Objekte über bestehendes Staging | Direkt nur für VAR; bei PARAM-Ziel im gesamten Baum gesperrt. |
| MacPack | PATH; schreibt ein Containerziel | Spezifischer Dateivertrag und echte Rückgabe in CWM-Tests geprüft. |
| MacVerify | VAL; liest/prüft Container | Boolean-Invariante durch Ergebnisklasse. |
| Print, Info | Kein für Assignment nutzbarer Wert; Ausgabe/Callbacks | Vor Auswertung des gesamten Baums abgewiesen. |
| Save, Load | Snapshot-I/O; Load kann Interpreterzustand veröffentlichen | Vor RHS ausgeschlossen. |
| SecureSave, SecureLoad | I/O, Passwort-Callback, asynchroner Spezialpfad; SecureLoad kann Zustand veröffentlichen | Vor RHS ausgeschlossen, kein Passwortdialog durch Assignment. |
| Delete | Änderung des Interpreterzustands | Vor RHS ausgeschlossen. |
| Unbekannte/neue Funktionen | Nicht auditiert | Werden vor Argumentauswertung abgewiesen. |

`ValidateCallTree` besucht rekursiv alle `NestedCallArgumentNode`-Kinder, bevor irgendein Geschwisteraufruf ausgeführt wird. Die bestehenden Tests zählen ausgeführte Operationen und prüfen ausgebliebene Ausgabe. Es wurde kein Verschachtelungs-Bypass dieser Zulassung nachgewiesen.

**Keine notwendige zentrale Metadatenarchitektur:** Die bestätigten Fehler lassen sich mit lokalen Ergebnis-/Initialisiererprüfungen und Regressionstests beheben. Ein späterer automatischer Abgleich zwischen Registry und Zulassungsliste wäre eine optionale Wartbarkeitshilfe, keine Abnahmevoraussetzung dieses Reviews.

## 5. Diagnostics und Fehlerbehandlung

Die numerischen Werte 0–17 bleiben unverändert. Hinzu kommen ausschließlich 18 AssignmentTypeMismatch, 19 InvalidAssignmentInitializer und 20 MissingReturnValue. Enum- und Transporttests wurden ausgeführt.

Ziel-/Typ-/Initialisiererfehler tragen die Statement-Zuordnung; Operationsfehler behalten ihre ExecutionId sowie OperationId/ParentOperationId. Die Probe `target=Compare(Hash(HASH-SHA256,missing),0x(01))` ordnet beide erzeugten Meldungen dem inneren Hash zu: OperationId 2, ParentOperationId 1, StatementIndex 1, gemeinsame ExecutionId. Kein Verlust durch das Assignment-Catch wurde beobachtet.

Die Diagnostic-Sammlung dedupliziert identische Error-Objekte bei wiederholtem Capture. Bei einer fehlenden Funktionsargumentvariable gibt es allerdings zwei verschiedene Meldungen: UnknownVariable als Ursache und FunctionExecutionFailed als Funktionskontext. Dieses Verhalten stammt aus dem unveränderten FunctionCallEvaluator; es ist keine durch Assignment neu eingeführte doppelte Registrierung desselben Objekts. Die neuen Tests verwenden teilweise `Last(d => d.DiagnosticCode == code)` und prüfen deshalb nicht die gesamte Anzahl/Reihenfolge. Eine Zusage „immer genau eine Diagnostic pro fehlgeschlagener RHS“ ist durch die Implementierung nicht gedeckt.

Der Assignment-Catch redigiert generische Funktionsfehler und Deklarationsmeldungen aus gemeinsam verwendeten Initialisierern. Die bestehenden Marker-/Transporttests sowie die Gegenproben zeigten keine vertraulichen Literalwerte in diesen Fehlermeldungen. Dies ist keine vollständige Informationsflussanalyse aller Mechanismen.

`KeyCheckValueSearchException` wird weiterhin vom StatementEvaluator abgefangen. Das Assignment belässt den alten KEY, die passende Diagnostic wird gesammelt und die folgende Anweisung ausgeführt; beide Runner-Fälle wurden getestet.

## 6. Unabhängig ausgeführte Tests und Abdeckung

Alle folgenden Läufe waren Debug unter Windows/.NET SDK 10.0.401. Core wurde zusätzlich mit aktiviertem Build und `--no-restore` aus dem geprüften Worktree ausgeführt; GUI und MCP liefen mit den bereits vorhandenen Debug-Builds und `--no-build --no-restore`.

| Lauf | Ergebnis | Beleg |
|---|---:|---|
| Core: AssignmentTests, neue CWM-Assignmentfälle, VariableDeclaration, StatementExecution, ParameterInitializer, Tr31HeaderInitializer, SemanticDiagnostic, AntlrLanguageMetadata | 1.431 bestanden, 0 fehlgeschlagen | `CryptoScriptUnitTest/TestResults/l1-b1-review-core-built.trx` |
| Core: AsyncPasswordInfrastructureTests, einschließlich Reentranz, paralleler Runner und Zugriffsrechten | 25 bestanden, 0 fehlgeschlagen | `CryptoScriptUnitTest/TestResults/l1-b1-review-async.trx` |
| GUI: AssignmentMcpIntegrationTests, NativeEnterContinuationTests, SemanticDiagnosticPresentationTests | 48 bestanden, 0 fehlgeschlagen | `CryptoWorkBenchGuiTest/TestResults/l1-b1-review-gui.trx` |
| GUI: LeaseV2Tests und CryptoScriptExecutionPresentationTests | 49 bestanden, 0 fehlgeschlagen | `CryptoWorkBenchGuiTest/TestResults/l1-b1-review-lease.trx` |
| MCP: bestehende Suite | 176 bestanden, 0 fehlgeschlagen; 13 explizite Fälle nicht ausgeführt | `CryptoWorkBench.Mcp.Tests/TestResults/l1-b1-review-mcp.trx` |

Die GUI-Läufe enthalten 97 unterschiedliche Testfälle. Bei MCP weist die TRX 189 Einträge aus, davon 176 Passed und 13 NotExecuted; die CLI-Zusammenfassung meldet trotzdem „Skipped: 0“. Maßgeblich ist hier die Einzelresultatliste.

Die 66 neuen L1-A-Fälle wurden anhand der Testdefinitionen und Ergebnisse nachvollzogen: 61 AssignmentTests, 2 CWM-Fälle, 1 nativer Enter-Fall und 2 MCP-Lease-Integrationsfälle. Alle sind in den obigen Läufen enthalten und bestanden.

Die Tests sind aussagekräftig für den produktiven Parser-/Runnerpfad, sieben Typfamilien, Namen/Typen, wiederholte Ersetzung, beobachtbare Folgeoperationen, KEY-Metadaten, fehlende Ziele, ausgewählte Fehler, Alias-Rückgaben, Void-Preflight und Hostintegration. Besonders sinnvoll ist der explizite Test gegen die Behauptung globaler Transaktionalität.

**Nachgewiesene Abdeckungslücken:** typisierte, aber inhaltlich leere Rückgaben einer realen zugelassenen Funktion; `MECH` als Teil eines fremden Parameterwerts; numerische Bereichsgrenzen; gesamte Diagnostic-Anzahl/-Reihenfolge; Mapping beschädigter Recovery-Kontexte. Die vier injizierten Ergebnisfehler prüfen null, untypisiert, Exception und direkten Alias, aber keinen leeren typisierten Header.

Zusätzlich wurde ein separater Konsolenprozess unter `artifacts/l1-b1-probe` gegen die Core-DLL ausgeführt. Er verwendet den öffentlichen Parser und beide Runner, hält die alte Zielreferenz fest und protokolliert Objektidentität, vorherigen/nachherigen Wert, Typ, Headerdefinition, Mechanismus und Diagnostics. Elf Gegenbeispiele wurden jeweils synchron und asynchron ausgeführt; zwölf weitere Parser-/Deklarationskontrollen prüfen Recovery und Herkunft älterer Verhaltensweisen. Quelle und JSON-Ausgabe liegen dort als ignorierte Reviewartefakte, nicht als neue permanente Tests.

## 7. Abgrenzung, offene Risiken und nicht ausgeführte Prüfungen

**Bestätigte Bestandsbeobachtungen ohne zusätzliche L1-Regressionsbehauptung:**

- VAR speichert `0x(A)` und `b64(A)` als Rohtext; KEY dekodiert dagegen strikt. Die bestehende VAR-Deklarationssemantik ist ausdrücklich zu erhalten. Daraus wurde keine pauschale neue Pflicht zur Binärdekodierung sämtlicher VAR-Werte abgeleitet.
- `target=#MECH:"BOGUS"` wird im Parameterliteralpfad ebenfalls angenommen; vollständige Mechanismus-/Operationsvalidierung ist dort nicht einheitlich. B1-02 verlangt gezielt die tatsächlich vorhandene Pflichtkomponente, keinen ungefragten Umbau aller Parameterverträge.
- `Compare(Compare(0x(01),0x(01)),Compare(0x(01),0x(02)))` liefert auch als normale Deklarations-RHS „Values are equal“. `FunctionCallEvaluator.cs:298` wandelt die inneren unquotierten Vergleichstexte erneut mit `Expression.Create` um, wodurch beide leer werden. Der Ablauf ist unverändert; er ist eine bestätigte Grenze bestehender verschachtelter Ausdrücke und keine Umgehung der neuen Funktionszulassung. Ein allgemeiner Ausbau der Ausdruckssprache gehört nicht automatisch zu L1-B2.
- Direkte Benutzer der Parser-/Runner-API müssen Syntaxdiagnostics beachten; die öffentliche Runner-Überladung validiert einen angelieferten Parsebaum nicht erneut. Produktive Hosts besitzen die erforderliche Prüfung.

**Nicht vollständig verifiziert:** alle Algorithmus-/Argumentkombinationen aller zugelassenen Funktionen; ein vollständiger adversarialer Alias-Objektgraph; beliebige mutierende C#-Plugins/Testinjektionen; Langzeit-Race-Stresstests; ein formaler Nachweis gegen jede mögliche Datenoffenlegung. Für diese Punkte wird weder Fehlerfreiheit noch ein konkreter zusätzlicher Fehler behauptet.

**Nicht erneut ausgeführt:** vollständige Core- und GUI-Suiten, Release-Konfiguration, komplette Solution-Builds, manuelle Desktop-Bedienung, explizite Installed-Codex-/Native-End-to-End-Fälle und Hardwaretests. Die Angaben aus L1-A von 11.571 Core-, 800 GUI- und 176 MCP-Erfolgen jeweils für Debug und Release bleiben für die nicht erneut ausgeführten Anteile reine Angaben des Implementierungsberichts. Die unabhängige MCP-Verifikation gilt hier nur für Debug.

## 8. Empfehlung für L1-B2

B1-01 und B1-02 vor Abnahme beheben; B1-03 mit einer begrenzten Assignment-Literalprüfung mitnehmen. Die oben genannten reproduzierbaren Gegenbeispiele als permanente Tests für beide Runner aufnehmen und nach der Korrektur die betroffenen Core-/Host-Suiten erneut ausführen. Dabei muss das bisherige Zielobjekt bei Ablehnung einschließlich sämtlicher Metadaten erhalten bleiben.

Die Korrekturen sollten am neuen Assignment-Vertrag ansetzen. Eine Änderung älterer Deklarationssemantik oder der allgemeinen verschachtelten Ausdrucksauswertung wäre gesondert zu entscheiden. Weder eine zentrale Vertragsmetadatenarchitektur noch ein GUI-Umbau ist für die nachgewiesenen Korrekturen notwendig.

Es wurden keine automatischen Korrekturen vorgenommen. Die Entscheidung über L1-B2 bleibt beim Projekteigentümer.

**CHANGES REQUIRED**

