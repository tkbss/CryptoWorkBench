# L1-B5 – Final Independent Re-Review

Datum: 2026-11-06.

## Ergebnis

**B3-01: NOT FIXED** hinsichtlich des vollständigen geforderten Mechanismuskonsistenzvertrags. Die ursprünglichen Konflikte zwischen tatsächlichen MECH-Einträgen werden jetzt korrekt abgewiesen. Ein anderer Parameter kann jedoch über seinen aufgelösten Referenzwert einen weiteren Mechanismus in die serialisierte Darstellung einschleusen. Eine anschließende Hash-Operation verwendet nachweislich einen anderen Mechanismus als die Eigenschaften des veröffentlichten PARAM. Dies ist das neue High-Finding **B5-01**.

Die drei ursprünglichen B1-Findings bleiben im jeweiligen geprüften Umfang korrigiert. Sämtliche permanenten Tests sind grün, einschließlich der unabhängig wiederholten vollständigen Debug-/Release-Suiten. Diese Resultate decken die nachfolgend nachgewiesene Lücke nicht ab.

## Verifizierter Stand und Reviewgrenzen

- Repository: `tkbss/CryptoWorkBench`.
- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment`.
- Branch: `feature/script-assignment`.
- HEAD und Basis: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Geprüft wurde ausdrücklich der Arbeitsbaum einschließlich der nicht committeten B2-/B4-Korrekturen und beider untracked Testdateien.

Git-Diff gegen HEAD zu Reviewbeginn:

| Datei | Hinzugefügte / entfernte Zeilen |
|---|---:|
| CryptoScript/Model/AssignmentEvaluator.cs | +10 / -2 |
| CryptoScript/Model/ParameterInitializerEvaluator.cs | +41 / -3 |

Bereits untracked waren `AssignmentCorrectionTests.cs`, `ParameterAssignmentConsistencyTests.cs` sowie die Berichte L1-B1, L1-B2, L1-B3 und L1-B4. Die neuen Testdateien wurden gelesen und tatsächlich mitgebaut und ausgeführt.

SHA-256 des geprüften produktiven Stands:

- AssignmentEvaluator.cs: `8B435DBAA804D07CBB3DE98D6D3D5A62E1147EF7FA33B8355CAFE2D7F5AA831F`.
- ParameterInitializerEvaluator.cs: `29FAFFB2AAED8422E54B47220CBBC500964777D957BADAFC7D8A8293A71ED7AE`.

Das Eingangsmanifest `artifacts/l1-b5-probe/input-hashes.json` umfasst beide Produktivdateien, die relevanten A-/B2-/B4- und GUI-Testdateien sowie B3-/B4-Berichte. Alle zehn Dateien sind nach der Reviewausführung unverändert. Die ursprünglichen versionierten L1-A-Testdateien haben keinen Diff gegen HEAD.

Keine Produktiv- oder permanenten Teständerungen, Korrekturen, Commits, Pushes oder Merges. Zusätzlich zu diesem Bericht wurden ausschließlich lokale ignorierte Build-/Testartefakte und temporäre Gegenproben erzeugt. Master und G1 wurden nicht bearbeitet.

## B5-01 – High: Referenzwert eines Nicht-MECH-Parameters verändert den konsumierten Mechanismus

Primäre Stelle: [ParameterInitializerEvaluator.cs:43](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:43) und dessen abschließende [Konsistenzprüfung:60](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:60).

### Reproduktion

```makefile
PATH payload="0x(00)#MECH:HASH-SHA512"
PARAM target=#MECH:HASH-SHA256
target=#MECH:HASH-SHA256 #IV:payload
VAR digest=Hash(target,0x(01))
```

Die unabhängige temporäre Probe führt die ersten beiden Deklarationen als Vorbereitung aus und anschließend das Assignment samt Hash über ParserBuilder und jeweils den synchronen bzw. asynchronen produktiven Runner. Vor und nach dem Assignment stehen zusätzliche Sentinel-Anweisungen zur Kontrolle der Runner-Semantik.

Beobachtung in **beiden Runnern**:

| Repräsentation / Beobachtung | Tatsächlicher Wert |
|---|---|
| Syntaxfehler | Keine |
| Assignment-Diagnostic | Keine |
| Zielobjekt | Durch neue PARAM-Instanz ersetzt |
| target.Mechanism | HASH-SHA256 |
| target.GetParameter("MECH") | HASH-SHA256 |
| target.Value | #MECH:HASH-SHA256#IV:0x(00)#MECH:HASH-SHA512 |
| ExplicitParameterNames | #MECH, #IV |
| Hash-Ergebnis | Exakt SHA-512 über Byte 01; nicht SHA-256 |
| Nachfolgende Sentinel-Anweisung | Ausgeführt |

Der Digest wurde gegen unabhängig berechnete `.NET SHA256.HashData` und `SHA512.HashData` verglichen; der Nachweis beruht nicht nur auf einer unterschiedlichen Ausgabelänge.

Dasselbe Ergebnis entsteht bei `target=#IV:payload #MECH:HASH-SHA256` und bei `target=#MECH:HASH-SHA256 #LABEL:payload #MECH:HASH-SHA256`. Die letzte Variante enthält zwei identische tatsächliche MECH-Einträge und umgeht dennoch den vollständigen Konsistenzvertrag. Diese Varianten wurden zusätzlich mit einem alten PARAM einschließlich strukturierter Headerreferenz und Metadaten ausgeführt. Die alte separat gehaltene Instanz bleibt unverändert, wird aber durch das inkonsistente neue Objekt ersetzt.

