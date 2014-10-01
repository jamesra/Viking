<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Content
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <form id="form1" runat="server">

    <h2>Databases</h2>
<p>
    <asp:HyperLink ID="HyperLink1" runat="server" Target="&quot;/Home/">HyperLink</asp:HyperLink>
</p>

</form>

</asp:Content>
