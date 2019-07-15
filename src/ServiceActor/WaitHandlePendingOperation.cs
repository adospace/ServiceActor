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
        private readonly Action<bool> _actionAfterCompletion;

        public WaitHandlerPendingOperation(
            WaitHandle waitHandler, 
            int timeoutMilliseconds = 0,
            Action<bool> actionAfterCompletion = null)
        {
            _waitHandler = waitHandler ?? throw new ArgumentNullException(nameof(waitHandler));
            _timeoutMilliseconds = timeoutMilliseconds;
            _actionAfterCompletion = actionAfterCompletion;
        }

        public void WaitForCompletion()
        {
            var completed = _timeoutMilliseconds > 0 ? 
                    _waitHandler.WaitOne(_timeoutMilliseconds) :
                    _waitHandler.WaitOne();

            _actionAfterCompletion?.Invoke(completed);
        }
    }

    public class WaitHandlePendingOperation<T> : WaitHandlerPendingOperation, IPendingOperationWithResult
    {
        private readonly Func<T> _getResultFunction;

        public WaitHandlePendingOperation(
            WaitHandle waitHandler, 
            Func<T> getResultFunction, 
            int timeoutMilliseconds = 0,
            Action<bool> actionAfterCompletion = null)
            :base(waitHandler, timeoutMilliseconds, actionAfterCompletion)
        {
            _getResultFunction = getResultFunction ?? throw new ArgumentNullException(nameof(getResultFunction));
        }

        public object GetResult()
        {
            return _getResultFunction.Invoke();
        }
    }
}
