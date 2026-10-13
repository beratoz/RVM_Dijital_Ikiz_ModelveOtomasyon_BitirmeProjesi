"""Local Unity HTTP <-> CODESYS OPC UA adapter. No sequence logic or fallback PLC.
Run with ../.venv-rvm-opcua/Scripts/python.exe bridge.py
"""
import asyncio, json, math, os, time, logging
from pathlib import Path
from asyncua import Client, ua

SPEC=json.loads((Path(__file__).with_name('contract.json')).read_text())
TYPES={'BOOL':ua.VariantType.Boolean,'UINT':ua.VariantType.UInt16,
       'UDINT':ua.VariantType.UInt32,'REAL':ua.VariantType.Float}
LOG=logging.getLogger('rvm')

def validate_sensor_packet(data):
    if not isinstance(data,dict) or set(data)!=set(SPEC['input']):
        raise ValueError('Sensor alanlari contract.json ile ayni olmali')
    for key,typ in SPEC['input'].items():
        v=data[key]
        if typ=='BOOL' and type(v) is not bool: raise ValueError(key+' BOOL olmali')
        if typ in ('UINT','UDINT') and (type(v) is not int or not 0<=v<2**(16 if typ=='UINT' else 32)):
            raise ValueError(key+' aralik/tip hatasi')
        if typ=='REAL' and (type(v) not in (int,float) or not math.isfinite(v)): raise ValueError(key+' sonlu REAL olmali')
    for key,v in data.items():
        meta=SPEC['metadata'][key]
        lo,hi=meta['limits']
        epsilon=1e-6 if SPEC['input'][key]=='REAL' else 0
        if not lo-epsilon<=v<=hi+epsilon: raise ValueError(key+' fiziksel sinir disinda')
        if meta['allowed_values'] is not None and v not in meta['allowed_values']: raise ValueError(key+' gecersiz kod')
    if data['uiUnityProtokol']!=SPEC['version'] or not data['udiUnityOturumu'] or not data['udiUnitySayaci']:
        raise ValueError('Protokol/oturum/sayac gecersiz')
    if data['uiOperatorKomutu'] not in range(6): raise ValueError('Gecersiz operator komutu')
    return data

async def discover(client):
    """Resolve names under dedicated GVL only; never guess namespace numeric indexes."""
    fields={**SPEC['input'],**SPEC['output'],'udiGirisBaslangic':'UDINT','udiGirisBitis':'UDINT'}
    found={}; visited=set(); queue=[(client.nodes.objects,0,False)]
    while queue:
        node,depth,in_scope=queue.pop(0)
        if node.nodeid.to_string() in visited or depth>10: continue
        visited.add(node.nodeid.to_string())
        name=(await node.read_browse_name()).Name
        qualified=str(node.nodeid.Identifier)
        scope=in_scope or 'GVL_RVMUnity' in name or 'GVL_RVMUnity' in qualified
        short=name.rsplit('.',1)[-1]
        if scope and short in fields:
            if short in found: raise RuntimeError('Tekrarlanan sembol: '+short)
            actual=await node.read_data_type_as_variant_type()
            if actual!=TYPES[fields[short]]: raise RuntimeError('OPC UA tipi uyusmuyor: '+short)
            found[short]=node
        if depth<10 and (await node.read_node_class()) in (ua.NodeClass.Object,ua.NodeClass.Variable):
            for child in await node.get_children(): queue.append((child,depth+1,scope))
        if len(found)==len(fields): break
    missing=set(fields)-set(found)
    if missing: raise RuntimeError('RVM Unity sembolleri eksik: '+','.join(sorted(missing)))
    return found

