# CryptoScript AP5 Abschlussbericht

Die freigegebenen Metadatenmigrationen sind implementiert, unabhängig reviewed und unter Windows validiert. Der uneingeschränkte AP5-Gesamtabschluss bleibt wegen ausdrücklich offener Protokoll- und Backend-Entscheidungen aus. Keine dieser Entscheidungen wurde durch eine Warning oder eine automatische Verfahrenswahl ersetzt.

AP5-Basisstand: 16. Oktober 2026; AP5-A-Ergänzung: 17. Oktober 2026. Entscheidung A ist verbindlich getroffen und AP5-A abgeschlossen; der aktuelle AP5-A-Abschluss mit zwölf Berichtspunkten steht am Ende dieses bestehenden Dokuments. Repository tkbss/CryptoWorkBench, Branch master, HEAD dabcddf8320d40f6d824d1918bea70e255e52acc.

## 1 Vollständiges Analyseinventar

[AP5-analysis.md](AP5-analysis.md) enthält die vollständige Tabelle mit Mechanismus, Funktion, Ausgangsdatei und Zeile, bisheriger Prüfung, genau einer Kategorie M/A/P/T, vorgeschlagenem Verhalten, Warning-Code, technischem Risiko und erforderlichen Tests.

[AP5-final-inventory.md](AP5-final-inventory.md) enthält das zweite vollständige Inventar mit Abschlussstatus und Testnachweis. Beide Tabellen besitzen dieselben 66 eindeutigen IDs. Der unabhängige Reviewer hat IDs, Mechanismen, Funktionen, Ausgangsreferenzen, Kategorien und Abschlussstatus gegeneinander abgeglichen. Kein identifizierter Eintrag ist verschwunden.

Kein eigenständiger KeyPurpose-Vertrag vorhanden. Verwendet werden die bestehenden KeyType- und KeyUsagePolicy-Verträge sowie die protokolleigenen Parameter; keine neue KeyPurpose-Architektur.

## 2 Implementierte Änderungen nach Mechanismusfamilie

| Familie | Implementierung |
|---|---|
| HKDF Full/Extract | DeriveKey-Diagnose. IKM ist Rohmaterial; keine falsche Hmac-Typanforderung, denn Extract verwendet den Salt als HMAC-Schlüssel. |
| HKDF Expand | Erwarteter Hmac-PRK und DeriveKey; Hash-/Längenwahl unverändert. |
| SP800-108 Counter | Erwarteter Algorithmus durch #PRF festgelegt: Aes für AES-CMAC, sonst Hmac; DeriveKey-Diagnose. Kein Wechsel des PRF. |
| EP2 Session und Receipt TRM | DeriveKey für das Extract-Eingangsmaterial; keine Algorithmusanforderung an die IKM-Deklaration. |
| EP2 Surrogate TRX und Receipt TRX | Hmac für Expand-PRKs; DeriveKey. Keine neue Composite-Funktion und keine Änderung von Info, Salt, Hash oder Ergebnissen. |
| DUKPT AES Initial/Working | Aes und DeriveKey warnen. Bisherige Working-Key-Typblockade entfernt; #USAGE/#KEYTYPE, Schlüsselstärke, Zwischenableitungen, Derivation Data und Ergebnis-Usage bleiben unverändert. |
| DUKPT TDEA Initial/Working | Tdea und DeriveKey warnen; AP4.1-Qualitätsdiagnose. Initial nutzt nur lokal die bereits vorhandene DES3_ECB-Analyseprimitive. Working bleibt beim bestehenden BC-Verfahren und unveränderten Varianten. |
| EMV Master A/B/C | Feste Mechanismen erwarten Tdea/Tdea/Aes und DeriveKey. Deklarative Typblockaden durch Warnungen ersetzt; PAN/PSN/KeySize/Referenzvertrag erhalten. |
| EMV AC Session | DeriveKey-Warnung; bestehender KeyAlgorithm-Selektor unverändert. |
| EMV AC/VERIFY/ARPC 1/2 | MacGenerate für AC und beide ARPC-Methoden; MacVerify für AC-VERIFY. Nur Usage-Diagnose, unveränderte AES/TDEA-Auswahl und Fail-Closed-Verifikation. |
| HMAC alle 11 | Hmac und MacGenerate; Original-KEY aus AlgorithmCallArguments statt wertbasierter Rekonstruktion. |
| TR-31 AES D und TDEA A/B/C | Nur KBPK: Aes/Tdea, WrapKey bzw. UnwrapKey. Originalreferenzen bevorzugt, eindeutige Legacy-Werte/IDs weiter möglich, mehrdeutige Legacy-Werte blockieren. AES-Unwrap mutiert die Eingabe-VAR nicht mehr. Der transportierte KEY benötigt keine KBPK-Usage. |

Ausschliesslich bestehende Validatoren, Codes, Locations und Sink-/Invocation-Verträge verwendet. Kein globaler Diagnosezustand, keine Mutation von Eingabe-KEY/PARAM, keine neue öffentliche produktive API. Typwarnung kommt vor Usage-Warnung; danach gegebenenfalls DES-Qualitätswarnungen. Unknown und Unspecified bleiben bei rein deklarativen Prüfungen warnungsfrei. Ein algorithmusbestimmendes Unknown in EMV bleibt dagegen ein Selektionsfehler.

## 3 Bewusst unverändert belassene technische Prüfungen

KEY-/Datenformat und Decodierung, tatsächliche Schlüssellängen, bekannte KeySize-Konsistenz, KSN/ATC/PAN/PSN, DUKPT-Stärkegrenzen und Parameterverträge bleiben blockierend. Die KDF-Details sind je Mechanismus im Inventar aufgeführt.

TR-31-Headerstruktur, Version, authentisierte Algorithmus-/Usage-Felder, KBEK/KBAK-Verfahren, Block-/MAC-Längen, Obfuskationsvorgaben und Authentisierungsprüfung bleiben unverändert. Fehlgeschlagene Authentisierung ist Kategorie T und gibt keinen KEY zurück.

ARQC-Verifikation bleibt Fail-Closed: ein falsches erwartetes Kryptogramm wirft weiterhin einen Fehler und verhindert folgende Scriptanweisungen. ARPC-Berechnung verifiziert das empfangene ARQC weiterhin nicht selbst; die vorhandene Transaktionspolicy des Aufrufers bleibt erforderlich.

Gemeinsame strikte DES3-Cores wurden nicht gelockert. Degenerierte Schlüssel bleiben in EMV Master A/B, AC Session, AC/ARPC und TDEA-TR-31-KBPK-Pfaden blockierend. Die Adapter berichten vorhandene Qualitätsdiagnosen; diese verdecken den anschliessenden technischen Fehler nicht. DUKPT-TDEA-Initial ist die lokal begrenzte Ausnahme mit bestehendem AP4.1-Analysemodus.

## 4 Ergebnis des unabhängigen Reviews

Separater Reviewer mit eigenständigem Auftrag; tatsächlichen Diff, neue Dateien, relevante unveränderte Aufrufer, Diagnosetransport und EMV-/TR-31-Cores gelesen. Das Review wurde nicht auf eine Bestätigung der Implementierungsbegründung begrenzt.

| ID | Schwere | Fundstelle im ursprünglichen Reviewstand | Technische Begründung | Minimale Korrektur |
|---|---|---|---|---|
| R1 | Medium | CryptoScriptUnitTest/Ap5SemanticDiagnosticsTests.cs:368 | Weak-Komponenten mit K1 != K2 erreichen die neue Degenerations-Ausweichprimitive nicht; Typprüfung allein belegt keine festen Ableitungsbytes. | Degenerierten BDK mit unabhängigem festem IPEK, Sink/ohne Sink und Mutation prüfen. |
| R2 | Medium | docs/architecture/AP5-analysis.md:58 | Authentisierungsfehler waren unter P gebündelt; HMAC-T-Prüfungen fehlten, KDF-T-Zeilen zu pauschal. | 20T/21U/22U ergänzen, technische T-Prüfungen von Protokollentscheidungen trennen und KDF-Prüfstellen konkretisieren. |
| R3 | Low | docs/architecture/AP5-analysis.md:66 | DUKPT-Initial-Eintrag beschrieb eine fortbestehende Blockade trotz lokal aktivierter Analyseprimitive. | 24T an den begrenzten tatsächlichen Implementierungsumfang angleichen. |

Critical: 0. High: 0. Alle zwei Medium- und ein Low-Finding bestätigt und behoben. Keine offenen bestätigten Codefindings.

## 5 Korrekturen und Nachreview

R1: neuer degenerierter BDK 0123456789ABCDEF0123456789ABCDEF; festes IPEK 7140297ECB0DD8F1D6D854E305FB4129. Unabhängig mit .NET Single-DES für Eingangsblock FFFF9876543210E0 und Komponenten 0123456789ABCDEF bzw. C1E385A789ABCDEF berechnet. Produktiv nutzt die bereits vorhandene Bouncy-Castle-EDE-Ausweichprimitive. Der Reviewer hat denselben festen Wert unabhängig nachgerechnet. Der Test prüft direkte Aufrufe mit/ohne Sink, Runner, Legacy, Metadaten-/Qualitätswarnungen sowie unveränderte KEY/PARAM/Daten.

R2/R3: Analyseinventar korrigiert, alle ursprünglichen IDs erhalten und zweite Tabelle vollständig erstellt. Der Reviewer hat beide Tabellen mit je 66 eindeutigen IDs sowie ihre Status unabhängig abgeglichen.

Unabhängiger Nachtest: alle 328 AP5-Tests erfolgreich, Exit 0. Nachreview der Korrekturen und der kritischen unveränderten Pfade abgeschlossen; keine weiteren notwendigen Korrekturen. Die sachlich berechtigten offenen Architekturgrenzen bleiben ausdrücklich offen.

