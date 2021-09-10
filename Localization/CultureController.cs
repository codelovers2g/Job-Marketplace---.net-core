using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace _999Space.Controllers
{
    public class CultureController : Controller
    {
        IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly ILogger<CultureController> _logger;
        public CultureController(IStringLocalizer<SharedResource> sharedLocalizer, ILogger<CultureController> logger)
        {
            _sharedLocalizer = sharedLocalizer;
            _logger = logger;
        }
        [HttpGet]
        public IActionResult SelectCulture(string culture, string returnUrl)
        {
            try
            {
                Response.Cookies.Append(
                  CookieRequestCultureProvider.DefaultCookieName,
                  CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                  new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
              );
                return LocalRedirect(returnUrl);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return LocalRedirect(returnUrl);
            }
           
        }
        [HttpGet]
        public JsonResult GetAllCultureResource()
        {
            var AllResource = _sharedLocalizer.GetAllStrings(true).ToList();
            IDictionary<string, object> expando = new ExpandoObject();
            foreach (var localstring in AllResource)
            {
                expando[localstring.Name] = localstring.Value;
            }
            return Json(expando);
        }
    }
}
