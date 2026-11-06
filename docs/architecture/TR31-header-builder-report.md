# TR-31 Header Builder: Implementierung und Validierung

Datum: 20. Oktober 2026. Plattform: .NET 10.

Nachtrag: Dieser Bericht beschreibt den ursprünglichen Implementierungsstand. Der unabhängige Review fand fünf korrekturbedürftige Punkte. Korrekturen, verschärfte Leseverträge, neue Regressionstests und aktuelle Validierung sind im [Korrekturbericht](TR31-review-corrections-report.md) dokumentiert; dessen Ergebnisse ersetzen die ursprüngliche Freigabebewertung und Testzahlen.

Die Zielkette ist implementiert und getestet: `Info(tr31)` → `Info(tr31attributes)` / `Info(CreateHeader)` → `TR31H` → `#BLKH:header` → Wrap mit automatisch berechnetem KBLEN → authentifiziertes Unwrap. Es wurden keine Commits, Pushes oder Merges durchgeführt.

## A. Architektur

### Zuständigkeiten

| Komponente | Zuständigkeit |
|---|---|
| `Tr31AttributeRegistry` | Zentrale semantische Regeln des unterstützten symmetrischen Profils: Feldnamen, KEYU/ALGO/MODEU/Versionskombinationen, bekannte und unterstützte Optional-Block-IDs, Datenformate |
| `Tr31HeaderDefinition` | Unveränderliche strukturierte Definition; feste Attribute, geordnete Optional Blocks, erwartete Länge, ungelöster Längenstatus; endgültige Serialisierung pro Wrap |
| `OptionalBlock.Serialize()` | Gemeinsame Kurz-/Langformat-Serialisierung der vorhandenen OptionalBlock-Klasse |
| `Tr31HeaderBinding` | Verbindet einen konkreten Wrap/Unwrap mit der Headerdefinition; prüft Raw-Längen ohne Umschreiben; vergleicht typisierte Header nach Authentifizierung |
| `BlockHeaderVariableDeclaration` | Vorhandener Variablentyp mit korrektem `CryptoTypeTR31Header`, strukturierter Definition und verständlicher Print-Darstellung |
| `HeaderOperations` | Eng begrenzter CreateHeader-Dispatch ohne kryptografische Operation |
| Parser/AST/Evaluator | Benannte Argumente ausschließlich für CreateHeader; Brace-Initialisierer verwendet denselben Builder |

`CryptoType.Parse("TR31H")` funktioniert. `ArgumentParameter` und `ParameterVariableDeclaration` halten die echte Headerreferenz. Der Header wird nicht anhand eines gleichen Zeichenkettenwerts gesucht. Nachträgliche Neudeklaration eines gleichnamigen Headers ersetzt die bereits in einem PARAM gespeicherte Objektreferenz nicht.

Die bestehende `Tr31BlockHeader`-Klasse bleibt für den alten `BlockHeader()`-Anzeigepfad kompatibel. Sie war keine vollständige Wire-Header-Implementierung und wird ausdrücklich nicht als validierte CreateHeader-Definition verwendet. Die vorhandenen kryptografischen A/C-, B- und D-Kerne sowie `Tr31ConfidentialData` bleiben die einzigen Implementierungen von Binding, Obfuskation und Padding.

### Länge und Unveränderlichkeit

Die endgültige Länge wird aus der tatsächlich erzeugten vertraulichen Datenstruktur berechnet:

`Headerlänge + 2 × (Ciphertextbytes + versionsabhängige MAC-Bytes)`

Diese Länge wird vor MAC-Berechnung und Verschlüsselung eingesetzt. Wrap authentifiziert und liefert dieselben Headerbytes. Eine ausdrücklich vorgegebene KBLEN ist ausschließlich eine Erwartung. NUMOPTB wird einschließlich PB ermittelt und gegebenenfalls gegen die ausdrückliche Erwartung geprüft.

Attribute sind schreibgeschützt. Die vorhandene mutable OptionalBlock-Klasse wird an der Definitionsgrenze defensiv kopiert. Wrap erzeugt lokale Strings und ändert weder TR31H noch PARAM. Eine Definition bleibt auch nach erfolgreichem Wrap ungelöst und wiederverwendbar.

