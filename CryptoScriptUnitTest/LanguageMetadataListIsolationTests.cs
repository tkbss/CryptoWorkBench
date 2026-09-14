using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class LanguageMetadataListIsolationTests
{
    private List<string> mechanisms = null!;
    private List<string> parameterMechanisms = null!;
    private string[] mechanismValues = null!;
    private string[] parameterMechanismValues = null!;

    [SetUp]
    public void SaveLists()
    {
        mechanisms = MechanismList.Instance.Mechanisms;
        parameterMechanisms = ParameterTypeList.Instance.Mechanism;
        mechanismValues = mechanisms.ToArray();
        parameterMechanismValues = parameterMechanisms.ToArray();
    }

    [TearDown]
    public void RestoreLists()
    {
        mechanisms.Clear();
        mechanisms.AddRange(mechanismValues);
        parameterMechanisms.Clear();
        parameterMechanisms.AddRange(parameterMechanismValues);
        MechanismList.Instance.Mechanisms = mechanisms;
        ParameterTypeList.Instance.Mechanism = parameterMechanisms;
    }

    [Test]
    public void RuntimeMechanismListsHaveEqualContentsButIndependentMutations()
    {
        Assert.That(mechanisms, Is.EqualTo(parameterMechanisms));
        Assert.That(mechanisms, Is.Not.SameAs(parameterMechanisms));
        mechanisms.Add("test-only-mechanism");
        Assert.That(parameterMechanisms, Is.EqualTo(parameterMechanismValues));
        parameterMechanisms.RemoveAt(0);
        Assert.That(mechanisms, Is.EqualTo(mechanismValues.Append("test-only-mechanism")));
        Assert.That(AntlrLanguageMetadata.GetMechanisms(), Is.EqualTo(mechanismValues));
    }
}
