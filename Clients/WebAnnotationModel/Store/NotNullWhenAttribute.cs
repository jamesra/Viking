#if NETSTANDARD2_0 || NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for <c>NotNullWhenAttribute</c> on netstandard2.0 / .NET Framework.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        public bool ReturnValue { get; }
    }
}
#endif
