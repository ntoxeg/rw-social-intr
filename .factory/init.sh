#!/bin/bash
# Restore NuGet packages for main project
cd "$(dirname "$0")/.." || exit 1
dotnet restore SocialInteractions/SocialInteractions.csproj

# Restore test project if it exists
if [ -f "SocialInteractions.Tests/SocialInteractions.Tests.csproj" ]; then
    dotnet restore SocialInteractions.Tests/SocialInteractions.Tests.csproj
fi
