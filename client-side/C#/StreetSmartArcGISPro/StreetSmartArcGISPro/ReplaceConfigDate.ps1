param (
    [string]$folder = "."
)
(Get-Content "$folder\Config.daml") -replace '<Date>.*?</Date>', "<Date>$(Get-Date -format o)</Date>" | Out-File "$folder\Config.daml"
