#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:2.1-stretch-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:2.1-stretch AS build
WORKDIR /src
COPY ["CoreDapperRepository/CoreDapperRepository.Web.csproj", "CoreDapperRepository/"]
COPY ["CoreDapperRepository.Data/CoreDapperRepository.Data.csproj", "CoreDapperRepository.Data/"]
COPY ["CoreDapperRepository.Core/CoreDapperRepository.Core.csproj", "CoreDapperRepository.Core/"]
COPY ["CoreDapperRepository.Services/CoreDapperRepository.Services.csproj", "CoreDapperRepository.Services/"]
RUN dotnet restore "CoreDapperRepository/CoreDapperRepository.Web.csproj"
COPY . .
WORKDIR "/src/CoreDapperRepository"
RUN dotnet build "CoreDapperRepository.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CoreDapperRepository.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CoreDapperRepository.Web.dll"]