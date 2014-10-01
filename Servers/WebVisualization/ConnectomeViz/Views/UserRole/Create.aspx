<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Admin.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="AdminContent" runat="server">

    <h2>Create User Role</h2>
    
    <% using (Html.BeginForm())
       { %>
    <table class="info"> 
        <tbody>
            <tr>
                <th><label for="Role">Role</label></th>
                <td>
                    <%= Html.TextBox("Role")%>
                    <%= Html.ValidationMessage("Role", "*")%>
                </td>
            </tr>
        </tbody>
        <tfoot>
            <tr>
                <td colspan="2" class="align_right"><input type="submit" value="Save" /></td>
            </tr>
        </tfoot>
    </table>
    <% } %>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdminTitle" runat="server">
Connectome Viz - Create User role
</asp:Content>
