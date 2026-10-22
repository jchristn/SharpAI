#!/usr/bin/env python3
"""Create a two-shard inference fixture from real GGUF v3 weights, preserving tensor bytes.
Test tooling only: supports ordinary unsharded GGUF files, never converts/quantizes weights.
"""
import argparse
import io
import pathlib
import struct


def split(source, directory):
    data = pathlib.Path(source).read_bytes()
    stream = io.BytesIO(data)
    def read(fmt):
        return struct.unpack('<' + fmt, stream.read(struct.calcsize('<' + fmt)))[0]
    def string():
        return stream.read(read('Q')).decode('utf-8')
    def value(kind):
        formats = {0:'B', 1:'b', 2:'H', 3:'h', 4:'I', 5:'i', 6:'f', 7:'?', 10:'Q', 11:'q', 12:'d'}
        if kind == 8:
            return string()
        if kind == 9:
            element, count = read('I'), read('Q')
            for _ in range(count):
                value(element)
            return None
        return read(formats[kind])
    def encoded(s):
        b = s.encode()
        return struct.pack('<Q', len(b)) + b
    if stream.read(4) != b'GGUF' or read('I') != 3:
        raise ValueError('Expected GGUF v3')
    tensors, metadata = read('Q'), read('Q')
    entries, alignment = [], 32
    for _ in range(metadata):
        start = stream.tell()
        key, kind = string(), read('I')
        result = value(kind)
        if key.startswith('split.'):
            raise ValueError('Fixture must be unsharded')
        if key == 'general.alignment':
            alignment = result
        entries.append(data[start:stream.tell()])
    descriptors = []
    for _ in range(tensors):
        start = stream.tell()
        string()
        dimensions = read('I')
        stream.read(dimensions * 8)
        read('I')
        prefix = data[start:stream.tell()]
        offset = read('Q')
        descriptors.append((prefix, offset))
    base = (stream.tell() + alignment - 1) // alignment * alignment
    offsets = sorted({offset for _, offset in descriptors} | {len(data) - base})
    ends = dict(zip(offsets, offsets[1:]))
    directory = pathlib.Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    if tensors < 2:
        raise ValueError('Need at least two tensors')
    for index, group in enumerate((descriptors[:tensors//2], descriptors[tensors//2:])):
        extra = [encoded('split.no') + struct.pack('<IH', 2, index),
                 encoded('split.count') + struct.pack('<IH', 2, 2),
                 encoded('split.tensors.count') + struct.pack('<Ii', 5, tensors)]
        header = b'GGUF' + struct.pack('<IQQ', 3, len(group), len(entries) + 3) + b''.join(entries + extra)
        payload = bytearray()
        for prefix, offset in group:
            header += prefix + struct.pack('<Q', len(payload))
            payload.extend(data[base + offset:base + ends[offset]])
            payload.extend(b'\0' * (-len(payload) % alignment))
        header += b'\0' * (-len(header) % alignment)
        (directory / f'model-Q4_K_M-{index+1:05}-of-00002.gguf').write_bytes(header + payload)
    return directory / 'model-Q4_K_M-00001-of-00002.gguf'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source')
    parser.add_argument('directory')
    args = parser.parse_args()
    print(split(args.source, args.directory))
