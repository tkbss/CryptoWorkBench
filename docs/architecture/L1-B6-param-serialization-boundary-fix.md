# L1-B6 – PARAM Serialization Boundary Fix

Datum: 2026-11-06.

## Stand und Ergebnis

Finding B5-01 ist im PARAM-Assignment-Pfad korrigiert. Aufgelöste Werte mit einem mehrdeutigen Parametertrenner werden vor der Veröffentlichung abgewiesen. Eine zusätzliche Prüfung liest die fertige Darstellung mit dem bestehenden Verbraucherparser wieder ein und vergleicht sämtliche Parameterwerte und den Mechanismus.

- Repository: `tkbss/CryptoWorkBench`.
- Branch: `feature/script-assignment`.
- Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment`.
- Unveränderter HEAD: `9a710654a32456ddd93b10027bd34dbbdbffbe81`.
- Referenz: [L1-B5-Review](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/docs/architecture/L1-B5-final-independent-rereview.md).
- Die nicht committeten B2-/B4-Korrekturen und bisherigen Testdateien/Berichte bleiben erhalten.
- Keine Commits, Pushes oder Merges. Master und G1 wurden nicht bearbeitet.

Neue Produktivänderungen ausschließlich in [ParameterInitializerEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:46) und [AssignmentEvaluator.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/AssignmentEvaluator.cs:88). Hinzu kommen [ParameterAssignmentSerializationTests.cs](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScriptUnitTest/ParameterAssignmentSerializationTests.cs) und dieser Bericht. Der gesamte aktuelle Produktivdiff gegen HEAD, einschließlich B2/B4, beträgt +21/-2 bzw. +70/-3 Zeilen; der B6-Zuwachs beträgt zusammen 40 Zeilen.

## Ursache und technische Invariante

BuildValue verkettet `#NAME:VALUE`; SetInstance zerlegt am Zeichen `#`. Das Format besitzt keinen Escape-Mechanismus, auch nicht innerhalb quotierter Werte. Deshalb konnten ein zusätzlicher Parameter oder ein neuer Mechanismus entstehen beziehungsweise Teile eines Werts verworfen werden, obwohl der strukturierte Zustand vor dem Serialisieren korrekt aussah.

Die neue Veröffentlichungsschranke verlangt:

1. Der aufgelöste effektive Mechanismus und die gespeicherten Parameterwerte enthalten keine rohen `#`-Trenner.
2. Mechanism und GetParameter("MECH") stimmen überein.
3. SetInstance kann Value auf einer neuen, ausschließlich lokalen Instanz konsumieren.
4. Deren Mechanismus, vollständiges Parameterdictionary einschließlich Anzahl und exakter Werte sowie erneut serialisierter Value stimmen mit dem vorbereiteten Objekt überein.

Es wird nicht ausschließlich nach `#MECH` gesucht. Auch `#IV`, `#PAD`, unbekannte Namen, ein einzelnes `#` und überschreibbare Zwischenwerte werden abgewiesen.

## Implementierung

### Frühe Prüfung aufgelöster Werte

Im strengen Literal-Assignment-Pfad wird jeder Nicht-MECH-Wert unmittelbar nach ParameterEvaluator und vor SetParameter geprüft. Dadurch kann auch ein später überschriebener, zuvor mehrdeutiger Parameterwert nicht unbemerkt passieren. Deklarationen aktivieren diesen strengen Modus weiterhin nicht.

Bei Mechanismusreferenzen bleibt die B4-Normalisierung erhalten: Ein ausdrücklich unterstützter führender `#MECH:`-Selektor wird ohne Beachtung der Groß-/Kleinschreibung entfernt. Direkt danach wird der verbleibende Wert auf weitere Trenner geprüft, bevor ein Mechanism-Setter ihn interpretieren könnte. Registrykanonisierung und Vergleich mehrerer tatsächlicher MECH-Einträge bleiben bestehen.

### Abschließender Roundtrip

[ValidateAssignmentSerialization](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/script-assignment/CryptoScript/Model/ParameterInitializerEvaluator.cs:116) arbeitet mit einer neuen ParameterVariableDeclaration und dem tatsächlich verwendeten SetInstance. Die dortigen vorhandenen Parametervertragprüfungen laufen ebenfalls. Dieser Pfad publiziert keine Variablen und greift nicht schreibend auf das alte Ziel oder dessen Referenzen zu.

