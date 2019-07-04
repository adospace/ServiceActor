using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceActor.Tests
{
    [TestClass]
    public class CodeGenerationTests
    {
        [TestMethod]
        public void ShouldGenerateCodeForSimpleTypes()
        {
            Assert.AreEqual("System.Int32", typeof(int).GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForAssemblyTypes()
        {
            Assert.AreEqual("System.IDisposable", typeof(IDisposable).GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForGenericTypes()
        {
            Assert.AreEqual("System.Collections.Generic.IDictionary<System.String, System.String>", typeof(IDictionary<string, string>).GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForGenericTypesWithAssemblyArguments()
        {
            Assert.AreEqual("System.Collections.Generic.IDictionary<System.String, System.Action<System.String>>", typeof(IDictionary<string, Action<string>>).GetTypeReferenceCode());
        }

        private interface IMyIntType
        {
        }

        [TestMethod]
        public void ShouldGenerateCodeForClassNestedTypes()
        {
            Assert.AreEqual("ServiceActor.Tests.CodeGenerationTests.IMyIntType", typeof(IMyIntType).GetTypeReferenceCode());
        }

        private interface IMyIntType<TKey, TValue>
        {
            IDictionary<T1, T2> Dict<T1, T2>();

            IDictionary<T1, Action<T2>> Dict2<T1, T2>();

            IDictionary<T1, Action<T2>> Dict3<T1, T2>(ref Action<T1> p1, out T2 p2);
        }

        [TestMethod]
        public void ShouldGenerateCodeForClassNestedTypesWithGenericArguments()
        {
            Assert.AreEqual("ServiceActor.Tests.CodeGenerationTests.IMyIntType<System.String, System.String>", typeof(IMyIntType<string, string>).GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForClassWithGenericArgumentsWithoutFullName()
        {
            Assert.AreEqual("System.Collections.Generic.IDictionary<T1, T2>", typeof(IMyIntType<string, string>).GetMethods()[0].ReturnType.GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForClassWithComplexGenericArgumentsWithoutFullName()
        {
            Assert.AreEqual("System.Collections.Generic.IDictionary<T1, System.Action<T2>>", typeof(IMyIntType<string, string>).GetMethods()[1].ReturnType.GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForTypeWithRef()
        {
            Assert.AreEqual("ref System.Action<T1>", typeof(IMyIntType<string, string>).GetMethods()[2].GetParameters()[0].ParameterType.GetTypeReferenceCode());
        }

        [TestMethod]
        public void ShouldGenerateCodeForTypeWithOut()
        {
            Assert.AreEqual("out T2", typeof(IMyIntType<string, string>).GetMethods()[2].GetParameters()[1].GetTypeReferenceCode());
        }

        private interface ITestItfWithMethods
        {
            void TestMethod();

            void TestMethod(int i, Action action);

            void TestMethod<T1, T2>(T1 i, IDictionary<string, Action<T1>> dict);

            Task<T3> TestMethod<T1, T2, T3>(T1 i, IDictionary<string, Action<T1>> dict, out T3 t3);
        }

        [TestMethod]
        public void ShouldGenerateDeclarationCodeForSimpleMethod()
        {
            Assert.AreEqual("void TestMethod()", typeof(ITestItfWithMethods).GetMethods()[0].GetMethodDeclarationCode());
        }

        [TestMethod]
        public void ShouldGenerateInvocationCodeForSimpleMethod()
        {
            Assert.AreEqual("TestMethod()", typeof(ITestItfWithMethods).GetMethods()[0].GetMethodInvocationCode());
        }

        [TestMethod]
        public void ShouldGenerateDeclarationCodeForSimpleMethodWithParameters()
        {
            Assert.AreEqual("void TestMethod(System.Int32 i, System.Action action)", typeof(ITestItfWithMethods).GetMethods()[1].GetMethodDeclarationCode());
        }

        [TestMethod]
        public void ShouldGenerateInvocationCodeForSimpleMethodWithParameters()
        {
            Assert.AreEqual("TestMethod(i, action)", typeof(ITestItfWithMethods).GetMethods()[1].GetMethodInvocationCode());
        }

        [TestMethod]
        public void ShouldGenerateDeclarationCodeForGenericMethodWithParameters()
        {
            Assert.AreEqual("void TestMethod<T1, T2>(T1 i, System.Collections.Generic.IDictionary<System.String, System.Action<T1>> dict)", typeof(ITestItfWithMethods).GetMethods()[2].GetMethodDeclarationCode());
        }

        [TestMethod]
        public void ShouldGenerateInvocationCodeForGenericMethodWithParameters()
        {
            Assert.AreEqual("TestMethod<T1, T2>(i, dict)", typeof(ITestItfWithMethods).GetMethods()[2].GetMethodInvocationCode());
        }

        [TestMethod]
        public void ShouldGenerateDeclarationCodeForComplexGenericMethodWithParameters()
        {
            Assert.AreEqual("System.Threading.Tasks.Task<T3> TestMethod<T1, T2, T3>(T1 i, System.Collections.Generic.IDictionary<System.String, System.Action<T1>> dict, out T3 t3)", typeof(ITestItfWithMethods).GetMethods()[3].GetMethodDeclarationCode());
        }

        [TestMethod]
        public void ShouldGenerateInvocationCodeForComplexGenericMethodWithParameters()
        {
            Assert.AreEqual("TestMethod<T1, T2, T3>(i, dict, t3)", typeof(ITestItfWithMethods).GetMethods()[3].GetMethodInvocationCode());
        }
    }
}