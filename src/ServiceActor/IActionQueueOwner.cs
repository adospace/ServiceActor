using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    public interface IActionQueueOwner
    {
        ActionQueue ActionQueue { get; }
    }
}
