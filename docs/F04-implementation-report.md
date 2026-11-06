# F04 — MacPack und MacVerify

Stand: 24. Oktober 2026. Repository `tkbss/CryptoWorkBench`, Branch `master`,
Baseline und unveränderter HEAD `38e00c32828e9550d42d413c0e6f00c621ee9f5e`.
Kein Commit und kein Push. Keine Änderungen an `.codex/config.toml`, anderen
Worktrees oder dem benachbarten SecurityStandards-Repository. Der bereits
unversionierte `.codex/`-Bereich gehört nicht zur Implementierung.

## Architektur und Entscheidungen

`MacPack(PARAM,KEY,PATH)` liefert PATH, `MacVerify(PARAM,KEY,PATH)` liefert VAL
mit dem Stringwert `true` oder `false`. VAL existierte in der Baseline noch nicht;
Lexer, Parser und CryptoType wurden entsprechend ergänzt. Seit der Reviewkorrektur
trägt ValVariableDeclaration den strikt booleschen Ergebnistyp. `Mac(PARAM,KEY,PATH)` bleibt in Signatur und Verhalten
unverändert. FN erkennt die neuen Funktionsnamen bereits über die allgemeine
Namensregel; ein zusätzliches Funktionstoken ist deshalb nicht nötig.

FileMacContract prüft Deklarationen und alle verschachtelten Aufrufe vor deren
Auswertung. Genau drei originale PARAM-/KEY-/PATH-Referenzen sind nötig. Der
OperationInvocation-Einstieg prüft diese Identitäten ebenfalls, bevor eine Datei
geöffnet wird. OperationFactory und OperationList registrieren die Funktionen;
der Legacy-String-Einstieg weist sie zurück. Fehler laufen über den bestehenden
semantischen Vertrag: FunctionContractViolation für Aufrufformen, ein redigierter
FunctionExecutionFailed für Laufzeitfehler. Cancellation wird weitergereicht.

CwmCoordinator verbindet Containerverarbeitung und bestehende F03-MACs. Die
Vorvalidierung verwendet FileMacCoordinator, MechanismRegistry und
MechanismParameterContractValidator. Eine zusätzliche interne MacOpened-Methode
arbeitet auf dem bereits sicher geöffneten Handle, prüft dessen Anfangs-/Endlänge
und Endposition und liefert MAC und tatsächliche Grösse zusammen. Die alte
Mac-Methode bleibt unverändert. Bei Verifikation gilt KeyUsage.MacVerify;
technische Wiederholungsprüfungen erzeugen keine doppelten Metadatenwarnungen.

Alle 17 freigegebenen F03-Mechanismen sind abgedeckt: AES-CMAC, DES3-CMAC,
AES-GMAC, AES-CBC-MAC, DES3-CBC, DES3-RETAIL und elf HMAC-Varianten.
Wirksame Parameter sind explizit normalisiert: GMAC-Nonce, CBC-/Retail-Padding
und MAC-Länge, beim CBC-MAC auch der wirksame Null-IV. Der zufällige
DES3-CBC-Verschlüsselungs-IV aus PARAM ist gemäss F03 kein MAC-Parameter.

Die Quelle wird über FilePublication geöffnet. Erst nach vollständiger
MAC-Berechnung und Metadatenbildung entsteht eine private temporäre Datei;
Flush und atomare Publikation ersetzen nie bestehende Einträge. Das F01-Schema
`input.bin.cwm`, `input.bin.1.cwm`, ... bleibt erhalten. Der temporäre Name wird
auch bei Fehler, Cancellation oder erschöpftem Namensraum zur Bereinigung
vorgemerkt. Die geerbte Bereinigung ist best effort: zusätzliche Löschfehler oder
ein Prozessabbruch können einen temporären Eintrag hinterlassen.

