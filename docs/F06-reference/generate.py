"""Independent F06 vectors: CPython hashlib/OpenSSL, no application dependencies."""
import hashlib, json
from pathlib import Path
algorithms = ['sha1', 'sha224', 'sha256', 'sha384', 'sha512', 'sha512_224', 'sha512_256', 'sha3_224', 'sha3_256', 'sha3_384', 'sha3_512']
lengths = [0, 1, 63, 64, 65, 65535, 65536, 65537, 196625]
rows = []
for algorithm in algorithms:
    mechanism = 'HASH-' + algorithm.upper().replace('_', '-')
    for length in lengths:
        data = bytes(i % 256 for i in range(length))
        rows.append(dict(mechanism=mechanism, length=length, digest='0x(' + hashlib.new(algorithm, data).hexdigest().upper() + ')'))
Path(__file__).with_name('vectors.json').write_text(json.dumps(rows, indent=2) + '\n', encoding='utf-8')
