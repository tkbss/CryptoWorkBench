# A12-01-C1 – Path-independent Core Diagnostic Tests

Stand: 27.10.2026. Ergebnis: **C1 COMPLETE**.

Die Assertions sind pfadunabhängig und erkennen weiterhin sensible Werte in Fehlermeldungen, einschließlich InnerExceptions. Unter beiden tatsächlichen Quellpfaden bestehen die neun gezielten Fälle sowie die vollständige Core-Suite mit jeweils **7.667 PASS, 0 FAIL und 5 NotExecuted**. Der Arbeitsstand ist für einen gezielten Review vorbereitet.

## Ausgangsbasis und Änderungsumfang

Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`; Branch `feature/avalonia12-migration`; unveränderter HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

Vor der Änderung wurden am 27.10.2026 um 10:19:29 UTC Git-Status, unstaged/staged Diff, 776 Quelldateien mit SHA-256 und eine Kopie der ursprünglichen Testdatei gesichert. Die bestehenden uncommitteten Migrationsarbeiten sind Bestandteil dieser Ausgangsbasis. Nachweise: [baseline](../../artifacts/a12-01-c1/baseline/).

Geändert wurde ausschließlich die private Hilfsfunktion `AssertSanitized` in `CryptoScriptUnitTest/EmvMasterKeyDerivationOptionCTests.cs:235`. Hinzu kommen dieser Bericht und neue Evidenzen unter `artifacts/a12-01-c1/`.

Kein produktiver Core-, Parser-, Runner- oder Kryptocode wurde verändert. Testdaten, Parameterprüfungen, Immutabilitätsassertions, Projektdateien und Paketversionen bleiben unverändert. Keine PathMap-Konfiguration, keine Prism-Integration und keine Änderungen an anderen Worktrees. `.codex/config.toml` wurde weder gelesen noch verändert. Kein Commit, Push, Merge, Reset, Rebase oder Stash.

## Ursache und ursprüngliche Sicherheitsabsicht

`RejectsInvalidPsnWithoutNormalizing` hat neun Testfälle:

```text
"", "1", "001", "A1", " 1", "1 ", "１２", "١٢", "1\n"
```

Jeder Fall ruft die tatsächliche EMV-Option-C-Ableitung mit unverändertem AES-Schlüssel und PAN auf. Geprüft werden:

1. Ungültige PSN wird als ArgumentException mit ParamName `psn` abgewiesen.
2. Die Fehlermeldung gibt Schlüsselmaterial, PAN oder die ungültige PSN nicht wieder.
3. Der übergebene Schlüssel bleibt unverändert.

Die private Hilfsfunktion wird zusätzlich von sieben Fällen für ungültige Schlüssellängen und 13 Fällen für ungültige PAN verwendet. Sie ist nicht zwischen Testdateien oder Suiten geteilt.

Der Fehler lag ausschließlich in der Auswahl des zu prüfenden Textes: `Exception.ToString()` umfasst neben der fachlichen Message auch Typ, gegebenenfalls innere Exceptions und Stacktrace. Mit PDB-Informationen enthält dieser Stacktrace absolute Quelldateipfade und Zeilennummern. Ein Ordnername wie `A12-01-F1-Validation` enthält sowohl `1` als auch `A1`, ohne dass diese Zeichenfolgen als sensible Eingabe in der fachlichen Fehlermeldung ausgegeben wurden.

Der neue Vorher-Vergleich bestätigt die Ursache direkt: dieselben unveränderten neun Tests scheitern unter dem A1-Pfad in genau zwei Fällen, bestehen aber unter dem neutralen Quellpfad vollständig.

## Tatsächliche Exceptionstruktur

Der produktive PSN-Validator wirft unmittelbar:

```csharp
new ArgumentException(
    $"EMV Option C {parameterName} must contain {minimum}-{maximum} ASCII decimal digits.",
    parameterName);
