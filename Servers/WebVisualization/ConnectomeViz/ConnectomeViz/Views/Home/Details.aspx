<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<Tracker.Model.Structure>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Details
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <h2>Details</h2>

    <fieldset>
        <legend>Fields</legend>
        <p>
            ID:
            <%= Html.Encode(Model.ID) %>
        </p>
        <p>
            TypeID:
            <%= Html.Encode(Model.TypeID) %>
        </p>
        <p>
            Notes:
            <%= Html.Encode(Model.Notes) %>
        </p>
        <p>
            Verified:
            <%= Html.Encode(Model.Verified) %>
        </p>
        <p>
            Tags:
            <%= Html.Encode(Model.Tags) %>
        </p>
        <p>
            Confidence:
            <%= Html.Encode(String.Format("{0:F}", Model.Confidence)) %>
        </p>
        <p>
            ParentID:
            <%= Html.Encode(Model.ParentID) %>
        </p>
        <p>
            Created:
            <%= Html.Encode(String.Format("{0:g}", Model.Created)) %>
        </p>
        <p>
            Label:
            <%= Html.Encode(Model.Label) %>
        </p>
    </fieldset>
    <p>

        <%=Html.ActionLink("Edit", "Edit", new { id=Model.ID }) %>
        
    </p>

</asp:Content>

