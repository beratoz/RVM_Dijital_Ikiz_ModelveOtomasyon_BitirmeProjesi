using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RVM
{
    // CODESYS owns every production transition. This component is only the plant.
    public class RvmDigitalTwin : MonoBehaviour
    {
        public Transform machine, shuttle, doorLeft, doorRight, rotorA, rotorB, bottleTemplate;
        public Renderer belt;
        public TextMesh tabletScreen;
        public string bridgeUrl = "http://127.0.0.1:8766/exchange";
        public UnitySensors sensors = new UnitySensors();
        public PlcCommands plc = new PlcCommands();
        public RvmVisionCamera vision;
        public RVM.Experimental.RvmInlineMeasurement inlineMeasurement;
        uint visionEpoch, visionRequest;
        public bool autoFeed; // Only inserts the selected physical specimen when PLC is ready.
        public int selectedSpecimen = 1;
        public bool linkLive;
        public string connectionMessage = "OPC UA bağlantısı bekleniyor";
        public string plantFault = "";
        public float lastCycleSeconds;
        Quaternion leftClosed, rightClosed;
        Vector3 shuttleHome;
        GameObject item;
        Vector3 normalScale;
        float doorAngle, crushElapsed, emptyElapsed, lastGood = -100, cycleStart, nextFeed;
        bool crushed, emptied, enteredCrusher;
        uint productCounter, lastPlcCounter, countedTotal;
        bool[] full = new bool[4];
        GUIStyle header, label, status;
        int recentState = -1;
        public string lastTransition = "";
        uint handledRequestEpoch, handledRequest;
        bool commandPending, wasLive;
        public string operatorFeedback = "Oturum eslesmesi bekleniyor";
        Vector2 tabletScroll;

        void BeginSession()
        {
            uint old = sensors.udiUnityOturumu;
            sensors.udiUnityOturumu = (uint)(DateTime.UtcNow.Ticks % uint.MaxValue) | 1;
            if (sensors.udiUnityOturumu == old) sensors.udiUnityOturumu = old < uint.MaxValue-2 ? old+2 : 1;
            sensors.udiUnitySayaci = 1; sensors.udiGorulenPlcOturumu = 0;
            sensors.udiKomutKimligi = 0; sensors.uiOperatorKomutu = 0; sensors.uiOperatorParametresi = 0;
            sensors.udiNesneOnayi = 0; sensors.uiNesneSonucu = 0; sensors.udiNesneUrunKimligi = 0;
            if(inlineMeasurement)inlineMeasurement.Cancel(); if(vision)vision.Cancel(); visionEpoch=visionRequest=0;
            commandPending = false; autoFeed = false; linkLive = wasLive = false; lastGood = -100;
            operatorFeedback = "Yeni oturum; otomatik baslatma yok";
        }

        void Awake()
        {
            Application.runInBackground = true;
            leftClosed = doorLeft.localRotation; rightClosed = doorRight.localRotation;
            shuttleHome = shuttle.position;
            sensors = new UnitySensors(); plc = new PlcCommands();
            sensors.uiUnityProtokol = 106;
            BeginSession();
            inlineMeasurement=GetComponent<RVM.Experimental.RvmInlineMeasurement>();
            if(!inlineMeasurement){enabled=false;Debug.LogError("Open RVM_InlineMeasurement_v106 scene: measurement station missing");return;}
            if (bottleTemplate) bottleTemplate.gameObject.SetActive(false);
            UpdateSensors();
            StartCoroutine(Exchange());
        }

        IEnumerator Exchange()
        {
            while (enabled)
            {
                if (sensors.udiUnitySayaci == uint.MaxValue) BeginSession();
                sensors.udiUnitySayaci++;
                using (var request = new UnityWebRequest(bridgeUrl, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(sensors)));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json"); request.timeout = 2;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        BridgeReply reply = null;
                        try { reply = JsonUtility.FromJson<BridgeReply>(request.downloadHandler.text); }
                        catch (Exception e) { connectionMessage = e.Message; }
                        if (reply != null) AcceptReply(reply);
                    }
                    else connectionMessage = "OPC UA köprüsüne erişilemiyor: " + request.error;
                }
                yield return new WaitForSecondsRealtime(.04f);
            }
        }

        void AcceptReply(BridgeReply reply)
        {
            var p = reply.plc;
            if (p == null || p.uiProtokolSurumu != 106 || p.udiPlcOturumu == 0 ||
                p.udiCikisBaslangic == 0 || p.udiCikisBaslangic != p.udiCikisBitis)
            { connectionMessage = reply.error ?? "v106 PLC paketi bekleniyor"; return; }
            if (sensors.udiGorulenPlcOturumu != 0 && p.udiPlcOturumu != sensors.udiGorulenPlcOturumu)
            { BeginSession(); connectionMessage = "PLC yeniden basladi; yeni oturum"; return; }
            if (sensors.udiGorulenPlcOturumu == 0)
            {
                if (p.udiUnityOnayOturumu == sensors.udiUnityOturumu)
                { sensors.udiGorulenPlcOturumu = p.udiPlcOturumu; lastPlcCounter = p.udiPlcSayaci; }
                connectionMessage = "v106 HELLO / oturum onayi"; return;
            }
            if (!reply.connected || p.udiUnityOnayOturumu != sensors.udiUnityOturumu)
            { connectionMessage = reply.error ?? "Veri onayi bekleniyor"; return; }
            if (!ValidCommands(p)) { FailPlant("Gecersiz PLC hareket parametresi"); lastGood=-100; return; }
            if (p.udiPlcSayaci != lastPlcCounter)
            { lastGood=Time.realtimeSinceStartup; lastPlcCounter=p.udiPlcSayaci; }
            plc=p; connectionMessage="CODESYS / OPC UA / Protokol 106";
            if (sensors.udiKomutKimligi!=0 && p.udiKomutOnayi==sensors.udiKomutKimligi)
            {
                operatorFeedback=CommandStatus(p.uiKomutDurumu)+" / "+CommandReason(p.uiKomutNedeni);
                if(p.uiKomutDurumu>=2)commandPending=false;
            }
        }
        static bool Range(float v,float min,float max) => !float.IsNaN(v)&&!float.IsInfinity(v)&&v>=min-0.000001f&&v<=max+0.000001f;
        static bool ValidCommands(PlcCommands p) => p.uiVeriModu==1 && p.uiDesteklenenModlar==2 &&
            Range(p.rBantHizi,0,1.2f)&&Range(p.rKiriciDevri,0,180)&&Range(p.rHazneHizi,0,1.5f)&&
            Range(p.rHazneHedefi,-.77f,.31f)&&Range(p.rKapakAcisi,0,75)&&p.uiNesneProfili>=0&&p.uiNesneProfili<=3;

        void ConsumeInsertionRequest()
        {
            if(!linkLive || !plc.xNesneIstegiEtkin || plc.udiNesneIstegi==0) return;
            if(handledRequestEpoch==plc.udiPlcOturumu && plc.udiNesneIstegi<=handledRequest) return;
            handledRequestEpoch=plc.udiPlcOturumu; handledRequest=plc.udiNesneIstegi;
            bool allowed=inlineMeasurement&&inlineMeasurement.ready&&plc.uiDurum==10&&plc.xCalismaIzni&&plc.uiHataKodu==0&&!item&&
                sensors.xYolBos&&sensors.xKapakKapali&&Mathf.Abs(shuttle.position.x-shuttleHome.x)<.008f&&
                !sensors.xAcilStop&&!sensors.xSikisma&&plantFault.Length==0&&productCounter<uint.MaxValue;
            if(allowed) CreateSpecimen(plc.uiNesneProfili);
            sensors.udiNesneOnayi=plc.udiNesneIstegi;
            sensors.uiNesneSonucu=allowed&&item?1:2;
            sensors.udiNesneUrunKimligi=allowed&&item?sensors.udiUrunKimligi:0;
        }

        void Update()
        {
            linkLive = Time.realtimeSinceStartup - lastGood < .6f;
            if(wasLive&&!linkLive)BeginSession();
            wasLive=linkLive;
            ConsumeInsertionRequest();
            UpdateVision();
            if (recentState != plc.uiDurum)
            {
                recentState = plc.uiDurum;
                lastTransition = DateTime.Now.ToString("HH:mm:ss") + "  " + StateName(recentState);
                Debug.Log("RVM PLC: " + lastTransition + " | ürün=" + plc.udiIslemKimligi + " | malzeme=" + plc.uiTaninmisMalzeme);
            }
            uint total = plc.udiPetSayaci + plc.udiAluminyumSayaci + plc.udiCamSayaci;
            if (plc.uiDurum == 10 && countedTotal != total)
            { lastCycleSeconds = Time.time - cycleStart; countedTotal = total; }
            if (autoFeed && linkLive && !commandPending && !plc.xNesneIstegiEtkin && plc.uiDurum == 10 && item == null && Time.time > nextFeed)
            { InsertSpecimen(selectedSpecimen); nextFeed = Time.time + .1f; }
            if (tabletScreen)
                tabletScreen.text = "RVM / DIGITAL TWIN\n" + (linkLive ? "PLC ONLINE" : "PLC OFFLINE") + "\n" +
                    StateName(plc.uiDurum) + "\nPET " + plc.udiPetSayaci + "   ALU " + plc.udiAluminyumSayaci + "   CAM " + plc.udiCamSayaci;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool run = linkLive && plc.xCalismaIzni && plc.uiHataKodu == 0 && !sensors.xAcilStop && !sensors.xSikisma;
            sensors.rGercekBantHizi = 0; sensors.rGercekKiriciDevri = 0;
            // Independent plant interlocks expose disagreements instead of inventing a new PLC state.
            if (run && plc.xKonveyorIleri && plc.xKonveyorGeri) FailPlant("İleri/geri komut çakışması");
            if (run && plc.xKiriciCalis && (!plc.xTanimaOnayi || plc.udiIslemKimligi != sensors.udiUrunKimligi))
                FailPlant("Tanıma veya ürün kimliği uyuşmuyor");
            if (run && plc.xHazneHareket && doorAngle > 1) FailPlant("Kapak açıkken hazne hareket komutu");
            run &= plantFault.Length == 0;
            if (run)
            {
                float direction = plc.xKonveyorIleri ? 1 : plc.xKonveyorGeri ? -1 : 0;
                sensors.rGercekBantHizi = direction * Mathf.Clamp(plc.rBantHizi, 0, 1.2f);
                if (belt && direction != 0)
                {
                    var mat = belt.material;
                    mat.mainTextureOffset += new Vector2(sensors.rGercekBantHizi * dt, 0);
                }
                if (item && !enteredCrusher && direction != 0)
                {
                    item.transform.position += Vector3.right * (sensors.rGercekBantHizi * dt);
                    // Physical stop gate holds the product at the measurement station.
                    // A delayed PLC reply cannot carry it past the camera before approval.
                    if(direction>0 && !plc.xTanimaOnayi && item.transform.position.x>-.55f)
                    { var held=item.transform.position;held.x=-.55f;item.transform.position=held; }
                    if (item.transform.position.x >= .225f)
                    { enteredCrusher = true; item.transform.position = new Vector3(.225f,1.17f,0); }
                    if (item.transform.position.x < -1.14f && direction < 0)
                    { Destroy(item); item = null; }
                }
                if (plc.xKiriciCalis)
                {
                    sensors.rGercekKiriciDevri = Mathf.Clamp(plc.rKiriciDevri,0,180);
                    float angle = sensors.rGercekKiriciDevri * 6 * dt;
                    rotorA.Rotate(Vector3.up,angle,Space.Self); rotorB.Rotate(Vector3.up,-angle,Space.Self);
                    if (item && enteredCrusher && !crushed)
                    {
                        crushElapsed += dt;
                        float t = Mathf.Clamp01(crushElapsed / 1.15f);
                        item.transform.position = Vector3.Lerp(new Vector3(.225f,1.17f,0),shuttle.position+Vector3.up*.055f,t);
                        item.transform.localScale = Vector3.Scale(normalScale,new Vector3(1,1-.65f*t,1-.35f*t));
                        if (t >= 1)
                        {
                            crushed = true;
                            if (plc.uiTaninmisMalzeme == 3) MakeGlassFragments();
                            item.transform.SetParent(shuttle,true);
                        }
                    }
                }
                if (plc.xHazneHareket)
                {
                    var p=shuttle.position;
                    p.x = Mathf.MoveTowards(p.x,Mathf.Clamp(plc.rHazneHedefi,-.77f,.31f),Mathf.Clamp(plc.rHazneHizi,0,1.5f)*dt);
                    shuttle.position=p;
                }
                float targetAngle=plc.xKapakAc ? Mathf.Clamp(plc.rKapakAcisi,0,75) : 0;
                doorAngle=Mathf.MoveTowards(doorAngle,targetAngle,300*dt);
                doorLeft.localRotation=leftClosed*Quaternion.AngleAxis(doorAngle,Vector3.up);
                doorRight.localRotation=rightClosed*Quaternion.AngleAxis(-doorAngle,Vector3.up);
                if (item && crushed && !emptied && doorAngle >= 70)
                {
                    item.transform.SetParent(null,true);
                    emptyElapsed += dt;
                    item.transform.position += Vector3.down*(.85f*dt);
                    if (emptyElapsed >= .36f)
                    {
                        sensors.uiBosaltilanKutu = BinAt(item.transform.position.x);
                        emptied=true;
                        Destroy(item); item=null;
                    }
                }
            }
            UpdateSensors();
        }

        void UpdateVision()
        {
            if(!inlineMeasurement)return;
            if(linkLive&&plc.uiDurum==30&&plc.udiGoruntuIstegi!=0&&
                (visionEpoch!=plc.udiPlcOturumu||visionRequest!=plc.udiGoruntuIstegi)&&item&&!inlineMeasurement.busy&&!inlineMeasurement.optical.busy&&inlineMeasurement.ready)
            {
                visionEpoch=plc.udiPlcOturumu;visionRequest=plc.udiGoruntuIstegi;
                inlineMeasurement.Request(item,plc.udiGoruntuIstegi,sensors.udiUrunKimligi,plc.udiPlcOturumu,sensors.udiUnityOturumu);
            }
            inlineMeasurement.CopyTo(sensors);
        }

        public void InsertSpecimen(int profile)
        {
            if (!linkLive || plc.uiDurum != 10 || item || commandPending || plc.xNesneIstegiEtkin) return;
            OperatorCommand(4,profile);
        }
        void CreateSpecimen(int profile)
        {
            inlineMeasurement.Cancel();
            item=inlineMeasurement.Create(profile,new Vector3(-1.03f,1.20f,0));
            if(!item)return;
            normalScale=item.transform.localScale;
            productCounter++; if(productCounter==0)productCounter=1;
            sensors.udiUrunKimligi=productCounter;
            crushElapsed=emptyElapsed=0; crushed=emptied=enteredCrusher=false; sensors.uiBosaltilanKutu=0;
            cycleStart=Time.time;
            UpdateSensors();
        }
        void MakeGlassFragments()
        {
            foreach(var r in item.GetComponentsInChildren<Renderer>()) r.enabled=false;
            for(int i=0;i<8;i++)
            {
                var shard=GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(shard.GetComponent<Collider>());
                shard.name="GlassShard_"+i; shard.transform.SetParent(item.transform,false);
                shard.transform.localPosition=new Vector3((i%3-1)*.014f,(i/3)*.008f,((i*2)%3-1)*.011f);
                shard.transform.localScale=new Vector3(.016f,.009f,.012f);
                shard.transform.localRotation=Quaternion.Euler(i*31,i*47,i*19);
                shard.GetComponent<Renderer>().material.color=new Color(.18f,.5f,.3f);
            }
        }
        void UpdateSensors()
        {
            sensors.xGiris=item && !enteredCrusher && item.transform.position.x < -.90f;
            sensors.xTanima=item && !enteredCrusher && Mathf.Abs(item.transform.position.x+.55f)<.11f;
            sensors.xKiriciGiris=item && enteredCrusher;
            sensors.xKirildi=crushed;
            sensors.xHazneDolu=crushed&&!emptied;
            sensors.xBosaltildi=emptied;
            sensors.xKapakAcik=doorAngle>=74; sensors.xKapakKapali=doorAngle<.5f;
            sensors.xYolBos=item==null;
            sensors.uiPlantVeriKaynagi=1;
            sensors.xPlantVerisiGecerli=plantFault.Length==0;
            sensors.rHazneKonumu=shuttle.position.x;
            sensors.xKutuPetDolu=full[1]; sensors.xKutuAluminyumDolu=full[2]; sensors.xKutuCamDolu=full[3];
            if(inlineMeasurement)inlineMeasurement.CopyTo(sensors);
        }
        public static int BinAt(float x)
        { if(Mathf.Abs(x+.23f)<.04f)return 1; if(Mathf.Abs(x+.77f)<.04f)return 2; if(Mathf.Abs(x-.31f)<.04f)return 3; return 0; }
        void FailPlant(string reason) { plantFault=reason; sensors.xSikisma=true; }
        public void OperatorCommand(int code) { OperatorCommand(code,0); }
        public void OperatorCommand(int code,int parameter)
        {
            if(!linkLive || sensors.udiGorulenPlcOturumu==0 || (commandPending&&code!=2))return;
            if(sensors.udiKomutKimligi==uint.MaxValue){BeginSession();return;}
            sensors.uiOperatorKomutu=code;sensors.uiOperatorParametresi=parameter;sensors.udiKomutKimligi++;
            commandPending=true;operatorFeedback="PLC onayi bekleniyor";
            if(code==2)autoFeed=false;
        }
        public void ClearStoppedPlant()
        {
            if(!linkLive || plc.xCalismaIzni || (plc.uiDurum!=0 && plc.uiDurum!=900))return;
            if(inlineMeasurement)inlineMeasurement.Cancel();
            if(item)Destroy(item); item=null;
            shuttle.position=shuttleHome; doorAngle=0;
            doorLeft.localRotation=leftClosed; doorRight.localRotation=rightClosed;
            crushed=emptied=enteredCrusher=false; plantFault=""; sensors.xSikisma=false;
            sensors.uiBosaltilanKutu=0; sensors.rKutleGram=0; sensors.rOptikGecirgenlik=0;
            sensors.rBoyMetre=0; sensors.xMetalAlgilandi=false;
            UpdateSensors();
        }
        public static string MaterialName(int code) => code==1?"PET":code==2?"ALÜMİNYUM":code==3?"CAM":"BİLİNMEYEN";
        public static string StateName(int code)
        { switch(code) {case 0:return "DURDU";case 10:return "HAZIR";case 20:return "ALGILAMA";case 30:return "TANIMA";case 40:return "TAŞIMA";case 50:return "KIRMA / EZME";case 60:return "HAZNE KONUMLAMA";case 70:return "BOŞALTMA";case 80:return "KAPAK KAPAMA";case 90:return "HAZNE DÖNÜŞ";case 100:return "YABANCI NESNE İADE";case 900:return "HATA / DURUŞ";default:return "?";} }
        string Bit(bool v) => v?"●":"○";
        public static string CommandStatus(int s) => s==1?"Bekliyor":s==2?"Tamamlandi":s==3?"Reddedildi":s==4?"Zaman asimi":s==5?"Iptal":"Yok";
        public static string FaultName(int code)
        {
            switch(code){
                case 0:return "Hata yok";
                case 301:return "PLC / Unity haberlesmesi kesildi";
                case 302:return "Protokol surumu uyusmuyor";
                case 303:return "Acil durus";
                case 304:return "Sikisma / Unity hareket kilidi";
                case 305:return "Islenen urunun kimligi degisti";
                case 306:return "Islem adimi zaman asimi";
                case 307:return "Tanima bolgesinde nesne kayboldu";
                case 308:return "Hedef atik kutusu dolu";
                case 309:return "Bosaltilan kutu hedefle uyusmuyor";
                case 310:return "Sensor / hazne / kapak geri bildirimi uyusmuyor";
                case 312:return "Nesne ekleme onayi veya bos yol uyusmazligi";
                case 313:return "PLC oturum / komut kimligi sayaci tukendi";
                default:return "Bilinmeyen PLC hata kodu";
            }
        }
        public static string CommandReason(int r)
        {
            string[] names={"Yok","Durdurma onceligi","Baglanti/veri gecersiz","Makine hazir degil","Yol/hazne/kapak uygun degil","Hata/acil durus","Kaynak mesgul","Gecersiz parametre","Mod desteklenmiyor","Diger kaynak secildi","Oturum/kimlik uyusmazligi","Nesne onayi gecikti","Nesne ekleme reddedildi","Eski kimlik/degismis veri","Saglayici hazir degil"};
            return r>=0&&r<names.Length?names[r]:"Bilinmeyen neden "+r;
        }
        void OnGUI()
        {
            if(header==null)
            {
                header=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};
                label=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};
                status=new GUIStyle(label){fontSize=18,fontStyle=FontStyle.Bold};
            }
            float scale=Mathf.Clamp(Screen.height/850f,.6f,1.3f);
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale;
            GUILayout.BeginArea(new Rect(width-410,12,398,Screen.height/scale-24),GUI.skin.box);
            tabletScroll=GUILayout.BeginScrollView(tabletScroll);
            GUILayout.Label("RVM • OPERATÖR TABLETİ",header);
            GUI.contentColor=linkLive?new Color(.3f,1,.6f):new Color(1,.55f,.25f);
            GUILayout.Label(linkLive?"PLC BAĞLI / VERİ GÜNCEL":"PLC BAĞLANTISI YOK — HAREKET DURDU",label);
            GUI.contentColor=Color.white;
            GUILayout.Label(connectionMessage.Length>140?connectionMessage.Substring(0,140)+"… (ayrıntı: bridge.log)":connectionMessage,label,GUILayout.MaxHeight(56));
            GUILayout.Space(8); GUILayout.Label(StateName(plc.uiDurum),status);
            GUILayout.Label("Ürün #"+sensors.udiUrunKimligi+"  / PLC #"+plc.udiIslemKimligi+"\nTanıma: "+MaterialName(plc.uiTaninmisMalzeme)+"   Kutu: "+plc.uiHedefKutu,label);
            GUILayout.Label("Mod: KAMERA + SIMULE SENSOR / PLC oturum "+plc.udiPlcOturumu+"\nSon komut kaynagi: "+(plc.uiSonKomutKaynagi==2?"CODESYS":plc.uiSonKomutKaynagi==1?"Unity":"Yok"),label);
            GUILayout.Label("Unity #"+sensors.udiKomutKimligi+": "+operatorFeedback+"\nCODESYS #"+plc.udiPlcOperatorKomutKimligi+": "+CommandStatus(plc.uiPlcKomutDurumu)+" / "+CommandReason(plc.uiPlcKomutNedeni)+"\nNesne istegi/onayi: "+plc.udiNesneIstegi+" / "+sensors.udiNesneOnayi,label);
            GUILayout.BeginHorizontal(); GUI.enabled=linkLive;
            if(GUILayout.Button("BAŞLAT",GUILayout.Height(32)))OperatorCommand(1);
            if(GUILayout.Button("DURDUR",GUILayout.Height(32)))OperatorCommand(2);
            if(GUILayout.Button("RESET",GUILayout.Height(32)))OperatorCommand(3);
            GUILayout.EndHorizontal(); GUI.enabled=true;
            sensors.xAcilStop=GUILayout.Toggle(sensors.xAcilStop," ACİL STOP (simülasyon)");
            GUILayout.Label("Fiziksel prefab secimi (sinif sonucu degil)",label);
            selectedSpecimen=GUILayout.SelectionGrid(selectedSpecimen,new[]{"Diğer","PET","Alüminyum","Cam"},4);
            GUI.enabled=inlineMeasurement&&inlineMeasurement.ready&&linkLive&&plc.uiDurum==10&&!item&&!commandPending&&!plc.xNesneIstegiEtkin;
            if(GUILayout.Button("NESNEYİ GİRİŞE KOY",GUILayout.Height(30)))InsertSpecimen(selectedSpecimen);
            GUI.enabled=true; autoFeed=GUILayout.Toggle(autoFeed," Otomatik besle (aynı seçili nesne)");
            GUILayout.Space(6); GUILayout.Label("PLC KOMUTU → GERÇEK HAREKET",label);
            GUILayout.Label("Bant "+Bit(plc.xKonveyorIleri)+" / "+sensors.rGercekBantHizi.ToString("F2")+" m/s    Kırıcı "+Bit(plc.xKiriciCalis)+" / "+sensors.rGercekKiriciDevri.ToString("F0")+" rpm\nHazne "+plc.rHazneHedefi.ToString("F3")+" → "+sensors.rHazneKonumu.ToString("F3")+" m\nKapak "+Bit(plc.xKapakAc)+" / "+doorAngle.ToString("F0")+"°",label);
            GUILayout.Label("UNITY KAMERA GORUNTUSU / PIKSEL OLCUMU",label);
            if(inlineMeasurement && inlineMeasurement.optical.preview) GUILayout.Label(inlineMeasurement.optical.preview,GUILayout.Width(360),GUILayout.Height(180));
            GUILayout.Label("Goruntu istek/onay: "+plc.udiGoruntuIstegi+" / "+sensors.udiGoruntuOnayi+
                "\nBoy/en: "+sensors.rGoruntuBoy.ToString("F3")+" / "+sensors.rGoruntuEn.ToString("F3")+" m"+
                "\nGecerli: "+sensors.xGoruntuGecerli+"  Nesne: "+sensors.xGoruntuNesneVar+
                "\nPLC goruntu karari: "+plc.uiGoruntuKarari+"  (1 kabul, 2 uygun degil, 3 gecersiz, 4 eski, 5 zaman asimi)"+
                "\n"+(inlineMeasurement?inlineMeasurement.status:"Olcum istasyonu yok"),label);
            GUILayout.Label("PIKSEL + TEMAS KUVVETI + SANAL PROB",label);
            GUILayout.Label("Tarti/prob gecerli: "+sensors.xOlcumTartiGecerli+" / "+sensors.xOlcumProbGecerli+
                "\nYansima: "+sensors.rOlcumYansima.ToString("F3")+" Metal: "+sensors.rOlcumMetal.ToString("F3")+
                " Manyetik: "+sensors.rOlcumManyetik.ToString("F3")+"\nOlcum yasi: "+sensors.rGoruntuYasiMs.ToString("F0")+" ms",label);
            // Asama 4: yalniz goruntuleme; PLC/sensor alanlarina yazmaz.
