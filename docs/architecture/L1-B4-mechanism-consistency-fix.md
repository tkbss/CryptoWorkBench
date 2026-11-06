# L1-B4 – PARAM Mechanism Consistency Fix

Datum: 2026-11-06.

## Stand und Umfang

- Branch: `feature/script-assignment`.
- Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\script-assignment`.
- Ausgangscommit und unveränderter HEAD: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Referenz: `docs/architecture/L1-B3-independent-rereview.md`, Finding B3-01.
- Die nicht committeten L1-B2-Korrekturen bleiben erhalten. TR31H- und Integer-Guards in AssignmentEvaluator sowie die bestehenden Testdateien AssignmentTests und AssignmentCorrectionTests sind anhand vor/nach der Arbeit verglichener SHA-256-Werte unverändert.
- Neue Produktivänderung ausschließlich in [ParameterInitializerEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:24). Neu hinzu kommen [ParameterAssignmentConsistencyTests.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScriptUnitTest/ParameterAssignmentConsistencyTests.cs) und dieser Bericht.
- Keine Commits, Pushes oder Merges. Master wurde nicht bearbeitet und ist sauber; im parallelen G1-Worktree wurden keine Dateien geändert.

## Ursache und genaue Korrektur

B3-01 entstand durch zwei voneinander abweichende Mechanismusrepräsentationen: Die Auswahl des ersten MECH-Eintrags setzte `Parameter.Mechanism`, spätere Einträge überschrieben jedoch nur den Dictionarywert und damit den serialisierten Parametertext. Validatoren und Folgeoperationen konnten deshalb unterschiedliche Mechanismen sehen.

Im Modus `requireStructuredMechanism: true` gilt nun dieser Ablauf:

1. Die bereits vorhandene strukturelle Pflichtprüfung verlangt mindestens einen tatsächlichen AST-Eintrag mit `TypeName == "#MECH"`.
2. **Vor Konstruktion des neuen PARAM-Objekts** löst ResolveAssignmentMechanism sämtliche solchen Einträge über den vorhandenen ParameterEvaluator auf. Damit werden Referenzwerte nach denselben Regeln wie andere Parameterwerte gelesen; es wird nicht nach beliebigen Teilstrings im ursprünglichen Text gesucht.
3. Ein führendes `#MECH:` des aufgelösten Werts wird gemäß bestehender Selektorregel ohne Beachtung der Groß-/Kleinschreibung entfernt. Bekannte Mechanismen werden über die bestehende Registry nach ToUpperInvariant auf ihren CanonicalName abgebildet. Es gibt keine neue Registrierung und keine automatische Entquotierung.
4. Alle so bestimmten Werte müssen ordinal identisch sein. Bei einem Widerspruch bricht die Vorbereitung ab. Der vorhandene Assignment-Catch erzeugt genau eine redigierte Diagnostic `InvalidAssignmentInitializer` (19) mit dem Zielidentifier und der aktuellen Ausführungs-/Statementzuordnung.
5. Das neue PARAM erhält genau diesen effektiven Mechanismus über seinen Mechanism-Setter. Spätere MECH-Einträge werden nicht nochmals in das Dictionary geschrieben.
6. Gleichwertige MECH-Wiederholungen werden auch in ExplicitParameterNames zu einem effektiven Eintrag zusammengefasst. Andere Parametereinträge werden unverändert vorbereitet und protokolliert.
7. Vor den bestehenden fachlichen Validatoren wird nochmals geprüft, dass Mechanism und GetParameter("MECH") exakt dem effektiven Wert entsprechen. Die Validatoren verwenden somit denselben Mechanismus, der über Dictionary und BuildValue serialisiert und später veröffentlicht wird.

Die Veröffentlichung erfolgt unverändert erst im AssignmentEvaluator nach erfolgreicher Vorbereitung. Die Auflösung der Mechanismuswerte liest bestehende Variablen, verändert aber weder das alte Zielobjekt noch dessen Headerreferenz.

