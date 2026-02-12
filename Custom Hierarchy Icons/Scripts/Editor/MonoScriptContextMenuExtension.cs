using UnityEditor;
using UnityEngine;
using System.Linq;

public static class MonoScriptContextMenuExtension
{
    [MenuItem("CONTEXT/MonoScript/Add to Hierarchy Icons")]
    private static void AddToHierarchyIcons(MenuCommand command)
    {
        MonoScript script = command.context as MonoScript;

        if (script == null)
            return;

        // Check if it's a MonoBehaviour
        var scriptClass = script.GetClass();
        if (scriptClass == null || !typeof(MonoBehaviour).IsAssignableFrom(scriptClass))
        {
            EditorUtility.DisplayDialog("Invalid Script",
                "Only MonoBehaviour scripts can be added to Hierarchy Icons.", "OK");
            return;
        }

        // Load settings
        string[] guids = AssetDatabase.FindAssets("t:HierarchyIconSettings");
        HierarchyIconSettings settings = null;

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            settings = AssetDatabase.LoadAssetAtPath<HierarchyIconSettings>(path);
        }
        else
        {
            // Create new settings if none exist
            settings = ScriptableObject.CreateInstance<HierarchyIconSettings>();

            string folderPath = "Assets/Editor";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Editor");
            }

            AssetDatabase.CreateAsset(settings, "Assets/Editor/HierarchyIconSettings.asset");
            AssetDatabase.SaveAssets();
        }

        // Check if already added
        if (settings.iconMappings.Any(m => m.script == script))
        {
            EditorUtility.DisplayDialog("Already Added",
                $"{script.name} is already in the Hierarchy Icons list.", "OK");
            return;
        }

        // Add the mapping
        settings.iconMappings.Add(new HierarchyIconSettings.IconMapping
        {
            script = script,
            useBuiltInIcon = true
        });

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        // Refresh icons
        CustomHierarchyIcons.RefreshIcons();

        EditorUtility.DisplayDialog("Success",
            $"{script.name} has been added to Hierarchy Icons!", "OK");
    }

    [MenuItem("CONTEXT/MonoScript/Add to Hierarchy Icons", true)]
    private static bool ValidateAddToHierarchyIcons(MenuCommand command)
    {
        MonoScript script = command.context as MonoScript;
        if (script == null)
            return false;

        var scriptClass = script.GetClass();
        return scriptClass != null && typeof(MonoBehaviour).IsAssignableFrom(scriptClass);
    }
}