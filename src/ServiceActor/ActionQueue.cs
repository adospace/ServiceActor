using Nito.AsyncEx;
using System;
using System.Threading.Tasks.Dataflow;

namespace ServiceActor
{
    public class ActionQueue
    {
        private readonly ActionBlock<Action> _actionQueue;

        public ActionQueue()
        {
            _actionQueue = new ActionBlock<Action>(action =>
            {
                //Console.WriteLine($"Current Thread ID Before action.Invoke: {Thread.CurrentThread.ManagedThreadId}");
                AsyncContext.Run(action);
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

            _actionQueue.Post(action);
        }
    }

}