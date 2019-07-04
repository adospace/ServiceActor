using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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

        private static string GenerateReferenceCodeForTypeString(string typeString, bool isOut = false)
        {
            var generatedCodeTokens = typeString.Split('`');

            if (generatedCodeTokens.Length == 1)
            {
                if (generatedCodeTokens[0].EndsWith("&"))
                    return (isOut ? "out " : "ref ") + generatedCodeTokens[0].TrimEnd('&');

                return generatedCodeTokens[0];
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

        //private static string GenerateReferenceCodeForType(Type type)
        //{
        //    var codeTypeReference = new CodeTypeReference(type);
        //    var provider = new CSharpCodeProvider();
        //    var codeGenerated = provider.GetTypeOutput(codeTypeReference);

        //    var generatedCodeGenricTagIndex = codeGenerated.IndexOf('<');
        //    if (generatedCodeGenricTagIndex > -1)
        //        return codeGenerated.Substring(0, generatedCodeGenricTagIndex);

        //    return codeGenerated;
        //}
    }
}
