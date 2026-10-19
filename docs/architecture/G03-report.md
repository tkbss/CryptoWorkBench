# G03 – Schlüsselkomponenten und Secret Sharing

Stand: 19. Oktober 2026. G03 einschließlich G03-R01, R1 und R2 implementiert, auf Windows x64 mit .NET 10 geprüft und nach unabhängigem Korrektur-Re-Review als **READY FOR COMMIT** freigegeben. Native Ausführung auf Linux x64, macOS x64 und macOS ARM64 bleibt unbestätigt; dieser Bericht ist kein plattformübergreifender Freigabenachweis und keine ISO-Zertifizierung.

## 1. Baseline und Umfang

- Branch: `master`; G03-Baseline: `453216e22aaf3c528d13806bc0a8ba6e6bfa7766`.
- Vor Beginn: ausschließlich unversioniertes `.codex/`. Keine dortige Datei geändert oder aufgenommen. Abschluss: ein G03-Commit nach Staging-Audit autorisiert; kein Push oder Merge.
- SDK 10.0.401; Runtime 10.0.12, Windows x64; Microsoft OpenJDK 21.0.12.1.
- Vor Implementierung: 6.861 Kerntests bestanden, keine fehlgeschlagenen/übersprungenen Tests. Ein gesonderter GUI-Baseline-Lauf vor Änderungen liegt nicht vor; alle 209 bisherigen GUI-Tests bestehen im Abschlusslauf zusätzlich zu 13 G03-Testfällen.
- Umfang: SECRET_SPLIT, SECRET_SHAMIR, Share/Secret/Restore, SECRET, vier Parameter, Print, InfoDocs, Tests und Referenznachweise. Keine G03-Persistenz, allgemeine Serialisierung, Datei-Import/-Export oder neue asymmetrische Kryptographie. Console-Quellcode unverändert.

## 2. Architekturprüfung und Integration

Die vorhandene Pipeline bleibt erhalten: ANTLR → AST-Adapter → Evaluatoren → FunctionCall → typisierte OperationInvocation → OperationFactory. Share und Restore verwenden die tatsächlichen SourceVariable-Objekte. Secret verlangt eine PARAM-Referenz und streng typisierte positionale Literalargumente. Es gibt weder einen zweiten Variablen-Lookup anhand von Strings noch eine serialisierte SECRET-Repräsentation für den Aufruftransport.

Die bestehende KEY-Literaldeklaration erzeugt teilweise eine allgemeine VariableDeclaration mit CryptoTypeKey. Share akzeptiert diesen vorhandenen Vertrag ohne Umbau anderer Mechanismen. Bei KeyVariableDeclaration werden Metadaten kopiert, sonst bleiben sie unbekannt. Share dekodiert vorhandene HEX- und Base64-KEYs; Restore erzeugt HEX. Nicht dekodierbare oder nicht binäre KEY-Darstellungen werden ausdrücklich abgewiesen.

MechanismRegistry beschreibt die Funktionen und Parameter. AlgorithmFactory liefert einen schmalen SecretSharingAlgorithm-Adapter für die bestehenden Parameters-Overloads; die objektwertigen Funktionen laufen direkt über SecretOperations. Parameterwerte werden sowohl beim Erstellen von PARAM als auch bei Share/Secret validiert. PARAM-Änderungen nach Share beeinflussen den Container nicht. Restore benötigt kein PARAM.

Die bestehenden nicht blockierenden KeyType-/Usage-Diagnosen anderer Funktionen bleiben unangetastet. G03 erfindet keine zusätzliche Usage-Berechtigung. Strukturfehler, inkonsistente Längen und unmögliche Rekonstruktionen sind harte Fehler. Neue Funktionsnamen sind im bestehenden vertraulichen Fehlerpfad erfasst; Tests prüfen die Redaktion von Share-Literalen.

