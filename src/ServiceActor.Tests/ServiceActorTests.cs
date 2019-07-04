using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceActor.Tests
{
    [TestClass]
    public class ServiceActorTests
    {
        public interface ITestService
        {
            int SimplePropertyGet { get; }
            int SimplePropertySet { get; }
            int SimplePropertyGetSet { get; set; }

            void SimpleMethod();

            void SimpleMethodWithArguments(int i);

            void SimpleMethodWithGenericArguments<T1, T2>(T1 t1, T2 t2);

            void SimpleMethosWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict);

            Task TaskMethod();

            Task TaskMethodWithArguments(int i);

            Task TaskMethodWithGenericArguments<T1, T2>(T1 t1, T2 t2);

            Task TaskMethosWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict);

            Task<int> TaskMethodWithReturnType();

            Task<int> TaskMethodWithReturnTypeAndWithArguments(int i);

            Task<int> TaskMethodWithReturnTypeAndWithGenericArguments<T1, T2>(T1 t1, T2 t2);

            Task<int> TaskMethodWithReturnTypeAndWithWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict);

            Task<Action<int>> TaskMethodWithComplexReturnType();

            Task<Action<int>> TaskMethodWithComplexReturnTypeAndWithArguments(int i);

            Task<Action<T2>> TaskMethodWithComplexReturnTypeAndWithGenericArguments<T1, T2>(T1 t1, T2 t2);

            Task<Action<T2>> TaskMethodWithComplexReturnTypeAndWithWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict);
        }

        private class TestService : ITestService
        {
            public int SimplePropertyGet => throw new NotImplementedException();

            public int SimplePropertySet => throw new NotImplementedException();

            public int SimplePropertyGetSet { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public void SimpleMethod()
            {
                throw new NotImplementedException();
            }

            public void SimpleMethodWithArguments(int i)
            {
                throw new NotImplementedException();
            }

            public void SimpleMethodWithGenericArguments<T1, T2>(T1 t1, T2 t2)
            {
                throw new NotImplementedException();
            }

            public void SimpleMethosWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict)
            {
                throw new NotImplementedException();
            }

            public Task TaskMethod()
            {
                throw new NotImplementedException();
            }

            public Task TaskMethodWithArguments(int i)
            {
                throw new NotImplementedException();
            }

            public Task<Action<int>> TaskMethodWithComplexReturnType()
            {
                throw new NotImplementedException();
            }

            public Task<Action<int>> TaskMethodWithComplexReturnTypeAndWithArguments(int i)
            {
                throw new NotImplementedException();
            }

            public Task<Action<T2>> TaskMethodWithComplexReturnTypeAndWithGenericArguments<T1, T2>(T1 t1, T2 t2)
            {
                throw new NotImplementedException();
            }

            public Task<Action<T2>> TaskMethodWithComplexReturnTypeAndWithWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict)
            {
                throw new NotImplementedException();
            }

            public Task TaskMethodWithGenericArguments<T1, T2>(T1 t1, T2 t2)
            {
                throw new NotImplementedException();
            }

            public Task<int> TaskMethodWithReturnType()
            {
                throw new NotImplementedException();
            }

            public Task<int> TaskMethodWithReturnTypeAndWithArguments(int i)
            {
                throw new NotImplementedException();
            }

            public Task<int> TaskMethodWithReturnTypeAndWithGenericArguments<T1, T2>(T1 t1, T2 t2)
            {
                throw new NotImplementedException();
            }

            public Task<int> TaskMethodWithReturnTypeAndWithWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict)
            {
                throw new NotImplementedException();
            }

            public Task TaskMethosWithComplexArguments<T1, T2>(Action<T1> action, IDictionary<T1, T2> dict)
            {
                throw new NotImplementedException();
            }
        }

        [TestMethod]
        public void ShouldCreateRefToServiceWithoutException()
        {
            var serviceRef = ServiceRef.Create<ITestService>(new TestService());

            Assert.IsNotNull(serviceRef);
        }

        public interface ITestServiceWithEvents
        {
            event EventHandler Event;
        }

        public class TestServiceWithEvents : ITestServiceWithEvents
        {
#pragma warning disable 0067

            public event EventHandler Event;

#pragma warning restore 0067
        }

        [TestMethod]
        public void ShouldCreateRefThrowExceptionWhenWrapTypesWithEvents()
        {
            Assert.ThrowsException<InvalidOperationException>(() => ServiceRef.Create<ITestServiceWithEvents>(new TestServiceWithEvents()));
        }

        [TestMethod]
        public void ShouldCreateRefThrowExceptionWhenWrapTypesThatNotAreInterfaces()
        {
            Assert.ThrowsException<InvalidOperationException>(() => ServiceRef.Create(new TestServiceWithEvents()));
        }

        [TestMethod]
        public void ShouldCreateRefReuseAlreadyCreatedWrapper()
        {
            var testService = new TestService();
            var serviceRef1 = ServiceRef.Create<ITestService>(testService);
            var serviceRef2 = ServiceRef.Create<ITestService>(testService);

            Assert.AreSame(serviceRef1, serviceRef2);
        }

        public interface ICounter
        {
            int Count { get; }

            void Increment();
        }

        private class Counter : ICounter
        {
            public int Count { get; protected set; }

            public void Increment()
            {
                Count += 1;
            }
        }

        [TestMethod]
        public void ConcurrentAccessToServiceActorShouldJustWork()
        {
            var counter = ServiceRef.Create<ICounter>(new Counter());

            Task.WaitAll(
                Enumerable
                .Range(0, 10)
                .Select(_ =>
                    Task.Factory.StartNew(() =>
                    {
                        counter.Increment();
                    }))
                .ToArray());

            Assert.AreEqual(10, counter.Count);
        }

        public interface IAdvancedCounter : ICounter
        {
            void Increment(int countToAdd);
        }

        public interface IDecrementerCounter : IAdvancedCounter
        {
            void Decrementer();
        }

        private class AdvancedCounter : Counter, IAdvancedCounter
        {
            public void Increment(int countToAdd)
            {
                Count += 10;
            }
        }
        private class AdvancedCounterWithDecrementer : Counter, IAdvancedCounter, IDecrementerCounter
        {
            public void Decrementer()
            {
                Count -= 1;
            }

            public void Increment(int countToAdd)
            {
                Count += 10;
            }
        }

        [TestMethod]
        public void ShouldCreateRefToServiceWithBaseInterfacesWithoutException()
        {
            Assert.IsNotNull(ServiceRef.Create<IAdvancedCounter>(new AdvancedCounter()));
            Assert.IsNotNull(ServiceRef.Create<IDecrementerCounter>(new AdvancedCounterWithDecrementer()));
        }

        public interface IInterfaceWithIncrement
        {
            void Increment<T>(T counter) where T : ICounter;
        }

        public class TypeWithIncrement : IInterfaceWithIncrement
        {
            public void Increment<T>(T counter) where T : ICounter
            {

            }
        }

        [TestMethod]
        public void ShouldCreateRefToServiceWithTemplateConstraintWithoutException()
        {
            Assert.IsNotNull(ServiceRef.Create<IInterfaceWithIncrement>(new TypeWithIncrement()));
        }

        [ServiceDomain(Domain.MyCustomDomain)]
        public interface IServiceA
        {
            void UseServiceB();
        }

        [ServiceDomain(Domain.MyCustomDomain)]
        public interface IServiceB
        {
            bool ServiceUsed { get; }
            void UseService();
        }

        private enum Domain
        {
            MyCustomDomain
        }

        private class ServiceA : IServiceA
        {
            private readonly IServiceB _serviceB;

            public ServiceA(IServiceB serviceB)
            {
                _serviceB = serviceB;
            }

            public void UseServiceB()
            {
                _serviceB.UseService();
            }
        }

        private class ServiceB : IServiceB
        {
            public bool ServiceUsed { get; private set; }

            public void UseService()
            {
                ServiceUsed = true;
            }
        }

        [TestMethod]
        public void ShouldCreateRefWithSingleDomain()
        {
            var serviceB = ServiceRef.Create<IServiceB>(new ServiceB());
            var serviceA = ServiceRef.Create<IServiceA>(new ServiceA(serviceB));

            serviceA.UseServiceB();
            Assert.IsTrue(serviceB.ServiceUsed);
        }
    }
}