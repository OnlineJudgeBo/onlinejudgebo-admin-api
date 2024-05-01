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

    public async Task<string> GetLast365DaysSubmissionsByMonthAsync()
    {
        string allSubmissionsQuery = @"SELECT YEAR(in_date) AS Year, MONTH(in_date) AS Month, COUNT(*) AS TotalSubmissions
                                    FROM solution
                                    GROUP BY YEAR(in_date), MONTH(in_date)
                                    ORDER BY YEAR(in_date), MONTH(in_date);";

        var submissionsList = await _context.Set<DbMonthlySubmission>()
                                            .FromSqlRaw(allSubmissionsQuery)
                                            .ToListAsync();

        string jsonResult = JsonSerializer.Serialize(submissionsList);

        return jsonResult;
    }

    public async Task<string> GetSubmissionsByLanguageAsync()
    {
        string languageQuery = @"SELECT language, COUNT(*) AS TotalSubmissions
                            FROM solution
                            WHERE language IN (3, 11, 19)
                            GROUP BY language
                            ORDER BY language;";

        var submissionsList = await _context.Set<DbLanguageSubmission>()
                                            .FromSqlRaw(languageQuery)
                                            .ToListAsync();

        string jsonResult = JsonSerializer.Serialize(submissionsList);

        return jsonResult;
    }
}
