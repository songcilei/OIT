using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]
public class AOInfo : MonoBehaviour
{
    public Material mat;
    public float radius;

    public float phaA = 0.1f;
    public float phaB = 0.1f;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        mat.SetFloat("_rValue",radius);
        mat.SetVector("_pos",transform.position);
        
        //---------------------------------
        float sum = phaA + phaB;
        float diff = Mathf.Abs(phaA - phaB);
        
        mat.SetFloat("_Sum",sum);
        mat.SetFloat("_Diff",diff);
        mat.SetFloat("_phaA",phaA);
        mat.SetFloat("_phaB",phaB);
    }
}
