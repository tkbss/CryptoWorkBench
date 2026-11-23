# AR-01: Current Baseline Evidence

Implementation baseline: `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`. Verification: static source inspection on 2026-11-06. Test definitions are not executed test results. IDs are identical between languages. The shared JSON register contains full paths, commit, SHA-256 and line counts.

## R01

Production project declarations.

- [CryptoScript/CryptoScript.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/CryptoScript.csproj)
- [CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj)
- [CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia.Desktop/CryptoWorkBenchAvalonia.Desktop.csproj)
- [CryptoWorkBench.Automation.Abstractions/CryptoWorkBench.Automation.Abstractions.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.Automation.Abstractions/CryptoWorkBench.Automation.Abstractions.csproj)
- [CryptoWorkBench.Mcp/CryptoWorkBench.Mcp.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.Mcp/CryptoWorkBench.Mcp.csproj)

## R02

Solutions and verification projects.

- [CryptoScriptUnitTest/CryptoScriptUnitTest.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/CryptoScriptUnitTest.csproj)
- [CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj)
- [CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj)
- [CryptoWorkBench.sln](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.sln)
- [CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln)
- [eng/mcp/native/B3NativeProbe.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/eng/mcp/native/B3NativeProbe.csproj)
- [TestInfrastructure/Avalonia.Headless.Backport/Avalonia.Headless.Backport.csproj](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/TestInfrastructure/Avalonia.Headless.Backport/Avalonia.Headless.Backport.csproj)

## R03

Build and CI policy.

- [Directory.Build.props](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/Directory.Build.props)
- [Directory.Build.targets](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/Directory.Build.targets)
- [global.json](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/global.json)
- [NuGet.Config](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/NuGet.Config)
- [eng/prism/Lock.props](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/eng/prism/Lock.props)
- [eng/prism/ProjectScope.props](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/eng/prism/ProjectScope.props)
- [eng/prism/Prepare-Prism.ps1](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/eng/prism/Prepare-Prism.ps1)
- [eng/prism/Build.ps1](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/eng/prism/Build.ps1)
- [.github/workflows/dotnet.yml](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/.github/workflows/dotnet.yml)
- [.github/scripts/Invoke-PrismCi.ps1](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/.github/scripts/Invoke-PrismCi.ps1)
- [.github/scripts/Validate-DesktopPublish.ps1](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/.github/scripts/Validate-DesktopPublish.ps1)
- [CryptoScript/Native/FilePublication.targets](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Native/FilePublication.targets)

## R04

Startup and automation boundaries.

- [CryptoWorkBenchAvalonia.Desktop/Program.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia.Desktop/Program.cs)
- [CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs)
- [CryptoWorkBenchAvalonia/App.axaml.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/App.axaml.cs)
- [CryptoWorkBench.Mcp/LocalMcpHost.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.Mcp/LocalMcpHost.cs)
- [CryptoWorkBench.Mcp/PinnedMcpStdio.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.Mcp/PinnedMcpStdio.cs)
- [CryptoWorkBench.Automation.Abstractions/AutomationV2.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBench.Automation.Abstractions/AutomationV2.cs)

## R05

Host execution and presentation; ASCII ScriptBook saving and current loading path.

- [CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs)
- [CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs)
- [CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs)
- [CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs)
- [CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs)
- [CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs)
- [CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs)

## R06

Grammar and generated frontend.

- [CryptoScript/Antlr/CryptoScript.g4](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/CryptoScript.g4)
- [CryptoScript/Antlr/CryptoLexer.g4](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/CryptoLexer.g4)
- [CryptoScript/Antlr/TR31Lexer.g4](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/TR31Lexer.g4)
- [CryptoScript/Antlr/CryptoScriptLexer.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/CryptoScriptLexer.cs)
- [CryptoScript/Antlr/CryptoScriptParser.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/CryptoScriptParser.cs)
- [CryptoScript/Antlr/CryptoScriptLexerExtensions.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/CryptoScriptLexerExtensions.cs)
- [CryptoScript/Antlr/CompileGrammar.bat](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/CompileGrammar.bat)
- [CryptoScript/Antlr/compile-grammar.sh](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Antlr/compile-grammar.sh)

## R07

Parser construction and syntax state.

