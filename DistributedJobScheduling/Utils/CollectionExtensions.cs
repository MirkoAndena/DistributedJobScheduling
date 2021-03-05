using System;
using System.Collections.Generic;

public static class CollectionExtensions
{
    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach(T item in enumerable)
            action.Invoke(item);
    }
}