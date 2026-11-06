"""Independent S05-A v1 reference. Python cryptography 50.0.2/OpenSSL, no C# helpers.
Run manually to reproduce vectors.json; not required by NUnit/CI.
All credentials/material below are synthetic public test data.
"""
import base64
import hashlib
import hmac
import json
import struct
from pathlib import Path
from cryptography.hazmat.primitives.kdf.argon2 import Argon2id
from cryptography.hazmat.primitives.ciphers.aead import AESGCM


def u32(n): return struct.pack('>I', n)
def u64(n): return struct.pack('>Q', n)
def blob(b): return u64(len(b)) + b
def string(s): return blob(s.encode('utf-8'))
def b64(b): return base64.b64encode(b).decode('ascii')
def js(o): return json.dumps(o, ensure_ascii=False, separators=(',', ':'))


salt = bytes(range(16))
nonce = bytes(range(16, 28))
password = 'päss🔐\0'.encode('utf-8')
payload = js({'format': 'CryptoScriptVariables', 'version': 1, 'variables': [
    {'objectId': 'o1', 'identifier': 'data', 'metadata': {'type': 'VAR', 'format': '',
     'payloadKind': 'scalar', 'runtimeTypeId': None, 'gmac': ''}, 'value': 'Grüezi 世界 🔐\n\0'}
], 'dependencies': []}).encode('utf-8')
manifest = [{'identifier': 'data', 'type': 'VAR', 'key': None}]
m = u32(1) + string('data') + string('VAR') + b'\0'
vectors = []
for pbkdf in (False, True):
    if pbkdf:
        kid = 'PBKDF2-HMAC-SHA256'
        kdf = {'id': kid, 'salt': b64(salt), 'iterations': 600000, 'outputLength': 32}
        kf = string(kid) + blob(salt) + u32(600000) + u32(32)
        master = hashlib.pbkdf2_hmac('sha256', password, salt, 600000, 32)
    else:
        kid = 'ARGON2ID'
        kdf = {'id': kid, 'version': 19, 'salt': b64(salt), 'memoryKiB': 65536,
               'iterations': 3, 'parallelism': 4, 'outputLength': 32}
        kf = string(kid) + u32(19) + blob(salt) + u32(65536) + u32(3) + u32(4) + u32(32)
        master = Argon2id(salt=salt, length=32, iterations=3, lanes=4, memory_cost=65536).derive(password)
    for gmac in (False, True):
        mode = 'AES-256-GMAC' if gmac else 'AES-256-GCM'
        encoding = 'utf8' if gmac else 'base64'
        info = string('CryptoScriptSecureVariables/key') + u32(1) + string(mode) + string(kid)
        prk = hmac.digest(bytes(32), master, 'sha256')
        key = hmac.digest(prk, info + b'\x01', 'sha256')
        h = (string('CryptoScriptSecureVariables/auth') + string('CryptoScriptSecureVariables') +
             u32(1) + string(mode) + kf + blob(nonce) + u32(16) + m + string(encoding) + u64(len(payload)))
        result = AESGCM(key).encrypt(nonce, b'' if gmac else payload, h + blob(payload) if gmac else h)
        envelope = {'format': 'CryptoScriptSecureVariables', 'version': 1, 'mode': mode, 'kdf': kdf,
                    'cipher': {'nonce': b64(nonce), 'tagLength': 16}, 'manifest': manifest,
                    'payloadEncoding': encoding, 'payload': payload.decode('utf-8') if gmac else b64(result[:-16]),
                    'tag': b64(result[-16:])}
        vectors.append({'gmac': gmac, 'pbkdf': pbkdf, 'passwordHex': password.hex(), 'saltHex': salt.hex(),
                        'nonceHex': nonce.hex(), 'payloadHex': payload.hex(), 'manifestHex': m.hex(),
                        'headerHex': h.hex(), 'infoHex': info.hex(), 'masterHex': master.hex(),
                        'modeKeyHex': key.hex(), 'envelopeHex': js(envelope).encode('utf-8').hex()})
Path(__file__).with_name('vectors.json').write_text(json.dumps(vectors, indent=2) + '\n', encoding='utf-8')
