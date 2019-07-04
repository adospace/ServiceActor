using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace ServiceActor
{
    public class ActionQueue
    {
        private readonly ActionBlock<Action> _actionQueue;
        private int? _executingActionThreadId;

        public ActionQueue()
        {
            _actionQueue = new ActionBlock<Action>(action =>
            {
                //Console.WriteLine($"Current Thread ID Before action.Invoke: {Thread.CurrentThread.ManagedThreadId}");
                _executingActionThreadId = Thread.CurrentThread.ManagedThreadId;
                AsyncContext.Run(action);
                _executingActionThreadId = null;
                //action.Invoke();
                //Console.WriteLine($"Current Thread ID After action.Invoke: {Thread.CurrentThread.ManagedThreadId}");
            });
        }

        public void Stop()
        {
            _actionQueue.Complete();
        }

        public void Enqueue(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Thread.CurrentThread.ManagedThreadId == _executingActionThreadId)
            {
                //if the calling thread is the same as the first executing action just pass thru
                action();
                return;
            }

            _actionQueue.Post(action);
        }
    }
}