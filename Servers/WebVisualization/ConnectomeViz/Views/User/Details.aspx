<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Admin.Master" Inherits="System.Web.Mvc.ViewPage<UserFormViewModel>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="AdminContent" runat="server">

    <h2>Details</h2>
    
    <% Html.RenderPartial("UserForm"); %>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdminTitle" runat="server">
Connectome Viz - User Details
</asp:Content>
