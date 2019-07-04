using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Reflection;

namespace ServiceActor
{
    public static class ServiceRef
    {
        private class Globals
        {
            public object ObjectToWrap { get; set; }

            public ActionQueue ActionQueueToShare { get; set; }
        }

        public static T Create<T>(T objectToWrap)
        {
            Console.WriteLine(new ServiceActorWrapperTemplate(typeof(T)).TransformText());

            var asyncActorCode = new ServiceActorWrapperTemplate(typeof(T)).TransformText();
            return CSharpScript.EvaluateAsync<T>(
                asyncActorCode,
                options: ScriptOptions.Default.AddReferences(
                    Assembly.GetExecutingAssembly(), 
                    typeof(T).Assembly, 
                    typeof(Nito.AsyncEx.AsyncAutoResetEvent).Assembly),
                globals: new Globals { ObjectToWrap = objectToWrap, ActionQueueToShare = new ActionQueue() }
                ).Result;
        }
    }
}
