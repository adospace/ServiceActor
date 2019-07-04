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

        private static ConcurrentDictionary<object, object> _wrappersCache = new ConcurrentDictionary<object, object>();


        public static T Create<T>(T objectToWrap)
        {
            //Console.WriteLine(new ServiceActorWrapperTemplate(typeof(T)).TransformText());

            //NOTE: Do not user AddOrUpdate() to avoid _wrapperCache lock while generating wrapper
            if (_wrappersCache.TryGetValue(objectToWrap, out var wrapper))
                return (T)wrapper;

            var asyncActorCode = new ServiceActorWrapperTemplate(typeof(T)).TransformText();
            wrapper = CSharpScript.EvaluateAsync<T>(
                asyncActorCode,
                options: ScriptOptions.Default.AddReferences(
                    Assembly.GetExecutingAssembly(),
                    typeof(T).Assembly,
                    typeof(Nito.AsyncEx.AsyncAutoResetEvent).Assembly),
                globals: new ScriptGlobals { ObjectToWrap = objectToWrap, ActionQueueToShare = new ActionQueue() }
                ).Result;

            _wrappersCache.TryAdd(objectToWrap, wrapper);

            _wrappersCache.TryGetValue(objectToWrap, out wrapper);

            return (T)wrapper;
        }


    }
}
