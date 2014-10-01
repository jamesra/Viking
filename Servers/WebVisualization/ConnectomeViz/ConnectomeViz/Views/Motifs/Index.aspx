<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage"  %>
<%@ Register TagPrefix="uc" TagName="EndpointSelectorControl" 
    Src="~/Views/Shared/EndpointSelectorControl.ascx" %>

<script runat="server">    
        
    protected void Page_Load(object sender, EventArgs e)
    {
        string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
        string vPath = HttpContext.Current.Request.ApplicationPath;
        if (vPath == "/")
            vPath = "";
        string webPath = vHost + vPath + "/Motifs/Trace/";
        form1.Action = webPath;
         
        //if (!String.IsNullOrEmpty(val))
        //{
        //    List<string> keys = ConnectomeViz.Models.State.serviceDictionary.Keys.ToList<string>();
        //    for (int i = 0; i < keys.Count; i++)
        //    {
        //        if (keys[i].ToString().Equals(val))
        //            dataSource.SelectedIndex = i;
        //    }
        //}
    }
</script>

<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
   Network Motifs Visualization
</asp:Content>

<asp:Content ID="indexContent" ContentPlaceHolderID="MainContent" runat="server"> 
    <% string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
        
   string vPath = HttpContext.Current.Request.ApplicationPath;
   if (vPath == "/")
       vPath = "";
   string webPath = vHost + vPath; %>    
 <script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/NetworkUtils.js"></script>    
 <script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/TabControl.js"></script>
 <script type="text/javascript" language="javascript">
     window.onload = function () {

         typeOfCall = 1;

         var animatorIconPath = "<%=webPath%>/Content/ajax-loader.gif";

         document.getElementById("progress").src = animatorIconPath;

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

        labelPatternListClientID = "<%=labelPatternList.ClientID%>"; 

        RunAfterLoad();
     }

//     function colorImage(tag)
//     {
//        tag.src = "<%=webPath%>/Content/icons/arrow_g_r.png";
//     }
//     function removeColor(tag)
//     {
//        tag.src = "<%=webPath%>/Content/icons/arrow_b_r.png";
//     }

 </script>
 
 
  <form method="post" action="<%=webPath%>/FormRequest/ExportToExcel">
    <input type="hidden" name="gridData" id="gridData" value="" />
</form>

   <div id="Controls">   
      <form id="form1" runat="server">
        <label style="font-size: medium"> 
        <uc:endpointselectorcontrol id="EndpointSelector" runat="server" EnableViewState="true"/>
        &nbsp;<br />
        &nbsp; Enter Label or Pattern&nbsp;&nbsp;&nbsp; :</label>&nbsp;&nbsp;&nbsp; <input type="text" id="labelPattern" name="labelPattern" 
         onkeydown="update_button()" onkeyup="update_button()" onfocus="update_button()" onmousemove="update_button()" 
         onchange="update_button()" onkeypress="update_button()" onmouseout="update_button()" title="Enter a label or pattern"/>
       &nbsp;<label style="font-size:medium"></label>
        <label style="font-size: medium">
        (or) 
        Choose from List : <asp:DropDownList id="labelPatternList" clientIDMode="Static" runat="server">
        </asp:DropDownList> <a href="#networkGrid" id="networkButton" name="modal" style="display:none">
                    <img  border="0" id="arrow" src="" alt="explore" height="24" width="24" /></a>
      <br />
      <br />
        &nbsp; </label>
        <label style="font-size:medium"> Choose Layout Algorithm&nbsp;&nbsp;&nbsp; :</label>&nbsp;&nbsp;
        <select name="layout">
        <option value="neato">neato</option>
        <option value="dot">dot</option>       
        </select>
        &nbsp; &nbsp; &nbsp; 
        <label style="font-size:medium"> Choose Hops&nbsp;&nbsp;&nbsp; :</label>&nbsp;&nbsp;
        <select name="hops">
        <option value="1">1</option>
        <option value="2">2</option>
        <option value="3" selected="selected">3</option>     
        <option value="12">Max Possible</option>

        </select>&nbsp;&nbsp;<strong><span class="Apple-style-span" 
            style="border-collapse: separate; color: rgb(0, 0, 0); font-family: 'Times New Roman'; font-style: normal; font-variant: normal; font-weight: normal; letter-spacing: normal; line-height: normal; orphans: 2; text-align: -webkit-auto; text-indent: 0px; text-transform: none; white-space: normal; windows: 2; word-spacing: 0px; -webkit-border-horizontal-spacing: 0px; -webkit-border-vertical-spacing: 0px; -webkit-text-decorations-in-effect: none; -webkit-text-size-adjust: auto; -webkit-text-stroke-width: 0px; font-size: medium; "><span 
            class="style1" 
            style="color: rgb(51, 51, 51); font-family: Verdana, Arial, sans-serif; line-height: 14px;"> 
        → </span></span>
        <input name="freshQuery" id="freshQuery" type="checkbox" value="latest" /> Update 
        Graph</strong>&nbsp;&nbsp;&nbsp;
       <input type="submit" id="submitButton" value="Go" onclick="animate()"/>&nbsp;&nbsp;&nbsp;&nbsp;
       <label id="message" style="font-size:small"></label>
       <img id="progress" style="visibility:hidden;" height="25" width="25" src=""  alt="Loading..."/>
           <br /><br /> 

         
         &nbsp; 

         
         <input type="radio" name="group1" value="generate" id="svgGraph" checked="checked"/> <strong>Interactive Graph </strong>
        
        &nbsp;<input type="radio" name="group1" value="flash" id="flashGraph" /> <strong>Flash Graph</strong>        
        &nbsp;<strong> |</strong>&nbsp;&nbsp;
         <input name="reduceEdges" id="reduceEdges" type="checkbox" value="reduce" checked="checked"/><strong>Reduce edges</strong><br/><br />      

        
        
      </form>  
  </div>  

<script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/MotifScript.js"></script>

  
    
    

</asp:Content>


<asp:Content ID="Content1" runat="server" contentplaceholderid="HeadContent">
    <style type="text/css">
        .style1
        {
            font-size: large;
        }
        #flashGraph
        {
            font-weight: 700;
        }
    </style>
</asp:Content>



