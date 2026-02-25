using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public sealed class RandomManager : MonoSingleton<RandomManager>, IGameFlowManaged
{
    private System.Random random;
    
    public void SetSeed(int seed)
    {
        random = new System.Random(seed);
    }

    public IEnumerator Init()
    {
        Debug.Log("随机数管理器初始化完毕");
        yield break;
    }

    public IEnumerator Begin()
    {
        Debug.Log("随机数管理器启动完毕");
        yield break;
    }

    public void Tick(ulong currentTick){}

    public IEnumerator Clean()
    {
        random = null;
        yield break;
    }
    
    public float GetRandom(float min = float.MinValue, float max = float.MaxValue)
    {
        return (float)(random.NextDouble() * (max - min) + min);
    }
    
    public int GetRandom(int min = int.MinValue, int max = int.MaxValue)
    {
        return random.Next(min, max);
    }
    
    public Vector3 GetRandomVector()
    {
        return GetRandomVector(-1f, 1f);
    }
    
    public Vector3 GetRandomVector(float min, float max)
    {
        return new Vector3(
            GetRandom(min, max),
            GetRandom(min, max),
            GetRandom(min, max)
        );
    }
    
    public Vector3 GetRandomVector(Vector3 min, Vector3 max)
    {
        return new Vector3(
            GetRandom(min.x, max.x),
            GetRandom(min.y, max.y),
            GetRandom(min.z, max.z)
        );
    }
    
    public Vector3 GetRandomVector(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
        return new Vector3(
            GetRandom(minX, maxX),
            GetRandom(minY, maxY),
            GetRandom(minZ, maxZ)
        );
    }

    public Vector2 GetRandomVector2()
    {
        return GetRandomVector2(-1f, 1f);
    }

    public Vector2 GetRandomVector2(float min, float max)
    {
        return new Vector2(GetRandom(min, max), GetRandom(min, max));
    }

    public Vector2 GetRandomVector2(Vector2 min, Vector2 max)
    {
        return new Vector2(GetRandom(min.x, max.x), GetRandom(min.y, max.y));
    }
    
    public Vector3 GetRandomDirection()
    {
        float theta = GetRandom(0f, Mathf.PI * 2);
        float phi = Mathf.Acos(GetRandom(-1f, 1f));
        return new Vector3(
            Mathf.Sin(phi) * Mathf.Cos(theta),
            Mathf.Sin(phi) * Mathf.Sin(theta),
            Mathf.Cos(phi)
        );
    }
    
    public bool NextBool()
    {
        return random.Next(2) == 0;
    }
    
    public Color GetRandomColor()
    {
        return Color.HSVToRGB(GetRandom(0f, 1f), 1f, 1f);
    }
    
    public Color GetRandomColor(float saturationMin, float saturationMax, float valueMin, float valueMax)
    {
        float h = GetRandom(0f, 1f);
        float s = GetRandom(saturationMin, saturationMax);
        float v = GetRandom(valueMin, valueMax);
        return Color.HSVToRGB(h, s, v);
    }
    
    public T Choose<T>(T[] array)
    {
        if (array == null || array.Length == 0)
            return default(T);
        return array[random.Next(array.Length)];
    }
    
    public T Choose<T>(List<T> list)
    {
        if (list == null || list.Count == 0)
            return default(T);
        return list[random.Next(list.Count)];
    }
    
    public bool Probability(float probability)
    {
        return GetRandom(0f, 1f) < probability;
    }
}
