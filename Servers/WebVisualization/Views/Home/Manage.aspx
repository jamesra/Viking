<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<IEnumerable<Tracker.Models.Inter>>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Manage
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
 <%string newpath = Server.MapPath("~"); %>
  <h2>Trace of Cell Number: <a href="/Tracker/Home/Manage/<%= ViewData["id"] %>"><%= ViewData["id"] %></a></h2>
    <h2><a href="/Tracker/Files/<%= ViewData["id"] %>.svg"> Save SVG </a> | <a href="/Tracker/Files/<%= ViewData["id"] %>.png"> Save PNG </a> | <a href="/Tracker/Files/<%= ViewData["id"] %>.pdf"> Save PDF </a> | <a href="/Tracker/Files/<%= ViewData["id"] %>.csv"> Save CSV </a></h2>
     <h2><a href="/Tracker/Home/Sort/graph"> Graph Layout </a> | <a href="/Tracker/Home/Sort/circular"> Circular Layout</a> | <a href="/Home/Sort/tree"> Tree Layout </a>

    <p>   
    
<embed src="/Tracker/Files/<%= ViewData["id"] %>.svg" type="image/svg+xml" width="1000" height="1000" pluginspage="http://www.adobe.com/svg/viewer/install"></embed>

<!--<comment><object data="/Tracker/Files/<%= ViewData["id"] %>.svg" type="image/svg+xml" width="1000" height="1000" class= "svg" ></object> </comment>-->
       
    </p>
 <h2>Trace of Cell Number: <a href="/Tracker/Home/Manage/<%= ViewData["id"] %>"><%= ViewData["id"] %></a></h2>
    <table>
        <tr>            
            <th>
                MainID
            </th>
            <th>
                ID
            </th>
            <th>
                TypeID
            </th>
            <th>
                ChildTypeID
            </th>
            <th>
                ChildStructID
            </th>
            <th>
                Label
            </th>
            <th>
                Dir
            </th>
            <th>
               Name
            </th>
        </tr>

    <% foreach (var item in Model) { %>
    
        <tr>
            <td>
                <%= Html.Encode(item.MainID) %>
            </td>
            <td>
                <a href="/Tracker/Home/Manage/<%=item.ID%>"><%=item.ID%>
            </td>
            <td>
                <%= Html.Encode(item.TypeID) %>
            </td>
            <td>
                <%= Html.Encode(item.ChildTypeID) %>
            </td>
            <td>
                <%= Html.Encode(item.ChildStructID) %>
            </td>
            <td>
                <%= Html.Encode(item.Label) %>
            </td>
             <td>
                <%= Html.Encode(item.Dir) %>
            </td>
            <td>
                <%= Html.Encode(item.Name) %>
            </td>
        </tr>
    
    <% } %>

    </table>
<br />
<br />
 
</asp:Content>

