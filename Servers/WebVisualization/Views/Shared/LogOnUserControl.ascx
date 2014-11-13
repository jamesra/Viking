<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl" %>
<%
    if (Request.IsAuthenticated) {
%>
        Hello, <b><%= Html.Encode(Page.User.Identity.Name) %></b>, you may
        [ <%= Html.ActionLink("Log Off", "LogOff", "Account") %> ] after use
<%
    }
    else {
%> 
       Please <%= Html.ActionLink("Log On", "LogOn", "Account") %> to use the website or <%= Html.ActionLink("Register", "Register","Account") %> to create a new account
<%
    }
%>

<%--<br /><br />
<b>Info:</b><br />
Use tabs at the top right to choose which type of visualization you'd want to see.As our database is growing, <br />
the visualizations work only for cells. For example, try 180, 422, 514, 514, 476 and other cell numbers you see <br />
in "Circuit Viz". <br /><br />

The website might seem slow (especially "Network Viz") as the webserivce is being maintained.<br /><br />

VikingPlot 3D is still a work in progress. <br /><br />

<b>Supported Browsers: </b><br />
Firefox (all features tested to work)<br />
Safari (all features work, though zoom scroll is quickly)<br />
Chrome (fancy transparency effects dont' work)<br />--%>

