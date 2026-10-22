# F01-ARM64 – Native ABI Correction

Stand: 22. Oktober 2026. Basis: `master`, Commit
`4b785324c9796dc0f3bb7605904343941970b767`.
Status: **Korrektur uncommitted; lokale Windows-Prüfungen erfolgreich; native
macOS-Validierung ausstehend. F01 ist noch nicht technisch vollständig validiert.**

## Ursache und Abgrenzung zum Hänger

Apple deklariert `openat(int, const char *, int, ...)` und
`fcntl(int, int, ...)` als variadische Funktionen. Die Apple-ARM64-ABI weist
variadische Argumente Stack-Slots zu. Die bisherigen festen P/Invoke-Signaturen
übergaben dagegen das vierte beziehungsweise dritte Argument als festes Argument.
Dies ist eine nachgewiesene ABI-Verletzung.

Alle betroffenen Aufrufstellen in `FilePublication` wurden geprüft:

| Aufruf | Bedeutung des variadischen Arguments |
|---|---|
| Wurzelverzeichnis öffnen | Kein `O_CREAT`; mode wird nicht benötigt |
| Verzeichniskomponenten öffnen | Kein `O_CREAT`; mode wird nicht benötigt |
| Quelle öffnen | Kein `O_CREAT`; mode wird nicht benötigt |
| Temporäre Datei erstellen | `O_CREAT | O_EXCL` benötigt mode `0600`; falsche Übergabe gefährdet korrekte Rechte |
| Aktuellen Verzeichnispfad bestimmen | `fcntl(F_GETPATH)` benötigt einen gültigen Pufferzeiger; falsche Übergabe kann Fehler oder Speicherbeschädigung verursachen |

Die weiteren Deklarationen `CreateFileW`, `GetFileType`,
`SetFileInformationByHandle`, `fstat`, `fstatat`, deren Darwin-INODE64-Varianten,
`linkat` und `unlinkat` haben feste native Parameterlisten. Kein vergleichbarer
Varargs-Fehler wurde gefunden. Die bestehenden plattformabhängigen `stat`-Layouts
bleiben unverändert und müssen weiterhin durch native Tests bestätigt werden.
Der Linux-Wert `-100` beim Öffnen von `/` ist unter Darwin nicht `AT_FDCWD`;
bei diesem absoluten Pfad wird der Verzeichnisdescriptor jedoch ignoriert.

