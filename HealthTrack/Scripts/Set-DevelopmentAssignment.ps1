param(
    [Parameter(Mandatory = $true)][string]$TrainerEmail,
    [Parameter(Mandatory = $true)][string]$ClientEmail,
    [switch]$PromoteTrainer
)

# Compatibility launcher. All validation, Identity changes and EF persistence run in C#.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\HealthTrack.csproj'
& dotnet run --project $project --no-launch-profile -- --environment Development --Assignment:Run true --Assignment:TrainerEmail $TrainerEmail --Assignment:ClientEmail $ClientEmail --Assignment:PromoteTrainer $PromoteTrainer.IsPresent.ToString()
if ($LASTEXITCODE -ne 0) { throw 'C# development assignment command failed.' }
