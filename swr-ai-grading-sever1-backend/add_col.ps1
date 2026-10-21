Add-Type -Path "C:\SEP490 Project\SWR_GRADING_SYSTEM_BE\swr-ai-grading-sever1-backend\src\WebApi\bin\Debug\net8.0\Npgsql.dll"
$conn = New-Object Npgsql.NpgsqlConnection('Host=aws-0-ap-southeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.qvsuinyyatewrylcijyy;Password=SWR302@_SYSTEM;SSL Mode=Require')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "ALTER TABLE evaluation_item_details ADD COLUMN IF NOT EXISTS not_recognized text;"
$cmd.ExecuteNonQuery()
Write-Host "Column not_recognized checked/added successfully!"
$conn.Close()
