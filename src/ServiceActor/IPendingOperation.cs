using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public interface IPendingOperation
    {
        void Complete();
    }
}
