using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json;
using static CryptoScriptUnitTest.StoreAdmissionTests;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class P1F3CorrectionTests
{
    private readonly VariableSnapshotJsonSerializer snapshots = new();

    [Test]
    public void UpdateRestoresLegacyHeaderWithoutGrantingValidatedHeaderSemantics()
    {
        var store = new VariableDictionary();
        var runner = new CryptoScriptRunner(store);
        runner.Execute(Parse("TR31H display=BlockHeader(WRAP-AES-TR31)"));
        var original = (BlockHeaderVariableDeclaration)store.Get("display");
        string payload = snapshots.Serialize([original]);
        var restored = (BlockHeaderVariableDeclaration)snapshots.Deserialize(payload).Single();
        Assert.That(restored.Definition, Is.Null);
        Assert.That(restored.Value, Is.Not.Empty);
        long before = store.Revision;
        using var lease = InterpreterExecution.Enter(store);
        store.Update(restored);
        Assert.That(store.Get("display"), Is.SameAs(restored));
        Assert.That(snapshots.Serialize([restored]), Is.EqualTo(payload));
        Assert.That(restored.Type, Is.TypeOf<CryptoTypeTR31Header>());
        Assert.That(restored.PrintOutput(), Is.EqualTo(restored.Value));
        Assert.Throws<ArgumentException>(() => ParameterEvaluator.Evaluate("#BLKH", "display", store));
        var wrong = new StringVariableDeclaration { Id = "display", Type = new CryptoTypeTR31Header() };
        Assert.Throws<ArgumentException>(() => store.Update(wrong));
        Assert.That(store.Get("display"), Is.SameAs(restored));
        Assert.That(store.Revision, Is.EqualTo(before + 1));
        Assert.That(snapshots.Serialize([restored]), Is.EqualTo(payload));

        runner.Execute(Parse("TR31H header={KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;}"));
        var validated = store.Get("header");
        var downgrade = (BlockHeaderVariableDeclaration)snapshots.Deserialize(payload).Single();
        downgrade.Id = "header";
        long definedRevision = store.Revision;
        Assert.Throws<ArgumentException>(() => store.Update(downgrade));
        Assert.That(store.Get("header"), Is.SameAs(validated));
        Assert.That(ParameterEvaluator.Evaluate("#BLKH", "header", store).HeaderReference, Is.SameAs(validated));
        Assert.That(store.Revision, Is.EqualTo(definedRevision));
    }

    [Test]
    public void UpdateAcceptsPathLiteralParamAndPreservesTheV1SnapshotBoundary()
    {
        var store = new VariableDictionary();
        var runner = new CryptoScriptRunner(store);
        runner.Execute(Parse(@"PARAM location=C:\review\first"));
        var original = (PathVariableDeclaration)store.Get("location");
        Assert.That(original.Type, Is.TypeOf<CryptoTypeParameters>());
        Assert.Throws<JsonSerializationException>(() => snapshots.Serialize([original]));
        var candidate = (PathVariableDeclaration)Expression.PrepareVariable(
            Expression.Create(@"C:\review\second"), "location", new CryptoTypeParameters());
        candidate.Text = @"PARAM location=C:\review\second";
        var descriptor = candidate.Type;
        using var lease = InterpreterExecution.Enter(store);
        long before = store.Revision;
        store.Update(candidate);
        store.Update(candidate);
        Assert.That(store.Get("location"), Is.SameAs(candidate));
        Assert.That(candidate.Type, Is.SameAs(descriptor));
        Assert.That((candidate.Value, candidate.ValueFormat, candidate.Text),
            Is.EqualTo((@"C:\review\second", FormatConversions.PATH, @"PARAM location=C:\review\second")));
        Assert.Throws<JsonSerializationException>(() => snapshots.Serialize([candidate]));
        var wrong = new PathVariableDeclaration { Id = "location", Type = new CryptoTypeParameters(), Value = "relative" };
        Assert.Throws<ArgumentException>(() => store.Update(wrong));
        Assert.That(store.Get("location"), Is.SameAs(candidate));
        Assert.That(store.Revision, Is.EqualTo(before + 1));
        wrong.Type!.Text = "still detached";
    }

    [TestCase(false)]
    [TestCase(true)]
    public void KeyPublicationRequiresAnAttributeArrayAndSaveLoadPreservesItsContents(bool hasBlocks)
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new VariableDictionary();
            var runner = new CryptoScriptRunner(store);
            store.Add(new PathVariableDeclaration { Id = "file", Type = new CryptoTypePath(), Value = file });
            runner.Execute(Parse("KEY item=0x(00112233445566778899AABBCCDDEEFF)"));
            var original = (KeyVariableDeclaration)store.Get("item");
            string baseline = snapshots.Serialize([original]);
            var candidate = (KeyVariableDeclaration)snapshots.Deserialize(baseline).Single();
            candidate.KeyAttributes = null!;
            using var lease = InterpreterExecution.Enter(store);
            long before = store.Revision;
            Assert.Throws<ArgumentNullException>(() => store.Update(candidate));
            Assert.Throws<ArgumentNullException>(() => original.KeyAttributes = null!);
            Assert.That(store.Get("item"), Is.SameAs(original));
            Assert.That(snapshots.Serialize([original]), Is.EqualTo(baseline));
            candidate.Id = "fresh";
            Assert.Throws<ArgumentNullException>(() => store.Add(candidate));
            Assert.That(store.Contains("fresh"), Is.False);
            Assert.That(candidate.KeyAttributes, Is.Null);
            Assert.That(store.Revision, Is.EqualTo(before));

            candidate.Id = "item";
            candidate.KeyAttributes = hasBlocks
                ? [new OptionalBlock { ID = "AB", Length = 6, Data = "xy" }, new OptionalBlock { ID = null, Length = 0, Data = null }]
                : [];
            store.Update(candidate);
            candidate.KeyAttributes = candidate.KeyAttributes;
            Assert.That(store.Get("item"), Is.SameAs(candidate));
            Assert.That(store.Revision, Is.EqualTo(before + 1));
            string expected = snapshots.Serialize([candidate]);
            runner.Execute(Parse("Save(file,item) Load(file,item,#ONCONFLICT:REPLACE)"));
            var restored = (KeyVariableDeclaration)store.Get("item");
            Assert.That(restored, Is.Not.SameAs(candidate));
            Assert.That(restored.KeyAttributes.Count, Is.EqualTo(hasBlocks ? 2 : 0));
            Assert.That(snapshots.Serialize([restored]), Is.EqualTo(expected));
            Assert.That(store.Revision, Is.EqualTo(before + 2));
        }
        finally { File.Delete(file); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task BoundDescriptorRequiresWriteAuthorityAndTheOriginalInstance(bool boolean)
    {
        var store = new VariableDictionary();
        VariableDeclaration root = boolean ? new ValVariableDeclaration(true) : new StringVariableDeclaration { Value = "text" };
        root.Id = "item";
        store.Add(root);
        var published = root.Type!;
        CryptoType Equivalent() => boolean ? new CryptoTypeVal() : new CryptoTypeVar();
        var equivalent = Equivalent();
        var changed = Equivalent();
        changed.Text = "proposal";
        var incompatible = new CryptoTypeKey();
        var state = (root.Value, root.Text, published.Id, published.Text, store.Revision);
        await InterpreterExecution.RunAsync(store, host => Task.FromResult(InterpreterExecution.AccessState(host, () =>
        {
            foreach (var descriptor in new[] { published, equivalent, changed, incompatible })
                Assert.Throws<ExecutionBusyException>(() => root.Type = descriptor);
            return true;
        })));
        using var lease = InterpreterExecution.Enter(store);
        root.Type = published;
        foreach (var descriptor in new[] { equivalent, changed, incompatible })
        {
            Assert.Throws<InvalidOperationException>(() => root.Type = descriptor);
            Assert.That(root.Type, Is.SameAs(published));
            Assert.That(store.Get("item"), Is.SameAs(root));
            Assert.That((root.Value, root.Text, published.Id, published.Text, store.Revision), Is.EqualTo(state));
            descriptor.Text = "caller can edit";
        }
        Assert.Throws<InvalidOperationException>(() => published.Id = "frozen");
    }
}
