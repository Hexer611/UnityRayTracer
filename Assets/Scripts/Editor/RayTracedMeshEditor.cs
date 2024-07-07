using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RayTracedMesh))]
public class RayTracedMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var script = target as RayTracedMesh;
        if (GUILayout.Button("Build BVH"))
        {
            script.CreateBVH();
        }
    }
}
