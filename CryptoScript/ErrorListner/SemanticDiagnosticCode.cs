namespace CryptoScript.ErrorListner;

/// <summary>Stable general diagnostic categories, independent of function-contract error codes.</summary>
public enum SemanticDiagnosticCode
{
    Unclassified = 0,
    UnknownFunction = 1,
    UnknownVariable = 2,
    InvalidMechanism = 3,
    FunctionContractViolation = 4,
    FunctionExecutionFailed = 5,
    InvalidDeclaration = 6,
    InvalidParameter = 7,
    KeyUsageConflict = 8,
    KeyTypeConflict = 9,
    DesParityViolation = 10,
    WeakDes3Key = 11
}