MacVerify begrenzt das Lesen unabhängig von nachträglichem Dateiwachstum auf
65.536 Bytes und JSON-Tiefe 8. Es prüft UTF-8, Schema, Konstanten, HEX, Grösse,
Duplikate und relative Namen. Erst nach gültigem Metadaten-HMAC und PARAM-Abgleich
wird die Referenz über dasselbe gebundene Verzeichnis geöffnet. Eine abweichende
Grösse ergibt false, auch wenn die geänderte Datei keine gültige NONE-Blocklänge
mehr hat. Bei gleicher Grösse wird der Datei-MAC erneut gestreamt. Beide
MAC-Vergleiche verwenden CryptographicOperations.FixedTimeEquals.

## CWM- und Kryptografievertrag

Das normative Schema und sämtliche Feldtypen stehen in
`CryptoScript/InfoDocs/Info.Func.MacPack.md`. Version 1 erlaubt ausschliesslich
einen relativen Dateinamen im Containerverzeichnis und grosse HEX-Zeichen ohne
Präfix. Die Dateigrösse ist eine sichere JSON-Ganzzahl bis 2^53−1. Unbekannte
Top-Level-/fileMac-/metadataAuth-Felder und doppelte Schlüssel werden abgewiesen;
authentifizierte Parameter müssen genau den wirksamen PARAM-Feldern entsprechen.

metadataAuth nennt ausdrücklich HMAC-SHA256, RFC8785-JCS und HKDF-SHA256.
MAC-Eingabe ist ausschliesslich das kanonisierte fileMac-Objekt in UTF-8; Ausgabe
sind volle 32 Bytes. HKDF verwendet die dekodierten KEY-Bytes als IKM, den
UTF-8-Salt `CryptoWorkBench/CWM/v1/HKDF-SHA256`, die UTF-8-Info
`CryptoWorkBench/CWM/v1/metadataAuth/HMAC-SHA256` und L=32. Kein NUL-Abschluss
und kein zweiter Benutzerschlüssel. Schlüsselbytes und Ableitungspuffer werden
im finally-Block gelöscht.

CwmJcs implementiert RFC 8785 für die vollständig festgelegte CWM-Domäne aus
Objekten, Unicode-Strings und sicheren Ganzzahlen. Es ist kein gewöhnlicher
JsonSerializer-Aufruf: Schlüssel werden ordinal nach UTF-16 sortiert, Strings
mit den RFC-Escapes ohne Unicode-Normalisierung geschrieben und strikt als UTF-8
kodiert. Sichere Ganzzahlen erhalten ihre exakte dezimale ECMAScript-Darstellung.
Andere JSON-Wertarten, Nachkomma-/Exponenttokens und Zahlen ausserhalb der
sicheren Domäne werden abgewiesen. Dadurch ist keine potenziell abweichende
Gleitkommaformatierung im authentifizierten Format enthalten.

Der Konformitätsnachweis ist auf diese CWM-Domäne beschränkt: RFC-8785-Vektoren
für UTF-16-Sortierung und sichere Ganzzahlen, alle Steuerzeichen und unabhängige
Node/ECMAScript-Referenzvektoren. Die Implementierung beansprucht ausdrücklich
keine allgemeine RFC-8785-API für beliebige Floating-Point-JSON-Dokumente.
`docs/F04-reference/generate.mjs` erstellt zusätzlich einen vollständigen
unabhängigen Container mit Node HKDF/HMAC; ein zweiter C#-Test berechnet
Extract/Expand und HMAC ohne produktive HKDF-/JCS-Helfer.

## Dateiliste

Neue Dateien:

- `CryptoScript/FileEncryption/CwmCoordinator.cs`
- `CryptoScript/FileEncryption/CwmJcs.cs`
- `CryptoScript/InfoDocs/Info.Func.MacPack.md`
- `CryptoScript/InfoDocs/Info.Func.MacVerify.md`
- `CryptoScriptUnitTest/CwmTests.cs`
- `CryptoWorkBenchGuiTest/CwmInfoTests.cs`
- `docs/F04-reference/{README.md,generate.mjs,reference.json,jcs-vectors.json}`
- `docs/F04-implementation-report.md`

Geänderte Integrationsdateien:

