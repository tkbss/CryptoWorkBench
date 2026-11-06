# F04 – Security- und Architekturreview

Datum: 24. Oktober 2026. **Status: READY FOR CORRECTION.**

Ergebnis: **0 Critical, 0 High, 2 Medium, 2 Low**. Kein nachgewiesener
Authentifizierungs-Bypass. Die Findings betreffen den Schema-/Fehlervertrag,
die Integrität des VAL-Typs, Fehlerredaktion und die Testdokumentation.
Die offene Anforderungsabgrenzung zu HKDF ist separat unter R1 dokumentiert.

## 1. Baseline, Umfang und Vorgehen

Repository `CryptoWorkBench`, Branch `master`, HEAD und Reviewbaseline
`38e00c32828e9550d42d413c0e6f00c621ee9f5e` unverändert.
Geprüft wurden die 32 geänderten versionierten Dateien und elf neuen
F04-Dateien, einschliesslich generierter Sprachdateien, Fixtures und Dokumentation.
Alle Zeilenangaben beziehen sich auf den uncommitteten Stand dieses Reviews.

Geänderte Dateien:

```text
CryptoScript/Antlr/CryptoLexer.g4
CryptoScript/Antlr/CryptoScript.g4
CryptoScript/Antlr/CryptoScript.interp
CryptoScript/Antlr/CryptoScript.tokens
CryptoScript/Antlr/CryptoScriptLexer.cs
CryptoScript/Antlr/CryptoScriptLexer.interp
CryptoScript/Antlr/CryptoScriptLexer.tokens
CryptoScript/Antlr/CryptoScriptParser.cs
CryptoScript/CryptoScript.csproj
CryptoScript/Documentation/FileInfoDocumentationProvider.cs
CryptoScript/Documentation/InfoDocumentCatalog.cs
CryptoScript/FileEncryption/FileMacContract.cs
CryptoScript/FileEncryption/FileMacCoordinator.cs
CryptoScript/InfoDocs/Info.Func.Encrypt.md
CryptoScript/InfoDocs/Info.Functions.md
CryptoScript/InfoDocs/Info.Mech.AES-CBC-MAC.md
CryptoScript/InfoDocs/Info.Mech.AES-GMAC.md
CryptoScript/InfoDocs/Info.Mech.DES3-CBC.md
CryptoScript/InfoDocs/Info.Mech.DES3-RETAIL.md
CryptoScript/InfoDocs/Info.Types.md
CryptoScript/Model/CryptoType.cs
CryptoScript/Model/FunctionCallEvaluator.cs
CryptoScript/Operations/OperationFactory.cs
CryptoScript/Operations/OperationList.cs
CryptoScriptUnitTest/CryptoScriptUnitTest.csproj
CryptoScriptUnitTest/InfoDocumentCatalogTests.cs
CryptoWorkBenchAvalonia/Models/FunctionOverlayDictionaryModel.cs
CryptoWorkBenchAvalonia/Models/InfoCompletionProvider.cs
CryptoWorkBenchGuiTest/InfoCompletionProviderTests.cs
README.md
docs/F03.2-implementation-report.md
docs/F03.2-reference/README.md
```

Neue Dateien:

```text
CryptoScript/FileEncryption/CwmCoordinator.cs
CryptoScript/FileEncryption/CwmJcs.cs
CryptoScript/InfoDocs/Info.Func.MacPack.md
CryptoScript/InfoDocs/Info.Func.MacVerify.md
CryptoScriptUnitTest/CwmTests.cs
CryptoWorkBenchGuiTest/CwmInfoTests.cs
docs/F04-implementation-report.md
docs/F04-reference/README.md
docs/F04-reference/generate.mjs
docs/F04-reference/reference.json
docs/F04-reference/jcs-vectors.json
```

Zusätzlich wurden die wiederverwendeten produktiven Pfade verfolgt:
FilePublication, Pfadvalidierung/private Dateierstellung, StreamingBlockMac,
DigestFactory, Mechanismen-/Parameter-/Schlüsselvalidierung, Evaluatoren und
Snapshot-Serializer. Vorhandene F01–F03-Tests wurden als ergänzende Evidenz bewertet.

Das Review arbeitete lesend. Einzige erstellte Datei ist dieser ausdrücklich
angeforderte Bericht. Keine Codekorrektur, kein Build, kein vollständiger Testlauf,
keine Fixture-Regenerierung, kein Commit/Push/Branchwechsel. Andere Worktrees
wurden nicht bearbeitet. Der vorbestehende unversionierte `.codex/`-Bereich gehört
nicht zu F04 und wurde ausgenommen; `.codex/config.toml` wurde nicht gelesen.

Zusätzliche Prüfungen liefen ohne Dateierstellung: Node-Nachrechnung der
vorhandenen Referenzen sowie In-Memory-Aufrufe bestehender Debug-Assemblies
über PowerShell/.NET 10. Diese Laufzeitbeobachtungen ergänzen die Quelltextprüfung;
sie ersetzen keinen reproduzierbaren Neubuild des geprüften Quellstands.
Bestehende TRX-Dateien wurden nur gelesen. `git diff --check` war erfolgreich.

## 2. Findings

### Critical

Keine festgestellt.

### High

Keine festgestellt. Dies ist keine Aussage über Sicherheit ausserhalb der
beschriebenen Dateisystem- und Schlüsselvertrauensgrenzen.

### Medium – F04-RV-01: Ungültiges fileMac-Schema wird als Authentifizierungsfehler behandelt

**Stellen:** `CryptoScript/FileEncryption/CwmCoordinator.cs:112–126`, insbesondere
der frühe Rücksprung in Zeile 119; Gegenvertrag
`CryptoScript/InfoDocs/Info.Func.MacVerify.md:13–18,27–28,36–38`.
Betroffener Test: `CryptoScriptUnitTest/CwmTests.cs:104–121`.

