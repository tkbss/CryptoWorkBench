using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.Variables;
using static CryptoScriptUnitTest.StoreAdmissionTests;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class ScopedOperationTests
{
    private readonly VariableDictionary a = new();
    private readonly VariableDictionary b = new();
    private const string Material = "0x(000102030405060708090A0B0C0D0E0F)";

    [SetUp]
    public void ClearStores()
    {
        a.Clear(); b.Clear();
        TraceStore(a, "A before"); TraceStore(b, "B before");
    }

    [TearDown]
    public void RecordResult()
    {
        TraceStore(a, "A after"); TraceStore(b, "B after");
    }

    private static void Run(VariableDictionary store, string script) =>
        new CryptoScriptRunner(store).Execute(Parse(script));

    [Test]
    public void TypedAndMaterialLookupsUseOnlyTheAdmittedStore()
    {
        Run(a, $"KEY local=GenerateKey(AES-ECB,{Material}) PARAM p=Parameters(AES-ECB,#PAD:NONE)");
        Run(b, $"KEY foreign=GenerateKey(AES-ECB,{Material}) PARAM p=Parameters(AES-ECB,#PAD:NONE)");
        var keyB = b.Get("foreign");
        var paramB = b.Get("p");
        var before = b.Revision;
        using (InterpreterExecution.Enter(a))
        {
            var root = a.Get("local");
            var projected = new AlgorithmCallArgument(Material) { Store = a };
            Assert.That(AlgorithmArgumentResolver.ResolveTr31Key(projected), Is.SameAs(root));
            Assert.That(LegacyDeriveArgumentAdapter.ResolveKey(projected), Is.SameAs(root));
            Assert.Throws<InvalidOperationException>(() => AlgorithmArgumentResolver.ResolveKey(
                new AlgorithmCallArgument(Material, keyB) { Store = a }));
            Assert.Throws<InvalidOperationException>(() => AlgorithmArgumentResolver.ResolveParameter(
                new AlgorithmCallArgument("#MECH:AES-ECB", paramB) { Store = a }));
            Assert.Throws<ExecutionBusyException>(() => keyB.Value = "other");
            Assert.Throws<ExecutionBusyException>(() => ((ParameterVariableDeclaration)paramB).SetParameter("PAD", "NONE"));
            Assert.That(a.Get("foreign"), Is.Null);
            Run(a, "KEY found=Check(0x(C6A13B37)) VAR encrypted=Encrypt(p,local,0x(00112233445566778899AABBCCDDEEFF))");
            Assert.That(a.Get("found").Value, Is.EqualTo(Material).IgnoreCase);
            Assert.That(a.Get("encrypted").Value, Is.EqualTo("0x(69C4E0D86A7B0430D8CDB78070B4C55A)").IgnoreCase);
        }
        Assert.That(b.Revision, Is.EqualTo(before));
        Assert.That(b.Contains("found"), Is.False);
    }

    [Test]
    public void OwnedMetadataCopiesCannotChangeStateAndRetiredRootsCannotBeRead()
    {
        Run(a, "VAR data=1 PARAM p=#MECH:AES-ECB");
        var data = a.Get("data");
        var parameter = (ParameterVariableDeclaration)a.Get("p");
        var revision = a.Revision;
        var typeId = data.Type!.Id;
        Assert.Throws<InvalidOperationException>(() => data.Type.Id = "other");
        parameter.GetParameters().Clear();
        Assert.That(data.Type.Id, Is.EqualTo(typeId));
        Assert.That(parameter.GetParameter("MECH"), Is.EqualTo("AES-ECB"));
        Assert.That(a.Revision, Is.EqualTo(revision));
        data.Value = "2";
        Assert.That(a.Revision, Is.EqualTo(revision + 1));
        Run(a, "data=3");
        Assert.Throws<InvalidOperationException>(() => _ = data.Value);
        a.Clear();
        Assert.That(a.Revision, Is.EqualTo(revision + 3));
        Assert.Throws<InvalidOperationException>(() => _ = parameter.Value);
    }

    [Test]
    public void StandaloneMetadataReferencesCannotMutateTheCompatibilityStoreFromAnotherBook()
    {
        var compatibility = VariableDictionary.Instance();
        var parameter = new ParameterVariableDeclaration { Id = "compatParam", Mechanism = "AES-ECB" };
        var detachedMap = parameter.GetParameters();
        compatibility.Add(parameter);
        try
        {
            var savedMap = parameter.GetParameters();
            var revision = compatibility.Revision;
            using (InterpreterExecution.Enter(a))
            {
                savedMap.Clear();
                detachedMap.Clear();
                Assert.Throws<ExecutionBusyException>(() => parameter.ReplaceParameters(savedMap));
            }
            Assert.That(parameter.GetParameter("MECH"), Is.EqualTo("AES-ECB"));
            Assert.That(compatibility.Revision, Is.EqualTo(revision));
        }
        finally { Run(compatibility, "Delete(compatParam)"); }
    }

    [Test]
    public void LegacyDeriveUsesTheRequestedStoresKeyMaterial()
    {
        const string bdk = "0x(0123456789ABCDEFFEDCBA9876543210)";
        const string mechanism = "DUKPT-TDEA-INITIAL-KEY";
        Run(a, $"KEY bdk=GenerateKey(DES3-ECB,{bdk}) PARAM p=Parameters({mechanism})");
        Run(b, $"KEY other=GenerateKey(DES3-ECB,{Material}) PARAM p=Parameters({mechanism})");
        var algorithm = AlgorithmFactory.Create(mechanism);
        var arguments = new[] { a.Get("p").Value, bdk, "0x(FFFF9876543210E00000)" };
        Assert.That(algorithm.Derive(a, arguments).Value, Is.EqualTo("0x(6AC292FAA1315B4D858AB3A3D7D5933A)").IgnoreCase);
        Assert.Catch<ArgumentException>(() => algorithm.Derive(b, arguments));
    }

    [Test]
    public void PinPairUpdatesOwnedObjectsAndAdvancesOneRevision()
    {
        const string script = "PARAM source=#MECH:WRAP-DES3-PINBLOCK-3 #PAN:0x(1234567890123456) " +
            "PARAM target=#MECH:WRAP-AES-PINBLOCK-4 #PAN:0x(1234567890123456) " +
            "KEY sk=0x(0123456789ABCDEFFEDCBA9876543210) KEY tk=" + Material + " VAR block=0x(6694CAF82B1C2994)";
        Run(a, script); Run(b, script);
        var before = a.Revision;
        var other = b.Revision;
        var source = (ParameterVariableDeclaration)a.Get("source");
        var target = (ParameterVariableDeclaration)a.Get("target");
        Run(a, "PinBlockTranslate(source,sk,block,target,tk)");
        Assert.That(source.GetParameter("FILL"), Is.EqualTo("0x(ABCDEFABC)"));
        Assert.That(target.GetParameter("RANDOM"), Is.Not.Empty);
        Assert.That(a.Get("target"), Is.SameAs(target));
        Assert.That(a.Revision, Is.EqualTo(before + 1));
        Assert.That(b.Revision, Is.EqualTo(other));
        Assert.That(((ParameterVariableDeclaration)b.Get("target")).GetParameter("RANDOM"), Is.Empty);
    }

    [Test]
    public void LoadRejectReplaceAndReservedNamesAreAtomic()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            a.Add(new PathVariableDeclaration { Id = "path", Type = new CryptoTypePath(), Value = path });
            Run(a, "VAR k=1 VAR extra=2 Save(path,k,extra) Delete(k,extra) VAR k=3");
            var prior = a.Get("k");
            long revision = a.Revision;
            Assert.Throws<SemanticErrorException>(() => Run(a, "Load(path)"));
            Assert.That(a.Revision, Is.EqualTo(revision));
            Assert.That(a.Contains("extra"), Is.False);
            var parent = new CryptoScriptRunner(a, (_, _) =>
            {
                Assert.Throws<SemanticErrorException>(() => Run(a, "Load(path,#ONCONFLICT:REPLACE)"));
                Assert.That(a.Get("k"), Is.SameAs(prior));
            });
            parent.Execute(Parse("VAR extra=Compare(1,1)"));
            Run(a, "Load(path,#ONCONFLICT:REPLACE)");
            Assert.That(a.Revision, Is.EqualTo(revision + 2));
            Assert.That(a.Get("k").Value, Is.EqualTo("1"));
            using var lease = InterpreterExecution.Enter(a);
            var stale = new OperationInvocation([new("3", ResolvedCallArgumentKind.Variable, prior)]) { Store = a };
            Assert.Throws<InvalidOperationException>(() => new OutputOperations().Print(stale));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void SelectedParameterKeepsItsPrivateHeaderThroughWrap()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            foreach (var store in new[] { a, b })
                store.Add(new PathVariableDeclaration { Id = "path", Type = new CryptoTypePath(), Value = path });
            Run(a, "TR31H h=CreateHeader(KBVID:D,KEYU:D0,ALGO:A,MODEU:B,EXP:E) PARAM p=#MECH:WRAP-AES-TR31 #BLKH:h Save(path,p)");
            Run(b, "TR31H h=CreateHeader(KBVID:D,KEYU:D0,ALGO:T,MODEU:B,EXP:E) Load(path,p)");
            var dependency = ((ParameterVariableDeclaration)b.Get("p")).HeaderReference;
            Assert.That(dependency, Is.Not.Null.And.Not.SameAs(a.Get("h")).And.Not.SameAs(b.Get("h")));
            Run(b, $"KEY kbpk=GenerateKey(AES-CBC,256) KEY k=GenerateKey(AES-CBC,{Material}) VAR wire=Wrap(p,kbpk,k) KEY restored=Unwrap(p,kbpk,wire)");
            Assert.That(b.Get("restored").Value, Is.EqualTo(Material).IgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task SecureRoundTripPinsOriginAcrossBothPasswordAwaits()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            a.Add(new PathVariableDeclaration { Id = "path", Type = new CryptoTypePath(), Value = path });
            Run(a, "VAR data=1"); Run(b, "VAR data=2");
            var other = b.Get("data");
            var otherRevision = b.Revision;
            var runner = new CryptoScriptRunner(a);
            var nested = new CryptoScriptRunner(b);
            var program = Parse("VAR unexpected=1");
            var purposes = new List<PasswordPurpose>();
            runner.PasswordRequested += async (_, request) =>
            {
                await Task.Yield();
                purposes.Add(request.Purpose);
                Assert.Throws<ExecutionBusyException>(() => nested.Execute(program));
                Assert.Throws<ExecutionBusyException>(() => other.Value = "3");
                var password = PasswordSecret.TakeOwnership("test password".ToCharArray());
                return PasswordResponse.Supplied(request.RequestId, password, request.ConfirmationRequired
                    ? PasswordSecret.TakeOwnership("test password".ToCharArray()) : null);
            };
            long before = a.Revision;
            await runner.ExecuteAsync(Parse("SecureSave(path,data,#SECUREKDF:PBKDF2)"));
            Assert.That(a.Revision, Is.EqualTo(before));
            Run(a, "data=3");
            await runner.ExecuteAsync(Parse("SecureLoad(path,#ONCONFLICT:REPLACE)"));
            Assert.That(a.Get("data").Value, Is.EqualTo("1"));
            Assert.That(a.Revision, Is.EqualTo(before + 2));
            Assert.That(purposes, Is.EqualTo(new[] { PasswordPurpose.SecureSave, PasswordPurpose.SecureLoad }));
            Assert.That(b.Revision, Is.EqualTo(otherRevision));
            Assert.That(other.Value, Is.EqualTo("2"));
        }
        finally { File.Delete(path); }
    }
}
