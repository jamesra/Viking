#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Enables init-only properties and records when compiling netstandard2.0. Newer target frameworks define this type themselves.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
