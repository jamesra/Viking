<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Synapses Statistics
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <h2>Visualize</h2>

 <p>   
    
    
<div align="center">
 	<object classid="clsid:D27CDB6E-AE6D-11cf-96B8-444553540000"
			id="Wire" width="1000" height="600"
			codebase="http://fpdownload.macromedia.com/get/flashplayer/current/swflash.cab">
			<param name="movie" value="SynapticStats.swf" />
			<param name="quality" value="high" />
			<param name="bgcolor" value="#ffffff" />
			<param name="allowScriptAccess" value="sameDomain" />
			<embed src="<%=ViewData["virtualRoot"]%>/flex/SynapticStats.swf" quality="high" bgcolor="#ffffff"
				width="1100" height="600" name="Wire" align="middle"
				play="true"
				loop="false"
				quality="high"
				allowScriptAccess="sameDomain"
				type="application/x-shockwave-flash"
				pluginspage="http://www.adobe.com/go/getflashplayer">
			</embed>
	</object>
</div>
 </p>
</asp:Content>
