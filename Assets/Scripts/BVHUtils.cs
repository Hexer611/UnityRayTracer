using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Profiling;

public class BVHUtils : MonoBehaviour
{
}

[Serializable]
public class BVH
{
    public int MAXDEPTH = 4;
    public int visualDepth = 0;
    public int nodesStartIndex;
    public int trianglesStartIndex;
    public List<BVHNode> nodes = new();
    public List<SPTriangle> allTriangles = new();
    public BVHNode root = new BVHNode();
    public float time;

    public BVH(Vector3[] vertices, int[] tris, Vector3[] normals, int maxDepth)
    {
        var startTime = Time.realtimeSinceStartup;
        MAXDEPTH = maxDepth;
        BVHBoundingBox bounds = new BVHBoundingBox();
        bounds.Min = Vector3.one * float.MaxValue;
        bounds.Max = Vector3.one * float.MinValue;
        foreach (Vector3 v in vertices)
        {
            bounds.GrowToInclude(v);
        }

        SPTriangle[] triangles = new SPTriangle[tris.Length / 3];

        for (int i = 0; i < triangles.Length; i ++)
        {
            Vector3 a = vertices[tris[i*3 + 0]];
            Vector3 b = vertices[tris[i*3 + 1]];
            Vector3 c = vertices[tris[i*3 + 2]];

            Vector3 na = normals[tris[i * 3 + 0]];
            Vector3 nb = normals[tris[i * 3 + 1]];
            Vector3 nc = normals[tris[i * 3 + 2]];

            triangles[i] = new SPTriangle(a, b, c, na, nb, nc);
        }

        root = new BVHNode();
        root.Bounds = bounds;

        allTriangles = new List<SPTriangle>(triangles);
        root.triangleIndex = 0;
        root.triangleCount = allTriangles.Count;
        nodes.Add(root);

        Profiler.BeginSample("Split begin");
        Split(ref root);
        Profiler.EndSample();

        time = Time.realtimeSinceStartup - startTime;
    }

    BVHNode tmp_child1 = new BVHNode();
    BVHNode tmp_child2 = new BVHNode();

    public void Split(ref BVHNode parent, int depth = 0)
    {
        if (depth > MAXDEPTH) return;

        int minCostSplitAxis = 0;
        float minCostSplitPos = 0;
        float minCost = float.MaxValue;

        for (float splitNormPos = 0.1f; splitNormPos < 1; splitNormPos += 0.1f)
        {
            for (int splitAxis = 0; splitAxis < 3; splitAxis++)
            {
                float splitPos = parent.Bounds.Min[splitAxis] + (parent.Bounds.Max[splitAxis] - parent.Bounds.Min[splitAxis]) * splitNormPos;

                tmp_child1.Reset();
                tmp_child2.Reset();

                for (int i = parent.triangleIndex; i < parent.triangleIndex + parent.triangleCount; i++)
                {
                    var tri = allTriangles[i];
                    bool inA = tri.Center()[splitAxis] < splitPos;

                    BVHNode child = inA ? tmp_child1 : tmp_child2;
                    child.Bounds.GrowToInclude(tri);
                    child.triangleCount++;
                }
                if (tmp_child1.triangleCount == 0 || tmp_child2.triangleCount == 0)
                    continue;

                float curCost = tmp_child1.Bounds.Cost(tmp_child1.triangleCount) + tmp_child2.Bounds.Cost(tmp_child2.triangleCount);
                if (curCost < minCost)
                {
                    minCostSplitAxis = splitAxis;
                    minCostSplitPos = splitPos;
                    minCost = curCost;
                }
            }
        }

        //var size = parent.Bounds.Max - parent.Bounds.Min;
        //minCostSplitAxis = size.x > Mathf.Max(size.y, size.z) ? 0 : size.y > size.z ? 1 : 2;
        //minCostSplitPos = parent.Bounds.Center[minCostSplitAxis];

        if (minCost == 9999999)
            return;

        var child1 = new BVHNode();
        var child2 = new BVHNode();

        BVHBoundingBox bounds1 = new BVHBoundingBox();
        bounds1.Min = Vector3.one * float.MaxValue;
        bounds1.Max = Vector3.one * float.MinValue;
        child1.Bounds = bounds1;

        BVHBoundingBox bounds2 = new BVHBoundingBox();
        bounds2.Min = Vector3.one * float.MaxValue;
        bounds2.Max = Vector3.one * float.MinValue;
        child2.Bounds = bounds2;

        child1.triangleIndex = parent.triangleIndex;
        child2.triangleIndex = parent.triangleIndex;

        parent.childIndex = nodes.Count;
        nodes.Add(child1);
        nodes.Add(child2);

        for (int i = parent.triangleIndex; i < (parent.triangleIndex + parent.triangleCount); i++)
        {
            var tri = allTriangles[i];
            bool inA = tri.Center()[minCostSplitAxis] < minCostSplitPos;
            BVHNode child = inA ? child1 : child2;
            child.Bounds.GrowToInclude(tri);
            child.triangleCount++;

            if (inA)
            {
                int swapIndex = child.triangleIndex + child.triangleCount - 1;
                (allTriangles[i], allTriangles[swapIndex]) = (allTriangles[swapIndex], allTriangles[i]);
                child2.triangleIndex++;
            }
        }

        if (child1.triangleCount > 0 && child2.triangleCount > 0)
        {
            Split(ref child1, depth + 1);
            Split(ref child2, depth + 1);
        }
    }

