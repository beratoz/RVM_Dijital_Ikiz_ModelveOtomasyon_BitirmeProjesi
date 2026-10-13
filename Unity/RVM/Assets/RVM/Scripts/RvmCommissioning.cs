using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace RVM
{
    // Explicit commissioning runner; all decisions still come from the real CODESYS runtime.
    // Add temporarily in Play mode. Never saved into the production scene.
    public class RvmCommissioning : MonoBehaviour
    {
        public string reportPath="";
        RvmDigitalTwin t;
        readonly List<string> report=new List<string>();
        bool failed;
        void Save(string text){report.Add(DateTime.Now.ToString("HH:mm:ss")+" "+text);File.WriteAllLines(reportPath,report);Debug.Log("RVM TEST "+text);}
        bool Check(bool condition,string name){Save((condition?"PASS ":"FAIL ")+name);if(!condition)failed=true;return condition;}
        IEnumerator Until(Func<bool> predicate,float timeout,string name)
        {
            float end=Time.realtimeSinceStartup+timeout;
            while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;
            Check(predicate(),name);
        }
        uint Total()=>t.plc.udiPetSayaci+t.plc.udiAluminyumSayaci+t.plc.udiCamSayaci;
        IEnumerator ResetStart()
        {
            t.OperatorCommand(2); uint stop=t.sensors.udiKomutKimligi;
            yield return Until(()=>t.plc.udiKomutOnayi==stop && t.plc.uiKomutDurumu>=2 && !t.plc.xCalismaIzni,4,"Stop acknowledged");
            t.ClearStoppedPlant();yield return new WaitForSeconds(.3f);t.OperatorCommand(3);
            uint reset=t.sensors.udiKomutKimligi;
            yield return Until(()=>t.plc.udiKomutOnayi==reset && t.plc.uiKomutDurumu==2 && t.plc.uiDurum==0,4,"Reset to stopped");
            t.OperatorCommand(1);uint start=t.sensors.udiKomutKimligi;
            yield return Until(()=>t.plc.udiKomutOnayi==start && t.plc.uiKomutDurumu==2 && t.plc.uiDurum==10,4,"Start ready");
        }
        IEnumerator Start()
        {
            if (string.IsNullOrWhiteSpace(reportPath)) reportPath=Path.Combine(Application.persistentDataPath,"commissioning.txt");
            reportPath=Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            t=GetComponent<RvmDigitalTwin>();t.autoFeed=false;
            Save("REAL CODESYS / OPC UA / UNITY commissioning");
            yield return Until(()=>t.linkLive,30,"PLC link");if(failed)yield break;
            yield return ResetStart();if(failed)yield break;
            foreach(int profile in new[]{1,2,3,0})
            {
                uint total=Total(),rejected=t.plc.udiReddedilenSayaci;
                uint pet=t.plc.udiPetSayaci,alu=t.plc.udiAluminyumSayaci,glass=t.plc.udiCamSayaci;
                var seen=new List<int>();float began=Time.realtimeSinceStartup;
                t.InsertSpecimen(profile);
                bool crusherBeforeApproval=false;
                do
                {
                    if(!seen.Contains(t.plc.uiDurum))seen.Add(t.plc.uiDurum);
                    if(t.plc.xKiriciCalis&&!t.plc.xTanimaOnayi)crusherBeforeApproval=true;
                    if(t.plc.uiDurum==900)break;
                    yield return null;
                }while(Time.realtimeSinceStartup-began<14 && !(t.plc.uiDurum==10 && (Total()!=total||t.plc.udiReddedilenSayaci!=rejected)));
                float elapsed=Time.realtimeSinceStartup-began;
                Check(!crusherBeforeApproval,"Crusher requires recognition approval");
                Check(t.plc.uiDurum==10,"Return ready profile="+profile+" states="+string.Join(",",seen)+" fault="+t.plc.uiHataKodu);
                if(t.plc.uiProtokolSurumu==105)
                {
                    Check(t.vision && t.vision.complete && t.sensors.udiGoruntuOnayi==t.plc.udiGoruntuIstegi &&
                        t.sensors.udiGoruntuUrunu==t.plc.udiIslemKimligi && t.sensors.udiGoruntuPlcOturumu==t.plc.udiPlcOturumu &&
                        t.sensors.udiGoruntuUnityOturumu==t.sensors.udiUnityOturumu,"Camera request/product/session matched");
                    Check(t.plc.uiGoruntuKarari==(profile>0?1:2),"PLC image decision profile="+profile+" decision="+t.plc.uiGoruntuKarari);
                    Save("IMAGE length="+t.sensors.rGoruntuBoy+" width="+t.sensors.rGoruntuEn+" fill="+t.sensors.rGoruntuDoluluk+" valid="+t.sensors.xGoruntuGecerli);
                    if(t.vision && t.vision.preview)File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(reportPath),"camera_profile_"+profile+".png"),t.vision.preview.EncodeToPNG());
                }
                if(profile>0)
                {
                    Check(Total()==total+1,"Count exactly once profile="+profile);
                    Check(t.plc.udiPetSayaci==pet+(profile==1?1u:0u)&&t.plc.udiAluminyumSayaci==alu+(profile==2?1u:0u)&&t.plc.udiCamSayaci==glass+(profile==3?1u:0u),"Correct material counter");
                    Check(t.sensors.uiBosaltilanKutu==profile,"Physical bin matches material");
                    Check(seen.Contains(20)&&seen.Contains(30)&&seen.Contains(40)&&seen.Contains(50)&&seen.Contains(60)&&seen.Contains(70)&&seen.Contains(80)&&seen.Contains(90),"All PLC stages observed");
                    Check(elapsed>=5&&elapsed<=6.667f,"9-12/min cycle="+elapsed.ToString("F3")+"s rate="+(60/elapsed).ToString("F2"));
                }
                else
                { Check(Total()==total&&t.plc.udiReddedilenSayaci==rejected+1,"Unknown rejected without acceptance");Check(!seen.Contains(50),"Unknown never crushed"); }
                if(failed)yield break;
            }
            t.InsertSpecimen(1);yield return Until(()=>t.plc.uiDurum==40,4,"Fault test reaches transport");
            t.sensors.xSikisma=true;
            yield return Until(()=>t.plc.uiHataKodu==304,3,"Jam detected by PLC");
            Check(!t.plc.xKiriciCalis&&!t.plc.xKonveyorIleri,"Fault stops actuators");
            yield return ResetStart();if(failed)yield break;
            t.InsertSpecimen(2);yield return Until(()=>t.plc.uiDurum==40,4,"Disconnect test reaches transport");
            string url=t.bridgeUrl;t.bridgeUrl="http://127.0.0.1:8766/exchange";
            yield return new WaitForSeconds(1.5f);
            Check(!t.linkLive&&t.sensors.rGercekBantHizi==0&&t.sensors.rGercekKiriciDevri==0,"Disconnected Unity freezes motion");
            t.bridgeUrl=url;yield return Until(()=>t.linkLive,8,"Connection recovers");
            yield return Until(()=>t.plc.uiHataKodu==301,3,"PLC watchdog latches communication fault");
            Check(!t.plc.xCalismaIzni,"Reconnection does not auto restart");
            yield return ResetStart();if(failed)yield break;
            t.InsertSpecimen(3);yield return Until(()=>t.plc.uiDurum==40,4,"Identity test reaches transport");
            t.sensors.udiUrunKimligi+=100;
            yield return Until(()=>t.plc.uiHataKodu==305,3,"Wrong product ID rejected");
            yield return ResetStart();
            t.sensors.xAcilStop=true;yield return Until(()=>t.plc.uiHataKodu==303,3,"Emergency stop acknowledged");
            t.sensors.xAcilStop=false;
            t.ClearStoppedPlant();yield return new WaitForSeconds(.3f);t.OperatorCommand(3);
            yield return Until(()=>t.plc.uiDurum==0,3,"Finished safely stopped");
            Save(failed?"RESULT FAIL":"RESULT PASS");
        }
    }
}
