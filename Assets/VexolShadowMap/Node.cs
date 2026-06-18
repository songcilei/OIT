using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VexolShadowMap
{
    public class Node
    {
        public int level;
        public int x;
        public int z;
        public int size;
        public int flag;
        public Node parent;
        public Node[] children;
        public int index;
        public int depth;
        public Bounds bound;

        public Node(Node parent,int depth,int maxDepth,Bounds bound)
        {
            this.parent = parent;
            this.depth = depth;
            this.bound = bound;
            this.x = (int)(bound.center.x-bound.size.x/2)*10;//乘以10  是为了方便计算 以0.1米为 1个单位
            this.z = (int)(bound.center.z-bound.size.z/2)*10;
            this.flag = 0;
 
            if (depth < maxDepth)
            {
                CreateChild(depth,maxDepth,bound);
            }
        }

        private void CreateChild(int depth,int maxDepth,Bounds bounds) {
            children = new Node[4];
            int index = 0;
            for (int i=-1;i<=1;i+=2) {
                for (int j =-1;j<=1;j+=2) {
                    Vector3 centerOffset = new Vector3(bounds.size.x/4*i,0,bounds.size.z/4*j);
                    Vector3 cSize = new Vector3(bounds.size.x/2,bounds.size.y,bounds.size.z/2);
                    Bounds cBound = new Bounds(bounds.center+centerOffset,cSize);
                    children[index++] = new Node(this, depth + 1,maxDepth,cBound);
                }
            }
        }

        public bool GetChildFlag()
        {
            if (children[0].flag ==1 && children[1].flag ==1 && children[2].flag ==1 && children[3].flag ==1)
            {
                return true;
            }

            return false;
        }
        
        public void insterNode()
        {
            
        }


        
        public void GizmosBound(float height,Vector3 lightDir)
        {
            if (flag==1)
            {
                Gizmos.color = Color.red;
                // Gizmos.DrawLine(bound.center, bound.center+ lightDir*100);
                Gizmos.DrawWireCube(bound.center,bound.size+new Vector3(0,height,0));
                
            }
            else
            {
                Gizmos.color = Color.white;
                if (children!=null)
                {
                    foreach (var child in children)
                    {
                        child.GizmosBound(height,lightDir);
                    }
                }

            }
            Gizmos.DrawWireCube(bound.center,bound.size+new Vector3(0,height,0));
        }
    }

}
