﻿# RavenDB in a nutshell

RavenDB is a **transactional**, **open-source** [Document Database](what-is-a-document-database) written in **.NET**, and offering a **flexible data model** designed to address requirements coming from real-world systems. RavenDB allows you to build high-performance, low-latency applications quickly and efficiently.

Data in RavenDB is stored **schema-less** as JSON documents, and can be queried efficiently using **Linq** queries from your .NET code or using **RESTful** API using other tools. 

Internally, RavenDB makes use of indexes which are automatically created based on your usage, or created explicitly by the consumer.

RavenDB is built for **web-scale**, and offers **replication** and **sharding** support out-of-the-box.

## Using RavenDB

RavenDB consists of two parts - a server and a client. The server handles data storage and queries, and the client is what a consumer uses to communicate with the server. 

There are several ways to deploy RavenDB in your project:

* Starting the server (Raven.Server.exe) directly from a command prompt
 
* As a windows service on a Windows based machine

* Integrated with IIS

* Embedded client - embeds a RavenDB instance in your .NET application, web or desktop.

After you have a RavenDB server instance up and running, it is easy to connect to it using the RavenDB client to store and retrieve your data. RavenDB works with your [POCO](https://en.wikipedia.org/wiki/Plain_old_CLR_object)s, meaning it is super-easy to integrate it with your existing or newly-built application:

{CODE nutshell1@Intro\RavenDbInNutshell.cs /}

As you may have noticed, RavenDB uses the [Unit of Work pattern](https://martinfowler.com/eaaCatalog/unitOfWork.html), so all changes made before calling session.SaveChanges() will be persisted in the database in a single transaction.

## RavenDB Development cycle

There are two flavors of RavenDB available - the stable build, which is production ready, and the unstable build. Since we at Hibernating Rhinos make a public build out of every push, the unstable build is **not** recommended for production, although it is thoroughly tested before being made available.

New unstable builds are available daily, and sometimes more than once a day. Stable builds are released when we feel comfortable enough with recent changes we made - usually when enough time has passed and a handful of people have used the unstable builds. This is usually done on a biweekly basis.

Only stable builds are supported for production use. 

## Reporting bugs

Bugs should be reported in the mailing list: [https://groups.google.com/forum/#!forum/ravendb](https://groups.google.com/forum/#!forum/ravendb).

When reporting a bug, please include:

* The version and build of Raven that you used (can be the build number, or the git commit hash).
* What did you try to do?
* What happened?
* What did you expect to happen?
* Details about your environment.
* Details about how to reproduce this error.

*Bugs that comes with a way for us to reproduce the program locally (preferably a unit test) are usually fixed much more quickly.*

A list of outstanding bugs can be found here: [https://issues.hibernatingrhinos.com/](https://issues.hibernatingrhinos.com/).

## Licensing and support

RavenDB is released as open-source under the AGPL license. What that means is it is freely available, but if you want to use this with proprietary software, you **must** buy a [commercial license](https://ravendb.net/buy).

RavenDB has a very active [mailing list](https://groups.google.com/forum/#!forum/ravendb), where users and RavenDB developers attend all queries quickly and efficiently.
