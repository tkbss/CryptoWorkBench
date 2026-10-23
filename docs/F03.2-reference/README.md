# F03.2 reference and POS/host interoperability fixtures

These are public, reproducible test keys and messages, never production secrets.
`vectors.json` records every vector's provenance, source, exact key, input hex,
file, padding, initial IV/nonce, MAC length in bytes and expected tag.
`manifest.json` records byte lengths and SHA-256 hashes of all binary files and
the downloaded NIST archive. `.gitattributes` prevents newline conversion of `.bin`.

## Sources and independence

- Six **official NIST CAVP** GMAC cases are copied unchanged from
  [NIST GCM test vectors](https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Algorithm-Validation-Program/documents/mac/gcmtestvectors.zip),
  linked by the [NIST CAVP block-mode page](https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program/cavp-testing-block-cipher-modes).
  The files are `gcmEncryptExtIV128.rsp`, `gcmEncryptExtIV192.rsp`,
  `gcmEncryptExtIV256.rsp`, Count=0 in sections with IVlen=96, PTlen=0,
  Taglen=128 and AADlen=0 or 128. With empty plaintext the AAD is the MAC input.
  These are CAVP data for SP 800-38D, not claimed as examples printed in that standard.
- 253 **independently calculated** cases use the supplied `generate.py` with
  Python `cryptography` 50.0.2 and its OpenSSL backend, without loading CryptoScript,
  .NET or Bouncy Castle. They are not official NIST or ISO test vectors.
  [Cipher API](https://cryptography.io/en/latest/hazmat/primitives/symmetric-encryption/)
  and [TripleDES API](https://cryptography.io/en/latest/hazmat/decrepit/ciphers/)
  document the independent primitive implementation. AES-GCM authenticates data
  with empty plaintext. CBC vectors use independent CBC encryption and its last
  block. Retail vectors use DES-CBC(K1), then DES-decrypt(K2), then DES-encrypt(K3),
  representing single DES as TripleDES(K,K,K). Two-key TDEA expands K1,K2 to K1,K2,K1.
- Padding in the generator is a separate, auditable implementation of the stated
  reference contract. M3 preserves CryptoScript's existing prefix-length convention,
  including the zero data block for empty messages. No expected tag is obtained
  from CryptoWorkBench. The generator also verifies the NIST tags with AESGCM.

Reproduction (Python with `cryptography` installed):

```text
python docs/F03.2-reference/generate.py path/to/gcmtestvectors.zip
```

The binary inputs are `empty.bin` (0 bytes), `aligned.bin` (bytes 00 through 1F),
and `records.bin` (the exact prefix `00 50 4F 53 0D 0A 48 4F 53 54 0A 1E FF 00`
followed by all bytes 00 through FF). Six additional files contain the official
NIST AAD bytes. The generator deterministically rewrites these fixtures and JSON.
Changing the Python library version changes the recorded provenance string;
cryptographic tags and binary files must remain identical.

## External POS / HP-NonStop comparison

1. Select one full record in `vectors.json`; the same registered mechanism name,
   key bytes, padding, MAC length and nonce/initial chaining value must be used.
2. Transfer the associated `.bin` file in binary mode and verify its SHA-256 from
   `manifest.json`. A text editor, ASCII/EBCDIC conversion, newline translation,
   record expansion, record truncation or removed NULs changes the message.
3. Configure the POS/host MAC input as the identical byte stream, including all
   record-framing bytes. No implicit length prefix is allowed except the M3
   algorithm's specified prefix. MAC bytes are compared as bytes (hex letter case
   is irrelevant). No actual POS or HP-NonStop hardware was exercised locally.
4. Run a corresponding script from the repository root; use an absolute source
   path when running the GUI from a different working directory. Only a declared
   PATH activates file mode. VAR strings containing paths remain ordinary data.

For example, all of these use `records.bin` and key
`000102030405060708090A0B0C0D0E0F`:

```cryptoscript
KEY k = GenerateKey(AES-GMAC,0x(000102030405060708090A0B0C0D0E0F))
PARAM p = Parameters(AES-GMAC,#NONCE:0x(000000000000000000000003))
PATH source = "docs/F03.2-reference/records.bin"
VAR tag = Mac(p,k,source)
```

For the other examples, replace the mechanism in both GenerateKey and Parameters,
and replace the parameter list as follows. Expected tags are listed below.
DES3 keys use the same bytes but a TDEA classification; mismatched KEY metadata
produces warnings and must not be used to alter the key bytes.

| Mechanism | Parameters | Key bytes | MAC length |
|---|---|---:|---:|
| AES-GMAC | explicit NONCE `000000000000000000000003` | 16 | 16 |
| AES-CBC-MAC | PAD ISO-9797-M3, MACLEN "16" | 16 | 16 |
| DES3-CBC | PAD ISO-9797-M3, MACLEN "8" | 16 | 8 |
| DES3-RETAIL | PAD ISO-9797-M2, MACLEN "8" | 16 | 8 |

CBC-MAC always starts with a zero IV. DES3-CBC's encryption IV is ignored by MAC;
AES-CBC-MAC and Retail do not accept an external IV. Retail uses K3=K1 for these
16-byte keys; separate vectors cover 24-byte keys. AES vectors cover 16/24/32-byte
keys. Truncation takes the leftmost bytes.

Never reuse a GMAC nonce under the same key for different production messages.
Replaying the same published reference vector is distinct from production nonce
management. The generator uses a different explicit nonce per synthetic message.
CBC-MAC and Retail-MAC are legacy/reference tools, not a recommendation for new
security protocols. CBC-MAC on variable-length messages, DES's small block size
and short tags have security limitations. Padding alone does not provide CMAC's
security properties. Future F04 MacPack/MacVerify are separate container features
and are not implemented; these calls return hexadecimal VAR only and create no files.
- AES-GMAC: `1142b3e96979a5f1d46c49638acc5fc9`
- AES-CBC-MAC: `fab4e74f6aecdc77338632f65ea253cf`
- DES3-CBC: `9538e8ad8ee90eab`
- DES3-RETAIL: `bde504838150e69c`
