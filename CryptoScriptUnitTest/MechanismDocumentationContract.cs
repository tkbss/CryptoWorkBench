namespace CryptoScriptUnitTest;

internal static class MechanismDocumentationContract
{
    internal static readonly IReadOnlyList<string> RequiredSections = Array.AsReadOnly(new[]
    {
        "## Key Features",
        "## Functions",
        "## Parameters",
        "## Example Usage"
    });

    internal static void AssertRequiredSections(string document)
    {
        Assert.That(
            ExtractLevelTwoSections(document),
            Is.EqualTo(RequiredSections),
            "mechanism documentation must use the shared section contract");
    }

    internal static IReadOnlyList<string> ExtractExecutableExamples(string document)
    {
        string[] lines = NormalizeLineEndings(document).Split('\n');
        var examples = new List<string>();
        var legacyLines = new List<string>();
        List<string>? cryptoScriptBlock = null;
        bool insideOtherFence = false;

        void FlushLegacyExample()
        {
            if (legacyLines.Count == 0)
                return;

            examples.Add(string.Join(Environment.NewLine, legacyLines));
            legacyLines.Clear();
        }

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (cryptoScriptBlock is not null)
            {
                if (IsFence(trimmed))
                {
                    string example = string.Join(Environment.NewLine, cryptoScriptBlock).Trim();
                    if (example.Length > 0)
                        examples.Add(example);
                    cryptoScriptBlock = null;
                }
                else
                {
                    cryptoScriptBlock.Add(line.TrimEnd());
                }

                continue;
            }

            if (insideOtherFence)
            {
                if (IsFence(trimmed))
                    insideOtherFence = false;

                continue;
            }

            if (IsCryptoScriptFence(trimmed))
            {
                FlushLegacyExample();
                cryptoScriptBlock = new List<string>();
            }
            else if (IsFence(trimmed))
            {
                insideOtherFence = true;
            }
            else if (IsLegacyExampleLine(line))
            {
                legacyLines.Add(line.TrimEnd());
            }
        }

        if (cryptoScriptBlock is not null)
            throw new InvalidOperationException("Unclosed CryptoScript code fence.");

        FlushLegacyExample();
        return examples.AsReadOnly();
    }

    internal static string ExtractCombinedExecutableExample(string document) =>
        string.Join(Environment.NewLine, ExtractExecutableExamples(document));

    private static string[] ExtractLevelTwoSections(string document) =>
        NormalizeLineEndings(document).Split('\n')
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToArray();

    private static bool IsCryptoScriptFence(string line)
    {
        if (!IsFence(line))
            return false;

        string language = line[3..].Trim();
        return language.Equals("cryptoscript", StringComparison.OrdinalIgnoreCase) ||
               language.Equals("crypto-script", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFence(string line) =>
        line.StartsWith("```", StringComparison.Ordinal);

    private static bool IsLegacyExampleLine(string line) =>
        line.StartsWith("KEY ", StringComparison.Ordinal) ||
        line.StartsWith("PARAM ", StringComparison.Ordinal) ||
        line.StartsWith("VAR ", StringComparison.Ordinal);

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
