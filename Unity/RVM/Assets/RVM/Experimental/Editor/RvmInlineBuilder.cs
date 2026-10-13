using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RVM.Experimental
{
    public static class RvmInlineBuilder
    {
        public const string ScenePath="Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity";
        [MenuItem("RVM/Experimental/Create integrated v106 scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)
                throw new System.Exception("Clean experimental Edit mode required");
            if(System.IO.File.Exists(ScenePath))throw new System.Exception("Integrated scene already exists; do not overwrite");
            var scene=EditorSceneManager.OpenScene("Assets/RVM/Scenes/RVM_DigitalTwin.unity");
            var twin=Object.FindAnyObjectByType<RvmDigitalTwin>();
            var station=twin.gameObject.AddComponent<RvmInlineMeasurement>();station.twin=twin;twin.inlineMeasurement=station;
            twin.bridgeUrl="http://127.0.0.1:8766/exchange";twin.autoFeed=false;
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="V106_Contact_Scale";plate.layer=29;
            plate.transform.position=new Vector3(-.55f,1.125f,0);plate.transform.localScale=new Vector3(.42f,.03f,.22f);
            plate.AddComponent<Rigidbody>().isKinematic=true;station.scale=plate.AddComponent<RvmLoadCell>();
            var bg=GameObject.CreatePrimitive(PrimitiveType.Cube);bg.name="V106_Optical_Background";bg.layer=30;
            bg.transform.position=new Vector3(-.55f,1.128f,0);bg.transform.localScale=new Vector3(.45f,.002f,.25f);
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            bg.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/RVM/Experimental/Lab_Background.mat");
            var camera=new GameObject("V106_Measurement_Camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(-.55f,2.14f,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.orthographic=true;camera.nearClipPlane=.05f;camera.farClipPlane=2;
            camera.cullingMask=(1<<30)|(1<<31);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.enabled=false;
            station.optical=twin.gameObject.AddComponent<RvmOpticalStation>();station.optical.sensorCamera=camera;station.optical.background=bg.GetComponent<Renderer>();
            var probe=new GameObject("V106_Inductive_Magnetic_Probe");probe.transform.position=new Vector3(-.55f,1.13f,0);
            station.probe=probe.AddComponent<RvmInductiveProbe>();
            foreach(var light in Object.FindObjectsByType<Light>())light.cullingMask&=~(1<<31);
            var fixedLight=new GameObject("V106_Calibrated_Illumination").AddComponent<Light>();fixedLight.type=LightType.Directional;
            fixedLight.intensity=1;fixedLight.cullingMask=1<<31;fixedLight.shadows=LightShadows.None;fixedLight.transform.rotation=Quaternion.Euler(65,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.15f,.15f,.15f);
            station.petPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RVM/Experimental/Models/B101_Physical.prefab");
            station.glassPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RVM/Experimental/Models/B102_Physical.prefab");
            station.aluminiumPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RVM/Experimental/Models/B103_Physical.prefab");
            if(!station.petPrefab||!station.glassPrefab||!station.aluminiumPrefab)throw new System.Exception("Missing prepared physical prefabs");
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
        }
    }
}
