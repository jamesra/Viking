<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<Tracker.Model.Structure>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Edit
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <h2>Edit</h2>

    <%= Html.ValidationSummary("Edit was unsuccessful. Please correct the errors and try again.") %>

    <% using (Html.BeginForm()) {%>

        <fieldset>
            <legend>Fields</legend>
            <p>
                <label for="ID">ID:</label>
                <%= Html.TextBox("ID", Model.ID) %>
                <%= Html.ValidationMessage("ID", "*") %>
            </p>
            <p>
                <label for="TypeID">TypeID:</label>
                <%= Html.TextBox("TypeID", Model.TypeID) %>
                <%= Html.ValidationMessage("TypeID", "*") %>
            </p>
            <p>
                <label for="Notes">Notes:</label>
                <%= Html.TextBox("Notes", Model.Notes) %>
                <%= Html.ValidationMessage("Notes", "*") %>
            </p>
            <p>
                <label for="Verified">Verified:</label>
                <%= Html.TextBox("Verified", Model.Verified) %>
                <%= Html.ValidationMessage("Verified", "*") %>
            </p>
            <p>
                <label for="Tags">Tags:</label>
                <%= Html.TextBox("Tags", Model.Tags) %>
                <%= Html.ValidationMessage("Tags", "*") %>
            </p>
            <p>
                <label for="Confidence">Confidence:</label>
                <%= Html.TextBox("Confidence", String.Format("{0:F}", Model.Confidence)) %>
                <%= Html.ValidationMessage("Confidence", "*") %>
            </p>
            <p>
                <label for="ParentID">ParentID:</label>
                <%= Html.TextBox("ParentID", Model.ParentID) %>
                <%= Html.ValidationMessage("ParentID", "*") %>
            </p>
            <p>
                <label for="Created">Created:</label>
                <%= Html.TextBox("Created", String.Format("{0:g}", Model.Created)) %>
                <%= Html.ValidationMessage("Created", "*") %>
            </p>
            <p>
                <label for="Label">Label:</label>
                <%= Html.TextBox("Label", Model.Label) %>
                <%= Html.ValidationMessage("Label", "*") %>
            </p>
            <p>
                <input type="submit" value="Save" />
            </p>
        </fieldset>

    <% } %>

    <div>
        <%=Html.ActionLink("Back to List", "Index") %>
    </div>

</asp:Content>

