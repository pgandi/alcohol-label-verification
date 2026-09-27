# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY LabelVerifier/LabelVerifier.csproj LabelVerifier/
RUN dotnet restore LabelVerifier/LabelVerifier.csproj
COPY . .
RUN dotnet publish LabelVerifier/LabelVerifier.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Hosts like Render provide the port via $PORT. Default to 8080 locally.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Bind to the platform-provided port when present.
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet LabelVerifier.dll"]
