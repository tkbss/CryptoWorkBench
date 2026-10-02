namespace CryptoScript.Documentation;

public static class InfoDocumentUri
{
    public const string Scheme = "cryptoscript-info";

    public static string ToCanonicalString(InfoDocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        return documentId.Kind switch
        {
            InfoDocumentKind.MechanismsOverview => $"{Scheme}://overview/mechanisms",
            InfoDocumentKind.FunctionsOverview => $"{Scheme}://overview/functions",
            InfoDocumentKind.ParametersOverview => $"{Scheme}://overview/parameters",
            InfoDocumentKind.PaddingsOverview => $"{Scheme}://overview/paddings",
            InfoDocumentKind.Mechanism =>
                $"{Scheme}://mechanism/{Escape(documentId.Mechanism!)}",
            InfoDocumentKind.Padding =>
                $"{Scheme}://padding/{Escape(documentId.Padding!)}",
            InfoDocumentKind.Function =>
                $"{Scheme}://function/{Escape(documentId.Function!)}",
            InfoDocumentKind.MechanismFunction =>
                $"{Scheme}://function/{Escape(documentId.Function!)}/{Escape(documentId.Mechanism!)}",
            InfoDocumentKind.MechanismParameter =>
                $"{Scheme}://parameter/{Escape(documentId.Mechanism!)}/{Escape(documentId.Parameter!)}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(documentId), documentId.Kind, "Unknown info document kind.")
        };
    }

    public static bool TryParse(string? value, out InfoDocumentId? documentId)
    {
        documentId = null;
        if (string.IsNullOrEmpty(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !uri.Scheme.Equals(Scheme, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !TryGetSegments(uri, out string[] segments))
        {
            return false;
        }

        try
        {
            documentId = uri.Host switch
            {
                "overview" when segments is ["mechanisms"] =>
                    InfoDocumentId.CreateMechanismsOverview(),
                "overview" when segments is ["functions"] =>
                    InfoDocumentId.CreateFunctionsOverview(),
                "overview" when segments is ["parameters"] =>
                    InfoDocumentId.CreateParametersOverview(),
                "overview" when segments is ["paddings"] =>
                    InfoDocumentId.CreatePaddingsOverview(),
                "mechanism" when segments is [var mechanism] =>
                    InfoDocumentId.CreateMechanism(mechanism),
                "padding" when segments is [var padding] =>
                    InfoDocumentId.CreatePadding(padding),
                "function" when segments is [var function] =>
                    InfoDocumentId.CreateFunction(function),
                "function" when segments is [var function, var mechanism] =>
                    InfoDocumentId.CreateMechanismFunction(function, mechanism),
                "parameter" when segments is [var mechanism, var parameter] =>
                    InfoDocumentId.CreateMechanismParameter(mechanism, parameter),
                _ => null
            };
        }
        catch (ArgumentException)
        {
            documentId = null;
            return false;
        }

        if (documentId is null ||
            !value.Equals(ToCanonicalString(documentId), StringComparison.Ordinal))
        {
            documentId = null;
            return false;
        }

        return true;
    }

    private static bool TryGetSegments(Uri uri, out string[] segments)
    {
        segments = Array.Empty<string>();
        string path = uri.AbsolutePath;
        if (path.Length < 2 || path[0] != '/' || path[^1] == '/')
            return false;

        string[] encodedSegments = path[1..].Split('/');
        if (encodedSegments.Any(string.IsNullOrEmpty))
            return false;

        var decodedSegments = new string[encodedSegments.Length];
        for (int index = 0; index < encodedSegments.Length; index++)
        {
            string segment;
            try
            {
                segment = Uri.UnescapeDataString(encodedSegments[index]);
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.IsNullOrEmpty(segment) ||
                segment is "." or ".." ||
                segment.Contains('/') ||
                segment.Contains('\\'))
            {
                return false;
            }

            decodedSegments[index] = segment;
        }

        segments = decodedSegments;
        return true;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
