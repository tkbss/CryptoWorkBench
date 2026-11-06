# L1-B7 – Final Independent Review

Datum: 2026-11-06.

## Ergebnis

**B5-01: FIXED.** Die kombinierte B2-/B4-/B6-Absicherung stellt die geforderte Konsistenz an der Veröffentlichung eines PARAM-Assignments her. Im geprüften produktiven Datenfluss wurde kein weiterer reproduzierbarer Verstoß und keine konkrete Regression gefunden.

Die Entscheidung beruht auf Quellprüfung, neu entwickelten adversarialen Gegenproben, unabhängigen kryptografischen Vergleichen und selbst ausgeführten vollständigen Debug-/Release-Suiten. Die Berichte B5/B6 dienen als Referenz, nicht als Ersatz für diese Ausführung.

## 1. Geprüfter Git- und Worktree-Stand

- Repository: `tkbss/CryptoWorkBench`.
- Branch: `feature/script-assignment`.
- HEAD/Basis: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment`.
- Explizit geprüft: aktueller Arbeitsbaum mit nicht committeten B2-, B4- und B6-Korrekturen und allen drei untracked Regressionstestdateien.

| Produktivdatei | Gesamtdiff gegen HEAD |
|---|---:|
| CryptoScript/Model/AssignmentEvaluator.cs | +21 / -2 |
| CryptoScript/Model/ParameterInitializerEvaluator.cs | +70 / -3 |

Bereits untracked: AssignmentCorrectionTests.cs, ParameterAssignmentConsistencyTests.cs, ParameterAssignmentSerializationTests.cs und Berichte B1 bis B6. Die Tests wurden tatsächlich kompiliert und ausgeführt.

SHA-256 des geprüften Stands:

- AssignmentEvaluator.cs: `1AD72884FA3CDF9A6FA20EC026ABE8239554C348FFC3758BE0B9DC536852335D`.
- ParameterInitializerEvaluator.cs: `00B83CAC82656B2E4688DA9FDE2478316F7CB6919A0BA315C209AD5366CAA8D0`.

Das Eingangsmanifest umfasst 16 Produktiv-, Test- und Berichtsdateien. Alle bleiben bytegenau unverändert. Die ursprünglichen versionierten L1-A-Testdateien haben keinen Diff gegen HEAD. git diff --check ist erfolgreich, abgesehen vom bestehenden informativen LF-/CRLF-Hinweis.

Read-only hinsichtlich Produktivcode und permanenter Tests. Neu erstellt wurden nur dieser verlangte Bericht und ignorierte lokale Review-/Build-/Testartefakte. Keine Commits, Pushes, Merges oder Änderungen an Master/G1.

## 2. Prüfung der vollständigen Invariante

### AST und Auflösung

Der strenge Literalpfad verlangt einen tatsächlichen MECH-AST-Eintrag. [ResolveAssignmentMechanism](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:85) löst jeden solchen Eintrag über den vorhandenen ParameterEvaluator auf. Ein unterstützter führender `#MECH:`-Selektor wird entfernt; danach sind weitere rohe Trenner unzulässig. Bekannte Mechanismen werden über die Registry kanonisiert, mehrere Einträge müssen ordinal denselben effektiven Mechanismus ergeben.

Alle anderen aufgelösten Werte passieren [ValidateAssignmentValue](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:108) vor SetParameter. Das gilt auch dann, wenn eine spätere Wiederholung denselben Parameter überschreiben würde. Dadurch können nicht nur MECH, sondern auch andere Namen oder wertlose Fragmente nicht eingeschleust werden.

Parameternamen kommen aus der bestehenden Sprache und werden durch SetParameter gegen die vorhandene Liste geprüft und kanonisiert. Das vorbereitete Dictionary enthält damit eindeutige kanonische Namen. Die B4-Regel reduziert gleichwertige tatsächliche MECH-Wiederholungen auf einen effektiven MECH und einen entsprechenden Provenienzeintrag.

### Darstellung und tatsächlicher Verbraucher

[ValidateAssignmentSerialization](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:116) vergleicht nicht lediglich zwei Vorbereitungsausgaben. Sie ruft das produktive [SetInstance](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Variables/ParameterVariableDeclaration.cs:76) auf einer neuen Instanz auf. Dessen Semantik ist Zerlegung an `#`, Trennung eines Segments am ersten Doppelpunkt und Einsetzen in das Parameterdictionary mit bestehenden Vertragsprüfungen.

