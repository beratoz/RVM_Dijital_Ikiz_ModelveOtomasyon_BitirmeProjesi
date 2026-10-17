using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RvmBagimlilikDenetimi
{
    public static void Calistir()
    {
        var roots = new[] {"Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity", "Assets/RVM/Products24/Catalog.asset"}
            .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/Settings/") || p.Contains("/Resources/")))
            .Concat(Directory.GetFiles("ProjectSettings", "*.asset")).Distinct().ToArray();
        var paths = AssetDatabase.GetDependencies(roots, true).OrderBy(p => p).ToArray();
        var folder = Path.Combine(Application.persistentDataPath, "Dogrulama");
        Directory.CreateDirectory(folder);
        var output = Path.Combine(folder, "UNITY_BAGIMLILIKLARI.txt");
        File.WriteAllLines(output, paths);
        RvmTeslimDogrulama.Calistir();
    }
}