## 6 Vollständige Test und Build Ergebnisse

| Prüfung | Ergebnis | Nachweis |
|---|---|---|
| Gesamte CryptoScriptUnitTest-Suite | 4’998 bestanden, 0 fehlgeschlagen, 0 übersprungen | TestResults/AP5/AP5-unit.trx |
| Gesamte GUI-/Konsolensuite | 174 bestanden, 0 fehlgeschlagen, 0 übersprungen | TestResults/AP5/AP5-gui-console.trx |
| Unabhängiger AP5-Nachtest | 328/328 bestanden | Separater Review-Agent, Exit 0 |
| Root-Solution-Build | Erfolgreich, final 0 Fehler / 0 Warnungen | TestResults/AP5/root-build.log |
| Avalonia-/Desktop-Build | Erfolgreich, final 0 Fehler / 0 Warnungen | TestResults/AP5/desktop-build.log |
| git diff --check | Erfolgreich | Abschlussprüfung ohne Befund |
| Dokumentations-/Navigationsregressionen | Erfolgreich innerhalb beider vollständigen Testsuiten | Info-Dokumentationsverträge, Deployment, Beispiele, GUI-Info und Completion |
| Git-Status und Index | Erwartete lokale Änderungen; Index leer; HEAD unverändert | Abschnitt 10 |

Die vollständigen Suiten enthalten alle bestehenden EP2-, AES-/TDEA-DUKPT-, EMV-Master-A/B/C-, AC-Session-, ARQC-/ARPC-, TR-31-A/B/C/D-, Authentisierungsnegativ-, AES-/DES3- und ISO-9564-PIN-Block-0–4-Referenzen. Die neuen AP5-Tests prüfen feste veröffentlichte bzw. bereits unabhängig charakterisierte Werte, keine alleinigen Roundtrips. Die Metadatenmatrix umfasst Typkonflikt, Usage-Konflikt, beide, korrekt, Unknown/Unspecified; algorithmusbestimmende EMV-Typen bleiben explizit aus der Typmigration ausgenommen. Aufrufvarianten, verschachtelte Operationen, gleichwertige KEY-Objekte, Warnungen vor technischem Fehler, Authentisierung und Mutation sind abgedeckt.

Validierung unter Windows, .NET SDK 10.0.401. Linux/macOS wurden lokal nicht ausgeführt; es wurde keine neue CI gestartet, da kein Push erfolgt. Keine plattformspezifischen Abhängigkeiten eingeführt.

Die Standard-MSBuild-Workeraufrufe endeten in dieser Ausführungsumgebung teilweise mit Exit 1 ohne Fehlerdiagnose. Die abschliessenden Root-/Desktop-Builds liefen mit `-m:1 -nr:false` erfolgreich. Einzelne Testrunner-Aufrufe ohne Abschluss wurden beendet und sauber wiederholt; die oben genannten vollständigen Abschlussläufe lieferten jeweils Exit 0. Der erste GUI-Build meldete vorhandene Nullable-Warnungen in unveränderten GUI-Dateien; die finalen inkrementellen Builds meldeten 0 Warnungen. Keine Buildkonfiguration dafür verändert.

## 7 Dokumentationsänderungen

33 betroffene Info-Seiten ergänzt: erwartete KeyType-Metadaten und KeyUsage, bestehende Warning-Codes, nicht blockierende Konflikte, weiterhin blockierende technische Fehler und algorithmusbestimmende Parameter. Veraltete Aussagen über strikt abgelehnte Typkonflikte in DUKPT und EMV Master A/B/C korrigiert. Die gemeinsame Abschnittsstruktur der Dokumentationsverträge bleibt erhalten.

Analyseinventar, Abschlussinventar und dieser Abschlussbericht liegen unter docs/architecture. Keine PCI-/EP2-Konformität aus Warnungen abgeleitet und die EP2-Sicherheitsmatrix nicht als abgeschlossen markiert. Keine Änderungen an ANTLR, GUI-Design, asymmetrischer Kryptografie oder V2.

## 8 Verbleibende Architekturentscheidungen

| Entscheidung | Betroffene IDs | Erforderliche Freigabe vor Änderung |
|---|---|---|
| EMV-Algorithmusauswahl verbindlich entschieden | 15A–19A | Eigentümerentscheid vom 17.10.2026: KEY.KeyType.Algorithm bleibt algorithmusbestimmender AES-/TDEA-Selektor. Unknown ist Fehler; kein #ALGORITHM und keine Längenheuristik. Kategorie A bleibt erhalten. |
| TR-31-Obfuskationsquelle bei widersprüchlichem KEY-Typ/Header | 23P | Festlegen, welche unabhängige Protokollquelle den enthaltenen Algorithmus und damit die Obfuskation bestimmt. Vorher keine Änderung des authentisierten Blockaufbaus. |
| Degenerierte DES3-Schlüssel in bisher strikten EMV-/TR-31-Pfaden | 25T–30T | Einen begrenzten Analysebackend-Vertrag für ECB/CBC/MAC und seine Core-Aufrufer festlegen. Vorhandene gemeinsame NoPadding-/Core-Verträge nicht global lockern. |

Es ist keine neue KeyPurpose-Architektur erforderlich. Referenzvektoren weichen nicht unerwartet ab. Alle freigegebenen unabhängigen M-Arbeiten sind abgeschlossen; die weiterhin offenen P-/T-Änderungen wurden vor ihrer Umsetzung gestoppt. Entscheidung A ist inzwischen ausdrücklich durch Beibehalten des bisherigen Selektors entschieden.

## 9 AP5 Dateiliste vom 16 Oktober 2026

66 AP5-Dateien: 21 produktive C#-Dateien, 9 Testdateien, 33 Info-Seiten und 3 Architektur-/Berichtsdateien. Die vorhandene unversionierte .codex-Konfiguration gehört nicht zu den AP5-Änderungen.

- CryptoScript/CryptoAlgorithm/AlgorithmArgumentResolver.cs
- CryptoScript/CryptoAlgorithm/EMV/MAC_EMV_AC.cs
- CryptoScript/CryptoAlgorithm/EMV/MAC_EMV_ARPC_METHOD_1.cs
- CryptoScript/CryptoAlgorithm/HMAC/HMAC.cs
- CryptoScript/CryptoAlgorithm/KDF/DUKPT_AES_INITIAL_KEY.cs
- CryptoScript/CryptoAlgorithm/KDF/DUKPT_AES_WORKING_KEY.cs
- CryptoScript/CryptoAlgorithm/KDF/DUKPT_TDEA_INITIAL_KEY.cs
- CryptoScript/CryptoAlgorithm/KDF/DUKPT_TDEA_WORKING_KEY.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_AC_SESSION.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_A.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_B.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_C.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_PAN_RECEIPT_TRM.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_PAN_RECEIPT_TRX.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_PAN_SURROGATE_TRX.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EP2_SESSION.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_HKDF.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_SP800_108_COUNTER.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/WrapAESTR31.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/WrapDES3TR31.cs
- CryptoScript/InfoDocs/Info.Mech.DUKPT-AES-INITIAL-KEY.md
- CryptoScript/InfoDocs/Info.Mech.DUKPT-AES-WORKING-KEY.md
- CryptoScript/InfoDocs/Info.Mech.DUKPT-TDEA-INITIAL-KEY.md
- CryptoScript/InfoDocs/Info.Mech.DUKPT-TDEA-WORKING-KEY.md
- CryptoScript/InfoDocs/Info.Mech.HKDF-EXPAND.md
- CryptoScript/InfoDocs/Info.Mech.HKDF-EXTRACT.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA1.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA224.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA256.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-224.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-256.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-384.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-512.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA384.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA512-224.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA512-256.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA512.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-AC-SESSION.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-A.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-B.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-C.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EP2-PAN-RECEIPT-TRM.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EP2-PAN-RECEIPT-TRX.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EP2-PAN-SURROGATE-TRX.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EP2-SESSION.md
- CryptoScript/InfoDocs/Info.Mech.KDF-HKDF.md
- CryptoScript/InfoDocs/Info.Mech.KDF-SP800-108-COUNTER.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-AC-VERIFY.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-AC.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-ARPC-METHOD-1.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-ARPC-METHOD-2.md
- CryptoScript/InfoDocs/Info.Mech.WRAP-AES-TR31.md
- CryptoScript/InfoDocs/Info.Mech.WRAP-DES3-TR31.md
- CryptoScript/Model/KeyMetadataValidator.cs
- CryptoScriptUnitTest/AlgorithmCallArgumentsTests.cs
- CryptoScriptUnitTest/Ap5SemanticDiagnosticsTests.cs
- CryptoScriptUnitTest/DukptAesWorkingKeyTests.cs
- CryptoScriptUnitTest/DukptTdeaWorkingKeyTests.cs
- CryptoScriptUnitTest/EmvAcVerifyMechanismTests.cs
- CryptoScriptUnitTest/EmvMasterOptionAMechanismTests.cs
- CryptoScriptUnitTest/EmvMasterOptionBMechanismTests.cs
- CryptoScriptUnitTest/EmvMasterOptionCMechanismTests.cs
- CryptoScriptUnitTest/SymmetricAlgoHmacTests.cs
- docs/architecture/AP5-analysis.md
- docs/architecture/AP5-final-inventory.md
- docs/architecture/AP5-report.md

Build-/Testprotokolle unter TestResults/AP5 sind generierte ignorierte Prüfartefakte und gehören nicht zum vorgesehenen Source-Diff.

## 10 AP5 Git Status vom 16 Oktober 2026