ANTLR: T_SECRET, zwei Mechanismen, vier Parameternamen und dedizierte polynomialValue/shareFormatValue-Regeln. Lokale semantische Parser-Prädikate beschränken die erlaubten NORMAL_STRING-Werte; ungültige Werte erzeugen Syntaxfehler. NORMAL_STRING selbst bleibt unverändert, damit andere Parameter nicht regressieren. Generierte Quellen wurden mit ANTLR 4.13.2 wie die Baseline regeneriert; abschließende Leerzeichen wurden entfernt. Regenerierung: `java -jar antlr4-4.13.2-complete.jar -Dlanguage=CSharp -visitor CryptoScript.g4` im Antlr-Verzeichnis.

OutputOperations erweitert den objektbewussten Print-Pfad; ein einziges PrintEvent trägt die vollständige Ausgabe. Jede physische Ausgabezeile erhält `out: `, damit ScriptBooks sie weiterhin überspringen. Kopierbare Statements verwenden gültige Namen `importedParameters` und `importedSecret`. Der vorhandene Lexer reserviert mit Groß-/Kleinbuchstaben beginnende Namen wie `ImportedSecret` als FN. Für einen erneuten Import werden die `out: `-Präfixe entfernt. Metadaten ursprünglicher KEYs werden dabei absichtlich nicht erfunden.

## 3. Neue Dateien und Klassen

| Datei/Gruppe | Verantwortung |
|---|---|
| `CryptoScript/SecretSharing/Gf256.cs` | Maskierte Multiplikation, Addition, Inversion; keine geheimen Tabellenzugriffe |
| `SecretSharing/SecretSharingEngine.cs` | CSPRNG-XOR und Shamir, Interpolation und Konsistenzprüfung |
| `SecretSharing/SecretSet.cs` | SecretShare, SecretDescriptor, SecretSet, SecretKeyMetadata, SecretKeyAttribute |
| `SecretSharing/SecretParameters.cs` | Strenge Parameter- und Profilverträge für Script-Integration |
| `SecretSharing/SecretSharingAlgorithm.cs` | Parameters-Adapter für AlgorithmFactory |
| `SecretSharing/BouncyCastle-LICENSE.html` | Ungekürzter upstream MIT-Lizenztext; wird mit ausgeliefert |
| `Operations/SecretOperations.cs` | Share, Secret, Restore, strenge HEX-Dekodierung |
| `Variables/SecretVariableDeclaration.cs` | SECRET-Variable und vollständige Print-Darstellung |
| `CryptoScriptUnitTest/SecretSharingTests.cs` | 107 G03-Testfälle inklusive Parser-/Java-Vektoren und Sicherheitsregressionen |
| `ErrorListner/SecretImportDiagnosticScope.cs` | Eingrenzung sensibler Syntaxdiagnosen mit dem tatsächlichen ANTLR-Lexer |
| `InfoDocs/Info.Mech.SECRET_*.md` | Beide Mechanismen |
| `InfoDocs/Info.Func.{Share,Secret,Restore}.md` | Drei Funktionen |
| `InfoDocs/Info.Type.SECRET.md` | Datenmodell und vollständige Print-Beispiele aller Formate |
| `InfoDocs/Info.Param.SECRET_*.md` | Vier Parameterthemen, zusätzlich XOR-spezifische PARTS/SHAREFORMAT-Seiten |
| `docs/architecture/evidence/G03/` | Originalquellen, Manifest, Java-Harness, Vektoren, reproduzierbarer Ablauf und TRX-Evidenz |

Die arithmetischen Klassen und das immutable Datenmodell kennen weder ANTLR noch Avalonia. Sie verwenden die vorhandenen unveränderlichen KeyType-/KeyUsagePolicy-Werttypen. Script- und mutable OptionalBlock-Objekte werden erst in SecretOperations auf unveränderliche Metadaten abgebildet. Die Parameter-/Algorithmusadapter sind ausdrücklich Integrationscode, nicht der Kryptokern.

Bestehende Dateien wurden für Grammar, Typ-/Mechanismus-/Parameterregister, Funktionsdispatch, Deklarationsprüfung, Print, Dokumentidentitäten/Katalog/URI, Kopieren der InfoDocs und GUI-Funktionshinweise erweitert. Enum-Werte bestehender Funktionen/InfoDocumentKinds bleiben stabil. Bestehende Registertests behalten ihre Prüfungen; nur erwartete Inventare und die Discovery der neuen objektwertigen Operations wurden ergänzt. Die Mechanismenliste hat 68 statt 66 Einträge, die Parameterliste 31 statt 27.

