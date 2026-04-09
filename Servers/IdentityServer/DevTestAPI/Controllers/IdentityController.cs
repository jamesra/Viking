using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DevTestAPI.Controllers
{
    [Route("identity")]
    [Authorize]
    public class IdentityController : Controller
    {
        // GET api/values
        [HttpGet]
        
        public IActionResult Get()
        {
            return new JsonResult(from c in User.Claims select new { c.Type, c.Value });
        }
    }
}
