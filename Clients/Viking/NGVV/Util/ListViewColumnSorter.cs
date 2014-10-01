using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace Viking.Common
{
    /// <summary>
    /// Summary description for ListViewColumnSorter.
    /// </summary>
    public class ListViewColumnSorter : IComparer
    {
        public int SortIndex = 0;
        public bool AscendingSort = true;

        public ListViewColumnSorter(int SortOnIndex)
        {
            this.SortIndex = SortOnIndex;
        }

        int IComparer.Compare(object A, object B)
        {
            ListViewItem ItemA;
            ListViewItem ItemB;

            ItemA = A as ListViewItem;
            ItemB = B as ListViewItem;

            if (ItemA == null && ItemB == null)
                return 0;
            if (ItemA == null)
                return 1;
            if (ItemB == null)
                return -1;

            ListViewItem.ListViewSubItem SubA = ItemA.SubItems[SortIndex];
            ListViewItem.ListViewSubItem SubB = ItemB.SubItems[SortIndex];

            if (SubA == null && SubB == null)
                return 0;
            if (SubA == null)
                return 1;
            if (SubB == null)
                return -1;

            string TextA = SubA.ToString();
            string TextB = SubB.ToString();

            if (AscendingSort)
                return TextA.CompareTo(TextB);
            else
                return TextB.CompareTo(TextA);
        }
    }
}
