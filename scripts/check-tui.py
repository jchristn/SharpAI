#!/usr/bin/env python3
"""Check terminal catalog/route/localization drift and the inference-free dependency boundary."""
import argparse
import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release', action='store_true')
    parser.add_argument('--results', type=pathlib.Path)
    args = parser.parse_args()
    parity = json.loads((ROOT / 'src/SharpAI.Tui/parity.json').read_text())
    operations = json.loads((ROOT / 'dashboard/src/api/operations.json').read_text())
    for operation in operations:
        assert any(row['method'] == operation['method'] and row['path'] == operation['path'] for row in parity['operations']), operation
    assert len(operations) == len(parity['operations']), 'Operation catalog drift'
    routes = re.findall(r"\{\s*path:\s*'([^']+)'", (ROOT / 'dashboard/src/app/NewApp.tsx').read_text())
    for route in routes:
        assert any(row['path'] == route for row in parity['routes']), f'Unmapped web route: {route}'
    for project in ['src/SharpAI.Tui/SharpAI.Tui.csproj', 'sdk/csharp/src/SharpAI.Client/SharpAI.Client.csproj']:
        tree = ET.parse(ROOT / project)
        for reference in tree.findall('.//ProjectReference'):
            assert not re.search(r'SharpAI(?:\.Server)?[/\\]SharpAI', reference.attrib['Include']), 'Native/server dependency in HTTP-only client'
        for package in tree.findall('.//PackageReference'):
            assert not re.search(r'Llama|SqlClient|Sqlite|Npgsql|MySql', package.attrib['Include'], re.I), package.attrib
    server_config = (ROOT / 'src/SharpAI/Models/GenerationConfiguration.cs').read_text().replace('namespace SharpAI.Models', 'namespace SharpAI.Client')
    assert server_config == (ROOT / 'sdk/csharp/src/SharpAI.Client/GenerationConfiguration.cs').read_text(), 'SDK sampling validation drift'
    for source in list((ROOT / 'src/SharpAI.Tui').rglob('*.cs')) + list((ROOT / 'sdk/csharp/src/SharpAI.Client').rglob('*.cs')):
        if 'obj' in source.parts or 'bin' in source.parts:
            continue
        text = source.read_text()
        assert not re.search(r'\bvar\s+\w+\s*=', text), f'Implicit C# variable in {source}'
        assert '\u2014' not in text, f'Em dash in {source}'
    if args.release:
        assert args.results, 'Release gate requires executable results'
        results = json.loads(args.results.read_text())
        passed = {case['testId'] for case in results if case['success'] and not case['skipped']}
        for workflow in parity['workflows']:
            assert workflow['status'] == 'complete', f'Incomplete workflow: {workflow["id"]}'
            assert workflow.get('testIds') and all(case in passed for case in workflow['testIds']), workflow['id']
    print(f'TUI drift checks passed: {len(routes)} routes, {len(operations)} operations')


if __name__ == '__main__':
    main()
