param (
    [string]$folder = "."
)

$date = Get-Date -format o
(Get-Content "$folder\Config.daml") -replace '<Date>.*?</Date>', "<Date>$date</Date>" | Out-File "$folder\Config.daml"
(Get-Content "$folder\Config.fr.daml") -replace '<Date>.*?</Date>', "<Date>$date</Date>" | Out-File "$folder\Config.fr.daml"