Die Metadaten-HMAC-Prüfung erfolgt vor der Validierung des gespeicherten
Dateimechanismus, des mechanismspezifischen Parameterschemas und der vollständigen
Datei-MAC-Länge. `Fields(fm, ...)` prüft nur die äusseren Feldnamen.
`CwmJcs` akzeptiert beispielsweise auch einen String als `parameters`.
Bei falschem Metadaten-HMAC ergibt dies `false`, obwohl die Dokumentation für
ungültiges Schema, nicht unterstützte Algorithmen und ungültige Taglängen einen
semantischen Fehler verlangt – ausdrücklich auch bei gleichzeitig falschem HMAC.

**Reproduktion:** Ausgehend vom gültigen HMAC-SHA256-Container aus
`docs/F04-reference/reference.json` jeweils genau eine Änderung vornehmen und den
bisherigen Metadaten-Tag beibehalten:

```text
fileMac.mechanism = "NOT-A-MECHANISM"
fileMac.parameters = "oops"
fileMac.mac = "00"
```

Mit gültigem HMAC-SHA256-PARAM und Referenz-KEY liefert der Kontrollfluss jeweils
VAL `false` vor dem Öffnen der Originaldatei, statt eines Schemafehlers. Diese
Dateimutationen wurden im lesenden Review nicht ausgeführt; sie ergeben sich
direkt aus dem vollständig nachvollzogenen Kontrollfluss. Der vorhandene Test
`MetadataTamperFailsBeforeOpeningReference("mechanism")` verwendet bereits
`"missing.bin"` als unbekannten Mechanismus und erwartet ausdrücklich `false`.

**Auswirkung:** Externe Verifier und Aufrufer können Formatfehler und fehlgeschlagene
Authentifizierung nicht entsprechend dem veröffentlichten Vertrag unterscheiden.
Keine Akzeptanz eines ungültigen MACs als `true`; die Verifikation scheitert sicher.

**Empfehlung:** Vor der HMAC-Entscheidung den gespeicherten Mechanismus gegen die
CWM-Whitelist und dessen Parameterschema/Taglänge prüfen. Den späteren Vergleich
mit dem vom Aufrufer gelieferten PARAM separat nach erfolgreicher Authentifizierung
beibehalten. Keine Referenzdatei während der Schemaprüfung öffnen. Positive,
wohlgeformte Manipulationen und strukturell ungültige Manipulationen in den Tests
trennen; beide jeweils mit korrektem und falschem Metadaten-HMAC abdecken.

### Medium – F04-RV-02: VAL hat keine durchgesetzte boolesche Wertdomäne

**Stellen:** `CryptoScript/Model/CryptoType.cs:31–32,45–48`,
`CryptoScript/Antlr/CryptoScript.g4:31–33`; fehlende Absicherung im bestehenden
`CryptoScript/Model/VariableDeclarationEvaluator.cs:56–113` und in
`CryptoScript/Expressions/Expression.cs:30–94`.
Gegenvertrag: `CryptoScript/InfoDocs/Info.Types.md:8`.

VAL wird als allgemeiner Deklarationstyp eingeführt, ohne seine Initialisierer
einzuschränken. Der vorhandene Literalpfad übernimmt beliebige String-, HEX-,
Base64- und Zahlwerte mit dem neu angegebenen CryptoTypeVal. Der korrekte
boolesche Rückgabewert von MacVerify garantiert daher nicht die Wertdomäne aller
VAL-Variablen.

**Reproduktion, tatsächlich in-memory ausgeführt:**

```cryptoscript
VAL v = "junk"
```

Auch `VAL v = 0x(AA)` und `VAL v = 123` werden ohne Fehler registriert. Beobachtet
wurde jeweils `StringVariableDeclaration`, Type.Name `VAL`, mit dem unveränderten
Literalwert. Dies widerspricht dem dokumentierten Boolean-Ergebnistyp und erlaubt
nachgelagerten Verbrauchern keine zuverlässige Interpretation allein anhand von VAL.

**Empfehlung:** Den VAL-Vertrag explizit durchsetzen: entweder nur passende
Funktionsresultate zulassen oder eine genau definierte Boolean-Literalsyntax
validieren. Ungültige Initialisierer vor Registrierung mit InvalidDeclaration
ablehnen. Tests für String/HEX/Base64/Zahl/Pfad sowie Dictionary-Unverändertheit
ergänzen. Keine implizite Umwandlung in VAR und keine Snapshot-Erweiterung vornehmen.

### Low – F04-RV-03: Redigierte CWM-Diagnostik hängt von der Funktionsschreibweise ab

**Stellen:** `CryptoScript/Model/FunctionCallEvaluator.cs:410–418,435–448`;
case-insensitiver Dispatch in `CryptoScript/Operations/OperationFactory.cs:32–33`
und Vertragsprüfung in `CryptoScript/FileEncryption/FileMacContract.cs:13–14`.

Die besondere CWM-Fehlerredaktion verwendet einen case-sensitiven Stringvergleich.
Der Dispatcher und die Aufrufprüfung akzeptieren dagegen auch `Macpack` und
`Macverify`; diese Schreibweisen passen zudem zur FN-Lexerregel.

**Reproduktion, tatsächlich ausgeführt, ohne Ausgabe-Dateierstellung:**

```cryptoscript
PARAM p = Parameters(AES-CMAC)
KEY k = 0x(00112233445566778899AABBCCDDEEFF)
PATH source = "C:/CWM-review-nonexistent-directory/private-source.bin"
PATH result = Macpack(p,k,source)
```

Beim nicht vorhandenen Verzeichnis lautet FunctionCall `Macpack(p,k,source)` und
Message `CWE filesystem operation failed.`. Mit `MacPack` wird dagegen
`MacPack(<redacted>)` / `MacPack failed.` ausgegeben. Die beobachtete Diagnose
enthielt keine rohen Schlüsselbytes; nachgewiesen ist die Umgehung des zugesagten
Redaktionspfads, keine Schlüsseloffenlegung. Der Diagnosecode war in beiden
beobachteten Runner-Fällen FunctionExecutionFailed.