Verglichen werden alle Dictionaryeinträge, nicht nur MECH. Die konsumierte Hilfsinstanz ersetzt das vorbereitete Objekt ausdrücklich nicht: HeaderReference, ExplicitParameterNames und OriginalExplicitPadding bleiben am vorgesehenen neuen PARAM erhalten. Die Provenienz kann legitime doppelte Nicht-MECH-Einträge enthalten und ist kein serialisiertes Feld; bestehende fachliche Validatoren behalten ihre bisherigen Duplikatregeln.

Der gemeinsame Aufruf im AssignmentEvaluator liegt vor dem Setzen der Zielmetadaten und vor VariableDictionary.Update. Er schützt sowohl PARAM-Literale als auch bereits vorbereitete, getrennte Ergebnisse von Parameters(...). Diese kleine Erweiterung ist notwendig: Die neuen Gegenproben zeigen, dass etwa Parameters(KDF-HKDF, ... #SALT:"private-marker#discarded" ...) ebenfalls ein mehrdeutiges Objekt zurückgeben konnte.

Ein Fehler an dieser Grenze ergibt genau eine generische, redigierte Assignment-Diagnostic InvalidAssignmentInitializer (19), auch wenn das vorbereitete Objekt aus Parameters(...) stammt. Fehler bei der eigentlichen vorherigen Funktionsausführung behalten ihre bisherigen Diagnoseverträge. Es wurde keine allgemeine Funktions-, Registry- oder Serializerarchitektur geändert.

Die mechanismusspezifischen bisherigen Validatoren bleiben aktiv. Bei erfolgreicher Veröffentlichung garantiert der Roundtrip, dass die konsumierte Darstellung den zuvor geprüften Mechanismus und dieselben Werte beschreibt.

## Auswirkungen auf bisherige Werte

Bewusst enger wird ausschließlich die PARAM-Zuweisung:

- Rohe `#` in aufgelösten Werten werden abgewiesen, auch innerhalb von Anführungszeichen oder bei einem unbekannten nachfolgenden Parameternamen.
- Der einzelne erlaubte führende Selektor einer Mechanismusreferenz bleibt nutzbar; weitere Trenner nicht.
- Doppelpunkte, Leerzeichen in Stringwerten, leere Werte sowie bestehende Hex-/Base64-Darstellungen ohne rohen Trenner bleiben zulässig. Die Tests prüfen insbesondere `b64(Iw==)`, die bestehende Base64-Darstellung des Bytes `#`.
- Eine strukturierte Headerreferenz wird weiterhin als verlustfreier Identifier übertragen; ihre Objektidentität bleibt erhalten.
- Unbekannte Mechanismuswerte werden nicht durch eine neue globale Whitelist eingeschränkt, sofern sie die vorhandenen Regeln und die neue Darstellungsprüfung erfüllen.
- Historische Deklarationen behalten ihr bisheriges Verhalten, einschließlich der ausdrücklich akzeptierten unsicheren Legacy-Serialisierung. Dies wird durch einen Test mit anschließendem Hash belegt.

Ein PARAM, dessen Darstellung schon der tatsächliche Verbraucherparser nicht akzeptieren oder verlustfrei rekonstruieren kann, wird jetzt vor Veröffentlichung zurückgewiesen. Eine allgemeine Escape-/Quoting-Erweiterung oder ein neues Dateiformat wurde nicht eingeführt.

## Neue permanente Regressionstests

**68 neue Fälle**, alle über ParserBuilder und beide produktiven Runner:

| Gruppe | Fälle |
|---|---:|
| Mehrdeutige Werte: 26 Varianten × beide Runner | 52 |
| Ursprüngliche minimale B5-01-Reproduktion | 2 |
| Gültige SHA-256-/SHA-512-Zuweisungen, direkte/normalisierte Referenzselektoren | 8 |
| Gültiges Parameters(...)-Ergebnis mit SHA-512-Folgeoperation | 2 |
| Erhalt strukturierter Headerreferenz bei erfolgreicher Prüfung | 2 |
| Unveränderte historische Deklarationssemantik mit Folgeoperation | 2 |
| Gesamt | 68 |

Die 26 negativen Varianten umfassen:

- Eingebettete MECH-, IV-, PAD-, LABEL- und unbekannte Segmente; jeweils direkte Literale und Referenzwerte.
- Führendes, abschließendes oder allein als Trenner wirkendes `#` ohne gültiges Folgesegment.
- IV vor dem tatsächlichen MECH, identische mehrfache MECH-Einträge, LABEL-/PAD-Träger und später überschriebene Werte.
- Mehrdeutige Mechanismusreferenzen nach dem erlaubten Selektor sowie unbekannte Mechanismuswerte mit Trennern.
- Mehrdeutige SALT-Werte aus einem Parameters(...)-Ergebnis, direkt und über VAR-Referenz.

Jeder dieser negativen Fälle vergleicht die alte Zielreferenz, Typreferenz, vollständigen serialisierten Zustand einschließlich Wert, Parameterdictionary, expliziter Namen und Padding sowie HeaderReference. Referenzquellen bleiben unverändert. Genau eine Diagnostic mit Code 19, Kategorie Assignment, Identifier target, aktueller ExecutionId und StatementIndex 2 ist erforderlich; private Marker und RHS dürfen nicht erscheinen. Die vorherige Sentinel-Anweisung läuft, Hash und nachfolgende Anweisungen laufen nicht.

Erfolgsfälle vergleichen alle Parameterwerte mit einer separat über SetInstance konsumierten Instanz. SHA-256 und SHA-512 werden anschließend tatsächlich über Hash ausgeführt und mit unabhängig durch .NET berechneten Digests verglichen. Es werden somit weder nur Textfragmente noch lediglich MECH-Zählungen getestet.

Alle bisherigen **146 L1-Regressionen** sind unverändert erhalten und in beiden vollständigen Konfigurationen Passed: 61 AssignmentTests, zwei CWM-Fälle, ein nativer Enter-Fall, zwei authentifizierte MCP-Lease-Fälle, 32 B2- und 48 B4-Fälle. Mit B6 sind es **214 spezifische L1-Fälle**.

## Tatsächliche Validierung

### Vorher-/Nachher-Läufe

- Erster lokaler Versuch: Testlauf wegen fehlgeschlagener Testhost-Verbindung nach 90 Sekunden abgebrochen; keine verwertbaren Testergebnisse. Dies war ein Infrastrukturproblem der Sandbox.
- Erneuter Lauf mit funktionierender lokaler Testhost-Verbindung, noch vor Produktivänderungen: **48 Failed, 20 Passed, 0 Skips**. Die Fehler zeigen die bisher angenommenen mehrdeutigen Werte; die sechs bereits abgewiesenen Mechanismusvarianten und 14 positiven/Legacy-Kontrollen bestanden.
- Dieselben 68 Tests nach der Korrektur: **68 Passed, 0 Failed, 0 Skips**.
- Gezielter Core-Gesamtlauf für Assignment-, Deklarations-, Statement-, Parameter-/Header-, Diagnostic- und Async-Infrastrukturregressionen: **1.686 Passed, 0 Failed, 0 Skips**.

Die erwarteten Fehler des Vorher-Laufs und der abgebrochene Infrastrukturversuch werden nicht als Fehler des korrigierten Stands gezählt.

### Builds

Ausgeführt mit dem vorhandenen Prism-Wrapper:

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Release
```

Beide Aufrufe erfolgreich; jeweils GUI-Lösung sowie Core-/MCP-Lösung, ohne Buildfehler. Der Wrapper verwendet unverändertes Restore im Locked Mode und die bestehende Auditkonfiguration.

Warnungen:

- Debug: sechs bekannte Warnungen in der ersten Lösung, null in der zweiten.
- Release: zwölf bekannte Warnungen in der ersten Lösung, null in der zweiten.
- Vergleich der konkreten Warnungszeilen mit B4: identisch, keine neue Warnung.
- Gegenüber dem inkrementellen B5-Release-Lauf mit sechs Warnungen werden die sechs bekannten Core-CS8603-Warnungen durch die erneute Core-Kompilierung wieder ausgegeben. Keine davon betrifft den neuen Guard oder die neue Testdatei.
- git diff --check erfolgreich; Git meldet nur den bestehenden LF-/CRLF-Hinweis.

### Vollständige Tests

Alle drei Projekte nach den Builds jeweils mit `dotnet test -c <Debug|Release> --no-build --no-restore` und neuen TRX-Dateien vollständig ausgeführt:

| Suite | B5 Passed je Konfiguration | B6 Debug Passed | B6 Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|---:|
| Core | 11.651 | 11.719 | 11.719 | 0 | 27 |
| GUI | 800 | 800 | 800 | 0 | 0 |
| MCP | 176 | 176 | 176 | 0 | 13 |
| Gesamt | 12.627 | 12.695 | 12.695 | 0 | 40 |

Der Zuwachs beträgt genau 68 Core-Fälle. Die 40 NotExecuted-Fälle wurden je Suite und Konfiguration namentlich mit B5 verglichen: keine Änderung. Enthalten sind plattformabhängige Prüfungen, eine explizite Hardwaremessung und explizite Installed-Codex-/Desktop-Fälle. Die Zahlen stammen aus TRX-Einzelresultaten; CLI-Skipzahlen lassen explizite Fälle teilweise aus.

### Unabhängige B5-Gegenproben

Der separate temporäre B5-Harness wurde in ein neues ignoriertes B6-Verzeichnis kopiert. Nur die erwartete Entscheidung der vier bisherigen explorativen Einschleusungsfälle wurde auf Ablehnung umgestellt; B5-Quellen und Ergebnisse bleiben unverändert.

**36 Szenarien × zwei Runner = 72 erwartungsgemäße Ausführungen**: 44 Ablehnungen und 28 erfolgreiche Kontrollen.

Insbesondere werden ursprüngliche Reproduktion, umgekehrte IV-/MECH-Reihenfolge und LABEL mit identischen mehrfachen MECH-Einträgen nun mit Code 19 abgewiesen. Alte Zielinstanz und Metadaten bleiben erhalten, keine Hash-Folgeoperation wird ausgeführt. Erfolgreiche Kontrollen einschließlich normalisierter Selektoren, Hash und Share/Restore funktionieren weiterhin.

Dieser separate Prozess verwendet die aktualisierte Debug-Core-DLL. Die Release-Evidenz stammt aus den vollständigen permanenten Suiten.

## Nachweise und unveränderte Grenzen

Lokale ignorierte Artefakte im Worktree:

- `artifacts-l1-b6-before.log` und `l1-b6-before.trx`: abgebrochener Infrastrukturversuch.
- `artifacts-l1-b6-before-retry.log` und `l1-b6-before-retry.trx`: fehlerhafter Vorher-Stand.
- `artifacts-l1-b6-after.log`, `artifacts-l1-b6-targeted.log` und entsprechende Core-TRX.
- `artifacts-l1-b6-build-<Debug|Release>.log`.
- `artifacts-l1-b6-<core|gui|mcp>-<Debug|Release>.log`.
- Je Testprojekt `TestResults/l1-b6-full-<Debug|Release>.trx`.
- `artifacts/l1-b6/validation-summary.json` und `input-hashes.json`.
- `artifacts/l1-b6-probe/Program.cs`, `Probe.csproj`, `results.jsonl`.

Das Eingangsmanifest enthält 13 bestehende Dateien. Außer den beiden vorgesehenen Produktivdateien bleiben alle elf weiteren Dateien bytegenau unverändert: bestehende L1-Tests, GUI-Integrationstests und sämtliche B1–B5-Berichte. Die B2-TR31H-/Integer-Guards und die B4-Konsistenzlogik wurden nicht ersetzt oder abgeschwächt.

Das allgemeine PARAM-Format und seine Verbraucher bleiben unverändert. Ebenso unverändert sind andere Typverträge, Deklarationen, GUI-/MCP-Koordination, Persistenz und Scoping. Kein globales Transaktionsmodell und keine zentrale Funktionsmetadatenarchitektur wurden eingeführt. Die Roundtrip-Prüfung kostet eine lokale PARAM-Rekonstruktion pro erfolgreicher PARAM-Zuweisung; sie ist kein Umbau der allgemeinen Ausführung.

Die dokumentierten NotExecuted-Fälle bleiben ungetestete externe beziehungsweise plattformabhängige Grenzen. Die Korrektur schützt die Veröffentlichung durch Assignments, nicht jede spätere mögliche Mutation eines PARAM durch andere bestehende Operationen.

**READY FOR INDEPENDENT RE-REVIEW**