- `CryptoScript/Antlr/{CryptoLexer.g4,CryptoScript.g4}` und regenerierte
  CryptoScriptLexer/Parser-CS-, Token- und Interp-Dateien (ANTLR 4.13.2).
- `CryptoScript/FileEncryption/{FileMacCoordinator.cs,FileMacContract.cs}`
- `CryptoScript/Model/{CryptoType.cs,FunctionCallEvaluator.cs}`
- `CryptoScript/Operations/{OperationFactory.cs,OperationList.cs}`
- `CryptoScript/Documentation/{InfoDocumentCatalog.cs,FileInfoDocumentationProvider.cs}`
- `CryptoScript/CryptoScript.csproj` und `CryptoScriptUnitTest/CryptoScriptUnitTest.csproj`
- `CryptoWorkBenchAvalonia/Models/{FunctionOverlayDictionaryModel.cs,InfoCompletionProvider.cs}`
- `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs` und
  `CryptoWorkBenchGuiTest/InfoCompletionProviderTests.cs`: Inventarerwartungen ergänzt.
- `CryptoScript/InfoDocs/{Info.Functions.md,Info.Types.md,Info.Func.Encrypt.md,
  Info.Mech.AES-CBC-MAC.md,Info.Mech.AES-GMAC.md,Info.Mech.DES3-CBC.md,
  Info.Mech.DES3-RETAIL.md}`: veraltete F04-Aussagen aktualisiert.
- `README.md`, `docs/F03.2-implementation-report.md`, `docs/F03.2-reference/README.md`: aktueller F04-Verweis;
  historische F03.2-Ergebnisse bleiben als solche erhalten.

## Tests und Validierung

Die neue CwmTests-Suite deckt alle Mechanismen über leere Dateien,
8-/16-Byte-Blockgrenzen, 64-KiB-Grenzen und 1.000.003 Bytes ab. Zusätzlich:
64-MiB-Speichergrenze, Padding-/Truncation-Varianten inklusive M3 und NONE,
Nonce-/PARAM-Abgleich, falscher KEY, veränderte Quelle und beide MAC-Objekte,
unabhängige Referenzen, alle Metadatenlabels, falsche Version, kaputtes JSON,
Duplikate in jedem Objekt, ungültiges UTF-8/Unicode/HEX, übergrosse Container,
Path Traversal, Symlinks, fehlende Dateien, bestehende Dateien/Verzeichnisse,
zwölf parallele Publikationen, injizierte I/O-Fehler vor Publikation,
Namenserschöpfung und Cancellation einschliesslich ExecuteAsync.
Die vorhandenen F01/F02/F03-Tests prüfen zusätzlich die wiederverwendeten
nativen Dateioperationen und Fehler-/Cancellation-Szenarien während des Lesens.

CwmInfoTests prüft Overlay, Funktions- und Info-Vervollständigung, ausgelieferte
Hilfedateien sowie Rücknavigation für beide Funktionen. Die vollständigen
Core- und GUI-Suiten werden ausgeführt; TRX-Ergebnisse liegen lokal unter
`artifacts/F04/` (ignorierte Buildartefakte).

Ausgeführte Befehle:

```text
node docs/F04-reference/generate.mjs
dotnet build CryptoWorkBench.sln --no-restore -m:1 -p:UseSharedCompilation=false -v quiet
dotnet build CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v quiet
dotnet build CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v quiet
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore -v minimal --logger "trx;LogFileName=core.trx" --results-directory artifacts/F04
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore -v minimal --logger "trx;LogFileName=gui.trx" --results-directory artifacts/F04
git diff --check
```

Ein erster sandboxierter VSTest-Lauf konnte den lokalen testhost nicht verbinden;
die anschliessenden Läufe mit freigegebener lokaler Prozesskommunikation liefen.
Der erste vollständige Lauf fand feste Katalog-/Completion-Inventarerwartungen;
diese wurden um die beiden neuen Funktionen ergänzt. Das VAL-Token wurde vor ID
platziert, nachdem der erste Sprachtest den Lexer-Vorrangfehler aufgedeckt hatte.

