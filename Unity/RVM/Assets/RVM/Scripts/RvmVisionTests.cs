using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RVM
{
    // Explicit temporary test fixture. Never attached to the saved production scene.
    public class RvmVisionTests : MonoBehaviour
    {
        public string outputDirectory;
        public bool complete,passed;
        RvmVisionCamera cameraRig;
        GameObject specimen;
        string report="";
        void Check(bool condition,string message)
        {
            passed &= condition; report+=(condition?"PASS ":"FAIL ")+message+"\n";
            File.WriteAllText(Path.Combine(outputDirectory,"image_tests.txt"),report);
        }
        IEnumerator Capture(uint request,string name)
        {
            cameraRig.Request(request,1,1,1);
            float until=Time.realtimeSinceStartup+2;
            while(!cameraRig.complete&&Time.realtimeSinceStartup<until)yield return null;
            Check(cameraRig.complete,name+" frame completed");
            File.WriteAllBytes(Path.Combine(outputDirectory,name+".png"),cameraRig.preview.EncodeToPNG());
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);passed=true;
            cameraRig=gameObject.AddComponent<RvmVisionCamera>();
            yield return Capture(1,"empty");
            Check(cameraRig.measurement.valid&&!cameraRig.measurement.present,"Empty rendered image does not invent a specimen");
            specimen=GameObject.CreatePrimitive(PrimitiveType.Cube);
            specimen.name="Vision_TestFixture";specimen.layer=RvmVisionCamera.SpecimenLayer;
            specimen.transform.position=new Vector3(-.55f,1.17f,0);
            specimen.transform.localScale=new Vector3(.2f,.04f,.04f);
            yield return Capture(2,"calibration_200x40mm");
            var m=cameraRig.measurement;
            Check(m.valid&&m.present&&Mathf.Abs(m.length-.2f)<.004f&&Mathf.Abs(m.width-.04f)<.004f,
                "Rendered calibration size: "+m.length+" x "+m.width+" m, "+m.reason);
            specimen.transform.localScale=new Vector3(.06f,.04f,.04f);
            yield return Capture(3,"short_60mm");
            m=cameraRig.measurement;
            Check(m.valid&&m.present&&Mathf.Abs(m.length-.06f)<.004f,"Pixels follow changed geometry, independent of profile");
            specimen.transform.position=new Vector3(-.28f,1.17f,0);
            yield return Capture(4,"clipped");
            Check(!cameraRig.measurement.valid,"Clipped image invalid");
            Destroy(specimen);specimen=null;yield return null;
            Check(!RvmImageAnalysis.Analyze(null,512,256,.56f).valid,"Missing pixel buffer invalid");
            report+=(passed?"RESULT PASS":"RESULT FAIL")+"\n";
            File.WriteAllText(Path.Combine(outputDirectory,"image_tests.txt"),report);
            Destroy(cameraRig);complete=true;
        }
        void OnDestroy(){if(specimen)Destroy(specimen);if(cameraRig)Destroy(cameraRig);}
    }
}
