# L1-B3 – Independent Re-Review

Datum: 2026-11-06.

## 1. Tatsächlich geprüfter Stand

Geprüft wurde der **aktuelle, nicht committete Worktree**, nicht nur HEAD:

- Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\script-assignment`.
- Branch: `feature/script-assignment`.
- Verifizierter HEAD/Basiscommit: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Tracked Diff: `AssignmentEvaluator.cs` +10/−2 Zeilen; `ParameterInitializerEvaluator.cs` +6/−3 Zeilen.
- Zusätzlich vollständig gelesen und ausgeführt: die neue, noch untracked Testdatei `CryptoScriptUnitTest/AssignmentCorrectionTests.cs` mit 190 Zeilen und 32 Testfällen.
- Die ebenfalls untracked Berichte L1-B1 und L1-B2 wurden als Referenz berücksichtigt. Ihre Resultate wurden von den nachfolgend dokumentierten eigenen B3-Ausführungen getrennt.

Für die drei geprüften Code-/Testdateien wurden vor der Ausführung SHA-256-Werte erfasst:

| Datei | SHA-256 |
|---|---|
| CryptoScript/Model/AssignmentEvaluator.cs | `8B435DBAA804D07CBB3DE98D6D3D5A62E1147EF7FA33B8355CAFE2D7F5AA831F` |
| CryptoScript/Model/ParameterInitializerEvaluator.cs | `315C3FD03FFF818D4D6F9CC0189ABE73EB6D1C02AA0320A3A3F4B28B7013666D` |
| CryptoScriptUnitTest/AssignmentCorrectionTests.cs | `9B60908E5621A25E43945D48AA36E05323108206060206865F4BB21D2CB80CB6` |

Das vollständige Eingangsmanifest einschließlich beider Referenzberichte liegt in `artifacts/l1-b3-probe/input-hashes.json`. Der Abschlussabgleich bestätigt unveränderte Eingangsdateien. Es wurden keine Produktivdateien oder permanenten Tests geändert, keine Commits, Pushes oder Merges vorgenommen. Ergänzt wurden ausschließlich dieser Bericht und ignorierte temporäre Review-/Ausführungsartefakte.

## 2. Ergebnis je Finding

| Finding | Befund | Begründung |
|---|---|---|
| B1-01 – TR31H | **FIXED** | Platzhalter und Legacy-Anzeigeobjekte ohne Definition werden vor Veröffentlichung abgewiesen. Gültige Definitionen besitzen den erforderlichen Strukturvertrag. |
| B1-02 – PARAM/#MECH | **NOT FIXED** im vollständigen B3-Prüfumfang | Die ursprüngliche Fehlannahme ohne echten #MECH ist behoben. Bei den ausdrücklich zusätzlich zu prüfenden Mehrfachvorkommen bleibt die Mechanismuswahl jedoch inkonsistent; siehe neues Finding B3-01. |
| B1-03 – Zahlen | **FIXED** | Bereichsprüfung verhindert den leeren Fallback bei numerischen Assignments. Gültige Grenzen, führende Nullen und quotierter Text funktionieren in den geprüften Kulturen. |

Die Einstufung von B1-02 bedeutet ausdrücklich **nicht**, dass die ursprüngliche Reproduktion `target=#IV:"MECH"` weiterhin angenommen wird. Sie wird korrekt abgewiesen. Der Abnahmeblocker ist der nachgewiesene zusätzliche Mehrfachfall innerhalb des angeforderten PARAM-Re-Reviews.

### B1-01: Definition als fachlicher Ergebnisvertrag

`Tr31HeaderDefinition` ist sealed, besitzt einen privaten Konstruktor und wird regulär nur über `Create` erzeugt. Diese Factory prüft unter anderem Pflichtfelder, doppelte Attribute, Attributkombinationen, optionale Blöcke, Längen und Padding. Die Attributsammlung umschließt ein privat angelegtes Dictionary; optionale Blöcke werden kopiert und nach außen nur als defensive Kopien ausgegeben.

Eine nicht-null Definition ist deshalb im regulären Scriptpfad mehr als ein Klassenmarker. Noch nicht aufgelöstes KBLEN ist Teil des bestehenden Headervertrags und wird erst beim Wrap mit dem konkreten Inhalt abgeglichen; daraus folgt keine ungültige Definition.

Der Abgleich aller regulären Erzeugungsstellen ergab:

