#!/usr/bin/env python3
"""Verify standalone Metal offload and overrides in fresh library-only .NET processes.

Requires native Apple Silicon, a built solution, and an existing GGUF model. No downloads,
server startup, or changes to the local deployment are performed.
"""
import argparse
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', required=True, help='Existing GGUF model used for actual offload and inference checks')
    parser.add_argument('--output', help='Evidence directory; defaults to a new temporary directory')
    parser.add_argument('--framework', action='append', choices=['net8.0', 'net10.0'], help='Framework to check; defaults to both')
    args = parser.parse_args()
    if sys.platform != 'darwin' or platform.machine().lower() != 'arm64':
        parser.error('Metal checks require native macOS on Apple Silicon')
    model = Path(args.model).resolve()
    if not model.is_file():
        parser.error('The supplied GGUF model does not exist')
    output = Path(args.output or tempfile.mkdtemp(prefix='sharpai-metal-')).resolve()
    output.mkdir(parents=True, exist_ok=True)
    cases = [
        {'name': 'automatic', 'backend': 'metal', 'layers': 999, 'model': True},
        {'name': 'force-cpu', 'backend': 'cpu', 'layers': 0, 'env': {'SHARPAI_FORCE_BACKEND': 'cpu'}, 'model': True},
        {'name': 'zero-layers', 'backend': 'metal', 'layers': 0, 'env': {'SHARPAI_GPU_LAYERS': '0'}, 'model': True},
        {'name': 'three-layers', 'backend': 'metal', 'layers': 3, 'env': {'SHARPAI_GPU_LAYERS': '3'}, 'model': True},
        {'name': 'all-layers', 'backend': 'metal', 'layers': 999, 'env': {'SHARPAI_GPU_LAYERS': '-1'}},
        {'name': 'explicit-cpu', 'backend': 'cpu', 'layers': 0, 'env': {'SHARPAI_FORCE_BACKEND': 'metal'}, 'scenario': '--explicit-cpu'},
        {'name': 'missing-metal-fallback', 'backend': 'cpu', 'layers': 0, 'scenario': '--missing-metal'},
        {'name': 'required-metal-failure', 'backend': 'cpu', 'layers': 0, 'scenario': '--missing-metal',
         'env': {'SHARPAI_REQUIRE_BACKEND': 'true'}, 'fails': True},
    ]
    checks = 0
    for framework in args.framework or ['net8.0', 'net10.0']:
        worker = ROOT / 'src/Test.Automated/bin/Debug' / framework / 'Test.Automated.dll'
        if not worker.is_file():
            parser.error(f'Build src/SharpAI.sln first; worker is missing: {worker}')
        for case in cases:
            env = {key: value for key, value in os.environ.items() if not key.startswith('SHARPAI_')}
            env.update({'SHARPAI_CONTEXT_SIZE': '128', 'SHARPAI_ENABLE_NATIVE_LOGGING': 'true',
                        'SHARPAI_NUM_THREADS': '2', 'SHARPAI_BATCH_THREADS': '2'})
            env.update(case.get('env', {}))
            command = ['dotnet', str(worker), '--native-backend-probe', case['backend'], str(case['layers'])]
            if case.get('model'):
                command.append(str(model))
            if case.get('scenario'):
                command.append(case['scenario'])
            result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=60)
            text = result.stdout + result.stderr
            log = output / f'{framework}-{case["name"]}.log'
            log.write_text(text)
            if case.get('fails'):
                assert result.returncode != 0 and 'FileNotFoundException' in text, f'Expected required-backend failure: {log}'
            else:
                assert result.returncode == 0, f'Standalone backend probe failed: {log}'
                receipt = next(json.loads(line) for line in result.stdout.splitlines() if line.startswith('{"backend":'))
                assert receipt['backend'] == case['backend'] and receipt['gpu_layers'] == case['layers'], log
                if case.get('model'):
                    offload = re.search(r'offloaded (\d+)/(\d+) layers to GPU', text)
                    assert offload, f'Native offload evidence missing: {log}'
                    actual = int(offload[1])
                    total = int(offload[2])
                    expected = total if case['layers'] == 999 else min(case['layers'], total)
                    assert total > 0 and actual == expected, f'Unexpected actual layer offload: {log}'
            checks += 1
            print(f'PASS {framework} {case["name"]}', flush=True)
    print(f'{checks} standalone backend scenarios passed; evidence: {output}')


if __name__ == '__main__':
    main()
