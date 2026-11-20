# F2 — Vollständiger Copy-Vertrag und Golden Values

**Normativer Architekturanhang · READY FOR OWNER REVIEW, nicht ACCEPTED**

## 1. Exakter Clipboard-Inhalt

| Tatsächlicher Werttyp | Exakter Text-Payload |
|---|---|
| string | Unveränderte UTF-16-Zeichenfolge, ohne JSON-Anführungszeichen. Kein Trim, Unicode-/Newline-Normalisieren, Umformatieren von Hexstrings oder zusätzlicher Zeilenabschluss. |
| char | Genau diese UTF-16-Codeeinheit; keine Ersetzung durch ein Codepoint-Label. Plattformgrenzen für isolierte Surrogate ausdrücklich behandeln. |
| bool | `true` / `false`. |
| byte, int, long, ulong | Invariante Dezimalziffern, kein Gruppierungszeichen, keine Exponentialdarstellung; Minus nur bei negativem signed Wert. Auch `18446744073709551615` muss exakt bleiben. |
| fachliches Enum | Deklarierter Enum-Name, beispielsweise `Aes`, `Secret`, `Unspecified`. Keine Lokalisierung. |
| KeyUsage-Flags | `None` bei 0; sonst bekannte Einzelbit-Namen in aufsteigender Bitposition, getrennt durch `, `, z.B. `Encrypt, Decrypt`. Die Core-Klasse validiert unbekannte Bits; für absichtlich synthetische undefinierte Enumwerte deterministisch die invariante zugrunde liegende Ganzzahl verwenden. |
| null | Die vier Zeichen `null`; ein tatsächlicher String `"null"` hat im Skalar-Copy dieselben Zeichen. Der Typ steht im UI, nicht als zusätzliche Hülle. |
| leerer String | Leerer Text-Payload, kein `(leer)` und keine Schlusszeile. |
| byte[] | `0x(` + zwei **großgeschriebene** Hexziffern je Byte + `)`. Leeres Array: `0x()`. Byte-Reihenfolge unverändert. |
| Bekanntes fachliches Objekt | Kanonisches JSON mit tatsächlichen öffentlichen fachlichen Membernamen und deren Werten gemäß Abschnitt 2. |
| bekannte Collection | JSON-Array der vollständigen Elemente in Indexreihenfolge. Count/Capacity/Item gehören zur getrennten Property-Ansicht, nicht zum Collection-Wert. |
| Dictionary<string,string> / ReadOnlyDictionary | JSON-Objekt aus tatsächlichen Key/Value-Paaren. Keys ordinal sortiert. Keine Metadaten-/Comparer-Hülle. |
| bekannte Keys-/Values-Collection | JSON-Array in der aufgenommenen Enumerationsreihenfolge. Values nicht unabhängig sortieren. |
| KeyValuePair<string,string> | JSON mit genau `Key` und `Value`. |
| bekannter Standard-Comparer ohne öffentliche Properties | `{}`: ausdrücklich vereinbarte leere öffentliche Memberprojektion; der Runtime-Typ bleibt im UI. Nicht auf unbekannte Objekte übertragen. |

Ein bereits stringförmiger KCV-/KEY-Wert wird **nicht** wie byte[] neu formatiert. Der bestehende KCV-Calculator verwendet derzeit `FormatConversions.ByteArrayToHexString`, also Kleinbuchstaben. Diese Stringausgabe muss im Copy unverändert bleiben. Beispiel: string `0x(aabb)` → `0x(aabb)`; byte[] `{0xAA,0xBB}` → `0x(AABB)`.

### Kanonisches JSON-Wertformat

- Keine Einrückung, keine optionalen Leerzeichen, kein abschließendes LF/BOM. UTF-16-String als Clipboard-Text; Dateien mit Golden Values sind UTF-8-Dokumente, deren `expectedText` den exakten Clipboard-String enthält.
- Objektnamen auf jeder Ebene nach `StringComparer.Ordinal` sortieren. Arrays behalten ihre Reihenfolge. Case bewahren: OptionalBlock hat `ID`, SecretKeyAttribute hat `Id`.
- Strings in JSON mit normalen JSON-Anführungszeichen; `"`, Backslash und Steuerzeichen escapen. Kurzformen `\b`, `\t`, `\n`, `\f`, `\r`, sonst U+0000..001F als `\u00xx` mit kleinen Hexziffern. Slash nicht escapen. Normale Unicodezeichen einschließlich Surrogatpaaren bleiben erhalten. Isolierte Surrogate als `\uxxxx` bewahren. Keine Unicode-Normalisierung. Golden-Vergleich erfolgt auf Codeeinheiten, nicht nur nach JSON-Parse.
- Zahlen als die oben definierten vollständigen Zahlen; kein Double-Zwischenschritt für ulong/long. Im Browser-Prototyp sind fachliche Beispielzahlen klein; ein gesondertes BigInt-Golden deckt die Encodergrenze ab.
- Enums/Flags innerhalb JSON als Strings nach obiger Regel; null/bool/Zahlen bleiben native JSON-Werte. Bytearrays als JSON-String mit dem Hexliteral.
- Tatsächliche fachliche Keys namens `type`, `status`, `revision` oder `$ref` dürfen vorkommen. Der Inspector fügt solche Keys nicht hinzu. Keine pauschale Blacklist echter Daten.

