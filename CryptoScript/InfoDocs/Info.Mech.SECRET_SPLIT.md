# MECHANISM SECRET_SPLIT

XOR n-of-n splitting generates n-1 independently random components and chooses the last component so that XOR of all n components equals the key. Every component has the original key's byte length. All components are necessary: `2 <= #PARTS <= 255`, and MinParts always equals Parts. Component indices identify components uniquely; they are not interpolation coordinates. External indices can be any distinct integers from 1 to 255.

```cryptoscript
KEY k = GenerateKey(AES-ECB, 128)
PARAM p = Parameters(SECRET_SPLIT, #PARTS: 3)
SECRET s = Share(p, k)
Print(s)
KEY restored = Restore(s)
```

`#MECH` and [PARTS](cryptoscript-info://parameter/SECRET_SPLIT/PARTS) are required. [SHAREFORMAT](cryptoscript-info://parameter/SECRET_SPLIT/SHAREFORMAT) defaults to `"X-Y"`; `"Y-X"` and `"SEPARATE-X"` are also supported. MINPARTS and POLYNOM are forbidden. See [Secret](cryptoscript-info://function/Secret) for imports and [SECRET](cryptoscript-info://type/SECRET) for complete Print examples.

Missing components prevent Restore. Malformed/empty HEX, unequal lengths, excess components, duplicate indices and indices outside 1..255 are hard errors. Corrupt components can produce an incorrect key: XOR splitting provides no authenticity or integrity check. Restore uses only the container descriptor, not the original PARAM variable. Internal sharing copies key metadata; external import restores Unknown algorithm/Unspecified usage without invented attributes.

Randomness is supplied by .NET RandomNumberGenerator. Explicit Print displays every full component and can disclose the key. Managed strings and GC memory are not guaranteed to be erasable. XOR splitting is an additive construction in characteristic two; it is not the Shamir construction reviewed against ISO/IEC 19592-2:2017 section 5.2. General terminology: ISO/IEC 19592-1:2016. No certification is claimed. Productive PCI ceremonies still require their own controls for dual control, custodians and secure cryptographic devices.

Functions: [Share](cryptoscript-info://function/Share), [Secret](cryptoscript-info://function/Secret), [Restore](cryptoscript-info://function/Restore).
