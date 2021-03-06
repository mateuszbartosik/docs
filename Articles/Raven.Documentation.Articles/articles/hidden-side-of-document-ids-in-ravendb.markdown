<div class="series-top-nav"><small class="series-name">Yet Another Bug Tracker: Article #2</small>
<a href="https://ravendb.net/news/use-cases/yabt-series"><small class="margin-top">Read more articles in this series ›</small></a></div>
<h1>Hidden side of document IDs in RavenDB</h1>
<small>by <a href="https://alex-klaus.com" target="_blank" rel="nofollow">Alex Klaus</a></small>

<div class="article-img figure text-center">
  <img src="images/hidden-side-of-document-ids-in-ravendb.jpg" alt="Practical modelling of the same database for a traditional SQL and a NoSQL. Comparison of the two approaches and their alignment with the DDD (Domain Driven Design)." class="img-responsive img-thumbnail">
</div>

{SOCIAL-MEDIA-LIKE/}

<p>At first glance, you don't need to pay special attention to <a href="https://ravendb.net/docs/article-page/latest/csharp/client-api/document-identifiers/working-with-document-identifiers">document IDs</a> in RavenDB. The default autogenerated <a href="https://ravendb.net/docs/article-page/latest/csharp/client-api/document-identifiers/working-with-document-identifiers#custom--semantic-ids">semantic IDs</a> (e.g. <code>users/1-A</code>) are good enough – robust, concise, human-readable, customisable. Of course, there are other options including GUID if you want to go fancy, but the <a href="https://github.com/ravendb/samples-yabt" target="_blank" rel="nofollow">YABT</a> (<em>"Yet Another Bug Tracker"</em>) sticks to the defaults and here are some hitches you may come across.</p>

### 1. Passing ID in the URL
<hr>

Consider the traditional URL format for updating/deleting/viewing an entity. For a `User` the format would look like `/api/users/{id}`, where the `{id}` must be a unique identifier.

*What would you pass as the `{id}`?*

Passing the document ID 'as is' would be suboptimal. For ID `users/1-A` the URL `/api/users/users/1-A` not only looks ugly, it also will derail the routing if passed unencoded. Encoded ID `/api/users/users%2F1-A` though functional, looks rather puzzling and doesn't bring much joy either.

#### 1.1. Masking the ID

<p><em>Oren Eini</em> <a href="https://ravendb.net/articles/avoiding-exposing-identifier-details-to-your-users">recommends to avoid exposing the ID</a> by masking it via encryption, so the URL would look like <code>/api/users/bPSPEZii22y5JwUibkQgUuXR3VHBDCbUhC343HBTnd1XMDFZMuok</code>. In <a href="https://ravendb.net/articles/avoiding-exposing-identifier-details-to-your-users">that blog post</a> he provides the code for using the AES encryption and then encoding to the Bitcoin format.</p>

The main benefit is disguising the pace of growing records in a collection that could be visible through sequential IDs (e.g. how many orders were created between events *A* and *B*). However, for enterprise applications it would mean sacrificing the user experience – the format is not human-readable and harder to manage (e.g. accidentally miss a character or two when selecting for copy-paste).

<p>This method is a bit faster than GUIDs due to lower impact on the <a href="https://en.wikipedia.org/wiki/B-tree" target="_blank" rel="nofollow">B-tree</a> index (though, Oren says: "<em>[impact] isn't going to be a major one before you get to 100 million records</em>"), but there can be a better approach.</p>

#### 1.2. Dropping the prefix

Another option would be decomposition of the ID by taking out the prefix (e.g. use `1-A` for `users/1-A`) that gives a more conventional URL like `/api/users/1-A`. Note that the `A` (a *node tag*) is an integral part of the ID.

It works when the prefix can be resolved from the context. I guess, it would be the case for most of enterprise apps. The apps need context to correctly present data on the front-end, to run data validation on incoming parameters, to apply appropriate business logic, etc. Usually, context for each request is well-known and it persists when the request hits the DB.

Once we know what kind of entity the short ID is for, the ID transition is trivial and here are two helper methods that mimic the *RavenDB* logic:

