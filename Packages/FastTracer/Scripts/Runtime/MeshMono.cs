using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

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
    private List<Texture2D> createdTextures = new List<Texture2D>();
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

            textureArray = new Texture2DArray(2048, 2048, textures.Count, textures[0].format, false);

            foreach (var tex in createdTextures)
            {
                DestroyImmediate(tex);
            }
            createdTextures = new List<Texture2D>();

            for (int i = 0; i < textures.Count; i++)
            {
                Debug.Log(textures[i].width);
                Debug.Log(textures[i].height);
                var targetTexture = textures[i];
                Debug.Log(targetTexture.width);
                Debug.Log(targetTexture.height);
                if (targetTexture.width != 2048 || targetTexture.height != 2048)
                {

                    RenderTexture rt = new RenderTexture(2048, 2048, 0);
                    Graphics.Blit(targetTexture, rt);

                    RenderTexture.active = rt;
                    targetTexture = new Texture2D(2048, 2048, textures[0].format, false);
                    targetTexture.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0);
                    RenderTexture.active = null;
                    GameObject.DestroyImmediate(rt);
                    createdTextures.Add(targetTexture);
                }
                Debug.Log(targetTexture.width);
                Debug.Log(targetTexture.height);
                Graphics.CopyTexture(targetTexture, 0, 0, textureArray, i, 0);
            }
        }
        Debug.Log("end");
    }
}
