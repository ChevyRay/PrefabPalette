using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;

[CustomEditor(typeof(PrefabPalette))]
public class PrefabPaletteEditor : Editor
{
    static string prevFolder = "Assets";

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
            RefreshWindow();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        var prefabs = new List<GameObject>(palette.prefabs);
        for (int i = 0; i < prefabs.Count - 1; ++i)
        {
            if (prefabs[i] != null)
            {
                int j = prefabs.IndexOf(prefabs[i], i + 1);
                if (j > i && i != j)
                {
                    prefabs.RemoveAt(j);
                    --i;
                }
            }
            else
                prefabs.RemoveAt(i--);
        }

        GUI.enabled = prefabs.Count != palette.prefabs.Length;
        if (GUILayout.Button(new GUIContent("Cleanup", "Clear nulls and duplicates"), EditorStyles.miniButton))
        {
            Undo.RecordObject(target, "cleanup palette");
            palette.prefabs = prefabs.ToArray();
            RefreshWindow();
        }

        GUI.enabled = !string.IsNullOrEmpty(palette.prevFolder) && AssetDatabase.IsValidFolder(palette.prevFolder);
        var content = new GUIContent("↺ Folder");
        if (GUI.enabled)
            content.tooltip = "Reload: " + palette.prevFolder;
        if (GUILayout.Button(content, EditorStyles.miniButton))
            AddFolder(palette.prevFolder);

        GUI.enabled = true;
        if (GUILayout.Button(new GUIContent("＋ Folder", "Add all prefabs in a folder"), EditorStyles.miniButton))
        {
            var folder = EditorUtility.OpenFolderPanel("Select Folder", prevFolder, "");
            if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath, System.StringComparison.Ordinal))
            {
                folder = folder.Remove(0, Application.dataPath.Length - 6);
                AddFolder(folder);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void RefreshWindow()
    {
        var wins = Resources.FindObjectsOfTypeAll<PrefabPaletteWindow>();
        foreach (var win in wins)
            win.Repaint();
    }

    void AddFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            prevFolder = folder;
            var objs = new List<GameObject>(palette.prefabs);
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new string[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null && !objs.Contains(obj))
                    objs.Add(obj);
            }
            if (objs.Count > 0)
            {
                Undo.RecordObject(target, "add prefabs");
                palette.prefabs = objs.ToArray();
                palette.prevFolder = folder;
                RefreshWindow();
            }
        }
    }

    PrefabPalette palette
    {
        get { return (PrefabPalette)target; }
    }
}
