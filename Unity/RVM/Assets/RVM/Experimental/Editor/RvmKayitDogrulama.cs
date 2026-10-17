using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RVM.Experimental
{
    public static class RvmKayitDogrulama
    {
        public static void Calistir()
        {
            var root=Application.persistentDataPath;
            var output=Path.Combine(root,"Dogrulama","Kayit_Dogrulama");Directory.CreateDirectory(output);
            try {
                RvmTeslimDogrulama.Calistir();
                EditorSceneManager.OpenScene("Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity");
                var station=UnityEngine.Object.FindFirstObjectByType<RvmInlineMeasurement>();
                if(!station||!station.optical||!station.scale||!station.probe||station.catalog.entries.Length!=24)
                    throw new Exception("Scene/station/catalog references missing");
                // File pipeline test only. Never enters Play or communicates with the PLC.
                RvmTezKaydi.DogrulamaKaynakKapisi=new System.Threading.ManualResetEventSlim(false);
                RvmTezKaydi.Baslat();
                RvmTezKaydi.Ekle("altyapi_testi","{\"gercek_deney\":false}");
                RvmTezKaydi.Goruntu("test_2x2",new[]{new Color32(255,0,0,255),new Color32(0,255,0,255),new Color32(0,0,255,255),new Color32(255,255,255,255)},2,2);
                var dir=RvmTezKaydi.Dizin;
                // Hold source preparation until all live events have been produced.
                for(int i=0;i<3000;i++) {
                    RvmTezKaydi.Ekle("yuk_testi","{\"index\":"+i+"}");
                    if(i%300==0)RvmTezKaydi.Goruntu("yuk_testi",new Color32[800*400],800,400);
                    if(i%10==0)System.Threading.Thread.Sleep(2);
                }
                if(File.Exists(Path.Combine(dir,"kaynak_kopyasi_tamam.txt")))throw new Exception("Source gate did not hold preparation");
                if(new FileInfo(Path.Combine(dir,"olaylar.jsonl")).Length<1000)throw new Exception("Live writer blocked by source preparation");
                RvmTezKaydi.DogrulamaKaynakKapisi.Set();
                var deadline=DateTime.UtcNow.AddSeconds(45);
                while(!File.Exists(Path.Combine(dir,"kaynak_kopyasi_tamam.txt"))&&DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(50);
                RvmTezKaydi.Kapat();
                RvmTezKaydi.DogrulamaKaynakKapisi.Dispose();RvmTezKaydi.DogrulamaKaynakKapisi=null;
                if(!File.Exists(Path.Combine(dir,"kayit_kapandi.txt"))||!File.Exists(Path.Combine(dir,"kaynak_kopyasi_tamam.txt")))
                    throw new Exception("Recorder did not flush cleanly");
                long expected=1;int stress=0,images=0;
                foreach(var line in File.ReadLines(Path.Combine(dir,"olaylar.jsonl"))) {
                    var row=JsonUtility.FromJson<TestRow>(line);
                    if(row.sira!=expected++)throw new Exception("Event sequence gap");
                    if(row.tur=="yuk_testi")stress++;
                    if(row.tur=="goruntu") {
                        var frame=JsonUtility.FromJson<TestFrame>(row.veri);
                        if(new FileInfo(Path.Combine(dir,frame.dosya)).Length!=frame.en*frame.boy*4)throw new Exception("Incomplete raw image");
                        images++;
                    }
                }
                if(stress!=3000||images!=11)throw new Exception("Missing stress event/image");
                Directory.Move(dir,Path.Combine(output,Path.GetFileName(dir)));
                File.WriteAllText(Path.Combine(output,"unity_derleme_kayit_testi.txt"),"PASS: 3000 live events + 11 raw images during delayed source hashing; no sequence gaps; clean shutdown; scene and 24 catalog entries valid. No PLC connection; no product test.");
                Debug.Log("TEZ_PREFLIGHT_PASS");
                EditorApplication.Exit(0);
            } catch(Exception e) {
                RvmTezKaydi.DogrulamaKaynakKapisi?.Set();
                RvmTezKaydi.Kapat(); File.WriteAllText(Path.Combine(output,"unity_derleme_kayit_testi.txt"),e.ToString());
                Debug.LogException(e);EditorApplication.Exit(1);
            }
        }
        [Serializable] sealed class TestRow {public long sira;public string tur,veri;}
        [Serializable] sealed class TestFrame {public string dosya;public int en,boy;}
    }
}
