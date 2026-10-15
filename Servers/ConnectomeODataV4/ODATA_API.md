# ConnectomeODataV4 – StructureFreshness

Batched cache-key query for mesh/cache clients: for each requested structure, the newest
`LastModified` and the location count over the structure **and its descendants**.
Read-only. Unknown IDs are omitted from the result.

Result (collection of complex type `StructureFreshness`):

| Property          | Type  | Meaning |
|-------------------|-------|---------|
| `StructureID`     | long  | Requested root structure |
| `LastModified`    | UTC datetime | Max of `Structure.LastModified` and `Location.LastModified` over the set |
| `AnnotationCount` | long  | Number of locations over the same set |

## Depth

| `Depth`   | Set aggregated |
|-----------|----------------|
| omitted   | Structure + direct children (`ParentID = StructureID`) |
| `0`       | The structure only |
| `n > 0`   | Structure + descendants down to `n` levels |
| `-1`      | Whole subtree (unlimited). Cycles in `ParentID` are tolerated. |

## Three ways to call it

OData v4 functions are invoked with GET only and cannot take a request body, so the
request length is bounded by HTTP.sys / IIS. The service offers three forms.

### 1. Inline function call – short lists (up to ~30 IDs)

    GET /StructureFreshness(IDs=[1,2,3])
    GET /StructureFreshness(IDs=[1,2,3],Depth=-1)

HTTP.sys limits one URL **path segment to 260 characters**, which is roughly 30–60 IDs.
Longer lists get `400 Bad Request – Invalid URL` before reaching the service.

### 2. Parameter alias – medium lists (up to ~400 IDs)

The OData spec lets a parameter value be moved into the query string with an alias:

    GET /StructureFreshness(IDs=@ids,Depth=-1)?@ids=[1,2,3,...]

The path segment stays short; the limit becomes IIS `requestFiltering/maxQueryString`
(default **2048 characters**, a longer query returns `404`). Raise `maxQueryString` /
`maxUrl` in `system.webServer/security/requestFiltering` if you need more.

### 3. Action with POST body – any size

    POST /StructureFreshnessBatch
    Content-Type: application/json

    {"IDs":[1,2,3,...],"Depth":-1}

`Depth` is optional (same defaults as above). The response is identical to the function:
`{"@odata.context":"…","value":[{"StructureID":…,"LastModified":"…","AnnotationCount":…}]}`.
Tested with 5000 IDs. Prefer this form for anything beyond a few dozen IDs.

Note: the spec reserves actions for operations with side effects; `StructureFreshnessBatch`
is an action only because functions cannot carry a body. It does not write anything.
`StructureFreshnessBatch` appears in `$metadata` as an `Action`, `StructureFreshness` as a `Function`.

## Other things the spec allows (not implemented)

* `POST /$batch` – wraps ordinary GET requests (including the function call) in a JSON body,
  so the inner URL should not be subject to the HTTP.sys path-segment limit (not tested here).
  Enabled on this service.
* OData 4.01 `POST /$query` – not supported by Microsoft.AspNet.OData 7.x.
