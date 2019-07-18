using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServiceActor
{
    public partial class ServiceActorWrapperTemplate
    {
        public ServiceActorWrapperTemplate(Type typeToWrap, Type typeOfObjectToWrap)
        {
            TypeToWrap = typeToWrap ?? throw new ArgumentNullException(nameof(typeToWrap));
            TypeOfObjectToWrap = typeOfObjectToWrap ?? throw new ArgumentNullException(nameof(typeOfObjectToWrap));

            if (!TypeToWrap.IsInterface)
            {
                throw new InvalidOperationException("Only interfaces can be wrapped");
            }

            if (TypeToWrap.GetEvents().Any())
            {
                throw new InvalidOperationException("Type to wrap should not contain events");
            }


            ThrowIfRefOutParametersExistsForMethodsWithoutTheAllowConcurrentAccessAttribute();

            _blockCallerByDefault = BlockCaller(TypeOfObjectToWrap) || BlockCaller(TypeToWrap);
            _keepAsyncContextByDefault = KeepAsyncContext(TypeOfObjectToWrap) || KeepAsyncContext(TypeToWrap);
        }

        public Type TypeToWrap { get; }
        public Type TypeOfObjectToWrap { get; }

        public string TypeToWrapName => TypeToWrap.Name.Replace('`', '_');

        public string TypeToWrapFullName => TypeToWrap.GetTypeReferenceCode();
        public string TypeOfObjectToWrapFullName => TypeOfObjectToWrap.GetTypeReferenceCode();

        private readonly bool _blockCallerByDefault = false;

        private readonly bool _keepAsyncContextByDefault = false;

        private IEnumerable<InterfaceMethod> GetMethods() => TypeToWrap
            .GetFlattenMethods()
            .Where(_ => !_.Info.Name.StartsWith("get_") && !_.Info.Name.StartsWith("set_") && !_.Info.Name.StartsWith("add_") && !_.Info.Name.StartsWith("remove_"));

        private IEnumerable<InterfaceProperty> GetProperties()
        {
            var allPropertyMethods = TypeToWrap
                .GetFlattenMethods()
                .Where(_ => _.Info.Name.StartsWith("get_") || _.Info.Name.StartsWith("set_"))
                .ToList();

            while (allPropertyMethods.Count > 0)
            {
                if (allPropertyMethods[0].Info.Name.StartsWith("get_"))
                {
                    var getAccessor = allPropertyMethods[0];
                    var propertyName = getAccessor.Info.Name.Substring(4);
                    allPropertyMethods.RemoveAt(0);

                    var setAccessor = allPropertyMethods.FirstOrDefault(_ => _.Info.Name == "set_" + propertyName);
                    if (setAccessor != null)
                    {
                        allPropertyMethods.Remove(setAccessor);
                    }

                    yield return new InterfaceProperty(getAccessor, setAccessor);
                }
                else
                {
                    var setAccessor = allPropertyMethods[0];
                    var propertyName = setAccessor.Info.Name.Substring(4);
                    allPropertyMethods.RemoveAt(0);

                    var getAccessor = allPropertyMethods.FirstOrDefault(_ => _.Info.Name == "get_" + propertyName);
                    if (getAccessor != null)
                    {
                        allPropertyMethods.Remove(getAccessor);
                    }

                    yield return new InterfaceProperty(getAccessor, setAccessor);
                }
            }
        }

        //private IEnumerable<PropertyInfo> GetProperties() => TypeToWrap
        //    .GetFlattenProperties();

        private void ThrowIfRefOutParametersExistsForMethodsWithoutTheAllowConcurrentAccessAttribute()
        {
            foreach (var method in GetMethods()
                .Where(_ => _.Info.GetParameters().Any(p => p.IsOut || p.ParameterType.IsByRef)))
            {
                if (!MethodAllowsConcurrentAccess(method))
                    throw new InvalidOperationException($"Method '{method.Info.Name}' of '{TypeToWrapFullName}' contains ref or out parameters but not support concurrent access (MethodAllowsConcurrentAccess attribute)");
            }
        }

        private MethodInfo GetActualTypeMappedMethod(InterfaceMethod method)
        {
            var interfaceMapping = TypeOfObjectToWrap.GetInterfaceMap(method.InterfaceType);
            return interfaceMapping.TargetMethods[
                Array.IndexOf(interfaceMapping.InterfaceMethods, method.Info)];
        }

        //private bool PropertyGetAllowsConcurrentAccess(PropertyInfo propertyInfo)
        //{
        //    if (Attribute.GetCustomAttribute(TypeOfObjectToWrap.GetProperty(propertyInfo.Name), typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute concurrentAccessAttributeOfActualType)
        //    {
        //        return concurrentAccessAttributeOfActualType.PropertyGet;
        //    }

        //    if (Attribute.GetCustomAttribute(propertyInfo, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute concurrentAccessAttribute)
        //    {
        //        return concurrentAccessAttribute.PropertyGet;
        //    }

        //    return false;
        //}

        //private bool PropertySetAllowsConcurrentAccess(PropertyInfo propertyInfo)
        //{
        //    if (Attribute.GetCustomAttribute(TypeOfObjectToWrap.GetProperty(propertyInfo.Name), typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute concurrentAccessAttributeOfActualType)
        //    {
        //        return concurrentAccessAttributeOfActualType.PropertySet;
        //    }

        //    if (Attribute.GetCustomAttribute(propertyInfo, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute concurrentAccessAttribute)
        //    {
        //        return concurrentAccessAttribute.PropertySet;
        //    }

        //    return false;
        //}

        private bool MethodAllowsConcurrentAccess(InterfaceMethod method)
        {
            if (Attribute.GetCustomAttribute(GetActualTypeMappedMethod(method), typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute)
            {
                return true;
            }

            if (Attribute.GetCustomAttribute(method.Info, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute)
            {
                return true;
            }

            if (Attribute.GetCustomAttribute(method.InterfaceType, typeof(AllowConcurrentAccessAttribute)) is AllowConcurrentAccessAttribute)
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

        //private bool BlockCaller(PropertyInfo propertyInfo)
        //{
        //    if (Attribute.GetCustomAttribute(TypeOfObjectToWrap.GetProperty(propertyInfo.Name), typeof(BlockCallerAttribute)) is BlockCallerAttribute)
        //    {
        //        return true;
        //    }

        //    if (Attribute.GetCustomAttribute(propertyInfo, typeof(BlockCallerAttribute)) is BlockCallerAttribute)
        //    {
        //        return true;
        //    }

        //    return _blockCallerByDefault;
        //}

        private bool BlockCaller(InterfaceMethod method)
        {
            if (Attribute.GetCustomAttribute(GetActualTypeMappedMethod(method), typeof(BlockCallerAttribute)) is BlockCallerAttribute)
            {
                return true;
            }

            if (Attribute.GetCustomAttribute(method.Info, typeof(BlockCallerAttribute)) is BlockCallerAttribute)
            {
                return true;
            }

            if (Attribute.GetCustomAttribute(method.InterfaceType, typeof(BlockCallerAttribute)) is BlockCallerAttribute)
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

        //private bool KeepAsyncContext(PropertyInfo propertyInfo)
        //{
        //    if (Attribute.GetCustomAttribute(TypeOfObjectToWrap.GetProperty(propertyInfo.Name), typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttributeOfActualType)
        //    {
        //        return keepAsyncContextAttributeOfActualType.KeepContext;
        //    }

        //    if (Attribute.GetCustomAttribute(propertyInfo, typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttribute)
        //    {
        //        return keepAsyncContextAttribute.KeepContext;
        //    }

        //    return _keepAsyncContextByDefault;
        //}

        private bool KeepAsyncContext(InterfaceMethod method)
        {
            if (Attribute.GetCustomAttribute(GetActualTypeMappedMethod(method), typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttributeOfActualType)
            {
                return keepAsyncContextAttributeOfActualType.KeepContext;
            }

            if (Attribute.GetCustomAttribute(method.Info, typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttribute)
            {
                return keepAsyncContextAttribute.KeepContext;
            }

            if (Attribute.GetCustomAttribute(method.InterfaceType, typeof(KeepAsyncContextAttribute)) is KeepAsyncContextAttribute keepAsyncContextAttributeOfInterfaceType)
            {
                return keepAsyncContextAttributeOfInterfaceType.KeepContext;
            }

            return _keepAsyncContextByDefault;
        }
    }
}