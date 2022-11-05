# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/CrosshairTweaks/*" -Force -Recurse
dotnet publish "./CrosshairTweaks.csproj" -c Release -o "$env:RELOADEDIIMODS/CrosshairTweaks" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location