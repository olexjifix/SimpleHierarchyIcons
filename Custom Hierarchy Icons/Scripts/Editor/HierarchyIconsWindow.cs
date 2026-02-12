using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

public class HierarchyIconsWindow : EditorWindow
{
    private HierarchyIconSettings settings;
    private Vector2 scrollPos;
    private Vector2 componentPickerScrollPos; // Add this line
    private MonoScript scriptToAdd;
    private string componentSearchFilter = "";

    private bool showComponentPicker = false;

    // Cache for built-in components
    private static List<Type> allBuiltInComponents;

    // Common Unity built-in components
    private static readonly Type[] commonBuiltInComponents = new Type[]
    {
        typeof(Transform),
        typeof(Camera),
        typeof(Light),
        typeof(MeshRenderer),
        typeof(MeshFilter),
        typeof(Rigidbody),
        typeof(BoxCollider),
        typeof(SphereCollider),
        typeof(CapsuleCollider),
        typeof(MeshCollider),
        typeof(CharacterController),
        typeof(Animator),
        typeof(Animation),
        typeof(AudioSource),
        typeof(AudioListener),
        typeof(ParticleSystem),
        typeof(Canvas),
        typeof(CanvasRenderer),
        typeof(RectTransform),
        typeof(SpriteRenderer),
        typeof(LineRenderer),
        typeof(TrailRenderer),
    };

    [MenuItem("Tools/Hierarchy Icons/Settings")]
    public static void ShowWindow()
    {
        GetWindow<HierarchyIconsWindow>("Hierarchy Icons");
    }

    [MenuItem("Tools/Hierarchy Icons/Refresh Icons")]
    public static void RefreshIconsMenu()
    {
        CustomHierarchyIcons.RefreshIcons();
        Debug.Log("Hierarchy icons refreshed!");
    }

    void OnEnable()
    {
        LoadOrCreateSettings();
        if (allBuiltInComponents == null)
        {
            allBuiltInComponents = GetAllBuiltInComponents();
        }
    }

    // Get all built-in Unity components
    private static List<Type> GetAllBuiltInComponents()
    {
        List<Type> components = new List<Type>();

        // Get all Unity engine assemblies
        Assembly[] assemblies = new Assembly[]
        {
            typeof(Transform).Assembly,        // UnityEngine.CoreModule
            typeof(Camera).Assembly,           // UnityEngine
            typeof(Canvas).Assembly,           // UnityEngine.UIModule (if exists)
        };

        HashSet<Assembly> uniqueAssemblies = new HashSet<Assembly>(assemblies);

        // Also check for additional Unity assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.FullName.StartsWith("UnityEngine"))
            {
                uniqueAssemblies.Add(assembly);
            }
        }

        foreach (var assembly in uniqueAssemblies)
        {
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    // Check if it's a Component and not abstract
                    if (typeof(Component).IsAssignableFrom(type)
                        && !type.IsAbstract
                        && type.IsPublic
                        && !typeof(MonoBehaviour).IsAssignableFrom(type)) // Exclude MonoBehaviours
                    {
                        components.Add(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some assemblies might have loading issues, skip them
                continue;
            }
        }

        // Sort alphabetically
        components.Sort((a, b) => a.Name.CompareTo(b.Name));

        return components;
    }