**Empfehlung:** Eine gemeinsame case-insensitive CWM-Erkennung beziehungsweise
kanonische Namensauflösung für Dispatch, Vertragsprüfung und Diagnostik verwenden.
Regressionstests für zulässige Schreibvarianten und Laufzeitfehler ergänzen.

### Low – F04-RV-04: Implementierungsbericht nennt eine falsche Anzahl übersprungener Tests

**Stelle:** `docs/F04-implementation-report.md:163,167`.

Der Bericht nennt 24 Core-Skips. Das vorhandene `artifacts/F04/core.trx` enthält
10.676 UnitTestResult-Einträge: 10.651 Passed und **25 NotExecuted**, ohne Failed.
GUI: 244 Passed. Der neue CWM-Test `SymlinksRejected` gehört zu den 25 Skips.

**Reproduktion:** `core.trx` als XML laden und
`TestRun.Results.UnitTestResult` nach dem Attribut `outcome` gruppieren. Die
Summary-Counters enthalten `total=10676`, `executed=10651`, `passed=10651`;
für die Skip-Ermittlung sind die einzelnen Resultate entscheidend.

**Empfehlung:** Bericht anhand der konkreten finalen TRX-Datei korrigieren,
25 Skips und deren Kategorien angeben; die fehlende Symlinkvalidierung weiterhin
sichtbar lassen. Bestehende Resultate nicht als im Review neu ausgeführt bezeichnen.

## 3. R1 – Kryptografischer Vertrag und Interoperabilität

