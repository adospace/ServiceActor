using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ServiceActor
{
    internal interface IPendingOperationOnAction
    {
        bool WaitForCompletion();

        Task<bool> WaitForCompletionAsync();
    }

    internal interface IPendingOperationWithResult : IPendingOperationOnAction
    {
        object GetResult();
    }
}