Historische Ergebnisse vor Reviewkorrekturen unter Windows x64 (.NET SDK 10.0.401),
Skip-Zahl anhand der einzelnen TRX-Resultate korrigiert:

| Prüfung | Ergebnis |
|---|---|
| Solution-Build (Core, Console, Core-Tests) | Erfolgreich |
| GUI-Testprojekt inkl. Avalonia-GUI-Build | Erfolgreich |
| Avalonia-Desktop-Host-Build | Erfolgreich |
| Vollständige Core-Suite | 10.651 bestanden, 25 NotExecuted, 0 fehlgeschlagen; 36 s |
| Vollständige GUI-/Console-Integrationssuite | 244 bestanden, 0 übersprungen, 0 fehlgeschlagen |
| git diff --check | Erfolgreich, keine Whitespace-Fehler |

Die 25 Core-Skips umfassen fehlende Windows-Symlinkrechte, plattformspezifische
Unix/macOS-Tests und den ausdrücklich optionalen Hardware-Benchmark. Neue Dateien
wurden zusätzlich auf abschliessenden Whitespace geprüft, da git diff unversionierte
Dateien nicht einschliesst. Die Unicode-Dateinamenprüfung weist lone surrogates
vor gewöhnlicher JSON-Serialisierung zurück, damit diese nicht stillschweigend
durch Ersatzzeichen verändert werden; ein Roundtrip erhält kombinierende
Zeichen und ergänzende Unicode-Codepunkte unverändert.

## Bekannte Grenzen

- Lokal wird nur Windows x64 ausgeführt. Linux x64, macOS x64 und macOS ARM64
  sind in der bestehenden `.github/workflows/dotnet.yml`-Matrix mit vollständigen
  Core-/GUI-Suiten enthalten. Diese Remote-Läufe wurden ohne Commit/Push nicht
  ausgelöst; dafür wird kein Testerfolg behauptet.
- Windows erlaubt hier keine Symlink-Erstellung. Der neue Symlinktest wird dann
  explizit übersprungen; Plattform-/Berechtigungs-Skips werden separat gezählt.
- Die F01-Dateisystemgrenzen gelten unverändert: Unix verhindert keine fremden
  gleichlangen Schreibzugriffe; Linux benötigt procfs/Hardlinks; macOS benötigt
  ein vertrauenswürdiges Publikationsverzeichnis. Keine unsicheren Fallbacks.
- Bouncy Castle bietet keine vollständige Schlüsselplan-Löschung. Exponierte
  eigene Schlüssel-/Streamingpuffer werden soweit möglich gelöscht.
- CWM v1 begrenzt Referenzen auf Geschwisterdateien und Grössen auf 2^53−1;
  Unterverzeichnisse und allgemeine Floating-Point-JCS-Daten sind ausgeschlossen.
- VAL ist für den Verifikationswert ergänzt. Die bestehenden S01/S05-Snapshot-
  Formate werden nicht erweitert: Sie weisen VAL weiterhin als nicht unterstützte
  Deklaration zurück, statt den Typ stillschweigend in VAR umzuwandeln.
- Build-Warnung NU1900: NuGet-Vulnerability-Feed war nicht erreichbar. Die
  vorhandenen GUI-Nullable-Warnungen bleiben unverändert; keine neue
  CWM-Compilerwarnung wurde beobachtet.

Für den unabhängigen Review sind insbesondere das eingeschränkte, explizite
JCS-Datenmodell, die HKDF-Domainstrings und die unveränderten nativen
FilePublication-Vertrauensgrenzen relevant.

## Reviewkorrekturen vom 24. Oktober 2026

Grundlage ist `docs/F04-review-report.md`; der historische Reviewbericht bleibt
unverändert. Abschlussstatus dieser Korrekturrunde: **READY FOR RE-REVIEW**.
HEAD bleibt `38e00c32828e9550d42d413c0e6f00c621ee9f5e` auf master. Kein Commit,
kein Push, keine Änderung oder Einsicht in `.codex/config.toml`.

