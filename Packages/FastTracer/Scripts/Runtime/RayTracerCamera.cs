using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracerCamera : MonoBehaviour
{
    public int MaxBounceCount;
    public int NumberOfRaysPerPixel;
    public bool render;
    public bool accumulate;
    public bool composite;
    public Material rayTracingMaterial;
    public Material compositeMaterial;
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
    public RenderTexture compositeResultTexture;

    private ComputeBuffer sphereBuffer;
    private ComputeBuffer meshBuffer;
    private ComputeBuffer computeBufferNodes;
    private ComputeBuffer computeBufferTrigs;

    [SerializeField]
    private MeshMono meshMono;
    [SerializeField]
    private Camera myCamera;
    [SerializeField]
    private RenderTexture targetTexture;

    private void Start()
    {
        meshBuffer = null;
        sphereBuffer = null;
        computeBufferNodes = null;
        computeBufferTrigs = null;
    }

    private void Update()
    {
        if (myCamera == null)
            return;
        if (targetTexture == null)
            return;

        int textureWidth = targetTexture.width;
        int textureHeight = targetTexture.height;

        if (resultTexture == null || !resultTexture.IsCreated())
        {
            resultTexture = new RenderTexture(textureWidth, textureHeight, 1);
            resultTexture.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
            resultTexture.enableRandomWrite = true;
            resultTexture.autoGenerateMips = false;
            resultTexture.useMipMap = false;
            resultTexture.Create();

            resultTexture.wrapMode = TextureWrapMode.Clamp;
            resultTexture.filterMode = FilterMode.Bilinear;
        }

        if (compositeResultTexture == null || !compositeResultTexture.IsCreated())
        {
            compositeResultTexture = new RenderTexture(textureWidth, textureHeight, 1);
            compositeResultTexture.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
            compositeResultTexture.enableRandomWrite = true;
            compositeResultTexture.autoGenerateMips = false;
            compositeResultTexture.useMipMap = false;
            compositeResultTexture.Create();

            compositeResultTexture.wrapMode = TextureWrapMode.Clamp;
            compositeResultTexture.filterMode = FilterMode.Bilinear;
        }

        UpdateCameraParams(myCamera);

        if (render)
        {
            if (accumulate)
            {
                RenderTexture prevFrameCopy = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, GraphicsFormat.R32G32B32A32_SFloat);
                Graphics.Blit(resultTexture, prevFrameCopy);

                rayTracingMaterial.SetInt("Frame", frame);
                RenderTexture curFrameCopy = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, GraphicsFormat.R32G32B32A32_SFloat);
                Graphics.Blit(null, curFrameCopy, rayTracingMaterial);

                accumulateMaterial.SetInt("_Frame", frame);
                accumulateMaterial.SetTexture("_PrevFrame", prevFrameCopy);
                Graphics.Blit(curFrameCopy, resultTexture, accumulateMaterial);

                Graphics.Blit(resultTexture, targetTexture);

                RenderTexture.ReleaseTemporary(curFrameCopy);
                RenderTexture.ReleaseTemporary(prevFrameCopy);

                frame += Application.isPlaying ? 1 : 0;
            }
            else
                Graphics.Blit(null, targetTexture, rayTracingMaterial);
            if (composite)
            {
                compositeMaterial.SetTexture("ColorTexture", myCamera.targetTexture);
                compositeMaterial.SetTexture("LightTexture", targetTexture);
                Graphics.Blit(null, compositeResultTexture, compositeMaterial);
            }
        }
        else
            targetTexture.Release();
    }

    private void UpdateCameraParams(Camera camera)
    {
        if (meshMono == null)
            return;

        var cameraNear = camera.nearClipPlane;
        var cameraFOV = camera.fieldOfView;
        var cameraAspect = camera.aspect;

        float planeHeight = cameraNear * Mathf.Tan(cameraFOV * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cameraAspect;

        rayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, cameraNear));
        rayTracingMaterial.SetVector("CameraPosition", camera.transform.position);
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

        //if (meshTransforms == null || meshTransforms.Length == 0)
        //    meshTransforms = FindObjectsOfType<RayTracedMesh>();

        if (sphereTransforms.Length > 0)
        {
            if (sphereBuffer == null)
                sphereBuffer = new ComputeBuffer(sphereTransforms.Length, 20 * sizeof(float), ComputeBufferType.Default);
            sphereBuffer.SetData(spheres);
            rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
        }

        if (meshMono.meshes.Count > 0)
        {
            CreateStructuredBuffer(ref meshBuffer, meshMono.meshes);
            rayTracingMaterial.SetBuffer("Meshes", meshBuffer);

            CreateStructuredBuffer(ref computeBufferNodes, meshMono.nodes);
            rayTracingMaterial.SetBuffer("Nodes", computeBufferNodes);

            CreateStructuredBuffer(ref computeBufferTrigs, meshMono.tris);
            rayTracingMaterial.SetBuffer("Triangles", computeBufferTrigs);
        }

        rayTracingMaterial.SetInt("NumSpheres", sphereTransforms.Length);
        rayTracingMaterial.SetInt("NumMeshes", meshMono.meshes.Count);
        rayTracingMaterial.SetTexture("_textures", meshMono.textureArray);
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
            if (buffer != null)
            { 
                buffer.Release();
            }
            buffer = new ComputeBuffer(length, stride, ComputeBufferType.Structured);
        }

        buffer.SetData(data);
    }

    private void OnDestroy()
    {
        if (sphereBuffer != null)
            sphereBuffer.Release();
        if (meshBuffer != null)
            meshBuffer.Release();
        if (computeBufferNodes != null)
            computeBufferNodes.Release();
        if (computeBufferTrigs != null)
            computeBufferTrigs.Release();
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
    public float opacity;

    public float diffuseIndex;
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

    public Vector2 uvA, uvB, uvC;

    Vector3 _min;
    Vector3 _max;
    Vector3 _center;
    Vector3 _normal;

    public SPTriangle(Vector3 _posA, Vector3 _posB, Vector3 _posC)
    {
        posA = _posA;
        posB = _posB;
        posC = _posC;

        normalA = Vector3.zero;
        normalB = Vector3.zero;
        normalC = Vector3.zero;

        _min = Vector3.zero;
        _max = Vector3.zero;
        _center = Vector3.zero;
        _normal = Vector3.zero;

        uvA = Vector2.zero;
        uvB = Vector2.zero;
        uvC = Vector2.zero;

        Recalculate();
    }

    public SPTriangle(Vector3 _posA, Vector3 _posB, Vector3 _posC, Vector3 _normalA, Vector3 _normalB, Vector3 _normalC)
    {
        posA = _posA;
        posB = _posB;
        posC = _posC;

        normalA = _normalA;
        normalB = _normalB;
        normalC = _normalC;

        _min = Vector3.zero;
        _max = Vector3.zero;
        _center = Vector3.zero;
        _normal = Vector3.zero;

        uvA = Vector2.zero;
        uvB = Vector2.zero;
        uvC = Vector2.zero;

        Recalculate();
    }

    public SPTriangle(Vector3 _posA, Vector3 _posB, Vector3 _posC, 
                      Vector3 _normalA, Vector3 _normalB, Vector3 _normalC,
                      Vector2 _uvA, Vector2 _uvB, Vector2 _uvC)
    {
        posA = _posA;
        posB = _posB;
        posC = _posC;

        normalA = _normalA;
        normalB = _normalB;
        normalC = _normalC;

        _min = Vector3.zero;
        _max = Vector3.zero;
        _center = Vector3.zero;
        _normal = Vector3.zero;

        uvA = _uvA;
        uvB = _uvB;
        uvC = _uvC;

        Recalculate();
    }

    void Recalculate()
    {
        float minX = Mathf.Min(Mathf.Min(posA.x, posB.x), posC.x);
        float minY = Mathf.Min(Mathf.Min(posA.y, posB.y), posC.y);
        float minZ = Mathf.Min(Mathf.Min(posA.z, posB.z), posC.z);
        _min = new Vector3(minX, minY, minZ);

        float maxX = Mathf.Max(Mathf.Max(posA.x, posB.x), posC.x);
        float maxY = Mathf.Max(Mathf.Max(posA.y, posB.y), posC.y);
        float maxZ = Mathf.Max(Mathf.Max(posA.z, posB.z), posC.z);
        _max = new Vector3(maxX, maxY, maxZ);

        _center = (posA + posB + posC) / 3.0f;

        Vector3 A = posB - posA;
        Vector3 B = posC - posA;
        _normal = Vector3.Cross(A, B);
        _normal = _normal.normalized;
    }

    public Vector3 Min()
    {
        return _min;
    }
    public Vector3 Max()
    {
        return _max;
    }
    public Vector3 Center()
    {
        return _center;
    }
    public Vector3 Normal()
    {
        return _normal;
    }
}

[Serializable]
public struct ShaderTriangle
{
    public Vector3 posA, posB, posC;
    public Vector3 normalA, normalB, normalC;

    public Vector2 uvA, uvB, uvC;

    public ShaderTriangle(SPTriangle triangle)
    {
        posA = triangle.posA;
        posB = triangle.posB;
        posC = triangle.posC;

        normalA = triangle.normalA;
        normalB = triangle.normalB;
        normalC = triangle.normalC;

        uvA = triangle.uvA;
        uvB = triangle.uvB;
        uvC = triangle.uvC;
    }
}
