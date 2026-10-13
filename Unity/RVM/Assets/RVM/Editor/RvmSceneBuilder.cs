using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace RVM.Editor
{
    public static class RvmSceneBuilder
    {
        const string Root="Assets/RVM/";
        [MenuItem("RVM/Dijital ikiz sahnesini oluştur")]
        public static void Build()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Önce açık sahneyi kaydedin; mevcut sahne korunuyor.");
            Directory.CreateDirectory(Root+"Materials");
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"Models/RVM_Kompakt_Ust_Pano.fbx");
            importer.globalScale=1; importer.useFileScale=true; importer.importCameras=false; importer.importLights=false;
            importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var machine=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/RVM_Kompakt_Ust_Pano.fbx"));
            machine.name="RVM_Compact_175cm_User";
            PrefabUtility.UnpackPrefabInstance(machine,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var all=machine.GetComponentsInChildren<Transform>(true);
            Transform Find(string n)=>all.First(t=>t.name==n);
            foreach(var r in machine.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials=r.sharedMaterials.Select(m=>MakeMaterial(m ? m.name : "Mat_Steel")).ToArray();
            }
            var controls=new GameObject("RVM_DigitalTwin_Controller").AddComponent<RvmDigitalTwin>();
            controls.machine=machine.transform; controls.shuttle=Find("Shuttle");
            controls.doorLeft=Find("Shuttle_Door_L");controls.doorRight=Find("Shuttle_Door_R");
            controls.rotorA=Find("Crusher_Rotor_A");controls.rotorB=Find("Crusher_Rotor_B");
            controls.bottleTemplate=Find("Bottle_Reference"); controls.belt=Find("Conveyor_Belt").GetComponent<Renderer>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="SimulationFloor";floor.transform.localScale=Vector3.one*.6f;
            floor.GetComponent<Renderer>().sharedMaterial=Material("Mat_Floor",new Color(.12f,.17f,.21f),0,.6f);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(3.4f,2.8f,4.6f);camera.transform.LookAt(new Vector3(-.12f,1.03f,0));
            camera.orthographic=true;camera.orthographicSize=1.65f;camera.nearClipPlane=.05f;camera.farClipPlane=50;
            camera.backgroundColor=new Color(.06f,.1f,.14f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.rect=new Rect(0,0,.70f,1);camera.gameObject.AddComponent<AudioListener>();
            var sun=new GameObject("Key light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;
            sun.transform.rotation=Quaternion.Euler(40,-30,0);sun.shadows=LightShadows.Soft;
            var fill=new GameObject("Fill light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.9f;fill.transform.rotation=Quaternion.Euler(55,140,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.55f,.65f);
            // Tablet sits beside the human-height inlet; cabinet remains on top and open.
            var tablet=GameObject.CreatePrimitive(PrimitiveType.Cube);tablet.name="Operator_Tablet";
            tablet.transform.position=new Vector3(-1.2f,1.4f,.30f);tablet.transform.localScale=new Vector3(.34f,.23f,.025f);
            tablet.GetComponent<Renderer>().sharedMaterial=Material("Mat_Tablet",new Color(.015f,.025f,.04f),.2f,.3f);
            var screen=new GameObject("Tablet_Status").AddComponent<TextMesh>();
            screen.transform.position=tablet.transform.position+new Vector3(.145f,.085f,.018f);
            screen.transform.rotation=Quaternion.Euler(0,180,0);screen.characterSize=.008f;screen.fontSize=48;
            screen.anchor=TextAnchor.UpperLeft;screen.color=new Color(.3f,1,.7f);screen.text="RVM / DIGITAL TWIN\nPLC OFFLINE\nOPC UA";
            controls.tabletScreen=screen;
            Time.fixedDeltaTime=.02f;
            EditorSceneManager.SaveScene(scene,Root+"Scenes/RVM_DigitalTwin.unity");
            var existing=EditorBuildSettings.scenes.Where(s=>s.path!=Root+"Scenes/RVM_DigitalTwin.unity").ToList();
            existing.Insert(0,new EditorBuildSettingsScene(Root+"Scenes/RVM_DigitalTwin.unity",true));EditorBuildSettings.scenes=existing.ToArray();
            Selection.activeGameObject=machine;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(-.1f,1,0),camera.transform.rotation,3);
            AssetDatabase.SaveAssets();
            Validate();
        }
        static Material Material(string name,Color color,float metal,float rough)
        {
            var path=Root+"Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",1-rough);return mat;
        }
        static Material MakeMaterial(string name)
        {
            name=name.Replace(" (Instance)","");
            Color c=new Color(.55f,.59f,.64f);float metal=.65f;
            if(name.Contains("Heavy")){c=new Color(.18f,.21f,.24f);metal=.9f;}
            if(name.Contains("Plastic")||name.Contains("Rubber")||name.Contains("Belt")){c=new Color(.045f,.06f,.07f);metal=0;}
            if(name.Contains("Powder")){c=new Color(.73f,.77f,.81f);metal=.2f;}
            if(name.Contains("Blue")){c=new Color(.02f,.25f,.6f);metal=0;}
            if(name.Contains("Yellow")){c=new Color(.9f,.65f,.06f);metal=0;}
            if(name.Contains("Red")){c=new Color(.8f,.04f,.03f);metal=0;}
            if(name.Contains("Green")){c=new Color(.02f,.7f,.12f);metal=0;}
            if(name.Contains("Clear")||name.Contains("PET")){c=new Color(.4f,.73f,.9f,.65f);metal=0;}
            if(name.Contains("White"))c=Color.white;
            var mat=Material(name,c,metal,.4f);
            if(name.Contains("Emissive")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",c*(name.Contains("White")?2:1.5f));}
            if(name.Contains("Belt"))
            {
                var tex=new Texture2D(32,4);tex.name="Belt_Stripes";
                for(int y=0;y<4;y++)for(int x=0;x<32;x++)tex.SetPixel(x,y,x<2?new Color(.17f,.2f,.22f):new Color(.04f,.055f,.06f));
                tex.Apply();tex.wrapMode=TextureWrapMode.Repeat;
                var path=Root+"Materials/Belt_Stripes.asset";
                if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))AssetDatabase.CreateAsset(tex,path);
                mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);mat.mainTextureScale=new Vector2(15,1);
            }
            EditorUtility.SetDirty(mat);return mat;
        }
        [MenuItem("RVM/Sahneyi doğrula")]
        public static void Validate()
        {
            var twin=UnityEngine.Object.FindFirstObjectByType<RvmDigitalTwin>();
            if(!twin)throw new Exception("Controller missing");
            if(twin.doorLeft.parent!=twin.shuttle||twin.doorRight.parent!=twin.shuttle)throw new Exception("Door hierarchy invalid");
            foreach(var t in twin.machine.GetComponentsInChildren<Transform>())
                if(Vector3.Distance(t.localScale,Vector3.one)>.005f)throw new Exception("Unapplied scale: "+t.name);
            if(RvmDigitalTwin.BinAt(-.23f)!=1||RvmDigitalTwin.BinAt(-.77f)!=2||RvmDigitalTwin.BinAt(.31f)!=3)throw new Exception("Bin mapping invalid");
            Debug.Log("RVM validation PASS: scale=1, separate actuators, hinge hierarchy, bin codes 1 PET / 2 ALU / 3 CAM.");
        }
    }
}
