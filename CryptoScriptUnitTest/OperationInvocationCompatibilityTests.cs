using CryptoScript.Documentation;
using CryptoScript.Model;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class OperationInvocationCompatibilityTests
{
    [Test]
    public void ValuesProjectionPreservesOrderAndExactStrings()
    {
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument("AES-CBC", ResolvedCallArgumentKind.Mechanism),
            new ResolvedCallArgument("0x(AbCd)", ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument("#IV:0x(0011)", ResolvedCallArgumentKind.Parameter),
            new ResolvedCallArgument("functions", ResolvedCallArgumentKind.Info)
        });

        Assert.That(invocation.Values,
            Is.EqualTo(new[] { "AES-CBC", "0x(AbCd)", "#IV:0x(0011)", "functions" }));
    }

    [Test]
    public void EmptyArgumentPreservesItsLegacyNullPosition()
    {
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument("first", ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument(null, ResolvedCallArgumentKind.Empty),
            new ResolvedCallArgument("third", ResolvedCallArgumentKind.Expression)
        });

        Assert.That(invocation.Values, Is.EqualTo(new string?[] { "first", null, "third" }));
        Assert.That(invocation.Arguments[1].SourceVariable, Is.Null);
    }

    [Test]
    public void LegacyAndInvocationComparePathsReturnTheSameResult()
    {
        string[] values = ["0x(AB)", "0x(ab)"];

        var legacy = OperationFactory.CreateOperation("Compare")(values);
        var invocation = OperationFactory.CreateInvocationOperation("Compare")(
            Invocation(values, ResolvedCallArgumentKind.Expression));

        Assert.Multiple(() =>
        {
            Assert.That(invocation.Value, Is.EqualTo(legacy.Value));
            Assert.That(invocation.ValueFormat, Is.EqualTo(legacy.ValueFormat));
            Assert.That(invocation.GetType(), Is.EqualTo(legacy.GetType()));
        });
    }

    [Test]
    public void LegacyAndInvocationPrintPathsEmitTheSameOutput()
    {
        var output = new List<string>();
        void Capture(string value) => output.Add(value);
        OutputOperations.PrintEvent += Capture;
        try
        {
            OperationFactory.CreateOperation("Print")(["0x(AbCd)"]);
            OperationFactory.CreateInvocationOperation("Print")(
                Invocation(["0x(AbCd)"], ResolvedCallArgumentKind.Expression));
        }
        finally
        {
            OutputOperations.PrintEvent -= Capture;
        }

        Assert.That(output, Is.EqualTo(new[] { "out: 0x(AbCd)", "out: 0x(AbCd)" }));
    }

    [Test]
    public void LegacyAndInvocationInfoPathsEmitTheSameOutput()
    {
        var output = new List<string>();
        void Capture(string value) => output.Add(value);
        var operations = new OutputOperations(new FixedInfoDocumentationProvider("documentation"));
        OutputOperations.InfoEvent += Capture;
        try
        {
            operations.Info(["functions"]);
            operations.Info(Invocation(["functions"], ResolvedCallArgumentKind.Info));
        }
        finally
        {
            OutputOperations.InfoEvent -= Capture;
        }

        Assert.That(output, Is.EqualTo(new[] { "documentation", "documentation" }));
    }

    [Test]
    public void LegacyAndInvocationCryptoPathsReturnTheSameHash()
    {
        string[] values = ["#MECH:HASH-SHA256", "0x(616263)"];

        var legacy = OperationFactory.CreateOperation("Hash")(values);
        var invocation = OperationFactory.CreateInvocationOperation("Hash")(
            Invocation(values, ResolvedCallArgumentKind.Expression));

        Assert.Multiple(() =>
        {
            Assert.That(invocation.Value, Is.EqualTo(legacy.Value));
            Assert.That(invocation.ValueFormat, Is.EqualTo(legacy.ValueFormat));
            Assert.That(invocation.GetType(), Is.EqualTo(legacy.GetType()));
        });
    }

    private static OperationInvocation Invocation(
        IEnumerable<string> values, ResolvedCallArgumentKind kind) =>
        new(values.Select(value => new ResolvedCallArgument(value, kind)));

    private sealed class FixedInfoDocumentationProvider(string documentation) : IInfoDocumentationProvider
    {
        public bool HasDocumentation(string name) => true;

        public bool TryGetDocumentation(string name, out string value)
        {
            value = documentation;
            return true;
        }
    }
}
