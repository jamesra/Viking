using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.UI;
using System.Net.Mail;
using System.Text;
using System.Net;
using ConnectomeViz.Helpers;
using ConnectomeViz.Models;
using System.IO;
using MvcReCaptcha;
using System.Configuration;


namespace ConnectomeViz.Controllers
{

    [HandleError]
    [RequiresSSL]
    public class AccountController : Controller
    {

        // This constructor is used by the MVC framework to instantiate the controller using
        // the default forms authentication and membership providers.

        public int loginCount = 0;

        public AccountController()
            : this(null, null)
        {
        }

        // This constructor is not used by the MVC framework but is instead provided for ease
        // of unit testing this type. See the comments at the end of this file for more
        // information.
        public AccountController(IFormsAuthentication formsAuth, IMembershipService service)
        {
            FormsAuth = formsAuth ?? new FormsAuthenticationService();
            MembershipService = service ?? new AccountMembershipService();
        }

        public IFormsAuthentication FormsAuth
        {
            get;
            private set;
        }

        public IMembershipService MembershipService
        {
            get;
            private set;
        }

        public ActionResult LogOn()
        {
            ViewData["showCaptcha"] =  State.loginAttempts;          

            return View();
        }

        public ActionResult ResetPassword()
        {

            return View();
        }
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ResetPassword(string userName, string email)
        {

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";

            string host = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            if (String.IsNullOrEmpty(userName))
            {
                ModelState.AddModelError("username", "You must specify a username.");
            }
            if (String.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("email", "You must specify an email address.");
            }

            if (!ModelState.IsValid)
                return View();

            MembershipUser user = Membership.GetUser(userName);
           
            if (user != null)
            {
                if (user.Email == email)
                {
                    string tempPwd = user.ResetPassword();
                    string changedPassword = System.Guid.NewGuid().ToString().Replace("-", String.Empty).Substring(0, 8);
                    user.ChangePassword(tempPwd, changedPassword);
                    string temp = "Please use this temporary password to login, but change it as soon as you logon: \"" + changedPassword + "\"\n\n" +
                                    "\nUse this link to access your account and change password: " +
                                    host + "/Admin/User/Details/" + userName;
                    string subject = userName + " - Temporary password for your account";

                    SendVerificationMail(user, subject, temp);

                    ViewData["message"] = "Please check your email for a temporary password";

                    return View("Verify");
                }
                else
                {
                    ModelState.AddModelError("email", "Username and Email do not match account");
                }
            }
            else
            {
                ModelState.AddModelError("user", "You must specify a valid username.");
            }

            return View();
        }

        public bool captchaValidate()
        {
            string ChallengeFieldKey = "recaptcha_challenge_field";
            string ResponseFieldKey = "recaptcha_response_field";

            var captchaChallengeValue = HttpContext.Request.Form[ChallengeFieldKey];
            var captchaResponseValue = HttpContext.Request.Form[ResponseFieldKey];
            var captchaValidtor = new Recaptcha.RecaptchaValidator
                                      {
                                          PrivateKey = ConfigurationManager.AppSettings["ReCaptchaPrivateKey"],
                                          RemoteIP = HttpContext.Request.UserHostAddress,
                                          Challenge = captchaChallengeValue,
                                          Response = captchaResponseValue
                                      };

            var recaptchaResponse = captchaValidtor.Validate();

            return recaptchaResponse.IsValid;
        }
        
