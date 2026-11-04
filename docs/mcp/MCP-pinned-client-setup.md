# Lokales MCP-Pairing ab B4-F2

Der freigegebene Codex-Verbindungspfad verwendet den integrierten STDIO-Adapter. Er bindet TLS an den SHA-256-Fingerabdruck genau des im Workbench-Pairingdialog gezeigten Serverzertifikats. Zusätzliche CA-Bundles oder Proxys erweitern diese Bindung nicht. Die früheren direkten HTTP-/HTTPS-Konfigurationen aus B4/F1 sind historische Versuchsanordnungen und keine sichere F2-Pairinganleitung.

1. In einer frischen Windows-Workbench den synthetischen Testeditor öffnen und lokal `Connect Codex` erlauben.
2. Vor dem Start des Clients `CRYPTOWORKBENCH_MCP_CERT_SHA256` auf den angezeigten Fingerabdruck und `CRYPTOWORKBENCH_MCP_TOKEN` auf das angezeigte Sitzungstoken setzen. Beides aus demselben authentischen Dialog übernehmen. Das Token gehört ausschliesslich in die lokale Prozessumgebung, nicht in TOML, Befehlsargumente, Chat, Script Book oder Logs.
3. In der Codex-Konfiguration den absoluten Pfad des zugehörigen Workbench-Apphosts einsetzen:

```toml
[mcp_servers.cryptoworkbench]
command = 'C:\path\to\CryptoWorkBenchAvalonia.Desktop.exe'
args = ['--mcp-stdio']
env_vars = ['CRYPTOWORKBENCH_MCP_CERT_SHA256', 'CRYPTOWORKBENCH_MCP_TOKEN']
tool_timeout_sec = 150
```

Kein `url` oder `bearer_token_env_var` für diesen Eintrag konfigurieren. `CODEX_CA_CERTIFICATE` ist für diesen Verbindungspfad nicht erforderlich. STDIO-`command`, `args` und weitergereichte Umgebungsvariablen sind reguläre Codex-MCP-Einstellungen. [OpenAI Docs](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

Der Adapter startet keine GUI und besitzt keine Editor-, Credential-Issuing- oder Ausführungsregistry. Die Workbench bleibt alleinige Autorität. Das Sitzungstoken wird nur nach erfolgreicher Zertifikatsbindung über `https://127.0.0.1:51837/mcp` übertragen. Das Zertifikat muss gültig sein; eine andere CA, ein neues Workbench-Zertifikat oder eine Weiterleitung wird nicht automatisch akzeptiert. Der Adapter verwendet keinen Proxy und installiert keine Zertifikate im Betriebssystem.

`Disconnect Codex` widerruft die Sitzung. Ausstehende Arbeit muss weiter über den ursprünglichen Coordinator abgeschlossen werden. Ein neuer Workbench-Prozess erfordert neues Pairing und einen neu gestarteten Client/Adapter. Nach unklarem Transportausgang keine Mutation automatisch wiederholen: über den Originalzustand beziehungsweise die bekannte Execution-ID prüfen, bevor erneut gehandelt wird.

**Präzisierung nach R3:** Der Adapter besitzt keine eigene Wiederholungsschleife; die verwendete SDK-Methode `SendRequestAsync` kann jedoch automatische MRTR-Fortsetzungen ausführen. R3 hat nach einer injizierten MRTR-Antwort einen zweiten Dispatch nachgewiesen. Die unveränderte CAS-Revision verhinderte im geprüften Edit-Fall einen zweiten Text-, Revisions- oder History-Effekt. Der aktuelle Workbench-Server erzeugt diese MRTR-Antwort nicht. Das ist keine allgemeine Garantie gegen automatische Fortsetzungen oder wiederholte Toolaufrufe. Bei gewöhnlichem Antwortverlust wurde separat nur ein Dispatch beobachtet. Details und Abnahmegrenzen: [B4-R3-02 und Abschnitt 5](MCP-B4-R3-independent-review.md).

Der zweite akzeptierte Low-Restbefund **B4-R3-01** betrifft unbekannte Methoden mit `params: []` oder `params: null`: Direktes authentisiertes HTTPS kann den Methodennamen in der Fehlerantwort spiegeln; der freigegebene STDIO-Ausgang bereinigt diese Fälle. STDIO-EOF ist ausserdem kein sofortiger Jobabbruch oder Credential-Widerruf. Laufende Arbeit bleibt an den ursprünglichen Coordinator und die Produktfristen gebunden; vor dem Clientende nach Möglichkeit explizit abbrechen. Diese Hinweise ergänzen die historische F2-Dokumentation, ohne deren damalige Ergebnisse umzuschreiben.

Andere MCP-Clients können denselben STDIO-Apphost und dieselben beiden Umgebungsvariablen verwenden, benötigen aber eigene Kompatibilitätsnachweise. Ein direkter HTTPS-Client muss die gleiche exklusive Zertifikatsbindung vor HTTP implementieren; blosses CA-Vertrauen bietet diese Garantie nicht. Die MCP-Aktivierung bleibt auf Linux/macOS deaktiviert. Eine native Desktop-Plattformabnahme ist keine Freigabe der dortigen MCP-Aktivierung.
