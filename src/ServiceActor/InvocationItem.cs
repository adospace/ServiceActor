using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceActor
{
    public class InvocationItem : IDisposable
    {
        private readonly AutoResetEvent _executedEvent;
        private readonly AsyncAutoResetEvent _asyncExecutedEvent;

        public InvocationItem(
            Action action,
            IServiceActorWrapper target,
            string typeOfObjectToWrap,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
        {
            Action = action;
            Target = target;
            TypeOfObjectToWrap = typeOfObjectToWrap;
            KeepContextForAsyncCalls = keepContextForAsyncCalls;
            Async = false;
            BlockingCaller = blockingCaller;

            _executedEvent = new AutoResetEvent(false);
        }

        public InvocationItem(
            Action action,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
        {
            Action = action;
            KeepContextForAsyncCalls = keepContextForAsyncCalls;
            Async = false;
            BlockingCaller = blockingCaller;

            _executedEvent = new AutoResetEvent(false);
        }

        public InvocationItem(
            Func<Task> action,
            IServiceActorWrapper target,
            string typeOfObjectToWrap,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
        {
            ActionAsync = action;
            Target = target;
            TypeOfObjectToWrap = typeOfObjectToWrap;
            KeepContextForAsyncCalls = keepContextForAsyncCalls;
            Async = true;
            BlockingCaller = blockingCaller;

            _asyncExecutedEvent = new AsyncAutoResetEvent(false);
        }

        public InvocationItem(
            Func<Task> action,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
        {
            ActionAsync = action;
            KeepContextForAsyncCalls = keepContextForAsyncCalls;
            Async = true;
            BlockingCaller = blockingCaller;

            _asyncExecutedEvent = new AsyncAutoResetEvent(false);
        }

        internal void SignalExecuted()
        {
            _executedEvent?.Set();
            _asyncExecutedEvent?.Set();
        }

        public Action Action { get; private set; }

        public Func<Task> ActionAsync { get; private set; }

        public IServiceActorWrapper Target { get; private set; }

        public string TypeOfObjectToWrap { get; private set; }

        public bool KeepContextForAsyncCalls { get; private set; }
        public bool Async { get; }
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
                    return default;
                }

                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)true;
                }

                return default;
            }

            return (T)lastPendingOperationWithResult.GetResult();
        }

        public void WaitExecuted()
        {
            if (ActionQueue.CallTimeout == Timeout.Infinite)
            {
                _executedEvent.WaitOne();
            }
            else if (!_executedEvent.WaitOne(ActionQueue.CallTimeout))
            {
                throw new TimeoutException();
            }
        }

        public Task WaitExecutedAsync()
        {
            return _asyncExecutedEvent.WaitAsync();
        }

        public override string ToString()
        {
            return $"{Target?.WrappedObject}({TypeOfObjectToWrap}) {Action?.Method ?? ActionAsync?.Method}";
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _executedEvent?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~InvocationItem()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}