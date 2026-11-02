# Multi-stage production build for IlyPhoto Web (ASP.NET Core 9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project definitions for optimized caching
COPY ["IlyPhotoWeb/IlyPhotoWeb.csproj", "IlyPhotoWeb/"]
COPY ["IlyPhotoWeb.Tests/IlyPhotoWeb.Tests.csproj", "IlyPhotoWeb.Tests/"]
COPY ["IlyPhoto.sln", "./"]
RUN dotnet restore "IlyPhoto.sln"

# Copy remaining source code and publish
COPY . .
WORKDIR "/src/IlyPhotoWeb"
RUN dotnet publish "IlyPhotoWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IlyPhotoWeb.dll"]
