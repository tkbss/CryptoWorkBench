# Independent CWM v1 fixtures

Run `node docs/F04-reference/generate.mjs` from the repository root. The generator
uses only Node's standard crypto/fs libraries and ECMAScript JSON.stringify. It
does not load CryptoScript or the C# canonicalizer. Fixture keys are public test
material.

`reference.json` contains source bytes, the raw KEY, the derived metadata key,
the exact canonical UTF-8 input string, and an independently generated complete
HMAC-SHA256 CWM container. CwmTests verifies that container and compares newly
packed output against it.

`jcs-vectors.json` covers the entire ASCII control range, Unicode sorting across
the UTF-16 surrogate boundary, combining characters without normalization, escaping
and safe integer boundaries in the CWM JCS domain. Tests additionally retain the
RFC 8785 section 3.2.3 ordering example and Appendix B safe integer examples.
This fixture intentionally does not claim floating-point support: CWM v1 admits
only safe integer JSON number tokens.

Primary specifications: [RFC 8785](https://www.rfc-editor.org/rfc/rfc8785.html)
and [RFC 5869](https://www.rfc-editor.org/rfc/rfc5869.html).
