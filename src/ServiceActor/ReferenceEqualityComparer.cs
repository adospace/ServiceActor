using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ServiceActor
{
    internal class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(RuntimeHelpers.GetHashCode(x), RuntimeHelpers.GetHashCode(y));
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