### F04-RV-01 – Schema vor Authentifizierungsentscheidung

`CwmCoordinator.ValidateFileSchema` prüft den gespeicherten kanonischen Mechanismus
gegen die bestehende CWM-Whitelist, die exakten Parameterfelder, Paddingnamen,
MACLEN-Typ und -Bereich, Null-IV, Nonce-HEX und die vollständige mechanismenspezifische
Taglänge. Dies erfolgt vor dem möglichen `false` durch einen falschen Metadaten-HMAC.
Die Prüfung braucht weder KEY noch Quelldatei. Der Abgleich mit dem vom Aufrufer
gelieferten PARAM erfolgt weiterhin erst nach gültiger Metadatenauthentifizierung.
Wohlgeformte Manipulationen bleiben `false`; ungültige Schemata sind Fehler.

Regressionstests prüfen zwölf Fehlerbilder jeweils mit gültigem und ungültigem
Metadaten-HMAC, einschliesslich der drei konkreten Reviewbeispiele. Die Quelldatei
ist dabei entfernt: eine versehentliche Öffnung würde IOException statt der
erwarteten FormatException ergeben. Verzeichnisinventar und Containerbytes bleiben
unverändert. Eine explizite Tabelle prüft alle 17 Taglängen sowie verkürzte Tags.
Ein weiterer Test trennt wohlgeformten Mechanismuswechsel von PARAM-Mismatch nach
gültiger Authentifizierung. Die bisher falsche Erwartung für unbekannten Mechanismus
und unbekannte Parameter wurde verschärft; kein Test wurde entfernt.

### F04-RV-02 – VAL-Wertdomäne auf allen unterstützten Wegen

`ValVariableDeclaration` speichert einen bool statt beliebiger Stringdaten und
liefert genau `true`/`false`. Der virtuelle Value-Setter akzeptiert ausschliesslich
diese beiden Zeichenfolgen, auch bei Zugriff über VariableDeclaration. Typänderungen
werden abgewiesen; gewöhnliche VariableDeclaration-/StringVariableDeclaration-
Objekte können nicht mehr mit CryptoTypeVal etikettiert werden.

VAL-Deklarationen verlangen einen passenden Funktionsrückgabewert. Es wird keine
zusätzliche Boolean-Literalsyntax eingeführt. String-, HEX-, Base64-, Zahl-, Pfad-
und leere Initialisierer werden vor Registrierung mit InvalidDeclaration verworfen.
Die Sprache hat keine separate allgemeine Zuweisungsregel: erneute Deklarationen
laufen durch denselben Evaluator. Fehler ersetzen keine bestehende Variable.
Synchroner und asynchroner Runner teilen diese Validierung. Print erhält weiterhin
die Textdarstellung aus Value. GUI-Änderungen akzeptieren nur die zwei booleschen
Werte und stellen bei ungültiger Eingabe den bisherigen Wert wieder her, ohne eine
Exception auf dem UI-Thread. Snapshot-Serialisierung bleibt ausdrücklich ausgeschlossen.

Tests decken Literalpfade, Modellmutation, Typumetikettierung, Snapshot-Ablehnung,
beide Resultatwerte im asynchronen Runner und vier GUI-Editierfälle ab.
Neue produktive Datei: `CryptoScript/Variables/ValVariableDeclaration.cs`.
Zusätzlich angepasst: VariableDeclaration, VariableDeclarationEvaluator und die
VAL-bezogene Editiergrenze in VariableViewModel; übrige Typen behalten ihr Verhalten.

### F04-RV-03 – Schreibweisenunabhängige Fehlerredaktion

Die CWM-Erkennung im Fehlerpfad verwendet wie die Aufrufprüfung einen ordinalen,
case-insensitiven Vergleich. Alle akzeptierten Schreibweisen bekommen dieselbe
redigierte FunctionCall-/Message-Form. FunctionName, FunctionExecutionFailed,
Severity und die vom Runner zugeordneten Location-/Operation-Daten bleiben erhalten.
Native Exceptiontexte oder Pfade werden nicht übernommen. Sechs Schreibvarianten
beider Funktionen werden gegen einen fehlenden Quellpfad getestet; kein Resultat
oder Dateieintrag entsteht.

