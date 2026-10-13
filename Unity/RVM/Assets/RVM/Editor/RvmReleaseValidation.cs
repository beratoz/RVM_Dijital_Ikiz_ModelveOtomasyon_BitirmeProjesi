using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RVM.Editor
{
    public static class RvmReleaseValidation
    {
        // Also usable with Unity -batchmode -executeMethod RVM.Editor.RvmReleaseValidation.Run
        [MenuItem("RVM/Paylaşım projesini doğrula")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                    throw new Exception("Missing script: " + node.name);
                foreach (var renderer in node.GetComponents<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                        throw new Exception("Missing material/shader: " + node.name);
            }
            var twin = UnityEngine.Object.FindFirstObjectByType<RvmDigitalTwin>();
            if (twin == null || twin.machine == null || twin.shuttle == null || twin.doorLeft == null ||
                twin.doorRight == null || twin.rotorA == null || twin.rotorB == null ||
                twin.bottleTemplate == null || twin.belt == null || twin.tabletScreen == null)
                throw new Exception("RVM scene references are incomplete.");
            RvmSceneBuilder.Validate();
            var station = twin.inlineMeasurement;
            if (station == null || station.scale == null || station.optical == null || station.probe == null ||
                station.optical.sensorCamera == null || station.optical.background == null ||
                station.petPrefab == null || station.glassPrefab == null || station.aluminiumPrefab == null)
                throw new Exception("v106 measurement station references are incomplete.");
            if (twin.bridgeUrl != "http://127.0.0.1:8766/exchange")
                throw new Exception("Unexpected bridge URL in release scene.");
            foreach (var prefab in new[] { station.petPrefab, station.glassPrefab, station.aluminiumPrefab })
            {
                var body = prefab.GetComponent<Rigidbody>();
                if (body == null || body.mass <= 0 || !body.useGravity ||
                    prefab.GetComponentsInChildren<Rigidbody>().Length != 1 || prefab.GetComponentsInChildren<Collider>().Length == 0)
                    throw new Exception("Invalid physical specimen: " + prefab.name);
            }
            Debug.Log("RVM v106 RELEASE VALIDATION PASS: scene, scripts, materials, camera, scale, probe and specimen references.");
        }
    }
}
