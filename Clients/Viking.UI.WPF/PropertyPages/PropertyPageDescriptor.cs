using System;
using Viking.Common;

namespace Viking.UI.WPF.PropertyPages
{
    internal sealed class PropertyPageDescriptor
    {
        public PropertyPageDescriptor(Type pageType, PropertyPageAttribute attribute)
        {
            PageType = pageType ?? throw new ArgumentNullException(nameof(pageType));
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        public Type PageType { get; }

        public PropertyPageAttribute Attribute { get; }

        public int Priority => Attribute.Priority;

        public Type TargetType => Attribute.ResolveTargetType();
    }
}

