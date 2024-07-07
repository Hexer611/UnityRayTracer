using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static UnityEditor.Progress;
using static UnityEngine.GraphicsBuffer;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracerCamera : MonoBehaviour
{
    public int MaxBounceCount;
    public int NumberOfRaysPerPixel;
    public bool render;
    public bool accumulate;
    public Material rayTracingMaterial;
    public Material accumulateMaterial;
    public RayTracedObject[] sphereTransforms;
    public RayTracedMesh[] meshTransforms;
    public Light directionalLight;
    public float sunFocus;
    public Color skyColorHorizon;
    public Color skyColorZenith;
    public Color groundColor;

    public float EnvironmentIntensity;

    public int frame = 0;
    public RenderTexture resultTexture;

    private ComputeBuffer sphereBuffer;
    private ComputeBuffer meshBuffer;
    private ComputeBuffer triangleBuffer;

    public SPTriangle[] trigs;
    public List<SPMesh> meshes;

    private void Start()
    {
        triangleBuffer = null;
        meshBuffer = null;
        sphereBuffer = null;
    }
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        bool isSceneCam = Camera.current.name == "SceneCamera";
        UpdateCameraParams(Camera.current);

        if (isSceneCam)
        {
            Graphics.Blit(source, destination);return;
            if (render)
            {
                Graphics.Blit(null, destination, rayTracingMaterial);
            }
            else
            {
                Graphics.Blit(source, destination); // Draw the unaltered camera render to the screen
            }
            return;
        }

        if (render)
        {
            if (resultTexture == null || !resultTexture.IsCreated())
            {
                resultTexture = new RenderTexture(Screen.width, Screen.height, 1);
                resultTexture.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
                resultTexture.enableRandomWrite = true;
                resultTexture.autoGenerateMips = false;
                resultTexture.useMipMap = false;
                resultTexture.Create();

                resultTexture.wrapMode = TextureWrapMode.Clamp;
                resultTexture.filterMode = FilterMode.Bilinear;
            }


            if (accumulate)
            {
                RenderTexture prevFrameCopy = RenderTexture.GetTemporary(source.width, source.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
                Graphics.Blit(resultTexture, prevFrameCopy);

                rayTracingMaterial.SetInt("Frame", frame);
                RenderTexture curFrameCopy = RenderTexture.GetTemporary(source.width, source.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
                Graphics.Blit(null, curFrameCopy, rayTracingMaterial);

                accumulateMaterial.SetInt("_Frame", frame);
                accumulateMaterial.SetTexture("_PrevFrame", prevFrameCopy);
                Graphics.Blit(curFrameCopy, resultTexture, accumulateMaterial);

                Graphics.Blit(resultTexture, destination);

                RenderTexture.ReleaseTemporary(curFrameCopy);
                RenderTexture.ReleaseTemporary(prevFrameCopy);
                RenderTexture.ReleaseTemporary(curFrameCopy);

                frame += Application.isPlaying ? 1 : 0;
            }
            else
                Graphics.Blit(null, destination, rayTracingMaterial);
        }
        else
            Graphics.Blit(source, destination); ;
    }

    public bool update = false;
    private void UpdateCameraParams(Camera camera)
    {
        if (!update)
            return;
        var cameraNear = camera.nearClipPlane;
        var cameraFOV = camera.fieldOfView;
        var cameraAspect = camera.aspect;

        float planeHeight = cameraNear * Mathf.Tan(cameraFOV * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cameraAspect;

        rayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, cameraNear));
        rayTracingMaterial.SetMatrix("CamLocalToWorldMatrix", camera.transform.localToWorldMatrix);
        rayTracingMaterial.SetInt("MaxBounceCount", MaxBounceCount);
        rayTracingMaterial.SetInt("NumberOfRaysPerPixel", NumberOfRaysPerPixel);

        // Sun
        if (directionalLight != null)
        {
            rayTracingMaterial.SetVector("SunLightDirection", directionalLight.transform.forward);
            rayTracingMaterial.SetFloat("SunIntensity", directionalLight.intensity);
            rayTracingMaterial.SetFloat("SunFocus", sunFocus);
            rayTracingMaterial.SetColor("SunColor", directionalLight.color);
        }

        rayTracingMaterial.SetFloat("EnvironmentIntensity", EnvironmentIntensity);
        rayTracingMaterial.SetColor("SkyColorHorizon", skyColorHorizon);
        rayTracingMaterial.SetColor("SkyColorZenith", skyColorZenith);
        rayTracingMaterial.SetColor("GroundColor", groundColor);

        var spheres = new SPSphere[sphereTransforms.Length];
        for (int i = 0; i < spheres.Length; i++)
        {
            spheres[i] = new SPSphere();
            spheres[i].position = sphereTransforms[i].transform.position;
            spheres[i].radius = sphereTransforms[i].transform.localScale.x/2;
            spheres[i].material.color = sphereTransforms[i].color;
            spheres[i].material.emissionColor = sphereTransforms[i].emissionColor;
            spheres[i].material.emissionStrength = sphereTransforms[i].emissionStrength;
            spheres[i].material.smoothness = sphereTransforms[i].smoothness;
        }

        if (meshTransforms == null || meshTransforms.Length == 0)
            meshTransforms = FindObjectsOfType<RayTracedMesh>();

        meshes = new List<SPMesh>();
        int lastTrigIndex = 0;

        List<SBVHNode> nodes = new List<SBVHNode>();
        List<SPTriangle> tris = new List<SPTriangle>();

        int curTriangleIndex = 0;
        int curNodeIndex = 0;

        foreach (var meshTransform in meshTransforms)
        {
            meshTransform.Initialize();
            var newMesh = new SPMesh();
            newMesh.trigCount = meshTransform.triangleCount;
            newMesh.firstTrig = lastTrigIndex;
            newMesh.boundsMin = meshTransform.bounds.min;
            newMesh.boundsMax = meshTransform.bounds.max;

            newMesh.nodesStartIndex = curNodeIndex;

            newMesh.material.color = meshTransform.color;
            newMesh.material.emissionColor = meshTransform.emissionColor;
            newMesh.material.emissionStrength = meshTransform.emissionStrength;
            newMesh.material.smoothness = meshTransform.smoothness;
            newMesh.material.specularProbability = meshTransform.specularProbability;
            newMesh.material.specularColor = meshTransform.specularColor;

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

            tris.AddRange(meshTransform.bvh.allTriangles);
            curTriangleIndex = tris.Count;
            meshes.Add(newMesh);
        }

        /*
        trigs = new SPTriangle[lastTrigIndex];
        lastTrigIndex = 0;
        for (int i = 0;i < meshTransforms.Length;i++)
        {
            Array.Copy(meshTransforms[i].SPTriangles, 0, trigs, lastTrigIndex, meshTransforms[i].triangleCount);
            lastTrigIndex += meshTransforms[i].triangleCount;
        }
        */

        if (sphereTransforms.Length > 0)
        {
            if (sphereBuffer == null)
                sphereBuffer = new ComputeBuffer(sphereTransforms.Length, 13 * sizeof(float), ComputeBufferType.Default);
            sphereBuffer.SetData(spheres);
            rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
        }
        
        if (meshes.Count > 0)
        {
            /*
            if (meshBuffer == null)
                meshBuffer = new ComputeBuffer(meshes.Length, 15 * sizeof(float) + 2 * sizeof(int), ComputeBufferType.Default);
            meshBuffer.SetData(meshes);*/
            CreateStructuredBuffer(ref meshBuffer, meshes);
            rayTracingMaterial.SetBuffer("Meshes", meshBuffer);


            ComputeBuffer computeBufferNodes = null;
            CreateStructuredBuffer(ref computeBufferNodes, nodes);
            rayTracingMaterial.SetBuffer("Nodes", computeBufferNodes);

            int fef = 0;
            foreach (var item in tris)
            {
                if (fef++ > -1)
                    break;
                Debug.Log(item.normalA);
                Debug.Log(item.normalB);
                Debug.Log(item.normalC);
            }
            ComputeBuffer computeBufferTrigs = null;
            CreateStructuredBuffer(ref computeBufferTrigs, tris);
            rayTracingMaterial.SetBuffer("Triangles", computeBufferTrigs);
        }

        if (trigs.Length > 0)
        {
            /*
            if (triangleBuffer == null)
                triangleBuffer = new ComputeBuffer(trigs.Length, 18 * sizeof(float), ComputeBufferType.Default);
            triangleBuffer.SetData(new List<SPTriangle>(trigs));
            */

            //CreateStructuredBuffer(ref triangleBuffer, new List<SPTriangle>(trigs));
            //rayTracingMaterial.SetBuffer("Triangles", triangleBuffer);
        }

        rayTracingMaterial.SetInt("NumSpheres", sphereTransforms.Length);
        rayTracingMaterial.SetInt("NumMeshes", meshes.Count);
        rayTracingMaterial.SetInt("NumTriangles", trigs.Length);
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, List<T> data) where T : struct
    {
        // Cannot create 0 length buffer (not sure why?)
        int length = Mathf.Max(1, data.Count);
        // The size (in bytes) of the given data type
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

        // If buffer is null, wrong size, etc., then we'll need to create a new one
        if (buffer == null || !buffer.IsValid() || buffer.count != length || buffer.stride != stride)
        {
            if (buffer != null) { buffer.Release(); }
            buffer = new ComputeBuffer(length, stride, ComputeBufferType.Structured);
        }

        buffer.SetData(data);
    }

    private void OnDisable()
    {
        if (sphereBuffer != null)
            sphereBuffer.Release();
        if (meshBuffer != null)
            meshBuffer.Release();
        if (triangleBuffer != null)
            triangleBuffer.Release();
    }
}

[Serializable]
public struct SPMaterial
{
    public Color color;
    public Color emissionColor;
    public float emissionStrength;
    public float smoothness;
    public float specularProbability;
    public Color specularColor;
}

[Serializable]
public struct SPSphere
{
    public Vector3 position;
    public float radius;
    public SPMaterial material;
}

[Serializable]
public struct SPMesh
{
    public int trigCount;
    public int firstTrig;
    public int nodesStartIndex;
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public SPMaterial material;
}

[Serializable]
public struct SPTriangle
{
    public Vector3 posA, posB, posC;
    public Vector3 normalA, normalB, normalC;
    public Vector3 Center => (posA + posB + posC) / 3;
}