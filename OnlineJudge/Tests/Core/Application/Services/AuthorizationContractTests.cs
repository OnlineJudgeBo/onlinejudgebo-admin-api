using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OnlineJudgeAdminApi.Controllers;

internal static class AuthorizationContract
{
    // "Controller.Action [VERB template]" -> "anonymous" | "authenticated" | "roles:A,B" (all role attributes must pass).
    public static SortedDictionary<string, string> Current()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var controllers = typeof(UsersController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

        foreach (var controller in controllers)
        {
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var http = action.GetCustomAttributes<HttpMethodAttribute>().ToList();
                if (http.Count == 0)
                {
                    continue;
                }

                var route = string.Join("|", http.Select(h => $"{string.Join(",", h.HttpMethods)} {h.Template}"));
                result[$"{controller.Name}.{action.Name} [{route}]"] = Policy(controller, action);
            }
        }

        return result;
    }

    private static string Policy(Type controller, MethodInfo action)
    {
        if (action.IsDefined(typeof(AllowAnonymousAttribute), true)
            || (controller.IsDefined(typeof(AllowAnonymousAttribute), true) && !action.IsDefined(typeof(AuthorizeAttribute), true)))
        {
            return "anonymous";
        }

        var attributes = controller.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>(true))
            .ToList();
        if (attributes.Count == 0)
        {
            return "anonymous";
        }

        var roleSets = attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Roles))
            .Select(attribute => string.Join(",", attribute.Roles!.Split(',').Select(role => role.Trim()).OrderBy(role => role)))
            .Distinct()
            .ToList();

        return roleSets.Count == 0 ? "authenticated" : "roles:" + string.Join(" & ", roleSets);
    }
}

