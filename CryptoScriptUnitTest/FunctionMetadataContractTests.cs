using System.Collections.Frozen;
using CryptoScript.Model;

namespace CryptoScriptUnitTest;

public class FunctionMetadataContractTests
{
    [Test]
    public void ConstructsSeparateValueAndParameterSetArguments()
    {
        var parameterSet = new ParameterSetContract("AES-CBC", new[] { "#MECH", "#IV", "#PAD" });
        var arguments = new[]
        {
            new FunctionArgument(new ArgumentPosition(0), "parameters", ArgumentCardinality.Required,
                FunctionArgumentKind.ParameterSet, parameterSet),
            new FunctionArgument(new ArgumentPosition(1), "key", ArgumentCardinality.Required)
        };
        var result = new ParameterSetContract("RESULT", Array.Empty<string>());

        var variant = new CallVariant(new CallVariantId("encrypt"), arguments, result);

        Assert.Multiple(() =>
        {
            Assert.That(variant.Id.Value, Is.EqualTo("encrypt"));
            Assert.That(variant.Arguments[0].Kind, Is.EqualTo(FunctionArgumentKind.ParameterSet));
            Assert.That(variant.Arguments[0].ParameterSet, Is.SameAs(parameterSet));
            Assert.That(variant.Arguments[1].Kind, Is.EqualTo(FunctionArgumentKind.Value));
            Assert.That(variant.Arguments[1].ParameterSet, Is.Null);
            Assert.That(variant.ProducesParameterSet, Is.SameAs(result));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void CallVariantIdRejectsMissingValues(string? value)
    {
        Assert.Catch<ArgumentException>(() => new CallVariantId(value!));
    }

    [Test]
    public void CallVariantIdsUseStableOrdinalRecordEquality()
    {
        var first = new CallVariantId("encrypt");
        var same = new CallVariantId("encrypt");
        var differentCase = new CallVariantId("Encrypt");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(differentCase));
            Assert.That(first.ToString(), Is.EqualTo("encrypt"));
        });
    }

