using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class MeshMono : MonoBehaviour
{
    [SerializeField] int textureResolution = 512;
    public Camera targetCamera;
    [NonSerialized]
    public List<SBVHNode> nodes = new List<SBVHNode>();
    [NonSerialized]
    public List<ShaderTriangle> tris = new List<ShaderTriangle>();

    public List<SPMesh> meshes = new List<SPMesh>();
    public List<Texture2D> textures = new List<Texture2D>();
    public Texture2DArray textureArray;
    public Texture2D lastCopyTex;
    public RenderTexture rt;

    [NonSerialized]
    public RayTracedMesh[] meshTransforms;
    public List<Texture> createdTextures = new List<Texture>();
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    public void CreateBVH()
    {
        Debug.Log("start");
        meshTransforms = RayTracedMeshUtils.GetRaytracedMeshesFromScene(targetCamera.cullingMask, out nodes, out tris, out meshes, out textures);

        if (textures.Count > 0)
        {
            if (textureArray != null)
                DestroyImmediate(textureArray);

            textureArray = new Texture2DArray(textureResolution, textureResolution, textures.Count, TextureFormat.RGBA32, false);

            foreach (var tex in createdTextures)
            {
                DestroyImmediate(tex);
            }
            createdTextures = new List<Texture>();

            for (int i = 0; i < textures.Count; i++)
            {
                Texture targetTexture = textures[i];
                //if (targetTexture.width != textureResolution || targetTexture.height != textureResolution)
                {
                    rt = new RenderTexture(textureResolution, textureResolution, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
                    Graphics.Blit(targetTexture, rt);
                    createdTextures.Add(rt);
                    targetTexture = rt;
                }
                Graphics.CopyTexture(targetTexture, 0, 0, textureArray, i, 0);
            }
        }
        Debug.Log("end");
    }
}
