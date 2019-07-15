using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public interface IPendingOperation
    {
        bool WaitForCompletion();

    }

    public interface IPendingOperationWithResult : IPendingOperation
    {
        object GetResult();
    }
}