- [CryptoScript/Model/ParserBuilder.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/ParserBuilder.cs)
- [CryptoScript/ErrorListner/LexerErrorListener.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/ErrorListner/LexerErrorListener.cs)
- [CryptoScript/ErrorListner/SyntaxErrorListner.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/ErrorListner/SyntaxErrorListner.cs)
- [CryptoScript/ErrorListner/SyntaxErrorList.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/ErrorListner/SyntaxErrorList.cs)

## R08

Syntax records.

- [CryptoScript/Model/Ast/StatementNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/StatementNode.cs)
- [CryptoScript/Model/Ast/AssignmentStatementNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/AssignmentStatementNode.cs)
- [CryptoScript/Model/Ast/VariableDeclarationNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/VariableDeclarationNode.cs)
- [CryptoScript/Model/Ast/ExpressionNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/ExpressionNode.cs)
- [CryptoScript/Model/Ast/LiteralInitializerNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/LiteralInitializerNode.cs)
- [CryptoScript/Model/Ast/FunctionCallExpressionNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/FunctionCallExpressionNode.cs)
- [CryptoScript/Model/Ast/ParameterListInitializerNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/ParameterListInitializerNode.cs)
- [CryptoScript/Model/Ast/ParameterInitializerNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/ParameterInitializerNode.cs)
- [CryptoScript/Model/Ast/Tr31HeaderInitializerNode.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/Ast/Tr31HeaderInitializerNode.cs)

## R09

Parse-tree mapping.

- [CryptoScript/Model/AntlrToStatement.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AntlrToStatement.cs)
- [CryptoScript/Model/AntlrToAssignment.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AntlrToAssignment.cs)
- [CryptoScript/Model/AntlrToVariableDeclaration.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AntlrToVariableDeclaration.cs)
- [CryptoScript/Model/AntlrToFunctionCallExpression.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AntlrToFunctionCallExpression.cs)
- [CryptoScript/Model/AntlrToParameterInitializer.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AntlrToParameterInitializer.cs)

## R10

Declaration and assignment evaluation.

- [CryptoScript/Model/StatementEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/StatementEvaluator.cs)
- [CryptoScript/Model/AssignmentEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AssignmentEvaluator.cs)
- [CryptoScript/Model/VariableDeclarationEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/VariableDeclarationEvaluator.cs)
- [CryptoScript/Model/ParameterInitializerEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/ParameterInitializerEvaluator.cs)
- [CryptoScript/Model/PathInitializerEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/PathInitializerEvaluator.cs)
- [CryptoScript/Expressions/Expression.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Expressions/Expression.cs)

## R11

Arguments and invocation identity.

- [CryptoScript/Model/FunctionCallEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/FunctionCallEvaluator.cs)
- [CryptoScript/Model/FunctionCall.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/FunctionCall.cs)
- [CryptoScript/Model/OperationInvocation.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/OperationInvocation.cs)
- [CryptoScript/Arguments/Argument.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Arguments/Argument.cs)
- [CryptoScript/Model/ParameterEvaluator.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/ParameterEvaluator.cs)
- [CryptoScript/CryptoAlgorithm/AlgorithmCallArguments.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/CryptoAlgorithm/AlgorithmCallArguments.cs)
- [CryptoScript/CryptoAlgorithm/AlgorithmArgumentResolver.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/CryptoAlgorithm/AlgorithmArgumentResolver.cs)

## R12

Runtime types and variable state.

- [CryptoScript/Model/CryptoType.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/CryptoType.cs)
- [CryptoScript/Variables/VariableDictionary.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Variables/VariableDictionary.cs)
- [CryptoScript/Variables/KeyVariableDeclaration.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Variables/KeyVariableDeclaration.cs)
- [CryptoScript/Variables/ParameterVariableDeclaration.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Variables/ParameterVariableDeclaration.cs)
- [CryptoScript/Variables/FormatConversions.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Variables/FormatConversions.cs)
- [CryptoScript/Model/AntlrLanguageMetadata.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/AntlrLanguageMetadata.cs)
- [CryptoScript/Model/MechanismList.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/MechanismList.cs)
- [CryptoScript/Variables/ParameterTypeList.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Variables/ParameterTypeList.cs)

## R13

Runner and interpreter ownership.

