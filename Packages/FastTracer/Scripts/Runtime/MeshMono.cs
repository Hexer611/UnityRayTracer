using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshMono : MonoBehaviour
{
    public Camera targetCamera;
    [HideInInspector]
    public List<SBVHNode> nodes = new List<SBVHNode>();
    [HideInInspector]
    public List<ShaderTriangle> tris = new List<ShaderTriangle>();

    public List<SPMesh> meshes = new List<SPMesh>();
    public List<Texture2D> textures = new List<Texture2D>();
    public Texture2DArray textureArray;

    public RayTracedMesh[] meshTransforms;
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

            textureArray = new Texture2DArray(textures[0].width, textures[0].height, textures.Count, textures[0].format, false);

            for (int i = 0; i < textures.Count; i++)
            {
                Debug.Log(textures[i].width);
                Debug.Log(textures[i].height);
                Graphics.CopyTexture(textures[i], 0, 0, textureArray, i, 0);
            }
        }
        Debug.Log("end");
    }
}