        [AcceptVerbs(HttpVerbs.Post)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings",
            Justification = "Needs to take same parameter type as Controller.Redirect()")]
        public ActionResult LogOn(string userName, string password, bool rememberMe, string returnUrl)
        {
            bool captchaValid= true;

            string key = null;
            key = Request.Form["recaptcha_response_field"];
            if( key != null || key == "")
            {
                captchaValid = captchaValidate();
            }
           
            if (!ValidateLogOn(userName, password, captchaValid))
            {
                State.loginAttempts++;
                ViewData["showCaptcha"] =  State.loginAttempts;
                return View();
            }

              
            MembershipUser user = Membership.GetUser(userName);
            if (!user.IsApproved)
            {
                string applicationPath = HttpContext.Request.ApplicationPath;
                if (applicationPath == "/")
                    applicationPath = "";

                string host = "http://" + HttpContext.Request.Url.Authority + applicationPath;

                Guid guid = (Guid)Membership.GetUser(userName).ProviderUserKey;


                SendVerificationMail(Membership.GetUser(userName),
                      user.UserName + "- Welcome to Marc Lab! Activate your account via this Link",
                  "Activation link: " + "http://" + host + "/Account/Verify/" + guid.ToString());

                ViewData["message"] = "A verification link was resent to your email address: " + user.Email + ", Please check your email";
                return View("Verify");

            }
            else
            {
               FormsAuth.SignIn(userName, rememberMe);
                State.loginAttempts = 0; // reset login counter
            }

            if (!String.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Default");
            }
        }

        //[AcceptVerbs(HttpVerbs.Get)]
        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings",
        //    Justification = "Needs to take same parameter type as Controller.Redirect()")]
        //public ActionResult Authenticate(string userName, string password)
        //{

        //    bool result = ValidateLogOn(userName, password);

        //    string[] roles = Roles.GetRolesForUser(userName);

        //    if (result)
        //    {
        //        FormsAuth.SignIn(userName, false);
        //        return Content(roles[0]) ;
        //    }
                        
        //    return Content("Invalid");
            
        //}