<pre>
    <code class="language-csharp" style="background:transparent;">
    // From `users/1-A` to `1-A`
    static string? GetShortId(this string? fullId) => fullId?.Split('/').Last();

    // From `1-A` to `users/1-A`
    static string GetFullId<T>(this IAsyncDocumentSession session, string shortId) where T : IEntity
    {
        // Pluralise the collection name (e.g. 'User' becomes 'Users', 'Person' becomes 'People')
        var pluralisedName = DocumentConventions.DefaultGetCollectionName(typeof(T));
        // Fix the later case - converts `Users` to `users`, `BacklogItems` to `backlogItems`
        var prefix = session.Advanced.DocumentStore.Conventions.TransformTypeCollectionNameToDocumentIdPrefix(pluralisedName);

        return $"{prefix}/{shortId}";
    }
    </code>
</pre>

<p>This approach is adopted in the <a href="https://github.com/ravendb/samples-yabt" target="_blank" rel="nofollow">YABT</a>.</p>

### 2. Exposing nested references
<hr>

It's getting more interesting when we need to process an entity containing nested references.

Take a sample *Backlog item* record from the *YABT* database:

<pre>
    <code class="language-json" style="background:transparent;">
    {
        "Status": "Active",
        "Title": "Malfunction at the Springfield Nuclear Power Plant",
        "Assignee": {
            "Id": "users/1-A",
            "Name": "N. Flanders",
            "FullName": "Ned Flanders"
        }
    }
    </code>
</pre>

The `Assignee` has a reference to a corresponding record in the `Users` collection. When we expose this backlog item (e.g. via API or on a web front-end), the reference must be consumable with minimum transformation to form a URL for navigating to the user.

<div class="margin-top-sm margin-bottom-sm">
    <img src="images/yabt2/1.png" class="img-responsive m-0-auto" alt="Malfunction at the Springfield Nuclear Power Plant"/>
</div>

Ideally, the recipient should simply concatenate `Assignee.Id` to the base URL and get a URL for the user's page (like `https://yabt.dev/users/1-A`) not thinking of complicated ID rules.

There are two options.

#### 2.1. Store processed IDs in the DB records

The most direct approach would be storing the reference ID in the form you present to the consumers. E.g.

<pre>
    <code class="language-json" style="background:transparent;">
    "Assignee": {
        "Id": "1-A",
        "Name": "N. Flanders",
        "FullName": "Ned Flanders"
    }   
    </code>
</pre>

It's viable and the main advantage – no need in post processing when such references are passed onto the consumer. The same as in the previous example, the domain logic can resolve the full record ID from the context (only a `User` can be the assignee).

<p>Another advantage can be a far-fetched one. What if your collection name may change somewhere down the track? It's not an unimaginable scenario when the <a href="" target="_blank" rel="nofollow">ubiquitous language</a> is evolving (in spite of your rigorous efforts to get it right at the start). To reflect the change, devs need to rename the collection (e.g. from <code>Users</code> to <code>Clients</code>)... and all the full references with prefixes (from <code>users/1-A</code> to <code>clients/1-A</code>). Storing partial references at least eliminates the last task.</p>

Nothing is perfect and the downsides would be

<ul>
    <li class="margin-top-xs">
        Can't easily use <code>Include()</code> to prevent excessive round trips for fetching referred records from the DB. <code>Include</code> requires a full document ID in the reference.
    <pre>
        <code class="language-csharp" style="background:transparent;">
    var ticket = session
                    .Include&lt;BacklogItem&gt;(x => x.Assignee.Id)
                    .Load("backlogItems/1-A");
        </code>
    </pre>
    </li>
    <li class="margin-top-xs">
        Some obscurity when looking at the record in the <em>RavenDB Studio</em>. It's not transparent what collection the reference is coming from and the <em>Studio</em> won't show a <a href="https://ravendb.net/docs/article-page/latest/csharp/studio/database/documents/document-view" target="_blank" rel="nofollow">list of related documents</a> for quick navigation.
        <img src="images/yabt2/2.png" class="img-responsive margin-top-sm margin-bottom-sm" style="margin-left:auto;margin-right:auto;" alt="Related studio screenshot"/>
    </li>
</ul>

#### 2.2. Process references before exposing

