using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ServiceActor
{
    public class ActionQueue
    {
        private readonly ActionBlock<InvocationItem> _actionQueue;
        private int? _executingActionThreadId;
        private InvocationItem _executingInvocationItem;

        public class InvocationItem
        {
            private readonly AutoResetEvent _autoResetEvent;
            private readonly AsyncAutoResetEvent _asyncAutoResetEvent;

            public InvocationItem(
                Action action,
                IServiceActorWrapper target,
                string typeOfObjectToWrap,
                bool keepContextForAsyncCalls = true,
                bool async = false)
            {
                Action = action;
                Target = target;
                TypeOfObjectToWrap = typeOfObjectToWrap;
                KeepContextForAsyncCalls = keepContextForAsyncCalls;
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
                bool async = false)
            {
                Action = action;
                KeepContextForAsyncCalls = keepContextForAsyncCalls;
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

            private readonly Queue<IPendingOperation> _pendingOperations = new Queue<IPendingOperation>();

            public void EnqueuePendingOperation(IPendingOperation pendingOperation) => 
                _pendingOperations.Enqueue(pendingOperation);

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
                //_autoResetEvent.WaitOne();
                if (!_autoResetEvent.WaitOne(30000))
                {
                    throw new InvalidOperationException();
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

        public ActionQueue()
        {
            _actionQueue = new ActionBlock<InvocationItem>(invocation =>
            {
                if (invocation.KeepContextForAsyncCalls)
                {
                    //Console.WriteLine($"Current Thread ID Before action.Invoke: {Thread.CurrentThread.ManagedThreadId}");
                    _executingActionThreadId = Thread.CurrentThread.ManagedThreadId;

                    try
                    {
                        //System.Diagnostics.Debug.WriteLine($"-----Executing {invocation.Target?.WrappedObject}({invocation.TypeOfObjectToWrap}) {invocation.Action.Method}...");
                        if (_actionCallMonitor != null)
                        {
                            var callDetails = new CallDetails(this, invocation.Target, invocation.Target?.WrappedObject, invocation.TypeOfObjectToWrap, invocation.Action);
                            _actionCallMonitor?.EnterMethod(callDetails);
                        }
                        _executingInvocationItem = invocation;
                        AsyncContext.Run(invocation.Action);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }


                    //System.Diagnostics.Debug.WriteLine($"-----Executed {invocation.Target?.WrappedObject}({invocation.TypeOfObjectToWrap}) {invocation.Action.Method}");
                    if (_actionCallMonitor != null)
                    {
                        var callDetails = new CallDetails(this, invocation.Target, invocation.Target?.WrappedObject, invocation.TypeOfObjectToWrap, invocation.Action);
                        _actionCallMonitor?.ExitMethod(callDetails);
                    }

                    var executingInvocationItem = _executingInvocationItem;
                    _executingInvocationItem = null;
                    _executingActionThreadId = null;

                    executingInvocationItem.SignalExecuted();
                    //action.Invoke();
                    //Console.WriteLine($"Current Thread ID After action.Invoke: {Thread.CurrentThread.ManagedThreadId}");
                }
                else
                {
                    invocation.Action();
                }
            });
        }

        public void Stop()
        {
            _actionQueue.Complete();
        }

        public InvocationItem Enqueue(IServiceActorWrapper target, string typeOfObjectToWrap, Action action, bool keepContextForAsyncCalls = true, bool asyncEvent = false)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (typeOfObjectToWrap == null)
            {
                throw new ArgumentNullException(nameof(typeOfObjectToWrap));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Thread.CurrentThread.ManagedThreadId == _executingActionThreadId)
            {
                //if the calling thread is the same as the executing action thread then just pass thru
                action();
                return null;
            }

            var invocationItem = new InvocationItem(
                action, 
                target,
                typeOfObjectToWrap,
                keepContextForAsyncCalls,
                asyncEvent);

            _actionQueue.Post(invocationItem);

            return invocationItem;
        }

        public InvocationItem Enqueue(Action action, bool keepContextForAsyncCalls = true, bool asyncEvent = false)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Thread.CurrentThread.ManagedThreadId == _executingActionThreadId)
            {
                //if the calling thread is the same as the first executing action then just pass thru
                action();
                return _executingInvocationItem;
            }

            var invocationItem = new InvocationItem(
                action,
                keepContextForAsyncCalls,
                asyncEvent
            );

            _actionQueue.Post(invocationItem);

            return invocationItem;
        }

        #region Calls Monitor
        private static IActionCallMonitor _actionCallMonitor;
        public static void BeginMonitor(IActionCallMonitor actionCallMonitor)
        {
            _actionCallMonitor = actionCallMonitor ?? throw new ArgumentNullException(nameof(actionCallMonitor));
        }

        public static void ExitMonitor(IActionCallMonitor actionCallMonitor)
        {
            if (actionCallMonitor == null)
            {
                throw new ArgumentNullException(nameof(actionCallMonitor));
            }

            if (actionCallMonitor != _actionCallMonitor)
            {
                throw new InvalidOperationException();
            }

            _actionCallMonitor = null;
        }
        #endregion

        #region Pending Operations
        public void RegisterPendingOperation(IPendingOperation pendingOperation)
        {
            if (pendingOperation == null)
            {
                throw new ArgumentNullException(nameof(pendingOperation));
            }

            _executingInvocationItem.EnqueuePendingOperation(pendingOperation);
        }

        public void RegisterPendingOperation(WaitHandle waitHandle, int timeoutMilliseconds = 0, Action<bool> actionOnCompletion = null)
        {
            RegisterPendingOperation(new WaitHandlerPendingOperation(waitHandle, timeoutMilliseconds, actionOnCompletion));
        }

        public void RegisterPendingOperation<T>(WaitHandle waitHandle, Func<T> getResultFunction, int timeoutMilliseconds = 0, Action<bool> actionOnCompletion = null)
        {
            RegisterPendingOperation(new WaitHandlePendingOperation<T>(waitHandle, getResultFunction, timeoutMilliseconds, actionOnCompletion));
        }
        #endregion
    }
}