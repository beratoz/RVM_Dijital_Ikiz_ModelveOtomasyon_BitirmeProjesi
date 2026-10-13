using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RVM.Experimental
{
    public sealed class RvmMeasurementLab : MonoBehaviour
    {
        public RvmLoadCell scale;
        public RvmOpticalStation optical;
        public RvmInductiveProbe metalProbe;
        public RvmMaterialDecision.Decision decision;
        public RvmMaterialDecision.Reading lastReading;
        public GameObject CurrentSpecimen=>specimen;
        public float decisionTime;
        [Tooltip("An arbitrary physical prefab: Rigidbody mass, renderer/material and colliders must be supplied. No class label required.")]
        public GameObject importedPrefab;
        public GameObject[] blindTestPrefabs;
        public bool testing, complete, passed;
        public string reportPath;
        public string status = "Independent calibration lab. No PLC connection.";
        GameObject specimen;
        string report = "";
        bool previousBackground;
        int selectedTestPrefab;
        readonly string measurementSession = Guid.NewGuid().ToString("N");
        uint productSequence, requestSequence, completedRequest;
        Rigidbody measuredBody;
        double measurementCompletedAt;

        public bool TryCreateMeasurementPacket(out string json)
        {
            json=null;
            if(!specimen || !measuredBody || measuredBody!=scale.ContactBody || !scale.valid ||
                optical.busy || completedRequest==0) return false;
            var reading=lastReading;
            reading.ageSeconds+=(float)(Time.realtimeSinceStartupAsDouble-measurementCompletedAt);
            if(Mathf.Abs(scale.grams-reading.grams)>Mathf.Max(2,reading.grams*.02f))return false;
            try {
                json=JsonUtility.ToJson(RvmMeasurementPacket.Create(measurementSession,productSequence,completedRequest,reading),true);
                return true;
            } catch(ArgumentException) { return false; }
              catch(InvalidOperationException) { return false; }
        }
        void Awake()
        {
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            if(!metalProbe)
            {
                var probe=new GameObject("Simulated_Inductive_And_Magnetic_Probe");probe.transform.SetParent(transform);
                probe.transform.position=new Vector3(0,-.01f,0);metalProbe=probe.AddComponent<RvmInductiveProbe>();
            }
        }
        void Update()
        {
            if(metalProbe)metalProbe.Sample(scale.ContactBody);
            if(decision.materialCode!=0&&(!specimen||!scale.valid||Time.realtimeSinceStartup-decisionTime>2))
                decision=new RvmMaterialDecision.Decision{reason="Measurement expired or object moved; measure again"};
        }

        Material FixtureMaterial(float alpha)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor",new Color(.35f,.6f,.8f,alpha));
            m.SetFloat("_Metallic",0); m.SetFloat("_Smoothness",.3f);
            if (alpha<1)
            {
                m.SetFloat("_Surface",1); m.SetFloat("_Blend",0); m.SetFloat("_ZWrite",0);
                m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType","Transparent"); m.renderQueue=3000;
            }
            return m;
        }
        public void ClearSpecimen()
        {
            completedRequest=0; measuredBody=null;
            checked { productSequence++; }
            if(specimen) { specimen.SetActive(false); Destroy(specimen); }
            specimen=null;
            decision=new RvmMaterialDecision.Decision{reason="No current product"};
            optical.valid=false;
        }
        void Fixture(float grams, float length=.2f,float width=.04f,float alpha=1)
        {
            ClearSpecimen();
            specimen=GameObject.CreatePrimitive(PrimitiveType.Cube);
            specimen.name="Unlabelled_Calibration_Object"; specimen.layer=31;
            specimen.transform.position=new Vector3(0,.035f,0);
            specimen.transform.localScale=new Vector3(length,.04f,width);
            specimen.GetComponent<Renderer>().sharedMaterial=FixtureMaterial(alpha);
            var body=specimen.AddComponent<Rigidbody>();
            // Ground truth belongs only to this physical test fixture, not to either sensor.
            body.mass=grams/1000f; body.sleepThreshold=0;
            body.solverIterations=12; body.solverVelocityIterations=8;
            specimen.AddComponent<RvmSupportMonitor>().allowedPlatform=scale.GetComponent<Collider>();
        }
        public bool LoadImported()
        {
            if(testing || !importedPrefab) { status="Select a physical prefab in the Inspector."; return false; }
            var rb=importedPrefab.GetComponent<Rigidbody>();
            if(!rb || rb.isKinematic || !rb.useGravity || rb.mass<=0 || importedPrefab.GetComponentsInChildren<Collider>().Length==0)
            { status="Prefab needs dynamic Rigidbody, gravity, positive physical mass and colliders."; return false; }
            if(importedPrefab.GetComponentsInChildren<Rigidbody>().Length!=1)
            { status="Use one Rigidbody per product."; return false; }
            ClearSpecimen(); specimen=Instantiate(importedPrefab,new Vector3(0,.12f,0),importedPrefab.transform.rotation);
            specimen.name="Imported_Unlabelled_Object";
            foreach(var part in specimen.GetComponentsInChildren<Transform>(true))part.gameObject.layer=31;
            specimen.GetComponent<Rigidbody>().sleepThreshold=0;
            var guard=specimen.GetComponent<RvmSupportMonitor>();if(!guard)guard=specimen.AddComponent<RvmSupportMonitor>();
            guard.allowedPlatform=scale.GetComponent<Collider>();
            status="Imported object: no material/class code read. Wait for a stable scale."; return true;
        }
        public IEnumerator MeasureAndClassify()
        {
            // Do not invalidate an in-flight request by starting a second capture.
            if(optical.busy)yield break;
            completedRequest=0; measuredBody=null;
            uint requestedProduct=productSequence;
            uint request=checked(++requestSequence);
            decision=new RvmMaterialDecision.Decision{reason="Measurement pending"};
            if(!scale.valid||optical.busy||!specimen){status="Stable single product required";yield break;}
            var body=scale.ContactBody;float beforeMass=scale.grams;
            yield return optical.Measure();
            metalProbe.Sample(scale.ContactBody);
            bool same=requestedProduct==productSequence&&body&&body==scale.ContactBody&&specimen&&body.gameObject==specimen;
            lastReading=new RvmMaterialDecision.Reading{
                imageValid=optical.valid,scaleValid=scale.valid&&same&&Mathf.Abs(scale.grams-beforeMass)<=Mathf.Max(2,beforeMass*.02f),probeValid=metalProbe.valid,
                length=optical.lengthMetres,width=optical.widthMetres,grams=scale.grams,transmission=optical.transmissionIndex,
                reflection=optical.reflectionIndex,metal=metalProbe.signal,magnetic=metalProbe.magneticSignal,ageSeconds=Time.realtimeSinceStartup-optical.capturedAt};
            decision=RvmMaterialDecision.Evaluate(lastReading);decisionTime=Time.realtimeSinceStartup;
            if(same) {
                measuredBody=body;completedRequest=request;measurementCompletedAt=Time.realtimeSinceStartupAsDouble;
            }
            status=decision.reason;
        }
        void Check(bool condition,string message)
        {
            passed &= condition; report+=(condition?"PASS ":"FAIL ")+message+"\n";
            File.WriteAllText(reportPath,report);
        }
        public void RunTests(string path)
        {
            if(testing)return;
            reportPath=path; Directory.CreateDirectory(Path.GetDirectoryName(path));
            StartCoroutine(TestSequence());
        }
        public void RunImportedTests(string path)
        {
            if(testing)return;
            reportPath=path;Directory.CreateDirectory(Path.GetDirectoryName(path));
            StartCoroutine(ImportedSequence());
        }
        IEnumerator ImportedSequence()
        {
            complete=false;passed=true;report="New Blender models: independent sensor measurements\n";
            ClearSpecimen();yield return new WaitForSeconds(.3f);yield return optical.CalibrateEmpty();
            // These references are assertions only, never inputs to the sensors.
            float[] lengths={.213f,.247f,.127f}, widths={.056f,.068f,.066f}, masses={28,235,17};
            for(int i=0;i<blindTestPrefabs.Length;i++)
            {
                importedPrefab=blindTestPrefabs[i];testing=false;bool loaded=LoadImported();testing=true;
                Check(loaded,"Imported anonymous prefab "+i);
                yield return new WaitForSeconds(3);
                yield return optical.Measure();
                Check(optical.valid&&Mathf.Abs(optical.lengthMetres-lengths[i])<=.003f&&Mathf.Abs(optical.widthMetres-widths[i])<=.003f,
                    "Image "+i+": "+optical.lengthMetres.ToString("F4")+" x "+optical.widthMetres.ToString("F4")+" m, "+optical.reason);
                Check(scale.valid&&Mathf.Abs(scale.grams-masses[i])<=Mathf.Max(2,masses[i]*.02f),
                    "Contact scale "+i+": "+scale.grams.ToString("F3")+" g, "+scale.reason);
                report+="OPTICAL "+i+" transmission="+optical.transmissionIndex.ToString("F3")+" reflection="+optical.reflectionIndex.ToString("F3")+"\n";
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(reportPath),"blender_"+i+".png"),optical.preview.EncodeToPNG());
                ClearSpecimen();yield return new WaitForSeconds(.3f);
            }
            report+=(passed?"RESULT PASS":"RESULT FAIL")+"\n";File.WriteAllText(reportPath,report);
            testing=false;complete=true;status=passed?"New Blender model measurements passed. Material classifier pending.":"Imported model measurement failed.";
        }
        IEnumerator TestSequence()
        {
            testing=true;complete=false;passed=true;report="v106 isolated measurement calibration\n";
            scale.noiseEnabled=false;ClearSpecimen(); yield return new WaitForSeconds(.3f);
            yield return optical.CalibrateEmpty(); Check(optical.calibrated,"Empty optical references captured");
            yield return optical.Measure(); Check(!optical.present&&!optical.valid,"Empty image does not create a product");
            Check(!scale.valid,"Empty scale invalid for product weighing");
            foreach(float reference in new[]{5f,22f,180f,1000f})
            {
                Fixture(reference); yield return new WaitForSeconds(2.0f);
                Check(scale.valid&&Mathf.Abs(scale.grams-reference)<=Mathf.Max(2,reference*.02f),
                    "Force scale reference="+reference+" g measured="+scale.grams.ToString("F3")+" g sd="+scale.standardDeviation.ToString("F3")+" "+scale.reason);
            }
            yield return optical.Measure();
            Check(optical.valid&&Mathf.Abs(optical.lengthMetres-.2f)<=.003f&&Mathf.Abs(optical.widthMetres-.04f)<=.003f,
                "Opaque image dimensions "+optical.lengthMetres.ToString("F4")+" x "+optical.widthMetres.ToString("F4")+" m; "+optical.reason);
            float opaqueTransmission=optical.transmissionIndex;
            Check(opaqueTransmission<.1f,"Opaque transmission index="+opaqueTransmission.ToString("F3"));
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(reportPath),"opaque.png"),optical.preview.EncodeToPNG());
            Fixture(22,.2f,.04f,.25f); yield return new WaitForSeconds(2);
            yield return optical.Measure();
            Check(optical.valid&&optical.transmissionIndex>opaqueTransmission+.2f,
                "Transparent render index="+optical.transmissionIndex.ToString("F3")+"; dimension validity="+optical.valid);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(reportPath),"transparent.png"),optical.preview.EncodeToPNG());
            Fixture(83,.237f,.063f); yield return new WaitForSeconds(2); yield return optical.Measure();
            Check(optical.valid&&Mathf.Abs(optical.lengthMetres-.237f)<=.003f&&Mathf.Abs(optical.widthMetres-.063f)<=.003f&&Mathf.Abs(scale.grams-83)<=2,
                "New geometry/mass: "+optical.lengthMetres.ToString("F4")+" m x "+optical.widthMetres.ToString("F4")+" m, "+scale.grams.ToString("F3")+" g");
            specimen.transform.position=new Vector3(.19f,.03f,0); yield return new WaitForSeconds(.2f); yield return optical.Measure();
            Check(!optical.valid,"Clipped product rejected: "+optical.reason);
            Fixture(1200); yield return new WaitForSeconds(2); Check(!scale.valid,"Over-range 1200 g rejected");
            Fixture(22,.06f); yield return new WaitForSeconds(2); yield return optical.Measure();
            Check(!optical.valid,"Below-range 60 mm object rejected");
            ClearSpecimen();yield return new WaitForSeconds(.3f);Check(!scale.valid&&scale.grams==0,"Removing object clears stale scale result");
            report+=(passed?"RESULT PASS":"RESULT FAIL")+"\n";File.WriteAllText(reportPath,report);
            status=passed?"Calibration tests passed. Classification is not calibrated yet.":"Calibration failed; see report.";
            testing=false;complete=true;
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16,16,440,650),GUI.skin.box);
            GUILayout.Label("RVM v106 | DENEYSEL OLCUM LABORATUVARI");
            GUILayout.Label("PLC baglantisi yok / Deneysel kural tabanli malzeme ADAYI");
            GUILayout.Label(status);
            GUILayout.Label("Tarti: "+scale.grams.ToString("F2")+" g | sigma "+scale.standardDeviation.ToString("F2")+" | "+scale.reason);
            GUILayout.Label("Kamera: "+(optical.lengthMetres*1000).ToString("F1")+" x "+(optical.widthMetres*1000).ToString("F1")+" mm | "+optical.reason);
            GUILayout.Label("Gecirgenlik gostergesi: "+optical.transmissionIndex.ToString("F3")+" / Yansima gostergesi: "+optical.reflectionIndex.ToString("F3"));
            GUILayout.Label("Boy 100–300 mm / en 20–90 mm / tarti 5–1000 g");
            GUILayout.Label("Metal: "+metalProbe.signal.ToString("F2")+" / Manyetik: "+metalProbe.magneticSignal.ToString("F2")+" / "+metalProbe.reason);
            GUILayout.Label("ADAY: "+RvmDigitalTwin.MaterialName(decision.materialCode)+" | "+decision.reason);
            GUI.enabled=!testing&&!optical.busy;
            if(GUILayout.Button("1. BOS ISTASYONU KALIBRE ET")&&!specimen)StartCoroutine(optical.CalibrateEmpty());
            if(blindTestPrefabs!=null&&blindTestPrefabs.Length==3)
            {
                int selected=GUILayout.SelectionGrid(selectedTestPrefab,new[]{"B101","B102","B103"},3);
                if(selected!=selectedTestPrefab){selectedTestPrefab=selected;importedPrefab=blindTestPrefabs[selected];}
            }
            if(GUILayout.Button("2. INSPECTOR PREFABINI TARTIYA KOY"))LoadImported();
            if(GUILayout.Button("3. GORUNTUDEN OLC"))
            {
                if(scale.valid)StartCoroutine(MeasureAndClassify());
                else status="Wait for a stable, in-range scale before paired imaging.";
            }
            if(GUILayout.Button("NESNEYI KALDIR"))ClearSpecimen();
            scale.noiseEnabled=GUILayout.Toggle(scale.noiseEnabled,"Tekrarlanabilir tarti gurultusu (+/-0.5 g)");
            optical.pixelNoiseEnabled=GUILayout.Toggle(optical.pixelNoiseEnabled,"Kamera piksel gurultusu (+/-1 / 255)");
            GUI.enabled=true;
            if(optical.preview)GUILayout.Label(optical.preview,GUILayout.Width(400),GUILayout.Height(200));
            GUILayout.EndArea();
        }
        void OnDestroy(){ClearSpecimen();Application.runInBackground=previousBackground;}
    }
}
