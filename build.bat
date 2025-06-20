@cd /D "%~dp0"
@dotnet publish --nologo -p:PublishProfile=Properties/PublishProfiles/Publish.pubxml .\WindowCloser.csproj