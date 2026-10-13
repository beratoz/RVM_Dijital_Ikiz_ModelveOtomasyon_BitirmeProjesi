using System.Collections;
using UnityEngine;

namespace RVM.Experimental
{
    // Measures the very same object travelling through the machine. No duplicate
    // specimen or class/profile values enter the measurement path.
    public sealed class RvmInlineMeasurement : MonoBehaviour
    {
        public RvmDigitalTwin twin;
        public RvmLoadCell scale;
        public RvmOpticalStation optical;
        public RvmInductiveProbe probe;
        public GameObject petPrefab, glassPrefab, aluminiumPrefab, customPrefab;
        public bool noiseEnabled;
        public bool ready, busy, complete;
        public string status="Empty calibration pending";
        public RvmMaterialDecision.Reading reading;
        public uint requestId, productId, plcSession, unitySession;
        public float measuredAt;
        Rigidbody activeBody;
        int generation;
        bool[] oldCollisions=new bool[32];

        IEnumerator Start()
        {
            for(int layer=0;layer<32;layer++) {
                oldCollisions[layer]=Physics.GetIgnoreLayerCollision(31,layer);
                Physics.IgnoreLayerCollision(31,layer,layer!=29);
            }
            yield return optical.CalibrateEmpty();
            ready=optical.calibrated;
            status=ready?"Measurement station ready":"Empty calibration failed; restart Play with empty station";
        }
        public GameObject Create(int profile,Vector3 position)
        {
            if(!ready)return null;
            var prefab=profile==1?petPrefab:profile==2?aluminiumPrefab:profile==3?glassPrefab:customPrefab;
            GameObject obj;
            if(prefab) {
                var body=prefab.GetComponent<Rigidbody>();
                if(!body||body.mass<=0||body.isKinematic||!body.useGravity||prefab.GetComponentsInChildren<Rigidbody>().Length!=1||prefab.GetComponentsInChildren<Collider>().Length==0) {
                    status="Prefab needs one dynamic root Rigidbody, gravity, positive mass and colliders";return null;
                }
                obj=Instantiate(prefab,position,prefab.transform.rotation);
            } else if(profile==0) {
                obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.localScale=new Vector3(.06f,.04f,.04f);obj.transform.position=position;
                obj.AddComponent<Rigidbody>().mass=.08f;
                obj.AddComponent<RvmConductiveBody>().propertiesKnown=false;
            } else return null;
            obj.name="Unlabelled_Physical_Product";
            obj.SetActive(true);
            foreach(var part in obj.GetComponentsInChildren<Transform>(true))part.gameObject.layer=31;
            var rb=obj.GetComponent<Rigidbody>();rb.isKinematic=true;rb.sleepThreshold=0;rb.solverIterations=12;rb.solverVelocityIterations=8;
            var guard=obj.GetComponent<RvmSupportMonitor>();if(!guard)guard=obj.AddComponent<RvmSupportMonitor>();
            guard.allowedPlatform=scale.GetComponent<Collider>();
            return obj;
        }
        public void Cancel()
        {
            generation++;complete=false;busy=false;reading=default;
            if(activeBody)activeBody.isKinematic=true;
            activeBody=null;
        }
        public void Request(GameObject obj,uint request,uint product,uint plc,uint unity)
        {
            if(busy||optical.busy||!ready)return;
            Cancel();requestId=request;productId=product;plcSession=plc;unitySession=unity;
            StartCoroutine(Measure(obj,generation));
        }
        bool Current(int token,GameObject obj)=>token==generation&&obj&&twin.linkLive&&twin.plc.uiDurum==30&&
            twin.plc.xCalismaIzni&&twin.plc.uiHataKodu==0&&!twin.sensors.xAcilStop&&!twin.sensors.xSikisma&&
            twin.sensors.udiUrunKimligi==productId&&twin.plc.udiPlcOturumu==plcSession;
        IEnumerator Measure(GameObject obj,int token)
        {
            busy=true;status="Settling on contact scale";scale.noiseEnabled=noiseEnabled;optical.pixelNoiseEnabled=noiseEnabled;
            activeBody=obj.GetComponent<Rigidbody>();
            obj.transform.position=new Vector3(-.55f,1.22f,0);
            activeBody.isKinematic=false;activeBody.linearVelocity=Vector3.zero;activeBody.angularVelocity=Vector3.zero;
            float deadline=Time.realtimeSinceStartup+2.5f;
            while(Current(token,obj)&&(!scale.valid||scale.ContactBody!=activeBody)&&Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
            if(!Current(token,obj)){if(token==generation)Cancel();yield break;}
            float before=scale.grams;
            bool stable=scale.valid&&scale.ContactBody==activeBody;
            status="Capturing black/white images";
            yield return optical.Measure();
            if(!Current(token,obj)){if(token==generation)Cancel();yield break;}
            probe.Sample(scale.ContactBody);
            reading=new RvmMaterialDecision.Reading {
                imageValid=optical.valid,scaleValid=stable&&scale.valid&&scale.ContactBody==activeBody&&Mathf.Abs(scale.grams-before)<=Mathf.Max(2,before*.02f),
                probeValid=probe.valid,length=optical.lengthMetres,width=optical.widthMetres,grams=scale.grams,
                transmission=optical.transmissionIndex,reflection=optical.reflectionIndex,metal=probe.signal,magnetic=probe.magneticSignal,
                ageSeconds=Mathf.Max(0,Time.realtimeSinceStartup-optical.capturedAt)
            };
            measuredAt=Time.realtimeSinceStartup;activeBody.isKinematic=true;activeBody=null;
            complete=true;busy=false;status="Measurement sent for PLC decision (no Unity acceptance)";
        }
        void Update()
        {
            if(busy&&(!twin.linkLive||twin.plc.uiDurum!=30||!twin.plc.xCalismaIzni||twin.sensors.xAcilStop||twin.sensors.xSikisma))Cancel();
        }
        public void CopyTo(UnitySensors s)
        {
            s.xOlcumTartiGecerli=s.xOlcumProbGecerli=false;
            s.rKutleGram=s.rBoyMetre=s.rOptikGecirgenlik=s.rOlcumYansima=s.rOlcumMetal=s.rOlcumManyetik=0;
            s.xMetalAlgilandi=false;
            s.xGoruntuGecerli=s.xGoruntuNesneVar=false;s.udiGoruntuOnayi=0;
            if(!complete||s.udiUrunKimligi!=productId||s.udiUnityOturumu!=unitySession||s.udiGorulenPlcOturumu!=plcSession)return;
            s.udiGoruntuOnayi=requestId;s.udiGoruntuUrunu=productId;s.udiGoruntuPlcOturumu=plcSession;s.udiGoruntuUnityOturumu=unitySession;
            s.xGoruntuGecerli=reading.imageValid;s.xGoruntuNesneVar=optical.present;
            s.rGoruntuBoy=reading.length;s.rGoruntuEn=reading.width;s.rGoruntuDoluluk=optical.fill;
            s.rGoruntuYasiMs=Mathf.Min(999999,(reading.ageSeconds+Mathf.Max(0,Time.realtimeSinceStartup-measuredAt))*1000);
            s.xOlcumTartiGecerli=reading.scaleValid;s.xOlcumProbGecerli=reading.probeValid;
            s.rKutleGram=reading.grams;s.rBoyMetre=reading.length;s.rOptikGecirgenlik=reading.transmission;
            s.rOlcumYansima=reading.reflection;s.rOlcumMetal=reading.metal;s.rOlcumManyetik=reading.magnetic;
            s.xMetalAlgilandi=reading.probeValid&&reading.metal>=.5f;
        }
        void OnDestroy()
        {
            for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(31,layer,oldCollisions[layer]);
        }
    }
}