### F04-RV-04 – TRX statt verkürzter Konsolensumme

Die historische Skip-Zahl wurde von 24 auf 25 berichtigt. Auch beim neuen Lauf
zeigt die CLI 24 Skips, während die TRX-Datei 25 NotExecuted-Einträge enthält,
einschliesslich des expliziten optionalen Hardwaretests. Massgeblich sind die
einzelnen TRX-Resultate. `docs/F04-reference/check-test-results.ps1` zählt diese
und prüft die folgenden Berichtszeilen exakt; Fehler oder unbekannte Outcomes
führen ebenfalls zum Fehlschlag. Ein negativer Kontrolllauf mit absichtlich
veränderter Berichtskopie muss die Abweichung erkennen.

### Neue Validierungsergebnisse

Windows x64; vollständige Suiten einschliesslich der bestehenden F01–F03-Tests:

| Prüfung | Ergebnis |
|---|---|
| Solution-Build, GUI-Testprojekt, Desktop-Host | Erfolgreich |
| Gezielte CwmTests | 320 bestanden, 1 NotExecuted, 0 fehlgeschlagen |
| Vollständige Core-Suite | 10.698 bestanden, 25 NotExecuted, 0 fehlgeschlagen |
| Vollständige GUI-Suite | 248 bestanden, 0 NotExecuted, 0 fehlgeschlagen |
| Unabhängige Node-F04-Referenzen | Beide JSON-Dateien bytegleich regeneriert; Referenztests bestanden |
| Berichts-/TRX-Abgleich | Positive Prüfung erfolgreich; falsche Skip-Zahl wird zurückgewiesen |
| git diff --check | Erfolgreich |

Maschinenprüfbare Resultatzeilen:

```text
F04-CORRECTIONS-core: passed=10698 skipped=25 failed=0 total=10723
F04-CORRECTIONS-gui: passed=248 skipped=0 failed=0 total=248
```

TRX-Dateien: `artifacts/F04-corrections/{targeted,core,gui}.trx`.
Verwendete Befehle (alle Builds mit `--no-restore -m:1 -p:UseSharedCompilation=false -v quiet`):

```text
dotnet build CryptoWorkBench.sln
dotnet build CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj
dotnet build CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore --filter FullyQualifiedName~CwmTests -v minimal --logger "trx;LogFileName=targeted.trx" --results-directory artifacts/F04-corrections
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore -v minimal --logger "trx;LogFileName=core.trx" --results-directory artifacts/F04-corrections
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore -v minimal --logger "trx;LogFileName=gui.trx" --results-directory artifacts/F04-corrections
node docs/F04-reference/generate.mjs
pwsh -File docs/F04-reference/check-test-results.ps1
git diff --check
```

Die bestehenden NU1900-/GUI-Nullable-Warnungen bleiben. Windows-Symlinkrechte,
Linux/macOS-Läufe und Dependency-Feed-Prüfung bleiben offen wie im Review;
es wird kein plattformübergreifender Testerfolg behauptet.

## Gesonderte Vertragsentscheidung: direkter KEY statt HKDF

**Nicht umgesetzt.** CWM v1, HKDF-Salt/Info/Länge und vorhandene Referenzvektoren
bleiben kryptografisch unverändert. Ein Benutzer-KEY und identische rohe Schlüsselbytes
für Datei-MAC und Metadaten-HMAC sind unterschiedliche Verträge. Der überlieferte
Implementierungsauftrag verlangt in Abschnitt D explizit HKDF; der Reviewauftrag
verweist auf eine ältere Direct-Key-Anforderung. Diese Priorität muss separat
entschieden werden. Die vier Findings erfordern keine Änderung der Ableitung.

Bei genehmigter Umstellung wären folgende Änderungen nötig:

