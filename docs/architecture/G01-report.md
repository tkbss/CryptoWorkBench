# G01 – Key Check Values: Implementierung und Prüfung

Stand: 18. Oktober 2026. Baseline: `71a5ff3af0a6c3e45aa8c3dcd93b9039ed09f27a`.
Umfang ausschließlich G01, symmetrische Kryptographie in CryptoScript V1.

Aktueller Status nach M1-Korrektur: **READY FOR FINAL REVIEW**. Die ursprünglichen
Prüfergebnisse unten bleiben als Historie erhalten; der abschließende M1-Nachtrag
enthält die aktuellen Ergebnisse und ersetzt das frühere Urteil ohne offene Befunde.

## Ergebnis und Architektur

AES-/TDEA-KEY-Objekte berechnen KCV, KCVC und KCVK aus aktuellem `Value`, dessen
Encoding und dem expliziten `KeyType`. Ein zentraler Rechner verwendet bestehende
AES-ECB-/CMAC- und TDEA-Analyseprimitiven. Keine neuen Kryptographie-Abhängigkeiten,
kein Algorithmusraten anhand der Länge, kein Cache, keine persistierten KCV-Felder.
Die volle Blocklänge bleibt erhalten. Eigene temporäre Bytepuffer werden in `finally`
bereinigt; unveränderbare .NET-Strings und interne Provider-Key-Schedules unterliegen
den bestehenden Plattform-/Providergrenzen.

Geprüfte Erzeugungspfade: AES/DES3/HMAC GenerateKey (Import und Zufall), DUKPT AES/TDEA,
EMV-KDFs, generische HKDF/SP800-108-/EP2-KDFs, TR-31-AES-/TDEA-Unwrap,
EP2-Unwrap, JSON-Deserialisierung und objektbewusste Argumentauflösung. Die Erzeuger
bleiben unverändert. Generische KDF-Ergebnisse mit Unknown-Algorithmus erhalten
bewusst keine algorithmusspezifischen KCVs. Die AES-TR-31-Altimplementierung liefert
getaggtes HEX ohne ValueFormat; der Rechner erkennt ausschließlich das Encoding
an diesem Tag, ohne Metadaten zu verändern.

Print verwendet `ResolvedCallArgument.SourceVariable` und den vorhandenen PrintEvent-
und GUI-Ausgabeweg. Check sucht im vorhandenen VariableDictionary, vergleicht Bytes
und kopiert nur bei genau einem passenden KEY. KeyType und Usage sind unveränderbare
Records; die mutable Attributliste und ihre Elemente sowie der CryptoType werden neu
angelegt. Verschiedene Variablen mit demselben Material bleiben verschiedene Kandidaten.

Fehlende/mehrdeutige Suchergebnisse werden als spezielle interne SemanticErrorException
transportiert. FunctionCallEvaluator registriert sie einmal; StatementEvaluator beendet
nur die betroffene Anweisung. Dadurch wird die Deklaration gar nicht erreicht, auch
bei verschachteltem Check. Andere Fehler behalten ihr bisheriges Verhalten.

## Dateiinventar

| Datei | Zweck |
|---|---|
| `CryptoScript/CryptoAlgorithm/KeyCheckValueCalculator.cs` | Zentrale vollständige Berechnung, Encoding-/Längenprüfung, Pufferbereinigung |
| `CryptoScript/Variables/KeyVariableDeclaration.cs` | Nicht serialisierte Getter und strukturierte vollständige KEY-Ausgabe |
| `CryptoScript/Operations/OutputOperations.cs` | KEY-Print über SourceVariable |
| `CryptoScript/Operations/KeyCheckOperations.cs` | Eingabevalidierung, Präfixsuche, Kandidatenzählung, unabhängige Kopie |
| `CryptoScript/ErrorListner/KeyCheckValueSearchException.cs` | Eng begrenzter Transport nicht blockierender Suchfehler |
| `CryptoScript/ErrorListner/SemanticDiagnosticCode.cs` | Neue stabile Codes 12 und 13; bestehende Werte unverändert |
| `CryptoScript/Model/FunctionCallEvaluator.cs` | Diagnoseübernahme und vertrauliche Behandlung von Check-Fehlern |
| `CryptoScript/Model/StatementEvaluator.cs` | Fortsetzung nach genau diesen Suchfehlern |
| `CryptoScript/Operations/OperationFactory.cs` | Objektbewusste und kompatible string[]-Registrierung |
| `CryptoScript/Operations/OperationList.cs` | Check in bestehender Funktionsliste |
| `CryptoScript/InfoDocs/Info.Types.md` | KEY-Modell, Formeln, Längen, PCI-/XFS4IoT-Einordnung |
| `CryptoScript/InfoDocs/Info.Functions.md` | Print/Check-Verträge, Diagnosen und ausführbare Beispiele |
| `CryptoWorkBenchAvalonia/Models/FunctionOverlayDictionaryModel.cs` | Check- und KEY-Print-Hilfe im vorhandenen Overlay |
| `CryptoScriptUnitTest/KeyCheckValueTests.cs` | 49 neue Fälle einschließlich unabhängiger Referenzen und Transporttests |
| `CryptoScriptUnitTest/SemanticDiagnosticSinkTests.cs` | Erweiterter stabiler Enum-Vertrag |
| `CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs` | Nicht blockierende Fehler, Variablenanzeige und vollständige Print-Ausgabe |
| `CryptoWorkBenchGuiTest/InfoCompletionProviderTests.cs` | Check-Funktionslisten-/Overlay-Vertrag |
| `docs/architecture/G01-report.md` | Dieser Abschlussbericht |

