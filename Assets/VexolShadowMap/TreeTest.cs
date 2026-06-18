using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

public class TreeTest : MonoBehaviour
{
    public Texture2D tex;
    void Start()
    {
        List<int> randomValues = new List<int>();
        List<int> randomValues2 = new List<int>();
        randomValues.Add(5);
        randomValues.Add(7);
        randomValues.Add(37);
        randomValues.Add(38);
        randomValues.Add(39);
        randomValues.Add(40);
        randomValues.Add(95);

        randomValues.Add(52);
        randomValues.Add(54);
        randomValues.Add(56);
        randomValues.Add(58);
        randomValues.Add(60);
        randomValues.Add(62);
        randomValues.Add(64);
        randomValues.Add(66);
        randomValues.Sort(); 
        fillData(randomValues, -1, 7+8, randomValues2);

        //网上常见二叉树
        //test2(randomValues2, findValues);
        BinaryTree bt = new BinaryTree();
      
        foreach (var item in randomValues2)
        {
           
            bt.Insert(item);
        }
       
        //bt.fillToFullTree();// 如果用相对索引偏移量查找子节点 需要调用这句 变成完全二叉树 有空节点的二叉树 节点与子对象索引间隔不固定的 不能查找的
     


        var nodes = bt.getAllNodes(true);
        tex = new Texture2D(nodes.Count+1, 1, TextureFormat.RGBAFloat, false);//其实 rfloat 就可以 但为了不想看到报错 就用rgba
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var colors = tex.GetPixels();
        print("------");
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].index = i;
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            print(nodes[i].Item);
            colors[i].r = nodes[i].Item;
            colors[i].g = nodes[i].LeftChild!=null? nodes[i].LeftChild.index:-1;
         
        }
        tex.SetPixels(colors);
        tex.Apply();
        Shader.SetGlobalTexture("_TestBTreeTex", tex);
        Shader.SetGlobalInt("_TestBTreeNodeCount", nodes.Count);
    }

    private void fillData(List<int> randomValues, int min, int max, List<int> randomValues2)
    {
        
        if (min + 1 >= max) return;
        
        if (randomValues.Count == randomValues2.Count) return;
        int mid = (min + max) / 2;
        
        randomValues2.Add(randomValues[mid]);
        fillData(randomValues, min, mid, randomValues2);
        fillData(randomValues, mid,max, randomValues2);
    }
 
     
    public class Node
    {
        public int Item;
        public int index;
        public Node LeftChild;
        public Node RightChild;
     

        public Node(int data)
        {
            this.Item = data;
        }
    }
    public class BinaryTree
    {
        //表示根节点
        public Node _root;
         


        //查找节点
        public Node Find(int key)
        {
            Node current = _root;
            while (current != null)
            {
                if (current.Item > key)
                {//当前值比查找值大，搜索左子树
                    current = current.LeftChild;
                }
                else if (current.Item < key)
                {//当前值比查找值小，搜索右子树
                    current = current.RightChild;
                }
                else
                {
                    return current;
                }
            }
            return null;//遍历完整个树没找到，返回null
        }

        //插入节点
        public bool Insert(int data)
        {
            Node newNode = new Node(data);
            
            if (_root == null)
            {//当前树为空树，没有任何节点
                _root = newNode;
                return true;
            }
            else
            {
                Node current = _root;
                Node parentNode = null;
                while (current != null)
                {
                    if (current.Item == data) return true;
                    parentNode = current;
                     
                    if (current.Item > data)
                    {//当前值比插入值大，搜索左子节点
                        current = current.LeftChild;
                        if (current == null)
                        {//左子节点为空，直接将新值插入到该节点
                            parentNode.LeftChild = newNode;
                            return true;
                        }
                    }
                    else
                    {
                        current = current.RightChild;
                        if (current == null)
                        {//右子节点为空，直接将新值插入到该节点
                            parentNode.RightChild = newNode;
                          
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        //完全二叉树模式 这样每一层数量固定 gpu内无对象引用功能 查找自己child时 仅仅通过offset即可实现
        public  void fillToFullTree() {
            List<Node> printNodes = new List<Node>();
            printNodes.Add(_root);
            int index = 0;
            while (true)
            {
                int end = printNodes.Count;
                int realValueCount=0;
                for (int j = index; j < end; j++)
                {

                    if (printNodes[j].LeftChild == null) 
                        printNodes[j].LeftChild = new Node(-1);
                    else if(printNodes[j].LeftChild.Item!=-1)
                        realValueCount++;
                    
                    printNodes.Add(printNodes[j].LeftChild);
                    
                    if (printNodes[j].RightChild == null) 
                        printNodes[j].RightChild = new Node(-1);
                    else if (printNodes[j].RightChild.Item != -1)
                        realValueCount++;
                    
                    printNodes.Add(printNodes[j].RightChild);
                }
                
                if (realValueCount == 0)
                {
                    for (int j = index; j < end; j++)
                    {
                        printNodes[j].LeftChild = printNodes[j].RightChild = null;
                    }
                    break;
                }
                index = end ;
            }
        }
        public List<Node> getAllNodes(bool printNode=false)
        {
            List<Node> printNodes = new List<Node>();
            printNodes.Add(_root);
            int index = 0;
            while(true)
            {
                int end = printNodes.Count;
                string str = "";
                for (int j = index; j < end; j++)
                {
                    if(printNode) 
                        str += printNodes[j].Item + "||";
                    
                    if (printNodes[j].LeftChild != null)
                        printNodes.Add(printNodes[j].LeftChild);
                    else if(printNode) 
                        str += "-||";
                    
                    if (printNodes[j].RightChild != null)
                        printNodes.Add(printNodes[j].RightChild);
                    else if(printNode) 
                        str += "-||";
                }
                if (printNode) {
                    print(str);
                }
                print(end - index);
                if (end - index == 0) break;
                index   = end;
            }
            return printNodes;
        }
    }
}