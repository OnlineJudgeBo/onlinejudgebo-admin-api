using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdminApi;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;
using static ControllerTestSupport;

public class PublicAuthControllerTests
{
    private const string JwtKey = "unit-test-signing-key-that-is-long-enough-32b";
    private readonly Mock<IPublicService> _service = new();

    private PublicAuthController Controller(HttpContext? context = null, Dictionary<string, string?>? settings = null)
    {
        context ??= new DefaultHttpContext();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtKey,
            ["Jwt:Issuer"] = "patito",
            ["Jwt:Audience"] = "patito-web",
            ["Jwt:ExpiresHours"] = "2",
        }).Build();
        return new PublicAuthController(_service.Object, Claims(context), configuration).WithContext(context);
    }

    private static PublicAuthenticatedUser User() => new() { UserId = "ana", Role = "Docente", SiteId = 3, Email = "a@b.c" };

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var claims = Claims(new DefaultHttpContext());
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<ArgumentNullException>(() => new PublicAuthController(null!, claims, configuration));
        Assert.Throws<ArgumentNullException>(() => new PublicAuthController(_service.Object, null!, configuration));
        Assert.Throws<ArgumentNullException>(() => new PublicAuthController(_service.Object, claims, null!));
    }

    [Fact]
    public async Task Login_IssuesSignedTokenWithUserRoleAndSiteClaims()
    {
        _service.Setup(item => item.LoginAsync("ana", "secret1", 3, It.IsAny<string>())).ReturnsAsync(User());
        var before = DateTime.UtcNow;

        var response = Assert.IsType<PublicAuthResponse>(OkValue(await Controller().LoginAsync(new PublicLoginForCreation { UserId = "ana", Password = "secret1", SiteId = 3 })));

        // JsonWebTokenHandler is what ASP.NET JwtBearer uses to validate these tokens in production.
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "patito",
            ValidAudience = "patito-web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
        });
        Assert.True(validation.IsValid, validation.Exception?.Message);
        Assert.Equal("ana", validation.Claims[ClaimTypes.NameIdentifier]);
        Assert.Equal("Docente", validation.Claims[ClaimTypes.Role]);
        Assert.Equal("3", validation.Claims["site_id"]);
        Assert.Equal(SecurityAlgorithms.HmacSha256, ((JsonWebToken)validation.SecurityToken).Alg);
        Assert.InRange(response.ExpiresAtUtc, before.AddHours(2).AddSeconds(-1), DateTime.UtcNow.AddHours(2).AddSeconds(1));
        Assert.Same(_service.Object.LoginAsync("ana", "secret1", 3, "0.0.0.0").Result, response.User);
    }

    [Fact]
    public async Task Login_TokenFailsValidationWithAnotherKey()
    {
        _service.Setup(item => item.LoginAsync("ana", "secret1", 3, It.IsAny<string>())).ReturnsAsync(User());
        var response = Assert.IsType<PublicAuthResponse>(OkValue(await Controller().LoginAsync(new PublicLoginForCreation { UserId = "ana", Password = "secret1", SiteId = 3 })));

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "patito",
            ValidAudience = "patito-web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-signing-key-32bytes!")),
        });

        Assert.False(validation.IsValid);
        Assert.IsAssignableFrom<SecurityTokenSignatureKeyNotFoundException>(validation.Exception);
    }

    [Theory]
    [InlineData(null, 8)]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("24", 24)]
    public async Task Login_ExpirationDefaultsToEightHoursWithOneHourMinimum(string? configured, int expectedHours)
    {
        _service.Setup(item => item.LoginAsync("ana", "pw", 1, It.IsAny<string>())).ReturnsAsync(User());
        var controller = Controller(settings: new Dictionary<string, string?> { ["Jwt:Key"] = JwtKey, ["Jwt:ExpiresHours"] = configured });
        var before = DateTime.UtcNow;

        var response = Assert.IsType<PublicAuthResponse>(OkValue(await controller.LoginAsync(new PublicLoginForCreation { UserId = "ana", Password = "pw", SiteId = 1 })));

        Assert.InRange(response.ExpiresAtUtc, before.AddHours(expectedHours).AddSeconds(-1), DateTime.UtcNow.AddHours(expectedHours).AddSeconds(1));
    }

    [Fact]
    public async Task Login_MissingJwtKeyIsAServerError()
    {
        _service.Setup(item => item.LoginAsync("ana", "pw", 1, It.IsAny<string>())).ReturnsAsync(User());
        var controller = Controller(settings: new Dictionary<string, string?>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.LoginAsync(new PublicLoginForCreation { UserId = "ana", Password = "pw", SiteId = 1 }));

        Assert.Equal("Jwt:Key missing.", error.Message);
    }

    [Fact]
    public async Task Login_MapsServiceErrorsToStatusCodes()
    {
        _service.Setup(item => item.LoginAsync("bad", It.IsAny<string>(), 1, It.IsAny<string>())).ThrowsAsync(new UnauthorizedAccessException("Usuario o contraseña incorrectos."));
        _service.Setup(item => item.LoginAsync("", It.IsAny<string>(), 1, It.IsAny<string>())).ThrowsAsync(new ArgumentException("Credenciales inválidas."));
        var controller = Controller();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(await controller.LoginAsync(new PublicLoginForCreation { UserId = "bad", SiteId = 1 }));
        var badRequest = Assert.IsType<BadRequestObjectResult>(await controller.LoginAsync(new PublicLoginForCreation { UserId = "", SiteId = 1 }));

        Assert.Equal("Usuario o contraseña incorrectos.", Assert.IsType<ErrorDetails>(unauthorized.Value).Message);
        Assert.Equal(400, Assert.IsType<ErrorDetails>(badRequest.Value).StatusCode);
    }

    [Fact]
    public async Task Register_PassesAllFieldsAndClientIpAndReturnsToken()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.18.0.2");
        context.Request.Headers["X-Forwarded-For"] = "10.1.1.1";
        _service.Setup(item => item.RegisterAsync("ana", "secret1", "a@b.c", "Ana", "Pérez", "UMSA", 3, "10.1.1.1")).ReturnsAsync(User());

        var result = await Controller(context).RegisterAsync(new PublicRegisterForCreation
        {
            UserId = "ana", Password = "secret1", Email = "a@b.c", Nick = "Ana", LastName = "Pérez", School = "UMSA", SiteId = 3
        });

        Assert.False(string.IsNullOrEmpty(Assert.IsType<PublicAuthResponse>(OkValue(result)).AccessToken));
    }

    [Fact]
    public async Task Register_MapsConflictAndValidationErrors()
    {
        _service.Setup(item => item.RegisterAsync("taken", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("El usuario ya existe."));
        _service.Setup(item => item.RegisterAsync("x", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("UserId inválido."));
        var controller = Controller();

        var conflict = Assert.IsType<ConflictObjectResult>(await controller.RegisterAsync(new PublicRegisterForCreation { UserId = "taken" }));
        var badRequest = Assert.IsType<BadRequestObjectResult>(await controller.RegisterAsync(new PublicRegisterForCreation { UserId = "x" }));

        Assert.Equal(409, Assert.IsType<ErrorDetails>(conflict.Value).StatusCode);
        Assert.Equal("UserId inválido.", Assert.IsType<ErrorDetails>(badRequest.Value).Message);
    }

    [Fact]
    public async Task PasswordRecoveryRequest_ReturnsSameMessageAndMapsErrors()
    {
        _service.Setup(item => item.RequestPasswordRecoveryAsync("bad", 1)).ThrowsAsync(new ArgumentException("Correo electrónico inválido."));
        _service.Setup(item => item.RequestPasswordRecoveryAsync("smtp@down.bo", 1)).ThrowsAsync(new InvalidOperationException("No se pudo enviar el correo de recuperación."));
        var controller = Controller();

        var ok = Assert.IsType<OkObjectResult>(await controller.RequestPasswordRecoveryAsync(new PublicPasswordRecoveryRequestForCreation { Email = "a@b.c", SiteId = 1 }));
        var badRequest = Assert.IsType<BadRequestObjectResult>(await controller.RequestPasswordRecoveryAsync(new PublicPasswordRecoveryRequestForCreation { Email = "bad", SiteId = 1 }));
        var serverError = Assert.IsType<ObjectResult>(await controller.RequestPasswordRecoveryAsync(new PublicPasswordRecoveryRequestForCreation { Email = "smtp@down.bo", SiteId = 1 }));

        Assert.Contains("Revisa tu correo", ok.Value!.ToString());
        Assert.Equal(400, Assert.IsType<ErrorDetails>(badRequest.Value).StatusCode);
        Assert.Equal(500, serverError.StatusCode);
    }

    [Fact]
    public async Task PasswordRecoveryConfirm_MapsResults()
    {
        _service.Setup(item => item.ResetPasswordWithRecoveryCodeAsync("a@b.c", "EXPIRED", 1)).ThrowsAsync(new UnauthorizedAccessException("The recovery code is invalid or has expired."));
        _service.Setup(item => item.ResetPasswordWithRecoveryCodeAsync("a@b.c", "x", 1)).ThrowsAsync(new ArgumentException("Código de recuperación inválido."));
        var controller = Controller();

        Assert.IsType<OkObjectResult>(await controller.ConfirmPasswordRecoveryAsync(new PublicPasswordRecoveryConfirmForCreation { Email = "a@b.c", RecoveryCode = "GOODCODE", SiteId = 1 }));
        Assert.IsType<UnauthorizedObjectResult>(await controller.ConfirmPasswordRecoveryAsync(new PublicPasswordRecoveryConfirmForCreation { Email = "a@b.c", RecoveryCode = "EXPIRED", SiteId = 1 }));
        Assert.IsType<BadRequestObjectResult>(await controller.ConfirmPasswordRecoveryAsync(new PublicPasswordRecoveryConfirmForCreation { Email = "a@b.c", RecoveryCode = "x", SiteId = 1 }));
        _service.Verify(item => item.ResetPasswordWithRecoveryCodeAsync("a@b.c", "GOODCODE", 1), Times.Once);
    }

    [Fact]
    public async Task Me_ReturnsAuthenticatedUserAndRejectsAnonymous()
    {
        await Controller(AuthenticatedContext("ana", 3, "Invitado")).MeAsync();

        _service.Verify(item => item.GetAuthenticatedUserAsync(It.Is<CurrentUser>(u => u.UserId == "ana" && u.SiteId == 3)), Times.Once);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller().MeAsync());
    }
}

