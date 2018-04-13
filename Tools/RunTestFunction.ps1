param($key, [switch]$localhost)

$uri = "https://arvidnydenfunctions.azurewebsites.net/api/DownloadImageToBlob?code=" + $key

if($localhost) {
    $uri = "http://localhost:7071/api/DownloadImageToBlob"
}

$body = @{
            Link = "http://www.nasa.gov/press-release/nasa-to-discuss-deep-space-exploration-progress-at-johnson-space-center"; 
            ImageUrl = "http://www.nasa.gov/sites/default/files/styles/1x1_cardfeed/public/thumbnails/image/m18-058.jpg?itok=ZfBVSVX2"
        }

$bodyJson = $body | ConvertTo-Json
Invoke-WebRequest -Uri $uri -Method Post -Body $bodyJson