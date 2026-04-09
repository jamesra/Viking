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
    public class MyAccessController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public MyAccessController(
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
            var model = new MyAccessViewModel
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
                const string volumeEndpoint = "/Permissions/AccessibleVolumes";
                var volumeResponse = await client.GetAsync(volumeEndpoint);

                if (volumeResponse.IsSuccessStatusCode)
                {
                    var jsonContent = await volumeResponse.Content.ReadAsStringAsync();
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
                    Console.WriteLine($"Failed to get accessible volumes: {volumeResponse.StatusCode} - {volumeResponse.ReasonPhrase}");
                }

                // Fetch segmentation services
                const string segmentationEndpoint = "/Permissions/AccessibleSegmentationServices";
                var segmentationResponse = await client.GetAsync(segmentationEndpoint);
                if (segmentationResponse.IsSuccessStatusCode)
                {
                    var segJsonContent = await segmentationResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] Segmentation JSON Response: {segJsonContent}");

                    var segApiResponse = JsonSerializer.Deserialize<Dictionary<long, JsonElement>>(segJsonContent);

                    if (segApiResponse != null)
                    {
                        model.AccessibleSegmentationServices = segApiResponse.Values
                            .Select(serviceElement =>
                            {
                                try
                                {
                                    return new SegmentationServiceAccessInfo
                                    {
                                        Id = serviceElement.GetProperty("id").GetInt64(),
                                        Name = serviceElement.GetProperty("name").GetString(),
                                        Description = serviceElement.TryGetProperty("description", out var desc) ? desc.GetString() : string.Empty,
                                        Endpoint = serviceElement.TryGetProperty("endpoint", out var endpoint) ? endpoint.GetString() : string.Empty,
                                        Permissions = serviceElement.TryGetProperty("permissions", out var perms)
                                            ? perms.EnumerateArray().Select(p => p.GetString()).ToList()
                                            : new List<string>()
                                    };
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[DEBUG] Error processing segmentation service element: {ex.Message}");
                                    Console.WriteLine($"[DEBUG] Element: {serviceElement}");
                                    throw;
                                }
                            })
                            .ToList();
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to get accessible segmentation services: {segmentationResponse.StatusCode} - {segmentationResponse.ReasonPhrase}");
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

