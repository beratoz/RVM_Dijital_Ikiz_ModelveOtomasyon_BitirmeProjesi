using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RVM.Experimental
{
    public sealed class RvmPacketSmokeTest : MonoBehaviour
    {
        public RvmMeasurementLab lab;
        public string reportPath;
        public bool complete, passed=true;
        string report="v106 short sensor / packet check\n";
        void Check(bool ok,string text) { passed &= ok; report+=(ok?"PASS ":"FAIL ")+text+"\n"; }
        public IEnumerator Start()
        {
            lab.ClearSpecimen();yield return new WaitForSeconds(.3f);
            yield return lab.optical.CalibrateEmpty();
            uint previousProduct=0,previousRequest=0;
            string session=null;
            int[] expected={1,3,2};
            for(int i=0;i<3;i++) {
                lab.importedPrefab=lab.blindTestPrefabs[i];
                Check(lab.LoadImported(),"Load specimen "+i);
                yield return new WaitForSeconds(2);
                yield return lab.MeasureAndClassify();
                Check(lab.decision.materialCode==expected[i],"Candidate "+expected[i]+" actual "+lab.decision.materialCode);
                bool available=lab.TryCreateMeasurementPacket(out var json);
                Check(available,"Fresh packet "+i);
                if(available) {
                    var packet=JsonUtility.FromJson<RvmMeasurementPacket>(json);
                    Check(packet.contractVersion==106 && packet.productId>previousProduct && packet.requestId>previousRequest &&
                        (session==null||session==packet.sessionId),"Session / product / request consistency");
                    session=packet.sessionId;previousProduct=packet.productId;previousRequest=packet.requestId;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"packet_"+i+".json"),json);
                }
                lab.ClearSpecimen();
                Check(!lab.TryCreateMeasurementPacket(out _),"Removed product cannot export");
                yield return new WaitForSeconds(.2f);
            }
            var reading=new RvmMaterialDecision.Reading{imageValid=true,scaleValid=true,probeValid=true,ageSeconds=3};
            bool staleRejected=false;
            try{RvmMeasurementPacket.Create("test",1,1,reading);}catch(InvalidOperationException){staleRejected=true;}
            Check(staleRejected,"Stale packet rejected");
            reading.ageSeconds=0;reading.grams=float.NaN;bool nanRejected=false;
            try{RvmMeasurementPacket.Create("test",1,1,reading);}catch(ArgumentException){nanRejected=true;}
            Check(nanRejected,"NaN packet rejected");
            report+=passed?"RESULT PASS\n":"RESULT FAIL\n";
            File.WriteAllText(reportPath,report);complete=true;
        }
    }
}
