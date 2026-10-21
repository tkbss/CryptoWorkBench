using CryptoScript;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.SecretSharing;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class DeleteTests
{
    private readonly VariableDictionary dictionary = VariableDictionary.Instance();
    private VariableDeclaration[] previous = null!;
    [SetUp] public void Setup() { previous = dictionary.GetVariables().ToArray(); dictionary.Clear(); }
    [TearDown] public void Cleanup() { dictionary.Clear(); foreach (var v in previous) dictionary.Add(v); }
    private static CryptoScriptParser.ProgramContext Parse(string text)
    { var p=ParserBuilder.StringBuild(text);var result=p.program();Assert.That(p.NumberOfSyntaxErrors,Is.Zero,text);return result; }
    private void Execute(string text) => new CryptoScriptRunner().Execute(Parse(text));
    private StringVariableDeclaration Add(string id,string value="canary-secret")
    {var v=new StringVariableDeclaration{Id=id,Value=value,Text="source-canary"};dictionary.Add(v);return v;}
    private static BlockHeaderVariableDeclaration Header(string id="header") => new() {Id=id,
        Definition=Tr31HeaderDefinition.Create(new Dictionary<string,string>{["KBVID"]="D",["KEYU"]="D0",["ALGO"]="A",["MODEU"]="B",["EXP"]="E"})};
    private ParameterVariableDeclaration Linked(string id,BlockHeaderVariableDeclaration h)
    {
        var p=new ParameterVariableDeclaration{Mechanism="WRAP-AES-TR31"};p.SetParameter("#BLKH",h.Id);
        var result=ParameterVariableDeclaration.FromSnapshot(p.Mechanism,p.GetParameters(),p.ExplicitParameterNames,p.Value,h);
        result.Id=id;dictionary.Add(result);return result;
    }
    private void Failure(string text,string category)
    {
        var before=dictionary.GetVariables().ToArray();string json=new VariableSnapshotJsonSerializer().Serialize(before);
        int operations=0;var runner=new CryptoScriptRunner((_,_)=>operations++);
        var error=Assert.Throws<SemanticErrorException>(()=>runner.Execute(Parse(text)));
        Assert.That(error!.InnerException,Is.Null);Assert.That(runner.Diagnostics.Last().Message,Does.Contain(category));
        Assert.That(runner.Diagnostics.All(d=>!d.Message.Contains("canary") && !d.FunctionCall.Contains("canary")),Is.True);
        var after=dictionary.GetVariables().ToArray();Assert.That(after.Length,Is.EqualTo(before.Length));
        for(int i=0;i<before.Length;i++) Assert.That(after[i],Is.SameAs(before[i]));
        Assert.That(new VariableSnapshotJsonSerializer().Serialize(after),Is.EqualTo(json));
        if(category!="reference conflict") Assert.That(operations,Is.Zero);
    }
    [TestCase(false)][TestCase(true)]
    public async Task PublicRunnerDeletesAtomicallyAndReturnsVoid(bool asynchronous)
    {
        var first=Add("first");var keep=Add("keep");var last=Add("last");var second=Add("secondKeep");
        var runner=new CryptoScriptRunner();int passwords=0;runner.PasswordRequested+=(_,_)=>{passwords++;throw new Exception();};
        var program=asynchronous?await runner.ExecuteAsync(Parse("Delete(first,last)")):runner.Execute(Parse("Delete(first,last)"));
        Assert.That(dictionary.GetVariables(),Is.EqualTo(new VariableDeclaration[]{keep,second}));
        Assert.That(dictionary.Get("keep"),Is.SameAs(keep));Assert.That(dictionary.Get("secondKeep"),Is.SameAs(second));
        Assert.That(first.Value,Is.EqualTo("canary-secret"));Assert.That(last.Text,Is.EqualTo("source-canary"));
        var call=(FunctionCall)program.Statements.Single();Assert.That(call.ReturnVariable,Is.Null);Assert.That(call.CallText,Is.EqualTo("Delete(<redacted>)"));
        Assert.That(call.Invocation!.Arguments[0].SourceVariable,Is.SameAs(first));Assert.That(passwords,Is.Zero);
    }
    [TestCase("VAR")][TestCase("KEY")][TestCase("PARAM")][TestCase("PATH")][TestCase("TR31H")][TestCase("SECRET")]
    public void SupportsEveryRuntimeTypeWithoutMutatingRemovedObjects(string type)
    {
        VariableDeclaration value=type switch
        {
            "KEY"=>new KeyVariableDeclaration{Value="0x(000102030405060708090a0b0c0d0e0f)",ValueFormat=FormatConversions.HEX,KeyType=KeyType.Secret(KeyAlgorithm.Aes)},
            "PARAM"=>new ParameterVariableDeclaration(),
            "PATH"=>new PathVariableDeclaration{Type=new CryptoTypePath(),Value="untouched.json"},
            "TR31H"=>Header(),
            "SECRET"=>new SecretVariableDeclaration(new(new(SecretDescriptor.Split,2,2,8),[new(1,new byte[]{42})])),
            _=>new StringVariableDeclaration{Value="canary-secret"}
        };
        value.Id="target";dictionary.Add(value);string before=new VariableSnapshotJsonSerializer().Serialize([value]);
        Execute("Delete(target)");Assert.That(dictionary.GetVariables(),Is.Empty);
        Assert.That(new VariableSnapshotJsonSerializer().Serialize([value]),Is.EqualTo(before));
    }
    [TestCase(false)][TestCase(true)]
    public void AllWorksOnEmptyOrPopulatedDictionary(bool populated)
    {if(populated){Add("a");Add("b");}Execute("Delete(ALL)");Assert.That(dictionary.GetVariables(),Is.Empty);}
    [Test] public void AllIsExactCaseAndFunctionTokenNamesAreSelectable()
    {var upper=Add("ALL");Add("all");Add("MasterKey");Execute("Delete(all,MasterKey)");Assert.That(dictionary.GetVariables().Single(),Is.SameAs(upper));Execute("Delete(ALL)");Assert.That(dictionary.GetVariables(),Is.Empty);}
    [TestCase("Delete()","requires at least one")]
    [TestCase("Delete(missing)","unknown variable")]
    [TestCase("Delete(master,missing)","unknown variable")]
    [TestCase("Delete(master,master)","duplicate selectors")]
    [TestCase("Delete(ALL,master)","cannot be combined")]
    [TestCase("Delete(ALL,ALL)","cannot be combined")]
    [TestCase("Delete(0x(010203))","only variable references")]
    [TestCase("Delete(\"canary-secret\")","only variable references")]
    [TestCase("Delete(AES-ECB)","only variable references")]
    [TestCase("Delete(#ONCONFLICT:REPLACE)","accepted only")]
    [TestCase("Delete(Print(master))","only variable references")]
    [TestCase("VAR result=Delete(master)","cannot initialize")]
    [TestCase("Print(Delete(master))","cannot be used as a function argument")]
    [TestCase("Compare(Print(master),Delete(master))","cannot be used as a function argument")]
    [TestCase("VAR result=Compare(Print(master),Delete(master))","cannot be used as a function argument")]
    public void InvalidCallsLeaveAllObjectsAndMetadataUntouched(string script,string category)
    {Add("master");Add("unselected");Failure(script,category);}
    [Test] public void SharedHeaderRequiresDeletingEveryReferencingRoot()
    {
        var h=Header();dictionary.Add(h);var p=Linked("p",h);var q=Linked("q",h);Add("other");
        Failure("Delete(other,header)","reference conflict");Failure("Delete(p,header)","reference conflict");
        Execute("Delete(p)");Assert.That(q.HeaderReference,Is.SameAs(h));Assert.That(p.HeaderReference,Is.SameAs(h));
        Execute("Delete(q,header)");Assert.That(dictionary.GetVariables().Single().Id,Is.EqualTo("other"));
    }
    [Test] public void AllDeletesReferencedHeaderAndParamsTogether()
    {var h=Header();dictionary.Add(h);var p=Linked("p",h);Execute("Delete(ALL)");Assert.That(dictionary.GetVariables(),Is.Empty);Assert.That(p.HeaderReference,Is.SameAs(h));}
    [Test] public void SameNamedEmbeddedHeaderDoesNotBlockDistinctRoot()
    {
        var root=Header();var embedded=Header();dictionary.Add(root);var p=Linked("p",embedded);
        Execute("Delete(header)");Assert.That(dictionary.GetVariables().Single(),Is.SameAs(p));Assert.That(p.HeaderReference,Is.SameAs(embedded));
        Failure("Delete(header)","unknown variable");Execute("Delete(p)");Assert.That(dictionary.GetVariables(),Is.Empty);
    }
    [Test] public void TypedInvocationRejectsStaleOrValueOnlyReferences()
    {
        var old=Add("master");var live=Add("master");
        foreach(var arg in new[]{new ResolvedCallArgument(old.Value,ResolvedCallArgumentKind.Variable,old),new ResolvedCallArgument(live.Value,ResolvedCallArgumentKind.Variable)})
        {Assert.Throws<SnapshotArgumentException>(()=>DeleteOperations.Delete(new OperationInvocation([arg])));Assert.That(dictionary.Get("master"),Is.SameAs(live));}
    }
    [Test] public void SaveDeleteLoadRestoresKeyAndFilesSurviveAll()
    {
        string directory=Path.Combine(Path.GetTempPath(),"D01-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,"snapshot.json");
        try
        {
            Execute("PATH backup = "+file+"\nKEY master = GenerateKey(AES-ECB,128)");
            var key=dictionary.Get("master");string expected=new VariableSnapshotJsonSerializer().Serialize([key]);
            Execute("Save(backup,master) Delete(master) Load(backup,master) Print(master)");
            Assert.That(new VariableSnapshotJsonSerializer().Serialize([dictionary.Get("master")]),Is.EqualTo(expected));
            Assert.That(dictionary.Get("master"),Is.Not.SameAs(key));Execute("Delete(ALL)");
            Assert.That(dictionary.GetVariables(),Is.Empty);Assert.That(File.Exists(file),Is.True);
            Execute("PATH backup = "+file+"\nLoad(backup,master)");
            Assert.That(new VariableSnapshotJsonSerializer().Serialize([dictionary.Get("master")]),Is.EqualTo(expected));
        }
        finally {Directory.Delete(directory,true);}
    }
    [Test] public void RegistryAndInfoAreAvailable()
    {Assert.That(OperationList.Operations,Does.Contain("Delete"));Execute("Info(Delete)");}
}
