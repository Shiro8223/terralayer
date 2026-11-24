# Builds Terralayer through the ModSources junction so the mod is named "Terralayer".
$proj = Join-Path $env:USERPROFILE "Documents\My Games\Terraria\tModLoader\ModSources\Terralayer\Terralayer.csproj"
Push-Location (Split-Path (Split-Path $proj))
try { dotnet build $proj } finally { Pop-Location }
