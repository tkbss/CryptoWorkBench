"""Independent fixed-vector verification for G02; requires Python cryptography.

Explicit ISO fields below are test fixtures, not a runtime PIN codec.
PIN 01234; PAN 1234567890123456; fixed transaction/fill/random fields.
"""
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES


def xor(left, right):
    return bytes(a ^ b for a, b in zip(left, right, strict=True))


def encrypt(algorithm, data):
    operation = Cipher(algorithm, modes.ECB()).encryptor()
    return operation.update(data) + operation.finalize()


key = bytes.fromhex("0123456789ABCDEFFEDCBA9876543210")
tdea = TripleDES(key + key[:8])
pan = bytes.fromhex("0000456789012345")
fields = ["0501234FFFFFFFFF", "1501234012345678", "2501234FFFFFFFFF", "3501234ABCDEFABC"]
expected = ["2D8F0FF09AFDCCD0", "429B597B94CB18A5", "978BB4AC07BBD2EB", "6694CAF82B1C2994"]
for index, field in enumerate(fields):
    clear = bytes.fromhex(field)
    if index in (0, 3):
        clear = xor(clear, pan)
    actual = encrypt(tdea, clear).hex().upper()
    assert actual == expected[index]
    print(index, actual)

aes = algorithms.AES(bytes.fromhex("000102030405060708090A0B0C0D0E0F"))
first = encrypt(aes, bytes.fromhex("4501234AAAAAAAAA0011223344556677"))
actual = encrypt(aes, xor(first, bytes.fromhex("41234567890123456000000000000000"))).hex().upper()
assert actual == "6685B016B0CEC63CB7E0F2663C9551EC"
print(4, actual)
