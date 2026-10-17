using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Delivery integrity check only; never enters Play or sends PLC commands.
public static class RvmTeslimDogrulama
{
    [MenuItem("RVM/Paylaşım projesini doğrula")]
    public static void Calistir()
    {
        string root=Path.Combine(Application.persistentDataPath,"Dogrulama");
        Directory.CreateDirectory(root);
        string output=Path.Combine(root,"UNITY_DOGRULAMA.txt");
        try {
            var catalog=AssetDatabase.LoadAssetAtPath<RVM.Experimental.RvmProductCatalog>("Assets/RVM/Products24/Catalog.asset");
            if(catalog==null||catalog.entries.Length!=24)throw new Exception("Catalog count");
            if(catalog.entries.Select(e=>e.id).Distinct().Count()!=24)throw new Exception("Duplicate catalog identity");
            foreach(var e in catalog.entries) {
                if(e.prefab==null)throw new Exception("Missing prefab "+e.id);
                if(e.prefab.GetComponent<Rigidbody>()==null)throw new Exception("Missing physics "+e.id);
                if(e.prefab.GetComponentsInChildren<Renderer>(true).Length==0)throw new Exception("Missing renderer "+e.id);
                foreach(var renderer in e.prefab.GetComponentsInChildren<Renderer>(true))
                    if(renderer.sharedMaterials.Any(m=>m==null))throw new Exception("Missing material "+e.id);
            }
            var scene=EditorSceneManager.OpenScene("Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity");
            var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            if(objects.Any(g=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g)>0))throw new Exception("Missing scene script");
            if(!objects.Any(g=>g.GetComponent<RVM.Experimental.RvmInlineMeasurement>()!=null))throw new Exception("Station missing");
            if(!objects.Any(g=>g.GetComponent<RVM.RvmDigitalTwin>()!=null))throw new Exception("Twin missing");
            var station=UnityEngine.Object.FindFirstObjectByType<RVM.Experimental.RvmInlineMeasurement>();
            if(!station.optical||!station.probe||!station.scale||station.catalog!=catalog)throw new Exception("Measurement reference missing");
            var helper=new GameObject("Offline optical condition check");
            try {
                var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
                var original=lights.Select(l=>l.intensity).ToArray();
                var settings=helper.AddComponent<RVM.Experimental.RvmOptikAyar>();settings.Hazirla();
                if(settings.kosul!="B_yari_isik"||settings.isikCarpani!=.5f||settings.arkaPlan!=1)throw new Exception("Wrong release optical condition");
                for(int i=0;i<lights.Length;i++)if(lights[i].intensity!=original[i]*.5f)throw new Exception("Light multiplier mismatch");
            } finally {UnityEngine.Object.DestroyImmediate(helper);}
            var pixels=new Color32[64*64];
            for(int y=20;y<30;y++)for(int x=10;x<40;x++)pixels[y*64+x]=new Color32(255,255,255,255);
            var measured=RVM.RvmImageAnalysis.Analyze(pixels,64,64,.32f);
            if(!measured.valid||!measured.present||Math.Abs(measured.length-.15f)>1e-6f||Math.Abs(measured.width-.05f)>1e-6f||measured.fill!=1)throw new Exception("Silhouette extraction regression");
            Debug.Log("RVM_UPDATED_UNITY_VALIDATION_PASS");
            File.WriteAllText(output,"PASS: cleaned Unity compiles; final scene opens; 24 unique prefab/physics/render/material links; measurement references complete; B half-light default verified; extracted silhouette helper verified. Edit mode only; no PLC/product commands. Live smoke test pending.");
        } catch(Exception e) {File.WriteAllText(output,"FAIL: "+e);throw;}
    }
}
