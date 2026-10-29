#!/usr/bin/env python3
"""Isolated synthetic-data lab. Never connects to the Coiny development database."""
import argparse, concurrent.futures, json, os, pathlib, secrets, subprocess, sys, time, urllib.request, urllib.error, uuid
ROOT=pathlib.Path(__file__).resolve().parent
URL='http://127.0.0.1:6088'

def initialize():
    path=ROOT/'.env'
    if path.exists():
        print('Using existing isolated lab configuration.');return
    content=''.join(f'{name}={secrets.token_hex(32)}\n' for name in ['LAB_POSTGRES_PASSWORD','LAB_KEY','LAB_SIGNING_KEY'])
    descriptor=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
    with os.fdopen(descriptor,'w') as output:output.write(content)
    print('Created private lab configuration. No real credentials or data used.')

def compose(*args):
    subprocess.run(['docker','compose','--project-name','coiny-hybrid-lab','--file',str(ROOT/'compose.yaml'),*args],cwd=ROOT,check=True)

def settings():
    return dict(line.split('=',1) for line in (ROOT/'.env').read_text().splitlines() if line and not line.startswith('#'))

class Verification:
    def __init__(self):self.config=settings();self.users={};self.households={};self.results=[]
    def request(self,path,method='GET',body=None,user=None,status=200):
        headers={'Content-Type':'application/json'}
        if path.startswith('/lab'):headers['X-Lab-Key']=self.config['LAB_KEY']
        if user:headers['Authorization']='Bearer '+self.users[user]['token']
        request=urllib.request.Request(URL+path,data=None if body is None else json.dumps(body).encode(),headers=headers,method=method)
        try:
            with urllib.request.urlopen(request,timeout=20) as response:code=response.status;raw=response.read()
        except urllib.error.HTTPError as error:code=error.code;raw=error.read()
        assert code==status,f'{method} {path}: expected {status}, got {code}: {raw[:200].decode(errors="replace")}'
        return json.loads(raw) if raw else None
    def wait_ready(self):
        deadline=time.monotonic()+90
        while time.monotonic()<deadline:
            try:
                self.request('/health');return
            except (AssertionError,OSError):time.sleep(.5)
        raise AssertionError('Lab API did not become ready within 90 seconds.')
    def fixtures(self):
        data=self.request('/lab/fixtures');self.households=data['households'];self.users={u['name']:u for u in data['users']}
    def snapshot(self,household,user='Alice',status=200):return self.request(f'/api/households/{household}/snapshot',user=user,status=status)
    def import_body(self,count,amount=13):return {'operationId':str(uuid.uuid4()),'rows':[{'description':f'Synthetic transaction {i}','amountCents':amount} for i in range(count)]}
    def write(self,household,body,user='Alice',status=200):return self.request(f'/api/households/{household}/imports','POST',body,user,status)
    def inventory(self):return {row['id']:row for row in self.request('/lab/inventory')}
    def check(self,name,action):
        start=time.monotonic()
        try:
            details=action() or {};self.results.append({'name':name,'passed':True,'durationMs':round((time.monotonic()-start)*1000,1),'details':details});print('PASS',name,flush=True)
        except Exception as error:
            self.results.append({'name':name,'passed':False,'error':str(error)});raise
    def isolation(self):
        a,b=self.households['a'],self.households['b'];before=self.inventory();opens=self.request('/lab/metrics')['financialFileOpens']
        self.snapshot(a,user=None,status=401);self.snapshot(a,'Eve',404);self.snapshot(b,'Alice',404);self.snapshot(a,'Bob',404)
        self.snapshot(str(uuid.uuid4()),'Alice',404)
        assert self.inventory()==before,'Unauthorized access changed file/catalog inventory.'
        assert self.request('/lab/metrics')['financialFileOpens']==opens,'Unauthorized requests opened financial files.'
        assert before[a]['storageKey']!=before[b]['storageKey']
        assert before[a]['fileExists'] and before[b]['fileExists']
        self.request(f'/lab/households/{a}/members/{self.users["Amy"]["id"]}','POST',{},status=204)
        assert self.snapshot(a,'Amy')['instanceId']==self.snapshot(a)['instanceId']
        return {'distinctHouseholdFiles':True,'unauthorizedRequestsDeniedBeforeFileOpens':True}
    def concurrency(self):
        a,b=self.households['a'],self.households['b'];before_a=self.snapshot(a);before_b=self.snapshot(b,'Bob')
        operations=[(a,self.import_body(250,11),'Alice') for _ in range(10)]+[(b,self.import_body(250,17),'Bob') for _ in range(10)]
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            responses=list(pool.map(lambda args:self.write(*args),operations))
        assert sum(row['rowsAdded'] for row in responses)==5000
        after_a=self.snapshot(a);after_b=self.snapshot(b,'Bob')
        assert after_a['rows']==before_a['rows']+2500 and after_a['balanceCents']==before_a['balanceCents']+27500
        assert after_b['rows']==before_b['rows']+2500 and after_b['balanceCents']==before_b['balanceCents']+42500
        assert self.write(*operations[0])['replayed']
        changed=dict(operations[0][1]);changed['rows']=[{'description':'Different input','amountCents':1}]
        self.write(a,changed,status=409)
        assert self.snapshot(a)==after_a
        self.write(a,self.import_body(100,31),'Amy');assert self.snapshot(a)['rows']==after_a['rows']+100
        return {'imports':20,'concurrentClients':8,'importedRows':5000,'exactBalances':True,'idempotentRetry':True,'sharedHouseholdMemberWrite':True}
    def locks(self):
        a,b=self.households['a'],self.households['b']
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            hold=pool.submit(self.request,f'/lab/households/{a}/hold?milliseconds=4000','POST',{})
            deadline=time.monotonic()+5
            while not self.request(f'/lab/households/{a}/holding')['holding']:
                assert time.monotonic()<deadline,'Writer hold never started.';time.sleep(.02)
            start=time.monotonic();self.write(b,self.import_body(100),'Bob');b_ms=(time.monotonic()-start)*1000
            assert not hold.done(),'Other household did not finish while A was locked.'
            start=time.monotonic();self.snapshot(a);read_ms=(time.monotonic()-start)*1000
            assert not hold.done(),'WAL read did not finish while A was locked.'
            same=pool.submit(self.write,a,self.import_body(100),'Amy');time.sleep(.15)
            assert not same.done(),'Same-household writer bypassed its write queue.'
            hold.result();same.result()
        return {'heldWriterMs':4000,'otherHouseholdWriteMs':round(b_ms,1),'sameHouseholdReadMs':round(read_ms,1),'sameHouseholdWriterQueued':True}
    def provisioning(self):
        ids=[]
        for fail in ['after-catalog','after-file']:
            row=self.request('/lab/households','POST',{'userId':self.users['Alice']['id'],'name':'Interrupted synthetic household','failAt':fail})
            id=row['id'];ids.append(id);before=self.inventory()[id]
            assert before['status']=='Provisioning';assert before['fileExists']==(fail=='after-file');self.snapshot(id,status=503)
            self.request('/lab/recover','POST',{});snapshot=self.snapshot(id)
            self.request('/lab/recover','POST',{});assert self.snapshot(id)==snapshot
            assert self.inventory()[id]['storageKey']==before['storageKey'] and snapshot['rows']==0
        return {'recoveredHouseholds':ids,'duplicateRecoverySafe':True}
    def migrations(self):
        row=self.request('/lab/households','POST',{'userId':self.users['Alice']['id'],'name':'Schema-v1 synthetic household','schemaVersion':1});id=row['id']
        self.snapshot(id,status=503)
        self.request(f'/lab/households/{id}/migrate?failAfterCommit=true','POST',{})
        assert self.inventory()[id]['status']=='Maintenance';self.snapshot(id,status=503)
        self.request('/lab/recover','POST',{});assert self.snapshot(id)['schemaVersion']==2
        self.write(id,self.import_body(10));before=self.snapshot(id)
        self.request(f'/lab/households/{id}/migrate','POST',{});assert self.snapshot(id)==before
        return {'upgrade':'v1 → v2','interruptedCatalogUpdateRecovered':True,'repeatMigrationPreservesData':True}
    def restore(self):
        a,b=self.households['a'],self.households['b'];baseline=self.snapshot(a);other=self.snapshot(b,'Bob')
        backup=self.request(f'/lab/households/{a}/backup','POST',{})['backupId']
        self.write(a,self.import_body(50),'Amy');assert self.snapshot(a)['rows']==baseline['rows']+50
        self.request(f'/lab/households/{a}/members/{self.users["Amy"]["id"]}','DELETE',status=204);self.snapshot(a,'Amy',404)
        self.request(f'/lab/households/{a}/restore','POST',{'backupId':backup,'failAfterMarker':True});self.snapshot(a,status=503)
        self.request('/lab/recover','POST',{});assert self.snapshot(a)==baseline;self.snapshot(a,'Amy',404)
        self.request(f'/lab/households/{b}/restore','POST',{'backupId':backup},status=404)
        assert self.snapshot(b,'Bob')==other
        return {'consistentSqliteBackupApi':True,'pendingRestoreRecovered':True,'revokedMembershipNotRestored':True,'otherHouseholdUnchanged':True}
    def ownership(self):
        a=self.households['a'];baseline=self.snapshot(a)
        self.request(f'/lab/households/{a}/node/wrong-node','PUT',{},status=204)
        try:self.snapshot(a,status=503)
        finally:self.request(f'/lab/households/{a}/node/lab-node-1','PUT',{},status=204)
        assert self.snapshot(a)==baseline
        return {'wrongNodeDenied':True}
    def mapping_fault(self):
        a,b=self.households['a'],self.households['b'];before_a=self.snapshot(a);before_b=self.snapshot(b,'Bob')
        self.request(f'/lab/swap-storage/{a}/{b}','POST',{})
        try:self.snapshot(a,status=503);self.snapshot(b,'Bob',503)
        finally:self.request(f'/lab/swap-storage/{a}/{b}','POST',{})
        assert self.snapshot(a)==before_a and self.snapshot(b,'Bob')==before_b
        return {'incorrectRegistryMappingsFailClosed':True,'financialDataUnchanged':True}
    def lifecycle(self):
        a,b=self.households['a'],self.households['b'];baseline=self.snapshot(a);other=self.snapshot(b,'Bob')
        backup=self.request(f'/lab/households/{a}/backup','POST',{})['backupId'];self.write(a,self.import_body(10))
        self.request(f'/lab/households/{a}/restore','POST',{'backupId':backup,'failAfterMarker':True})
        pending=self.request('/lab/households','POST',{'userId':self.users['Alice']['id'],'name':'Recovered after app recreation','failAt':'after-file'})['id']
        compose('up','-d','--no-deps','--force-recreate','app');self.wait_ready()
        assert self.snapshot(a)==baseline and self.snapshot(b,'Bob')==other;self.snapshot(a,'Amy',404);assert self.snapshot(pending)['rows']==0
        compose('up','-d','--no-deps','--force-recreate','postgres');self.wait_ready()
        assert self.snapshot(a)==baseline and self.snapshot(b,'Bob')==other;self.snapshot(a,'Amy',404)
        return {'appContainerRecreated':True,'postgresContainerRecreated':True,'pendingProvisionAndRestoreRecoveredOnStartup':True,'dataAndAccessPersisted':True}
    def outage(self):
        a=self.households['a'];baseline=self.snapshot(a)
        compose('stop','postgres')
        try:self.snapshot(a,status=503)
        finally:compose('start','postgres');self.wait_ready()
        assert self.snapshot(a)==baseline
        return {'failsClosedWithoutPostgres':True,'resumedWithoutDataLoss':True}
    def run(self,lifecycle):
        self.wait_ready();self.fixtures()
        try:
            for name,action in [('Household isolation',self.isolation),('Concurrent imports and retry safety',self.concurrency),('Independent writers and WAL reads',self.locks),('Interrupted provisioning',self.provisioning),('Interrupted schema migration',self.migrations),('Backup, restore, and current permissions',self.restore),('Household node ownership',self.ownership),('Incorrect registry mapping',self.mapping_fault)]:self.check(name,action)
            if lifecycle:self.check('Container recreation and startup recovery',self.lifecycle);self.check('Control database outage',self.outage)
        finally:
            path=ROOT/'results';path.mkdir(exist_ok=True)
            (path/'latest.json').write_text(json.dumps({'syntheticOnly':True,'lifecycleIncluded':lifecycle,'checks':self.results,'allPassed':bool(self.results) and all(r['passed'] for r in self.results)},indent=2))
        print(f'{len(self.results)} checks passed. Report: {ROOT / "results/latest.json"}')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['init','up','test','status','down']);parser.add_argument('--lifecycle',action='store_true');args=parser.parse_args()
    if args.command=='init':initialize()
    elif args.command=='up':initialize();compose('up','-d','--build','--wait')
    elif args.command=='test':Verification().run(args.lifecycle)
    elif args.command=='status':compose('ps')
    elif args.command=='down':compose('down') # Deliberately no -v: synthetic volumes persist.
