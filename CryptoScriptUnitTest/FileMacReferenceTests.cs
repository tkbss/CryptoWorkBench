using System.Text.Json;
using Antlr4.Runtime;
using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class FileMacReferenceTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    private static readonly string[] Mechanisms = ["AES-GMAC", "AES-CBC-MAC", "DES3-CBC", "DES3-RETAIL"];
    private static readonly string[] Paddings = ["NONE", "PKCS-7", "ANSI-X923", "ISO-7816", "ISO-9797-M1", "ISO-9797-M2", "ISO-9797-M3", "TLS-CBC"];
    private const string Nonce = "000102030405060708090A0B";
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CWE-F032-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/var/")) directory = "/private" + directory;
        Directory.CreateDirectory(directory);
        previous = VariableDictionary.Instance().GetVariables().ToArray(); VariableDictionary.Instance().Clear();
    }
    [TearDown] public void Cleanup()
    {
        VariableDictionary.Instance().Clear(); foreach (var v in previous) VariableDictionary.Instance().Add(v);
        Directory.Delete(directory, true);
    }
    private static string Hex(byte[] bytes) => FormatConversions.ByteArrayToHexString(bytes);
    private (ParameterVariableDeclaration P, KeyVariableDeclaration K, PathVariableDeclaration S) Inputs(
        string mechanism, string padding = "ISO-9797-M2", int keyBytes = 16, int macLength = 0, string? key = null, string nonce = Nonce)
    {
        var algo = AlgorithmFactory.Create(mechanism);
        string[] options = mechanism == "AES-GMAC" ? ["#NONCE:0x(" + nonce + ")"] :
            ["#PAD:" + padding, "#MACLEN:\"" + (macLength == 0 ? mechanism.StartsWith("AES") ? 16 : 8 : macLength) + "\""];
        var p = algo.GenerateParameters(mechanism, options); p.Id = "p";
        var k = algo.GenerateKey(mechanism, key is null ? Hex(Enumerable.Range(0,keyBytes).Select(i => (byte)i).ToArray()) : "0x("+key+")"); k.Id = "k";
        var s = new PathVariableDeclaration { Id = "source", Type = new CryptoTypePath(), Value = Path.Combine(directory,"source.bin") };
        foreach (var v in new VariableDeclaration[] { p,k,s }) VariableDictionary.Instance().Add(v);
        return (p,k,s);
    }
    private static AlgorithmCallArguments Args(ParameterVariableDeclaration p, KeyVariableDeclaration k, VariableDeclaration s) =>
        new([new(p.Value,p),new(k.Value,k),new(s.Value,s)]);
    private static CryptoScriptParser.ProgramContext Parse(string script) =>
        new CryptoScriptParser(new CommonTokenStream(new CryptoScriptLexer(new AntlrInputStream(script)))).program();
    public static IEnumerable<TestCaseData> Matrix()
    {
        foreach (var mechanism in Mechanisms)
        foreach (int key in mechanism.StartsWith("AES") ? new[] {16,24,32} : new[] {16,24})
        foreach (int mac in mechanism == "AES-GMAC" ? new[] {16} : mechanism.StartsWith("AES") ? new[] {8,12,16} : new[] {4,6,8})
        foreach (string pad in mechanism == "AES-GMAC" ? new[] {""} : mechanism == "DES3-RETAIL" ? new[] {"ISO-9797-M1","ISO-9797-M2"} : Paddings)
        foreach (int length in new[] {0,1,7,8,9,15,16,17,32,65535,65536,65537,131073})
            yield return new TestCaseData(mechanism,key,mac,pad,length);
    }
    [TestCaseSource(nameof(Matrix))]
    public void MatrixMatchesVarAndPreservesInputs(string mechanism, int key, int mac, string pad, int length)
    {
        var (p,k,s) = Inputs(mechanism,pad,key,mac);
        byte[] bytes = Enumerable.Range(0,length).Select(i=>(byte)i).ToArray(); File.WriteAllBytes(s.Value,bytes);
        string pv=p.Value,kv=k.Value; var names=p.ExplicitParameterNames.ToArray(); var parameters=p.GetParameters().ToArray();
        var data = new StringVariableDeclaration { Value=Hex(bytes),ValueFormat=FormatConversions.HEX };
        if (pad == "NONE" && (length==0 || length%(mechanism.StartsWith("AES")?16:8)!=0))
        {
            Assert.Throws<ArgumentException>(() => AlgorithmFactory.Create(mechanism).Mac(Args(p,k,data)));
            Assert.Throws<ArgumentException>(() => FileMacCoordinator.Mac(Args(p,k,s),s));
        }
        else
        {
            var expected=AlgorithmFactory.Create(mechanism).Mac(Args(p,k,data));
            var actual=FileMacCoordinator.Mac(Args(p,k,s),s);
            Assert.That(actual.Value,Is.EqualTo(expected.Value));
            Assert.That(actual.ValueFormat,Is.EqualTo(expected.ValueFormat)); Assert.That(actual.Type,Is.TypeOf<CryptoTypeVar>());
        }
        Assert.That(p.Value,Is.EqualTo(pv)); Assert.That(k.Value,Is.EqualTo(kv));
        Assert.That(p.GetParameters(),Is.EqualTo(parameters)); Assert.That(p.ExplicitParameterNames,Is.EqualTo(names));
        Assert.That(File.ReadAllBytes(s.Value),Is.EqualTo(bytes)); Assert.That(Directory.GetFileSystemEntries(directory),Is.EqualTo(new[]{s.Value}));
    }
    public static IEnumerable<TestCaseData> References()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","F03.2","vectors.json")));
        foreach (var row in json.RootElement.EnumerateArray()) yield return new TestCaseData(row.GetRawText()).SetName("Reference_"+row.GetProperty("id").GetString());
    }
    [TestCaseSource(nameof(References))]
    public void IndependentReferencesMatchPathAndVar(string json)
    {
        using var doc=JsonDocument.Parse(json); var row=doc.RootElement;
        string Text(string name)=>row.GetProperty(name).GetString()!;
        var (p,k,s)=Inputs(Text("mechanism"),Text("padding"),macLength:row.GetProperty("macLength").GetInt32(),key:Text("key"),nonce:Text("nonce"));
        byte[] bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","F03.2",Text("file")));
        Assert.That(Convert.ToHexString(bytes),Is.EqualTo(Text("input")).IgnoreCase);
        File.WriteAllBytes(s.Value,bytes);
        string expected="0x("+Text("expected")+")";
        Assert.That(FileMacCoordinator.Mac(Args(p,k,s),s).Value,Is.EqualTo(expected).IgnoreCase);
        using var fragmented = new FragmentedReadStream(bytes);
        Assert.That(FileMacCoordinator.MacStream(Args(p,k,s),fragmented).Value,
            Is.EqualTo(expected).IgnoreCase, "Independent reference tag across changing read boundaries");
        var data=new StringVariableDeclaration{Value=Hex(bytes),ValueFormat=FormatConversions.HEX};
        Assert.That(AlgorithmFactory.Create(Text("mechanism")).Mac(Args(p,k,data)).Value,Is.EqualTo(expected).IgnoreCase);
    }
    [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")] [TestCase("DES3-RETAIL")]
    [TestCase("AES-GMAC")]
    public void DocumentedFileExamplesExecuteWithFixture(string mechanism)
    {
        string doc=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"InfoDocs","Info.Mech."+mechanism+".md"));
        string section=doc[doc.IndexOf("### Streaming",StringComparison.Ordinal)..];
        string script=System.Text.RegularExpressions.Regex.Match(section,@"```text\s*([\s\S]*?)```").Groups[1].Value;
        Assert.That(script,Is.Not.Empty);
        string path=Path.Combine(directory,"records.bin");
        File.Copy(Path.Combine(AppContext.BaseDirectory,"Fixtures","F03.2","records.bin"),path);
        script=script.Replace("docs/F03.2-reference/records.bin",path.Replace('\\','/'));
        new CryptoScriptRunner().Execute(Parse(script));
        using var vectors=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","F03.2","vectors.json")));
        var row=vectors.RootElement.EnumerateArray().Single(v=>
            v.GetProperty("mechanism").GetString()==mechanism && v.GetProperty("file").GetString()=="records.bin" &&
            v.GetProperty("key").GetString()!.Length==32 && v.GetProperty("macLength").GetInt32()==(mechanism.StartsWith("AES")?16:8) &&
            v.GetProperty("padding").GetString()==(mechanism=="AES-GMAC"?"":mechanism=="DES3-RETAIL"?"ISO-9797-M2":"ISO-9797-M3"));
        Assert.That(VariableDictionary.Instance().Get("tag").Value,Is.EqualTo("0x("+row.GetProperty("expected").GetString()+")"));
    }
    [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")] [TestCase("DES3-RETAIL")]
    public void InvalidParametersAreRejected(string mechanism)
    {
        var (p,k,s)=Inputs(mechanism); File.WriteAllBytes(s.Value,[0]);
        foreach (string pad in new[]{"", "UNKNOWN", "ISO-10126"})
        {
            p.SetParameter("PAD",pad); Assert.Throws<ArgumentException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        }
        p.SetParameter("PAD","ISO-9797-M2");
        foreach (string mac in new[]{"0","3","17","bad"})
        {
            p.SetParameter("MACLEN",mac); Assert.Throws<ArgumentException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        }
        p.SetParameter("MACLEN",mechanism.StartsWith("AES")?"16":"8");
        if(mechanism != "DES3-CBC")
        {
            p.SetParameter("IV","0x(0000000000000000)");
            if (mechanism == "AES-CBC-MAC") Assert.Throws<FunctionContractException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
            else Assert.Throws<ArgumentException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        }
        Assert.That(Directory.GetFiles(directory),Is.EqualTo(new[]{s.Value}));
    }
    [TestCase("AES-GMAC")] [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")] [TestCase("DES3-RETAIL")]
    public void TechnicalKeysAndRunnerCancellation(string mechanism)
    {
        var (p,k,s)=Inputs(mechanism); File.WriteAllBytes(s.Value,[0,1]);
        string valid=k.Value; k.Value="0x(00)";
        Assert.Throws<ArgumentException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        if (mechanism.StartsWith("DES3"))
        {
            k.Value="0x(00000000000000000000000000000000)";
            Assert.Throws<ArgumentException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        }
        k.Value=valid;
        using var cts=new CancellationTokenSource();
        var runner=new CryptoScriptRunner((call,_)=>{if(call.Name=="Mac")cts.Cancel();});
        Assert.ThrowsAsync<OperationCanceledException>(async()=>await runner.ExecuteAsync(Parse("VAR cancelled = Mac(p,k,source)"),cts.Token));
        Assert.That(VariableDictionary.Instance().Get("cancelled"),Is.Null);
        Assert.That(Directory.GetFiles(directory),Is.EqualTo(new[]{s.Value}));
    }
    [Test] public void GmacNonceIsPreservedAndRequired()
    {
        var (p,k,s)=Inputs("AES-GMAC",nonce:"0001020304050607"); byte[] bytes=[0,1,0,255]; File.WriteAllBytes(s.Value,bytes);
        string original=p.Value;
        var data=new StringVariableDeclaration{Value=Hex(bytes),ValueFormat=FormatConversions.HEX};
        Assert.That(FileMacCoordinator.Mac(Args(p,k,s),s).Value,Is.EqualTo(AlgorithmFactory.Create("AES-GMAC").Mac(Args(p,k,data)).Value));
        Assert.That(p.Value,Is.EqualTo(original));
        foreach(var nonce in new[]{"", "0x()"})
        {
            p.SetParameter("NONCE",nonce); Assert.Throws<ArgumentException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        }
    }
    [Test] public void Des3CbcMacIgnoresEncryptionIv()
    {
        var (p,k,s)=Inputs("DES3-CBC"); File.WriteAllBytes(s.Value,[0]);
        string expected=FileMacCoordinator.Mac(Args(p,k,s),s).Value;
        p.SetParameter("IV","0x(FFFFFFFFFFFFFFFF)");
        Assert.That(FileMacCoordinator.Mac(Args(p,k,s),s).Value,Is.EqualTo(expected));
    }
    [TestCase("AES-GMAC",false)] [TestCase("AES-GMAC",true)]
    [TestCase("AES-CBC-MAC",false)] [TestCase("AES-CBC-MAC",true)]
    [TestCase("DES3-CBC",false)] [TestCase("DES3-CBC",true)]
    [TestCase("DES3-RETAIL",false)] [TestCase("DES3-RETAIL",true)]
    public void SourceAndAncestorLinksAreRejected(string mechanism,bool ancestor)
    {
        var (p,k,s)=Inputs(mechanism); string target=Path.Combine(directory,"target");
        Directory.CreateDirectory(target); string actual=Path.Combine(target,"bytes.bin"); File.WriteAllBytes(actual,[0,255]);
        string link=Path.Combine(directory,"link");
        try
        {
            if(ancestor) Directory.CreateSymbolicLink(link,target); else File.CreateSymbolicLink(link,actual);
        }
        catch(Exception ex) when(OperatingSystem.IsWindows() && ex is UnauthorizedAccessException or IOException)
        { Assert.Ignore("Windows symlink creation unavailable: "+ex.Message); }
        s.Value=ancestor?Path.Combine(link,"bytes.bin"):link;
        var before=Directory.GetFileSystemEntries(directory);
        Assert.Throws<IOException>(()=>FileMacCoordinator.Mac(Args(p,k,s),s));
        Assert.That(File.ReadAllBytes(actual),Is.EqualTo(new byte[]{0,255}));
        Assert.That(Directory.GetFileSystemEntries(directory),Is.EqualTo(before));
    }
    [TestCase("AES-GMAC")] [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")] [TestCase("DES3-RETAIL")]
    public void ShortReadsCancellationAndFailure(string mechanism)
    {
        var (p,k,s)=Inputs(mechanism);
        using var normal=new GeneratedStream(131073); using var shortReads=new GeneratedStream(131073,chunk:3);
        Assert.That(FileMacCoordinator.MacStream(Args(p,k,s),shortReads).Value,Is.EqualTo(FileMacCoordinator.MacStream(Args(p,k,s),normal).Value));
        using var cts=new CancellationTokenSource(); using var cancelled=new GeneratedStream(131073,onRead:cts.Cancel);
        Assert.Throws<OperationCanceledException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),cancelled,cts.Token));
        using var broken=new GeneratedStream(131073,onRead:()=>throw new IOException("injected"));
        Assert.Throws<IOException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),broken));
        Assert.That(Directory.GetFiles(directory),Is.Empty);
    }
    [TestCase("AES-GMAC")] [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")] [TestCase("DES3-RETAIL")]
    public void RunnerPreservesMetadataDiagnosticsAndNoResultOnError(string mechanism)
    {
        var (p,k,s)=Inputs(mechanism); File.WriteAllBytes(s.Value,[0,0xff,13,10]);
        k.KeyType=KeyType.Secret(KeyAlgorithm.Hmac); k.Usage=KeyUsagePolicy.Restricted(KeyUsage.Encrypt);
        var runner=new CryptoScriptRunner(); runner.Execute(Parse("VAR result = Mac(p,k,source)"));
        Assert.That(runner.Diagnostics.Select(d=>d.DiagnosticCode),Does.Contain(SemanticDiagnosticCode.KeyTypeConflict));
        Assert.That(runner.Diagnostics.Select(d=>d.DiagnosticCode),Does.Contain(SemanticDiagnosticCode.KeyUsageConflict));
        File.Delete(s.Value);
        Assert.Throws<SemanticErrorException>(()=>new CryptoScriptRunner().Execute(Parse("VAR failed = Mac(p,k,source)")));
        Assert.That(VariableDictionary.Instance().Get("failed"),Is.Null);
    }
    [TestCase("AES-CBC-MAC")] [TestCase("DES3-CBC")]
    public void M3RejectsUnreliableLengthsAndProcessesShortReads(string mechanism)
    {
        var (p,k,s)=Inputs(mechanism,"ISO-9797-M3");
        using var normal=new GeneratedStream(65537); using var shortReads=new GeneratedStream(65537,chunk:3);
        Assert.That(FileMacCoordinator.MacStream(Args(p,k,s),shortReads).Value,Is.EqualTo(FileMacCoordinator.MacStream(Args(p,k,s),normal).Value));
        using var unseekable=new GeneratedStream(32,seekable:false);
        Assert.Throws<IOException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),unseekable));
        using var overflow=new GeneratedStream(long.MaxValue);
        Assert.Throws<OverflowException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),overflow));
        using var earlyEnd=new GeneratedStream(32,actualLength:16);
        Assert.Throws<IOException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),earlyEnd));
        foreach(int delta in new[]{-1,1})
        {
            using var changed=new GeneratedStream(65537); changed.OnRead=()=>changed.ReportedLength+=delta;
            Assert.Throws<IOException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),changed));
        }
        using var unknown=new GeneratedStream(32,unknownLength:true);
        Assert.Throws<NotSupportedException>(()=>FileMacCoordinator.MacStream(Args(p,k,s),unknown));
    }
    [TestCase("AES-GMAC", "")] [TestCase("AES-CBC-MAC", "ISO-9797-M3")]
    [TestCase("DES3-CBC", "ISO-9797-M3")] [TestCase("DES3-RETAIL", "ISO-9797-M2")]
    public void LargeFilesUseBoundedMemory(string mechanism,string pad)
    {
        var (p,k,s)=Inputs(mechanism,pad);
        long Measure(int size)
        {
            using(var file=File.Create(s.Value)) file.SetLength(size);
            long before=GC.GetAllocatedBytesForCurrentThread(); var result=FileMacCoordinator.Mac(Args(p,k,s),s);
            long allocation=GC.GetAllocatedBytesForCurrentThread()-before;
            using var generated=new GeneratedStream(size);
            Assert.That(result.Value,Is.EqualTo(FileMacCoordinator.MacStream(Args(p,k,s),generated).Value));
            Assert.That(new FileInfo(s.Value).Length,Is.EqualTo(size)); return allocation;
        }
        Measure(65536); long small=Measure(1024*1024), large=Measure(16*1024*1024);
        TestContext.Out.WriteLine($"{mechanism}: 1 MiB={small} allocated bytes; 16 MiB={large} allocated bytes");
        Assert.That(large,Is.LessThan(small+1024*1024)); Assert.That(Directory.GetFiles(directory),Is.EqualTo(new[]{s.Value}));
    }
    private sealed class FragmentedReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        private static readonly int[] ReadSizes = [1, 3, 7, 8, 15, 16, 17, 2];
        private int next;
        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, Math.Min(count, ReadSizes[next++ % ReadSizes.Length]));
    }
    private sealed class GeneratedStream(long length,int chunk=65536,Action? onRead=null,bool seekable=true,long? actualLength=null,bool unknownLength=false):Stream
    {
        public long ReportedLength=length;
        private readonly long availableLength=actualLength??length;
        public Action? OnRead=onRead;
        public override long Length=>unknownLength?throw new NotSupportedException():ReportedLength;
        public override long Position{get;set;}
        public override bool CanRead=>true; public override bool CanSeek=>seekable; public override bool CanWrite=>false;
        public override int Read(byte[] buffer,int offset,int count)
        {
            int read=(int)Math.Min(Math.Min(count,chunk),availableLength-Position);
            Array.Clear(buffer,offset,read);Position+=read;OnRead?.Invoke();return read;
        }
        public override void Flush(){}
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