Branch master; HEAD weiterhin dabcddf8320d40f6d824d1918bea70e255e52acc. Index leer. 62 bereits versionierte Dateien lokal verändert; vier neue AP5-Dateien unversioniert: Ap5SemanticDiagnosticsTests.cs und die drei AP5-Dokumente. Das bereits vor Beginn unversionierte .codex/ bleibt vorhanden. .codex/config.toml wurde weder gelesen noch verändert.

Keine Commits, Pushes oder Merges. Keine neue Branch-/Worktree-/ANTLR-/Backend-Architekturentscheidung vorgenommen.

**Semantic Diagnostics AP5 technisch abgeschlossen: NEIN**

Die freigegebenen Änderungen sind grün getestet und unabhängig reviewed; der vollständige AP5-Abschluss würde jedoch die offenen TR-31- und DES3-Backend-Entscheidungen übergehen.

**AP5 commitfähig: NEIN**

Als vollständig abgeschlossenes AP5-Paket noch nicht freigabereif, solange diese ausdrücklich angeforderten Entscheidungen offen sind. Der implementierte, unabhängig reviewed Teilstand ist technisch vorbereitet und bleibt zur Prüfung uncommitted.


## AP5A Abschluss vom 17 Oktober 2026

AP5-A ist implementiert, unabhängig reviewed und vollständig unter Windows validiert. Die folgenden zwölf Punkte ergänzen den erhaltenen AP5-Bericht vom 16. Oktober 2026. Der Gesamtabschluss bleibt wegen 23P und 25T–30T offen.

### 1 Vollständige Registry und GenerateKey Inventarisierung

Die vollständige Tabelle mit Mechanismus, Algorithmus, Materialart, Implementierung und Längen steht in [AP5-analysis.md](AP5-analysis.md) unter »Vollständiges GenerateKey Inventar«. Registry und tatsächliche Implementierungen stimmen überein:

| Familie | Sämtliche freigegebenen Mechanismen | Resultierendes KEY |
|---|---|---|
| AES | AES-CBC, AES-CBC-MAC, AES-CCM, AES-CMAC, AES-CTR, AES-ECB, AES-GCM, AES-GMAC | Secret(Aes), 128/192/256 Bits; HEX-Import 16/24/32 Bytes |
| DES3 | DES3-CBC, DES3-CMAC, DES3-ECB, DES3-RETAIL | Secret(Tdea), 128/192 Bits; HEX-Import 16/24 Bytes |
| HMAC | HMAC-SHA1, HMAC-SHA224, HMAC-SHA256, HMAC-SHA3-224, HMAC-SHA3-256, HMAC-SHA3-384, HMAC-SHA3-512, HMAC-SHA384, HMAC-SHA512, HMAC-SHA512-224, HMAC-SHA512-256 | Secret(Hmac), positive Vielfache von 8 Bits; nicht leerer HEX-Import |

Damit besitzen genau 23 Mechanismen eine GenerateKey-Freigabe: acht AES-, vier DES3- und elf HMAC-Mechanismen. Alle übrigen Registry-Einträge unterstützen GenerateKey nicht, insbesondere HASH, KDF, TR-31-/PIN-Wrapper und EMV-MAC. Unbekannte Mechanismen sind ebenfalls Fehler. Zufall und Import ergeben denselben Algorithmustyp.

### 2 Implementierte Vertragsprüfung

MechanismRegistryEntry besitzt die unveränderliche Angabe GeneratedKeyAlgorithm. Bei GenerateKey-Freigabe verlangt der Konstruktor genau einen bekannten symmetrischen Algorithmus; ohne Freigabe darf kein Generatoralgorithmus angegeben werden. Die einzige produktive Zuordnung steht unmittelbar in den vorhandenen Registry-Einträgen.

Der interne GenerateKeyContract prüft Mechanismusexistenz, Registry-Funktionsfreigabe und eindeutigen Algorithmus. Konkrete Implementierungen prüfen zusätzlich ihre passende Familie. CryptoOperations.GenerateKey validiert vor dem Factory-Aufruf. Runner, OperationInvocation, direkter params-string-Aufruf und Legacy-OperationFactory verwenden somit dieselbe zulässige Semantik. Direkte Algorithmusaufrufe können diese Freigabe nicht umgehen. Gross-/Kleinschreibung und #MECH:-Normalisierung bleiben auf den direkten Wegen unterstützt.

### 3 Änderungen an den Algorithmusklassen

AES, DES3 und HMAC prüfen die zentrale Registry-Freigabe vor Zufallsgenerierung und Import. Ihre kryptografischen Generatoren, zulässigen Längen, sichere DES3-Zufallsgenerierung und KEY-Serialisierung bleiben erhalten.

Der lokale GenerateKey-Importdecoder prüft die vollständige HEX-Schreibweise einschliesslich Schlussklammer, gerader Zeichenzahl und gültiger Hexziffern. Gültige importierte KEY-Strings und Bytes bleiben exakt erhalten, auch bei schwachen oder paritätsfehlerhaften DES3-Testschlüsseln. Der gemeinsame Legacy-Formatdecoder wurde nicht verändert.

CryptoAlgorithm.GenerateKey wirft bei fehlender Implementierung statt eines leeren KEY. Die symmetrische und die vorhandene asymmetrische Stub-Basisklasse delegieren an diese Ablehnung. Es wurde keine asymmetrische Kryptografie implementiert oder in die Factory aufgenommen. HASH behält seine direkte explizite Ablehnung; der Operationsweg lehnt bereits über die Registry ab.

AES übernimmt die validierte tatsächliche Größe in KeySizeInBits. Dadurch erhalten auch akzeptierte direkte Größenangaben wie » 128«, »+128« und »128 « korrekt KeySize=128 und KeySizeInBits=128. Dieser minimale Fix stammt aus dem unabhängigen Review.

AlgorithmFactory selbst sowie sämtliche produktiven EMV-/KDF-/TR-31-Cores sind in AP5-A unverändert. Der Hashvergleich gegen den gesicherten AP5-Teilstand bestätigt keine Abweichung in den geschützten EMV-, KDF- und WRAPPERS-Quellen.

### 4 Ergänzte Info Dokumentation

23 Generator-Info-Seiten beschreiben GenerateKey, Zufallsgenerierung und Import, Secret(Aes/Tdea/Hmac), Längen, exakten Byteerhalt und spätere Verwendung in kompatiblen Mechanismen derselben Familie. Ein AES-ECB-erzeugtes KEY ist beispielsweise nicht dauerhaft auf ECB beschränkt. Ein widersprüchlicher älterer AES-CBC-Satz zur KEY-/PARAM-Modusbindung wurde korrigiert.

Fünf EMV-Info-Seiten erklären den verbindlichen KeyType-Selektor, Unknown als Fehler, fehlende Längenheuristik, nicht blockierende Usage-Warnungen sowie zusätzliche KEY-Quellen Derive, Unwrap, JSON und Legacy. Diese Mechanismen erhalten keine GenerateKey-Freigabe. Kein #ALGORITHM-Parameter und keine neue KeyPurpose-Architektur.

Die Dokumentationstests verwenden alle Registry-Einträge und deren DocumentationFileName. Sie prüfen exakte Dateinamen einschliesslich Gross-/Kleinschreibung sowie GenerateKey-Funktionszeilen, Algorithmusangabe, Materialart und Modusunabhängigkeit. Bestehende Abschnitte und ausführbare Beispiele bleiben erhalten.

### 5 Neue und angepasste Tests

GenerateKeyContractTests.cs enthält 991 neue Fälle, EmvAlgorithmSelectionContractTests.cs weitere 21: insgesamt 1’012 zusätzliche Tests.

Die Generator-Matrix umfasst alle 23 Mechanismen, Familienlängen, Zufall/Import, Secret-Material, tatsächliche und typisierte Länge, unveränderte Importbytes, JSON-Roundtrip sowie Nutzung in einem anderen kompatiblen Modus oder HMAC-Digest. Sie führt Runner, OperationInvocation, direkte CryptoOperations-, AlgorithmFactory- und Legacy-Aufrufe aus.

Negative Fälle betreffen alle nicht freigegebenen Registry-Einträge, unbekannte Namen, falsche Implementierungsfamilien, inkonsistente Registry-Metadaten, Basisklassen, ungültige Längen und fehlerhafte/unvollständige HEX-Werte. Externe schwache/paritätsfehlerhafte DES3-Imports bleiben bytegenau erhalten.

EMV prüft feste AES-128/192/256- und TDEA-Hierarchien über Session, AC, ARQC-Verifikation und ARPC 1/2 mit nicht blockierenden Usage-Warnungen. Dieselben 16 Master-Key-Bytes ergeben abhängig allein von Aes/Tdea die jeweils fest erwarteten unterschiedlichen Session Keys. Unknown aus typisiertem oder altem JSON ohne Algorithmus bleibt bei 16/24/32 Bytes in allen fünf Selektorpfaden ein Fehler; Folgeanweisungen laufen danach nicht weiter.

Zwei bestehende Tests wurden eng angepasst: die HASH-Ablehnung an den zentralen FunctionContract-Fehler und ein synthetischer GenerateKey-Registryeintrag an die neue Algorithmusangabe. Alle bisherigen 4’998 CryptoScript-Tests bleiben erfolgreich.

### 6 Ergebnis des unabhängigen Reviews

Ein separater Reviewer las den tatsächlichen Diff, die AP5-A-Differenz gegenüber .tmp/AP5A-baseline, neue Dateien und unveränderte Aufrufer. Prüfschwerpunkte: Registry/Runtime, sämtliche Generatorwege, Basis-/Familienumgehungen, Byteerhalt, DES3 Random/Import, unveränderte EMV-/TR-31-/Backend-Pfade, 28 Info-Seiten und Inventarstatus.

