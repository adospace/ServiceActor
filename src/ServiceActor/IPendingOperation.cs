using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public interface IPendingOperation
    {
        void WaitForCompletion();

    }

    public interface IPendingOperationWithResult : IPendingOperation
    {
        object GetResult();
    }
}