    public void DrawNodes(BVHNode node, Ray ray, int depth = 0)
    {
        if (depth > visualDepth)
            return;
        //if (NoBoundsHit(ray, node.Bounds.Min, node.Bounds.Max))
        //{
        //    return;
        //}

        //if (node.childIndex == 0)
        //{
        //    Gizmos.color = new Color(1, 0, 0, 0.3f);
        //    Gizmos.DrawCube((node.Bounds.Max + node.Bounds.Min) / 2, node.Bounds.Max - node.Bounds.Min);

        //    //for (int i = node.triangleIndex; i < node.triangleIndex + node.triangleCount; i++)
        //    //{
        //    //    var tri = allTriangles[i];
        //    //    if (RayTriangle(ray, tri))
        //    //    {
        //    //        var ms = new Mesh();
        //    //        ms.vertices = new Vector3[] { tri.posA, tri.posB, tri.posC };
        //    //        ms.normals = new Vector3[] { tri.normalA, tri.normalB, tri.normalC };
        //    //        ms.triangles = new int[] { 0, 1, 2 };
        //    //        Gizmos.color = new Color(0, 1, 0, 1f);
        //    //        Gizmos.DrawMesh(ms);
        //    //        return;
        //    //    }
        //    //}
        //    return;
        //}

        Gizmos.color = new Color(1, 0, 0, 0.5f);
        if (visualDepth == depth)
        {
            Gizmos.DrawCube((node.Bounds.Max + node.Bounds.Min) / 2, node.Bounds.Max - node.Bounds.Min);
        }
        else
            Gizmos.DrawWireCube((node.Bounds.Max + node.Bounds.Min) / 2, node.Bounds.Max - node.Bounds.Min);

        DrawNodes(nodes[node.childIndex], ray, depth + 1);
        DrawNodes(nodes[node.childIndex+1], ray, depth + 1);
    }

    bool NoBoundsHit(Ray ray, Vector3 boxMin, Vector3 boxMax)
    {
        Vector3 invDir = new Vector3(1f/ ray.direction.x, 1f/ ray.direction.y, 1f/ ray.direction.z);
        Vector3 tMin = Vector3.Scale(boxMin - ray.origin, invDir);
        Vector3 tMax = Vector3.Scale(boxMax - ray.origin, invDir);
        Vector3 t1 = Vector3.Min(tMin, tMax);
        Vector3 t2 = Vector3.Max(tMin, tMax);
        float tNear = Mathf.Max(Mathf.Max(t1.x, t1.y), t1.z);
        float tFar = Mathf.Min(Mathf.Min(t2.x, t2.y), t2.z);
        return tNear > tFar;
    }

    bool RayTriangle(Ray ray, SPTriangle tri)
    {
        Vector3 edgeAB = tri.posB - tri.posA;
        Vector3 edgeAC = tri.posC - tri.posA;
        Vector3 normalVector = Vector3.Cross(edgeAB, edgeAC);
        Vector3 ao = ray.origin - tri.posA;
        Vector3 dao = Vector3.Cross(ao, ray.direction);

        float determinant = -Vector3.Dot(ray.direction, normalVector);
        float invDet = 1 / determinant;

        // Calculate dst to triangle & barycentric coordinates of intersection point
        float dst = Vector3.Dot(ao, normalVector) * invDet;
        float u = Vector3.Dot(edgeAC, dao) * invDet;
        float v = -Vector3.Dot(edgeAB, dao) * invDet;
        float w = 1 - u - v;

        return determinant >= 1E-6 && dst >= 0 && u >= 0 && v >= 0 && w >= 0;
        // Initialize hit info
        //HitInfo hitInfo;
        //hitInfo.didHit = determinant >= 1E-6 && dst >= 0 && u >= 0 && v >= 0 && w >= 0;
        //hitInfo.hitPoint = ray.origin + ray.direction * dst;
        //hitInfo.normal = (tri.normalA * w + tri.normalB * u + tri.normalC * v).normalized;
        //hitInfo.dst = dst;
        //return hitInfo;
    }
}

[Serializable]
public class BVHNode
{
    public BVHBoundingBox Bounds = new BVHBoundingBox();
    public int childIndex;
    public int triangleIndex;
    public int triangleCount;

    public void Reset()
    {
        childIndex = 0;
        triangleIndex = 0;
        triangleCount = 0;

        Bounds.Min = Vector3.positiveInfinity;
        Bounds.Max = Vector3.negativeInfinity;
    }
}

[Serializable]
public class BVHBoundingBox
{
    public Vector3 Min = Vector3.positiveInfinity;
    public Vector3 Max = Vector3.negativeInfinity;

    public float Cost(int numTriangles)
    {
        Vector3 Size = Max - Min;
        return (Size.x * Size.y + Size.x * Size.z + Size.y * Size.z) * numTriangles;
    }

    public void GrowToInclude(Vector3 point)
    {
        //Profiler.BeginSample("Grow to include point");
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
        //Profiler.EndSample();
    }

    public void GrowToInclude(SPTriangle triangle)
    {
        //Profiler.BeginSample("Grow to include triangle");
        Vector3 triMin = triangle.Min();
        Vector3 triMax = triangle.Max();

        //Min = Vector3.Min(Min, triMin);
        //Max = Vector3.Max(Max, triMax);

        Min.x = triMin.x < Min.x ? triMin.x : Min.x;
        Min.y = triMin.y < Min.y ? triMin.y : Min.y;
        Min.z = triMin.z < Min.z ? triMin.z : Min.z;

        Max.x = triMax.x > Max.x ? triMax.x : Max.x;
        Max.y = triMax.y > Max.y ? triMax.y : Max.y;
        Max.z = triMax.z > Max.z ? triMax.z : Max.z;
        //Profiler.EndSample();
    }
}

public struct SBVHNode
{
    public SBVHBoundingBox Bounds;
    public int childIndex;
    public int triangleIndex;
    public int triangleCount;
}

public struct SBVHBoundingBox
{
    public Vector3 Min;
    public Vector3 Max;

    public SBVHBoundingBox (Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }
}
