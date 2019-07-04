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
        }

        public Type TypeToWrap { get; }
        public bool SharedQueue { get; }

        public string TypeToWrapFullName => GetFullTypeName(TypeToWrap);

        public IEnumerable<MethodInfo> GetMethods() => TypeToWrap
            .GetMethods()
            .Where(_ => !_.Name.StartsWith("get_") && !_.Name.StartsWith("set_") && !_.Name.StartsWith("add_") && !_.Name.StartsWith("remove_"));

        public static string GetMethodName(MethodInfo methodInfo)
        {
            if (methodInfo.IsGenericMethod)
            {
                return $"{methodInfo.Name}<{string.Join(",", methodInfo.GetGenericArguments().Select(_=> CSharpTypeName(_)))}>";
            }

            return methodInfo.Name;
        }

        public static string GetFullTypeName(Type type)
        {
            if (type.DeclaringType != null)
            {
                return $"{type.Namespace}.{GetFullTypeName(type.DeclaringType)}.{type.Name}";
            }

            return $"{type.Namespace}.{type.Name}";
        }


        private static readonly Dictionary<Type, string> _shorthandMap = new Dictionary<Type, string>
        {
            { typeof(bool), "bool" },
            { typeof(byte), "byte" },
            { typeof(char), "char" },
            { typeof(decimal), "decimal" },
            { typeof(double), "double" },
            { typeof(float), "float" },
            { typeof(int), "int" },
            { typeof(long), "long" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(string), "string" },
            { typeof(uint), "uint" },
            { typeof(ulong), "ulong" },
            { typeof(ushort), "ushort" },
        };

        private static string CSharpTypeName(Type type, bool isOut = false)
        {
            var codeCompileUnit = new CodeTypeReference(type);
            var provider = new CSharpCodeProvider();
            return provider.GetTypeOutput(codeCompileUnit);


            //if (type.IsByRef)
            //{
            //    return string.Format("{0} {1}", isOut ? "out" : "ref", CSharpTypeName(type.GetElementType()));
            //}
            //if (type.IsGenericType)
            //{
            //    if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
            //    {
            //        return string.Format("{0}?", CSharpTypeName(Nullable.GetUnderlyingType(type)));
            //    }
            //    else
            //    {
            //        return string.Format("{0}<{1}>", GetFullTypeName(type).Split('`')[0],
            //            string.Join(", ", type.GenericTypeArguments.Select(a => a.Name).ToArray()));
            //    }
            //}
            //if (type.IsArray)
            //{
            //    return string.Format("{0}[]", CSharpTypeName(type.GetElementType()));
            //}

            //return _shorthandMap.ContainsKey(type) ? _shorthandMap[type] : GetFullTypeName(type);
        }

        private bool IsTaskWithArgumentType(Type type) => CSharpTypeName(type).StartsWith("Task<");

        private string GetTaskArgument(Type type) => CSharpTypeName(type.GenericTypeArguments[0]);

        private string GetParametersString(MethodInfo method, bool callable = false)
        {
            var sigBuilder = new StringBuilder();
            var firstParam = true;
            var secondParam = false;
            foreach (var param in method.GetParameters())
            {
                if (firstParam)
                {
                    firstParam = false;
                    if (method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                    {
                        if (callable)
                        {
                            secondParam = true;
                            continue;
                        }
                        sigBuilder.Append("this ");
                    }
                }
                else if (secondParam == true)
                    secondParam = false;
                else
                    sigBuilder.Append(", ");
                if (param.ParameterType.IsByRef)
                    sigBuilder.Append("ref ");
                else if (param.IsOut)
                    sigBuilder.Append("out ");
                if (!callable)
                {
                    sigBuilder.Append(CSharpTypeName(param.ParameterType));
                    sigBuilder.Append(' ');
                }
                sigBuilder.Append(param.Name);
            }

            return sigBuilder.ToString();
        }
    }

}