To have your ducks in a row at the DB level we can store full reference IDs and process them before exposing to the consumer. This way we avoid the downsides described above.

At its minimum, we can call `GetShortId()` (described above) on all the properties of the returned DTO that require ID processing... It would be a bit tedious and prone to human error. So we need helper methods.

Let's apply a constraint on all the classes with the ID property:

<pre>
    <code class="language-csharp" style="background:transparent;">
    interface IEntity
    {
        string Id { get; }
    }
    </code>
</pre>

To make it more generic, we avoid a setter for `ID` property (it's generated by Raven for entities and we should read it only). `IEntity` interface would be implemented by all the entities and references:

<pre>
    <code class="language-csharp" style="background:transparent;">
    public class User: IEntity
    {
        public string Id { get; }
        ...
    }
    public class UserReference: IEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
    }
    </code>
</pre>

Hence a more generic implementation would require <a href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/reflection" target="_blank" rel="nofollow">Reflection</a> to set a property value without a public setter:

<pre>
    <code class="language-csharp" style="background:transparent;">
    static T RemoveEntityPrefixFromId<T>(this T target) where T: IEntity
    {
        var newRefId = target?.Id?.GetShortId();    // The new ID value without the entity prefix

        if (newRefId == null)
            return target;
        var type = target!.GetType();
        
        var idProp = type.GetProperty(nameof(IEntity.Id));
        if (idProp == null)
            throw new NotImplementedException($"No '{nameof(IEntity.Id)}' property of '{type.Name}' type");

        idProp.SetValue(target, newRefId);

        return target;
    }
    </code>
</pre>

To sanitise the `Id` property on a DTO representing a Backlog Item:

<pre>
    <code class="language-csharp" style="background:transparent;">
    backlogItem.Assignee.RemoveEntityPrefixFromId()
    </code>
</pre>

To make the solution a bit niter and sanitise multiple properties at once we add two more helpers:

<pre>
    <code class="language-csharp" style="background:transparent">
    static void RemoveEntityPrefixFromIds&lt;T, TReference&gt;(this T target, params Expression&lt;Func&lt;T, TReference&gt;&gt;[] referenceMemberLambdas) where TReference : IEntity
    {
        foreach (var referenceMember in referenceMemberLambdas)
            target.RemoveEntityPrefixFromIds(referenceMember);
    }

    static void RemoveEntityPrefixFromIds&lt;T, TReference&gt;(this T target, Expression&lt;Func&lt;T, TReference&gt;&gt; referenceMemberLambda) where TReference : IEntity
    {
        if (   !(referenceMemberLambda.Body is MemberExpression referenceMemberSelectorExpression)
            || !(referenceMemberSelectorExpression.Member is PropertyInfo referenceProperty))
            return;
        
        // Read the current reference
        var referenceFunc = referenceMemberLambda.Compile();
        var reference = referenceFunc(target);

        if (reference == null)
            return;

        // Update the reference
        reference.RemoveEntityPrefixFromId();
    }
    </code>
</pre>

So before returning a DTO, we sanitise all the references by calling

<pre>
    <code class="language-csharp" style="background:transparent;">
    backlogItem.RemoveEntityPrefixFromIds(b => b.Assignee, b => b.AnotherReference)
    </code>
</pre>

And it's the main downside, the devs need to diligently call the method on the returning DTOs. The perfection at the DB level turns out to be a bit of a hustle at the domain services level.

Of course, it can be taken one step further – looping through all the properties of the DTO via recursion, but we'll stop here.

<p>It's for you to decide which approach is better for your project. <em>YABT</em> is using the last one to provide a better <em>RavenDB</em> experience. Check out more <a href="https://github.com/ravendb/samples-yabt/tree/master/back-end/Domain/Helpers" target="_blank" rel="nofollow">helpers</a> used in the <em>YABT</em>.</p>

### 3. Customising the ID
<hr>

To cover every aspect, let's show alternatives to the default document IDs.

#### 3.1. GUID

The simplest from the dev's perspective solution would be configuring RavenDB to generate [GUID document IDs](https://ravendb.net/docs/article-page/latest/csharp/server/kb/document-identifier-generation#guid). Such IDs don't have a prefix, so no problems with passing them around in the URL (it may look like `/api/users/b794686e-7bbf-42fd-a1fe-e4a94025735a`). This way we avoid issues described at the beginning and it's easy to use, just set the `ID` to `Guid.NewGuid()` or leave it as `string.Empty`:

