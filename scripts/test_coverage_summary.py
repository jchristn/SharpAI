import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('coverage_summary', Path(__file__).with_name('coverage-summary.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CoverageSummaryTests(unittest.TestCase):
    def report(self, path, filename, hits, condition='50% (1/2)', package='SharpAI'):
        path.write_text(f'''<coverage><packages><package name="{package}"><classes>
          <class filename="{filename}"><lines><line number="12" hits="{hits}" condition-coverage="{condition}" /></lines></class>
          </classes></package></packages></coverage>''')

    def test_empty_is_not_full_coverage(self):
        result = module.summarize([])
        self.assertEqual(0, result['core']['line_percent'])
        self.assertEqual(0, result['operations']['line_percent'])

    def test_union_hits_without_inventing_branch_coverage(self):
        with tempfile.TemporaryDirectory() as directory:
            a = Path(directory) / 'a.xml'
            b = Path(directory) / 'b.xml'
            self.report(a, 'SharpAI/Operations/Transfer.cs', 0)
            self.report(b, '/workspace/src/SharpAI/Operations/Transfer.cs', 1)
            result = module.summarize([a, b])
            self.assertEqual(1, result['core']['lines_total'])
            self.assertEqual(100, result['operations']['line_percent'])
            self.assertEqual(50, result['operations']['branch_percent_lower_bound'])

    def test_test_assemblies_are_excluded(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'a.xml'
            self.report(path, 'SharpAI/Operations/Transfer.cs', 1, package='Test.Shared')
            self.assertEqual(0, module.summarize([path])['core']['lines_total'])

    def test_branch_count_uses_best_observed_bound(self):
        with tempfile.TemporaryDirectory() as directory:
            a = Path(directory) / 'a.xml'
            b = Path(directory) / 'b.xml'
            self.report(a, 'SharpAI/Operations/Transfer.cs', 1)
            self.report(b, 'SharpAI/Operations/Transfer.cs', 1, '100% (2/2)')
            self.assertEqual(100, module.summarize([a, b])['operations']['branch_percent_lower_bound'])


if __name__ == '__main__':
    unittest.main()