## B. Funktion und Grenzen

Unterstützte Hauptsyntax:

```cryptoscript
KEY kbpk = GenerateKey(AES-CBC, 256)
KEY transportKey = GenerateKey(AES-CBC, 128)
TR31H header = CreateHeader(
    KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E,
    OPTID:LB, OPTBD:"Test"
)
PARAM p = #MECH:WRAP-AES-TR31 #BLKH:header
VAR wrapped = Wrap(p, kbpk, transportKey)
KEY recovered = Unwrap(p, kbpk, wrapped)
```

Pflichtfelder: KBVID, KEYU, ALGO, MODEU, EXP. Defaults: KEYVN=00, KEYCTX=0, RSV=0. KBLEN/NUMOPTB sind automatisch; widersprüchliche ausdrückliche Angaben blockieren. Wiederholte OPTID-/OPTBD-Paare erhalten ihre Reihenfolge. Doppelte IDs, unbekannte Namen und nicht unterstützte Strukturen werden abgelehnt.

| Schutzversion | Mechanismus | Transportierte Schlüssel |
|---|---|---|
| A/B/C | WRAP-DES3-TR31 | TDEA 128/192 Bit |
| D | WRAP-AES-TR31 | AES 128/192/256 und TDEA 128/192 Bit |

Die 17 Lexer-IDs sind vollständig inventarisiert. Unterstützt werden BI, DA, IK, KC, KP, KS, KV, LB, PA, PB, PK, TC, TS und WP. AL, CT und HM werden mit einer Diagnose abgelehnt, da die zugrunde liegenden asymmetrischen bzw. HMAC-Transportprofile nicht implementiert sind. Proprietäre numerische IDs werden nicht stillschweigend als Text übernommen.

Optional Blocks verwenden kurze oder erweiterte Längen (`00` + `04` + vier Hexziffern). PB wird nötigenfalls angehängt; ausdrückliches PB muss zuletzt stehen und bereits korrekt ausrichten. Maximalwerte und Gesamtüberlauf werden geprüft.

Die Referenzseite dokumentiert eine bewusst begrenzte Runtime-Allowlist. Insbesondere werden historische A-Derivationskombinationen mit X/Y, I0-Daten sowie asymmetrische/HMAC-Profile nicht neu freigeschaltet. Diese Grenzen gelten nicht rückwirkend als neue semantische Validierung aller alten Raw-Header.

KC/KP/PK werden strukturell geprüft, nicht aus Schlüsselmaterial berechnet. DA/PK/WP werden als authentifizierte Metadaten transportiert; die Workbench implementiert keine SCD-Policy-Engine für zukünftige Ableitungen/Exporte oder attestierte Schlüsselhistorie.

### Kompatibilität

- Raw-Header behalten ihre Bytes und ihre ausdrückliche Länge; Wrap überschreibt sie nicht.
- Bestehende Referenzvektoren wurden nicht verändert. Die vollständige Suite prüft weiterhin ihre bitgenauen Ergebnisse.
- Beim typisierten Unwrap wird nach MAC-Prüfung der vollständige Header exakt verglichen. Nur die automatisch offene KBLEN wird aus dem authentifizierten Header eingesetzt; die tatsächliche empfangene Wire-Länge muss ebenfalls stimmen. Eine ausdrückliche Erwartung muss passen.
- Allgemeine Raw-Unwrap-Adapter behalten ihr bisheriges Verhalten ohne zusätzlichen Headervergleich. EP2 behält seinen eigenen optionalen exakten authentifizierten Raw-Headervergleich.
- EP2 akzeptiert keine TR31H-Referenzen; seine strengeren Profilregeln werden nicht durch den allgemeinen Builder ersetzt.
- Die frühere AES-Composite-Leselücke bei Optional Blocks ist behoben. Composite und vollständige Wire-Form funktionieren einschließlich druckbarer Sonderzeichen in Labels.
- Die Zielintegration ist die direkte PARAM-Deklaration. Der bestehende stringbasierte `Parameters(...)`-Generator wurde nicht in ein allgemeines Objektparametersystem umgebaut.
- Import/Export und automatische Attribut-Completion sind nicht implementiert. Die Definition liegt strukturiert vor; Funktions- und Info-Completion sind integriert.

