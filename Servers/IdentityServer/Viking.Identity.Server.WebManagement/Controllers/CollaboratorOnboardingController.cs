using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.Services;
using Viking.Identity.Server.WebManagement.Extensions;
using Viking.Identity.Server.WebManagement.Helpers;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize(Roles = Special.Roles.Admin)]
    public class CollaboratorOnboardingController : Controller
    {
        private const string CompleteTempDataKey = "CollaboratorOnboardingComplete";

        private readonly ApplicationDbContext _context;
        private readonly VikingXmlMetadataService _xmlMetadata;
        private readonly CollaboratorOnboardingService _onboarding;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<CollaboratorOnboardingController> _logger;
        private readonly VikingIdentityServerOptions _identityServerOptions;

        public CollaboratorOnboardingController(
            ApplicationDbContext context,
            VikingXmlMetadataService xmlMetadata,
            CollaboratorOnboardingService onboarding,
            IEmailSender emailSender,
            ILogger<CollaboratorOnboardingController> logger,
            IOptions<VikingIdentityServerOptions> identityServerOptions)
        {
            _context = context;
            _xmlMetadata = xmlMetadata;
            _onboarding = onboarding;
            _emailSender = emailSender;
            _logger = logger;
            _identityServerOptions = identityServerOptions?.Value;
        }

        [HttpGet]
        public IActionResult Index()
        {
            SetAvailableParents(null);
            return View(new CollaboratorOnboardingViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(CollaboratorOnboardingViewModel model)
        {
            ModelState.Clear();

            if (string.IsNullOrWhiteSpace(model?.VikingXmlUrl))
            {
                ModelState.AddModelError(nameof(model.VikingXmlUrl), "VikingXML URL is required.");
                SetAvailableParents(model?.Org?.ParentId);
                return View(nameof(Index), model ?? new CollaboratorOnboardingViewModel());
            }

            try
            {
                var metadata = await _xmlMetadata.FetchAsync(model.VikingXmlUrl.Trim());
                model.VikingXmlUrl = metadata.SourceUrl ?? model.VikingXmlUrl.Trim();
                model.Org ??= new CreateOrgUnitViewModel();
                model.Volume ??= new CreateVolumeViewModel();

                // Only fill empty fields so Preview does not wipe admin edits.
                if (string.IsNullOrWhiteSpace(model.Org.Name))
                    model.Org.Name = metadata.OrgNameSuggestion;
                if (string.IsNullOrWhiteSpace(model.Org.Description))
                    model.Org.Description = metadata.Description;
                if (string.IsNullOrWhiteSpace(model.Volume.Name))
                    model.Volume.Name = metadata.VolumeName;
                if (string.IsNullOrWhiteSpace(model.Volume.Description))
                    model.Volume.Description = metadata.Description;

                if (Uri.TryCreate(model.VikingXmlUrl, UriKind.Absolute, out var endpoint))
                    model.Volume.URL = endpoint;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preview VikingXML at {Url}", model.VikingXmlUrl);
                ModelState.AddModelError(nameof(model.VikingXmlUrl), ex.Message);
            }

            SetAvailableParents(model.Org?.ParentId);
            return View(nameof(Index), model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CollaboratorOnboardingViewModel model)
        {
            model.Org ??= new CreateOrgUnitViewModel();
            model.Volume ??= new CreateVolumeViewModel();

            if (string.IsNullOrWhiteSpace(model.VikingXmlUrl) ||
                !Uri.TryCreate(model.VikingXmlUrl.Trim(), UriKind.Absolute, out var vikingXmlUri) ||
                (vikingXmlUri.Scheme != Uri.UriSchemeHttp && vikingXmlUri.Scheme != Uri.UriSchemeHttps))
            {
                ModelState.AddModelError(nameof(model.VikingXmlUrl), "A valid http(s) VikingXML URL is required.");
            }
            else
            {
                model.Volume.URL = vikingXmlUri;
            }

            if (_context.IsResourceNameTaken(model.Org.Name, nameof(OrganizationalUnit)))
                ModelState.AddModelError("Org.Name", $"An organizational unit named {model.Org.Name} already exists");

            if (_context.IsResourceNameTaken(model.Volume.Name, nameof(Volume)))
                ModelState.AddModelError("Volume.Name", $"A volume named {model.Volume.Name} already exists");

            if (!ModelState.IsValid)
            {
                SetAvailableParents(model.Org.ParentId);
                return View(nameof(Index), model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            CollaboratorOnboardingResult result;
            try
            {
                result = await _onboarding.CreateLabAndInviteAsync(
                    model.Org.Name,
                    model.Org.Description,
                    model.Org.ParentId,
                    model.Volume.Name,
                    model.Volume.Description,
                    model.Volume.URL,
                    model.CollaboratorEmail,
                    userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Collaborator onboarding create failed");
                ModelState.AddModelError(string.Empty, ex.Message);
                SetAvailableParents(model.Org.ParentId);
                return View(nameof(Index), model);
            }

            var complete = new CollaboratorOnboardingCompleteViewModel
            {
                OrganizationalUnitId = result.OrganizationalUnitId,
                OrganizationalUnitName = result.OrganizationalUnitName,
                VolumeId = result.VolumeId,
                VolumeName = result.VolumeName,
                CollaboratorEmail = result.CollaboratorEmail,
                ExistingUserGranted = result.ExistingUserGranted
            };

            if (!result.ExistingUserGranted && !string.IsNullOrEmpty(result.InviteToken))
            {
                complete.InviteUrl = Url.CollaboratorInviteRegistrationLink(
                    result.InviteToken,
                    Request.Scheme,
                    _identityServerOptions?.Authority);

                try
                {
                    await _emailSender.SendCollaboratorInviteAsync(
                        result.CollaboratorEmail,
                        result.OrganizationalUnitName,
                        result.VolumeName,
                        complete.InviteUrl);
                    complete.EmailSent = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send collaborator invite email to {Email}", result.CollaboratorEmail);
                    complete.EmailSent = false;
                    complete.EmailError = "Email could not be sent. Use the invite URL below to share with the collaborator.";
                }
            }

            TempData[CompleteTempDataKey] = JsonSerializer.Serialize(complete);
            return RedirectToAction(nameof(Complete));
        }

        [HttpGet]
        public IActionResult Complete()
        {
            if (TempData[CompleteTempDataKey] is not string json)
                return RedirectToAction(nameof(Index));

            var model = JsonSerializer.Deserialize<CollaboratorOnboardingCompleteViewModel>(json);
            if (model == null || model.OrganizationalUnitId == 0)
                return RedirectToAction(nameof(Index));

            return View(model);
        }

        private void SetAvailableParents(long? selectedParentId)
        {
            ViewBag.AvailableParents = OrgUnitSelectListHelper.AvailableParents(_context, selectedParentId);
        }
    }
}
