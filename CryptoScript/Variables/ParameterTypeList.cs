using System.Collections.Immutable;
using CryptoScript.Model;

namespace CryptoScript.Variables;

public sealed class ParameterTypeList
{
    public static ParameterTypeList Instance { get; } = new();
    public IReadOnlyList<string> Paddings { get; } = AntlrLanguageMetadata.GetPaddings().ToImmutableList();
    public IReadOnlyList<string> Mechanism => MechanismList.Instance.Mechanisms;
    public IReadOnlyList<string> ParameterTypes { get; }

    private ParameterTypeList()
    {
        // Load's call option is language metadata, never a PARAM/mechanism field.
        var names = AntlrLanguageMetadata.GetParameters().Where(name => name != "#ONCONFLICT").ToList();
        if (!names.Contains("#MACLEN")) names.Add("#MACLEN");
        ParameterTypes = names.ToImmutableList();
    }
}
