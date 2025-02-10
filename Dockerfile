# Step 1: Build the SmppLibrary and Channels_Api application

# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the SmppLibrary and Channels_Api projects
COPY SmppLibrary/SmppLibrary.csproj ./SmppLibrary/
COPY Channels_Api/Channels_Api.csproj ./Channels_Api/

# Restore dependencies
RUN dotnet restore ./Channels_Api/Channels_Api.csproj

# Copy the rest of the code
COPY . ./

# Build and publish the Channels_Api project
RUN dotnet publish ./Channels_Api/Channels_Api.csproj -c Release -o /app/publish

# Step 2: Set up the runtime environment

# Use the official .NET runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Set the working directory for the runtime container
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose the port the app will run on (usually 80 for HTTP)
EXPOSE 5008

# Run the application
ENTRYPOINT ["dotnet", "Channels_Api.dll"]
