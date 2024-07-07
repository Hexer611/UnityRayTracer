using UnityEngine;

public class RayTracedMesh : MonoBehaviour
{
    public bool _initialized = false;
    public Color color;
    public Color emissionColor;
    public float emissionStrength;
    public float smoothness;
    public float specularProbability;
    public Color specularColor;

    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public Mesh mesh;
    public int[] triangles;
    public Vector3[] vertices;
    public Vector3[] normals;
    private Vector3[] worldVertices;
    private Vector3[] worldNormals;

    public int MAXDEPTH;
    public int triangleCount;
    public int vertexCount;

    public SPTriangle[] SPTriangles;
    public Bounds bounds;
    public BVH bvh;
    public BVHDisplayer bvhDisplayer;
    public int visualDepth;

    public Transform rayGo;

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer= GetComponent<MeshRenderer>();
        mesh = meshFilter.sharedMesh;
        triangles = mesh.triangles;
        vertices = mesh.vertices;
        normals = mesh.normals;

        triangleCount = triangles.Length;
        vertexCount = vertices.Length;

        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        Vector3 scale = transform.lossyScale;

        SPTriangles = new SPTriangle[triangleCount];
        Vector3 boundMin = PointLocalToWorld(mesh.vertices[mesh.triangles[0]], pos, rot, scale);
        Vector3 boundMax = boundMin;
        worldVertices= new Vector3[vertexCount];

        for (int j = 0; j < triangleCount; j += 3)
        {
            int trigIndex = j;

            SPTriangles[trigIndex] = new SPTriangle();
            SPTriangles[trigIndex].posA = mesh.vertices[mesh.triangles[j]];
            SPTriangles[trigIndex].posB = mesh.vertices[mesh.triangles[j + 1]];
            SPTriangles[trigIndex].posC = mesh.vertices[mesh.triangles[j + 2]];

            SPTriangles[trigIndex].posA = transform.TransformPoint(SPTriangles[trigIndex].posA);
            SPTriangles[trigIndex].posB = transform.TransformPoint(SPTriangles[trigIndex].posB);
            SPTriangles[trigIndex].posC = transform.TransformPoint(SPTriangles[trigIndex].posC);

            SPTriangles[trigIndex].normalA = DirectionLocalToWorld(mesh.normals[mesh.triangles[j]], rot);
            SPTriangles[trigIndex].normalB = DirectionLocalToWorld(mesh.normals[mesh.triangles[j + 1]], rot);
            SPTriangles[trigIndex].normalC = DirectionLocalToWorld(mesh.normals[mesh.triangles[j + 2]], rot);

            boundMin = Vector3.Min(boundMin, SPTriangles[trigIndex].posA);
            boundMax = Vector3.Max(boundMax, SPTriangles[trigIndex].posA);
            boundMin = Vector3.Min(boundMin, SPTriangles[trigIndex].posB);
            boundMax = Vector3.Max(boundMax, SPTriangles[trigIndex].posB);
            boundMin = Vector3.Min(boundMin, SPTriangles[trigIndex].posC);
            boundMax = Vector3.Max(boundMax, SPTriangles[trigIndex].posC);
        }

        //bounds = meshRenderer.bounds;
        bounds = new Bounds((boundMin + boundMax) / 2, boundMax - boundMin);
    }

    public void CreateBVH()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = meshFilter.sharedMesh;

        Quaternion rot = transform.rotation;

        var trigs = mesh.triangles;
        var verts = mesh.vertices;
        var normals = mesh.normals;

        triangleCount = trigs.Length;
        vertexCount = verts.Length;

        worldVertices = new Vector3[vertexCount];
        worldNormals = new Vector3[vertexCount];

        for (int j = 0; j < triangleCount; j += 3)
        {
            var i1 = trigs[j];
            var i2 = trigs[j + 1];
            var i3 = trigs[j + 2];

            worldVertices[i1] = transform.TransformPoint(verts[i1]);
            worldVertices[i2] = transform.TransformPoint(verts[i2]);
            worldVertices[i3] = transform.TransformPoint(verts[i3]);

            worldNormals[i1] = DirectionLocalToWorld(normals[i1], rot);
            worldNormals[i2] = DirectionLocalToWorld(normals[i2], rot);
            worldNormals[i3] = DirectionLocalToWorld(normals[i3], rot);
        }

        bvh = new BVH(worldVertices, trigs, worldNormals, MAXDEPTH);
        Debug.Log(bvhDisplayer);
        if (bvhDisplayer != null)
        {
            bvhDisplayer.Display(bvh);
        }
    }

    static Vector3 PointLocalToWorld(Vector3 p, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        return rot * Vector3.Scale(p, scale) + pos;
    }

    static Vector3 DirectionLocalToWorld(Vector3 dir, Quaternion rot)
    {
        return rot * dir;
    }

    public int drawTriangleCount;
    private void OnDrawGizmos()
    {
        if (rayGo == null)
            return;
        
        Gizmos.color = new Color(1, 1, 0, 1f);
        Gizmos.DrawLine(rayGo.position, rayGo.position + rayGo.forward*3);
        
        if (bvh != null)
        {
            bvh.visualDepth = visualDepth;
            var ray = new Ray(rayGo.position, rayGo.forward);
            bvh.DrawNodes(bvh.root, ray);
        }
        return;
        var _drawTriangleCount = Mathf.Min(drawTriangleCount, triangleCount);
        var bvhBounds = new BVHBoundingBox();

        for (int i = 0; i < _drawTriangleCount; i++)
        {
            Gizmos.DrawSphere((SPTriangles[i].posA + SPTriangles[i].posB + SPTriangles[i].posC) / 3, 0.01f);
            bvhBounds.GrowToInclude(SPTriangles[i]);
        }

        Gizmos.color = new Color(1, 0, 0, 1f);
        Gizmos.DrawWireCube((bvhBounds.Max + bvhBounds.Min) / 2, bvhBounds.Max - bvhBounds.Min);

        Gizmos.color = new Color(1,0,0,0.4f);
        Gizmos.DrawCube((bvhBounds.Max + bvhBounds.Min) / 2, bvhBounds.Max - bvhBounds.Min);
    }
}
