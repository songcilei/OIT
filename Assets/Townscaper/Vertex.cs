using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vertex
{

}

public class Coord//Cube Coord
{
    public readonly int q;//x 右斜方向
    public readonly int r;//z 下上方向
    public readonly int s;//y 左斜方向

    public readonly Vector3 worldPosition;
    public Coord(int q,int r,int s)
    {
        this.q = q;
        this.r = r;
        this.s = s;
        worldPosition = WorldPosition();
    }

    //从cube 坐标系转换到世界坐标系
    public Vector3 WorldPosition()
    {
        return new Vector3(q * Mathf.Sqrt(3) / 2, 0 - (float)r - ((float)q / 2)) * 2*Grid.cellSize;
    }

    //coord direction
    static public Coord[] directions = new Coord[]
    {
        new Coord(0, 1, -1),//左下
        new Coord(-1, 1, 0),//右下
        new Coord(-1, 0, 1),//右
        new Coord(0, -1, 1),//左上
        new Coord(1, -1, 0),//右上
        new Coord(1, 0, -1)//左
    };

    //获取六边形周围的六边形点
    static public Coord Direction(int direction)
    {
        return Coord.directions[direction];
    }

    //向外增加一个环
    public Coord Add(Coord coord)
    {
        return new Coord(q + coord.q, r + coord.r, s + coord.s);
    }

    //获取某个方向上的外层的六边形数据
    public Coord Scale(int k)
    {
        return new Coord(q * k, r * k, s * k);
    }

    //获取 附近的六边形 数据点 或者可以说是每圈？？
    public Coord Neighbor(int direction)
    {
        return Add(Direction(direction));
    }

    //根据传入的半径层数 创建对应层数的六边形中心点
    public static List<Coord> Coord_Ring(int radius)
    {
        List<Coord> result = new List<Coord>();
        if (radius == 0)
        {
            result.Add(new Coord(0,0,0));//中心点
        }
        else
        {
            Coord coord = Coord.Direction(4).Scale(radius);//获取右上方向5层外层的六边形数据
            for (int i = 0; i < 6; i++)// 6个方向 = 六边形6条边
            {
                for (int j = 0; j < radius; j++)// 每条边走 radius 个格子
                {
                    result.Add(coord); // 把格子加入结果
                    coord = coord.Neighbor(i); // 沿着第 i 个方向走下一步 这个是Cube坐标系的一个特性
                }
            }
        }

        return result;
    }
    //根据六边形半径创建多层环绕点
    public static List<Coord> Coord_Hex()
    {
        List<Coord> result = new List<Coord>();
        for (int i = 0; i < Grid.radius; i++)
        {
            result.AddRange(Coord_Ring(i));
        }

        return result;
    }
}

public class Vertex_hex : Vertex
{
    public readonly Coord coord;

    public Vertex_hex(Coord coord)
    {
        this.coord = coord;
    }

    public static void Hex(List<Vertex_hex> vertices)
    {
        foreach (Coord coord in Coord.Coord_Hex())
        {
            vertices.Add(new Vertex_hex(coord));
        }
    }

    public static List<Vertex_hex> GrabRing(int radius,List<Vertex_hex> vertices)
    {
        if (radius == 0)
        {
            return vertices.GetRange(0, 1);
        }

        return vertices.GetRange(radius * (radius - 1) * 3 + 1, radius * 6);
    }
}