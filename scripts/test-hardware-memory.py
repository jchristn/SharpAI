#!/usr/bin/env python3
"""Measure native model memory at multiple contexts/slot counts. GPU runs require proven native offload."""
import argparse
import concurrent.futures
import json
import os
import pathlib
import re
import socket
import subprocess
import tempfile

import importlib.util

spec = importlib.util.spec_from_file_location('observability', pathlib.Path(__file__).with_name('test-observability.py'))
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', required=True)
    parser.add_argument('--backend', choices=['cpu', 'metal', 'cuda'], default='cpu')
    parser.add_argument('--output', required=True)
    parser.add_argument('--allowance-mb', type=int, default=128, help='Allocator/JIT overhead allowed beyond the model estimate')
    args = parser.parse_args()
    output = pathlib.Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    evidence = []
    for context, slots in [(128, 1), (256, 1), (128, 3), (256, 3)]:
        with tempfile.TemporaryDirectory(prefix='sharpai-memory-') as temp:
            runtime = pathlib.Path(temp)
            with socket.socket() as sock:
                sock.bind(('127.0.0.1', 0))
                port = sock.getsockname()[1]
            base = f'http://127.0.0.1:{port}'
            settings = json.loads((helpers.ROOT / 'docker/sharpai.json').read_text())
            settings['Rest'].update(Hostname='127.0.0.1', Port=port)
            settings['Auth'] = {'Enabled': False}
            settings['Telemetry'] = {'Enable': False}
            (runtime / 'sharpai.json').write_text(json.dumps(settings))
            env = {k:v for k,v in os.environ.items() if not k.startswith('SHARPAI_TELEMETRY_')}
            env.update(SHARPAI_FORCE_BACKEND=args.backend, SHARPAI_REQUIRE_BACKEND='true', SHARPAI_CONTEXT_SIZE=str(context),
                SHARPAI_MAX_CONCURRENT_GENERATIONS=str(slots), SHARPAI_ENABLE_NATIVE_LOGGING='true',
                SHARPAI_NUM_THREADS=os.environ.get('SHARPAI_TEST_THREADS', '2'), SHARPAI_BATCH_THREADS=os.environ.get('SHARPAI_TEST_THREADS', '2'))
            log_path = output / f'{args.backend}-context{context}-slots{slots}.log'
            with log_path.open('w') as log:
                server = subprocess.Popen(['dotnet', str(helpers.ROOT / 'src/SharpAI.Server/bin/Debug/net8.0/SharpAI.Server.dll')],
                    cwd=runtime, env=env, stdout=log, stderr=subprocess.STDOUT)
                try:
                    helpers.eventually(lambda: helpers.request(base + '/health'), 'server')
                    helpers.request(base + '/api/import', {'path': str(pathlib.Path(args.model).resolve()), 'name': 'ops-story'})
                    baseline = helpers.request(base + '/api/diagnostics/memory')['working_set_bytes']
                    observed = []
                    def generate(_):
                        response = helpers.request(base + '/v1/completions', {'model':'ops-story', 'prompt':'Once upon a time', 'max_tokens':16, 'temperature':0}, timeout=60)
                        assert response['usage']['completion_tokens'] == 16
                        return response
                    # Warm the model before testing concurrent slot contexts.
                    generate(0)
                    import threading
                    errors = []
                    stop = threading.Event()
                    def sample():
                        while not stop.wait(0.025):
                            try:
                                observed.append(helpers.request(base + '/api/diagnostics/memory')['working_set_bytes'])
                            except Exception as error:
                                errors.append(str(error))
                                return
                    sampler = threading.Thread(target=sample)
                    sampler.start()
                    try:
                        with concurrent.futures.ThreadPoolExecutor(max_workers=slots) as pool:
                            list(pool.map(generate, range(slots * 4)))
                    finally:
                        stop.set()
                        sampler.join(timeout=15)
                    assert not errors and observed, f'Memory sampling failed: {errors}'
                    memory = helpers.request(base + '/api/diagnostics/memory')
                    runtime_info = helpers.request(base + '/api/diagnostics/runtime')
                    assert runtime_info['backend_selected'] == args.backend
                    estimate = memory['models'][0]
                    assert estimate['EffectiveContext'] == context and estimate['Slots'] == slots
                    peak = max(observed + [memory['working_set_bytes']])
                    delta = max(0, peak - baseline)
                    assert delta <= estimate['HostBytes'] + args.allowance_mb * 1024 * 1024, (delta, estimate)
                    item = {'backend':args.backend, 'context':context, 'slots':slots, 'baseline_working_set':baseline,
                        'sampled_peak_working_set':peak, 'observed_process_delta':delta, 'estimate':estimate,
                        'allowance_mb':args.allowance_mb, 'sample_count':len(observed),
                        'measurement_note':'Whole-process working-set delta; includes JIT/allocator overhead, not per-model allocation attribution.'}
                    if args.backend != 'cpu':
                        text = log_path.read_text(errors='replace')
                        assert re.search(r'offloaded [1-9][0-9]*/[0-9]+ layers to GPU', text), 'No native evidence of GPU offload'
                        item['offload_verified'] = True
                    if args.backend == 'cuda':
                        raw = subprocess.check_output(['nvidia-smi', '--query-compute-apps=pid,used_memory', '--format=csv,noheader,nounits'], text=True)
                        device_mb = sum(int(row.split(',')[1].strip()) for row in raw.splitlines() if row.split(',')[0].strip() == str(server.pid))
                        assert device_mb > 0, 'nvidia-smi did not observe allocations for the native server process'
                        item['observed_device_bytes'] = device_mb * 1024 * 1024
                        assert item['observed_device_bytes'] <= estimate['DeviceBytes'] + args.allowance_mb * 1024 * 1024
                    evidence.append(item)
                    print(f'PASS: {args.backend} context={context} slots={slots} process delta={delta} estimate={estimate["HostBytes"]}', flush=True)
                finally:
                    server.terminate()
                    try:
                        server.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        server.kill()
                        server.wait()
                    (output / 'memory-matrix.json').write_text(json.dumps(evidence, indent=2))


if __name__ == '__main__':
    main()
