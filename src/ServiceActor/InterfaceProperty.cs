using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public class InterfaceProperty
    {
        public InterfaceProperty(InterfaceMethod getAccessor, InterfaceMethod setAccessor)
        {
            if (getAccessor == null && setAccessor == null)
                throw new ArgumentException();

            GetAccessor = getAccessor;
            SetAccessor = setAccessor;
        }

        public InterfaceMethod GetAccessor { get; }
        public InterfaceMethod SetAccessor { get; }
        public Type InterfaceType => GetAccessor?.InterfaceType ??
            SetAccessor?.InterfaceType;

        public string Name => GetAccessor?.Info.Name.Substring(4) ?? 
            SetAccessor?.Info.Name.Substring(4);

        public Type PropertyType => GetAccessor?.Info.ReturnType ??
            SetAccessor?.Info.ReturnType;

        public bool CanRead => GetAccessor != null;
        public bool CanWrite => SetAccessor != null;
    }
}
