#!/usr/bin/env python3
"""Validate portable .NET tool install/update/run/remove using a private local feed."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', required=True, type=Path)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix='sharpai-tool-') as temporary:
        root = Path(temporary)
        feed = root / 'feed'
        tools = root / 'tools'
        elsewhere = root / 'elsewhere'
        elsewhere.mkdir()
        for version in ['4.0.1', '4.0.2']:
            subprocess.run(['dotnet', 'pack', str(ROOT / 'src/SharpAI.Serve'), '-c', 'Release', '-o', str(feed), '-p:Version=' + version, '-v', 'quiet'], check=True)
            package = feed / ('SharpAI.Serve.' + version + '.nupkg')
            assert package.stat().st_size < 250 * 1024 * 1024, 'package exceeds public NuGet size limit'
            with zipfile.ZipFile(package) as archive:
                paths = archive.namelist()
                assert any('runtimes/win-x64/native' in path for path in paths)
                assert any('runtimes/linux-x64/native' in path for path in paths)
                assert any('runtimes/osx-arm64/native' in path for path in paths)
                assert not any('/cuda12/' in path for path in paths)
        config = root / 'NuGet.Config'
        config.write_text('<configuration><packageSources><clear/><add key="local" value="' + str(feed) + '"/></packageSources></configuration>')
        environment = os.environ.copy()
        environment['PATH'] = str(tools) + os.pathsep + environment['PATH']
        for step in [{'action': 'install', 'version': '4.0.1'}, {'action': 'update', 'version': '4.0.2'}]:
            subprocess.run(['dotnet', 'tool', step['action'], 'SharpAI.Serve', '--tool-path', str(tools), '--configfile', str(config), '--version', step['version']], cwd=elsewhere, check=True)
            result = subprocess.run(['sharpai', '--version'], cwd=elsewhere, env=environment, check=True, capture_output=True, text=True)
            assert step['version'] in result.stdout, result.stdout
            subprocess.run(['sharpai', 'serve', '--help'], cwd=elsewhere, env=environment, check=True, stdout=subprocess.DEVNULL)
        executable = tools / ('sharpai.exe' if os.name == 'nt' else 'sharpai')
        subprocess.run(['python' if os.name == 'nt' else 'python3', str(ROOT / 'scripts/test-serve.py'), '--command', str(executable), '--model', str(args.model.resolve())], cwd=elsewhere, env=environment, check=True)
        subprocess.run(['python' if os.name == 'nt' else 'python3', str(ROOT / 'scripts/test-serve-logging.py'), '--command', str(executable), '--model', str(args.model.resolve())], cwd=elsewhere, env=environment, check=True)
        subprocess.run(['dotnet', 'tool', 'uninstall', 'SharpAI.Serve', '--tool-path', str(tools)], cwd=elsewhere, check=True)
        assert not executable.exists(), 'uninstall left the command shim'
    print('PASS: local-feed install, PATH execution, version update, native inference and uninstall')


if __name__ == '__main__':
    main()
