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
        public bool pixelNoiseEnabled;
        public int pixelNoiseSeed=106, pixelNoiseAmplitude=1;
        System.Random pixelRandom;
        Color32[] emptyBlack, emptyWhite, pixels;
        RenderTexture target;
        Material backgroundMaterial;
        bool framePending, frameReceived;
        int captureGeneration;
        bool disposed;
        const int W = 800, H = 400;

        void Awake()
        {
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
            backgroundMaterial.SetColor("_BaseColor",colour);
            captureGeneration++;
            frameReceived = false; pixels = null;
            yield return null;
            framePending = true; sensorCamera.enabled = true;
            float timeout = Time.realtimeSinceStartup + 2;
            while (!frameReceived && Time.realtimeSinceStartup < timeout) yield return null;
            if(!frameReceived)captureGeneration++;
            framePending = false; sensorCamera.enabled = false;
        }
        public IEnumerator CalibrateEmpty()
        {
            if (busy) yield break;
            busy = true; valid = calibrated = false;
            yield return Capture(Color.black); emptyBlack = pixels;
            yield return Capture(Color.white); emptyWhite = pixels;
            calibrated = emptyBlack != null && emptyWhite != null;
            reason = calibrated ? "Empty references captured" : "Calibration render timeout";
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
            if (black == null || white == null) { reason = "Render timeout"; busy = false; yield break; }
            Analyze(emptyBlack,emptyWhite,black,white);
            capturedAt=Time.realtimeSinceStartup;
            busy = false;
        }
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
            lengthMetres = measured.length; widthMetres = measured.width; fill = measured.fill;
            present = measured.present;
            transmissionIndex = count>0 ? (float)(transmission/count) : 0;
            reflectionIndex = count>0 ? (float)(reflection/count) : 0;
            bool range = lengthMetres>=minLength && lengthMetres<=maxLength && widthMetres>=minWidth && widthMetres<=maxWidth;
            valid = measured.valid && present && range;
            reason = !measured.valid || !present ? measured.reason : !range ? "Outside calibrated dimension range" : "Paired pixel measurement";
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
