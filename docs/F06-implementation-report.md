# F06: Streaming File Hash – Implementierungsbericht

Stand: 24.10.2026. Repository `tkbss/CryptoWorkBench`, Branch `master`,
Baseline `09239c4d7c6bc48b38caf99253724e1c0855c837`.
Commit und regulärer Push wurden am 24.10.2026 vom Auftraggeber freigegeben.
Dieser Bericht dokumentiert den Stand unmittelbar vor dem einzigen F06-Commit.
Commit-SHA und tatsächliche Vier-Plattform-CI-Ergebnisse folgen im Abschlussbericht
der Unterhaltung.

## Architektur und Vertrag

`CryptoOperations.Hash(OperationInvocation)` prüft genau zwei Argumente und wählt
den Dateimodus ausschliesslich über `ResolvedCallArgument.SourceVariable` vom Typ
`PathVariableDeclaration`. Der erste Parameter muss dann eine echte PARAM-Referenz
sein. PARAM-Text in einem VAR oder Literal ist dafür unzulässig. Der vorhandene
`Hash(string[])`-Kompatibilitätspfad und die In-Memory-HASH-Engine bleiben erhalten.
Ein Dateiname als VAR oder Stringliteral wird weiterhin als Nachricht gehasht und
öffnet keine Datei. Damit bleiben auch In-Memory-Aufrufe mit Literalen kompatibel.

`FileHashContract` prüft den gesamten Aufrufbaum vor der Argumentauswertung in
FunctionCallEvaluator und VariableDeclarationEvaluator. Nur direkte Datei-Aufrufe
mit PARAM/PATH und VAR-Rückgabedeklaration sind erlaubt. Falsche Deklarationen,
verschachtelte Datei-Aufrufe und Argumentfunktionen scheitern mit dem bestehenden
`FunctionContractViolation`-Diagnosecode. Laufzeitfehler verwenden die vorhandene
Diagnosebehandlung; Abbruch propagiert als OperationCanceledException. Die
Ergebnisvariable wird erst nach erfolgreicher Berechnung registriert.

Der neue `FileHashCoordinator` validiert Mechanismus und PARAM-Optionen vor dem
Dateizugriff, erzeugt mit `DigestFactory.Create()` eine frische IDigest-Instanz und
liest in 65.536-Byte-Blöcken. Jeder gelesene Block geht unverändert an BlockUpdate.
Ein erfolgreicher Durchlauf ruft DoFinal genau einmal auf. Leere Dateien sind
zulässig. Der vollständige Digest wird als `0x(HEX)` mit Grossbuchstaben geliefert.
Der bestehende In-Memory-Pfad behält seine bisherige HEX-Schreibweise; Vergleiche
prüfen deshalb die Digestbytes bzw. ignorieren nur die Hex-Zeichen-Grossschreibung.

Der Dateipfad läuft durch `SnapshotByteFilePublisher.ValidatePath`, FilePublication
und OpenSource. F06 erzeugt keine Ausgabe- oder temporären Dateien und verändert
die Quelle nicht. Puffer und Digestbytearray werden im finally-Block genullt.
HashStream ist ein interner Test-Seam; die Produktionsöffnung bleibt unverändert
über FilePublication gesichert. Keine neue Kryptobibliothek, kein neuer Mechanismus,
keine Änderung an CWE/CWM oder anderen F01–F05-Dateiformaten, kein HashPack/HashVerify.

## Dokumentation und GUI

Neue `Info.Func.Hash.md` mit Beispiel, elf Mechanismen, Rückgabeformat, Hash/MAC-
Abgrenzung und Dateisicherheitsgrenzen. Zentrale Funktionsliste und alle elf
HASH-Mechanismusdokumente verweisen auf die Dateifunktion. Dokumentkatalog,
FileInfoDocumentationProvider, Deployment und Info-Completion unterstützen Hash.
Die GUI erhält zwei Hash-Overlays und den bislang fehlenden OperationList-Eintrag.
Navigation einschliesslich Rücksprung und deployte Hilfedateien werden getestet.

## Testmatrix

