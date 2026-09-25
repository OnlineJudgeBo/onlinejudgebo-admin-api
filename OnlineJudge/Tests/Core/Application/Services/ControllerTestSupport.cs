using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdminApi.Mappers;

// Builds controllers the way ASP.NET does: authenticated HttpContext + the API's real AutoMapper profiles.
internal static class ControllerTestSupport
{
    public static readonly IMapper ApiMapper = new ServiceCollection()
        .AddLogging()
        .AddAutoMapper(_ => { }, typeof(ProblemProfile).Assembly)
        .BuildServiceProvider()
        .GetRequiredService<IMapper>();

    public static DefaultHttpContext AuthenticatedContext(string userId = "teacher", int siteId = 1, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("site_id", siteId.ToString())
        };
        claims.AddRange((roles.Length == 0 ? new[] { "Docente" } : roles).Select(role => new Claim(ClaimTypes.Role, role)));

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
    }

    public static UserClaimsHelper Claims(HttpContext context)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(item => item.HttpContext).Returns(context);
        return new UserClaimsHelper(accessor.Object);
    }

    public static T WithContext<T>(this T controller, HttpContext context) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    public static object? OkValue(IActionResult result) => Assert.IsType<OkObjectResult>(result).Value;

    public static object? OkValue<T>(ActionResult<T> result) => Assert.IsType<OkObjectResult>(result.Result).Value;
}
