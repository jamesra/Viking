using System;

namespace Viking.Common
{
    /// <summary>
    /// Attribute for specifying that a class provides a property page for a target type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class PropertyPageAttribute : Attribute
    {
        /// <summary>
        /// Target runtime type the property page can display (resolved lazily if provided via name).
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Optional assembly-qualified name if the target type cannot be directly referenced.
        /// </summary>
        public string TargetTypeName { get; }

        /// <summary>
        /// Order hint for displaying pages. Smaller values appear first.
        /// </summary>
        public int Priority { get; }

        public PropertyPageAttribute(Type targetType)
            : this(targetType, priority: 1)
        {
        }

        public PropertyPageAttribute(Type targetType, int priority)
        {
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            Priority = priority;
        }

        public PropertyPageAttribute(string targetTypeName, int priority = 1)
        {
            TargetTypeName = targetTypeName ?? throw new ArgumentNullException(nameof(targetTypeName));
            Priority = priority;
        }

        public Type ResolveTargetType()
        {
            if (TargetType != null)
            {
                return TargetType;
            }

            if (!string.IsNullOrWhiteSpace(TargetTypeName))
            {
                return Type.GetType(TargetTypeName, throwOnError: false);
            }

            return null;
        }
    }
}

