# S06 – SecureLoad: Implementierung und lokale Validierung

Stand: 2026-10-21. Repository `tkbss/CryptoWorkBench`, Branch `master`, Baseline
`e8ec9db8b3f604bc599130c83808b2d007445e64`.
Grundlagen: S05-A-Containerdesign und Architekturreview unter `artifacts/`, freigegebene
Entscheidungen D1–D7 sowie die B1-/B2-Validierungsberichte.

## Ergebnis

`SecureLoad(path[, selections][, #ONCONFLICT:REJECT|REPLACE])` ist produktiv über
`CryptoScriptRunner.ExecuteAsync` verfügbar. Weglassen und ALL importieren alle Roots;
Selektoren sind ordinal und müssen im Live-Dictionary nicht existieren. REJECT ist Default.
Void-/Initializer-/Nested-Call-Prüfung erfolgt ohne Argumentauswertung; `Execute` meldet
`AsyncExecutionRequired` vor IO und Passwortanforderung.

Die vorhandene kontextuelle Load-Grammatik und ihr AST-Mapping werden für SecureLoad
wiederverwendet. Der Lexer erkennt den Namen bereits als FN. Keine neuen Tokens oder
PARAM-Einträge sind erforderlich. ANTLR wurde mit 4.13.2 neu generiert; nur die beiden
Prädikatstellen im generierten Parser ändern sich. Die übrigen generierten Artefakte
sind inhaltlich unverändert.

Der Reader verwendet die S02-Pfad-/Targetprüfung, löst den absoluten Pfad einmal auf und
liest einen einzigen geöffneten Handle vollständig vor dem Dialog. PATH- oder CWD-Mutationen
und Dateiersetzung während der Passwortantwort beeinflussen die vorbereiteten Bytes nicht.
Lesesegmente, Dateibytes, dekodierte Payload und eigene Passwort-/Schlüsselpuffer werden gelöscht.

Die strukturelle Prüfung erzwingt exakte Properties einschliesslich Duplikatverbot,
Format/Version/Algorithmen, Integer-Lexeme, striktes UTF-8, kanonisches Base64, feste
Salt-/Nonce-/Tag-Längen, Modus-Encoding, Manifeststruktur sowie die bestehenden KDF-Limits.
Grenzen: Datei 96 MiB, Payload 16 MiB, Manifest wire/frame jeweils 1 MiB, 4096 Roots,
Envelope-Tiefe 8. Ein begrenzter Token-Vorlauf (100000 Tokens, ausreichend für 4096 vollständige
KEY-Einträge) und Manifest-/Stringgrenzen begrenzen die nachfolgende DOM-Metadatenallokation.
Äussere Strings bleiben zusätzlich durch die feste Dateigrenze beschränkt.

Das runner-lokale B1-Passwortprotokoll verwendet `PasswordPurpose.SecureLoad` ohne Bestätigung.
KDF und Authentifizierung laufen auf einem Worker. Argon2id/PBKDF2, HKDF-info und H/M-Framing
werden direkt mit SecureSave geteilt. GCM gibt erst nach erfolgreicher Tagprüfung Klartext frei;
GMAC prüft GCM mit leerem Ciphertext und `H || B(P)` als AAD. Falsches Passwort und Tagfehler
liefern dieselbe feste Kategorie `SecureAuthenticationFailed`, ohne ursprüngliche Exception
oder InnerException. Kein Passwort landet in Invocation.Values oder Script-/Diagnosetext.

Erst danach erfolgen strikte UTF-8-Dekodierung, vollständige S01-Rekonstruktion und exakter
ordinaler Manifestvergleich aller Felder und ihrer Reihenfolge. Unselektierte beschädigte
Roots oder Dependencies verhindern den gesamten Import. Snapshot-Quelltexte werden nicht
ausgeführt. Die Auswahl folgt der bisherigen S04-Load-Semantik; der gemeinsame Importbaustein
erhält Objektidentitäten und den Referenzschutz für unveränderte PARAMs. Eingebettete TR31H
werden nicht zu zusätzlichen Roots. `PublishBatch` erhält einen optionalen Token und prüft
ihn nach seiner letzten Dictionary-Allokation unmittelbar vor dem Referenztausch. Bestehende
Load-Aufrufer verwenden den unveränderten Default. Nach Commit wird Cancellation nicht
rückwirkend geprüft.

## Lokale Validierung

Windows x64, .NET SDK 10.0.401, Release. Ergebnisse stammen aus den lokalen Logs/TRX unter
`artifacts/`; diese Dateien und Publish-Ausgaben werden nicht aufgenommen.

