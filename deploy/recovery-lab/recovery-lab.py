#!/usr/bin/env python3
"""Fresh loopback-only recovery destination. Never seeds, resets or imports data."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parent
URL = 'http://127.0.0.1:6082'

def compose(*arguments, check=True, capture=False):
    return subprocess.run(['docker', 'compose', '--project-name', 'coiny-recovery-lab', '--file', str(ROOT/'compose.yaml'), *arguments], cwd=ROOT, check=check, text=True, stdout=subprocess.PIPE if capture else None)

def configure():
    path=ROOT/'.env'
    if path.exists(): return
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
    with os.fdopen(fd,'w') as output:
        output.write(f'RECOVERY_POSTGRES_PASSWORD={secrets.token_hex(32)}\nRECOVERY_JWT_KEY={secrets.token_hex(32)}\n')

def request(path, expected=200):
    try:
        with urllib.request.urlopen(URL+path,timeout=10) as response: status,raw=response.status,response.read()
    except urllib.error.HTTPError as error: status,raw=error.code,error.read()
    if status!=expected: raise RuntimeError(f'{path}: expected {expected}, received {status}')
    return raw

def smoke():
    deadline=time.monotonic()+90
    while True:
        try: request('/api/auth/recovery-options');break
        except (OSError,RuntimeError):
            if time.monotonic()>deadline:raise RuntimeError('Recovery lab did not become ready; inspect its logs.')
            time.sleep(1)
    assert b'<app-root' in request('/')
    assert b'<app-root' in request('/settings')
    request('/api/accounts',401);request('/api/household-data/capabilities',401)
    disabled=compose('exec','-T','api','printenv','Operations__ScheduledJobsEnabled',capture=True).stdout.strip()
    assert disabled=='false'
    print('PASS: recovery UI/API ready, protected routes require login, scheduled scans disabled. No user or household was created by this check.')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['up','smoke','status','logs','down'])
    action=parser.parse_args().action
    if action=='up':
        configure();compose('build','api','web');compose('up','--wait','--detach','postgres')
        if not (ROOT/'.initialized').exists():
            compose('run','--rm','--no-deps','--user','0:0','api','--initialize-hybrid','--offline')
            compose('run','--rm','--no-deps','--user','0:0','--entrypoint','chown','api','-R','1654:1654','/data')
            (ROOT/'.initialized').write_text('Fresh hybrid initialization completed; never automatically reset these volumes.\n')
        compose('up','--detach','api','web');smoke();print('Recovery lab: '+URL)
    elif action=='smoke':smoke()
    elif action=='status':compose('ps')
    elif action=='logs':compose('logs','--tail','60')
    elif action=='down':compose('down') # User-created recovery data and keys are retained.

if __name__=='__main__':main()
