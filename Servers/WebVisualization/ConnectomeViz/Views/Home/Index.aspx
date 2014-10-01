<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>


<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
    Cell Tracker
</asp:Content>

<asp:Content ID="indexContent" ContentPlaceHolderID="MainContent" runat="server">    
  <div>
  
      <form id="form1" action="/Tracker/Home/Manage" method= "post" >
  <label style="font-size: medium">Enter Cell ID</label>:&nbsp;&nbsp; <input type="text" id="cellid" name="cellid" />&nbsp;&nbsp;
  <input type="submit" value="Go"/><br /><br />
  <input type="radio" name="group1" value="Table" id="Table" checked/> Table<br/><br />
<input type="radio" name="group1" value="XML" id="XML" /> Visio VDX<br/><br />
<input type="radio" name="group1" value="SVG" id="SVG"/>SVG </form>
  </div>
  
</asp:Content>
