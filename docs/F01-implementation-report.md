# F01 – Implementierungs- und Reviewbericht

Stand: 22. Oktober 2026. Änderungen liegen ausschliesslich uncommitted im aktuellen
CryptoWorkBench-Worktree. Keine Commits, kein Push, kein Merge, kein Pull Request.
Andere Worktrees wurden nicht verändert. Das bereits untracked vorhandene
`.codex/config.toml` wurde nicht bearbeitet.

**Status: zur gemeinsamen Prüfung vorbereitet; F01 ist ohne ausdrückliche
Nutzerfreigabe nicht abgeschlossen. Native Linux-/macOS-Validierung bleibt offen.**

## Architektur und Funktionsvertrag

`Encrypt(parameters, key, data)` behält drei Argumente. Der Dispatch in
`CryptoOperations` prüft das konkrete `SourceVariable` des dritten Arguments.
Eine echte PATH-Referenz liefert eine `PathVariableDeclaration` mit `CryptoTypePath`;
VAR und bisherige Datenliterale verwenden weiterhin den bestehenden Algorithmusweg.
Keine Pfadheuristik und keine wertbasierte Suche nach gleichlautenden Variablen.

`FileEncryptionContract` läuft vor Initialisiererausführung und vor der Auswertung
verschachtelter Argumente. Eine falsche Deklaration oder ein verschachtelter Dateiaufruf
wird als `FunctionContractViolation` abgewiesen, bevor Dateien geöffnet werden.
Dateiaufrufe mit verschachtelten Argumenten sind bewusst ebenfalls ausgeschlossen;
PARAM und KEY sollen vorher deklariert werden. Ein direkt ausgeführter Dateiaufruf
ohne Deklaration darf die Datei erzeugen; bei einer Deklaration wird das Ergebnis
erst nach erfolgreichem Commit registriert.

`FileEncryptCoordinator` validiert, koordiniert und erzeugt den absoluten Ergebnis-PATH.
`EncryptedFileCodec` ist reine Framing-/Metadatenlogik. `AES_CBC.EncryptStream` ergänzt
Streaming innerhalb des bestehenden AES-CBC-Typs. `FilePublication` kapselt gebundene
Dateizugriffe und atomare Namensoperationen. Private Windows-Dateierstellung und
macOS-ACL-Entfernung werden aus der Snapshot-Infrastruktur wiederverwendet, die
Snapshot-Serialisierung bleibt getrennt. Keine Avalonia-Abhängigkeit im Kern.

Die öffentlichen CryptoAlgorithm-Rückgabetypen bleiben unverändert. Die bestehenden
OperationInvocation-Konstruktoren bleiben erhalten; ein zusätzlicher Konstruktor
transportiert CancellationToken. Der asynchrone Runner reicht ihn über den
ausführungseigenen Diagnosekontext weiter. Der synchrone Runner bleibt synchron.

## Kryptografie, IV und Container

AES-128/192/256-CBC nutzt dieselbe .NET-AES-Konfiguration wie VAR, mit gemeinsamem
Cipher-Factory-Helfer, bestehender Paddingauswahl und gemeinsamem NONE-Längenvalidator.
64 KiB Klartextpuffer begrenzen den Speicherbedarf. Klartextpuffer und temporär
dekodierte Schlüsselbytes werden gelöscht; Cipher/Transform/Streams werden disposed.
PKCS-7 akzeptiert auch leere Dateien; NONE verlangt eine nichtleere Länge teilbar durch 16.

Die AES-Parametererzeugung zeichnet explizit übergebene Parameter nun in der vorhandenen
Provenienzliste auf. Explizite IVs bleiben bytegenau erhalten. Automatische IVs werden
je Datei neu erzeugt, ohne den ursprünglichen PARAM-Zustand zu verändern. Vorhandene
KeyType-/KeyUsage-Prüfungen und Warnungssemantik laufen auch im Dateimodus; technische
Schlüssellängen und IVs werden validiert. Metadatenkonflikte bleiben entsprechend dem
bestehenden Vertrag Warnungen, sie werden nicht neu in Verbote umgedeutet.

`.cwe` enthält Magic `CWBENC\0\0`, Version 1.0, Big-Endian-Länge, maximal 4096 Bytes
strenges UTF-8-JSON und binären Ciphertext ohne Tag. Der Header enthält Mechanismus,
Padding und tatsächlich verwendeten IV; keine Schlüssel oder ursprünglichen Pfade.
Unbekannte Versionen/Felder, Duplikate und ungültige Werte werden abgelehnt. Der Codec
liefert die originalen Headerbytes für eine spätere AAD-Verwendung zurück.

Die verbindliche Spezifikation und der reproduzierbare Referenzvektor stehen in
[F01-container-format.md](F01-container-format.md) und [F01-reference.hex](F01-reference.hex).
AES-CBC schützt weder Ciphertext noch Header vor Manipulation; das Format ist ausdrücklich
unauthentifiziert. Decrypt(PATH), Mac(PATH), AEAD und zusätzliche Paddingverfahren sind
nicht implementiert.

