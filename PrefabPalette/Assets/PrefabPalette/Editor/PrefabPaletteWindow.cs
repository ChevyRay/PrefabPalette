using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class PrefabPaletteWindow : EditorWindow
{
    [MenuItem("Window/Prefab Palette", priority = 10001)]
    static void CreateWindow()
    {
        var win = GetWindow<PrefabPaletteWindow>("Prefab Palette");
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Gizmos/PrefabPalette Icon.png");
        win.titleContent = new GUIContent("Prefab Palette", icon);
        win.Show();
    }

    public enum RotationType
    {
        Prefab,
        Custom,
        Random
    }

    public enum ScaleType
    {
        Prefab,
        Custom,
        Random,
        RandomXYZ
    }

    PrefabPalette palette;
    GameObject selected;
    Vector2 prefabScroll;

    List<PrefabPalette> palettes = new List<PrefabPalette>();
    string[] paletteNames;

    Vector2 mousePos;
    GameObject placingObj;
    Vector3 placePos;
    Vector3 placeNor;

    bool optionsToggle = true;

    int raycastMask;
    Transform parentTo;
    bool onlyUpwards;

    bool snap;
    float snapValue = 1f;

    RotationType rotationMode;
    Vector3 customRotation;
    Vector3 minRotation;
    Vector3 maxRotation;

    ScaleType scaleMode;
    Vector3 customScale = Vector3.one;
    Vector3 minScale = Vector3.one;
    Vector3 maxScale = Vector3.one;
    float minScaleU = 1f;
    float maxScaleU = 1f;

    void OnEnable()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        SceneView.onSceneGUIDelegate += OnSceneGUI;

        Undo.undoRedoPerformed -= Repaint;
        Undo.undoRedoPerformed += Repaint;

        raycastMask = 1;

        wantsMouseMove = true;
        wantsMouseEnterLeaveWindow = true;
        LoadPalettes();
    }

    void OnDisable()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        Undo.undoRedoPerformed -= Repaint;
    }

    void OnSelectionChange()
    {
        LoadPalettes();
    }

    void OnFocus()
    {
        LoadPalettes();
    }

    void LoadPalettes()
    {
        palettes.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:PrefabPalette"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var pal = AssetDatabase.LoadAssetAtPath<PrefabPalette>(path);
            if (pal != null)
                palettes.Add(pal);
        }

        paletteNames = new string[palettes.Count];
        for (int i = 0; i < palettes.Count; ++i)
            paletteNames[i] = palettes[i].name;
        
        if (palette != null && !palettes.Contains(palette))
            palette = null;

        if (palette == null && palettes.Count > 0)
            palette = palettes[0];
    }

    void Deselect()
    {
        EditorApplication.delayCall += () => {
            selected = null;
            Repaint();
        };
    }

    void OnGUI()
    {
        EditorGUILayout.Space();

        int paletteIndex = palettes.IndexOf(palette);
        paletteIndex = EditorGUILayout.Popup("Palette", paletteIndex, paletteNames);
        palette = paletteIndex < 0 ? null : palettes[paletteIndex];

        if (palette == null)
            return;
            
        if (ev.isMouse)
        {
            mousePos = ev.mousePosition;
            Repaint();
        }

        optionsToggle = EditorGUILayout.Foldout(optionsToggle, "Options");
        if (optionsToggle)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical();

            raycastMask = LayerMaskField("Raycast Mask", raycastMask);

            var par = EditorGUILayout.ObjectField("Parent To", parentTo, typeof(Transform), true) as Transform;
            if (par != parentTo)
                if (par == null || (PrefabUtility.GetCorrespondingObjectFromSource(par) == null && PrefabUtility.GetPrefabObject(par) == null))
                    parentTo = par;

            onlyUpwards = EditorGUILayout.Toggle("Only Up Normals", onlyUpwards);

            //Snapping
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Snap");
            snap = EditorGUILayout.Toggle(snap, GUILayout.Width(15f));
            GUI.enabled = snap;
            snapValue = EditorGUILayout.FloatField(snapValue);
            snapValue = Mathf.Max(snapValue, 0f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            //Rotation mode
            rotationMode = (RotationType)EditorGUILayout.EnumPopup("Rotation Mode", rotationMode);
            if (rotationMode == RotationType.Random)
            {
                minRotation = EditorGUILayout.Vector3Field("Min Rotation", minRotation);
                maxRotation = EditorGUILayout.Vector3Field("Max Rotation", maxRotation);
            }
            else if (rotationMode == RotationType.Custom)
                customRotation = EditorGUILayout.Vector3Field("Rotation", customRotation);

            //Scale mode
            scaleMode = (ScaleType)EditorGUILayout.EnumPopup("Scale Mode", scaleMode);
            if (scaleMode == ScaleType.Random)
            {
                minScale = EditorGUILayout.Vector3Field("Min Scale", minScale);
                maxScale = EditorGUILayout.Vector3Field("Max Scale", maxScale);
            }
            else if (scaleMode == ScaleType.RandomXYZ)
            {
                minScaleU = EditorGUILayout.FloatField("Min Scale", minScaleU);
                maxScaleU = EditorGUILayout.FloatField("Max Scale", maxScaleU);
            }
            else if (scaleMode == ScaleType.Custom)
                customScale = EditorGUILayout.Vector3Field("Scale", customScale);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        var header = EditorGUILayout.GetControlRect();
        GUI.Label(header, "Prefabs", EditorStyles.boldLabel);
        header.y += header.height - 1f;
        header.height = 1f;
        EditorGUI.DrawRect(header, EditorStyles.label.normal.textColor);

        GUILayout.Space(2f);

        GUI.enabled = selected != null;
        if (GUILayout.Button("Stop Placement (ESC)", EditorStyles.miniButton))
            Deselect();
        GUI.enabled = true;
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            Deselect();
            
        var buttonHeight = EditorGUIUtility.singleLineHeight * 2f;
        var heightStyle = GUILayout.Height(buttonHeight);

        var lastRect = GUILayoutUtility.GetLastRect();
        var scrollMouse = mousePos;
        scrollMouse.x -= lastRect.xMin - prefabScroll.x;
        scrollMouse.y -= lastRect.yMax - prefabScroll.y;

        prefabScroll = EditorGUILayout.BeginScrollView(prefabScroll);

        foreach (var prefab in palette.prefabs)
        {
            if (prefab == null)
                continue;

            var rect = EditorGUILayout.GetControlRect(heightStyle);

            var bgRect = rect;
            bgRect.x -= 1f;
            bgRect.y -= 1f;
            bgRect.width += 2f;
            bgRect.height += 2f;
            if (prefab == selected)
            {
                EditorGUI.DrawRect(bgRect, new Color32(0x42, 0x80, 0xe4, 0xff));
            }
            else
            {
                EditorGUIUtility.AddCursorRect(bgRect, MouseCursor.Link);

                if (bgRect.Contains(scrollMouse))
                {
                    EditorGUI.DrawRect(bgRect, new Color32(0x42, 0x80, 0xe4, 0x40));
                    if (ev.type == EventType.MouseDown)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            selected = prefab;
                            SceneView.RepaintAll();
                        };
                    }
                }
            }

            var iconRect = new Rect(rect.x, rect.y, rect.height, rect.height);

            var icon = AssetPreview.GetAssetPreview(prefab);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true, 1f, Color.white, Vector4.zero, Vector4.one * 4f);
            else
                EditorGUI.DrawRect(iconRect, EditorStyles.label.normal.textColor * 0.25f);

            var labelRect = rect;
            labelRect.x += iconRect.width + 4f;
            labelRect.width -= iconRect.width + 4f;
            labelRect.height = EditorGUIUtility.singleLineHeight;
            labelRect.y += (buttonHeight - labelRect.height) * 0.5f;
            var labelStyle = prefab == selected ? EditorStyles.whiteBoldLabel : EditorStyles.label;
            GUI.Label(labelRect, prefab.name, labelStyle);
        }

        EditorGUILayout.Space();
        EditorGUILayout.EndScrollView();

        if (AssetPreview.IsLoadingAssetPreviews())
            Repaint();
    }

    void OnSceneGUI(SceneView view)
    {
        view.wantsMouseMove = true;
        view.wantsMouseEnterLeaveWindow = true;

        if (selected == null)
        {
            ClearPlacingObj();
            return;
        }

        int control = GUIUtility.GetControlID(FocusType.Passive);

        HandleUtility.Repaint();

        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            Deselect();

        if (ev.isMouse)
            mousePos = ev.mousePosition;
            
        if (ev.type == EventType.MouseLeaveWindow)
            ClearPlacingObj();
        else if (ev.isMouse || ev.type == EventType.MouseEnterWindow)
            UpdatePlacingObj();

        switch (ev.type)
        {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(control);
                break;
            case EventType.MouseDown:
                if (ev.button == 0)
                {
                    Tools.current = Tool.None;
                    ev.Use();
                    PlaceObj();
                }
                break;
            case EventType.MouseUp:
                if (ev.button == 0)
                {
                    Tools.current = Tool.None;
                    ev.Use();
                }
                break;
        }

        if (placingObj != null)
            Handles.RectangleHandleCap(control, placePos, Quaternion.FromToRotation(Vector3.forward, placeNor), 0.45f, EventType.Repaint);

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(4f, 4f, 300f, EditorGUIUtility.singleLineHeight * 3f));
        var r = GUILayoutUtility.GetRect(300f, EditorGUIUtility.singleLineHeight);
        GUI.Label(r, "X: " + placePos.x.ToString("0.00"), EditorStyles.whiteBoldLabel);
        r = GUILayoutUtility.GetRect(300f, EditorGUIUtility.singleLineHeight);
        r.y -= 4f;
        GUI.Label(r, "Y: " + placePos.y.ToString("0.00"), EditorStyles.whiteBoldLabel);
        r = GUILayoutUtility.GetRect(300f, EditorGUIUtility.singleLineHeight);
        r.y -= 8f;
        GUI.Label(r, "Z: " + placePos.z.ToString("0.00"), EditorStyles.whiteBoldLabel);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    void ClearPlacingObj()
    {
        if (placingObj != null)
        {
            DestroyImmediate(placingObj);
            placingObj = null;
        }
    }

    void UpdatePlacingObj()
    {
        if (placingObj != null)
        {
            var prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(placingObj);
            if (selected != prefab)
                ClearPlacingObj();
        }

        if (placingObj == null && selected != null)
        {
            placingObj = (GameObject)PrefabUtility.InstantiatePrefab(selected, SceneManager.GetActiveScene());
            placingObj.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
        }

        if (placingObj == null)
            return;

        var ray = HandleUtility.GUIPointToWorldRay(mousePos);
        var hits = Physics.RaycastAll(ray, float.MaxValue, raycastMask);
        var nearest = new RaycastHit();
        if (hits.Length > 0)
        {
            float nearestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == placingObj || hit.collider.transform.IsChildOf(placingObj.transform))
                    continue;
                if (onlyUpwards && Vector3.Dot(hit.normal, Vector3.up) <= 0f)
                    continue;
                if (hit.distance < nearestDist)
                {
                    nearestDist = hit.distance;
                    nearest = hit;
                }
            }
        }
        if (nearest.collider != null)
        {
            placePos = nearest.point;
            placeNor = nearest.normal;
        }
        else
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                placePos = ray.GetPoint(enter);
                placeNor = Vector3.up;
            }
        }

        var rot = Quaternion.LookRotation(placeNor) * Quaternion.Euler(90f, 0f, 0f);

        if (snap && snapValue > 0f)
        {
            var pos = Quaternion.Inverse(rot) * placePos;
            pos.x = Mathf.Round(pos.x / snapValue) * snapValue;
            pos.z = Mathf.Round(pos.z / snapValue) * snapValue;
            placePos = rot * pos;
        }

        if (rotationMode == RotationType.Custom)
            rot *= Quaternion.Euler(customRotation);
        else
            rot *= selected.transform.localRotation;

        placingObj.transform.localPosition = placePos;
        placingObj.transform.localRotation = rot;

        if (scaleMode == ScaleType.Custom)
            placingObj.transform.localScale = customScale;
        else
            placingObj.transform.localScale = selected.transform.localScale;
    }

    void PlaceObj()
    {
        if (placingObj == null)
            return;

        var t = placingObj.transform;
        placingObj.hideFlags = HideFlags.None;

        Undo.RegisterCreatedObjectUndo(placingObj, "place object");

        placingObj = null;
        UpdatePlacingObj();

        if (rotationMode == RotationType.Random)
        {
            var rot1 = t.localRotation;
            var rot2 = Quaternion.Euler(
                minRotation.x + (maxRotation.x - minRotation.x) * Random.value,
                minRotation.y + (maxRotation.y - minRotation.y) * Random.value,
                minRotation.z + (maxRotation.z - minRotation.z) * Random.value
            );
            t.localRotation = rot1 * rot2;
        }

        if (parentTo != null)
        {
            var pos = t.localPosition;
            var rot = t.localRotation;
            t.parent = parentTo;
            t.position = pos;
            t.rotation = rot;
        }

        if (scaleMode == ScaleType.Random)
        {
            t.localScale = new Vector3(
                minScale.x + (maxScale.x - minScale.x) * Random.value,
                minScale.y + (maxScale.y - minScale.y) * Random.value,
                minScale.z + (maxScale.z - minScale.z) * Random.value
            );
        }
        else if (scaleMode == ScaleType.RandomXYZ)
        {
            float s = minScaleU + (maxScaleU - minScaleU) * Random.value;
            t.localScale = new Vector3(s, s, s);
        }
    }

    public Event ev
    {
        get { return Event.current; }
    }

    static int LayerMaskField(string label, int layerMask)
    {
        var layers = new List<string>();
        var indices = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (layerName != "")
            {
                layers.Add(layerName);
                indices.Add(i);
            }
        }
        int maskWithoutEmpty = 0;
        for (int i = 0; i < indices.Count; i++)
        {
            if (((1 << indices[i]) & layerMask) > 0)
                maskWithoutEmpty |= 1 << i;
        }
        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
        int mask = 0;
        for (int i = 0; i < indices.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
                mask |= 1 << indices[i];
        }
        layerMask = mask;
        return layerMask;
    }
}
