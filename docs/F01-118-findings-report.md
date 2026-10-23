# F01-118 – macOS CI Findings

Stand: 22. Oktober 2026. Basis: `master`,
`69434392f06e3f247395fe111e98a9eb974a148d`.

**A ist im Skript korrigiert; die native Ausführung steht aus. B ist als externer
Bibliotheksfehler eingegrenzt, aber noch nicht behoben.** Für B wird unten ein
konkreter kompatibler Backport vorgeschlagen, entsprechend dem Auftrag für externe
Bibliotheksfehler. Sämtliche Änderungen bleiben uncommitted.

## A – Publish-Validierung

### Nachgewiesene Ursache

`lipo -verify_arch` behandelt alle nachfolgenden Argumente als Architekturen.
Der Dateipfad muss davor stehen. Genau dies erklärt die Meldung aus CI #118,
die den vollständigen Bibliothekspfad als unbekannte Architektur zurückweist.
Die Meldung beweist keine fehlende Mach-O-Architektur.

Primärquelle: [Apple lipo-Handbuch](https://raw.githubusercontent.com/apple-oss-distributions/cctools/main/man/lipo.1).

### Implementierte Korrektur

```powershell
& xcrun lipo $shimPath -verify_arch x86_64 arm64
```

Zusätzlich wurden die nachfolgenden Prüfschritte untersucht. `otool -L` untersucht
standardmässig die Host-Architektur, sofern vorhanden. Deshalb wird die bisherige
exakte Abhängigkeitsliste jetzt getrennt für `x86_64` und `arm64` verlangt:

```powershell
& xcrun otool -arch $architecture -L $shimPath
```

Erwartet werden pro Architektur ausschliesslich die eigene Install-ID
`@rpath/libCryptoScript.FileSystem.dylib` und `/usr/lib/libSystem.B.dylib`.
Die eigene ID ist Metadaten der Bibliothek und keine zusätzliche externe
Abhängigkeit. Fehlende oder zusätzliche Einträge führen weiterhin zum Abbruch.
Quelle: [Apple otool-Handbuch](https://raw.githubusercontent.com/apple-oss-distributions/cctools/main/man/otool-classic.1).

Die folgenden Prüfungen bleiben erhalten:

- `codesign --verify --strict` mit Fehlerprüfung.
- Laden mit `NativeLibrary.Load` über den absoluten Publish-Pfad.
- Auflösen von `cwe_openat` und `cwe_getpath`, danach Freigabe des Handles.
- Native und verwaltete Assets gemäss Publish-Manifest.
- Vollständigkeit, korrekte Gross-/Kleinschreibung, Hashgleichheit und Lesbarkeit aller InfoDocs.

Erfolgsmeldungen nach Architektur-, Signatur-, Abhängigkeits- und Ladeprüfung
machen den nächsten nativen CI-Nachweis sichtbar. Es gibt keine Erfolgsmeldung
vor Bestehen der jeweiligen Prüfung und keinen Ersatzpfad bei einem Fehler.

### Prüfung und verbleibendes Risiko

PowerShell-Syntax und Windows-Publish-Validierung sind erfolgreich: drei native
Assets und 129 InfoDocs bestätigt. Keine offensichtlichen weiteren Argument-
oder Plattformfehler wurden im unabhängigen Review gefunden. Die tatsächlichen
Xcode-, Signatur-, Abhängigkeits- und Ladeprüfungen müssen weiterhin auf beiden
macOS-Runnern ausgeführt werden. Dieser Rechner ist Windows x64.

## B – ARM64-GUI-Fehler

### Beobachtungen und Reproduktion

Der ursprüngliche CI-Fehler stammt aus
`RenderedMatrixLinkAndBackNavigateThroughActualView(2,4)` beim
`HeadlessUnitTestSession.Dispose()`: `AggregateException` mit innerer
`InvalidOperationException` aus `BlockingCollection.Take()`.

Der unveränderte Test wurde lokal untersucht. Das tatsächlich geladene
`CryptoWorkBenchGuiTest.deps.json` bestätigt `Avalonia.Headless/11.3.20`.

| Lauf mit unverändertem GUI-Testcode | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---:|---:|---:|
| Erster gezielter Matrixlauf | 25 | 0 | 0 |
| Nachfolgender gezielter Matrixlauf | 24 | 1 | 0 |
| Vollständiger GUI-Satz danach | 239 | 0 | 0 |
| Weitere 30 separate gezielte Läufe | 750 | 0 | 0 |
| Abschliessender vollständiger GUI-Satz | 239 | 0 | 0 |

Der lokale Fehler betraf ebenfalls exakt Fall `(2,4)` und `Dispose()`, diesmal
eine `NullReferenceException`. Damit ist ein sporadischer Fehler am selben
Session-Lebenszyklus lokal reproduziert. Die ursprüngliche CI-Exception wurde
lokal nicht erneut beobachtet. Die 30 erfolgreichen Läufe sind keine Behebung
und kein Nachweis der Abwesenheit des Fehlers.

Der erste Einzelaufruf enthielt `NUnit.Repeat=30`, führte aber nachweislich nur
25 Fälle aus. Er wird deshalb als ein Lauf gezählt. Die anschliessenden
30 Wiederholungen waren echte getrennte `dotnet test`-Aufrufe mit eigenen TRX
und Logs, ohne Retry-Attribute oder Änderung des Testcodes.

### Lebenszyklus, Threading und Ereignisse

Der Test führt seine UI-Arbeit innerhalb eines abgewarteten `session.Dispatch`
aus. Der Callback ist synchron; seine Ausführung wird mit `await` abgewartet.
`OutputOperations.Info` ruft `InfoEvent` synchron auf. Der Handler wird innerhalb
des Dispatchs registriert und im `finally` vor dessen Ende wieder entfernt.
`InfoViewModel.SetInfoText`, Linknavigation und Back-Command starten in diesem
Test keine eigenen asynchronen Aufgaben. Der Modellkonstruktor verwendet keine
History-Instanz. Für einen vergessenen Handler oder eine nicht abgewartete
produktive Aufgabe gibt es hier keinen Nachweis.

Der Matrixfall ist NUnit-`NonParallelizable`; die anderen Session verwendenden
Fixtures sind ebenfalls `NonParallelizable`. Wiederholtes Erstellen/Beenden
kommt in allen 25 Matrixfällen sowie in `AsyncPasswordGuiTests` und einem
Darstellungstest vor. Die gezielte Reproduktion benötigt diese anderen Fixtures
nicht. Trotzdem bleiben Avalonia-interne Aufräumarbeiten und der Queue-Worker
asynchron gegenüber dem NUnit-Testthread.

### Konkrete externe Fehlermechanismen

In der verwendeten [Avalonia-Version 11.3.20](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/11.3.20/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs)
existieren zwei Races:

1. Der Worker kann die Session veröffentlichen, bevor die Variable seines
   `Task.Run`-Tasks zugewiesen wurde. Die Session kann dadurch einen null-Task
   für das spätere `Dispose` erhalten. Das passt zur lokalen NullReferenceException.
2. `Dispose` beendet die Queue nach Cancellation, bevor es auf das Worker-Ende
   wartet. Der Worker fängt nur Cancellation ab; `Take` kann bei vollständig
   abgeschlossener leerer Queue stattdessen die in CI beobachtete
   InvalidOperationException werfen.

Der bereits gemergte [Avalonia-PR #22222](https://github.com/AvaloniaUI/Avalonia/pull/22222)
behandelt genau beide Mechanismen. Der lokale und der CI-Stack passen unmittelbar
dazu. Die einzelnen Scheduler-Interleavings wurden nicht mit einem Debugger
aufgezeichnet; die Zuordnung beruht auf den Stacks, dem konkreten Bibliothekscode
und der passenden Upstream-Korrektur. Ein eigener Test-Lebenszyklusfehler ist
nicht nachgewiesen.

### Technisch begründeter Lösungsvorschlag – noch nicht implementiert

Ein kleiner, nachvollziehbarer **Avalonia-11.3-kompatibler Backport beider
Änderungen aus #22222** ist der bevorzugte nächste Schritt:

- Worker-Task zuerst erzeugen, danach starten; damit besitzt jede veröffentlichte
  Session bereits den gültigen Worker-Task.
- Beim Shutdown: Cancellation, Worker vollständig abwarten, erst danach Queue
  abschliessen.

Der Backport sollte als reproduzierbar gebautes und eindeutig versioniertes
Testpaket mit festgehaltener Herkunft und Patch vorliegen. Die Auswahl/Erstellung
dieses Pakets ist noch offen. Nur der Testprojekt-Verweis soll darauf wechseln;
die produktiven Avalonia-Abhängigkeiten sollen kompatibel bleiben. Ein blosses
Update auf einen anderen 11.3-Patch darf erst nach Prüfung seines tatsächlichen
Quellcodes als Lösung gelten. Ein Wechsel auf Avalonia 12 nur wegen dieses
Findings wäre wesentlich umfangreicher als der gezielte Backport.

Danach sind gezielte Session-Start/Stop-Stresstests, die unveränderten 25
Matrixfälle und die vollständigen GUI-Suiten auf allen Plattformen erforderlich.
Die bestehenden Hang-Diagnosen müssen erhalten bleiben.

Für diesen Auftrag wurde gemäss der Klausel zu externen Bibliotheksfehlern
die Evidenz dokumentiert und diese konkrete Lösung vorgeschlagen. Es wurde
kein Reflexions-Workaround eingebaut, keine Dispose-Ausnahme unterdrückt,
kein Test übersprungen, kein Retry-Attribut und keine Wartezeit hinzugefügt.
Der GUI-Code und sämtliche Testmethoden bleiben unverändert. **Finding B bleibt
bis zur Umsetzung und Validierung des kompatiblen Fixes blockierend.**

## Regression und Review

| Lokale Prüfung | Ergebnis |
|---|---|
| Release-Build GUI/Testprojekt samt Kern und historischer Console | Erfolgreich; nur die bekannten 14 Nullable-Warnungen in unveränderten Dateien |
| Vollständige Kerntests | 7.712 bestanden, 0 fehlgeschlagen, 12 übersprungen |
| F01-Container-Referenzvektor | Bestanden, im Kern-TRX bestätigt |
| Abschliessende vollständige GUI-/Integrationstests | 239 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Windows-Publish-Validierung | Erfolgreich, 3 native Assets, 129 InfoDocs |
| PowerShell-Syntax und `git diff --check` | Erfolgreich |

Die zwölf Core-Skips bestehen aus sieben Darwin-Regressionen, dem Darwin-ACL-Test
und vier vorhandenen Symlink-Fällen ohne lokales Windows-Privileg. Der explizite
manuelle Benchmark ist zusätzlich als `NotExecuted` im TRX, ausserhalb der zwölf
CLI-Skips. Native macOS-/Linux-Dateioperationen wurden in diesem Auftrag nicht
neu ausgeführt. Die früheren grünen Kerntests aus #118 sind Basisevidenz, ersetzen
aber keine erneute native Validierung.

Ein unabhängiger, rein lesender Reviewer hat die Publish-Korrektur und die
Bibliotheksanalyse geprüft sowie das lokale fehlgeschlagene TRX selbst gelesen.
Keine handlungsrelevanten Findings an A. Für B bestätigt er den Bezug auf beide
Upstream-Races und die Notwendigkeit beider Backport-Änderungen. B wird ausdrücklich
nicht als behoben bewertet. Keine neuen Compilerwarnungen; keine Änderungen an
produktiver Kryptografie, `.cwe`-Format/Referenzvektor, nativen Dateioperationen,
Sicherheitsgarantien oder GUI-Tests.

Lokale Nachweise liegen unter `artifacts/f01-118/`: `baseline-targeted/matrix-1.trx`,
`baseline-repeat/matrix-1.trx` bis `matrix-30.trx` mit Logs,
`baseline-full.trx`, `final-gui.trx` und `core.trx`.

## Vollständige Dateiliste und Git

Geändert:

- `.github/scripts/Validate-DesktopPublish.ps1`

Neu:

- `docs/F01-118-findings-report.md`

Index leer; beide Dateien uncommitted auf `master`. HEAD bleibt der Basiscommit
`69434392f06e3f247395fe111e98a9eb974a148d`. Kein Commit, Push oder Pull Request.
Das vorhandene unversionierte `.codex/` bleibt bestehen;
`.codex/config.toml` wurde weder gelesen noch verändert. Andere Worktrees
wurden nicht verändert. F01 ist weiterhin nicht technisch vollständig validiert.
