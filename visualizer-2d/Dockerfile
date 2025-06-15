# Build stage for frontend
FROM node:20 AS frontend-builder
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

# Build stage for API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-builder
WORKDIR /src
COPY api/*.csproj ./
RUN dotnet restore
COPY api .
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=api-builder /app/publish .
COPY --from=frontend-builder /app/dist ./wwwroot
EXPOSE 80
ENTRYPOINT ["dotnet", "Zooscape.Visualizer2d.API.dll"]
