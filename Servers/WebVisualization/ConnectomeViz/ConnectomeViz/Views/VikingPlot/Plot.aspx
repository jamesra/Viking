<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage"  %>



<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
   VikingPlot3D Visualization
</asp:Content>

<asp:Content ID="indexContent" ContentPlaceHolderID="MainContent" runat="server"> 
<% string vHost = "http://" + HttpContext.Current.Request.Url.Authority;
   string vPath = HttpContext.Current.Request.ApplicationPath;
   if (vPath == "/")
       vPath = "";
   string webPath = vHost + vPath; %> 
<script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/TabControl.js"></script>
  

<div align="center"> 
  
  <%--<p>
  <b>Click the link to launch this app on your system locally <a href="<%=ViewData["virtualRoot"] %>/flex/VikingPlot.jnlp">3D VikingPlot!</a></b>
  <br />
  This is still beta. You may have to wait for a while after submitting a cell to see the visualization. <br />
   A more robust version of 3D Structure plot is in the works. 
  </p>--%>
<p> 
    <%--<APPLET
      NAME="PLOT"
      CODE='<%=ViewData["className"]%>'
      ALT='Enable Java to proceed'
      WIDTH='900'
      CODEBASE='<%=ViewData["virtualRoot"] %>/flex/'
      HEIGHT='700'>      
    <param name='targetVersion' value='1.6'>
    <param name='cache_option' value='No'>
    <param name='redirect' value='http://www.java.com/'>
    This applet requires the Java Plug-In available from
    <a href='http://www.java.com/'>www.java.com</a>
    </APPLET>  --%>

        <%--<applet width="1100" height="700" code='VikingPlot.class' codebase='<%=ViewData["virtualRoot"] %>/flex/' archive='<%=ViewData["virtualRoot"] %>/flex/VikingPlot.jar' >
            <param name="jnlp_href" value="VikingPlot.jnlp"/>
        </applet>  --%> 
</p>
<%--<p> Can't see anything? or encountered an error? you're probably missing <a href="http://www.oracle.com/technetwork/java/javase/tech/index-jsp-138252.html">Java 3D Plugin</a> Download and Install it</p>--%>


<script type="text/javascript" src="<%=webPath%>/Scripts/o3d/o3d-webgl/base.js"></script>
<script type="text/javascript" src="<%=webPath%>/Scripts/o3d/o3djs/base.js"></script>
<script type="text/javascript" src="<%=webPath%>/Scripts/o3d/3DLoaderSingle.js"></script>
<script type="text/javascript" id="o3dscript">

    window.onload = function () {

        initO3D();
    }
   
</script>

<h1>
3D Viz Test
</h1>

<p>
<a href="#" id="colladaLocation">Download Collada File</a>
<input type="text" name="fileurl" id="fileurl" size="120">
<input type="button" id="load" onclick="doload();" value="Render 3D">
<img id="3dprogress" style="visibility:hidden;" height="25" width="25" src="<%=webPath%>/Content/ajax-loader.gif" align="absmiddle" alt="Loading..."/>
<div id="message" style="color:Green;"></div>
</p>


<div align="center" id="o3d" style="width: 100%; height: 100%;"></div>





</div>
</asp:Content>