`CwmCoordinator.cs:15–16,139–148` verwendet den KEY als rohe IKM-Bytes,
HKDF-SHA256 mit festem UTF-8-Salt und Info sowie L=32; danach HMAC-SHA256 über
das kanonische fileMac-Objekt. Eigene Rohschlüssel-/Ableitungspuffer werden im
finally gelöscht. Extract/Expand entspricht der dokumentierten Konvention aus
[RFC 5869, Abschnitt 2](https://www.rfc-editor.org/rfc/rfc5869.html#section-2).

**Abweichung zum im Reviewauftrag beschriebenen Gleichschlüssel-Vertrag:** Der
Datei-MAC verwendet K, der Metadaten-HMAC verwendet HKDF(K). Ein einziger
Benutzerschlüssel ist nicht dasselbe wie identische Schlüsselbytes für beide MACs.
Direkt mit K authentifizierende Implementierungen sind inkompatibel; ein ansonsten
gültiger Tag ergibt `false`, ein abweichendes keyDerivation-Label einen Formatfehler.
Es gibt keinen Direct-Key-Fallback.

**Quellenkonflikt:** Die tatsächlich beigefügte ursprüngliche Implementierungsanweisung
`Pasted text.txt`, Abschnitt D, Zeilen 89–91, verlangt ausdrücklich HKDF-SHA256
mit dokumentierter Domain Separation und nur einem Benutzer-KEY. Die Umsetzung
entspricht diesem überlieferten Auftrag. Eine nicht autorisierte Abweichung von
diesem Text lässt sich deshalb nicht feststellen. Die im aktuellen Reviewauftrag
genannte ältere Gleichschlüssel-Anforderung muss vor einer verbindlichen externen
Freigabe mit diesem Auftrag abgeglichen werden. Dieser Review entscheidet den
Vertrag nicht eigenständig und empfiehlt keine stillschweigende Änderung.

Die lesende Node-Nachrechnung des Referenzcontainers ergab:

```text
HKDF-Metadaten-Tag:
92224F80F8F5232A62BA72765F6B09B7196C69A98BC3878376110D5531F54243
Direkter Metadaten-HMAC mit demselben rohen Datei-KEY:
84E10664E0636A0D35EFE7ACF4258E683EABAB9B31F66D975D2D0CD1558A308F
```

Salt, Info, UTF-8 ohne NUL, Ausgabelänge und vollständiger Tag sind in MacPack.md
reproduzierbar dokumentiert. Eine spätere Änderung dieser Konstanten oder der
Ableitung benötigt eine ausdrücklich festgelegte Format-/Versionsentscheidung.

## 4. R2 – JCS innerhalb des CWM-Profils

Kein Konformitätsfehler innerhalb der zugelassenen Domäne festgestellt.
Die relevante Referenz ist [RFC 8785, Abschnitt 3.2](https://www.rfc-editor.org/rfc/rfc8785.html#section-3.2).

| Aspekt | Quelltext und Bewertung |
|---|---|
| UTF-16-Sortierung | CwmJcs.cs:25, StringComparer.Ordinal; rekursiv und unabhängig von Kultur |
| Unicode | Zeilen 35,44–55; keine Normalisierung, gültige Paare bleiben erhalten, einzelne Surrogates werden verworfen |
| Escaping | Zeilen 51–55; kurze Kontrollzeichen-Escapes, übrige Controls als kleingeschriebenes uXXXX, Quote/Backslash escaped |
| Ganzzahlen | Zeilen 36–39; Int64-Parsing plus sichere Grenze ±(2^53−1), invariant dezimal, -0 wird 0 |
| UTF-8 | Zeilen 13–17,47; strikt, ohne BOM und ohne Ersatzzeichen |
| Duplikate | Zeilen 25–27 sowie CwmCoordinator.cs:169–174; auch nach JSON-Unescaping identische Namen werden verworfen |

Alle 13 vorhandenen Node-Vektoren stimmen sowohl bei erneuter unabhängiger
ECMAScript-Berechnung als auch beim Aufruf des bestehenden C#-Canonicalizers.
Zusätzlich in-memory geprüft: `{"a":1,"\u0061":2}`, einzelner hoher Surrogate
im Wert, einzelner niedriger Surrogate im Schlüssel, 9007199254740992, `1.0`
und `1e0` werden verworfen; `-0` ergibt `0`.

Die Ablehnung von Arrays, Boolean, null und nicht ganzzahlig geschriebenen Tokens
ist eine dokumentierte Profilbeschränkung. Die API ist kein allgemeiner JCS-
Serializer. Unabhängige Verifier müssen vor allgemeinem JCS ebenfalls dieses
strengere Profil prüfen. Die Node-Referenz ist unabhängig vom C#-Code, aber ein
kleiner profilspezifischer Generator, keine vollständige JCS-Konformitätssuite.

## 5. R3 – Produktive MAC-Berechnung und Verifikation

Der Pfad wurde von OperationFactory über CwmCoordinator, FileMacCoordinator,
Parameter-/Schlüsselvalidierung bis zu StreamingBlockMac beziehungsweise Bouncy
Castle nachvollzogen. Die Whitelist enthält die erwarteten 17 Mechanismen:

| Mechanismus | Datei-Tag in Bytes |
|---|---:|
| AES-CMAC / DES3-CMAC / AES-GMAC | 16 / 8 / 16 |
| AES-CBC-MAC | explizit 8–16, Standard 16 |
| DES3-CBC / DES3-RETAIL | explizit 4–8, Standard 8 |
| HMAC-SHA1 | 20 |
| HMAC-SHA224 / HMAC-SHA256 | 28 / 32 |
| HMAC-SHA384 / HMAC-SHA512 | 48 / 64 |
| HMAC-SHA512-224 / HMAC-SHA512-256 | 28 / 32 |
| HMAC-SHA3-224 / HMAC-SHA3-256 | 28 / 32 |
| HMAC-SHA3-384 / HMAC-SHA3-512 | 48 / 64 |

Kein zusätzliches Abschneiden vollständiger CMAC-/GMAC-/HMAC-Tags gefunden.
Die CBC-/Retail-Länge ist Bestandteil der authentifizierten Parameter.
GMAC verwendet den Nonce als Bytefolge; CBC benutzt Null-IV, auch bei einem
gespeicherten DES3-Verschlüsselungs-IV. Padding, MACLEN und HEX-Nonce werden für
den Container normalisiert. M3 benutzt die Länge des geöffneten Handles.
KeyUsage.MacVerify wird bei Verifikation verwendet; Metadatenwarnungen sind
gemäss bestehendem Vertrag Warnungen und keine Zugriffskontrolle.

`MacOpened` (FileMacCoordinator.cs:48–57) liest dasselbe frisch geöffnete Handle,
prüft Anfangs-/Endlänge und Endposition und gibt MAC und Grösse zusammen zurück.
Es gibt keinen zweiten pfadbasierten Datei-Read für die Tagbildung. Der gemeinsame
Reader verarbeitet tatsächlich gelesene Bytes und löscht seine eigenen Puffer.

Beide MAC-Vergleiche verwenden FixedTimeEquals (CwmCoordinator.cs:119,132).
Taglängen werden vor dem jeweiligen erfolgreichen Vergleich eingeschränkt;
F04-RV-01 betrifft die Fehlerreihenfolge. Der PARAM-Vergleich mit SequenceEqual
vergleicht öffentliche Metadaten, keine geheimen MAC-Bytes. Das gesamte Verfahren
ist wegen Parsing, Längen-/PARAM-Prüfungen und I/O selbstverständlich nicht
laufzeitkonstant. Eine gültige Metadatenauthentifizierung ist Voraussetzung für
das Öffnen der referenzierten Datei. Ein falscher Dateiinhalt oder eine falsche
Grösse wird nicht als erfolgreich akzeptiert.

## 6. R4 – Dateisystem, Rennen und Cancellation

FilePublication wird unverändert wiederverwendet. Windows bindet die
Verzeichniskette, öffnet Quellen ohne Reparse-Folgen und mit FILE_SHARE_READ;
Unix verwendet gebundene Directory-FDs, no-follow und Regular-File-Prüfungen.
SafeName begrenzt die authentifizierte Referenz auf einen einzelnen Dateinamen
und sperrt Pfadseparatoren, Traversal, ADS und problematische Zeichen.

Die Containergrösse wird beim Lesen auf 65.536 Bytes begrenzt, unabhängig von
einer zuvor ermittelten Dateilänge. Tiefe 8 begrenzt JSON-Rekursion. Ein Tempfile
entsteht erst nach der MAC-Berechnung, wird privat erstellt, geflusht und atomar
ohne Überschreiben veröffentlicht. Kollisionen führen zum nächsten Namen;
parallele Operationen teilen keinen temporären Dateinamen. Windows publiziert
per offenem Handle, Linux per fd-bezogenem Link, macOS mit Identitätsprüfung
innerhalb der vorhandenen Trusted-Directory-Grenze.

Cancellation wird vor/bei dem Lesen und vor Publikationsversuchen geprüft;
der gemeinsame Datei-Reader prüft auch nach Reads. Abbruch nach erfolgreicher
Publikation kann die Veröffentlichung nicht rückgängig machen. Synchrone native
I/O-Aufrufe sind nicht jederzeit unterbrechbar. Das ist keine Garantie sofortiger
Cancellation bei blockiertem Dateisystem.

**Grenzen, keine neu eingeführten Bypässe:**

- Unix verhindert keine gleichlangen konkurrierenden Schreibzugriffe. Handlebindung
  schützt Identität, nicht eine unveränderliche Momentaufnahme des Inhalts.
- Auf macOS bleibt zwischen Identitätsprüfung und namensbasiertem linkat ein
  Rennen gegen einen feindlichen Verzeichnisschreiber ausserhalb des zugesagten Modells.
- Bei Verzeichnisumbenennung unter Unix kann der zurückgegebene Pfad veralten;
  die fd-bezogene Publikation bleibt an das gebundene Verzeichnis gebunden.
- Cleanup ist best effort: FilePublication.cs:156–163 ignoriert Löschfehler.
  Die absolute Bereinigungsaussage in Implementierungsbericht Zeile 44 gilt daher
  nicht bei zusätzlichem Dateisystem-/Berechtigungsfehler während Cleanup oder
  Prozessabbruch. Dokumentation sollte diesen übernommenen Grenzfall benennen.
- Flush plus atomare Publikation ist keine nachgewiesene Crash-Durability-Garantie
  für den Verzeichniseintrag. Linux benötigt procfs und Hardlinks.

## 7. R5 – Sprache, Integration und Snapshot-Abgrenzung

Lexerregel T_VAL steht vor ID, Parser-Typregel und generierte Tokenzuordnungen
sind konsistent ergänzt. Die grossen generierten Diffs entstehen überwiegend
durch Tokenverschiebungen und ATN-Neugenerierung. OperationFactory, OperationList,
Hilfekatalog, Auslieferung der Dokumente, GUI-Overlays und Info-Completion enthalten
beide Funktionen. Verschachtelte Aufrufe/falsche Deklarationsresultate werden vor
Ausführung zurückgewiesen; der Legacy-String-Einstieg lehnt CWM ab.

F04-RV-02 und F04-RV-03 begrenzen die zugesagte Typ-/Diagnostikintegration.
Eine unbeabsichtigte Erweiterung des S01/S05-Snapshot-Formats wurde nicht gefunden:
VariableSnapshotJsonSerializer.cs:54–74 prüft konkrete Klasse und deklarierten Typ.
Ein StringVariableDeclaration mit CryptoTypeVal scheitert an dieser Prüfung.
Dies wurde zusätzlich in-memory mit dem Serializer bestätigt. Save nach einer
Verifikation muss VAL daher ausdrücklich aus der zu speichernden Auswahl lassen.

Die F01/F02-Publikationslogik ist unverändert. Bei F03 sind Validate/MacOpened
und die Verify-Usage-Auswahl hinzugekommen; die bestehenden Mac-/MacStream-Pfade
verwenden weiterhin den bisherigen Reader und die bisherige Vorbereitung mit
Default verify=false. Keine weitere konkrete F01–F03-Regression festgestellt;
eine neue vollständige Regressionsausführung war nicht Teil des lesenden Reviews.

## 8. R6 – Testabdeckung und Abgleich des Implementierungsberichts

**Tragfähige Evidenz:**

- 17 Mechanismen × zwölf Dateilängen, darunter leere Dateien, Block- und 64-KiB-
  Grenzen, prüfen Zusammenspiel und Streaming. Die 64-MiB-Allokationsprüfung
  kontrolliert grössenunabhängigen Speicherverbrauch.
- F04-Node-Fixture erzeugt HMAC-Dateitag, HKDF-Schlüssel, kanonische Bytes und
  Metadaten-HMAC ohne produktive C#-Helfer. Alle Bestandteile wurden lesend
  nachgerechnet. Der C#-Extract/Expand-Test ist eine zusätzliche unabhängige
  Formulierung des Ableitungsschritts.
- Bestehende FileMacReferenceTests.cs:86–100 vergleichen F03-Pfade mit externen
  Referenz-Tags und wechselnden Read-Grenzen. Das ergänzt die CWM-Roundtrips.
- Manipulation von Quelle/Metadaten, falscher KEY, PARAM-/Nonce-Abweichung,
  Duplikate, ungültiges UTF-8/Unicode/HEX, Traversal und fehlende Dateien sind
  enthalten. Zwölf parallele Publikationen und injizierte Fehler vor Publikation
  prüfen Kollisionen und Cleanup unter normalen Dateisystembedingungen.

**Lücken und Aussagegrenzen:**

- RoundtripAllMechanisms, insbesondere CwmTests.cs:71–73, vergleicht mit demselben
  FileMacCoordinator. Das ist kein unabhängiger Beweis aller 17 MAC-Definitionen.
  Die vollständige unabhängige CWM-Referenz deckt nur HMAC-SHA256 ab.
- Sign() benutzt produktives Authenticate/JCS. Damit signierte Negativfälle prüfen
  Verifier-Verhalten, aber nicht unabhängig die Korrektheit dieser Helfer.
- F04-RV-01 wird teilweise durch eine falsche Testerwartung festgeschrieben.
  Tests für ungültige VAL-Literale und CWM-Schreibvarianten fehlen.
- Im neuen CWM-Test fehlen gezielte Assertions auf Length/Position-Rennen des
  neuen MacOpened-Pfads, auf Cleanup-Fehler selbst sowie auf Cancellation während
  MacVerify-Lesen. F03-Streamtests liefern Teilabdeckung, ersetzen diese nicht.
- Zahlreiche Assert.Catch prüfen nur irgendeine Ausnahme. Sie beweisen nicht die
  korrekte Fehlerkategorie, Diagnose-Redaktion oder fehlende Nebenwirkungen.
- CwmInfoTests prüft die Rücknavigation, aber nicht ausdrücklich die im
  Implementierungsbericht genannte Vorwärtsnavigation.

Die wesentlichen Architektur-/HKDF-/JCS-Beschreibungen im Implementierungsbericht
stimmen mit dem Code überein. Zu korrigieren/präzisieren sind insbesondere die
Schema-/Diagnostikbehauptungen, die VAL-Wertdomäne, die Skip-Zahl und die absolute
Cleanup-Aussage. Der externe Vertrag ist für HKDF/JCS und Parameterspeicherung
gut reproduzierbar; für Padding und MAC-Details verweist er auf F03. Für ein
eigenständiges Austauschdokument wären direkte Links zu diesen Mechanismen und
die vollständige 17-Zeilen-Taglängentabelle hilfreich.

Vorhandene lokale Ergebnisdateien, **nicht im Review neu ausgeführt**:

| Evidenz | Gelesenes Ergebnis |
|---|---|
| artifacts/F04/core.trx | 10.651 Passed, 25 NotExecuted, 0 Failed |
| artifacts/F04/gui.trx | 244 Passed, 0 Failed |
| Node-Nachrechnung in diesem Review | HKDF, Datei-/Metadaten-Tag und 13 JCS-Vektoren stimmen |
| In-Memory-C#-Prüfungen in diesem Review | JCS-Negativfälle korrekt; VAL-/Diagnostik-Findings reproduziert; Snapshot lehnt VAL ab |

## 9. Offene Plattformvalidierungen und Abschluss

Vor Freigabe sind die vorhandenen vollständigen Core-/GUI-Suiten für Linux x64,
macOS x64 und macOS ARM64 auszuführen. Die Matrix in `.github/workflows/dotnet.yml`
ist vorhanden, aber kein Nachweis eines Laufs dieses uncommitteten Stands.
Windows-Symlink-/Reparse-Tests müssen mit den nötigen Rechten tatsächlich laufen;
der CWM-Symlinktest wurde im vorhandenen Lauf übersprungen. Auf macOS sind
native ABI/ACL/Directory-FD-Publikation und auf Linux procfs/Hardlink- sowie
Umbenennungs-/Konkurrenzfälle besonders relevant. Ein Dependency-Audit wurde
nicht nachgeholt; der dokumentierte nicht erreichbare Vulnerability-Feed bleibt offen.

**READY FOR CORRECTION.** Zuerst die Findings beheben beziehungsweise die
betroffenen Verträge ausdrücklich entscheiden, passende Regressionstests ergänzen
und die Plattformvalidierung nachholen. Die Gleichschlüssel-/HKDF-Anforderungsfrage
bedarf einer expliziten Vertragsentscheidung, falls der frühere Direct-Key-Vertrag
weiter gelten soll. Es wurden keine Korrekturen vorgenommen und keine Vertrags-
oder Versionsänderung festgelegt. Kein Commit und kein Push.

---

## 10. Unabhängiger Re-Review nach Korrekturen – 24. Oktober 2026

**Abschluss: READY FOR COMMIT für den geprüften CWM-v1-/HKDF-Stand.**
Die vier ursprünglichen Findings sind **VERIFIED FIXED**. Neue Findings:
**0 Critical, 0 High, 0 Medium, 0 Low**. Dies ist keine Freigabe einer
Direct-Key-Vertragsänderung oder eine Behauptung plattformübergreifender Tests.
Die unter R1 beschriebene Vertragsentscheidung bleibt ausdrücklich offen.

### 10.1 Unabhängigkeit, Umfang und Grenzen

Dieser Abschnitt stammt von einem getrennten Reviewer, der die Korrekturen
nicht implementiert hat. Grundlage waren der historische Bericht oben, der
Implementierungsbericht einschliesslich Korrekturrunde, die tatsächlichen
versionierten und unversionierten F04-Quellen/Tests/Referenzen und der beigefügte
Originalauftrag `C:\Users\stefa\.codex\attachments\e06a9b4b-bf8f-4f2c-9643-41662afe152a\Pasted text.txt`.
Die Bewertungen wurden anhand von Quelltext, neu ausgeführten Builds/Suiten und
eigenen Gegenproben gebildet, nicht aus dem Abschluss des Implementierungsagenten.

Branch `master`, HEAD/Baseline `38e00c32828e9550d42d413c0e6f00c621ee9f5e`.
Keine produktive Korrektur, kein Commit/Push/Merge/Rebase/Reset/Branchwechsel;
andere Worktrees wurden nicht bearbeitet. Einzige bewusst geschriebene
Quell-/Berichtsdatei ist dieser angehängte Abschnitt. Build-/TRX-Artefakte und
ein separater temporärer Gegenproben-Runner liegen in `artifacts/F04-rereview/`.
Die historischen Berichtsteile wurden nicht umgeschrieben.
`.codex/config.toml` wurde weder gelesen noch geschrieben. Ohne Inhalts-/Hash-
Vergleich wird keine umfassend nachgewiesene Unverändertheit dieser Datei behauptet.
Die LF/CRLF-Warnung für CryptoScriptLexerExtensions.cs ist kein inhaltlicher Diff:
`git diff` für diese Datei ist leer; sie erscheint nicht in `git status --short`.

Geprüft wurden insbesondere die neuen Dateien CwmCoordinator, CwmJcs,
ValVariableDeclaration, CwmTests, CwmInfoTests, beide Funktionshilfen, F04-Referenzen
und check-test-results.ps1; dazu die Änderungen an Grammatik/generiertem Parser,
Evaluatoren, Basistyp, Operationsregistrierung, Datei-MAC, GUI/Infointegration
und Projekteinbindung. Die unveränderten FilePublication-/Snapshot-Pfade wurden
an ihren relevanten Aufruf- und Fehlergrenzen nachvollzogen.

### 10.2 Erneute Bewertung der ursprünglichen Findings

| Finding | Status | Evidenz |
|---|---|---|
| F04-RV-01 | **VERIFIED FIXED** | CwmCoordinator.cs:114 ruft ValidateFileSchema vor Authenticate/FixedTimeEquals auf; Schemafunktion ab Zeile 141 prüft Whitelist, exakte Parameterfelder und Taglänge unabhängig vom Caller-PARAM. |
| F04-RV-02 | **VERIFIED FIXED** | VariableDeclarationEvaluator.cs:53 weist Nicht-Funktionsinitialisierer vor Registrierung ab; ValVariableDeclaration.cs:7–34 speichert bool und validiert virtuelle Setter; VariableDeclaration.cs:15 verbietet Umetikettieren gewöhnlicher Objekte. |
| F04-RV-03 | **VERIFIED FIXED** | FunctionCallEvaluator.cs:410–419 verwendet ordinalen case-insensitiven Vergleich im gemeinsamen Laufzeitfehlerpfad. |
| F04-RV-04 | **VERIFIED FIXED** | Neu erzeugte TRX-Dateien unabhängig ausgezählt; Bericht und check-test-results.ps1 stimmen mit Einzelresultaten überein, negative Berichtskopie wird zurückgewiesen. |

**RV-01, eigene Gegenprobe:** Separater Runner über die öffentliche
OperationFactory, mit unabhängigem Extract/Expand und HMAC, ohne Aufruf des
produktiven Authenticate. Für unbekannten Mechanismus, parameters als String,
Array oder null, zu kurzen/zu langen Tag, Kleinbuchstaben-HEX und zusätzliches
Parameterfeld wurde jeweils mit unverändertem falschem sowie neu berechnetem
richtigem Metadaten-HMAC geprüft. Die Originaldatei existierte nicht.
Alle 16 Kombinationen scheiterten an FormatException; keine fiel auf `false`
oder eine fehlende Quelldatei zurück. Eine wohlgeformte Referenzänderung mit
falschem HMAC lieferte dagegen `false` ohne Quelle. Die neu ausgeführten
Repositorytests ergänzen dies um Padding, MACLEN-Typ/Bereich, IV, Nonce,
Retail-Padding, alle 17 Taglängen sowie authentifizierten PARAM-Mismatch.
Die Quelldatei wird weiterhin erst in CwmCoordinator.cs:129 geöffnet.

**RV-02, vollständiger Weg:** Parser/AST leiten VAL an denselben Evaluator;
Funktionsresultate werden zusätzlich auf Typgleichheit geprüft. Eigene String-,
HEX-, Base64-, Zahlen-, Pfad- und Leerinitialisierer wurden abgewiesen, ohne
ein vorhandenes VAL-Objekt im Dictionary zu ersetzen. Über die Basisklassen-
Referenz wurden `junk`, `1`, `True`, Leerstring und null abgewiesen; der Wert
blieb `true`. Direkte Expression.BuildVariable-Pfade erreichen den gesicherten
Basistyp-Setter vor Dictionary.Add. Die Klasse ist sealed, ihr Type-Getter
liefert immer CryptoTypeVal. Die GUI-Grenze in VariableViewModel.cs:38–43
stellt ungültige Eingaben wieder her; die vier neuen GUI-Fälle bestanden.
Synchroner/asynchroner Runner, true/false und fehlerhafte erneute Deklarationen
sind durch neu ausgeführte CWM-Tests abgedeckt. Print nutzt Value über
OutputOperations.cs:34. VariableSnapshotJsonSerializer.cs:64/139 lehnt VAL
beim Schreiben/Lesen ab; keine stille VAR-Konvertierung und keine Änderung
des Snapshotformats. Keine Regression der übrigen Typen in Core/GUI festgestellt.

**RV-03, eigene exhaustive Schreibweisenprüfung:** Die FN-Grammatik verlangt
initial `[A-Z][a-z]`, danach beliebige ASCII-Buchstabencases. Alle 32 zulässigen
Schreibweisen von MacPack und 128 von MacVerify wurden mit fehlendem privaten
Dateinamen tatsächlich ausgeführt. Alle 160 ergaben FunctionExecutionFailed,
`<Funktionsname>(<redacted>)` und `<Funktionsname> failed.`; kein roher Pfad
oder Exceptiontext erschien darin. Repositorytests prüfen zusätzlich Location
und fehlende Ergebnisregistrierung. Case-insensitive Erkennung gilt für den
zentralen Fehlerpfad; neue API-Vertragsfehler enthalten nur Typ-/Referenzangaben.

**RV-04, Zählung und Gegenkontrolle:** Der eigene XML-Lauf zählt UnitTestResult-
Elemente direkt, nicht die verkürzte CLI-Summe. Core enthält 10.723 Resultate:
10.698 Passed und 25 NotExecuted; GUI 248 Passed. Das neue Prüfskript akzeptiert
diese frisch erzeugten Dateien mit dem Implementierungsbericht. Eine nur im
Artefaktverzeichnis erzeugte Kopie mit `skipped=24` wurde mit `Report does not
match TRX` zurückgewiesen. Die CLI zeigt tatsächlich weiterhin 24 Skips und
10.722 Total; der optionale Hardwaretest ist zusätzlich im TRX enthalten.
Die 25 Skips umfassen 16 Symlinkfälle ohne lokale Rechte, sieben Unix-spezifische
Native-Tests, einen macOS-ACL-Test und den optionalen Hardwarebenchmark.

### 10.3 Regressionen und unabhängige kryptografische Evidenz

Alle 17 Mechanismen bleiben durch dieselbe FileMacCoordinator-Whitelist und
bestehende MAC-Implementierungen abgedeckt. Die CWM-Roundtrips über 12 Dateigrössen
je Mechanismus, Blockgrenzen, 64-KiB-Grenzen und grosse Dateien wurden neu
ausgeführt. Die vollständigen Coretests umfassen auch die bestehenden
F01–F03-Referenz-/Fehler-/Streamingtests. Eine unabhängige eigene Node-Berechnung
bestätigte den Datei-HMAC-SHA256 der F04-Referenz, die kanonischen bytes und den
HKDF-Metadaten-Tag. Nicht für jede der 17 Implementierungen wurde in diesem
Re-Review ein neuer externer Kryptografie-Oracle geschrieben; deren Absicherung
beruht zusätzlich auf Quellvergleich und vorhandenen, neu ausgeführten Vektoren.

CWM-Version 1, sämtliche Formatlabels, HKDF-Salt/Info/Länge und Referenzen bleiben
unverändert. Eigener Extract/Expand bestätigt Kmeta
`2C809233956A961FF13E540A758B0D0CEEC18D7A25BEFCB345F9A3C023D3EA36` und Tag
`92224F80F8F5232A62BA72765F6B09B7196C69A98BC3878376110D5531F54243`.
Der eigene Direct-Key-Tag
`84E10664E0636A0D35EFE7ACF4258E683EABAB9B31F66D975D2D0CD1558A308F`
wurde als `false` abgelehnt. Der Originalauftrag Abschnitt D verlangt HKDF;
der Gegensatz zur älteren Gleichschlüssel-Anforderung wird hier nicht entschieden.
Es wurde keine Umstellung und kein Fallback eingeführt.

JCS: Alle 13 gespeicherten Vektoren wurden unabhängig mit ECMAScript nachgerechnet;
die C#-Referenztests bestanden ebenfalls. Eigene C#-Negativproben wiesen nach
Unescaping doppelte Schlüssel, 9007199254740992, 1.0, 1e0, Array, Boolean, null
und einen lone surrogate zurück. Quellprüfung bestätigt UTF-16-Ordnung, striktes
UTF-8, rekursive Duplikatkontrolle, Escapes und fehlende Unicode-Normalisierung.
Dies ist weiterhin das dokumentierte eingeschränkte CWM-Profil, kein allgemeiner
Float-JCS-Serializer.

Beide MAC-Vergleiche bleiben FixedTimeEquals (CwmCoordinator.cs:120/133).
SequenceEqual vergleicht nur öffentliche kanonische Parameter. Die Taglängen
werden vor dem HMAC-Ergebnis geprüft. Eigene Rohschlüssel-/HKDF-Puffer werden
im finally gelöscht; die bereits dokumentierte Bouncy-Castle-Key-Schedule-Grenze
bleibt bestehen. FileMacCoordinator.MacOpened bindet Daten und Grösse an dasselbe
Handle und prüft Endposition/Endlänge; keine neue pfadbasierte Doppelöffnung.

FilePublication wird unverändert genutzt: sichere Verzeichniskette, no-follow/
Reparse-Prüfung, private exklusive Temporärdatei, Flush und atomare Publikation
ohne Überschreiben. Der neue Kontrollfluss behält Cancellation vor I/O, während
Reads und vor Publikation sowie finally-Cleanup. Tests für vorhandene Dateien/
Verzeichnisse, parallele Publikation, erzwungene I/O-Fehler, Namenserschöpfung,
Cancellation und unveränderte Originaldateien bestanden. Bereits dokumentierte
Grenzen (Unix gleichlange Fremdschreibzugriffe, Linux procfs/Hardlinks, macOS
vertrauenswürdiges Publikationsverzeichnis, Cancellation nach Commitpunkt)
bleiben ausdrücklich bestehen.

### 10.4 Neu ausgeführte Validierung

Windows x64, .NET 10; alle folgenden Ergebnisse stammen aus diesem Re-Review:

| Prüfung | Ergebnis |
|---|---|
| Solution-Build | erfolgreich |
| GUI-Testprojekt-Build | erfolgreich |
| Desktop-Host-Build | erfolgreich |
| Gezielte CwmTests | 320 Passed, 1 NotExecuted, 0 Failed |
| Vollständige Core-Suite | 10.698 Passed, 25 NotExecuted, 0 Failed |
| Vollständige GUI-Suite | 248 Passed, 0 NotExecuted, 0 Failed |
| Eigene C#-Gegenproben | erfolgreich; Schema, VAL, 160 Schreibweisen, JCS und Direct-Key-Ablehnung |
| Eigene Node-Nachrechnung | erfolgreich; Referenz-HMAC/HKDF, Canonical bytes, 13 JCS-Vektoren |
| TRX-Prüfskript positiv/negativ | erfolgreich / manipulierte Berichtskopie erwartungsgemäss abgelehnt |
| git diff --check | erfolgreich; nur LF/CRLF-Hinweise |

Builds jeweils `--no-restore -m:1 -p:UseSharedCompilation=false -v quiet`;
Tests jeweils `--no-build --no-restore -v minimal` mit TRX-Logger und
`--results-directory artifacts/F04-rereview`. Targeted zusätzlich
`--filter FullyQualifiedName~CwmTests`.
TRX-Dateien: `artifacts/F04-rereview/{targeted,core,gui}.trx`.
Eigener temporärer Runner: `artifacts/F04-rereview/probe/Program.cs` mit
`dotnet run --project artifacts/F04-rereview/probe/probe.csproj --no-restore`;
Output `artifacts/F04-rereview/probe-output.txt`.
Beim ersten Probelauf musste die nur im Hilfsrunner verwendete Canon-Funktion
für einen absichtlich ungültigen null-Parameter ergänzt werden; anschliessend
wurde der gesamte Runner erfolgreich erneut ausgeführt. Kein Produktfehler.

NU1900 wegen nicht erreichbarem NuGet-Vulnerability-Feed bleibt offen; erfolgreiche
Builds ersetzen diese externe Abfrage nicht. Linux x64, macOS x64/ARM64 wurden
hier **nicht ausgeführt**. Windows-Symlinktests wurden **übersprungen**, nicht
bestanden. Diese Grenzen sind kein neu behobener oder verschleierter Nachweis.

### 10.5 Entscheidung

**READY FOR COMMIT.** Die vier konkreten Korrekturfindings sind nach eigenständiger
Prüfung geschlossen; kein neuer behebungsbedürftiger Befund oder nachgewiesener
Authentifizierungs-Bypass. Die Freigabe betrifft den vorhandenen CWM-v1-HKDF-
Vertrag mit den dokumentierten Plattformgrenzen. Die gesonderte Direct-Key-
Vertragsentscheidung bleibt offen und darf nicht aus dieser Reviewfreigabe
abgeleitet werden. Es wurde kein Commit ausgeführt.

## 11. Nachfolgende verbindliche Vertragsentscheidung

Nach Abschluss des unabhängigen Re-Reviews hat der Benutzer am 24. Oktober 2026
mit «F04 – Commit, Push und CI-Validierung» CWM v1 mit unveränderter
HKDF-SHA256-Ableitung ausdrücklich freigegeben. Damit ist die im historischen
Review als offen bezeichnete Vertragsentscheidung abgeschlossen. Direct-Key und
Formatänderungen sind ausgeschlossen. Die Reviewbewertung der Implementierung
bleibt unverändert; die Plattformnachweise werden durch den beauftragten
Push-Workflow erhoben und im abschliessenden Chatbericht verlinkt.
