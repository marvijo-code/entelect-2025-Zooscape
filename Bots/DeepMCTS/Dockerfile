FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# The EXPOSE instruction does not actually publish the port but functions as a type of documentation
# between the person who builds the image and the person who runs the container.
# The actual port mapping is done during `docker run`.
# EXPOSE 5000 # Runner port, if this bot were hosting. For client bot, not strictly necessary.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the solution file first if you have one, or project file for better layer caching
# Assuming DeepMCTS.csproj is in the root of the Bots/DeepMCTS directory being copied
COPY ["DeepMCTS.csproj", "./"]
# If you have a solution file (.sln) at a higher level, adjust paths accordingly.
# For example: COPY ["MySolution.sln", "./"]
# And then: COPY ["Bots/DeepMCTS/DeepMCTS.csproj", "Bots/DeepMCTS/"]

# Copy rest of the application code for this specific bot
# Adjust the source path if your Docker context is the workspace root vs. the bot directory.
# Assuming context is Bots/DeepMCTS:
COPY . .
# If context is workspace root (e.g., /c:/dev/2025-Zooscape), then it would be:
# COPY ["Bots/DeepMCTS/.", "./"]

RUN dotnet restore "./DeepMCTS.csproj"
RUN dotnet build "./DeepMCTS.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DeepMCTS.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DeepMCTS.dll"] # Changed DLL name 