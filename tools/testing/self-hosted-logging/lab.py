#!/usr/bin/env python3
"""Isolated synthetic self-hosting lab. Never mounts an existing installation."""
import argparse
import json
import os
import pathlib
import secrets
import ssl
import subprocess
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent
SOURCE = ROOT.parents[2]
STATE = ROOT / '.runtime'
PROJECT = 'coiny-selfhost-logging-lab'
URL = 'https://localhost:6443'
os.umask(0o077)


def private_write(path, data):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, 'w') as stream:
        stream.write(data)


def init():
    STATE.mkdir(mode=0o700, exist_ok=True)
    if not (ROOT / '.env').exists():
        values = dict(COINY_HOST='localhost', ACME_EMAIL='lab@example.test',
                      POSTGRES_PASSWORD=secrets.token_hex(32), JWT_KEY=secrets.token_hex(32),
                      COINY_BIND_IP='127.0.0.1', COINY_HTTP_PORT='6084', COINY_HTTPS_PORT='6443',
                      COINY_VERSION='selfhost-logging-lab', COINY_PROXY_SUBNET='172.30.66.0/24',
                      COINY_PROXY_IP='172.30.66.3', COINY_SOURCE_ROOT=str(SOURCE))
        assert all("'" not in value and '\n' not in value for value in values.values())
        private_write(ROOT / '.env', ''.join(f"{key}='{value}'\n" for key, value in values.items()))
    print('Lab configuration ready; existing credentials preserved.', flush=True)


def environment():
    values = dict(os.environ)
    for line in (ROOT / '.env').read_text().splitlines():
        if '=' in line and not line.startswith('#'):
            key, value = line.split('=', 1)
            values[key] = value.strip().strip("'")
    values['COINY_SOURCE_ROOT'] = str(SOURCE)
    return values


def run(args, stdin=None):
    result = subprocess.run(args, input=stdin, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=environment())
    if result.returncode:
        private_write(STATE / 'last-error.txt', result.stdout.decode(errors='replace') + result.stderr.decode(errors='replace'))
        raise RuntimeError('Lab step failed. Private details: ' + str(STATE / 'last-error.txt'))
    return result.stdout


def compose(*args):
    return run(['docker', 'compose', '--env-file', str(ROOT / '.env'), '-p', PROJECT,
                '-f', str(SOURCE / 'Docker/compose.yaml'), '-f', str(ROOT / 'compose.lab.yaml'), *args])


def certificate():
    compose('cp', 'web:/data/caddy/pki/authorities/local/root.crt', str(STATE / 'root.crt'))
    return ssl.create_default_context(cafile=str(STATE / 'root.crt'))


