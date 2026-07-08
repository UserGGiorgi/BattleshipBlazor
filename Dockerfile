# Stage 1: Build the app
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "BattleshipBlazor.csproj"
RUN dotnet publish "BattleshipBlazor.csproj" -c Release -o /app --no-restore

# Sanity checks: fail loudly if key output is missing
RUN test -d /app/wwwroot || (echo "ERROR: wwwroot missing from publish output" && exit 1)
RUN test -f /app/BattleshipBlazor.styles.css || (echo "ERROR: scoped CSS bundle missing from publish output" && exit 1)

# Stage 2: Run the app
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "BattleshipBlazor.dll"]