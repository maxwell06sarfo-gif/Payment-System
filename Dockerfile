# Use the official .NET ASP.NET runtime as a base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PaymentSystem.Web/PaymentSystem.Web.csproj", "PaymentSystem.Web/"]
COPY ["PaymentSystem.Infrastructure/PaymentSystem.Infrastructure.csproj", "PaymentSystem.Infrastructure/"]
COPY ["PaymentSystem.Core/PaymentSystem.Core.csproj", "PaymentSystem.Core/"]
RUN dotnet restore "PaymentSystem.Web/PaymentSystem.Web.csproj"

COPY . .
WORKDIR "/src/PaymentSystem.Web"
RUN dotnet build "PaymentSystem.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PaymentSystem.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage: run the app
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Render uses the PORT environment variable
ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "PaymentSystem.Web.dll"]