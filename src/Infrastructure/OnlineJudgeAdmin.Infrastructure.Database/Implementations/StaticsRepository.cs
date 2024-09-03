using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class StaticsRepository : IStaticsRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public StaticsRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<string> GetLast365DaysSubmissionsByMonthAsync(int siteId)
    {
        string allSubmissionsQuery = @"
            SELECT YEAR(in_date) AS Year, MONTH(in_date) AS Month, COUNT(*) AS TotalSubmissions
            FROM solution
            WHERE site_id = @siteId
            GROUP BY YEAR(in_date), MONTH(in_date)
            ORDER BY YEAR(in_date), MONTH(in_date);";

        var submissionsList = await _context.Set<DbMonthlySubmission>()
                                            .FromSqlRaw(allSubmissionsQuery, new MySqlConnector.MySqlParameter("@siteId", siteId))
                                            .ToListAsync();

        string jsonResult = JsonSerializer.Serialize(submissionsList);

        return jsonResult;
    }

    public async Task<string> GetSubmissionsByLanguageAsync(int siteId)
    {
        string languageQuery = @"
            SELECT tmp.Language, SUM(tmp.TotalSubmissions) AS TotalSubmissions
            FROM (
                SELECT
                    CASE
                        WHEN language IN (15, 17, 19, 6) THEN 19
                        WHEN language IN (0, 1, 13, 14, 16) THEN 16
                        WHEN language IN (3) THEN 3
                    ELSE language
                    END AS Language,
                    COUNT(*) AS TotalSubmissions
                FROM solution
                WHERE language IN (15, 17, 19, 6, 0, 1, 13, 14, 16, 3)
                    AND site_id = @siteId
                GROUP BY Language
            ) AS tmp
            GROUP BY tmp.Language
            ORDER BY tmp.Language;";

        var submissionsList = await _context.Set<DbLanguageSubmission>()
                                            .FromSqlRaw(languageQuery, new MySqlConnector.MySqlParameter("@siteId", siteId))
                                            .ToListAsync();

        string jsonResult = JsonSerializer.Serialize(submissionsList);

        return jsonResult;
    }
}