### Ursache und Auswirkung

1. ResolveAssignmentMechanism prüft nur tatsächliche MECH-AST-Einträge. Diese stimmen in der Reproduktion korrekt überein.
2. Der vorhandene ParameterEvaluator übernimmt bei `#IV:payload` den Wert der PATH-Referenz. Der enthaltene Text `#MECH:HASH-SHA512` ist zunächst Bestandteil dieses Werts.
3. [ParameterVariableDeclaration.BuildValue:121](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Variables/ParameterVariableDeclaration.cs:121) verkettet Parameternamen und Werte ohne eine Darstellung, die diesen eingebetteten Parametertrenner schützt.
4. Die neue Prüfung vergleicht nur Mechanism und den Dictionaryeintrag MECH mit effectiveMechanism. Beide bleiben SHA-256, daher passieren Vorbereitung, fachliche Validatoren und Veröffentlichung.
5. [CryptoOperations.Hash:82](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Operations/CryptoOperations.cs:82) reicht den serialisierten Wert für diesen In-Memory-Fall weiter. [HASH.ResolveParameter:77](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/CryptoAlgorithm/HASH/HASH.cs:77) rekonstruiert daraus den PARAM.
6. [ParameterVariableDeclaration.SetInstance:76](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Variables/ParameterVariableDeclaration.cs:76) zerlegt am Zeichen `#`; der spätere MECH-Eintrag setzt den Mechanismus auf SHA-512. Damit wird tatsächlich SHA-512 berechnet.

Dies verletzt die ausdrücklich verlangte Konsistenz zwischen Eigenschaften, serialisiertem Parameterwert, Provenienzmetadaten und Folgeoperationen. Validatoren betrachten SHA-256, während der Verbraucher SHA-512 verwendet. Eine zusätzliche Secret-Validatorumgehung wird hier nicht als separat bewiesener Fehler behauptet.

Die zugrunde liegende Serialisierung existiert bereits. Das Finding behauptet daher nicht, dass B4 sie eingeführt hat. Es zeigt, dass die neue Assignment-Vorbereitung den zugesagten Vertrag noch nicht vollständig absichert. Die Reproduktion benötigt keine inkonsistente Legacy-PARAM-Deklaration; deren ausdrücklich akzeptierte Bestandsgrenze greift hier nicht.

