using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WPoint : MonoBehaviour
{
    public Transform _Target;
    
    void Update()
    {
        if (_Target != null)
        {
            transform.position = _Target.position;
        }
    }
}
