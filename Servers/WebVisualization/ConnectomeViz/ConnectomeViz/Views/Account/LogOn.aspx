<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>


<script runat="server">
    
    protected void Page_Load(object sender, EventArgs e)
    {
       
    }
    
</script>
<asp:Content ID="loginTitle" ContentPlaceHolderID="TitleContent" runat="server">
    Log On
</asp:Content>

<asp:Content ID="loginContent" ContentPlaceHolderID="MainContent" runat="server">
<script type="text/javascript" >
window.onload = function() {
  document.getElementById("username").focus();
};
</script>
    <h2>Log On</h2>
    <p>
        Please enter your username and password to proceed. <%= Html.ActionLink("Register", "Register") %> if you don't have an account.
    </p>
    <%= Html.ValidationSummary("Login was unsuccessful. Please correct the errors and try again.") %>
 
    <% using (Html.BeginForm()) { %>
        <div>
            <fieldset>
                <legend>Account Information</legend>
                <p>
                    <label for="username">Username:</label>
                    <%= Html.TextBox("username") %>
                    <%= Html.ValidationMessage("username") %>
                </p>
                <p>
                    <label for="password">Password:</label>
                    <%= Html.Password("password") %>
                    <%= Html.ValidationMessage("password") %>
                    <%= Html.ActionLink("Forgot Password?","ResetPassword","Account")  %>
                </p>
                <p>
                    <%= Html.CheckBox("rememberMe",true) %> <label class="inline" for="rememberMe">Remember me?</label>
                </p>
             
                <% if (Convert.ToInt32(ViewData["showCaptcha"]) >= 2)
                   {                
                     
                %>
                     <p>
                
                    <label>Confirm you're human:</label>
                    <script type="text/javascript">
                        var RecaptchaOptions = {
                            theme: 'clean',
                            tabindex: 0
                        };

                    </script>
                    <script type="text/javascript" src="https://www.google.com/recaptcha/api/challenge?k=6Lc12cQSAAAAALr9krf8JJacg5VfGW5xREBU2XV_"></script>
                    <noscript>
		                <iframe src="https://www.google.com/recaptcha/api/noscript?k=6Lc12cQSAAAAALr9krf8JJacg5VfGW5xREBU2XV_" width="500" height="300" frameborder="0">

		                </iframe><br/>
                  
		               <textarea name="recaptcha_challenge_field" rows="3" cols="40"></textarea><input name="recaptcha_response_field" value="manual_challenge" type="hidden" />
                   </noscript>
                </p>

               
               <% } %>
                    <input type="submit" value="Log On" />
                </p>
            </fieldset>
        </div>
    <% } %>
</asp:Content>
