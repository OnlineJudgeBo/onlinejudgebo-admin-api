using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using static ControllerTestSupport;

public class SitemapControllerTests
{
    [Fact]
    public async Task Sitemap_ListsPublicPagesOfTheSite()
    {
        var publicService = new Mock<IPublicService>();
        var learningPaths = new Mock<ILearningPathService>();
        learningPaths.Setup(service => service.GetLearningPathsAsync(3, It.IsAny<CurrentUser>()))
            .ReturnsAsync([new LearningPathTrackSummary { Id = "c&c" }]);
        publicService.Setup(service => service.GetProblemsAsync(3, 1, 100, null, null, null, null, null, "id_asc", null, null))
            .ReturnsAsync(new PublicProblemsResponse { Items = [new PublicProblemItem { ProblemId = 1001 }, new PublicProblemItem { ProblemId = 1002, ContestId = 5 }] });
        publicService.Setup(service => service.GetContestsAsync(3, null, null, null, 1, 100, null))
            .ReturnsAsync(new PublicContestsResponse { Items = [new PublicContestItem { ContestId = 7 }, new PublicContestItem { ContestId = 8, IsPrivate = true }] });

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("juezvirtual.com.bo");
        var controller = new SitemapController(publicService.Object, learningPaths.Object).WithContext(context);

        var xml = Assert.IsType<ContentResult>(await controller.GetSitemapAsync(3)).Content!;

        Assert.Contains("<loc>https://juezvirtual.com.bo/</loc>", xml);
        Assert.Contains("<loc>https://juezvirtual.com.bo/topics/c%26c</loc>", xml);
        Assert.Contains("<loc>https://juezvirtual.com.bo/problems/1001</loc>", xml);
        Assert.Contains("<loc>https://juezvirtual.com.bo/contests/7</loc>", xml);
        Assert.DoesNotContain("/problems/1002", xml);
        Assert.DoesNotContain("/contests/8", xml);
    }
}