Critical: 0. High: 0. Medium: 1 bestätigt und korrigiert. Low: 0. Keine offenen bestätigten Findings.

### 7 Korrigiertes Finding und Nachreview

| Schwere | Fundstelle im Reviewstand | Technische Begründung | Minimaler Fix |
|---|---|---|---|
| Medium | CryptoScript/CryptoAlgorithm/AES/AES.cs:31 | Convert.ToInt32 akzeptiert » 128«/»+128«, der bisherige KeySize-Setter konnte diese Zeichenfolge aber nicht kanonisch parsen. Ergebnis: 16 Bytes mit unbekannter KeySizeInBits. | Die bereits validierte tatsächliche Bitzahl in KeySizeInBits übernehmen; akzeptierte Eingaben bleiben zulässig. |

Drei Schreibweisentests prüfen je vier direkte Aufrufwege. Der Reviewer prüfte zusätzlich die frisch gebaute Assembly direkt: alle drei Schreibweisen ergeben KeySize=128, TypedBits=128 und 16 Bytes. Das Finding ist geschlossen.

Unabhängiger Nachtest: 1’012/1’012 neue AP5-A-Tests erfolgreich, Exit 0. Der zusätzliche feste TDEA-Sessionwert 07547F1BAF714A4F608E9FE0D19938A5 wurde unabhängig mit .NET TripleDES ECB und explizitem K1/K2/K1 bestätigt. Keine Anpassung produktiver EMV-Algorithmen oder des ARQC-Fail-Closed-Verhaltens erforderlich.

### 8 Vollständige Test und Build Ergebnisse nach Review

| Prüfung | Ergebnis | Nachweis |
|---|---|---|
| Neue Generator-/EMV-Vertragstests | 1’012 bestanden, zusätzlich unabhängig 1’012/1’012 | TestResults/AP5A/AP5A-targeted.trx und Review-Agent |
| Gesamte CryptoScript-Suite | 6’010 bestanden, 0 fehlgeschlagen, 0 übersprungen | TestResults/AP5A/AP5A-unit.trx |
| Gesamte GUI-/Konsolensuite | 174 bestanden, 0 fehlgeschlagen, 0 übersprungen | TestResults/AP5A/AP5A-gui-console.trx |
| Root-Solution-Build | Erfolgreich, 0 Fehler / 0 Warnungen | TestResults/AP5A/root-build.log |
| Avalonia-/Desktop-Build | Erfolgreich, final 0 Fehler / 0 Warnungen im inkrementellen Build | TestResults/AP5A/desktop-build.log |
| GUI-Testprojekt-Build | Erfolgreich; neun bestehende Nullable-Warnungen in unveränderten GUI-Dateien | TestResults/AP5A/gui-build.log |
| Dokumentation und Navigation | Erfolgreich in beiden vollständigen Testsuiten und der Registry-Dokumentationsmatrix | Info-/Completion-/Navigationstests und Generator-Dokumentation |
| git diff --check | Erfolgreich; neue Dateien zusätzlich auf Whitespace geprüft | Abschlussprüfung |
| Schutz des AP5-Teilstands | Keine unerwartete Dateiveränderung; EMV/KDF/WRAPPERS bytegleich zur Sicherung | SHA256-Vergleich gegen .tmp/AP5A-baseline |
| Inventarregression | 66 eindeutige IDs in beiden Tabellen, unverändert vollständig | Unabhängiger und automatischer Vergleich |

Die vollständigen Suiten umfassen bestehende KeyType-/KeyUsage-, AES-/DES3-/HMAC-, EMV-Session-/AC-/ARPC-, AP5-, EP2-, DUKPT-, TR-31- und PIN-Block-Referenz-/Negativtests. Keine unerwartete kryptografische Referenzabweichung.

Builds: --no-restore -m:1 -nr:false. Tests anschliessend gegen gebaute Artefakte mit --no-build. Ein erster kombinierter Testaufruf blieb nach dem Runner-Start ohne Abschluss und wurde beendet; die getrennten vollständigen Abschlussläufe lieferten Exit 0. Windows, .NET SDK 10.0.401; keine Linux-/macOS-Ausführung behauptet, keine neue Plattformabhängigkeit eingeführt.

### 9 Aktualisierte Architekturentscheidung 15A bis 19A

**Entschieden – bisheriger Selektor beibehalten.** KEY.KeyType.Algorithm bestimmt verbindlich AES oder TDEA für KDF-EMV-AC-SESSION, MAC-EMV-AC, MAC-EMV-AC-VERIFY und beide ARPC-Methoden. Ihre Kategorie A bleibt bestehen, da die Eigenschaft die Berechnung auswählt.

GenerateKey bestimmt den Algorithmus für Zufall und Import durch seinen freigegebenen Registry-Mechanismus. Weitere KEY-Quellen können Unknown enthalten; diese Fälle bleiben Auswahlfehler. Kein zusätzliches #ALGORITHM und keine Auswahl aus Bytezahl. KeyUsage bleibt eine nicht blockierende Diagnose.

### 10 Verbleibende AP5 Entscheidungen

23P bleibt offen: verbindliche TR-31-Obfuskationsquelle bei widersprüchlichem KEY-Typ/Header.

25T–30T bleiben offen: begrenzter Analysebackend-Vertrag für degenerierte DES3-Schlüssel in bisher strikten EMV-/TR-31-Pfaden.

AP5 bleibt deshalb insgesamt offen. AP5-A verändert weder diese Protokollregeln noch den DES3-Analysebackend-Vertrag.

### 11 Vollständige Liste der durch AP5A ergänzten oder erneut geänderten Dateien

44 AP5-A-Dateien: neun produktive C#-Dateien, vier Testdateien, 28 Info-Seiten und die drei vorhandenen AP5-Architekturdateien. GenerateKeyContract.cs und die beiden neuen Testdateien sind neu hinzugekommen; die anderen 41 Dateien ergänzen vorhandene lokale Dateien. Keine parallele Berichtsstruktur.

- CryptoScript/CryptoAlgorithm/AES/AES.cs
- CryptoScript/CryptoAlgorithm/AsymmetricCryptoAlgorithm.cs
- CryptoScript/CryptoAlgorithm/CryptoAlgorithm.cs
- CryptoScript/CryptoAlgorithm/DES3/DES3.cs
- CryptoScript/CryptoAlgorithm/GenerateKeyContract.cs
- CryptoScript/CryptoAlgorithm/HMAC/HMAC.cs
- CryptoScript/CryptoAlgorithm/SymmetricCryptoAlgorithm.cs
- CryptoScript/InfoDocs/Info.Mech.AES-CBC-MAC.md
- CryptoScript/InfoDocs/Info.Mech.AES-CBC.md
- CryptoScript/InfoDocs/Info.Mech.AES-CCM.md
- CryptoScript/InfoDocs/Info.Mech.AES-CMAC.md
- CryptoScript/InfoDocs/Info.Mech.AES-CTR.md
- CryptoScript/InfoDocs/Info.Mech.AES-ECB.md
- CryptoScript/InfoDocs/Info.Mech.AES-GCM.md
- CryptoScript/InfoDocs/Info.Mech.AES-GMAC.md
- CryptoScript/InfoDocs/Info.Mech.DES3-CBC.md
- CryptoScript/InfoDocs/Info.Mech.DES3-CMAC.md
- CryptoScript/InfoDocs/Info.Mech.DES3-ECB.md
- CryptoScript/InfoDocs/Info.Mech.DES3-RETAIL.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA1.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA224.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA256.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-224.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-256.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-384.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA3-512.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA384.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA512-224.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA512-256.md
- CryptoScript/InfoDocs/Info.Mech.HMAC-SHA512.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-AC-SESSION.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-AC-VERIFY.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-AC.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-ARPC-METHOD-1.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-ARPC-METHOD-2.md
- CryptoScript/Model/MechanismRegistry.cs
- CryptoScript/Operations/CryptoOperations.cs
- CryptoScriptUnitTest/EmvAlgorithmSelectionContractTests.cs
- CryptoScriptUnitTest/GenerateKeyContractTests.cs
- CryptoScriptUnitTest/HashSetupTests.cs
- CryptoScriptUnitTest/MechanismParameterContractValidatorTests.cs
- docs/architecture/AP5-analysis.md
- docs/architecture/AP5-final-inventory.md
- docs/architecture/AP5-report.md

Diese Liste bezeichnet die Differenz zum gesicherten lokalen AP5-Ausgangsstand. Die vorherige AP5-Dateiliste bleibt oben als Stand vom 16. Oktober 2026 erhalten.

### 12 Git Endzustand

Branch master; HEAD unverändert dabcddf8320d40f6d824d1918bea70e255e52acc. Index leer. Insgesamt 83 veränderte versionierte Dateien und sieben neue unversionierte Source-/Dokumentationsdateien aus AP5 und AP5-A, zusammen 90 Dateien. Das bereits vorhandene unversionierte .codex/ bleibt zusätzlich sichtbar.

Keine Dateien zurückgesetzt oder bereinigt. Keine Branch-Wechsel oder Worktrees. Keine Commits, Pushes oder Merges. .codex/config.toml weder gelesen noch verändert. Sicherung und Build-/Testnachweise liegen in ignorierten lokalen Prüfverzeichnissen.

**AP5-Architekturentscheidung A abgeschlossen: JA**

Zentraler GenerateKey-Vertrag, dokumentierter bestehender Selektor, unabhängiges Review mit geschlossenem Finding und vollständige Validierung sind abgeschlossen.

**AP5-Gesamtabschluss: weiterhin offen wegen P und T**

23P und 25T–30T werden nicht als erledigt markiert.

