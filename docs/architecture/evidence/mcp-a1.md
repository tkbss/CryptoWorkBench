# MCP source evidence

IDs E01–E30 come from AR-MCP-01-A1. Each link opens the first reviewed source range in that file. The machine-readable register records all ranges, symbols and SHA-256 digests. E22–E25 identify test definitions. E26–E30 identify documentation and historical reports.

Implementation baseline `8c58e2bd564280b71412cfd6ed9997688bd0bde4`.

### E01

**Production reference graph**

[CryptoScript.csproj](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoScript/CryptoScript.csproj#L1-L31) · [CryptoWorkBench.Automation.Abstractions.csproj](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Automation.Abstractions/CryptoWorkBench.Automation.Abstractions.csproj#L1-L3) · [CryptoWorkBench.Mcp.csproj](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/CryptoWorkBench.Mcp.csproj#L2-L7) · [CryptoWorkBenchAvalonia.csproj](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj#L24-L50) · [CryptoWorkBenchAvalonia.Desktop.csproj](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj#L3-L19) · [CryptoWorkBenchAvalonia.sln](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln#L6-L27).

### E02

**Application composition**

[Program.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia.Desktop/Program.cs#L13-L48) · [App.axaml.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/App.axaml.cs#L30-L32) · [DockShellStartup.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs#L9-L25).

### E03

**Desktop activation and disconnection**

[DesktopMcpSession.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs#L26-L81) · [McpDisconnect.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/McpDisconnect.cs#L6-L41).

### E04

**Endpoint and session identity**

[LocalMcpHost.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/LocalMcpHost.cs#L16-L83) · [LocalMcpIdentity.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/LocalMcpIdentity.cs#L16-L64).

### E05

**Proxy trust and forwarding**

[PinnedMcpStdio.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/PinnedMcpStdio.cs#L16-L32).

### E06

**Tool discovery and invocation**

[WorkbenchToolsV2.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/WorkbenchToolsV2.cs#L15-L53) · [StatusTransport.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/StatusTransport.cs#L11-L25).

### E07

**Host-neutral types and validation**

[AutomationV2.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Automation.Abstractions/AutomationV2.cs#L5-L34) · [AutomationContracts.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Automation.Abstractions/AutomationContracts.cs#L6-L35) · [IWorkbenchAutomationService.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Automation.Abstractions/IWorkbenchAutomationService.cs#L3-L18) · [AutomationJson.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Automation.Abstractions/AutomationJson.cs#L15-L67) · [AutomationSchemas.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Automation.Abstractions/AutomationSchemas.cs#L9-L78).

### E08

**Host-issued authority and epoch**

[WorkbenchAutomationRegistry.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs#L18-L47).

### E09

**Single validated dispatch**

[SafeMcpParseErrors.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp/SafeMcpParseErrors.cs#L19-L69).

### E10

**Registration and dispatcher ownership**

[EditorBridge.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs#L27-L74) · [WorkbenchEditorOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs#L16-L32) · [UiDispatch.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/UiDispatch.cs#L10-L32).

### E11

**Native editor coordination**

[EditorCoordinator.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs#L30-L69) · [WorkbenchTextEditor.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs#L198-L272).

### E12

**Acquisition, renewal and retirement**

[ProcessAcquisitions.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/ProcessAcquisitions.cs#L6-L57) · [WorkbenchLeaseV2.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs#L14-L46).

### E13

**V2 reads, writes and run approval**

[WorkbenchBindingV2.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchBindingV2.cs#L15-L74) · [WorkbenchExecutionV2.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionV2.cs#L12-L55).

### E14

**Execution admission and cancellation**

[WorkbenchAutomationRegistry.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs#L74-L111).

### E15

**Host reservation and Core ownership**

[InterpreterHostQueue.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs#L8-L39) · [InterpreterExecution.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoScript/Model/InterpreterExecution.cs#L5-L74) · [CryptoScriptRunner.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoScript/Model/CryptoScriptRunner.cs#L78-L160) · [VariableDictionary.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoScript/Variables/VariableDictionary.cs#L6-L63).

### E16

**Parse units and host callbacks**

[WorkbenchAutomationService.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs#L13-L64) · [WorkbenchExecutionPipeline.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs#L35-L80) · [PasswordDialogHost.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/PasswordDialogHost.cs#L13-L61).

### E17

**Final state, retention and recovery**

[WorkbenchAutomationRegistry.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs#L129-L177).

### E18

**Projection boundary**

[AutomationProjection.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/AutomationProjection.cs#L11-L48) · [VariablePresentationCapture.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs#L9-L58).

### E19

**Interactive GUI and dock behavior**

[CryptoScriptEditViewModel.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs#L213-L245) · [DockEditorSession.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs#L85-L126) · [DockOutputTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/DockOutputTests.cs#L174-L187) · [DockToolTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/DockToolTests.cs#L162-L187).

### E20

**Local save and inspector operations**

[WorkbenchSaveCommit.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs#L16-L68) · [DockEditorSession.Files.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs#L72-L93) · [WorkbenchInspectorRead.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchInspectorRead.cs#L14-L64).

### E21

**Shutdown policy**

[App.axaml.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/App.axaml.cs#L125-L170) · [WorkbenchAutomationRegistry.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs#L753-L799).

### E22

**Transport test definitions**

[BindingV2Tests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/BindingV2Tests.cs#L27-L53) · [B3F1WireTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/B3F1WireTests.cs#L66-L93) · [B3F4WireTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs#L127-L151) · [LocalMcpHostTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/LocalMcpHostTests.cs#L13-L52) · [TransportTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/TransportTests.cs#L74-L89) · [B4F1SecurityTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/B4F1SecurityTests.cs#L72-L97) · [B4F2SecurityTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBench.Mcp.Tests/B4F2SecurityTests.cs#L23-L51).

### E23

**Lease and native editor test definitions**

[LeaseV2Tests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/LeaseV2Tests.cs#L23-L74) · [EditorCoordinatorTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs#L114-L146).

### E24

**Execution and projection test definitions**

[WorkbenchAutomationTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/WorkbenchAutomationTests.cs#L38-L74) · [AssignmentMcpIntegrationTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/AssignmentMcpIntegrationTests.cs#L12-L47) · [WorkbenchRegistryTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs#L51-L124).

### E25

**Local file and inspector test definitions**

[EditorAutomationTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/EditorAutomationTests.cs#L549-L596) · [InspectorReadTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/CryptoWorkBenchGuiTest/InspectorReadTests.cs#L27-L36).

### E26

**Edition and publication history**

[README.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/README.md#L3-L39) · [README.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/de/README.md#L3-L39) · [publication.json](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/publication.json#L2-L44) · [ar-01-bilingual-publication.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/decisions/ar-01-bilingual-publication.md#L5-L37) · [AR-01-integration-report.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/AR-01-integration-report.md#L5-L28).

### E27

**Publication implementation**

[publication.mjs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/tools/publication.mjs#L5-L34) · [bilingual-checks.mjs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/tools/bilingual-checks.mjs#L25-L66) · [generate.mjs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/tools/generate.mjs#L239-L260) · [locales.mjs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/tools/locales.mjs#L6-L19) · [validate.mjs](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/tools/validate.mjs#L24-L31) · [README.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/tools/README.md#L3-L27).

### E28

**Chapter text and diagrams**

[01-introduction.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/chapters/01-introduction.md#L24-L32) · [02-system-overview.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/chapters/02-system-overview.md#L12-L18) · [03-project-structure-and-dependencies.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/chapters/03-project-structure-and-dependencies.md#L20-L24) · [04-language-architecture.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/chapters/04-language-architecture.md#L12-L18) · [09-data-flows-and-execution-sequences.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/chapters/09-data-flows-and-execution-sequences.md#L5-L23) · [01-project-references.mmd](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/diagrams/01-project-references.mmd#L3-L14) · [02-system-context.mmd](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/diagrams/02-system-context.mmd#L3-L10) · [02-container-overview.mmd](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/diagrams/02-container-overview.mmd#L3-L19) · [03-build-and-test-dependency-coverage.mmd](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/architecture/diagrams/03-build-and-test-dependency-coverage.mmd#L3-L18).

### E29

**Design record**

[MCP-00-architecture.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/mcp/MCP-00-architecture.md#L3-L13) · [MCP-00-R2-consolidated-architecture-decision.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/mcp/MCP-00-R2-consolidated-architecture-decision.md#L3-L19).

### E30

**Pairing guide and historical review**

[MCP-pinned-client-setup.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/mcp/MCP-pinned-client-setup.md#L3-L6) · [MCP-B4-R3-independent-review.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/mcp/MCP-B4-R3-independent-review.md#L34-L49) · [MCP-B4-P2-windows-consolidation-and-macos-preparation.md](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/docs/mcp/MCP-B4-P2-windows-consolidation-and-macos-preparation.md#L3-L11).
