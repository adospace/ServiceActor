using System;
using System.Threading.Tasks;

namespace ServiceActor
{
    public class CallDetails
    {
        public ActionQueue ActionQueue { get; }
        public IServiceActorWrapper Target { get; }
        public object WrappedObject { get; }
        public string TypeOfObjectToWrap { get; }
        public Action Action { get; }
        public Func<Task> ActionAsync { get; }

        public CallDetails(ActionQueue actionQueue, IServiceActorWrapper target, object wrappedObject, string typeOfObjectToWrap, Action action, Func<Task> actionAsync)
        {
            ActionQueue = actionQueue;
            Target = target;
            WrappedObject = wrappedObject;
            TypeOfObjectToWrap = typeOfObjectToWrap;
            Action = action;
            ActionAsync = actionAsync;
        }

        public override string ToString()
        {
            return $"{Target?.WrappedObject}({TypeOfObjectToWrap}) {Action?.Method ?? ActionAsync?.Method}";
        }
    }
}