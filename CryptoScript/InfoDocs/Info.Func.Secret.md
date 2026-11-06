# Secret(PARAM, Shares...) → SECRET

Imports literal share arguments into an immutable container. No algorithm/usage/attributes of the original key are inferred. At least one share is needed to determine payload length; fewer than MinParts is allowed, but cannot yet be restored. A PARAM variable reference is required. A repeated #SHARE parameter is not supported.

These three equivalent examples reconstruct `0x(AABB)` using the linear polynomial with constant AABB and coefficient 0102 under either supported field representation:

```cryptoscript
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2)
SECRET s = Secret(p, 0x(03A9BD), 0x(01ABB9))
KEY r = Restore(s)
```

```cryptoscript
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2, #SHAREFORMAT: "Y-X")
SECRET s = Secret(p, 0x(A9BD03), 0x(ABB901))
KEY r = Restore(s)
```

```cryptoscript
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2, #SHAREFORMAT: "SEPARATE-X")
SECRET s = Secret(p, 3, 0x(A9BD), 1, 0x(ABB9))
KEY r = Restore(s)
```

X-Y stores one index byte first, Y-X stores it last. SEPARATE-X strictly alternates decimal integer index and HEX payload. The format changes representation only, not arithmetic. Indices 1..255 must be distinct; order is immaterial. All payloads must have equal nonzero byte lengths. Empty/odd HEX, missing indices, malformed pairs, excess shares or invalid parameters cause hard errors. Never mix independent share sets. Exactly k shares cannot prove authenticity; see [Restore](cryptoscript-info://function/Restore). Use [Print](cryptoscript-info://type/SECRET) to obtain copyable import statements without file persistence.
