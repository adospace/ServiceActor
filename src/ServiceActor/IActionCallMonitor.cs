using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public interface IActionCallMonitor
    {
        void EnterMethod(CallDetails callDetails);

        void ExitMethod(CallDetails callDetails);

        void UnhandledException(CallDetails callDetails, Exception ex);
    }
}
