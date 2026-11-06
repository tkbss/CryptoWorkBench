# L1-B2 – Assignment Corrections

Datum: 2026-11-06.

## Ausgangspunkt und Umfang

- Branch: `feature/script-assignment`.
- Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\script-assignment`.
- Verifizierter Ausgangscommit und weiterhin unveränderter HEAD: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Grundlage: `docs/architecture/L1-B1-independent-review.md`, Findings B1-01, B1-02 und B1-03.
- Die Korrekturen liegen als **nicht committete Änderungen** im Worktree. Alle nachfolgenden Tests wurden gegen diesen korrigierten Stand ausgeführt.
- Geändert wurden ausschließlich die beiden betroffenen Core-Evaluatoren; hinzu kommen eine permanente Testdatei und dieser Bericht. Parser, allgemeine Expression-Auswertung, Deklarationsaufrufer, GUI, MCP, Persistenz und Scoping wurden nicht umgebaut.

## B1-01 – Fachlicher TR31H-Ergebnisvertrag

**Ursache:** Die Assignment-Repräsentationsprüfung akzeptierte jedes `BlockHeaderVariableDeclaration`, auch mit leerem Inhalt oder ausschließlich einer Legacy-Anzeige und ohne strukturierte Definition.

**Änderung:** [AssignmentEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:133) verlangt für TR31H jetzt `BlockHeaderVariableDeclaration { Definition: not null }`. `Tr31HeaderDefinition` ist unveränderlich und kann regulär nur durch ihre validierende Factory erzeugt werden. Ein vorhandenes Definition-Objekt belegt damit den bestehenden fachlichen Headervertrag; eine zusätzliche Parsing-/Validierungsarchitektur ist nicht nötig.

Ein unbrauchbares Ergebnis wird vor Änderung von Name, Typ, Text und Dictionary mit `FunctionContractViolation` (Code 4), Kategorie `Assignment`, abgewiesen. Die bestehende Statement-Diagnosezuordnung bleibt erhalten. Sowohl der Mehrargument-Platzhalter als auch die einargumentige Legacy-Anzeige werden abgelehnt.

**Vorher/Nachher:**

```cryptoscript
TR31H target={KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;}
target=BlockHeader(WRAP-AES-TR31,0x(01))
```

Vorher: Erfolg ohne Diagnostic; der validierte Header wurde durch einen leeren Header ohne Definition ersetzt. Nachher: SemanticErrorException mit Code 4; dieselbe alte Zielreferenz und dieselbe Definition bleiben erhalten. `target=BlockHeader(WRAP-AES-TR31)` wird entsprechend abgewiesen.

**Permanente Regressionstests:** Beide abgelehnten Formen synchron/asynchron; das Ausgangsobjekt besitzt zusätzlich ein Optional Block Label mit einem privaten Marker. Geprüft werden Zielreferenz, Typreferenz, Wert, vollständiger serialisierter Zustand, Definitionsreferenz, genau eine Diagnostic, ExecutionId, StatementIndex und fehlende Offenlegung des Markers. Vier positive Fälle prüfen Headerliteral und CreateHeader in beiden Runnern sowie die anschließende Verwendung des neuen Headers durch `#BLKH:target`.

**Kompatibilität und verbleibende Grenze:** Die BlockHeader-Factory und ihre Deklarationsaufrufer bleiben unverändert. Zwei zusätzliche Kompatibilitätstests sichern das bisherige leere beziehungsweise angezeigte Deklarationsergebnis ausdrücklich ab. Die strengere Anforderung gilt für Assignments.

## B1-02 – Tatsächlicher #MECH-Eintrag

**Ursache:** Der gemeinsame PARAM-Helfer suchte mit `RawText.Contains("MECH")`. Ein Fremdparameterwert konnte dadurch als Mechanismusdeklaration dienen. Auch ein später vorhandenes echtes `#MECH` verhinderte die falsche Auswahl des ersten Texttreffers nicht zuverlässig.

**Änderung:** [ParameterInitializerEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:12) erhält die optionale Einstellung `requireStructuredMechanism`, standardmäßig `false`. Ausschließlich [AssignmentEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:65) aktiviert sie.

