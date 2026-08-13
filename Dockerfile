FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WorldEngine.slnx ./
COPY src/WorldEngine.Domain/WorldEngine.Domain.csproj src/WorldEngine.Domain/
COPY src/WorldEngine.Infrastructure/WorldEngine.Infrastructure.csproj src/WorldEngine.Infrastructure/
COPY src/WorldEngine.Api/WorldEngine.Api.csproj src/WorldEngine.Api/
COPY src/WorldEngine.Tests/WorldEngine.Tests.csproj src/WorldEngine.Tests/
RUN dotnet restore WorldEngine.slnx

COPY src/ src/
RUN dotnet publish src/WorldEngine.Api/WorldEngine.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "WorldEngine.Api.dll"]