public class IdeControllersTests
{
    private readonly Mock<IIdeLaunchTokenValidator> _validator = new();
    private readonly Mock<IIdeContextService> _contextService = new();
    private readonly Mock<IIdeLaunchTokenIssuer> _issuer = new();

    private static IdeLaunchClaims Claims(int problemId = 1000) => new("ana", 1, problemId, 5, 2, new[] { 1, 2 });

    private IdeContextController ContextController(string? bearer = null)
    {
        var context = new DefaultHttpContext();
        if (bearer != null)
        {
            context.Request.Headers.Authorization = bearer;
        }

        return new IdeContextController(_validator.Object, _contextService.Object).WithContext(context);
    }

    [Fact]
    public void Constructors_RejectNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new IdeContextController(null!, _contextService.Object));
        Assert.Throws<ArgumentNullException>(() => new IdeContextController(_validator.Object, null!));
        Assert.Throws<ArgumentNullException>(() => new IdeLaunchTokenController(null!, ControllerTestSupport.Claims(AuthenticatedContext())));
        Assert.Throws<ArgumentNullException>(() => new IdeLaunchTokenController(_issuer.Object, null!));
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task Context_InvalidTokenIsUnauthorized(Type exceptionType)
    {
        _validator.Setup(item => item.Validate(It.IsAny<string>())).Throws((Exception)Activator.CreateInstance(exceptionType, "bad")!);

        var result = await ContextController().GetContextAsync(null, null, null, null, "bad-token");

        Assert.Equal("invalid_ide_token", Assert.IsType<ErrorDetails>(Assert.IsType<UnauthorizedObjectResult>(result).Value).Message);
        _contextService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Context_QueryTokenWinsOverBearerHeader()
    {
        _validator.Setup(item => item.Validate("from-query")).Returns(Claims());
        _contextService.Setup(item => item.BuildContextAsync(It.IsAny<IdeLaunchClaims>(), It.IsAny<IdeContextRequest>())).ReturnsAsync(IdeContextBuildResult.ProblemNotFound());

        await ContextController("Bearer from-header").GetContextAsync(null, null, null, null, "from-query");

        _validator.Verify(item => item.Validate("from-query"), Times.Once);
    }

    [Fact]
    public async Task Context_FallsBackToBearerHeader()
    {
        _validator.Setup(item => item.Validate("from-header")).Returns(Claims());
        _contextService.Setup(item => item.BuildContextAsync(It.IsAny<IdeLaunchClaims>(), It.IsAny<IdeContextRequest>())).ReturnsAsync(IdeContextBuildResult.ProblemNotFound());

        await ContextController("Bearer from-header").GetContextAsync(null, null, null, null, null);

        _validator.Verify(item => item.Validate("from-header"), Times.Once);
    }

    [Fact]
    public async Task Context_RequiresProblemClaim()
    {
        _validator.Setup(item => item.Validate(It.IsAny<string>())).Returns(Claims(problemId: 0));

        Assert.IsType<BadRequestObjectResult>(await ContextController().GetContextAsync(null, null, null, null, "t"));
    }

    [Fact]
    public async Task Context_ForbidsProblemDifferentFromToken()
    {
        _validator.Setup(item => item.Validate(It.IsAny<string>())).Returns(Claims(problemId: 1000));

        Assert.IsType<ForbidResult>(await ContextController().GetContextAsync(2000, null, null, null, "t"));
        _contextService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(IdeContextBuildStatus.UserNotAllowed, typeof(ForbidResult))]
    [InlineData(IdeContextBuildStatus.ProblemNotFound, typeof(NotFoundObjectResult))]
    [InlineData((IdeContextBuildStatus)99, typeof(StatusCodeResult))]
    public async Task Context_MapsBuildStatus(IdeContextBuildStatus status, Type expectedResult)
    {
        _validator.Setup(item => item.Validate(It.IsAny<string>())).Returns(Claims());
        _contextService.Setup(item => item.BuildContextAsync(It.IsAny<IdeLaunchClaims>(), It.IsAny<IdeContextRequest>())).ReturnsAsync(new IdeContextBuildResult(status, null));

        var result = await ContextController().GetContextAsync(1000, null, null, null, "t");

        Assert.IsType(expectedResult, result);
    }

    [Fact]
    public async Task Context_SuccessMapsResponseAndPassesRequest()
    {
        _validator.Setup(item => item.Validate(It.IsAny<string>())).Returns(Claims());
        var domain = new OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration.IdeContextResponse(
            new IdeProblemContext("1000", "Suma", "desc", "in", "out", "limits", "hint", new IdeProblemExample("1 2", "3"), "1s", "64MB"),
            new[] { 1, 2 },
            new[] { new IdeLanguageDefinition(1, "C++", "cpp") },
            new IdeContextIdentifiers(1000, 5, 2, "ana", 1, 1, "C++", "h"));
        _contextService
            .Setup(item => item.BuildContextAsync(It.IsAny<IdeLaunchClaims>(), new IdeContextRequest(1000, 1, "C++", "h")))
            .ReturnsAsync(IdeContextBuildResult.Success(domain));

        var result = await ContextController().GetContextAsync(1000, 1, "C++", "h", "t");

        var dto = Assert.IsType<OnlineJudgeAdminApi.DataTransferObjects.IdeContextResponse>(OkValue(result));
        Assert.Equal("Suma", dto.Problem.Title);
        Assert.Equal("1 2", dto.Problem.Example.Input);
        Assert.Equal("64MB", dto.Problem.MemoryLimit);
        Assert.Equal(new[] { 1, 2 }, dto.AllowedLanguages);
        Assert.Equal("cpp", dto.LanguageDefinitions.Single().IdeLanguage);
        Assert.Equal("ana", dto.Identifiers.UserId);
        Assert.Equal(5, dto.Identifiers.ContestId);
        Assert.Equal("h", dto.Identifiers.Handoff);
    }

    [Fact]
    public void LaunchToken_RequiresProblem()
    {
        var context = AuthenticatedContext("ana", 1, "Invitado");
        var controller = new IdeLaunchTokenController(_issuer.Object, ControllerTestSupport.Claims(context));

        Assert.IsType<BadRequestObjectResult>(controller.CreateLaunchToken(new IdeLaunchTokenForCreation { ProblemId = 0 }));
        _issuer.VerifyNoOtherCalls();
    }

    [Fact]
    public void LaunchToken_IssuesForCurrentUserAndSanitizesInput()
    {
        IdeLaunchClaims? issued = null;
        _issuer.Setup(item => item.Issue(It.IsAny<IdeLaunchClaims>())).Callback<IdeLaunchClaims>(claims => issued = claims).Returns("signed");
        var controller = new IdeLaunchTokenController(_issuer.Object, ControllerTestSupport.Claims(AuthenticatedContext("ana", 4, "Invitado")));

        var result = controller.CreateLaunchToken(new IdeLaunchTokenForCreation { ProblemId = 1000, ContestId = 0, Num = 3, AllowedLanguages = new[] { 0, 2, -1, 5 } });

        Assert.Equal("signed", Assert.IsType<IdeLaunchTokenResponse>(OkValue(result)).Token);
        Assert.Equal("ana", issued!.UserId);
        Assert.Equal(4, issued.SiteId);
        Assert.Null(issued.ContestId);
        Assert.Equal(3, issued.Num);
        Assert.Equal(new[] { 2, 5 }, issued.AllowedLanguages);
    }

    [Fact]
    public void LaunchToken_KeepsPositiveContestAndHandlesNullLanguages()
    {
        IdeLaunchClaims? issued = null;
        _issuer.Setup(item => item.Issue(It.IsAny<IdeLaunchClaims>())).Callback<IdeLaunchClaims>(claims => issued = claims).Returns("signed");
        var controller = new IdeLaunchTokenController(_issuer.Object, ControllerTestSupport.Claims(AuthenticatedContext("ana", 4, "Invitado")));

        controller.CreateLaunchToken(new IdeLaunchTokenForCreation { ProblemId = 1000, ContestId = 7, AllowedLanguages = null });

        Assert.Equal(7, issued!.ContestId);
        Assert.Empty(issued.AllowedLanguages);
    }
}