### Erforderliche Nachbesserung

Vor der Veröffentlichung muss der strenge Assignment-Pfad gewährleisten, dass auch die von Folgeoperationen konsumierte Darstellung denselben effektiven Mechanismus enthält. Mehrdeutige aufgelöste Parameterwerte müssen sicher abgewiesen oder konsistent repräsentiert werden. Ein bloßer Vergleich zweier Eigenschaften desselben vorbereiteten Objekts reicht nicht aus.

Dazu gehören permanente Gegenproben mit eingebettetem MECH in anderen Parameterreferenzen, beiden Reihenfolgen, identischen tatsächlichen MECH-Wiederholungen und einer anschließenden Hash-Operation. Bei Ablehnung sind Code 19, Redigierung und vollständiger Erhalt des alten Ziels erforderlich. Eine zentrale Metadatenarchitektur oder Änderung historischer Deklarationen ist für die Feststellung dieses Fehlers nicht erforderlich. Im read-only Review wurde keine Korrektur vorgenommen.

## Befund zum übrigen Mechanismusvertrag

Die direkten B3-01-Reproduktionen sind erfolgreich korrigiert:

| Unabhängige Probe, jeweils beide Reihenfolgen und Runner | Ergebnis |
|---|---|
| AES-CBC / AES-ECB | Ablehnung mit Code 19 |
| AES-CBC / SECRET_SPLIT, PARTS:1 | Ablehnung mit Code 19 vor Veröffentlichung |
| HASH-SHA256 / HASH-SHA512 mit nachfolgendem Hash | Ablehnung mit Code 19; Hash wird nicht ausgeführt |

Weitere eigene Gegenproben:

- Identische MECH-Einträge werden zu einem effektiven Eintrag zusammengeführt.
- Bekannte Registrywerte aus Referenzen, Kleinschreibung und führendes `#mech:` werden kanonisiert; umgekehrte Reihenfolgen funktionieren.
- Drei gleichwertige MECH-Einträge funktionieren; ein Konflikt am Anfang oder Ende wird abgewiesen.
- Dazwischenliegende gewöhnliche IV-/PAD-Werte verfälschen die Auswahl nicht.
- Einzelnes SECRET_SPLIT mit PARTS:1, identisches doppeltes SECRET_SPLIT mit PARTS:1 und doppelte PARTS-Einträge werden abgewiesen.
- Gültiges SECRET_SPLIT mit identischen MECH-Wiederholungen funktioniert einschließlich Share/Restore.
- Fehlende Referenzen an erster oder letzter MECH-Position ergeben eine redigierte Assignment-Diagnostic.
- Selbstreferenz als Mechanismusquelle funktioniert ohne Mutation der alten Instanz.
- Eine alte inkonsistente Legacy-Deklaration als Referenzquelle ergibt im geprüften Fall einen neuen konsistenten SHA-512-PARAM samt korrektem Hash.
- Identische unbekannte Werte, etwa `future-v1`, bleiben zulässig und konsistent. Unterschiedliche Groß-/Kleinschreibung unbekannter Werte wird nicht über die Registry normalisiert und führt bei widersprüchlichen Wiederholungen zur Ablehnung. Auch identische quotierte unbekannte Werte bleiben im bestehenden permissiven Vertrag. Daraus allein wird kein neues Finding abgeleitet.

Für sämtliche 18 negativen temporären Fälle, jeweils synchron und asynchron, wurden geprüft: genau eine Diagnostic InvalidAssignmentInitializer (19), Kategorie Assignment, Identifier target, aktuelle ExecutionId, StatementIndex 2, keine privaten Marker oder RHS-Offenlegung; dieselbe Ziel-/Typ-/Headerreferenz und unveränderte vollständige Metadaten; keine Folgeanweisung nach Fehler. Für die 14 positiven Kontrollfälle wurden neues Objekt, konsistente Mechanismusrepräsentationen, erhaltene alte Instanz und unveränderte Referenzquellen geprüft. Hash- und Share/Restore-Folgeoperationen wurden für die entsprechenden Fälle abgeglichen.

