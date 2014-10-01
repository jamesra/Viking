<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage"  %>

<%@ Register TagPrefix="uc" TagName="NetworkGraphInterface" 
    Src="~/Views/Shared/NetworkGraphInterface.ascx" %>

<script runat="server">    
        
    protected void Page_Load(object sender, EventArgs e)
    {
        string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
        string vPath = HttpContext.Current.Request.ApplicationPath;
        if (vPath == "/")
            vPath = "";
        string webPath = vHost + vPath + "/Network/Trace/";
        form1.Action = webPath;
    }
</script>

<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
   Network Visualization
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

<div>   
    <form id="form1" runat="server">
    <% Html.RenderPartial("~/Views/VolumeChooser.cshtml"); %></div>
    &nbsp;<uc:NetworkGraphInterface id="NetworkInterface" runat="server" /> 
    </form>  
</div>   

 <script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/NetworkScript.js"></script>
  

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



