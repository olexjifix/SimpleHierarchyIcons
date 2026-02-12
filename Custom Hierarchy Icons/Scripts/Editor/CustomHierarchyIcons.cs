using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

using System.IO;


    /// <summary>
    /// Adds icons to the hierarchy for components of interest.
    /// </summary>
    /// <remarks>
    /// This class uses reflection to find all MonoBehaviour types with the CustomEditorHierarchyIcon attribute.
    /// It then loads the icon texture from the specified path and adds it to the hierarchy.
    /// </remarks>
    [InitializeOnLoad]
    public class CustomHierarchyIcons
    {
        private static Dictionary<Type, GUIContent> typeIcons = new Dictionary<Type, GUIContent>();

        // cached game object information
        static Dictionary<int, List<GUIContent>> labeledObjects = new Dictionary<int, List<GUIContent>>();
        static HashSet<int> unlabeledObjects = new HashSet<int>();
        static GameObject[] previousSelection = null; // used to update state on deselect

        // set up all callbacks needed
        static CustomHierarchyIcons()
        {
            InitializeTypeIcons(); // Populate typeIcons AFTER setting iconsBasePath
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;

            // callbacks for when we want to update the object GUI state:
            ObjectFactory.componentWasAdded += c => UpdateObject(c.gameObject.GetInstanceID());
            // there's no componentWasRemoved callback, but we can use selection as a proxy:
            Selection.selectionChanged += OnSelectionChanged;

            EditorApplication.quitting += ClearCache;
        }

    static void OnHierarchyGUI(int id, Rect rect)
    {
        if (unlabeledObjects.Contains(id))
            return;

        GameObject obj = EditorUtility.InstanceIDToObject(id) as GameObject;
        if (obj == null)
            return;
        List<GUIContent> icons = new List<GUIContent>();

        // Check all components for icons (changed from GetComponents<MonoBehaviour> to GetComponents<Component>)
        foreach (var component in obj.GetComponents<Component>())
        {
            if (component == null)
                continue; // Handle missing script components

            Type type = component.GetType();
            if (typeIcons.TryGetValue(type, out GUIContent icon))
            {
                icons.Add(icon);
            }
        }

        DrawIcons(rect, icons);
    }

    static void DrawIcons(Rect rect, List<GUIContent> icons)
    {
        if (icons == null || icons.Count == 0)
            return;

        const float iconSize = 16f;
        const float spacing = 2f;
        const int maxIconsToShow = 10;

        int iconsToDisplay = Mathf.Min(icons.Count, maxIconsToShow);
        bool hasMore = icons.Count > maxIconsToShow;

        // Calculate total width including the count label if needed
        float iconsWidth = iconsToDisplay * iconSize + (iconsToDisplay - 1) * spacing;
        float countWidth = hasMore ? 30f : 0f; // Width for "+X" label
        float totalWidth = iconsWidth + (hasMore ? spacing + countWidth : 0f);

        // Start position for icons
        Rect iconRect = new Rect(rect.xMax - totalWidth, rect.y, iconSize, iconSize);

        // Draw icons
        for (int i = 0; i < iconsToDisplay; i++)
        {
            GUI.Label(iconRect, icons[i]);
            iconRect.x += iconSize + spacing;
        }

        // Show count label beside the icons (not overlapping)
        if (hasMore)
        {
            Rect countRect = new Rect(iconRect.x, rect.y, countWidth, iconSize);
            EditorGUI.LabelField(countRect, $"+{icons.Count - maxIconsToShow}", EditorStyles.miniLabel);
        }
    }


    static bool SortObject(int id, out List<GUIContent> icons)
        {
            GameObject go = EditorUtility.InstanceIDToObject(id) as GameObject;
            icons = new List<GUIContent>();

            if (go != null)
            {
                foreach (var kvp in typeIcons)
                {
                    // Check if the GameObject has the component
                    if (go.GetComponent(kvp.Key) != null)
                    {
                        icons.Add(kvp.Value);
                    }
                }

                if (icons.Count > 0)
                {
                    labeledObjects[id] = icons;
                    return true;
                }
            }

            unlabeledObjects.Add(id);
            return false;
        }


        static void UpdateObject(int id)
        {
            unlabeledObjects.Remove(id);
            labeledObjects.Remove(id);
            SortObject(id, out _);
        }


        const int MAX_SELECTION_UPDATE_COUNT = 3; // how many objects we want to allow to get updated on select/deselect

        static void OnSelectionChanged()
        {
            TryUpdateObjects(previousSelection); // update on deselect
            TryUpdateObjects(previousSelection = Selection.gameObjects); // update on select
        }

        static void TryUpdateObjects(GameObject[] objects)
        {
            if (objects != null && objects.Length > 0 && objects.Length <= MAX_SELECTION_UPDATE_COUNT)
            { // max of three to prevent performance hitches when selecting many objects
                foreach (GameObject go in objects)
                {
                    UpdateObject(go.GetInstanceID());
                }
            }
        }

    static void InitializeTypeIcons()
    {
        typeIcons.Clear();

        string[] guids = AssetDatabase.FindAssets("t:HierarchyIconSettings");
        if (guids.Length == 0)
            return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        HierarchyIconSettings settings = AssetDatabase.LoadAssetAtPath<HierarchyIconSettings>(path);

        if (settings == null)
            return;

        foreach (var mapping in settings.iconMappings)
        {
            Type type = null;
            Texture2D icon = null;

            // Get the type - either from script or built-in component
            if (mapping.script != null)
            {
                type = mapping.script.GetClass();
            }
            else if (!string.IsNullOrEmpty(mapping.componentType))
            {
                type = Type.GetType(mapping.componentType);
            }

            if (type == null || !typeof(Component).IsAssignableFrom(type))
                continue;

            // Get the icon
            if (mapping.useBuiltInIcon)
            {
                if (mapping.script != null)
                {
                    icon = AssetPreview.GetMiniThumbnail(mapping.script);
                }
                else
                {
                    // For built-in components, use Unity's icon
                    icon = EditorGUIUtility.ObjectContent(null, type).image as Texture2D;
                }
            }
            else if (mapping.customIcon != null)
            {
                icon = mapping.customIcon;
            }

            if (icon != null)
            {
                typeIcons[type] = new GUIContent(icon);
            }
        }
    }
    
    public static void RefreshIcons()
    {
        InitializeTypeIcons();
        labeledObjects.Clear();
        unlabeledObjects.Clear();
        EditorApplication.RepaintHierarchyWindow();
    }

    static MonoScript FindMonoScriptForType(Type type)
        {
            // Search all MonoScripts for the type's source file
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return script;
                }
            }
            return null;
        }

        static string GetFullIconPath(string scriptFolder, string iconPath)
        {
            // Handle absolute paths (starting with "Assets/") directly
            if (iconPath.StartsWith("Assets/"))
                return iconPath;

            // Resolve relative paths
            return Path.Combine(scriptFolder, iconPath).Replace('\\', '/');
        }


        static void ClearCache()
        {
            typeIcons.Clear();
        }

    }

