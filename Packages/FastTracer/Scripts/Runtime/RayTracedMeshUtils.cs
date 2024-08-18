using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RayTracedMeshUtils
{
    public static int MAXDEPTH = 32;
    public static Dictionary<Mesh, BVH> MeshBVHPairs = new Dictionary<Mesh, BVH>();
    public static Dictionary<MeshRenderer, RayTracedMesh> MeshPairs = new Dictionary<MeshRenderer, RayTracedMesh>();
    public static Dictionary<Texture2D, int> textureIndices = new Dictionary<Texture2D, int>();

    public static RayTracedMesh[] GetRaytracedMeshesFromScene(LayerMask cameraMask, out List<SBVHNode> nodes, out List<ShaderTriangle> tris, out List<SPMesh> meshes,
        out List<Texture2D> textures)
    {
        var meshTransforms = new List<RayTracedMesh>();
        nodes = new List<SBVHNode>();
        tris = new List<ShaderTriangle>();
        meshes = new List<SPMesh>();
        textures = new List<Texture2D>();

        //return meshTransforms.ToArray();

        foreach (var item in GameObject.FindObjectsOfType<MeshRenderer>())
        {
            if (item.GetComponent<MeshFilter>() == null)
                continue;
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
        int currentTextureIndex = 0;

        foreach (var meshTransform in meshTransforms)
        {
            var newMesh = new SPMesh();
            newMesh.trigCount = meshTransform.bvh.allTriangles.Count;
            newMesh.firstTrig = lastTrigIndex;
            newMesh.boundsMin = meshTransform.bvh.nodes[0].Bounds.Min;
            newMesh.boundsMax = meshTransform.bvh.nodes[0].Bounds.Max;

            newMesh.nodesStartIndex = curNodeIndex;

            var material = meshTransform.meshRenderer.sharedMaterial;

            newMesh.material.color = material.GetColor("_BaseColor");
            newMesh.material.emissionColor = material.GetColor("_EmissionColor");
            newMesh.material.emissionStrength = material.GetFloat("_EmissiveIntensity");
            newMesh.material.smoothness = material.GetFloat("_Smoothness");
            newMesh.material.specularProbability = material.GetFloat("_Smoothness");
            newMesh.material.specularColor = Color.white;
            newMesh.material.opacity = 0;
            //newMesh.material.opacity = 1 - material.GetColor("_Color").a;

            var curDiffuse = material.GetTexture("_BaseMap") as Texture2D;
            Debug.Log(curDiffuse == null);
            if (curDiffuse == null || curDiffuse.width != 2048 || curDiffuse.height != 2048)
                newMesh.material.diffuseIndex = -1;
            else
            {
                if (textureIndices.ContainsKey(curDiffuse))
                {
                    newMesh.material.diffuseIndex = textureIndices[curDiffuse];
                }
                else
                {
                    newMesh.material.diffuseIndex = currentTextureIndex;
                    textures.Add(curDiffuse);
                    textureIndices.Add(curDiffuse, currentTextureIndex);
                    currentTextureIndex++;
                }
            }

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

        rayTracedMesh.meshFilter = meshFilter;
        rayTracedMesh.meshRenderer = target;
        rayTracedMesh.transform = target.transform;

        rayTracedMesh.mesh = meshFilter.sharedMesh;
        rayTracedMesh.triangles = meshFilter.sharedMesh.triangles;
        rayTracedMesh.vertices = meshFilter.sharedMesh.vertices;
        rayTracedMesh.normals = meshFilter.sharedMesh.normals;
        rayTracedMesh.uvs = meshFilter.sharedMesh.uv;

        rayTracedMesh.triangleCount = meshFilter.sharedMesh.triangles.Length;
        rayTracedMesh.vertexCount = meshFilter.sharedMesh.vertices.Length;

        return rayTracedMesh;
    }

    public static void GetBVHFromMesh(MeshFilter meshFilter, out BVH meshBVH)
    {
        var mesh = meshFilter.sharedMesh;
        var trigs = mesh.triangles;
        var verts = mesh.vertices;
        var uvs = mesh.uv;
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

        meshBVH = new BVH(worldVertices, trigs, worldNormals, uvs, MAXDEPTH);
    }
}
