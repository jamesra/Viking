using System;

namespace WebAnnotationModel
{
    /// <summary>
    /// Where a query may read. ClientCache is 0 (cache-only equality checks). Server is 1.
    /// Do not OR them expecting two bits — ClientCache | Server == Server.
    /// </summary>
    [Flags]
    public enum QueryTargets
    {
        ClientCache,
        Server
    }
}