Der Deklarationsaufrufer aktiviert den strengen Modus weiterhin nicht. Sein historisches Verhalten mit mehreren MECH-Einträgen wurde nicht geändert.

## Entscheidung zu identischen MECH-Einträgen

Identische beziehungsweise gemäß vorhandener Normalisierung gleichwertige MECH-Einträge sind **zulässig**. Sie beschreiben einen einzigen effektiven Parameter und werden als solcher gespeichert und validiert.

Dies gilt auch für SECRET_SPLIT: Zwei identische MECH-Einträge mit gültigem PARTS-Wert sind zulässig. Die fachlichen Validatoren werden nicht abgeschaltet; sie erhalten eine normalisierte Parameterliste mit einem MECH-Eintrag.

Insbesondere bleiben folgende Fehler erhalten:

- Ein einzelner SECRET_SPLIT-Eintrag mit PARTS:1.
- Wiederholtes identisches SECRET_SPLIT mit PARTS:1.
- Doppelte Nicht-MECH-Parameter, etwa zwei PARTS-Einträge bei SECRET_SPLIT.

Die identischen MECH-Wiederholungen müssen daher nicht pauschal abgewiesen werden, um die bestehenden strengeren Mechanismusverträge aufrechtzuerhalten.

Die Normalisierung erweitert keine allgemeine Registry-Validierung: Die historische Behandlung anderer PARAM-Literalwerte wird nicht zu einer neuen globalen Mechanismus-Whitelist umgebaut. Ziel dieser Änderung ist die konsistente Auswahl und Veröffentlichung; bekannte Mechanismen werden kanonisch behandelt.

## Permanente Regressionstests

48 neue Fälle, vollständig über ParserBuilder und beide produktiven Runner:

| Gruppe | Fälle |
|---|---:|
| Abweisung widersprüchlicher/ungültiger Initialisierer einschließlich fachlicher Secret-Kontrollen | 26 |
| Erfolgreiche konsistente Assignments einschließlich normalisierter Referenzwerte und Folgeoperationen | 20 |
| Unveränderte historische Deklarationen in beiden Runnern | 2 |
| Gesamt | 48 |

Die negativen Fälle prüfen:

- AES-CBC gegen AES-ECB in beiden Reihenfolgen.
- AES-CBC gegen SECRET_SPLIT in beiden Reihenfolgen.
- HASH-SHA256 gegen HASH-SHA512 in beiden Reihenfolgen.
- Zwischenliegende IV-/PAD-Parameter, einschließlich privatem Marker.
- Widerspruch zwischen aufgelöstem Referenzwert und direktem Mechanismustoken in beiden Reihenfolgen.
- Einzelne und wiederholte identische SECRET_SPLIT-Einträge mit ungültigen Parametern sowie weiterhin verbotene doppelte PARTS-Einträge.

Jeder negative Fall beginnt mit einem PARAM mit strukturierter Headerreferenz und Header-Metadaten. Geprüft werden alte Ziel- und Typreferenz, Wert, vollständiger Zustand einschließlich Parameterdictionary, ExplicitParameterNames, OriginalExplicitPadding sowie Identität der Headerreferenz. Eine vorangestellte Anweisung wird ausgeführt, die nachfolgende wird nicht ausgeführt. Genau eine Diagnostic mit Code 19, Kategorie Assignment, Identifier target, aktueller ExecutionId und StatementIndex 2 wird verlangt. Private Marker und RHS dürfen nicht in den Diagnostics erscheinen.

Die positiven Fälle prüfen einen einzelnen MECH-Eintrag, identische Wiederholungen, unterschiedliche Positionen anderer Parameter, Hash-, Secret- und Headerfälle. Zusätzlich werden ein aufgelöster `#MECH:HASH-SHA256`-Selektor aus einem PARAM sowie ein aufgelöster kleingeschriebener Wert `hash-sha256` geprüft.

