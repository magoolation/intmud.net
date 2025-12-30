$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", 4002)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

# Read all available output with longer wait
Start-Sleep -Milliseconds 2000
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== WELCOME ==="
Write-Host $buffer

# Send a command and wait
$writer.WriteLine("help")
Start-Sleep -Milliseconds 1000
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== HELP ==="
Write-Host $buffer

# Send quit
$writer.WriteLine("quit")
Start-Sleep -Milliseconds 500

$client.Close()
