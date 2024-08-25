using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        textureIndices = new Dictionary<Texture2D, int>();

        //return meshTransforms.ToArray();

        foreach (var item in GameObject.FindObjectsOfType<MeshRenderer>())
        {
            if (!item.gameObject.activeInHierarchy)
                continue;
            if (!item.enabled)
                continue;
            if ((cameraMask & (1 << item.gameObject.layer)) == 0)
                continue;
            var meshFilter = item.GetComponent<MeshFilter>();
            if (meshFilter == null)
                continue;
            List<RayTracedMesh> rMeshes = null;
            rMeshes = GetRTMFromMeshRenderer(item);

            //MeshPairs.TryGetValue(item, out rMesh);

            if (rMeshes == null)
            {
                //MeshPairs.Add(item, rMesh);
            }

            meshTransforms.AddRange(rMeshes);
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

            var material = meshTransform.material;
            Texture2D curDiffuse;

            if (material.HasTexture("_FabricBaseMap"))
            {
                newMesh.material.color = Color.white;
                newMesh.material.emissionColor = Color.white;
                newMesh.material.emissionStrength = 0;
                newMesh.material.smoothness = 0;
                newMesh.material.specularProbability = 0;
                newMesh.material.specularColor = Color.white;
                newMesh.material.opacity = 0;
                //newMesh.material.opacity = 1 - material.GetColor("_Color").a;
                curDiffuse = material.GetTexture("_FabricBaseMap") as Texture2D;
            }
            else
            {
                newMesh.material.color = material.GetColor("_BaseColor");
                newMesh.material.emissionColor = material.GetColor("_EmissionColor");
                if (material.HasProperty("_EmissiveIntensity"))
                    newMesh.material.emissionStrength = material.GetFloat("_EmissiveIntensity");
                else
                    newMesh.material.emissionStrength = 1;
                newMesh.material.smoothness = material.GetFloat("_Smoothness");
                newMesh.material.specularProbability = material.GetFloat("_Smoothness");
                newMesh.material.specularColor = Color.white;
                newMesh.material.opacity = 0;
                //newMesh.material.opacity = 1 - material.GetColor("_Color").a;
                curDiffuse = material.GetTexture("_BaseMap") as Texture2D;
            }

            Debug.Log(curDiffuse == null);
            if (curDiffuse == null)
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

    public static List<RayTracedMesh> GetRTMFromMeshRenderer(MeshRenderer target)
    {
        var meshFilter = target.GetComponent<MeshFilter>();
        var rMeshes = new List<RayTracedMesh>();
        BVH meshBVH = null;
        //MeshBVHPairs.TryGetValue(meshFilter.sharedMesh, out meshBVH);

        if (meshBVH == null)
        {
            if (meshFilter.sharedMesh.subMeshCount == 1 || target.sharedMaterials.Length == 1)
            {
                var rayTracedMesh = new RayTracedMesh();
                GetBVHFromMesh(meshFilter.sharedMesh, meshFilter.transform, out meshBVH);

                rayTracedMesh.bvh = meshBVH;

                rayTracedMesh.material = target.sharedMaterial;
                rMeshes.Add(rayTracedMesh);
            }
            else if (meshFilter.sharedMesh.subMeshCount == target.sharedMaterials.Length)
            {
                for (int i = 0; i < meshFilter.sharedMesh.subMeshCount; i++)
                {
                    var curSubMesh = meshFilter.sharedMesh.GetSubMesh(i);
                    var rayTracedMesh = new RayTracedMesh();
                    GetBVHFromSubMesh(meshFilter.sharedMesh, curSubMesh, meshFilter.transform, out meshBVH);

                    rayTracedMesh.bvh = meshBVH;
                    rayTracedMesh.material = target.sharedMaterials[i];

                    rMeshes.Add(rayTracedMesh);
                }
            }
            else
            {
                Debug.LogError("Mesh count and material count is a problem here");
                Debug.LogError("Mesh count : " + meshFilter.sharedMesh.subMeshCount + " Material Count : " + target.sharedMaterials.Length);
            }
            //MeshBVHPairs.Add(meshFilter.sharedMesh, meshBVH);
        }

        return rMeshes;
    }

    public static void GetBVHFromMesh(Mesh mesh, Transform meshTransform, out BVH meshBVH)
    {
        var trigs = mesh.triangles;
        var verts = mesh.vertices;
        var uvs = mesh.uv;
        var normals = mesh.normals;

        var triangleCount = trigs.Length;
        var vertexCount = verts.Length;

        var worldVertices = new Vector3[vertexCount];
        var worldNormals = new Vector3[vertexCount];

        Quaternion rot = meshTransform.rotation;

        for (int j = 0; j < triangleCount; j += 3)
        {
            var i1 = trigs[j];
            var i2 = trigs[j + 1];
            var i3 = trigs[j + 2];

            worldVertices[i1] = meshTransform.TransformPoint(verts[i1]);
            worldVertices[i2] = meshTransform.TransformPoint(verts[i2]);
            worldVertices[i3] = meshTransform.TransformPoint(verts[i3]);

            worldNormals[i1] = rot * normals[i1];
            worldNormals[i2] = rot * normals[i2];
            worldNormals[i3] = rot * normals[i3];
        }

        meshBVH = new BVH(worldVertices, trigs, worldNormals, uvs, MAXDEPTH);
    }

    public static void GetBVHFromSubMesh(Mesh mesh, SubMeshDescriptor subMesh, Transform meshTransform, out BVH meshBVH)
    {
        //var trigs = mesh.triangles;
        int[] trigs = new int[subMesh.indexCount];
        Array.Copy(mesh.triangles, subMesh.indexStart, trigs, 0, subMesh.indexCount);

        //var verts = mesh.vertices;
        Vector3[] verts = new Vector3[subMesh.vertexCount];
        Array.Copy(mesh.vertices, subMesh.firstVertex, verts, 0, subMesh.vertexCount);

        //var uvs = mesh.uv;
        Vector2[] uvs = new Vector2[subMesh.vertexCount];
        Array.Copy(mesh.uv, subMesh.firstVertex, uvs, 0, subMesh.vertexCount);

        //var normals = mesh.normals;
        Vector3[] normals = new Vector3[subMesh.vertexCount];
        Array.Copy(mesh.normals, subMesh.firstVertex, normals, 0, subMesh.vertexCount);

        var triangleCount = trigs.Length;
        var vertexCount = verts.Length;

        var worldVertices = new Vector3[vertexCount];
        var worldNormals = new Vector3[vertexCount];

        Quaternion rot = meshTransform.rotation;

        for (int j = 0; j < triangleCount; j += 3)
        {
            trigs[j] -= subMesh.firstVertex;
            trigs[j + 1] -= subMesh.firstVertex;
            trigs[j + 2] -= subMesh.firstVertex;

            var i1 = trigs[j];
            var i2 = trigs[j + 1];
            var i3 = trigs[j + 2];

            worldVertices[i1] = meshTransform.TransformPoint(verts[i1]);
            worldVertices[i2] = meshTransform.TransformPoint(verts[i2]);
            worldVertices[i3] = meshTransform.TransformPoint(verts[i3]);

            worldNormals[i1] = rot * normals[i1];
            worldNormals[i2] = rot * normals[i2];
            worldNormals[i3] = rot * normals[i3];
        }

        meshBVH = new BVH(worldVertices, trigs, worldNormals, uvs, MAXDEPTH);
    }
}
