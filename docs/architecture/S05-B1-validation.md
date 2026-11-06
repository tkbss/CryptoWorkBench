# S05-B1 – Async Execution and Password Event Infrastructure

Stand: 2026-10-21. Basis: `master`, `fa9194e1839806b7142526946fd95480f1c4fafc`.
Verbindliche Grundlage: `artifacts/S05-A-secure-container-design.md` und
`artifacts/S05-A-architecture-review.md`, Eigentümerentscheidungen D1–D7.

## Umfang

Implementiert sind asynchrone Runner-Ausführung, das Runner-lokale Passwortprotokoll,
begrenzte Ausführungskoordination und die Avalonia-Anbindung. Es gibt keine neue
CryptoScript-Syntax, keine registrierte SecureSave-/SecureLoad-Funktion und keine
Container-, Manifest-, KDF- oder Kryptografieimplementierung. Bestehende Algorithmen,
NuGet-Abhängigkeiten und S01-/S02-Dateiformate bleiben unverändert.

Der interne `AsyncInfrastructureStatement` ist ausschliesslich ein kontrollierter
Test-Seam. Parser und Funktionsregistry können ihn nicht erzeugen. Der echte Dialog
wird in Headless-Integrationstests über diesen Seam geöffnet.

## Core-API und Execution-Lifecycle

`CryptoScriptRunner.Execute(...)` bleibt erhalten. Die beiden neuen Überladungen
`ExecuteAsync(CryptoScriptParser.ProgramContext, CancellationToken)` und
`ExecuteAsync(IEnumerable<StatementNode?>, CancellationToken)` liefern
`Task<CryptoScriptProgram>`. Normale Statements laufen synchron im bestehenden
Evaluator; ausschliesslich der explizite interne Async-Dispatch wird awaited.

Jeder Runner besitzt eine unveränderliche `RunnerId`. Jede Ausführung besitzt eine
eigene Diagnose-Execution mit `ExecutionId`; Statementindex und Diagnoseorte bleiben
erhalten. Mapping und Auswertung erfolgen schrittweise. Bereits erfolgreiche
Statements bleiben bei späteren Fehlern wirksam. Ein `finally` stellt den aktiven
Diagnosekontext wieder her und publiziert die zuletzt abgeschlossene äussere
Execution auch nach Fehler oder Abbruch. Bestehende synchrone Reentrancy bleibt
innerhalb derselben synchronen Thread-Lease möglich; konkurrierende/asynchrone
Reentrancy endet kontrolliert mit `ExecutionBusyException`.

`ExecuteAsync` verschiebt Arbeit selbst nicht auf einen Worker. Diese Verantwortung
trägt der Host. Es gibt keine blockierende Task-Überbrückung im neuen Pfad.

## Passwortevent und Antwortvertrag

`PasswordRequested` ist ein Instanzevent vom Typ `PasswordRequestHandler`, dessen
Rückgabe `Task<PasswordResponse>` ist. Es existiert kein statisches Passwortevent.
Die unveränderlichen Eventargumente enthalten RunnerId, ExecutionId, die neue
RequestId, Purpose, ConfirmationRequired und CancellationToken, aber kein Passwort.

Vor dem Aufruf wird die Subscriberliste als Snapshot geprüft: exakt ein Handler ist
erforderlich. Null Task, Null Response, fremde RequestId, wiederverwendete/disponierte
Antworten und falsche Antwortformen sind kontrollierte Protokollfehler. Hostfehler
vor und nach Task-Rückgabe werden durch einen festen `PasswordProtocolException`-
Text und Code ersetzt; die ursprüngliche Exception wird weder als InnerException
noch in Diagnosen/Logs weitergereicht. Eine Task kann nur einmal abgeschlossen werden;
zusätzlich verhindert die Antwort selbst mehrfachen Consume.

SecureSave verlangt zwei getrennte Puffer; SecureLoad genau einen. Der Core prüft
unabhängig vom Dialog die Form, striktes UTF-8 ohne Ersatz ungültiger Surrogate,
1–1024 UTF-8-Bytes und übereinstimmende Bestätigung. Temporäre UTF-8-Puffer werden
im `finally` gelöscht; der Vergleich verwendet `FixedTimeEquals`.

## Ownership und Speichergrenzen

`PasswordSecret.TakeOwnership(char[])` übernimmt den kompletten Puffer. Der Aufrufer
muss danach alle Aliase aufgeben und darf ihn weder weiter verändern noch erneut
verpacken. Dieser Besitzvertrag ist keine technische Isolation gegenüber einem
bösartigen Host mit bereits gehaltenen Referenzen. `Characters` ist eine geliehene
`ReadOnlyMemory<char>`-Ansicht, die die Besitzdauer nicht überschreiten darf.

`PasswordResponse.Supplied` übernimmt gültige Secrets atomar; eine erneute Übergabe
derselben Wrapper oder ein gemeinsam verwendetes Bestätigungsarray wird abgelehnt.
Nach erfolgreicher Übergabe darf der Host Antwort und Secrets nicht weiter verwenden.
Bei einer fehlgeschlagenen Factory bleibt die Bereinigung beim bisherigen Besitzer.
Nach validierter Antwort besitzt die Operation die Response und muss sie per
`using`/`finally` entsorgen. Fehlerhafte und späte Antworten entsorgt der Runner.

