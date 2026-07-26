using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;

namespace Viking.Identity.Server.WebApi.Controllers
{
    /// <summary>
    /// API controller for managing debug logging settings at runtime
    /// All endpoints require admin authorization
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Special.Roles.Admin)]
    public class DebugLoggingController : ControllerBase
    {
        private readonly IDebugLoggingService _debugLoggingService;

        public DebugLoggingController(IDebugLoggingService debugLoggingService)
        {
            _debugLoggingService = debugLoggingService;
        }

        /// <summary>
        /// Get current debug logging settings (including runtime overrides)
        /// </summary>
        [HttpGet]
        public ActionResult<DebugLoggingOptions> Get()
        {
            return Ok(_debugLoggingService.GetOptions());
        }

        /// <summary>
        /// Toggle debug logging for a specific category
        /// </summary>
        /// <param name="category">The debug logging category to toggle</param>
        /// <param name="enabled">Whether to enable or disable the category</param>
        [HttpPost("category/{category}")]
        public IActionResult SetCategory(string category, [FromBody] bool enabled)
        {
            if (!Enum.TryParse<DebugLogCategory>(category, true, out var debugCategory))
            {
                return BadRequest($"Invalid category: {category}. Valid categories are: {string.Join(", ", Enum.GetNames(typeof(DebugLogCategory)))}");
            }

            _debugLoggingService.SetEnabled(debugCategory, enabled);
            return Ok(new { category = category, enabled = enabled });
        }

        /// <summary>
        /// Toggle global debug logging
        /// </summary>
        /// <param name="enabled">Whether to enable or disable global debug logging</param>
        [HttpPost("global")]
        public IActionResult SetGlobal([FromBody] bool enabled)
        {
            _debugLoggingService.SetGlobalEnabled(enabled);
            return Ok(new { globalEnabled = enabled });
        }

        /// <summary>
        /// Reset all runtime overrides to configuration values
        /// </summary>
        [HttpPost("reset")]
        public IActionResult Reset()
        {
            _debugLoggingService.ResetToConfiguration();
            return Ok(new { message = "Runtime overrides reset to configuration values" });
        }
    }
}