Im strengen Pfad wird anhand von `ParameterInitializerNode.TypeName == "#MECH"` gesucht und `RawValue` für den Mechanismus verwendet. Fehlt der tatsächliche Eintrag, führt die bestehende Assignment-Fehlerbehandlung zu `InvalidAssignmentInitializer` (Code 19) mit einer generischen, redigierten Meldung. Die Vorbereitung veröffentlicht weiterhin nichts selbst.

**Vorher/Nachher:**

```cryptoscript
PARAM target=#MECH:AES-CBC
target=#IV:"MECH"
```

Vorher: Veröffentlichung eines neuen PARAM mit erfundenem Mechanismus `"MECH"`. Nachher: Code 19, keine Ersetzung oder Mutation des alten PARAM.

**Permanente Regressionstests:** Sechs negative Fälle: MECH in IV, in PAD und als Teil eines privaten IV-Werts, jeweils in beiden Runnern. Das Ausgangs-PARAM besitzt eine strukturierte Headerreferenz und explizite Parameter-Metadaten. Zusätzlich zum vollständigen öffentlichen Zustand werden Parameterdictionary, ExplicitParameterNames und OriginalExplicitPadding im Snapshot erfasst; die Headerreferenz wird separat auf Identität geprüft. Diagnostic-Anzahl, Code, Quellzuordnung und Redigierung werden geprüft.

Vier positive Fälle prüfen normale PARAM-Assignments und `#IV:"MECH" #MECH:AES-CBC`. Auch wenn der irreführende Wert zuerst steht, stimmen Mechanism, tatsächlicher MECH-Parameter und explizite Parameterliste mit dem strukturierten Eintrag überein.

**Kompatibilität und verbleibende Grenze:** Die bisherige Deklarationsauswertung verwendet den unveränderten Standardpfad. Das bekannte Altverhalten `PARAM legacy=#IV:"MECH"` ist durch Kompatibilitätstests gesichert. Die Korrektur führt weder eine allgemeine Mechanismus-Registry-Prüfung noch neue Regeln für sämtliche Parameterwerte ein.

## B1-03 – Numerische Bereichsprüfung

**Ursache:** Die Grammatik akzeptiert beliebig lange INT-Ziffernfolgen. `Expression.Create` erkennt Zahlen dagegen mit `int.TryParse` und fällt bei Überlauf auf einen leeren String zurück.

**Änderung:** [AssignmentEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:73) prüft vor `Expression.Create`, ob der Literaltext ausschließlich ASCII-Ziffern enthält. Solche numerischen Literale müssen mit `int.TryParse`, `NumberStyles.None` und `InvariantCulture` im Int32-Bereich liegen. Andernfalls entsteht `InvalidAssignmentInitializer` (Code 19), ohne den Literalinhalt in der Meldung auszugeben.

**Vorher/Nachher:**

```cryptoscript
VAR target="old"
target=2147483648
```

Vorher: Erfolg und Ersetzung durch `""`. Nachher: Code 19; Zielreferenz, Wert, Typ und Metadaten bleiben erhalten.

**Permanente Regressionstests:** Vier negative Fälle mit `2147483648` beziehungsweise 1.024 Neunen, jeweils synchron/asynchron. Acht positive Fälle prüfen `0`, `2147483647`, dieselbe gültige Zahl mit führenden Nullen und den quotierten Text `"2147483648"`. Die positive Prüfung umfasst den unveränderten Literalinhalt und eine nachfolgende Compare-Operation.

**Kompatibilität und verbleibende Grenze:** `Expression.Create` wurde nicht verändert. Die alten Deklarationen großer Zahlen liefern weiterhin ihren bisherigen leeren String; beide Runner werden darauf geprüft. Es gibt keine Erweiterung der Zahlentypen, der Syntax oder der allgemeinen verschachtelten Ausdrucksauswertung.

## Permanente Tests und Wirksamkeitsnachweis

Neue Datei: [AssignmentCorrectionTests.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScriptUnitTest/AssignmentCorrectionTests.cs).

Alle Fälle benutzen ParserBuilder und den produktiven synchronen beziehungsweise asynchronen CryptoScriptRunner. Es werden keine Factory-Rückgaben simuliert.

| Testgruppe | Neue Fälle |
|---|---:|
| Abgewiesene Header-, PARAM- und Zahleninitialisierer | 14 |
| Gültiges Headerliteral und CreateHeader mit nachfolgender Headerverwendung | 4 |
| Tatsächlicher MECH-Eintrag einschließlich irreführendem vorangestelltem Wert | 4 |
| Gültige Zahlengrenzen, führende Nullen und quotierter Text | 8 |
| Unveränderte Legacy-Deklarationen für alle drei Bereiche | 2 |
| Gesamt | 32 |