## Dateien, Fehler und Wiederholung

Die Originaldatei bleibt unverändert. Der erste freie Kandidat ist `message.bin.cwe`,
danach `message.bin.1.cwe`, `.2.cwe` usw.; Lücken werden wiederverwendet. Vorhandene Ziele
werden niemals ersetzt. Die Suche ist auf 1.000.000 Kandidaten begrenzt. Überlange Namen
führen zum kontrollierten Dateisystemfehler; es gibt keine stille Kürzung.

Temporäre Dateien entstehen im Zielverzeichnis mit privaten Rechten. Erst fertig
verschlüsselte und geflushte Dateien werden atomar publiziert, ohne Copy-Fallback.
Kollisionen normaler konkurrierender Publisher werden erneut mit dem nächsten Namen
versucht. Fehler vor Commit registrieren keinen Ergebnis-PATH; temporäre Einträge werden
bestmöglich entfernt. Cancellation wird zwischen Blöcken und vor Namensversuchen geprüft,
nach erfolgreichem Commit aber nicht rückwirkend als Operationsfehler gemeldet.

## Plattformen und Sicherheitsgrenzen

| Plattform | Implementierung / Nachweis |
|---|---|
| Windows x64 | Lokal gebaut und getestet. Private DACL, exklusive temporäre Datei, Handle-Rename ohne Ersetzen. Verzeichnishandles verhindern Rename, Quellhandle verweigert Write/Delete-Sharing. |
| Linux x64 | Implementiert, nicht nativ ausgeführt. openat/O_NOFOLLOW, reguläre Quelldatei, linkat über offenen Descriptor in procfs; kein Fallback ohne procfs/Hardlinks. |
| macOS x64 | Implementiert, nicht nativ ausgeführt. Explizite INODE64-stat-Symbole, openat/O_NOFOLLOW, ACL-Entfernung, relative linkat-Publikation mit Identitätsprüfung. |
| macOS ARM64 | Implementiert, nicht nativ ausgeführt. Native stat-ABI, ansonsten derselbe macOS-Weg. |

Symlink-/Reparse-Komponenten werden abgelehnt. Auf macOS müssen insbesondere `/var` und
`/tmp` durch kanonische `/private/...`-Pfade ersetzt werden. Ein stabiler Quellhandle ist
auf Unix kein Schreibschutz gegen andere Prozesse: Längenänderungen werden erkannt,
gleich lange In-place-Änderungen nicht zuverlässig. Verzeichnisse müssen während Commit
und Ergebnisverwendung stabil bleiben. Flush(true) garantiert nicht universell die
Dauerhaftigkeit eines Verzeichniseintrags bei Stromausfall.

**Bewusste macOS-Grenze:** Das bestehende S02-Schutzmodell eines vertrauenswürdigen
Zielverzeichnisses bleibt erforderlich. Geräte-/Inodevergleich erkennt den getesteten
Temp-Austausch; ein feindlicher Verzeichnisschreiber kann auf macOS weiterhin zwischen
Identitätsprüfung und linkat eingreifen. Eine uneingeschränkte Racefreiheit gegenüber
solchen Angreifern wird nicht behauptet. Falls F01 diese stärkere Garantie verlangt,
ist dieser Punkt vor Freigabe weiterzuentwickeln; eine reine native Testausführung
beseitigt diese architektonische Grenze nicht.

## Automatische Prüfung

Alle Ausgaben stammen aus lokalen Windows-Läufen mit .NET SDK 10.0.401:

| Prüfung | Ergebnis |
|---|---|
| Release-Build CryptoWorkBench.sln | Erfolgreich; Core, Console, Kern-Testprojekt |
| Release-Build Desktop-Host | Erfolgreich; einschliesslich Avalonia-Projekt |
| Release-Build GUI-Testprojekt | Erfolgreich |
| Vollständige CryptoScriptUnitTest-Suite | 7.712 bestanden, 5 übersprungen, 0 fehlgeschlagen; 7.717 gesamt |
| Vollständige CryptoWorkBenchGuiTest-Suite | 239 bestanden, 0 fehlgeschlagen |
| Neue F01-Tests | 45 Fälle, davon unter Windows 44 bestanden und 1 Symlink-Test mangels Berechtigung übersprungen |
| git diff --check | Keine Whitespace-Fehler |

Tests umfassen VAR/PATH-Vertrag, frühe Ablehnung, verschachtelte Aufrufe, alle AES-Längen,
beide Paddings, Leerdateien, Binärdaten, 64-MiB-generierten Stream mit weniger als 2 MiB
Thread-Allokation, Vergleich mit VAR und festem NIST-Ciphertext, dokumentierten vollständigen
Binärvektor, IV-Provenienz, Headerfehler, Nummernlücken, acht konkurrierende Publisher,
Kollision unmittelbar vor Commit, Temp-Austausch, Verzeichnis-Rename, Cancellation im
Stream und im Runner, Cleanup, fehlende/gesperrte/Verzeichnis-Quelle, ungültige Schlüssel/IVs
und Erhalt der Metadatenwarnungen. Disk-full und reale Unix-Zugriffsfehler wurden nicht
physisch provoziert; diese laufen über dieselben exception-/cleanup-Pfade.

