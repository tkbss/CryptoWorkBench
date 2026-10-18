# WRAP-AES-PINBLOCK-4

ISO 9564 format 4 PIN-block wrapping and unwrapping with AES.

`Wrap(PARAM, KEY, VAR)` accepts a PIN stored as a hexadecimal `VAR`, for example `0x(01234)`, and returns a 16-byte encrypted PIN block as a hexadecimal `VAR`. The PIN must contain 4 to 12 decimal nibbles. Leading zeros and odd PIN lengths are preserved; quoted strings are not valid PIN inputs.

`Unwrap(PARAM, KEY, VAR)` accepts a 16-byte encrypted PIN block and returns the recovered PIN as a hexadecimal `VAR`.

The key must contain 16, 24 or 32 bytes of AES key material. Format 4 uses two AES-ECB operations without padding or an IV. The encoded PIN field is encrypted, XORed with the encoded PAN field, and encrypted again. Unwrap applies the inverse construction and validates the recovered PIN field.

Wrap and Unwrap treat declared key metadata as descriptive: an explicit algorithm other than AES produces a non-blocking `KeyTypeConflict`; usage without `PinEncrypt` produces a non-blocking `KeyUsageConflict`. `Encrypt` is not equivalent to `PinEncrypt`. Unknown algorithm and Unspecified usage cause no corresponding conflict warning. Type conflicts are reported before usage conflicts. The selected mechanism still performs AES format 4, without changing KEY bytes, KeyType or Usage. Without a diagnostic sink these conflicts remain non-blocking and no global warning collection is created. DES parity and TDEA weakness diagnostics do not apply to AES format 4.

Warnings do not relax technical validation: invalid key lengths or encodings, PIN/PAN fields, encrypted block lengths, format identifiers, fill fields and parameter contracts remain blocking errors. Invalid recovered structures retain the public error `Decrypted PIN block is invalid.` These analysis capabilities do not imply suitability for production PIN processing or PCI certification.

`#PAN` is required and contains the complete 10-to-19-digit decimal PAN, including its check digit, encoded as a hexadecimal value.

`#RANDOM` is optional for Wrap and must contain exactly 16 hexadecimal nibbles. When omitted, eight fresh bytes are generated with a cryptographically secure random-number generator. After successful Wrap, the value actually used is stored in the original PARAM variable. An explicit value is accepted only to support reproducible development, analysis, and tests; the caller is responsible for suitable freshness and entropy, and reuse impairs the intended randomization. Unwrap extracts the random field and overwrites any existing `#RANDOM` value in the original PARAM variable.

The PARAM variable is mutated only after all validation, field processing and AES operations have completed successfully. `#MECH` and `#PAN` remain unchanged.

A wrong PAN can produce an invalid recovered PIN field, but is not guaranteed to be detected when the resulting field happens to be structurally valid.
Structural validation is not authentication and provides no authenticity guarantee for a PIN block.

## Security boundary

CryptoScript processes cleartext PINs and key material in ordinary process memory. It is neither a secure cryptographic device (SCD) nor a hardware security module (HSM). These PIN-block functions are exclusively for development, analysis, interoperability testing, and education; they are not a substitute for standards-compliant production PIN processing. Processing real cardholder PINs in production requires appropriate secure infrastructure and compliance with all applicable requirements. No PCI compliance or certification is implied.

## PIN-block translation

Use `Info(pintranslations)` for [PCI translation rules and examples](cryptoscript-info://overview/pintranslations). `PinBlockTranslate` retains Wrap and Unwrap as independent operations.
