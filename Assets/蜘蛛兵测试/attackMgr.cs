using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class attackMgr : MonoBehaviour
{
    public ActorLogic _ActorLogic;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var mousePos = Input.mousePosition;
            Debug.Log(mousePos);
            Ray ray = Camera.main
                .ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = hit.point;
                PlayEvent(hit.point);
            }
        }

    }


    private void PlayEvent(Vector3 target)
    {
        if (_ActorLogic==null)
        {
            return;
        }
        
        _ActorLogic.PlayAttack(target);
    }

}
