# F01-118-B – Avalonia Headless Race Fix

Stand: 22. Oktober 2026. Branch `master`, Basis und unveränderter HEAD:
`69434392f06e3f247395fe111e98a9eb974a148d`.

**Beide Headless-Races sind im testseitigen Quellbackport korrigiert und lokal
validiert. Native macOS-/ARM64-Validierung bleibt ausstehend.** Alle Änderungen
bleiben uncommitted. Die bereits vorhandene Publish-Korrektur und der frühere
Analysebericht bleiben erhalten; dessen Aussage „B offen“ beschreibt den damaligen
Stand vor diesem Backport.

## Umsetzung und Herkunft

Gewählt wurde ein lokales Quellprojekt statt eines vorgebauten oder separat zu
bootstrappenden NuGet-Pakets. Der GUI-Test verweist direkt darauf. Dadurch werden
Restore und Build vom vorhandenen CI-Testgraphen erfasst; kein neuer Feed,
Remote-Buildscript oder eigener Bootstrap-Schritt ist erforderlich.

Alle neun C#-Quelldateien stammen aus dem offiziellen Avalonia-Tag `11.3.20`,
Commit `984c3b833f29806eb159fb7f12a6132e936b75df`. Ihre Original-Git-Blob-Hashes wurden
gegen die commitgebundenen GitHub-Metadaten geprüft. Nur die Session-Datei hat
funktionale Änderungen. Der vollständige, offline prüfbare Diff liegt in
`TestInfrastructure/Avalonia.Headless.Backport/backport.patch`.

