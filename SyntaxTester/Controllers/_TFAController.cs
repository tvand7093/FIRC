using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace SyntaxTester.Controllers
{
    [HandleError]
    [Authorize]
    public class _TFAController : Controller
    {
    }

    public class _AnotherController : Controller { }
}