Der Vergleich umfasst:

- Mechanism gegen GetParameter("MECH").
- Den aus Value konsumierten Mechanismus.
- Dictionaryanzahl und jeden Namen mit exakt demselben Wert.
- Den wieder aufgebauten Value.

Für das vorhandene Format ist diese Prüfung zusammen mit der frühen Trennersperre schlüssig: Werte enthalten kein rohes `#`; die Parametergrenzen stammen daher ausschließlich von den kanonischen Namen. Zusätzliche Doppelpunkte innerhalb eines Werts bleiben hinter dem ersten Doppelpunkt erhalten. Jeder veröffentlichte Dictionaryeintrag muss beim tatsächlichen Wiederlesen unverändert vorhanden sein. Verwerfen, zusätzliche Segmente und Überschreiben durch eine zweite konsumierte MECH-Definition können so nicht unbemerkt bleiben.

Die Verbraucher verwenden zwei vorhandene Wege:

1. In-Memory-Hash reicht den serialisierten Wert weiter; [HASH.ResolveParameter](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/CryptoAlgorithm/HASH/HASH.cs:77) rekonstruiert mit SetInstance.
2. Strukturierte AES-/KDF-/weitere Aufrufe erhalten häufig das ursprüngliche PARAM über [AlgorithmArgumentResolver](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/CryptoAlgorithm/AlgorithmArgumentResolver.cs:8). Dessen textueller Fallback verwendet ebenfalls SetInstance.

Die Roundtrip-Prüfung modelliert daher die relevante Textrekonstruktion; der Vergleich bindet diese an das direkt konsumierte strukturierte Objekt. Zusätzlich wurden AES und HKDF sowohl über Parser/Runner mit echter PARAM-Referenz als auch über die produktive textbasierte CryptoOperations-Überladung geprüft. Beide Wege und die unabhängigen .NET-Erwartungswerte stimmen überein.

### Veröffentlichung, Validierung und Metadaten

Die gemeinsame [Veröffentlichungsschranke](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:88) erfasst Literale und getrennte Parameters(...)-Ergebnisse. Erst danach werden Zielmetadaten gesetzt und VariableDictionary.Update ausgeführt. Typbindung und Abweisung lebender Ergebnisaliasreferenzen bleiben erhalten.

Die bestehenden fachlichen Validatoren verwenden den vorbereiteten Mechanismus. Da dessen konsumierte Darstellung identisch sein muss, wurde kein erneuter Wechsel zwischen validiertem und verwendetem Mechanismus gefunden.

Die Roundtrip-Hilfsinstanz wird nicht veröffentlicht. HeaderReference, ExplicitParameterNames und OriginalExplicitPadding verbleiben am vorgesehenen Objekt. SetInstance und die dabei aufgerufenen Validatoren arbeiten hier auf lokalen Instanzen beziehungsweise lesen registrierte Regeln; es wurde kein schreibender Zugriff auf das alte Ziel oder seine Headerreferenz gefunden.

Metadaten sind nicht mit serialisierten Werten gleichzusetzen: Der Text enthält etwa den BLKH-Identifier, nicht die Objektidentität. Die echte Headerreferenz bleibt deshalb bewusst am strukturierten Objekt. Ebenso können erlaubte doppelte Nicht-MECH-Einträge in ExplicitParameterNames dokumentiert bleiben, obwohl das Dictionary einen effektiven Wert enthält. Die bestehenden mechanismusspezifischen Duplikatregeln, insbesondere bei SECRET_SPLIT, bleiben zuständig. Das ist keine unerkannte zusätzliche konsumierte Parameterdefinition.

## 3. Eigene adversariale und positive Gegenproben

Ein neuer temporärer Harness wurde für B7 erstellt; keine Übernahme der B6-Erwartungsumschaltung als eigener Nachweis. Er verwendet ParserBuilder, beide produktiven Runner, vollständige Zustandssnapshots und zusätzlich unabhängige kryptografische Erwartungswerte.

**141 Szenarien × zwei Runner = 282 Ausführungen, alle erwartungsgemäß:**

| Gruppe | Ausführungen |
|---|---:|
| Abgewiesene PARAM-Initialisierer | 198 |
| Erfolgreiche PARAM-Kontrollen | 72 |
| Bestehende Syntaxgrenzen, keine Ausführung | 6 |
| Erneute Originalreproduktionen B1-01/B1-02/B1-03 | 6 |
| Gesamt | 282 |

### Negative Eingabeklassen

