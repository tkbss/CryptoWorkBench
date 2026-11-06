# KEY-Datenmodell: Value als einziges Schlüsselmaterial

Stand: 20. Oktober 2026

## Ergebnis

`KeyVariableDeclaration.KeyValue` ist vollständig entfernt. Jede KEY-Instanz
speichert ihr Schlüsselmaterial ausschliesslich in der geerbten Eigenschaft
`VariableDeclaration.Value`. Es gibt weder eine zweite Materialeigenschaft
noch einen Kompatibilitätsalias. Die kryptografischen Referenztests bestehen.

## Analyse vor der Änderung

| Aspekt | Befund |
| --- | --- |
| Definition | `VariableDeclaration.Value` war ein virtuelles `string`-Auto-Property; `KeyVariableDeclaration.KeyValue` ein zusätzliches `string`-Auto-Property. Beide waren mit `string.Empty` initialisiert und unabhängig beschreibbar. |
| Bedeutung | Beide repräsentierten dieselben Schlüsselbytes. Es gab keine eigenständige fachliche Bedeutung, etwa verschlüsseltes Material, eine Referenz oder einen Schlüsselparameter. |
| Format | Erzeuger schrieben in beide Felder dieselbe Hex-Zeichenfolge, üblicherweise `0x(...)`. Unterschiedliche Decoder akzeptieren wie bisher teilweise rohe Hexwerte, ein fehlendes `ValueFormat` oder andere über `ValueFormat` unterstützte Kodierungen. Das war keine Trennung der beiden Eigenschaften nach Bedeutung oder Kodierung. |
| Erzeuger | AES, DES3 und HMAC, sämtliche vorhandenen KDF-Familien, interne KDF-PRF-Schlüssel, TR-31-Unwrap, Secret-Rekonstruktion und Check-Ergebnisobjekte schrieben beide Felder. Es gab keine notwendige eigene Konstruktorsemantik für `KeyValue`; betroffen waren vor allem Initialisierer und Zuweisungen in Factory-/Operationsmethoden. |
| Leser | KDF-, EMV- und EP2-Pfade bevorzugten häufig ein nichtleeres `KeyValue` und verwendeten `Value` nur als Fallback. AES-/DES3-TR-31 lasen `KeyValue` direkt. Klassische AES-/DES3-/HMAC-Modi, KCV und die Variablentabelle verwendeten bereits `Value`. |
| AST und Runtime | Die Argumentauflösung übergibt Variablenobjekte und projiziert deren `Value`. AST, Evaluatoren und Runtime benötigen keine eigene Materialeigenschaft und mussten nicht geändert werden. |
| GUI und Ausgabe | `DataVariablesViewModel.Add` übernimmt bereits `Value`. `PrintOutput` gab zusätzlich `KeyValue` aus; diese zweite Zeile entfällt. |
| Öffentliche Schnittstellen | Die C#-Eigenschaft war öffentlich. Die vorhandene JSON-Serialisierung nahm beide Eigenschaften auf. Hilfedokumentation und zahlreiche Tests beschrieben beziehungsweise prüften die Gleichheit. |

Es wurde keine fachliche Information gefunden, die nur durch eine zweite
Materialeigenschaft erhalten werden könnte. Die bestehende Interpretation
der Schlüsselbytes bleibt für konsistente KEY-Objekte erhalten.

## Gefundene Inkonsistenzen und Fehlerquellen

Es gab keinen Setter oder anderen Mechanismus, der beide Eigenschaften
automatisch synchronisierte. Eine Änderung von `Value` konnte deshalb KCV,
klassische Verschlüsselung und GUI aktualisieren, während KDF, EMV, EP2 oder
TR-31 weiterhin die alten Bytes aus `KeyValue` verwendeten. Umgekehrt änderte
ein Schreiben nur auf `KeyValue` diese Operationen, aber nicht die Anzeige
oder KCV-Berechnung.

