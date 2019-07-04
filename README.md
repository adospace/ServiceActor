# ServiceActor
This is a .NET library that implements a new way to handle concurrent access to services based on Actor model

[![Build status](https://ci.appveyor.com/api/projects/status/ip4lahn844gqfa8d?svg=true)](https://ci.appveyor.com/project/adospace/serviceactor) ![Nuget](https://img.shields.io/nuget/v/serviceactor.svg)

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
