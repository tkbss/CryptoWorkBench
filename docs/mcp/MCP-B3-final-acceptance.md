# MCP-B3 · Eigentümerabnahme

Der Projekteigentümer hat MCP-B3 einschließlich F1–F4 am 3. November 2026 ausdrücklich unter dem konservativen, prozessgebundenen D02-Rückfall abgenommen. Seine Bestätigung im Abschlussauftrag lautete „Ja, in diesem Umfang abgenommen“.

Der Prozess behält bis zu 4.096 Acquisition-Slots, höchstens 256 je Principal. Tombstones und Erwerbsbudget bleiben bis Prozessende erhalten. Dieselbe Hostinstanz lässt sich kein zweites Mal aktivieren. Alte Credentials bleiben widerrufen. Renew und Release bleiben bei erschöpftem Budget möglich.

## Technische Grundlage

R5 empfahl READY FOR OWNER ACCEPTANCE. Zwei vollständige Hosts wiesen fremde Tools vor dem Resolver ab. Bei Workspacewechsel und reentrantem Detach blieb L1 erreichbar. Ein früher HTTP-Abbruch sperrte den Erstdispatch. Beim Abbruch im angenommenen Edit blieben Q und Originalticket bis zur Callbackfreigabe gebunden. Die Receipt bestätigte Applied/Applied/Complete für die ursprüngliche Editorrevision 0→1. Undo und Redo gelangen. Kein neuer Abnahmeblocker. [Vollständiges R5 mit Messprotokollen und R1–R4-Eingangsinventar](../../../b3-r5-independent/MCP-B3-R5-independent-rereview.md)

[B3](MCP-B3-mcp-binding-lease-v2.md) · [F1](MCP-B3-F1-corrections.md) · [F2](MCP-B3-F2-corrections.md) · [F3](MCP-B3-F3-corrections.md) · [F4](MCP-B3-F4-corrections.md) · [B1](MCP-B1-N1-R-final-acceptance-addendum.md) · [B2](MCP-B2-N1-final-native-acceptance.md) · [AP0](MCP-00-AP0-contract-finalization.md) · [D02](MCP-00-D02-S1-epoch-safety.md)

