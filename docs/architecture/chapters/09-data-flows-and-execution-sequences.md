# 9. Data Flows and Execution Sequences

[Architecture documentation](../README.md) | [Evidence conventions](../README.md#evidence-conventions)

| Metadata | Value |
| --- | --- |
| Status | Partial chapter: approved partial-state contract documented; documentation awaiting owner review |
| Implementation baseline | `b2ed0299d33b49d1fa7a6c9f0d75b12fd3d8c9cf` |
| Analysis date | 2026-10-28 |
| Verification | Static implementation and regression-test inspection; no new test run for this documentation change |
| Decision basis | A12-08-F1 owner decision F03; A12-08-F1-R1 independent review ACCEPTED |

## 9.1 Scope

This section documents execution termination, diagnostic transport, state capture and variable presentation for an admitted workbench submission. It records the established contract; it does not introduce a script transaction or change an operation's publication guarantees. Other data flows and the broader GUI architecture remain outside this partial chapter.

## 9.2 Sequential Execution and Partial State

**[F]** `CryptoScriptRunner` evaluates statements sequentially. Successfully completed statements retain their published effects unless a later operation changes them under its own contract. An execution-terminating exception leaves the evaluation loop, so subsequent statements are not executed. Failure does not automatically roll back the entire script or remove results published by earlier statements. The failed operation retains its existing atomicity, cleanup and publication guarantees; partial-state presentation neither weakens those guarantees nor adds new ones.

A semantic diagnostic is not necessarily an execution-terminating exception. Existing CryptoScript policy permits some diagnostics, including some error-severity diagnostics, while evaluation continues. The workbench must not infer termination or rollback solely from diagnostic severity. `ContinuingErrorCompletesAndLaterThrownErrorPublishesPartialState` exercises both a continuing error and a later thrown error. Syntax rejection is a separate pre-execution path: the rejected submission does not execute its statements.

Sources: [`CryptoScriptRunner.ExecuteAsyncCore`](../../../CryptoScript/Model/CryptoScriptRunner.cs), [`WorkbenchExecutionPipeline.ExecuteAsync`](../../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs) and [`WorkbenchAutomationTests`](../../../CryptoWorkBenchGuiTest/WorkbenchAutomationTests.cs). Representative operation-level guarantees remain covered by `MissingSourceDoesNotRegisterResult`, `PublicationFailureRemovesTemporary` and `CancelBeforePublicationRemovesTemporary` in [`FileEncryptionTests`](../../../CryptoScriptUnitTest/FileEncryptionTests.cs).

## 9.3 From Execution to GUI Presentation

**[F]** The workbench uses the following sequence for an admitted submission:

1. The host obtains admission and enters the authorized execution context. `WorkbenchExecutionPipeline` parses the submission and, when parsing succeeds, awaits Runner execution. Presentation does not acquire or pass an independent nested execution lease into that operation.
2. The Runner completes or exits through an execution-terminating exception. The pipeline records the outcome and waits for pending password-host callbacks to unwind before final capture; host admission and the core gate remain held through that work.
3. The pipeline copies the Runner's diagnostic snapshot in its existing order. `FinalizePresentation` calls `InterpreterExecution.AccessState(VariablePresentationCapture.Capture)` to copy the actual Runner-visible variable state through the authorized state-access path. It does not reconstruct an assumed all-or-nothing result from the success flag.
4. `CryptoScriptEditViewModel.Present` appends diagnostics and updates status. If capture succeeded, it applies the captured variable models even when the script terminated with an error. The error outcome and a valid partial-state snapshot can therefore coexist.
5. If capture failed, the GUI marks its existing variable presentation stale and does not apply the failed capture's placeholder collection. It preserves the previously displayed variables rather than overwriting them with an invalid empty snapshot. A later successful authorized capture and application can clear the stale flag.

Diagnostic ordering and duplicate suppression are specific: the pipeline preserves the identity relationship between an exception diagnostic and the corresponding copied snapshot entry. `StatusViewModel.AppendDiagnostics` appends that exception diagnostic only if it was not already transported in the sequence. It does not globally collapse distinct diagnostics merely because their text matches. A variable-presentation failure must not append the same execution diagnostics again.

Capture failure and failure while applying an otherwise valid snapshot are separate cases. `VariableViewModel.ApplyVariables` marks the presentation stale and rethrows if collection updates fail; `Present` reports a presentation failure. This is not a transactional rollback guarantee for GUI collection updates. Neither case changes the completed Runner outcome or authorizes replaying the script. Stale presentation is not evidence that the Runner has no state.

These steps remain within the existing MCP/host admission and execution-state boundaries. The automation registry likewise retains its previous variable snapshot with a stale marker when capture fails; this contract does not authorize a caller to bypass admission or read live mutable state directly.

Sources: [`WorkbenchExecutionPipeline`](../../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs), [`InterpreterHostQueue`](../../../CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs), [`VariablePresentationCapture`](../../../CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs), [`CryptoScriptEditViewModel.Present`](../../../CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs), [`VariableViewModel.ApplyVariables`](../../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs), [`StatusViewModel.AppendDiagnostics`](../../../CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs) and [`WorkbenchAutomationRegistry`](../../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs).

## 9.4 Observable Behavior and Decision History

The representative sequence is a successful declaration of `before`, a statement that reports a warning and then terminates execution with an error, followed by a declaration of `after`. The completed `before` result remains in the Runner state and is included in the GUI snapshot. The declaration of `after` is not executed. Warning and error diagnostics retain their sequence and are presented once; the execution still reports failure.

The A12-08-F1 owner decision F03 explicitly superseded the earlier master expectation represented by `WarningBeforeErrorUsesRunnerSnapshotWithoutDuplicateOrVariableRefresh`, which expected an empty variable presentation after the error. The current regression is `WarningBeforeErrorUsesRunnerSnapshotWithoutDuplicateAndRefreshesPartialState` in [`CryptoScriptExecutionPresentationTests`](../../../CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs). The old no-refresh assertion is historical, not an alternative supported contract.

A12-08-F1 records the approval; the ACCEPTED A12-08-F1-R1 independent review confirms the implementation and regression mapping. Those historical reports remain external review evidence and are not copied into this chapter or linked as if they were versioned repository files. This section preserves the approved decision in the developer architecture documentation at the implementation baseline above.

## 9.5 Representative Regression Evidence

**[F]** The following existing tests specify the observable boundaries. Their assertions were inspected for this documentation task; their earlier execution results remain historical F1/R1 evidence.

| Concern | Representative regression |
| --- | --- |
| Partial state, termination and ordered diagnostics | `WarningBeforeErrorUsesRunnerSnapshotWithoutDuplicateAndRefreshesPartialState` in [presentation tests](../../../CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs) |
| Continuing diagnostics versus thrown errors | `ContinuingErrorCompletesAndLaterThrownErrorPublishesPartialState` in [automation tests](../../../CryptoWorkBenchGuiTest/WorkbenchAutomationTests.cs) |
| Successful refresh and syntax rejection | `SuccessfulInteractiveExecutionClearsOldDiagnosticsAndUpdatesVariables` and `SyntaxErrorClearsOldWarningsAndDoesNotExecute` in [presentation tests](../../../CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs) |
| Presentation failure without duplicate diagnostics | `VariableRefreshFailureDoesNotAppendSuccessfulDiagnosticsAgain` in [presentation tests](../../../CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs) |
| Failed capture preserves the displayed variables and marks them stale | `GuiKeepsOriginalStatusAndDiagnosticsAndMarksUnchangedVariablesStale` in [review regressions](../../../CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs) |

This documentation does not add assertions or change these regressions. The optional H01 suggestion in A12-08-F1-R1 remains outside this task.