public class AuthHelpersTests
{
    [Theory]
    [InlineData("Bearer abc.def", "abc.def")]
    [InlineData("bearer   abc.def  ", "abc.def")]
    [InlineData("Basic dXNlcjpwYXNz", "")]
    [InlineData("", "")]
    [InlineData("Bearer", "")]
    public void GetBearerToken_ParsesAuthorizationHeader(string header, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = header;

        Assert.Equal(expected, context.Request.GetBearerToken());
    }

    [Theory]
    [InlineData(new[] { "Invitado", "Docente" }, UserRolesEnum.Docente)]
    [InlineData(new[] { "Auxiliar", "Docente" }, UserRolesEnum.Docente)]
    [InlineData(new[] { "Docente,Administrador" }, UserRolesEnum.Administrador)]
    [InlineData(new[] { " auxiliar " }, UserRolesEnum.Auxiliar)]
    [InlineData(new[] { "teacher" }, UserRolesEnum.Docente)]
    [InlineData(new[] { "admin" }, UserRolesEnum.Administrador)]
    [InlineData(new[] { "administrator" }, UserRolesEnum.Administrador)]
    [InlineData(new[] { "assistant" }, UserRolesEnum.Auxiliar)]
    [InlineData(new[] { "guest" }, UserRolesEnum.Invitado)]
    [InlineData(new[] { "unknown", "invitado" }, UserRolesEnum.Invitado)]
    public void GetUserContextRole_PicksHighestPriorityRole(string[] roles, UserRolesEnum expected)
    {
        var helper = ControllerTestSupport.Claims(AuthenticatedContext("ana", 1, roles));

        Assert.Equal(expected, helper.GetUserContextRole().Role);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("roles")]
    public void GetUserContextRole_ReadsAlternativeRoleClaimTypes(string claimType)
    {
        var helper = HelperWith(new Claim("sub", "ana"), new Claim("siteId", "2"), new Claim(claimType, "Auxiliar"));

        var user = helper.GetUserContextRole();

        Assert.Equal("ana", user.UserId);
        Assert.Equal(2, user.SiteId);
        Assert.Equal(UserRolesEnum.Auxiliar, user.Role);
    }

