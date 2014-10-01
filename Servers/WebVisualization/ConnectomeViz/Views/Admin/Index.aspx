<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Admin.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="AdminContent" runat="server">

    <p>Connectome Viz - Account Management:</p>
    
    <p>Currently the following options are supported for your access &amp; role in the system</p>
    
    <table class="info">
        <tbody>
            <%
                if (Page.User.IsInRole("Admin"))
                {
                
                 %>
            <tr>
                <th><%= Html.ActionLink("Manage users", "List", "User")%></th>
                <td>Allows you to manage the user access levels, unlock their account etc.</td>
            </tr>
            <tr>
                <th><%= Html.ActionLink("Manage user roles", "List", "UserRole")%></th>
                <td>Allows you to manage the different roles for the application &amp; add/delete roles</td>
          </tr>
          <% } %>
          <tr>
            <th><%= Html.ActionLink("Manage your account", "Details", "User", new { username=Page.User.Identity.Name }, null)%></th>
            <td>Allows you to manage your account setting, password etc.</td>
          </tr>
          
        </tbody>
    </table>

    <br /><br />
    
                 <%
                     if (Page.User.IsInRole("Admin"))
                     {
                
                 %>
                <h3>Analytics Summary: <a href="http://www.google.com/analytics/" target="_blank">Access Comprehensive Google Analytics for "connectomes.utah.edu" >></a></h3>

                <iframe marginwidth="0"  marginheight="0" width="600" height="350" src="https://www.embeddedanalytics.com/reports/displayreport?reportcode=1nEODstTwH&chckcode=gadFFimgPdpJoDxTOmkYpr" type="text/html" frameborder="0" scrolling="no" title="connectomes.utah.edu - Analytics"></iframe>
                  
                  
                  
                  
                  
                  
                  <%} %>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdminTitle" runat="server">
Connectome Viz - Account Management
</asp:Content>
