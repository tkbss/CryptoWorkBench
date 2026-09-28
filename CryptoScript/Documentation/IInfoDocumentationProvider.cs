namespace CryptoScript.Documentation;

public interface IInfoDocumentationProvider
{
    bool HasDocumentation(string name);
    bool TryGetDocumentation(string name, out string documentation);
}
