using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceActor
{
    internal class WaitHandlerPendingOperation : IPendingOperationOnAction, IPendingOperation
    {
        private readonly EventWaitHandle _waitHandler;
        private readonly AsyncAutoResetEvent _waitHandlerAsync = new AsyncAutoResetEvent();
        private readonly int _timeoutMilliseconds;
        private readonly Action<bool> _actionAfterCompletion;

        public WaitHandlerPendingOperation(
            EventWaitHandle waitHandler = null,
            int timeoutMilliseconds = 0,
            Action<bool> actionAfterCompletion = null)
        {
            if (timeoutMilliseconds < 0)
            {
                throw new ArgumentException(nameof(timeoutMilliseconds));
            }

            _waitHandler = waitHandler ?? new AutoResetEvent(false);
            _timeoutMilliseconds = timeoutMilliseconds;
            _actionAfterCompletion = actionAfterCompletion;
        }

        public void Complete()
        {
            _waitHandler.Set();
            _waitHandlerAsync.Set();
        }

        public bool WaitForCompletion()
        {
            var completed = _timeoutMilliseconds > 0 ? 
                    _waitHandler.WaitOne(_timeoutMilliseconds) :
                    _waitHandler.WaitOne();

            _actionAfterCompletion?.Invoke(completed);

            return completed;
        }

        public async Task<bool> WaitForCompletionAsync()
        {
            bool completed = false;
            if (_timeoutMilliseconds > 0)
            {
                using (var timeoutTokenSource = new CancellationTokenSource(_timeoutMilliseconds))
                {
                    try
                    {
                        await _waitHandlerAsync.WaitAsync(timeoutTokenSource.Token);
                        completed = true;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            else
            {
                await _waitHandlerAsync.WaitAsync();
                completed = true;
            }

            _actionAfterCompletion?.Invoke(completed);

            return completed;
        }
    }

    internal class WaitHandlePendingOperation<T> : WaitHandlerPendingOperation, IPendingOperationWithResult
    {
        private readonly Func<T> _getResultFunction;

        public WaitHandlePendingOperation(
            Func<T> getResultFunction,
            EventWaitHandle waitHandler = null,
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
