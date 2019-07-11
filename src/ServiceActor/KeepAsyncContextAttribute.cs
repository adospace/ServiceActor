using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public class KeepAsyncContextAttribute : Attribute
    {
        public KeepAsyncContextAttribute(bool keepContext = true)
        {
            KeepContext = keepContext;
        }

        public bool KeepContext { get; }
    }
}