        [AcceptVerbs(HttpVerbs.Post)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings",
            Justification = "Needs to take same parameter type as Controller.Redirect()")]
        public ActionResult Authenticate(string userName, string password, bool captchaValid = true)
        {
         
            //string userName = Request.Form["userName"];

            //string password = Request.Form["password"];


            bool result = ValidateLogOn(userName, password, captchaValid);

            string[] roles = Roles.GetRolesForUser(userName);

            if (roles.Count() == 0)
            {
            }

            if (result)
            {
                FormsAuth.SignIn(userName, false);
                return Content(roles[0]);
            }

           
            return Content("Invalid");

        }

        public ActionResult LogOff()
        {

            FormsAuth.SignOut();

            return RedirectToAction("Index", "Default");
        }

        public ActionResult Register()
        {
            List<string> roles = Roles.GetAllRoles().ToList();

            roles.Remove("Admin");

            string[] rolesArray = roles.ToArray();

            ViewData["roleName"] = new SelectList(rolesArray, "roleName");

            ViewData["PasswordLength"] = MembershipService.MinPasswordLength;

            return View();
        }

        [CaptchaValidator]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Register(string userName, string email,string confirmEmail, string password, string confirmPassword,bool captchaValid)
        {
            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";

            string host = "http://" + HttpContext.Request.Url.Authority + applicationPath;


            ViewData["PasswordLength"] = MembershipService.MinPasswordLength;

            if (ValidateRegistration(userName, email,confirmEmail, password, confirmPassword, captchaValid))
            {
                // Attempt to register the user
                MembershipCreateStatus createStatus = MembershipService.CreateUser(userName, password, email);

                if (createStatus == MembershipCreateStatus.Success)
                {
                    string roleName = Request.Form["roleName"];                   

                    MembershipUser user = Membership.GetUser(userName);

                    user.IsApproved = false;

                    Membership.UpdateUser(user);

                    Roles.AddUserToRole(userName,roleName);
                    Guid guid = (Guid)user.ProviderUserKey;


                    SendVerificationMail(Membership.GetUser(userName), 
                        user.UserName + "- Welcome to Marc Lab! Activate your account via this Link",
                    "Activation link: "+ host +"/Account/Verify/" + guid.ToString());

                    ViewData["message"] = "Check your email for an activation link <br / <br />However, if you do not receive one, try logging in with " + 
                                            "your chosen credentials and the server shall send another link, Thank you.";

                    return View("Verify");
                    
                }
                else
                {
                    ModelState.AddModelError("_FORM", ErrorCodeToString(createStatus));

                   
                }
            }

            //we've reached this point, so there's smth wrong with the user input

            List<string> roles = Roles.GetAllRoles().ToList();

            roles.Remove("Admin");

            string[] rolesArray = roles.ToArray();

            ViewData["roleName"] = new SelectList(rolesArray, "roleName");

            ViewData["PasswordLength"] = MembershipService.MinPasswordLength;


            return View();
        }

        private void SendVerificationMail(MembershipUser user,string subject, string matter)
        {
            
            SmtpClient ss2 = new SmtpClient("smtp.utah.edu", 25);

            ss2.DeliveryMethod = SmtpDeliveryMethod.Network;

            ss2.EnableSsl = true;

            MailMessage madmin = new MailMessage();

            madmin.From = new MailAddress("MarcLabNoReply@utah.edu", " Marc Lab, U of U");

            madmin.Subject = subject;

            madmin.Body = matter;
         
            madmin.To.Add(new MailAddress(user.Email));        

            madmin.IsBodyHtml = true;

            madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

            ss2.Send(madmin);

            madmin.Dispose();

        }

        public ActionResult Done()
        {
            return View();
        }

        public ActionResult Verify(string id)
        {
            Guid guid = new Guid(id);
            MembershipUser user = Membership.GetUser(guid);

            //FileStream fl = new FileStream(Server.MapPath("~") + "\\Files\\New.txt", FileMode.Create);
            //StreamWriter write = new StreamWriter(fl);
            //write.Write(user +"," + guid.ToString() + "\n");

            //Response.Write(user);
            //Response.Write(user.IsApproved);
            
            if (user != null)
            {
                user.IsApproved = true;
                Membership.UpdateUser(user);

                string userName = user.UserName;
                string email = user.Email;                           

                string[] roleName = Roles.GetRolesForUser(userName);
                //Response.Write(Roles.GetRolesForUser(userName));
                //write.Write("in if before try\n");
                try
                {
                    string applicationPath = HttpContext.Request.ApplicationPath;
                    if (applicationPath == "/")
                        applicationPath = "";

                    string host = "http://" + HttpContext.Request.Url.Authority + applicationPath;

                     string message = "";

                    

                        if (roleName.Contains("Modify"))
                        {
                            message = "| Give rights to User -->" + "<a href=\""+ host+"/Admin/User/Details/" + userName + "\">Grant Rights</a>";
                            if(!roleName.Contains("Read"))
                                Roles.AddUserToRole(userName,"Read");
                            Roles.RemoveUserFromRole(userName, "Modify");
                        }
                                     

                         SmtpClient ss2 = new SmtpClient("smtp.utah.edu", 25);

                        ss2.DeliveryMethod = SmtpDeliveryMethod.Network;                  

                        ss2.EnableSsl = true;

                        MailMessage madmin = new MailMessage();

                        madmin.From = new MailAddress("MarcLab@utah.edu", " Marc Lab, U of U");

                        madmin.Subject = "Registration: " + userName + " registered at Marc lab website";

                        madmin.Body = "<br/>" + madmin.Subject +
                            "<br/><br/>" + "User Registration Time: " + System.DateTime.Now.ToString() +
                            "<br/><br/>" + "User Email: " + email +
                            "<br/><br/>" + "Requested access level: "+ roleName[0] + message+
                            "<br/><br/>" + "--" + "<br/>" + "<a href=\"http://prometheus.med.utah.edu/~marclab/index.html\">Marc Lab, University of Utah</a>";



                        madmin.To.Add(new MailAddress("james.r.anderson@utah.edu"));

                        madmin.CC.Add(new MailAddress("robert.marc@hsc.utah.edu"));

                        madmin.To.Add(new MailAddress("hishoeb@gmail.com"));

                        //madmin.CC.Add(new MailAddress("shoeb.mohammed@utah.edu"));

                        //madmin.To.Add(new MailAddress("shoeb@cs.utah.edu"));

                       

                        madmin.IsBodyHtml = true;

                        madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                        ss2.Send(madmin);

                        madmin.Dispose();

                        MailMessage mm = new MailMessage();

                        mm.From = new MailAddress("james.r.anderson@utah.edu", "Marc lab, U of U");

                        mm.To.Add(email);

                        mm.Subject = "Welcome to Marc lab";

                        mm.Body = "<br/>Thank you for registering.Your account is Approved and ready for use <br/><br/>Please visit http://connectomes.utah.edu/viz for visualizations or <br/><br/>visit http://connectomes.utah.edu to download viking" +
                                     "<br/><br/>"+ "--" + "<br/>" + "<a href=\"http://prometheus.med.utah.edu/~marclab/index.html\">Marc Lab, University of Utah</a>";

                        mm.IsBodyHtml = true;

                        mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                        ss2.Send(mm);

                        mm.Dispose();

                        ViewData["message"] = "Your Account is Activated!! please visit homepage and login: "+ "<a href=\""+ host +"\">Homepage</a>";

                        //write.Write("in if\n");

                        //write.Close();
                        //fl.Close();
                        //Response.Write("successful in sending mail and validating");
                      
                       
                   }
                    catch (Exception e)
                    {
                        //write.Write("in if, exception \n");
                        //Response.Write(e);
                    }

               
            }
            else
            {
                //Response.Write("going into else");
                ViewData["message"] = "Your account is already approved, try logging in!";
                //write.Write("in else\n");
                
            }

            //write.Close();
            //fl.Close();
            return View();

            
        }


        [Authorize]
        public ActionResult ChangePassword()
        {

            ViewData["PasswordLength"] = MembershipService.MinPasswordLength;

            return View();
        }

        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Exceptions result in password not being changed.")]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {

            ViewData["PasswordLength"] = MembershipService.MinPasswordLength;

            if (!ValidateChangePassword(currentPassword, newPassword, confirmPassword))
            {
                return View();
            }

            try
            {
                if (MembershipService.ChangePassword(User.Identity.Name, currentPassword, newPassword))
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }
                else
                {
                    ModelState.AddModelError("_FORM", "The current password is incorrect or the new password is invalid.");
                    return View();
                }
            }
            catch
            {
                ModelState.AddModelError("_FORM", "The current password is incorrect or the new password is invalid.");
                return View();
            }
        }

        public ActionResult ChangePasswordSuccess()
        {

            return View();
        }

  


        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity is WindowsIdentity)
            {
                throw new InvalidOperationException("Windows authentication is not supported.");
            }
        }

        #region Validation Methods

        private bool ValidateChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (String.IsNullOrEmpty(currentPassword))
            {
                ModelState.AddModelError("currentPassword", "You must specify a current password.");
            }
            if (newPassword == null || newPassword.Length < MembershipService.MinPasswordLength)
            {
                ModelState.AddModelError("newPassword",
                    String.Format(CultureInfo.CurrentCulture,
                         "You must specify a new password of {0} or more characters.",
                         MembershipService.MinPasswordLength));
            }

            if (!String.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                ModelState.AddModelError("_FORM", "The new password and confirmation password do not match.");
            }

            return ModelState.IsValid;
        }

        private bool ValidateLogOn(string userName, string password, bool captchaValid)
        {
           
            if (String.IsNullOrEmpty(userName))
            {
                ModelState.AddModelError("username", "You must specify a username.");
            }
            if (String.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("password", "You must specify a password.");
            }
            if (!MembershipService.ValidateUser(userName, password))
            {
                ModelState.AddModelError("_FORM", "The username or password provided is incorrect.");
            }
            if (!captchaValid)
            {             
                ModelState.AddModelError("_FORM", "You did not type the Captcha word(s) correctly. Please try again");
            }

            return ModelState.IsValid;
        }

        private bool ValidateRegistration(string userName, string email,string confirmEmail, string password, string confirmPassword, bool captchaValid)
        {
            MembershipUserCollection users = Membership.GetAllUsers();

            Boolean error = false;
            foreach (MembershipUser user in users)
            {
                if (userName.Equals(user.UserName.ToString()))
                    error = true;

            }

            if (error)
                ModelState.AddModelError("username", "A user with a similar username already exists");

            if (String.IsNullOrEmpty(userName))
            {
                ModelState.AddModelError("username", "You must specify a username.");
            }
            if (String.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("email", "You must specify an email address.");
            }
             if (!String.Equals(email, confirmEmail, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("_FORM", "The Email and confirmation email do not match.");
            }            

            if (password == null || password.Length < MembershipService.MinPasswordLength)
            {
                ModelState.AddModelError("password",
                    String.Format(CultureInfo.CurrentCulture,
                         "You must specify a password of {0} or more characters.",
                         MembershipService.MinPasswordLength));
            }
            if (!String.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                ModelState.AddModelError("_FORM", "The new password and confirmation password do not match.");
            }

            if (!captchaValid)
                ModelState.AddModelError("_FORM", "You did not type the Captcha word(s) correctly. Please try again");
            return ModelState.IsValid;
        }

        private static string ErrorCodeToString(MembershipCreateStatus createStatus)
        {
            // See http://msdn.microsoft.com/en-us/library/system.web.security.membershipcreatestatus.aspx for
            // a full list of status codes.
            switch (createStatus)
            {
                case MembershipCreateStatus.DuplicateUserName:
                    return "Username already exists. Please enter a different user name.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "A username for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipCreateStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipCreateStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                default:
                    return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
        }
        #endregion
    }

    // The FormsAuthentication type is sealed and contains static members, so it is difficult to
    // unit test code that calls its members. The interface and helper class below demonstrate
    // how to create an abstract wrapper around such a type in order to make the AccountController
    // code unit testable.

    public interface IFormsAuthentication
    {
        void SignIn(string userName, bool createPersistentCookie);
        void SignOut();
    }

    public class FormsAuthenticationService : IFormsAuthentication
    {
        public void SignIn(string userName, bool createPersistentCookie)
        {
            FormsAuthentication.SetAuthCookie(userName, createPersistentCookie);
        }
        public void SignOut()
        {
            FormsAuthentication.SignOut();
        }
    }

    public interface IMembershipService
    {
        int MinPasswordLength { get; }

        bool ValidateUser(string userName, string password);
        MembershipCreateStatus CreateUser(string userName, string password, string email);
        bool ChangePassword(string userName, string oldPassword, string newPassword);
    }

    public class AccountMembershipService : IMembershipService
    {
        private MembershipProvider _provider;

        public AccountMembershipService()
            : this(null)
        {
        }

        public AccountMembershipService(MembershipProvider provider)
        {
            _provider = provider ?? Membership.Provider;
        }

        public int MinPasswordLength
        {
            get
            {
                return _provider.MinRequiredPasswordLength;
            }
        }

        public bool ValidateUser(string userName, string password)
        {
            MembershipUser user = Membership.GetUser(userName);
            bool result = _provider.ValidateUser(userName, password);

            Boolean flag = false;

            if (user == null)
                return false; 

            if (!user.IsApproved)
            {
                flag = true;
            }
           
            return result | flag;
        }

        public MembershipCreateStatus CreateUser(string userName, string password, string email)
        {
            MembershipCreateStatus status;
            _provider.CreateUser(userName, password, email, null, null, true, null, out status);
            return status;
        }

        public bool ChangePassword(string userName, string oldPassword, string newPassword)
        {
            MembershipUser currentUser = _provider.GetUser(userName, true /* userIsOnline */);
            return currentUser.ChangePassword(oldPassword, newPassword);
        }
    }
}
