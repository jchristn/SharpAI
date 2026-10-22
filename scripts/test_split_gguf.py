import importlib.util
import pathlib
import struct
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('split_fixture', pathlib.Path(__file__).with_name('split-test-gguf.py'))
fixture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixture)


def string(value):
    data = value.encode()
    return struct.pack('<Q', len(data)) + data


class SplitFixtureTests(unittest.TestCase):
    def test_preserves_tensor_bytes_and_writes_valid_shard_identities(self):
        with tempfile.TemporaryDirectory() as directory:
            source = pathlib.Path(directory) / 'original.gguf'
            metadata = string('general.architecture') + struct.pack('<I', 8) + string('llama')
            header = b'GGUF' + struct.pack('<IQQ', 3, 2, 1) + metadata
            for index in range(2):
                header += string(f'tensor{index}') + struct.pack('<I Q I Q', 1, 8, 0, index * 32)
            header += b'\0' * (-len(header) % 32)
            source.write_bytes(header + b'A' * 32 + b'B' * 32)
            entry = fixture.split(source, pathlib.Path(directory) / 'shards')
            files = sorted(entry.parent.glob('*.gguf'))
            self.assertEqual(2, len(files))
            for index, path in enumerate(files):
                data = path.read_bytes()
                self.assertEqual([1, 4], list(struct.unpack_from('<QQ', data, 8)))
                self.assertIn(string('split.no') + struct.pack('<IH', 2, index), data)
                self.assertIn(string('split.tensors.count') + struct.pack('<Ii', 5, 2), data)
                self.assertTrue(data.endswith(bytes([65 + index]) * 32))
                self.assertEqual(0, len(data) % 32)

    def test_rejects_invalid_fixture(self):
        with tempfile.TemporaryDirectory() as directory:
            source = pathlib.Path(directory) / 'bad.gguf'
            source.write_bytes(b'BAD!')
            with self.assertRaises(ValueError):
                fixture.split(source, pathlib.Path(directory) / 'shards')


if __name__ == '__main__':
    unittest.main()