- Headerliteral und CreateHeader führen zur validierenden Factory.
- BlockHeader sowie die Basisimplementierungen können leere/Legacy-Objekte liefern; die zentrale Assignment-Ergebnisprüfung weist sie jetzt ab.
- HeaderSnapshotCodec rekonstruiert strukturierte Definitionen ebenfalls über die Factory und prüft normalisierte Konsistenz. Load/SecureLoad sind weiterhin keine zulässigen Assignment-Funktionen.
- Jeder zugelassene Funktions-RHS durchläuft vor dem Commit die Script-Typ- und Repräsentationsprüfung. Es wurde kein alternativer Scriptpfad für ein unvalidiertes TR31H zum Assignment-Commit gefunden.

Die eigenen Proben bestätigen Mehrargument-Platzhalter, einargumentige Anzeige und alternative Groß-/Kleinschreibung als abgewiesene Fälle. Headerliteral und CreateHeader funktionieren; doppelte CreateHeader-Attribute scheitern bereits in der Factory. Eine fremde VAR-Rückgabe für TR31H wird mit AssignmentTypeMismatch abgelehnt.

### B1-02: Strukturelle Prüfung und verbleibende Inkonsistenz

Der strenge Modus prüft tatsächlich `TypeName == "#MECH"` und übernimmt `RawValue`. Er wird ausschließlich vom Assignment-Aufrufer aktiviert; der Deklarationsaufrufer verwendet weiterhin den Standardwert false.

Die Reihenfolge eines echten MECH-Eintrags gegenüber irreführenden IV-/PAD-Werten ist damit korrigiert. Fehlt der Eintrag, entsteht Code 19 mit Identifier target; alte Zielidentität, Parameterwerte und Headerreferenz bleiben erhalten.

Allerdings erlaubt `declareparam+` mehrere echte MECH-Einträge. Die bestehende AST-Testdatei enthält ausdrücklich eine Parameterliste mit zwei verschiedenen MECH-Werten und bewahrt beide samt Reihenfolge. Die Syntax selbst stellt also keine Eindeutigkeit her. Die neue Suche wählt den ersten Eintrag, während die nachfolgende Schleife alle Einträge in ein Dictionary schreibt. B3-01 beschreibt die daraus entstehende, tatsächlich veröffentlichte Inkonsistenz.

### B1-03: Zahlen, Kultur und bestehende Syntax

Die neue Prüfung verwendet ASCII-Ziffern, NumberStyles.None und InvariantCulture. Das passt zu `INT: [0-9]+`. Negative Zahlen einschließlich −2147483648 sind in dieser Grammatik weiterhin keine zulässigen INT-Literale; die Korrektur erweitert die Sprache nicht.

Eigene Proben wurden jeweils synchron und asynchron in en-US, de-CH, ar-SA und tr-TR ausgeführt:

| Kategorie | Werte | Ergebnis |
|---|---|---|
| Gültige numerische Literale | 0, 2147483647, 0002147483647 | Exakter Literaltext erhalten |
| Sehr lange, aber gültige Schreibweise | 4.096 Nullen; 4.096 Nullen vor 2147483647 | Angenommen, kein leerer Fallback |
| Außerhalb Int32 | 2147483648; 4.096 Nullen davor; 4.096 Neunen | Code 19, altes Ziel vollständig erhalten |
| Quotierter Text | "2147483648" | Als Text unverändert angenommen |
| Weiterhin unzulässige Syntax | −2147483648, arabisch-indische Ziffern, gruppierte Zahl mit Kommata | Syntaxdiagnostics; keine Runner-Ausführung im Probeprozess |

Das ergibt 96 numerische/kulturelle Durchläufe. Alle entsprachen der jeweiligen erwarteten Kategorie. Der nachfolgende ältere Expression.Create-Aufruf verwendet zwar weiterhin die aktuelle Kultur, erzeugte bei den gültigen ASCII-Ziffernfolgen in keiner der vier geprüften Kulturen einen leeren Fallback. Es wird kein formaler Nachweis für beliebig manipulierte benutzerdefinierte CultureInfo-Objekte behauptet.

KEY- und PATH-Ziele konnten numerische Literale bereits vorher nicht erfolgreich verwenden. Die vorgeschaltete Bereichsprüfung macht daher in diesen Familien keine zuvor gültige Zahlzuweisung ungültig. Quotierte Strings, HEX, Base64 und Pfadtexte werden nicht als reine Ziffernfolgen behandelt.

## 3. Neues bestätigtes Finding

### B3-01 — High — Widersprüchliche #MECH-Einträge veröffentlichen einen inkonsistenten PARAM und umgehen dessen mechanismusspezifische Prüfung

