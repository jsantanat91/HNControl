FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./HNControl.Web.csproj
RUN dotnet publish ./HNControl.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        postgresql-client \
        libreoffice \
        libreoffice-writer \
        fonts-dejavu \
        fonts-liberation \
        tzdata \
    && rm -rf /var/lib/apt/lists/*

# Zona horaria de operación (México). Hace que DateTime.Now/Today sean hora local.
ENV TZ=America/Mexico_City
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV PG_DUMP_PATH=pg_dump
ENV PSQL_PATH=psql
ENV Office__Command=/usr/bin/soffice
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","HNControl.Web.dll"]