| Finding | R5 | Begründung |
|---|---|---|
| R1-01 | FIXED | `AcceptSave` entscheidet nach dem Observer über die gepinnte Originalarbeit. L1 zuerst ergab RevisionConflict ohne Delete, Annahme zuerst ließ nur den ursprünglichen Write zu. |
| R1-02 | FIXED | `StartEditorClose` hält den Originalcoordinator über den letzten Detach hinaus. Ein echtes Clipboard-Cut und ein versiegeltes Ticket hielten den Drain bei Pending 1 bzw. 2 offen. Nach Providerfreigabe blieb der Originaltext erhalten. |
| R1-03 | FIXED | `EpochWorkFinalized` berücksichtigt die ursprüngliche native Gruppe. Strict L2 scheiterte bei offener Gruppe, bei geschlossenem Gruppenfault gelang der Drain. Der Fault blieb schreibgesperrt. Ein unsicherer Exitfehler ließ das Fenster bei zwei Versuchen offen. |
| R1-04 | FIXED | `ExecuteEditorV2Async/Lookup` prüfen den Lesegrant beim Replay. Run-only bekam Forbidden. Ein leseberechtigter Owner konnte sein Ergebnis auch nach Release abrufen. |
| R1-05 | FIXED | `VisibleResult` projiziert Metadaten und Output bereits bei der Erstantwort. Der lokale Seed enthielt R1Meta, im beschränkten Ergebnis fehlte er. Die positiven Outputgrant-Kontrollen bestanden. |
| R1-06 | FIXED | `CheckNames` prüft die rohen Argument-Properties vor der verlustbehafteten SDK-Dictionaryprojektion. Duplikate erreichten keinen Resolver. Eindeutige Requests erreichten die vorgesehenen Mappings. |
| R1-07 | FIXED | Das Execute-Binding publiziert OpenWorld. Der lokale Dialog zeigte Skript und Risikohinweis. Die Kontrolldatei entstand erst nach Zustimmung. |
| R1-08 | FIXED | Lokale Bestätigung und Execution-Pipeline trennen fehlenden Dialogowner, explizite Ablehnung und Zustimmung. Failed/ConfirmationUnavailable blieb beim Replay erhalten. Akzeptierter Lauf endete Completed. |
| R1-09 | FIXED | `Binding.InvokeAsync` leitet nur bei Execute einen Toolfehler aus dem Jobzustand ab. Get eines rejected Jobs lieferte isError=false. Execute- und Lookupfehler blieben sichtbar. |
| R1-10 | FIXED | Das Binding trennt erkennbare Legacyform, gültiges v2 ohne Lease und fehlerhafte Parameter. Ungültige Formen scheiterten vor dem Resolver, ein gültiger Edit wurde zugestellt. |
| R1-11 | FIXED | Bei Applied/CallbackFailed liefert das Edit-Binding den strukturierten D01-Beleg, isError und den Refresh-Hinweis im Text. Das wahrt die beobachtete Wirkung auch nach Callbackfehler. |
| R2-01 | FIXED | Save-Admission und Provideraufruf sind getrennt. Bei Delete, Open, Write, Folder und Picker konnte L1 im synchronen Providerpräfix zurückkehren. Ein abgebrochener Einzelwait gab weder Q noch Originalticket frei. |
| R2-02 | FIXED | `BeginShutdown/StartEditorClose` erfassen spätere GUI-Arbeit auch nach Disable. Nach letztem Detach blieben zwei Dispose-Waits beim offenen Cut ausstehend. Späte Freigabe ergab Pending 0. |
| R2-03 | FIXED | Der Jobschlüssel bindet Source, Principal, Epoche und ID. Fremd und unbenutzt zeigten denselben Fehler. Zwei Owner verwendeten dieselbe UUID, As alter Timer traf Bs wartenden Job nicht. |
| R2-04 | FIXED | Die Middleware prüft äußere Namen vor der SDK-Auswahl. Doppelte Felder einschließlich vertauschter Reihenfolge wurden neutral abgewiesen. Der gültige Kontrollrequest dispatchte genau einmal. |
| R2-05 | FIXED | `ValidateUnleasedV2` prüft die übrigen Pflichtfelder vor LeaseRequired. Leere IDs und ungültige Revisionen führten mit und ohne Lease zu InvalidParams vor dem Resolver. |
| R2-06 | FIXED | Leaseabschluss stoppt den Originaltimer und löst Tickverbindung und Lifetime nach Quieszenz. Nach Dispose waren Timer aus und CTS entsorgt, der offene native Fault blieb erkennbar. |
| R3-01 | FIXED | `TryStartCommit` entscheidet unter der L1-Sperre, `FinishCommit` wirkt danach am Original. L1 vor Annahme verhinderte Wirkung, L1 im nativen Observer konnte zurückkehren. Akzeptierte Arbeit finalisierte mit Originalrevision. |
| R3-02 | FIXED | `FinishEpochCloseAsync` wartet auf Producerabschluss und unabhängige Editorquieszenz. Ein werfender Cancellationcallback ersparte den Drain bei Pending 2 nicht. Erst Originalfreigabe erlaubte Dispose. |
| R3-03 | FIXED | Die ehemals öffentliche Toolfabrik ist privat. Fehlender Guard, fremder registrierter Katalog, manipulierte Parameter und vorab abgebrochener POST scheiterten jeweils vor Authentisierung. Eigene vollständige Registrierung blieb verwendbar. |
| R3-04 | FIXED | Status projiziert Active und Last einzeln nach Owner und execution.read. Beide echten Adapter zeigten A nur Last und B nur Active. Status-only und A ohne Read sahen keines der beiden Felder. |
| R3-05 | FIXED | `TryIdentifyToolCall/CheckNames` korrelieren einen Parameterfehler nur bei eindeutigem Envelope. Kaputte Kombinationen ergaben HTTP 400/−32600/null. Beim eindeutigen Toolaufruf mit Argumentduplikat blieb die Original-ID erhalten. |
| R3-06 | FIXED | `CheckDrain/StopWatchingDrain` lösen UpdateFinished vom Original. Bei offener Gruppe war der Handler während des gehaltenen Callbacks verknüpft, vor und nach Dispose entfernt. Die geschlossene Gruppe hatte keinen Resthandler. |
| R4-01 | FIXED | Der bei der Session gebundene Katalog prüft die konkrete Bindingreferenz. Zwei komplette Hosts, drei Toolarten, auch bei gemeinsamem Dienst, ließen keinen fremden Resolver zu. |
| R4-02 | FIXED | Attach, Activate, Retirement und Legacy-Commit publizieren außerhalb des Monitors. Reentranter Detach, Identitätswechsel und Shutdown gaben keinem überholten Nachfolger Rechte. Die echte VariableView wurde weiterhin invalidiert. |
| R4-03 | FIXED | Erster Anspruch prüft den ursprünglichen RequestAborted-Token. Vorheriger Abbruch sperrte Dispatch, späterer Abbruch bewahrte den angenommenen Originalcommit samt Q und Ticket. |

## Test- und Binärnachweise

