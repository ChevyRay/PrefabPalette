using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class Build
{
    [MenuItem("Build/Prefab Palette")]
	public static void BuildPackage()
    {
        var guids = AssetDatabase.FindAssets("", new string[]{
            "Assets/PrefabPalette",
            "Assets/Gizmos"
        });

        var assets = new string[guids.Length];
        for (int i = 0; i < guids.Length; ++i)
            assets[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

        var file = EditorUtility.SaveFilePanel("Export Package", "Assets/../..", "PrefabPalette", "unitypackage");
        if (!string.IsNullOrEmpty(file))
            AssetDatabase.ExportPackage(assets, file, ExportPackageOptions.Default);
    }
}
