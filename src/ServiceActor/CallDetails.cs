using System;

namespace ServiceActor
{
    public class CallDetails
    {
        public ActionQueue ActionQueue { get; }
        public IServiceActorWrapper Target { get; }
        public object WrappedObject { get; }
        public string TypeOfObjectToWrap { get; }
        public Action Action { get; }

        public CallDetails(ActionQueue actionQueue, IServiceActorWrapper target, object wrappedObject, string typeOfObjectToWrap, Action action)
        {
            ActionQueue = actionQueue;
            Target = target;
            WrappedObject = wrappedObject;
            TypeOfObjectToWrap = typeOfObjectToWrap;
            Action = action;
        }

        public override string ToString()
        {
            return $"{Target?.WrappedObject}({TypeOfObjectToWrap}) {Action.Method}";
        }
    }
}