| Lauf | GUI bestanden | MCP bestanden | Fehler | Nicht ausgeführt |
|---|---:|---:|---:|---:|
| Debug | 790 | 159 | 0 | 1 |
| Release | 790 | 159 | 0 | 1 |
| R5 Release | 790 | 159 | 0 | 1 |

Allein InstalledCodexDiscoversAndCallsStatus blieb offen. Beide Buildlogs weisen sechs Warnungen und null Fehler aus. Die native Windows-Probe mit echtem HWND bestand für Besitzwechsel, Release und Shutdown. Sechs PE/PDB-Paare und alle 38 C#-Quelldokumentprüfsummen passten. Die Release-Desktop-DLL wurde beim Abschluss erneut gehasht.

| Release | SHA-256 |
|---|---|
| Desktop | A4541ECEC80BDC8F03F8F0C17DA413F18A886E4986D3B37DE6C7F6C110D8165C |
| MCP | 6C42A8F8A0188DEFDAB25C82FFD6E467F88357EA7B61DD11D1901C8B2E05C1AF |
| Native Probe | 151FA21977BEC183E9D6322220FB3E5A2C0DC5E13FA9FFE772DF16E1C5182731 |

## Spätere Entscheidungen

Produktives HTTP/Pairing, sichere L3-Epochenrotation, physischer Suspend/Resume, installierter Client-End-to-End und umfassende reale Provider-/OS-Fehlerkombinationen bleiben separate Gates. Erzeugt ein Dritter die im Picker ausgewählte Datei vor dem Öffnen, hängt Legacy-Append an deren Inhalt an. Ob dieser Fall exklusive Neuanlage verlangen soll, bleibt eine separate Produktentscheidung.

## Git und vollständiger Dateiumfang

Der Vergleich auf feature/mcp-ap3 bei 683f789324bd040b5e2f6e834b4c2f1ef0e8aa9f ergab vor Erstellung dieses Berichts einen leeren Stagingbereich und exakt die 23 bearbeiteten und 21 unversionierten R5-Dateien. Jede Arbeitskopie stimmte byteweise im SHA-256 mit dem R5-Abschlussmanifest überein. Die 45. Commitdatei ist docs/mcp/MCP-B3-final-acceptance.md.

