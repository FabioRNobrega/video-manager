FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /workspace

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

EXPOSE 8080

CMD ["sh", "-lc", "cd WebApp/WebApp && exec dotnet watch run --no-launch-profile --urls=http://0.0.0.0:8080"]