Die neuen Tests wurden **vor** der Produktivkorrektur ausgeführt: 16 erwartete Fehler und 16 erfolgreiche Fälle. Die Fehler bestanden aus den 14 unerkannt angenommenen ungültigen Initialisierern sowie den zwei Fällen mit falsch ausgewähltem Mechanismus trotz vorhandenem echten Eintrag. Es waren Testfehler, keine Buildfehler.

Nach der Korrektur bestanden alle 32 neuen Fälle. Der zusätzliche gezielte Lauf mit bestehenden Assignment-, CWM-, Deklarations-, Statement- und Initializer-Tests bestand mit **189 Tests, 0 Fehlern**. Die vollständigen Suiten enthalten darüber hinaus sämtliche bestehenden Deklarations- und Assignment-Tests.

Jeder negative neue Test prüft insbesondere:

- Identität des alten Zielobjekts und seines CryptoScript-Typs, Wert und Metadaten.
- Erhalt der jeweiligen Header-/Definitionsreferenz.
- Genau eine strukturierte Diagnostic mit erwartetem Code, Kategorie Assignment und Zielidentifier.
- ExecutionId des ausführenden Runners und StatementIndex 2 nach einer vorhergehenden Anweisung.
- Keine Ausführung der nachfolgenden Anweisung und keine Offenlegung der privaten Marker oder der RHS in den Diagnostics.

Die Erfolgstests prüfen die vollständige Ersetzung bei unverändertem Namen und Typ sowie die Unversehrtheit des separat gehaltenen alten Objekts.

## Tatsächlich ausgeführte Builds und vollständige Suiten

Umgebung: Windows, .NET SDK 10.0.401. Die Builds liefen mit dem vorhandenen Prism-Wrapper, gepinntem Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`, unveränderten Lockfiles und aktivierter NuGet-Auditprüfung.

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Release
```

Beide Aufrufe bauten beide Lösungen erfolgreich:
`CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` und `CryptoWorkBench.sln`.

Danach wurde jedes der drei Testprojekte vollständig in beiden Konfigurationen ausgeführt:

```powershell
dotnet test <Projekt.csproj> -c <Debug|Release> --no-build --no-restore --logger "trx;LogFileName=l1-b2-full-<Debug|Release>.trx"
```

| Suite | Debug bestanden | Release bestanden | Fehlgeschlagen je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|
| CryptoScriptUnitTest | 11.603 | 11.603 | 0 | 27 |
| CryptoWorkBenchGuiTest | 800 | 800 | 0 | 0 |
| CryptoWorkBench.Mcp.Tests | 176 | 176 | 0 | 13 |
| Summe | 12.579 | 12.579 | 0 | 40 |

Die Core-Zahl steigt gegenüber L1-A exakt um die **32 neuen Tests** von 11.571 auf 11.603. Alle 32 wurden anhand der TRX-Testdefinitionen und Einzelresultate für Debug und Release als Passed verifiziert. GUI und MCP entsprechen zahlenmäßig L1-A.

### Warnungen, Fehler und Skips

- Debug: 6 Build-Warnungen in der GUI-Lösung, 0 in der Core-/MCP-Lösung; 0 Buildfehler.
- Release: 12 Build-Warnungen in der GUI-Lösung einschließlich Core-Abhängigkeit, 0 in der zweiten Lösung; 0 Buildfehler.
- Die sechs Debug-Warnungen betreffen unveränderte GUI-/GUI-Testdateien: CS8618, AVA2001 und CS8619. Release enthält zusätzlich sechs bestehende CS8603-Warnungen in VariableDictionary, BlockHeaderVariableDeclaration und Tr31Header.
- Jede konkrete Warnungszeile wurde mit den vorhandenen L1-A-Debug-/Release-Buildlogs verglichen; **keine neue Warnungszeile**. Keine Warnung betrifft einen geänderten Evaluator oder die neuen Tests.
- L1-A dokumentierte zuletzt 0 Debug- und 90 Release-Warnungen. Die hier ausgeführten Builds sind inkrementell; die unterschiedlichen Summen bedeuten keine Beseitigung alter Warnungen.
- **Keine veränderten Skips:** Die Namen sämtlicher NotExecuted-Testfälle stimmen je Suite und Konfiguration mit den L1-A-TRX-Dateien überein.
- Core: unverändert 26 Plattform-/Symlink-Fälle sowie eine explizite Hardwaremessung nicht ausgeführt. MCP: unverändert 13 explizite Installed-Codex-/Desktop-Fälle nicht ausgeführt. Diese Fälle werden nicht als bestanden gezählt.
- Die CLI-Zusammenfassungen berücksichtigen explizite Fälle nicht vollständig. Die Tabelle verwendet deshalb die tatsächlichen TRX-Einzelresultate, nicht allein die CLI-Skipzahl.
- `git diff --check` ist erfolgreich. Git weist lediglich auf die konfigurierte LF/CRLF-Normalisierung hin; dies ist keine Compilerwarnung.