## Ursprüngliche B1-Findings und Testqualität

| Finding | Status im ursprünglichen Umfang | Eigene Evidenz |
|---|---|---|
| B1-01 TR31H | FIXED | Unveränderter Definition-Guard; beide BlockHeader-Negativfälle, Headerliteral/CreateHeader und Deklarationskontrollen in den erneut ausgeführten B2-Tests |
| B1-02 tatsächliches MECH erforderlich | FIXED | Strukturelle AST-Pflichtprüfung; irreführendes MECH in anderen Werten ohne tatsächlichen MECH wird abgewiesen; eigene Gegenprobe und B2-Tests |
| B1-03 Integerüberlauf | FIXED | Unveränderter Guard vor Expression.Create; B2-Tests einschließlich Grenzen/langer Ziffernfolge und Deklarationskontrolle bestehen |

B5-01 betrifft einen anderen Fall als das fehlende MECH von B1-02: Ein tatsächlicher gültiger MECH ist vorhanden, aber die konsumierte Serialisierung enthält zusätzlich einen widersprüchlichen Mechanismus.

Die 48 B4-Tests wurden inhaltlich geprüft: 26 negative, 20 positive und zwei Legacy-Deklarationsfälle. Ihre Zustands-, Referenz-, Diagnostic- und Folgeoperationsprüfungen sind aussagekräftig. Insbesondere prüfen die positiven Fälle bereits die Zahl serialisierter MECH-Einträge und den wirklichen SHA-256-Digest. Es fehlen jedoch Eingaben, die über einen anderen aufgelösten Parameterwert einen Trenner einschleusen. Die Assertions sind somit stärker als die Auswahl der getesteten Werte. Die temporären B5-Proben sind keine neuen permanenten Regressionstests.

Alle 146 spezifischen L1-Fälle sind in beiden vollständigen Läufen anhand der TRX-Einzelresultate Passed: 61 AssignmentTests, zwei CWM-Fälle, ein nativer Enter-Fall, zwei authentifizierte MCP-Lease-Fälle, 32 B2-Fälle und 48 B4-Fälle. Die strengere Initialisierung bleibt durch den Assignment-Aufrufer aktiviert; die historischen Deklarationskontrollen bestehen. Für GUI-/MCP-Koordination ergab sich kein neuer Befund.

## Tatsächlich eigene Ausführung

Alle folgenden Resultate stammen aus diesem B5-Review, nicht aus übernommenen B4-Testresultaten.

### Gezielte Regressionen und temporäre Gegenproben

- Core Debug mit Build: **1.618 Passed, 0 Failed, 0 NotExecuted**.
- Filter: `FullyQualifiedName~Assignment|FullyQualifiedName~VariableDeclaration|FullyQualifiedName~StatementExecution|FullyQualifiedName~ParameterInitializer|FullyQualifiedName~Tr31Header|FullyQualifiedName~SemanticDiagnostic|FullyQualifiedName~AsyncPasswordInfrastructure`.
- Neuer separater Probeprozess über produktiven Parser und beide Runner: **36 Szenarien × 2 Runner = 72 Ausführungen**.
- 32 Kontrollszenarien: 64 erwartungsgemäße Ergebnisse.
- Vier explorative Szenarien: acht bestätigte Nachweise von B5-01, einschließlich minimaler Reproduktion. Deren JSON-Feld `pass=false` ist der Default für `Expected=explore`, kein NUnit-Testfehler. Entscheidend sind `consistent=false`, `digest256=false`, `digest512=true` und leere Diagnostics.
- Der temporäre Probeprozess verwendet die frisch geprüfte Debug-Core-DLL. Die Release-Evidenz stammt aus den separat wiederholten permanenten Suiten.

