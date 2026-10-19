# MECHANISM SECRET_SHAMIR

Shamir shares symmetric key bytes using a threshold: any k distinct shares reconstruct the original key. `2 <= #MINPARTS <= #PARTS <= 255`. Every byte is an element of GF(256), a field with 256 elements. Addition is XOR; multiplication is carryless polynomial multiplication followed by reduction.

`#POLYNOM` selects the field representation: `"SHAMIR-GF256-11B"` (default, reduction polynomial x^8+x^4+x^3+x+1) or `"SHAMIR-GF256-11D"` (x^8+x^4+x^3+x^2+1). These labels do not select the original key algorithm. The fixed reduction polynomial is different from the independently random sharing polynomial for each key byte. The latter has the secret byte at x=0 and k-1 random coefficients, including possible zero coefficients. Reconstruction uses Lagrange interpolation at zero. Share coordinates can be any distinct values from 1 to 255, even when greater than PARTS.

```cryptoscript
KEY k = GenerateKey(AES-ECB, 128)
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 3)
SECRET s = Share(p, k)
Print(s)
KEY restored = Restore(s)
```

External shares are repeated positional arguments. A structurally valid collection below the threshold can be held, but Restore fails. See [Secret](cryptoscript-info://function/Secret) for all formats and [SECRET](cryptoscript-info://type/SECRET) for complete Print output. The container holds all reconstruction parameters independently of p. Additional points are checked against the degree-at-most-k-1 polynomial and inconsistent points cause a hard error. This check does not authenticate shares: exactly k incorrect shares can reconstruct an incorrect key. Even a fully consistent set may be forged. Use the same profile, threshold and share-set origin when exchanging data.

Internal shares preserve independent copies of known key type, usage, derivation and optional attributes. External shares restore an Unknown secret key with Unspecified usage and actual bit length; no algorithm is inferred from length.

Duplicate/zero/out-of-range indices, unequal/empty payloads, invalid profiles, forbidden parameters, excess or insufficient shares, malformed HEX and inconsistent extra shares are hard errors. No share bytes appear in ordinary variable display; explicit Print discloses all shares and therefore enough material to reconstruct the key.

The core is adapted from Bouncy Castle Java commit `c314b9cdffa3958a0eff5344f8fdcdb0181ed830`; it uses tabulation-free GF arithmetic and .NET RandomNumberGenerator. Managed memory and JIT execution are not a certified constant-time or secure-erasure environment.

Reference: [ISO/IEC 19592-2:2017 preview, section 5.2](https://cdn.standards.iteh.ai/samples/65425/86901291939c44edb722f64aef7985d9/ISO-IEC-19592-2-2017.pdf), with terminology from ISO/IEC 19592-1:2016. Review evidence and its scope are in `docs/architecture/G03-report.md`; no formal ISO certification is claimed. Share/Restore alone do not establish PCI compliance, dual control, independent key custodians or use of secure cryptographic devices. This workbench has no G03 file import/export, Save/Load or SECRET serialization.

Parameters: [PARTS](cryptoscript-info://parameter/SECRET_SHAMIR/PARTS), [MINPARTS](cryptoscript-info://parameter/SECRET_SHAMIR/MINPARTS), [POLYNOM](cryptoscript-info://parameter/SECRET_SHAMIR/POLYNOM), [SHAREFORMAT](cryptoscript-info://parameter/SECRET_SHAMIR/SHAREFORMAT).

Functions: [Share](cryptoscript-info://function/Share), [Secret](cryptoscript-info://function/Secret), [Restore](cryptoscript-info://function/Restore).
