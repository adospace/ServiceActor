using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;

namespace ServiceActor
{
    public static class ServiceRef
    {
        private static readonly ConcurrentDictionary<object, ActionQueue> _queuesCache = new ConcurrentDictionary<object, ActionQueue>();
        private readonly static ConcurrentDictionary<(Type, Type), Lazy<Type>> _wrapperAssemblyCache = new ConcurrentDictionary<(Type, Type), Lazy<Type>>();
        private static readonly ConditionalWeakTable<object, ConcurrentDictionary<(Type, Type), object>> _wrappersCache = new ConditionalWeakTable<object, ConcurrentDictionary<(Type, Type), object>>();
        private static Assembly _netstandardAssembly;
        private static Assembly _systemRuntimeAssembly;

        /// <summary>
        /// Path to cache folder or null to let ServiceActor create one in SpecialFolder.ApplicationData\ServiceActor
        /// </summary>
        public static string CachePath { get; set; }

        /// <summary>
        /// Enable cache or service wrappers
        /// </summary>
        public static bool EnableCache { get; set; } = true;

        /// <summary>
        /// Execute <paramref name="actionToExecute"/> in the same queue of <paramref name="serviceObject"/>
        /// </summary>
        /// <param name="serviceObject">Service implementation or wrapper</param>
        /// <param name="actionToExecute">Action to execute in the queue of <paramref name="serviceObject"/></param>
        /// <param name="createWrapperIfNotExists">Generate a wrapper for the object on the fly if it doesn't exist</param>
        public static void Call(object serviceObject, Action actionToExecute, bool createWrapperIfNotExists = false)
        {
            InternalCall(serviceObject, actionToExecute, null, createWrapperIfNotExists);
        }

        /// <summary>
        /// Execute <paramref name="actionToExecute"/> in the same queue of <paramref name="serviceObject"/>
        /// </summary>
        /// <param name="serviceObject">Service implementation or wrapper</param>
        /// <param name="asyncActionToExecute">Async action to execute in the queue of <paramref name="serviceObject"/></param>
        /// <param name="createWrapperIfNotExists">Generate a wrapper for the object on the fly if it doesn't exist</param>
        public static void Call(object serviceObject, Func<Task> asyncActionToExecute, bool createWrapperIfNotExists = false)
        {
            InternalCall(serviceObject, null, asyncActionToExecute, createWrapperIfNotExists);
        }


        /// <summary>
        /// Execure <paramref name="actionToExecute"/> in the same queue of <paramref name="serviceObject"/> waiting for the call to be completed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceObject">Service implementation or wrapper</param>
        /// <param name="actionToExecute">Action to execute in the queue of <paramref name="serviceObject"/></param>
        /// <param name="cancellationToken">Cancellation token used for the call</param>
        /// <param name="createWrapperIfNotExists">Generate a wrapper for the object on the fly if it doesn't exist</param>
        /// <returns>Task that can be awaited</returns>
        public static Task CallAndWaitAsync(object serviceObject, Action actionToExecute, CancellationToken cancellationToken = default, bool createWrapperIfNotExists = false)
        {
            var actionQueue = InternalCall(serviceObject, actionToExecute, null, createWrapperIfNotExists);

            var completionEvent = new AsyncAutoResetEvent(false);

            actionQueue.Enqueue(() => completionEvent.Set());

            if (cancellationToken != default)
            {
                return completionEvent.WaitAsync(cancellationToken);
            }
            else
            {
                return completionEvent.WaitAsync();
            }
        }

        /// <summary>
        /// Execure <paramref name="actionToExecute"/> in the same queue of <paramref name="serviceObject"/> waiting for the call to be completed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceObject">Service implementation or wrapper</param>
        /// <param name="asyncActionToExecute">Async action to execute in the queue of <paramref name="serviceObject"/></param>
        /// <param name="cancellationToken">Cancellation token used for the call</param>
        /// <param name="createWrapperIfNotExists">Generate a wrapper for the object on the fly if it doesn't exist</param>
        /// <returns>Task that can be awaited</returns>
        public static Task CallAndWaitAsync(object serviceObject, Func<Task> asyncActionToExecute, CancellationToken cancellationToken = default, bool createWrapperIfNotExists = false)
        {
            var actionQueue = InternalCall(serviceObject, null, asyncActionToExecute, createWrapperIfNotExists);

            var completionEvent = new AsyncAutoResetEvent(false);

            actionQueue.Enqueue(() => completionEvent.Set());

            if (cancellationToken != default)
            {
                return completionEvent.WaitAsync(cancellationToken);
            }
            else
            {
                return completionEvent.WaitAsync();
            }
        }


