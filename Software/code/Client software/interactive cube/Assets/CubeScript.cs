using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeScript : MonoBehaviour
{
    public float RotationSpeed = 5;
    // Start is called before the first frame update
    void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
       if(Input.GetMouseButton(2))
        {
            //transform.rotation = Quaternion.identity;
            //transform.RotateAround(transform.position + new Vector3(arrow.GetWidth() / 2f, this.GetHeight() / 2f, 0f), Vector3.forward, (Input.GetAxis("Mouse X") * RotationSpeed * Time.deltaTime));
           transform.Rotate((Input.GetAxis("Mouse Y") * RotationSpeed * Time.deltaTime), (Input.GetAxis("Mouse X") * RotationSpeed * Time.deltaTime), 0);
        }
    }
}