Mehrere Tests veränderten absichtlich nur `KeyValue`, etwa für ungültige
Schlüssellängen in EMV, EP2 und TR-31. Diese Änderungen betreffen nun `Value`.
Einige direkt erzeugte Objekte enthielten nur `Value`, andere nur `KeyValue`;
dadurch hing ihre Verwendbarkeit vom jeweiligen Leser ab. Die betroffenen
Initialisierer verwenden jetzt einheitlich `Value`.

Die alten Gleichheitsprüfungen zwischen beiden Eigenschaften sind entfernt.
Konkrete Referenzwerte, Materiallängen, Metadaten und Rückgabeformate werden
weiter geprüft; es wurden keine nutzlosen Vergleiche von `Value` mit sich
selbst eingeführt.

## Umsetzung und Architektur

| Bereich | Wesentliche Änderung |
| --- | --- |
| `CryptoScript/Variables/KeyVariableDeclaration.cs` | Property und doppelte Ausgabezeile entfernt; kein neues Feld, kein Override von `Value`. |
| `CryptoScript/CryptoAlgorithm/AES`, `DES3`, `HMAC` | Redundante Materialzuweisungen in Generate/Import entfernt. |
| `CryptoScript/CryptoAlgorithm/KDF` | HKDF, SP800-108, DUKPT AES/TDEA, EMV Master A/B/C und AC Session sowie EP2-KDFs lesen und erzeugen ausschliesslich `Value`. Vorhandene Formatprüfung und temporäre Byteverarbeitung bleiben erhalten. |
| `CryptoScript/CryptoAlgorithm/EMV`, `EP2` | Materialdecoder lesen `Value`, einschliesslich der gemeinsam verwendeten EMV-AC-/Verify-/ARPC-Verträge und EP2-Verschlüsselungs-, MAC- und TR-31-Pfade. |
| `CryptoScript/CryptoAlgorithm/WRAPPERS` | AES-/DES3-TR-31 verwenden `Value` sowohl für Schutzschlüssel als auch transportiertes und entpacktes Material. Header, Metadaten und Decoder bleiben getrennt. |
| `CryptoScript/Operations` | Check-Kopien und Secret-Rekonstruktion setzen das Material einmal. Die berechneten KCV-Eigenschaften bleiben ungespeichert und ungecached. |
| Core- und GUI-Tests | Initialisierer, Mutationen und Assertions migriert; neue Regressionen für Materialänderungen, Serialisierung und Anzeige ergänzt. |
| `CryptoScript/InfoDocs` | Sechs Hilfeseiten dokumentieren den alleinigen Materialwert `Value` und die reduzierte Print-Ausgabe. |

`OperationInvocation`, `ResolvedCallArgument`, `AlgorithmCallArguments`,
die Resolver, AST und Evaluatoren bleiben unverändert. Es wurde keine
schlüsselwertbasierte Suche hinzugefügt. Bereits bestehende Legacy-Adapter
bleiben unverändert. Die Unterscheidung zwischen KEY, VAR, PARAM, PATH und
anderen Typen sowie die Regeln für Metadaten, Diagnostik und das Löschen
temporärer Schlüsselbytes wurden nicht verändert.

## Öffentliche API und verbleibende Grenzen

- C#-Aufrufer müssen `key.KeyValue` durch `key.Value` ersetzen und gegen die
  neue Version neu bauen. Dies ist eine bewusste Quell- und Binär-API-Änderung.
- Die bestehende `Serialize()`-Ausgabe enthält nur noch `Value` als
  Materialfeld. Bestehende konsistente JSON-Objekte mit beiden Feldern
  bleiben über `Value` lesbar; das unbekannte alte Feld wird vom bestehenden
  JSON-Verhalten ignoriert. Ein Test stellt sicher, dass auch ein
  widersprüchliches altes Feld `Value` nicht überschreibt.
- Externe JSON-Objekte, die ausschliesslich `KeyValue` enthalten, müssen
  vor der Übergabe auf `Value` migriert werden. Eine neue Importmigration
  oder Import-/Export-Infrastruktur wurde nicht eingeführt.
- Verbraucher, die die exakte Textausgabe auswerten, müssen mit einer
  KEY-Ausgabezeile weniger rechnen.
