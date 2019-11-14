using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Flatten<T>(this IEnumerable<T> e, Func<T, IEnumerable<T>> f)
        {
            if (e == null) return Enumerable.Empty<T>();

            var arr = e as T[] ?? e.ToArray();
            return arr.SelectMany(c => f(c).Flatten(f)).Concat(arr);
        }
    }
}
