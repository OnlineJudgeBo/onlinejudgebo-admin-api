FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

WORKDIR "/src/"

RUN dotnet restore "OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj"

RUN dotnet build "OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj" -c Release -o /app/build

FROM build AS publish

RUN dotnet publish "OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "OnlineJudgeAdminApi.dll"]
