"""Independent CWE vectors: CPython + cryptography/OpenSSL; no product imports.
Run: python docs/F05-reference/generate.py (cryptography 50.0.2).
ISO-10126 uses recorded 0xa5 filler: valid independent decryption fixtures.
"""
import base64
import json
import struct
from pathlib import Path
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.ciphers.aead import AESGCM, AESCCM
from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES

PADS = ['NONE', 'PKCS-7', 'ANSI-X923', 'ISO-10126', 'ISO-7816',
        'ISO-9797-M1', 'ISO-9797-M2', 'ISO-9797-M3', 'TLS-CBC']
b64 = lambda b: base64.b64encode(b).decode('ascii')

def pad(data, name, block):
    n = block - len(data) % block
    if name == 'NONE': return data
    if name == 'PKCS-7': return data + bytes([n]) * n
    if name == 'ANSI-X923': return data + bytes(n-1) + bytes([n])
    if name == 'ISO-10126': return data + bytes([0xa5]) * (n-1) + bytes([n])
    if name in ('ISO-7816', 'ISO-9797-M2'): return data + b'\x80' + bytes(n-1)
    if name == 'TLS-CBC': return data + bytes([n-1]) * n
    zero = data + bytes((-len(data)) % block) if data else bytes(block)
    if name == 'ISO-9797-M1': return zero
    return (len(data)*8).to_bytes(block, 'big') + zero

vectors = []
for mech in ['AES-CBC', 'AES-ECB', 'AES-CTR', 'AES-GCM', 'AES-CCM', 'DES3-CBC', 'DES3-ECB']:
    des = mech.startswith('DES3')
    block = 8 if des else 16
    aead = mech in ('AES-GCM', 'AES-CCM')
    for size in ([16, 24] if des else [16, 24, 32]):
        key = bytes(range(size))
        for padding in (PADS if mech.endswith('CBC') or des else ['NONE']):
            for length in sorted(set([0, 1, block-1, block, block+1, block*2, block*3+5])):
                if padding == 'NONE' and (mech.endswith('CBC') or mech.endswith('ECB')):
                    if length % block or length == 0 and mech != 'AES-ECB': continue
                data = bytes((i*37) % 256 for i in range(length))
                if data: data = data[:-1] + b'\0'
                iv = bytes(range(block)) if mech.endswith('CBC') else b''
                nonce = bytes(range(12))
                counter = bytes.fromhex('000001ff')
                metadata = dict(format='CryptoWorkBenchEncryptedFile', mechanism=mech,
                    padding=padding, iv=b64(iv), payloadEncoding='binary',
                    authentication=mech[4:] if aead else 'none', tagLength=16 if aead else 0)
                if aead or mech == 'AES-CTR': metadata['nonce'] = b64(nonce)
                if mech == 'AES-CTR':
                    metadata['counter'] = b64(counter)
                    metadata['counterEncoding'] = 'uint32-be'
                if aead: metadata['associatedData'] = b64(b'\0\xffF05\0')
                if padding == 'ISO-9797-M1': metadata['plaintextLength'] = str(length)
                encoded = json.dumps(metadata, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
                frame = b'CWBENC\0\0' + struct.pack('>HHI', 1, 0, len(encoded)) + encoded
                padded = data if aead or mech == 'AES-CTR' else pad(data, padding, block)
                if aead:
                    cipher = (AESGCM(key) if mech == 'AES-GCM' else AESCCM(key, tag_length=16)).encrypt(nonce, data, frame)
                else:
                    mode = modes.CBC(iv) if mech.endswith('CBC') else modes.CTR(nonce+counter) if mech == 'AES-CTR' else modes.ECB()
                    encryptor = Cipher(TripleDES(key) if des else algorithms.AES(key), mode).encryptor()
                    cipher = encryptor.update(padded) + encryptor.finalize()
                vectors.append(dict(mechanism=mech, padding=padding, key=key.hex(), plaintext=data.hex(),
                    padded=padded.hex(), frame=frame.hex(), payload=cipher.hex()))
Path(__file__).with_name('vectors.json').write_text(json.dumps(vectors, indent=2)+'\n', encoding='utf-8')
print(f'{len(vectors)} independently generated vectors')
