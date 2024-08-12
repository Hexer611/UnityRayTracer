using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RayTracedMeshUtils
{
    public static int MAXDEPTH = 32;
    public static Dictionary<Mesh, BVH> MeshBVHPairs = new Dictionary<Mesh, BVH>();
    public static Dictionary<MeshRenderer, RayTracedMesh> MeshPairs = new Dictionary<MeshRenderer, RayTracedMesh>();

    public static RayTracedMesh[] GetRaytracedMeshesFromScene(LayerMask cameraMask, out List<SBVHNode> nodes, out List<ShaderTriangle> tris, out List<SPMesh> meshes)
    {
        var meshTransforms = new List<RayTracedMesh>();
        nodes = new List<SBVHNode>();
        tris = new List<ShaderTriangle>();
        meshes = new List<SPMesh>();

        //return meshTransforms.ToArray();

        foreach (var item in GameObject.FindObjectsOfType<MeshRenderer>())
        {
            if (!item.gameObject.activeInHierarchy)
                continue;
            if (!item.enabled)
                continue;
            if ((cameraMask & (1 << item.gameObject.layer)) == 0)
                continue;
            RayTracedMesh rMesh = null;
            rMesh = GetRTMFromMeshRenderer(item);

            //MeshPairs.TryGetValue(item, out rMesh);

            if (rMesh == null)
            {
                //MeshPairs.Add(item, rMesh);
            }

            meshTransforms.Add(rMesh);
        }

        int lastTrigIndex = 0;
        int curTriangleIndex = 0;
        int curNodeIndex = 0;

        foreach (var meshTransform in meshTransforms)
        {
            var newMesh = new SPMesh();
            newMesh.trigCount = meshTransform.bvh.allTriangles.Count;
            newMesh.firstTrig = lastTrigIndex;
            newMesh.boundsMin = meshTransform.bvh.nodes[0].Bounds.Min;
            newMesh.boundsMax = meshTransform.bvh.nodes[0].Bounds.Max;

            newMesh.nodesStartIndex = curNodeIndex;

            newMesh.material.color = meshTransform.color;
            newMesh.material.emissionColor = meshTransform.emissionColor;
            newMesh.material.emissionStrength = meshTransform.emissionStrength;
            newMesh.material.smoothness = meshTransform.smoothness;
            newMesh.material.specularProbability = meshTransform.specularProbability;
            newMesh.material.specularColor = meshTransform.specularColor;
            newMesh.material.opacity = meshTransform.opacity;

            lastTrigIndex += newMesh.trigCount;

            foreach (var item in meshTransform.bvh.nodes)
            {
                SBVHNode sNode = new SBVHNode();
                sNode.triangleIndex = item.triangleIndex + curTriangleIndex;
                sNode.triangleCount = item.triangleCount;
                sNode.childIndex = item.childIndex + curNodeIndex;
                sNode.Bounds = new SBVHBoundingBox(item.Bounds.Min, item.Bounds.Max);
                nodes.Add(sNode);
            }
            curNodeIndex = nodes.Count;

            foreach (var item in meshTransform.bvh.allTriangles)
            {
                tris.Add(new ShaderTriangle(item));
            }
            curTriangleIndex = tris.Count;
            meshes.Add(newMesh);
        }

        return meshTransforms.ToArray();
    }

    public static RayTracedMesh GetRTMFromMeshRenderer(MeshRenderer target)
    {
        var meshFilter = target.GetComponent<MeshFilter>();
        var rayTracedMesh = new RayTracedMesh();
        BVH meshBVH = null;
        //MeshBVHPairs.TryGetValue(meshFilter.sharedMesh, out meshBVH);
        if (meshBVH == null)
        {
            GetBVHFromMesh(meshFilter, out meshBVH);
            //MeshBVHPairs.Add(meshFilter.sharedMesh, meshBVH);
        }

        rayTracedMesh.bvh = meshBVH;
        var material = target.sharedMaterial;
        rayTracedMesh.color = material.GetColor("_Color");
        rayTracedMesh.smoothness = 1;
        rayTracedMesh.specularProbability = material.GetFloat("_Glossiness");
        rayTracedMesh.specularColor = Color.white;
        rayTracedMesh.opacity = 1 - material.GetColor("_Color").a;
        rayTracedMesh.emissionColor = material.GetColor("_EmissionColor");
        rayTracedMesh.emissionStrength = 1;

        rayTracedMesh.meshFilter = meshFilter;
        rayTracedMesh.meshRenderer = target;
        rayTracedMesh.transform = target.transform;

        rayTracedMesh.mesh = meshFilter.sharedMesh;
        rayTracedMesh.triangles = meshFilter.sharedMesh.triangles;
        rayTracedMesh.vertices = meshFilter.sharedMesh.vertices;
        rayTracedMesh.normals = meshFilter.sharedMesh.normals;

        rayTracedMesh.triangleCount = meshFilter.sharedMesh.triangles.Length;
        rayTracedMesh.vertexCount = meshFilter.sharedMesh.vertices.Length;

        return rayTracedMesh;
    }

    public static void GetBVHFromMesh(MeshFilter meshFilter, out BVH meshBVH)
    {
        var mesh = meshFilter.sharedMesh;
        var trigs = mesh.triangles;
        var verts = mesh.vertices;
        var normals = mesh.normals;

        var triangleCount = trigs.Length;
        var vertexCount = verts.Length;

        var worldVertices = new Vector3[vertexCount];
        var worldNormals = new Vector3[vertexCount];

        Quaternion rot = meshFilter.transform.rotation;

        for (int j = 0; j < triangleCount; j += 3)
        {
            var i1 = trigs[j];
            var i2 = trigs[j + 1];
            var i3 = trigs[j + 2];

            worldVertices[i1] = meshFilter.transform.TransformPoint(verts[i1]);
            worldVertices[i2] = meshFilter.transform.TransformPoint(verts[i2]);
            worldVertices[i3] = meshFilter.transform.TransformPoint(verts[i3]);

            worldNormals[i1] = rot * normals[i1];
            worldNormals[i2] = rot * normals[i2];
            worldNormals[i3] = rot * normals[i3];
        }

        meshBVH = new BVH(worldVertices, trigs, worldNormals, MAXDEPTH);
    }
}
