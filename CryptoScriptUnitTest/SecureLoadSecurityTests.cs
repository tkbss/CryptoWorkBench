using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using CryptoScript.SecretSharing;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

public partial class SecureLoadTests
{
    [TestCase("payload")][TestCase("tag")][TestCase("password")]
    public void GmacTamperingHasSameAuthenticationFailure(string field)
    {
        var e=WriteFixture(3);
        if(field=="payload") e["payload"]=(string)e["payload"]!+" ";
        if(field=="tag") {var b=Convert.FromBase64String((string)e["tag"]!);b[0]^=1;e["tag"]=Convert.ToBase64String(b);}
        Write(e);Failure("SecureAuthenticationFailed",runner:Runner(field=="password"?"wrong":Password));
    }
    [TestCase(65535,3,4)][TestCase(262145,3,4)][TestCase(262144,4,4)][TestCase(65536,3,3)]
    [TestCase(65536,2,4)][TestCase(65536,7,4)][TestCase(65536,3,0)][TestCase(65536,3,5)]
    public void ArgonReaderLimitsRejectBeforePassword(int memory,int iterations,int lanes)
    {
        var e=WriteFixture(0);e["kdf"]!["memoryKiB"]=memory;e["kdf"]!["iterations"]=iterations;e["kdf"]!["parallelism"]=lanes;Write(e);
        int requests=0;var r=new CryptoScriptRunner();r.PasswordRequested+=(_,p)=>{requests++;return Task.FromResult(Supply(p));};
        Failure("ResourceLimitExceeded",runner:r);Assert.That(requests,Is.Zero);
    }
    [Test] public void BrokenTypedHeaderDependencyRejectsEvenWhenUnselected()
    {
        var header=new BlockHeaderVariableDeclaration {Id="header",Definition=Tr31HeaderDefinition.Create(new Dictionary<string,string>
            {["KBVID"]="D",["KEYU"]="D0",["ALGO"]="A",["MODEU"]="B",["EXP"]="E"})};
        var p=new ParameterVariableDeclaration {Mechanism="WRAP-AES-TR31"};p.SetParameter("#BLKH",header.Id);
        var linked=ParameterVariableDeclaration.FromSnapshot(p.Mechanism,p.GetParameters(),p.ExplicitParameterNames,p.Value,header);linked.Id="params";
        var roots=new VariableDeclaration[]{new StringVariableDeclaration {Id="data",Value="ok"},linked};
        var json=JObject.Parse(new VariableSnapshotJsonSerializer().Serialize(roots));
        json["dependencies"]=new JArray();
        ProtectPayload(json.ToString(),SecureManifest.Create(roots));Failure("InvalidSecureSnapshot","SecureLoad(path,data)");
    }
    private void ProtectPayload(string payload, SecureManifestEntry[] manifest, bool gmac=false)
    {
        File.WriteAllBytes(path.Value,SecureContainerCryptography.Protect(new(Gmac:gmac,Pbkdf:true),Encoding.UTF8.GetBytes(payload),
            SecureManifest.Json(manifest),SecureManifest.Frame(manifest),Encoding.UTF8.GetBytes(Password),new byte[16],new byte[12]));
    }
    [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
    public async Task AllTypesRoundTripPreservesMetadataAndHeaderIdentity(bool gmac,bool pbkdf)
    {
        var header=new BlockHeaderVariableDeclaration {Id="header",Definition=Tr31HeaderDefinition.Create(new Dictionary<string,string>
            {["KBVID"]="D",["KEYU"]="D0",["ALGO"]="A",["MODEU"]="B",["EXP"]="E"})};
        var parameter=new ParameterVariableDeclaration {Mechanism="WRAP-AES-TR31"}; parameter.SetParameter("#BLKH",header.Id);
        var linked=ParameterVariableDeclaration.FromSnapshot(parameter.Mechanism,parameter.GetParameters(),parameter.ExplicitParameterNames,parameter.Value,header); linked.Id="params";
        var second=ParameterVariableDeclaration.FromSnapshot(parameter.Mechanism,parameter.GetParameters(),parameter.ExplicitParameterNames,parameter.Value,header); second.Id="second";
        var key=new KeyVariableDeclaration {Id="key",ValueFormat=FormatConversions.HEX,Value="0x(000102030405060708090a0b0c0d0e0f)",KeyType=KeyType.Secret(KeyAlgorithm.Aes)};
        var secret=new SecretVariableDeclaration(new(new(SecretDescriptor.Split,2,2,8),[new(1,new byte[]{42})])){Id="secret"};
        var data=new StringVariableDeclaration {Id="data",Value="秘密🔐\0"};
        foreach(var v in new VariableDeclaration[]{header,linked,second,key,secret,data}) dictionary.Add(v);
        string expected=new VariableSnapshotJsonSerializer().Serialize(dictionary.GetVariables());
        await Runner().ExecuteAsync(Parse($"SecureSave(path,ALL,#SECUREMODE:{(gmac?"GMAC":"GCM")},#SECUREKDF:{(pbkdf?"PBKDF2":"ARGON2ID")})"));
        dictionary.Clear(); dictionary.Add(path);
        await Runner().ExecuteAsync(Parse("SecureLoad(path,ALL,#ONCONFLICT:REPLACE)"));
        Assert.That(new VariableSnapshotJsonSerializer().Serialize(dictionary.GetVariables()),Is.EqualTo(expected));
        var restored=(ParameterVariableDeclaration)dictionary.Get("params");
        Assert.That(restored.HeaderReference,Is.SameAs(dictionary.Get("header")));
        Assert.That(((ParameterVariableDeclaration)dictionary.Get("second")).HeaderReference,Is.SameAs(restored.HeaderReference));
        Failure("ReferenceConflict","SecureLoad(path,header,#ONCONFLICT:REPLACE)");
        dictionary.Clear(); dictionary.Add(path);
        await Runner().ExecuteAsync(Parse("SecureLoad(path,params,second)"));
        Assert.That(dictionary.Contains("header"),Is.False);
        Assert.That(((ParameterVariableDeclaration)dictionary.Get("params")).HeaderReference,
            Is.SameAs(((ParameterVariableDeclaration)dictionary.Get("second")).HeaderReference));
    }

    [TestCase("manifest")][TestCase("unselectedRoot")][TestCase("reference")][TestCase("source")]
    public async Task AuthenticatedButInvalidSnapshotsNeverPublish(string kind)
    {
        var roots=new VariableDeclaration[]{new StringVariableDeclaration {Id="data",Value="ok"},new StringVariableDeclaration {Id="other",Value="other"}};
        string payload=new VariableSnapshotJsonSerializer().Serialize(roots); var manifest=SecureManifest.Create(roots);
        var json=JObject.Parse(payload);
        if(kind=="manifest") manifest[1]=manifest[1] with {Identifier="mismatch"};
        if(kind=="unselectedRoot") json["variables"]![1]!["metadata"]!["type"]="BAD";
        if(kind=="reference") json["variables"]![1]!["objectId"]="o1";
        if(kind=="source") json["variables"]![1]!["unexpected"]="Print(0x(010203))";
        ProtectPayload(json.ToString(),manifest);
        Failure("InvalidSecureSnapshot","SecureLoad(path,data)");
        await Task.CompletedTask;
    }

    [TestCase(false)][TestCase(true)]
    public void AuthenticatedPayloadBytesAreNotReserialized(bool gmac)
    {
        string payload="  "+Encoding.UTF8.GetString(Convert.FromHexString((string)Fixture()["payloadHex"]!))+" \n";
        var roots=new VariableSnapshotJsonSerializer().Deserialize(payload);
        ProtectPayload(payload,SecureManifest.Create(roots),gmac);
        using var envelope=SecureEnvelope.Parse(File.ReadAllBytes(path.Value));
        byte[] password=Encoding.UTF8.GetBytes(Password), decoded=SecureContainerCryptography.Unprotect(envelope,password,default);
        try {Assert.That(decoded,Is.EqualTo(Encoding.UTF8.GetBytes(payload))); Assert.That(password,Is.All.Zero);}
        finally {CryptographicOperations.ZeroMemory(decoded);}
        Assert.DoesNotThrowAsync(async()=>await Runner().ExecuteAsync(Parse("SecureLoad(path)")));
    }

    [Test] public void InvalidUtf8AfterSuccessfulGcmAuthenticationIsRejected()
    {
        File.WriteAllBytes(path.Value,SecureContainerCryptography.Protect(new(Pbkdf:true),new byte[]{0xff},"[]"u8.ToArray(),
            SecureManifest.Frame([]),Encoding.UTF8.GetBytes(Password),new byte[16],new byte[12]));
        Failure("InvalidSecureSnapshot");
    }
    [TestCase("file")][TestCase("roots")][TestCase("wire")][TestCase("payload")][TestCase("depth")]
    public void ResourceLimitsRejectBeforePassword(string kind)
    {
        var e=WriteFixture();
        if(kind=="file") {using var file=new FileStream(path.Value,FileMode.Create); file.SetLength((long)SecureSaveOptions.FileLimit+1);}
        else
        {
            if(kind=="roots") e["manifest"]=new JArray(Enumerable.Range(0,4097).Select(i=>new JObject{{"identifier","a"+i},{"type","VAR"},{"key",null}}));
            if(kind=="payload") e["payload"]=new string('A',4*((SecureSaveOptions.PayloadLimit+2)/3)+4);
            if(kind=="depth") e["extra"]=JArray.Parse("[[[[[[[[[0]]]]]]]]]");
            Write(e);
            if(kind=="wire") File.WriteAllText(path.Value,File.ReadAllText(path.Value).Replace("\"manifest\": [","\"manifest\": ["+new string(' ',SecureSaveOptions.ManifestLimit)));
        }
        int requests=0; var runner=new CryptoScriptRunner();runner.PasswordRequested+=(_,r)=>{requests++;return Task.FromResult(Supply(r));};
        Failure(kind=="depth"?"InvalidEnvelope":"ResourceLimitExceeded",runner:runner);Assert.That(requests,Is.Zero);
    }

    [TestCase("kdf")][TestCase("cipher")][TestCase("entry")][TestCase("key")]
    public void NestedDuplicateFieldsAreRejected(string scope)
    {
        string text=WriteFixture().ToString();
        text=scope switch
        {
            "kdf"=>text.Replace("\"outputLength\": 32","\"outputLength\":32,\"outputLength\":32"),
            "cipher"=>text.Replace("\"tagLength\": 16","\"tagLength\":16,\"tagLength\":16"),
            "entry"=>text.Replace("\"type\": \"VAR\"","\"type\":\"VAR\",\"type\":\"VAR\""),
            _=>text.Replace("\"type\": \"VAR\"","\"type\":\"KEY\"").Replace("\"key\": null","\"key\":{\"algorithm\":\"Aes\",\"bitLength\":128,\"kcv\":null,\"kcv\":null}")
        };
        File.WriteAllText(path.Value,text);int requests=0;var r=new CryptoScriptRunner();r.PasswordRequested+=(_,e)=>{requests++;return Task.FromResult(Supply(e));};
        Failure("InvalidEnvelope",runner:r);Assert.That(requests,Is.Zero);
    }
    [Test] public void CancellationAtFinalAllocationBoundaryDoesNotPublish()
    {
        using var cts=new CancellationTokenSource();
        IEnumerable<VariableDeclaration> State(){yield return new StringVariableDeclaration {Id="data",Value="new"};cts.Cancel();}
        Assert.Throws<OperationCanceledException>(()=>dictionary.PublishBatch(State(),cts.Token));
        Assert.That(dictionary.GetVariables().Single(),Is.SameAs(path));
    }
    [Test] public async Task CancellationAfterCommittedFinalStatementDoesNotUndoSuccess()
    {
        WriteFixture();using var cts=new CancellationTokenSource();
        IEnumerable<StatementNode?> Statements()
        {
            yield return AntlrToStatement.Map(Parse("SecureLoad(path)").statement(0));
            Assert.That(dictionary.Contains("data"),Is.True);cts.Cancel();
        }
        await Runner().ExecuteAsync(Statements(),cts.Token);Assert.That(dictionary.Contains("data"),Is.True);
    }
    [Test] public async Task CancelledPendingPasswordQuarantinesLateResponse()
    {
        WriteFixture();using var cts=new CancellationTokenSource();var pending=new TaskCompletionSource<PasswordResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked=new TaskCompletionSource<PasswordRequestEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var r=new CryptoScriptRunner();r.PasswordRequested+=(_,e)=>{asked.SetResult(e);return pending.Task;};
        var task=r.ExecuteAsync(Parse("SecureLoad(path)"),cts.Token);var request=await asked.Task;cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async()=>await task);
        char[] password=Password.ToCharArray();pending.SetResult(PasswordResponse.Supplied(request.RequestId,PasswordSecret.TakeOwnership(password)));
        for(int i=0;i<100 && password.Any(c=>c!='\0');i++) await Task.Delay(10);
        Assert.That(password,Is.All.EqualTo('\0'));Assert.That(dictionary.GetVariables().Single(),Is.SameAs(path));
    }
    [Test] public void PlainLoadNeverFallsBackToSecureReader()
    {WriteFixture();Failure("UnsupportedFormat","Load(path)");}
}
