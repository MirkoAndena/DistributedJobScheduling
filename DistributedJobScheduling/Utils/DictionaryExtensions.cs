using System;
using System.Collections.Generic;

public static class DictionaryExtensions
{
    public static void ForEach<K, V>(this Dictionary<K, V> dictionary, Action<K, V> action)
    {
        using (Dictionary<K, V>.Enumerator enumerator = dictionary.GetEnumerator())
        {
            while (enumerator.MoveNext())
                action.Invoke(enumerator.Current.Key, enumerator.Current.Value);
        }
    }
}