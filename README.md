### FIRC (Force-Inheritance-Roslyn-Compliler)

This is a simple little tool for following a certain code standard when dealing with ASP.NET MVC/Web API sites.

#### Code Standard

This enforces a few basic rules as seen below. Keep in mind that this is just a test of the Roslyn compiler capabilities
so the contents of this repo may change drastically and shouldn't be relied on. If you want stability, fork your own
copy for safe keeping.

  - All ASP.NET MVC Controllers **MUST** be inside a `Controllers` folder in the web project.
  - All controllers **MUST** inherit from either the MVC Controller class or a custom controller that is named
  using the format: `_Base<your custom names here>`. Note that the case is insensitive with respect to the word "Base" in the
  controller name.
  - All API Controllers **MUST** be inside a `Controllers\API` folder in the web project.
  - All API Controllers **MUST** follow the same naming patter as the regular controllers. The format is *exactly* the same.

#### Known limitations

The tool will insert the needed references if they are missing but it will not auto-import the namespace required. This is currently
due to the way it is built. It hasn't been built for production use, so it is fairly strict where classes go so it will require that
all the controllers share the correct folders.
