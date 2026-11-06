# F02 – Implementierungs- und Reviewbericht

Stand: 23. Oktober 2026. Bestehender Worktree `CryptoWorkBench`, Branch `master`,
HEAD und Baseline `ca77a94c24721398a194f38b26572d2079fa181e`.
Vor Arbeitsbeginn war nur `.codex/` unversioniert. `.codex/config.toml` wurde
weder gelesen noch verändert. Keine Commits, kein Push, Merge oder Rebase.

**Empfehlung: NOT READY.** Implementierung, Windows-x64-Builds und lokale Tests
sind erfolgreich; der unabhängige Review hat keine bestätigten Findings.
Für eine Freigabe über alle angeforderten Plattformen fehlen tatsächliche
F02-Läufe unter Linux x64, macOS x64 und macOS ARM64 sowie die unten genannten
Windows-Symlinkprüfungen. Diese Aussage ist keine Behauptung eines bekannten
Implementierungsfehlers.

## Architektur und Verhalten

`Decrypt(PARAM, KEY, PATH) -> PATH` wird in `CryptoOperations` ausschliesslich
anhand der tatsächlichen `PathVariableDeclaration` des dritten `SourceVariable`
ausgewählt. VAR und Literale laufen weiter durch die unveränderte
`algorithm.Decrypt`-Implementierung. Die Dateiendung entscheidet nicht über den
Dispatch. Es gibt keine Parseränderungen und keine neue Avalonia-Abhängigkeit.

Der bestehende frühe `FileEncryptionContract` erfasst jetzt auch Decrypt.
Falsche Ergebnisdeklarationen, verschachtelte Dateiaufrufe und verschachtelte
Argumente werden vor Auswertung mit Seiteneffekten abgelehnt. Der bestehende
Runner registriert den Ergebnis-PATH erst nach erfolgreicher Rückkehr des
Coordinators. Diagnose- und Key-Metadatenwarnungen verwenden die vorhandenen Wege.

`FileDecryptCoordinator` öffnet die Quelle über F01 `FilePublication` und lässt
`EncryptedFileCodec` den unveränderten 1.0-Container vollständig validieren:
Magic, Version, Headergrenze 4096 Bytes, UTF-8/JSON, Felder, Mechanismus, Padding,
IV, Binärcodierung und fehlender Authentifizierungstag. Ciphertext muss mindestens
16 Bytes enthalten und durch 16 teilbar sein.

Der Container liefert IV und Padding. PARAM muss AES-CBC auswählen; nur in
`ExplicitParameterNames` verzeichnete IV-/Paddingwerte müssen übereinstimmen.
IV wird byteweise verglichen, PAD ohne Beachtung der Gross-/Kleinschreibung.
Automatische/default Parameter erzeugen keinen künstlichen Konflikt.
Für die Paddingauswahl wird ein privates Parameterobjekt erstellt; Werte,
Dictionary und Provenienz des übergebenen PARAM bleiben erhalten.

`AES_CBC.DecryptStream` verwendet `CreateCipher` und die vorhandene
Paddingauswahl. AES-128/192/256, PKCS-7 einschliesslich Leerdateien und NONE werden
unterstützt. Ein 64-KiB-Eingabepuffer und ein 64-KiB-plus-Block-Ausgabepuffer
begrenzen den Speicher. Kurze Reads werden bis zur Blockgrenze gesammelt.
Der Transform hält gegebenenfalls den letzten PKCS-7-Block bis zur abschliessenden
Paddingprüfung zurück. Die anfängliche Eingabelänge wird einmal erfasst;
vorzeitiges EOF sowie beobachtetes Wachstum/Schrumpfen führen zum Fehler.

Cancellation wird zwischen Reads/Chunks, vor dem Abschluss und vor jedem
Publikationsversuch geprüft. Kein nachträglicher Cancellation-Check entfernt
eine bereits erfolgreich veröffentlichte Datei. Eingabe-, Klartext- und finale
Transformbuffer werden im `finally` gelöscht; kontrollierte dekodierte
Schlüsselbytes werden durch die bestehende Cipher-Factory gelöscht.
Cipher, Transform und Streams werden entsorgt.

## Dateisicherheit und Namen

F01-Dateizugriff und Publikation werden unverändert wiederverwendet:
handlegebundene Verzeichnisse, Ablehnung von Symlinks/Reparse-Points, reguläre
Quelldateien, private temporäre Dateien, Flush vor Publikation, atomare
No-Replace-Operationen, keine Copy-Fallbacks. Die temporäre Datei bleibt bis zur
Publikation geöffnet. Fehler/Cancellation entfernen sie im `finally`, soweit
das Dateisystem die Bereinigung erlaubt. Der Originalcontainer wird nicht geändert.