## Referenzen und Tests

Alle 15 kryptographischen Referenzwerte (5 Schlüssellängen × 3 Verfahren) wurden
unabhängig mit OpenSSL 3.6.1 vom 27. Januar 2026 erzeugt und als Konstanten eingefroren.
Die Tests verwenden nicht den zu testenden Rechner als Erwartungswertquelle.
Reproduktion: `openssl enc -aes-128-ecb -K <hexkey> -nopad -in <input> -out <result>`;
analog AES-192/AES-256 und `-des-ede`/`-des-ede3`. Für CMAC:
`openssl mac -macopt cipher:AES-128-CBC -macopt hexkey:<hexkey> -in <zero-block> CMAC`,
analog mit AES-192-CBC/AES-256-CBC/DES-EDE-CBC/DES-EDE3-CBC.
Input ist ein vollständiger Nullblock bzw. der erste Schlüsselblock. Zusätzlich ist
der unterschiedliche AES-CMAC-Wert für die leere Nachricht unabhängig festgehalten.

Abgedeckt sind vollständige und verkürzte Werte aller Verfahren/Algorithmen, Filter,
HEX-VARs, Case-Insensitivity, genau ein/kein/mehrere Kandidaten, gleiche Materialien,
mehrere Methoden eines Kandidaten, Kopierisolation, zu lange/ungültige Eingaben,
fehlgeschlagene Neudeklaration und Wiederdeklaration, verschachtelte Suchfehler,
execution-lokale Diagnosen, Weiterverarbeitung, Mutation und JSON, nicht anwendbare
Typen, schwache TDEA-Schlüssel, GenerateKey/Derive/Unwrap und Print.
TR-31-Version-D-Transporttests enthalten alle fünf AES-/TDEA-Materiallängen sowie
HMAC/Unknown; die gesamte vorhandene Suite prüft weitere Erzeuger und Regressionen.

PCI PIN Security v3.1, März 2021, Requirement 15-1 wurde in der vorhandenen lokalen
Originalausgabe geprüft. XFS4IoT CWA 17852 release v2.0 wurde in der CEN-Ausgabe
nachgelesen. Quellenlinks und klare Abgrenzung der vollständigen Analysewerte von
PCI-konformen gekürzten Ausgaben stehen in den bestehenden InfoDocs.

## Abschließende Prüfresultate

Windows, .NET SDK 10.0.401, TargetFramework net10.0, Debug:

| Prüfung | Ergebnis |
|---|---|
| `dotnet test CryptoScriptUnitTest --no-restore` | 6687 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| `dotnet test CryptoWorkBenchGuiTest --no-restore` | 179 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| `dotnet build CryptoWorkBench.sln --no-restore -t:Rebuild` | Erfolgreich, 0 Warnungen, 0 Fehler |
| `dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -t:Rebuild` | Erfolgreich, 9 Warnungen, 0 Fehler |
| Bestehende InfoDocs-/Registry-Vertragstests | In den vollständigen erfolgreichen Tests enthalten |
| `git diff --check` | Erfolgreich, keine Whitespace-Fehler |

Gesamt: 6866 erfolgreiche Tests, davon 51 neue G01-Fälle (49 Core, 2 GUI).
TRX-Nachweise: `CryptoScriptUnitTest/TestResults/g01-all.trx` und
`CryptoWorkBenchGuiTest/TestResults/g01-gui.trx` (lokal, ignoriert).
Build-/Testlogs liegen lokal unter `.tmp/g01-*.log`.

Die neun Desktop-Warnungen betreffen unveränderte Dateien:

| Code | Datei/Zeile | Anzahl/Inhalt |
|---|---|---|
| CS8618 | `ViewModels/KeyMapViewModel.cs:18` | 1: `_infoText` |
| CS8618 | `ViewModels/MainViewModel.cs:19` | 4: InfoHistoryBack/ForwardCommand, LineHistoryBack/ForwardCommand |
| CS8618 | `Views/CryptoScriptEditView.axaml.cs:25` | 1: `_insightWindow` |
| CS8625 | `Views/CryptoScriptEditView.axaml.cs:114,159` | 2: null an nicht-nullbaren Parameter |
| CS8602 | `ViewModels/SidebarViewModel.cs:140` | 1: mögliche Null-Dereferenzierung |

Anfängliche Sandbox-Testläufe brachen nach 90 Sekunden wegen fehlender Testhost-
Verbindung ab; die finalen Läufe außerhalb der Sandbox waren erfolgreich. Zwischenstände
enthielten behobene Test-/Compilerfehler (Kopiertyp, Testparameter-Vertrag, Enum-Erwartung,
Zugänglichkeit eines Testdatentyps) und eine behobene Nullable-Warnung im neuen Test.
Git meldet bei einigen Dateien lediglich die konfigurierte LF/CRLF-Konvertierung.

## Separater abschließender Review

Der Diff wurde nach der Implementierung nochmals anhand der Anforderungen auf
Objektidentität, Serialisierung, Metadatenmutation, Kandidatenzählung, verschachtelte
Fehler, Redaction, Weak-Key-Verhalten und Scope geprüft. Dies war ein separater eigener
Prüfdurchgang, kein Review durch eine zweite Person oder einen zweiten Agenten.

Behobene Befunde nach Schweregrad:

- P1: JSON-basierte Check-Kopie stellte den konkreten CryptoType nicht wieder her.
  Explizite unabhängige Kopie verhindert den Deklarationstypfehler.
- P2: Ältere AES-TR-31-Objekte ohne ValueFormat hätten keine KCVs erhalten.
  Encoding-Erkennung am HEX-Tag und Transporttests decken dies ab.
- P2: Der allgemeine alte HEX-Konverter prüft ein schließendes Tag nicht strikt.
  Der neue Rechner prüft es und verwendet `Convert.FromHexString`; Fehlformen
  erhalten keine KCVs.

Keine offenen funktionalen Befunde im abschließenden eigenen Review. Linux/macOS
wurden nicht ausgeführt; die Implementierung verwendet ausschließlich vorhandene
plattformübergreifende .NET-/Bouncy-Castle-Primitiven. Keine Änderungen an G02–G04,
keine neuen Pakete, keine Änderung an `.codex/config.toml`, kein Commit/Push/Merge.

## Nachtrag: M1 – ScriptBook-Kompatibilität der KEY-Ausgabe

Der anschließende kritische Review fand einen Medium-Befund: Der mehrzeilige
KEY-Text erhielt nur vor seiner ersten Zeile `out:`. Beim Wiederladen eines gespeicherten
ScriptBooks ignorierte der unveränderte Lader nur diese erste Zeile. `Type: KEY`
wurde als Script interpretiert, verursachte einen Syntaxfehler und verhinderte die
Ausführung nachfolgender Anweisungen. Das ursprüngliche Urteil ohne offene Befunde
war deshalb nicht ausreichend.

### Gezielte Korrektur

`OutputOperations.Print(OperationInvocation)` normalisiert ausschließlich beim
KEY-Ausgabepfad die Zeilenenden mit `ReplaceLineEndings` und setzt nach jedem
Zeilenende `out: `. Der vorhandene string[]-Print-Pfad ergänzt weiterhin das erste
Präfix, schreibt auf die Konsole und löst genau ein PrintEvent aus. Keine zusätzlichen
Zeilen oder Änderungen an Schlüsselmaterial, KCV-Berechnung, KCV-Speicherung,
Check, Serialisierung oder anderen G01-Funktionen. Einzeiliges Print bleibt unverändert.
Der ScriptBook-Lader und der produktive Avalonia-View/ViewModel-Code bleiben unverändert.

Für M1 geänderte Dateien (Zeilen im Stand dieses Nachtrags):

| Datei | Zeile | Änderung |
|---|---:|---|
| `CryptoScript/Operations/OutputOperations.cs` | 27–29 | Durchgängiges Präfix aller physischen KEY-Ausgabezeilen |
| `CryptoScriptUnitTest/KeyCheckValueTests.cs` | 243–266 | Zwei Fälle für eingebettete LF-/CRLF-Zeilenenden, ein Event, keine zusätzlichen Leerzeilen |
| `CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs` | 97–158 | Headless-Testkonfiguration und vier Editor-/Speicher-/Wiederladefälle |
| `CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj` | 12 | Testabhängigkeit Avalonia.Headless 11.3.20, passend zur vorhandenen Avalonia-Version |
| `docs/architecture/G01-report.md` | 6 und dieser Nachtrag | Aktualisierter Status, Finding und Prüfnachweise |

