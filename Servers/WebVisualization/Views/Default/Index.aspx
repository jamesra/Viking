<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Connectome Viz >>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">



 <% string vHost = "http://"+ HttpContext.Current.Request.Url.Authority;
       string vPath = HttpContext.Current.Request.ApplicationPath;
       if (vPath == "/")
           vPath = "";
        string webPath = vHost+vPath;%>
    <% MembershipUser user = Membership.GetUser(Page.User.Identity.Name); %>
    
    
   <% if (Request.IsAuthenticated && user.IsApproved)
       { %>
    <h2>You're Logged In!</h2>
    <% Html.RenderPartial("LogOnUserControl"); %>
    <%}
       else if (!Request.IsAuthenticated)
       { %> 
       <h2>Welcome to Marc lab!</h2>
       <% Html.RenderPartial("LogOnUserControl"); %>
    <%}
       else
       { %>
            <h2>Your account needs approval to be active, this may take 5-6 hours,Thank you.</h2>
    <%} %>
  <p> 
    
  </p> 


  <script type="text/javascript" language="javascript" src="<%=webPath%>/Scripts/TabControl.js"></script>
<table class="layout" border="0" cellpadding="3">
<tr>
<td><a href="http://prometheus.med.utah.edu/~marclab/index.html"><img src="<%=webPath%>/Content/marclab.png" border="0" alt="MarcLab"/></a></td>
<td><script src="http://widgets.twimg.com/j/2/widget.js"></script>
<script>
    new TWTR.Widget({
        version: 2,
        type: 'profile',
        rpp: 2,
        interval: 6000,
        width: 400,
        height: 210,
        theme: {
            shell: {
                background: '#354159',
                color: '#ffffff'
            },
            tweets: {
                background: '#000000',
                color: '#ffffff',
                links: '#4aed05'
            }
        },
        features: {
            scrollbar: true,
            loop: false,
            live: true,
            hashtags: true,
            timestamp: true,
            avatars: true,
            behavior: 'all'
        }
    }).render().setUser('marclab_utah').start();
</script></td>
</tr>
</table>



</asp:Content>