Die letzte Endung `.cwe` wird entfernt: `message.bin.cwe` wird `message.bin`,
`message.bin.1.cwe` wird `message.bin.1`. Kollisionen verwenden die erste freie
Erweiterung `.1`, `.2` usw.; Dateien, Verzeichnisse und Links werden nicht ersetzt.
Die F01-Grenze von 1.000.000 Kandidaten und deren atomare Kollisionsbehandlung
bleiben erhalten. Umbenannte Container ohne entfernbare `.cwe`-Endung erhalten
als Basis `<vollständiger Name>.decrypted`. Dieses Verhalten ist dokumentiert.

## Ausgeführte Validierung

Host: Windows x64, .NET SDK 10.0.401, Runtime 10.0.12. Release-Konfiguration.
Die letzten funktionalen Änderungen wurden vor den vollständigen Suites gebaut.
Danach wurden nur ein Kommentar, ein ungenutztes `using` und dieser Bericht bereinigt.

| Prüfung | Ergebnis |
|---|---|
| `dotnet build CryptoWorkBench.sln --configuration Release --no-restore -m:1` | Erfolgreich |
| `dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --configuration Release --no-restore -m:1` | Erfolgreich |
| F02- und Dokumentationstests | 141 bestanden, 2 übersprungen, 0 Fehler |
| Vollständige Core-Suite | 7.794 bestanden, 14 übersprungen laut Testlauf-Zusammenfassung, 0 Fehler |
| Vollständige GUI-Suite | 242 bestanden, 0 übersprungen, 0 Fehler |
| `git diff --check` | Erfolgreich |

Die 83 neuen F02-Testfälle enthalten 81 erfolgreiche und 2 wegen fehlender
Windows-Symlinkberechtigung übersprungene Fälle. Abgedeckt sind Roundtrips für
alle AES-Schlüssellängen, PKCS-7/NONE, Leerdateien/Blockgrenzen/Binärdaten,
der bestehende unabhängige F01/NIST-Referenzcontainer, automatisch erzeugte Werte,
explizite Konflikte, falsche Mechanismen, Header-/Versions-/UTF-8-/JSON-Fehler,
Trunkierung, Ciphertextlängen, ungültiges Padding und ein ausgewählter falscher
Schlüssel. Ein gesonderter NONE-Test demonstriert die fehlende Schlüsselerkennung.

Weitere Tests prüfen Namenslücken, konkurrierende Veröffentlichungen mit Barrier,
vorhandene Dateien/Verzeichnisse, Cancellation, fehlende PATH-Registrierung,
Cleanup, fehlende/gesperrte/nichtreguläre Quellen, private Windows-DACLs,
Temp-Austausch, Verzeichnisverschiebung, kurze Reads, beobachtete Längenänderungen,
Schreibfehler und Bufferbereinigung. Bestehende VAR-Decrypt-Regressionen laufen
in der vollständigen Core-Suite; zusätzlich ist ein bekannter VAR-Vektor enthalten.

Ein tatsächlicher 64-MiB-Dateiroundtrip vergleicht SHA-256 über Streams und misst
die verwalteten Allokationen des kompletten Decrypt-Coordinators:
**216.088 Bytes**, unter dem Testlimit von 2 MiB. Das ist eine Messung verwalteter
Allokationen, keine Messung des gesamten Prozess-RSS oder der Betriebssystemcaches.

Nachweise im lokalen, Git-ignorierten Verzeichnis `artifacts/f02/`:

- `F02-focused.trx`, `F02-core.trx`, `F02-gui.trx`
- `build-main.log`, `build-avalonia.log`
- `test-core.log`, `test-gui.log`: die zuvor abgebrochenen Sandbox-Startversuche,
  nicht die erfolgreichen vollständigen Läufe; deren Nachweise sind die TRX-Dateien.

Die Core-TRX enthält zusätzlich den expliziten Benchmark `MeasureLocalHardware`
als `NotExecuted`; deshalb sind dort 15 solche Ergebniszeilen sichtbar, während
die Testlauf-Zusammenfassung 14 übersprungene Tests meldet. Die übrigen Ausnahmen
sind sieben Darwin-ABI-Tests, ein macOS-ACL-Test und sechs Windows-Symlinkfälle
(davon zwei neue F02-Fälle).

Ein erster fokussierter Lauf fand den fehlenden Kopiereintrag der Decrypt-Hilfeseite;
dieser wurde im Core-Projekt ergänzt und danach erfolgreich getestet.
Ein paralleler Solution-Build in der Sandbox endete ohne Compilerdiagnose;
beide Builds mit `-m:1` waren erfolgreich. Die sandboxierten vollständigen
Testhost-Starts brachen nach 90 Sekunden beim Verbindungsaufbau ab.
Die erfolgreichen vollständigen Suites liefen anschliessend ausserhalb der
Sandbox. Es gab keine automatische Ablehnung dieser Testausführung.
Bei initialen Neukompilierungen gemeldete Nullable-Warnungen betreffen
unveränderte Core-/GUI-Stellen; die abschliessenden inkrementellen Builds
meldeten jeweils 0 Warnungen und 0 Fehler.

## Plattformverträge und verbleibende Grenzen

