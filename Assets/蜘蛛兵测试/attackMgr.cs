using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class attackMgr : MonoBehaviour
{
    public ActorLogic _ActorLogic;
    
    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var mousePos = Input.mousePosition;
    
            Ray ray = Camera.main
                .ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);

                go.transform.position = hit.point;
                var tt = go.AddComponent<TargetLogicTest>();
                tt.Init(AttackPart.rightUp,_ActorLogic);
                PlayEvent(hit.point,AttackPart.rightUp);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            var mousePos = Input.mousePosition;
            Ray ray = Camera.main
                .ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);

                go.transform.position = hit.point;
                var tt = go.AddComponent<TargetLogicTest>();
                tt.Init(AttackPart.leftUp,_ActorLogic);
                PlayEvent(hit.point,AttackPart.leftUp);
            }
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            var mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            RaycastHit hit;
            if (Physics.Raycast(ray,out hit))
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = hit.point;
                var tt = go.AddComponent<TargetLogicTest>();
                tt.Init(AttackPart.leftDown,_ActorLogic);
                PlayEvent(hit.point,AttackPart.leftDown);
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            var mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            RaycastHit hit;
            if (Physics.Raycast(ray,out hit))
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                
                go.transform.position = hit.point;
                var tt = go.AddComponent<TargetLogicTest>();
                tt.Init(AttackPart.rightDown,_ActorLogic);
                PlayEvent(hit.point,AttackPart.rightDown);
            }
        }

    }


    private void PlayEvent(Vector3 target,AttackPart part)
    {
        if (_ActorLogic==null)
        {
            return;
        }
        
        _ActorLogic.PlayAttack(target,part);
    }

}
