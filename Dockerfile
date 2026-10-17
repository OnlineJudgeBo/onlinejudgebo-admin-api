FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

WORKDIR "/src/"

RUN dotnet restore "OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj"

# Free DB-IP country database for the admin's request list (CC BY 4.0, https://db-ip.com). This month's
# file may not be out yet, so the previous one is the fallback; without either the countries stay blank.
RUN mkdir -p /geo && for month in $(date +%Y-%m) $(date -d "$(date +%Y-%m-01) -1 month" +%Y-%m); do \
      curl -fsSL "https://download.db-ip.com/free/dbip-country-lite-$month.mmdb.gz" | gunzip > /geo/dbip-country-lite.mmdb && break; \
    done; [ -s /geo/dbip-country-lite.mmdb ] || rm -f /geo/dbip-country-lite.mmdb

RUN dotnet build "OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj" -c Release -o /app/build

FROM build AS publish

RUN dotnet publish "OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# The BOCA importer reads PDF statements with pdftotext.
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=publish /app/publish .
COPY --from=build /geo ./geo

ENTRYPOINT ["dotnet", "OnlineJudgeAdminApi.dll"]