**AP5-A Änderungen commitfähig: JA**

Alle freigegebenen AP5-A-Ergänzungen sind unabhängig reviewed und grün validiert. Diese Bewertung gilt für die Ergänzungen zum vorhandenen AP5-Arbeitsstand und erklärt AP5 insgesamt nicht für abgeschlossen. Kein Commit erstellt.



## AP5-P Abschluss der Entscheidung 23P – 17. Oktober 2026

Dieser Abschnitt schreibt den vorhandenen Bericht fort. Die vorherigen AP5-/AP5-A-Abschlussstände sind historisch; AP5-A bleibt abgeschlossen, 23P wird nachfolgend entschieden. Keine neue parallele Berichtsstruktur.

### 1 Ausgangszustand und Schutz des lokalen Arbeitsstands

Branch master, HEAD dabcddf8320d40f6d824d1918bea70e255e52acc, leerer Index. Zu Beginn 83 modifizierte versionierte und sieben neue Source-/Dokumentationsdateien aus AP5/AP5-A (90 Dateien) sowie das bereits vorhandene unversionierte .codex/. Der lokale AP5-A-Stand war massgeblich. Vor Änderungen wurden Diffs und relevante Kerne geprüft; .tmp/AP5P-baseline enthält 97 gesicherte Dateien mit SHA256-Manifest: die 90 bisherigen Dateien plus sieben relevante bisher unveränderte TR-31-Dateien. .codex/config.toml wurde weder gelesen noch verändert.

### 2 HMAC und Legacy-Kompatibilitätsprüfung mit expliziter Freigabe

Bestehende H-/D-Klassifizierungs- und HMAC-JSON-Tests erzeugten ihre Eingaben über Wrap mit AES-deklarierten 16 Bytes. Die unabhängige Vorprüfung reproduzierte zusätzlich akzeptierte R-/Z-Writes. Diese bisher opaken Nicht-A/T-Schreibfälle wären betroffen gewesen. Vor Umsetzung wurde deshalb gestoppt und die konkrete Grenze zur Entscheidung vorgelegt.

Der Benutzer hat ausdrücklich freigegeben: «Ja: Wrap nur A/T; H-/Unknown-Unwrap erhalten (empfohlen)». Neue Wrap-Aufrufe sind damit ausschliesslich A/T; auch frühere opake R-/Z-Schreibfälle entfallen. H-/D-Unwrap-Testsetups nutzen nun feste unabhängige Blöcke. HMAC-Reads, Unknown-Reads, der vorhandene R-gekennzeichnete opake DER-Block mit vier Optional Blocks, historisches AES-128-D0112, Rewrap und A/B/C bleiben erhalten. R wird weiterhin Secret(Unknown), es entsteht keine RSA-Repräsentation oder RSA-Implementierung.

Eine zusätzliche neue Lesetestannahme deckte eine alte Parsergrenze auf: Composite-Unwrap mit Optional Blocks scheitert bereits im Baseline-Reader, weil der generische Parser den vollständigen Wire-Block erwartet. Vorhandene positive Optional-Tests verwendeten vollständige Wire-Strings. AP5-P belässt diesen Reader unverändert; die dokumentierte und getestete Optional-Leseform ist der vollständige Wire-String. Dies wurde unabhängig bestätigt.

### 3 Verbindliche Architekturentscheidung 23P

**Entschieden und umgesetzt: Das tatsächlich verwendete TR-31-Headeralgorithmusfeld bestimmt die Obfuskation des transportierten Schlüssels.**

Das übergebene #BLKH[7] wählt A oder T, nicht KeyType, KeySize oder ein gespeichertes HDR-Attribut. A erlaubt 16/24/32 Bytes und obfuskiert auf 32 Schlüsselbytes; T erlaubt 16/24 Bytes und obfuskiert auf 24. Danach bleiben Zwei-Byte-Bitlänge und AES-Blockpadding unverändert. Der AES-KBPK bestimmt das Version-D-Binding und ist eine andere Schlüsselrolle.

### 4 Implementierte Codeänderungen

WrapAESTR31 löst den tatsächlichen Header einmal auf, prüft die Struktur einschliesslich Optional-Block-Grenzen und unterstützt neue Writes ausschliesslich für A/T. Die komplette Hex-Decodierung des transportierten KeyValue akzeptiert vollständige 0x(...)-Literale sowie bisherige rohe Host-Hexwerte. Tatsächliche Materiallängen werden gegen das Headerprofil geprüft. Bekannte KeySizeInBits müssen zu diesen Bytes passen; wirklich unbekannte Metadaten bleiben unbekannt und werden nicht zur Algorithmuswahl verwendet.

Die alte typ-/HDR-basierte Obfuskationshilfe und der abschneidende Array.Copy-Materialpfad sind entfernt. Tr31ConfidentialData erhält sämtliche tatsächlichen Bytes und das Headerziel. Der einmal aufgelöste Header geht unverändert in MAC und Ausgabe ein. KBEK/KBAK, CMAC, CBC, Zufallsfüller, Gesamtlängenprüfung, MAC-Vergleich, Unwrap und generischer Parser wurden nicht geändert. Keine Änderungen an A/B/C, DES3-Backend, ANTLR oder AP5-A-Verträgen.

### 5 KBPK- und Transport-Key-Diagnostik

KBPK: erwarteter Algorithmus Aes, Usage WrapKey; bestehende Typ-/Usage-Warnings bleiben erhalten. Transport: erwarteter Algorithmus aus A/T, KeyUsage.None. Explizite Abweichung erzeugt eine nicht blockierende KeyTypeConflict-Warning am ursprünglich übergebenen transportierten KEY, Unknown keine Typwarnung. Technisch falsche Bytes oder bekannte Grössenmetadaten bleiben Fehler.

Identische Bytewerte vermischen unterschiedliche KEY-Objekte nicht. Derselbe physische KEY in beiden Rollen dupliziert keine identische AES-Typwarning. Originalreferenzen, Identifier, OperationId und ExecutionId bleiben erhalten. Keine KEY-/PARAM-Mutation und keine künstliche Transport-Usage. Nach bereits erzeugten Warnings auftretende technische Fehler bleiben Fehler; fehlgeschlagene Authentisierung liefert keinen KEY.

### 6 Unabhängige AES- und TDEA-Referenzwerte

Zehn vollständige feste Ciphertext/MAC-Antworten wurden mit OpenSSL 3.6.1 CMAC plus AES-128-CBC ohne Padding berechnet: AES-128/192/256 mit D0144 und TDEA-128/192 mit D0112, jeweils zusätzlich mit KS-Optional-Block und entsprechend D0156/D0124. Gemeinsamer KBPK 000102...0F, Schlüsselbytes 202122..., deterministischer Füller 010203.... Die zwei festen H-/D-Read-Blöcke stammen aus derselben unabhängigen Berechnung.

Der Reviewer hat alle zwölf Antworten zusätzlich mit Python cryptography 50.0.2 und einer separaten KBEK/KBAK-/CMAC-/CBC-Berechnung bestätigt. Kein Produktionscore berechnet die erwarteten Testwerte. Der veröffentlichte Version-D-AES-128-Vektor und vorhandene KBPK-128/192/256-Referenztests bleiben unverändert grün.

### 7 Neue und angepasste Regressionstests

Tr31HeaderAuthorityTests prüft alle zehn A/T-Vektoren mit je sechs vorhandenen Algorithmusdeklarationen (Aes, Tdea, Hmac, Unknown sowie Rsa/Ec als rein deklarative Konflikte). Vollständige Ausgaben bleiben identisch; Warnings ändern nur die Diagnostik. Direkte Aufrufe mit/ohne Sink, Invocation, Runner und Legacy-Identifier, SourceVariable-Identität trotz gleicher Bytes, Konflikt-HDR, fehlender Header, Header-/Material-/KeySize-Fehler, Optional-Block-Fehler, TDEA-Zufallsfüller-Fallback, Rollenwarnungen und Authentisierungsfehler sind abgedeckt.

Tr31AesCharacterizationTests behält produktive AES-/TDEA-Layouts, deterministische Antworten, Zufallsfüller, Rewrap, Optional-Wire-Reads und H-/D-Klassifizierung/JSON. Zwei alte Tests ausschliesslich der entfernten internen typ-/HDR-basierten Obfuskationshilfe wurden durch die produktive Header-Autoritätsmatrix ersetzt. H-/D-Testeingaben wurden erst nach expliziter Freigabe auf feste Referenzen umgestellt.

### 8 Unabhängiges Review und Findings

Separater Agent ap5_independent_review prüft den tatsächlichen lokalen Delta gegen .tmp/AP5P-baseline, geschützte Bestandsdateien, Referenzwerte, Code und Dokumentation. Vorprüfung: elf vorhandene Kompatibilitätsfälle unabhängig erfolgreich.

Ein bestätigter Medium-Befund: NumberStyles.HexNumber akzeptiert Leerzeichen in Optional-Block-Längen, während der unveränderte Reader diese zurückweist. Neue Writes hätten damit malformed Blöcke akzeptiert. Das Finding wurde geschlossen. Abschliessendes Code-/Test-/Inventar-/Info-Review: keine offenen bestätigten Findings; Critical 0, High 0, Medium 0 offen (1 behoben), Low 0.

### 9 Korrektur und unabhängiges Nachreview

Beide Optional-Längenfelder verlangen nun vor der Konvertierung ausschliesslich ASCII-Hexziffern. Vier gezielte Space-Negativfälle sichern kurze und erweiterte Längenfelder ab. Der Reviewer hat den minimalen Quellfix unabhängig bestätigt und auf dem final gebauten Testcode 141/141 gezielte Header-/AES-Tests erfolgreich nachgetestet. Das Medium-Finding ist geschlossen. Alle zwölf kryptografischen Referenzen wurden separat nachgerechnet, beide 66-ID-Inventare gegen die Baseline verglichen.

