$p = Get-Process "Lethal Company" -ErrorAction SilentlyContinue
while ($p -ne $null) {
    Start-Sleep -Seconds 2
    $p = Get-Process "Lethal Company" -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 1
Copy-Item "C:\Users\kmh04\Desktop\Folder\ai\LethalCompany\betterSuppression\BetterSuppression.dll" "C:\Users\kmh04\AppData\Roaming\com.kesomannen.gale\lethal-company\profiles\Default\BepInEx\plugins\BetterSuppression\BetterSuppression.dll" -Force
Remove-Item "C:\Users\kmh04\AppData\Roaming\com.kesomannen.gale\lethal-company\profiles\Default\BepInEx\config\com.lethalcompany.bettersuppression.cfg" -Force -ErrorAction SilentlyContinue
Write-Host "Deploy complete!"