Systematisch kombiniert wurden IV, LABEL, SALT und NONCE als Träger, direkte Stringliterale und PATH-Referenzwerte sowie folgende Nutzlasten:

- MECH-, IV-, PAD-, BLKH- und unbekannte Segmente.
- Einfacher abschließender Trenner, wertloser Folgetext und mehrere aufeinanderfolgende Trenner.
- Anführungszeichen vor einer eingeschleusten Parameterdefinition.
- 32.768 Zeichen vor dem mehrdeutigen Trenner.

Zusätzliche Fälle prüfen:

- Umgekehrte IV-/MECH-Reihenfolge.
- Identische tatsächliche MECH-Wiederholungen mit eingeschleustem Wert.
- Mehrere verdächtige Träger in einem Assignment.
- Später überschriebene, zuerst mehrdeutige Werte.
- Weitere Segmente und wiederholte Selektorpräfixe in Mechanismusreferenzen.
- Fehlende Referenzen und fehlendes tatsächliches MECH.
- Sämtliche ursprünglichen B3-Konflikte in beiden Reihenfolgen.
- Einzelnes ungültiges SECRET_SPLIT und doppelte PARTS.
- Ein mehrdeutiges SALT im Ergebnis von Parameters(KDF-HKDF,...).

Alle 198 Ausführungen enden mit genau einer redigierten InvalidAssignmentInitializer-Diagnostic (19), Kategorie Assignment, Identifier target, aktueller ExecutionId und StatementIndex 2. Das alte Ziel bleibt dieselbe Instanz mit derselben Typ- und Headerreferenz, unverändertem Wert, Dictionary, expliziten Namen und Paddingmetadaten. Referenzquellen bleiben ebenfalls unverändert. Die nachfolgende Sentinel-Anweisung wird nicht ausgeführt.

Die permanenten B6-Tests prüfen ergänzend ausdrücklich, dass die nachfolgende Hash-Anweisung bei der Originalreproduktion und den Trennereinschleusungen nicht ausgeführt wird; sie wurden in B7 erneut vollständig ausgeführt.

### Positive Eingabeklassen und reale Operationen

- Leere Werte, einfache und mehrfache Doppelpunkte, Leerzeichen, Semikolon, Komma, Klammern, escaped Anführungszeichen, Backslash sowie Tab/CR/LF.
- 32.768 Zeichen lange verlustfreie Werte.
- Unicode-Zeichen, die einem Hashzeichen ähneln, aber nicht der ASCII-Trenner sind.
- Direkte Literale und nach bestehenden PATH-Regeln aufgelöste Werte; deren exakte erwartete LABEL-Werte wurden zusätzlich zum Roundtrip verglichen.
- Normalisierte SHA-256-/SHA-512-Selektoren einschließlich gemischtem `#mEcH:`-Präfix, einfache PARAM-Selektorreferenzen und drei gleichwertige MECH-Einträge.
- Selbstreferenz auf einen einfachen Mechanismus-PARAM, unbekannte und leere Mechanismusreferenzen im bestehenden permissiven Vertrag.
- AES-CBC mit gültigem Hex-/Base64-IV, NONE und PKCS-7 sowie Parameters(...)-Ergebnis: Encrypt/Decrypt, Vergleich mit .NET Aes.EncryptCbc und zusätzlicher produktiver Textkonsum.
- SHA-256 und SHA-512: tatsächliches Hash-Ergebnis gegen SHA256.HashData/SHA512.HashData.
- SECRET_SPLIT mit identischen MECH-Wiederholungen: Share/Restore stellt den Schlüssel wieder her.
- Gültige strukturierte Headerreferenz, deren Header-Metadaten selbst ein `#` enthalten. Der PARAM speichert den verlustfreien Identifier; die Referenz bleibt erhalten.
- HKDF als Literal und Parameters(...)-Ergebnis mit Base64-kodiertem Byte `#` im SALT: Derive stimmt sowohl zwischen strukturiertem/textuellem Konsum als auch mit .NET HKDF.DeriveKey überein.

Alle 72 positiven Ausführungen veröffentlichen ein neues Objekt, erhalten die alte separat gehaltene Instanz und deren Metadaten und liefern einen identischen konsumierten Zustand.

### Syntaxgrenzen

