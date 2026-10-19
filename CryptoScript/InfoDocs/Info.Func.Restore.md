# Restore(SECRET) → KEY

Reconstructs exclusively from the immutable descriptor and shares held in one typed SECRET variable. PARAM is not needed. XOR requires every Parts component; Shamir requires at least MinParts distinct shares. Extra Shamir points must lie on the same degree-at-most-k-1 polynomial; contradictions are hard errors.

```cryptoscript
PARAM p = Parameters(SECRET_SPLIT, #PARTS: 2)
SECRET s = Secret(p, 0x(010102), 0x(02ABB9))
KEY r = Restore(s)
```

This external XOR example returns Unknown/Secret, Unspecified usage, 16 bits, no attributes, and value `0x(AABB)`. An internally generated container restores an independent metadata snapshot. Key length alone never implies AES, TDEA or HMAC. Unknown algorithm does not prevent reconstruction. A fresh KEY object is created on each successful call.

Shamir and XOR do not authenticate reconstruction. With exactly k Shamir shares an altered share may yield a different, mathematically valid key. Even consistency of extra shares is not proof of origin. See [Shamir](cryptoscript-info://mechanism/SECRET_SHAMIR) and [XOR](cryptoscript-info://mechanism/SECRET_SPLIT) for security, ISO and PCI limitations. Malformed or insufficient containers fail, rather than emitting a non-blocking metadata warning.
