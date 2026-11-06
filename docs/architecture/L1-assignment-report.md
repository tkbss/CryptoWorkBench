# L1-A – CryptoScript Assignment

Stand: 5. November 2026. Basis: `master` bei
`7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.
Implementierung im isolierten Worktree `artifacts/worktrees/script-assignment`
auf `feature/script-assignment`. Kein Merge oder Push; der ursprüngliche
Arbeitsbereich und sein master bleiben unverändert.

## Ergebnis und Architektur

`identifier = initializer` ist ein eigenes Statement mit eigenem
`AssignmentStatementNode`, reinem ANTLR-Mapping und `AssignmentEvaluator`.
Die Initialisierungsformen verwenden die vorhandenen Regeln für Literale,
Funktionsaufrufe, Parameter und TR31-Header. Die bisherige Grammatik für
Deklarationen, insbesondere leere Deklarationen, bleibt erhalten.
Leere Assignments sind Syntaxfehler.

Der Evaluator sucht zuerst das Ziel. Er bereitet einen vollständigen Ersatz vor,
prüft CryptoScript-Typ und Rückgabevertrag, setzt Name, unveränderten deklarierten
Typ und Statement-Text und veröffentlicht erst danach mit `VariableDictionary.Update`.
Er verwendet keine publizierende Deklarationsroutine. Literal-, PATH- und
PARAM-Vorbereitung sind gemeinsame Helfer ohne Dictionary-Schreibzugriff;
Deklarationen behalten ihre bisherigen Veröffentlichungsschritte und Diagnostics.

Die Integration erfolgt einmal in `StatementEvaluator`. Beide Runnerwege nutzen
diesen Pfad unter ihren bestehenden `InterpreterExecution`-Scopes. Der asynchrone
Runner benötigt keinen eigenen Assignment-Sonderweg. Enter und MCP Lease V2
erreichen denselben Parser und Runner; produktiver GUI- und MCP-Code wurde nicht
geändert. Es gibt in dieser Basis keine separate Editor-Assignment-Grammatik oder
Syntaxhervorhebung, die angepasst werden müsste.

Typkompatibilität richtet sich nach dem CryptoScript-Typ, nicht nach identischen
C#-Variablenklassen. Anschließend werden die erforderlichen Repräsentationsverträge
geprüft: insbesondere KEY mit Größenmetadaten, Boolean-VAL, PARAM, PATH,
TR31H und SECRET. Funktionsresultate müssen vom Live-Dictionary getrennt sein;
ein zurückgelieferter Live-Alias wird vor Änderungen an Name oder Typ abgelehnt.
Erfolgreiche Zuweisungen übernehmen die vollständigen Metadaten des neuen Werts.
Ein KEY-Literal erhält dieselben Unknown-/Unspecified-Defaults wie eine Deklaration;
GenerateKey und Check liefern ihre eigenen Algorithmus-, Usage- und Attributdaten.

Neue Diagnostic-Codes wurden ausschließlich angehängt:

| Code | Wert |
|---|---:|
| AssignmentTypeMismatch | 18 |
| InvalidAssignmentInitializer | 19 |
| MissingReturnValue | 20 |

UnknownVariable, UnknownFunction, FunctionContractViolation und
FunctionExecutionFailed werden wiederverwendet. Die Werte 0–17 sind unverändert.
Diagnostiken verwenden die vorhandene ExecutionId-/StatementIndex-Zuordnung;
Funktionsdiagnostiken behalten ihre Operation-Kontexte. Unspezifische
Ausführungsfehler im Assignment werden zusätzlich ohne rohe Exception-Texte
ausgegeben. Die Redaktionsregeln anderer Statements bleiben unverändert.

## Vorab geprüfte Kompatibilitätsrisiken

1. `VariableDeclarationEvaluator` und `Expression.BuildVariable` publizierten
   während der Auswertung. Direkte Wiederverwendung hätte den alten Zielzustand
   vor Abschluss der Prüfung überschreiben können. Die extrahierten
   Prepare-Helfer beseitigen dieses Risiko für Assignments, ohne die bestehende
   Deklarationssemantik zu ersetzen.
2. CryptoScript-Typ und C#-Klasse sind nicht gleichbedeutend. Der Test mit einem
   über den produktiven Algorithmus-Dispatch gelieferten anderen VAR-Objekttyp
   schützt diesen Unterschied. Spezielle Typfamilien benötigen trotzdem ihre
   fachlichen Metadaten und Repräsentationsverträge.
3. Print/Info liefern historische Platzhalter; Snapshot- und Delete-Funktionen
   haben keinen nutzbaren Rückgabewert und können Zustand oder Dateien ändern.
   Sie werden im vollständigen Aufrufbaum vor Ausführung irgendeines Geschwisters
   abgelehnt. SecureSave/SecureLoad lösen dabei auch asynchron keinen Passwortdialog aus.
4. PIN-Block-Operationen verändern PARAM-Eingaben. Daher sind für PARAM-Ziele nur
   Parameters-Aufrufbäume zulässig. PinBlockTranslate wird direkt nur einem VAR-Ziel
   zugewiesen. Alle aktuell zugelassenen Rückgabefunktionen stehen in einer
   expliziten Liste; neue Funktionen benötigen eine Prüfung ihrer Seiteneffekte.
5. Die Garantie betrifft den Zielwert einschließlich Metadaten. Fremde PARAM-
   Änderungen und Dateiveröffentlichungen werden nicht global zurückgerollt.
   Ein echter verschachtelter Wrap-Aufruf mit anschließend fehlschlagendem Compare
   prüft diese Grenze dauerhaft.
6. Check hat bereits eine weiterlaufende Fehlersemantik bei fehlendem/mehrdeutigem
   Suchtreffer. Der bestehende KeyCheckValueSearchException-Pfad bleibt auch bei
   Assignments erhalten: Diagnose, unverändertes Ziel, folgende Anweisung läuft.
7. Die eingecheckten Parserartefakte verwenden ANTLR 4.13.2, während das
   Runtime-Paket 4.13.1 ist. Regeneriert wurde entsprechend der Projektkonvention
   mit Generator 4.13.2; Paketversionen wurden nicht verändert.

## Geänderte Dateien

Parser und Syntaxmodell:

- `CryptoScript/Antlr/CryptoScript.g4`
- `CryptoScript/Antlr/CryptoScript.interp`
- `CryptoScript/Antlr/CryptoScriptParser.cs`
- `CryptoScript/Antlr/CryptoScriptBaseListener.cs`
- `CryptoScript/Antlr/CryptoScriptListener.cs`
- `CryptoScript/Antlr/CryptoScriptBaseVisitor.cs`
- `CryptoScript/Antlr/CryptoScriptVisitor.cs`
- `CryptoScript/Model/Ast/AssignmentStatementNode.cs`
- `CryptoScript/Model/AntlrToAssignment.cs`
- `CryptoScript/Model/AntlrToStatement.cs`

Auswertung und Diagnostics:

- `CryptoScript/Model/AssignmentEvaluator.cs`
- `CryptoScript/Model/StatementEvaluator.cs`
- `CryptoScript/Model/ParameterInitializerEvaluator.cs`
- `CryptoScript/Model/PathInitializerEvaluator.cs`
- `CryptoScript/Model/VariableDeclarationEvaluator.cs`
- `CryptoScript/Expressions/Expression.cs`
- `CryptoScript/ErrorListner/SemanticDiagnosticCode.cs`

Tests und Dokumentation:

- `CryptoScriptUnitTest/AssignmentTests.cs`
- `CryptoScriptUnitTest/CwmTests.cs`
- `CryptoScriptUnitTest/SemanticDiagnosticSinkTests.cs`
- `CryptoWorkBenchGuiTest/NativeEnterContinuationTests.cs`
- `CryptoWorkBenchGuiTest/AssignmentMcpIntegrationTests.cs`
- `CryptoScript/InfoDocs/Info.Types.md`
- `docs/architecture/L1-assignment-report.md`

## Validierung

ANTLR-Regenerierung im Verzeichnis `CryptoScript/Antlr`:

```powershell
java -jar <lokaler-cache>/antlr4-4.13.2-complete.jar -Dlanguage=CSharp -visitor CryptoScript.g4
```

Abschließende Leerzeichen der generierten C#-Dateien wurden wie in den bisherigen
Projektberichten entfernt. Lexer und Token-Dateien wurden ebenfalls regeneriert,
haben aber keine inhaltlichen Änderungen.

Builds gemäß `eng/prism/README.md`, mit SDK 10.0.401, gepinntem Prism-Commit,
unveränderten Lockfiles und aktiviertem NuGet-Audit:

```powershell
pwsh -NoProfile ./eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile ./eng/prism/Build.ps1 -Configuration Release
```

Beide Konfigurationen bauten beide Lösungen erfolgreich. Der abschließende
Debug-Build hatte keine Warnungen. Der Release-Build meldete 90 Warnungen in
unveränderten Prism-/Bestandsdateien und keine Fehler; keine Warnung betrifft
die neue Assignment-Implementierung.

Die 92 zunächst ausgeführten bestehenden Deklarations-/Statement-/Initializer-
Regressionen bestanden. Die gezielte Abschlussprüfung bestand mit 64 Tests
(63 Assignment-/CWM-Fälle plus Enum-Stabilität); die drei neuen Enter-/MCP-Fälle
bestanden ebenfalls. Insgesamt wurden 66 neue Testfälle ergänzt.

Vollständige Suiten wurden mit `dotnet test <Projekt> -c <Konfiguration>
--no-build --no-restore --logger "trx;LogFileName=l1-final-<Konfiguration>.trx"`
ausgeführt. Die Zahlen sind aus den finalen TRX-Berichten abgelesen:

| Suite | Debug bestanden | Release bestanden | Fehlgeschlagen | Nicht ausgeführt je Konfiguration |
|---|---:|---:|---:|---:|
| CryptoScriptUnitTest | 11.571 | 11.571 | 0 | 27 |
| CryptoWorkBenchGuiTest | 800 | 800 | 0 | 0 |
| CryptoWorkBench.Mcp.Tests | 176 | 176 | 0 | 13 |
| Summe | 12.547 | 12.547 | 0 | 40 |

Core: 26 Plattform-/Symlink-Prüfungen übersprungen (Darwin oder fehlendes
Windows-Symlink-Recht), zusätzlich eine explizite Hardwaremessung nicht gestartet.
MCP: 13 explizite Tests gegen eine extern installierte Codex-/Desktop-Umgebung
nicht gestartet. Der CLI-Summary zählt die expliziten Tests nicht als Skips;
die Tabelle berücksichtigt alle `NotExecuted`-Einträge der TRX-Dateien.
Alle 66 neuen Testfälle wurden in beiden Konfigurationen ausgeführt und bestanden.
`git diff --check` ist ebenfalls erfolgreich.

Logs liegen im Worktree als `artifacts-l1-debug.log`, `artifacts-l1-release.log`
und `artifacts-l1-final-<core|gui|mcp>-<Debug|Release>.log`; TRX-Berichte liegen in
den jeweiligen `TestResults`-Verzeichnissen. Diese lokalen Buildartefakte sind
absichtlich nicht Teil des Quellcode-Diffs.

Die anfängliche Sandbox verhinderte den Prism-Netzwerkzugriff und den
Testhost-Verbindungsaufbau. Nach Ausführung mit den dafür nötigen Rechten
erfolgten die finalen Builds und Tests über die normalen Projektpfade.

## Einschränkungen und offene Risiken

- Die Prüfung fand unter Windows statt. Plattformabhängig übersprungene Tests
  sowie explizite externe Codex-/Hardware-Smokes sind keine bestätigten Ergebnisse.
- Neue Funktionsregistrierungen müssen ihre Rückgabe- und Mutationsverträge auch
  in `ValidateCallTree` berücksichtigen. Die explizite Zulassung verhindert eine
  unbemerkte Freigabe neuer mutierender Funktionen für Assignments.
- Vollständige Ausdrücke, direkte Variablenkopien (`a = b`), neue Scopes und
  implizite Typkonvertierungen bleiben außerhalb von L1-A.
- Fremde Seiteneffekte sind nicht transaktional. Eine erfolgreiche Ersetzung
  lässt bestehende Referenzen auf das alte Objekt bestehen, etwa bereits erzeugte
  Statement-Ergebnisse oder PARAM-Headerreferenzen. Folgeoperationen, die den
  Variablennamen neu auflösen, sehen den neuen Wert. Es gibt kein neues Persistenzmodell.
- Die bestehenden Release-Warnungen und die temporäre Prism-Integration wurden
  nicht als Teil dieser Sprachänderung verändert.

## Empfehlung für L1-B

L1-A als isolierten Sprachbaustein reviewen. Für L1-B zunächst die zentralen
Funktionsverträge um Rückgabetypen und deklarierte Mutationsziele erweitern,
damit die Assignment-Zulassung aus geprüften Metadaten abgeleitet werden kann.
Anschließend gewünschte Editorhinweise und die sichtbare Behandlung ersetzter
Variablenobjekte mit G1 abstimmen. Keine automatische Ausweitung auf Scoping,
Persistenz oder eine globale Transaktion. Vor plattformübergreifender Freigabe
die einschlägigen Linux-/macOS- und externen Desktop-Smokes nachholen.
