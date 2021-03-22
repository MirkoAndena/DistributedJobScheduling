using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedJobScheduling.Extensions
{
    public static class CollectionExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach(T item in enumerable)
                action.Invoke(item);
        }

        public static string ToString<T>(this IEnumerable<T> enumerable) 
        {
            StringBuilder stringBuilder = new StringBuilder();
            enumerable.ForEach(element => stringBuilder.Append($"{element.ToString()}, "));
            return stringBuilder.ToString().Trim(new char[] {',', ' '});
        }

        public static string ToString<K, V>(this Dictionary<K, V> dictionary) 
        {
            StringBuilder stringBuilder = new StringBuilder();
            dictionary.ForEach(element => stringBuilder.Append($"[{element.Key}]: {element.Value}, "));
            return $"{{{stringBuilder.ToString().Trim(new char[] {',', ' '})}}}";
        }
    }
}