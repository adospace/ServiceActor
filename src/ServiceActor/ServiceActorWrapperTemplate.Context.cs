using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServiceActor
{
    public partial class ServiceActorWrapperTemplate
    {
        public ServiceActorWrapperTemplate(Type typeToWrap)
        {
            TypeToWrap = typeToWrap ?? throw new ArgumentNullException(nameof(typeToWrap));

            if (!TypeToWrap.IsInterface)
            {
                throw new InvalidOperationException("Only interfaces can be wrapped");
            }

            if (TypeToWrap.GetEvents().Any())
            {
                throw new InvalidOperationException("Type to wrap should not contain events");
            }

            ThrowIfRefOutParametersExistsForMethodsWithoutTheAllowConcurrentAccessAttribute();

            _blockCallerByDefault = BlockCaller(TypeToWrap);
            _keepAsyncContextByDefault = KeepAsyncContext(TypeToWrap);
        }

        public Type TypeToWrap { get; }

        public string TypeToWrapName => TypeToWrap.Name.Replace('`', '_');

        private string TypeToWrapFullName => TypeToWrap.GetTypeReferenceCode();

        private readonly bool _blockCallerByDefault = false;

        private readonly bool _keepAsyncContextByDefault = false;

        private IEnumerable<MethodInfo> GetMethods() => TypeToWrap
            .GetFlattenMethods()
            .Where(_ => !_.Name.StartsWith("get_") && !_.Name.StartsWith("set_") && !_.Name.StartsWith("add_") && !_.Name.StartsWith("remove_"));

        private IEnumerable<PropertyInfo> GetProperties() => TypeToWrap
            .GetFlattenProperties();

        private void ThrowIfRefOutParametersExistsForMethodsWithoutTheAllowConcurrentAccessAttribute()
        {
            foreach (var method in GetMethods()
                .Where(_ => _.GetParameters().Any(p => p.IsOut || p.ParameterType.IsByRef)))
            {
                if (!MethodAllowsConcurrentAccess(method))
                    throw new InvalidOperationException($"Method '{method.Name}' of '{TypeToWrapFullName}' contains ref or out parameters but not support concurrent access (MethodAllowsConcurrentAccess attribute)");
            }
        }

        private bool PropertyGetAllowsConcurrentAccess(PropertyInfo propertyInfo)
        {
            if (Attribute.GetCustomAttribute(propertyInfo, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute concurrentAccessAttribute)
            {
                return concurrentAccessAttribute.PropertyGet;
            }

            return false;
        }

        private bool PropertySetAllowsConcurrentAccess(PropertyInfo propertyInfo)
        {
            if (Attribute.GetCustomAttribute(propertyInfo, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute concurrentAccessAttribute)
            {
                return concurrentAccessAttribute.PropertySet;
            }

            return false;
        }

        private bool MethodAllowsConcurrentAccess(MethodInfo methodInfo)
        {
            if (Attribute.GetCustomAttribute(methodInfo, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute)
            {
                return true;
            }

            return false;
        }

        private bool BlockCaller(Type type)
        {
            if (Attribute.GetCustomAttribute(type, typeof(BlockCallerAttribute)) is BlockCallerAttribute)
            {
                return true;
            }

            return false;
        }

        private bool BlockCaller(PropertyInfo propertyInfo)
        {
            if (Attribute.GetCustomAttribute(propertyInfo, typeof(BlockCallerAttribute)) is BlockCallerAttribute)
            {
                return true;
            }

            return _blockCallerByDefault;
        }

        private bool BlockCaller(MethodInfo methodInfo)
        {
            if (Attribute.GetCustomAttribute(methodInfo, typeof(BlockCallerAttribute)) is BlockCallerAttribute)
            {
                return true;
            }

            return _blockCallerByDefault;
        }

        private bool KeepAsyncContext(Type type)
        {
            if (Attribute.GetCustomAttribute(type, typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttribute)
            {
                return keepAsyncContextAttribute.KeepContext;
            }

            return true;
        }

        private bool KeepAsyncContext(PropertyInfo propertyInfo)
        {
            if (Attribute.GetCustomAttribute(propertyInfo, typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttribute)
            {
                return keepAsyncContextAttribute.KeepContext;
            }

            return _keepAsyncContextByDefault;
        }

        private bool KeepAsyncContext(MethodInfo methodInfo)
        {
            if (Attribute.GetCustomAttribute(methodInfo, typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttribute)
            {
                return keepAsyncContextAttribute.KeepContext;
            }

            return _keepAsyncContextByDefault;
        }
    }
}