def request(path, body=None, token=None, expected=200):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(URL + path, data=None if body is None else json.dumps(body).encode(), headers=headers)
    try:
        response = urllib.request.urlopen(req, context=ssl.create_default_context(cafile=str(STATE / 'root.crt')), timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    assert response.status == expected, f'{path.split("?")[0]}: unexpected HTTP status {response.status}'
    return response.read(), response.headers


def up():
    init()
    compose('build', 'api', 'web')
    compose('up', '-d', '--no-build', '--wait', 'postgres')
    if not (STATE / 'initialized').exists():
        # The application refuses pre-existing storage; never delete volumes to retry.
        compose('run', '--rm', '--no-deps', '--user', '0:0', '--entrypoint', 'sh', 'api', '/app/initialize.sh')
        private_write(STATE / 'initialized', 'Initialized empty lab storage.\n')
    compose('up', '-d', '--no-build', '--wait', 'api', 'web')
    for _ in range(60):
        try:
            certificate()
            request('/api/auth/recovery-options')
            break
        except (OSError, RuntimeError, AssertionError):
            time.sleep(1)
    else:
        raise RuntimeError('Lab HTTPS/API did not become ready; inspect lab logs.')
    print('Isolated self-hosting lab ready: ' + URL, flush=True)


def smoke():
    assert (STATE / 'initialized').exists(), 'Run lab.py up first.'
    certificate()
    request('/')
    credentials = STATE / 'smoke-credentials.json'
    if credentials.exists():
        user = json.loads(credentials.read_text())
        raw, _ = request('/api/auth/login', user)
    else:
        user = {'email': 'logging-lab@example.test', 'password': secrets.token_hex(24) + 'aA1!'}
        raw, _ = request('/api/auth/register', dict(firstName='Logging lab', **user))
        private_write(credentials, json.dumps(user))
    token = json.loads(raw)['token']
    household, _ = request('/api/households/me', token=token, expected=200 if (STATE / 'household-ready').exists() else 404)
    if not (STATE / 'household-ready').exists():
        request('/api/households', {'name': 'Synthetic logging lab'}, token=token, expected=201)
        private_write(STATE / 'household-ready', 'Synthetic household created.\n')
    request('/api/accounts', token=token)
    sentinel = 'lab-redaction-' + secrets.token_hex(12)
    request('/api/auth/recovery-options?token=' + sentinel)
    request('/api/not-a-route-' + sentinel, expected=404)
    request('/api/auth/login', {'email': user['email'], 'password': sentinel}, expected=401)
    _, headers = request('/api/accounts', token=token)
    trace = headers.get('X-Request-ID')
    assert trace, 'Missing request correlation ID'
    for _ in range(30):
        journal = compose('exec', '-T', 'api', 'sh', '-c', 'cat /logs/coiny-*.jsonl').decode()
        if trace in journal:
            break
        time.sleep(.1)
    else:
        raise AssertionError('Request did not reach persistent journal')
    api = compose('ps', '-q', 'api').decode().strip()
    console = run(['docker', 'logs', api]).decode()
    for value in [sentinel, token, user['password'], environment()['JWT_KEY'], environment()['POSTGRES_PASSWORD']]:
        assert value not in journal and value not in console, 'Sensitive value found; output suppressed'
    events = [json.loads(line) for line in journal.splitlines() if line.strip()]
    assert any(e['fields'].get('status') == '401' and e['level'] == 'Warning' for e in events)
    assert any(e['requestId'] == trace for e in events)
    permissions = compose('exec', '-T', 'api', 'sh', '-c', 'stat -c "%a" /logs; stat -c "%a" /logs/coiny-*.jsonl').decode().splitlines()
    assert permissions[0] == '700' and all(mode == '600' for mode in permissions[1:])
    for service in ['api', 'web', 'postgres']:
        cid = compose('ps', '-q', service).decode().strip()
        container = json.loads(run(['docker', 'inspect', cid]))[0]
        assert container['HostConfig']['LogConfig']['Type'] == 'local'
        assert all(m['Name'].startswith(PROJECT + '_') for m in container['Mounts'] if m['Type'] == 'volume')
    request('/api/auth/logout', {}, token=token, expected=204)
    request('/api/accounts', token=token, expected=401)
    print('PASS HTTPS, synthetic signup/household, financial API routing, session revocation, redaction, request IDs, private persistence, rotation settings and isolated volumes.', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['init', 'up', 'smoke', 'test', 'logs', 'down', 'status'])
    args = parser.parse_args()
    if args.action in ['init', 'up']:
        globals()[args.action]()
        return
    assert (ROOT / '.env').exists(), 'Run lab.py init first.'
    if args.action == 'smoke':
        smoke()
    elif args.action == 'test':
        compose('--profile', 'tests', 'up', '-d', '--wait', 'test-postgres')
        compose('--profile', 'tests', 'build', 'integration')
        output = compose('--profile', 'tests', 'run', '--rm', '--no-deps', 'integration')
        private_write(STATE / 'test-results.txt', output.decode(errors='replace'))
        for line in output.decode(errors='replace').splitlines():
            if line.startswith(('Passed!', 'Failed!')):
                print(line, flush=True)
    elif args.action == 'down':
        compose('--profile', 'tests', 'down')  # Preserve every volume; no reset/delete-data shortcut.
        print('Lab stopped; data volumes preserved.')
    elif args.action == 'status':
        print(compose('ps').decode())
    else:
        print(compose('logs', '--tail', '100', 'api', 'web', 'postgres').decode())


if __name__ == '__main__':
    main()
