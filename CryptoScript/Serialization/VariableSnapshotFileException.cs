namespace CryptoScript.Serialization;

public enum VariableSnapshotFileError
{
    InvalidArgument,
    InvalidPath,
    AlreadyExists,
    NotFound,
    AccessDenied,
    WriteError,
    ReadError,
    InvalidSnapshot,
    UnsupportedFormat,
    SizeLimitExceeded
}

/// <summary>Sanitized failure: never contains paths, snapshot data or an inner exception.</summary>
public sealed class VariableSnapshotFileException : Exception
{
    public VariableSnapshotFileError Error { get; }

    internal VariableSnapshotFileException(VariableSnapshotFileError error)
        : base($"Snapshot file operation failed: {error}.") => Error = error;
}

public enum VariableSnapshotWriteMode
{
    CreateNew,
    ReplaceExisting
}
