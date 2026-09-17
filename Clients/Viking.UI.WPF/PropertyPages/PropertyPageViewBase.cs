using System.Windows.Controls;

namespace Viking.UI.WPF.PropertyPages
{
    /// <summary>
    /// Convenience base class so property pages only override the events they need.
    /// </summary>
    public abstract class PropertyPageViewBase : UserControl, IPropertyPageView
    {
        protected object Context { get; private set; }

        public abstract string Title { get; }

        public virtual System.Windows.FrameworkElement View => this;

        public void Initialize(object context)
        {
            Context = context;
            OnContextUpdated(context);
        }

        protected virtual void OnContextUpdated(object context)
        {
        }

        public virtual bool ValidateChanges() => true;

        public virtual void SaveChanges()
        {
        }

        public virtual void CancelChanges()
        {
        }
    }
}