## C. Dokumentation

Neue ausgelieferte Seiten:

- `CryptoScript/InfoDocs/Info.TR31.md`
- `CryptoScript/InfoDocs/Info.Func.CreateHeader.md`
- `CryptoScript/InfoDocs/Info.TR31Attributes.md`

Katalog, Dokumentidentität, Provider und kanonische URIs sind in der bestehenden Infrastruktur ergänzt. Bestehende numerische Dokumenttyp-IDs bleiben erhalten. Der unveränderte InfoViewModel-Navigationspfad unterstützt die neuen Dokumente und Zurück-Navigation.

Die Attributseite enthält alle zwölf Builder-Feldnamen, die vollständige Runtime-KEYU-Kombinationstabelle, alle Lexer-Optional-Block-IDs, genaue Kodierungen, Defaults, Versionsgrenzen und Beispiele. Tests vergleichen dokumentierte Namen und Kombinationen mit den Metadaten. Alle `cryptoscript`-Beispielblöcke der drei Seiten werden ausgeführt; interne Links werden auf auflösbare und ausgelieferte Dokumente geprüft.

Die drei TR-31-Mechanismusseiten sowie Functions, Parameters und Types sind aktualisiert. Die feste Überschriftenstruktur der Mechanismusseiten bleibt erhalten. Die Info-Completion bietet `tr31`, `CreateHeader`, `tr31attributes`; für CreateHeader wird das vorhandene Funktionsoverlay verwendet.

