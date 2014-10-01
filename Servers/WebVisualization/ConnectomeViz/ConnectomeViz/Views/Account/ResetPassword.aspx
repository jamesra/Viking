<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	ResetPassword
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <p>
        Enter the details below to receive a secure password reset email. 
    </p>

    <%= Html.ValidationSummary("Incorrect Username/Email, please enter again") %>

    <% using (Html.BeginForm()) { %>
        <div>
            <fieldset>
                <legend>User Information</legend>
                <p>
                    <label for="username">Username:</label>
                    <%= Html.TextBox("username") %>
                    <%= Html.ValidationMessage("username") %>
                </p>
                <p>
                    <label for="email">Email:</label>
                    <%= Html.TextBox("email") %>
                    <%= Html.ValidationMessage("email") %>
                </p>
                <input type="submit" value="Retrieve" />
                </p>
            </fieldset>
        </div>
    <% } %>
</asp:Content>
