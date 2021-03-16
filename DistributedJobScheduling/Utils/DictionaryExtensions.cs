using System.Text;
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

    public static string ToString<K, V>(this Dictionary<K, V> dictionary) 
    {
        StringBuilder stringBuilder = new StringBuilder();
        dictionary.ForEach(element => stringBuilder.Append($" {element.Key}: {element.Value}"));
        return stringBuilder.ToString().Trim();
    }
}