Die drei Diff-Hunks übertragen exakt die beiden für 11.3 anwendbaren Änderungen
aus [Avalonia-PR #22222](https://github.com/AvaloniaUI/Avalonia/pull/22222),
Merge `52bbf7571f291a3515a7cc9f2cbd6ae865b37e30`:

1. `Task.Run(callback)` wird durch `new Task(callback, TaskCreationOptions.DenyChildAttach)`
   und anschliessendes `task.Start(TaskScheduler.Default)` ersetzt.
2. `Dispose` führt Cancellation, Worker-Wait und erst danach Queue-Abschluss aus.

Der Task ist damit vor der Veröffentlichung der Session zugewiesen. Der Worker
kann nicht mehr über eine bereits abgeschlossene Queue stolpern, während Dispose
noch auf ihn wartet. Worker-Ausnahmen werden weiterhin durch `Task.Wait` propagiert.
`DisposeAsync` existiert im 11.3-Ausgangscode nicht und wurde nicht hinzugefügt.
Die öffentliche API bleibt unverändert; lokale Metadatenprüfung bestätigt dieselben
48 exportierten Typ-/Member-Einträge wie die offizielle Assembly.

Der unabhängige Reviewer hat die drei Edits in-memory zurückgenommen und den
ursprünglichen Session-Git-Blob
`65f648b98c1c5b35164e47d9f888fbd5c30c16db` reproduziert. Alle anderen Quellblobs
sind unverändert. MIT-Lizenz und öffentlicher Upstream-Schlüssel liegen bei.
Details stehen im README des Backport-Projekts.

## Assembly- und Paketauflösung

| Eigenschaft | Nachweis |
|---|---|
| Projekt-/Abhängigkeits-ID | `Avalonia.Headless.Backport/11.3.20-cwb.1`, Typ `project` |
| Assembly-Identität | `Avalonia.Headless, Version=11.3.20.0, Culture=neutral, PublicKeyToken=c8d484a7012f9a8b` |
| Dateiversion | `11.3.20.1` |
| Informationsversion | Backport-Version plus vollständige Upstream-SHA und PR-Kennung |
| Tatsächliche DLL | Genau eine Assembly aus dem Test-Ausgabeverzeichnis; SHA256 entspricht dem lokalen Buildmanifest |
| Offizielles Headless-Paket im Testgraphen | Entfernt; keine zweite Headless-Implementierung |
| Produktiver Graph | Paketliste vor/nach Restore unverändert; Desktop-deps und Publish enthalten kein Headless |

`PublicSign` mit dem veröffentlichten öffentlichen Schlüssel erhält den für
Avalonia-internen Friend-Zugriff nötigen Namen/Token. Es ist keine Signatur mit
Avalonias privatem Schlüssel und keine Authentizitätsbehauptung des Publishers.
Die Herkunft wird durch Quellpinning, Patch, Lizenz und Prüfsummen dokumentiert.

Eine reine Verwendung der öffentlichen Avalonia-Referenzfassaden liess den
Backport erwartungsgemäss nicht kompilieren: Diese blenden implementierbare
interne Verträge aus. Das Quellprojekt kompiliert deshalb gegen die unveränderten
`lib/net8.0`-Assemblies Base/Controls aus dem exakt gepinnten Avalonia-Paket.
Es verwendet dieselben Runtime-Pakete wie zuvor, ohne Umleitung oder Loader-Hook.
Der Testhost und das Quellprojekt zielen auf `net10.0`; der produktive Graph
wird dadurch nicht geändert.

Eine zunächst doppelte Deps-Buchführung durch gleichnamige Standard-Projekt-ID
wurde vor Abschluss durch eine explizite eigene `PackageId` beseitigt. Der finale
Test prüft genau einen Eintrag und eine geladene Headless-Assembly.

## Reproduzierbarkeit und Integrität

- Alle kompilierten Quellen sowie Lizenz, Patch, Herkunftsmanifest und Public Key werden vor dem Compile mit gepinnten SHA256-Werten geprüft.
- Ein temporärer Manipulationstest wurde erwartungsgemäss abgewiesen; nach bytegenauer Wiederherstellung bestand die Prüfung wieder.
- Die vollständige neue Paketabhängigkeit ist mit Inhalts-Hashes gelockt; Locked-Restore ist Standard.
- Keine Quellen oder vorgebauten Backport-DLLs werden während des Builds heruntergeladen.
- Ein zweiter Build aus einem anderen Quellpfad, nach Restore mit leerer Offline-Quelle und vorhandenem Cache, erzeugte dieselben DLL-Bytes.

Der erste Pfadvergleich deckte SDK-generierte checkoutabhängige PathMap-/SourceLink-
Metadaten auf. Nur im Backport-Projekt wurden die ambienten Git-Abfragen deaktiviert
und der explizite projektlokale PathMap verwendet. Die Herkunft bleibt unverändert
explizit in der Assembly dokumentiert. Mit SDK 10.0.401 ergibt sich final:

```text
D6A47A3D95A54FE93FD92575BB3086E51552FC92EAD15981FE9633D5B8953332
```

Dieser Hash ist ein lokaler Nachweis, kein plattformübergreifend fest vorgegebener
Sollwert. Andere SDK-Versionen/OS-Umgebungen wurden nicht auf Bytegleichheit geprüft.
Jeder Runner vergleicht die geladene DLL mit seinem eigenen Buildmanifest.
Die bestehende SDK-Roll-forward-Regel wurde nicht verändert.

## Regressionsergebnisse

Windows x64, .NET SDK 10.0.401, Release:

| Prüfung | Ergebnis |
|---|---|
| Identitäts-/Deps-/Buildhash-Test | Bestanden |
| 10.000 unmittelbare Session-Lebenszyklen | Bestanden, ohne Delay, Retry oder Exception-Unterdrückung |
| Worker-Ausnahme während Shutdown | Gezielte injizierte Ausnahme wird exakt über Dispose weitergegeben |
| Unveränderte 25 PinTranslation-Matrixfälle | Bestanden |
| 30 separate finale Testprozesse | 840 Fälle bestanden; darunter 750 Matrixfälle und 300.000 Session-Lebenszyklen |
| Gesamte GUI-/Integrationstestsuite | 242 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Vollständige Kerntests | 7.712 bestanden, 0 fehlgeschlagen, 12 vorhandene lokale Skips |
| F01-Container-Referenzvektor | Bestanden |
| Release-Builds inkl. Backport, GUI, historischer Console und Desktop | Erfolgreich; keine neuen Compilerwarnungen |
| Windows-Publish-Validierung | Erfolgreich; 3 native Assets, 129 InfoDocs, kein Headless im Produkt |
| Unabhängiger Pfad-/Offline-Rebuild | Bytegleiche DLL |
| Öffentliche API gegen offizielle Assembly | 48 identische Typ-/Member-Einträge |

Die zwölf Core-Skips sind unverändert: sieben Darwin-Fälle, der Darwin-ACL-Test
und vier Symlink-Fälle ohne lokales Windows-Privileg. Kein neuer Test enthält eine
Skip-Bedingung. Der explizite manuelle Benchmark wird zusätzlich im TRX als
`NotExecuted` geführt, ausserhalb der zwölf CLI-Skips.

Der Shutdown-Fehlertest verwendet Ereignisse und begrenzte Synchronisations-Waits,
keine künstlichen Schlafzeiten. Die Reflection greift ausschliesslich für die
kontrollierte Fehler-Injektion auf die gepinnten privaten Felder zu. Sie ist weder
Implementierung des Fixes noch Assembly-Umleitung. Nur die absichtlich injizierte
Ausnahme wird per Testassertion erwartet; echte unerwartete Fehler lassen den Test
scheitern. Die anderen Tests lassen Dispose-Ausnahmen ungehindert fehlschlagen.

Lokale Nachweise: `artifacts/f01-118-b/`, insbesondere `gui-final.trx`, `core.trx`,
`final-processes/`, `public-api.txt` und `repro/`. Die neuen Prüfungen laufen im
vorhandenen vollständigen GUI-CI-Schritt automatisch mit dessen Hang-Zeitlimit.

## Unabhängiger Review und offene native Validierung

Ein separater, rein lesender Reviewer prüfte Quellherkunft, exakte Patch-Semantik,
Task-Publikation, Scheduling, Shutdown, Fehlerweitergabe, Testcode, PublicSign und
Auflösung. Keine blockierenden Implementierungs- oder Sicherheits-Findings.
Sein Hinweis zur SDK-Eigenschaft `InformationalVersion` wurde umgesetzt. Die
geerbten Upstream-Whitespace-Stellen bleiben bytegenau erhalten; nur die vendorten
Quellen und der Unified-Patch mit erforderlichen Kontext-Leerzeichen sind dafür
gezielt über `.gitattributes` aus der Whitespace-Prüfung genommen.
Für eigene Änderungen gelten die normalen Prüfungen.

Noch ausstehend: Ausführung auf Linux, macOS x64 und echtem macOS ARM64, einschliesslich
Testhost-Identität, Stress- und kompletter GUI-Regression. Finding A benötigt weiterhin
den nativen Architektur-/Signatur-/Abhängigkeits-/Lade-/Export-/InfoDocs-Nachweis
auf beiden Macs. Die native F01-Implementierung, Kryptografie und das `.cwe`-Format
wurden nicht geändert. Die konkrete Ursache des alten Hängers aus CI #117 bleibt
weiterhin unbewiesen. F01 ist noch nicht technisch vollständig validiert.

## Vollständige Dateiliste

Schon vor diesem Auftrag vorhanden und erhalten:

- `.github/scripts/Validate-DesktopPublish.ps1`
- `docs/F01-118-findings-report.md`

In diesem Auftrag geändert:

- `CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj`

In diesem Auftrag neu:

- `CryptoWorkBenchGuiTest/HeadlessSessionBackportTests.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/.gitattributes`
- `TestInfrastructure/Avalonia.Headless.Backport/Avalonia.Headless.Backport.csproj`
- `TestInfrastructure/Avalonia.Headless.Backport/Avalonia.public.snk`
- `TestInfrastructure/Avalonia.Headless.Backport/LICENSE.md`
- `TestInfrastructure/Avalonia.Headless.Backport/README.md`
- `TestInfrastructure/Avalonia.Headless.Backport/VerifiedInputs.props`
- `TestInfrastructure/Avalonia.Headless.Backport/backport.patch`
- `TestInfrastructure/Avalonia.Headless.Backport/packages.lock.json`
- `TestInfrastructure/Avalonia.Headless.Backport/upstream-files.json`
- `TestInfrastructure/Avalonia.Headless.Backport/src/AvaloniaHeadlessPlatform.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/AvaloniaTestApplicationAttribute.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/HeadlessPlatformRenderInterface.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/HeadlessPlatformStubs.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/HeadlessUnitTestIsolationAttribute.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/HeadlessUnitTestSession.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/HeadlessWindowExtensions.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/HeadlessWindowImpl.cs`
- `TestInfrastructure/Avalonia.Headless.Backport/src/IHeadlessWindow.cs`
- `docs/F01-118-B-backport-report.md`

Insgesamt 23 betroffene Dateien einschliesslich der zwei bestehenden Änderungen.
Index bleibt leer; kein Commit, Push, Merge oder PR. `.codex/config.toml` wurde
weder gelesen noch verändert. Andere Worktrees bleiben unangetastet.