- [CryptoScript/Model/CryptoScriptRunner.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/CryptoScriptRunner.cs)
- [CryptoScript/Model/InterpreterExecution.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/InterpreterExecution.cs)
- [CryptoScript/Model/CryptoScriptProgram.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/CryptoScriptProgram.cs)
- [CryptoScript/Model/SemanticDiagnosticExecution.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Model/SemanticDiagnosticExecution.cs)
- [CryptoScript/ErrorListner/DiagnosticLocation.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/ErrorListner/DiagnosticLocation.cs)

## R14

Operation dispatch.

- [CryptoScript/Operations/OperationFactory.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/OperationFactory.cs)
- [CryptoScript/CryptoAlgorithm/AlgorithmFactory.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/CryptoAlgorithm/AlgorithmFactory.cs)
- [CryptoScript/Operations/OutputOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/OutputOperations.cs)
- [CryptoScript/Operations/HeaderOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/HeaderOperations.cs)
- [CryptoScript/Operations/SnapshotOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/SnapshotOperations.cs)
- [CryptoScript/Operations/SecureSaveOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/SecureSaveOperations.cs)
- [CryptoScript/Operations/SecureLoadOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/SecureLoadOperations.cs)
- [CryptoScript/Operations/DeleteOperations.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Operations/DeleteOperations.cs)

## R15

Help and file contracts.

- [CryptoScript/Documentation/FileInfoDocumentationProvider.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Documentation/FileInfoDocumentationProvider.cs)
- [CryptoScript/Documentation/InfoDocumentCatalog.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/Documentation/InfoDocumentCatalog.cs)
- [CryptoScript/FileEncryption/FileEncryptionContract.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/FileEncryption/FileEncryptionContract.cs)
- [CryptoScript/FileEncryption/FileMacContract.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/FileEncryption/FileMacContract.cs)
- [CryptoScript/FileEncryption/FileHashContract.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScript/FileEncryption/FileHashContract.cs)

## R16

Inspected regression definitions.

- [CryptoScriptUnitTest/AssignmentTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/AssignmentTests.cs)
- [CryptoScriptUnitTest/AssignmentCorrectionTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/AssignmentCorrectionTests.cs)
- [CryptoScriptUnitTest/ParameterAssignmentConsistencyTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/ParameterAssignmentConsistencyTests.cs)
- [CryptoScriptUnitTest/ParameterAssignmentSerializationTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/ParameterAssignmentSerializationTests.cs)
- [CryptoWorkBenchGuiTest/AssignmentMcpIntegrationTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchGuiTest/AssignmentMcpIntegrationTests.cs)
- [CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs)
- [CryptoScriptUnitTest/Tr31HeaderInitializerAstTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/Tr31HeaderInitializerAstTests.cs)
- [CryptoScriptUnitTest/FunctionCallEvaluatorTests.cs](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/CryptoScriptUnitTest/FunctionCallEvaluatorTests.cs)

## Removed Sources

The following paths are absent from the pinned Git tree; their earlier existence remains historically documented.

- `CryptoWorkBenchConsole/CryptoWorkBenchConsole.csproj`
- `CryptoWorkBenchConsole/Program.cs`
- `CryptoWorkBenchGuiTest/ConsoleSemanticDiagnosticTests.cs`
- `CryptoScript/CryptoAlgorithm/AsymmetricCryptoAlgorithm.cs`
- `CryptoScript/CryptoAlgorithm/RSA/Rsa.cs`

## Historical Evidence

Original evidence and approvals remain tied to their original baselines. Current drafts do not inherit earlier approval. Original files are also retained byte-for-byte beneath evidence/history/abbf7b7b; relative links inside those copies belong to the original tree. The immutable links below provide navigable originals.

- [README.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/README.md)
- [chapters/01-introduction.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/chapters/01-introduction.md)
- [chapters/02-system-overview.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/chapters/02-system-overview.md)
- [chapters/03-project-structure-and-dependencies.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/chapters/03-project-structure-and-dependencies.md)
- [chapters/04-language-architecture.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/chapters/04-language-architecture.md)
- [evidence/01-introduction-baseline.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/evidence/01-introduction-baseline.md)
- [evidence/02-system-overview-baseline.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/evidence/02-system-overview-baseline.md)
- [evidence/03-project-structure-and-dependencies-baseline.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/evidence/03-project-structure-and-dependencies-baseline.md)
- [tools/REVIEW-METADATA.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/tools/REVIEW-METADATA.md)
- [tools/VALIDATION.md](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/tools/VALIDATION.md)
