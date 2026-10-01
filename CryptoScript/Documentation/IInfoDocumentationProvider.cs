namespace CryptoScript.Documentation;

public interface IInfoDocumentationProvider
{
    bool HasDocumentation(string name);
    bool TryGetDocumentation(string name, out string documentation);

    bool HasDocument(InfoDocumentId id) => false;

    bool TryGetDocument(InfoDocumentId id, out string documentation)
    {
        documentation = string.Empty;
        return false;
    }
}