TRX-Nachweise: `artifacts/test-results/F01-core-final.trx` und
`artifacts/test-results/F01-gui.trx`. Einzelne vorherige sandboxierte Testhost-Starts
scheiterten an einem Verbindungs-Timeout; die vollständigen erfolgreichen Läufe erfolgten
danach ausserhalb der Sandbox. Ein erster Kernlauf fand vier Dokumentationsvertragstests,
deren Erwartungen an den neuen Encrypt-Hilfeeintrag angepasst beziehungsweise deren
Abschnittsstruktur korrigiert wurden. Release-Compilerwarnungen betreffen unveränderte
Nullable-Stellen in VariableDictionary, BlockHeaderVariableDeclaration und Tr31Header;
beim ersten GUI-Build zusätzlich unveränderte GUI-Nullable-Stellen.

GitHub Actions wurde nicht gestartet: Der vorhandene Workflow reagiert auf Push/PR,
beides ist ausdrücklich nicht autorisiert. Keine Linux-/macOS-CI-Ergebnisse werden behauptet.

## Unabhängiger Review

Ein separater, rein lesender Review-Agent prüfte den Code und nach Korrektur erneut
die betroffenen Stellen. Keine Dateien wurden durch diesen Reviewer verändert.

| Ursprüngliche Schwere | Finding | Behandlung |
|---|---|---|
| P1 | Temp-Name nach Schliessen austauschbar; fremde Bytes könnten publiziert werden | Offenen Handle bis Commit behalten. Windows Handle-Rename; Linux Descriptor-Link; macOS Identitätsprüfung unter expliziter S02-Vertrauensgrenze. Regressionstest vorhanden. macOS-Restgrenze bleibt sichtbar. |
| P2 | Unix-Verzeichnisverschiebung führte zu veraltetem Ergebnis-PATH | Aktuellen absoluten Pfad aus gebundenem Verzeichnishandle vor Commit ermitteln. Regressionstest; notwendige Stabilität während Commit dokumentiert. |

Der erneute statische Review fand keine weiteren konkreten Bugs. Anschliessend wurden
noch die Entsorgung bei Schlüssel-Dekodierungsfehlern und die einmalige Erfassung der
Streaming-Eingabelänge gehärtet sowie der bestehende öffentliche Zweiargument-Konstruktor
von OperationInvocation erhalten. Die vollständigen Suites wurden danach erneut geprüft.
Dies ersetzt weder die ausstehende native Plattformprüfung noch die ausdrückliche Freigabe.

## Geänderte und neue Dateien

Neu:

- `CryptoScript/FileEncryption/EncryptedFileCodec.cs`
- `CryptoScript/FileEncryption/FileEncryptCoordinator.cs`
- `CryptoScript/FileEncryption/FileEncryptionContract.cs`
- `CryptoScript/FileEncryption/FilePublication.cs`
- `CryptoScript/InfoDocs/Info.Func.Encrypt.md`
- `CryptoScriptUnitTest/FileEncryptionTests.cs`
- `docs/F01-container-format.md`
- `docs/F01-reference.hex`
- `docs/F01-implementation-report.md`

Geändert:

- `CryptoScript/CryptoAlgorithm/AES/AES.cs`
- `CryptoScript/CryptoAlgorithm/AES/AES_CBC.cs`
- `CryptoScript/CryptoAlgorithm/EncryptionMode.cs`
- `CryptoScript/CryptoScript.csproj`
- `CryptoScript/Documentation/FileInfoDocumentationProvider.cs`
- `CryptoScript/Documentation/InfoDocumentCatalog.cs`
- `CryptoScript/InfoDocs/Info.Functions.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-CBC.md`
- `CryptoScript/Model/CryptoScriptRunner.cs`
- `CryptoScript/Model/FunctionCall.cs`
- `CryptoScript/Model/FunctionCallEvaluator.cs`
- `CryptoScript/Model/OperationInvocation.cs`
- `CryptoScript/Model/SemanticDiagnosticExecution.cs`
- `CryptoScript/Model/VariableDeclarationEvaluator.cs`
- `CryptoScript/Operations/CryptoOperations.cs`
- `CryptoScript/Serialization/SnapshotByteFilePublisher.cs`
- `CryptoScriptUnitTest/CryptoScriptUnitTest.csproj`
- `CryptoScriptUnitTest/InfoDocumentCatalogTests.cs`
- `CryptoScriptUnitTest/InfoDocumentationProviderTests.cs`
