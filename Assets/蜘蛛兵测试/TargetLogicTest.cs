using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetLogicTest : MonoBehaviour
{
    public AttackPart part = AttackPart.None;
    public ActorLogic actor;
    private bool track = false;
    private bool move = false;
    private float FrameCount;
    private Vector3 dir = Vector3.zero;
    public void Init(AttackPart part,ActorLogic logic)
    {
        this.part = part;
        this.actor = logic;
    }


    void Start()
    {
        
    }
    
    // Update is called once per frame
    void Update()
    {
        Vector3 Point = actor.GetClawPointPosition(part);
        if (Vector3.Distance(Point,this.transform.position)<1 && !move)
        {
            track = true;
        }

        if (track)
        {
            FrameCount += Time.deltaTime;
            transform.position = Point;
            if (FrameCount>0.8f)//假设离这里是脱离动画的时间
            {
                track = false;
                move = true;
                dir = Vector3.Normalize(Point-actor.transform.position);
            }
        }

        if (move)
        {
            transform.Translate(dir * Time.deltaTime*70);
            // transform.Rotate(transform.up,Time.deltaTime*1000,Space.Self);
        }
    }
}
