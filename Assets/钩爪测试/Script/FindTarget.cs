using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum PathState
{
    None,
    InitForward,
    InitReForward,
    Forward,
    InitBack,
    InitReBack,
    Back
}


public class FindTarget : MonoBehaviour
{
    public GameObject headNode;
    public GameObject endNode;
    public float speed= 0.1f; 
    private Vector3 targetPos;


    public PathState _state = PathState.None;
    public int sample = 32;
    public List<Vector3> pathPoints = new List<Vector3>();
    void Start()
    {
        targetPos = headNode.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                targetPos = hit.point;
                // ComputePathPoint(targetPos);
                if (_state == PathState.None)
                {
                    _state = PathState.InitForward;
                }

                if (_state == PathState.Forward)
                {
                    _state = PathState.InitReForward;
                }

                if (_state == PathState.Back)
                {
                    _state = PathState.InitReBack;
                }
            }

        }
        
        UpdatePath(_state,targetPos);
    }

    public void UpdatePath(PathState state,Vector3 targetPos)
    {
        switch (state)
        {
            case PathState.None:
                break;
            case PathState.InitForward:
                ComputePathPoint(targetPos);
                _state = PathState.Forward;
                break;
            case PathState.InitReForward:
                ComputeRePathPoint(targetPos);
                _state = PathState.Forward;
                break;
            case PathState.Forward:
                MoveForwardTarget();
                break;
            case PathState.InitBack:
                ComputeBackPathPoint(targetPos);
                _state = PathState.Back;
                break;
            case PathState.InitReBack:
                ComputeReBackPathPoint(targetPos);
                _state = PathState.Forward;
                break;
            case PathState.Back:
                MoveBackTarget();
                break;
            default:
                break;
        }
    }
    
    


    void ComputePathPoint(Vector3 targetPos)
    {

        pathPoints.Clear();
        index = 1;

        for (int i = 0; i < sample; i++)//正常的
        {
            var point = Vector3.Lerp(endNode.transform.position, targetPos,(float)i/sample);
            pathPoints.Add(point);
        }
            
    }

    void ComputeRePathPoint(Vector3 targetPos)
    {
        var currentPoint = pathPoints[index];
        List<Vector3> oldPoints = pathPoints.GetRange(0, index);
        pathPoints.Clear();
        pathPoints.AddRange(oldPoints);

        int hasCount = sample - oldPoints.Count;
        for (int i = 0; i < hasCount; i++)
        {
            var point = Vector3.Lerp(currentPoint, targetPos, (float)i / hasCount);
            pathPoints.Add(point);
        }
    }

    void ComputeBackPathPoint(Vector3 targetPos)
    {
        // pathPoints.Clear();
        index = 1;
        pathPoints.Reverse();
        // for (int i = 0; i < sample; i++)//正常的
        // {
        //     var point = Vector3.Lerp(targetPos,endNode.transform.position,(float)i/sample);
        //     pathPoints.Add(point);
        // }
    }
    
    void ComputeReBackPathPoint(Vector3 targetPos)
    {
        var currentPoint = pathPoints[index];
        List<Vector3> oldPoints = pathPoints.GetRange(index, sample-index);
        pathPoints.Clear();
        pathPoints.AddRange(oldPoints);

        int hasCount = sample - oldPoints.Count;
        for (int i = 0; i < hasCount; i++)
        {
            var point = Vector3.Lerp(currentPoint, targetPos, (float)i / hasCount);
            pathPoints.Add(point);
        }
    }
    
    public int index = 1;
    void MoveForwardTarget()
    {
        // index = Mathf.Min(sample - 1, index);
        Debug.Log(index);
        Vector3 dir = Vector3.Normalize(pathPoints[index] - headNode.transform.position);

        
        if (Vector3.Distance(pathPoints[index] ,headNode.transform.position)<0.1f)//跳到下一个路点
        {
            index++;
        }
        if (index == pathPoints.Count)
        {
            _state = PathState.InitBack;
        }
        headNode.transform.position += dir * speed;
    }
    

    void MoveBackTarget()
    {
        // index = Mathf.Min(sample - 1, index);
        Vector3 dir = Vector3.Normalize(pathPoints[index] - headNode.transform.position);
        

        
        if (Vector3.Distance(pathPoints[index] ,headNode.transform.position)<0.1f)//跳到下一个路点
        {
            index++;
        }
        if (index == pathPoints.Count)
        {
            _state = PathState.None;
        }

        
        headNode.transform.position += dir * speed;
    }



    private void OnDrawGizmos()
    {
        if (pathPoints.Count>0)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < sample; i++)
            {
                Gizmos.DrawSphere(pathPoints[i],0.25f);
            }
        }

    }
}
