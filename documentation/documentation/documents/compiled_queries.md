<!--Title:Compiled Queries-->

Linq is easily one of the most popular features in .Net and arguably the one thing that other platforms strive to copy. We generally like being able
to express document queries in compiler-safe manner, but there is a non-trivial cost in parsing the resulting [Expression trees](https://msdn.microsoft.com/en-us/library/bb397951.aspx) and then using plenty of string concatenation to build up the matching SQL query. Fortunately, as of v0.8.10, Marten supports the concept of a _Compiled Query_ that you can use to reuse the SQL template for a given Linq query and bypass the performance cost of continuously parsing Linq expressions.

All compiled queries are classes that implement the `ICompiledQuery<TDoc, TResult>` interface shown below:

<[sample:ICompiledQuery]>

In its simplest usage, let's say that we want to find the first user document with a certain first name. That class would look like this:

<[sample:FindByFirstName]>

So a couple things to note in the class above:

1. The `QueryIs()` method returns an Expression representing a Linq query
1. `FindByFirstName` has a property (it could also be just a public field) called `FirstName` that is used to express the filter of the query

To use the `FindByFirstName` query, just use the code below:

<[sample:using-compiled-query]>

Or to use it as part of a batched query, this syntax:

<[sample:batch-query-with-compiled-queries]>


## How does it work?

The first time that Marten encounters a new type of `ICompiledQuery`, it executes the `QueryIs()` method and:

1. Parses the Expression just to find which property getters or fields are used within the expression as input parameters
1. Parses the Expression with our standard Linq support and to create a template database command and the internal query handler
1. Builds up an object with compiled Func's that "knows" how to read a query model object and set the command parameters for the query
1. Caches the resulting "plan" for how to execute a compiled query

On subsequent usages, Marten will just reuse the existing SQL command and remembered handlers to execute the query.


## What is supported?

To the best of our knowledge and testing, you may use any <[linkto:documentation/documents/linq;title=Linq feature that Marten supports]> within a compiled query. So any combination of:

* `Select()` transforms
* `First/FirstOrDefault()`
* `Single/SingleOrDefault()`
* `Where()`
* `OrderBy/OrderByDescending` etc.
* `Count()`
* `Any()`

At this point (v0.9), the only limitations are:

1. You cannot yet incorporate the <[linkto:documentation/documents/include;title=Include's]> feature with compiled queries, but there is an open
   [GitHub issue](https://github.com/JasperFx/marten/issues/309) you can use to track progress on adding this feature.
1. You cannot use the Linq `ToArray()` or `ToList()` operators. See the next section for an explanation of how to query for multiple results



## Querying for multiple results

To query for multiple results, you need to just return the raw `IQueryable<T>` as `IEnumerable<T>` as the result type. You cannot use the `ToArray()` or `ToList()` operators (it'll throw exceptions from the Relinq library if you try). As a convenience mechanism, Marten supplies these helper interfaces:

If you are selecting the whole document without any kind of `Select()` transform, you can use this interface:

<[sample:ICompiledListQuery-with-no-select]>

A sample usage of this type of query is shown below:

<[sample:UsersByFirstName-Query]>

If you do want to use a `Select()` transform, use this interface:

<[sample:ICompiledListQuery-with-select]>

A sample usage of this type of query is shown below:

<[sample:UserNamesForFirstName]>



## Querying for a single document

Finally, if you are querying for a single document with no transformation, you can use this interface as a convenience:

<[sample:ICompiledQuery-for-single-doc]>

And an example:

<[sample:FindUserByAllTheThings]>