Dieses Format ist eine explizite Darstellung der fachlichen öffentlichen Werte, **keine allgemeine CLR-Objektserialisierung und kein zugesagtes Deserialize-/Reconstruct-Format**. Reflection-Attribute/Serializer-Defaults beeinflussen es nicht. Einen unbekannten Typ als `{}` auszugeben ist verboten.

## 2. Vollständige bekannte Objektschemata

Die Reihenfolge hier dient dem Lesen; der Encoder sortiert ordinal. Alle aufgeführten Member werden ausgegeben, auch wenn null oder leer. Kein Statuswert wird als fachlicher Wert erfunden.

| Typ | Tatsächliche Member im JSON |
|---|---|
| VariableDeclaration / String / Path / Val | `Id`, `Type`, `Value`, `ValueFormat`, `Text`; nur String zusätzlich `GMAC`. Val.Value bleibt String. |
| KEY-Declaration | Basis + `DerivationMechanism`, `KeyType`, `Usage`, `KeySizeInBits`, `KeySize`, `KeyAttributes`, `Kcv`, `Kcvc`, `Kcvk`. |
| PARAM-Declaration | Basis + `Mechanism`, `HeaderReference`. **Kein** erfundenes `Parameters`. Die Methode GetParameters hat eigene Adapterzeile und eigenes Dictionary-Copy. |
| SECRET-Declaration | Basis + `SecretSet`; Value bleibt `SECRET`. |
| TR31H-Declaration | Basis + `Definition`; Value ist der tatsächliche auditierte Formatter-String, kein Wire-Header. |
| CryptoType und bekannte Unterklassen | `Id`, `Name`, `Text`; Id einschließlich seiner tatsächlichen Quotes. |
| KeyType | `Algorithm`, `MaterialKind`. |
| KeySize | `Bits`, `IsKnown`. |
| KeyUsagePolicy | `Mode`, `AllowedUsages`. |
| OptionalBlock | `ID`, `Length`, `Data`; keine Reparatur inkonsistenter synthetischer DTOs. |
| SecretSet | `Descriptor`, `Shares`, `PreferredShareFormat`, `OriginalKeyMetadata`. |
| SecretDescriptor | `Mechanism`, `Parts`, `MinParts`, `KeySizeBits`, `Profile`, `Field`, `ReductionPolynomial`, `SecretPosition`. |
| SecretShare | `X`, `Value` als reversibler Hexstring. |
| SecretKeyMetadata | `KeyType`, `Usage`, `DerivationMechanism`, `Attributes`. |
| SecretKeyAttribute | `Id`, `Length`, `Data`. |
| Tr31HeaderDefinition | `Attributes`, `OptionalBlocks`, `ExpectedLength`, `IsLengthResolved`, `Version`, `Algorithm`, `HeaderLength`. |

## 3. Unvollständigkeit, Berechnung, Referenzen und Historie