### 10 Test- und Build-Ergebnisse

Windows / .NET SDK 10.0.401. Getrennte Builds mit --no-restore -m:1 -nr:false, Tests danach mit --no-build. Linux und macOS wurden nicht ausgeführt.

| Prüfung | Abschlussergebnis | Nachweis |
|---|---|---|
| Neue AP5-P-Tests | 107 bestanden; zwei entfernte Alt-Hilfstests ergeben netto +105 | Tr31HeaderAuthorityTests; vollständiges Unit-TRX |
| Gezielter unabhängiger Nachtest | 141/141 bestanden | Separater Reviewer, finale gebaute Assembly |
| TR-31 A/B/C/D, Negativ-/Legacy-, AP5- und AP5-A-Regression | 1’644 bestanden, 0 fehlgeschlagen/übersprungen | TestResults/AP5P/AP5P-regression.trx |
| Gesamte CryptoScript-Suite | 6’115 bestanden, 0 fehlgeschlagen/übersprungen | TestResults/AP5P/AP5P-unit.trx |
| Gesamte GUI-/Konsolensuite | 174 bestanden, 0 fehlgeschlagen/übersprungen | TestResults/AP5P/AP5P-gui-console.trx |
| Root-Solution-Build | Erfolgreich, 0 Fehler / 0 Warnungen | TestResults/AP5P/root-build.log |
| Avalonia-/Desktop-Build | Erfolgreich, 0 Fehler; neun bestehende Nullable-Warnungen in unveränderten GUI-Dateien | TestResults/AP5P/desktop-build.log |
| GUI-Testprojekt-Build | Erfolgreich, 0 Fehler / 0 Warnungen | TestResults/AP5P/gui-build.log |
| Dokumentation und Navigation | Bestanden innerhalb beider vollständigen Suiten; unter anderem 93 Unitfälle mit InfoDoc/Navigation/Documentation im Namen | Unit-/GUI-TRX |
| Schutz des AP5-/AP5-A-Stands | 97 Baseline-Dateien geprüft: nur sechs freigegebene bestehende Dateien geändert, 91 bytegleich; zusätzlich zwei neue Testdateien | SHA256-Manifest .tmp/AP5P-baseline/manifest.json |
| Inventarregression | Je 66 eindeutige IDs, vollständige Reihenfolge identisch zum Ausgangsstand | Beide Inventare gegen Baseline |
| git diff --check | Erfolgreich; neue Source-/Dokumentationsdateien zusätzlich auf Whitespace geprüft | Abschlussprüfung |

Der erste neue Testlauf hatte 30 Fehler ausschliesslich durch die bisher nicht unterstützte Optional-Composite-Leseannahme. Alle neuen vollständigen A/T-Wrap-Referenzwerte waren dabei korrekt. Die Tests wurden auf die bestehende unterstützte Optional-Wire-Leseform begrenzt; kein kryptografischer Sollwert wurde angepasst.

### 11 Aktualisierte AP5-Inventare

Alle 66 ursprünglichen IDs bleiben erhalten. 23P bleibt Kategorie P und trägt den oben genannten verbindlichen Entscheidungsstatus. AP5-A bleibt abgeschlossen; 15A–19A behalten den bisherigen KEY.KeyType.Algorithm-Selektor. Die vorhandene Mechanismus-Info und beide Inventare beschreiben neue A/T-Writes und erhaltene authentisierte Legacy-Reads.

### 12 Verbleibende Entscheidung 25T–30T

Ausschliesslich 25T–30T bleiben offen: begrenzter Analysebackend-Vertrag für bisher strikte degenerierte DES3-Schlüssel in EMV-/TR-31-Cores. AP5-P verändert diese Cores oder die Entscheidung nicht. Kein neuer KeyPurpose-Vertrag und keine Version-2-Asymmetrie.

### 13 Vollständige zusätzlich geänderte Dateiliste

Acht Source-/Dokumentationsdateien im Delta zum gesicherten lokalen AP5-A-Stand:

- CryptoScript/CryptoAlgorithm/WRAPPERS/WrapAESTR31.cs
- CryptoScript/InfoDocs/Info.Mech.WRAP-AES-TR31.md
- CryptoScriptUnitTest/Tr31AesCharacterizationTests.cs
- CryptoScriptUnitTest/Tr31HeaderAuthorityTests.cs (neu)
- CryptoScriptUnitTest/Tr31VersionDTransportVectors.cs (neu)
- docs/architecture/AP5-analysis.md
- docs/architecture/AP5-final-inventory.md
- docs/architecture/AP5-report.md

Ignorierte Sicherungen und Prüfartefakte unter .tmp/AP5P-* und TestResults/AP5P sind kein Produktdelta. Die bisherigen AP5-/AP5-A-Dateilisten bleiben als historische Stände erhalten.

### 14 Abschliessender Git-Status

Branch master; HEAD unverändert dabcddf8320d40f6d824d1918bea70e255e52acc. Index leer. Insgesamt 84 modifizierte versionierte Dateien und neun neue unversionierte Source-/Dokumentationsdateien aus AP5, AP5-A und AP5-P, zusammen 93. Das bereits vorhandene unversionierte .codex/ bleibt zusätzlich sichtbar. AP5-P verändert ausschliesslich die acht oben genannten Dateien; sämtliche anderen gesicherten Dateien sind bytegleich.

Keine Commits, Pushes, Merges, Branch-Wechsel, Worktrees oder Bereinigungen. .codex/config.toml weder gelesen noch verändert. Nach Implementierung, unabhängigem Review und Validierung endet dieser Arbeitsschritt.

**AP5-P Architekturentscheidung 23P abgeschlossen: JA**

**AP5-P unabhängig reviewed: JA**

**AP5-P Änderungen commitfähig: JA**

**AP5-Gesamtabschluss: weiterhin offen wegen 25T–30T**

## AP5-T Abschluss der Entscheidung 25T–30T – 17. Oktober 2026

Dieser Abschnitt schreibt den bestehenden AP5-Bericht fort. Die vorherigen AP5-/AP5-A-/AP5-P-Stände bleiben historische Nachweise. AP5-A und AP5-P werden nicht neu entschieden; AP5-T schliesst ausschliesslich die sechs verbliebenen T-Einträge.

### 1 Geschützter Git-Ausgangszustand

Branch master, HEAD dabcddf8320d40f6d824d1918bea70e255e52acc, leerer Index. 84 modifizierte versionierte Dateien und neun neue Source-/Dokumentationsdateien (93), dazu das schon vorhandene unversionierte .codex/. Der lokale uncommittete AP5-/AP5-A-/AP5-P-Stand ist verbindlich. Vor Änderungen wurden Git-Zustand, Diffs und aktuelle Architekturberichte geprüft. .tmp/AP5T-baseline sichert diese 93 Dateien und 60 zusätzliche relevante Core-/Testdateien, insgesamt 153 mit SHA256-Manifest. .codex/config.toml wurde weder gelesen noch verändert. Kein Commit, Push, Merge, Branch-Wechsel, Worktree oder Reset.

### 2 Vollständige Analyse von 25T–30T

25T/26T: Master-A/B-Adapter diagnostizieren bereits Original-KEY-Metadaten und -Qualität. Die Cores prüften dann ValidateUsableKey und verschlüsselten zwei Diversifikationsblöcke über striktes ECB. PAN/PSN, Option-B-SHA1/Dezimalisierung, Komplementbildung und Ausgabe-Odd-Parity bleiben unverändert.

27T: Der durch AP5-A festgelegte KEY.KeyType.Algorithm-Selektor wählt AES oder TDEA; Unknown bleibt Fehler. Der TDEA-Core prüfte ValidateUsableKey und benutzte zwei strikte ECB-Aufrufe für ATC||F0/0F||Nullbytes. AES-Derivation bleibt unverändert im bisherigen Pfad.

28T: Application Cryptogram und Verify sowie ARPC2 verwenden TDEA Algorithm 3/Retail-M2; ARPC1 verwendet die bestehende ARQC-XOR-ARC-Konstruktion und ECB. Die Session-Key-Validierung war strikt. AES-CMAC/ECB, Datenzusammenbau, Längen und Trunkierung bleiben unverändert. Verify vergleicht konstant und der Adapter wirft bei Abweichung weiterhin einen Fehler.

29T: Version B führt KBPK → CMAC-KDF → KBEK/KBAK → CMAC über Header und Klartext → CBC mit MAC als IV. Unwrap entschlüsselt, authentisiert und extrahiert erst danach. Nur das anfängliche Qualitätsgate zu entfernen hätte CBC mit intern degeneriertem KBEK nicht freigegeben.

30T: A/C bilden KBEK/KBAK durch unveränderte XOR-Konstanten 45/4D. CBC nutzt die ersten acht Headerbytes als IV; CBC-MAC authentisiert Header und Ciphertext, auf vier Bytes gekürzt. Unwrap authentisiert vor Entschlüsselung/Extraktion. Die interne Schlüsselverwendung muss ebenfalls im expliziten Analysepfad liegen.

### 3 Eingeführte interne DES3-Analysemethoden

ECB: EncryptNoPaddingForAnalysis prüft Material-/Blocklänge und verwendet den vorhandenen privaten Analyse-Transform. CBC: EncryptNoPaddingForAnalysis, DecryptNoPaddingForAnalysis und ComputeMacNoPaddingForAnalysis prüfen Länge/IV/MAC-Länge und verwenden denselben bestehenden Analysemodus. Retail: ComputeMacForAnalysis ruft den vorhandenen ComputeMacCore mit analysisKeys=true auf. CMAC: ComputeForAnalysis validiert Länge und ruft das unveränderte vorhandene Bouncy-Castle-Primitive auf.

