using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntaxTester.Controllers.API
{
    public interface ITest { int Id { get; } }
    public class TestApiController : _BaseApiController, ITest
    {
        public int Id { get; }
    }
}