    void LoadOrCreateSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:HierarchyIconSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            settings = AssetDatabase.LoadAssetAtPath<HierarchyIconSettings>(path);
        }
        else
        {
            settings = CreateInstance<HierarchyIconSettings>();

            string folderPath = "Assets/Editor";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Editor");
            }

            AssetDatabase.CreateAsset(settings, "Assets/Editor/HierarchyIconSettings.asset");
            AssetDatabase.SaveAssets();
        }
    }

    void OnGUI()
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Settings asset not found. Creating new one...", MessageType.Warning);
            LoadOrCreateSettings();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Hierarchy Icon Settings", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh", GUILayout.Width(80)))
        {
            CustomHierarchyIcons.RefreshIcons();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Add new component section
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Add Component", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        scriptToAdd = (MonoScript)EditorGUILayout.ObjectField("Custom Script", scriptToAdd, typeof(MonoScript), false);

        if (GUILayout.Button("Add", GUILayout.Width(60)) && scriptToAdd != null)
        {
            AddScriptMapping(scriptToAdd);
            scriptToAdd = null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Built-in component picker
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(showComponentPicker ? "Hide Built-in Components ▼" : "Show Built-in Components ▶"))
        {
            showComponentPicker = !showComponentPicker;
        }
        EditorGUILayout.LabelField($"({allBuiltInComponents.Count} available)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (showComponentPicker)
        {
            EditorGUI.indentLevel++;
            componentSearchFilter = EditorGUILayout.TextField("Search", componentSearchFilter);

            EditorGUILayout.BeginVertical("box");

            // Filter components based on search
            var filteredComponents = allBuiltInComponents;
            if (!string.IsNullOrEmpty(componentSearchFilter))
            {
                filteredComponents = allBuiltInComponents
                    .Where(t => t.Name.ToLower().Contains(componentSearchFilter.ToLower()))
                    .ToList();
            }

            EditorGUILayout.LabelField($"Showing {filteredComponents.Count} components", EditorStyles.miniLabel);

            Vector2 scrollViewSize = new Vector2(0, Mathf.Min(300, filteredComponents.Count * 20));
            //Vector2 tempScroll = EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(scrollViewSize.y));
            componentPickerScrollPos = EditorGUILayout.BeginScrollView(componentPickerScrollPos, GUILayout.Height(300));

            foreach (var componentType in filteredComponents)
            {
                EditorGUILayout.BeginHorizontal();

                // Show icon preview
                Texture2D icon = EditorGUIUtility.ObjectContent(null, componentType).image as Texture2D;
                if (icon != null)
                {
                    GUILayout.Label(new GUIContent(icon), GUILayout.Width(20), GUILayout.Height(18));
                }
                else
                {
                    GUILayout.Space(20); // Maintain alignment even without icon
                }

                GUILayout.Label(componentType.Name, GUILayout.Width(200));

                // Show namespace for disambiguation
                GUILayout.Label(componentType.Namespace, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                bool alreadyAdded = settings.iconMappings.Any(m => m.componentType == componentType.AssemblyQualifiedName);

                EditorGUI.BeginDisabledGroup(alreadyAdded);
                if (GUILayout.Button(alreadyAdded ? "Added" : "Add", GUILayout.Width(60)))
                {
                    AddComponentMapping(componentType);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Display existing mappings
        EditorGUILayout.LabelField($"Active Icon Mappings ({settings.iconMappings.Count})", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = settings.iconMappings.Count - 1; i >= 0; i--)
        {
            var mapping = settings.iconMappings[i];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            // Determine if this is a built-in component or custom script
            Type componentType = GetComponentType(mapping);
            string componentName = componentType != null ? componentType.Name : "Unknown Component";

            // Show icon preview
            Texture2D previewIcon = GetIconForMapping(mapping);
            if (previewIcon != null)
            {
                GUILayout.Label(new GUIContent(previewIcon), GUILayout.Width(20), GUILayout.Height(18));
            }

            // Component name label
            EditorGUILayout.LabelField(componentName, GUILayout.Width(150));

            // Script field (only for custom scripts)
            if (mapping.script != null)
            {
                mapping.script = (MonoScript)EditorGUILayout.ObjectField(
                    mapping.script, typeof(MonoScript), false, GUILayout.Width(150));
            }
            else
            {
                EditorGUILayout.LabelField("(Built-in)", GUILayout.Width(150));
            }

            // Use built-in icon toggle
            bool previousUseBuiltIn = mapping.useBuiltInIcon;
            mapping.useBuiltInIcon = EditorGUILayout.Toggle("Use Built-in", mapping.useBuiltInIcon, GUILayout.Width(100));

            // Custom icon field (only if not using built-in)
            if (!mapping.useBuiltInIcon)
            {
                mapping.customIcon = (Texture2D)EditorGUILayout.ObjectField(
                    mapping.customIcon, typeof(Texture2D), false, GUILayout.Width(70));
            }

            // Remove button
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                settings.iconMappings.RemoveAt(i);
                EditorUtility.SetDirty(settings);
                CustomHierarchyIcons.RefreshIcons();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (previousUseBuiltIn != mapping.useBuiltInIcon)
            {
                EditorUtility.SetDirty(settings);
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(settings);
            CustomHierarchyIcons.RefreshIcons();
        }
    }


    private void AddScriptMapping(MonoScript script)
    {
        if (settings.iconMappings.Any(m => m.script == script))
        {
            EditorUtility.DisplayDialog("Already Added", $"{script.name} is already in the list.", "OK");
            return;
        }

        settings.iconMappings.Add(new HierarchyIconSettings.IconMapping
        {
            script = script,
            useBuiltInIcon = true
        });
        EditorUtility.SetDirty(settings);
    }

    private void AddComponentMapping(Type componentType)
    {
        if (settings.iconMappings.Any(m => m.componentType == componentType.AssemblyQualifiedName))
            return;

        settings.iconMappings.Add(new HierarchyIconSettings.IconMapping
        {
            componentType = componentType.AssemblyQualifiedName,
            useBuiltInIcon = true
        });
        EditorUtility.SetDirty(settings);
    }

    private Type GetComponentType(HierarchyIconSettings.IconMapping mapping)
    {
        if (mapping.script != null)
        {
            return mapping.script.GetClass();
        }
        else if (!string.IsNullOrEmpty(mapping.componentType))
        {
            return Type.GetType(mapping.componentType);
        }
        return null;
    }

    private Texture2D GetIconForMapping(HierarchyIconSettings.IconMapping mapping)
    {
        if (!mapping.useBuiltInIcon && mapping.customIcon != null)
        {
            return mapping.customIcon;
        }

        Type componentType = GetComponentType(mapping);
        if (componentType != null)
        {
            if (mapping.script != null)
            {
                return AssetPreview.GetMiniThumbnail(mapping.script);
            }
            else
            {
                return EditorGUIUtility.ObjectContent(null, componentType).image as Texture2D;
            }
        }

        return null;
    }
}