```

Für die beiden betroffenen Fälle ist der Laufzeittyp genau ArgumentException, ParamName ist `psn`, InnerException ist null und Data ist leer. Dies wurde zusätzlich gegen die tatsächliche Core-Methode geprüft. Der Reflection-Aufruf der externen Probe erzeugt technisch eine TargetInvocationException; die Probe untersucht ausdrücklich deren innere, vom Core geworfene ArgumentException.

Die fachliche Message enthält die Formatregel und bei ArgumentException zusätzlich den Parameternamen. Die bestehende WithParameterName-Assertion bleibt erhalten. Es gibt auf diesem direkten Core-Pfad keine separat transportierten strukturierten SemanticError-Diagnosen. Die benachbarten Validatoren werfen ebenfalls unmittelbar ArgumentException beziehungsweise bei null ArgumentNullException; die Ableitung hat hier keinen Catch-/Wrapping-Pfad.

Deshalb ist Message die geeignete Prüfoberfläche. Um eine zukünftige oder eingewickelte Offenlegung nicht versehentlich auszunehmen, prüft die Hilfsfunktion zusätzlich jede Message entlang der InnerException-Kette. Es wird weder eine Exception noch eine Message verändert oder bereinigt.

Beliebige Exception.Data-Einträge sind kein neuer Prüfgegenstand: Sie werden in diesen Fällen nicht befüllt und wurden auch von der bisherigen Standard-ToString-Ausgabe nicht allgemein serialisiert. Der Nachweis betrifft die tatsächlich verwendeten diagnostischen Meldungen, keine allgemeine Serialisierung beliebiger Exceptions.

## Exakte Assertion vor und nach C1

Vorher:

```csharp
foreach (string? value in sensitiveValues)
    if (!string.IsNullOrEmpty(value))
        exception.ToString().Should().NotContain(value);
