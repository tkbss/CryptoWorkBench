AS-01 compatibility fixture

symmetric-baseline.json was captured on Windows with .NET SDK 10.0.401 from
the unchanged production code at c3586fe5b07116fb314fddc46148e78d679e808b,
before removing asymmetric key handling. A temporary capture test passed
against that code and was then removed. Do not regenerate this fixture with
the implementation being tested.

The seven keys use Unknown/19 bytes, AES/16,24,32 bytes, TDEA/16,24 bytes,
and HMAC/19 bytes. Each key consists of consecutive bytes starting at 01.
Identifiers are key0 through key6; type is CryptoTypeKey, encoding is HEX,
bit length is known, usage is Restricted(MacVerify), and derivation is
the literal "fixture". All values are synthetic public test data.

The fixture stores the legacy JSON strings, one structured snapshot, the
SecureManifest JSON and binary frame, and two authenticated containers:
AES-256-GCM and AES-256-GMAC, both PBKDF2-HMAC-SHA256/600000 iterations.
The public test password is "AS-01 fixture password"; salt bytes are 00..0F
and nonce bytes 10..1B. Deterministic values are used only by tests.

Tests compare the legacy writer, snapshot writer, manifest and authenticated
container bytes against this pre-change output and read the existing formats.
Snapshot pretty-print line endings follow the host platform, as before.
The container tests preserve the exact original plaintext bytes.

The legacy reader already reconstructs Type as a general CryptoType and can
lose its derived Name. AS-01 preserves that behavior: the legacy reader test
compares key metadata/material, while exact writer output is checked from
the typed snapshot. The unrelated legacy Type behavior is not repaired here.