Dispose ist idempotent und löscht die vollständigen eigenen char-Arrays mit
`CryptographicOperations.ZeroMemory`. Spätere Zugriffe auf `Characters` werfen
`ObjectDisposedException`. `ToString()` enthält kein Material. Die vertraulichen
Properties sind für System.Text.Json und Newtonsoft.Json ausgeschlossen.
Dies ist keine Zusage für beliebige reflektionsbasierte Fremdserializer.

Es gibt keine Passwortproperties in langlebigen ViewModels, History oder Runnern.
Avalonia-TextBoxen verwenden unvermeidlich verwaltete Strings; die Felder werden
beim Schliessen geleert. GUI-interne Kopien, verwaltete Strings, zuvor kopierte
Speicheransichten, GC-Kopien und von Fremdcode angelegte Kopien können nicht garantiert
gelöscht werden. BouncyCastle-/Argon2id-Speichergrenzen aus D1–D7 werden durch B1 weder
erweitert noch gelöst, da B1 diese Ableitung nicht ausführt.

## Cancellation

Token-Abbruch beendet die Core-Task als Canceled. Benutzerabbruch wirft stattdessen
`SecurePasswordCancelledException` mit festem Text. Beide Fälle liefern keinen Erfolg
und verhindern die nächste Statementauswertung. Nach der letzten erfolgreich
abgeschlossenen Operation erfolgt kein nachträglicher Tokencheck, der deren Erfolg
rückwirkend umdeuten könnte. B1 selbst führt keinen Secure-Commit aus.

Der Runner wartet mit `WaitAsync(token)` auf den Host. Ignoriert dieser den Abbruch,
beobachtet ein intern vollständig abgesicherter Observer seine späte Antwort oder
Exception, entsorgt die Antwort und gibt die Request-Sperre frei. Bis zur tatsächlichen
Host-Task-Beendigung lehnt derselbe Runner neue Passwortanfragen mit
`PasswordRequestBusy` ab. Ein Host, der nie beendet wird, bleibt damit quarantänisiert;
er lässt sich nicht zwangsweise terminieren. Es gibt höchstens eine offene Anfrage
je Runner und keine unbeobachtete Host-Task-Exception aus diesem Pfad.

## Koordination und Host-Threading

`InterpreterExecution.Enter()` reserviert synchron fail-fast die globale Execution.
`RunAsync` reserviert sie über den gesamten asynchronen Auftrag. AsyncLocal überträgt
die Execution-Identität, erteilt aber allein keine Berechtigung zum Dictionaryzugriff.
`AccessState` kapselt kurze synchrone Hostschritte wie Parsing und Snapshot-Erstellung.
State-Scopes sind threadgebunden und dürfen keine asynchrone Funktion umschliessen.
Der Runner kapselt GetEnumerator, MoveNext/Current, Evaluate und Dispose entsprechend.

ParserBuilder und Dictionary-Einstiege prüfen die Zugriffsberechtigung. Hostcallbacks
erhalten keine Berechtigung, selbst wenn sie den ExecutionContext geerbt haben.
Ein zweiter Runner während einer laufenden Execution wird abgelehnt. Die GUI nutzt
zusätzlich eine asynchrone Hostqueue, die ihre Aufträge inklusive Syntaxreset,
Parsing, Auswertung und Snapshot serialisiert und über Task.Run ausführt. Verschachtelte
Queuesubmission wird abgelehnt, damit ein Callback nicht auf sich selbst wartet.

Dies ist eine begrenzte Koordination, keine allgemein threadsichere Neugestaltung des
Singletons. Bereits extern gehaltene mutable Variablenreferenzen und direkte Zugriffe
auf bestehende statische Syntaxfelder lassen sich damit nicht rückwirkend isolieren.
Externe Hosts müssen Parsing, Syntaxreset, Ausführung und Präsentationscapture unter
derselben Lease ausführen und eigene konkurrierende Arbeiten vollständig awaiten.
Im Async-Host sind synchrone Zustandszugriffe explizit in `AccessState` auszuführen.

## Avalonia-Integration und Kompatibilität

Der Editor verwendet den asynchronen Pfad, verfolgt die Eingabeausführung als Task
und platziert Printausgabe an einem Dokumentanker. Scriptbook-Zeilen werden strikt
sequenziell awaited; Benutzerabbruch stoppt den Rest. Open/Save-Sidebar-Commands sind
AsyncDelegateCommands. KeyMap initialisiert im laufenden UI ebenfalls über die Queue.
Die alten synchronen Einstiege bleiben für bestehende Core-/Hostaufrufe erhalten.

