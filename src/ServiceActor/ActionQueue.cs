using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace ServiceActor
{
    public class ActionQueue
    {
        private readonly ActionBlock<InvocationItem> _actionQueue;
        private int? _executingActionThreadId;
        private InvocationItem _executingInvocationItem;

        private class InvocationItem
        {
            public Action Action { get; set; }

            public IServiceActorWrapper Target { get; set; }

            public string TypeOfObjectToWrap { get; set; }

            public bool KeepContextForAsyncCalls { get; set; } = true;
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
                    _executingActionThreadId = null;
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

        public void Enqueue(IServiceActorWrapper target, string typeOfObjectToWrap, Action action, bool keepContextForAsyncCalls = true)
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
                //if the calling thread is the same as the first executing action then just pass thru
                action();
                return;
            }

            _actionQueue.Post(new InvocationItem()
            {
                Target = target,
                TypeOfObjectToWrap = typeOfObjectToWrap,
                Action = action,
                KeepContextForAsyncCalls = keepContextForAsyncCalls });
        }

        public void Enqueue(Action action, bool keepContextForAsyncCalls = true)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Thread.CurrentThread.ManagedThreadId == _executingActionThreadId)
            {
                //if the calling thread is the same as the first executing action then just pass thru
                action();
                return;
            }

            _actionQueue.Post(new InvocationItem()
            {
                Action = action,
                KeepContextForAsyncCalls = keepContextForAsyncCalls
            });
        }


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
    }
}