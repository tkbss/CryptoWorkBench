# TR-31 Header Builder: Korrekturbericht

Datum: 20. Oktober 2026. Ausgangspunkt: lokaler, uncommitteter Arbeitsstand auf `master`, HEAD `a713ea83c00ceb5b95d3583569d14b54cce5afd1`.

## Ergebnis und Abschlussreview

Die fünf beauftragten Findings sind korrigiert. Die strukturierte Headerdefinition, Objektbindung, automatische KBLEN-Berechnung und die kryptografischen Binding-Verfahren bleiben erhalten.

| ID | Korrektur | Regressionstest in `CryptoScriptUnitTest/Tr31ReviewRegressionTests.cs` | Ergebnis |
|---|---|---|---|
| H-01 | Gemeinsame vollständige Auswertung der zwei Längenbytes; positive, byteausgerichtete, verfügbare Schlüssellänge; A/T-Algorithmusgrößen nach Authentifizierung prüfen | `H01_AuthenticatedInvalidKeyLengthsNeverPublishKey`, `H01_AuthenticatedSupportedKeyLengthsRemainExact`, `H01_TdeaReadersRejectAuthenticatedInvalidLengths` | Pass |
| H-02 | Zentraler vollständiger Wire-Parser: exakte KBLEN, vollständige Hexbytes, Grenzen optionaler Blöcke, nichtleerer ausgerichteter Ciphertext, versionsgerechter MAC; Composite-Grenzen zusätzlich vergleichen | `H02_AuthenticatedD0145RejectsDiscardedLastCharacter`, `H02_AllVersionsRejectIncompleteOrExtraWireData`, `H02_CompositeCannotMoveOrTruncateCiphertextMacBoundary` | Pass |
| H-03 | DA ausschließlich für B3; zentrale Registry, bestehender Positivtest und Attributdokumentation berichtigt | `H03_DaIsRejectedOutsideB3`, `H03_B3StillRequiresCompleteDaData`; bestehender B3-Positivfall in `CreateHeaderTests.EverySupportedOptionalStructure` | Pass |
| M-01 | Historische Literal- und JSON-Darstellung getrennt strukturell prüfen; genau eine unterschiedliche Bytefolge zulassen; anschließend unveränderte MAC-Prüfung ohne Interpretationswechsel | `M01_LegacyAndEscapedRepresentationsAuthenticateExactHeaderBytes` | Pass |
| M-02 | Themen wieder als ID lexen und im Info-Kontext auflösen; nackte Funktionsnamen nur im Info-Aufruf zulassen | `M02_InfoTopicsRemainUsableVariableNames`, `M02_BareFunctionNamesAreNotGeneralArguments`; bestehende Info-/Completion-/Navigationstests | Pass |

Die erste Fassung der Regressionstests wurde **vor den Produktkorrekturen** ausgeführt: 35 Fälle, davon 25 fehlgeschlagen und zehn bestanden. Die Fehler betrafen alle fünf Findings. Danach wurden sieben zusätzliche Fälle für Composite-Grenzen und TDEA-Leser ergänzt. Die endgültigen 42 neuen Fälle bestehen vollständig. Die authentifizierten Negativblöcke werden unabhängig von der normalen Writer-Längenvalidierung aus bewusst ungültigen Klartextstrukturen aufgebaut. Vorhandene unabhängige Referenzvektoren bleiben unverändert und bestehen weiterhin.

## Technische Änderungen und Legacy-Verträge

### H-01

Die fehlerhafte `WrapAESTR31.ParseBinaryKeyData()`-Sonderlogik ist entfernt. AES verwendet nun dieselbe `Tr31ConfidentialData.ExtractKey()`-Interpretation wie die TDEA-Kerne. Null Bit, nicht byteausgerichtete Werte und Längen außerhalb des verfügbaren Klartexts werden abgelehnt.

Nach erfolgreicher Authentifizierung prüfen die allgemeinen Adapter die technischen Größen der authentifizierten Algorithmusklasse: AES 128/192/256 Bit, TDEA 128/192 Bit. Die Klassifizierung entspricht auch bei kleinen Buchstaben der bestehenden KEY-Typzuordnung. Andere bisher opaque gelesene Algorithmuscodes bleiben opaque; es entstehen keine neuen Writer-Profile oder asymmetrischen/HMAC-Mechanismen. Auch diese Legacy-Leser erhalten die gemeinsame positive, byteausgerichtete Längenprüfung.

### H-02

`TR31Block.FromString()` erwartet jetzt einen vollständigen A/B/C/D-Wire-Block. Es gibt keine erfolgreiche Header-only-/MAC-only-Rückgabe mit fehlenden Nutzdaten und kein Ignorieren von Zeichen hinter KBLEN. Ciphertext und MAC werden vollständig als Hex dekodiert; Header und Optional-Block-Text bleiben ASCII und werden nicht als Hex interpretiert.

