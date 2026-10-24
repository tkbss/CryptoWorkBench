# F05 independent reference vectors

`generate.py` imports no CryptoWorkBench code. It uses CPython and
`cryptography==50.0.2` (OpenSSL-backed AES/TDEA modes and AEAD).

```text
python -m pip install cryptography==50.0.2
python docs/F05-reference/generate.py
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj -c Release --filter FullyQualifiedName~F05
```

`vectors.json` contains 478 cases across every mechanism, key size and valid padding,
with lengths 0, 1, B-1, B, B+1, 2B and 3B+5 where allowed. Each case records key,
plaintext, independently padded plaintext, exact framed header, and payload in hex.
The complete container is `frame || payload`; AEAD payload is `ciphertext || tag`.
Frame bytes are also the complete AEAD AAD. Input patterns include leading and
trailing zeros and non-text bytes. Keys are public test data, never production keys.

PKCS-7, ANSI-X923, ISO-7816/M2, M1, M3 and TLS padding are independently constructed
in Python. CTR uses the 12-byte nonce and counter 000001ff, exercising carry between
counter octets. ISO-10126 fixtures use valid recorded 0xa5 filler: their decryption
is deterministic, but newly encrypted random filler is not compared unless only
the length octet is needed. Every fixture is decrypted by the streaming engine;
deterministic encryption is compared byte-for-byte. VAR encryption is compared
with the same inputs (AEAD receives the exact CWE frame as its AAD).

The existing `../F01-reference.hex` NIST SP 800-38A CBC known answer is retained.
F05FileTests additionally compare multi-MiB AEAD outputs with independent platform
AesGcm/AesCcm providers, test 64 MiB bounded allocations, and cover actual private
file publication. Platforms lacking system AesCcm skip only those extra comparisons;
the committed Python/OpenSSL CCM fixtures always execute. F05ValidationTests cover
CCM nonce lengths 7–13, malformed metadata/padding, CTR limits and fault cleanup.
GCM non-12-byte nonce tests use the existing VAR implementation as a regression
oracle; those additional cases are not an independent provider comparison.

References: [NIST SP 800-38A](https://csrc.nist.gov/pubs/sp/800/38/a/final),
[NIST SP 800-38C](https://csrc.nist.gov/pubs/sp/800/38/c/upd1/final),
[RFC 3610](https://www.rfc-editor.org/rfc/rfc3610),
[cryptography AEAD API](https://cryptography.io/en/latest/hazmat/primitives/aead/).
