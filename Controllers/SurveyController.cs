using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Employee_Survey.Controllers
{
    [Authorize(Roles = "Employee")]
    public class SurveyController : Controller
    {
        public IActionResult MySurveys() => View();
    }
}
