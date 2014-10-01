<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<UserFormViewModel>" %>

<%

    MembershipUser user = Model.User;

    var user_name = user != null ? user.UserName : String.Empty;
    var email = user != null ? user.Email : String.Empty;
    var role_select = new MultiSelectList(Model.AllRoles, Model.UserRoles);
    var is_online = user != null ? user.IsOnline : false;
    var is_lockedOut = user != null ? user.IsLockedOut : false;
    var is_approved = user != null ? user.IsApproved : false;
    var last_lockOut_date = (user != null && user.CreationDate < user.LastLockoutDate) ? user.LastLockoutDate.ToShortDateString() : String.Empty;
    var last_active = user != null ? user.LastActivityDate.ToShortDateString() : String.Empty;
    var last_login = user != null ? user.LastLoginDate.ToShortDateString() : String.Empty;
    var created = user != null ? user.CreationDate.ToShortDateString() : String.Empty;
    created = Model.Mode == ViewMode.Create ? DateTime.Now.ToShortDateString() : created;
    var txt_username_attr = Model.Mode == ViewMode.Create ? null : new { disabled="disabled" };
    var txt_email_attr = new { disabled="disabled" };
    
%>

<% using (Html.BeginForm())
   { %>

<table class="info">
    <tbody>
    <tr>
        <th>User Name</th>
        <td><%= Html.TextBox("UserName", user_name, txt_username_attr)%></td>
        <th>Email</th>
        <td><%= Html.TextBox("Email", email, txt_email_attr)%></td>
    </tr>
    <tr>
        <th>Created</th>
        <td><%= created%></td>
        <th>Last login</th>
        <td><%= last_login%></td>
    </tr>
    <tr>
        <th>Last active</th>
        <td><%= last_active%></td>
        <th>Last lock</th>
        <td><%= last_lockOut_date%></td>
    </tr>
    <tr>
        <th>Online</th>
        <td><%= Html.CheckBoxItem("IsOnline", "IsOnline", is_online, true, user_name)%></td>
        <th>Locked</th>
        <td><%= Html.CheckBoxItem("IsLockedOut", "IsLockedOut", is_lockedOut, !is_lockedOut, user_name)%></td>
        
    </tr>
    <tr>
        <th>Approved</th>
        <% string[] roles = Roles.GetRolesForUser(Page.User.Identity.Name);
            if (roles.Count()>0 && roles[0]== "Admin") { %>
         <td><%= Html.CheckBoxItem("IsApproved", "IsApproved", is_approved, false, user_name)%></td>
        <%}
          else
          { %>
        <td><%= Html.CheckBoxItem("IsApproved", "IsApproved", is_approved, true, user_name)%></td>
        <%} %>
        <th>Delete</th>
        <td><%= Html.CheckBoxItem("Delete", "Delete", false, false, user_name)%></td>
    </tr>
    <% if (String.Compare(Page.User.Identity.Name, user_name, true) == 0)
       { %>
    <tr>
        <th colspan="4">Password</th>
    </tr>
    <tr>
        <td>Old Password</td>
        <td colspan="3">
            <%= Html.Password("OldPassword")%>
            <%= Html.ValidationMessage("OldPassword", "*") %>
        </td>
    </tr>
    <tr>
        <td>New Password</td>
        <td>
            <%= Html.Password("NewPassword") %>
            <%= Html.ValidationMessage("NewPassword", "*") %>
        </td>
        <td>Confirm Password</td>
        <td>
            <%= Html.Password("ConfirmPassword") %>
            <%= Html.ValidationMessage("ConfirmPassword", "*") %>
        </td>
    </tr>
    <% } %>
    <tr>
        <th colspan="4">Roles</th>
    </tr>
    <tr>
        <td colspan="4">
         <%= Html.MultiSelectList(role_select, "UserRoles", !Page.User.IsInRole("admin"))%>
        </td>
    </tr>
    </tbody>
    <tfoot>
    <tr>
        <td colspan="4"><input type="submit" value="Save" /></td>
    </tr>
    </tfoot>
</table>

<% } %>
