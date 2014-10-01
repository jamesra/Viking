#region references
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Web.Mvc.Html;
#endregion

namespace ConnectomeViz.Helpers
{
    public static class Extensions
    {
        #region menu extensions 

        public static string MenuItem(this HtmlHelper helper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            var format = "<li{0}>{1}</li>";
            var link = helper.ActionLink(linkText, actionName, controllerName, routeValues, htmlAttributes);
            return String.Format(format, "", link);
        }

        public static string MenuItem(this HtmlHelper helper, string url, string title, string name)
        {
            var format = "<li{2}><a href=\"{0}\" title=\"{1}\">{3}</a></li>";
            UrlHelper urlHelper = new UrlHelper(helper.ViewContext.RequestContext);
            var selected = String.Empty;
            
            // Setting the url
            if (url.StartsWith("~"))
            {
                url = urlHelper.Content(url);
            }

            // Setting the selected style
            if (helper.ViewContext.HttpContext.Request.Url.AbsolutePath.EndsWith(url))
            {
                selected = " class=\"selected\"";
            }

            return String.Format(format, url, title, selected, name);
        }

        #endregion

        #region html extensions 

        public static string ToAttributeList(this object list)
        {
            StringBuilder sb = new StringBuilder();
            if (list != null)
            {
                Hashtable attributeHash = GetPropertyHash(list);
                string resultFormat = "{0}=\"{1}\" ";
                foreach (string attribute in attributeHash.Keys)
                {
                    sb.AppendFormat(resultFormat, attribute.Replace("_", ""),
                        attributeHash[attribute]);
                }
            }
            return sb.ToString();
        }

        public static string ToAttributeList(this object list,
                                             params object[] ignoreList)
        {
            Hashtable attributeHash = GetPropertyHash(list);

            string resultFormat = "{0}=\"{1}\" ";
            StringBuilder sb = new StringBuilder();
            foreach (string attribute in attributeHash.Keys)
            {
                if (!ignoreList.Contains(attribute))
                {
                    sb.AppendFormat(resultFormat, attribute,
                        attributeHash[attribute]);
                }
            }
            return sb.ToString();
        }


        public static Hashtable GetPropertyHash(object properties)
        {
            Hashtable values = null;

            if (properties != null)
            {
                values = new Hashtable();
                PropertyDescriptorCollection props =
                    TypeDescriptor.GetProperties(properties);

                foreach (PropertyDescriptor prop in props)
                {
                    values.Add(prop.Name, prop.GetValue(properties));
                }
            }
            return values;
        }

        

        public static string HtmlInput(this HtmlHelper helper, object htmlAttributes)
        {
            return String.Format("<input {0} />", htmlAttributes.ToAttributeList());
        }

        public static string CheckBoxItem(this HtmlHelper helper, string id, string name, bool isChecked, bool disabled, string value)
        {
            return helper.CheckBoxItem(id, name, isChecked, disabled, value, String.Empty);
        }

        public static string CheckBoxItem(this HtmlHelper helper, string id, string name, bool isChecked, bool disabled, string value, string text)
        {
            var format = "<input type=\"checkbox\" id=\"{0}\" name=\"{1}\"{2}{3} value=\"{4}\">{5}";
            var s_checked = isChecked ? " checked=\"checked\"" : String.Empty;
            var s_disabled = disabled ? " disabled=\"disabled\"" : String.Empty;
            return String.Format(format, id, name, s_checked, s_disabled, value, text);
        }

        public static string MultiSelectList(this HtmlHelper helper, MultiSelectList list, string name, bool disabled)
        {
            var output = new StringBuilder();
            var i = 0;
            foreach (var item in list)
            {
                output.Append(helper.CheckBoxItem(name + "_" + i.ToString(), name, item.Selected, disabled, String.IsNullOrEmpty(item.Value) ? item.Text : item.Value, item.Text));
                i++;
            }

            return output.ToString();
        }

        #endregion

        #region validations 

        public static bool StringHasValue(string value)
        {
            return (!String.IsNullOrEmpty(value) && !String.IsNullOrEmpty(value.Trim()));
        }

        public static bool IsValidEmailId(string emailId)
        {
            var email_exp = new Regex(@"[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?");
            return email_exp.IsMatch(emailId);
        }

        #endregion
    }
}
