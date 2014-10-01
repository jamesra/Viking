<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Admin.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="AdminContent" runat="server">

    <h2>Create</h2>
    
    <% Html.RenderPartial("UserForm"); %>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdminTitle" runat="server">
Connectome Viz - Create User
</asp:Content>
