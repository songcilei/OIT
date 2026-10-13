using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullettrack : MonoBehaviour
{
    private Transform enemy;
    
    public float minDist = 0.2f;
    private float AngleSpeed = 5.0f;
    private float trackSpeed = 1.0f;
    private Vector3 targetDir;
    private Damage damage;
    public void SetEnemy(Transform target,float RotSpeed,float TrackSpeed)
    {
        enemy = target;
        AngleSpeed = RotSpeed;
        trackSpeed = TrackSpeed;
        damage = enemy.gameObject.GetComponent<Damage>();
    }

    public void Update()
    {
        if (enemy == null) return;


        
        //fly path

        targetDir = Vector3.Normalize(enemy.transform.position-this.transform.position);
        Vector3 dir = Vector3.Lerp(transform.forward, targetDir, Time.deltaTime*trackSpeed);
        transform.forward = dir;
        transform.Translate(Vector3.forward * Time.deltaTime*AngleSpeed, Space.Self);
        
        //check distance
        if (Vector3.Distance(this.transform.position,enemy.transform.position)<minDist )
        {
            Damage();    
        }
    }


    private void Damage()
    {
        damage.Damaged();
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }



    private void OnDrawGizmos()
    {
        if (enemy==null)
        {
            return;
        }
        targetDir = Vector3.Normalize(enemy.transform.position-this.transform.position);
        Vector3 dir = Vector3.Lerp(transform.forward, targetDir, Time.deltaTime*trackSpeed);
        Gizmos.DrawLine(this.transform.position,this.transform.position + dir *1);
    }
}
