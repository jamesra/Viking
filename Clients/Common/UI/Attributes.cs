/******************************************************************************
 * Viking is Open Source under a Creative Commons License:
 * Attribution-NonCommercial-ShareAlike
 * http://creativecommons.org/licenses/by-nc-sa/3.0/legalcode
 * 
 * The reference to use for attribution is 
 * Anderson JR, et al 2010
 * The Viking viewer for connectomics: scalable multi-user annotation and
 * summarization of large volume data sets.
 * J Microscopy: [doi: 10.1111/j.1365-2818.2010.03402.x]
 * 
 * Summary: 
 * 1. You can use or change Viking any way you like 
 * 2. ... for non-commercial purposes.
 * 3. ... as long as you attibute the original development 
 * 4. ... you share your developments with us
 * 5. ... you distribute any derivative works under the same license provisions
 *  
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.UI
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ColumnAttribute : System.Attribute
    {
        public string ColumnName;

        public ColumnAttribute(string columnName)
        {
            this.ColumnName = columnName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ThisToManyRelationAttribute : System.Attribute
    {
        public ThisToManyRelationAttribute()
        { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ThisToOneRelationAttribute : System.Attribute
    {
        public ThisToOneRelationAttribute()
        { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ChildRelationshipAttribute : System.Attribute
    {
        public ChildRelationshipAttribute()
        { }
    }

    /// <summary>
    /// When placed on an object it indicates that the object should be included in the DataObjectTreeView
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TreeViewVisibleAttribute : System.Attribute
    {
        public TreeViewVisibleAttribute()
        {
        }
    }
}