Keine eigene DES-/TDEA-Blockchiffre. Der vorhandene ECB-/CBC-Bouncy-Castle-Pfad greift ausschliesslich bei expliziter Analyse und TripleDES.IsWeakKey; normale Schlüssel bleiben im .NET-Backend. Kein globaler Schalter, AsyncLocal oder Fallback auf beliebige Exceptions. Keine öffentliche API-Erweiterung.

### 4 Erhaltene strikte Schnittstellen

DES3.ValidateUsableKey und die bisherigen ECB-/CBC-NoPadding-/CBC-MAC-Aufrufe bleiben unverändert strikt. Retail.ComputeMac und die parameterlosen DES3-Modusinstanzen behalten ihre Qualitätsgates. CMAC.Compute war bereits caller-validiert und wurde nicht nachträglich umgedeutet. Öffentliche Tr31TdeaKeyDerivation.DeriveKeys und Tr31TdeaKeyVariants.DeriveKeys behalten die strikte Validierung; neue Analyseentries sind intern.

Auch bisherige EMV-Core-Entries Derive/ComputeAc/VerifyAc/ComputeArpcMethod1/2 bleiben strikt. Neue ForAnalysis-Entries sind klar getrennt. Die internen TR-31-Versionskerne behalten über analysisKeys=false ihr altes Aufrufverhalten; nur der CryptoScript-Adapter fordert true an. Tests sichern die weiterhin blockierende Semantik alter Aufrufe ab.

### 5 EMV Master A/B

Die Adapter verwenden DeriveForAnalysis; der Scope wird durch die gemeinsame Diversifikation zu beiden ECB-Aufrufen weitergereicht. Die gelieferten effektiven DES-Bits bestimmen die Berechnung. Original-KEY und seine Parität bleiben unverändert; nur die bereits bestehende Odd-Parity-Anpassung des neu abgeleiteten ICC-Keys erfolgt weiter. Ausgabe-KeyType Tdea, KeySize 128, Usage Unspecified und DerivationMechanism bleiben erhalten. Technische PAN-/PSN-/16-Byte-/bekannte-Grössen-Verträge bleiben blockierend.

### 6 EMV AC Session, AC und ARPC

Nur ausgewählte TDEA-Sessions verwenden die Analyse-Derivation; AES und Unknown-Verhalten bleiben unverändert. AC/Verify/ARPC2 verwenden den expliziten Retail-Analyseentry, ARPC1 den ECB-Analyseentry. Ausgabegrössen und ARPC-/IAD-Konstruktion sind erhalten.

Metadatenwarnungen behalten ihre Reihenfolge; danach folgen DesParityViolation (10) und WeakDes3Key (11), jeweils höchstens einmal je Kategorie und Operation. Die bestehenden Des3KeyQuality-Prüfungen bleiben unverändert und normalisieren nur temporäre Inspektionskopien. Schwache/semi-schwache Komponenten und effektives K1=K2/K2=K3 werden paritätsunabhängig erkannt; K1=K3 allein gilt nicht als Single-DES-Degeneration.

Mit und ohne Sink werden technisch gültige schwache Testschlüssel berechnet. Falsches EXPECTED erzeugt weiterhin einen Fehler, keinen erfolgreichen Verify-Wert; nachfolgende Statements werden nicht ausgeführt. Bereits gemeldete Warnings bleiben im Execution-Snapshot. Ungültige KEY-/PARAM-Referenzen, Längen, ATC, ARQC, ARC, CSU und ADATA bleiben Fehler.

### 7 TR-31 A/B/C

WrapDES3TR31 aktiviert Analyse ausdrücklich beim Aufruf der bestehenden Versionskerne. Für B umfasst dies KBPK-CMAC-KDF, KBAK-CMAC und KBEK-CBC in beiden Richtungen. Für A/C umfasst es Variantenbildung, KBEK-CBC und KBAK-CBC-MAC. Algorithmen, Konstanten, Header-/Füllbytes, MAC-Trunkierung und Key-Block-Struktur bleiben unverändert.

FixedTimeEquals, Fehler bei falschem MAC und Extraktion erst nach erfolgreicher Authentisierung sind erhalten. Die Originalreferenzauflösung und Legacy-Mehrdeutigkeitsfehler bleiben erhalten. AP5-P-Dateien, Version-D-Headerautorität/Obfuskation, AES-Binding und HMAC-/Opaque-/Legacy-Lesekompatibilität wurden nicht verändert. Keine RSA- oder Version-2-Erweiterung.

### 8 Interne abgeleitete Schlüssel und Speicherbehandlung

Originalschlüssel werden am Adapter diagnostiziert. Interne KBEK/KBAK werden mit tatsächlichen effektiven Bits verarbeitet, ohne künstliche KEY-Objekte, zusätzliche Qualitätswarnungen oder globalen Zustand. Neue/angepasste Pfade zeroisieren temporäre Materialkopien, abgeleitete Schutzschlüssel und vertrauliche MAC-Eingaben soweit möglich. Backendinterne Key-Schedules bleiben backendverwaltet. Originale KEY-/PARAM-/VAR-Objekte werden weder verändert noch in Fehlermeldungen offengelegt.

Die A/C-XOR-Varianten erhalten effektive Gleichheitsrelationen. Ein nichtdegenerierter KBPK wird dadurch nicht degeneriert; eine einzelne schwache Komponente kann neu entstehen. Fester Fall: KBPK 45454545454545450123456789ABCDEF → KBEK 000000000000000044660022CCEE88AA, erste effektive DES-Komponente 0101010101010101. Degenerierte Original-KBPKs liefern zusätzlich tatsächlich degenerierte interne A/C-Schutzschlüssel, die die neuen CBC-/MAC-Pfade erreichen.

Abdeckungsgrenze für B: Kein nichtdegenerierter KBPK mit zufällig degeneriertem CMAC-KDF-Ausgang wurde konstruiert. Die explizite Analyseweitergabe an alle Zwischenstufen wurde unabhängig im tatsächlichen Diff geprüft; dieselben Primitive sind mit problematischen KBPK-/A/C-Zwischenschlüsseln und separaten technischen Grenzfällen abgesichert. Diese offen beschriebene Testgrenze ist kein verbleibender Architekturentscheid.

### 9 Neue und angepasste Regressionstests

350 neue Fälle in Ap5TAnalysisBackendTests: 230 unabhängige Ergebnis-/Diagnostik-/Aufrufformenfälle, 33 KBEK/KBAK-Vergleiche, 40 MAC-/ARQC-Fail-Closed-Fälle, 33 Header-Manipulationen, ein strikter API-Matrixtest, elf technische EMV-Grenzen, ein Analyseprimitive-Grenztest und ein verschachtelter Kontexttest.

Die Matrix umfasst Direktaufrufe mit/ohne Sink, Invocation, regulären Runner und bisher unterstütztes Legacy. Nicht unterstützte EMV-MAC-Legacy-Aufrufe bleiben abgelehnt. Geprüft werden FunctionName/Mechanism/Identifier, ExecutionId/OperationId/ParentOperationId, keine Warnungsduplikate, gleiche Bytes in verschiedenen KEY-Objekten, SourceVariable-Identität, Fehler nach Warnungen und keine Eingabemutation. Schwache Ausgabe-KEYs behalten ihre Metadaten.

Die vorhandene AP5-Adaptertestmatrix erwartet nun Ausführung beziehungsweise Erreichen der Authentisierung statt Qualitätsabbruch. Zwei ARPC-Integrationstests wurden von der alten Schwachschlüssel-Abbrucherwartung auf feste unabhängige Werte und Weiterlaufen nach Warnings umgestellt. Keine bestehenden kryptografischen Sollwerte wurden geändert. Alle bisherigen strikten Core-Negativtests bleiben bestehen.

### 10 Unabhängige kryptografische Referenzwerte

Elf Profile: normal16/24, parity16/24, bekannte schwache und semi-schwache erste Komponente, effektives K1=K2, K2=K3, K1=K3 ohne Degeneration, gleichzeitige Paritäts-/Schwächebefunde sowie internalWeak. EMV unterstützt unverändert 16-Byte-TDEA; vier 24-Byte-Profile gelten für TR-31 und technische EMV-Ablehnung.

42 EMV-Antworten: Master-A-PAN 1234567890123456, Master-B-PAN 1234567890123456789, jeweils PSN 01; Session-ATC 0001; AC-Daten 000102...17; ARPC-ARQC 0102030405060708, ARC 3030, CSU 01020304, ADATA A1A2A3. 33 vollständige TR-31-A/B/C-Blöcke verwenden transportierte Bytes 202122...2F, RND 010203...0E, Header A0088/B0096/C0088P0TE00E0000 sowie feste KBEK/KBAK, Ciphertexts und versionsgerechte MACs.

Erstberechnung: Python cryptography 50.0.2 / gebündeltes OpenSSL 4.0.3 vom 29. September 2026, getrennte Formelimplementierung ohne Produktionscore. Unabhängiger Reviewer: OpenSSL CLI 3.6.1 vom 27. Januar 2026, 209 separate Subprozesse; Retail separat DES-CBC(K1)→D(K2)→E(K1), SHA1 und zweipassige Option-B-Dezimalisierung separat. Alle 42 EMV-Werte und 33 Blöcke samt KBEK/KBAK und C#-Quellfixtures exakt bestätigt. Keine unverifizierbare Referenzabweichung.

