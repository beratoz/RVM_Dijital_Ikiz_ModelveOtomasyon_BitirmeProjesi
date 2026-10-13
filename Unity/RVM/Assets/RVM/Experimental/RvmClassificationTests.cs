using System.Collections;
using System.IO;
using UnityEngine;
namespace RVM.Experimental
{
    public sealed class RvmClassificationTests:MonoBehaviour
    {
        public RvmMeasurementLab lab;
        public string reportPath;
        public bool complete,passed=true;
        string report="v106 experimental sensor fusion tests\n";
        void Check(bool ok,string text){passed&=ok;report+=(ok?"PASS ":"FAIL ")+text+"\n";File.WriteAllText(reportPath,report);}
        IEnumerator Load(int i)
        {
            lab.testing=false;lab.importedPrefab=lab.blindTestPrefabs[i];Check(lab.LoadImported(),"Load "+i);lab.testing=true;
            yield return new WaitForSeconds(2);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));lab.testing=true;lab.ClearSpecimen();
            yield return new WaitForSeconds(.2f);yield return lab.optical.CalibrateEmpty();
            for(int i=0;i<3;i++)
            {
                yield return Load(i);yield return lab.MeasureAndClassify();
                int expected=i==0?1:i==1?3:2;
                Check(lab.decision.materialCode==expected,"Nominal Blender "+i+" -> "+lab.decision.materialCode+"; "+JsonUtility.ToJson(lab.lastReading));
            }
            lab.scale.noiseEnabled=true;lab.optical.pixelNoiseEnabled=true;
            for(int i=0;i<3;i++)
            {
                yield return Load(i);
                for(int n=0;n<3;n++)
                {
                    yield return lab.MeasureAndClassify();int expected=i==0?1:i==1?3:2;
                    Check(lab.decision.materialCode==expected,"Noisy repeat "+i+"/"+n+" -> "+lab.decision.materialCode+"; grams="+lab.scale.grams+" length="+lab.optical.lengthMetres);
                    yield return new WaitForSeconds(.1f);
                }
            }
            lab.scale.noiseEnabled=false;lab.optical.pixelNoiseEnabled=false;
            for(int i=0;i<3;i++)
            {
                yield return Load(i);
                // Held-out dimensional/physical variants, not additional rule-fitting samples.
                lab.CurrentSpecimen.transform.localScale=Vector3.one*.86f;
                lab.CurrentSpecimen.GetComponent<Rigidbody>().mass=i==0?.036f:i==1?.190f:.024f;
                yield return new WaitForSeconds(2);yield return lab.MeasureAndClassify();
                Check(lab.decision.materialCode==(i==0?1:i==1?3:2),"Held-out scaled/weight variant "+i+" -> "+lab.decision.materialCode);
            }
            yield return Load(2);lab.CurrentSpecimen.GetComponent<RvmConductiveBody>().magneticResponse=1;
            yield return lab.MeasureAndClassify();Check(lab.decision.materialCode==0,"Magnetic metal cannot become aluminium");
            yield return Load(0);lab.CurrentSpecimen.GetComponent<RvmConductiveBody>().propertiesKnown=false;
            yield return lab.MeasureAndClassify();Check(lab.decision.materialCode==0,"Missing electrical properties reject instead of assuming nonmetal");
            yield return Load(0);lab.CurrentSpecimen.GetComponent<Rigidbody>().mass=.090f;
            yield return new WaitForSeconds(1);yield return lab.MeasureAndClassify();Check(lab.decision.materialCode==0,"Ambiguous 90 g object rejected");
            yield return Load(0);lab.CurrentSpecimen.transform.rotation=Quaternion.Euler(0,35,0);
            yield return new WaitForSeconds(1);yield return lab.MeasureAndClassify();Check(lab.decision.materialCode==0,"Rotated object outside width range rejected");
            yield return Load(0);
            var extra=Instantiate(lab.blindTestPrefabs[0],new Vector3(0,.04f,.075f),Quaternion.identity);
            foreach(var part in extra.GetComponentsInChildren<Transform>())part.gameObject.layer=31;
            extra.GetComponent<Rigidbody>().sleepThreshold=0;
            yield return new WaitForSeconds(2);Check(!lab.scale.valid,"Multiple platform objects invalidate weighing");
            extra.SetActive(false);Destroy(extra);
            yield return Load(0);
            var support=GameObject.CreatePrimitive(PrimitiveType.Cube);support.name="External support fault fixture";
            support.transform.position=new Vector3(.07f,.005f,0);support.transform.localScale=new Vector3(.06f,.02f,.1f);
            yield return new WaitForSeconds(2);Check(!lab.scale.valid,"External support contact invalidates weighing");
            support.SetActive(false);Destroy(support);
            yield return Load(0);yield return lab.MeasureAndClassify();
            var stale=lab.lastReading;stale.ageSeconds=3;Check(RvmMaterialDecision.Evaluate(stale).materialCode==0,"Stale snapshot rejected");
            stale.ageSeconds=0;stale.grams=float.NaN;Check(RvmMaterialDecision.Evaluate(stale).materialCode==0,"Non-finite data rejected");
            lab.ClearSpecimen();yield return new WaitForSeconds(.2f);Check(lab.decision.materialCode==0&&!lab.scale.valid,"Removal clears decision and scale");
            report+=(passed?"RESULT PASS":"RESULT FAIL")+"\n";File.WriteAllText(reportPath,report);lab.testing=false;complete=true;
        }
    }
}