Drei Klassen, jeweils für beide Runner vorbereitet, scheitern bereits vor der Ausführung: fehlender Wert nach `#IV:`, unbekannter Parametername als Sprachsymbol und ein nicht unterstütztes `\u0023`-Escape im Literal. Es wird keine neue Escape-Syntax vorausgesetzt. Diese sechs Fälle sind keine Laufzeit-Assignment-Ablehnungen und werden nicht als Code-19-Fälle ausgegeben.

### Ursprüngliche B1-Findings

Die drei ursprünglichen Reproduktionen wurden zusätzlich separat über Parser und beide Runner wiederholt:

| Finding | Ergebnis |
|---|---|
| B1-01 BlockHeader-Platzhalter | FIXED; FunctionContractViolation, alte gültige Headerinstanz erhalten |
| B1-02 nur MECH-Text in IV | FIXED; Code 19, alter PARAM erhalten |
| B1-03 2147483648 | FIXED; Code 19, alter VAR erhalten |

Die B2-Suite ergänzt Legacy-BlockHeader, Headerliteral/CreateHeader, Zahlengrenzen, führende Nullen, quotierte Zahl und sehr lange Ziffernfolgen. Alle 32 Fälle bestehen unverändert.

## 4. Qualität und Verhältnis der vorhandenen Tests

Die 68 B6-Fälle wurden inhaltlich geprüft: 52 Varianten negativer Initialisierer, zwei Originalreproduktionen, acht Hash-Erfolgskontrollen, zwei Funktionsergebnisse, zwei Headerkontrollen und zwei Legacy-Deklarationskontrollen. Die Assertions prüfen Referenz- und Metadatenerhalt, Code/Quellzuordnung/Redigierung, Konsum aller Parameter und tatsächliche Digests. Die Abdeckung ist damit nicht auf Textfragmente oder MECH-Zählung reduziert.

Die vorhandenen 72 temporären B6-Ausführungen sind 36 frühere B5-Szenarien mit zwei Runnern. Vier explorative Einschleusungsszenarien wurden dabei auf erwartete Ablehnung umgestellt. Ihre gespeicherten Ergebnisse zeigen 44 Ablehnungen und 28 Erfolge. Diese Ergebnisse wurden in B7 analysiert, nicht als neuer Lauf ausgegeben.

Die 68 permanenten Fälle und 72 temporären Ausführungen sind weder disjunkt noch 140 zusätzliche permanente Tests. Sie überlappen besonders bei Originalreproduktion, Reihenfolgen, Selektoren und Hash. Die neuen 282 B7-Ausführungen erweitern die Evidenz insbesondere um weitere Träger, BLKH-Einschleusung, echte Syntaxgrenzen, lange/mehrfach getrennte Werte, genaue sichere Werte und AES-/HKDF-Vergleiche beider Konsumwege.

Die 48 B4-Tests und 32 B2-Tests wurden ebenfalls auf ihren Prüfgegenstand kontrolliert und erneut ausgeführt. Der B3-01-Fix bleibt wirksam; die B6-Schranke schließt die anschließend nachgewiesene Serialisierungslücke.

## 5. Eigene Builds und Testausführung

Alle nachfolgenden Zahlen stammen aus B7-Läufen, nicht aus übernommenen B6-Berichten.

### Gezielter Lauf

Core Debug einschließlich Build:

```powershell
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj -c Debug --no-restore --filter "FullyQualifiedName~Assignment|FullyQualifiedName~VariableDeclaration|FullyQualifiedName~StatementExecution|FullyQualifiedName~ParameterInitializer|FullyQualifiedName~Tr31Header|FullyQualifiedName~SemanticDiagnostic|FullyQualifiedName~AsyncPasswordInfrastructure" --logger "trx;LogFileName=l1-b7-targeted.trx"
```

**1.686 Passed, 0 Failed, 0 NotExecuted.**

### Builds und vollständige Suiten