## Erneute Ausführung der ursprünglichen Review-Reproduktionen

Der separate Probeprozess aus L1-B1 wurde erneut gegen die korrigierte Debug-Core-DLL ausgeführt. Die alten Probe-Ergebnisse wurden nicht überschrieben; die neue Ausgabe liegt in `artifacts-l1-b2-review-reproductions.log`.

| Originalfall | Synchron | Asynchron | Zielzustand danach |
|---|---|---|---|
| Gültiger Header, danach BlockHeader mit zusätzlichem Argument | FunctionContractViolation (4) | FunctionContractViolation (4) | Dieselbe Zielreferenz, gleicher Headerwert |
| PARAM AES-CBC, danach nur `#IV:"MECH"` | InvalidAssignmentInitializer (19) | InvalidAssignmentInitializer (19) | Dieselbe Zielreferenz, gleicher Mechanismus und Wert |
| VAR `"old"`, danach `2147483648` | InvalidAssignmentInitializer (19) | InvalidAssignmentInitializer (19) | Dieselbe Zielreferenz, Wert weiterhin `"old"` |
| Zusätzlich: einargumentiger Legacy-BlockHeader | FunctionContractViolation (4) | FunctionContractViolation (4) | Dieselbe Zielreferenz, gleicher Headerwert |

Jede dieser Ausführungen hatte null Syntaxfehler und genau eine Semantic Diagnostic mit der jeweiligen ExecutionId und StatementIndex 1 des separat ausgeführten Assignments.

## Nachweisdateien

Alle Laufartefakte liegen lokal im Worktree und sind ignorierte Build-/Testartefakte:

- `artifacts-l1-b2-before.log` und `CryptoScriptUnitTest/TestResults/l1-b2-before.trx`.
- `artifacts-l1-b2-targeted.log` und `CryptoScriptUnitTest/TestResults/l1-b2-targeted.trx`.
- `artifacts-l1-b2-build-Debug.log` und `artifacts-l1-b2-build-Release.log`.
- `artifacts-l1-b2-<core|gui|mcp>-<Debug|Release>.log`.
- `<Testprojekt>/TestResults/l1-b2-full-<Debug|Release>.trx`.
- `artifacts-l1-b2-review-reproductions.log`.

## Abschließende Abgrenzung

Alle drei bestätigten Findings sind korrigiert und durch permanente positive und negative Tests abgesichert. Die geforderten Build- und vollständigen Testläufe wurden erfolgreich ausgeführt; die unveränderten plattformabhängigen und expliziten Ausnahmen sind oben ausgewiesen. Es wurde keine weitere Architektur eingeführt.

Die bestehenden Grenzen aus L1-A bleiben bestehen: keine globale Transaktion fremder Seiteneffekte, keine Neudeklaration durch Assignment und kein automatisches Umbinden alter Objektreferenzen. Die historische Deklarationssemantik bleibt absichtlich erhalten. Andere Betriebssysteme sowie die übersprungenen externen/hardwareabhängigen Fälle wurden nicht zusätzlich validiert.

Es wurden keine Commits, Pushes oder Merges durchgeführt. `master` bleibt sauber auf `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Im parallelen G1-Worktree wurden ausschließlich Status/HEAD gelesen; dort währenddessen sichtbare unabhängige GUI-Änderungen wurden weder bearbeitet noch zurückgesetzt. Der vorhandene L1-B1-Bericht wurde ebenfalls nicht verändert.

**READY FOR INDEPENDENT RE-REVIEW**

