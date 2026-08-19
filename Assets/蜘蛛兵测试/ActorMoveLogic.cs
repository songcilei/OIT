using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActorMoveLogic : MonoBehaviour
{
    private Animator _animator;
    public float speed = 2;
    public float turnSpeed = 12;
    private float h;
    private float v;
    private Camera cam;
    private Vector3 camForward;
    private Vector3 movement;
    


    private void Init(Camera cam)
    {
        _animator = this.GetComponent<Animator>();
        this.cam = cam;
    }

    private void Start()
    {

        Init(Camera.main);
    }


    // Update is called once per frame
    void Update()
    {
        Move();
    }



    void Move()
    {
        if (cam==null)
        {
            Debug.LogError("cam is null!!!");
        }
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");
        Vector2 ve = new Vector2(h,v);

        //移动动画
        float moveSpeed = Vector3.Dot(ve, ve);
        _animator.SetFloat("Speed",moveSpeed);
        
        transform.Translate(cam.transform.right * h * speed*Time.deltaTime + 
                            camForward * v * speed*Time.deltaTime,Space.World);
        if (h!=0 ||v!=0)
        {
            Rotating(h,v);
        }
    }

    void Rotating(float h,float v)
    {
        camForward = Vector3.Cross(cam.transform.right, Vector3.up);
        Vector3 targetDir = cam.transform.right * h + camForward * v;
        Quaternion targetRotation = Quaternion.LookRotation(targetDir, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

}
