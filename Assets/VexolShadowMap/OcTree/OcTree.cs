using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VexolShadowMap
{
    public class OcTree
    {
        public Bounds bound;
        public OcNode root;
        public int maxDepth;
        public int maxChilCount;
        public List<OcNode> Nodes = new List<OcNode>();
        private Vector3 min;
        private Vector3 max;
        public OcTree(Bounds bound,int maxDepth)
        {
            this.bound = bound;
            min = bound.center - bound.extents;
            max = bound.center + bound.extents;
            this.maxDepth = maxDepth;
            root = new OcNode(null, 1, maxDepth, bound);//create tree
            GetAllNodeIndex();//这个是均匀收集版本
            // GetAllNotUniformNodeIndex();
        }

        //之所以这么设计 Node 的index 是为了在shader内读取方便  这是均匀收集
        public void GetAllNodeIndex()
        {
            Nodes = new List<OcNode>();
            Nodes.Add(root);
            int startIndex = 0;
            int endIndex = Nodes.Count;
            
            while (startIndex!=endIndex)
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    if (Nodes[i].children!=null)
                    {
                        Nodes.AddRange(Nodes[i].children);
                    }
                }
                startIndex = endIndex;
                endIndex = Nodes.Count;
            }

            //设置对应的index
            for (int i = 0; i < Nodes.Count; i++)
            {
                Nodes[i].index = i;
            }
        }
        
        /// <summary>
        /// 这是上面的非均匀收集版本！！
        /// </summary>
        public void GetAllNotUniformNodeIndex()
        {
            Nodes = new List<OcNode>();
            Nodes.Add(root);
            int startIndex = 0;
            int endIndex = Nodes.Count;
            while (startIndex != endIndex)
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    if (Nodes[i].children!=null && Nodes[i].flag == 0)
                    {
                        Nodes.Add(Nodes[i].children[0]);
                        Nodes.Add(Nodes[i].children[1]);
                        Nodes.Add(Nodes[i].children[2]);
                        Nodes.Add(Nodes[i].children[3]);
                        // Nodes.AddRange(Nodes[i].children);
                    }
                }
                startIndex = endIndex;
                endIndex = Nodes.Count;
            }
            
            //设置对应的index
            for (int i = 0; i < Nodes.Count; i++)
            {
                Nodes[i].index = i;
            }
        }
    }

}
