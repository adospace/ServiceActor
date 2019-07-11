using System;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Class)]
    public class BlockCallerAttribute : Attribute
    {
    }
}