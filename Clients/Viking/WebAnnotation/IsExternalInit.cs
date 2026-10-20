#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Enables init-only properties when compiling net48. Newer target frameworks define this type themselves.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
