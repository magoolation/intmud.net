$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", 4002)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

# Read welcome message
Start-Sleep -Milliseconds 500
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== WELCOME ==="
Write-Host $buffer

# Send 'ajuda' command
$writer.WriteLine("ajuda")
Start-Sleep -Milliseconds 500
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== AJUDA ==="
Write-Host $buffer

# Send 'olhar' command
$writer.WriteLine("olhar")
Start-Sleep -Milliseconds 500
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== OLHAR ==="
Write-Host $buffer

# Send 'norte' command
$writer.WriteLine("norte")
Start-Sleep -Milliseconds 500
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== NORTE ==="
Write-Host $buffer

# Send 'quit' command
$writer.WriteLine("quit")
Start-Sleep -Milliseconds 500
$buffer = ""
while ($stream.DataAvailable) {
    $buffer += [char]$reader.Read()
}
Write-Host "=== QUIT ==="
Write-Host $buffer

$client.Close()
