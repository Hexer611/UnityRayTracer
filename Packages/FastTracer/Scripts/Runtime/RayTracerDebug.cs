using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracerDebug : MonoBehaviour
{
    public Camera camera;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        var cameraPosition = camera.transform.position;
        var cameraNear = camera.nearClipPlane;
        var cameraFOV = camera.fieldOfView;
        var cameraAspect = camera.aspect;

        float planeHeight = cameraNear * Mathf.Tan(cameraFOV * Mathf.Deg2Rad * 0.5f);
        float planeWidth = planeHeight * cameraAspect;

        Gizmos.color = Color.yellow;
        //Gizmos.DrawSphere(cameraPosition, 0.1f);
        float xCount = 10f;
        float yCount = 10f;

        for (int i = 0; i < xCount; i++)
        {
            for (int j = 0; j < yCount; j++)
            {
                var position = cameraPosition;
                position += camera.transform.forward * cameraNear;
                position += camera.transform.up * planeHeight * (-1 + i / (xCount - 1 ) * 2);
                position += camera.transform.right * planeWidth * (-1 + j / (yCount - 1) * 2);
                Gizmos.DrawSphere(position, 0.1f);
            }
        }
    }
}
