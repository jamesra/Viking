using System.Collections.ObjectModel;
using System.Linq;

namespace Viking.UI.WPF.PropertyPages
{
    internal sealed class PropertySheetViewModel
    {
        public ObservableCollection<PropertyPageHost> Pages { get; } = new ObservableCollection<PropertyPageHost>();

        public object Target { get; private set; }

        public void Initialize(object target)
        {
            Target = target;
            Pages.Clear();

            foreach (PropertyPageDescriptor descriptor in PropertyPageRegistry.GetPagesFor(target))
            {
                if (System.Activator.CreateInstance(descriptor.PageType) is IPropertyPageView view)
                {
                    view.Initialize(target);
                    Pages.Add(new PropertyPageHost(view, descriptor.Attribute));
                }
            }
        }

        public bool ValidateAll()
        {
            foreach (PropertyPageHost host in Pages)
            {
                if (!host.View.ValidateChanges())
                {
                    return false;
                }
            }

            return true;
        }

        public void SaveAll()
        {
            foreach (PropertyPageHost host in Pages)
            {
                host.View.SaveChanges();
            }
        }

        public void CancelAll()
        {
            foreach (PropertyPageHost host in Pages.Reverse())
            {
                host.View.CancelChanges();
            }
        }
    }
}

