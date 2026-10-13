using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Damage : MonoBehaviour
{
    public GameObject DamageEffect;
    public void Damaged()
    {
        if (DamageEffect==null)
        {
            return;
        }
        var ef = Instantiate(DamageEffect, transform.position, transform.rotation);
        Destroy(ef,5);
    }
}
