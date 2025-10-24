using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Viking.Identity.Server.WebManagement.Extensions;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    public class MyRightsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public MyRightsController(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var model = new MyRightsViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                Username = User.Identity?.Name ?? "Guest"
            };

            try
            {
                // Get the HttpClient for calling the WebAPI
                var client = _httpClientFactory.CreateClient("IdentityApi");

                // For now, let's use a simpler approach - call the API directly without authentication
                // since the API endpoint we created supports both authenticated and unauthenticated access
                string apiEndpoint;
                if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(User.Identity.Name))
                {
                    // Use the username-specific endpoint for authenticated users
                    apiEndpoint = $"/Permissions/AccessibleVolumes/{Uri.EscapeDataString(User.Identity.Name)}";
                }
                else
                {
                    // Use the general endpoint for unauthenticated users (will return empty)
                    apiEndpoint = "/Permissions/AccessibleVolumes";
                }

                var response = await client.GetAsync(apiEndpoint);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] JSON Response: {jsonContent}");
                    
                    var apiResponse = JsonSerializer.Deserialize<Dictionary<long, JsonElement>>(jsonContent);

                    if (apiResponse != null)
                    {
                        Console.WriteLine($"[DEBUG] Deserialized {apiResponse.Count} items");
                        foreach (var kvp in apiResponse)
                        {
                            Console.WriteLine($"[DEBUG] Key: {kvp.Key}, Value: {kvp.Value}");
                        }
                        
                        model.AccessibleVolumes = apiResponse.Values
                            .Select(volumeElement => 
                            {
                                try
                                {
                                    Console.WriteLine($"[DEBUG] Processing volume element: {volumeElement}");
                                    return new VolumeAccessInfo
                                    {
                                        Id = volumeElement.GetProperty("id").GetInt64(),
                                        Name = volumeElement.GetProperty("name").GetString(),
                                        Description = volumeElement.TryGetProperty("description", out var desc) ? desc.GetString() : string.Empty,
                                        Endpoint = volumeElement.TryGetProperty("endpoint", out var endpoint) ? endpoint.GetString() : string.Empty,
                                        Permissions = volumeElement.TryGetProperty("permissions", out var perms) 
                                            ? perms.EnumerateArray().Select(p => p.GetString()).ToList() 
                                            : new List<string>()
                                    };
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[DEBUG] Error processing volume element: {ex.Message}");
                                    Console.WriteLine($"[DEBUG] Element: {volumeElement}");
                                    throw;
                                }
                            })
                            .ToList();
                    }
                }
                else
                {
                    // Log the error but don't fail the view - just show empty list
                    // In a production app, you might want to use a proper logging framework
                    Console.WriteLine($"Failed to get accessible volumes: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the view - just show empty list
                Console.WriteLine($"Error calling WebAPI: {ex.Message}");
            }

            return View(model);
        }
    }
}

