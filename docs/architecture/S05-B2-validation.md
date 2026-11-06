# S05-B2 – SecureSave: Implementierung und lokale Validierung

Stand: 2026-10-21. Baseline `ed0939f67432f94433638b5293f37fa4fb49d519`, Branch `master`.
Verbindliche Grundlage: freigegebenes S05-A-Design und Architekturreview unter `artifacts/`,
einschliesslich D1–D7, sowie `S05-B1-validation.md`.

## Ergebnis und Umfang

Die produktive void-Funktion `SecureSave` ist über `CryptoScriptRunner.ExecuteAsync` verfügbar.
Syntax, Optionskombinationen, Selektion, PATH und bekannte Ressourcenlimits werden vor der
Passwortanforderung geprüft. Eigene `SOPT_`-Tokens und AST-Optionsknoten gelangen nicht in
die `P_`-/PARAM-Registry. Der synchrone Runner meldet `AsyncExecutionRequired` (neuer stabiler
Diagnosewert 17); Initializer und verschachtelte Verwendung scheitern vor Nebenwirkungen
früherer Geschwisterargumente.

Die Implementierung serialisiert S01 genau einmal, validiert diese feste Zeichenfolge
detached und projiziert daraus das öffentliche Root-Manifest. Absoluter Zielpfad und Payload
stehen vor dem Dialog fest. Der Runner verwendet das B1-Passwortprotokoll mit Bestätigung,
Ownership, späten Antworten und den vorhandenen Execution-/Operation-IDs. Kein Passwort
wird in Invocation.Values, Variablen oder Diagnosen abgelegt. KDF und Dateiveröffentlichung
laufen auf einem Worker unter der bestehenden globalen Interpreter-Ausführungslease.

Argon2id v1.3 benutzt BouncyCastle 2.7.0 und den Bytearray-Overload mit explizit leeren
Secret-/Additional-Eingaben; PBKDF2 und HKDF benutzen die .NET-10-APIs. Die vier Kombinationen
GCM/GMAC × Argon2id/PBKDF2 entsprechen dem v1-Envelope und dem binären H/M-Framing aus S05-A.
GMAC verwendet leeren GCM-Plaintext und `H || B(P)` als AAD. Salt und Nonce werden pro Aufruf
unabhängig mit dem kryptografischen RNG erzeugt. Es gibt keine öffentliche deterministische
Salt-/Nonce-Override-API, keinen Passwortverifier und keine zusätzliche MAC im GCM-Modus.

Der gemeinsame interne `SnapshotByteFilePublisher` übernimmt die unveränderten S02-Pfad-,
ACL-, Dateirechte-, Flush- und nativen Publikationsmechanismen. S02s öffentliche API,
Fehlerreihenfolge und 16-MiB-Limit bleiben erhalten. SecureSave verwendet ausschliesslich
CreateNew. Cancellation wird vor Temp-Erzeugung, nach Flush und unmittelbar vor Publish
geprüft; erfolgreicher nativer Publish ist die Commit-Grenze. Cleanup bleibt best effort.
Interne, nicht registrierte Test-Seams markieren KDF-Ende, Flush und Veröffentlichung.

Kein SecureLoad, keine Inspect-Funktion, keine S01-Formatänderung, keine neuen NuGet-Pakete
und keine Änderung bestehender kryptografischer Algorithmen. Kein Commit oder Push.
`.codex/config.toml` wurde weder gelesen noch verändert. Der Index blieb leer.

## Unabhängige Referenzen und Testvektoren

`CryptoScriptUnitTest/Fixtures/SecureSave/generate_vectors.py` ist eine getrennte
Python-Referenzimplementierung des freigegebenen Framings. Sie importiert keine C#-Helper
und erzeugt vier feste vollständige Container einschliesslich S01-Payload, Manifest, H/M,
HKDF-info, Master-/Mode-Key und erwarteter Envelope-Bytefolge. Sämtliche Daten und Passwörter
sind synthetisch. Der NUnit-Test vergleicht jeden dieser Zwischenwerte und den vollständigen
Container bytegenau; die Fixture enthält einen Unicode-S01-Payload und ein Unicode-/NUL-Passwort.

