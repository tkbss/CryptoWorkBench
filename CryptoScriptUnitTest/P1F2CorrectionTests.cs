using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Variables;
using static CryptoScriptUnitTest.StoreAdmissionTests;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class P1F2CorrectionTests
{
    private const string Material = "0x(00112233445566778899AABBCCDDEEFF)";

    [Test]
    public async Task DeniedTypeWritesLeaveCallerDescriptorsEditable()
    {
        var store = new VariableDictionary();
        var root = new KeyVariableDeclaration { Id = "item", Type = new CryptoTypeKey(), Value = Material, KeySize = "128" };
        store.Add(root);
        var other = new VariableDictionary();
        var foreign = new StringVariableDeclaration { Id = "other" };
        other.Add(foreign);
        var frozen = foreign.Type!;
        var original = root.Type!;
        long revision = store.Revision;
        var fresh = new CryptoTypeKey();
        await InterpreterExecution.RunAsync(store, host => Task.FromResult(InterpreterExecution.AccessState(host, () =>
        {
            Assert.Throws<ExecutionBusyException>(() => root.Type = fresh);
            Assert.Throws<ExecutionBusyException>(() => root.Type = frozen);
            fresh.Id = "after denial";
            return true;
        })));
        void AfterAdmission(string _)
        {
            Assert.Throws<InvalidOperationException>(() => root.Type = fresh);
            fresh.Id = "after validation";
            Assert.Throws<InvalidOperationException>(() => root.Type = frozen);
        }
        OutputOperations.PrintEvent += AfterAdmission;
        try { new CryptoScriptRunner(store).Execute(Parse("Print(1)")); }
        finally { OutputOperations.PrintEvent -= AfterAdmission; }
        Assert.That(store.Get("item"), Is.SameAs(root));
        Assert.That(root.Type, Is.SameAs(original));
        Assert.That((root.Value, store.Revision, fresh.Id), Is.EqualTo((Material, revision, "after validation")));
        Assert.Throws<InvalidOperationException>(() => original.Id = "retained");
        var valid = new KeyVariableDeclaration { Id = "next", Type = fresh };
        store.Add(valid);
        Assert.That(valid.Type, Is.SameAs(fresh));
        Assert.Throws<InvalidOperationException>(() => fresh.Id = "published");
        Assert.Throws<InvalidOperationException>(() => fresh.Text = "retained");
    }

    [Test]
    public void AdmittedKeyUpdateValidatesRepresentationAndMaterialBeforePublication()
    {
        var store = new VariableDictionary();
        new CryptoScriptRunner(store).Execute(Parse("KEY item=" + Material));
        var initial = store.Get("item");
        var revision = store.Revision;
        KeyVariableDeclaration Key(string bits) => new()
        {
            Id = "item", Type = new CryptoTypeKey(), Value = Material, KeySize = bits,
            KeyType = KeyType.Secret(KeyAlgorithm.Aes), ValueFormat = FormatConversions.HEX
        };
        void Update(string _)
        {
            var valid = Key("128");
            store.Update(valid);
            var type = valid.Type;
            var metadata = (valid.KeyType, valid.Usage, valid.KeySize, valid.KeySizeInBits, valid.ValueFormat);
            void Unchanged()
            {
                Assert.That(store.Get("item"), Is.SameAs(valid).And.Not.SameAs(initial));
                Assert.That(valid.Type, Is.SameAs(type));
                Assert.That((valid.KeyType, valid.Usage, valid.KeySize, valid.KeySizeInBits, valid.ValueFormat), Is.EqualTo(metadata));
                Assert.That((valid.Value, store.Revision), Is.EqualTo((Material, revision + 1)));
            }
            Unchanged();
            Assert.Throws<ArgumentException>(() => store.Update(new StringVariableDeclaration { Id = "item", Type = new CryptoTypeKey() }));
            Unchanged();
            Assert.Throws<InvalidOperationException>(() => valid.Type = new CryptoTypeVar());
            Unchanged();
            var mismatch = Key("256");
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => store.Update(mismatch));
            mismatch.Type!.Id = "still detached";
            Unchanged();
            Assert.Throws<ArgumentException>(() => store.Update(Key("")));
            Unchanged();
        }
        OutputOperations.PrintEvent += Update;
        try { new CryptoScriptRunner(store).Execute(Parse("Print(1)")); }
        finally { OutputOperations.PrintEvent -= Update; }
    }

    [Test]
    public void ScalarParamCanBeReplacedInsideItsOwnExecution()
    {
        var store = new VariableDictionary();
        var runner = new CryptoScriptRunner(store);
        runner.Execute(Parse("PARAM p=0x(01)"));
        var original = store.Get("p");
        Assert.That(original, Is.TypeOf<StringVariableDeclaration>());
        var replacement = new StringVariableDeclaration { Id = "p", Type = new CryptoTypeParameters(), Value = "0x(02)" };
        void Update(string _) => store.Update(replacement);
        OutputOperations.PrintEvent += Update;
        try { runner.Execute(Parse("Print(1)")); }
        finally { OutputOperations.PrintEvent -= Update; }
        Assert.That(store.Get("p"), Is.SameAs(replacement));
        Assert.That((replacement.Type!.Name, replacement.Value, store.Revision), Is.EqualTo(("PARAM", "0x(02)", 2L)));
    }

    [Test]
    public void LegacyUntypedPathUpdatePreservesItsRepresentation()
    {
        var store = new VariableDictionary();
        store.Add(new PathVariableDeclaration { Id = "path", Value = "before" });
        var replacement = new PathVariableDeclaration { Id = "path", Value = "after" };
        void Update(string _)
        {
            store.Update(replacement);
            var wrong = new StringVariableDeclaration { Id = "path", Type = null };
            Assert.Throws<ArgumentException>(() => store.Update(wrong));
        }
        OutputOperations.PrintEvent += Update;
        try { new CryptoScriptRunner(store).Execute(Parse("Print(1)")); }
        finally { OutputOperations.PrintEvent -= Update; }
        Assert.That(store.Get("path"), Is.SameAs(replacement));
        Assert.That((replacement.Value, store.Revision), Is.EqualTo(("after", 2L)));
    }

    [Test]
    public async Task ReadScopeCannotChangeParameterLookupInEitherStore()
    {
        var a = new VariableDictionary();
        var b = new VariableDictionary();
        ParameterVariableDeclaration Add(VariableDictionary store, string iv)
        {
            var p = new ParameterVariableDeclaration { Id = "p", Mechanism = "AES-CBC" };
            p.SetParameter("IV", iv);
            store.Add(p);
            return p;
        }
        var first = Add(a, "0x(11)");
        var second = Add(b, "0x(22)");
        var revisions = (a.Revision, b.Revision);
        await InterpreterExecution.RunAsync(a, host => Task.FromResult(InterpreterExecution.AccessState(host, () =>
        {
            var metadata = ParameterTypeList.Instance;
            foreach (var names in new[] { metadata.ParameterTypes, metadata.Mechanism, metadata.Paddings })
            {
                Assert.Throws<NotSupportedException>(() => ((IList<string>)names).Clear());
                if (((System.Collections.ICollection)names).SyncRoot is IList<string> syncRoot)
                    Assert.Throws<NotSupportedException>(() => syncRoot.Remove("#IV"));
            }
            AntlrLanguageMetadata.GetParameters().Remove("#IV");
            Assert.That(first.GetParameter("IV"), Is.EqualTo("0x(11)"));
            return true;
        })));
        Assert.That((first.GetParameter("IV"), second.GetParameter("IV")), Is.EqualTo(("0x(11)", "0x(22)")));
        Assert.That((a.Revision, b.Revision), Is.EqualTo(revisions));
        first.SetParameter("IV", "0x(33)");
        first.SetParameter("IV", "0x(33)");
        Assert.That((first.GetParameter("IV"), second.GetParameter("IV")), Is.EqualTo(("0x(33)", "0x(22)")));
        Assert.That((a.Revision, b.Revision), Is.EqualTo((revisions.Item1 + 1, revisions.Item2)));
    }

    [Test]
    public void KeyAttributesCompareAllOrderedFieldsAndDetachSnapshots()
    {
        var store = new VariableDictionary();
        var key = new KeyVariableDeclaration { Id = "item", Type = new CryptoTypeKey() };
        store.Add(key);
        long revision = store.Revision;
        key.KeyAttributes = [];
        Assert.That(store.Revision, Is.EqualTo(revision));
        List<OptionalBlock> blocks = [new() { ID = "AB", Length = 5, Data = "1" }, new() { ID = "CD", Length = 5, Data = "2" }];
        key.KeyAttributes = blocks;
        blocks[0].ID = "XX";
        blocks.Clear();
        Assert.That(key.KeyAttributes.Select(b => (b.ID, b.Length, b.Data)), Is.EqualTo(new[] { ("AB", 5, "1"), ("CD", 5, "2") }));
        Assert.That(store.Revision, Is.EqualTo(++revision));
        foreach (Action<List<OptionalBlock>> change in new Action<List<OptionalBlock>>[]
                 { x => x[0].Length++, x => x[0].ID = "EF", x => x[0].Data = "3", x => x.Reverse() })
        {
            var snapshot = key.KeyAttributes;
            change(snapshot);
            Assert.That(store.Revision, Is.EqualTo(revision));
            key.KeyAttributes = snapshot;
            Assert.That(store.Revision, Is.EqualTo(++revision));
            Assert.That(key.KeyAttributes.Select(b => (b.ID, b.Length, b.Data)), Is.EqualTo(snapshot.Select(b => (b.ID, b.Length, b.Data))));
            key.KeyAttributes = key.KeyAttributes;
            Assert.That(store.Revision, Is.EqualTo(revision));
            Assert.That(store.Get("item"), Is.SameAs(key));
        }
    }
}
