# WRAP-DES3-PINBLOCK-0

ISO 9564 format 0 PIN-block wrapping and unwrapping with TDEA.

`Wrap(PARAM, KEY, VAR)` accepts a 4-to-12-digit PIN stored as a hexadecimal nibble sequence such as `0x(0123)` and returns an 8-byte encrypted PIN block as `VAR`. Only decimal PIN nibbles `0` through `9` are permitted. Leading zeros and odd PIN lengths are preserved. Quoted strings are not valid PIN inputs. `Unwrap(PARAM, KEY, VAR)` accepts an 8-byte encrypted PIN block and returns the PIN as a hexadecimal `VAR`.

The key must contain exactly 16 or 24 bytes of TDEA key material. DES3-ECB processes exactly one block without padding or an IV.

Wrap and Unwrap treat declared key metadata as descriptive: an explicit algorithm other than TDEA produces a non-blocking `KeyTypeConflict`; usage without `PinEncrypt` produces a non-blocking `KeyUsageConflict`. `Encrypt` is not equivalent to `PinEncrypt`. Unknown algorithm and Unspecified usage cause no corresponding conflict warning. The selected mechanism still performs TDEA, without changing the KEY bytes, KeyType or Usage.

Invalid odd DES parity produces `DesParityViolation`; known weak or semi-weak DES components and adjacent effective K1=K2 or K2=K3 degeneration produce `WeakDes3Key`. K1=K3 alone is ordinary two-key TDEA, not single-DES degeneration. At most one warning per quality category is emitted per operation. When applicable, warnings appear in type, usage, parity, weakness order and contain no key bytes. Without a diagnostic sink these conflicts remain non-blocking and no global warning collection is created. Weak and degenerate test keys are supported only for analysis and interoperability testing, not as suitable keys for production payment processing.

Warnings do not relax technical validation: invalid key lengths or encodings, PIN/PAN fields, encrypted block lengths, format identifiers, fill fields and parameter contracts remain blocking errors. Invalid recovered structures retain the public error `Decrypted PIN block is invalid.` No partial output-field update occurs on a technical failure.

`#PAN` is required and contains the complete 10-to-19-digit decimal PAN, including its check digit, encoded as a hexadecimal value. The check digit is excluded when constructing the format 0 PAN field.

Unwrap validates the format identifier, PIN length, decimal PIN digits, and fixed `F` fill field. A wrong PAN can sometimes produce structurally plausible clear data and is therefore not guaranteed to be detected.
Structural validation is not authentication and provides no authenticity guarantee for a PIN block.

## Security and lifecycle

CryptoScript processes cleartext PINs and key material in ordinary process memory. It is neither a secure cryptographic device (SCD) nor a hardware security module (HSM). These PIN-block functions are exclusively for development, analysis, interoperability testing, and education; they are not a substitute for standards-compliant production PIN processing. Processing real cardholder PINs in production requires appropriate secure infrastructure and compliance with all applicable requirements. No PCI compliance or certification is implied.

TDEA/DES3 is a legacy algorithm. This mechanism is retained for analysis and interoperability with existing systems. New systems should evaluate a current, approved AES-based solution, including format 4 where it is suitable after assessment of the complete payment infrastructure; format 4 is not automatically suitable for every existing environment.
