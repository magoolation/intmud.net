$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", 4002)
$stream = $client.GetStream()
$stream.ReadTimeout = 3000
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

Write-Host "Connected, reading for 3 seconds..."

try {
    $line = $reader.ReadLine()
    while ($line -ne $null) {
        Write-Host $line
        $stream.ReadTimeout = 500  # Short timeout for subsequent reads
        $line = $reader.ReadLine()
    }
} catch [System.IO.IOException] {
    Write-Host "[Timeout, sending olhar...]"
}

$writer.WriteLine("olhar")
$stream.ReadTimeout = 3000

Write-Host "=== OLHAR RESPONSE ==="
try {
    $line = $reader.ReadLine()
    while ($line -ne $null) {
        Write-Host $line
        $stream.ReadTimeout = 500
        $line = $reader.ReadLine()
    }
} catch [System.IO.IOException] {
    Write-Host "[Timeout, done]"
}

$writer.WriteLine("quit")
$client.Close()
