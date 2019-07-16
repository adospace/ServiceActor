using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor.Tests
{
    [TestClass]
    public class SetupAssemblyInitializer
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            ServiceRef.ClearCache();
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
        }

    }

}