Die einzige neue Paketabhängigkeit liegt im GUI-Testprojekt. Sie ermöglicht einen
echten AvaloniaEdit-Editor mit UI-Dispatcher ohne natives Fenster. Es wurden keine
Produktiv- oder Kryptographie-Abhängigkeiten ergänzt.

### Regressionstest und Red-/Green-Nachweis

`KeyPrintSurvivesEditorSaveAndScriptBookReload` verwendet einen echten TextEditor
und ruft den bestehenden View-Eingabehandler auf. Nur die für diesen Handler nicht
benötigte XAML-/Navigationsinitialisierung wird ausgelassen. Der Handler führt die
KEY-Deklaration und Print-Aufrufe aus und fügt die Ausgabe in das Editor-Dokument ein.
Danach werden die öffentlichen Methoden `SaveScriptBook` und `OpenScriptBook` mit
einer temporären Datei aufgerufen; vor dem Wiederladen werden Dictionary und Editor
geleert. Die Testdatei wird anschließend entfernt.

Vier Varianten: LF/CRLF × ein/zwei aufeinanderfolgende KEY-Print-Aufrufe.
Der vorhandene Speicherpfad normalisiert auf LF. Für den CRLF-Wiederladefall wird
zusätzlich die gespeicherte Testdatei auf CRLF umgestellt. Mitgeprüft werden:

- Vollständige 15 KEY-Metadatenzeilen je Aufruf einschließlich der Legacy-Felder.
- Ungekürzte, bekannte KCV-/KCVC-/KCVK-Werte und vollständiges Schlüsselmaterial.
- Unverändertes einzeiliges `Print(0x(AB))`.
- Keine leeren Ausgabezeilen und vollständige Übernahme beim Wiederladen.
- `LastScriptBookSucceeded == true`, keine Syntax-/semantischen Diagnosen,
  korrekt registrierte Folgevariable `after` mit Wert `0x(CAFE)`.

**Vor der Produktivkorrektur:** Alle vier neuen Integrationstestvarianten scheiterten
am erwarteten `LastScriptBookSucceeded == true` mit tatsächlichem Wert false.
**Nach der Korrektur:** Alle vier Varianten bestanden unverändert. Damit ist die
Reproduktion des ursprünglichen M1-Fehlers und dessen Behebung belegt.

Die zwei zusätzlichen Core-Testfälle prüfen LF und CRLF innerhalb eines Metadatenwerts,
genau einen Event-Aufruf, Markierung aller physischen Zeilen sowie fehlende zusätzliche
Leerzeilen. Eine anfänglich falsche erwartete Zeilenanzahl im neuen Test wurde von
17 auf 16 berichtigt (15 Felder plus eine zusätzliche Metadatenzeile).

### Aktuelle Abschlussprüfungen

Windows, .NET SDK 10.0.401, net10.0, Debug:

| Prüfung | Ergebnis |
|---|---|
| Vollständige Core-/Integrationssuite | 6689 bestanden, 0 fehlgeschlagen, 0 übersprungen; einschließlich aller 51 G01-Core-Fälle |
| Vollständige GUI-Suite | 183 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Vollständiger Rebuild `CryptoWorkBench.sln` | 0 Warnungen, 0 Fehler |
| Vollständiger Rebuild `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | 9 bekannte Warnungen, 0 Fehler |
| `git diff --check` | Erfolgreich |

Insgesamt 6872 erfolgreiche Tests. Die neun Desktop-Warnungen sind dieselben oben
aufgelisteten CS8618-/CS8625-/CS8602-Warnungen in unveränderten Dateien. Keine neuen
Build-Warnungen durch M1. Die Avalonia-Accelerate-Lizenz-/Telemetriehinweise im
Buildlog sind keine Compilerwarnungen.

Lokale Nachweise: `.tmp/g01-m1-red.log`, `.tmp/g01-m1-green.log`,
`.tmp/g01-m1-unit.log`, `.tmp/g01-m1-gui.log`, `.tmp/g01-m1-build-core.log`,
`.tmp/g01-m1-build-gui.log`, `CryptoScriptUnitTest/TestResults/g01-m1-all.trx` und
`CryptoWorkBenchGuiTest/TestResults/g01-m1-gui.trx`.

Scope-Prüfung: Für M1 ausschließlich die fünf oben genannten Dateien verändert;
die übrigen vorhandenen G01-Änderungen bleiben erhalten. Keine Änderung des Laders
erforderlich. Keine offenen Findings aus der gezielten M1-Nachprüfung. Der finale
Review steht noch aus. `.codex/config.toml` wurde weder gelesen noch verändert oder
aufgenommen. Kein Commit, Push oder Merge.

**Status: READY FOR FINAL REVIEW.**