| Prüfung | Ergebnis | Nachweis |
|---|---|---|
| Gezielte SecureLoad-Tests | 88 bestanden, 0 Fehler, 0 Skips | `S06-targeted-final.log`, `test-results/S06-targeted.trx` |
| Snapshot-/Save-/Load-/SecureSave-/Container-/Manifest-Regressionen | 468 bestanden, 4 plattformbedingte Skips | `S06-regressions.log`, `test-results/S06-regressions.trx` |
| Vollständige Core-Suite | 7635 bestanden, 0 Fehler, 4 plattformbedingte Skips, regulär 7639 | `S06-core.log`, `test-results/S06-core.trx` |
| Gezielte Async-/Passwort-GUI-Tests | 13 bestanden, 0 Fehler/Skips | `S06-gui-focused-final.log`, `test-results/S06-gui-focused.trx` |
| Vollständige GUI-Suite | 239 bestanden, 0 Fehler/Skips | `S06-gui.log`, `test-results/S06-gui.trx` |
| `CryptoWorkBench.sln`, Release | erfolgreich | `S06-build-main.log` |
| Avalonia-Solution, Release | erfolgreich | `S06-build-avalonia.log` |
| Desktop-Publish `win-x64`, framework-dependent | erfolgreich | `S06-publish.log` |
| Publish-Layout, native Assets und InfoDocs | Apphost/Managed-Abhängigkeiten, 3 native Assets, 127 InfoDocs bestätigt | `S06-publish-validation.log` |
| `git diff --check` | erfolgreich | abschliessende lokale Prüfung |

Die vier plattformbedingten Skips sind `MacOsInheritedExtendedAclIsRemovedBeforePublication`,
`NativePublicationDoesNotFollowLateSymlink` und beide Fälle von
`TargetSymlinksIncludingDanglingLinksAreRejected`: macOS-spezifische ACL-Prüfung bzw. lokal
fehlendes Windows-Symlink-Privileg. Der explizite S05-B2-Benchmark `MeasureLocalHardware`
wurde nicht angefordert/ausgeführt; sein separater Explicit-Status zählt nicht zu diesen
vier regulären Skips.

Die vier unveränderten Python-/OpenSSL-Containervektoren werden über den öffentlichen
SecureLoad-Aufruf gelesen. Unicode-/NUL-Passwort und Unicode-Payload sind enthalten.
Zusätzlich werden genaue authentifizierte Payloadbytes bei abweichender innerer Whitespace,
alle vier produktiven Save/Load-Kombinationen, alle sechs Typen, vollständige Metadaten,
gemeinsame/eingebettete Headeridentität, REJECT/REPLACE, PATH-Konflikte und case-sensitive
Selektoren geprüft. Bestehende Argon2id-/PBKDF2-/HKDF-/GCM-/GMAC-KATs laufen unverändert mit.

Negative Tests decken GCM- und GMAC-Manipulationen, falsche Passwörter, Salt/Nonce/Tag,
Manifest/KDF-Manipulationen, unbekannte/fehlende/doppelte Properties auch in Unterobjekten,
UTF-8/Surrogates/BOM/Base64, Versionen, Integer-Lexeme, PBKDF2- und acht Argon2id-Grenzfälle,
Datei-/Payload-/Manifest-/Root-/Tiefenlimits, authentifiziert inkonsistente Snapshots,
unselektierte beschädigte Roots und tatsächlich fehlende TR31H-Dependencies ab.
Cancellation vor Veröffentlichung, nach finaler Allokation und nach erfolgreichem Commit,
Benutzerabbruch, Hostfehler, falsche Response-ID/Bestätigungsform und späte Antworten sind
abgedeckt. Fehlerfälle prüfen unveränderte Live-Objekte.

Die drei produktiven SecureLoad-Headless-Scriptbookfälle prüfen Erfolg, falsches Passwort
und Benutzerabbruch mit echtem Dialog ohne Bestätigung, UI-Heartbeat, Variablenansicht,
Status und Diagnoseliste sowie Abbruch nachfolgender Zeilen. `Info(SecureLoad)` wird im
erfolgreichen Scriptbook über den bestehenden Katalog ausgeführt.

## Unabhängige Review und behobene Findings

Der unabhängige Agent `s06_security_review` hat Architektur und Sicherheit read-only geprüft,
anschliessend die Korrekturen und ergänzten Tests erneut geprüft. Abschluss: keine offenen
Findings. Testausführung und obige Ergebnisse wurden vom Hauptagenten erhoben.

- P2: Der erste Reader verwendete einen wachsenden MemoryStream. Frühere Arrays konnten
  GMAC-Klartext behalten. Ersetzt durch begrenzte explizite Segmente; alle werden gelöscht.
