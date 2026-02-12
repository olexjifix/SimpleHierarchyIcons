using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "HierarchyIconSettings", menuName = "Editor/Hierarchy Icon Settings")]
public class HierarchyIconSettings : ScriptableObject
{
    [System.Serializable]
    public class IconMapping
    {
        public MonoScript script; // For custom MonoBehaviours
        public string componentType; // For built-in components (stores AssemblyQualifiedName)
        public Texture2D customIcon;
        public bool useBuiltInIcon = true;
    }

    public List<IconMapping> iconMappings = new List<IconMapping>();
}