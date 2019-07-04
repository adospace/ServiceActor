using System;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class AllowConcurrentAccessAttribute : Attribute
    {
        public AllowConcurrentAccessAttribute(bool propertyGet = true, bool propertySet = true)
        {
            PropertyGet = propertyGet;
            PropertySet = propertySet;
        }

        public bool PropertyGet { get; set; }

        public bool PropertySet { get; set; }
    }
}