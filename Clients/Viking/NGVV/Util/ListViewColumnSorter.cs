using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Viking.Common
{
    public static class GetTypeExtensions
    {
        public static HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(Byte),
            typeof(SByte),
            typeof(UInt16),
            typeof(UInt32),
            typeof(UInt64),
            typeof(Int16),
            typeof(Int32),
            typeof(Int64),
            typeof(Double),
            typeof(Single),
            typeof(Decimal)
        };

        public static bool IsNumericType(this Type t)
        {
            if (t == null)
                return false;

            if (GetTypeExtensions.NumericTypes.Contains(t))
                return true;

            if (Nullable.GetUnderlyingType(t) != null)
                return GetTypeExtensions.NumericTypes.Contains(Nullable.GetUnderlyingType(t));

            return false;
        }
    }

    /// <summary>
    /// Summary description for ListViewColumnSorter.
    /// </summary>
    public class ListViewColumnSorter : IComparer
    {
        public int SortIndex = 0;
        public bool AscendingSort = true;
        public Type ColumnType = null;

        public ListViewColumnSorter(int SortOnIndex, Type ColumnType)
        {
            this.SortIndex = SortOnIndex;
            this.ColumnType = ColumnType;
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

            if (ColumnType.IsNumericType())
            {
                if (SubA.Tag is IConvertible convA && SubB.Tag is IConvertible convB)
                {
                    Decimal ValA = convA.ToDecimal(null);
                    Decimal ValB = convB.ToDecimal(null);

                    return AscendingSort ? ValA.CompareTo(ValB) : ValB.CompareTo(ValA);
                }
            }

            string TextA;
            string TextB;

            if (SubA.Tag != null && SubB.Tag != null)
            {
                TextA = SubA.Tag.ToString();
                TextB = SubB.Tag.ToString();
            }
            else
            {
                TextA = SubA.Text;
                TextB = SubB.Text;
            }

            if (AscendingSort)
                return TextA.CompareTo(TextB);
            else
                return TextB.CompareTo(TextA);
        }
    }
}
