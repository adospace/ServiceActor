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

        public void UnhandledException(CallDetails callDetails, Exception ex)
        {
            Console.WriteLine($"Unhandled exception {callDetails}:{Environment.NewLine}{ex}");
        }
    }
}