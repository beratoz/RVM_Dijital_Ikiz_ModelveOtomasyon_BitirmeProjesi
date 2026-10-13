using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RVM.Experimental
{
    public static class RvmPhysicalPrefabBuilder
    {
        // Asset preparation only. These parameters build the physical world, not sensor outputs.
        public static void BuildReferencePrefabs()
        {
            if(EditorApplication.isPlaying)throw new System.Exception("Experimental Edit mode required");
            string dir="Assets/RVM/Experimental/Models/";
            string[] ids={"B101","B102","B103"};float[] masses={.028f,.235f,.017f},alpha={.25f,.38f,1};
            var prefabs=new GameObject[3];
            for(int i=0;i<3;i++)
            {
                string path=dir+ids[i]+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.isReadable=true;importer.SaveAndReimport();
                var root=new GameObject(ids[i]+"_Physical");
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                model.transform.SetParent(root.transform,false);
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor",new Color(.35f,.55f,.65f,alpha[i]));
                material.SetFloat("_Metallic",i==2?1:0);material.SetFloat("_Smoothness",.8f);
                if(alpha[i]<1)
                {
                    material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
                    material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
                }
                string matPath=dir+ids[i]+"_URP.mat";
                if(AssetDatabase.LoadAssetAtPath<Material>(matPath))throw new System.Exception("Do not overwrite prepared materials");
                AssetDatabase.CreateAsset(material,matPath);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
                // Box contact proxy avoids high-poly convex hull limits. Camera still renders the actual FBX mesh.
                Bounds bounds=new Bounds();bool first=true;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>()) {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
                var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
                var rb=root.AddComponent<Rigidbody>();rb.mass=masses[i];rb.sleepThreshold=0;rb.solverIterations=12;rb.solverVelocityIterations=8;
                prefabs[i]=PrefabUtility.SaveAsPrefabAsset(root,dir+ids[i]+"_Physical.prefab");Object.DestroyImmediate(root);
            }
            var lab=Object.FindAnyObjectByType<RvmMeasurementLab>();lab.blindTestPrefabs=prefabs;lab.importedPrefab=prefabs[0];
            EditorUtility.SetDirty(lab);EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);EditorSceneManager.SaveScene(lab.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
    }
}
