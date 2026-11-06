"""Reproduce F03.2 fixtures without importing CryptoScript or Bouncy Castle.
Requires Python cryptography (generated with 50.0.2 / OpenSSL backend).
Usage: python generate.py path/to/gcmtestvectors.zip
The NIST ZIP is obtained from the URL recorded below; expected NIST tags are copied,
not calculated. All other expected tags are independently calculated here.
"""
from pathlib import Path
import hashlib, json, sys, zipfile
import cryptography
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES

ROOT = Path(__file__).resolve().parent
NIST = 'https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Algorithm-Validation-Program/documents/mac/gcmtestvectors.zip'

def encrypt(algorithm, mode, data, decrypt=False):
    ctx = Cipher(algorithm, mode).decryptor() if decrypt else Cipher(algorithm, mode).encryptor()
    return ctx.update(data) + ctx.finalize()

def pad(data, size, name):
    n = size - len(data) % size
    if name == 'NONE':
        assert data and len(data) % size == 0
        return data
    if name == 'PKCS-7': return data + bytes([n]) * n
    if name == 'ANSI-X923': return data + bytes(n-1) + bytes([n])
    if name == 'TLS-CBC': return data + bytes([n-1]) * n
    if name in ('ISO-7816', 'ISO-9797-M2'): return data + b'\x80' + bytes(n-1)
    zeros = bytes(size if not data else (-len(data)) % size)
    if name == 'ISO-9797-M1': return data + zeros
    if name == 'ISO-9797-M3': return (len(data)*8).to_bytes(size, 'big') + data + zeros
    raise ValueError(name)

def mac(mechanism, key, data, padding, nonce):
    if mechanism == 'AES-GMAC': return AESGCM(key).encrypt(nonce, b'', data)
    size = 16 if mechanism.startswith('AES') else 8
    data = pad(data, size, padding)
    if mechanism == 'DES3-RETAIL':
        # TripleDES(K,K,K) = single DES(K); avoids deprecated short-key constructors.
        c = encrypt(TripleDES(key[:8]*3), modes.CBC(bytes(8)), data)[-8:]
        c = encrypt(TripleDES(key[8:16]*3), modes.ECB(), c, decrypt=True)
        k3 = key[:8] if len(key) == 16 else key[16:24]
        return encrypt(TripleDES(k3*3), modes.ECB(), c)
    cipher = algorithms.AES(key) if size == 16 else TripleDES(key if len(key)==24 else key+key[:8])
    return encrypt(cipher, modes.CBC(bytes(size)), data)[-size:]

vectors=[]
inputs={'empty.bin':b'', 'aligned.bin':bytes(range(32)),
        'records.bin':b'\x00POS\r\nHOST\n\x1e\xff\x00'+bytes(range(256))}
for name,data in inputs.items(): (ROOT/name).write_bytes(data)
paddings=['NONE','PKCS-7','ANSI-X923','ISO-7816','ISO-9797-M1','ISO-9797-M2','ISO-9797-M3','TLS-CBC']
for mechanism in ['AES-GMAC','AES-CBC-MAC','DES3-CBC','DES3-RETAIL']:
    for keylen in ([16,24,32] if mechanism.startswith('AES') else [16,24]):
        key=bytes(range(keylen))
        for input_index,(filename,data) in enumerate(inputs.items()):
            nonce=(input_index+1).to_bytes(12,'big') if mechanism=='AES-GMAC' else b''
            pads=[''] if mechanism=='AES-GMAC' else ['ISO-9797-M1','ISO-9797-M2'] if mechanism=='DES3-RETAIL' else paddings
            for padding in pads:
                size=16 if mechanism.startswith('AES') else 8
                if padding=='NONE' and (not data or len(data)%size): continue
                result=mac(mechanism,key,data,padding,nonce)
                for length in ([16] if mechanism=='AES-GMAC' else [8,16] if size==16 else [4,8]):
                    vectors.append(dict(id=f'{mechanism}-{keylen}-{filename}-{padding}-{length}',
                        provenance='independently-calculated', source='generate.py; Python cryptography '+cryptography.__version__+' / OpenSSL',
                        mechanism=mechanism,key=key.hex(),input=data.hex(),file=filename,padding=padding,
                        iv='' if nonce else bytes(size).hex(),nonce=nonce.hex(),macLength=length,expected=result[:length].hex()))

if len(sys.argv)!=2: raise SystemExit('Pass the official NIST gcmtestvectors.zip archive.')
archive=Path(sys.argv[1])
with zipfile.ZipFile(archive) as z:
    for bits in (128,192,256):
        name=f'gcmEncryptExtIV{bits}.rsp'
        metadata={}; fields={}
        for line in z.read(name).decode().splitlines():
            line=line.strip()
            if line.startswith('['):
                k,v=line[1:-1].split(' = '); metadata[k]=int(v)
            elif ' = ' in line:
                k,v=line.split(' = ',1); fields[k]=v
            elif line.endswith(' ='): fields[line[:-2]]=''
            if line.startswith('Tag ='):
                if metadata.get('PTlen')==0 and metadata.get('IVlen')==96 and metadata.get('Taglen')==128 and metadata.get('AADlen') in (0,128) and fields.get('Count')=='0':
                    data=bytes.fromhex(fields.get('AAD','')); key=bytes.fromhex(fields['Key']); nonce=bytes.fromhex(fields['IV'])
                    assert AESGCM(key).encrypt(nonce,b'',data).hex()==fields['Tag']
                    filename=f'nist-{bits}-aad{metadata["AADlen"]}.bin'; (ROOT/filename).write_bytes(data)
                    vectors.append(dict(id=filename[:-4],provenance='official-NIST-CAVP',source=f'{NIST}; {name}; {metadata}; Count=0',
                        mechanism='AES-GMAC',key=key.hex(),input=data.hex(),file=filename,padding='',iv='',nonce=nonce.hex(),macLength=16,expected=fields['Tag']))
                fields={}
assert sum(v['provenance']=='official-NIST-CAVP' for v in vectors)==6
(ROOT/'vectors.json').write_text(json.dumps(vectors,indent=2)+'\n',encoding='utf-8')
(ROOT/'manifest.json').write_text(json.dumps({'nistArchiveSha256':hashlib.sha256(archive.read_bytes()).hexdigest(),
    'files':{p.name:dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(ROOT.glob('*.bin'))}},indent=2)+'\n',encoding='utf-8')
print(f'{len(vectors)} vectors, including 6 official NIST CAVP cases')
