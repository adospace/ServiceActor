# ServiceActor
This is a .NET library that implements a basic actor model to handle concurrent access to services

[![Build status](https://ci.appveyor.com/api/projects/status/ip4lahn844gqfa8d?svg=true)](https://ci.appveyor.com/project/adospace/serviceactor) [![Nuget](https://img.shields.io/nuget/v/serviceactor.svg)](https://www.nuget.org/packages/ServiceActor)

# 

### What is ServiceActor?
ServiceActor goal is to simplify multithreading/concurrent access to services using an Actor-like model without requiring the project to be completely rewritten or designed from the start using an Actor model paradigm. 

### What ServiceActor is not
ServiceActor doesn't aim to be a full Actor model like Akka.Net or Orleans: actually it can be even compared to those frameworks! So if you're looking for a pure Actor model infrastructure I would point you to one of those frameworks.

### Main ServiceActor goals
- Makes concurrent access to services lock-free and straightforward
- Should not require complete rewrite of your service code
- Should be "pluggable": i.e. should be possibile to use ServiceActor only for a portion of your system
- Should be async/await friendly
- Should be easily adopted in projects that deliver services using a DI frameworks
- Should be possible to gracefully handle exceptions in services managing custom recover policies (not yet realized)

### Main ServiceActor non-goals
- Services should be accessible in the same process memory (not like pure Actors that are instead location unaware)

### Example of usage
For an example take a simple counter service like this:

```c#
public interface ICounter
{
    int Count { get; }

    void Increment();
}
```

and this is the initial service implementation:
```c#
private class Counter : ICounter
{
    public int Count { get; private set; }

    public void Increment()
    {
        Count += 1;
    }
}
```
I'm sure you already noticed that implementation is not thread-safe: i.e. `Increment` write private state without any lock.
This the test of (not-)multithreding behavior of `Counter` class:
```c#
ICounter counter = new Counter();//or get from DI 

Task.WaitAll(
    Enumerable
    .Range(0, 10)
    .Select(_ =>
        Task.Factory.StartNew(() =>
        {
            counter.Increment();
        }))
    .ToArray());

Assert.AreEqual(10, counter.Count);
```
If you run this code you'll find that easily last assertion fails: this is because multiple threads access the `Increment` and the `Count` property is incremented without a lock.

Now the usual way to fix this issue is to just place a lock inside the `Increment` method:
```c#
private class Counter : ICounter
{
    public int Count { get; private set; }

    public void Increment()
    {
        lock(this)
            Count += 1;
    }
}
```

If you're here, you probably knows that locking resources has some disadvantages expecially in large projects with many services and many threads running on. 
Actually there are a lot of articles on internet explaining why in some scenarios locking resources is a recipe for a disaster but I also know that in many cases expert programmers can create high-prefomance and perfectly working code. 
This is why I created (and use this library in large projects) with the intent to create a tool to facilitate multithreading code and not a one-way design pattern. 
What Serviceactor tries to do is to somewhat introduce the actor model paradigm in this scenario without requiring a complete rewrite or design of the services implementation classes.
Using ServiceActor final code would be almost the same but without locks:
```c#
private class Counter : ICounter
{
    public int Count { get; private set; }

    public void Increment()
    {
        Count += 1;
    }
}
```
```c#
var counter = ServiceRef.Create<ICounter>(new Counter() /*or get from DI*/);

Task.WaitAll(
    Enumerable
    .Range(0, 10)
    .Select(_ =>
        Task.Factory.StartNew(() =>
        {
            counter.Increment();
        }))
    .ToArray());

Assert.AreEqual(10, counter.Count);
```
Running this code you'll find that everything will just work.
### How ServiceActor works?
ServiceActor creates at runtime (on the fly) a wrapper for the service interface that implements a message queue for the actual service.
Calling a method or a property of the wrapper, it actually "enqueue" the call in a queue for the service. Each invocation is than executed one after the other so that is "guaranteed" that only one thread access the method at time.
For the above simple service ServiceActor enqueue all the calls coming from different threas to the `Increment` method and executes the method one per time.
