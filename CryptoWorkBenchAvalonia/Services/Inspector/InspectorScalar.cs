using System;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

// Lossless detached scalar carrier for the I2 audited projector. No conversion through double or ToString.
internal sealed record InspectorScalar
{
    internal object? Value { get; }
    internal InspectorScalar(object? value)
    {
        if (value != null && value is not (string or char or bool or byte or int or long or ulong))
            throw new NotSupportedException("Scalar type requires an audited projector.");
        Value = value;
    }
}