**Primärort:** [ParameterInitializerEvaluator.cs:15](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:15), Zeilen 15–26 und 27–36 sowie 49–56.

**Mitwirkender Bestandscode:** `ParameterVariableDeclaration.cs`, Zeilen 103–106 und 143–160: Mechanism besitzt ein eigenes Feld; SetParameter aktualisiert das Dictionary, aber nicht dieses Feld. Die Veröffentlichung erfolgt anschließend über `AssignmentEvaluator.cs:92`.

**Reproduktion 1 – Widerspruch im veröffentlichten Ergebnis:**

```cryptoscript
PARAM target=#MECH:AES-ECB
target=#MECH:AES-CBC #MECH:AES-ECB
```

Synchron und asynchron: keine Syntaxfehler, keine Exception und keine Diagnostic. Die alte Zielreferenz wird durch ein neues PARAM ersetzt. Danach gilt:

```text
target.Mechanism             = AES-CBC
target.GetParameter("MECH")  = AES-ECB
target.Value                = #MECH:AES-ECB
```

Bei umgekehrter Reihenfolge entsteht entsprechend der entgegengesetzte Widerspruch. Zwei identische AES-CBC-Einträge bleiben konsistent; das ist ein separat geprüfter Kontrollfall.

**Reproduktion 2 – Auswahl des falschen Validierungszweigs:**

```cryptoscript
PARAM target=#MECH:AES-ECB
target=#MECH:AES-CBC #MECH:SECRET_SPLIT #PARTS:1
```

Das Assignment veröffentlicht ohne Diagnostic:

```text
Mechanism = AES-CBC
Value     = #MECH:SECRET_SPLIT#PARTS:1
```

Die Secret-Prüfung wird anhand des ersten Mechanismus nicht aufgerufen. Dagegen wird `target=#MECH:SECRET_SPLIT #PARTS:1` mit Code 19 abgewiesen. Auch die umgekehrte widersprüchliche Liste mit SECRET_SPLIT zuerst scheitert. Dies belegt eine reihenfolgeabhängige Umgehung der Vorbereitungskontrolle, nicht nur einen kosmetischen Unterschied im Text.

**Beobachtbare Folgeoperation:**

```cryptoscript
PARAM target=#MECH:AES-CBC
target=#MECH:HASH-SHA256 #MECH:HASH-SHA512
VAR observed=Hash(target,0x(01))
```

Beide Runner erzeugen erfolgreich einen 64-Byte-Hashwert, während `target.Mechanism` HASH-SHA256 enthält und Dictionary/Value HASH-SHA512 nennen. Der in-memory Hash-Pfad rekonstruiert aus dem übergebenen Parametertext; andere typisierte Dispatch-Pfade lesen unmittelbar SourceVariable.Mechanism. Es existiert damit keine einheitliche Mechanismusidentität des veröffentlichten PARAM.

**Technische Ursache:** FirstOrDefault legt das separate Mechanism-Feld anhand des ersten strukturierten Eintrags fest. SetParameter überschreibt den Dictionaryeintrag bei späteren Vorkommen. Die nachgeschalteten Validatoren verwenden weiterhin das erste Feld. Weder eindeutige Auswahl noch Konsistenzprüfung findet vor Update statt.

**Betroffene Anforderungen:** B3 Abschnitt 2 (Mehrfachvorkommen, Reihenfolge, korrekte Mechanismuswahl) und Abschnitt 4 (vollständige Validierung vor Update), außerdem der bestehende L1-PARAM-Vertrag. High wegen Veröffentlichung eines fachlich inkonsistenten Zustands unter Umgehung vorgesehener Parameterprüfung. Ein weitergehender Schlüsselverlust oder kryptografischer Angriff wurde nicht nachgewiesen und wird nicht behauptet.

**Herkunft:** Die Kontrollprobe einer normalen Deklaration mit widersprüchlichen MECH-Einträgen zeigt denselben alten Fehler. B1-02 hat ihn nicht neu erzeugt. Er bleibt aber im ausdrücklich zu prüfenden Assignment-Pfad bestehen; die verlangte Deklarationskompatibilität erfordert nicht, diesen ungültigen Ersatz eines vorhandenen Ziels ebenfalls zuzulassen.