Bei jedem Erfolg stimmen Mechanism, GetParameter("MECH") und der einzige serialisierte MECH-Eintrag überein. Die Metadaten enthalten genau einen effektiven MECH-Eintrag. Die alte, separat gehaltene Zielinstanz bleibt unverändert. Folgeoperationen prüfen einen SHA-256-Wert gegen die .NET-SHA256-Berechnung beziehungsweise Share/Restore mit unverändert wiederhergestelltem Schlüssel.

## Testlauf vor und nach der Korrektur

Die neue Testdatei wurde zuerst gegen den noch fehlerhaften B2-Produktivstand ausgeführt:

- **Vorher:** 48 Fälle, davon 34 erwartete Fehler und 14 bestanden.
- **Nachher:** Alle 48 Fälle bestanden.
- **Gezielter Gesamtlauf:** 237 Assignment-, Deklarations-, Statement-, Parameter-/Header-Initializer- und zugehörige Regressionstests bestanden, 0 Fehler.

Der Vorher-Lauf weist sowohl die bisher angenommenen Widersprüche als auch die noch fehlende Normalisierung zu einem effektiven Eintrag nach. Es handelte sich um erwartete Testfehler, nicht um Buildfehler.

Die bisherigen 66 L1-A-Fälle und 32 L1-B2-Fälle bleiben erhalten. In beiden vollständigen Konfigurationen wurden insbesondere 61 AssignmentTests, die zwei CWM-Assignmentfälle, der native Enter-Fall, die zwei authentifizierten MCP-Lease-Assignmentfälle sowie alle 32 AssignmentCorrectionTests anhand der TRX-Einzelresultate als Passed geprüft. Zusammen mit B4 sind damit 146 spezifische L1-Regressionstestfälle enthalten.

## Builds und vollständige Validierung

