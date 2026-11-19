using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Services.Inspector;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class PropertyInspectorCorrectionsTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly WorkbenchAutomationService Service = new();
        internal readonly WorkbenchTextEditor Editor = new() { Text = "source" };
        internal readonly VariableViewModel Variables;
        internal readonly EditorBridge.Registration Registration;
        internal readonly VariableInspectorViewModel Model;
        internal Fixture(VariableDeclaration variable)
        {
            Registration = Service.AttachEditor(Editor,null!)!;
            Variables = new() { Automation = Service };
            var dictionary = VariableDictionary.Instance();
            dictionary.Clear();
            variable.Id = "sample";
            Service.Mutate(() => dictionary.Add(variable),Variables.ApplyVariables);
            Variables.SelectedVariable = Variables.DataVariables.Single();
            Model = new(Variables);
        }

        internal async Task Invalidate(bool lease)
        {
            if (!lease) { Editor.Text += " draft"; return; }
            Service.ActivateMcp();
            var caller = Service.IssueCredential("historical-copy",
                WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
            var result = await Service.AcquireEditorAsync(caller,
                new("2",Service.InstanceId,Registration.Id,Guid.NewGuid(),Registration.Snapshot().Revision),default);
            Assert.That(result.Error,Is.Null);
        }

        internal InspectorRow Row(string name) => Model.Page!.Rows.Single(r => r.Name == name);
        internal async Task<string?> Copy(InspectorRow? row = null)
        {
            string? copied = null;
            await Model.ValueAsync(row,false,s => { copied = s; return Task.CompletedTask; });
            return copied;
        }

        public async ValueTask DisposeAsync()
        {
            Model.Dispose();
            await Service.DisposeAsync();
            VariableDictionary.Instance().Clear();
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(5,timeout.Token);
    }

    [TestCase(false,false)]
    [TestCase(false,true)]
    [TestCase(true,false)]
    [TestCase(true,true)]
    public Task ExactHistoricalCoverage(bool lease,bool pages) => GuiTestSession.RunAsync(async () =>
    {
        string pattern = "a\uD800\"\\\n\rΩ\0\uDFFFz";
        string raw = string.Concat(Enumerable.Repeat(pattern,40))[..(pages ? 180 : 400)];
        await using var f = new Fixture(new StringVariableDeclaration { Value=raw });
        await Until(() => f.Model.Page != null);
        var row = f.Row("Value");
        if (pages)
        {
            await f.Model.OpenAsync(row);
            await f.Model.OpenAsync(null,1);
            Assert.That(f.Model.Page!.More,Is.False);
        }
        else Assert.That(await f.Copy(),Does.Contain("\\ud800"));
        long revision = InterpreterHostQueue.StateRevision;
        await f.Invalidate(lease);
        Assert.That(await f.Copy(pages ? null : row),Is.EqualTo(raw),f.Model.Status);
        Assert.That(f.Model.Status,Does.Contain("Historical"));
        Assert.That(InterpreterHostQueue.StateRevision,Is.EqualTo(revision));
        Assert.That(f.Service.EditorCoordinator.Pending,Is.Zero);
    });

    [Test] public Task MissingMiddlePageStaysIncomplete() => GuiTestSession.RunAsync(async () =>
    {
        await using var f = new Fixture(new StringVariableDeclaration { Value=new string('x',350) });
        await Until(() => f.Model.Page != null);
        await f.Model.OpenAsync(f.Row("Value"));
        await f.Model.OpenAsync(null,2);
        await f.Invalidate(false);
        Assert.That(await f.Copy(),Is.Null);
        Assert.That(f.Model.Message,Does.Contain("not fully captured"));
    });

    [Test] public Task DetailCopyUsesBoundPropertyAcrossSelectionChange() => GuiTestSession.RunAsync(async () =>
    {
        await using var f = new Fixture(new StringVariableDeclaration { Value="child-value" });
        await Until(() => f.Model.Page != null);
        Assert.That(await f.Copy(),Does.Contain("child-value"));
        await f.Invalidate(false);
        foreach (var (name,value) in new[] { ("Id","sample"),("Value","child-value") })
        {
            var row = f.Row(name);
            Assert.That(await f.Copy(row),Is.EqualTo(value));
            await f.Model.OpenAsync(row);
            Assert.That(f.Model.Detail,Is.EqualTo(value));
            Assert.That(await f.Copy(),Is.EqualTo(value));
            if (name=="Id") await f.Model.OpenAsync(null,back:true);
        }
        var offered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        string? committed = null;
        var pending = f.Model.ValueAsync(null,false,async text =>
        {
            offered.SetResult();
            await release.Task;
            committed=text;
        });
        await offered.Task;
        f.Variables.SelectedVariable=null;
        await Until(() => f.Model.Page == null);
        release.SetResult();
        await pending;
        Assert.That(committed,Is.EqualTo("child-value"));
    });

    [Test] public Task ExplicitKcvCanNavigateWithHostReserved() => GuiTestSession.RunAsync(async () =>
    {
        var key = new KeyVariableDeclaration { KeyType=KeyType.Secret(KeyAlgorithm.Aes),
            ValueFormat=FormatConversions.STR,Value="0123456789abcdef" };
        string expected = key.Kcv!;
        var projector = new InspectorProjector(key,default);
        var kcv = projector.Describe(new([])).Rows.Single(r=>r.Name=="Kcv");
        Assert.That(projector.CalculatedGetterCalls,Is.Zero);
        Assert.Throws<InvalidOperationException>(()=>projector.Describe(new([kcv.Step])));
        Assert.That(projector.CalculatedGetterCalls,Is.Zero);
        Assert.That(projector.Describe(new([kcv.Step],Calculate:true)).ExactScalar,Is.EqualTo(expected));
        Assert.That(projector.CalculatedGetterCalls,Is.EqualTo(1));
        await using var f = new Fixture(key);
        await Until(() => f.Model.Page != null);
        var view = new DockVariableInspectorView(f.Model);
        var row = f.Row("Kcv");
        Assert.That(row.State,Is.EqualTo("NotCalculated"));
        Assert.That(view.GetLogicalDescendants().OfType<Button>().Select(AutomationProperties.GetName),Does.Contain("Calculate Kcv"));
        await f.Model.OpenAsync(row);
        Assert.That(f.Model.Depth,Is.Zero);
        Assert.That(f.Model.Message,Does.Contain("Calculate"));
        await f.Model.CalculateAsync(row);
        Assert.That(f.Model.Page!.Preview,Is.EqualTo(expected));
        using var reservation = InterpreterHostQueue.Reserve();
        foreach (bool historical in new[] {false,true})
        {
            if (historical) await f.Invalidate(false);
            await f.Model.OpenAsync(f.Row("Length"));
            Assert.That(await f.Copy(),Is.EqualTo(expected.Length.ToString()));
            await f.Model.OpenAsync(null,back:true);
            Assert.That(f.Row("Chars").State,Does.Contain("Indexer"));
            await f.Model.OpenAsync(f.Row("[1]"));
            Assert.That(await f.Copy(),Is.EqualTo(expected[1].ToString()));
            await f.Model.OpenAsync(null,back:true);
        }
    });

    [Test] public Task BusyInitialReadCanBeRetriedExplicitly() => GuiTestSession.RunAsync(async () =>
    {
        await using var f = new Fixture(new StringVariableDeclaration { Value="retry" });
        using (InterpreterHostQueue.Reserve())
        {
            await Until(() => f.Model.CanReload);
            Assert.That(f.Model.Status,Does.Contain("busy").IgnoreCase);
        }
        await f.Model.ReloadAsync();
        Assert.That(f.Row("Value").ExactScalar,Is.EqualTo("retry"));
    });

    [Test] public Task ReparentRetainsDetailAndSemanticCloseClearsView() => GuiTestSession.RunAsync(async () =>
    {
        string expected = new('q',8192);
        await using var f = new Fixture(new StringVariableDeclaration { Value=expected });
        var view = new DockVariableInspectorView(f.Model);
        var window = new Window { Content=view };
        window.Show();
        try
        {
            await Until(() => f.Model.Page != null);
            await f.Model.OpenAsync(f.Row("Value"));
            await f.Model.ValueAsync(null,false,null);
            var box = view.GetVisualDescendants().OfType<TextBox>().Single();
            window.Content=null;
            window.Content=view;
            Assert.That(f.Model.Detail,Is.EqualTo(expected));
            Assert.That(box.Text,Has.Length.EqualTo(4096));
            f.Model.Dispose();
            f.Model.Dispose();
            Assert.That(box.Text,Is.Null.Or.Empty);
            Assert.That(f.Model.Page,Is.Null);
            Assert.That(f.Model.Detail,Is.Null);
            Assert.That(view.GetVisualDescendants().OfType<Button>().Any(b => b.Tag is InspectorRow),Is.False);
        }
        finally { window.Close(); }
    });

    private sealed class Overloads : VariableDeclaration
    {
        public int Calls;
        public int this[int index] => ++Calls;
        public int this[string key] => ++Calls;
    }
    [Test] public void IndexersKeepSeparateSlots()
    {
        var instance = new Overloads();
        var rows = new InspectorProjector(instance,default).Describe(new([])).Rows
            .Where(r => r.Name.EndsWith(".Item")).ToArray();
        Assert.That(rows,Has.Length.EqualTo(2));
        Assert.That(rows.Select(r=>r.Step).Distinct().Count(),Is.EqualTo(2));
        Assert.That(rows.Select(r=>r.Type).Distinct().Count(),Is.EqualTo(2));
        Assert.That(rows.All(r=>r.State=="UnsupportedAccessor"),Is.True);
        Assert.That(instance.Calls,Is.Zero);
    }

    [TestCase(typeof(StringVariableDeclaration))]
    [TestCase(typeof(PathVariableDeclaration))]
    [TestCase(typeof(ValVariableDeclaration))]
    [TestCase(typeof(KeyVariableDeclaration))]
    [TestCase(typeof(ParameterVariableDeclaration))]
    [TestCase(typeof(BlockHeaderVariableDeclaration))]
    [TestCase(typeof(SecretVariableDeclaration))]
    public void BasePropertiesFirst(Type type)
    {
        var hierarchy = new Stack<Type>();
        for (var t=type;t!=null;t=t.BaseType) hierarchy.Push(t);
        var properties = type.GetProperties();
        var expected = hierarchy.SelectMany(t => properties.Where(p=>p.DeclaringType==t)
            .OrderBy(p=>p.Name,StringComparer.Ordinal));
        Assert.That(InspectorProjector.TreeInventory(type).Select(p=>(p.Module,p.MetadataToken)),
            Is.EqualTo(expected.Select(p=>(p.Module,p.MetadataToken))).AsCollection);
    }

    [TestCase(99)]
    [TestCase(100)]
    [TestCase(101)]
    public void PagesHaveOneHundredEntries(int count)
    {
        var pr = new InspectorProjector(new StringVariableDeclaration {Value=new string('x',count)},default);
        var path = ImmutableArray.Create(pr.Describe(new([])).Rows.Single(r=>r.Name=="Value").Step);
        var page = pr.Describe(new(path));
        Assert.That(page.Rows.Count(r=>r.Step.Kind==StepKind.Entry),Is.EqualTo(Math.Min(100,count)));
        Assert.That(page.More,Is.EqualTo(count>100));
        var second = pr.Describe(new(path,1)).Rows.Where(r=>r.Step.Kind==StepKind.Entry);
        Assert.That(second.Select(r=>r.Step.Index),Is.EqualTo(Enumerable.Range(100,Math.Max(0,count-100))));
    }

    private static ParameterVariableDeclaration DictionaryFixture(int count)
    {
        var result = new ParameterVariableDeclaration();
        var data = result.GetParameters();
        data.Clear();
        for (int i=count-1;i>=0;i--)
        {
            string suffix = i%3==0 ? "\"\\Ω\n" : i%3==1 ? "longer-key" : "";
            data.Add(i.ToString("D6")+suffix,"v");
        }
        return result;
    }

    private static ImmutableArray<InspectorStep> MapPath(InspectorProjector projector) =>
        [projector.Describe(new([])).Rows.Single(r=>r.Step.Kind==StepKind.Parameters).Step];

    [Test] public void LatePageMatchesOrdinalWindowWithinBudget()
    {
        var param = DictionaryFixture(65536);
        var projector = new InspectorProjector(param,default);
        var path = MapPath(projector);
        InspectorPage? page = null;
        using var steps = projector.DescribeSteps(new(path,655),r => page=r).GetEnumerator();
        long maxWork=0, maxBytes=0;
        double maxMs=0;
        bool more;
        do
        {
            long before=projector.TraversalWork, allocated=GC.GetAllocatedBytesForCurrentThread();
            long clock=Stopwatch.GetTimestamp();
            more=steps.MoveNext();
            maxMs=Math.Max(maxMs,Stopwatch.GetElapsedTime(clock).TotalMilliseconds);
            maxBytes=Math.Max(maxBytes,GC.GetAllocatedBytesForCurrentThread()-allocated);
            maxWork=Math.Max(maxWork,projector.TraversalWork-before);
        } while (more);
        var expected = param.GetParameters().Keys.Order(StringComparer.Ordinal).Skip(65500);
        Assert.That(page!.Rows.Where(r=>r.Step.Kind==StepKind.Entry).Select(r=>r.Step.Name),Is.EqualTo(expected));
        TestContext.Out.WriteLine($"65536 keys, page 655: peak traversal={maxWork}, allocated bytes/step={maxBytes}, peak ms={maxMs:F3}");
        Assert.That(maxWork,Is.InRange(1,256));
        Assert.That(maxBytes,Is.LessThan(2*1024*1024));
    }

    [Test] public void FullDictionaryMatchesIndependentOrdinalEncoding()
    {
        var param = DictionaryFixture(300001);
        var projector = new InspectorProjector(param,default) { Index = ImmutableDictionary.CreateBuilder<string,CapturedSlice>() };
        var path = MapPath(projector);
        using var chunks = projector.Encode(new(path)).GetEnumerator();
        var bytes = new byte[65536];
        using var digest = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        string remainder="";
        long maxWork=0,maxAllocated=0,steps=0,overTarget=0;
        double maxMs=0;
        bool finished;
        do
        {
            long before=projector.TraversalWork, allocated=GC.GetAllocatedBytesForCurrentThread();
            long clock=Stopwatch.GetTimestamp();
            int n=projector.ReadChunk(chunks,bytes,ref remainder,out finished);
            double ms=Stopwatch.GetElapsedTime(clock).TotalMilliseconds;
            if (ms>10) overTarget++;
            maxMs=Math.Max(maxMs,ms);
            maxAllocated=Math.Max(maxAllocated,GC.GetAllocatedBytesForCurrentThread()-allocated);
            maxWork=Math.Max(maxWork,projector.TraversalWork-before);
            steps++;
            digest.AppendData(bytes,0,n);
        } while (!finished);
        var expected = "{" + string.Join(",",param.GetParameters().Keys.Order(StringComparer.Ordinal)
            .Select(k => "\"" + k.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n") + "\":\"v\"")) + "}";
        Assert.That(digest.GetHashAndReset(),Is.EqualTo(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(expected))));
        TestContext.Out.WriteLine($"300001 keys, steps={steps}, above 10 ms={overTarget}, peak traversal={maxWork}, allocated bytes/step={maxAllocated}, peak ms={maxMs:F3}");
        Assert.That(maxWork,Is.InRange(1,256));
        Assert.That(maxAllocated,Is.LessThan(2*1024*1024));
    }

    [TestCase(false)]
    [TestCase(true)]
    public Task CancellationDuringTraversalReleasesAdmission(bool navigate) => GuiTestSession.RunAsync(async () =>
    {
        await using var service=new WorkbenchAutomationService();
        service.AttachEditor(new WorkbenchTextEditor(),null!);
        var param=DictionaryFixture(65536);
        using var stop=new CancellationTokenSource();
        long work=0;
        try
        {
            await service.RunInspectorReadAsync(token =>
            {
                var pr = new InspectorProjector(param,token);
                var path = MapPath(pr);
                using var it = (navigate ? pr.DescribeSteps(new(path,655),_=>{}) : pr.Encode(new(path))).GetEnumerator();
                while (CryptoScript.Model.InterpreterExecution.AccessState(it.MoveNext))
                {
                    work=pr.TraversalWork;
                    if (work>=1024) stop.Cancel();
                }
                return Task.FromResult(true);
            },()=>true,stop.Token);
            Assert.Fail("Capture completed after cancellation.");
        }
        catch (OperationCanceledException) { }
        Assert.That(work,Is.InRange(1024,1280));
        Assert.That(service.EditorCoordinator.Pending,Is.Zero);
        Assert.That(await service.RunInspectorReadAsync(_=>Task.FromResult(1),()=>true),Is.EqualTo(1));
    });

    [Test] public Task HistoricalDictionaryUsesActualKeyAndValuePaths() => GuiTestSession.RunAsync(async () =>
    {
        var param=DictionaryFixture(3);
        param.GetParameters()[""]="empty key";
        await using var f=new Fixture(param);
        await Until(()=>f.Model.Page!=null);
        await f.Model.OpenAsync(f.Row("GetParameters()"));
        Assert.That(await f.Copy(),Does.Contain("empty key"));
        await f.Invalidate(false);
        var pair=f.Row("[]");
        Assert.That(await f.Copy(pair),Is.EqualTo("{\"Key\":\"\",\"Value\":\"empty key\"}"),f.Model.Status);
        await f.Model.OpenAsync(pair);
        Assert.That(await f.Copy(f.Row("Value")),Is.EqualTo("empty key"));
        Assert.That(await f.Copy(f.Row("Key")),Is.Empty);
    });

    [Test] public Task IdenticallyNamedPropertiesKeepTheirParentIdentity() => GuiTestSession.RunAsync(async () =>
    {
        var variable = new StringVariableDeclaration { Value="identity" };
        await using var f = new Fixture(variable);
        await Until(()=>f.Model.Page!=null);
        await f.Copy();
        await f.Invalidate(false);
        Assert.That(await f.Copy(f.Row("Id")),Is.EqualTo("sample"));
        await f.Model.OpenAsync(f.Row("Type"));
        Assert.That(await f.Copy(f.Row("Id")),Is.EqualTo(variable.Type.Id.ToString()),f.Model.Status);
    });

    [Test] public async Task PropertyPageRejectsOverlapConflictingLengthAndForeignStamp()
    {
        var stamp=new CaptureStamp("v",new(new StringVariableDeclaration()),null!,1,1,1);
        using var snapshot=new InspectorSnapshot(stamp);
        async Task<SnapshotPart> Part(int slot,int page,int total)
        {
            var payload=new InspectorPayload();
            int length=Math.Min(100,total-page*100);
            await payload.AppendAsync(new byte[length*2],default);
            await payload.SealAsync(default);
            return new(stamp,InspectorMember.PropertyPage,slot,0,length,length,payload,
                Page:new(typeof(string).FullName!,"",[],false,StringLength:total),Request:new([],page));
        }
        snapshot.Add(await Part(1,0,200));
        var duplicate=await Part(2,0,200);
        var conflict=await Part(3,1,201);
        var foreign=(await Part(4,1,200)) with {Stamp=stamp with {Revision=2}};
        var truncated = (await Part(5,1,200)) with { Member=InspectorMember.PropertyValue, Total=101 };
        foreach (var part in new[]{duplicate,conflict,foreign,truncated})
        {
            using var owned=part.Payload;
            Assert.Throws<InvalidOperationException>(()=>snapshot.Add(part));
        }
        Assert.That(InspectorValueSource.Find(snapshot,[]),Is.Null);
    }
}
