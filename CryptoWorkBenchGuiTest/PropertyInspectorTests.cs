using System.Collections.Immutable;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using System.Text.Json;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services.Inspector;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class PropertyInspectorTests
{
    private static SecretVariableDeclaration Secret(bool shamir)
    {
        string[] values = shamir ? ["ABBA","A8B9","A9B8"] : ["0001","0203","0405"];
        return new(new SecretSet(new(shamir ? SecretDescriptor.Shamir : SecretDescriptor.Split,3,shamir ? 2 : 3,16),
            values.Select((s,i) => new SecretShare(i+1,Convert.FromHexString(s))),
            originalKeyMetadata:new(KeyType.Secret(KeyAlgorithm.Unknown),KeyUsagePolicy.Unspecified,"",[])))
            { Id = shamir ? "sharedSecret" : "splitSecret" };
    }
    private static BlockHeaderVariableDeclaration Header() => new() { Id="header", Definition = Tr31HeaderDefinition.Create(
        new Dictionary<string,string> { ["KBVID"]="D",["KEYU"]="K0",["ALGO"]="A",["MODEU"]="B",["EXP"]="E" }) };
    private static ParameterVariableDeclaration Param()
    {
        var p = ParameterVariableDeclaration.FromSnapshot("TR31",new Dictionary<string,string>{ ["#MECH"]="TR31", ["#BLKH"]="header" },
            [],"#MECH:TR31#BLKH:header",Header());
        p.Id = "parameters"; return p;
    }
    private static string Copy(VariableDeclaration value, params string[] names)
    {
        var projector = new InspectorProjector(value,default);
        ImmutableArray<InspectorStep> path = [];
        foreach (var name in names)
            path = path.Add(projector.Describe(new(path,Calculate:true)).Rows.Single(r => r.Name == name).Step);
        return string.Concat(projector.Encode(new(path,Calculate:true)));
    }

    private static IEnumerable<string> GoldenIds()
    {
        using var stream = typeof(PropertyInspectorTests).Assembly.GetManifestResourceStream("CryptoWorkBenchGuiTest.Fixtures.inspector-f3-golden.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("cases").EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToArray();
    }
    [TestCaseSource(nameof(GoldenIds))]
    public void Golden(string id)
    {
        using var stream = GetType().Assembly.GetManifestResourceStream("CryptoWorkBenchGuiTest.Fixtures.inspector-f3-golden.json")!;
        using var doc = JsonDocument.Parse(stream);
        var data = doc.RootElement.GetProperty("cases").EnumerateArray().Single(e => e.GetProperty("id").GetString() == id);
        string expected = data.GetProperty("expectedText").GetString()!;
        var dict = VariableDictionary.Instance();
        var saved = dict.GetVariables().ToArray();
        try
        {
            dict.Clear();
            const string script = "KEY demoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)";
            var key = (KeyVariableDeclaration)new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program()).Statements.Single()!;
            var projector = new InspectorProjector(key,default);
            string actual = id switch
            {
                "root-key" => Copy(key), "root-param" => Copy(Param()), "root-split" => Copy(Secret(false)),
                "root-shamir" => Copy(Secret(true)), "root-header" => Copy(Header()), "param-adapter" => Copy(Param(),"GetParameters()"),
                "key-type" => Copy(key,"KeyType"), "key-size" => Copy(key,"KeySizeInBits"),
                "split-shares" => Copy(Secret(false),"SecretSet","Shares"), "tr31-definition" => Copy(Header(),"Definition"),
                "tr31-value" => Copy(Header(),"Value"), _ => string.Concat(projector.EncodeScalar(id switch
                {
                    "null" => null, "bytes" => new byte[]{0,127,128,255}, "empty-bytes" => Array.Empty<byte>(),
                    "ulong-max" => ulong.MaxValue, "real-dictionary-key" => new Dictionary<string,string>{{"type","actual data"},{"status","actual data"}},
                    _ => data.GetProperty("value").GetString()
                }))
            };
            Assert.That(actual,Is.EqualTo(expected));
        }
        finally { dict.Clear(); foreach(var old in saved) dict.Add(old); }
    }

    [TestCase(typeof(StringVariableDeclaration),6)]
    [TestCase(typeof(PathVariableDeclaration),5)]
    [TestCase(typeof(ValVariableDeclaration),5)]
    [TestCase(typeof(ParameterVariableDeclaration),7)]
    [TestCase(typeof(KeyVariableDeclaration),14)]
    [TestCase(typeof(SecretVariableDeclaration),6)]
    [TestCase(typeof(BlockHeaderVariableDeclaration),6)]
    public void Inventory(Type type,int count)
    {
        var properties = InspectorProjector.Inventory(type).ToList();
        Assert.That(properties,Has.Count.EqualTo(count));
        Assert.That(properties.Select(p=>p.Name),Does.Contain("Text"));
        Assert.That(properties.Select(p=>(p.Module,p.MetadataToken)),Is.EquivalentTo(type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Select(p=>(p.Module,p.MetadataToken))));
    }

    private sealed class Unknown : VariableDeclaration
    {
        public int Calls;
        public override string Value { get { Calls++; throw new InvalidOperationException(); } set {} }
        public new string Text => throw new InvalidOperationException();
        public string WriteOnly { set {} }
    }
    [Test] public void UnknownSlotsAreVisibleWithoutDispatch()
    {
        var unknown = new Unknown();
        var p = new InspectorProjector(unknown,default);
        var rows = p.Describe(new([])).Rows;
        Assert.That(rows.Count(r=>r.Name.EndsWith(".Text",StringComparison.Ordinal)),Is.EqualTo(2));
        Assert.That(rows.Single(r=>r.Name=="WriteOnly").State,Is.EqualTo("GetterNotPublic"));
        Assert.Throws<NotSupportedException>(()=>string.Concat(p.Encode(new([]))));
        Assert.That(unknown.Calls,Is.Zero);
    }
    [Test] public Task SessionNavigationAndHistoricalCopy() => GuiTestSession.RunAsync(async () =>
    {
        await using var service = new CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService();
        var editor = new CryptoWorkBenchAvalonia.Controls.WorkbenchTextEditor { Text = "source" };
        service.AttachEditor(editor,null!);
        var variables = new CryptoWorkBenchAvalonia.ViewModels.VariableViewModel { Automation = service };
        const string value = " \r\nΩ\n";
        var variable = new StringVariableDeclaration { Id = "uiSample", Value = value };
        var dict = VariableDictionary.Instance();
        try
        {
            service.Mutate(()=>dict.Add(variable),variables.ApplyVariables);
            variables.SelectedVariable = variables.DataVariables.Single(v=>v.Identifier==variable.Id);
            using var model = new CryptoWorkBenchAvalonia.ViewModels.VariableInspectorViewModel(variables);
            var view = new CryptoWorkBenchAvalonia.Views.DockVariableInspectorView(model);
            var window = new Avalonia.Controls.Window { Content=view };
            window.Show();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while(model.Page==null) await Task.Delay(5,timeout.Token);
                var revision=CryptoWorkBenchAvalonia.Services.InterpreterHostQueue.StateRevision;
                var row=model.Page.Rows.Single(r=>r.Name=="Value");
                await model.OpenAsync(row);
                Assert.That(model.Page!.Preview,Is.EqualTo(value));
                await model.ValueAsync(null,false,null);
                var full=view.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.That(full.IsReadOnly,Is.True);
                full.Focus(); full.SelectionStart=3; full.SelectionEnd=4;
                window.KeyPress(Key.C,RawInputModifiers.Control,PhysicalKey.C,"c");
                await Task.Delay(30);
                Assert.That(await window.Clipboard!.TryGetTextAsync(),Is.EqualTo("Ω"));
                await model.OpenAsync(null,back:true);
                editor.Text += " typed";
                string? copied=null;
                await model.ValueAsync(row,false,s=>{ copied=s;return Task.CompletedTask; });
                Assert.That(copied,Is.EqualTo(value));
                Assert.That(model.Status,Does.Contain("Historical"));
                Assert.That(CryptoWorkBenchAvalonia.Services.InterpreterHostQueue.StateRevision,Is.EqualTo(revision));
                Assert.That(variable.Value,Is.EqualTo(value));
            }
            finally { window.Close(); }
        }
        finally { dict.Clear(); }
    });

    [Test] public void DictionaryEntriesAndMetadataAreDistinct()
    {
        var param = Param();
        var pr = new InspectorProjector(param,default);
        var path = ImmutableArray.Create(pr.Describe(new([])).Rows.Single(r=>r.Name=="GetParameters()").Step);
        var page=pr.Describe(new(path));
        Assert.That(page.Rows.Select(r=>r.Name),Does.Contain("Capacity"));
        Assert.That(page.Rows.Single(r=>r.Name=="Item").State,Does.Contain("Indexer"));
        var entry=page.Rows.Single(r=>r.Name=="[#BLKH]");
        Assert.That(string.Concat(pr.Encode(new(path.Add(entry.Step)))),Is.EqualTo("{\"Key\":\"#BLKH\",\"Value\":\"header\"}"));
    }

    [Test] public void GlobalSettingsNeverExecuteDuringHeaderProjection()
    {
        var header=Header(); var prior=Newtonsoft.Json.JsonConvert.DefaultSettings; int calls=0;
        try
        {
            Newtonsoft.Json.JsonConvert.DefaultSettings=()=>{ calls++;throw new InvalidOperationException(); };
            Assert.Throws<NotSupportedException>(()=>Copy(header,"Value"));
            Assert.That(Copy(header,"Definition","Algorithm"),Is.EqualTo("A"));
            Assert.That(calls,Is.Zero);
        }
        finally { Newtonsoft.Json.JsonConvert.DefaultSettings=prior; }
    }

    [Test] public void AliasCopiesTheDomainValueAndLinksBack()
    {
        var attr=new OptionalBlock {ID="AB",Length=6,Data="XY"};
        var key=new KeyVariableDeclaration {KeyAttributes=[attr,attr]};
        var projector=new InspectorProjector(key,default);
        var path=ImmutableArray.Create(projector.Describe(new([])).Rows.Single(r=>r.Name=="KeyAttributes").Step);
        var rows=projector.Describe(new(path)).Rows;
        Assert.That(rows.Single(r=>r.Name=="[1]").Reference,Is.EqualTo(path.Add(rows.Single(r=>r.Name=="[0]").Step)).AsCollection);
        const string expected="{\"Data\":\"XY\",\"ID\":\"AB\",\"Length\":6}";
        Assert.That(string.Concat(projector.Encode(new(path))),Is.EqualTo("["+expected+","+expected+"]"));
    }
    [Test] public void ShareRangeIndexerAndSyncRoot()
    {
        var projector=new InspectorProjector(Secret(false),default);
        ImmutableArray<InspectorStep> path=[];
        foreach(var name in new[]{"SecretSet","Shares","[1]","Value"})
            path=path.Add(projector.Describe(new(path)).Rows.Single(r=>r.Name==name).Step);
        var rows=projector.Describe(new(path)).Rows;
        Assert.That(rows.Single(r=>r.Name=="SyncRoot").Reference,Is.EqualTo(path).AsCollection);
        Assert.That(string.Concat(projector.Encode(new(path))),Is.EqualTo("0x(0203)"));
        Assert.That(string.Concat(projector.Encode(new(path.Add(rows.Single(r=>r.Name=="[1]").Step)))),Is.EqualTo("3"));
    }

    private sealed class ForeignList : List<OptionalBlock>, System.Collections.IEnumerable
    {
        public int Calls;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            Calls++;
            throw new InvalidOperationException();
        }
    }
    [Test] public void ForeignCollectionIsNeverEnumerated()
    {
        var list=new ForeignList();
        var key=new KeyVariableDeclaration {KeyAttributes=list};
        var projector=new InspectorProjector(key,default);
        var path=ImmutableArray.Create(projector.Describe(new([])).Rows.Single(r=>r.Name=="KeyAttributes").Step);
        Assert.That(projector.Describe(new(path)).Rows.All(r=>r.State=="UnsupportedAccessor"),Is.True);
        Assert.Throws<NotSupportedException>(()=>string.Concat(projector.Encode(new(path))));
        Assert.That(list.Calls,Is.Zero);
    }
    [Test] public void HeaderOptionalBlocksPreserveFormatterAndCase()
    {
        var header=new BlockHeaderVariableDeclaration { Definition=Tr31HeaderDefinition.Create(new Dictionary<string,string>
            {["KBVID"]="D",["KEYU"]="D0",["ALGO"]="A",["MODEU"]="B",["EXP"]="E",["OPTID"]="LB",["OPTBD"]="\"abc\""}) };
        Assert.That(Copy(header,"Value"),Is.EqualTo(header.Value));
        var block=header.Definition.OptionalBlocks.First();
        var json=Copy(header,"Definition","OptionalBlocks","[0]");
        Assert.That(json,Is.EqualTo($"{{\"Data\":\"{block.Data}\",\"ID\":\"LB\",\"Length\":{block.Length}}}"));
    }
    [TestCase("0x(0G)")]
    [TestCase("0x(12)")]
    public void InvalidMaterialYieldsActualNull(string material)
    {
        var key=new KeyVariableDeclaration{ KeyType=KeyType.Secret(KeyAlgorithm.Aes),ValueFormat=FormatConversions.HEX,Value=material };
        foreach (var name in new[]{"Kcv","Kcvc","Kcvk"}) Assert.That(Copy(key,name),Is.EqualTo("null"));
    }

    [Test] public void Utf16Preserved()
    {
        const string raw = "\ud800\0\r\nΩ\udfff";
        Assert.That(Copy(new StringVariableDeclaration{ Value=raw },"Value"),Is.EqualTo(raw));
        Assert.That(string.Concat(InspectorProjector.Quoted(raw)),Is.EqualTo("\"\\ud800\\u0000\\r\\nΩ\\udfff\""));
    }
    [TestCase(KeyAlgorithm.Aes,"BASE64_STRING")]
    [TestCase(KeyAlgorithm.Aes,"NORMAL_STRING")]
    [TestCase(KeyAlgorithm.Tdea,"HEX_STRING")]
    public void ExplicitChecksMatchGetters(KeyAlgorithm algorithm,string format)
    {
        var k = new KeyVariableDeclaration{ KeyType=KeyType.Secret(algorithm),ValueFormat=format,
            Value=format==FormatConversions.HEX ? "0123456789ABCDEFFEDCBA9876543210" :
                format==FormatConversions.STR ? "0123456789abcdef" : new string(' ',10000)+Convert.ToBase64String(new byte[16]) };
        var p = new InspectorProjector(k,default);
        foreach (var name in new[]{"Kcv","Kcvc","Kcvk"})
        {
            var row=p.Describe(new([])).Rows.Single(r=>r.Name==name);
            Assert.That(row.State,Is.EqualTo("NotCalculated"));
            Assert.Throws<InvalidOperationException>(()=>string.Concat(p.Encode(new([row.Step]))));
            Assert.That(Copy(k,name),Is.EqualTo((string?)typeof(KeyVariableDeclaration).GetProperty(name)!.GetValue(k) ?? "null"));
        }
    }
}
