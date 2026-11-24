using System.Collections.Immutable;

namespace CryptoScript.Model;

public sealed class MechanismList
{
    public static MechanismList Instance { get; } = new();
    public IReadOnlyList<string> Mechanisms { get; } = AntlrLanguageMetadata.GetMechanisms().ToImmutableList();
    private MechanismList() { }
}
