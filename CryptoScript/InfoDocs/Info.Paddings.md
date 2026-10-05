# Paddings

Padding selects how CryptoScript prepares data for mechanisms that require padded input. Use the canonical name as the value of the `#PAD` parameter. Select a padding below to open its detailed documentation.

---

## Supported Paddings

| Padding | Description |
|---------|-------------|
| [ANSI-X923](cryptoscript-info://padding/ANSI-X923) | Zero-valued padding bytes followed by a padding-length byte. |
| [ISO-10126](cryptoscript-info://padding/ISO-10126) | Random padding bytes followed by a padding-length byte. |
| [ISO-7816](cryptoscript-info://padding/ISO-7816) | Padding beginning with a `0x80` marker followed by zero-valued bytes. |
| [ISO-9797-M1](cryptoscript-info://padding/ISO-9797-M1) | ISO/IEC 9797-1 Padding Method 1. |
| [ISO-9797-M2](cryptoscript-info://padding/ISO-9797-M2) | ISO/IEC 9797-1 Padding Method 2. |
| [ISO-9797-M3](cryptoscript-info://padding/ISO-9797-M3) | ISO/IEC 9797-1 Padding Method 3, which includes the original data length. |
| [NONE](cryptoscript-info://padding/NONE) | No padding is added or removed. |
| [PKCS-7](cryptoscript-info://padding/PKCS-7) | Padding bytes that each contain the total padding length. |
| [TLS-CBC](cryptoscript-info://padding/TLS-CBC) | The padding byte format used with TLS CBC records. |
