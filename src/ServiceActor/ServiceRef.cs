using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Concurrent;
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

        private static readonly ConcurrentDictionary<object, object> _wrappersCache = new ConcurrentDictionary<object, object>();

        private static readonly ConcurrentDictionary<object, ActionQueue> _queuesCache = new ConcurrentDictionary<object, ActionQueue>();

        public static T Create<T>(T objectToWrap) where T : class
        {
            if (objectToWrap == null)
            {
                throw new ArgumentNullException(nameof(objectToWrap));
            }

            //NOTE: Do not user AddOrUpdate() to avoid _wrapperCache lock while generating wrapper
            if (_wrappersCache.TryGetValue(objectToWrap, out var wrapper))
                return (T)wrapper;

            var asyncActorCode = new ServiceActorWrapperTemplate(typeof(T)).TransformText();
            //Console.WriteLine(asyncActorCode);
            wrapper = CSharpScript.EvaluateAsync<T>(
                asyncActorCode,
                options: ScriptOptions.Default.AddReferences(
                    Assembly.GetExecutingAssembly(),
                    typeof(T).Assembly,
                    typeof(Nito.AsyncEx.AsyncAutoResetEvent).Assembly),
                globals: new ScriptGlobals { ObjectToWrap = objectToWrap, ActionQueueToShare = GetActionQueueFor(typeof(T)) }
                ).Result;

            _wrappersCache.TryAdd(objectToWrap, wrapper);

            _wrappersCache.TryGetValue(objectToWrap, out wrapper);

            return (T)wrapper;
        }

        private static ActionQueue GetActionQueueFor(Type typeToWrap)
        {
            if (Attribute.GetCustomAttribute(typeToWrap, typeof(ServiceDomainAttribute)) is ServiceDomainAttribute serviceDomain)
            {
                return _queuesCache.AddOrUpdate(serviceDomain.DomainKey, new ActionQueue(), (key, oldValue) => oldValue);
            }

            return new ActionQueue();
        }
    }
}