| Plattform | Statisch geprüfter bestehender Vertrag | Tatsächlich ausgeführt |
|---|---|---|
| Windows x64 | Reparse-Prüfung, Verzeichnis-Leases, Quell-Sharing, private DACL, Handle-Rename ohne Ersetzen | Builds, vollständige Suites und F02-Dateifälle; Symlinkfälle mangels Berechtigung nicht ausgeführt |
| Linux x64 | `openat`/`O_NOFOLLOW`, reguläre Datei via `fstat`, Modus 0600, Descriptor-Link über procfs, atomare Kollision | Nicht ausgeführt |
| macOS x64 | Compilerbasierter nativer Wrapper, `INODE64`, Modus 0600 und ACL-Abbau, Identitätsprüfung vor `linkat`, `F_GETPATH` | Nicht ausgeführt |
| macOS ARM64 | Derselbe feste C-Wrapper für variadische native APIs, native ARM64-stat-ABI, private Publikation | Nicht ausgeführt |

Der vorhandene CI-Workflow enthält alle vier Plattformen und führt die gesamten
Core-/GUI-Suites aus; damit werden die neuen F02-Tests dort automatisch erfasst.
Es wurde kein Push/PR/CI-Lauf ausgelöst. Statische Prüfung und vorhandene
F01-Verträge ersetzen keine tatsächlichen F02-Läufe auf diesen Plattformen.

AES-CBC bietet keine Integritätsprüfung: Header und Ciphertext sind ungeschützt.
PKCS-7 kann manche falschen Schlüssel/Manipulationen erkennen, aber kein
erfolgreiches Decrypt authentifiziert den Schlüssel oder Inhalt. NONE kann mit
falschem Schlüssel ohne Fehler unbrauchbare Bytes veröffentlichen.

Unix kann konkurrierende gleichlange Quelldateiänderungen nicht zuverlässig
erkennen. Linux benötigt procfs und Hardlinks. macOS behält ausdrücklich die
F01/S02-Vertrauensgrenze eines vertrauenswürdigen Zielverzeichnisses; die
Identitätsprüfung ist keine Garantie gegen gleichprivilegierte feindliche
Verzeichnisschreiber. Ein stabiler Namensraum bleibt während und nach Publikation
erforderlich. Temp-Löschung ist keine sichere physische Löschung; Systemcaches,
Storage und nicht kontrollierbare interne Kryptografiepuffer sind nicht abgedeckt.
Physischer Datenträgervollstand, Geräteausfall, Stromausfall sowie echte native
Flush-/Publikationsfehler wurden nicht provoziert. Getestet wurden injizierte
Schreib-/I/O-Fehler, Kollisionen und die vorhandenen Fehler-/Cleanup-Wege.

## Unabhängiger Review

Ein separater, rein lesender Review-Agent prüfte Implementierung, Tests,
Dokumentation, VAR-Verträglichkeit und die wiederverwendeten F01-Sicherheitswege.
**Keine bestätigten P1/P2/P3-Findings.** Er empfahl weitere Fehlerfalltests für
Längenänderungen/EOF, ungültige Schlüssellängen, Schreibfehler sowie Temp-Austausch
und Verzeichnisverschiebung. Diese wurden ergänzt und erfolgreich ausgeführt.

Nachreview nach der konsistenten einmaligen Längenerfassung und den zusätzlichen
Tests: **keine bestätigten Findings**. Der Reviewer hat selbst keine Tests
ausgeführt oder Dateien verändert. Die Plattform- und Dateisystemgrenzen wurden
ausdrücklich als verbleibende Validierungslücken festgehalten.

## Vollständige F02-Dateiliste

Neu:

- `CryptoScript/FileEncryption/FileDecryptCoordinator.cs`
- `CryptoScript/InfoDocs/Info.Func.Decrypt.md`
- `CryptoScriptUnitTest/FileDecryptionTests.cs`
- `docs/F02-implementation-report.md`

Geändert:

- `CryptoScript/CryptoAlgorithm/AES/AES_CBC.cs`
- `CryptoScript/CryptoScript.csproj`
- `CryptoScript/Documentation/FileInfoDocumentationProvider.cs`
- `CryptoScript/Documentation/InfoDocumentCatalog.cs`
- `CryptoScript/FileEncryption/FileEncryptionContract.cs`
- `CryptoScript/InfoDocs/Info.Func.Encrypt.md`
- `CryptoScript/InfoDocs/Info.Functions.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-CBC.md`
- `CryptoScript/Operations/CryptoOperations.cs`
- `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs`
- `CryptoScriptUnitTest/InfoDocumentationProviderTests.cs`

Git-Abschluss: 11 modifizierte versionierte Dateien und 4 neue F02-Dateien,
zusätzlich der unveränderte vorbestehende unversionierte Eintrag `.codex/`.
Alle F02-Änderungen sind ungestaged und uncommitted; Branch und HEAD bleiben
auf dem oben genannten Ausgangszustand.