Erzeugungsumgebung: Python 3.12.14, `cryptography` 50.0.2, dessen Backend OpenSSL 4.0.3
(29 Sep 2026); PBKDF2/HKDF-Referenz mit Python `hashlib`/`hmac`. Reproduktion mit einer
entsprechend ausgestatteten Python-Umgebung:

```text
python CryptoScriptUnitTest/Fixtures/SecureSave/generate_vectors.py
```

Python ist keine Produktions- oder CI-Testabhängigkeit; NUnit verwendet die eingecheckten
JSON-Fixtures. `SecureContainerTestReader` rekonstruiert das Framing separat aus den
Envelopefeldern und verwendet eigenständig zusammengesetztes HMAC-Extract/Expand. Er prüft
Authentifizierung und exakten Payload, auch nach Umordnung äusserer JSON-Properties.
Er ist absichtlich kein strikter produktiver Envelopeparser, kein Importpfad und keine
SecureLoad-Implementierung. Daraus wird keine SecureLoad-Validierung abgeleitet.

Bekannte primitive Testvektoren:

- Argon2id: [RFC 9106 §5.3](https://www.rfc-editor.org/rfc/rfc9106.html#section-5.3),
  v19, 32 KiB, drei Durchläufe, vier Lanes, Secret/Additional gemäss RFC. Die kleine
  Speichergrösse gilt nur für den direkten Bibliotheks-KAT, nicht für die Produktionspolicy.
- PBKDF2-HMAC-SHA256: [RFC 7914 §11](https://www.rfc-editor.org/rfc/rfc7914.html#section-11),
  `passwd`, `salt`, eine Iteration, 64 Bytes (ebenfalls primitiver KAT).
- HKDF-SHA256: [RFC 5869 A.1](https://www.rfc-editor.org/rfc/rfc5869.html#appendix-A.1),
  42-Byte-OKM.
- AES-256-GCM/GMAC: offizielles [NIST-CAVP-GCM-Archiv](https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Algorithm-Validation-Program/documents/mac/gcmtestvectors.zip),
  Datei `gcmEncryptExtIV256.rsp`. Je `Count=0`, Keylen=256, IVlen=96, Taglen=128,
  AADlen=128; PTlen=0 für GMAC und PTlen=128 für GCM. Die beiden unveränderten Fälle
  sind in `nist-gcm256.json` enthalten. Das vollständige Downloadarchiv bleibt unter
  ignorierten `artifacts/`. Diese Tests sind keine CAVP-Zertifizierung.

## Testabdeckung und Review

Die B2-Tests decken zulässige Syntaxformen, Optionsreihenfolge, doppelte/ungültige Optionen,
unzulässige KDF-Kosten und Kombinationen, ALL/Referenzen, Void-Verträge, Registry-Isolation,
Ressourcenlimits und sanitierte Diagnosen ab. Hinzu kommen alle sechs Variablentypen,
TR31H-Root-/Dependency-Identität, detached Manifest-/Payload-Konsistenz, Live-PATH-/Wert-
und CWD-Mutation während des Dialogs, Unicode, frischer Salt/Nonce, Passwort-Wiping,
Benutzerabbruch, Hostfehler, Bestätigungsmismatch, falsches Passwort, späte Antworten,
Cancellation nach Hostantwort/KDF/Flush, Publish-Race und erfolgreicher Publish trotz
nachträglicher Cancellation. Tamper-Tests ändern Header-, Manifest-, Payload- und Tagwerte.
Windows-DACL beziehungsweise Unix-Modus werden im produktiven SecureSave-Test geprüft;
die vollständige bestehende S02-Suite enthält weiterhin die macOS-ACL-/Symlink-Fälle.

Elf direkte Manifesttests auf S01-validierten detached Roots prüfen AES-128/192/256,
TDEA-2/3, HMAC, Unknown sowie RSA/EC jeweils public/private. Sie prüfen tatsächliche
Bitlängen, volle bestehende KCV-Darstellung beziehungsweise null-KCV ohne weitere KDF-Läufe.
Die neuen Headless-GUI-Tests führen echte SecureSave-Scriptbook-Aufrufe durch den Dialog
aus und prüfen Responsive-Dispatcher, Bestätigung, Feldbereinigung, Abbruch/Reihenfolge,
Folge-Print und `Info(SecureSave)`.

Die unabhängige, read-only Architektur-/Securityreview durch einen separaten Agenten
umfasste Produktionscode, S05-A-Verträge, Testreader, Python-Fixtures und Tests. Findings
und Korrekturen:

1. Gültige VAR mit `Type=null`: Manifest klassifiziert jetzt `StringVariableDeclaration`
   anhand der Klasse; Regression mit S01-null-Runtime-Metadaten.
2. Passwortbytes lebten bis nach Envelope-Erzeugung: Löschung jetzt unmittelbar nach KDF
   im finally; äusserer finally bleibt für Fehler vor KDF erhalten.
3. Testlücke bei weiteren KEY-Varianten: elf direkte Manifesttests ergänzt.
4. Fehlende explizite Abbruch-/Negativfälle: falsches Passwort und kontrollierte Phasen
   nach Passwortantwort, KDF und Flush ergänzt.
5. Zusätzliche Leerzeile am Ende der Funktionsübersicht: entfernt.

Der abschliessende Quelltext-/Test-Recheck meldete keine offenen materiellen Findings.
Der Reviewer änderte keine Dateien und führte selbst keine Tests aus. Ausführung und
unten aufgeführte Messwerte stammen vom implementierenden Agenten.
Der anschliessende unabhängige Faktenabgleich bestätigte finale TRX-Ergebnisse,
alle 12+4 Messwerte, Build-/Publish-Belege, Warnungen, die 45-Dateiliste, Baseline,
leeren Index und erfolgreichen Diff-Check ohne Abweichungen. Hardwareprovenienz
und historische Zwischenläufe wurden dabei nicht erneut reproduziert.

## Lokale Validierung

Windows 11 Home 10.0.26200, x64, .NET SDK 10.0.401, Release; GUI mit Avalonia.Headless.
Keine zusätzliche Linux-/macOS-Ausführung und keine neue GitHub-Actions-Ausführung.

| Prüfung | Tatsächliches Ergebnis |
|---|---|
| SecureSave-/Container-/Manifest- und Katalogtests | 110 bestanden, 0 Fehler |
| Gezielte AsyncPasswordGuiTests | 10 bestanden, 0 Fehler |
| Vollständige Core-Suite nach Korrekturen | 7547 bestanden, 0 Fehler, vier bedingte Skips |
| Vollständige GUI-Suite nach Korrekturen | 236 bestanden, 0 Fehler, keine Skips |
| Expliziter Performance-Lauf | 1 bestanden; 12 End-to-End- und vier KDF-Messungen |
| CryptoWorkBench.sln, Release | erfolgreich |
| CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln, Release | erfolgreich |
| Windows-x64 Desktop-Publish, framework-dependent | erfolgreich |
| Validate-DesktopPublish.ps1 | Apphost/Dependencies, drei native Assets, 126 InfoDocs geprüft |
| git diff --check | erfolgreich |

Das Core-TRX enthält 7552 entdeckte Fälle und fünf `NotExecuted`-Ergebnisse: die vier
bedingten Skips plus den absichtlich `[Explicit]` markierten Benchmark. Die VSTest-
Konsolensumme zählt diesen expliziten Fall nicht mit (7551 = 7547 + 4). Der Benchmark
wurde separat erfolgreich ausgeführt und wird nicht als regulärer CI-Test gezählt.
Die vier bedingten Skips sind `MacOsInheritedExtendedAclIsRemovedBeforePublication`,
`NativePublicationDoesNotFollowLateSymlink` und beide Varianten von
`TargetSymlinksIncludingDanglingLinksAreRejected`; ihre lokalen Plattform-/Berechtigungs-
voraussetzungen waren nicht erfüllt.

Reproduktionsbefehle (gezielte Filter in den jeweiligen TRX-Läufen):

```text
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj -c Release --no-restore
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release --no-build
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj -c Release --no-restore --filter FullyQualifiedName~SecureSavePerformanceTests.MeasureLocalHardware
dotnet build CryptoWorkBench.sln -c Release --no-restore -m:1 -nr:false
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Release --no-restore -m:1 -nr:false
dotnet publish CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj -c Release -r win-x64 --self-contained false -p:UseAppHost=true -m:1 -nr:false -o artifacts/S05-B2-publish/win-x64
```

TRX-Protokolle: `CryptoScriptUnitTest/TestResults/S05-B2-targeted-final.trx`,
`S05-B2-core-full-final.trx`, `S05-B2-performance.trx`; GUI entsprechend unter
`CryptoWorkBenchGuiTest/TestResults/S05-B2-gui-{targeted,full-final}.trx`.
Build-/Publish-Logs liegen unter ignorierten `artifacts/S05-B2-*.log`.

Zwischenläufe: Zwei sandboxierte Core-Gesamtläufe erreichten den Testhost nicht
(90-/180-Sekunden-Verbindungstimeout, kein Testergebnis). Die vollständigen Läufe
ausserhalb der Sandbox funktionierten. Der erste erreichte Gesamtlauf meldete zwei
veraltete Erwartungswerte: Kataloganzahl und Diagnose-Enum. Diese wurden um SecureSave
beziehungsweise Wert 17 ergänzt; der abschliessende Gesamtlauf besteht. Ein paralleler
Solution-Build endete wie bereits bei B1 ohne Compilerdiagnose mit Exitcode 1;
serielle Builds mit `-m:1 -nr:false` bestehen. Der erste RID-Publish ohne Restore meldete
NETSDK1047; der anschliessende RID-Restore und Publish waren erfolgreich.

Warnungen: Kompilierende Zwischenläufe meldeten sechs bestehende Core- und acht bestehende
GUI-Nullable-Warnungen in unveränderten Dateien. Der RID-Restore konnte in der Sandbox
NuGets Vulnerability-Service nicht erreichen (NU1900). Die finalen inkrementellen Builds
melden deshalb eine beziehungsweise drei NU1900-Warnungen, jeweils null Fehler; der
Publish ebenfalls drei NU1900. Dies bestätigt keine aktuelle NuGet-Vulnerability-Prüfung.
Keine neuen Compilerwarnungen in B2-Quelldateien. Git kann bestehende LF/CRLF-Konvertierungs-
hinweise für generierte Dateien ausgeben; `diff --check` meldet keine Whitespacefehler.

## Performance und Speicher

Tatsächlicher Host: Intel Core i7-14700F, 20 physische Kerne, 28 logische Prozessoren,
16949420032 Bytes RAM (ca. 15,8 GiB), Windows 11 Home 10.0.26200, .NET SDK 10.0.401 x64.
Release-Build; synthetischer ASCII-Wert mit genau 1 KiB, 1 MiB oder 16 MiB vollständigem
S01-Payload. Ein Lauf je Kombination, keine statistische Benchmarkserie. End-to-End umfasst
Prepare/S01, sofort beantworteten Testhost, KDF, Envelope, Flush und Publish auf lokalem
Temp-Speicher; keine menschliche Dialogzeit. Der erste Lauf enthält Kaltstart/JIT-Effekte.

| Modus | KDF | Payloadbytes | Dateibytes | Laufzeit ms | Allokierte Bytes | Beobachtetes Peak-Working-Set Bytes |
|---|---|---:|---:|---:|---:|---:|
| GCM | Argon2id | 1024 | 1764 | 647,04 | 74450128 | 162828288 |
| GCM | Argon2id | 1048576 | 1398500 | 327,02 | 96080968 | 182194176 |
| GCM | Argon2id | 16777216 | 22370020 | 466,80 | 437944888 | 351158272 |
| GMAC | Argon2id | 1024 | 1643 | 225,46 | 74326888 | 303128576 |
| GMAC | Argon2id | 1048576 | 1049195 | 247,03 | 90058088 | 281403392 |
| GMAC | Argon2id | 16777216 | 16777835 | 566,59 | 359639264 | 364888064 |
| GCM | PBKDF2 | 1024 | 1732 | 68,83 | 3277080 | 191533056 |
| GCM | PBKDF2 | 1048576 | 1398468 | 85,72 | 23896648 | 160641024 |
| GCM | PBKDF2 | 16777216 | 22369988 | 361,69 | 366856504 | 354893824 |
| GMAC | PBKDF2 | 1024 | 1611 | 71,36 | 3276328 | 290897920 |
| GMAC | PBKDF2 | 1048576 | 1049163 | 86,51 | 19014136 | 248205312 |
| GMAC | PBKDF2 | 16777216 | 16777803 | 279,17 | 288543008 | 329367552 |

Argon2id verwendet hier 65536 KiB/3/4, PBKDF2 600000 Iterationen. Working Set wurde
während jedes Laufs ungefähr alle 5 ms am gesamten Testprozess abgelesen; kurze Peaks
können fehlen. Die Werte enthalten Runtime, Testhost und verbleibende Prozessspeicher-
belegung früherer Fälle, sind weder Delta noch reine KDF-Speicherkosten. Allokationen
stammen aus `GC.GetTotalAllocatedBytes`, einschliesslich Sampler/Testhost-Aktivität.

Zusätzliche reine KDF-Messungen (ohne Dateiverarbeitung):

| KDF / Kosten | Laufzeit ms | Kumulatives Prozess-Peak-Working-Set Bytes |
|---|---:|---:|
| Argon2id, 65536 KiB, 3 Iterationen, 4 Lanes | 264,89 | 370950144 |
| Argon2id, 262144 KiB, 3 Iterationen, 4 Lanes | 1347,04 | 423133184 |
| PBKDF2-HMAC-SHA256, 600000 Iterationen | 60,84 | 423133184 |
| PBKDF2-HMAC-SHA256, 2000000 Iterationen | 205,98 | 423133184 |

Die kumulative Peak-Zahl stammt aus `Process.PeakWorkingSet64`; identische Werte bei
späteren PBKDF2-Fällen bedeuten keinen identischen KDF-Speicherbedarf. Die PBKDF2-Zeit
liegt auf diesem Host unter der illustrativen S05-A-Zielspanne; der Maximal-Argon-Fall
darüber. Sicherheitsparameter wurden nicht angepasst. Keine Timing-Assertions in CI.
Rohdaten: `CryptoScriptUnitTest/bin/Release/net10.0/S05-B2-performance.csv`.

## Verbleibende, akzeptierte Grenzen

Eigene Passwortbytes werden direkt nach KDF gelöscht, Master-/Mode-Key und eigene
temporäre Klartext-/AAD-/Envelope-Bytepuffer in finally/Dispose. Die festen Buffer wachsen
nicht und erzeugen deshalb keine unbereinigten Resize-Kopien. Unveränderliche GUI-/S01-
Strings, Serializer-DOMs, GC-/JIT-Kopien und interne BouncyCastle-Working-Memory bleiben
dem genehmigten Restrisiko unterworfen. Die Grössenlimits sind keine RSS-Grenzen: der
vertrauenswürdige lokale S01-Serializer kann vor der abschliessenden Payloadprüfung
bereits grössere Strings/Objektgraphen erzeugen. Laufende KDF-Aufrufe lassen sich nicht
hart unterbrechen; Cancellation wird unmittelbar danach vor Verschlüsselung/Publish geprüft.

GCM veröffentlicht nur die genehmigten Metadaten; GMAC veröffentlicht bewusst den gesamten
Klartext. KCV/Identifier/Typen bleiben öffentliche Information. Keine Garantie für schwache
Passwörter oder kompromittierte Hosts. Verzeichnis/Ancestors müssen vertrauenswürdig sein;
Parent-Symlink-Austauschrennen und nicht löschbare temporäre Dateien im Fehlerfall bleiben
die dokumentierten S02-Dateisystemgrenzen. Der lokale Publish-Test startet die GUI nicht.

## Vollständige B2-Dateiliste

Die folgende Liste umfasst geänderte und neue Dateien; generierte ANTLR-Dateien sind
absichtlich enthalten. Ignorierte Build-/Analyseartefakte und das vorbestehende untracked
`.codex/` gehören nicht dazu.

45 Dateien:

- `CryptoScript/Antlr/CryptoLexer.g4`
- `CryptoScript/Antlr/CryptoScript.g4`
- `CryptoScript/Antlr/CryptoScript.interp`
- `CryptoScript/Antlr/CryptoScript.tokens`
- `CryptoScript/Antlr/CryptoScriptBaseListener.cs`
- `CryptoScript/Antlr/CryptoScriptBaseVisitor.cs`
- `CryptoScript/Antlr/CryptoScriptLexer.cs`
- `CryptoScript/Antlr/CryptoScriptLexer.interp`
- `CryptoScript/Antlr/CryptoScriptLexer.tokens`
- `CryptoScript/Antlr/CryptoScriptListener.cs`
- `CryptoScript/Antlr/CryptoScriptParser.cs`
- `CryptoScript/Antlr/CryptoScriptVisitor.cs`
- `CryptoScript/CryptoScript.csproj`
- `CryptoScript/Documentation/InfoDocumentCatalog.cs`
- `CryptoScript/ErrorListner/SemanticDiagnosticCode.cs`
- `CryptoScript/InfoDocs/Info.Func.SecureSave.md`
- `CryptoScript/InfoDocs/Info.Functions.md`
- `CryptoScript/Model/AntlrToFunctionCallExpression.cs`
- `CryptoScript/Model/Ast/FunctionCallExpressionNode.cs`
- `CryptoScript/Model/CryptoScriptRunner.cs`
- `CryptoScript/Model/FunctionCallEvaluator.cs`
- `CryptoScript/Model/PasswordProtocol.cs`
- `CryptoScript/Model/VariableDeclarationEvaluator.cs`
- `CryptoScript/Operations/OperationFactory.cs`
- `CryptoScript/Operations/SecureSaveOperations.cs`
- `CryptoScript/Serialization/SecureBuffer.cs`
- `CryptoScript/Serialization/SecureContainerCryptography.cs`
- `CryptoScript/Serialization/SecureManifest.cs`
- `CryptoScript/Serialization/SecureSaveOptions.cs`
- `CryptoScript/Serialization/SnapshotByteFilePublisher.cs`
- `CryptoScript/Serialization/VariableSnapshotFileStore.cs`
- `CryptoScriptUnitTest/CryptoScriptUnitTest.csproj`
- `CryptoScriptUnitTest/Fixtures/SecureSave/generate_vectors.py`
- `CryptoScriptUnitTest/Fixtures/SecureSave/nist-gcm256.json`
- `CryptoScriptUnitTest/Fixtures/SecureSave/vectors.json`
- `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs`
- `CryptoScriptUnitTest/SecureContainerTestReader.cs`
- `CryptoScriptUnitTest/SecureContainerVectorTests.cs`
- `CryptoScriptUnitTest/SecureManifestKeyTests.cs`
- `CryptoScriptUnitTest/SecureSavePerformanceTests.cs`
- `CryptoScriptUnitTest/SecureSaveSnapshotTests.cs`
- `CryptoScriptUnitTest/SecureSaveTests.cs`
- `CryptoScriptUnitTest/SemanticDiagnosticSinkTests.cs`
- `CryptoWorkBenchGuiTest/AsyncPasswordGuiTests.cs`
- `docs/architecture/S05-B2-validation.md`
