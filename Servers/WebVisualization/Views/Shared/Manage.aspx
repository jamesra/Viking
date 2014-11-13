<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Manage
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <h2>Manage User Account: <b><%=Page.User.Identity.Name %></b></h2>
    
     <% string[] roles = Roles.GetRolesForUser(Page.User.Identity.Name);

        if (roles[0] == "Admin")
            Response.Write("<p> Welcome, " + Page.User.Identity.Name + " -Administrator, your user management console would be ready by spring break :)</p>");
        else
            Response.Write("<p> Welcome, " + Page.User.Identity.Name + " -User, your account management console would be ready by spring break :|"); 
            %>
    
  
    
   
    
    

</asp:Content>
