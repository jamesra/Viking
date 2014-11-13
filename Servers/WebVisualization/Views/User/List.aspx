<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Admin.Master" Inherits="System.Web.Mvc.ViewPage<PaginatedList<MembershipUser>>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="AdminContent" runat="server">

<h2>Current user list</h2>

<% var users = Model; %>

<% using(Html.BeginForm("List", "User")) { %>

<table class="info">
    <thead>
        <tr>
            <th rowspan="2"></th>
            <th rowspan="2">User Name</th>
            <th rowspan="2">Email</th>
            <th colspan="4">Dates</th>
            <th colspan="4">Status</th>
        </tr>
        <tr>
            <th>Created</th>
            <th>Last Login</th>
            <th>Last Active</th>
            <th>Last Lock</th>
            <th>Online</th>
            <th>Approved</th>
            <th>Locked</th>
            <th>Delete</th>
        </tr>
    </thead>
    <tbody>
        <% if (users != null && users.Count > 0)
           {
               foreach (MembershipUser user in users)
               { %>
        <tr>
            <td><%= Html.ActionLink("Details", "Details", "User", new { username=user.UserName }, null) %></td>
            <td><%= user.UserName %></td>
            <td><%= user.Email %></td>
            <td><%= user.CreationDate.ToShortDateString() %></td>
            <td><%= user.LastLoginDate.ToShortDateString() %></td>
            <td><%= user.LastActivityDate.ToShortDateString() %></td>
            <td><%= user.CreationDate > user.LastLockoutDate ? "" : user.LastLockoutDate.ToShortDateString() %></td>
            <td><%= Html.CheckBox("IsOnline", user.IsOnline, new { disabled="disabled" })%></td>
            <td><%= Html.CheckBox("IsApproved", user.IsApproved, new { disabled = "disabled" })%></td>
            <td><%= Html.CheckBoxItem("IsLockedOut" + user.UserName, "IsLockedOut", user.IsLockedOut, !user.IsLockedOut, user.UserName) %></td>
            <td><%= Html.CheckBoxItem("Delete" + user.UserName, "Delete", false, (String.Compare(user.UserName, Page.User.Identity.Name, true) == 0), user.UserName) %></td>
        </tr>
        <% }
           }
           else
           { %>
        <tr>
            <td colspan="11">No users present</td>
        </tr>
        <% } %>
    </tbody>
    
    <tfoot>
        <tr>
            
            <td colspan="6">
            <% Html.RenderPartial("PaginatedListFooter"); %>
            </td>
            <td colspan="5" class="align_right">
                <input type="submit" value="Delete" />
            </td>
        </tr>
    </tfoot>
</table>



<% } %>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdminTitle" runat="server">
Connectome Viz - User List
</asp:Content>

