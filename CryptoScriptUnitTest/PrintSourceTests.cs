using CryptoScript.Model;
using CryptoScript.Model.Ast;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public sealed class PrintSourceTests
{
    [Test] public void ParserMapsNestedTokensWithoutSearchingText()
    {
        var parser = ParserBuilder.StringBuild("  Print(Concat(\"Print\",\"x\"))");
        var node = AntlrToFunctionCallExpression.Map(parser.program().statement(0).functionCall());
        Assert.That(node.Source, Is.EqualTo(new SourcePosition(1, 3)));
        Assert.That(((NestedCallArgumentNode)node.Arguments[0]).Call.Source, Is.EqualTo(new SourcePosition(1, 9)));
    }

    [Test] public async Task StructuredEventPreservesLegacyTextAndTokenPositions()
    {
        var legacy = new List<string>(); var structured = new List<OutputOperations.PrintResult>();
        OutputOperations.PrintEvent += legacy.Add; OutputOperations.PrintResultEvent += structured.Add;
        try
        {
            await new CryptoScriptRunner().ExecuteAsync(ParserBuilder.StringBuild("  Print(0x(AB))\n    Print(0x(CD))").program());
            Assert.That(structured.Select(p => p.Text), Is.EqualTo(legacy));
            Assert.That(structured.Select(p => p.Source), Is.EqualTo(new[] { new SourcePosition(1, 3), new SourcePosition(2, 5) }));
            Assert.That(legacy, Is.EqualTo(new[] { "out: 0x(AB)", "out: 0x(CD)" }));
        }
        finally { OutputOperations.PrintEvent -= legacy.Add; OutputOperations.PrintResultEvent -= structured.Add; }
    }

    [Test] public void LegacyDirectPrintHasExplicitlyUnknownSource()
    {
        OutputOperations.PrintResult? actual = null;
        void Capture(OutputOperations.PrintResult value) => actual = value;
        OutputOperations.PrintResultEvent += Capture;
        try
        {
            new OutputOperations().Print(["ä€😀\nYWJj=="]);
            Assert.That(actual!.Source, Is.Null); Assert.That(actual.Text, Is.EqualTo("out: ä€😀\nYWJj=="));
        }
        finally { OutputOperations.PrintResultEvent -= Capture; }
    }

    [Test] public void SyntheticAstDoesNotInventSource()
    {
        var node = new FunctionCallExpressionNode("Print", "Print(1)", [new LiteralArgumentNode("1")]);
        Assert.That(node.Source, Is.Null);
        Assert.That(new OperationInvocation([]).Source, Is.Null);
    }
}
