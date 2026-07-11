using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ScheduleServiceTests
{
    [Fact]
    public async Task CreateScheduleAsync_CreatesOneSchedulePerRequestedDay()
    {
        var repository = new Mock<IScheduleRepository>();
        repository.Setup(item => item.GetTeacherByIdAsync(7)).ReturnsAsync(new Teacher { Id = 7, Name = "Docente" });
        repository.Setup(item => item.GetSubjectByIdAsync(3)).ReturnsAsync(new Subject { Id = 3, Name = "Materia" });
        repository
            .Setup(item => item.CreateScheduleAsync(It.IsAny<Schedule>()))
            .ReturnsAsync((Schedule schedule) => schedule);
        var service = new ScheduleService(repository.Object);

        var schedules = await service.CreateScheduleAsync(new ScheduleForCreationModel
        {
            ScheduleDays = new List<string> { "Lunes", "Martes" },
            ScheduleTime = "08:00",
            Subject = 3,
            Teacher = 7
        });

        Assert.Equal(2, schedules.Count);
        Assert.Equal(new[] { "Lunes", "Martes" }, schedules.Select(item => item.DayOfWeek));
        repository.Verify(item => item.CreateScheduleAsync(It.IsAny<Schedule>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateScheduleAsync_RejectsMultipleDays()
    {
        var service = new ScheduleService(Mock.Of<IScheduleRepository>());

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateScheduleAsync(10, new ScheduleForCreationModel
        {
            ScheduleDays = new List<string> { "Lunes", "Martes" },
            ScheduleTime = "08:00",
            Subject = 3,
            Teacher = 7
        }));

        Assert.Equal("Debe enviar exactamente un día para actualizar un horario.", error.Message);
    }

    [Fact]
    public async Task CreateScheduleAsync_RejectsMissingTeacher()
    {
        var repository = new Mock<IScheduleRepository>();
        repository.Setup(item => item.GetTeacherByIdAsync(7)).ReturnsAsync((Teacher?)null);
        var service = new ScheduleService(repository.Object);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateScheduleAsync(new ScheduleForCreationModel
        {
            ScheduleDays = new List<string> { "Lunes" },
            ScheduleTime = "08:00",
            Subject = 3,
            Teacher = 7
        }));

        Assert.Equal("El profesor no existe.", error.Message);
        repository.Verify(item => item.CreateScheduleAsync(It.IsAny<Schedule>()), Times.Never);
    }
}
