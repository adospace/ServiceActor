using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
    }
}
