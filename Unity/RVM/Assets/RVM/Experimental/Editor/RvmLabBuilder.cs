using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RVM.Experimental
{
    public static class RvmLabBuilder
    {
        public const string ScenePath="Assets/RVM/Scenes/RVM_MeasurementLab_v106.unity";
        [MenuItem("RVM/Experimental/Create measurement laboratory")]
        public static void Build()
        {
            if(!System.IO.File.Exists("Assets/RVM/Scripts/RvmDigitalTwin.cs"))throw new System.Exception("RVM project required");
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.Exception("Need clean Edit mode scene");
            if(System.IO.File.Exists(ScenePath))throw new System.Exception("Lab already exists; open it instead of overwriting");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Independent_Measurement_Laboratory");var lab=root.AddComponent<RvmMeasurementLab>();
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Force_Scale_Platform";plate.layer=29;
            plate.transform.position=new Vector3(0,-.015f,0);plate.transform.localScale=new Vector3(.42f,.03f,.22f);
            var platformBody=plate.AddComponent<Rigidbody>();platformBody.isKinematic=true;
            lab.scale=plate.AddComponent<RvmLoadCell>();
            var backdrop=GameObject.CreatePrimitive(PrimitiveType.Cube);backdrop.name="Optical_Background";backdrop.layer=30;
            backdrop.transform.position=new Vector3(0,-.012f,0);backdrop.transform.localScale=new Vector3(.45f,.002f,.25f);
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            var bg=new Material(Shader.Find("Universal Render Pipeline/Unlit"));bg.SetColor("_BaseColor",Color.white);
            AssetDatabase.CreateAsset(bg,"Assets/RVM/Experimental/Lab_Background.mat");backdrop.GetComponent<Renderer>().sharedMaterial=bg;
            var rig=new GameObject("Paired_Pixel_Camera");var camera=rig.AddComponent<Camera>();
            camera.transform.position=new Vector3(0,1,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.orthographic=true;camera.nearClipPlane=.05f;camera.farClipPlane=2;
            camera.cullingMask=(1<<30)|(1<<31);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.enabled=false;
            lab.optical=root.AddComponent<RvmOpticalStation>();lab.optical.sensorCamera=camera;lab.optical.background=backdrop.GetComponent<Renderer>();
            var main=new GameObject("Main Camera");main.tag="MainCamera";var overview=main.AddComponent<Camera>();
            main.transform.position=new Vector3(.45f,.5f,-.55f);main.transform.LookAt(Vector3.zero);
            overview.clearFlags=CameraClearFlags.SolidColor;overview.backgroundColor=new Color(.035f,.05f,.07f);overview.nearClipPlane=.01f;
            main.AddComponent<AudioListener>();
            var light=new GameObject("Fixed_Front_Illumination").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;
            light.transform.rotation=Quaternion.Euler(65,-30,0);light.shadows=LightShadows.None;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.15f,.15f,.15f);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
