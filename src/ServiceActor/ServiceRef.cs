using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceActor
{
    public static class ServiceRef
    {
        //public class ScriptGlobals
        //{
        //    public object ObjectToWrap { get; set; }

        //    public ActionQueue ActionQueueToShare { get; set; }
        //}

        private static readonly ConcurrentDictionary<object, ConcurrentDictionary<(Type, Type), object>> _wrappersCache = new ConcurrentDictionary<object, ConcurrentDictionary<(Type, Type), object>>();

        private static readonly ConcurrentDictionary<object, ActionQueue> _queuesCache = new ConcurrentDictionary<object, ActionQueue>();

        public static T Create<T>(T objectToWrap, object aggregateKey = null) where T : class
            => CreateFor<T>(objectToWrap, aggregateKey);

        /// <summary>
        /// Get or create a synchronization wrapper around <paramref name="objectToWrap"/>
        /// </summary>
        /// <typeparam name="T">Type of the interface describing the service to wrap</typeparam>
        /// <param name="objectToWrap">Actual implementation type of the service</param>
        /// <param name="aggregateKey">Optional aggregation key to use in place of the <see cref="ServiceDomainAttribute"/> attribute</param>
        /// <returns>Synchronization wrapper object that implements <typeparamref name="T"/></returns>
        public static T CreateFor<T>(object objectToWrap, object aggregateKey = null) where T : class
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

            //NOTE: Do not use AddOrUpdate() to avoid _wrapperCache lock while generating wrapper

            ActionQueue actionQueue = null;
            object wrapper;
            var wrapperTypeKey = (serviceType, objectToWrap.GetType());

            if (_wrappersCache.TryGetValue(objectToWrap, out var wrapperTypes))
            {
                if (wrapperTypes.TryGetValue(wrapperTypeKey, out wrapper))
                    return (T)wrapper;

                var firstWrapper = wrapperTypes.First().Value;

                actionQueue = ((IServiceActorWrapper)firstWrapper).ActionQueue;
            }

            actionQueue = actionQueue ?? GetActionQueueFor(serviceType, aggregateKey);

            wrapper = GetOrCreateWrapper(serviceType, objectToWrap, actionQueue);

            wrapperTypes = _wrappersCache.GetOrAdd(objectToWrap, new ConcurrentDictionary<(Type, Type), object>());

            wrapperTypes.TryAdd(wrapperTypeKey, wrapper);

            return (T)wrapper;
        }

        //private class WrapperTypeKey
        //{
        //    private readonly Type _interfaceType;
        //    private readonly Type _implType;

        //    public WrapperTypeKey(Type @interfaceType, Type implType)
        //    {
        //        _interfaceType = interfaceType;
        //        _implType = implType;
        //    }

        //    public override bool Equals(object obj)
        //    {
        //        var key = obj as WrapperTypeKey;
        //        return key != null &&
        //               EqualityComparer<Type>.Default.Equals(_interfaceType, key._interfaceType) &&
        //               EqualityComparer<Type>.Default.Equals(_implType, key._implType);
        //    }

        //    public override int GetHashCode()
        //    {
        //        var hashCode = -20423575;
        //        hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(_interfaceType);
        //        hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(_implType);
        //        return hashCode;
        //    }
        //}

        private readonly static ConcurrentDictionary<(Type, Type), Assembly> _wrapperAssemblyCache = new ConcurrentDictionary<(Type, Type), Assembly>();

        //private static Script<T> GetOrCreateScript<T>(Type implType)
        //{
        //    var asyncActorCode = new ServiceActorWrapperTemplate(typeof(T), implType).TransformText();
        //    //Console.WriteLine(asyncActorCode);
        //    var script = _scriptCache.GetOrAdd(new ScriptTypeKey(typeof(T), implType), CSharpScript.Create<T>(
        //        asyncActorCode,
        //        options: ScriptOptions.Default.AddReferences(
        //            Assembly.GetExecutingAssembly(),
        //            typeof(T).Assembly,
        //            typeof(Nito.AsyncEx.AsyncAutoResetEvent).Assembly),
        //        globalsType: typeof(ScriptGlobals)
        //        ));

        //    return (Script<T>)script;
        //}

        public static bool EnableCache { get; set; } = true;

        public static string CachePath { get; set; }

        public static void ClearCache()
        {
            if (EnableCache)
            {
                var assemblyCacheFolder = CachePath ?? 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceActor", MD5Hash(Assembly.GetEntryAssembly().Location));
                if (Directory.Exists(assemblyCacheFolder))
                {
                    Directory.Delete(assemblyCacheFolder, true);
                }
            }
        }

        private static object GetOrCreateWrapper(Type interfaceType, object objectToWrap, ActionQueue actionQueue)
        {
            var implType = objectToWrap.GetType();
            var sourceTemplate = new ServiceActorWrapperTemplate(interfaceType, implType);

            var wrapperAssembly = _wrapperAssemblyCache.GetOrAdd((interfaceType, implType), (key) =>
            {
                var source = sourceTemplate.TransformText();

                string assemblyFilePath = null;
                if (EnableCache)
                {
                    var assemblyCacheFolder = CachePath ??
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceActor", MD5Hash(Assembly.GetEntryAssembly().Location));
                    Directory.CreateDirectory(assemblyCacheFolder);

                    assemblyFilePath = Path.Combine(assemblyCacheFolder, MD5Hash(source) + ".dll");

                    if (File.Exists(assemblyFilePath))
                    {
                        return Assembly.LoadFile(assemblyFilePath);
                    }
                }               

                var script = CSharpScript.Create(
                    source,
                    options: ScriptOptions.Default.AddReferences(
                        Assembly.GetExecutingAssembly(),
                        interfaceType.Assembly,
                        implType.Assembly,
                        typeof(Nito.AsyncEx.AsyncAutoResetEvent).Assembly)
                    );

                var diagnostics = script.Compile();
                if (diagnostics.Any())
                {
                    throw new InvalidOperationException();
                }

                var compilation = script.GetCompilation();

                //var tempFile = Path.GetTempFileName();
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

                    return Assembly.Load(dllStream.ToArray());
                }           
            });

            //return new <#= TypeToWrapName #>AsyncActorWrapper((<#= TypeToWrapFullName #>)ObjectToWrap, "<#= TypeToWrapFullName #>", ActionQueueToShare);
            var wrapperImplType = wrapperAssembly.GetTypes().First(_ => _.GetInterface("IServiceActorWrapper") != null);
            return Activator.CreateInstance(
                wrapperImplType, objectToWrap, sourceTemplate.TypeToWrapFullName, actionQueue);
        }

        private static string MD5Hash(string input)
        {
            using (var md5provider = new MD5CryptoServiceProvider())
            {
                byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(input));

                var hash = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash.Append(bytes[i].ToString("x2"));
                }
                return hash.ToString();
            }
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

            if (serviceObject is IServiceActorWrapper)
            {
                actionQueue = ((IServiceActorWrapper)serviceObject).ActionQueue;
            }

            if (actionQueue == null)
            {
                if (_wrappersCache.TryGetValue(serviceObject, out var wrapperTypes))
                {
                    var firstWrapper = wrapperTypes.First().Value;

                    actionQueue = ((IServiceActorWrapper)firstWrapper).ActionQueue;
                }
            }

            if (actionQueue == null)
            {
                throw new InvalidOperationException("Unable to get the action queue for the object: create a wrapper for the object with ServiceRef.Create<> first");
            }

            actionQueue.Enqueue(actionToExecute);
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
            var serviceActionWrapper = serviceObject as IServiceActorWrapper;

            if (serviceObject == null)
            {
                throw new ArgumentException("Service object argument is not a wrapper for a service");
            }

            var completionEvent = new AutoResetEvent(false);

            serviceActionWrapper.ActionQueue.Enqueue(() => completionEvent.Set());

            if (millisecondsTimeout > 0)
            {
                return completionEvent.WaitOne(millisecondsTimeout);
            }

            completionEvent.WaitOne();
            return true;
        }

        /// <summary>
        /// Async wait for a call queue to a service to complete
        /// </summary>
        /// <param name="serviceObject">Service object to wait call queue completion</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns></returns>
        /// <remarks>This an helper function useful while testing services</remarks>
        public static Task WaitForCallQueueCompletionAsync(object serviceObject, CancellationToken cancellationToken = default(CancellationToken))
        {
            var serviceActionWrapper = serviceObject as IServiceActorWrapper;

            if (serviceObject == null)
            {
                throw new ArgumentException("Service object argument is not a wrapper for a service");
            }

            var completionEvent = new AsyncAutoResetEvent(false);

            serviceActionWrapper.ActionQueue.Enqueue(() => completionEvent.Set());

            if (cancellationToken != default(CancellationToken))
            {
                return completionEvent.WaitAsync(cancellationToken);
            }
            else
            {
                return completionEvent.WaitAsync();
            }
        }
        #region Pending Operation

        #region Pending Operations
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
            if (_wrappersCache.TryGetValue(objectWrapped, out var wrapperTypes))
            {
                var firstWrapper = wrapperTypes.First().Value;

                actionQueue = ((IServiceActorWrapper)firstWrapper).ActionQueue;
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
        #endregion

        #endregion

        private static ActionQueue GetActionQueueFor(Type typeToWrap, object aggregateKey)
        {
            if (aggregateKey != null)
            {
                return _queuesCache.AddOrUpdate(aggregateKey,
                    new ActionQueue(), (key, oldValue) => oldValue);
            }

            if (Attribute.GetCustomAttribute(typeToWrap, typeof(ServiceDomainAttribute)) is ServiceDomainAttribute serviceDomain)
            {
                return _queuesCache.AddOrUpdate(serviceDomain.DomainKey, 
                    new ActionQueue(), (key, oldValue) => oldValue);
            }

            return new ActionQueue();
        }

        //public static bool TryGetWrappedObject<T>(object wrapper, out T wrappedObject) where T : class
        //{
        //    if (wrapper is IServiceActorWrapper serviceActorWrapper)
        //    {
        //        wrappedObject = (T)serviceActorWrapper.WrappedObject;
        //        return true;
        //    }

        //    if (wrapper is T)
        //    {
        //        wrappedObject = (T)wrapper;
        //        return true;
        //    }

        //    wrappedObject = null;
        //    return false;
        //}
    }
}