class Bridge:
    def __init__(self):
        self.sensors=None; self.sensor_time=0.; self.plc={}; self.plc_time=0.
        self.error='CODESYS baglantisi bekleniyor'; self.connected=False
        self.retired_sessions=set()
    def accept_packet(self,data):
        data=validate_sensor_packet(data)
        old=self.sensors
        if old and old['udiUnityOturumu']==data['udiUnityOturumu']:
            if data['udiUnitySayaci']<old['udiUnitySayaci']: raise ValueError('Eski paket sayaci')
            if data['udiUnitySayaci']==old['udiUnitySayaci']:
                if data!=old: raise ValueError('Ayni paket kimligiyle degistirilmis veri')
                return False
        elif old:
            if data['udiUnityOturumu'] in self.retired_sessions: raise ValueError('Kapatilmis Unity oturumu')
            if data['udiGorulenPlcOturumu']!=0: raise ValueError('Yeni oturum HELLO gerektirir')
            self.retired_sessions.add(old['udiUnityOturumu'])
        elif data['udiGorulenPlcOturumu']!=0:
            raise ValueError('Kopru yeniden acildi; yeni HELLO gerekli')
        self.sensors=dict(data); self.sensor_time=time.monotonic()
        return True
    def reply(self):
        fresh=self.connected and time.monotonic()-self.plc_time<.6 and time.monotonic()-self.sensor_time<.6
        return {'version':106,'project':'RVM_DENEYSEL_OLCUM_v106','connected':fresh,'error':(self.error or 'Guncel Unity sensor paketi / PLC kalp atisi bekleniyor') if not fresh else '', 'plc':self.plc}
    async def run_opc(self):
        while True:
            self.connected=False
            try:
                client=Client(os.environ.get('RVM_OPC_ENDPOINT','opc.tcp://localhost:4840'),timeout=3)
                if os.environ.get('RVM_OPC_USER'):
                    client.set_user(os.environ['RVM_OPC_USER']); client.set_password(os.environ.get('RVM_OPC_PASSWORD',''))
                if os.environ.get('RVM_OPC_SECURITY'):
                    await client.set_security_string(os.environ['RVM_OPC_SECURITY'])
                async with client:
                    nodes=await discover(client)
                    if await nodes['uiProtokolSurumu'].read_value()!=SPEC['version']: raise RuntimeError('PLC protokolu sozlesmeyle uyusmuyor')
                    LOG.info('CODESYS protocol %s sembolleri ve veri tipleri dogrulandi',SPEC['version'])
                    last_packet=None; seq=int(await nodes['udiGirisBitis'].read_value()); last_heartbeat=None
                    last_ack=None; ack_time=0.
                    while True:
                        packet=self.sensors
                        if packet and (packet['udiUnityOturumu'],packet['udiUnitySayaci'])!=last_packet and time.monotonic()-self.sensor_time<.5:
                            packet=dict(packet)
                            packet['rGoruntuYasiMs']=min(10000.,packet['rGoruntuYasiMs']+1000*(time.monotonic()-self.sensor_time))
                            seq=(seq+1)%4294967296 or 1
                            await nodes['udiGirisBaslangic'].write_value(ua.Variant(seq,ua.VariantType.UInt32))
                            await client.write_values([nodes[n] for n in SPEC['input']],
                                [ua.DataValue(ua.Variant(packet[n],TYPES[t])) for n,t in SPEC['input'].items()],raise_on_partial_error=True)
                            await nodes['udiGirisBitis'].write_value(ua.Variant(seq,ua.VariantType.UInt32))
                            last_packet=(packet['udiUnityOturumu'],packet['udiUnitySayaci'])
                        # Guard across scans so all returned outputs belong to one PLC scan.
                        for attempt in range(12):
                            before=await nodes['udiCikisBaslangic'].read_value()
                            vals=await client.read_values([nodes[n] for n in SPEC['output']])
                            after=await nodes['udiCikisBitis'].read_value()
                            again=await nodes['udiCikisBaslangic'].read_value()
                            candidate=dict(zip(SPEC['output'],vals))
                            if before!=0 and before==after==again==candidate['udiCikisBaslangic']==candidate['udiCikisBitis']: break
                        else:
                            if before==0 and after==0:
                                raise RuntimeError('PLC yuklu fakat cevrimsel cikislar baslamadi; Application RUN ve AnaGorev durumunu kontrol edin')
                            raise RuntimeError('PLC cikis goruntusu tutarli okunamadi')
                        snapshot=dict(zip(SPEC['output'],vals))
                        if snapshot['uiProtokolSurumu']!=SPEC['version']: raise RuntimeError('PLC protokolu degisti')
                        for name,value in snapshot.items():
                            typ=SPEC['output'][name]
                            if typ=='BOOL' and type(value) is not bool: raise ValueError(name+' BOOL degil')
                            if typ in ('UINT','UDINT') and (type(value) is not int or not 0<=value<2**(16 if typ=='UINT' else 32)): raise ValueError(name+' gecersiz tamsayi')
                            if typ=='REAL' and (type(value) not in (int,float) or not math.isfinite(value)): raise ValueError(name+' gecersiz REAL')
                        beat=(snapshot['udiPlcOturumu'],snapshot['udiPlcSayaci'])
                        if beat!=last_heartbeat:
                            last_heartbeat=beat; self.plc_time=time.monotonic()
                        if snapshot['udiGirisOnayi']!=last_ack:
                            last_ack=snapshot['udiGirisOnayi']; ack_time=time.monotonic()
                        self.plc=snapshot
                        self.connected=bool(packet and snapshot['udiUnityOnayOturumu']==packet['udiUnityOturumu']
                                            and snapshot['udiPlcOturumu']!=0 and packet['udiGorulenPlcOturumu']==snapshot['udiPlcOturumu']
                                            and time.monotonic()-ack_time<.6 and time.monotonic()-self.plc_time<.6)
                        self.error='' if self.connected else 'PLC / Unity oturum veya veri onayi bekleniyor'
                        await asyncio.sleep(.035)
            except Exception as exc:
                self.error=type(exc).__name__+': '+(str(exc) or 'PLC sunucusu cevap vermedi')
                self.connected=False; LOG.warning('OPC UA: %s',self.error)
                await asyncio.sleep(2)
    async def http(self,reader,writer):
        status='200 OK'
        try:
            header=await asyncio.wait_for(reader.readuntil(b'\r\n\r\n'),2)
            rows=header.decode('ascii').split('\r\n'); method,path,_=rows[0].split(' ')
            headers=dict(line.lower().split(':',1) for line in rows[1:] if ':' in line)
            if 'origin' in headers: raise ValueError('Tarayici kaynakli yazma istegi kabul edilmiyor')
            if method=='POST' and path=='/exchange':
                length=int(headers.get('content-length','0'))
                if not 0<length<=16384: raise ValueError('Gecersiz govde boyutu')
                self.accept_packet(json.loads(await asyncio.wait_for(reader.readexactly(length),2)))
            elif not (method=='GET' and path=='/health'): raise ValueError('Gecersiz yol')
            body=json.dumps(self.reply(),ensure_ascii=False).encode('utf8')
        except Exception as exc:
            status='400 Bad Request'; body=json.dumps({'connected':False,'error':str(exc)}).encode()
        writer.write(('HTTP/1.1 '+status+'\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: '+str(len(body))+'\r\nConnection: close\r\n\r\n').encode()+body)
        await writer.drain(); writer.close(); await writer.wait_closed()

async def main():
    bridge=Bridge()
    server=await asyncio.start_server(bridge.http,'127.0.0.1',8766,limit=32768)
    LOG.info('Unity adapter: http://127.0.0.1:8766; gercek CODESYS gerekli')
    async with server: await asyncio.gather(server.serve_forever(),bridge.run_opc())
if __name__=='__main__':
    logging.basicConfig(level=logging.INFO,format='%(asctime)s %(message)s')
    logging.getLogger('asyncua').setLevel(logging.ERROR)
    try: asyncio.run(main())
    except KeyboardInterrupt: pass