        /// <summary>
        /// Clear cache of service wrappers
        /// </summary>
        public static void ClearCache()
        {
            if (EnableCache)
            {
                var assemblyCacheFolder = CachePath ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceActor", Utils.MD5Hash(Assembly.GetEntryAssembly().Location));
                if (Directory.Exists(assemblyCacheFolder))
                {
                    Directory.Delete(assemblyCacheFolder, true);
                }
            }
        }

        public static T Create<T>(T objectToWrap, object aggregateKey = null) where T : class
                                                    => CreateFor<T>(objectToWrap, aggregateKey);

        /// <summary>
        /// Get or create a synchronization wrapper around <paramref name="objectToWrap"/>
        /// </summary>
        /// <typeparam name="T">Type of the interface describing the service to wrap</typeparam>
        /// <param name="objectToWrap">Actual implementation type of the service</param>
        /// <param name="aggregateKey">Optional aggregation key to use in place of the <see cref="ServiceDomainAttribute"/> attribute</param>
        /// <returns>Synchronization wrapper object that implements <typeparamref name="T"/></returns>
        public static T CreateFor<T>(object objectToWrap, object aggregateKey = null, bool throwIfNotFound = true) where T : class
        {
            if (objectToWrap == null)
            {
                throw new ArgumentNullException(nameof(objectToWrap));
            }

            var serviceType = typeof(T);

            if (objectToWrap is IServiceActorWrapper)
            {
                //objectToWrap is already a wrapper
                //test if it's the right interface
                if (objectToWrap is T)
                {
                    return (T)objectToWrap;
                }

                objectToWrap = ((IServiceActorWrapper)objectToWrap).WrappedObject;
                //throw new ArgumentException($"Parameter is already a wrapper but not for interface '{serviceType}'", nameof(objectToWrap));
            }

            if (!objectToWrap.GetType().Implements(typeof(T)))
            {
                if (!throwIfNotFound)
                    return null;

                throw new InvalidOperationException($"Object of type '{objectToWrap.GetType()}' does not implement interface '{typeof(T)}'");
            }

            //NOTE: Do not use AddOrUpdate() to avoid _wrapperCache lock while generating wrapper

            ActionQueue actionQueue = null;
            object wrapper;
            var wrapperTypeKey = (serviceType, objectToWrap.GetType());

            if (_wrappersCache.TryGetValue(objectToWrap, out var wrapperTypes))
            {
                if (wrapperTypes.TryGetValue(wrapperTypeKey, out wrapper))
                    return (T)wrapper;

                //use defensive code to get the underling ActionQueue
                //just to be sure that only one queue is created for a single object

                //var firstWrapper = wrapperTypes.First().Value;
                //actionQueue = ((IServiceActorWrapper)firstWrapper).ActionQueue;

                actionQueue = wrapperTypes.Values.Cast<IServiceActorWrapper>()
                    .Select(_ => _.ActionQueue)
                    .Distinct()
                    .SingleOrDefault();
            }

            actionQueue = actionQueue ?? GetActionQueueFor(objectToWrap, serviceType, aggregateKey);

            wrapper = GetOrCreateWrapper(serviceType, objectToWrap, actionQueue);

            //there is no need to use a Lazy here as the value factory is not used at all
            wrapperTypes = _wrappersCache.GetOrCreateValue(objectToWrap);

            wrapperTypes.TryAdd(wrapperTypeKey, wrapper);

            return (T)wrapper;
        }

        public static bool TryGetWrappedObject<T>(object wrapper, out T wrappedObject) where T : class
        {
            if (wrapper is IServiceActorWrapper serviceActorWrapper)
            {
                wrappedObject = (T)serviceActorWrapper.WrappedObject;
                return true;
            }

            if (wrapper is T)
            {
                wrappedObject = (T)wrapper;
                return true;
            }

            wrappedObject = null;
            return false;
        }

        /// <summary>
        /// Wait that call queue for a service is completed; i.e. all pending calls for the service are executed
        /// </summary>
        /// <param name="serviceObject">Service object whose call queue has to be awaited</param>
        /// <param name="millisecondsTimeout">Timeout while wait for completion</param>
        /// <returns>True if call queue has been completed</returns>
        /// <remarks>This function has to be considered as an helper while testing services.</remarks>
        public static bool WaitForCallQueueCompletion(object serviceObject, int millisecondsTimeout = 0)
        {
            if (!(serviceObject is IServiceActorWrapper serviceActionWrapper))
            {
                throw new ArgumentException("Service object argument is not a wrapper for a service");
            }

            using (var completionEvent = new AutoResetEvent(false))
            {
                serviceActionWrapper.ActionQueue.Enqueue(() => completionEvent.Set());

                if (millisecondsTimeout > 0)
                {
                    return completionEvent.WaitOne(millisecondsTimeout);
                }

                completionEvent.WaitOne();
            }
            return true;
        }

