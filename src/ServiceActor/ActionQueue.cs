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
        //private readonly ConcurrentDictionary<object, object> _targets = new ConcurrentDictionary<object, object>();
        private InvocationItem _executingInvocationItem;

        private class InvocationItem
        {
            public Action Action { get; set; }

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
                        //System.Diagnostics.Debug.WriteLine($"-----Executing {invocation.Action.Target} {invocation.Action.Method}...");
                        _executingInvocationItem = invocation;
                        AsyncContext.Run(invocation.Action);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }

                    //System.Diagnostics.Debug.WriteLine($"-----Executed {invocation.Action.Target} {invocation.Action.Method}");
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

            _actionQueue.Post(new InvocationItem() { Action = action, KeepContextForAsyncCalls = keepContextForAsyncCalls });
        }
    }
}