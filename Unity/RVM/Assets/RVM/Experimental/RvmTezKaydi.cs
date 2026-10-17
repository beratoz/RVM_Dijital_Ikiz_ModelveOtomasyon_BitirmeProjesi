using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace RVM.Experimental
{
    // Observation only: never changes sensors, commands or classification inputs.
    public static class RvmTezKaydi
    {
        [Serializable] sealed class Olay
        {
            public string utc, tur, katalog, veri;
            public long sira;
            public double saniye;
            public uint unity, plc, urun, istek;
        }
        [Serializable] sealed class Kare
        {
            public string dosya, bicim="RGBA32", satirSirasi="Unity bottom-to-top";
            public int en, boy;
        }
        static BlockingCollection<Action> kuyruk;
        static Thread yazici, kaynakYazici;
        static StreamWriter olaylar;
        static string dizin, katalog="";
        static uint unity, plc, urun, istek;
        static long sira;
        static volatile string hata="";
        static volatile bool kaynakHazir;
#if UNITY_EDITOR
        public static ManualResetEventSlim DogrulamaKaynakKapisi;
#endif
        public static string Durum => hata.Length>0 ? "KAYIT HATASI: "+hata :
            kuyruk==null ? "Kayit kapali" : !kaynakHazir ? "TEZ KAYDI HAZIRLANIYOR" : "TEZ KAYDI ACIK · bekleyen: "+kuyruk.Count;
        public static string Dizin => dizin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            Kapat(); if(kuyruk!=null)return;
            sira=0; katalog=""; unity=plc=urun=istek=0; hata="";kaynakHazir=false;
            Application.quitting-=Kapat;
            Application.quitting+=Kapat;
        }
        public static void Baslat()
        {
            if(kuyruk!=null)return;
            try {
                var proje=Directory.GetParent(Application.dataPath).FullName;
                var kok=Application.persistentDataPath;
                dizin=Path.Combine(kok,"Veriler",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
                Directory.CreateDirectory(dizin);
                olaylar=new StreamWriter(Path.Combine(dizin,"olaylar.jsonl"),false,new System.Text.UTF8Encoding(false)){AutoFlush=true};
                kuyruk=new BlockingCollection<Action>(512);
                yazici=new Thread(()=>{
                    foreach(var islem in kuyruk.GetConsumingEnumerable())
                        try { islem(); } catch(Exception e) { hata=e.Message; }
                }){IsBackground=true,Name="RVM tez kaydi"};
                yazici.Start();
                Ekle("oturum",JsonUtility.ToJson(new Oturum {
                    unitySurumu=Application.unityVersion,proje=proje,
                    sahne=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    gpu=SystemInfo.graphicsDeviceName,renkUzayi=QualitySettings.activeColorSpace.ToString(),
                    sabitAdim=Time.fixedDeltaTime,yercekimi=Physics.gravity,
                    grafikAPI=SystemInfo.graphicsDeviceType.ToString(),isletimSistemi=SystemInfo.operatingSystem
                }));
                // Copy configuration/source, not reference labels, into an immutable run folder.
                // Source hashing must never block the live event/image writer.
                kaynakYazici=new Thread(()=>{try {
#if UNITY_EDITOR
                    if(DogrulamaKaynakKapisi!=null&&!DogrulamaKaynakKapisi.Wait(60000))throw new TimeoutException("Test source gate timeout");
#endif
                    using(var inventory=new StreamWriter(Path.Combine(dizin,"tum_assets_sha256.txt")))
                    using(var sha=System.Security.Cryptography.SHA256.Create())
                        foreach(var file in Directory.GetFiles(ApplicationAssets(proje),"*",SearchOption.AllDirectories))
                            using(var stream=File.OpenRead(file))
                                inventory.WriteLine(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")+"  "+file.Substring(proje.Length+1));
                    foreach(var alt in new[]{"Assets/RVM","ProjectSettings","Packages"}) {
                        var kaynak=Path.Combine(proje,alt);
                        foreach(var dosya in Directory.GetFiles(kaynak,"*",SearchOption.AllDirectories)) {
                            var uzanti=Path.GetExtension(dosya);
                            if(Array.IndexOf(new[]{".cs",".json",".asset",".prefab",".unity",".mat",".meta",".shader",".png",".jpg",".physicMaterial"},uzanti)<0)continue;
                            var hedef=Path.Combine(dizin,"Kaynaklar",dosya.Substring(proje.Length+1));
                            Directory.CreateDirectory(Path.GetDirectoryName(hedef));File.Copy(dosya,hedef,false);
                        }
                    }
                    File.WriteAllText(Path.Combine(dizin,"kaynak_kopyasi_tamam.txt"),DateTime.UtcNow.ToString("O"));
                    kaynakHazir=true;
                } catch(Exception e) { hata="Kaynak hazirligi: "+e.Message; }
                }){IsBackground=true,Name="RVM kaynak arsivi"};
                kaynakYazici.Start();
            } catch(Exception e) { hata=e.Message; Debug.LogError("TEZ kaydi: "+hata); }
        }
        static string ApplicationAssets(string proje) => Path.Combine(proje,"Assets");
        [Serializable] sealed class Oturum
        {
            public string unitySurumu,proje,sahne,gpu,renkUzayi,grafikAPI,isletimSistemi;
            public float sabitAdim; public Vector3 yercekimi;
            public string asama="final_v1_0_0_temizlik_r1", veriTuru="Unity simulasyonu; gercek sensor kalibrasyonu degildir";
        }
        static void Kuyruga(Action islem)
        {
            if(kuyruk==null||kuyruk.IsAddingCompleted)hata="Kayit kuyrugu kapali; deney eksik";
            else if(!kuyruk.TryAdd(islem))hata="Canli kayit kuyrugu doldu (512); deney eksik";
        }
        public static void Ekle(string tur,string json)
        {
            if(kuyruk==null)return;
            var satir=JsonUtility.ToJson(new Olay {
                utc=DateTime.UtcNow.ToString("O"),saniye=Time.realtimeSinceStartupAsDouble,
                sira=++sira,tur=tur,katalog=katalog,unity=unity,plc=plc,urun=urun,istek=istek,veri=json
            });
            Kuyruga(()=>olaylar.WriteLine(satir));
        }
        public static void Urun(string kod,uint u,uint p,uint id,Transform govde)
        {
            katalog=kod;unity=u;plc=p;urun=id;istek=0;
            Ekle("urun_eklendi",JsonUtility.ToJson(new Konum {konum=govde.position,donus=govde.rotation,olcek=govde.localScale}));
        }
        [Serializable] sealed class Konum { public Vector3 konum,olcek; public Quaternion donus; }
        public static void Istek(uint id) { istek=id; Ekle("olcum_basladi","{}"); }
        public static void Goruntu(string ad,Color32[] piksel,int en,int boy)
        {
            if(kuyruk==null)return;
            if(piksel==null){Ekle("eksik_goruntu","{\"ad\":\""+ad+"\"}");return;}
            // The arrays are immutable after capture. Flatten on the writer thread.
            string dosya=(sira+1).ToString("D8")+"_"+ad+".rgba";
            Kuyruga(()=>{
                var veri=new byte[piksel.Length*4];
                for(int i=0;i<piksel.Length;i++) {var p=piksel[i];int j=4*i;veri[j]=p.r;veri[j+1]=p.g;veri[j+2]=p.b;veri[j+3]=p.a;}
                File.WriteAllBytes(Path.Combine(dizin,dosya),veri);
            });
            Ekle("goruntu",JsonUtility.ToJson(new Kare {dosya=dosya,en=en,boy=boy}));
        }
        public static void Kapat()
        {
            if(kuyruk==null)return;
            try {
                kuyruk.CompleteAdding();
                if(!yazici.Join(10000)) {hata="Yazici 10 saniyede kapanamadi";return;}
                if(kaynakYazici!=null&&!kaynakYazici.Join(30000)) {hata="Kaynak hazirligi 30 saniyede kapanamadi";return;}
                olaylar.Dispose();
                File.WriteAllText(Path.Combine(dizin,hata.Length==0?"kayit_kapandi.txt":"KAYIT_HATASI.txt"),hata.Length==0?DateTime.UtcNow.ToString("O"):hata);
                kuyruk.Dispose();kuyruk=null;
            } catch(Exception e) {hata=e.Message;}
        }
    }
}