**Minimale Korrekturempfehlung:** Im strengen Assignment-Modus mindestens widersprüchliche tatsächliche MECH-Einträge vor Veröffentlichung mit InvalidAssignmentInitializer ablehnen. Falls die Sprache stattdessen eine eindeutige Überschreibregel vorsehen soll, müssen Mechanism, Dictionary/Value und die abschließende fachliche Validierung denselben endgültigen Mechanismus verwenden. Identische Wiederholungen sind gemäß den jeweiligen vorhandenen Mechanismusregeln bewusst zu behandeln. Der alte Deklarationspfad kann unverändert bleiben; keine zentrale Metadatenarchitektur ist nötig.

**Erforderliche permanente Regressionen:** Beide widersprüchlichen Reihenfolgen, die Secret-Validierungsfälle und ein identischer Doppelwert in beiden Runnern. Bei Ablehnung müssen alte Zielreferenz, Typ, Werte, Metadaten und Headerreferenzen erhalten bleiben; genau eine redigierte Diagnostic mit Identifier, ExecutionId und StatementIndex prüfen. Bei erlaubten Ergebnissen muss Mechanism dem gespeicherten MECH-Wert entsprechen, einschließlich der Folgeoperation.

## 4. Übergreifende Zustandsintegrität und Diagnostics

Die drei B2-Guards liegen vor der Veröffentlichung. Headerdefinitionen werden auf dem vorbereiteten Ergebnis geprüft, PARAM entsteht in einem neuen Objekt, und die Zahlenprüfung erfolgt vor Expression.Create. Die alten Zielobjekte werden in diesen Wegen nicht vorab verändert. Script-Typbindung, Live-Alias-Prüfung und InterpreterExecution bleiben unverändert.

Die ursprünglichen drei Reproduktionen wurden im separaten Probeprozess über ParserBuilder und beide Runner erneut ausgeführt, jeweils nach einer vorangestellten Anweisung:

| Originalfall | Code | Zielreferenz und Metadaten | Zuordnung |
|---|---|---|---|
| Leerer BlockHeader | FunctionContractViolation (4) | Erhalten | Identifier target; StatementIndex 2; aktuelle ExecutionId |
| Nur #IV:"MECH" | InvalidAssignmentInitializer (19) | Erhalten | Identifier target; StatementIndex 2; aktuelle ExecutionId |
| 2147483648 | InvalidAssignmentInitializer (19) | Erhalten | Identifier target; StatementIndex 2; aktuelle ExecutionId |

Jeweils genau eine Diagnostic, keine Offenlegung der eingesetzten privaten Marker und gleiche Semantik in beiden Runnern. Die permanenten B2-Tests prüfen zusätzlich interne PARAM-Metadaten und die Identität referenzierter Header.

Abgrenzung bei Fehlern bereits innerhalb einer Funktion: Ein CreateHeader-Aufruf mit doppeltem KEYU scheitert vor dem neuen Ergebnisguard mit der bestehenden FunctionExecutionFailed-Diagnostic. ExecutionId und StatementIndex stimmen, Identifier ist dabei weiterhin leer; die Zuordnung erfolgt über die Funktions-/Operationsdaten. Zwei temporäre Proben markierten deshalb ihre zunächst pauschal erwartete Ziel-Identifier-Bedingung als nicht erfüllt. Das ist der unveränderte Funktionsdiagnosepfad, kein neuer Fehler des B2-Guards. Die neuen 32 Tests decken diesen Unterschied nicht ausdrücklich ab.

Für neue Aliasprobleme oder mutierte alte Ziele durch die B2-Guards wurde kein weiterer Gegenbeweis gefunden. B3-01 ist ein Fehler der Validierung vor Veröffentlichung: Der inkonsistente RHS wird als Erfolg behandelt. Die gesonderte Unveränderlichkeit des aus dem Dictionary entfernten alten Objekts macht diesen Commit nicht korrekt.

## 5. Kritische Bewertung der 32 neuen Tests

Die Tests erfassen alle drei Originalfehler über produktive Parser-/Runnerpfade, nicht über manipulierte Rückgabeobjekte. Beide Runner, negative und positive Fälle sowie Legacy-Deklarationen sind enthalten.

Besonders aussagekräftig sind die Identitätsprüfungen von Ziel und Typ, Snapshots einschließlich interner PARAM-Metadaten, Headerreferenzen, genau eine Diagnostic, Ausführungszuordnung, Abbruch der Folgeanweisung und private Marker. Die positiven Headerfälle prüfen außerdem die nachfolgende Nutzung als BLKH.

