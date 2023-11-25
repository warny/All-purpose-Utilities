using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects;

/// <summary>
/// Fusionne deux <see cref="IEnumerable{T}"/> déjà classées. La liste de gauche étant la liste principale
/// </summary>
/// <typeparam name="T1">Type d'élément de la liste de </typeparam>
/// <typeparam name="F1"></typeparam>
/// <typeparam name="T2"></typeparam>
/// <typeparam name="F2"></typeparam>
public class ForwardFusion<T1, T2> : IEnumerable<(T1 Left, T2 Right)>, IDisposable
{
    private readonly IEnumerable<T1> leftList;
    private readonly IEnumerable<T2> rightList;

    private readonly Func<T1, T2, int> compare;

    public ForwardFusion(IEnumerable<T1> leftList, IEnumerable<T2> rightList)
    {
        leftList.ArgMustNotBeNull();
        rightList.ArgMustNotBeNull();
        this.leftList = leftList;
        this.rightList = rightList;

        if (typeof(IComparable<T2>).IsAssignableFrom(typeof(T1)))
        {
            compare = (t1, t2) => ((IComparable<T2>)t1).CompareTo(t2);
        }
        else if (typeof(IComparable<T1>).IsAssignableFrom(typeof(T2)))
        {
            compare = (t1, t2) => -((IComparable<T1>)t2).CompareTo(t1);
        }
        else if (typeof(IComparable).IsAssignableFrom(typeof(T1)))
        {
            compare = (t1, t2) => ((IComparable)t1).CompareTo(t2);
        }
        else if (typeof(IComparable).IsAssignableFrom(typeof(T1)))
        {
            compare = (t1, t2) => -((IComparable)t2).CompareTo(t1);
        }
        else
        {
            throw new ArgumentException($"Les classes {typeof(T1).Name} et {typeof(T2).Name} ne peuvent pas être comparées");
        }
    }

    public ForwardFusion(IEnumerable<T1> leftList, IEnumerable<T2> rightList, IComparer comparer)
    {
        leftList.ArgMustNotBeNull();
        rightList.ArgMustNotBeNull();
        comparer.ArgMustNotBeNull();

        this.leftList = leftList;
        this.rightList = rightList;
        this.compare = (t1, t2) => comparer.Compare(t1, t2);
    }

    public ForwardFusion(IEnumerable<T1> leftList, IEnumerable<T2> rightList, Func<T1, T2, int> compare)
    {
        leftList.ArgMustNotBeNull();
        rightList.ArgMustNotBeNull();
        compare.ArgMustNotBeNull();

        this.leftList = leftList;
        this.rightList = rightList;
        this.compare = compare;
    }

    private IEnumerable<(T1 Left, T2 Right)> Enumerate()
    {
        leftList.ArgMustNotBeNull();
        rightList.ArgMustNotBeNull();

        var leftEnum = leftList.GetEnumerator();
        var rightEnum = rightList.GetEnumerator();

        if (!leftEnum.MoveNext()) { yield break; }
        if (!rightEnum.MoveNext()) { yield break; }

        while (true)
        {
            switch (compare(leftEnum.Current, rightEnum.Current))
            {
                case 0:
                    yield return (leftEnum.Current, rightEnum.Current);
                    if (!rightEnum.MoveNext()) { yield break; }
                    break;
                case -1:
                    if (!leftEnum.MoveNext()) { yield break; }
                    break;
                case 1:
                    if (!rightEnum.MoveNext()) { yield break; }
                    break;
            }
        }
    }

    public IEnumerator<(T1 Left, T2 Right)> GetEnumerator() => Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Enumerate().GetEnumerator();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    public void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (leftList is IDisposable l)
        {
            l.Dispose();
        }
        if (rightList is IDisposable r)
        {
            r.Dispose();
        }

    }
}