// Snapshot of who can call every endpoint. A change here must be deliberate: update the expected entry in the same PR.
public class AuthorizationContractTests
{
    private static readonly SortedDictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        ["AcademicController.AddCourseMemberAsync [POST sites/{siteId:int}/courses/{courseId:long}/members]"] = "authenticated",
        ["AcademicController.CreateCourseAssignmentAsync [POST sites/{siteId:int}/courses/{courseId:long}/assignments]"] = "authenticated",
        ["AcademicController.CreateCourseAsync [POST sites/{siteId:int}/courses]"] = "roles:Administrador,Auxiliar,Docente",
        ["AcademicController.CreateCourseMaterialAsync [POST sites/{siteId:int}/courses/{courseId:long}/materials]"] = "authenticated",
        ["AcademicController.CreateLearningPathAsync [POST learning-paths]"] = "roles:Administrador",
        ["AcademicController.CreateLearningPathStageAsync [POST learning-paths/{learningPathKey}/stages]"] = "roles:Administrador",
        ["AcademicController.CreateLearningPathTopicAsync [POST learning-paths/{learningPathKey}/stages/{stageId:long}/topics]"] = "roles:Administrador",
        ["AcademicController.DeleteCourseMaterialAsync [DELETE sites/{siteId:int}/courses/{courseId:long}/materials/{materialId:long}]"] = "authenticated",
        ["AcademicController.DeleteLearningPathAsync [DELETE learning-paths/{learningPathKey}]"] = "roles:Administrador",
        ["AcademicController.DeleteLearningPathStageAsync [DELETE learning-paths/{learningPathKey}/stages/{stageId:long}]"] = "roles:Administrador",
        ["AcademicController.DeleteLearningPathTopicAsync [DELETE learning-paths/{learningPathKey}/stages/{stageId:long}/topics/{topicId:long}]"] = "roles:Administrador",
        ["AcademicController.DownloadCourseReportCsvAsync [GET sites/{siteId:int}/courses/{courseId:long}/report.csv]"] = "authenticated",
        ["AcademicController.GetCourseAssignmentAsync [GET sites/{siteId:int}/courses/{courseId:long}/assignments/{assignmentId:long}]"] = "authenticated",
        ["AcademicController.GetCourseAssignmentSubmissionsAsync [GET sites/{siteId:int}/courses/{courseId:long}/assignments/{assignmentId:long}/submissions]"] = "authenticated",
        ["AcademicController.GetCourseAsync [GET sites/{siteId:int}/courses/{courseId:long}]"] = "authenticated",
        ["AcademicController.GetCourseMembersAsync [GET sites/{siteId:int}/courses/{courseId:long}/members]"] = "authenticated",
        ["AcademicController.GetCourseRankingAsync [GET sites/{siteId:int}/courses/{courseId:long}/ranking]"] = "authenticated",
        ["AcademicController.GetCourseReportAsync [GET sites/{siteId:int}/courses/{courseId:long}/report]"] = "authenticated",
        ["AcademicController.GetInstitutionRankingAsync [GET sites/{siteId:int}/institutions/ranking]"] = "authenticated",
        ["AcademicController.GetInstitutionsAsync [GET sites/{siteId:int}/institutions]"] = "authenticated",
        ["AcademicController.GetLearningPathAsync [GET sites/{siteId:int}/learning-paths/{learningPathKey}]"] = "anonymous",
        ["AcademicController.GetLearningPathProgressAsync [GET sites/{siteId:int}/learning-paths/{learningPathKey}/progress]"] = "authenticated",
        ["AcademicController.GetLearningPathsAsync [GET sites/{siteId:int}/learning-paths]"] = "anonymous",
        ["AcademicController.GetManageableCoursesAsync [GET sites/{siteId:int}/courses/manageable]"] = "authenticated",
        ["AcademicController.GetMyCoursesAsync [GET sites/{siteId:int}/courses/mine]"] = "authenticated",
        ["AcademicController.GetStudentProgressAsync [GET sites/{siteId:int}/courses/{courseId:long}/students/{userId}/progress]"] = "authenticated",
        ["AcademicController.JoinCourseAsync [POST sites/{siteId:int}/courses/join]"] = "authenticated",
        ["AcademicController.LinkLearningPathStageAsync [POST learning-paths/{learningPathKey}/stages/{stageId:long}/link]"] = "roles:Administrador",
        ["AcademicController.RemoveCourseMemberAsync [DELETE sites/{siteId:int}/courses/{courseId:long}/members/{memberUserId}]"] = "authenticated",
        ["AcademicController.ReorderCourseContentAsync [PUT sites/{siteId:int}/courses/{courseId:long}/content-order]"] = "authenticated",
        ["AcademicController.SaveLearningPathProgressAsync [PUT sites/{siteId:int}/learning-paths/{learningPathKey}/progress]"] = "authenticated",
        ["AcademicController.UnlinkLearningPathStageAsync [DELETE learning-paths/{learningPathKey}/stages/{stageId:long}/link]"] = "roles:Administrador",
        ["AcademicController.UpdateCourseAssignmentAsync [PUT sites/{siteId:int}/courses/{courseId:long}/assignments/{assignmentId:long}]"] = "authenticated",
        ["AcademicController.UpdateCourseMaterialAsync [PUT sites/{siteId:int}/courses/{courseId:long}/materials/{materialId:long}]"] = "authenticated",
        ["AcademicController.UpdateLearningPathAsync [PUT learning-paths/{learningPathKey}]"] = "roles:Administrador",
        ["AcademicController.UpdateLearningPathStageAsync [PUT learning-paths/{learningPathKey}/stages/{stageId:long}]"] = "roles:Administrador",
        ["AcademicController.UpdateLearningPathTopicAsync [PUT learning-paths/{learningPathKey}/stages/{stageId:long}/topics/{topicId:long}]"] = "roles:Administrador",
        ["BocaImportController.Confirm [POST confirm]"] = "roles:Administrador,Auxiliar,Docente",
        ["BocaImportController.Preview [POST preview]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestsController.CreateContestAsync [POST ]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestsController.GetAllContestAsync [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestsController.GetContestById [GET {contestId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestMachinesController.GetGroupAsync [GET group]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestMachinesController.ForwardAsync [GET {**path}|POST {**path}|PUT {**path}]"] = "roles:Administrador,Auxiliar,Docente",
        ["LabLoginController.LoginAsync [POST login]"] = "anonymous",
        ["ContestsController.GetClientIp [GET client-ip]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestsController.GetExamMonitorAsync [GET {contestId:int}/exam-monitor]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestsController.PromoteContestAsync [PUT {contestId:int}/promote]"] = "roles:Administrador,Auxiliar,Docente",
        ["ContestsController.UpdateContestAsync [PUT {contestId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["FileManagerController.DeleteFile [DELETE local-storage]"] = "roles:Administrador,Auxiliar,Docente",
        ["FileManagerController.GetFileContent [GET local-storage/content]"] = "roles:Administrador,Auxiliar,Docente",
        ["FileManagerController.GetFiles [GET local-storage]"] = "roles:Administrador,Auxiliar,Docente",
        ["FileManagerController.GetFilesAc [GET local-storage/ac]"] = "roles:Administrador,Auxiliar,Docente",
        ["FileManagerController.S3UploadFileContentAsync [POST cloud-storage]"] = "roles:Administrador,Auxiliar,Docente",
        ["FileManagerController.SaveFileContentAsync [POST local-storage]"] = "roles:Administrador,Auxiliar,Docente",
        ["IdeContextController.GetContextAsync [GET context]"] = "anonymous",
        ["IdeLaunchTokenController.CreateLaunchToken [POST launch-token]"] = "authenticated",
        ["JudgeController.GetRejudgeHistoryAsync [GET rejudge/history]"] = "roles:Administrador,Auxiliar,Docente",
        // Role is checked inside the action (HasManualJudgeRole -> 403).
        ["JudgeController.ManuallyJudgeSolutionAsync [PATCH solution/{id:int}/verdict]"] = "authenticated",
        ["JudgeController.RejudgeSolutionByContestIdAsync [GET rejudge/contest/{contestId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["JudgeController.RejudgeSolutionByIdAsync [GET rejudge/solution/{id:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["JudgeController.RejudgeSolutionByProblemIdAsync [GET rejudge/problem/{problemId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["JudgeController.RejudgeSolutionsByLanguageAsync [GET rejudge/language/{languageId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["JudgeController.RejudgeSolutionsByRangeAsync [GET rejudge/range]"] = "roles:Administrador,Auxiliar,Docente",
        ["JudgeController.RemoteExecutionAsync [POST remoteExecutionAsync]"] = "authenticated",
        // SECURITY: any signed-in user can overwrite the verdict of any remote solution (no site/owner check).
        ["JudgeController.RemoteExecutionResult [POST remoteExecutionResult]"] = "authenticated",
        ["ProblemsController.ChangeProblemVisibilityAsync [PUT {problemId:int}/visibility]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProblemsController.CreateProblemAsync [POST ]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProblemsController.DeleteProblemByIdAsync [DELETE {problemId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProblemsController.ExportProblemAsync [GET {problemId:int}/export]"] = "roles:Administrador,Auxiliar,Docente & Administrador",
        ["ProblemsController.GetClassificationSuggestionsAsync [GET {problemId:int}/classification-suggestions]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProblemsController.GetAllProblemsAsync [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProblemsController.GetProblemByIdAsync [GET {problem_id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProblemsController.ImportProblemAsync [POST import]"] = "roles:Administrador,Auxiliar,Docente & Administrador",
        ["ProblemsController.UpdateProblemAsync [PUT {problemId:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["ProgrammingLanguagesController.GetAllContestAsync [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["PublicAuthController.ConfirmPasswordRecoveryAsync [POST password-recovery/confirm]"] = "anonymous",
        ["PublicAuthController.LoginAsync [POST login]"] = "anonymous",
        ["PublicAuthController.MeAsync [GET me]"] = "authenticated",
        ["PublicAuthController.RegisterAsync [POST register]"] = "anonymous",
        ["PublicAuthController.RequestPasswordRecoveryAsync [POST password-recovery/request]"] = "anonymous",
        ["PublicController.DownloadOwnSourceCodesAsync [GET submissions/mine/source-codes.zip]"] = "authenticated",
        ["PublicController.GetContestProblemDetailAsync [GET contests/{contestId:int}/problems/{contestProblemId}]"] = "anonymous",
        ["PublicController.GetContestRecentSubmissionsAsync [GET activity/contest/{contestId:int}/recent-submissions]"] = "anonymous",
        ["PublicController.GetContestReportAsync [GET contests/{contestId:int}/report]"] = "anonymous",
        ["PublicController.GetContestReportCsvAsync [GET contests/{contestId:int}/report.csv]"] = "authenticated",
        ["PublicController.GetContestsAsync [GET contests]"] = "anonymous",
        ["PublicController.GetCourseRecentSubmissionsAsync [GET activity/course/{courseId:long}/recent-submissions]"] = "authenticated",
        ["PublicController.GetDashboardAsync [GET dashboard]"] = "anonymous",
        ["PublicController.GetLanguagesAsync [GET languages]"] = "anonymous",
        ["PublicController.GetOnlineUsersAsync [GET activity/online-users]"] = "authenticated",
        ["PublicController.GetOwnSubmissionsAsync [GET submissions/mine]"] = "authenticated",
        ["PublicController.GetProblemDetailAsync [GET problems/{problemId:int}]"] = "anonymous",
        ["PublicController.GetProblemFiltersAsync [GET problems/filters]"] = "anonymous",
        ["PublicController.GetProblemStatisticsAsync [GET problems/{problemId:int}/statistics]"] = "anonymous",
        ["PublicController.GetProblemsAsync [GET problems]"] = "anonymous",
        ["PublicController.GetRankingAsync [GET ranking]"] = "anonymous",
        ["PublicController.GetRecentSubmissionsAsync [GET activity/recent-submissions]"] = "anonymous",
        ["PublicController.GetSubmissionStatusAsync [GET submissions/{solutionId:int}]"] = "authenticated",
        ["PublicController.GetSubmissionsAsync [GET submissions]"] = "anonymous",
        ["PublicController.GetTopicsAsync [GET temas]"] = "anonymous",
        ["PublicController.RegisterForContestAsync [POST contests/{contestId:int}/register]"] = "authenticated",
        ["PublicController.SubmitAsync [POST submit]"] = "authenticated",
        ["RolesController.AddRoleToUserAsync [POST {userId}/{role}]"] = "roles:Administrador",
        ["RolesController.GetAllRolesAsync [GET rolesAvailable]"] = "roles:Administrador",
        ["RolesController.GetUserRolesAsync [GET ]"] = "roles:Administrador",
        ["RolesController.RemoveRoleFromUserAsync [DELETE {userId}/{role}]"] = "roles:Administrador",
        ["ScheduleController.CreateSchedule [POST ]"] = "roles:Administrador,Auxiliar,Docente",
        ["ScheduleController.DeleteSchedule [DELETE {id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["ScheduleController.GetSchedules [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["ScheduleController.GetSchedulesWithTeachers [GET teachers]"] = "roles:Administrador,Auxiliar,Docente",
        ["ScheduleController.UpdateSchedule [PUT {id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["StaticsController.GetLast365DaysSubmissionsByMonthAsync [GET GetLast365DaysSubmissionsByMonth]"] = "authenticated",
        ["StaticsController.GetSubmissionsByLanguageAsync [GET GetSubmissionsByLanguageAsync]"] = "authenticated",
        ["SubjectAssistantsController.GetSubjectAssistant [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["SubjectAssistantsController.UpsertSubjectAssistant [PUT ]"] = "roles:Administrador,Auxiliar,Docente",
        ["SubjectsController.CreateSubject [POST ]"] = "roles:Administrador,Auxiliar,Docente",
        ["SubjectsController.DeleteSubject [DELETE {id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["SubjectsController.GetSubjects [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["SubjectsController.UpdateSubject [PUT {id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["SubmissionController.CustomInputFromIdeAsync [POST /api/patito-ide/custom-input]"] = "anonymous",
        ["SubmissionController.GetIdeRunStatusAsync [GET /api/patito-ide/runs/{runId:int}]"] = "anonymous",
        ["SubmissionController.GetIdeSubmissionStatusAsync [GET /api/patito-ide/submissions/{submissionId:int}]"] = "anonymous",
        // SECURITY: any signed-in user can list every submission of the site, including client IPs.
        ["SubmissionController.GetSubmissionAuditAsync [GET ]"] = "authenticated",
        ["SubmissionController.RunFromIdeAsync [POST /api/patito-ide/runs]"] = "anonymous",
        ["SubmissionController.SubmitAsync [POST ]"] = "authenticated",
        ["SubmissionController.SubmitFromIdeAsync [POST /api/patito-ide/submissions]"] = "anonymous",
        ["TeachersController.CreateTeacher [POST ]"] = "roles:Administrador,Auxiliar,Docente",
        ["TeachersController.DeleteTeacher [DELETE {id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["TeachersController.GetTeachers [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["TeachersController.UpdateTeacher [PUT {id}]"] = "roles:Administrador,Auxiliar,Docente",
        ["TopicsController.AddClassificationToTopic [POST {id:int}/classification]"] = "roles:Administrador,Auxiliar,Docente",
        ["TopicsController.AddTopicAsync [POST ]"] = "roles:Administrador,Auxiliar,Docente",
        ["TopicsController.GetAllTopicsAsync [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["TopicsController.UpdateClassificationFromTopic [PUT {id:int}]"] = "roles:Administrador,Auxiliar,Docente",
        ["UsersController.ChangePassword [PUT changePassword/{userId}]"] = "authenticated",
        ["UsersController.CheckUserEmailAvailable [POST UserEmailIsAvailable]"] = "authenticated",
        ["UsersController.CheckUsernameAvailable [POST UsernameIsAvailable]"] = "authenticated",
        ["UsersController.DeleteRole [DELETE {userId}/role/{roleId:int}]"] = "authenticated",
        ["UsersController.DeleteUser [DELETE {userId}]"] = "authenticated",
        ["UsersController.GetAllUserProfilesAsync [GET ]"] = "roles:Administrador,Auxiliar,Docente",
        ["UsersController.UpdateProfileUser [PUT {userId}]"] = "authenticated",
    };

    [Fact]
    public void EveryEndpoint_HasTheExpectedAuthorizationPolicy()
    {
        var current = AuthorizationContract.Current();

        var changed = current.Where(kv => Expected.TryGetValue(kv.Key, out var expected) && expected != kv.Value)
            .Select(kv => $"CHANGED {kv.Key}: expected {Expected[kv.Key]}, got {kv.Value}");
        var added = current.Keys.Except(Expected.Keys).Select(key => $"NEW     {key}: {current[key]}");
        var removed = Expected.Keys.Except(current.Keys).Select(key => $"REMOVED {key}");
        var differences = changed.Concat(added).Concat(removed).ToList();

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    [Theory]
    [InlineData("RolesController")]
    [InlineData("ProblemsController.ExportProblemAsync")]
    [InlineData("ProblemsController.ImportProblemAsync")]
    [InlineData("AcademicController.CreateLearningPath")]
    [InlineData("AcademicController.DeleteLearningPath")]
    public void AdministrationEndpoints_RequireAdministrator(string prefix)
    {
        var endpoints = AuthorizationContract.Current().Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(endpoints);
        // Every role set must be exactly "Administrador" (AND-ed attributes: all must pass).
        Assert.All(endpoints, kv => Assert.Contains("Administrador", kv.Value.Replace("roles:", "").Split(" & ")));
    }

    [Fact]
    public void AnonymousEndpoints_AreOnlyPublicReadsAuthAndTokenValidatedIde()
    {
        var anonymous = AuthorizationContract.Current().Where(kv => kv.Value == "anonymous").Select(kv => kv.Key).ToList();

        Assert.All(anonymous, key => Assert.True(
            key.StartsWith("PublicController.Get", StringComparison.Ordinal)
            || key.StartsWith("PublicAuthController.", StringComparison.Ordinal)
            || key.StartsWith("AcademicController.GetLearningPath", StringComparison.Ordinal)
            || key.Contains("/api/patito-ide/", StringComparison.Ordinal)
            || key.StartsWith("IdeContextController.", StringComparison.Ordinal)
            // The lab ISO logs in before it has any token; it validates the Patito password itself.
            || key == "LabLoginController.LoginAsync [POST login]",
            $"Unexpected anonymous endpoint: {key}"));
    }
}
