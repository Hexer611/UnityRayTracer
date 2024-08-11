using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshMono : MonoBehaviour
{
    [HideInInspector]
    public List<SBVHNode> nodes = new List<SBVHNode>();
    [HideInInspector]
    public List<ShaderTriangle> tris = new List<ShaderTriangle>();
    [HideInInspector]
    public List<SPMesh> meshes = new List<SPMesh>();

    public RayTracedMesh[] meshTransforms;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    public void CreateBVH()
    {
        Debug.Log("start");
        meshTransforms = RayTracedMeshUtils.GetRaytracedMeshesFromScene(out nodes, out tris, out meshes);
        Debug.Log("end");
    }
}
