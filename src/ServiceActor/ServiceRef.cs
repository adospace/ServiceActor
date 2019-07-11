using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;

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

        /// <summary>
        /// Get or create a synchronization wrapper around <paramref name="objectToWrap"/>
        /// </summary>
        /// <typeparam name="T">Type of the interface describing the service to wrap</typeparam>
        /// <param name="objectToWrap">Actual implementation type of the service</param>
        /// <param name="aggregateKey">Optional aggregation key to use in place of the <see cref="ServiceDomainAttribute"/> attribute</param>
        /// <returns>Synchronization wrapper object that implements <typeparamref name="T"/></returns>
        public static T Create<T>(T objectToWrap, object aggregateKey = null) where T : class
        {
            if (objectToWrap == null)
            {
                throw new ArgumentNullException(nameof(objectToWrap));
            }

            if (objectToWrap is IActionQueueOwner)
            {
                //objectToWrap is already a wrapper
                //test if it's the right interface
                if (objectToWrap is T)
                    return (T)objectToWrap;

                throw new ArgumentException($"Parameter is already a wrapper but not for interface '{typeof(T)}'", nameof(objectToWrap));
            }

            //NOTE: Do not use AddOrUpdate() to avoid _wrapperCache lock while generating wrapper

            ActionQueue actionQueue = null;
            object wrapper;
            if (_wrappersCache.TryGetValue(objectToWrap, out var wrapperTypes))
            {
                if (wrapperTypes.TryGetValue(typeof(T), out wrapper))
                    return (T)wrapper;

                var firstWrapper = wrapperTypes.First().Value;

                actionQueue = ((IActionQueueOwner)firstWrapper).ActionQueue;
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

        /// <summary>
        /// Execute <paramref name="actionToExecute"/> in the same queue of <paramref name="serviceObject"/>
        /// </summary>
        /// <param name="serviceObject">Service implementation or wrapper</param>
        /// <param name="actionToExecute">Action to execute in the queue of <paramref name="serviceObject"/></param>
        /// <param name="createWrapperIfNotExists">Generate a wrapper for the object on the fly if it doesn't exist</param>
        public static void Call(object serviceObject, Action actionToExecute, bool createWrapperIfNotExists = false)
        {
            if (serviceObject == null)
            {
                throw new ArgumentNullException(nameof(serviceObject));
            }

            if (actionToExecute == null)
            {
                throw new ArgumentNullException(nameof(actionToExecute));
            }

            ActionQueue actionQueue = null;

            if (serviceObject is IActionQueueOwner)
            {
                actionQueue = ((IActionQueueOwner)serviceObject).ActionQueue;
            }

            if (actionQueue == null)
            {
                if (_wrappersCache.TryGetValue(serviceObject, out var wrapperTypes))
                {
                    var firstWrapper = wrapperTypes.First().Value;

                    actionQueue = ((IActionQueueOwner)firstWrapper).ActionQueue;
                }
            }

            if (actionQueue == null)
            {
                throw new InvalidOperationException("Unable to get the action queue for the object: create a wrapper for the object with ServiceRef.Create<> first");
            }

            actionQueue.Enqueue(actionToExecute);
        }

        private static readonly ConcurrentDictionary<object, IPendingOperation> _pendingOperations = new ConcurrentDictionary<object, IPendingOperation>();

        public static void RegisterPendingOperation(object objectWrapped, IPendingOperation pendingOperation)
        {
            if (objectWrapped == null)
            {
                throw new ArgumentNullException(nameof(objectWrapped));
            }

            if (pendingOperation == null)
            {
                throw new ArgumentNullException(nameof(pendingOperation));
            }

            if (!_pendingOperations.TryAdd(objectWrapped, pendingOperation))
            {
                throw new InvalidOperationException($"Pending operation already registerd for object '{objectWrapped}'");
            }
        }

        public static void RegisterPendingOperation(object objectWrapped, WaitHandle waitHandle, int timeoutMilliseconds = 0)
        {
            if (objectWrapped == null)
            {
                throw new ArgumentNullException(nameof(objectWrapped));
            }

            RegisterPendingOperation(objectWrapped, new WaitHandlerPendingOperation(waitHandle, timeoutMilliseconds));
        }

        public static void RegisterPendingOperation<T>(object objectWrapped, WaitHandle waitHandle, Func<T> getResultFunction, int timeoutMilliseconds = 0)
        {
            if (objectWrapped == null)
            {
                throw new ArgumentNullException(nameof(objectWrapped));
            }

            RegisterPendingOperation(objectWrapped, new WaitHandlerPendingOperation<T>(waitHandle, getResultFunction, timeoutMilliseconds));
        }

        public static bool TryGetPendingOperation(object objectWrapped, out IPendingOperation pendingOperation)
        {
            if (objectWrapped == null)
            {
                throw new ArgumentNullException(nameof(objectWrapped));
            }

            return _pendingOperations.TryRemove(objectWrapped, out pendingOperation);
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