| Zustand | Copy-Verhalten |
|---|---|
| Vollständiger Skalar / vollständiger Parent | Sofort den vorhandenen detached Wert kopieren, auch historisch oder während MCP. |
| Bekannte, noch nicht geladene Member; gültiger Handle | „Vollständig laden & kopieren“ nimmt den verlangten fachlichen Wert auf. UI-Paging begrenzt diesen Umfang nicht. |
| Berechnete Member fehlen | Explizit „Laden, berechnen & kopieren“; UI benennt KCV/KCVC/KCVK. Kein heimliches Berechnen beim Expandieren. |
| Tatsächlicher null-Getterwert / KCV nicht anwendbar | null ist ein vollständiger Wert; Copy `null` bzw. JSON-null. UI-NotApplicable wird nicht mitserialisiert. |
| GetterError, UnsupportedAccessor/Formatter, WriteOnly | Vollständiges Parent-Copy nicht erfolgreich möglich. Fehler daneben anzeigen; kein Status-/Fehlerstring, kein weggelassenes Member, kein falsches null. Vollständige Kinder bleiben kopierbar. |
| Historischer, unvollständiger Parent | Fehlende Teile nicht von R+1 ergänzen. UI zeigt „Nicht vollständig aufgenommen; neue Auswahl erforderlich“. Vollständige historische Kinder weiterhin kopierbar. |
| Wiederholtes Ziel ohne Vorfahrenzyklus | Zielwert in der JSON-Wertdarstellung wiederholen; Referenznavigation im Baum separat. |
| Tatsächlicher fachlicher Vorfahrenzyklus | Parent-Copy `CopyUnavailable(Cycle)`; kein `$ref`-/Pfadersatz. Baum bleibt durch Referenznavigation endlich. |
| Array.SyncRoot | UI-Referenz auf sich selbst. Wert-Copy des Arrays bleibt Hex, da Metadaten nicht Teil des Array-Werts sind. |
| Ressourcen-/Clipboardfehler | Kein Erfolg und keine Teilwerte in Clipboard schreiben; vollständige bereits aufgenommene Werte bleiben verfügbar. |

Ein Copy-Command bindet Herkunft und Payload zu Beginn. Lädt es noch Live-Daten, gelten Request-/Stamp-Abbruchregeln. Ist der Wert bereits vollständig detached, hält ein lokaler Payload-Lease die benötigten Segmente bis zum Ende von Formatierung/Clipboard-Übergabe; ein Auswahlwechsel vertauscht weder Inhalt noch Herkunft. Close/Reset/Shutdown canceln noch nicht übergebene eigene Arbeit und räumen nach Drain auf. Ein bereits ans OS übergebener Clipboard-Aufruf lässt sich nicht sicher zurückrufen; kein automatisches Löschen oder späteres Ersetzen. Alle Dateihandles sind lokal referenzgezählt, keine globale Payload-Registry.

## 4. Golden-Value-Beispiele

`copy-golden-values.json` enthält vollständige Root-Goldens für KEY, PARAM, SECRET Split/Shamir und TR31H, den Parameter-Adapter sowie Skalar-/Byte-/ulong-Grenzfälle. Die äußeren Testfallfelder `id`, `property`, `expectedText` sind **Dokumentationsmetadaten**, niemals Teil des Clipboard-Payloads. Die folgenden kompakten Beispiele sind wortwörtliche Payloads.

### KEY

Quelle: Literal `KEY demoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)`. Tatsächlicher Literal-Builder setzt 256 Bit, `Secret(Unknown)`, Unspecified-Usage und leere Attribute. Er leitet **kein AES** aus der Länge ab. Nach expliziter KCV-Anforderung sind alle drei Ergebnisse null.

**F3-Herkunftskorrektur zu R4-L01:** Das Script oben bleibt unverändert. Der tatsächliche Property-Wert `Statement.Text` lautet `KEYdemoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)`. Der [Lexer verwirft Whitespace zwischen Tokens](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Antlr/CryptoLexer.g4:177); [AntlrToStatement übernimmt context.GetText()](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/AntlrToStatement.cs:14), und [StatementEvaluator weist diesen Text zu](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/StatementEvaluator.cs:45). Die [vorhandene Testquelle bestätigt Tokenverkettung bei erhaltenen Leerzeichen innerhalb von Stringliteralen](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScriptUnitTest/StatementExecutionTests.cs:43). Der Inspector kopiert diesen bereits vorhandenen Property-Wert unverändert; er entfernt selbst keine Leerzeichen. Der unabhängige Erwartungswert in `validate-artifacts.cjs` ist ein vollständiges Literal und wird weder aus Fixture noch Golden abgeleitet. Dies korrigiert ausschließlich die Fixture-Herkunft, nicht das Copy-Format.

`KeyType`:

```json
{"Algorithm":"Unknown","MaterialKind":"Secret"}
```

`KeySizeInBits`:

```json
{"Bits":256,"IsKnown":true}
```

Der vollständige KEY-Golden enthält sämtliche 14 Properties einschließlich `Text`, `Type` und aller drei null-KCVs. Keine Inspector-Hüllen.

### PARAM mit nicht-null HeaderReference

