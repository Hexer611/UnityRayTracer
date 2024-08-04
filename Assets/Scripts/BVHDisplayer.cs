using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BVHDisplayer : MonoBehaviour
{
    public Text textTime;
    public Text textTriangles;
    public Text textNodes;
    public Text textLeaves;
    public Text textLeafMinDepth;
    public Text textLeafMaxDepth;
    public Text textLeafAVGDepth;
    public Text textLeafMinTris;
    public Text textLeafMaxTris;
    public Text textLeafAVGTris;

    float leaves = 0;
    float minLeafDepth = 0;
    float maxLeafDepth = 0;
    float avgLeafDepth = 0;
    float minTris = 0;
    float maxTris = 0;
    float avgTris = 0;

    public void Display(BVH bvh)
    {
        leaves = 0;
        minLeafDepth = int.MaxValue;
        maxLeafDepth = 0;
        avgLeafDepth = 0;
        minTris = int.MaxValue;
        maxTris = 0;
        avgTris = 0;

        foreach(var item in bvh.nodes)
        {
            if (item.childIndex == 0)
            {
                leaves ++;
                minTris = Mathf.Min(item.triangleCount, minTris);
                maxTris = Mathf.Max(item.triangleCount, maxTris);
                avgTris += item.triangleCount;
            }
        }

        avgTris = avgTris / bvh.nodes.Count;

        GetLeaves(bvh, bvh.root);

        avgLeafDepth = avgLeafDepth / leaves;

        textTime.text = (bvh.time * 1000).ToString() + " ms";
        textTriangles.text = bvh.allTriangles.Count.ToString();
        textNodes.text = bvh.nodes.Count.ToString();
        textLeaves.text = leaves.ToString();
        textLeafMinDepth.text = minLeafDepth.ToString();
        textLeafMaxDepth.text = maxLeafDepth.ToString();
        textLeafAVGDepth.text = avgLeafDepth.ToString();
        textLeafMinTris.text = minTris.ToString();
        textLeafMaxTris.text = maxTris.ToString();
        textLeafAVGTris.text = avgTris.ToString();
    }

    public void GetLeaves(BVH bvh, BVHNode node, int depth = 0)
    {
        if (node.childIndex == 0)
        {
            minLeafDepth = Mathf.Min(depth, minLeafDepth);
            maxLeafDepth = Mathf.Max(depth, maxLeafDepth);
            avgLeafDepth += depth;
            return;
        }

        GetLeaves(bvh, bvh.nodes[node.childIndex], depth + 1);
        GetLeaves(bvh, bvh.nodes[node.childIndex + 1], depth + 1);
    }
}
