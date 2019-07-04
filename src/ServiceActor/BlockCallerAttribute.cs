using System;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Interface)]
    public class BlockCallerAttribute : Attribute
    {
    }
}