~~~text
3F9DF35DC69DD04F7866882E1F31D6D37C7509D1CFAE257EE6CC4A1FB6F752DC CryptoWorkBench.Automation.Abstractions/AutomationContracts.cs
EA4EE6105EBEF377EFCF358BA2DA1B25D7F939BB76CB6A487AFE3984C0D1B345 CryptoWorkBench.Automation.Abstractions/AutomationErrors.cs
E9946AB4D8E5B724767336B8F36DAAA5F77A66230E8B5493E171BC625DC45F8A CryptoWorkBench.Automation.Abstractions/AutomationJson.cs
1A3BCC7D534F62544989D39C0898350E244A6D73240A9659F6A4BEC534916D96 CryptoWorkBench.Automation.Abstractions/AutomationSchemas.cs
B96F5E7542535BA2A7EB1B3B571061AA67800FEC9A68F6E5E7ECF103AC197E27 CryptoWorkBench.Mcp.Tests/ContractTests.cs
029FF69D1F695A9EAA04D7FD0BD0E552EF359A5812DEA48C2E2A27E3666C3047 CryptoWorkBench.Mcp.Tests/TestListener.cs
A0744A4F9437650696B842CC2A19DDBCACD40F096254FBA33C03C1139C2A1905 CryptoWorkBench.Mcp/SafeMcpParseErrors.cs
D5E380196B755B88228B14357E9992DD0D5DDE74A3B2D5917C1CC5177CA23899 CryptoWorkBenchAvalonia/App.axaml.cs
69B155169AE5722BF7245779B90A05244F4A138EE2AE18B0B5BB8C9E47F3E5DC CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs
FF8046A714783803ACA7F67E559FE49A21DED28D3CC16E4D7320618EA9D2C8D8 CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs
70D51DFCC859D276B42B1438FD691D93E9572B8CC2AFE0279686A1687B3BAF62 CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs
97F60032F9AA3766375F4DFA41EC8024A05E69A641522A5A8297A1544F92AC90 CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs
52510B8AFC5B702CF0BE5E2C441F2A451D801FDADC62CB32FCE8A623215F664A CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs
317230BA17E180FF8681A01C1B5889E6D12F28B3829634EAC12B35F924700625 CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs
390991356A49C95DEA320CC0237BB44103F58116FB9988CE5A7F6A3E8117A3F4 CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs
58095AC8111878BC13AFC5E16A4D561705D798B105E0281F61CC01423E325EF3 CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs
5CEAA12D5226B8CF3FC693F2747AB19D9938E678E9D2E8B9037E7051E4AFC075 CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs
334793535EB1221124B0EA6EB095CA98FB7CBCD7D3FD724B36AE25A9E82B4234 CryptoWorkBenchGuiTest/B1EditorIntegrationTests.cs
C223FE8860FE67B22DA2E36BAE77D32C2F1BE23BBC9B15A911900EF59682F594 CryptoWorkBenchGuiTest/B1F2CorrectionTests.cs
168D9FB7BD857C3AE9ECE8C08EF993D7F60246CB0FD811CF9413FF2C6F1D16B0 CryptoWorkBenchGuiTest/B1F3CorrectionTests.cs
3F3BAC4093AF4881EDDFEB334F30F4BFC99D2ED4603393E55631561D9DDF2F05 CryptoWorkBenchGuiTest/EditorAutomationTests.cs
1F08E9F20BFF5F22191DECBE67E083317199A3419EEB221FFC78DF92F7068513 CryptoWorkBenchGuiTest/WorkbenchRegistryCorrectionTests.cs
CE06F218462D5322C2EB88A82249A2E5305ECCE3C1F0E74B8E864EC6B8CA0C86 CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs
170B05B04529678035D0CC9B93AB931E0B7C946AC3EA017C114AB20C1EDB12DB CryptoWorkBench.Automation.Abstractions/AutomationV2.cs
60CC6017E1FB123E2CF8EF7A2E950D8849DF8B150AC77F6ECB541DC2270F333F CryptoWorkBench.Mcp.Tests/B3F1WireTests.cs
6DA1B7909913C37D8DB3F5B05DF12C7ECDABC71A564749AB77C51341AA186E5F CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs
4545BB3143F583FAE2A3083547F484F1AE6902730F6FD48B26928A10669F99AC CryptoWorkBench.Mcp.Tests/BindingV2Tests.cs
1612D3672F90077BF5FD13A13CCB15F462711910C70BAB9E8A859A7942338C85 CryptoWorkBench.Mcp/WorkbenchToolsV2.cs
97F48CEEB6D11BD01E8955D3717541C37D18C6F969B3D7030C05A0EFEE9A4583 CryptoWorkBenchAvalonia/Services/Automation/ProcessAcquisitions.cs
101865B88EA1C98E9A3ABB023C60BE4D4698D84D2563D9BDB7237560E77562E8 CryptoWorkBenchAvalonia/Services/Automation/WorkbenchBindingV2.cs
EDC973A498C36CCA49C38AFD35D9A2DC17C96F8BD479A5818120AF6D57414CFE CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionV2.cs
4DA255F9561D647988797EC8B08B8E80CBE411FF058729B57C07FD0D8B19906E CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs
510731FD82B01BB597333A04E1934BD60B790DA48F2A4F0F5F09F706BDE68731 CryptoWorkBenchGuiTest/B3F1Tests.cs
9C53EF8D0A9816B7DAB8826F748A417BCC354EDA5D2A7366EC94CA3F680CB4E2 CryptoWorkBenchGuiTest/B3F2Tests.cs
A46E9936CBBE69BDFFB57DCE4A5FFC5C90FD8F6DF085C865DE9BF3590B6B8E46 CryptoWorkBenchGuiTest/B3F3Tests.cs
8B9F75C71CDC793F5216969AE7508B6B93FC2D7FC122A01A9F5603341E22D751 CryptoWorkBenchGuiTest/B3F4Tests.cs
8BD50B90DD2E0FDB2C561712AE0FF5304DA81E0499BCED86D07EB1F34FC3F669 CryptoWorkBenchGuiTest/LeaseV2Tests.cs
C85084D5EE9D15C6B723CDAC09B91F271DF60913BE8F5C4033842424083C7BB1 docs/mcp/MCP-B3-F1-corrections.md
9937089795E70CA0C58E2AC211688A730AA3FECD2CCA83AF68FCCD9F34E26BAC docs/mcp/MCP-B3-F2-corrections.md
77E1614CF3C45697A1F77AB0AFECF9F60A5D87FA23AB37726471CAEB7993F0CF docs/mcp/MCP-B3-F3-corrections.md
18EB1379A36CEECC539104B5268E1B5B4F66558B5D1E6EB2553019D8E383FA11 docs/mcp/MCP-B3-F4-corrections.md
79E784CE64A0002B487324A61512C2382096FAB4668CA7C742E6DB890CA118F2 docs/mcp/MCP-B3-mcp-binding-lease-v2.md
6E818EA6F0220D58281A014AE608B9FF427FA082CB9225DFBE524CAB2AE002DC eng/mcp/native/B3NativeProbe.csproj
CE6AC6C8925DD3311AB40D1A7CBFC896DA3FE3F3F40AE1E6C0DE0377DCCB3726 eng/mcp/native/Program.cs
~~~