Die Darstellung im Composite-Format muss dieselben Header-, Ciphertext- und MAC-Grenzen ergeben wie der vollständige Wire-Parser. Ein Verschieben von Bytes zwischen den separat angegebenen Feldern ist nicht zulässig.

Bewusste Vertragsverschärfungen:

- Der bisherige Charakterisierungstest zur Akzeptanz von `DEADBEEF` hinter KBLEN prüft jetzt die Ablehnung.
- Die fünf bisherigen strukturellen MAC-only-Grenzfälle bleiben als Tests erhalten und prüfen jetzt die Ablehnung.
- Die RSA-Testkonstante enthält einen zusätzlichen Quelltext-Zeilenumbruch hinter ihren 3776 Wire-Zeichen. Die Konstante selbst bleibt unverändert; die beiden positiven Tests übergeben ausdrücklich nur die Wire-Zeichen. Produktionsleser entfernen keine Zeichen innerhalb einer gequoteten Wire-Darstellung.
- Bestehende Mechanismus-/Versionsdiagnosen und präzise Composite-MAC-Längendiagnosen bleiben erhalten.

Der allgemeine Raw-Unwrap-Vertrag ohne Vergleich eines erwarteten Raw-Headers bleibt bestehen. Seine strukturelle Eingabevalidierung ist jetzt ebenfalls strikt. EP2 behält seinen eigenen Leser und seine strengeren Profilregeln.

### H-03

Die lokale ANSI X9.143-2022, Abschnitt 6.3.6.4, Druckseite 36, bestätigt die Beschränkung von DA auf B3. B0 und B1 werden abgelehnt. Die Suche in den betroffenen InfoDocs und dem ursprünglichen Implementierungsbericht fand nach der Korrektur keine verbleibende B0/B1/B3-Freigabe für DA.

### M-01

Es gibt genau zwei definierte Darstellungen:

1. Historisch: unveränderte Zeichen zwischen äußeren Anführungszeichen.
2. Neu: JSON-String-Dekodierung.

Identische Ergebnisse werden dedupliziert. Jede unterschiedliche Interpretation muss durch den vollständigen Wire-Parser einschließlich KBLEN und bei Composite-Eingaben durch den Feldgrenzenvergleich passen. Null oder mehrere gültige Ergebnisse führen zum Fehler. Die eindeutige Bytefolge wird danach kryptografisch authentifiziert; es gibt weder einen Fallback nach MAC-Fehler noch Normalisierung nach Authentifizierung.

Die Tests decken alle vier Versionen, rohe und typisierte Parameter, beide Wire-Darstellungen, Composite-Darstellungen, einen Backslash, zwei Backslashes, wörtliches `\n`/`\t`, Anführungszeichen und andere Labeltrennzeichen ab. Manipulierte Labels werden abgelehnt.

### M-02

`tr31` und `tr31attributes` sind keine globalen INFO-Token mehr. `Info(tr31)` wählt auch bei gleichnamiger Variable das Dokument; `Print(tr31)` verwendet die Variable. Für `Info(CreateHeader)` verwendet die Grammatik eine auf Info beschränkte Funktionsnamenalternative. `Print(CreateHeader)` und `Print(GenerateKey)` sind weiterhin ungültig.

## Validierung

| Prüfung | Ergebnis |
|---|---|
| Neue Regressionstests | 42 bestanden, keine Fehler/Skips |
| Vollständiger Core-Rebuild | Erfolgreich, 0 Warnungen, 0 Fehler |
| Vollständiger GUI/Desktop-Rebuild | Erfolgreich, 9 bestehende Warnungen, 0 Fehler |
| Vollständige Core-Suite | **7109 bestanden**, 0 fehlgeschlagen, 0 übersprungen; Baseline 7067 |
| Vollständige GUI-Suite | **225 bestanden**, 0 fehlgeschlagen, 0 übersprungen |
| Bestehende Referenzvektoren | Unverändert, Tests bestanden |
| ANTLR-Reproduzierbarkeit | 10/10 generierte Artefakte byteidentisch nach identischer Entfernung generierter Zeilenend-Leerzeichen |
| InfoDocs-Auslieferung | 9/9 SHA-256-Vergleiche identisch: drei Dokumente in Core-, Desktop- und GUI-Test-Ausgabe |
| `git diff --check` | Erfolgreich |

Die neun GUI-Warnungen betreffen bestehende Nullable-Stellen in `CryptoScriptEditView.axaml.cs`, `KeyMapViewModel.cs`, `MainViewModel.cs` und `SidebarViewModel.cs`. Diese Dateien wurden durch die Korrekturen nicht verändert.

