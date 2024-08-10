using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MeshMono))]
public class MeshMonoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var script = target as MeshMono;
        if (GUILayout.Button("Build BVH"))
        {
            script.CreateBVH();
        }
    }
}
