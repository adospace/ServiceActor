using System;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class ServiceDomainAttribute : Attribute
    {
        public ServiceDomainAttribute(object domainKey)
        {
            DomainKey = domainKey;
        }

        public object DomainKey { get; }
    }
}