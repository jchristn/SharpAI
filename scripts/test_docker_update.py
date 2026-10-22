"""Exercise Docker update preflight without pulling images or changing real containers."""
import json
import os
import pathlib
import shutil
import subprocess
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class DockerUpdateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='sharpai-update-')
        self.addCleanup(self.temporary.cleanup)
        self.directory = pathlib.Path(self.temporary.name)
        shutil.copy2(ROOT / 'docker/update.sh', self.directory / 'update.sh')
        binary = self.directory / 'bin'
        binary.mkdir()
        fake = binary / 'docker'
        fake.write_text('''#!/usr/bin/env python3
import json, os, sys
with open(os.environ['UPDATE_TEST_LOG'], 'a') as log:
    log.write(json.dumps(sys.argv[1:]) + '\\n')
if ' '.join(sys.argv[1:]) == os.environ.get('UPDATE_TEST_FAIL'):
    sys.exit(42)
''')
        fake.chmod(0o755)
        self.log = self.directory / 'calls.jsonl'
        self.environment = dict(os.environ, PATH=str(binary) + os.pathsep + os.environ['PATH'], UPDATE_TEST_LOG=str(self.log))

    def run_update(self, fail=''):
        return subprocess.run(['bash', str(self.directory / 'update.sh')], cwd='/',
            env=dict(self.environment, UPDATE_TEST_FAIL=fail), text=True, capture_output=True)

    def calls(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []

    def test_missing_database_is_initialized_and_commands_keep_order(self):
        result = self.run_update()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue((self.directory / 'sharpai.db').is_file())
        self.assertEqual([['compose', 'pull'], ['compose', 'down'], ['compose', 'up', '-d'], ['ps', '-a']], self.calls())

    def test_existing_database_is_preserved(self):
        database = self.directory / 'sharpai.db'
        database.write_bytes(b'existing database contents')
        self.assertEqual(0, self.run_update().returncode)
        self.assertEqual(b'existing database contents', database.read_bytes())

    def test_directory_rejected_before_docker_and_contents_preserved(self):
        database = self.directory / 'sharpai.db'
        database.mkdir()
        (database / 'keep').write_text('keep')
        result = self.run_update()
        self.assertEqual(1, result.returncode)
        self.assertIn('SQLite requires a file', result.stderr)
        self.assertEqual([], self.calls())
        self.assertEqual('keep', (database / 'keep').read_text())

    def test_failed_pull_never_stops_services(self):
        self.assertEqual(42, self.run_update('compose pull').returncode)
        self.assertEqual([['compose', 'pull']], self.calls())


if __name__ == '__main__':
    unittest.main()
