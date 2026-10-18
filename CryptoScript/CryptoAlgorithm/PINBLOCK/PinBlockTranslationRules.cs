namespace CryptoScript.CryptoAlgorithm.PINBLOCK;

public enum PinBlockTranslationStatus { Allowed, IccOnly, Forbidden }

public sealed record PinBlockTranslationRule(
    int SourceFormat, int TargetFormat, PinBlockTranslationStatus Status,
    bool SourcePanRequired, bool TargetPanRequired, bool IdenticalPanRequired,
    string Reason, string Reference);

public static class PinBlockTranslationRules
{
    public static IReadOnlyList<PinBlockTranslationRule> Entries { get; } = Array.AsReadOnly(
        (from source in Enumerable.Range(0, 5)
         from target in Enumerable.Range(0, 5)
         let status = target == 2 ? PinBlockTranslationStatus.IccOnly
             : source == 2 || target == 1 && source != 1 ? PinBlockTranslationStatus.Forbidden
             : PinBlockTranslationStatus.Allowed
         select new PinBlockTranslationRule(source, target, status,
             UsesPan(source), UsesPan(target), UsesPan(source) && UsesPan(target),
             status == PinBlockTranslationStatus.IccOnly ? "Only for offline ICC PIN verification or PIN change."
             : status == PinBlockTranslationStatus.Forbidden ? "The source format must not be translated to this target format."
             : "Translation permitted subject to the full PAN and parameter contracts.",
             "PCI PIN Security 3.1, Requirement 3-3; ISO Format 4 Information Supplement, Figure 3.5, page 10."))
        .ToArray());

    public static PinBlockTranslationRule Get(int source, int target)
    {
        if (source is < 0 or > 4 || target is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(source));
        return Entries[source * 5 + target];
    }

    public static bool UsesPan(int format) => format is 0 or 3 or 4;
    public static string Mechanism(int format) => format switch
    {
        >= 0 and <= 3 => $"WRAP-DES3-PINBLOCK-{format}",
        4 => "WRAP-AES-PINBLOCK-4",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