Die vollständige Suite deckt auch vorhandene KEY-Diagnostik, EP2-Verträge, Headerwiederverwendung und Dokumentationsbeispiele ab. Der Abschlussreview überprüfte insbesondere die vollständige Verarbeitung aller Wire-Zeichen, die MAC-Prüfung vor Schlüsselveröffentlichung, die eindeutige Darstellungswahl sowie die Begrenzung der neuen Info-Grammatik.

Ignorierte Nachweise:

- `CryptoScriptUnitTest/TestResults/corrections-before.trx`
- `CryptoScriptUnitTest/TestResults/corrections-focused-final.trx`
- `CryptoScriptUnitTest/TestResults/corrections-core-final.trx`
- `CryptoWorkBenchGuiTest/TestResults/corrections-gui-final.trx`
- Build-/Testlogs mit Präfix `corrections-` unter `CryptoScriptUnitTest/TestResults/`
- Unabhängig regenerierte ANTLR-Artefakte unter `CryptoScriptUnitTest/obj/antlr-corrections-repro/`

Ein zusätzlicher Validierungslauf schlug zunächst fehl, weil die generierten ANTLR-Vergleichsdateien unter `TestResults` vom SDK als C#-Quellen erfasst wurden. Die eigenen Hilfsdateien wurden nach `obj` verschoben; Produktcode und Projektkonfiguration blieben dabei unverändert. Der Fehlerlog ist als `corrections-artifact-location-failure.log` erhalten. Die oben ausgewiesenen Ergebnisse stammen aus der anschließenden erneuten Validierung.

## KCV-Frage – unverändert und separat freizugeben

Der aktuelle Vertrag für KC/KP/PK bleibt unverändert:

- Legacy-KCV: Präfix `00` plus genau sechs Hexziffern.
- CMAC-KCV: Präfix `01` plus genau zehn Hexziffern.
- Bestehende Einschränkungen nach transportiertem Algorithmus beziehungsweise aktueller Schutzversion bleiben erhalten.

ANSI X9.143-2022, Tabellen 15 und 21, verweist für die variablen KCV-Längen auf ANSI X9.24-1. Die lokal geprüfte Ausgabe ANSI X9.24-1-2017, Anhang A.2 und A.3.1, beschreibt bis zu 24 Bit für Legacy beziehungsweise bis zu 40 Bit für CMAC. Die aktuelle Runtime verwendet jeweils die dokumentierte maximale Länge.

Eine Erweiterung müsste die zulässigen kürzeren Hex-/Bitlängen, eine Mindestlänge, die Darstellung nicht byteausgerichteter Werte und die Profileinschränkungen explizit festlegen. Danach wären Registry, Dokumentation und unabhängige positive/negative Fälle für KC/KP/PK anzupassen. Keine KCV-Validierungsänderung wurde vorgenommen; diese Frage ist kein behobenes Finding.

## Geänderte Dateien und Git

Zusätzlich zu den bereits vorhandenen Builder-Änderungen wurden in diesem Auftrag bearbeitet:

- `CryptoScript/CryptoAlgorithm/WRAPPERS/`: `TR31Block.cs`, `Tr31ConfidentialData.cs`, `WrapAESTR31.cs`, `WrapDES3TR31.cs`, `Tr31AttributeRegistry.cs`.
- `CryptoScript/Variables/TR31String.cs`.
- `CryptoScript/Antlr/`: `CryptoLexer.g4`, `CryptoScript.g4` und die durch ANTLR regenerierten Dateien.
- `CryptoScript/Model/AntlrToFunctionCallExpression.cs`.
- `CryptoScript/InfoDocs/`: `Info.TR31.md`, `Info.Func.CreateHeader.md`, `Info.TR31Attributes.md`.
- `CryptoScriptUnitTest/`: neue `Tr31ReviewRegressionTests.cs`; angepasst `CreateHeaderTests.cs`, `Tr31BlockParsingTests.cs`, `Tr31ConfidentialDataTests.cs`, `Tr31AesCharacterizationTests.cs`, `WrapperTests.cs`.
- `docs/architecture/`: Nachtrag in `TR31-header-builder-report.md`, dieser neue Korrekturbericht.

Kein Test und kein Referenzvektor wurde entfernt. Die vorhandenen uncommitteten Änderungen bleiben erhalten. Es gab kein `git add`, Commit, Push, Merge oder Rebase. `.codex/` und `.codex/config.toml` wurden weder gelesen noch verändert.

Abschließender Status: weiterhin `master` und derselbe HEAD; leerer Index, 55 geänderte versionierte Dateien und zwölf neue Dateien aus Implementierung/Korrektur einschließlich der Berichte. Zusätzlich erscheint das bereits vorhandene unversionierte `.codex/` als eigener Statuseintrag.

Offen bleiben die separat zu entscheidende KCV-Erweiterung und die unabhängige Folgeprüfung. Es wurde kein weiteres korrekturbedürftiges Problem innerhalb des fünfteiligen Korrekturumfangs festgestellt.

**READY FOR INDEPENDENT REVIEW**