    [Theory]
    [InlineData("nameid")]
    [InlineData("user_id")]
    public void GetUserContextRole_ReadsAlternativeUserClaimTypes(string claimType)
    {
        var helper = HelperWith(new Claim(claimType, " ana "), new Claim("site_id", "1"), new Claim(ClaimTypes.Role, "Invitado"));

        Assert.Equal("ana", helper.GetUserContextRole().UserId);
    }

    public static IEnumerable<object[]> IncompleteTokens => new[]
    {
        new object[] { new[] { new Claim("site_id", "1"), new Claim(ClaimTypes.Role, "Invitado") } },
        new object[] { new[] { new Claim("sub", "ana"), new Claim(ClaimTypes.Role, "Invitado") } },
        new object[] { new[] { new Claim("sub", "ana"), new Claim("site_id", "abc"), new Claim(ClaimTypes.Role, "Invitado") } },
        new object[] { new[] { new Claim("sub", "ana"), new Claim("site_id", "0"), new Claim(ClaimTypes.Role, "Invitado") } },
        new object[] { new[] { new Claim("sub", "ana"), new Claim("site_id", "1") } },
        new object[] { new[] { new Claim("sub", "ana"), new Claim("site_id", "1"), new Claim(ClaimTypes.Role, "superuser") } },
    };

    [Theory]
    [MemberData(nameof(IncompleteTokens))]
    public void GetUserContextRole_RejectsIncompleteTokens(Claim[] claims)
    {
        Assert.Throws<UnauthorizedAccessException>(() => HelperWith(claims).GetUserContextRole());
    }

    [Fact]
    public void TryGetUserContextRole_ReturnsNullForAnonymousAndUserForAuthenticated()
    {
        Assert.Null(ControllerTestSupport.Claims(new DefaultHttpContext()).TryGetUserContextRole());
        Assert.Equal("ana", ControllerTestSupport.Claims(AuthenticatedContext("ana")).TryGetUserContextRole()!.UserId);
    }

    [Fact]
    public void GetUserContextRole_RejectsMissingHttpContext()
    {
        var accessor = new Mock<IHttpContextAccessor>();

        Assert.Throws<UnauthorizedAccessException>(() => new UserClaimsHelper(accessor.Object).GetUserContextRole());
    }

    private static UserClaimsHelper HelperWith(params Claim[] claims) =>
        ControllerTestSupport.Claims(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) });
}
