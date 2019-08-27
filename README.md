![project icon](Assets/icon-64.png)

# Imperfect Dollop
This is a .net library that helps use create distributed in-memory cache.

The library targets [.net core 2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2)

Can be found on [NuGet.org](https://www.nuget.org/packages?q=Paden.ImperfectDollop):
* [Paden.ImperfectDollop](https://www.nuget.org/packages/Paden.ImperfectDollop/) - Base library; provides in-memory cache functionality
* [Paden.ImperfectDollop.Broker.RabbitMQ](https://www.nuget.org/packages/Paden.ImperfectDollop.Broker.RabbitMQ/) - RabbitMQ broker; provides data synchronization functionality over RabbitMQ server
* [Paden.ImperfectDollop.Broker.Redis](https://www.nuget.org/packages/Paden.ImperfectDollop.Broker.Redis/) - Redis broker; provides data synchronization functionality over Redis server
* [Paden.ImperfectDollop.Prometheus](https://www.nuget.org/packages/Paden.ImperfectDollop.Prometheus/) - Prometheus endpoint manager; repository information provider for [prometheus-net](https://www.nuget.org/packages/prometheus-net/)


## What this library is *not*

* Standalone server
* ACID compliant
 
## What this library *is*
* Really fast
* Data source connection fault tolerant
* Able to acquire data from sibling nodes

## Idea

We all have the requirements of caching commonly accessed data. Usualy we do this by storing the data to some caching server (like Redis) and read it from all the nodes. For most use cases this may be the best approach, and I strongly recommend using that if it fits all of your requirements.

From time to time this is not enough for one of the folowing reasons:
* Connection to caching server is slow, unreliable and / or expensive
* System needs to continue working while cache server is down (interruption or maintenance)
* Caching server does not support complex data operations that are required
* We really don't want to have it over there; we want to have it in our memory

## Sample project architecture
...

## Plans for next releases
* Entity Framework Core provider