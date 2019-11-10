using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceActor.Tests
{
    [TestClass]
    public class PendingOperationTests
    {
        public interface IPendingOpsService
        {
            bool OperationCompleted { get; }

            [BlockCaller]
            void BeginOperation();

            void CompleteOperation();
        }

        class PendingOpsService : IPendingOpsService
        {
            public bool OperationCompleted { get; private set; }

            private IPendingOperation _pendingOperation;

            public void BeginOperation()
            {
                _pendingOperation = ServiceRef.RegisterPendingOperation(this);

                Task.Factory.StartNew(() =>
                {
                    //simulate some work
                    Task.Delay(1000).Wait();
                    ServiceRef.Create<IPendingOpsService>(this)
                        .CompleteOperation();
                });
            }

            public void CompleteOperation()
            {
                OperationCompleted = true;
                _pendingOperation.Complete();
            }
        }

        [TestMethod]
        public void TestPendingOperations()
        {
            var pendingOpsTestService = ServiceRef.Create<IPendingOpsService>(new PendingOpsService());

            Assert.IsFalse(pendingOpsTestService.OperationCompleted);

            pendingOpsTestService.BeginOperation();

            Assert.IsTrue(pendingOpsTestService.OperationCompleted);
        }

        public interface IPendingOpsService<T>
        {
            T Result { get; }

            bool OperationCompleted { get; }

            T BeginOperation();

            void CompleteOperation(T result);
        }

        class PendingOpsService<T> : IPendingOpsService<T>
        {
            public PendingOpsService(T completedResult)
            {
                _completedResult = completedResult;
            }

            public bool OperationCompleted { get; private set; }

            private IPendingOperation _pendingOperation;
            public T Result { get; private set; } = default(T);
            private readonly T _completedResult;

            public T BeginOperation()
            {
                _pendingOperation = ServiceRef.RegisterPendingOperation(this, () => Result);

                Task.Factory.StartNew(() =>
                {
                    //simulate some work
                    Task.Delay(1000).Wait();
                    ServiceRef.Create<IPendingOpsService<T>>(this)
                        .CompleteOperation(_completedResult);
                });

                return default(T);
            }

            public void CompleteOperation(T result)
            {
                Result = result;
                OperationCompleted = true;
                _pendingOperation.Complete();
            }
        }

        [TestMethod]
        public void TestPendingOperationsT()
        {
            var pendingOpsTestService = ServiceRef.Create<IPendingOpsService<string>>(new PendingOpsService<string>("DONE"));

            Assert.IsFalse(pendingOpsTestService.OperationCompleted);
            Assert.IsNull(pendingOpsTestService.Result);

            pendingOpsTestService.BeginOperation();

            Assert.IsTrue(pendingOpsTestService.OperationCompleted);
            Assert.AreEqual("DONE", pendingOpsTestService.Result);
        }


        public interface IPendingOpsServiceAsync
        {
            bool OperationCompleted { get; }

            Task<bool> BeginOperationAsync();

            Task CompleteOperationAsync();
        }

        class PendingOpsServiceAsync : IPendingOpsServiceAsync
        {
            public bool OperationCompleted { get; private set; }

            private IPendingOperation _pendingOperation;
            public Task<bool> BeginOperationAsync()
            {
                _pendingOperation = ServiceRef.RegisterPendingOperation(this, () => OperationCompleted);

                Task.Run(async () =>
                {
                    //simulate some work
                    await Task.Delay(1000);
                    await ServiceRef.Create<IPendingOpsServiceAsync>(this)
                        .CompleteOperationAsync();
                });

                return Task.FromResult(false);
            }

            public Task CompleteOperationAsync()
            {
                OperationCompleted = true;
                _pendingOperation.Complete();
                return Task.CompletedTask;
            }
        }

        [TestMethod]
        public async Task TestPendingOperationsAsyncVersion()
        {
            var pendingOpsTestService = ServiceRef.Create<IPendingOpsServiceAsync>(new PendingOpsServiceAsync());

            Assert.IsFalse(pendingOpsTestService.OperationCompleted);

            await pendingOpsTestService.BeginOperationAsync();

            Assert.IsTrue(pendingOpsTestService.OperationCompleted);
        }


        public class ImageStuff
        {
            public string Url { get; set; }

            public byte[] Data { get; set; }
        }

        public interface IImageService
        {
            IList<ImageStuff> Images { get; }

            Task GetOrDownloadAsync(string url);
        }

        private class ImageServiceAsync : IImageService
        {
            private readonly List<ImageStuff> _images = new List<ImageStuff>();
            public IList<ImageStuff> Images
            {
                get
                {
                    Console.WriteLine($"Getting images...");
                    try
                    {
                        return _images.ToList();
                    }
                    finally
                    {
                        Console.WriteLine($"Getting images...completed");
                    }
                }
            }

            public async Task GetOrDownloadAsync(string url)
            {
                //simulate image download
                Console.WriteLine($"Downloading {url}");
                await Task.Delay(1000);
                Console.WriteLine($"Downloaded {url}");
                Task.Delay(1000).Wait();
                Console.WriteLine($"Adding to images {url}");
                _images.Add(new ImageStuff() { Url = url, Data = new byte[] { 0x01 } });
                Console.WriteLine($"Added to images {url}");
            }
        }

        private class ImageServiceWithPendingOperation : IImageService
        {
            private readonly List<ImageStuff> _images = new List<ImageStuff>();
            public IList<ImageStuff> Images
            {
                get
                {
                    Console.WriteLine($"Getting images...");
                    try
                    {
                        return _images.ToList();
                    }
                    finally
                    {
                        Console.WriteLine($"Getting images...completed");
                    }
                }
            }

            public Task GetOrDownloadAsync(string url)
            {
                //simulate image download
                IPendingOperation pendingOperation = null;
                Console.WriteLine($"Downloading {url}");
                Task.Delay(2000).ContinueWith(_=> 
                {
                    Console.WriteLine($"Downloaded {url}");
                    pendingOperation.Complete();
                });

                pendingOperation = ServiceRef.RegisterPendingOperation(this, actionOnCompletion: (res)=> 
                {
                    //Accessing _images here would be risky, just wrap the call...
                    ServiceRef.Call(this, () =>
                    {
                        Console.WriteLine($"Adding to images {url}");
                        _images.Add(new ImageStuff() { Url = url, Data = new byte[] { 0x01 } });
                        Console.WriteLine($"Added to images {url}");
                    });
                });

                return Task.CompletedTask;
            }
        }

        [TestMethod]
        public async Task TestImageServiceAsync()
        {
            var wrappedImageService = new ImageServiceAsync();
            var imageService = ServiceRef.Create<IImageService>(wrappedImageService);

            Assert.IsTrue(ServiceRef.TryGetWrappedObject<ImageServiceAsync>(imageService, out var returnedWrappedObject));
            Assert.AreSame(wrappedImageService, returnedWrappedObject);
            //await imageService.GetOrDownloadAsync("https://myimage");

            //Assert.AreEqual(1, imageService.Images.Count);
            //Assert.AreEqual("https://myimage", imageService.Images[0].Url);
            //Assert.IsTrue(imageService.Images[0].Data.Length > 0);

            var callTracer = new SimpleCallMonitorTracer();
            ActionQueue.BeginMonitor(callTracer);

            try
            {
                await imageService.GetOrDownloadAsync("https://myimage");

                Assert.AreEqual(1, imageService.Images.Count);
            }
            finally
            {
                ActionQueue.ExitMonitor(callTracer);
            }
        }

        [TestMethod]
        public void TestImageServiceAsyncMultiple()
        {
            var imageService = ServiceRef.Create<IImageService>(new ImageServiceAsync());

            var callTracer = new SimpleCallMonitorTracer();
            ActionQueue.BeginMonitor(callTracer);

            try
            {
                Task.WaitAll(
                    Enumerable
                        .Range(1, 5)
                        .Select(i => Task.Factory.StartNew(async () =>
                        {
                            await imageService.GetOrDownloadAsync("https://myimage" + i);
                        }))
                        .ToArray());

                Assert.AreEqual(5, imageService.Images.Count);
            }
            finally
            {
                ActionQueue.ExitMonitor(callTracer);
            }
        }

        [TestMethod]
        public async Task TestImageServiceWithPendingOperation()
        {
            var imageService = ServiceRef.Create<IImageService>(new ImageServiceWithPendingOperation());

            await imageService.GetOrDownloadAsync("https://myimage");

            Assert.AreEqual(1, imageService.Images.Count);
            Assert.AreEqual("https://myimage", imageService.Images[0].Url);
            Assert.IsTrue(imageService.Images[0].Data.Length > 0);
        }

        [TestMethod]
        public void TestImageServiceWithPendingOperationMultiple()
        {
            var imageService = ServiceRef.Create<IImageService>(new ImageServiceWithPendingOperation());

            var callTracer = new SimpleCallMonitorTracer();
            ActionQueue.BeginMonitor(callTracer);

            try
            {
                Task.WaitAll(
                    Enumerable
                        .Range(1, 5)
                        .Select(i => imageService.GetOrDownloadAsync("https://myimage" + i))
                        .ToArray());

                Assert.AreEqual(5, imageService.Images.Count);
            }
            finally
            {
                ActionQueue.ExitMonitor(callTracer);
            }
        }
    }
}
