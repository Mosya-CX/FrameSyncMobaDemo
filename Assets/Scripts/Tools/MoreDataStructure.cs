using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PriorityQueue<T>
{
    private List<T> _elements = new List<T>();
    private readonly IComparer<T> _comparator;
    public int Count => _elements.Count;

    public PriorityQueue(IComparer<T> comparator)
    {
        _comparator = comparator;
    }
    public void Enqueue(T element)
    {
        _elements.Add(element);
        UpAdjust(_elements.Count - 1);
    }
    public T Dequeue()
    {
        if (_elements.Count == 0) throw new InvalidOperationException("Queue is empty.");
        T top = _elements[0];
        _elements[0] = _elements[^1];
        _elements.RemoveAt(_elements.Count - 1);
        DownAdjust(0);
        return top;
    }
    public T Peek()
    {
        if (_elements.Count == 0) throw new InvalidOperationException("Queue is empty.");
        return _elements[0];
    }
    private void UpAdjust(int index)
    {
        int parent = (index - 1) / 2;
        T temp = _elements[index];
        while (index > 0 && _comparator.Compare(temp, _elements[parent]) > 0)
        {
            _elements[index] = _elements[parent];
            index = parent;
            parent = (parent - 1) / 2;
        }
        _elements[index] = temp;
    }
    private void DownAdjust(int index)
    {
        int count = _elements.Count;
        T temp = _elements[index];
        int child = index * 2 + 1;
        while (child < count)
        {
            if (child + 1 < count && _comparator.Compare(_elements[child + 1], _elements[child]) > 0)
                child++;
            if (_comparator.Compare(temp, _elements[child]) >= 0) break;
            _elements[index] = _elements[child];
            index = child;
            child = index * 2 + 1;
        }
        _elements[index] = temp;
    }

    public void Clear()
    {
        _elements.Clear();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _elements.GetEnumerator();
    }
}


/// <summary>
/// 按自定义比较器排序的列表，当两个元素被视为相等时，后加入的元素排在前面。
/// </summary>
/// <typeparam name="T">元素类型</typeparam>
public class OrderedList<T> : IEnumerable<T>
{
    // 内部包装类：存储值和插入顺序
    private class Entry
    {
        public T Value;
        public int Order; // 插入顺序，值越大表示越晚插入
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly IComparer<T> comparer;      // 用于比较 T 的规则
    private int orderCounter;                     // 递增的插入序号

    /// <summary>列表中元素的数量</summary>
    public int Count => entries.Count;

    /// <summary>通过索引访问元素（只读）</summary>
    public T this[int index] => entries[index].Value;

    /// <summary>
    /// 构造函数：使用自定义比较器
    /// </summary>
    public OrderedList(IComparer<T> comparer)
    {
        this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    /// <summary>
    /// 构造函数：使用比较委托（更方便）
    /// </summary>
    public OrderedList(Comparison<T> comparison) : this(Comparer<T>.Create(comparison)) { }

    /// <summary>
    /// 添加元素
    /// </summary>
    public void Add(T value)
    {
        var newEntry = new Entry { Value = value, Order = orderCounter++ };

        // 二分查找插入位置，使用自定义比较器 + 插入顺序规则
        int index = entries.BinarySearch(newEntry, Comparer<Entry>.Create(CompareEntries));
        if (index < 0)
            index = ~index;

        entries.Insert(index, newEntry);
    }

    /// <summary>
    /// 移除指定索引处的元素
    /// </summary>
    public void RemoveAt(int index)
    {
        entries.RemoveAt(index);
    }

    /// <summary>
    /// 移除第一个与指定元素相等（根据比较器）的元素。
    /// </summary>
    /// <param name="item">要移除的元素</param>
    /// <returns>如果成功移除则返回 true，否则返回 false</returns>
    public bool Remove(T item)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (comparer.Compare(entries[i].Value, item) == 0)
            {
                entries.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 移除所有满足指定条件的元素。
    /// </summary>
    /// <param name="match">条件谓词</param>
    /// <returns>移除的元素数量</returns>
    public int RemoveAll(Predicate<T> match)
    {
        if (match == null) throw new ArgumentNullException(nameof(match));
        return entries.RemoveAll(entry => match(entry.Value));
    }

    /// <summary>
    /// 清空列表
    /// </summary>
    public void Clear()
    {
        entries.Clear();
        orderCounter = 0;
    }

    /// <summary>
    /// 判断是否包含某个元素（使用默认相等比较器）
    /// </summary>
    public bool Contains(T value)
    {
        foreach (var entry in entries)
            if (EqualityComparer<T>.Default.Equals(entry.Value, value))
                return true;
        return false;
    }

    // 比较两个 Entry 的规则：
    // 1. 先按用户提供的比较器比较 Value
    // 2. 如果相等，则按插入顺序降序（后插入的 Order 更大，应排在前面）
    private int CompareEntries(Entry a, Entry b)
    {
        int cmp = comparer.Compare(a.Value, b.Value);
        if (cmp != 0)
            return cmp;
        // 值相等时，后插入的（Order 大）视为“更小”，以便排在前面
        return b.Order.CompareTo(a.Order);
    }

    // 实现 IEnumerable<T>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var entry in entries)
            yield return entry.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}