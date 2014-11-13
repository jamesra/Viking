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
        </asp:DropDownList>  <a href="#structureGrid" id="structureButton" name="modal" style="display:none">
                    <img  border="0" id="arrow" src="" alt="explore" height="24" width="24" src="" align="absmiddle"/></a>
      <br />
      <br />
        &nbsp; </label>
        &nbsp;<input type="radio" name="group2" value="3D" id="3d" checked="checked"/> 
        <strong>3D</strong>&nbsp;&nbsp;  
        <input type="radio" name="group2" value="2D" id="2d" /> <strong>2D</strong>   
        <strong>&nbsp;| </strong>&nbsp;<input type="radio" name="group1" value="generate" id="generateGraph" checked="checked"/> 
        <strong>Generate Graph&nbsp; </strong>  
        <input type="radio" name="group1" value="download" id="downloadGraph" /><strong> Download Graph</strong>&nbsp;&nbsp;
       <input type="submit" id="submitButton" disabled="disabled" value="Go" onclick="animate()"/>&nbsp;
       <label id="message" style="font-size:small"></label>
       <img id="progress" style="visibility:hidden;" height="25" width="25" src="" align="absmiddle" alt="Loading..."/>
           <br /><br /> 
       </form>

</div>

</asp:Content>

