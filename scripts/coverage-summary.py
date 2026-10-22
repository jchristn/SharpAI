#!/usr/bin/env python3
"""Union instrumented SharpAI core source lines across Coverlet runs.

Branch coverage uses the highest observed covered-branch count per source line,
not an assumed union of opaque branch identifiers; it is a conservative bound.
Test assemblies and external packages are excluded. Server HTTP checks execute
out of process and are not part of this instrumented core coverage figure.
"""
import argparse
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


def summarize(paths):
    files = {}
    for path in paths:
        root = ET.parse(path).getroot()
        for package in root.findall('./packages/package'):
            if package.get('name') != 'SharpAI':
                continue
            for cls in package.findall('./classes/class'):
                filename = cls.get('filename', '').replace('\\', '/')
                marker = 'src/SharpAI/'
                filename = filename.split(marker)[-1] if marker in filename else filename.removeprefix('SharpAI/')
                lines = files.setdefault(filename, {})
                for line in cls.findall('./lines/line'):
                    number = int(line.get('number'))
                    hit = int(line.get('hits', '0')) > 0
                    match = re.search(r'\((\d+)/(\d+)\)', line.get('condition-coverage', ''))
                    covered, total = map(int, match.groups()) if match else (0, 0)
                    previous = lines.get(number, (False, 0, 0))
                    lines[number] = (previous[0] or hit, max(previous[1], covered), max(previous[2], total))

    def counts(selected):
        values = [value for name, lines in files.items() if selected(name) for value in lines.values()]
        hit = sum(value[0] for value in values)
        branches = sum(value[2] for value in values)
        covered = sum(value[1] for value in values)
        return {'lines_covered': hit, 'lines_total': len(values),
                'line_percent': round(100 * hit / len(values), 2) if values else 0,
                'branches_covered_lower_bound': covered, 'branches_total': branches,
                'branch_percent_lower_bound': round(100 * covered / branches, 2) if branches else 100}

    return {'scope': 'SharpAI core; excludes server process, SDKs, dashboard, and test assemblies',
            'reports': len(paths), 'core': counts(lambda name: True),
            'operations': counts(lambda name: name.startswith('Operations/') and not name.endswith('AssemblyInfo.cs')),
            'files': {name: counts(lambda candidate, name=name: candidate == name) for name in sorted(files)}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--minimum-operations-line', type=float, default=0)
    parser.add_argument('--minimum-operations-branch', type=float, default=0)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    paths = sorted(args.directory.rglob('coverage.cobertura.xml'))
    if not paths:
        parser.error('No coverage.cobertura.xml reports found')
    result = summarize(paths)
    rendered = json.dumps(result, indent=2)
    if args.output:
        args.output.write_text(rendered + '\n')
    print(json.dumps({key: value for key, value in result.items() if key != 'files'}, indent=2))
    return 0 if (result['operations']['line_percent'] >= args.minimum_operations_line and
                 result['operations']['branch_percent_lower_bound'] >= args.minimum_operations_branch) else 1


if __name__ == '__main__':
    sys.exit(main())