Der ARM64-Job von [CI #117](https://github.com/tkbss/CryptoWorkBench/actions/runs/39802393450)
liefert **keinen Beweis für den tatsächlich blockierten Test oder die konkrete
Hängerursache**. Der unabhängige Prüfer hat den verfügbaren vollständigen Joblog
erneut gelesen: 3.343 protokollierte bestandene Fälle, zuletzt
`AsyncRunnerCancellationReachesOperation` um 15:12:31 UTC; danach erst Abbruch
um 15:26:21 UTC. Es fehlen Teststart-Sequenz, Stacktrace, Dump und abschliessendes
TRX. Der letzte erfolgreiche Test bricht vor den nativen Dateioperationen ab.
`AutomaticIvIsFreshAndParameterUnchanged` ist ein möglicher Reproduktionskandidat,
aber kein nachgewiesener Hänger. Auch ein künftig erfolgreicher korrigierter Lauf
beweist für sich allein nicht die Ursache des damaligen Hängers.

Primärquellen:

- [Apple ARM64 ABI: variadische Argumente](https://developer.apple.com/documentation/xcode/writing-arm64-code-for-apple-platforms)
- [Apple fcntl.h mit nativen Prototypen und Konstanten](https://raw.githubusercontent.com/apple-oss-distributions/xnu/main/bsd/sys/fcntl.h)
- [Apple fcntl-Handbuch](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/fcntl.2.html)
- [.NET Runtime: Apple-Silicon-Varargs und native Wrapper](https://github.com/dotnet/runtime/issues/48752)

## Korrektur und native Auslieferung

Ein kleiner C-Wrapper exportiert genau zwei feste Verträge:

```c
int cwe_openat(int fd, const char *path, int flags, uint32_t mode);
int cwe_getpath(int fd, char *path, int capacity);
```

Der Apple-C-Compiler ruft die echten SDK-Prototypen von `openat` und `fcntl` auf
und übernimmt deren architekturspezifische Varargs-Übergabe. `mode_t` wird korrekt
gecastet und nach den C-Regeln promoviert. `F_GETPATH` verwendet die SDK-Konstante;
der Wrapper prüft Nullzeiger und Kapazität gegen `MAXPATHLEN`. `errno` bleibt bis
zur Erfassung durch `SetLastError=true` erhalten. UTF-8-Pfade und Ausgabepuffer
werden synchron verwendet; native Zeiger werden nicht gespeichert.

Beide macOS-Architekturen verwenden diesen Wrapper. Die DllImport-Suchpfade sind
auf das Assembly-Verzeichnis begrenzt. Eine fehlende oder unladbare Bibliothek
führt zum Fehler, nicht zu einem Ersatzpfad mit unsicherer Signatur.
Windows verwendet weiterhin seine bisherigen APIs, Linux seinen bisherigen
`openat`-Aufruf.

Das importierte MSBuild-Target baut auf macOS mit Xcode Command Line Tools:
`xcrun --sdk macosx clang`, explizit C11, `-Wall -Wextra -Werror`, versteckte
Symbole ausser den zwei Exporten, `x86_64` und `arm64`, Mindestziel macOS 11.0,
Install-Name `@rpath/libCryptoScript.FileSystem.dylib`. `_DARWIN_C_SOURCE` macht
die benötigten Darwin-Schnittstellen explizit sichtbar. Die Universal-Bibliothek
wird ad hoc signiert. Erst nach erfolgreichem Build und Signieren wird die finale
Ausgabedatei ersetzt; ein Fehler kann daher nicht als erfolgreicher inkrementeller
Build erscheinen. Generierte Dateien liegen unter `obj`, nicht im Quellbaum.

`CopyToOutputDirectory` und `CopyToPublishDirectory` propagieren die Bibliothek
über Projektverweise in Test-, GUI- und Desktop-Verzeichnisse. Ein macOS-RID-Build
auf einem Nicht-macOS-Host wird ausdrücklich abgewiesen. Das ist eine neue
Buildvoraussetzung; für Windows/Linux entsteht keine zusätzliche native Bibliothek.
Auf macOS wird nur die Systembibliothek `libSystem` benötigt. Die Publish-Prüfung
kontrolliert zusätzlich beide Mach-O-Architekturen, Signatur, Abhängigkeiten,
Laden aus dem Publish-Verzeichnis und beide Exporte. Vorhandene Native-Assets und
alle InfoDocs bleiben Bestandteil der Prüfung.

Diese Build-/Ladekette ist implementiert und statisch geprüft, aber hier mangels
macOS-Host **noch nicht nativ ausgeführt**. Der SDK-/Runner-Stand wird vom späteren
CI-Lauf bestimmt; eine bitidentische Reproduktion über wechselnde Xcode-Versionen
wird nicht behauptet. Ad-hoc-Signierung ist keine Developer-ID-Notarisierung.

## Sicherheitsinvarianten

- Verzeichnis- und Dateihandles bleiben gebunden; keine erneute unsichere Pfadauflösung.
- `O_NOFOLLOW`, `O_CLOEXEC`, exklusive Erstellung und der angeforderte Modus `0600` bleiben erhalten.
- Geerbte Darwin-ACLs werden weiterhin vor Schreiben vertraulicher Daten entfernt.
- Veröffentlichung bleibt atomar und überschreibt kein vorhandenes Ziel.
- Darwin-Geräte-/Inode-Prüfung vor `linkat`, Fehlerbehandlung und Cleanup bleiben erhalten.
- Die dokumentierte macOS-Vertrauensgrenze bleibt unverändert: Das Ausgabeverzeichnis muss vertrauenswürdig sein; zwischen Identitätsprüfung und `linkat` besteht weiterhin die dokumentierte Grenze gegenüber bösartigen gleichberechtigten Verzeichnisschreibern.
- Containerformat, Referenzvektor, CryptoScript-Syntax und kryptografische Verarbeitung wurden nicht geändert.

## Regressionen und Hang-Diagnose

Sieben neue Darwin-Tests prüfen echte native Operationen:

1. Verschachtelte Verzeichnisöffnung und `F_GETPATH`.
2. Handle-gebundene Quelldateiöffnung und Lesen.
3. Temporäre Datei mit `0600`, `linkat`, Kollisionsabweisung, unveränderte Rechte und Cleanup.
4. Abweisung von Verzeichnis- und Quellsymlinks.
5. Exklusive Erstellung gegen einen bereits vorhandenen Symlink ohne Veränderung des Zieles.
6. Weitergabe von `ENOENT` und anschliessender erfolgreicher Erstellung/Bereinigung.
7. `F_GETPATH` nach Verschiebung des gebundenen Verzeichnisses.

Der gezielte Lauf umfasst zusätzlich alle 45 bestehenden F01-Fälle, insbesondere
automatische Nummerierung, Kollisionen, Cancellation, Fehlerbereinigung,
Identitätsaustausch und den vollständigen Container-Referenzvektor.

Vor den vollständigen Suiten führt jeder CI-Job `Test-F01Native.ps1` aus. Für
`osx-arm64` prüft das Skript PowerShell und der Testcode zusätzlich den tatsächlichen
.NET-Testhost auf Darwin/ARM64. Rosetta/x64 qualifiziert nicht. Das Skript verlangt
im TRX einen bestandenen Referenzvektor und auf macOS alle sieben bestandenen
nativen Fälle; fehlende Discovery oder Skips können das Gate nicht erfüllen.
Für jeden Job gilt das Gate vor dessen vollständigen Suiten; andere Plattformjobs
dürfen parallel laufen.

Gezielter Lauf: 60 Sekunden Inaktivitätsgrenze über VSTest `--blame-hang`, Mini-Dump,
Diagnoselogs, NUnit-Trace und fünf Minuten CI-Step-Limit. Vollständige Suiten:
zwei Minuten Hang-Grenze; gesamter Job maximal 30 Minuten. Die Artefaktpfade
umfassen TRX, Logs und bei einem erkannten Hänger Sequenzdateien/Dumps. Ein regulärer
fehlgeschlagener Testlauf lässt den Diagnose-Upload zu; ein manuell vollständig
abgebrochener Job garantiert keinen Upload. Erfolgreiche Läufe erzeugen erwartungsgemäss
keine Blame-Sequenzdatei.

## Tatsächlich ausgeführte lokale Prüfungen

Windows x64, .NET SDK 10.0.401, Release:

| Prüfung | Bestanden | Fehlgeschlagen | Übersprungen | Ergebnis |
|---|---:|---:|---:|---|
| Gezielter F01-Lauf, 52 Fälle | 44 | 0 | 8 | Erfolgreich |
| Vollständige Kernsuite, 7.724 ausgewählte Fälle | 7.712 | 0 | 12 | Erfolgreich |
| GUI-/historische Console-Integration | 239 | 0 | 0 | Erfolgreich |
| Release-Build Kern, beide Testprojekte, GUI, Desktop | – | – | – | Erfolgreich |
| Desktop-Publish win-x64 | – | – | – | Erfolgreich |
| Publish-Layout | – | – | – | 3 native Assets und 129 InfoDocs bestätigt |
| Nicht-macOS-Host für macOS-Build abweisen | – | – | – | Erwarteter expliziter MSBuild-Fehler |
| PowerShell-Syntax und `git diff --check` | – | – | – | Erfolgreich |

Die acht gezielten Skips: sieben neue Darwin-Fälle sowie
`SourceSymlinkIsRejected` wegen fehlendem Windows-Symlink-Privileg.
Die zwölf Kern-Skips: dieselben acht, der vorhandene Darwin-ACL-Test sowie
`NativePublicationDoesNotFollowLateSymlink` und die zwei
`TargetSymlinksIncludingDanglingLinksAreRejected`-Fälle wegen fehlendem
Symlink-Privileg. Der explizite manuelle Benchmark `MeasureLocalHardware` erscheint
zusätzlich im TRX als `NotExecuted`, ist aber nicht in den 7.724 ausgewählten
Fällen beziehungsweise zwölf CLI-Skips enthalten.

Keine neuen Compilerwarnungen im finalen Testbuild. Beim GUI-Build erschienen die
bereits bekannten Nullable-Warnungen in unveränderten GUI-Dateien. Ein erster
sandboxierter GUI-Build scheiterte ohne Compilerdiagnose; der erneute vollständige
Build ausserhalb der Sandbox war erfolgreich.

Lokale Nachweise (ignorierte Buildartefakte):

- `artifacts/f01-native/F01-native.trx` und zugehörige Diagnoseprotokolle
- `artifacts/f01-native-local.log`
- `artifacts/test-results/F01-ARM64-core.trx`
- `artifacts/test-results/F01-ARM64-gui.trx`
- `artifacts/publish/F01-ARM64-win-x64/`

## Unabhängiger Review und offene Validierung

Ein separater, rein lesender Review-Agent hat zuerst die Ursache unabhängig
untersucht und danach die Korrektur sowie ihre Nachbesserungen geprüft.
Schwerpunkte: ABI, Marshalling, Speichergrenzen, errno, Dateisystemsicherheit,
native Build-/Auslieferungskette und Plattformkompatibilität.

| Review-Finding | Behandlung |
|---|---|
| PowerShell-ARM64 allein beweist keinen ARM64-Testhost | Architekturprüfung im tatsächlichen Testprozess und zwingende TRX-Ergebnisprüfung ergänzt |
| Zusammensetzen des Zwischenpfads setzte relativen `BaseIntermediateOutputPath` voraus | Normalisierung über `Path.GetFullPath(base, project)` ergänzt |
| Fehlender nativer macOS-Ausführungsnachweis | Offen ausgewiesen; erst nach Freigabe über GitHub Actions möglich |

Der abschliessende statische Review fand keine blockierenden Implementierungs-
oder Sicherheitsprobleme. Das ist keine native Laufzeitfreigabe.

Noch nicht möglich waren: Apple-SDK-Kompilation/Signierung, tatsächliche
Weitergabe und Ladung der Universal-Bibliothek, alle sieben nativen Darwin-Fälle,
gezielte ARM64-Regression sowie vollständige Linux-/macOS-x64-/macOS-ARM64-Suiten
und deren Desktop-Publishes. Derzeit steht ausschliesslich der Windows-Rechner
zur Verfügung. Die früheren grünen Plattformläufe gelten für den Basiskommit,
nicht als Validierung dieser Korrektur.

Nach gemeinsamer Prüfung und gesonderter Freigabe können ein Korrektur-Commit und
Push den vorhandenen Vier-Plattformen-Workflow starten. Erst erfolgreiche
vollständige Plattformläufe ohne blockierende Findings erlauben die technische
Abschlusserklärung; die Projektabnahme bleibt beim Projekteigentümer.

## Vollständige Dateiliste und Git-Status

Geändert:

- `.github/scripts/Validate-DesktopPublish.ps1`
- `.github/workflows/dotnet.yml`
- `CryptoScript/CryptoScript.csproj`
- `CryptoScript/FileEncryption/FilePublication.cs`

Neu:

- `.github/scripts/Test-F01Native.ps1`
- `CryptoScript/Native/file_publication.c`
- `CryptoScript/Native/FilePublication.targets`
- `CryptoScriptUnitTest/FilePublicationNativeTests.cs`
- `docs/F01-ARM64-correction-report.md`

Alle neun Dateien bleiben uncommitted und unstaged auf `master`. HEAD bleibt
`4b785324c9796dc0f3bb7605904343941970b767`. Kein Branch, Commit, Push oder Pull
Request wurde erstellt; andere Worktrees wurden nicht verändert.
Das bereits unversionierte `.codex/` bleibt bestehen; `.codex/config.toml` wurde
weder gelesen noch verändert.
