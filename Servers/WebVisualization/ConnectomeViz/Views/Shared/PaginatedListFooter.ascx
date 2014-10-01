<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<IPaginatedList>" %>

<% if (Model is IPaginatedList)
   {
       var model = Model as IPaginatedList;

       if (model.HasPreviousPage)
       {
           Response.Write("[ " + Html.RouteLink("Previous", new {  page = (model.PageIndex - 1) }) + " ]");
       }
       else
       {
           Response.Write("[ Previous ]");
       }

       if (model.HasNextPage)
       {
           Response.Write("[ " + Html.RouteLink("Next", new { page = (model.PageIndex + 1) }) + " ]");
       }
       else
       {
           Response.Write("[ Next ]");
       }
       
   }
       
%>