## 4. Portierung und Lizenz

Unveränderliche Referenz: [bcgit/bc-java, Commit c314b9cdffa3958a0eff5344f8fdcdb0181ed830](https://github.com/bcgit/bc-java/tree/c314b9cdffa3958a0eff5344f8fdcdb0181ed830/core/src/main/java/org/bouncycastle/crypto/threshold).

Übernommen/adaptiert wurden die Algorithmen aus Polynomial.java, ShamirSecretSplitter.java (`resplit`, `initP`), ShamirSplitSecret.java (`getSecret`) und die Share-Darstellung aus ShamirSplitSecretShare.java. SecretShare.java, SecretSplitter.java, SplitSecret.java sind als unveränderte Compile-Abhängigkeiten enthalten. Zusätzlich liegt der unveränderte `core/src/test/java/org/bouncycastle/crypto/threshold/test/ShamirSecretSplitterTest.java` vor. Der vollständige MIT-Lizenztext und Copyright 2000–2026 The Legion of the Bouncy Castle Inc. liegen sowohl beim Port als auch bei den Originalquellen. SHA256SUMS.txt pinnt die Originalbytes; die lokale .gitattributes verhindert Zeilenendentransformationen.

Der Port nutzt den aktuellen `getInstance`-Pfad, nicht die veraltete Mode-Auswahl. BCs AES/RSA-Labels werden ausschließlich im Testadapter auf 11B/11D abgebildet. Produktionsprofile heißen SHAMIR-GF256-11B und SHAMIR-GF256-11D.

| Unterschied | Begründung und Interoperabilität |
|---|---|
| Horner-Auswertung statt vorbereiteter Potenzmatrix/gfVecMul | Algebraisch identischer Wert je Byte; weniger temporäre Daten. Java-Kreuztests prüfen beide Richtungen. |
| k >= 2 statt BCs k >= 1 | G03-Vertrag und zugängliche ISO-Parameter; k=1 wird ausdrücklich nicht unterstützt. |
| Nichtleere Schlüssel; keine BC-spezifische 65.534-Byte-Obergrenze | Ganzzahlige Bitlänge und Speichergrenzen werden geprüft; keine wire-format-Abhängigkeit von dieser BC-API-Grenze. Interoptests liegen unterhalb der BC-Grenze. |
| Harte Ablehnung von Null-/Doppelkoordinaten; Inverse(0) wirft | BC Polynomial.gfDiv bildet Null auf Null ab. Unterschiedliche Koordinaten garantieren nichtnull Nenner; x=0 würde den geheimen konstanten Term offenlegen. Gültige Shares bleiben interoperabel. |
| Mindestens k und Prüfung sämtlicher zusätzlicher Punkte | BC `getSecret` kennt keinen gespeicherten Schwellwert und interpoliert alle gelieferten Punkte. G03 rekonstruiert aus k Punkten und prüft den Rest gegen dasselbe Polynom. Widersprüchliche Mengen werden bewusst nicht akzeptiert. |
| Immutable Container statt exponierter Arrays | Keine unbeabsichtigte Mutation durch Aufrufer. Binäre Nutzdaten bleiben kompatibel. |
| .NET RNG statt Java SecureRandom; Löschen temporärer Puffer | Plattformgerechte Zufallsquelle, weniger langlebiges temporäres Material. Kein deterministischer RNG-Hook im Produktionscode. |

## 5. ISO-Prüfung unabhängig vom Java-Ergebnisvergleich

Quelle: [öffentliche Originalvorschau ISO/IEC 19592-2:2017](https://cdn.standards.iteh.ai/samples/65425/86901291939c44edb722f64aef7985d9/ISO-IEC-19592-2-2017.pdf). Abruf und visuelle Prüfung am 19.10.2026, PDF-Seiten 10/11, gedruckte Seiten 4/5. 5.2.1–5.2.5 waren zugänglich, einschließlich der Fortsetzung von 5.2.5. Temporäre PDF-SHA256: `5500AFA4890B5326A488D0ED715A9DEC9CB9DB5B402570418E068D1E854683ED`. Die urheberrechtlich geschützte PDF wird nicht ins Repository aufgenommen. ISO/IEC 19592-1:2016 wird als normative Referenz genannt; ihr vollständiger Text wurde nicht beschafft oder geprüft. Die vollständige Normkonformität ist deshalb **nicht abschließend verifiziert**.

Die folgende Anforderungszusammenfassung ist eine knappe Paraphrase; die Bewertung beruht zusätzlich auf eigenständiger Algebra und Codeprüfung.

| ISO | Anforderung | BC-Methode | CryptoScript | Bewertung / Abweichung | Korrektur | Testnachweis |
|---|---|---|---|---|---|---|
| 5.2.2 | Endlicher Körper; 2 <= k <= n < Körpergröße; bekannte Stützstellen | initP / Polynomial | SecretDescriptor, SecretShare, SecretSet | GF(256), n <= 255; BC akzeptiert zusätzlich k=1 | k=1 abweisen; x=0 und Duplikate sicherheitshalber ausschließen | ImmutableBoundariesAndInvalidContainers, InvalidParameterCombinationsFail, ThresholdBoundaryZeroSecretAndHomomorphism |
| 5.2.3 | Zufällige k−1 Koeffizienten; konstanter Term ist das Geheimnis; Auswertung an den Stützstellen | resplit / gfVecMul | Share | Identische Polynomauswertung; byteweise Vektorerweiterung | Horner statt Potenzmatrix; RNG.Fill über alle Koeffizientenbytes | FieldArithmeticExhaustivelyMatchesPolynomialOracle, Java-Vektoren und Kreuztests |
| 5.2.4 | Rekonstruktion durch Lagrange-Interpolation bei null aus k Punkten | getSecret | Restore / Interpolate | Nenner sind Koordinatendifferenzen; im Charakteristik-2-Körper ist Subtraktion XOR | Nullnenner verhindern, Schwelle erzwingen, zusätzliche Punkte prüfen | alle Teilmengen, unsortierte und nicht aufeinanderfolgende Java-Indizes; ExactlyThresholdDoesNotAuthenticateButAnyContradictoryExtraPointFails |
| 5.2.5 | Vertraulichkeit unterhalb k, gleich große Nutzdaten, additive Homomorphie; beschriebener Rechenaufwand | resplit / getSecret | Share / Restore | Mathematische Eigenschaften erhalten; CSPRNG ist praktische Zufallsquelle, kein Beweis perfekter physischer Zufälligkeit. Zusätzliche Konsistenzprüfung erhöht den Aufwand | Keine Authentizitätszusage; keine Fehlerkorrektur-/Homomorphie-API hinzugefügt | erschöpfender k=2-Einpunkt-Test; Homomorphietest; Längen-/Schwellen-/Teilmengentests |

In der Anmerkung zu 5.2.4 steht „degree k“, während die direkt davor stehende Formel bei k−1 endet. Der mathematisch konsistente Grad ist höchstens k−1 (ein zufälliger höchster Koeffizient darf null sein). Empfehlung/Umsetzung: Formel und Threshold-Vertrag beibehalten, keine künstlich zusätzliche Koeffiziente und kein Ausschluss von Nullkoeffizienten. BC resplit stimmt damit überein. Die fehlende ausdrückliche Nichtnull-/Eindeutigkeitsbedingung im zugänglichen Parametersatz rechtfertigt nicht die Offenlegung bei x=0 oder eine Division durch null. G03 validiert beides explizit.

Die Kontrolle der Aussage über den Rechenaufwand ist qualitativ/algebraisch, kein Laufzeitbenchmark. Die Informationsrate bezieht sich auf Share-Nutzdaten; externe Indexbytes und Container-Metadaten kommen hinzu. XOR n-von-n wird nicht als Shamir-Konformitätsnachweis ausgegeben. Keine formale ISO-Zertifizierung und kein Nachweis einer produktiven PCI-Schlüsselzeremonie.

## 6. Eigenständiger Sicherheits- und Architekturreview

Nach der Implementierung wurden die Bereiche A–J nochmals separat gegen Quellcode, Verträge und negative Tests geprüft. Dies ist ein zweiter Review-Durchgang innerhalb dieses Auftrags, kein externer Audit und kein Review durch eine unabhängige Organisation.

| Bereich | Prüfung / Ergebnis |
|---|---|
| A ISO | Formel, Grenzen, Nullkoeffizienten und Rekonstruktionsnenner gegen gerenderte Seiten geprüft; Scope-Grenze und Anmerkungsfehler oben dokumentiert. |
| B BC | Unveränderte gepinnte Quellen kompiliert, Originaltests ausgeführt, beide Profile in beiden Richtungen verglichen. Alle Produktions-GF-Aufrufe verwenden die maskierte Arithmetik. |
| C Sicherheit | RNG.Fill; feste acht Multiplikationsschritte; keine geheim indizierten Tabellen. Inverse-Verzweigungen betreffen ausschließlich öffentliche Koordinaten. Konsistenzvergleich akkumuliert Differenzen über alle Bytes und Zusatzpunkte vor der Fehlermeldung. Koeffizienten, Arbeits-, Dekodier- und Rekonstruktionspuffer werden in finally mit ZeroMemory gelöscht. Eingaben werden nicht verändert. |
| D Formate | Indexpräfix/-suffix bzw. strikte Integer/HEX-Paare; 1..255, ungerade HEX-Längen, Duplikate, unterschiedliche Längen, falsche Literaltypen und Reihenfolge geprüft. |
| E Architektur | Bestehende AST-Adapter unverändert wiederverwendet; typisierte SourceVariable-Identität auch nach Entfernen aus VariableDictionary getestet. Keine Secret-Stringauflösung. Literal-SECRET-Deklaration abgewiesen. |
| F Metadaten | Originaltyp/Usage kopiert; mutable OptionalBlocks in immutable Werte überführt und beim Restore frisch aufgebaut. Keine Algorithmusableitung bei 8/16/24/32/64 Byte externem Material. |
| G Print/GUI | Alle Werte vollständig, einmaliges PrintEvent, gültige kopierbare Namen, vorhandener GUI-Ausgabepfad, Überspringen sämtlicher out:-Zeilen in ScriptBooks getestet. |
| H Tests | Unabhängiger GF-Oracle mit carryless Integerpolynomen; sämtliche 65.536 Multiplikationen je Feld; echte Java-Ausführung zusätzlich zu Parser-Roundtrips. |
| I Dokumentation | Zwölf neue Katalogseiten, kanonische URI-Roundtrips, exakte Dateinamen/Großschreibung, alle internen Links und ausführbaren Beispiele getestet. |
| J Regression | Gesamte Kern-/GUI-Suiten grün; Core-Solution und Avalonia.Desktop gebaut. Keine Console-Quelländerung. Native Nicht-Windows-Tests bleiben offen. |

### Findings und Korrekturen

| Schwere | Finding | Korrektur / Evidenz |
|---|---|---|
| Hoch | Ungesicherte Übernahme von BCs Null-Divisor-Verhalten wäre fehlerhaft | Bereits im Port blockiert: eindeutige 1..255-Koordinaten plus Inverse(0)-Fehler; Negativtests. |
| Hoch | Interpolation aller gelieferten Punkte ohne bekannte Schwelle könnte Widersprüche verdecken | k-basierte Interpolation mit Prüfung jedes Zusatzpunkts; auch korrupter erster Basispunkt getestet. |
| Mittel | Frühe G03-Wertprüfung änderte die erwartete PSN-Fehlerklasse bestehender Registertests | Bestehende Support-/Scope-Diagnosen wieder vor G03-Wertprüfung; gesamte Regression grün. |
| Mittel | Erste Print-Fassung verwendete Namen, die der Lexer als FN erkennt | Klein beginnende Variablennamen; neun Parser-/Print-Roundtrips und Dokumentationsbeispiele grün. |
| Mittel | Ein Metadatenkonstruktor koppelte das Modell zunächst an eine Script-KEY-Klasse | Übertragung in SecretOperations verlegt; der Kern bekommt immutable Werte ohne Script-Objekt. |
| Niedrig | Neue Indexeinträge ohne erwartetes Leerzeichen vor Doppelpunkt wurden nicht verlinkt | Bestehendes Markdownformat übernommen; Katalog-/Dokumentationstests grün. |
| Niedrig | Neue GUI-Testannahme erwartete PrintMessage nach einer späteren Restore-Zeile | Test an bestehende zeilenweise Ausgabelebensdauer angepasst, Ausgabe vor dem nächsten Aufruf geprüft und out:-Replay zusätzlich getestet. Kein GUI-Verhalten verändert. |

Nach Korrektur sind keine im Review identifizierten Code-Findings offen. Verbleibende Nachweisgrenzen: kein externer Audit, keine JIT-/Maschinencode-Seitenkanalanalyse, keine Garantien für sichere Löschung von .NET-Strings, GC-Kopien oder langlebigen Share-Objekten. Die bestehende Workbench speichert Schlüssel als Strings. Genau k falsche Shares können ein falsches Ergebnis liefern; auch konsistente Mengen sind nicht authentifiziert. Eine vollständige Share-Ausgabe ist bewusst sensibel. Dual Control, Custodians und Secure Cryptographic Devices sind organisatorisch/technisch zusätzlich zu realisieren.

### Unabhängiger Korrektur-Re-Review G03-R01, R1 und R2

Das unabhängige Review fand eine Offenlegung vollständiger Share-Literale durch ANTLR-Syntaxdiagnosen vor der Funktionsauswertung (G03-R01). Lexer- und Parser-Listener redigieren im erkannten Secret-Import-Kontext sowohl Symbolwerte als auch Meldungstexte vor Speicherung oder Veröffentlichung in Diagnoseobjekten, statischem Fehlerpuffer, Konsole und GUI-Status. Fehlerpositionen und Originaleingabe bleiben erhalten. FileBuild behält seinen Konsolendiagnosevertrag; explizites Print(SECRET) gibt weiterhin alle Werte vollständig aus.

Der erste Korrektur-Review wies zwei verbleibende Ursachen nach: **R1** betraf die abweichende Interpretation ungültiger String-Escapes im manuellen Kontextscanner gegenüber der Lexer-Recovery; **R2** verwendete eine relative Spaltennummer als absoluten Quelltextoffset und konnte Material aus einer vorherigen Zeile übernehmen. R1 wurde durch einen isolierten Durchlauf des tatsächlichen CryptoScriptLexer ohne veröffentlichende Fehler-Listener behoben. R2 verwendet TokenStartCharIndex und die aktuelle Streamposition mit Eingabegrenzen. Bei NoViableAltException wird außerdem der Anfang des gesamten Diagnosebereichs berücksichtigt. Keine Änderungen an Arithmetik, Shareformaten oder G03-Funktionsverträgen.

Der anschließend beauftragte separate Review-Agent prüfte die sechs betroffenen Dateien strikt lesend und reproduzierte beide Findings sowie weitere Grenzfälle speicherbasiert mit den vorhandenen Assemblies. Ergebnis am 19.10.2026: **G03-R01, R1 und R2 RESOLVED; READY FOR COMMIT; keine neuen korrekturbedürftigen Findings.** Dies ist ein unabhängiger Agenten-Review innerhalb des Auftrags, kein externer Organisationsaudit. Die vollständigen Testsuiten wurden vom implementierenden Agenten ausgeführt; der Reviewer kontrollierte deren Protokolle und führte sie nicht erneut aus. Seine zusätzliche lesende Prüfung von git diff --check war erfolgreich.

Die Sicherheitsregressionen ergänzen die ursprünglichen 60 Core-/einen GUI-Fall um 47 Core- und zwölf GUI-Fälle: alle drei Shareformate, fehlende Kommas, beschädigte HEX-Literale, Lexer-Recovery, verschachtelte/mehrzeilige und nicht geschlossene Aufrufe, gültige Strings, getrennte Aufrufe, LF/CRLF, Spalten, EOF, StringBuild/FileBuild, Diagnoseobjekte/Fehlerpuffer/GUI und vollständige Print-Ausgabe. Geschützt werden tatsächliche vom Lexer erkannte Secret-Aufrufe; gültige String-/PATH-Inhalte gelten nicht als Aufruf. Eine beschädigte Funktionskennung ohne erkennbaren Aufruf liegt außerhalb dieses Kontexts. Nicht geschlossene erkannte Aufrufe reichen bis EOF. Der zusätzliche Lexerpass erfolgt je Diagnose; eine Lastmessung außergewöhnlich großer fehlerhafter Eingaben liegt nicht vor.

## 7. Reproduzierbare Interoperabilität

`evidence/G03/run-reference.ps1` prüft die Quell- und Dependency-Hashes, kompiliert die unveränderten Java-Klassen vor dem Dependency-JAR im Classpath, führt die Originaltests aus, vergleicht reproduzierbare Java-Vektoren, startet die G03-C#-Tests und lässt Java die C#-Shares rekonstruieren. Die JARs dienen nur als unveränderte Util-/JUnit-Abhängigkeiten; der getestete Threshold-Code stammt vom gepinnten Quellcommit. Kein zweiter C#-Nachbau ersetzt Java.

Abhängigkeiten: bcprov-jdk18on 1.83 und JUnit 4.13.2; URLs und SHA256 im Script. JARs und Klassen bleiben außerhalb der versionierten Quellen. Deterministischer SecureRandom steht ausschließlich im Java-Testharness. C# erzeugt für die Gegenrichtung echte RNG-Shares; diese ändern sich bei jedem Lauf. Die letzte separat erneut mit Java geprüfte Sammlung ist als `csharp-vectors.txt` dokumentiert.

- Java → C#: 11B und 11D, Schlüssel `000102030405060708090A0B0C0D0E0F80FF`, k=3, n=255, x=1/3/17/128/255. Alle fünf Shares werden in umgekehrter Reihenfolge in jedem der drei Formate durch den regulären Parser importiert und rekonstruiert.
- C# → Java: beide Profile, 1/16/24/32/64 Byte; jeweils drei sowie fünf nicht fortlaufende/unsortierte Shares. 20 unabhängige Rekonstruktionen stimmen mit dem Originalschlüssel überein.
- Java-Originaltests: `ShamirSecretSplitterTest.main`, inklusive 1.000 Multiple/Divide-Durchläufen sowie performTest (u.a. resplit, Feldäquivalenz, große Mengen, Längengrenzen, leere Mengen). Ausgabe `OK`. Historische Tests nutzen teils die deprecated Mode-API; der neue Kreuztest verwendet ausschließlich getInstance.

Reproduktion mit Java 21+, PowerShell 7 und .NET 10 aus dem Repository:

```powershell
./docs/architecture/evidence/G03/run-reference.ps1
dotnet test CryptoScriptUnitTest --logger "trx;LogFileName=core-final.trx" --results-directory docs/architecture/evidence/G03
dotnet test CryptoWorkBenchGuiTest --logger "trx;LogFileName=gui-final.trx" --results-directory docs/architecture/evidence/G03
dotnet build CryptoWorkBench.sln --no-restore -m:1
dotnet build CryptoWorkBenchAvalonia.Desktop --no-restore -m:1
```

## 8. CryptoScript-Beispiele

```cryptoscript
KEY k = GenerateKey(AES-ECB, 128)
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 3)
SECRET s = Share(p, k)
Print(s)
KEY restored = Restore(s)
```

11D und separate Koordinaten mit nachvollziehbarem kleinen Polynom:

```cryptoscript
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2,
    #POLYNOM: "SHAMIR-GF256-11D", #SHAREFORMAT: "SEPARATE-X")
SECRET s = Secret(p, 3, 0x(A9BD), 1, 0x(ABB9))
KEY restored = Restore(s)
```

Ergebnis `0x(AABB)`, Unknown/Secret, Unspecified, 16 Bit. Das kleine Beispiel nutzt niedrige Feldwerte und unterscheidet deshalb die Reduktionspolynome nicht; die Java-Vektoren und erschöpfenden GF-Tests tun dies. Äquivalente X-Y-Daten: `0x(03A9BD), 0x(01ABB9)`; Y-X: `0x(A9BD03), 0x(ABB901)` mit entsprechendem SHAREFORMAT. Vollständige Print-Beispiele stehen in Info.Type.SECRET.md und werden als erneuter Script-Import getestet.

## 9. Abschließende Testergebnisse

| Prüfung | Ergebnis |
|---|---|
| Kern-Baseline | 6.861 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Abschließende Kern-Suite nach R1/R2 | 6.980 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Abschließende GUI-Suite nach R1/R2 | 222 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Gezielte Secret-Sharing-Tests nach R1/R2 | 107 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Historischer G03-Java-Referenzlauf vor Diagnosekorrekturen | 60 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Original-Java-main | OK |
| C# → Java | 20/20 bestanden, beide Profile |
| CryptoWorkBench.sln Build | Erfolgreich, 0 Fehler / 0 Warnungen im Abschlussbuild |
| Avalonia.Desktop Build | Erfolgreich, 0 Fehler; beim Neuaufbau 9 Nullable-Warnungen in unveränderten Bestandsdateien, im abschließenden inkrementellen Build 0 Warnungen |
| git diff --check | Keine Whitespace-Fehler nach Generator-Bereinigung |

Die 119 zusätzlichen Kern-Testfälle gegenüber der Baseline bestehen aus 107 expliziten G03-Fällen und 12 durch erweiterte Metadaten/Register erzeugten Fällen. Die XML-TRX-Nachweise werden verlustfrei als `.trx.gz` aufbewahrt. Die aktuellen Nachweise unter `evidence/G03/security-final/` sind bytegetreue komprimierte Kopien von `TestResults/G03-R01-R1-R2/{focused,core-final,gui-final}.trx`; die dortige `verification.txt` enthält die ungekürzten abschließenden Test-/Build-Ausgaben. Die ursprünglichen Dateien `evidence/G03/{core-final,gui-final,g03-reference}.trx.gz`, `verification.txt` und `reference-verification.txt` bleiben historische Nachweise der ursprünglichen Implementierung mit 6.933/210/60 Tests; sie werden nicht als Nachweise des späteren Diagnosestands ausgegeben. Java-Originaltests und Interoperabilitätsnachweise bleiben gültig, da die nachfolgenden Korrekturen den Kryptokern nicht ändern. Frühere rote Register-/Inventartests sind als `core-first`/`core-second` dokumentiert. Keine Tests deaktiviert oder Validierungen umgangen.

Für den abschließenden Staging-Audit wurden ausschließlich diese dokumentarischen Statusangaben aktualisiert und vorhandene Abschlussprotokolle in den freigegebenen Evidence-Bereich übernommen. Eng begrenzte .gitattributes-Regeln erhalten die Originalbytes beider Lizenzkopien und der drei ursprünglichen Verifikationsprotokolle; deren vorhandene Leerzeichen beziehungsweise abschließende Leerzeilen werden ausdrücklich von der Whitespace-Prüfung ausgenommen. Alle übrigen Dateien bleiben regulär geprüft. Keine weiteren technischen Änderungen und kein erneuter vollständiger Testlauf; der Code entspricht dem freigegebenen Korrekturstand.

Umgebungsstörungen: ein früher GUI-Baseline-Versuch erfolgte nach dem Einfügen von T_SECRET, aber vor der Parser-Regenerierung, und konnte noch nicht kompilieren. Einzelne Sandbox-Testhost-Starts brachen mit Verbindungs-Timeout ab; der komplette Referenzablauf wurde anschließend außerhalb der Sandbox erfolgreich ausgeführt. Ein paralleler Solution-Build wurde vom Host abgebrochen; der sequenzielle Build mit `-m:1` bestand. Dies sind keine als bestanden ausgegebenen Testläufe.

## 10. Offene Nachweisgrenzen

Keine native Ausführung auf Linux/macOS oder ARM64 in dieser Sitzung; nur Prüfung auf plattformneutrale .NET-APIs, hostunabhängige Pfadbildung und ordinal exakte InfoDoc-Namen. Der Java-Reproduktionsablauf verwendet PathSeparator und plattformunabhängige Pfadbildung, setzt aber eine installierte Java-/PowerShell-Umgebung voraus. Eine vollständige ISO-Prüfung inklusive Teil 1 und weiteren referenzierten Anforderungen sowie ein externer Sicherheitsaudit sind nicht erbracht. Die funktionale Implementierung ersetzt diese Nachweise nicht.
