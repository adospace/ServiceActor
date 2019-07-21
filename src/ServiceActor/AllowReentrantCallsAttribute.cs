using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class AllowReentrantCallsAttribute : Attribute
    {
        public AllowReentrantCallsAttribute(bool allow = true)
        {
            Allow = allow;
        }

        public bool Allow { get; }
    }
}
