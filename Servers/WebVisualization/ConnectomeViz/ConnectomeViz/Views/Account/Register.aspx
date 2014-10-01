<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="registerTitle" ContentPlaceHolderID="TitleContent" runat="server">
    Register
</asp:Content>

<asp:Content ID="registerContent" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Create a New Account</h2>
    <p>
        Use the form below to create a new account. 
    </p>
    <p>
        Passwords are required to be a minimum of <%=Html.Encode(ViewData["PasswordLength"])%> characters in length.
    </p>
    <%= Html.ValidationSummary("Account creation was unsuccessful. Please correct the errors and try again.") %>

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
                    <label for="email">Email:</label>
                    <%= Html.TextBox("email") %>
                    <%= Html.ValidationMessage("email") %>
                </p>
                <p>
                    <label for="confirmEmail">Confirm Email:</label>
                    <%= Html.TextBox("confirmEmail") %>
                    <%= Html.ValidationMessage("confirmEmail") %>
                </p>

                <p>
                    <label for="password">Password:</label>
                    <%= Html.Password("password") %>
                    <%= Html.ValidationMessage("password") %>
                </p>
                <p>
                    <label for="confirmPassword">Confirm Password:</label>
                    <%= Html.Password("confirmPassword") %>
                    <%= Html.ValidationMessage("confirmPassword") %>
                </p>
                 <p>
                    <label for="roleName">Select Role: [Read- To explore our data, run our tools, use Viz website | Modify- All that + the ability to Annotate] </label>
                    <%= Html.DropDownList("roleName") %> 
                    <%= Html.ValidationMessage("roleName") %>
                </p>

                <p>
                
                    <label>Lastly, Confirm you're human:</label>
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

                <p>
                    <input type="submit" value="Register" />
                </p>
            </fieldset>
        </div>
    <% } %>
</asp:Content>
