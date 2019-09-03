using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public interface IServiceActorWrapper
    {
        object WrappedObject { get; }
        ActionQueue ActionQueue { get; }
    }
}
