using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServiceActor
{
    public class WaitHandlerPendingOperation : IPendingOperation
    {
        private readonly WaitHandle _waitHandler;

        public WaitHandlerPendingOperation(WaitHandle waitHandler)
        {
            _waitHandler = waitHandler ?? throw new ArgumentNullException(nameof(waitHandler));
        }

        public void WaitForCompletion() => _waitHandler.WaitOne();
    }

    public class WaitHandlerPendingOperation<T> : WaitHandlerPendingOperation, IPendingOperation<T>
    {
        private readonly Func<T> _getResultFunction;

        public WaitHandlerPendingOperation(WaitHandle waitHandler, Func<T> getResultFunction)
            :base(waitHandler)
        {
            _getResultFunction = getResultFunction ?? throw new ArgumentNullException(nameof(getResultFunction));
        }

        public Func<T> GetResultFunction() => _getResultFunction;
    }
}
