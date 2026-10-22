Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromSeconds(8)
$models = @('gemini-3.8-flash', 'gemini-3.7-flash', 'gemini-3.5-flash', 'gemini-3.5-flash-lite', 'gemini-3-flash-preview', 'gemini-flash-latest', 'gemini-pro-latest')
foreach ($m in $models) {
    try {
        $req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, "https://generativelanguage.googleapis.com/v1beta/models/${m}:generateContent")
        $req.Headers.Add('x-goog-api-key', 'AQ.Ab8RN6JOsMwOkUrSw4oURjK6zU8XSNisAtbK6X_LWBeyTHleKw')
        $req.Content = New-Object System.Net.Http.StringContent('{"contents":[{"parts":[{"text":"hi"}]}]}', [System.Text.Encoding]::UTF8, 'application/json')
        $res = $client.SendAsync($req).Result
        $content = $res.Content.ReadAsStringAsync().Result
        Write-Host "$m : $($res.StatusCode)"
        if ($res.StatusCode -ne [System.Net.HttpStatusCode]::OK) {
            Write-Host "   -> $content"
        }
    } catch {
        Write-Host "$m : Error - $($_.Exception.Message)"
    }
}
