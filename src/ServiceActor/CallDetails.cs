using System;
using System.Threading.Tasks;

namespace ServiceActor
{
    public class CallDetails
    {
        public ActionQueue ActionQueue { get; }
        public InvocationItem Invocation { get; }

        public IServiceActorWrapper Target => Invocation.Target;
        public object WrappedObject => Invocation.Target?.WrappedObject;
        public string TypeOfObjectToWrap => Invocation.TypeOfObjectToWrap;
        public bool BlockingCaller => Invocation.BlockingCaller;
        public Action Action => Invocation.Action;
        public Func<Task> ActionAsync => Invocation.ActionAsync;

        public CallDetails(
            ActionQueue actionQueue,
            InvocationItem invocation)
        {
            ActionQueue = actionQueue;
            Invocation = invocation;
        }

        public override string ToString()
        {
            return $"{Invocation}({ActionQueue.Name})";
        }
    }
}