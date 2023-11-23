$Directory = "$($env:USERPROFILE)\Downloads"
$TargetDirectory = "$($env:USERPROFILE)\stream"

Write-Host "Cleaning up $($TargetDirectory)"
Remove-Item -Path $TargetDirectory -Recurse -Force -ErrorAction SilentlyContinue -Verbose
New-Item -Path $TargetDirectory -ItemType Directory -Force -Verbose | Out-Null

$Files = Get-ChildItem -Path $Directory -Filter *.mp4 -Recurse
$Files | ForEach-Object {
    $File = $_
    $TargetFileName = $File.Name -replace "[^a-zA-Z0-9\.]", "_"
    Write-Host "Converting $($File.FullName) to $($TargetDirectory)\$($TargetFileName)"
    ffmpeg.exe -y -i "$($File.FullName)" -c:a copy -c:v copy -movflags faststart "$($TargetDirectory)\$($TargetFileName)"
    Write-Host "Extracting metadata from $($File.FullName)"
    ffprobe.exe `
        -hide_banner `
        -output_format json `
        -show_format `
        -show_streams `
        -show_error `
        -show_chapters `
        -show_private_data `
        -show_programs `
        -i "$($TargetDirectory)\$($TargetFileName)" `
        -o "$($TargetDirectory)\$($TargetFileName).json"
}
