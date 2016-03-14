### FIRC (Force-Inheritance-Roslyn-Compliler)

This is a simple little tool for following a certain code standard when dealing with ASP.NET MVC/Web API sites.

#### Code Standard

This enforces a few basic rules as seen below. Keep in mind that this is just a test of the Roslyn compiler capabilities
so the contents of this repo may change drastically and shouldn't be relied on. If you want stability, fork your own
copy for safe keeping.

  - All ASP.NET MVC Controllers **MUST** be inside a `Controllers` folder in the web project.
  - All controllers **MUST** inherit from a custom controller that is named using the format: `_<your custom names here>`.
  - All Base Controllers must inherit from another base controller or either of the ASP.NET MVC Controller or ApiController classes.
  - All API Controllers **MUST** be inside a `Controllers\ApiControllers` folder in the web project.
  - All API Controllers **MUST** follow the same naming patter as the regular controllers. The format is *exactly* the same.