Normgrundlage der Implementierungsprüfung: lokale ANSI X9.143-2022, insbesondere Tabellen 1–7 und 9–24 sowie Abschnitte 6.2–6.4. Die lizenzierte Norm wurde nur gelesen und nicht ins Repository kopiert. Die [Verlagsvorschau](https://store.accuristech.com/products/preview/3088609) bestätigt Ausgabe und Titel; technische Regeln wurden aus der vorhandenen lokalen Ausgabe geprüft.

## D. Tests und Builds

| Prüfung | Endergebnis |
|---|---|
| Core-Build, .NET 10 | Erfolgreich |
| GUI/Desktop-Build, .NET 10 | Erfolgreich |
| Vollständige Core-Suite | **7.067 bestanden, 0 fehlgeschlagen, 0 übersprungen** |
| Vollständige GUI-Suite | **225 bestanden, 0 fehlgeschlagen, 0 übersprungen** |
| Neue Tests | 82 Core-Testfälle und 2 GUI-Testfälle |
| ANTLR-Reproduzierbarkeit | 10 generierte Artefakte identisch |
| InfoDocs-Auslieferung | 9 Dateivergleiche: Core, Desktop und GUI-Test-Ausgabe byteidentisch zur Quelle |
| `git diff --check` | Erfolgreich |

Neue Tests decken Parser/AST/Runtime, Identität, Print, Brace-Syntax, Pflichtwerte, Defaults, Fehlkombinationen, alle unterstützten Optional-Block-Strukturen, erweiterte Längen, Padding, Erwartungen, Überläufe, unveränderte Eingaben und wiederholtes Wrap ab. Die Transportmatrix enthält alle unterstützten Schlüsselgrößen, alle vier Versionen und jeweils null/einen/mehrere Optional Blocks.

Zusätzlich geprüft: unabhängige AES/TDEA-Referenzbytes, falsche Header/Optional-Block-Daten/MACs, typisierter Erwartungsvergleich, angehängte Wire-Daten, Sonderzeichen-Roundtrips, EP2-Ablehnung sowie echte Info-Aufrufe, URIs, Completion, Deployment und Zurück-Navigation.

Die bestehenden Tests wurden nicht entfernt. Charakterisierungstests der ehemals fehlenden TR31H-Typauflösung und feste Katalog-/Completion-Erwartungen wurden auf die neue Funktion angepasst.

### Ausgeführte Befehle

```text
java -jar antlr4-4.13.2-complete.jar -Dlanguage=CSharp -visitor CryptoScript.g4
dotnet build CryptoScript/CryptoScript.csproj --no-restore
dotnet build CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj --no-restore
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore
git diff --check
git status --short
```

ANTLR wurde im Verzeichnis `CryptoScript/Antlr` mit der bereits vorhandenen Version 4.13.2 ausgeführt; nachlaufende Leerzeichen der generierten C#-Dateien wurden wie im Repositoryverfahren entfernt. Keine manuelle Bearbeitung generierter Parserlogik.

Lokale Ergebnisdateien (TestResults sind nicht Bestandteil des Commits):

- `CryptoScriptUnitTest/TestResults/tr31-core.trx`
- `CryptoWorkBenchGuiTest/TestResults/tr31-gui.trx`
- Build-/Testprotokolle unter `CryptoScriptUnitTest/TestResults/tr31-*.log`

### Fehlgeschlagene Zwischenläufe

Der erste Sandbox-Testlauf wurde vor der Testausführung abgebrochen: VSTest konnte nach 90 Sekunden keine Verbindung zum Testhost aufbauen. Ein Sandbox-GUI-Build endete ohne Compilerdiagnose. Außerhalb der Sandbox liefen Builds und Tests.

Der erste ausführbare Core-Lauf meldete fünf Fehler: zu breite neue Parseralternative, zwei zusätzliche nicht erlaubte Level-2-Dokumentüberschriften, verschobene Dokumenttyp-Nummern und einen geänderten Diagnosewortlaut. Diese wurden behoben. Der erste GUI-Lauf hatte eine veraltete Completion-Mengen-Erwartung; sie wurde um die drei neuen Einträge ergänzt. Die letzten vollständigen Läufe sind grün.

Bei der GUI-Neukompilierung wurden neun bestehende Nullable-Warnungen in unveränderten Views/ViewModels gemeldet. Die letzten inkrementellen Builds meldeten 0 Warnungen und 0 Fehler; dies bedeutet nicht, dass die vorhandenen Nullable-Warnungen behoben wurden.

## E. Review

Reviewumfang: vollständiger Diff der handgeschriebenen Änderungen, Tests und Dokumentation; generierte ANTLR-Dateien über Regeneration/Reproduzierbarkeit und Parser-Tests geprüft.

| Schwere | Ergebnis |
|---|---|
| Critical | Keine offenen Findings im geprüften Änderungsumfang |
| High | Keine offenen Findings im geprüften Änderungsumfang |
| Medium | Gefundene Probleme behoben: AES-Composite mit Optional Blocks, Label-Delimiter-Verwechslung, typisierte Unwrap-Längenprüfung sowie zu breite Parseralternative |
| Low | Bestehende GUI-Nullable-Warnungen bleiben; keine neue Abhängigkeit und kein daraus abgeleiteter Umbau |

Weitere behobene Reviewpunkte: stabil gehaltene Enum-Werte, erhaltener Raw-Diagnosewortlaut, korrigierte veraltete Mechanismusdokumentation und ausschließlich lokale Header-Serialisierung pro Wrap. Die technischen Grenzen aus Abschnitt B sind ausdrücklich dokumentiert und keine implizit angebotenen Funktionen.

## F. Git und Dateiumfang

Ausgangsstatus: ausschließlich das bereits vorhandene unversionierte `.codex/`. Dieses Verzeichnis wurde nicht bearbeitet. Alle Aufgabenänderungen bleiben ungestaged; keine Änderung an den Referenzvektordateien oder Paketabhängigkeiten.

51 vorhandene Dateien sind inhaltlich geändert; 10 Aufgaben-Dateien sind neu (einschließlich dieses Berichts). Der Diff umfasst Grammatik und generierte ANTLR-Artefakte, den begrenzten Header-/Argument-/Typ-Pfad, die beiden allgemeinen TR-31-Adapter, Info-Infrastruktur, Dokumentation und Tests. Ein großer Teil der geänderten Zeilen stammt aus den regenerierten Lexer-/Parser-Tabellen.

### Neue Dateien

```text
CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31AttributeRegistry.cs
CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderBinding.cs
CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderDefinition.cs
CryptoScript/Operations/HeaderOperations.cs
CryptoScript/InfoDocs/Info.TR31.md
CryptoScript/InfoDocs/Info.Func.CreateHeader.md
CryptoScript/InfoDocs/Info.TR31Attributes.md
CryptoScriptUnitTest/CreateHeaderTests.cs
CryptoWorkBenchGuiTest/CreateHeaderInfoTests.cs
docs/architecture/TR31-header-builder-report.md
```

### Geänderte vorhandene Dateien

```text
CryptoScript/Antlr/CryptoLexer.g4
CryptoScript/Antlr/CryptoScript.g4
CryptoScript/Antlr/CryptoScript.interp
CryptoScript/Antlr/CryptoScriptBaseListener.cs
CryptoScript/Antlr/CryptoScriptBaseVisitor.cs
CryptoScript/Antlr/CryptoScriptLexer.cs
CryptoScript/Antlr/CryptoScriptLexer.interp
CryptoScript/Antlr/CryptoScriptListener.cs
CryptoScript/Antlr/CryptoScriptParser.cs
CryptoScript/Antlr/CryptoScriptVisitor.cs
CryptoScript/Arguments/Argument.cs
CryptoScript/CryptoAlgorithm/WRAPPERS/OptionalBlock.cs
CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31Header.cs
CryptoScript/CryptoAlgorithm/WRAPPERS/WrapAESTR31.cs
CryptoScript/CryptoAlgorithm/WRAPPERS/WrapDES3TR31.cs
CryptoScript/CryptoScript.csproj
CryptoScript/Documentation/FileInfoDocumentationProvider.cs
CryptoScript/Documentation/InfoDocumentCatalog.cs
CryptoScript/Documentation/InfoDocumentId.cs
CryptoScript/Documentation/InfoDocumentUri.cs
CryptoScript/InfoDocs/Info.Functions.md
CryptoScript/InfoDocs/Info.Mech.EP2-TR31-KEY-BLOCK.md
CryptoScript/InfoDocs/Info.Mech.WRAP-AES-TR31.md
CryptoScript/InfoDocs/Info.Mech.WRAP-DES3-TR31.md
CryptoScript/InfoDocs/Info.Parameters.md
CryptoScript/InfoDocs/Info.Types.md
CryptoScript/Model/AntlrToFunctionCallExpression.cs
CryptoScript/Model/AntlrToVariableDeclaration.cs
CryptoScript/Model/Ast/FunctionCallExpressionNode.cs
CryptoScript/Model/Ast/Tr31HeaderInitializerNode.cs
CryptoScript/Model/CryptoType.cs
CryptoScript/Model/FunctionCall.cs
CryptoScript/Model/FunctionCallEvaluator.cs
CryptoScript/Model/MechanismParameterContractValidator.cs
CryptoScript/Model/OperationInvocation.cs
CryptoScript/Model/VariableDeclarationEvaluator.cs
CryptoScript/Operations/OperationFactory.cs
CryptoScript/Operations/OperationList.cs
CryptoScript/Operations/OutputOperations.cs
CryptoScript/Variables/BlockHeaderVariableDeclaration.cs
CryptoScript/Variables/FormatConversions.cs
CryptoScript/Variables/ParameterVariableDeclaration.cs
CryptoScript/Variables/TR31String.cs
CryptoScriptUnitTest/CryptoTypeMetadataTests.cs
CryptoScriptUnitTest/InfoDocumentCatalogTests.cs
CryptoScriptUnitTest/Tr31HeaderInitializerAstTests.cs
CryptoScriptUnitTest/VariableDeclarationAstTests.cs
CryptoScriptUnitTest/VariableDeclarationEvaluatorTests.cs
CryptoWorkBenchAvalonia/Models/FunctionOverlayDictionaryModel.cs
CryptoWorkBenchAvalonia/Models/InfoCompletionProvider.cs
CryptoWorkBenchGuiTest/InfoCompletionProviderTests.cs
```

Keine Commits, kein Push, kein Merge. Der Arbeitsbaum bleibt zur separaten Commit-Freigabe erhalten.