### Builds und vollständige Suiten

Eigene Aufrufe des bestehenden Prism-Wrappers:

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Release
```

Beide Aufrufe erfolgreich, jeweils GUI-Lösung und Core-/MCP-Lösung, ohne Buildfehler. Anschließend jeweils:

```powershell
dotnet test <Projekt.csproj> -c <Debug|Release> --no-build --no-restore --logger "trx;LogFileName=l1-b5-full-<Debug|Release>.trx"
```

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration | Änderung zu B4 |
|---|---:|---:|---:|---:|---|
| Core | 11.651 | 11.651 | 0 | 27 | Keine |
| GUI | 800 | 800 | 0 | 0 | Keine |
| MCP | 176 | 176 | 0 | 13 | Keine |
| Gesamt | 12.627 | 12.627 | 0 | 40 | Keine |

Die NotExecuted-Namen wurden je Suite und Konfiguration gegen die vorhandenen B4-TRX verglichen: identisch. Die CLI zählt explizite Fälle teilweise nicht als Skips; die Tabelle basiert auf den einzelnen TRX-Ergebnissen.

Warnungen: Debug sechs bekannte GUI-/GUI-Testwarnungen, zweite Lösung null; Release ebenfalls sechs, zweite Lösung null. Keine neue Warnung gegenüber B4. Gegenüber den zwölf Release-Warnungen des B4-Laufs werden die sechs bekannten Core-CS8603-Warnungen hier nicht erneut ausgegeben; der Wrapper nutzt inkrementelles `dotnet build`, keinen Rebuild, und der unveränderte Core-Stand war bereits gebaut. Dies ist keine behauptete Warnungsbehebung. Die sechs tatsächlich ausgegebenen Warnungen stimmen mit B4 überein. Die Core-Quelldateien wurden nicht geändert.

### Lokale Ausführungsnachweise

Im geprüften Worktree, als ignorierte Artefakte:

- `artifacts-l1-b5-targeted.log`, Core-TestResults/`l1-b5-targeted.trx`.
- `artifacts-l1-b5-build-Debug.log`, `artifacts-l1-b5-build-Release.log`.
- `artifacts-l1-b5-<core|gui|mcp>-<Debug|Release>.log`.
- Je Testprojekt `TestResults/l1-b5-full-<Debug|Release>.trx`.
- `artifacts/l1-b5-probe/Program.cs`, `Probe.csproj`, `results.jsonl` und `input-hashes.json`.

Der temporäre Harness hat keine PackageReference und verwendet vorhandene lokale Assemblyreferenzen. Seine lokale NuGetAudit-Einstellung gilt ausschließlich für dieses Hilfsprojekt; der produktive Prism-Wrapper und seine Restore-/Auditkonfiguration wurden nicht verändert.

## Akzeptierte Grenzen und Empfehlung

Historisch inkonsistente Mehrfach-MECH-Deklarationen bleiben ausdrücklich akzeptiert. Ebenso wird keine neue globale Whitelist unbekannter Mechanismen verlangt. Externe Installed-Codex-/Desktop-, Hardware- und plattformabhängige NotExecuted-Fälle sind keine bestätigten Erfolge; ihre Namen und Anzahl sind unverändert.

Die Grenzen anderer Typfamilien, Persistenz, Scoping sowie Enter-/MCP-Koordination wurden nicht erweitert. Eine allgemeine Architekturverbesserung ist kein zusätzlicher Freigabeblocker.

Die Gesamtfreigabe von L1 wird wegen des reproduzierten B5-01 noch nicht empfohlen. Nach einer begrenzten Korrektur der Assignment-Konsistenz und permanenten Gegenproben ist ein weiterer unabhängiger Review erforderlich. Freigabe und Commit bleiben separat beauftragt.

**CHANGES REQUIRED**