- Die Cancellation-Grenze wurde bis unmittelbar nach der letzten `PublishBatch`-Allokation
  geführt und separat getestet.
- Review-Testlücken geschlossen: produktive GMAC-Manipulation, Argon2id-Kosten-/Kombinations-
  limits und defekte echte PARAM→TR31H-Dependency bei nicht selektiertem PARAM.

Während der Entwicklung wurden Testannahmen korrigiert: Das Bestätigungsfeld ist bei Load
nicht Teil des Dialogbaums; der GUI-Status meldet allgemein einen semantischen Fehler,
während die konkrete sichere Kategorie in der Diagnoseliste steht. Die Katalogzählung
wurde um SecureLoad erhöht. Der erste Sandbox-Testhost konnte sich nicht verbinden;
die erfolgreichen Testläufe erfolgten ausserhalb der Sandbox. Ein nach einer fehlschlagenden
GUI-Assertion offener Testhost wurde gezielt beendet; der Test wartet nun auch beim Cleanup
auf den Ausführungsabschluss. Vorübergehende MSB3026/MSB3027-Dateisperren sind im abschliessenden
Lauf nicht vorhanden.

## Warnungen und Restrisiken

Es wurden sechs bestehende Core-Nullable-Warnungen CS8603 sowie acht bestehende GUI-
Nullable-Warnungen CS8618/CS8625/CS8602 beobachtet. Keine neue Nullable-Warnstelle in S06.
Inkrementelle Abschlussbuilds wiederholen diese nicht zwingend. NU1900 tritt lokal auf:
die NuGet-Auditquelle `https://api.nuget.org/v3/index.json` war nicht erreichbar. Das ist
kein Nachweis, dass Abhängigkeiten frei von Schwachstellen sind. Keine NuGet-Pakete geändert.

S02s dokumentierte Target-check/open-Race und Vertrauen in Verzeichnis/Ancestors bleiben.
Es gibt keine zusätzliche TOCTOU-/Sandbox-Garantie. BouncyCastle-Arbeitsspeicher sowie
Managed-Strings in JSON/S01, GUI und importierten Objekten können nicht zuverlässig gelöscht
werden; eigene mutable Byte-/Passwortpuffer werden bereinigt. GMAC bietet keine Vertraulichkeit,
das öffentliche Manifest ist vor Authentifizierung untrusted, gültige alte Container können
wieder eingespielt werden. Maximale zugelassene KDF-Kosten bleiben bewusst ressourcenintensiv.
Keine Änderungen am v1-Wireformat oder S01-/S02-Vertrag, kein Inspect, keine neue Kryptografie.

Publish-Layoutvalidierung startet die grafische Anwendung nicht. Linux/macOS wurden in diesem
lokalen Auftrag nicht ausgeführt; CI ist ohne Commit/Push nicht ausgelöst worden.

## Vollständige S06-Dateiliste

21 Pfade: 14 bestehende Dateien geändert, 7 neue Dateien.

```text
CryptoScript/Antlr/CryptoScript.g4
CryptoScript/Antlr/CryptoScriptParser.cs
CryptoScript/CryptoScript.csproj
CryptoScript/Documentation/InfoDocumentCatalog.cs
CryptoScript/InfoDocs/Info.Func.SecureLoad.md
CryptoScript/InfoDocs/Info.Functions.md
CryptoScript/Model/CryptoScriptRunner.cs
CryptoScript/Model/FunctionCallEvaluator.cs
CryptoScript/Operations/OperationFactory.cs
CryptoScript/Operations/SecureLoadOperations.cs
CryptoScript/Operations/SecureSaveOperations.cs
CryptoScript/Operations/SnapshotOperations.cs
CryptoScript/Serialization/SecureContainerCryptography.cs
CryptoScript/Serialization/SecureEnvelope.cs
CryptoScript/Serialization/SecureSnapshotByteReader.cs
CryptoScript/Variables/VariableDictionary.cs
CryptoScriptUnitTest/InfoDocumentCatalogTests.cs
CryptoScriptUnitTest/SecureLoadSecurityTests.cs
CryptoScriptUnitTest/SecureLoadTests.cs
CryptoWorkBenchGuiTest/AsyncPasswordGuiTests.cs
docs/architecture/S06-validation.md
```

Kein Staging, Commit oder Push. HEAD bleibt auf der Baseline, lokales `origin/master`
ebenfalls; Ahead/Behind 0/0, Index leer. Das bestehende ungetrackte `.codex/` bleibt
ausgeschlossen. `.codex/config.toml` wurde weder gelesen noch verändert.