- `ValueFormat`, `KeySize` und die übrigen Metadaten sind weiterhin separat.
  Wer Material manuell verändert, muss wie bisher passende Formate und
  Metadaten bereitstellen. Decoder- und Metadatenprüfungen bleiben bestehen.
- GUI-Anzeige wurde durch ViewModel- und Avalonia-Headless-Tests geprüft;
  ein manueller Desktop-Durchlauf wurde nicht durchgeführt.

## Build und Regressionen

Umgebung: .NET SDK 10.0.401, Windows.

| Prüfung | Ergebnis |
| --- | --- |
| `CryptoWorkBench.sln` | Erfolgreich, 0 Fehler. |
| `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | Erfolgreich, 0 Fehler; der erste kompilierende GUI-Lauf meldete 9 Nullable-Warnungen in unveränderten GUI-Dateien. Der abschliessende inkrementelle Lauf meldete keine Warnungen. |
| Vollständige Core-Suite | **6.985 bestanden, 0 fehlgeschlagen, 0 übersprungen.** |
| Vollständige GUI-Suite | **223 bestanden, 0 fehlgeschlagen, 0 übersprungen.** |
| `git diff --check` | Erfolgreich. |
| Statische Suche im produktiven Quellcode und in aktuellen InfoDocs | Keine Treffer für das eigenständige Wort `KeyValue`. |

Ausgeführte Abschlussbefehle:

```powershell
dotnet build CryptoWorkBench.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity quiet
dotnet build CryptoWorkBenchAvalonia\CryptoWorkBenchAvalonia.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity quiet
dotnet test CryptoScriptUnitTest\CryptoScriptUnitTest.csproj --no-build --no-restore --logger 'trx;LogFileName=key-value-core.trx' --results-directory TestResults\KeyValueCleanup --verbosity quiet
dotnet test CryptoWorkBenchGuiTest\CryptoWorkBenchGuiTest.csproj --no-build --no-restore --logger 'trx;LogFileName=key-value-gui.trx' --results-directory TestResults\KeyValueCleanup --verbosity quiet
```

Lokale TRX-Nachweise liegen unter `TestResults/KeyValueCleanup/` und sind
gemäss Repository-Konfiguration nicht versioniert. Frühere Startversuche
trafen auf Compiler-Server-/Testhost-Verbindungsprobleme in der Umgebung;
die abschliessenden oben genannten Läufe waren erfolgreich. Zwei zunächst
fehlgeschlagene Print-Tests erwarteten noch die alte Zeilenzahl; sie prüfen
nun 15 statt 16 physische Zeilen einschliesslich einer mehrzeiligen Metadatenangabe.

Neue gezielte Regressionen:

- `CanonicalKeyMaterialTests`: Änderung eines generierten AES-Schlüssels
  über die Basisklassenreferenz; feste Referenzwerte für Encrypt, Decrypt,
  MAC und alle drei KCVs; Objektidentität, unveränderte Metadaten, Print und
  bestehende Serialisierung.
- Derselbe Testbereich: AES-/TDEA-TR-31 mit Argumentobjekten, die vor der
  Materialänderung erzeugt wurden; vollständige bestehende Referenzblöcke
  und Unwrap-Ergebnisse stimmen weiterhin.
- `DeriveHkdfIntegrationTests`: nachträgliche Änderung von `Value` auf dem
  ursprünglichen KEY liefert den bestehenden RFC-5869-Referenzwert.
- `KeyDeclarationTests`: altes JSON-Feld beeinflusst den kanonischen Wert
  nicht und erscheint nicht wieder in der Serialisierung.
- `DataVariablesViewModelTests` und `CryptoScriptExecutionPresentationTests`:
  aktueller Wert nach dem üblichen Tabellen-Refresh, keine zweite
  Materialzeile, weiterhin korrekte ScriptBook-Ausgabe für LF und CRLF.

Die vollständigen Suiten umfassen darüber hinaus die vorhandenen
Generate-, KDF-, EMV-, EP2-, PIN-, Wrap-/Unwrap- und KCV-Referenzvektoren
sowie Tests für gleichwertige Schlüsselbytes mit unterschiedlicher
Objektidentität und unterschiedlichen Metadaten.

## Einordnung verbliebener Suchtreffer

Eine repositoryweite Suche nach `KeyValue` ergibt noch bewusst erhaltene
historische Erwähnungen im AP5-Bericht und archivierten TRX-Nachweisen,
alte JSON-Testfixtures und Negativprüfungen auf die entfernte Property
beziehungsweise Ausgabezeile. Teilworttreffer wie `KeyValuePair`,
`masterKeyValue` und Testnamen über gleiche Schlüsselwerte sind keine
Materialeigenschaft des KEY-Modells. Fachlich unabhängige Bezeichner wurden
nicht pauschal entfernt.

## Git-Zustand nach Implementierung, vor Freigabe

Arbeitsbeginn auf `master`; einzig vorhandener unversionierter Eintrag war
`.codex/`. Dieser Eintrag und `.codex/config.toml` wurden nicht verändert.
In der Implementierungsphase wurden kein Commit und kein Push ausgeführt.

Das Abschlusskriterium ist erfüllt: KEY speichert Schlüsselmaterial genau
einmal in `Value`; sämtliche produktiven Aufrufer verwenden diesen Wert.

## Review und Freigabeprüfung vom 20. Oktober 2026

Der tatsächliche Arbeitsbaum-Diff wurde erneut gegen
`0a9582030619e0f1725c58b7eac55667d92fd70e` geprüft, einschliesslich sämtlicher
Teständerungen und der beiden neuen Dateien. Der Bericht war dabei nicht
die alleinige Prüfbasis.

| Schweregrad | Offene Findings |
| --- | --- |
| Critical | Keine |
| High | Keine |
| Medium | Keine |
| Low | Keine |

Es waren keine zusätzlichen Codekorrekturen erforderlich. Der Review
bestätigt insbesondere:

- KEY erbt weiterhin genau ein Materialfeld über `VariableDeclaration.Value`.
  Es gibt keine neue Materialkopie oder einen Alias im Modell.
- Metadatenzuweisungen, Formatdecoder und die Behandlung temporärer Bytes
  sind unverändert. Eine Eigenschaft `KeyPurpose` existiert in diesem
  Repository nicht; die vorhandene Verwendungsmetadatenstruktur `Usage`
  einschliesslich ihrer Policy bleibt erhalten.
- `AlgorithmArgumentResolver`, `KdfArgumentResolver` und die speziellen
  EMV-/EP2-Leser bevorzugen das originale KEY-Objekt. Die Materialdecoder
  lesen dessen aktuelles `Value`. Die unveränderten Argumentobjekte können
  weiterhin String-Snapshots enthalten; diese ersetzen bei strukturierten
  kryptografischen Schlüsselargumenten nicht das referenzierte Objekt.
  Reine Legacy-Wertaufrufe behalten ihre bisherige Snapshot-Semantik.
- Keine Änderungen an AST, Evaluatoren, Runtime-Dispatch oder Suchregeln.
  Keine neue Ausgabe von Schlüsselbytes und keine zusätzliche öffentliche
  API-Änderung neben der beauftragten Property-Entfernung.
- Entfernte Assertions betrafen ausschliesslich das redundante Materialfeld
  oder doppelte Erwartungen desselben Referenzwerts. Referenzvektoren,
  Negativtests, Metadatenprüfungen und Identitätsprüfungen bleiben erhalten.
- JSON mit `Value` wird weiterhin deserialisiert; das entfernte Feld wird
  nicht serialisiert. Die GUI zeigt den aktuellen Wert beim bestehenden
  Ausführungs-Refresh; es wurde keine neue Live-Binding-Semantik eingeführt.

Beide Solutions wurden mit `-t:Rebuild --no-restore -m:1
-p:UseSharedCompilation=false` vollständig neu kompiliert. Core:
0 Warnungen, 0 Fehler. GUI: 9 bestehende Nullable-Warnungen, 0 Fehler:

| Datei | Stelle | Warnung |
| --- | --- | --- |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | 25 | CS8618, `_insightWindow` |
| dieselbe Datei | 114, 159 | Je einmal CS8625 |
| `CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs` | 18 | CS8618, `_infoText` |
| `CryptoWorkBenchAvalonia/ViewModels/MainViewModel.cs` | 19 | Viermal CS8618 für die History-Commands |
| `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` | 140 | CS8602 |

Alle genannten Dateien sind gegenüber HEAD unverändert. Die vollständigen
Tests wurden nach den Rebuilds erneut ausgeführt: **6.985 Core-Tests und
223 GUI-Tests bestanden, keine Fehler und keine übersprungenen Tests**.
Nachweise: `TestResults/KeyValueCleanup/review-core.trx`,
`review-gui.trx`, `review-build-core.log` und `review-build-gui.log`.
`git diff --check` ist erfolgreich; die erneute produktive Suche nach
`KeyValue` findet keine Verwendung. Git meldete bei der Diff-Ausgabe
LF/CRLF-Hinweise aufgrund der vorhandenen Windows-Konfiguration; es wurde
keine repositoryweite Zeilenendenkonvertierung durchgeführt.

Die Freigabe gilt für genau einen Commit
`Remove redundant KeyValue from KEY model` und einen regulären Push auf
`origin/master`. Lokale Codex-Konfiguration und ignorierte Build-/Testdateien
werden nicht aufgenommen. Commit-SHA, Push-Ergebnis und der erst durch den
Push ausgelöste CI-Lauf werden im abschliessenden Ausführungsbericht im Chat
dokumentiert.

## Dateiinventar

Die folgende Liste umfasst die Änderungen dieses Auftrags einschliesslich
der angepassten Testaufrufer.

- `CryptoScript/CryptoAlgorithm/AES/AES.cs`
- `CryptoScript/CryptoAlgorithm/DES3/DES3.cs`
- `CryptoScript/CryptoAlgorithm/EMV/MAC_EMV_AC.cs`
- `CryptoScript/CryptoAlgorithm/EP2/Ep2DataEncryption.cs`
- `CryptoScript/CryptoAlgorithm/EP2/Ep2Mac.cs`
- `CryptoScript/CryptoAlgorithm/EP2/Ep2PanReceiptEncryption.cs`
- `CryptoScript/CryptoAlgorithm/EP2/Ep2Tr31KeyBlock.cs`
- `CryptoScript/CryptoAlgorithm/HMAC/HMAC.cs`
- `CryptoScript/CryptoAlgorithm/KDF/AesDukptDerivation.cs`
- `CryptoScript/CryptoAlgorithm/KDF/DUKPT_AES_INITIAL_KEY.cs`
- `CryptoScript/CryptoAlgorithm/KDF/DUKPT_AES_WORKING_KEY.cs`
- `CryptoScript/CryptoAlgorithm/KDF/DUKPT_TDEA_INITIAL_KEY.cs`
- `CryptoScript/CryptoAlgorithm/KDF/DUKPT_TDEA_WORKING_KEY.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_AC_SESSION.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_A.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_B.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_C.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_PAN_RECEIPT_TRM.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_PAN_RECEIPT_TRX.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_PAN_SURROGATE_TRX.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_SESSION.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_HKDF.cs`
- `CryptoScript/CryptoAlgorithm/KDF/KDF_SP800_108_COUNTER.cs`
- `CryptoScript/CryptoAlgorithm/WRAPPERS/WrapAESTR31.cs`
- `CryptoScript/CryptoAlgorithm/WRAPPERS/WrapDES3TR31.cs`
- `CryptoScript/InfoDocs/Info.Functions.md`
- `CryptoScript/InfoDocs/Info.Mech.KDF-EMV-AC-SESSION.md`
- `CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-A.md`
- `CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-B.md`
- `CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-C.md`
- `CryptoScript/InfoDocs/Info.Mech.KDF-SP800-108-COUNTER.md`
- `CryptoScript/Operations/KeyCheckOperations.cs`
- `CryptoScript/Operations/SecretOperations.cs`
- `CryptoScript/Variables/KeyVariableDeclaration.cs`
- `CryptoScriptUnitTest/AesDes3SemanticDiagnosticsTests.cs`
- `CryptoScriptUnitTest/AesDocumentationTests.cs`
- `CryptoScriptUnitTest/Ap5SemanticDiagnosticsTests.cs`
- `CryptoScriptUnitTest/Ap5TAnalysisBackendTests.cs`
- `CryptoScriptUnitTest/CanonicalKeyMaterialTests.cs`
- `CryptoScriptUnitTest/DeriveArgumentResolutionTests.cs`
- `CryptoScriptUnitTest/DeriveHkdfIntegrationTests.cs`
- `CryptoScriptUnitTest/Des3DocumentationTests.cs`
- `CryptoScriptUnitTest/DukptAesInitialKeyTests.cs`
- `CryptoScriptUnitTest/DukptAesWorkingKeyTests.cs`
- `CryptoScriptUnitTest/DukptTdeaInitialKeyTests.cs`
- `CryptoScriptUnitTest/DukptTdeaWorkingKeyTests.cs`
- `CryptoScriptUnitTest/EmvAcMechanismTests.cs`
- `CryptoScriptUnitTest/EmvAcSessionMechanismTests.cs`
- `CryptoScriptUnitTest/EmvAcVerifyMechanismTests.cs`
- `CryptoScriptUnitTest/EmvAlgorithmSelectionContractTests.cs`
- `CryptoScriptUnitTest/EmvArpcIntegrationTests.cs`
- `CryptoScriptUnitTest/EmvMasterOptionAMechanismTests.cs`
- `CryptoScriptUnitTest/EmvMasterOptionBMechanismTests.cs`
- `CryptoScriptUnitTest/EmvMasterOptionCDocumentationTests.cs`
- `CryptoScriptUnitTest/EmvMasterOptionCMechanismTests.cs`
- `CryptoScriptUnitTest/Ep2DataEncryptionTests.cs`
- `CryptoScriptUnitTest/Ep2KdfDocumentationTests.cs`
- `CryptoScriptUnitTest/Ep2MacTests.cs`
- `CryptoScriptUnitTest/Ep2PanReceiptEncryptionTests.cs`
- `CryptoScriptUnitTest/Ep2Tr31KeyBlockTests.cs`
- `CryptoScriptUnitTest/GenerateKeyContractTests.cs`
- `CryptoScriptUnitTest/HmacSetupTests.cs`
- `CryptoScriptUnitTest/KdfDocumentationTests.cs`
- `CryptoScriptUnitTest/KdfEp2PanReceiptTrmTests.cs`
- `CryptoScriptUnitTest/KdfEp2PanReceiptTrxTests.cs`
- `CryptoScriptUnitTest/KdfEp2PanSurrogateTrxTests.cs`
- `CryptoScriptUnitTest/KdfEp2SessionTests.cs`
- `CryptoScriptUnitTest/KdfSp800108CounterTests.cs`
- `CryptoScriptUnitTest/KeyCheckValueTests.cs`
- `CryptoScriptUnitTest/KeyDeclarationTests.cs`
- `CryptoScriptUnitTest/KeyMechanismReaderMigrationTests.cs`
- `CryptoScriptUnitTest/KeyMetadataDiagnosticTransportTests.cs`
- `CryptoScriptUnitTest/KeyMetadataValidatorTests.cs`
- `CryptoScriptUnitTest/KeyTypeMigrationTests.cs`
- `CryptoScriptUnitTest/PinBlockSecurityRegressionTests.cs`
- `CryptoScriptUnitTest/PinBlockSemanticDiagnosticsTests.cs`
- `CryptoScriptUnitTest/Tr31AesCharacterizationTests.cs`
- `CryptoScriptUnitTest/Tr31AesVersionTests.cs`
- `CryptoScriptUnitTest/Tr31HeaderAuthorityTests.cs`
- `CryptoScriptUnitTest/Tr31ScriptIntegrationTests.cs`
- `CryptoScriptUnitTest/WrapperTests.cs`
- `CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs`
- `CryptoWorkBenchGuiTest/DataVariablesViewModelTests.cs`
- `docs/architecture/KEY-Value-cleanup-report.md`
