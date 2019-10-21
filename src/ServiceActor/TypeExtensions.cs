using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ServiceActor
{
    internal static class TypeExtensions
    {
        public static PropertyInfo GetPropertyEx(this Type type, PropertyInfo propertyInfo)
        {
            return type.GetProperties()
                .First(_ => _.Name == propertyInfo.Name && _.PropertyType == propertyInfo.PropertyType);
        }
    }
}
