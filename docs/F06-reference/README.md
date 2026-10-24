# F06 independent digest references

Run `python generate.py` with CPython and OpenSSL hashlib support. No CryptoScript
or Bouncy Castle implementation is used. The checked-in 99 vectors cover all eleven
registered HASH mechanisms at lengths 0, 1, 63, 64, 65, 65535, 65536, 65537, 196625.
Input byte i is i modulo 256, including NUL, CR/LF and all high bytes. Values are
complete uppercase CryptoScript HEX digests. Tests also compare the in-memory path.
