using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

public class BocaImportControllerTests
{
    [Theory]
    [InlineData("../../etc")]
    [InlineData("../../../home/judge")]
    [InlineData("not-a-guid")]
    public async Task Confirm_RejectsMalformedStagingIdWithoutTouchingTheFilesystem(string maliciousStagingId)
    {
        var problemService = new Mock<IProblemService>();
        var fileManager = new Mock<IFileSystemLocalManagerManager>();
        var controller = CreateController(problemService.Object, fileManager.Object);

        var result = await controller.Confirm(new BocaImportConfirmRequest
        {
            StagingIds = new List<string> { maliciousStagingId }
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var item = Assert.Single(GetResults(ok.Value!));
        Assert.False(item.Success);
        Assert.Equal("Invalid staging id.", item.Error);

        problemService.Verify(item => item.CreateProblemAsync(It.IsAny<string>(), It.IsAny<Problem>(), It.IsAny<int>()), Times.Never);
        fileManager.Verify(item => item.WriteToFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private static IEnumerable<BocaImportConfirmResult> GetResults(object value)
    {
        var property = value.GetType().GetProperty("results");
        return (IEnumerable<BocaImportConfirmResult>)property!.GetValue(value)!;
    }

    private static BocaImportController CreateController(IProblemService problemService, IFileSystemLocalManagerManager fileManager)
    {
        var httpContext = BuildAuthenticatedHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(item => item.HttpContext).Returns(httpContext);

        var controller = new BocaImportController(problemService, fileManager, new UserClaimsHelper(accessor.Object));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static DefaultHttpContext BuildAuthenticatedHttpContext(int siteId = 1, string role = "Docente", string userId = "teacher")
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("site_id", siteId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth");

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