```

Nachher:

```csharp
// Check diagnostic messages (including wrapped errors), not stack traces or PDB source paths.
for (Exception? diagnostic = exception; diagnostic is not null; diagnostic = diagnostic.InnerException)
{
    foreach (string? value in sensitiveValues)
        if (!string.IsNullOrEmpty(value))
            diagnostic.Message.Should().NotContain(value);
}
```

Alle übergebenen sensitiven Werte und deren unveränderte NotContain-Prüfung bleiben erhalten. Der bereits bestehende Ausschluss null/leerer sensitiver Strings bleibt ebenfalls erhalten; ein leerer String wäre Bestandteil jeder Meldung und kein brauchbarer Offenlegungsnachweis. Es werden keine Assertions entfernt oder fachliche Testwerte ersetzt.

Exakter, auf diesen Auftrag begrenzter Delta: [c1-test.diff](../../artifacts/a12-01-c1/c1-test.diff).

## Quellpfade und ausgeführte Validierung

Die zwei neuen isolierten Kopien liegen tatsächlich unter:

- Problematischer Quellpfad: `C:\Users\stefa\Documents\projects\GITHUB\CwbCoreDiagnostics\A1`
- Neutraler Quellpfad: `C:\Users\stefa\Documents\projects\GITHUB\CwbCoreDiagnostics\neutral`

Die ursprüngliche Assertion wurde zunächst in beiden Kopien ausgeführt. Erst danach wurde ausschließlich die korrigierte Testdatei in beide Kopien übernommen. Alle übrigen Dateien blieben identisch. Es wurden keine Pfade durch PDB-PathMap umgeschrieben. Die Befehle enthalten keine solche Option; die abgefragten MSBuild-Eigenschaften sind leer.

Umgebung: Windows 10.0.26200 win-x64, .NET SDK 10.0.401, MSBuild 18.9.11, Hostruntime 10.0.12. NuGet-Cache und CLI-Home liegen isoliert im neuen Validierungsverzeichnis.

| Lauf | PASS | FAIL | NotExecuted | Exit |
|---|---:|---:|---:|---:|
| Vorher, neun PSN-Fälle unter A1 | 7 | 2 | 0 | 1 |
| Vorher, dieselben neun Fälle unter neutral | 9 | 0 | 0 | 0 |
| Nachher, neun PSN-Fälle unter A1 | 9 | 0 | 0 | 0 |
| Nachher, dieselben neun Fälle unter neutral | 9 | 0 | 0 | 0 |
| Vollständige Core-Suite unter A1 | 7.667 | 0 | 5 | 0 |
| Vollständige Core-Suite unter neutral | 7.667 | 0 | 5 | 0 |
| Externe sichere Kontrollfälle | 4 | 0 | 0 | 0 |
| Absichtliche Offenlegungsfälle | 0 | 8 erwartet | 0 | 1 |

Die neun PSN-Fälle sind Teil der vollständigen Core-Suite und werden nicht als zusätzliche einzigartige Core-Tests gezählt. Die beiden vollständigen Läufe besitzen identische Fallnamen und Outcomes. Gegenüber der historischen F1-Core-Baseline ändern sich ausschließlich die beiden bekannten Ergebnisse von FAIL zu PASS; der Fallbestand von 7.672 bleibt gleich.

Die externe Probe ist kein Bestandteil der produktiven Core-Testsuite. Ihre acht absichtlichen Fehler sind der erwartete Nachweis, dass die Sicherheitsassertion anschlägt.

Ausführung und Rohbelege:

- `run-before.ps1`: Kopien, Restore und Vorher-Läufe.
- `run-after.ps1`: korrigierte gezielte und vollständige Läufe.
- `run-probe.ps1`: sichere und absichtlich offenlegende Diagnoseproben.
- `tests/*.trx`, `*.log`, `*.exit`: unveränderte Einzelergebnisse.
- `tests/summary.json`, `path-comparison.json`, `baseline-comparison.json`: Auswertung aus tatsächlichen UnitTestResult-Einträgen.

GUI-/Coordinator- und MCP-Läufe wurden nicht wiederholt: Deren Quellcode und Tests sind unverändert, und die Hilfsfunktion ist privat in der einzelnen Core-Testklasse. Alle benachbarten Nutzer der Hilfsfunktion sind durch die vollständigen Core-Läufe abgedeckt.

## Nachweis der erhaltenen Sicherheitsprüfung

Der externe Harness unter [probe](../../artifacts/a12-01-c1/probe/) ruft per Reflection genau die korrigierte private AssertSanitized-Funktion aus der gebauten Testassembly auf. Er ersetzt die Funktion nicht und ändert keine Produktionsfehlerbehandlung.

Die vier sicheren Kontrollfälle prüfen:

1. Tatsächliche flache und bereinigte ArgumentException für PSN `1`.
2. Tatsächliche flache und bereinigte ArgumentException für PSN `A1`.
3. Bereinigte Message akzeptieren, obwohl ToString einen tatsächlichen A1-Quellpfad enthält.
4. Bereinigte äußere und innere Messages weiterhin akzeptieren.

Für die Negativkontrolle wird jeweils ein sensitiver Testwert ausdrücklich in eine ArgumentException.Message eingefügt: PSN `1`, PSN `A1`, der ursprüngliche AES-128-Testschlüssel oder die ursprüngliche PAN. Jeder dieser vier Werte wird einmal in der äußeren Meldung und einmal nur in einer InnerException offengelegt. Alle **acht** Fälle scheitern an der tatsächlichen NotContain-Assertion.

Beispiel aus dem Rohbeleg:

```text
Did not expect diagnostic.Message "Invalid sensitive value: A1 (Parameter 'psn')" to contain "A1".
```

Die vollständigen Fehlertexte belegen jeweils den zu prüfenden Message-Wert, nicht einen zufälligen Stacktrace-Treffer. Es wurde kein produktiver Core-Code mutiert, um diese Fehler zu erzeugen; ausschließlich die isolierte Probe konstruiert absichtlich unsichere Exceptionobjekte.

## Bekannte Skips und Grenzen

Beide vollständigen Core-Läufe enthalten dieselben fünf nicht ausgeführten Fälle:

- NativePublicationDoesNotFollowLateSymlink.
- TargetSymlinksIncludingDanglingLinksAreRejected(False).
- TargetSymlinksIncludingDanglingLinksAreRejected(True).
- MacOsInheritedExtendedAclIsRemovedBeforePublication.
- MeasureLocalHardware.

Die ersten drei benötigen nicht verfügbare Symlinkrechte. Der vierte benötigt Darwin-ACLs. Der letzte ist ausdrücklich ein manueller Hardwaretest. Diese Fälle werden weder als PASS noch als Plattformnachweis ausgegeben. Die Zählung verwendet die TRX-Einzelfälle, weil Konsolensumme und zusammengefasste Skip-Zähler den expliziten Hardwarefall teilweise anders behandeln.

Linux/macOS wurden nicht ausgeführt und bleiben hinsichtlich Plattformvalidierung offen. C1 betrifft die diagnostische Testassertion; weder EMV-Konformität noch eine neue kryptographische oder allgemeine Logging-Sicherheitsfreigabe wird damit behauptet.

## Integrität und Bereitstellung für Review

[integrity-final.json](../../artifacts/a12-01-c1/integrity-final.json) bestätigt:

- Von **776 ursprünglichen Quelldateien** ist ausschließlich EmvMasterKeyDerivationOptionCTests.cs geändert.
- Die übrigen **775 Dateien**, einschließlich produktivem Core, Parser, Runner, Kryptocode, GUI/MCP, EditorCoordinator, EditorBridge, UiDispatch, bestehenden Migrationsberichten und Projektdateien, sind bytegleich.
- Beide Validierungskopien stimmen mit dem endgültigen Quellstand dieser ursprünglichen Dateien überein.
- Die **192 zu Beginn aufgenommenen historischen Review-/F1-Evidenzdateien** sind unverändert.
- HEAD und Branch sind unverändert; die Staging Area bleibt leer.

Der gezielte Review kann sich auf den privaten Helperdelta, die unveränderten Testdaten und den Meldungs-/InnerException-Nachweis konzentrieren. Originaltestdatei, Vorher-/Nachher-TRX, absichtliche Fehlerfälle, Skripte und Hashmanifeste liegen unter `artifacts/a12-01-c1/` bereit.

Keine automatische Fortsetzung anderer Arbeitspakete und keine Produkt- oder Prism-Freigabe.