Quelle ist ein ausdrücklich synthetischer C#-Zustand mittels des vorhandenen `ParameterVariableDeclaration.FromSnapshot`: Mechanism `TR31`, geordnete Paare `#MECH=TR31`, `#BLKH=header`, Value `#MECH:TR31#BLKH:header`, HeaderReference auf das unten beschriebene Headerobjekt, anschließend Id `parameters`. Kein behauptetes ausführbares Script und kein PrintOutput zum Aufbau der Anzeige.

`GetParameters()`-Adapter:

```json
{"#BLKH":"header","#MECH":"TR31"}
```

Root-PARAM hat genau sieben Properties. HeaderReference enthält den vollständigen Headerwert und Definition. Der Methodenadapter wird nicht als achte Property eingefügt. Die intern synthetisch gewählte Mechanism-Angabe ist keine Zusage, dass ein solches PARAM ohne weitere Validierung eine reale Kryptofunktion ausführen könnte.

### SECRET mit nicht-null OriginalKeyMetadata

Deterministischer Split-Objektzustand: `SecretDescriptor(SECRET_SPLIT,3,3,16)`, Shares 0001/0203/0405 mit X=1/2/3, Format X-Y, Metadaten Secret(Unknown)/Unspecified/leer. Diese Shares ergeben per XOR 0607. Es wird keine zufällige Share-Ausgabe behauptet.

`SecretSet.Shares`:

```json
[{"Value":"0x(0001)","X":1},{"Value":"0x(0203)","X":2},{"Value":"0x(0405)","X":3}]
```

`SecretSet.OriginalKeyMetadata`:

```json
{"Attributes":[],"DerivationMechanism":"","KeyType":{"Algorithm":"Unknown","MaterialKind":"Secret"},"Usage":{"AllowedUsages":"None","Mode":"Unspecified"}}
```

Zusätzliches Shamir-Fixture: Descriptor 3/2/16, GF256-11B; ABBA/A8B9/A9B8 entsprechen `f(x)=AABB XOR (0101*x)` für x=1,2,3. Der Root-Golden besitzt `Value:"SECRET"` und den vollständigen SecretSet-Graphen. Byte-Copy fügt den X-Wert nicht heimlich zum Value-Bytearray hinzu.

### TR31H

Quelle: geordneter Create-Aufruf mit KBVID D, KEYU K0, ALGO A, MODEU B, EXP E; keine Optional Blocks. Die validierte Definition ergänzt Defaults; HeaderLength 16, NUMOPTB 00 und ExpectedLength null. `IsLengthResolved` ist false.

`Definition`:

```json
{"Algorithm":"A","Attributes":{"ALGO":"A","EXP":"E","KBVID":"D","KEYCTX":"0","KEYU":"K0","KEYVN":"00","MODEU":"B","NUMOPTB":"00","RSV":"0"},"ExpectedLength":null,"HeaderLength":16,"IsLengthResolved":false,"OptionalBlocks":[],"Version":"D"}
```

String-Property `Value`, ohne äußere Quotes und ohne Schlusszeile:

```text
TR31H { KBVID:D, KEYU:K0, ALGO:A, MODEU:B, EXP:E, KEYVN:00, KEYCTX:0, RSV:0, NUMOPTB:00, KBLEN:auto (unresolved) }
```

Die Dictionary-Reihenfolge im tatsächlichen Value-Formatter wird **nicht** an die JSON-Keysortierung angepasst. JSON-Definition und Value-String sind unterschiedliche tatsächliche Properties.

## 5. Große Goldens und Vergleichsverfahren

Der 31,875-MiB-Testfall ist algorithmisch definiert, um keine 64-Mi-Zeichen-Datei als Reviewbeilage zu erzwingen: 255 Shares, X=1..255; jedes Value besteht aus dem jeweiligen X-Byte genau 131.072-mal. Descriptor Split/255/255/1.048.576 Bit. Ein Value-Copy hat 262.148 Zeichen (`4 + 2*131072`), der Gesamtgraph enthält alle 255 Values. Vergleich: alle X-Werte, pro Share exakte Länge und Byteidentität, vollständiger kanonischer Parent-Payload; nicht nur Präfix/Suffix oder Vorschau prüfen.

Für Implementierungstests müssen die erwarteten Daten unabhängig vom produktiven Encoder erzeugt werden. Fixture-/Golden-Konsistenzprüfungen des HTML-Pakets sind keine C#-Produktabnahme. Der Browser-Prototyp simuliert Bedarfsladen und Revisionskonflikte; er implementiert weder den geplanten Disk-Store noch native Clipboard-/Admission-Garantien.
