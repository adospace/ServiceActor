using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ServiceActor
{
    public class CallContext
    {
        public const string ThreadLocalSlotKey = "ServiceActor_CallContext";

        private ImmutableStack<ActionQueue> _callStack = ImmutableStack<ActionQueue>.Empty;

        public bool CanPush(ActionQueue actionQueue, bool allowReentrantCalls)
        {
            var actionQueueInStack = _callStack.Any(_ => _ == actionQueue);// &&
                                                                      //_callStack.Peek() != actionQueue;
            if (!allowReentrantCalls && actionQueueInStack)
            {
                throw new InvalidOperationException($"Reentrant call detected {string.Join(">>", _callStack.Select(_ => _.ExecutingInvocationItem))}");
            }

            if (actionQueueInStack)
                return false;

            return true;
        }

        public void Push(ActionQueue actionQueue)
        {
            _callStack = _callStack.Push(actionQueue);
        }

        public void Pop(ActionQueue actionQueue)
        {
            _callStack = _callStack.Pop(out ActionQueue removeActionQueue);

            if (removeActionQueue != actionQueue)
            {
                throw new InvalidOperationException();
            }
        }

        public static CallContext GetOrCreateCurrent()
        {
            var contextSlot = System.Threading.Thread.GetNamedDataSlot(ThreadLocalSlotKey);
            var currentContext = (CallContext)System.Threading.Thread.GetData(contextSlot);
            if (currentContext == null)
            {
                currentContext = new CallContext();
                System.Threading.Thread.SetData(contextSlot, currentContext);
                return currentContext;
            }
            else
            {
                return new CallContext() { _callStack = currentContext._callStack };
            }
        }

        public static void SetCurrent(CallContext callContext)
        {
            var contextSlot = System.Threading.Thread.GetNamedDataSlot(ThreadLocalSlotKey);
            System.Threading.Thread.SetData(contextSlot, callContext);
        }

        //public static void ResetCurrentContextIfEmpty()
        //{
        //    var contextSlot = System.Threading.Thread.GetNamedDataSlot(ThreadLocalSlotKey);
        //    var currentContext = (CallContext)System.Threading.Thread.GetData(contextSlot);
        //    if (currentContext != null)
        //    {
        //        if (currentContext._callStack.Count == 0)
        //        {
        //            System.Threading.Thread.SetData(contextSlot, null);
        //        }
        //    }
        //}
    }
}
