using System;
using System.Web.Mvc;

namespace ConnectomeViz.Helpers
{
    public static class TabControl
    {
        public const String DEFAULT_CSS_CLASS = "ui-tabs-selected ui-state-active";

        public static string ActiveTabClass(this HtmlHelper helper, string targetController, string targetAction)
        {
            return ActiveTabClass(helper, targetController, targetAction == null ? null : new string[] { targetAction });
        }

        public static string ActiveTabClass(this HtmlHelper helper, string targetController, string[] targetActions)
        {
            return ActiveTabClass(helper, targetController, targetActions, DEFAULT_CSS_CLASS);
        }

        public static string ActiveTabClass(this HtmlHelper helper, string targetController, string[] targetActions, string cssClass)
        {
            // CSS class
            string css = string.Empty;

            // Get the controller and action for the view
            string controller = helper.ViewContext.RouteData.GetRequiredString("controller").ToLower();
            string action = helper.ViewContext.RouteData.GetRequiredString("action").ToLower();
            string[] targetActionsLower = Array.ConvertAll(targetActions, delegate(string s) { return s.ToLower(); });

            // If the targets match what's in the view context, set the active tab class
            if ((targetController.ToLower().Equals(controller) && targetActions == null)
                || (targetController.ToLower().Equals(controller) && Array.IndexOf(targetActionsLower, action) > -1))
            {
                css = cssClass;
            }

            return css;
        }
    }
}