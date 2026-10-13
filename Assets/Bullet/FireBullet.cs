using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

public class FireBullet : MonoBehaviour
{
    public GameObject bullet;
    public Transform enemyTrans;
    public Transform FirePoint;
    public float RotSpeed = 10.0f;
    public float TrackSpeed = 5.0f;

    public GameObject FireEffect;
    private void OnGUI()
    {
        if (GUILayout.Button("Fire"))
        {
            OnFire(FirePoint);
        }
    }

    [Button]
    public void OnFire(Transform point)
    {
        GameObject bullet = Instantiate(this.bullet);
        //random angle
        bullet.transform.position = point.position;
        int ange = 0;
        float xz = Mathf.Sin(Random.Range(-1, 2f));
        float y = Random.Range(-1, 2f)>0?1:-1;
        bullet.transform.forward += new Vector3(xz,y,Mathf.Abs(xz)); 
        
        
        var tck = bullet.GetComponent<Bullettrack>();
        tck.SetEnemy(enemyTrans,RotSpeed,TrackSpeed);
        
        //fire effect
        if (FireEffect==null)
        {
            return;
        }
        Instantiate(FireEffect, point.position, point.rotation);
        Destroy(bullet, 5.0f);
    }


}
