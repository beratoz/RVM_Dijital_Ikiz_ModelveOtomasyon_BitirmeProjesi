using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RVM.Experimental
{
    // Temporary Play-only acceptance driver. Commands use the actual bridge/PLC.
    public sealed class RvmEndToEndAcceptance : MonoBehaviour
    {
        public RvmDigitalTwin twin;
        public string reportPath;
        public bool complete,passed;
        public bool extendedOnly;
        public int enduranceCycles;
        public string phase;
        readonly HashSet<int> states=new HashSet<int>();
        string report="v106 LIVE Unity / OPC UA / CODESYS acceptance\n";
        string originalUrl;
        float nextLog;
        uint[] Counts()=>new[]{twin.plc.udiReddedilenSayaci,twin.plc.udiPetSayaci,twin.plc.udiAluminyumSayaci,twin.plc.udiCamSayaci};
        void Log(string s){report+=s+"\n";File.WriteAllText(reportPath,report);}
        void Check(bool ok,string s){if(!ok)throw new Exception(s);Log("PASS "+s);}
        IEnumerator Await(Func<bool> condition,float seconds,string text)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),text);}
        void Update()
        {
            if(!twin||complete||string.IsNullOrEmpty(reportPath))return;
            states.Add(twin.plc.uiDurum);
            if(Time.realtimeSinceStartup>=nextLog){nextLog=Time.realtimeSinceStartup+.1f;
                File.AppendAllText(reportPath+".jsonl","{\"phase\":\""+phase+"\",\"time\":"+Time.realtimeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"plc\":"+JsonUtility.ToJson(twin.plc)+",\"sensors\":"+JsonUtility.ToJson(twin.sensors)+"}\n");}
        }
        public IEnumerator Start()
        {
            if (!twin) twin=GetComponent<RvmDigitalTwin>();
            if (!twin) throw new InvalidOperationException("Assign the RVM controller before starting the acceptance test.");
            if (string.IsNullOrWhiteSpace(reportPath)) reportPath=Path.Combine(Application.persistentDataPath,"v106_acceptance.txt");
            reportPath=Path.GetFullPath(reportPath);
            originalUrl=twin.bridgeUrl;Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var stack=new Stack<IEnumerator>();stack.Push(enduranceCycles>0?Endurance():extendedOnly?Extended():Suite());
            while(stack.Count>0){object current=null;bool moved=false;Exception failure=null;
                try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){Log("FAIL "+phase+": "+failure.Message+"\nRESULT FAIL");twin.bridgeUrl=originalUrl;twin.autoFeed=false;twin.OperatorCommand(2);complete=true;yield break;}
                if(!moved){stack.Pop();continue;}if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
            passed=complete=true;Log("RESULT PASS");
        }
        IEnumerator Recover()
        {
            twin.autoFeed=false;twin.sensors.xAcilStop=false;twin.sensors.xSikisma=false;
            var bins=(bool[])typeof(RvmDigitalTwin).GetField("full",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(twin);
            Array.Clear(bins,0,bins.Length);
            yield return Await(()=>twin.linkLive,10,"Link live");twin.OperatorCommand(2);
            yield return Await(()=>!twin.plc.xCalismaIzni,3,"Stopped");yield return new WaitForSecondsRealtime(.3f);
            twin.ClearStoppedPlant();yield return new WaitForSecondsRealtime(.4f);twin.OperatorCommand(3);
            yield return Await(()=>twin.plc.uiHataKodu==0&&twin.plc.uiDurum==0,4,"Reset acknowledged");
            yield return new WaitForSecondsRealtime(.2f);twin.OperatorCommand(1);
            yield return Await(()=>twin.plc.uiDurum==10,4,"PLC ready");
        }
        IEnumerator Cycle(int profile,bool noise=false,int expected=-1)
        {
            if(expected<0)expected=profile;
            phase="cycle_"+profile+(noise?"_noise":"");states.Clear();var before=Counts();
            twin.inlineMeasurement.noiseEnabled=noise;twin.InsertSpecimen(profile);
            yield return Await(()=>twin.plc.uiDurum!=10,3,"Product admitted "+profile);
            yield return Await(()=>twin.plc.uiDurum==10||twin.plc.uiHataKodu!=0,15,"Cycle returned or fault reported");
            Check(twin.plc.uiHataKodu==0,"No PLC fault "+profile);
            var after=Counts();for(int n=0;n<4;n++)Check(after[n]==before[n]+(n==expected?1u:0u),"Only expected counter "+n);
            Check(states.Contains(30),"Recognition observed");
            if(expected==0){Check(states.Contains(100)&&!states.Contains(40)&&!states.Contains(50),"Unknown rejected before crushing");}
            else {
                foreach(int state in new[]{40,50,60,70,80,90})Check(states.Contains(state),"Sequence state "+state);
                Check(twin.sensors.uiBosaltilanKutu==expected,"Physical bin matches PLC class");
                Log("CYCLE_SECONDS "+twin.lastCycleSeconds.ToString("F3"));
                var m=twin.inlineMeasurement.reading;Log("MEASURE "+JsonUtility.ToJson(m));
            }
        }
        IEnumerator Suite()
        {
            phase="startup";yield return Await(()=>twin.inlineMeasurement.ready,8,"Station calibrated");yield return Recover();
            yield return Cycle(1);yield return Cycle(2);yield return Cycle(3);yield return Cycle(0);yield return Cycle(1,true);
            phase="stop_mid_cycle";twin.InsertSpecimen(1);yield return Await(()=>twin.plc.uiDurum==30,4,"Stop at recognition");
            twin.OperatorCommand(2);yield return Await(()=>!twin.plc.xCalismaIzni,3,"Operator stop accepted");
            yield return new WaitForSecondsRealtime(.4f);Check(twin.sensors.rGercekBantHizi==0&&twin.sensors.rGercekKiriciDevri==0,"Motion stopped");yield return Recover();
            phase="emergency";twin.InsertSpecimen(2);yield return Await(()=>twin.plc.uiDurum==40,7,"Emergency during transport");
            twin.sensors.xAcilStop=true;yield return Await(()=>twin.plc.uiHataKodu==303,3,"PLC emergency 303");
            yield return new WaitForSecondsRealtime(.3f);Check(twin.sensors.rGercekBantHizi==0&&twin.sensors.rGercekKiriciDevri==0,"Emergency motion stopped");yield return Recover();
            phase="full_bin";var bins=(bool[])typeof(RvmDigitalTwin).GetField("full",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(twin);bins[1]=true;
            twin.InsertSpecimen(1);yield return Await(()=>twin.plc.uiHataKodu==308,8,"PLC full bin 308");Check(!twin.plc.xKapakAc,"Full bin not discharged");yield return Recover();
            phase="link_loss";twin.bridgeUrl="http://127.0.0.1:18766/exchange";
            yield return Await(()=>!twin.linkLive,4,"Unity link watchdog stopped motion");yield return new WaitForSecondsRealtime(1);
            Check(twin.sensors.rGercekBantHizi==0&&twin.sensors.rGercekKiriciDevri==0,"Link loss motion zero");
            twin.bridgeUrl=originalUrl;yield return Await(()=>twin.linkLive,12,"Link reconnected");
            Check(twin.plc.uiHataKodu==301&&!twin.plc.xCalismaIzni,"PLC 301 latched, no automatic restart");yield return Recover();
            yield return Cycle(3);
            phase="finish";twin.inlineMeasurement.noiseEnabled=false;twin.OperatorCommand(2);
            yield return Await(()=>!twin.plc.xCalismaIzni&&twin.plc.uiDurum==0,4,"Finished stopped without fault");
        }
        IEnumerator Extended()
        {
            phase="restart";yield return Await(()=>twin.linkLive&&twin.inlineMeasurement.ready,10,"Fresh Unity session and calibration");
            Check(!twin.plc.xCalismaIzni&&twin.sensors.udiGoruntuOnayi==0,"Restart does not reuse image or auto-start");
            yield return Recover();
            var original=twin.inlineMeasurement.customPrefab;
            var custom=Instantiate(twin.inlineMeasurement.petPrefab);custom.SetActive(false);
            custom.name="Never_Seen_Geometry_No_Class_Name";custom.transform.localScale*=.86f;custom.GetComponent<Rigidbody>().mass=.036f;
            // Scene template stands in for a newly imported physical prefab. Active clone is required.
            twin.inlineMeasurement.customPrefab=custom;
            yield return Cycle(0,false,1);
            Check(Mathf.Abs(twin.inlineMeasurement.reading.length-.213f*.86f)<.003f&&Mathf.Abs(twin.inlineMeasurement.reading.grams-36)<2,"New geometry and mass actually measured");
            custom.name="Renamed_Without_Material_Hint";yield return Cycle(0,false,1);
            custom.GetComponent<RvmConductiveBody>().propertiesKnown=false;yield return Cycle(0,false,0);
            custom.GetComponent<RvmConductiveBody>().propertiesKnown=true;custom.GetComponent<Rigidbody>().mass=1.2f;yield return Cycle(0,false,0);
            twin.inlineMeasurement.customPrefab=original;Destroy(custom);
            phase="moving_link_loss";twin.InsertSpecimen(2);yield return Await(()=>twin.plc.uiDurum==40,7,"Moving before disconnect");
            twin.bridgeUrl="http://127.0.0.1:18766/exchange";yield return Await(()=>!twin.linkLive,4,"Moving link watchdog");
            yield return new WaitForSecondsRealtime(1);Check(twin.sensors.rGercekBantHizi==0&&twin.sensors.rGercekKiriciDevri==0,"Moving actuators stopped on link loss");
            twin.bridgeUrl=originalUrl;yield return Await(()=>twin.linkLive,12,"Moving link restored");
            Check(twin.plc.uiHataKodu==301&&!twin.plc.xCalismaIzni,"Moving disconnect latched in PLC");yield return Recover();
            yield return Cycle(2);
            twin.OperatorCommand(2);yield return Await(()=>!twin.plc.xCalismaIzni&&twin.plc.uiDurum==0,4,"Extended finished stopped");
        }
        IEnumerator Endurance()
        {
            phase="endurance_start";yield return Await(()=>twin.inlineMeasurement.ready,8,"Empty calibration ready");yield return Recover();
            var before=Counts();var expected=new uint[4];
            for(int i=0;i<enduranceCycles;i++) {
                int material=i%10==9?0:i%3+1;
                yield return Cycle(material,i%2==1);expected[material]++;
                Log("PROGRESS "+(i+1)+"/"+enduranceCycles);
            }
            var after=Counts();for(int i=0;i<4;i++)Check(after[i]-before[i]==expected[i],"Batch exact counter "+i+" delta="+expected[i]);
            twin.inlineMeasurement.noiseEnabled=false;twin.OperatorCommand(2);
            yield return Await(()=>!twin.plc.xCalismaIzni&&twin.plc.uiDurum==0&&twin.plc.uiHataKodu==0,4,"Batch ended stopped with zero PLC fault");
        }
    }
}