| Bereich | Abdeckung |
| --- | --- |
| Mechanismen | Alle elf registrierten HASH-Mechanismen; Registry-Abgleich |
| Unabhängige Referenzen | 99 vollständige CPython hashlib/OpenSSL-Digests, ohne Bouncy Castle |
| Längen je Mechanismus | 0, 1, 63, 64, 65, 65.535, 65.536, 65.537, 196.625 Bytes |
| Binärdaten | Byte i = i modulo 256; NUL, CR/LF, sämtliche Werte über 0x7F |
| Integration | Skriptaufruf Hash(PARAM, PATH) und Hash(PARAM, VAR) gegen dieselbe Referenz |
| Vertrag | PATH/KEY/PARAM/VAL-Rückgabe, falsche Anzahl/Reihenfolge, geschachtelte Datei-Aufrufe und Argumentfunktionen |
| Typ/Mechanismus | Echter PARAM statt Text/VAR/Literal; HMAC, AES-CMAC, unbekannter HASH; zusätzliche Optionen |
| Dateifehler | Fehlende Datei, Verzeichnis, ungültiger Pfad, Windows-Sharing-Sperre |
| Links | Quelle als Symlink sowie Symlink im Elternpfad; ohne Symlinkrecht explizit übersprungen |
| Abbruch | Vor Öffnung, Runner-Token, nach drei erfolgreichen 65.536-Byte-Reads |
| Lesefehler | Injizierte IOException nach drei Chunks; keine Zeittests |
| Längenänderung | Wachstum/Schrumpfung nach drei Chunks; vorzeitiges EOF; Änderung bei EOF und nach Finalisierung |
| Ressourcen | Kurze Reads; Nullung des Lesepuffers bei Fehler/Abbruch; Quelle/Dateiartifakte geprüft |
| Speicher | Thread-Allokationen für 1 MiB und 8 MiB, echte Datei und generierter Stream; maximale Read-Anforderung 65.536; Differenz unter 256 KiB |
| Dokumentation/GUI | Info(Hash), alle Mechanismuslinks, Katalog, Completion, Overlay, deployte Hilfe, Navigation |

Referenzen reproduzieren: `python docs/F06-reference/generate.py`.
Fixture-SHA256 auf diesem Windows-Checkout:
`21BDE1B5A865C5E5D6E4E1FDA0F2E5FD725DB32D5A31523B9E2B6BAE7C71F93D`.
Die grösste reguläre Referenzdatei ist 196.625 Bytes; der Allokationstest nutzt
maximal eine 8-MiB-Datei. Es werden keine vollständigen Quelldateien in der
Produktionsimplementierung materialisiert.

## Sicherheitsgrenzen und Plattformen

Die Länge des geöffneten Handles wird initial, vor dem Lesen, nach jedem Read,
bei EOF und nach DoFinal geprüft. Zusätzlich wird die gelesene Gesamtbytezahl
gegen die Anfangslänge geprüft. Wachstum, Schrumpfung und unvollständiges Lesen
führen zu einem Fehler, soweit sie an diesen Grenzen erkennbar sind.

FilePublication öffnet ausschliesslich reguläre Dateien und folgt keinen
Quell-Symlinks, Eltern-Symlinks oder Reparse Points. Die bestehende Traversierung
bindet Verzeichnishandles; deren Vertrauensannahmen bleiben bestehen. Windows
verweigert normale konkurrierende Schreib-/Löschöffnungen durch Handle-Sharing.
Unix erzwingt dieses Sharing nicht. Gleichlange Änderungen, zwischen Prüfungen
zurückgesetzte Änderungen, bereits vorhandene schreibbare Mappings und privilegierte
Schreiber sind nicht allgemein erkennbar. F06 bietet keinen atomaren Snapshot.
Bouncy Castle garantiert keine vollständige Löschung des internen Digestzustands;
Reset ist keine Erasure-Garantie. Die expliziten temporären Bytepuffer werden genullt.

| Plattform | Umsetzung / lokale Ausführung |
| --- | --- |
| Windows x64 | Beide Solutions und vollständige Core-/GUI-Suites lokal ausgeführt |
| Linux x64 | Vorhandene OpenSource/openat/fstat-Infrastruktur unverändert; Tests vorbereitet, hier nicht ausgeführt |
| macOS x64 | Vorhandener Darwin-Shim unverändert; kanonische /private-Testpfade; hier nicht ausgeführt |
| macOS ARM64 | Bestehender Darwin-Shim und ABI-Behandlung unverändert; hier nicht ausgeführt |

## Validierung

.NET SDK 10.0.401, Windows x64, Debug. Befehle:

```powershell
dotnet build CryptoWorkBench.sln --no-restore --verbosity quiet
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore --verbosity quiet
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-build --no-restore --logger "trx;LogFileName=F06-core-full.trx" --verbosity quiet
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-build --no-restore --logger "trx;LogFileName=F06-gui-full.trx" --verbosity quiet
```

Beide Builds erfolgreich, ohne Compilerfehler. Bestehende GUI-Nullable-Warnungen
und NU1900 (NuGet-Vulnerability-Feed nicht erreichbar) bleiben sichtbar.
Sandbox-Läufe konnten VSTest/MSBuild-Prozesskommunikation nicht herstellen; die
erfolgreichen Validierungsläufe wurden mit genehmigter Sandbox-Ausnahme ausgeführt.

