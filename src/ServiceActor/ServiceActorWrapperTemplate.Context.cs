using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ServiceActor
{
    public partial class ServiceActorWrapperTemplate
    {
        public ServiceActorWrapperTemplate(Type typeToWrap, bool sharedQueue = false)
        {
            TypeToWrap = typeToWrap ?? throw new ArgumentNullException(nameof(typeToWrap));
            SharedQueue = sharedQueue;

            if (!TypeToWrap.IsInterface)
            {
                throw new InvalidOperationException("Only interfaces can be wrapped");
            }

            if (TypeToWrap.GetEvents().Any())
            {
                throw new InvalidOperationException("Typet to wrap should not contain events");
            }
        }

        public Type TypeToWrap { get; }
        public bool SharedQueue { get; }

        public string TypeToWrapFullName => TypeToWrap.GetTypeReferenceCode();

        public IEnumerable<MethodInfo> GetMethods() => TypeToWrap
            .GetMethods()
            .Where(_ => !_.Name.StartsWith("get_") && !_.Name.StartsWith("set_") && !_.Name.StartsWith("add_") && !_.Name.StartsWith("remove_"));
    }

}