    [Test]
    public void ArgumentPositionRejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArgumentPosition(-1));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void FunctionArgumentRejectsMissingNames(string? name)
    {
        Assert.Catch<ArgumentException>(() => new FunctionArgument(
            new ArgumentPosition(0), name!, ArgumentCardinality.Required));
    }

    [Test]
    public void FunctionArgumentEnforcesParameterSetKindConsistency()
    {
        var parameterSet = new ParameterSetContract("AES-CBC", Array.Empty<string>());

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new FunctionArgument(
                new ArgumentPosition(0), "parameters", ArgumentCardinality.Required,
                FunctionArgumentKind.ParameterSet));
            Assert.Throws<ArgumentException>(() => new FunctionArgument(
                new ArgumentPosition(0), "value", ArgumentCardinality.Required,
                FunctionArgumentKind.Value, parameterSet));
        });
    }

    [Test]
    public void FunctionArgumentRejectsUndefinedCardinalityAndKindValues()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FunctionArgument(
                new ArgumentPosition(0), "value", (ArgumentCardinality)int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FunctionArgument(
                new ArgumentPosition(0), "value", ArgumentCardinality.Required,
                (FunctionArgumentKind)int.MaxValue));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void ParameterSetContractRejectsMissingIds(string? id)
    {
        Assert.Catch<ArgumentException>(() => new ParameterSetContract(id!, Array.Empty<string>()));
    }

    [Test]
    public void ParameterSetContractRejectsMissingOrDuplicateNames()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new ParameterSetContract("set", new[] { "#IV", "" }));
            Assert.Throws<ArgumentException>(() => new ParameterSetContract("set", new[] { "#IV", "#IV" }));
        });
    }

    [Test]
    public void ParameterSetContractRejectsCaseInsensitiveDuplicateNames()
    {
        Assert.Throws<ArgumentException>(() => new ParameterSetContract(
            "set", new[] { "#IV", "#iv" }));
    }

    [Test]
    public void CallVariantRequiresOrderedContiguousUniquePositions()
    {
        FunctionArgument[][] invalidArguments =
        {
            new[] { Argument(0, "first"), Argument(0, "second") },
            new[] { Argument(0, "first"), Argument(2, "second") },
            new[] { Argument(1, "second"), Argument(0, "first") }
        };

        Assert.Multiple(() =>
        {
            foreach (FunctionArgument[] arguments in invalidArguments)
            {
                Assert.Throws<ArgumentException>(() => new CallVariant(
                    new CallVariantId("variant"), arguments));
            }
        });
    }

    [Test]
    public void CallVariantRejectsDuplicateNamesAndNonFinalVariadicArguments()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new CallVariant(new CallVariantId("duplicate"), new[]
            {
                Argument(0, "value"), Argument(1, "value")
            }));
            Assert.Throws<ArgumentException>(() => new CallVariant(new CallVariantId("variadic"), new[]
            {
                Argument(0, "values", ArgumentCardinality.Variadic), Argument(1, "tail")
            }));
        });
    }

    [Test]
    public void CallVariantRejectsRequiredArgumentsAfterOptionalArguments()
    {
        Assert.Throws<ArgumentException>(() => new CallVariant(
            new CallVariantId("invalid-cardinality-order"), new[]
            {
                Argument(0, "optional", ArgumentCardinality.Optional),
                Argument(1, "required", ArgumentCardinality.Required)
            }));
    }

    [Test]
    public void CallVariantCollectionRejectsDuplicateIds()
    {
        var variants = new[]
        {
            new CallVariant(new CallVariantId("same"), Array.Empty<FunctionArgument>()),
            new CallVariant(new CallVariantId("same"), Array.Empty<FunctionArgument>())
        };

        Assert.Throws<ArgumentException>(() => new CallVariantCollection(variants));
    }

    [Test]
    public void ConstructorsDefensivelyCopyAllInputCollections()
    {
        var names = new List<string> { "#IV" };
        var parameterSet = new ParameterSetContract("set", names);
        var arguments = new[] { Argument(0, "value") };
        var variant = new CallVariant(new CallVariantId("one"), arguments);
        var variants = new[] { variant };
        var collection = new CallVariantCollection(variants);

        names[0] = "#PAD";
        arguments[0] = Argument(0, "changed");
        variants[0] = new CallVariant(new CallVariantId("two"), Array.Empty<FunctionArgument>());

        Assert.Multiple(() =>
        {
            Assert.That(parameterSet.NamedParameterNames, Is.EquivalentTo(new[] { "#IV" }));
            Assert.That(variant.Arguments.Single().Name, Is.EqualTo("value"));
            Assert.That(collection.Single().Id, Is.EqualTo(new CallVariantId("one")));
        });
    }

    [Test]
    public void ExposedCollectionsAndContractsAreImmutable()
    {
        var parameterSet = new ParameterSetContract("set", new[] { "#IV" });
        var variant = new CallVariant(new CallVariantId("one"), new[] { Argument(0, "value") });
        var collection = new CallVariantCollection(new[] { variant });

        var names = (ISet<string>)parameterSet.NamedParameterNames;
        var arguments = (IList<FunctionArgument>)variant.Arguments;

        Assert.Multiple(() =>
        {
            Assert.That(parameterSet.NamedParameterNames, Is.InstanceOf<FrozenSet<string>>());
            Assert.Throws<NotSupportedException>(() => names.Add("#PAD"));
            Assert.Throws<NotSupportedException>(() => arguments.Add(Argument(1, "other")));
            Assert.That(collection, Is.Not.InstanceOf<ICollection<CallVariant>>());
            Assert.That(typeof(CallVariant).GetProperties().Select(property => property.SetMethod), Is.All.Null);
            Assert.That(typeof(FunctionArgument).GetProperties().Select(property => property.SetMethod), Is.All.Null);
            Assert.That(typeof(ParameterSetContract).GetProperties().Select(property => property.SetMethod), Is.All.Null);
            Assert.That(typeof(CallVariantId).GetProperties().Select(property => property.SetMethod), Is.All.Null);
        });
    }

    [Test]
    public void CallVariantCollectionSupportsTypedLookup()
    {
        var expected = new CallVariant(new CallVariantId("encrypt"), Array.Empty<FunctionArgument>());
        var collection = new CallVariantCollection(new[] { expected });

        Assert.Multiple(() =>
        {
            Assert.That(collection.TryGet(new CallVariantId("encrypt"), out CallVariant? actual), Is.True);
            Assert.That(actual, Is.SameAs(expected));
            Assert.That(collection[new CallVariantId("encrypt")], Is.SameAs(expected));
            Assert.That(collection.TryGet(new CallVariantId("decrypt"), out _), Is.False);
        });
    }

    private static FunctionArgument Argument(
        int position,
        string name,
        ArgumentCardinality cardinality = ArgumentCardinality.Required) =>
        new(new ArgumentPosition(position), name, cardinality);

}