| Prüfung | Ergebnis |
| --- | --- |
| Vollständige Core-Suite | 11.532 bestanden, 26 übersprungen, 0 fehlgeschlagen (11.558 gesamt) |
| Vollständige GUI-Suite | 249 bestanden, 0 übersprungen, 0 fehlgeschlagen |
| F06-Symlinkfälle | 2 der übersprungenen Core-Tests; Windows-Konto ohne Symlinkrecht |

Die TRX-Dateien liegen in den jeweiligen ignorierten TestResults-Verzeichnissen.
`git diff --check` ist erfolgreich. Keine Änderungen an nativer Dateiinfrastruktur.

## Separater Reviewdurchlauf

Zunächst manueller Selbstreview; anschliessend unabhängiger abschliessender Review
durch einen separaten Review-Agenten gegen die vollständige Baseline-Differenz,
einschliesslich aller neuen Dateien. Kein externer Audit. Geprüft wurden
Typ-Provenienz, Aufrufbaumprüfung vor Argumentauswertung, Mechanismusvalidierung
vor Öffnung, vollständige Referenzdigests, Längen-/EOF-Prüfungen, genau eine
Finalisierung, Cancellation, Pufferbereinigung, Ergebnisregistrierung und GUI-/
Dokumentationsanbindung.

| Schwere | Findings und Status |
| --- | --- |
| Critical | Keine offenen Code-Findings |
| High | Keine offenen Code-Findings |
| Medium | Gemeinsamer HEX-Formatter liefert Kleinbuchstaben: Dateipfad auf explizites Convert.ToHexString korrigiert, durch alle 99 Vektoren geprüft |
| Medium | Generischer PARAM-Validator allein weist zusätzliche HASH-Optionen nicht zurück: dateispezifische Vorprüfung ergänzt und getestet |
| Low | Hash fehlte in der GUI-OperationList: ergänzt, Overlay durch GUI-Test geprüft |
| Low | EOF-/Post-Finalisierungs-Änderungen und Puffernullung benötigen explizite Regressionen: deterministische Streamtests ergänzt |

Keine offenen Implementierungsfindings nach Korrekturen. Verbleibende
Validierungsgrenzen: Linux/macOS wurden hier nicht ausgeführt; Windows-Symlink-
Tests sind ohne erforderliches Konto-Recht übersprungen. Diese Einschränkungen
sind keine bestandenen Plattformprüfungen. Commit und Push sind freigegeben;
COMPLETE bleibt bis zum erfolgreichen Abschluss aller vier CI-Plattformjobs gesperrt.
Nach einem CI-Fehler erfolgen keine weiteren Commits ohne erneute Freigabe.

## Unabhängiger Final Review vor Commit

Der separate Reviewer fand keine Critical-, High- oder Medium-Findings und keine
blockierenden Produktionsfehler. Zwei Low-Findings wurden vor dem Commit korrigiert:

- Der generierte Stream enthielt nur Nullen; die Pufferbereinigungsassertion war
  damit tautologisch. Er liefert jetzt 0xA5. Vor Fehler/Abbruch wird der gefüllte
  Puffer geprüft, anschliessend dessen vollständige Nullung. Auch der Erfolgspfad
  wird auf Pufferbereinigung geprüft. Der Reviewer bestätigte die Korrektur.
- Eine gemischte UTF-8/Windows-1252-Kodierung der Ergebnistabelle wurde zu gültigem
  UTF-8 vereinheitlicht.

Die unveränderte In-Memory-HASH-Engine und String-Überladung wurden explizit
bestätigt. Der neue Count-Guard entspricht dem bisherigen Zwei-Argumente-Vertrag.
Die Datei- und PARAM-Provenienz, Vorabbaumprüfung, Zusatzparameter, Streaming,
Cancellation, konsumierte Bytezahl/EOF, Finalisierung und finally-Bereinigung
wurden unabhängig geprüft. Keine offenen Review-Findings nach Korrektur.

Beide Solutions wurden vor Commit auch in Release erfolgreich gebaut. Die
vollständigen Release-Suites werden in separaten F06-final-release-TRX-Dateien
protokolliert: Core 11.532 bestanden / 26 übersprungen / 0 fehlgeschlagen;
GUI 249 bestanden / 0 übersprungen / 0 fehlgeschlagen.
CI wird anhand der tatsächlichen Job-Ergebnisse ausgewertet;
lokale Zahlen werden nicht als CI-Zahlen ausgegeben.
