using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RVM.Experimental
{
    // Paired black/white background images; no access to object name, bounds, mass or class.
    public sealed class RvmOpticalStation : MonoBehaviour
    {
        public Camera sensorCamera;
        public Renderer background;
        public float fieldWidthMetres = .40f;
        public float minLength = .10f, maxLength = .30f, minWidth = .02f, maxWidth = .09f;
        public bool busy, calibrated, valid, present;
        public float lengthMetres, widthMetres, fill, transmissionIndex, reflectionIndex;
        public string reason = "Empty station calibration required";
        public Texture2D preview;
        public float capturedAt;
        public bool lastCaptureSucceeded;
        public float lastCaptureSeconds;
        public bool pixelNoiseEnabled;
        public bool bolgeselOptikEtkin = true;
        public bool kantilOptikEtkin = true;
        public int pixelNoiseSeed=106, pixelNoiseAmplitude=1;
        System.Random pixelRandom;
        Color32[] emptyBlack, emptyWhite, pixels;
        RenderTexture target;
        Material backgroundMaterial;
        RvmOptikAyar deney;
        bool framePending, frameReceived;
        int captureGeneration;
        bool disposed;
        const int W = 800, H = 400;

        void Awake()
        {
            deney=gameObject.AddComponent<RvmOptikAyar>(); deney.Hazirla();
            target = new RenderTexture(W,H,24,RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            target.Create();
            preview = new Texture2D(W,H,TextureFormat.RGBA32,false);
            backgroundMaterial = background.material;
            pixelRandom=new System.Random(pixelNoiseSeed);
            sensorCamera.targetTexture = target;
            sensorCamera.aspect = 2;
            sensorCamera.orthographicSize = fieldWidthMetres / 4;
            sensorCamera.allowHDR = false; sensorCamera.allowMSAA = false;
            sensorCamera.enabled = false;
            RenderPipelineManager.endCameraRendering += FrameReady;
        }
        void FrameReady(ScriptableRenderContext context, Camera camera)
        {
            if (!framePending || camera != sensorCamera) return;
            if(SystemInfo.supportsAsyncGPUReadback)
            {
                framePending=false;sensorCamera.enabled=false;
                int token=captureGeneration;
                AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32,request=>{
                    if(disposed||token!=captureGeneration)return;
                    if(request.hasError){pixels=null;frameReceived=true;return;}
                    pixels=request.GetData<Color32>().ToArray();
                    preview.SetPixels32(pixels);preview.Apply(false);
                    ApplyPixelNoise();frameReceived=true;
                });
                return;
            }
            var old = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                preview.ReadPixels(new Rect(0,0,W,H),0,0,false); preview.Apply(false);
                pixels = preview.GetPixels32(); frameReceived = true;
                ApplyPixelNoise();
            }
            finally { RenderTexture.active = old; framePending = false; sensorCamera.enabled = false; }
        }
        void ApplyPixelNoise()
        {
                if(pixelNoiseEnabled)
                    for(int i=0;i<pixels.Length;i++)
                    {
                        var p=pixels[i];int n=pixelRandom.Next(-pixelNoiseAmplitude,pixelNoiseAmplitude+1);
                        pixels[i]=new Color32((byte)Mathf.Clamp(p.r+n,0,255),(byte)Mathf.Clamp(p.g+n,0,255),(byte)Mathf.Clamp(p.b+n,0,255),255);
                    }
        }
        IEnumerator Capture(Color colour)
        {
            double started=Time.realtimeSinceStartupAsDouble;
            lastCaptureSucceeded=false;
            RvmTezKaydi.Ekle(colour==Color.black?"siyah_cekim_basladi":"beyaz_cekim_basladi","{}");
            backgroundMaterial.SetColor("_BaseColor",colour==Color.white ? new Color(deney.arkaPlan,deney.arkaPlan,deney.arkaPlan,1) : colour);
            captureGeneration++;
            frameReceived = false; pixels = null;
            yield return null;
            framePending = true; sensorCamera.enabled = true;
            float timeout = Time.realtimeSinceStartup + 2;
            while (!frameReceived && Time.realtimeSinceStartup < timeout) yield return null;
            if(!frameReceived)captureGeneration++;
            framePending = false; sensorCamera.enabled = false;
            lastCaptureSucceeded=frameReceived&&pixels!=null;
            lastCaptureSeconds=(float)(Time.realtimeSinceStartupAsDouble-started);
            RvmTezKaydi.Ekle(frameReceived?"cekim_tamam":"cekim_zaman_asimi","{}");
        }
        // Rendering preparation only; never analyzes or publishes product measurements.
        public IEnumerator PrepareRender(Color colour) { yield return Capture(colour); }
        public IEnumerator CalibrateEmpty()
        {
            if (busy) yield break;
            busy = true; valid = calibrated = false;
            deney.Kaydet();
            yield return Capture(Color.black); emptyBlack = pixels;
            yield return Capture(Color.white); emptyWhite = pixels;
            calibrated = emptyBlack != null && emptyWhite != null;
            reason = calibrated ? "Empty references captured" : "Calibration render timeout";
            RvmTezKaydi.Goruntu("referans_siyah",emptyBlack,W,H);
            RvmTezKaydi.Goruntu("referans_beyaz",emptyWhite,W,H);
            RvmTezKaydi.Ekle("optik_ayar",JsonUtility.ToJson(new OptikAyar {yontem=kantilOptikEtkin?RvmKantilOptik.Surum:bolgeselOptikEtkin?RvmBolgeselOptik.Surum:"tum-maske-v1",alanMetre=fieldWidthMetres,minBoy=minLength,maxBoy=maxLength,minEn=minWidth,maxEn=maxWidth,kameraKonumu=sensorCamera.transform.position,kameraDonusu=sensorCamera.transform.rotation,ortografik=sensorCamera.orthographic,ortografikBoy=sensorCamera.orthographicSize,ortamIsigi=RenderSettings.ambientLight,ortamSiddeti=RenderSettings.ambientIntensity,ortamModu=RenderSettings.ambientMode.ToString()}));
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) RvmTezKaydi.Ekle("isik_ayar",JsonUtility.ToJson(new IsikAyar {ad=l.name,tur=l.type.ToString(),renk=l.color,siddet=l.intensity,konum=l.transform.position,donus=l.transform.rotation,etkin=l.isActiveAndEnabled}));
            RvmTezKaydi.Ekle("kalibrasyon",JsonUtility.ToJson(new OptikSonuc {gecerli=calibrated,neden=reason}));
            busy = false;
        }
        public IEnumerator Measure()
        {
            if (busy) yield break;
            valid = present = false;
            if (!calibrated) { reason = "Calibrate the empty station first"; yield break; }
            busy = true;
            yield return Capture(Color.black); var black = pixels;
            yield return Capture(Color.white); var white = pixels;
            RvmTezKaydi.Goruntu("nesne_siyah",black,W,H);
            RvmTezKaydi.Goruntu("nesne_beyaz",white,W,H);
            if (black == null || white == null) { reason = "Render timeout"; RvmTezKaydi.Ekle("optik_hata","{\"neden\":\"Render timeout\"}"); busy = false; yield break; }
            Analyze(emptyBlack,emptyWhite,black,white);
            capturedAt=Time.realtimeSinceStartup;
            busy = false;
        }
        [Serializable] sealed class OptikAyar {public float alanMetre,minBoy,maxBoy,minEn,maxEn,ortografikBoy,ortamSiddeti;public Vector3 kameraKonumu;public Quaternion kameraDonusu;public Color ortamIsigi;public string ortamModu,yontem;public bool ortografik;public int pikselEn=800,pikselBoy=400;public float maskeReferansEsigi=.2f,maskeFarkEsigi=.045f;}
        [Serializable] sealed class IsikAyar {public string ad,tur;public Color renk;public float siddet;public Vector3 konum;public Quaternion donus;public bool etkin;}
        [Serializable] sealed class OptikSonuc {public float boyMetre,enMetre,doluluk,gecirgenlik,yansima,hamGecirgenlik,hamYansima;public bool gecerli,nesne,gurultu;public int gurultuTohumu,gurultuGenligi;public string neden,yontem;}
        static float Luma(Color32 c) { return (.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f; }
        void Analyze(Color32[] eb, Color32[] ew, Color32[] b, Color32[] w)
        {
            var mask = new Color32[w.Length];
            double transmission = 0, reflection = 0; int count = 0;
            for (int i=0;i<w.Length;i++)
            {
                float baseline = Luma(ew[i])-Luma(eb[i]);
                float difference = Mathf.Abs(Luma(w[i])-Luma(ew[i]))+Mathf.Abs(Luma(b[i])-Luma(eb[i]));
                if (baseline > .2f && difference > .045f)
                {
                    mask[i] = new Color32(255,255,255,255); count++;
                    transmission += Mathf.Clamp01((Luma(w[i])-Luma(b[i]))/baseline);
                    reflection += Mathf.Clamp01((Luma(b[i])-Luma(eb[i]))/baseline);
                }
            }
            var measured = RvmImageAnalysis.Analyze(mask,W,H,fieldWidthMetres);
            var contour=RvmKonturGozlemi.Hesapla(mask,W,H,out var component);
            RvmTezKaydi.Goruntu("kontur_ana_bilesen",component,W,H);
            RvmTezKaydi.Ekle("kontur_gozlem",JsonUtility.ToJson(contour));
            lengthMetres = measured.length; widthMetres = measured.width; fill = measured.fill;
            present = measured.present;
            transmissionIndex = count>0 ? (float)(transmission/count) : 0;
            reflectionIndex = count>0 ? (float)(reflection/count) : 0;
            float rawT=transmissionIndex,rawY=reflectionIndex;
            var regional=RvmBolgeselOptik.Hesapla(eb,ew,b,w,mask,W,out var roi,out var selection);
            RvmTezKaydi.Goruntu("govde_roi",roi,W,H);
            RvmTezKaydi.Goruntu("bolgesel_secim",selection,W,H);
            RvmTezKaydi.Ekle("optik_bolgesel_sonuc",JsonUtility.ToJson(regional));
            if(bolgeselOptikEtkin){transmissionIndex=regional.gecirgenlik;reflectionIndex=regional.yansima;}
            var kantil=RvmKantilOptik.Hesapla(eb,ew,b,w,mask,out var kantilSecim);
            RvmTezKaydi.Ekle("optik_kantil_sonuc",JsonUtility.ToJson(kantil));
            RvmTezKaydi.Goruntu("kantil_secim",kantilSecim,W,H);
            if(kantilOptikEtkin){transmissionIndex=kantil.gecirgenlik;reflectionIndex=kantil.yansima;}
            RvmTezKaydi.Goruntu("optik_secim",kantilOptikEtkin?kantilSecim:selection,W,H);
            bool range = lengthMetres>=minLength && lengthMetres<=maxLength && widthMetres>=minWidth && widthMetres<=maxWidth;
            valid = measured.valid && present && range && (kantilOptikEtkin?kantil.gecerli:(!bolgeselOptikEtkin||regional.gecerli));
            reason = !measured.valid || !present ? measured.reason : !range ? "Outside calibrated dimension range" : kantilOptikEtkin?kantil.neden:bolgeselOptikEtkin?regional.neden:"Paired pixel measurement";
            RvmTezKaydi.Goruntu("maske",mask,W,H);
            RvmTezKaydi.Ekle("optik_sonuc",JsonUtility.ToJson(new OptikSonuc {boyMetre=lengthMetres,enMetre=widthMetres,doluluk=fill,gecirgenlik=transmissionIndex,yansima=reflectionIndex,hamGecirgenlik=rawT,hamYansima=rawY,yontem=kantilOptikEtkin?RvmKantilOptik.Surum:bolgeselOptikEtkin?RvmBolgeselOptik.Surum:"tum-maske-v1",gecerli=valid,nesne=present,neden=reason,gurultu=pixelNoiseEnabled,gurultuTohumu=pixelNoiseSeed,gurultuGenligi=pixelNoiseAmplitude}));
        }
        void OnDestroy()
        {
            disposed=true;captureGeneration++;
            RenderPipelineManager.endCameraRendering -= FrameReady;
            if(sensorCamera) { sensorCamera.targetTexture=null; sensorCamera.enabled=false; }
            if(target) { target.Release(); Destroy(target); }
            if(preview) Destroy(preview);
            if(backgroundMaterial) Destroy(backgroundMaterial);
        }
    }
}