Vollständige Schlüssel, Eingaben und Ausgaben stehen eingefroren in Ap5TReferenceVectors.cs; Generator und JSON liegen als lokale Prüfartefakte unter .tmp/AP5T-reference. Zur Testlaufzeit wird kein Python oder OpenSSL benötigt.

### 11 Independent Review und Findings

Separater Agent ap5_independent_review mit eigener Aufgabenstellung prüfte tatsächliche lokale Diffs gegen die 153-Dateien-Baseline, alle 21 produktiven Dateien, neue und angepasste Tests, acht Info-Seiten und beide Inventare. Prüfschwerpunkte des Auftrags einschliesslich strikter APIs, interner Schlüssel, Authentisierung, AP5-A/P-Schutz, öffentlicher API und Plattformabhängigkeit wurden bestätigt. Ergebnis: keine bestätigten Critical-, High-, Medium- oder Low-Findings.

### 12 Korrekturen und Nachreview

Mangels bestätigter Implementierungsfindings waren keine kryptografischen Korrekturen erforderlich. Der erste Bestandslauf hatte zwei erwartete alte ARPC-Qualitätsabbruchtests; diese wurden entsprechend der verbindlichen AP5-T-Entscheidung ersetzt. Ein erster neuer Testaufbau initialisierte den generischen TR-31-PARAM unvollständig (165 Fehler vor dem Kryptopfad); ausschliesslich der Testaufbau wurde korrigiert. Ein Testhost-Verbindungsstart scheiterte nach 90 Sekunden ohne Testausführung; der getrennte Wiederholungslauf war grün.

Nachreview: letzte zusätzliche Assertions zu Fehleraufrufen mit/ohne Sink, Unverändertheit und Ausgabemetadaten unabhängig geprüft. 789/789 gezielte Fälle unabhängig bestanden. Das Quell-/Test-/Info-/Inventarreview ist ohne offene Findings abgeschlossen. Die vollständigen Prüfnachweise stehen unten; ein separater Schlussabgleich prüft den Bericht gegen diese Artefakte.

### 13 Vollständige Test- und Build-Ergebnisse

Windows / .NET SDK 10.0.401. Builds getrennt mit --no-restore -m:1 -nr:false, anschliessend Tests gegen die gebauten Artefakte mit --no-build. Keine neuen Windows-spezifischen APIs oder externen Testlaufzeitabhängigkeiten. Linux/macOS lokal nicht ausgeführt; keine dortigen Erfolge behauptet.

| Prüfung | Ergebnis | Nachweis |
|---|---|---|
| Gezielt AP5-T plus AP5-/ARPC-Integration | 789 bestanden, zusätzlich unabhängig 789/789 | AP5T-targeted.trx / Review-Agent |
| AP5/A/P/T, EMV, TR-31, DUKPT, DES3 und PIN-Block-Regressionsauswahl | 4’719 bestanden, 0 fehlgeschlagen/übersprungen | TestResults/AP5T/AP5T-regression.trx |
| Root-Solution-Build | Erfolgreich, 0 Fehler/0 Warnungen | TestResults/AP5T/root-build.log |
| Avalonia-/Desktop-Build | Erfolgreich, 0 Fehler; 9 bestehende Nullable-Warnungen in unveränderten GUI-Dateien | TestResults/AP5T/desktop-build.log |
| Neue AP5-T-Fälle | 350 bestanden | Ap5TAnalysisBackendTests / Unit-TRX |
| Vollständige CryptoScript-Suite | 6’465 bestanden, 0 fehlgeschlagen, 0 übersprungen | TestResults/AP5T/AP5T-unit.trx |
| Vollständige GUI-/Konsolensuite | 174 bestanden, 0 fehlgeschlagen, 0 übersprungen | TestResults/AP5T/AP5T-gui-console.trx |
| GUI-Testprojekt-Build | Erfolgreich, 0 Fehler / 0 Warnungen | TestResults/AP5T/gui-build.log |
| Dokumentation und Navigation | Bestanden innerhalb beider vollständigen Suiten | Unit-/GUI-TRX |
| Inventare | Je 66 eindeutige IDs und ursprüngliche Reihenfolge unverändert | Baselinevergleich beider Inventare |
| Dateischutz | 153 geprüft: 34 freigegebene bestehende Dateien geändert, 119 bytegleich; zwei neue Testdateien | SHA256-Manifest / final-protection-status.json |
| git diff --check | Erfolgreich; neue Source-/Architekturdateien zusätzlich auf Whitespace geprüft | Abschlussprüfung |

### 14 Dokumentationsänderungen

Acht betroffene Info-Seiten beschreiben DesParityViolation/WeakDes3Key als nicht blockierend, effektive Komponentengleichheit, Analysezweck, keine Eignungsaussage für produktive Zahlungssysteme und weiterhin blockierende Material-/Parameter-/Protokollfehler. ARQC- und TR-31-Authentisierung bleiben verbindlich. Veraltete Aussagen über grundsätzlichen Schwachschlüsselabbruch wurden dort ersetzt. Die drei vorhandenen AP5-Architekturdateien wurden fortgeschrieben; keine parallele Berichtsstruktur.

### 15 Abgleich aller 66 Inventar-IDs

Alle 66 ursprünglichen IDs und ihre Reihenfolge bleiben erhalten. 25T–30T behalten ihre technischen historischen IDs und sind als entschieden und umgesetzt markiert. 15A–19A bleiben durch AP5-A, 23P durch AP5-P abgeschlossen. Technische Fehler bleiben blockierend; Qualität ist im freigegebenen Analysevorgang eine Warning. Keine offene AP5-Architekturentscheidung verbleibt. Dies schliesst keine ausserhalb von AP5 liegenden EP2-Gap- oder Produktkonformitätsarbeiten ab.

### 16 Alle zusätzlich geänderten und neuen Dateien

36 Dateien gegenüber dem gesicherten lokalen AP5-P-Stand: 21 produktive C#-Dateien, zwei angepasste und zwei neue Testdateien, acht Info-Seiten und die drei vorhandenen Architekturdateien.

- CryptoScript/CryptoAlgorithm/DES3/DES3_CBC.cs
- CryptoScript/CryptoAlgorithm/DES3/DES3_CMAC.cs
- CryptoScript/CryptoAlgorithm/DES3/DES3_ECB.cs
- CryptoScript/CryptoAlgorithm/DES3/DES3_RETAIL.cs
- CryptoScript/CryptoAlgorithm/EMV/EmvApplicationCryptogram.cs
- CryptoScript/CryptoAlgorithm/EMV/MAC_EMV_AC.cs
- CryptoScript/CryptoAlgorithm/EMV/MAC_EMV_ARPC_METHOD_1.cs
- CryptoScript/CryptoAlgorithm/KDF/EmvAcSessionKeyDerivation.cs
- CryptoScript/CryptoAlgorithm/KDF/EmvMasterKeyDerivationOptionA.cs
- CryptoScript/CryptoAlgorithm/KDF/EmvMasterKeyDerivationOptionB.cs
- CryptoScript/CryptoAlgorithm/KDF/EmvTdeaMasterKeyDiversification.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_AC_SESSION.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_A.cs
- CryptoScript/CryptoAlgorithm/KDF/KDF_EMV_MASTER_B.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31TdeaKeyDerivation.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31TdeaKeyVariants.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31TdeaVariantUnwrap.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31TdeaVariantWrap.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31VersionBUnwrap.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31VersionBWrap.cs
- CryptoScript/CryptoAlgorithm/WRAPPERS/WrapDES3TR31.cs
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-AC-SESSION.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-A.md
- CryptoScript/InfoDocs/Info.Mech.KDF-EMV-MASTER-B.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-AC-VERIFY.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-AC.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-ARPC-METHOD-1.md
- CryptoScript/InfoDocs/Info.Mech.MAC-EMV-ARPC-METHOD-2.md
- CryptoScript/InfoDocs/Info.Mech.WRAP-DES3-TR31.md
- CryptoScriptUnitTest/Ap5SemanticDiagnosticsTests.cs
- CryptoScriptUnitTest/Ap5TAnalysisBackendTests.cs
- CryptoScriptUnitTest/Ap5TReferenceVectors.cs
- CryptoScriptUnitTest/EmvArpcIntegrationTests.cs
- docs/architecture/AP5-analysis.md
- docs/architecture/AP5-final-inventory.md
- docs/architecture/AP5-report.md

Ignorierte Sicherungen und Prüfartefakte unter .tmp/AP5T-* und TestResults/AP5T gehören nicht zum Produktdelta. Die vorherigen AP5-/AP5-A-/AP5-P-Dateilisten bleiben historische Nachweise.

### 17 Abschliessender Git-Status

Branch master; HEAD unverändert dabcddf8320d40f6d824d1918bea70e255e52acc; Index leer. Insgesamt 100 modifizierte versionierte Dateien und elf neue Source-/Dokumentationsdateien, zusammen 111 aus AP5, AP5-A, AP5-P und AP5-T. Das bereits vorhandene unversionierte .codex/ bleibt zusätzlich sichtbar. AP5-T verändert ausschliesslich die oben genannten 36 Dateien; alle anderen gesicherten Dateien sind bytegleich, einschliesslich der geschützten AP5-A-/AP5-P-Implementierungen.

Keine Commits, Pushes, Merges, Branch-Wechsel, Worktrees, Resets oder Bereinigungen. .codex/config.toml weder gelesen noch verändert. Nach Implementierung, unabhängigem Review und Abschlussvalidierung endet dieser Arbeitsschritt.

**AP5-T Architekturentscheidung 25T–30T abgeschlossen: JA**

**AP5-T unabhängig reviewed: JA**

**Semantic Diagnostics Phase 2 vollständig abgeschlossen: JA**

**Gesamtes AP5-Paket commitfähig: JA**
