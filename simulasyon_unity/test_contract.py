import copy, json, re, unittest, xml.etree.ElementTree as ET
from pathlib import Path
from bridge import validate_sensor_packet, SPEC, Bridge

class ContractTests(unittest.TestCase):
    def packet(self):
        d={n:False if t=='BOOL' else 0. if t=='REAL' else 0 for n,t in SPEC['input'].items()}
        d.update(uiUnityProtokol=106,udiUnityOturumu=1,udiUnitySayaci=1,uiPlantVeriKaynagi=1)
        return d
    def test_types_and_reject_malformed(self):
        validate_sensor_packet(self.packet())
        for key,value in [('uiUnityProtokol',102),('xGiris',1),('rKutleGram',float('nan')),('udiUrunKimligi',-1),('uiOperatorKomutu',99)]:
            d=self.packet();d[key]=value
            with self.assertRaises(ValueError):validate_sensor_packet(d)
    def test_material_not_direct_input(self):
        self.assertFalse(any('malzeme' in n.lower() for n in SPEC['input']))
        self.assertEqual(SPEC['materials'],{'0':'Bilinmeyen','1':'PET','2':'Aluminyum','3':'Cam'})
    def test_compiled_symbols_match_contract_and_direction(self):
        path=Path(__file__).resolve().parent.parent/'Codesys_Dosyalari/Paylasim/RVM_Unity_Dijital_Ikiz_v106.Device.Application.xml'
        root=ET.parse(path).getroot()
        ns={'s':'http://www.3s-software.com/schemas/Symbolconfiguration.xsd'}
        gvl=root.find('.//s:Node[@name="GVL_RVMUnity"]',ns)
        self.assertIsNotNone(gvl)
        exported={n.attrib['name']:n.attrib for n in gvl}
        for group in ('input','output'):
            for n,t in SPEC[group].items():
                self.assertEqual(exported[n]['type'],'T_'+t,n)
                self.assertEqual(exported[n]['access'],'ReadWrite' if group=='input' else 'Read',n)
    def test_replay_does_not_feed_watchdog(self):
        b=Bridge();p=self.packet();b.accept_packet(p);stamp=b.sensor_time
        self.assertFalse(b.accept_packet(p));self.assertEqual(stamp,b.sensor_time)
        p['xGiris']=True
        with self.assertRaises(ValueError):b.accept_packet(p)
    def test_reordering_and_retired_session_are_rejected(self):
        b=Bridge();p=self.packet();p['udiUnitySayaci']=5;b.accept_packet(p)
        old=dict(p);old['udiUnitySayaci']=4
        with self.assertRaises(ValueError):b.accept_packet(old)
        new=dict(p);new['udiUnityOturumu']=2;new['udiUnitySayaci']=1;b.accept_packet(new)
        with self.assertRaises(ValueError):b.accept_packet(p)
    def test_bridge_restart_requires_hello(self):
        p=self.packet();p['udiGorulenPlcOturumu']=9
        with self.assertRaises(ValueError):Bridge().accept_packet(p)
    def test_bounds_and_source_codes(self):
        for key,val in [('rHazneKonumu',.5),('uiPlantVeriKaynagi',99),('uiNesneSonucu',99),('rOptikGecirgenlik',2),('rGercekKiriciDevri',float('inf'))]:
            p=self.packet();p[key]=val
            with self.assertRaises(ValueError):validate_sensor_packet(p)
    def test_missing_extra_fields_and_bool_integer(self):
        for mode in range(3):
            p=self.packet()
            if mode==0:del p['udiGorulenPlcOturumu']
            elif mode==1:p['uiMaterial']=1
            else:p['udiUnitySayaci']=True
            with self.assertRaises(ValueError):validate_sensor_packet(p)
    def test_insert_uses_command_parameter_not_recognition_result(self):
        for profile in range(4):
            p=self.packet();p.update(uiOperatorKomutu=4,uiOperatorParametresi=profile,udiKomutKimligi=1)
            validate_sensor_packet(p)

    def test_unity_wire_fields_match_contract(self):
        path=Path(__file__).resolve().parent.parent/'Unity/RVM/Assets/RVM/Scripts/RvmContract.cs'
        text=path.read_text(encoding='utf-8-sig')
        types={'BOOL':'bool','UINT':'int','UDINT':'uint','REAL':'float'}
        for group,cls in [('input','UnitySensors'),('output','PlcCommands')]:
            body=re.search(r'class\s+'+cls+r'\s*\{([^}]+)\}',text).group(1)
            fields={name:typ for typ,name in re.findall(r'public\s+(bool|int|uint|float)\s+(\w+)\s*;',body)}
            self.assertEqual(fields,{name:types[typ] for name,typ in SPEC[group].items()})

    def test_v106_measurement_bounds(self):
        self.assertEqual(SPEC['version'],106)
        for key,value in [('rOlcumMetal',1.01),('rOlcumManyetik',-0.1),('rOlcumYansima',float('nan')),('xOlcumTartiGecerli',1)]:
            packet=self.packet();packet[key]=value
            with self.assertRaises(ValueError):validate_sensor_packet(packet)

if __name__=='__main__':unittest.main(verbosity=2)
