using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServiceActor
{
    public static class CodeGenerationExtensions
    {
        public static string GetTypeReferenceCode(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return GenerateReferenceCodeForTypeString(type.ToString().Replace('+', '.'));
        }

        public static string GetTypeReferenceCode(this ParameterInfo parameterInfo)
        {
            if (parameterInfo == null)
            {
                throw new ArgumentNullException(nameof(parameterInfo));
            }

            return GenerateReferenceCodeForTypeString(parameterInfo.ParameterType.ToString().Replace('+', '.'), parameterInfo.IsOut);
        }

        public static string GetMethodDeclarationCode(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            if (!methodInfo.IsGenericMethod)
                return $"{methodInfo.ReturnType.GetTypeReferenceCode()} {methodInfo.Name}({string.Join(", ", methodInfo.GetParameters().Select(_ => _.GetTypeReferenceCode() + " " + _.Name))})";

            return $"{methodInfo.ReturnType.GetTypeReferenceCode()} {methodInfo.Name}<{string.Join(", ", methodInfo.GetGenericArguments().Select(_ => _.GetTypeReferenceCode()))}>({string.Join(", ", methodInfo.GetParameters().Select(_ => _.GetTypeReferenceCode() + " " + _.Name))}) {GetGenericParameterConstraintsDeclarationCode(methodInfo)}".TrimEnd();
        }

        private static string GetGenericParameterConstraintsDeclarationCode(MethodInfo methodInfo)
        {
            return string.Join(" ", 
                methodInfo
                    .GetGenericArguments()
                    .Select(_ => 
                        _.GetGenericParameterConstraints().Any() ? 
                        ("where " + _.Name + ": " + string.Join(", ", _.GetGenericParameterConstraints().Select(c => c.GetTypeReferenceCode()))) : 
                        string.Empty));
        }

        public static string GetMethodInvocationCode(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            if (!methodInfo.IsGenericMethod)
                return $"{methodInfo.Name}({string.Join(", ", methodInfo.GetParameters().Select(_ => _.Name))})";

            return $"{methodInfo.Name}<{string.Join(", ", methodInfo.GetGenericArguments().Select(_ => _.GetTypeReferenceCode()))}>({string.Join(", ", methodInfo.GetParameters().Select(_ => _.Name))})";
        }

        public static IEnumerable<InterfaceMethod> GetFlattenMethods(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.GetMethods()
                .Select(_ => new InterfaceMethod(type, _))
                    .Concat(type.GetInterfaces().SelectMany(_ => _.GetFlattenMethods()))
                    .Distinct();
        }

        public static IEnumerable<PropertyInfo> GetFlattenProperties(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.GetProperties().Concat(type.GetInterfaces().SelectMany(_ => _.GetFlattenProperties())).Distinct();
        }

        private static string GenerateReferenceCodeForTypeString(string typeString, bool isOut = false)
        {
            var generatedCodeTokens = typeString.Split('`');

            if (generatedCodeTokens.Length == 1)
            {
                if (generatedCodeTokens[0].EndsWith("&"))
                    return (isOut ? "out " : "ref ") + generatedCodeTokens[0].TrimEnd('&');

                return generatedCodeTokens[0] == "System.Void" ? "void" : generatedCodeTokens[0];
            }

            var generatedCodeGenricTagStartIndex = typeString.IndexOf('[');
            var generatedCodeGenricTagEndIndex = typeString.LastIndexOf(']');
            if (generatedCodeGenricTagStartIndex > -1 && generatedCodeGenricTagEndIndex > -1)
            {
                var genericTypeDefinitionArguments = typeString.Substring(generatedCodeGenricTagStartIndex + 1, generatedCodeGenricTagEndIndex - generatedCodeGenricTagStartIndex - 1);
                var genericTypeDefinitionArgumentsTokens = genericTypeDefinitionArguments.Split(',');
                var refParameter = typeString.EndsWith("&");
                return $"{(refParameter ? (isOut ? "out " : "ref ") : string.Empty)}{generatedCodeTokens[0]}<{string.Join(", ", genericTypeDefinitionArgumentsTokens.Select(_ => GenerateReferenceCodeForTypeString(_)))}>";
            }

            throw new NotSupportedException();
        }

        public static bool Implements(this Type actualType, Type interfaceType) => interfaceType.IsAssignableFrom(actualType) ||
                actualType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);

    }
}