using System.Collections.Immutable;
using System.Diagnostics;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services.Inspector;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class DictionaryKeyCorrectionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void LongKeysRetainFullIdentity(bool collision)
    {
        string prefix=new string('x',1199994)+"Ω\r\n\"\\";
        string a=prefix+"a", b=prefix+"b";
        var param=new ParameterVariableDeclaration();
        var map=param.GetParameters();
        map.Clear();
        if(collision) map.Add(b,"second");
        map.Add(a,"first");
        var pr=new InspectorProjector(param,default);
        var path=ImmutableArray.Create(pr.Describe(new([])).Rows.Single(r=>r.Step.Kind==StepKind.Parameters).Step);
        InspectorPage? page=null;
        using var it=pr.DescribeSteps(new(path),result=>page=result).GetEnumerator();
        long alloc=0,visits=0;
        double ms=0;
        bool more;
        do
        {
            long before=GC.GetAllocatedBytesForCurrentThread(),work=pr.TraversalWork,start=Stopwatch.GetTimestamp();
            more=it.MoveNext();
            ms=Math.Max(ms,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            alloc=Math.Max(alloc,GC.GetAllocatedBytesForCurrentThread()-before);
            visits=Math.Max(visits,pr.TraversalWork-work);
        } while(more);
        TestContext.Out.WriteLine($"collision={collision} max allocation={alloc} visits={visits} ms={ms:F3}");
        Assert.That(alloc,Is.LessThan(2*1024*1024));
        Assert.That(visits,Is.LessThanOrEqualTo(256));
        var rows=page!.Rows.Where(r=>r.Step.Kind==StepKind.Entry).ToArray();
        Assert.That(rows.Length,Is.EqualTo(map.Count));
        Assert.That(rows.Select(r=>r.Name).Distinct().Count(),Is.EqualTo(1));
        foreach(var row in rows)
        {
            Assert.That(row.Name.Length,Is.LessThan(100));
            var selected=path.Add(row.Step);
            Assert.That(InspectorProjector.Key(selected).Length,Is.LessThan(256));
            var properties=pr.Describe(new(selected)).Rows;
            var expected=row==rows[0]?a:b;
            Assert.That(string.Concat(pr.Encode(new(selected.Add(properties.Single(r=>r.Name=="Key").Step))))==expected,Is.True);
            Assert.That(string.Concat(pr.Encode(new(selected.Add(properties.Single(r=>r.Name=="Value").Step)))),Is.EqualTo(map[expected]));
        }
        var escaped=prefix.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n");
        var expectedJson="{\""+escaped+"a\":\"first\""+(collision?",\""+escaped+"b\":\"second\"":"")+"}";
        Assert.That(string.Concat(pr.Encode(new(path)))==expectedJson,Is.True);
    }

    [Test] public Task HistoricalCollidingRowsCopyExactKeys() => GuiTestSession.RunAsync(async () =>
    {
        await using var host=new CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService();
        var editor=new CryptoWorkBenchAvalonia.Controls.WorkbenchTextEditor { Text="source" };
        host.AttachEditor(editor,null!);
        var variables=new CryptoWorkBenchAvalonia.ViewModels.VariableViewModel {Automation=host};
        var param=new ParameterVariableDeclaration {Id="params"};
        var map=param.GetParameters();
        map.Clear();
        string prefix=new('k',1200000);
        string[] keys=[prefix+"a",prefix+"z"];
        map.Add(keys[1],"second");
        map.Add(keys[0],"first");
        var dictionary=VariableDictionary.Instance();
        try
        {
            dictionary.Clear();
            host.Mutate(()=>dictionary.Add(param),variables.ApplyVariables);
            variables.SelectedVariable=variables.DataVariables.Single();
            using var model=new CryptoWorkBenchAvalonia.ViewModels.VariableInspectorViewModel(variables);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while(model.Page==null) await Task.Delay(5,timeout.Token);
            await model.OpenAsync(model.Page.Rows.Single(r=>r.Step.Kind==StepKind.Parameters));
            async Task<string?> Copy(InspectorRow? row)
            {
                string? result=null;
                await model.ValueAsync(row,false,text=>{result=text;return Task.CompletedTask;});
                return result;
            }
            Assert.That(await Copy(null)=="{\""+keys[0]+"\":\"first\",\""+keys[1]+"\":\"second\"}",Is.True);
            editor.Text+=" draft";
            var rows=model.Page!.Rows.Where(r=>r.Step.MapEntry).ToArray();
            Assert.That(rows[0].Name,Is.EqualTo(rows[1].Name));
            for(int i=0;i<2;i++)
            {
                await model.OpenAsync(rows[i]);
                Assert.That(await Copy(model.Page!.Rows.Single(r=>r.Name=="Key"))==keys[i],Is.True,model.Status);
                Assert.That(await Copy(model.Page.Rows.Single(r=>r.Name=="Value")),Is.EqualTo(i==0?"first":"second"));
                await model.OpenAsync(null,back:true);
            }
        }
        finally { dictionary.Clear(); }
    });

    [Test] public void CommonPrefixComparisonCanBeCancelled()
    {
        using var stop=new CancellationTokenSource();
        var param=new ParameterVariableDeclaration();
        var map=param.GetParameters();
        map.Clear();
        string prefix=new('z',1200000);
        map.Add(prefix+"a","1");
        map.Add(prefix+"b","2");
        var pr=new InspectorProjector(param,stop.Token);
        var path=ImmutableArray.Create(pr.Describe(new([])).Rows.Single(r=>r.Step.Kind==StepKind.Parameters).Step);
        using var it=pr.DescribeSteps(new(path),_=>Assert.Fail()).GetEnumerator();
        for(int i=0;i<5;i++) Assert.That(it.MoveNext(),Is.True);
        stop.Cancel();
        Assert.Throws<OperationCanceledException>(()=>it.MoveNext());
    }
}
