using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class LanguageMetadataListIsolationTests
{
    [Test]
    public void RuntimeMechanismListsHaveEqualContentsButIndependentMutations()
    {
        var expected = AntlrLanguageMetadata.GetMechanisms();
        var copy = AntlrLanguageMetadata.GetMechanisms();
        copy.Add("test-only-mechanism");
        var mechanisms = MechanismList.Instance.Mechanisms;
        var parameterMechanisms = ParameterTypeList.Instance.Mechanism;
        Assert.Throws<NotSupportedException>(() => ((IList<string>)mechanisms).Add("test-only-mechanism"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)parameterMechanisms).RemoveAt(0));
        Assert.That(mechanisms, Is.EqualTo(expected));
        Assert.That(parameterMechanisms, Is.EqualTo(expected));
        Assert.That(copy, Is.EqualTo(expected.Append("test-only-mechanism")));
    }
}