Die wesentliche Lücke ist **mehr als ein echter MECH-Eintrag**. Die vier positiven PARAM-Fälle enthalten jeweils nur einen echten Eintrag und können B3-01 daher nicht erkennen. Weitere fehlende permanente Gegenproben sind unterschiedliche Kulturen, sehr lange führende Nullfolgen und Fehler innerhalb von CreateHeader. Die ersten beiden Bereiche wurden hier temporär geprüft und zeigten keinen zusätzlichen Fehler. Architekturvorschläge werden daraus nicht zu Abnahmevoraussetzungen gemacht.

## 6. Eigene B3-Ausführung und vorhandene B2-Evidenz

### Im Re-Review selbst ausgeführt

Windows/.NET SDK 10.0.401, Debug. Der Core-Testaufruf enthielt einen erneuten Build des aktuellen Worktrees mit --no-restore. GUI/MCP liefen mit --no-build --no-restore auf den vorhandenen B2-Debug-Builds; deren Produktivdateien wurden durch B3 nicht verändert.

| Prüfung | Ergebnis | Nachweis |
|---|---|---|
| Core: Assignment, Declaration, StatementExecution, ParameterInitializer, Tr31Header, SemanticDiagnostic und AsyncPasswordInfrastructure | 1.570 Passed, 0 Failed, keine NotExecuted | CryptoScriptUnitTest/TestResults/l1-b3-core.trx |
| GUI: AssignmentMcpIntegration, NativeEnterContinuation, LeaseV2, ExecutionPresentation, SemanticDiagnosticPresentation | 97 Passed, 0 Failed | CryptoWorkBenchGuiTest/TestResults/l1-b3-gui.trx |
| MCP: bestehende Suite | 176 Passed, 0 Failed, 13 explizite NotExecuted | CryptoWorkBench.Mcp.Tests/TestResults/l1-b3-mcp.trx |
| Neue B2-Testdatei innerhalb des Core-Laufs | Alle 32 Passed | Testdefinitionen und Einzelresultate der Core-TRX |
| Temporäre Gegenproben | 142 protokollierte Durchläufe, einschließlich 96 Zahlen-/Kulturfällen und der bestätigten Mehrfach-MECH-Fehler | artifacts/l1-b3-probe/results.jsonl |

Die temporären Proben sind **keine neuen permanenten Regressionstests**. Explore-Fälle protokollieren Ergebnisse, ohne Fehlerfreiheit zu behaupten. Die zwei oben beschriebenen Identifier-Abweichungen werden ebenfalls nicht als erfolgreiche Ziel-Identifier-Prüfung ausgegeben. Quellen und eigenes Konsolenprojekt liegen ausschließlich im ignorierten Verzeichnis artifacts/l1-b3-probe. Die B1-/B2-Probeergebnisse wurden nicht überschrieben.

### Nur als vorhandene Evidenz berücksichtigt

B2 dokumentiert erfolgreiche Debug-/Release-Builds und vollständige Suiten mit jeweils 11.603 Core-, 800 GUI- und 176 MCP-Erfolgen, 27 Core- und 13 MCP-Ausnahmen sowie 32 neuen erfolgreichen Fällen. Diese vollständigen Läufe wurden in B3 **nicht erneut ausgeführt** und werden nicht als eigene Re-Review-Ausführung gezählt. Die grünen Ergebnisse widerlegen den Mehrfach-MECH-Befund nicht, da dessen Fälle in den permanenten Tests fehlen.

## 7. Verbleibende Grenzen und Empfehlung

Bewusst unverändert bleiben Legacy-Deklarationen, allgemeine Ausdrucksauswertung, negative Zahlensyntax, Scoping, Persistenz und die fehlende globale Transaktion fremder Effekte. Keine formale Prüfung beliebiger C#-Reflection-/Plugin-Manipulationen oder aller CultureInfo-Sonderkonstruktionen; solche Möglichkeiten werden nicht als Scriptfehler klassifiziert. Plattform-/Hardware-/Installed-Codex-Prüfungen außerhalb der tatsächlich ausgeführten Läufe bleiben unbestätigt.

Für den Abschluss von L1 ist B3-01 gezielt im Assignment-Modus zu korrigieren und durch die beschriebenen permanenten Tests abzusichern. Danach ist ein begrenzter erneuter Review der Mechanismuskonsistenz samt betroffenen Regressionen erforderlich. Die TR31H- und Zahlenguards benötigen aufgrund dieses Reviews keine weitere Korrektur. Eine zentrale Funktionsmetadatenarchitektur ist weiterhin kein notwendiger Bestandteil.

Keine automatische Korrektur wurde vorgenommen. Freigabe und Commit-Entscheidung bleiben separat beim Projekteigentümer.

**CHANGES REQUIRED**

