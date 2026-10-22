#!/usr/bin/env python3
"""Run the model-pull contract suites over the checked Postman simulation on assigned ports."""
import argparse
import json
import os
import pathlib
import shutil
import signal
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'artifacts/tui/downloads')
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    executable = shutil.which('postman') or shutil.which('postman.cmd') or 'postman'
    with (output / 'simulation.ndjson').open('w') as log, (output / 'simulation.err').open('w') as error:
        process = subprocess.Popen([executable, 'simulation', 'run', str(ROOT / 'postman/simulations/downloads.sim.yaml'), '--port', 'auto', '--output', 'ndjson'], stdout=log, stderr=error, start_new_session=os.name != 'nt')
        try:
            deadline = time.monotonic() + 30
            base = None
            while base is None and time.monotonic() < deadline:
                for line in (output / 'simulation.ndjson').read_text().splitlines():
                    try:
                        event = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if event.get('event') == 'listening':
                        base = event['url'].rstrip('/') + '/hf'
                if process.poll() is not None:
                    raise RuntimeError('Download simulation exited; see simulation.err')
                if base is None:
                    time.sleep(0.1)
            if base is None:
                raise RuntimeError('Download simulation did not become ready')
            for framework in ['net8.0', 'net10.0']:
                with (output / (framework + '.log')).open('w') as results:
                    subprocess.run(['dotnet', 'test', 'src/Test.Xunit', '-f', framework, '--filter', 'FullyQualifiedName~DownloadSimulationTests'], cwd=ROOT, env={**os.environ, 'SHARPAI_TEST_HF_URL': base}, stdout=results, stderr=subprocess.STDOUT, check=True)
        finally:
            if os.name == 'nt':
                subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True, check=False)
            else:
                try:
                    os.killpg(process.pid, signal.SIGINT)
                except ProcessLookupError:
                    pass
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)


if __name__ == '__main__':
    main()
