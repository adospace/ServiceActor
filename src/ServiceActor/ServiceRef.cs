using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace ServiceActor
{
    public static class ServiceRef
    {
        public class ScriptGlobals
        {
            public object ObjectToWrap { get; set; }

            public ActionQueue ActionQueueToShare { get; set; }
        }

        private static readonly ConcurrentDictionary<object, ConcurrentDictionary<Type, object>> _wrappersCache = new ConcurrentDictionary<object, ConcurrentDictionary<Type, object>>();

        private static readonly ConcurrentDictionary<object, ActionQueue> _queuesCache = new ConcurrentDictionary<object, ActionQueue>();

        public static T Create<T>(T objectToWrap, object aggregateKey = null) where T : class
        {
            if (objectToWrap == null)
            {
                throw new ArgumentNullException(nameof(objectToWrap));
            }

            //NOTE: Do not use AddOrUpdate() to avoid _wrapperCache lock while generating wrapper

            ActionQueue actionQueue = null;
            object wrapper;
            if (_wrappersCache.TryGetValue(objectToWrap, out var wrapperTypes))
            {
                if (wrapperTypes.TryGetValue(typeof(T), out wrapper))
                    return (T)wrapper;

                var firstWrapper = wrapperTypes.First().Value;

                actionQueue = (ActionQueue)firstWrapper.GetType().GetField("_actionQueue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(firstWrapper);
            }

            actionQueue = actionQueue ?? GetActionQueueFor(typeof(T), aggregateKey);

            var asyncActorCode = new ServiceActorWrapperTemplate(typeof(T)).TransformText();
            //Console.WriteLine(asyncActorCode);
            wrapper = CSharpScript.EvaluateAsync<T>(
                asyncActorCode,
                options: ScriptOptions.Default.AddReferences(
                    Assembly.GetExecutingAssembly(),
                    typeof(T).Assembly,
                    typeof(Nito.AsyncEx.AsyncAutoResetEvent).Assembly),
                globals: new ScriptGlobals { ObjectToWrap = objectToWrap, ActionQueueToShare = actionQueue }
                ).Result;

            wrapperTypes = _wrappersCache.GetOrAdd(objectToWrap, new ConcurrentDictionary<Type, object>());

            wrapperTypes.TryAdd(typeof(T), wrapper);

            return (T)wrapper;
        }

        private static ActionQueue GetActionQueueFor(Type typeToWrap, object aggregateKey)
        {
            if (aggregateKey != null)
            {
                if (_queuesCache.TryGetValue(aggregateKey, out var actionQueue))
                    return actionQueue;
            }

            if (Attribute.GetCustomAttribute(typeToWrap, typeof(ServiceDomainAttribute)) is ServiceDomainAttribute serviceDomain)
            {
                return _queuesCache.AddOrUpdate(serviceDomain.DomainKey, new ActionQueue(), (key, oldValue) => oldValue);
            }

            return new ActionQueue();
        }
    }
}