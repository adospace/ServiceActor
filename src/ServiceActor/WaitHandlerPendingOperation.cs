using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServiceActor
{
    public class WaitHandlerPendingOperation : IPendingOperation
    {
        private readonly WaitHandle _waitHandler;
        private readonly int _timeoutMilliseconds;

        public WaitHandlerPendingOperation(WaitHandle waitHandler, int timeoutMilliseconds = 0)
        {
            _waitHandler = waitHandler ?? throw new ArgumentNullException(nameof(waitHandler));
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        public void WaitForCompletion()
        {
            if (_timeoutMilliseconds > 0)
            {
                _waitHandler.WaitOne(_timeoutMilliseconds);
                return;
            }

            _waitHandler.WaitOne();
        }
    }

    public class WaitHandlerPendingOperation<T> : WaitHandlerPendingOperation, IPendingOperation<T>
    {
        private readonly Func<T> _getResultFunction;

        public WaitHandlerPendingOperation(WaitHandle waitHandler, Func<T> getResultFunction, int timeoutMilliseconds = 0)
            :base(waitHandler, timeoutMilliseconds)
        {
            _getResultFunction = getResultFunction ?? throw new ArgumentNullException(nameof(getResultFunction));
        }

        public Func<T> GetResultFunction() => _getResultFunction;
    }
}