        /// <summary>
        /// Async wait for a call queue to a service to complete
        /// </summary>
        /// <param name="serviceObject">Service object to wait call queue completion</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns></returns>
        /// <remarks>This an helper function useful while testing services</remarks>
        public static Task WaitForCallQueueCompletionAsync(object serviceObject, CancellationToken cancellationToken = default)
        {
            var serviceActionWrapper = serviceObject as IServiceActorWrapper;

            if (serviceActionWrapper == null)
            {
                throw new ArgumentException("Service object argument is not a wrapper for a service");
            }

            var completionEvent = new AsyncAutoResetEvent(false);

            serviceActionWrapper.ActionQueue.Enqueue(() => completionEvent.Set());

            if (cancellationToken != default)
            {
                return completionEvent.WaitAsync(cancellationToken);
            }
            else
            {
                return completionEvent.WaitAsync();
            }
        }

        private static ActionQueue GetActionQueueFor(object objectToWrap, Type typeToWrap, object aggregateKey)
        {
            if (aggregateKey != null)
            {
                return _queuesCache.AddOrUpdate(
                    aggregateKey,
                    new ActionQueue(aggregateKey.ToString()),
                    (key, oldValue) => oldValue);
            }

            if (Attribute.GetCustomAttribute(typeToWrap, typeof(ServiceDomainAttribute)) is ServiceDomainAttribute serviceDomain)
            {
                return _queuesCache.AddOrUpdate(
                    serviceDomain.DomainKey,
                    new ActionQueue(serviceDomain.DomainKey.ToString()),
                    (key, oldValue) => oldValue);
            }

            return _queuesCache.AddOrUpdate(
                objectToWrap.GetHashCode().ToString(),
                new ActionQueue(objectToWrap.GetType().FullName),
                (key, oldValue) => oldValue);
        }

        private static object GetOrCreateWrapper(Type interfaceType, object objectToWrap, ActionQueue actionQueue)
        {
            var implType = objectToWrap.GetType();
            var sourceTemplate = new ServiceActorWrapperTemplate(interfaceType, implType);

            //using a Lazy ensure that add value factory is execute only once
            //https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
            var wrapperImplType = _wrapperAssemblyCache.GetOrAdd((interfaceType, implType), (key) => new Lazy<Type>(() =>
            {
                var source = sourceTemplate.TransformText();

                string assemblyFilePath = null;
                if (EnableCache)
                {
                    var assemblyCacheFolder = CachePath ??
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceActor", Utils.MD5Hash(Assembly.GetEntryAssembly().Location));
                    Directory.CreateDirectory(assemblyCacheFolder);

                    assemblyFilePath = Path.Combine(assemblyCacheFolder, Utils.MD5Hash(source) + ".dll");

                    if (File.Exists(assemblyFilePath))
                    {
                        var cachedAssembly = Assembly.LoadFile(assemblyFilePath);
                        return cachedAssembly
                            .GetTypes()
                            .First(_ => _.GetInterface("IServiceActorWrapper") != null);
                    }
                }

                var sourceSyntaxTree = CSharpSyntaxTree.ParseText(source);

                _systemRuntimeAssembly = _systemRuntimeAssembly ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Single(_ => _.ManifestModule.Name == "System.Runtime.dll");

                _netstandardAssembly = _netstandardAssembly ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Single(_ => _.ManifestModule.Name == "netstandard.dll");

                string assemblyName = Path.GetRandomFileName();
                var references = new MetadataReference[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(_systemRuntimeAssembly.Location),
                    MetadataReference.CreateFromFile(_netstandardAssembly.Location),
                    MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
                    MetadataReference.CreateFromFile(interfaceType.Assembly.Location),
                    MetadataReference.CreateFromFile(implType.Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(AsyncAutoResetEvent).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(CSharpCompilationOptions).Assembly.Location)
                };

                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    syntaxTrees: new[] {sourceSyntaxTree},
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                //var script = CSharpScript.Create(
                //    source,
                //    options: ScriptOptions.Default.AddReferences(
                //        Assembly.GetExecutingAssembly(),
                //        interfaceType.Assembly,
                //        implType.Assembly,
                //        typeof(AsyncAutoResetEvent).Assembly)
                //    );

                //var diagnostics = script.Compile();
                //if (diagnostics.Any())
                //{
                //    throw new InvalidOperationException();
                //}

                //var compilation = script.GetCompilation();

                Assembly generatedAssembly;
                using (var dllStream = new MemoryStream())
                {
                    var emitResult = compilation.Emit(dllStream);
                    if (!emitResult.Success)
                    {
                        // emitResult.Diagnostics
                        throw new InvalidOperationException();
                    }

                    if (assemblyFilePath != null)
                    {
                        File.WriteAllBytes(assemblyFilePath, dllStream.ToArray());
                    }

                    generatedAssembly = Assembly.Load(dllStream.ToArray());
                }

                return generatedAssembly
                    .GetTypes()
                    .First(_ => _.GetInterface("IServiceActorWrapper") != null);
            }));

