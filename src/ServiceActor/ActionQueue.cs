using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ServiceActor
{
    public partial class ActionQueue
    {
        private readonly ActionBlock<InvocationItem> _actionQueue;
        private int? _executingActionThreadId;
        private InvocationItem _executingInvocationItem;

        public InvocationItem ExecutingInvocationItem => _executingInvocationItem;

        private static int _callTimeout = Timeout.Infinite;
        public static int CallTimeout
        {
            get => _callTimeout;
            set
            {
                if (value != Timeout.Infinite && value < 0)
                {
                    throw new ArgumentException("Invalid value");
                }

                _callTimeout = value == 0 ? Timeout.Infinite : value;
            }
        }

        public string Name { get; }

        public ActionQueue(string name)
        {
            _actionQueue = new ActionBlock<InvocationItem>(async invocation =>
            {
                _executingActionThreadId = Thread.CurrentThread.ManagedThreadId;

                var callDetails = new CallDetails(
                    this,
                    invocation);

                _actionCallMonitor?.EnterMethod(callDetails);
                _executingInvocationItem = invocation;

                try
                {
                    if (invocation.KeepContextForAsyncCalls)
                    {
                        if (invocation.Async)
                        {
                            var funcTask = invocation.ActionAsync;
                            AsyncContext.Run(funcTask);
                        }
                        else
                        {
                            AsyncContext.Run(invocation.Action);
                        }
                    }
                    else
                    {
                        if (invocation.Async)
                        {
                            await invocation.ActionAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            invocation.Action();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _actionCallMonitor?.UnhandledException(callDetails, ex);
                }

                _actionCallMonitor?.ExitMethod(callDetails);

                var executingInvocationItem = _executingInvocationItem;
                _executingInvocationItem = null;
                _executingActionThreadId = null;

                executingInvocationItem.SignalExecuted();
            });
            Name = name;
        }

        public void Stop()
        {
            _actionQueue.Complete();
        }

        public InvocationItem Enqueue(IServiceActorWrapper target,
            string typeOfObjectToWrap,
            Action action,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
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
                return _executingInvocationItem;
            }

            var invocationItem = new InvocationItem(
                action,
                target,
                typeOfObjectToWrap,
                keepContextForAsyncCalls,
                blockingCaller);

            _actionQueue.Post(invocationItem);

            return invocationItem;
        }

        public InvocationItem Enqueue(Action action,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
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
                blockingCaller
            );

            _actionQueue.Post(invocationItem);

            return invocationItem;
        }

        public InvocationItem EnqueueAsync(IServiceActorWrapper target,
            string typeOfObjectToWrap,
            Func<Task> action,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
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
                blockingCaller);

            _actionQueue.Post(invocationItem);

            return invocationItem;
        }

        public InvocationItem EnqueueAsync(Func<Task> action,
            bool keepContextForAsyncCalls = true,
            bool blockingCaller = true)
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
                blockingCaller
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
        internal IPendingOperation RegisterPendingOperation(IPendingOperationOnAction pendingOperation)
        {
            if (pendingOperation == null)
            {
                throw new ArgumentNullException(nameof(pendingOperation));
            }

            if (_executingInvocationItem == null)
            {
                throw new InvalidOperationException("Seems that RegisterPendingOperations has been called outside of a service actor wrapper or from a method not wrapped. Also ensure that calling method/property is decorated with a AllowConcurrentAccess attribute.");
            }

            _executingInvocationItem.EnqueuePendingOperation(pendingOperation);

            return (IPendingOperation)pendingOperation;
        }

        public IPendingOperation RegisterPendingOperation(int timeoutMilliseconds = 0, Action<bool> actionOnCompletion = null)
        {
            return RegisterPendingOperation(new WaitHandlerPendingOperation(null, timeoutMilliseconds, actionOnCompletion));
        }

        public IPendingOperation RegisterPendingOperation<T>(Func<T> getResultFunction, int timeoutMilliseconds = 0, Action<bool> actionOnCompletion = null)
        {
            return RegisterPendingOperation(new WaitHandlePendingOperation<T>(getResultFunction, null, timeoutMilliseconds, actionOnCompletion));
        }
        #endregion
    }
}