FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# ДОБАВЬТЕ ЭТУ СТРОКУ:
ENV DATABASE_URL=""

EXPOSE 10000
ENTRYPOINT ["dotnet", "wt-lab7-7postgres-efcore-denisyuk.dll"]