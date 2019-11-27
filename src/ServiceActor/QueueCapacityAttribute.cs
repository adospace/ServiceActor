using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceActor
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class QueueCapacityAttribute : Attribute
    {
        public QueueCapacityAttribute(int capacity = -1)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }
    }
}
