namespace CryptoScript.ErrorListner;

// Only failed Check searches are recoverable at a statement boundary. Nested calls
// propagate this outcome so their enclosing operation/declaration cannot execute.
internal sealed class KeyCheckValueSearchException : SemanticErrorException
{
    internal KeyCheckValueSearchException(SemanticError error) => SemanticError = error;
}