1. **Format:** Eine neue eindeutig unterscheidbare Version festlegen (Vorschlag:
   CWM v2 mit `metadataAuth.keyDerivation = "NONE"`). Der genaue Name ist eine
   noch offene Vertragsentscheidung. Das bisherige HKDF-SHA256-Label darf nicht
   für einen Direct-Key-Tag weiterverwendet werden. Format/Version/Ableitungslabel
   müssen als feste zusammengehörige Konstanten validiert werden.
2. **Berechnung:** Kmeta entfällt; der Metadaten-Tag wird zu
   `HMAC-SHA256(raw KEY bytes, UTF8(JCS(fileMac)))`, weiterhin volle 32 Bytes.
   Der KEY darf weder HEX-Text noch für HMAC zuvor eigenständig gehasht/gekürzt
   werden; die normale HMAC-Schlüsselaufbereitung bleibt Aufgabe der HMAC-API.
   Datei-MAC, Padding, Nonce, Dateigrösse und JCS bleiben unverändert. Die bisherige
   Domain Separation durch abgeleitete Schlüssel geht verloren und muss bei der
   Vertragsfreigabe ausdrücklich berücksichtigt werden.
3. **Referenzen:** V1-Vektoren archiviert beibehalten; neue V2-Vektoren separat
   unabhängig mit Node erzeugen. HKDF-Ausgabefeld entfällt in V2, Metadaten-Tag
   und Versions-/Ableitungslabel ändern sich. Der Datei-Tag und die kanonischen
   fileMac-Bytes bleiben für dasselbe Beispiel gleich. Für die vorhandenen
   Referenzdaten ist der Direct-Key-Metadaten-Tag
   `84E10664E0636A0D35EFE7ACF4258E683EABAB9B31F66D975D2D0CD1558A308F`
   statt des V1-Tags
   `92224F80F8F5232A62BA72765F6B09B7196C69A98BC3878376110D5531F54243`.
4. **Interoperabilität/Migration:** Alte Verifier müssen V2 als unbekannt ablehnen;
   Direct-Key-Verifier können V1 nicht ohne HKDF-Unterstützung prüfen. Falls beide
   Versionen unterstützt werden sollen, ausschliesslich anhand der validierten
   Version auswählen, niemals nach einem fehlgeschlagenen HMAC die andere Methode
   probieren. Bestehende Container lassen sich nicht durch blosses Umetikettieren
   migrieren: sie müssen mit KEY geprüft und unter dem neuen Vertrag neu
   authentifiziert werden. Hilfedokumente und externe Verifikationsanleitungen
   brauchen separate V1-/V2-Regeln und Kreuzversions-Negativtests.

Keine dieser vorgeschlagenen Format-/Migrationsänderungen ist Bestandteil des
aktuellen Codes. Eine Freigabe dieser Entscheidung steht weiterhin aus.

## Verbindliche Vertragsfreigabe und Commit-Vorbereitung

Mit dem Auftrag «F04 – Commit, Push und CI-Validierung» vom 24. Oktober 2026
ist die vorstehend historisch offene Vertragsfrage entschieden: **CWM v1 verwendet
unverändert HKDF-SHA256 zur Ableitung des Metadaten-HMAC-Schlüssels.** Der unabhängig
geprüfte Vertrag ist freigegeben. Es gibt keine Direct-Key-Umstellung und keine
Formatänderung; die zuvor beschriebenen V2-Überlegungen bleiben ausschliesslich
historische, nicht umgesetzte Alternativen.

Der freigegebene Commit umfasst die vollständige F04-Implementierung, Korrekturen,
Tests, Referenzen und beide Berichte. Die CI-Ergebnisse des anschliessenden Pushs
werden im Abschlussbericht des Chats mit Workflow-Link und Commit-SHA festgehalten;
sie sind zum Zeitpunkt dieser Dokumentation noch nicht ausgeführt. Es ist genau
ein Commit mit der Nachricht `Add authenticated CWM file MAC containers` beauftragt.
