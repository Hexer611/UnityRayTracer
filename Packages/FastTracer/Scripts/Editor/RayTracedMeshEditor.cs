using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RayTracedMeshGO))]
public class RayTracedMeshGOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var script = target as RayTracedMeshGO;
        if (GUILayout.Button("Build BVH"))
        {
            script.CreateBVH();
        }
    }
}
