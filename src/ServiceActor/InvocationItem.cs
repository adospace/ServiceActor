using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceActor
{
    public class InvocationItem
    {
        private readonly AutoResetEvent _autoResetEvent;
        private readonly AsyncAutoResetEvent _asyncAutoResetEvent;

        public InvocationItem(
            Action action,
            IServiceActorWrapper target,
            string typeOfObjectToWrap,
            bool keepContextForAsyncCalls = true,
            bool async = false,
            bool blockingCaller = true)
        {
            Action = action;
            Target = target;
            TypeOfObjectToWrap = typeOfObjectToWrap;
            KeepContextForAsyncCalls = keepContextForAsyncCalls;
            BlockingCaller = blockingCaller;

            if (async)
            {
                _asyncAutoResetEvent = new AsyncAutoResetEvent(false);
            }
            else
            {
                _autoResetEvent = new AutoResetEvent(false);
            }
        }

        public InvocationItem(
            Action action,
            bool keepContextForAsyncCalls = true,
            bool async = false,
            bool blockingCaller = true)
        {
            Action = action;
            KeepContextForAsyncCalls = keepContextForAsyncCalls;
            BlockingCaller = blockingCaller;

            if (async)
            {
                _asyncAutoResetEvent = new AsyncAutoResetEvent(false);
            }
            else
            {
                _autoResetEvent = new AutoResetEvent(false);
            }
        }

        internal void SignalExecuted()
        {
            _autoResetEvent?.Set();
            _asyncAutoResetEvent?.Set();
        }

        public Action Action { get; private set; }

        public IServiceActorWrapper Target { get; private set; }

        public string TypeOfObjectToWrap { get; private set; }

        public bool KeepContextForAsyncCalls { get; private set; }
        public bool BlockingCaller { get; }

        private readonly Queue<IPendingOperation> _pendingOperations = new Queue<IPendingOperation>();

        public void EnqueuePendingOperation(IPendingOperation pendingOperation)
        {
            if (!BlockingCaller)
            {
                throw new InvalidOperationException("Unable to register a pending operation for a method not marked with BlockCaller attribute");
            }

            _pendingOperations.Enqueue(pendingOperation);
        }

        public bool WaitForPendingOperationCompletion()
        {
            if (_pendingOperations.Count == 0)
            {
                return false;
            }

            bool completed = true;
            foreach (var pendingOperation in _pendingOperations)
            {
                if (!pendingOperation.WaitForCompletion())
                    completed = false;
            }

            return completed;
        }

        public T GetLastPendingOperationResult<T>()
        {
            var lastPendingOperationWithResult = _pendingOperations
                .OfType<IPendingOperationWithResult>()
                .LastOrDefault();

            if (lastPendingOperationWithResult == null)
            {
                var lastPendingOperation = _pendingOperations
                    .LastOrDefault();

                if (lastPendingOperation == null)
                {
                    return default(T);
                }

                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)true;
                }

                return default(T);
            }

            return (T)lastPendingOperationWithResult.GetResult();
        }

        public void WaitExecuted()
        {
            if (ActionQueue.CallTimeout == Timeout.Infinite)
            {
                _autoResetEvent.WaitOne();
            }
            else if (!_autoResetEvent.WaitOne(ActionQueue.CallTimeout))
            {
                throw new TimeoutException();
            }
        }

        public Task WaitExecutedAsync()
        {
            return _asyncAutoResetEvent.WaitAsync();
        }

        public override string ToString()
        {
            return $"{Target?.WrappedObject}({TypeOfObjectToWrap}) {Action.Method}";
        }

    }
}