GUILayout.Label(
    "Boy: " + sensors.rBoyMetre.ToString("F3") + " m" +
    "   Yol bos: " + sensors.xYolBos +
    "\nBosaltilan kutu: " + sensors.uiBosaltilanKutu +
    "   Ekleme sonucu: " + sensors.uiNesneSonucu +
    "\nEklenen urun ID: " + sensors.udiNesneUrunKimligi +
    "\nPlant verisi gecerli: " + sensors.xPlantVerisiGecerli +
    "   Kaynak: " + sensors.uiPlantVeriKaynagi +
    "\nProtokol Unity/PLC: " + sensors.uiUnityProtokol +
    " / " + plc.uiProtokolSurumu +
    "\nIleri/geri komutu: " + plc.xKonveyorIleri +
    " / " + plc.xKonveyorGeri +
    "   Hazne hareketi: " + plc.xHazneHareket +
    "\nKutu dolu PET/ALU/CAM: " + sensors.xKutuPetDolu +
    " / " + sensors.xKutuAluminyumDolu + " / " + sensors.xKutuCamDolu,
    label);

            GUILayout.Label("Giriş "+Bit(sensors.xGiris)+"  Tanıma "+Bit(sensors.xTanima)+"  Kırıcı "+Bit(sensors.xKiriciGiris)+"\nKırıldı "+Bit(sensors.xKirildi)+"  Hazne dolu "+Bit(sensors.xHazneDolu)+"  Boşaldı "+Bit(sensors.xBosaltildi)+"\nKapak açık "+Bit(sensors.xKapakAcik)+"  Kapalı "+Bit(sensors.xKapakKapali)+"\nMetal "+Bit(sensors.xMetalAlgilandi)+"  "+sensors.rKutleGram+" g  Optik "+sensors.rOptikGecirgenlik.ToString("F2"),label);
            GUILayout.Label("PET "+plc.udiPetSayaci+"   ALÜMİNYUM "+plc.udiAluminyumSayaci+"   CAM "+plc.udiCamSayaci+"\nReddedilen "+plc.udiReddedilenSayaci+"   Hedef 9–12 adet/dk",label);
            GUILayout.Label(lastCycleSeconds>0?"Son çevrim "+lastCycleSeconds.ToString("F2")+" s / "+(60/lastCycleSeconds).ToString("F1")+" adet/dk":"Çevrim ölçümü bekleniyor",label);
            GUILayout.Label("Arıza denemeleri",label);
            sensors.xSikisma=GUILayout.Toggle(sensors.xSikisma," Sıkışma");
            GUILayout.BeginHorizontal(); full[1]=GUILayout.Toggle(full[1],"PET dolu");full[2]=GUILayout.Toggle(full[2],"ALU dolu");full[3]=GUILayout.Toggle(full[3],"CAM dolu");GUILayout.EndHorizontal();
            GUI.enabled=linkLive&&!plc.xCalismaIzni&&(plc.uiDurum==0||plc.uiDurum==900);
            if(GUILayout.Button("Duruşta nesneyi al / hazneyi başlangıca getir"))ClearStoppedPlant();
            GUI.enabled=true;
            GUILayout.Label("PLC hata: "+plc.uiHataKodu+" / "+FaultName(plc.uiHataKodu)+"\n"+plantFault+"\nKomut onayı: "+plc.udiKomutOnayi+" / "+sensors.udiKomutKimligi,label);
            GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix=Matrix4x4.identity;
        }
    }
}
