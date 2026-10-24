using CryptoScript.Documentation;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class CwmInfoTests
{
    [TestCase("junk", "true")] [TestCase("True", "true")]
    [TestCase("false", "false")] [TestCase("true", "true")]
    [NonParallelizable]
    public void ValEditingPreservesBooleanDomain(string edit, string expected)
    {
        using var lease = InterpreterExecution.Enter();
        var dictionary = CryptoScript.Variables.VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        try
        {
            dictionary.Clear();
            var value = new CryptoScript.Variables.ValVariableDeclaration(true) { Id = "valid" };
            dictionary.Add(value);
            var vm = new VariableViewModel(); vm.SetupVariables();
            vm.SelectedVariable = vm.DataVariables.Single();
            vm.SelectedVariable.Value = edit;
            Assert.DoesNotThrow(() => vm.ModifyValueCommand.Execute(vm.SelectedVariable));
            Assert.That(value.Value, Is.EqualTo(expected));
            Assert.That(vm.DataVariables.Single().Value, Is.EqualTo(expected));
        }
        finally
        {
            dictionary.Clear();
            foreach (var value in previous) dictionary.Add(value);
        }
    }

    [TestCase("MacPack")] [TestCase("MacVerify")]
    public void CompletionOverlayAndDeployedHelp(string function)
    {
        Assert.That(OperationList.Operations, Does.Contain(function));
        Assert.That(new InfoCompletionProvider().GetCompletionItems("Info(", 5), Does.Contain(function));
        Assert.That(new FunctionOverlayDictionaryModel().GetOverlays(function + "(", function.Length + 1), Is.Not.Null);
        var provider = new FileInfoDocumentationProvider();
        Assert.That(provider.TryGetDocument(InfoDocumentId.CreateFunction(function), out string page), Is.True);
        Assert.That(page, Does.Contain("HKDF-SHA256").And.Contain("RFC8785-JCS"));
        var vm = new InfoViewModel(null, provider);
        vm.SetInfoText("Start");
        vm.OpenInfoLinkCommand.Execute("cryptoscript-info://function/" + function);
        Assert.That(vm.InfoText, Is.EqualTo(page));
        vm.NavigateBackCommand.Execute(); Assert.That(vm.InfoText, Is.EqualTo("Start"));
    }
}