Umgebung: Windows, .NET SDK 10.0.401. Beide Konfigurationen wurden mit dem vorhandenen Prism-Wrapper, gepinntem Prism-Checkout, unveränderten Lockfiles und aktivierter Auditprüfung gebaut:

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Release
```

Beide Aufrufe bauten die GUI-Lösung und die Core-/MCP-Lösung erfolgreich.

Danach wurden alle drei Testprojekte jeweils vollständig in Debug und Release ausgeführt:

```powershell
dotnet test <Projekt.csproj> -c <Debug|Release> --no-build --no-restore --logger "trx;LogFileName=l1-b4-full-<Debug|Release>.trx"
```

| Suite | L1-B2 bestanden je Konfiguration | B4 Debug | B4 Release | Fehler je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|---:|
| CryptoScriptUnitTest | 11.603 | 11.651 | 11.651 | 0 | 27 |
| CryptoWorkBenchGuiTest | 800 | 800 | 800 | 0 | 0 |
| CryptoWorkBench.Mcp.Tests | 176 | 176 | 176 | 0 | 13 |
| Summe | 12.579 | 12.627 | 12.627 | 0 | 40 |

Die Core-Zahl steigt exakt um die 48 neuen Fälle. Alle 48 sind sowohl in Debug als auch Release Passed.

### Warnungen und nicht ausgeführte Fälle

- Debug: 6 bekannte Warnungen in der GUI-Lösung, 0 in der zweiten Lösung.
- Release: 12 bekannte Warnungen in der GUI-Lösung einschließlich Core-Abhängigkeit, 0 in der zweiten Lösung.
- Beide Konfigurationen: 0 Buildfehler.
- Warnungszahlen entsprechen B2; der Vergleich der konkreten Warnungszeilen mit den B2-Buildlogs ergab **keine neue Warnung**. Keine betrifft die neue Konsistenzprüfung oder die Testdatei.
- Die NotExecuted-Fälle wurden je Suite und Konfiguration auch **namentlich** mit B2 verglichen: unverändert.
- Core: 26 Plattform-/Symlink-Prüfungen sowie eine explizite Hardwaremessung nicht ausgeführt. MCP: 13 explizite Installed-Codex-/Desktop-Fälle nicht ausgeführt.
- Die Tabelle zählt die TRX-Einzelresultate. Die CLI-Skipzahl lässt explizite Fälle teilweise aus und ist deshalb nicht die alleinige Grundlage.
- `git diff --check` ist erfolgreich. Git gibt lediglich den bestehenden Hinweis auf LF/CRLF-Normalisierung aus.

## Erneute separate Ausführung der B3-01-Reproduktionen

Der unveränderte temporäre Probeprozess aus B3 wurde erneut gegen die korrigierte Core-DLL ausgeführt. Die Ausgabe wurde unter einem neuen Namen gespeichert; die alten B3-Nachweise wurden nicht überschrieben. Diese Ausführung erfolgte außerhalb der permanenten NUnit-Tests.

| Reproduktion | Ergebnis in beiden Runnern |
|---|---|
| AES-CBC gegen AES-ECB sowie umgekehrte Reihenfolge | Code 19; alte Zielreferenz, Metadaten und konsistenter alter Mechanismus erhalten |
| AES-CBC gegen SECRET_SPLIT, PARTS:1, sowie umgekehrte Reihenfolge | Code 19; kein inkonsistentes PARAM veröffentlicht |
| HASH-SHA256 gegen HASH-SHA512 mit anschließendem Hash | Code 19 vor der Folgeoperation; alter AES-CBC-Zustand bleibt in Mechanism und gespeichertem MECH konsistent |
| Identischer Doppelwert AES-CBC | Erfolg; neues Objekt mit übereinstimmendem Mechanism und gespeichertem MECH |

Die erfolgreichen Hash-Folgeoperationen mit identischen beziehungsweise normalisierten HASH-SHA256-Werten sind zusätzlich durch die neuen permanenten Tests samt unabhängig berechnetem erwarteten Digest abgesichert.

## Nachweise und unveränderte Bestandsgrenzen

Lokale, ignorierte Ausführungsartefakte:

- `artifacts-l1-b4-before.log` und `CryptoScriptUnitTest/TestResults/l1-b4-before.trx`.
- `artifacts-l1-b4-targeted.log` und `CryptoScriptUnitTest/TestResults/l1-b4-targeted.trx`.
- `artifacts-l1-b4-build-<Debug|Release>.log`.
- `artifacts-l1-b4-<core|gui|mcp>-<Debug|Release>.log`.
- `<Testprojekt>/TestResults/l1-b4-full-<Debug|Release>.trx`.
- `artifacts-l1-b4-reproductions.log`.
- `artifacts/l1-b4/preserved-inputs.json` für den Nachweis unveränderter B2-Guards und bestehender Testdateien.

Unverändert bleiben die historischen Mehrfachparameterregeln von Deklarationen, die bisherigen Mechanismusregistrierungen und allgemeinen Funktionen, alle anderen Typfamilien, GUI/MCP-Koordination, Persistenz und Scoping. Eine bestehende Deklaration kann weiterhin den historisch inkonsistenten Mehrfach-MECH-Zustand erzeugen; dies ist durch explizite Kompatibilitätstests abgesichert und wird nicht als korrigierter Deklarationsvertrag ausgegeben.

Es gibt keine neue globale Transaktion fremder Seiteneffekte und keine zentrale Funktionsmetadatenarchitektur. Externe/hardwareabhängige sowie plattformbedingt nicht ausgeführte Tests sind keine bestätigten Erfolge.

B3-01 ist im strengen PARAM-Assignment-Pfad korrigiert. Alle geforderten Builds und Testläufe wurden erfolgreich durchgeführt, mit den unveränderten dokumentierten Ausnahmen. Freigabe und Commit-Entscheidung bleiben separat.

**READY FOR INDEPENDENT RE-REVIEW**

