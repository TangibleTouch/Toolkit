using UnityEngine;
using System.Collections;

public class CameraMouse : MonoBehaviour
{
    Ray ray;
    RaycastHit hit;

    void Update()
    {
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    }
}