<pre>
    <code class="language-csharp" style="background:transparent;">
    var user = new User
    {
        Id = string.Empty // database will create a GUID value for it
    };
    </code>
</pre>

The downsides are

<ul>
    <li class="margin-top-xs">it's considered not optimal for performance due to its randomness;</li>
    <li class="margin-top-xs">it's obscuring the name of the collection the reference is coming from;</li>
    <li class="margin-top-xs">too verbose for a neat UX.</li>
</ul>

#### 3.2. Customise the ID convention: identity part separator, collection name

Another way of avoiding those issues is to alter [the default ID convention](https://ravendb.net/docs/article-page/latest/csharp/client-api/configuration/identifier-generation/global). Though it would work for a small number of entities only.

Configure two parameters:

<ul>
    <li class="margin-top-xs">Set <code>IdentityPartsSeparator</code> to something neutral and URL-friendly (e.g. <code>-</code>);</li>
    <li class="margin-top-xs">Set <code>TransformTypeCollectionNameToDocumentIdPrefix</code> for shortening the collection prefix to 1-2 first letters (e.g. <code>TransformTypeCollectionNameToDocumentIdPrefix = name => name.FirstOrDefault();</code>.</li>
</ul>

Now, instead of `users/1-A` you get `u-1-A` that can be used in the URL (e.g. `api/v1/users/u-1-A`). The main downside – uniqueness of the ID prefix is on your shoulders now.

#### 3.3. Artificial ID

That one not a proper solution, but just a way to make IDs more expressive. By customising the [semantic IDs](https://ravendb.net/docs/article-page/latest/csharp/client-api/document-identifiers/working-with-document-identifiers#custom--semantic-ids) you can take it to another level and produce ID from the name (so-called [Artificial ID](https://ravendb.net/docs/article-page/latest/csharp/server/kb/document-identifier-generation#artificial-document-id)), so the user's record would look like

<pre>
    <code class="language-json" style="background:transparent;">
    {
        "Id": "users/userFlandersNerd",
        "Name": "N. Flanders",
        "FullName": "Ned Flanders"
    }
    </code>
</pre>

and then dropping the collection prefix in the reference will keep it as expressive as before (one of the problems indicated in *2.1*):

<pre>
    <code class="language-json" style="background:transparent;">
    "Assignee": {
        "Id": "userFlandersNerd",
        "Name": "N. Flanders",
    }
    </code>
</pre>

Though, it looks just slightly better and doesn't solve other concerns raised in *2.1.* And also it creates new problems:

<ul>
    <li class="margin-top-xs">enforcing uniqueness of the ID is now on your shoulders;</li>
    <li class="margin-top-xs">the name may change over time and you will face a dilemma of either accepting the out-of-sync ID or updating the ID along with all the references.</li>
</ul>

General recommendation would be to weigh carefully all pros and cons before embracing artificial document IDs.

That's it. There are ample options, but let's be reasonable, apply features wisely and avoid unnecessary complexity.

<p>Check out the full source code at our repository on GitHub - <a href="https://github.com/ravendb/samples-yabt" target="_blank" rel="nofollow">github.com/ravendb/samples-yabt</a> and let us know what you think. Stay tuned for the next articles in the <em>YABT</em> series.</p>

<a href="https://ravendb.net/news/use-cases/yabt-series"><h4 class="margin-top">Read more articles in this series</h4></a>
<div class="series-nav">
    <a href="https://ravendb.net/articles/nosql-data-model-through-ddd-prism">
        <div class="nav-btn margin-bottom-xs">
            <small>‹ Previous in the series</small>
            <strong class="previous">NoSQL Data Model Through DDD Prism</strong>
        </div>
    </a>
    <a href="https://ravendb.net/articles/dynamic-fields-for-indexing">
        <div class="nav-btn margin-bottom-xs">
            <small>Next in the series ›</small>
            <strong class="next">Dynamic Fields for Indexing</strong>
        </div>
    </a>
</div>