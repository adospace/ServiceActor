using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ServiceActor
{
    public class InterfaceMethod
    {
        public InterfaceMethod(Type @interface, MethodInfo methodInfo)
        {
            InterfaceType = @interface;
            Info = methodInfo;
        }

        public Type InterfaceType { get; }
        public MethodInfo Info { get; }

        public override bool Equals(object obj)
        {
            var method = obj as InterfaceMethod;
            return method != null &&
                   EqualityComparer<Type>.Default.Equals(InterfaceType, method.InterfaceType) &&
                   EqualityComparer<MethodInfo>.Default.Equals(Info, method.Info);
        }

        public override int GetHashCode()
        {
            var hashCode = -2021168548;
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(InterfaceType);
            hashCode = hashCode * -1521134295 + EqualityComparer<MethodInfo>.Default.GetHashCode(Info);
            return hashCode;
        }
    }
}
