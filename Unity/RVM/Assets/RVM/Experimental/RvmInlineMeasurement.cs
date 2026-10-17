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
        public RvmProductCatalog catalog;
        public bool noiseEnabled;
        public bool ready, busy, complete;
        public string status="Empty calibration pending";
        public RvmOlcumVerisi reading;
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
            yield return PrepareGraphics();
            if(!graphicsReady)yield break;
            yield return optical.CalibrateEmpty();
            ready=optical.calibrated;
            status=ready?"Measurement station ready":"Empty calibration failed; restart Play with empty station";
        }
        bool graphicsReady;
        [System.Serializable] sealed class RenderPreparation {public string katalog;public int tur;public float siyahSaniye,beyazSaniye;public bool basarili;}
        IEnumerator PrepareGraphics()
        {
            graphicsReady=false;
            if(!catalog||catalog.entries.Length==0){status="Goruntu hazirligi: katalog eksik";yield break;}
#if UNITY_EDITOR
            bool oldAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
#endif
            GameObject sample=null;
            try {
                RvmTezKaydi.Ekle("grafik_hazirligi_basladi","{}");
                for(int i=0;i<catalog.entries.Length;i++) {
                    var entry=catalog.entries[i];
                    status="GORUNTU HAZIRLIGI "+(i+1)+"/"+catalog.entries.Length+" (urun testi degil)";
                    sample=Instantiate(entry.prefab,new Vector3(-.55f,1.22f,0),entry.prefab.transform.rotation);
                    sample.name="RenderPreparation_Only";
                    foreach(var rb in sample.GetComponentsInChildren<Rigidbody>())rb.isKinematic=true;
                    foreach(var c in sample.GetComponentsInChildren<Collider>())c.enabled=false;
                    foreach(var t in sample.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                    yield return null;
                    bool prepared=false;
                    for(int attempt=1;attempt<=3;attempt++) {
                        yield return optical.PrepareRender(Color.black);
                        bool black=optical.lastCaptureSucceeded;float blackTime=optical.lastCaptureSeconds;
                        yield return optical.PrepareRender(Color.white);
                        bool ok=black&&optical.lastCaptureSucceeded&&blackTime<.4f&&optical.lastCaptureSeconds<.4f;
                        RvmTezKaydi.Ekle("grafik_hazirligi",JsonUtility.ToJson(new RenderPreparation {katalog=entry.id,tur=attempt,siyahSaniye=blackTime,beyazSaniye=optical.lastCaptureSeconds,basarili=ok}));
                        if(ok){prepared=true;break;}
                        yield return null;
                    }
                    Destroy(sample);sample=null;yield return null;
                    if(!prepared){status="GORUNTU HAZIRLIGI BASARISIZ: "+entry.id+"; urun gonderme";yield break;}
                }
                graphicsReady=true;
                RvmTezKaydi.Ekle("grafik_hazirligi_tamam","{}");
            } finally {
                if(sample)Destroy(sample);
#if UNITY_EDITOR
                UnityEditor.ShaderUtil.allowAsyncCompilation=oldAsync;
#endif
            }
        }
        public GameObject Create(int index,Vector3 position)
        {
            if(!ready)return null;
            if(!catalog||index<0||index>=catalog.entries.Length||!catalog.entries[index].prefab) {
                status="24 urun katalogu veya secimi eksik";return null;
            }
            var prefab=catalog.entries[index].prefab;
            var body=prefab.GetComponent<Rigidbody>();
            if(!body||body.mass<=0||body.isKinematic||!body.useGravity||prefab.GetComponentsInChildren<Rigidbody>().Length!=1||prefab.GetComponentsInChildren<Collider>().Length==0) {
                status="Prefab needs one dynamic root Rigidbody, gravity, positive mass and colliders";return null;
            }
            var obj=Instantiate(prefab,position,prefab.transform.rotation);
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
            if(busy)RvmTezKaydi.Ekle("olcum_iptal","{}");
            generation++;complete=false;busy=false;reading=default;
            if(activeBody)activeBody.isKinematic=true;
            activeBody=null;
        }
        public void Request(GameObject obj,uint request,uint product,uint plc,uint unity)
        {
            if(busy||optical.busy||!ready)return;
            Cancel();requestId=request;productId=product;plcSession=plc;unitySession=unity;
            RvmTezKaydi.Istek(request);
            RvmTezKaydi.Ekle("olcum_konumu",JsonUtility.ToJson(new OlcumKonumu {konum=obj.transform.position,donus=obj.transform.rotation,olcek=obj.transform.localScale}));
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
            RvmTezKaydi.Ekle("cekim_konumu",JsonUtility.ToJson(new OlcumKonumu {konum=obj.transform.position,donus=obj.transform.rotation,olcek=obj.transform.localScale}));
            yield return optical.Measure();
            if(!Current(token,obj)){if(token==generation)Cancel();yield break;}
            probe.Sample(scale.ContactBody);
            RvmTezKaydi.Ekle("kabuk_gozlem",JsonUtility.ToJson(RvmKabukGozlemi.Hesapla(scale.grams,optical.lengthMetres,optical.widthMetres,optical.fill)));
            reading=new RvmOlcumVerisi {
                imageValid=optical.valid,scaleValid=stable&&scale.valid&&scale.ContactBody==activeBody&&Mathf.Abs(scale.grams-before)<=Mathf.Max(2,before*.02f),
                probeValid=probe.valid,length=optical.lengthMetres,width=optical.widthMetres,grams=scale.grams,
                transmission=optical.transmissionIndex,reflection=optical.reflectionIndex,metal=probe.signal,magnetic=probe.magneticSignal,
                ageSeconds=Mathf.Max(0,Time.realtimeSinceStartup-optical.capturedAt)
            };
            measuredAt=Time.realtimeSinceStartup;activeBody.isKinematic=true;activeBody=null;
            RvmTezKaydi.Ekle("olcum_sonuc",JsonUtility.ToJson(reading));
            RvmTezKaydi.Ekle("prob_sonuc",JsonUtility.ToJson(new ProbSonuc {gecerli=probe.valid,metal=probe.signal,manyetik=probe.magneticSignal,menzilMetre=probe.rangeMetres,neden=probe.reason}));
            complete=true;busy=false;status="Measurement sent for PLC decision (no Unity acceptance)";
        }
        [System.Serializable] sealed class OlcumKonumu {public Vector3 konum,olcek;public Quaternion donus;}
        [System.Serializable] sealed class ProbSonuc {public bool gecerli;public float metal,manyetik,menzilMetre;public string neden;}
        [System.Serializable] sealed class TartiOrnegi {public float gram,sapmaGram,kuvvetNewton,adimSaniye,gurultuGenligi;public int temas,pencere,gurultuTohumu;public bool gecerli,gurultu;public string neden;}
        void FixedUpdate()
        {
            if(busy) RvmTezKaydi.Ekle("tarti_ornegi",JsonUtility.ToJson(new TartiOrnegi {gram=scale.grams,sapmaGram=scale.standardDeviation,kuvvetNewton=scale.forceNewtons,adimSaniye=Time.fixedDeltaTime,temas=scale.contactBodies,pencere=scale.windowSamples,gecerli=scale.valid,neden=scale.reason,gurultu=scale.noiseEnabled,gurultuGenligi=scale.noiseAmplitudeGrams,gurultuTohumu=scale.noiseSeed}));
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
