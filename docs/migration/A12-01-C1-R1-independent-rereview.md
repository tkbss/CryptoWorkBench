# A12-01-C1-R1 – Independent Core Diagnostic Tests Re-Review

Stand: 27.10.2026. Bewertung: **C1 ACCEPTED**.

Keine blockierenden Findings. Die private Assertion prüft die fachlichen Meldungen statt Stacktrace-/Quellpfadtext und berücksichtigt die gesamte lineare InnerException-Kette. Die Sicherheitsabsicht bleibt erhalten: Die neuen unabhängigen Kontrollen erzeugen bei jeder absichtlich offengelegten PSN, PAN oder Schlüsselzeichenfolge einen tatsächlichen Assertion-Fehler.

Die Bewertung betrifft ausschließlich C1. Sie gibt weder die Avalonia-12-Gesamtmigration noch Prism-Integration oder Produktveröffentlichung frei.

## 1. Ausgangsbasis und Integrität

Geprüfter Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`; Branch `feature/avalonia12-migration`; HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

Vor Beginn wurden am 27.10.2026 um 10:50:32 UTC der tatsächliche Git-Status, unstaged/staged Diff und SHA-256 von 777 vorhandenen Quelldateien aufgenommen. Die bestehenden uncommitteten Migrationsänderungen sind vollständig Bestandteil der Ausgangsbasis. Der Vergleich mit dem finalen C1-Quellmanifest ergab keine Abweichung. Die Staging Area war leer.

Der Review erfolgte methodisch separat mit neuer Bestandsaufnahme, direkter Codeprüfung, frischen Testläufen und einem eigenständig geschriebenen Probeprojekt. Der ausführende Agent verfügt allerdings über den vorherigen Implementierungskontext; dies ist keine personell oder organisatorisch unabhängige Zertifizierung.

Die neue isolierte Testkopie liegt unter `C:\Users\stefa\Documents\projects\GITHUB\CwbDiagnosticRereview\A1`. Der eigene Harness liegt daneben unter `CwbDiagnosticRereview\probe`. Separate NuGet-/CLI-Verzeichnisse wurden verwendet. Keine andere Git-Arbeitskopie wurde verändert.

[Integritätsnachweis](../../artifacts/a12-01-c1-r1/integrity-final.json):

- Alle **777 ursprünglichen Quelldateien** einschließlich vorhandener Tests und Berichte bleiben bytegleich.
- Die A1-Testkopie entspricht diesem unveränderten Quellstand.
- Alle **63 zu Beginn erfassten C1-Evidenzdateien** bleiben unverändert.
- Die 62 durch den historischen C1-Evidenzmanifest gehashten Dateien und dessen Berichtshash wurden zusätzlich erfolgreich geprüft.
- HEAD und Branch bleiben unverändert; die Staging Area bleibt leer.

Neu sind ausschließlich dieser Bericht und `artifacts/a12-01-c1-r1/`. Keine produktiven Änderungen, keine Änderungen bestehender Tests, keine Prism-Integration. `.codex/config.toml` wurde weder gelesen noch verändert. Kein Commit, Push, Merge, Reset, Rebase oder Stash.

## 2. Geprüfter Code- und Testdelta

Geprüft wurden die gesamte Testklasse EmvMasterKeyDerivationOptionCTests, ihre drei Aufrufergruppen von AssertSanitized, der C1-Diff, die originale C1-Ausgangsdatei und die tatsächlichen produktiven Validierungsstellen in EmvMasterKeyDerivationOptionC und AES_ECB.

C1 ändert ausschließlich den privaten Helper in `CryptoScriptUnitTest/EmvMasterKeyDerivationOptionCTests.cs:235–244`:

```csharp
for (Exception? diagnostic = exception; diagnostic is not null; diagnostic = diagnostic.InnerException)
{
    foreach (string? value in sensitiveValues)
        if (!string.IsNullOrEmpty(value))
            diagnostic.Message.Should().NotContain(value);
}
```

Vorher wurde für dieselben Werte `exception.ToString().Should().NotContain(value)` geprüft.

Der Quelltext vor diesem Helper stimmt mit der gesicherten ursprünglichen C1-Testdatei überein. Damit bleiben insbesondere alle TestCase-Daten, Schlüssel-/PAN-Konstanten, ArgumentException-/ParamName-Prüfungen, erwarteten Validierungsmeldungen und Assertions auf unveränderte Eingabepuffer erhalten. Der Diff enthält keine andere fachliche Änderung.

Die übrigen Helper-Nutzer funktionieren im neuen vollständigen Core-Lauf unverändert:

| Aufrufergruppe | Fälle | Ergebnis |
|---|---:|---|
| RejectsInvalidIssuerKeyLengthsWithoutMutatingInputs | 7 | 7 PASS |
| RejectsInvalidPanWithoutNormalizing | 13 | 13 PASS |
| RejectsInvalidPsnWithoutNormalizing | 9 | 9 PASS |

Beleg: `tests/helper-callers.json` und `baseline/test-contract-verification.json`.

## 3. Unabhängige Bewertung der Assertion

### Pfad- und Stacktrace-Unabhängigkeit

Der zu NotContain übergebene Prüftext stammt ausschließlich aus Message. Der Helper liest weder StackTrace noch ToString, Quellpfade oder PDB-Informationen. Ein späterer Assertion-Fehler kann natürlich einen Stacktrace im Testprotokoll enthalten; dieser Text ist jedoch nicht Bestandteil des Vergleichs.

Die neue Prüfung maskiert und verändert keine Meldung. Enthält die fachliche Message selbst einen sensitiven Testwert, bleibt das verboten und wird erkannt. Damit wird der bisherige falsche Treffer auf zufällige Bestandteile eines Quellpfads beseitigt, ohne die fachliche Meldung pauschal zu bereinigen.

Zusätzlich zur Ausführung unter einem tatsächlichen A1-Pfad verwendet die eigene Probe eine Exception, deren StackTrace-Getter und ToString jeweils absichtlich werfen. Ihre bereinigte Message wird akzeptiert. Ein unbemerkter Zugriff auf diese Metadaten hätte die Probe fehlschlagen lassen.

Die Release-MSBuild-Eigenschaften PathMap und DeterministicSourcePaths sind leer; es wurde keine PathMap als Ersatz für die Assertion verwendet.

### Exception.Messages und InnerExceptions

Die Schleife beginnt mit der äußeren Exception und folgt jedem InnerException-Verweis bis null. Sie überspringt weder die äußere Message noch eine innere Ebene. Eigene Kontrollfälle belegen die Erkennung einer Offenlegung auf Tiefe 0, 1 und 4.

Der produktive PSN-Validator wirft unmittelbar ArgumentException mit ParamName `psn`, einer konstant aufgebauten Formatmeldung, InnerException=null und leerem Data. Dies wurde erneut gegen die reale Core-Methode geprüft. Die Reflection-Hülle der Probe wurde dabei ausdrücklich entfernt; untersucht wurde die tatsächliche Core-Exception.

Die anderen Helper-Aufrufer prüfen ebenfalls unmittelbare ArgumentException-/ArgumentNullException-Validierungen. Die Produktionsmethode besitzt an dieser Stelle keinen Catch-/Wrapping-Pfad. Es werden keine beliebigen Exception-Graphen oder benutzerdefinierten Serialisierungen als neuer Sicherheitsvertrag eingeführt. Für den vorhandenen Vertrag ist die vollständige lineare InnerException-Kette sachgerecht.

### Geschützte Werte und ursprünglicher Prüfzweck

Alle ursprünglichen sensitiven Werte werden unverändert an den Helper übergeben:

- der AES-Testschlüssel beziehungsweise der ungültige Schlüsselinhalt;
- die PAN;
- die ungültige PSN beziehungsweise die beim jeweiligen Aufrufer relevante Eingabe.

Die bestehenden NotContain-Prädikate und der bisherige Ausschluss null/leerer sensitiver Strings bleiben erhalten. Es wurde kein Wert verkürzt, normalisiert oder durch einen anderen Testwert ersetzt. Ein leerer String wäre Bestandteil jeder Meldung und kein sinnvoller Offenlegungsindikator; diese bereits bestehende Behandlung ist unverändert.

Die Validierungsablehnung, der erwartete Parametername und die unveränderten Eingabepuffer werden zusätzlich weiterhin geprüft. Die Änderung reduziert daher nicht die Prüfung auf „irgendeine Exception“.

### Exception.Data und Stacktrace

Das Ignorieren von Stacktrace ist für den tatsächlich geprüften Meldungsvertrag richtig: PDB-Quellpfade, Zeilennummern und Methodennamen sind keine Ausgabe der geprüften sensitiven Eingabewerte durch den Validator. Genau dieser Unterschied wird durch den historischen Vorher-Vergleich und die neue A1-Ausführung sichtbar.

Data wird in den tatsächlichen betroffenen Exceptions nicht befüllt. Die bisherige Standard-ToString-Ausgabe serialisiert außerdem nicht allgemein sämtliche Data-Einträge. C1 entfernt damit keine zuvor tatsächlich vorhandene allgemeine Data-Prüfung.

Die Tests betreffen direkte Core-Validierungsmeldungen; auf diesem Pfad gibt es keine zusätzliche strukturierte SemanticError-Diagnose. Eine Prüfung beliebiger Data-Inhalte, eigener ToString-Serialisierungen oder fremder Exceptiontypen wäre ein neuer Vertrag und ist für diese Abnahme nicht erforderlich. Daraus wird ausdrücklich keine allgemeine Aussage über sicheres Logging beliebiger Exceptions abgeleitet.

## 4. Eigene positive und negative Gegenproben

Der neue Harness liegt unter [probe](../../artifacts/a12-01-c1-r1/probe/). Er referenziert die unveränderte Core-Testassembly und ruft deren tatsächlichen privaten AssertSanitized-Helper per Reflection auf. Der Helper wurde nicht kopiert, ersetzt oder verändert. Reflection-Invocation-Hüllen werden vor der Fehlerweitergabe entfernt.

**Vier sichere Kontrollfälle, alle PASS:**

1. Tatsächliche Validierung von PSN `1`: genauer Exceptiontyp, ParamName, Formatregel, leere Data und fehlende InnerException.
2. Dasselbe für PSN `A1`.
3. Bereinigte Meldung, obwohl jeder Zugriff auf StackTrace oder ToString absichtlich werfen würde.
4. Bereinigte Exception-Kette mit vier inneren Übergängen.

**Zwölf absichtliche Offenlegungen, alle erwartungsgemäß FAIL:**

Für PSN `1`, PSN `A1`, den ursprünglichen AES-128-Testschlüssel und die ursprüngliche PAN wurde jeweils eine Message mit dem Testwert konstruiert. Der Wert lag auf Tiefe 0, 1 oder 4. Alle zwölf Fälle scheitern an `diagnostic.Message.Should().NotContain(value)`.

Konkretes Beispiel für Tiefe 4:

```text
Did not expect diagnostic.Message "Disclosed value: A1 (Parameter 'psn')" to contain "A1".
```

Es handelt sich um echte Assertion-Fehler, nicht um einen absichtlich beliebigen Fehler im Harness. Alle zwölf Rohmeldungen wurden darauf geprüft. Die Negativkontrollen werden getrennt ausgeführt und dürfen nicht als Fehler der regulären Core-Suite gezählt werden.

Die unsicheren Exceptionobjekte entstehen ausschließlich in der isolierten Probe. Produktiver Core-Code und vorhandene Tests bleiben unverändert.

## 5. Tatsächliche Testergebnisse und neutrale Gegenprobe

Umgebung: Windows 10.0.26200 win-x64, .NET SDK 10.0.401, MSBuild 18.9.11, Hostruntime 10.0.12. Neue Release-Ausführung unter dem tatsächlichen Quellpfad `CwbDiagnosticRereview\A1`.

| Neuer Re-Review-Lauf | PASS | FAIL | NotExecuted | Exit |
|---|---:|---:|---:|---:|
| Neun PSN-Fälle | 9 | 0 | 0 | 0 |
| Vollständige Core-Suite | 7.667 | 0 | 5 | 0 |
| Eigene sichere Kontrollen | 4 | 0 | 0 | 0 |
| Eigene absichtliche Offenlegungen | 0 | 12 erwartet | 0 | 1 |

Die neun PSN-Fälle und alle 29 Fälle der Helper-Aufrufergruppen sind Teil der vollständigen Suite, keine zusätzlich zu zählenden einzigartigen Core-Fälle.

Die neutrale Gegenprobe wurde gemäß Auftrag **aus bestehenden Rohbelegen unabhängig ausgewertet**, nicht als neuer Lauf ausgegeben:

- C1 `focused-neutral.trx`: 9/9 PASS.
- C1 `core-neutral.trx`: 7.672 Fälle, davon 7.667 PASS und fünf NotExecuted.
- Tatsächlicher historischer Quellpfad: `C:\Users\stefa\Documents\projects\GITHUB\CwbCoreDiagnostics\neutral`.
- Die Dateien stimmen mit dem historischen C1-Evidenzmanifest überein.
- Fallnamen und Outcomes des neuen A1-Core-Laufs stimmen vollständig mit dem historischen neutralen Core-Lauf überein.

Auch die historischen Vorher-Belege wurden selbst ausgewertet: alte Assertion unter A1 = 7 PASS/2 FAIL; unter neutral = 9 PASS. Das unterstützt die ursprüngliche Pfaddiagnose. Die neue Abnahme hängt zusätzlich von den frischen Tests und eigenen Offenlegungsproben ab.

### Unveränderte fünf Skips

- NativePublicationDoesNotFollowLateSymlink: fehlende Symlinkberechtigung.
- TargetSymlinksIncludingDanglingLinksAreRejected(False): fehlende Symlinkberechtigung.
- TargetSymlinksIncludingDanglingLinksAreRejected(True): fehlende Symlinkberechtigung.
- MacOsInheritedExtendedAclIsRemovedBeforePublication: Darwin-ACLs.
- MeasureLocalHardware: ausdrücklich manueller Hardwaretest.

Keiner dieser Fälle wird als PASS oder Plattformnachweis gewertet. Die Auswertung zählt tatsächliche UnitTestResult-Einträge; verkürzte Konsolen-/Summary-Zähler sind nicht die Grundlage.

GUI-/Coordinator- und MCP-Suiten wurden nicht erneut ausgeführt. Das ist hier sachgerecht, weil die einzige C1-Codeänderung die private Hilfsfunktion in einer Core-Testklasse ist und deren übrige Aufrufer vollständig durch die Core-Suite abgedeckt sind.

## 6. Regression und Architekturgrenzen

Der C1-Diff verändert keinen produktiven Code. Der aktuelle Stand stimmt zudem mit dem finalen C1-Quellmanifest überein, und im Re-Review wurde keine dieser Dateien verändert.

Damit bestätigt die Quellprüfung unveränderte:

- Core-, Parser-, Runner- und kryptographische Implementierungen;
- GUI, EditorCoordinator und EditorBridge;
- MCP-Infrastruktur;
- UiDispatch und ExecutionContext-Grenzen;
- Prism-/Avalonia-Abhängigkeiten, Projektdateien und globale Buildkonfiguration.

Es wurde keine globale PathMap aktiviert. Testdaten und andere Assertions bleiben erhalten. Eine produktive Fehlerbehandlung wurde weder für die normalen Läufe noch für die Negativkontrolle manipuliert.

## 7. Findings, Grenzen und Abnahmeempfehlung

**Nachgewiesene Produkt- oder Testfehler: keine neuen Findings.**

Die ursprünglichen pfadabhängigen Testfehler sind im geprüften Stand behoben. Die Pflichtnachweise für Meldungsvertraulichkeit sind erfüllt. Die zusätzlichen Gegenproben decken über die bisherigen direkten InnerException-Kontrollen hinaus mehrere Ebenen ab; sie begründen keinen neuen Vertrag für beliebige Exception-Serialisierungen.

Bekannte Einschränkungen bleiben getrennt: fünf umgebungs-/plattformbedingte beziehungsweise explizite Skips; keine Linux-/macOS-Ausführung; keine erneute GUI-/MCP-Ausführung; neutrale Prüfung hier anhand historischer, hashverifizierter Rohbelege. Hypothetische Risiken ohne reproduzierten Bezug zum vorhandenen Vertrag wurden nicht als Findings eingestuft.

C1 kann formal im geprüften Umfang abgenommen werden. Neue Nachweise, Skripte, Einzelergebnisse und Integritätsdaten liegen unter [artifacts/a12-01-c1-r1](../../artifacts/a12-01-c1-r1/). Der Originalquellstand und historische Evidenzen sind erhalten.

Keine automatische Fortsetzung weiterer Arbeitspakete und keine Freigabe der Gesamtmigration, Prism-Integration oder Veröffentlichung.