Der vorhandene Prism-Wrapper wurde für Debug und Release erfolgreich ausgeführt, jeweils für GUI-Lösung und Core-/MCP-Lösung:

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Release
dotnet test <Projekt.csproj> -c <Debug|Release> --no-build --no-restore --logger "trx;LogFileName=l1-b7-full-<Debug|Release>.trx"
```

| Suite | B7 Debug Passed | B7 Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration | Änderung zu B6 |
|---|---:|---:|---:|---:|---|
| Core | 11.719 | 11.719 | 0 | 27 | Keine |
| GUI | 800 | 800 | 0 | 0 | Keine |
| MCP | 176 | 176 | 0 | 13 | Keine |
| Gesamt | 12.695 | 12.695 | 0 | 40 | Keine |

Alle **214 spezifischen L1-Fälle** sind in beiden Konfigurationen anhand der TRX-Einzelresultate Passed: ursprüngliche 66 L1-A-Fälle, 32 B2, 48 B4 und 68 B6. Enthalten sind die nativen Enter- und authentifizierten MCP-Lease-Integrationsfälle. Die 66 A-Fälle verteilen sich auf 61 AssignmentTests, zwei CWM-Fälle, einen nativen Enter-Fall und zwei MCP-Lease-Fälle.

Warnungen: jeweils sechs bekannte Warnungen in der ersten Lösung, null in der zweiten; keine Buildfehler und keine neue Warnung. Gegenüber den zwölf B6-Release-Warnungen fehlen die sechs bekannten Core-CS8603-Warnungen wegen des inkrementellen Builds des unveränderten Core-Stands. Das ist keine Warnungsbehebung. Die tatsächlich ausgegebenen Warnungszeilen sind bereits in B6 vorhanden.

NotExecuted wurde je Suite und Konfiguration namentlich gegen B6 verglichen: unverändert. Die TRX-Zählung schließt explizite Fälle ein, die die CLI-Skipzahl teilweise auslässt.

### Artefakte

Im Worktree, lokal ignoriert:

- `artifacts-l1-b7-targeted.log` und Core-TestResults/`l1-b7-targeted.trx`.
- `artifacts-l1-b7-build-<Debug|Release>.log`.
- `artifacts-l1-b7-<core|gui|mcp>-<Debug|Release>.log`.
- Je Testprojekt `TestResults/l1-b7-full-<Debug|Release>.trx`.
- `artifacts/l1-b7-probe/Program.cs`, `Probe.csproj`, `results.jsonl`.
- Dort außerdem `input-hashes.json`, `validation-summary.json` und `l1-results.json`.

Der temporäre Harness referenziert die frisch geprüfte Debug-Core-DLL und verwendet keine zusätzlichen PackageReferences. Seine Buildkonfiguration Release ist nicht mit einem Gegenprobenlauf gegen die Release-Core-DLL gleichzusetzen; dafür stehen die vollständigen Release-Suiten separat.

## 6. Akzeptierte Grenzen und Gesamtfreigabe

Keine neuen reproduzierbaren Findings.

Bewusste Produktgrenzen:

- Das bestehende Textformat kann rohes `#` in einem Parameterwert nicht verlustfrei ausdrücken. Die gezielte Ablehnung ist erforderlich; vorhandene sichere Hex-/Base64-Darstellungen bleiben nutzbar, soweit der jeweilige Mechanismus sie akzeptiert.
- Ein führender Mechanismus-Selektor ist erlaubt. Eine gesamte mehrfeldrige PARAM-Serialisierung als MECH-Wert wird wegen weiterer Trenner abgewiesen; das ist kein erlaubter Mechanismus-Selektor.
- Unbekannte, quotierte unbekannte und auch leere Mechanismuswerte aus Referenzen können im bisherigen permissiven Literalvertrag weiterhin verlustfrei gespeichert werden. Das bedeutet keine Unterstützung durch eine kryptografische Operation; AlgorithmFactory akzeptiert nur ihre vorhandenen Mechanismen. B6 ist keine neue globale Mechanismus-Whitelist.
- Historische Deklarationen bleiben unverändert, einschließlich ihrer akzeptierten inkonsistenten Fälle. Die erfolgreich geprüfte Assignment-Invariante wird dadurch nicht aufgehoben.
- Die Prüfung gilt am Veröffentlichungszeitpunkt. Sie ersetzt keine globale Transaktion späterer mutierender Operationen und sichert nicht sämtliche beliebigen externen Änderungen an öffentlichen Objekten ab.
- Plattform-, Hardware- und Installed-Codex-/Desktop-Fälle unter den unveränderten 40 NotExecuted bleiben externe Testgrenzen.

Als mögliche spätere Verbesserung kann ausgewählte B7-Evidenz zu AES/HKDF und langen Werten dauerhaft in Tests überführt werden. Das ist eine Erweiterung der Absicherung, kein nachgewiesener Fehler und keine Voraussetzung dieser Freigabe. Im read-only Review wurden keine permanenten Tests ergänzt.

Für den geprüften aktuellen Worktree wird die Gesamtfreigabe von L1 empfohlen. Commit und Integration bleiben ausdrücklich einer separaten Beauftragung vorbehalten.

**READY FOR ACCEPTANCE**

