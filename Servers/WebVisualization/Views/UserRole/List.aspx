<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Admin.Master" Inherits="System.Web.Mvc.ViewPage<PaginatedList<string>>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="AdminContent" runat="server">
<h2>Current User Roles</h2>

<% using (Html.BeginForm())
   { %>

<table class="info">
    <caption>
        [ <%= Html.ActionLink("Create", "Create")%> ]
    </caption>
    <thead>
        <tr>
            <th>Name</th>
            <th>Delete</th>
        </tr>
    </thead>
    <tbody>
        <%
    var i = 0;
    foreach (var role in Model)
    { %>
        <tr>
            <td><%= role%></td>
            <td><%= Html.CheckBoxItem("role" + i.ToString(), "Delete", false, false, role)%></td>
        </tr>
        <% i++;
    } %>
    </tbody>
    <tfoot>
        <tr>
            <td><% Html.RenderPartial("PaginatedListFooter"); %></td>
            <td><input type="submit" value="Delete" /></td>
        </tr>
    </tfoot>
</table>

<% } %>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdminTitle" runat="server">
Connectome Viz - User Roles
</asp:Content>