Print-/Info-Handler marshallen Workerereignisse auf den UI-Dispatcher. Eine
Dispatcherbarriere vor der Ergebnispräsentation erhält die Ausgabe-Reihenfolge.
Variablen werden unter der Execution-Lease in losgelöste Präsentationsmodelle kopiert;
die ObservableCollection wird auf dem UI-Thread aktualisiert. Modify-Commands weisen
aktive Editor-/Scriptbookarbeit ab und behandeln konkurrierenden Lease-Erwerb ohne
entweichende UI-Exception. Die Scriptbook-Reservierung liegt vor Datei-Await und Clear.

`PasswordDialogHost` öffnet ShowDialog über Dispatcher.UIThread und wartet asynchron.
Save zeigt Passwort und Bestätigung, Load nur Passwort. Schliessen, Cancel, Token
und Owner-Ende führen zur Beendigung. Lifetime-Cancellation schliesst offene Dialoge.
Die Eventsubscription gilt nur für die jeweilige Execution und wird im finally gelöst;
Editor und KeyMap lösen ihre Outputsubscriptions beim Beenden. Die Dialogtests prüfen
zusätzlich einen Dispatcher-Heartbeat während einer offenen Anfrage.

## Unabhängige Review

Ein separater Reviewer prüfte Architektur, Sicherheit und Regression rein lesend.
Alle konkret gemeldeten Findings wurden korrigiert und anschliessend von ihm geschlossen:

1. Scriptbook-Clear vor Zulassungsprüfung: frühe IsExecuting-Prüfung und bookActive-
   Reservierung; Regression mit offener Passwortanforderung und konkurrierendem Open.
2. IsBusy/Enter-Rennen in Modify-Commands: direkter Lease-Erwerb mit kontrollierter
   Busy-Behandlung, zusätzlich Editor-/Scriptbook-Sperre und Kollisionstest.
3. Geerbter AsyncLocal-Kontext: direkte Zustandszugriffe benötigen nun einen kurzen
   threadgebundenen State-Scope; Regression für geerbten Kontext.
4. State-Lease über asynchronem Funktionsaufruf: entfernt; sämtliche Hostschritte
   explizit und synchron gekapselt. Der Regressionstest verifiziert, dass der
   Async-Callback keinen impliziten Statezugriff erhält.
5. Iterator-Dispose ausserhalb State-Scope: explizites geschütztes finally; Regression
   mit Dictionaryzugriff beim Cleanup und erhaltenem Canceled-Status.

Der Reviewer führte keine eigenen Tests aus. Die unten aufgeführten Tests wurden
durch den implementierenden Agenten ausgeführt. Im zuletzt nachgelesenen Stand meldete
die unabhängige Review keine weiteren konkreten Findings.

## Lokale Validierung

Windows, .NET 10, Debug; GUI-Tests mit Avalonia.Headless. Keine Aussage über eine
zusätzliche manuelle Desktop- oder Linux-/macOS-Ausführung.

| Prüfung | Ergebnis |
| --- | --- |
| Gezielte Coretests: AsyncPasswordInfrastructureTests + SemanticDiagnosticExecutionTests | 53 bestanden |
| Gezielte GUItests: AsyncPasswordGuiTests + CryptoScriptExecutionPresentationTests | 39 bestanden |
| Vollständige Core-Suite | 7453 bestanden, 0 fehlgeschlagen, 4 übersprungen (7457 gesamt) |
| Vollständige GUI-Suite | 234 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| CryptoWorkBench.sln | Build erfolgreich |
| CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln | Build erfolgreich |
| git diff --check | erfolgreich |

Testbefehle verwenden `dotnet test <csproj> --no-restore`, für gezielte Läufe den
entsprechenden FullyQualifiedName-Filter. TRX-Protokolle liegen lokal unter
`artifacts/test-results/S05-B1-{core,gui}-{focused,full}.trx` (ignorierte Artefakte).
Die finalen Solution-Builds verwenden `dotnet build <solution> --no-restore -m:1
-nr:false`. Unbeschränkte parallele Solution-Builds endeten in dieser Umgebung zunächst
mit Exitcode 1 ohne Compilerdiagnose; die seriellen Builds bestanden. Kompilierende
GUI-Testläufe meldeten bestehende Nullable-Warnungen; finale inkrementelle Builds
meldeten 0 Warnungen und 0 Fehler.

Die vier bedingten Skips betreffen `MacOsInheritedExtendedAclIsRemovedBeforePublication`,
`NativePublicationDoesNotFollowLateSymlink` und beide Fälle von
`TargetSymlinksIncludingDanglingLinksAreRejected`. Ihre plattform-/berechtigungsabhängigen
Voraussetzungen waren lokal nicht erfüllt; sie werden nicht als bestandene Tests gezählt.
Zwischenläufe ohne Fortschritt wurden abgebrochen. Ein zusätzlicher Versuch mit
Blame-Hang-Datacollector brach wegen eines VSTest-Verbindungs-Timeouts nach 90 Sekunden
ab. Der abschliessende reguläre Gesamtlauf ohne Datacollector bestand in acht Sekunden;
das finale Core-TRX enthält dieses erfolgreiche Ergebnis.

Kein Commit oder Push. `.codex/config.toml` wurde weder gelesen noch verändert.
