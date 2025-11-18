using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Viking.Common;

namespace Viking.UI.WPF.PropertyPages
{
    internal static class PropertyPageRegistry
    {
        private static readonly Lazy<IReadOnlyList<PropertyPageDescriptor>> _descriptors =
            new Lazy<IReadOnlyList<PropertyPageDescriptor>>(LoadDescriptors, isThreadSafe: true);

        private static IReadOnlyList<PropertyPageDescriptor> LoadDescriptors()
        {
            Assembly assembly = typeof(PropertyPageRegistry).Assembly;
            var descriptorList = new List<PropertyPageDescriptor>();

            foreach (Type type in assembly.GetTypes())
            {
                if (!typeof(IPropertyPageView).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.IsAbstract)
                {
                    continue;
                }

                foreach (PropertyPageAttribute attribute in type.GetCustomAttributes<PropertyPageAttribute>())
                {
                    descriptorList.Add(new PropertyPageDescriptor(type, attribute));
                }
            }

            return descriptorList;
        }

        public static IEnumerable<PropertyPageDescriptor> GetPagesFor(object target)
        {
            if (target is null)
            {
                return Enumerable.Empty<PropertyPageDescriptor>();
            }

            Type targetRuntimeType = target.GetType();

            return _descriptors.Value
                .Where(d =>
                {
                    Type pageTarget = d.TargetType;
                    return pageTarget != null && pageTarget.IsAssignableFrom(targetRuntimeType);
                })
                .OrderBy(d => d.Priority)
                .ThenBy(d => d.PageType.FullName, StringComparer.Ordinal);
        }
    }
}

