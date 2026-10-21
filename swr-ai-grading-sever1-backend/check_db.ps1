$dllPath = "C:\Users\TUAN MINH\.nuget\packages\npgsql\8.0.5\lib\net8.0\Npgsql.dll"
Add-Type -Path $dllPath
$conn = New-Object Npgsql.NpgsqlConnection('Host=aws-0-ap-southeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.qvsuinyyatewrylcijyy;Password=SWR302@_SYSTEM;SSL Mode=Require')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'evaluation_item_details';"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader.GetString(0)) : $($reader.GetString(1))"
}
$conn.Close()
