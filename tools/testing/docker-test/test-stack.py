#!/usr/bin/env python3
"""Local-only synthetic Coiny deployment. No real database mounts or cloud resources."""
import uuid
import argparse, json, os, pathlib, secrets, subprocess, time, urllib.request, urllib.error
ROOT = pathlib.Path(__file__).resolve().parent
URL = 'http://127.0.0.1:6080'

def compose(*args, hybrid=False):
    files = ['--file', str(ROOT/'compose.yaml')]
    if hybrid or (ROOT/'.hybrid-enabled').exists(): files += ['--file', str(ROOT/'compose.hybrid.yaml')]
    subprocess.run(['docker', 'compose', '--project-name', 'coiny-test', *files, *args], cwd=ROOT, check=True)

def init():
    path = ROOT/'.env'
    if path.exists():
        print('Keeping existing test credentials.'); return
    fd = os.open(path, os.O_WRONLY|os.O_CREAT|os.O_EXCL, 0o600)
    with os.fdopen(fd, 'w') as output:
        for key in ['TEST_POSTGRES_PASSWORD', 'TEST_JWT_KEY']:
            output.write(f'{key}={secrets.token_hex(32)}\n')
    print('Created private test-only configuration.')

def request(path, body=None, token=None, expected=200):
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer '+token
    req = urllib.request.Request(URL+path, data=None if body is None else json.dumps(body).encode(), headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as response: status, raw = response.status, response.read()
    except urllib.error.HTTPError as error: status, raw = error.code, error.read()
    assert status == expected, f'{path}: expected {expected}, got {status}'
    return raw

def smoke():
    deadline = time.monotonic()+120
    while True:
        try:
            request('/api/auth/recovery-options'); break
        except (OSError, AssertionError):
            if time.monotonic() > deadline: raise RuntimeError('Coiny test API did not become ready')
            time.sleep(1)
    assert b'<app-root' in request('/'), 'Angular entry point missing'
    assert b'<app-root' in request('/settings'), 'SPA deep link failed'
    request('/api/does-not-exist', expected=404)
    login = json.loads(request('/api/auth/login', {'email':'alex@coiny.test', 'password':'CoinyDemo!2026'}))
    token = login.get('token') or login.get('accessToken')
    assert token, 'Login returned no token'
    accounts = json.loads(request('/api/accounts', token=token))
    assert accounts, 'Synthetic demo accounts missing'
    request('/api/auth/logout', {}, token=token, expected=204)
    request('/api/accounts', token=token, expected=401)
    print('PASS: UI, deep links, API routing, demo login/accounts and session revocation.')

def maintenance(action):
    if not (ROOT/'.hybrid-enabled').exists():
        raise RuntimeError('Maintenance requires the verified hybrid test deployment.')
    command = ('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'api', 'Coiny.API.dll', '--maintenance')
    marker = ROOT/'.maintenance-operation'
    if action == 'status':
        compose(*command, 'status'); return
    if action == 'pause':
        if not marker.exists():
            fd = os.open(marker, os.O_WRONLY|os.O_CREAT|os.O_EXCL, 0o600)
            with os.fdopen(fd, 'w') as output: output.write(str(uuid.uuid4()))
        operation = str(uuid.UUID(marker.read_text().strip()))
        compose(*command, 'pause', operation)
        compose('stop', 'api')
        print('Test API stopped after drain. Pause retained; use maintenance-resume when finished.')
    elif action == 'resume':
        operation = str(uuid.UUID(marker.read_text().strip()))
        compose(*command, 'resume', operation)
        compose('up', '--detach', 'api'); smoke(); marker.unlink()
    elif action == 'smoke':
        maintenance('pause')
        compose('up', '--detach', 'api')
        deadline = time.monotonic()+60
        while True:
            try:
                raw = request('/api/auth/recovery-options', expected=503)
                assert b'maintenance' in raw
                break
            except (OSError, AssertionError):
                if time.monotonic() > deadline:
                    raise RuntimeError('Restarted API did not retain maintenance; inspect status before resuming.')
                time.sleep(1)
        maintenance('resume')
        print('PASS: pause/drain, API stop, paused restart, explicit resume and normal smoke.')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('action', choices=['init','up','smoke','status','logs','down','integration','migrate-hybrid','maintenance-status','maintenance-pause','maintenance-resume','maintenance-smoke'])
    action = parser.parse_args().action
    if action.startswith('maintenance-'): maintenance(action.removeprefix('maintenance-'))
    elif action == 'init': init()
    elif action == 'up': init(); compose('up','--build','--detach'); smoke()
    elif action == 'migrate-hybrid':
        init(); compose('build', 'api'); compose('up', '--detach', 'postgres'); compose('stop', 'api')
        try:
            compose('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'api', 'Coiny.API.dll', '--migrate-docker-test', hybrid=True)
        except BaseException:
            print('Migration did not complete. API remains stopped; original data and snapshot are retained. Resolve the error and retry migration before accepting more writes.')
            raise
        (ROOT/'.hybrid-enabled').write_text('Verified local Docker test migration; preserve split storage on restart.\n')
        compose('up', '--detach', '--force-recreate', 'api'); smoke()
    elif action == 'integration':
        init(); compose('up', '--detach', 'postgres'); compose('run', '--build', '--rm', 'integration')
    elif action == 'smoke': smoke()
    elif action == 'status': compose('ps')
    elif action == 'logs': compose('logs','--tail','60')
    elif action == 'down': compose('down')  # Deliberately retains all named volumes.