            return Activator.CreateInstance(
                wrapperImplType.Value, objectToWrap, sourceTemplate.TypeToWrapFullName, actionQueue);
        }

        private static ActionQueue InternalCall(object serviceObject, Action actionToExecute = null, Func<Task> asyncActionToAxecute = null, bool createWrapperIfNotExists = false)
        {
            if (serviceObject == null)
            {
                throw new ArgumentNullException(nameof(serviceObject));
            }

            if (actionToExecute == null && asyncActionToAxecute == null)
            {
                throw new ArgumentNullException(nameof(actionToExecute));
            }

            ActionQueue actionQueue = null;

            if (serviceObject is IServiceActorWrapper)
            {
                actionQueue = ((IServiceActorWrapper)serviceObject).ActionQueue;
            }

            if (actionQueue == null)
            {
                if (_wrappersCache.TryGetValue(serviceObject, out var wrapperTypes))
                {
                    //use defensive code to get the underling ActionQueue
                    //just to be sure that only one queue is created for a single object

                    //var firstWrapper = wrapperTypes.First().Value;
                    //actionQueue = ((IServiceActorWrapper)firstWrapper).ActionQueue;

                    actionQueue = wrapperTypes.Values.Cast<IServiceActorWrapper>()
                        .Select(_ => _.ActionQueue)
                        .Distinct()
                        .Single();
                }
            }

            if (actionQueue == null)
            {
                throw new InvalidOperationException("Unable to get the action queue for the object: create a wrapper for the object with ServiceRef.Create<> first");
            }

            if (actionToExecute != null)
                actionQueue.Enqueue(actionToExecute);
            else
                actionQueue.EnqueueAsync(asyncActionToAxecute);

            return actionQueue;
        }

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

            ActionQueue actionQueue = null;

            if (objectWrapped is IServiceActorWrapper objectWrappedAsActionQueueOwner)
            {
                actionQueue = objectWrappedAsActionQueueOwner.ActionQueue;
            }

            if (actionQueue == null)
            {
                if (_wrappersCache.TryGetValue(objectWrapped, out var wrapperTypes))
                {
                    //use defensive code to get the underling ActionQueue
                    //just to be sure that only one queue is created for a single object

                    //var firstWrapper = wrapperTypes.First().Value;
                    //actionQueue = ((IServiceActorWrapper)firstWrapper).ActionQueue;

                    actionQueue = wrapperTypes.Values.Cast<IServiceActorWrapper>()
                        .Select(_ => _.ActionQueue)
                        .Distinct()
                        .SingleOrDefault();
                }
            }

            if (actionQueue == null)
            {
                throw new InvalidOperationException($"Object {objectWrapped} of type '{objectWrapped.GetType()}' is not managed by ServiceActor: call ServiceRef.Create<> to create a service actor for it");
            }

            actionQueue.RegisterPendingOperation(pendingOperation);
        }

        public static void RegisterPendingOperation(object objectWrapped, WaitHandle waitHandle, int timeoutMilliseconds = 0, Action<bool> actionOnCompletion = null)
        {
            if (objectWrapped == null)
            {
                throw new ArgumentNullException(nameof(objectWrapped));
            }

            RegisterPendingOperation(objectWrapped, new WaitHandlerPendingOperation(waitHandle, timeoutMilliseconds, actionOnCompletion));
        }

        public static void RegisterPendingOperation<T>(object objectWrapped, WaitHandle waitHandle, Func<T> getResultFunction, int timeoutMilliseconds = 0, Action<bool> actionOnCompletion = null)
        {
            if (objectWrapped == null)
            {
                throw new ArgumentNullException(nameof(objectWrapped));
            }

            RegisterPendingOperation(objectWrapped, new WaitHandlePendingOperation<T>(waitHandle, getResultFunction, timeoutMilliseconds, actionOnCompletion));
        }
    }
}