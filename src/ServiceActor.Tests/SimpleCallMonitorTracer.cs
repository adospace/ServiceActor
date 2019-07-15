using System;

namespace ServiceActor.Tests
{
    public class SimpleCallMonitorTracer : IActionCallMonitor
    {
        public void EnterMethod(CallDetails callDetails)
        {
            Console.WriteLine($"Entering {callDetails}");
        }

        public void ExitMethod(CallDetails callDetails)
        {
            Console.WriteLine($"Exit {callDetails}");
        }
    }
}