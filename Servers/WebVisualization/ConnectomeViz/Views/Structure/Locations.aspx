<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>
<%@ Register TagPrefix="uc" TagName="EndpointSelectorControl" 
    Src="~/Views/Shared/EndpointSelectorControl.ascx" %>

<script runat="server">
    protected void Page_Load(object sender, EventArgs e)
    {
        string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
        string vPath = HttpContext.Current.Request.ApplicationPath;
        if (vPath == "/")
            vPath = "";
        string webPath = vHost + vPath + "/Structure/Locations/";
        form1.Action = webPath;
    }    
</script>

<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
   Structure Visualization
</asp:Content>

<asp:Content ID="indexContent" ContentPlaceHolderID="MainContent" runat="server"> 
    <% string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
   string vPath = HttpContext.Current.Request.ApplicationPath;
   if (vPath == "/")
       vPath = "";
   string webPath = vHost + vPath; %>    
<script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/TabControl.js"></script>

 <script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/StructureScript.js"></script>
 <script type="text/javascript" language="javascript">

     window.onload = function () {

         typeOfCall = 0;

         var animatorIconPath = "<%=webPath%>/Content/ajax-loader.gif";
         document.getElementById("arrow").src = "<%=webPath%>/Content/icons/arrow_b_r.png";

         $("#arrow")
        .mouseover(function () {
            var src = "<%=webPath%>/Content/icons/arrow_g_r.png";
            $(this).attr("src", src);
        })
        .mouseout(function () {
            var src = "<%=webPath%>/Content/icons/arrow_b_r.png";
            $(this).attr("src", src);
        });

         document.getElementById("progress").src = animatorIconPath;

         structureClientID = "<%=structureID.ClientID%>";

         dataSourceClientID = "<%=EndpointSelector.serverName%>";
         labNameClientID = "<%=EndpointSelector.volumeName%>";

         RunAfterLoad();
     }

 </script>
 
  <div>
    <form method="post" action="<%=webPath%>/FormRequest/ExportToExcel">
    <input type="hidden" name="gridData" id="gridData" value="" />
</form>
    <form id="form1" runat="server">
        <label style="font-size: medium">
        <uc:endpointselectorcontrol id="EndpointSelector" runat="server"/>
        &nbsp;<br />
        &nbsp; Enter Cell ID&nbsp;&nbsp;&nbsp; :</label>&nbsp;&nbsp;&nbsp; <input type="text" id="cellID" name="cellID" 
         onkeydown="update_button()" onkeyup="update_button()" onfocus="update_button()" onmousemove="update_button()" 
         onchange="update_button()" onkeypress="update_button()" onmouseout="update_button()" autocomplete="off" title="Enter a Cell ID"/>
       &nbsp;<label style="font-size:medium"></label>
        <label style="font-size: medium">
        (or) 
        Choose from List : <asp:DropDownList id="structureID" clientIDMode="Static" onchange= "update_button1()" runat="server">
        </asp:DropDownList>    <a href="#structureGrid" id="structureButton" name="modal" style="display:none">
                    <img  border="0" id="arrow" src="" alt="explore" height="24" width="24" src="" align="absmiddle"/></a>
      <br />
      <br />
        &nbsp; </label>
        &nbsp;<input type="radio" name="group2" value="3D" id="Radio1" checked="checked"/> 
        <strong>3D</strong>&nbsp;&nbsp;  
        <input type="radio" name="group2" value="2D" id="Radio2" /> <strong>2D</strong>   
        <strong>&nbsp;| </strong>&nbsp;<input type="radio" name="group1" value="generate" id="generate" checked="checked"/> 
        <strong>Generate Graph&nbsp; </strong>  
        <input type="radio" name="group1" value="download" id="download" /><strong> Download Graph</strong>&nbsp;&nbsp;
       <input type="submit" id="submitButton" disabled="disabled" value="Go" onclick="animate()"/>&nbsp;
       <label id="message" style="font-size:small"></label>
       <img id="progress" style="visibility:hidden;" height="25" width="25" src="" align="absmiddle" alt="Loading..."/>
           <br /><br /> 
       </form>

</div>

<h3> Structure ID: '<%=ViewData["structureid"]%>' Statistics from DataSource - <%=ViewData["dataSource"]%></h3>

<div style="float:left">
    <table class="info">
    <tr><th> Surface Area </th><th> Volume </th><th> A/V Ratio</th></tr>
    <tr>
    <td><%=ViewData["area"]%> &#181m<sup>2</sup></td>
    <td><%=ViewData["volume"]%> &#181m<sup>3</sup></td>
    <td><%=ViewData["ratio"]%> &#181m<sup>-1</sup></td>
    </tr>
    </table>
</div>
<div style="float:left; margin-left: 50px">
    <table ="info">
    <tr>
    <%foreach (var row in ConnectomeViz.Models.State.stringLong)
      { %>
      <th><%= row.Split(',')[0]%></th>
      <%} %>
     </tr>
     <tr>
    <%foreach (var row in ConnectomeViz.Models.State.stringLong)
      { %>
      <td><%=  row.Split(',')[1]%></td>
      <%} %>
    </tr>
    </table>
</div>
<div style="clear:both;"></div>


<h3>2D Structure Visualization of ID: '<%=ViewData["structureid"]%>' from DataSource - <%=ViewData["dataSource"]%> (Scroll Down for a 2D version)</h3>
       


<%--  <p>   
    
<embed src="<%=ViewData["virtualRoot"]%>/Files/<%=ViewData["username"] %>/<%= ViewData["structureid"] %>.svg" type="image/svg+xml" width="1000" height="800" pluginspage="http://www.adobe.com/svg/viewer/install"></embed>

<!--<comment><object data="<%=ViewData["virtualRoot"]%>/Files/<%= ViewData["structureid"] %>.svg" type="image/svg+xml" width="1000" height="800" class= "svg" ></object> </comment>-->
       
 </p>--%>
  <p align="center">
      <object data="<%=ViewData["virtualRoot"]%>/Files/<%=ViewData["username"] %>/<%= ViewData["structureid"] %>.svg" type="image/svg+xml" 
              width="1000" height="800"
              id="SVGGraph" onload="runcheck()">
      </object>
</p>

</asp:Content>
