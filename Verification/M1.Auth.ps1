param([string]$BaseUrl = 'http://127.0.0.1:5081')
$ErrorActionPreference = 'Stop'
$script:checks = 0
function Assert($ok, $name) { if (-not $ok) { throw "FAIL: $name" }; $script:checks++; Write-Output "PASS: $name" }
function Call($method, $path, $body = $null, $token = '') {
    $args = @{ Method=$method; Uri="$BaseUrl$path"; SkipHttpErrorCheck=$true }
    if ($null -ne $body) { $args.Body = $body | ConvertTo-Json -Compress; $args.ContentType = 'application/json' }
    if ($token) { $args.Headers = @{ Authorization="Bearer $token" } }
    $r = Invoke-WebRequest @args
    return @{ Code=[int]$r.StatusCode; Data= $(if ($r.Content) { $r.Content | ConvertFrom-Json }) }
}
$email = 'm1-check-' + [Guid]::NewGuid().ToString('N') + '@example.test'
$password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$registration = @{ email=$email.ToUpperInvariant(); password=$password; displayName='M1 verification'; role='Admin' }
Assert ((Call GET '/health').Code -eq 200) 'health'
Assert ((Call GET '/auth/me').Code -eq 401) 'anonymous rejected'
Assert ((Call POST '/auth/register' @{email='bad';password='short';displayName=''}).Code -eq 400) 'invalid registration'
$r = Call POST '/auth/register' $registration
Assert ($r.Code -eq 201) 'register'
$id = $r.Data.id
$seed = $r.Data.seed
Assert ($r.Data.email -eq $email -and $r.Data.role -eq 'User') 'normalized email and no role escalation'
Assert ($r.Data.defaultSessionMinutes -eq 50 -and $r.Data.timeZoneId -eq 'Asia/Ho_Chi_Minh') 'profile defaults'
Assert (-not $r.Data.PSObject.Properties['passwordHash']) 'profile excludes credentials'
Assert ((Call POST '/auth/register' $registration).Code -eq 409) 'duplicate case-insensitive email'
$invalid = @{email=('x'+$email);password=$password;displayName='test';defaultSessionMinutes=14;timeZoneId='not-a-zone'}
Assert ((Call POST '/auth/register' $invalid).Code -eq 400) 'invalid session duration and timezone'
Assert ((Call POST '/auth/login' @{email=$email;password='wrong-password'}).Code -eq 401) 'wrong password'
Assert ((Call POST '/auth/login' @{email=('missing'+$email);password=$password}).Code -eq 401) 'unknown account'
$login = Call POST '/auth/login' @{email=$email.ToUpperInvariant();password=$password}
Assert ($login.Code -eq 200 -and $login.Data.accessToken -and $login.Data.refreshToken) 'login with uppercase email'
$tokens = $login.Data
Assert (([DateTimeOffset]$tokens.accessExpiresAt - [DateTimeOffset]::UtcNow).TotalMinutes -gt 29) 'access lifetime 30 minutes'
Assert (([DateTimeOffset]$tokens.refreshExpiresAt - [DateTimeOffset]::UtcNow).TotalDays -gt 29) 'refresh lifetime 30 days'
$me = Call GET '/auth/me' $null $tokens.accessToken
Assert ($me.Code -eq 200 -and $me.Data.id -eq $id -and $me.Data.seed -eq $seed) 'authenticated current user and stable seed'
Assert ((Call GET '/auth/me' $null 'invalid.token.value').Code -eq 401) 'invalid JWT rejected'
$refresh = Call POST '/auth/refresh' @{refreshToken=$tokens.refreshToken}
Assert ($refresh.Code -eq 200 -and $refresh.Data.refreshToken -ne $tokens.refreshToken) 'refresh rotates token'
Assert ((Call POST '/auth/refresh' @{refreshToken=$tokens.refreshToken}).Code -eq 401) 'used refresh rejected'
Assert ((Call GET '/auth/me' $null $refresh.Data.accessToken).Code -eq 200) 'new access token works'
Assert ((Call POST '/auth/refresh' @{refreshToken=('a'*64)}).Code -eq 401) 'unknown refresh rejected'

# Two simultaneous refreshes must produce exactly one successor.
$client = [System.Net.Http.HttpClient]::new()
try {
    $body = @{refreshToken=$refresh.Data.refreshToken} | ConvertTo-Json -Compress
    $tasks = 1..2 | ForEach-Object { $client.PostAsync("$BaseUrl/auth/refresh", [System.Net.Http.StringContent]::new($body,[System.Text.Encoding]::UTF8,'application/json')) }
    $responses = $tasks | ForEach-Object { $_.GetAwaiter().GetResult() }
    $codes = $responses | ForEach-Object { [int]$_.StatusCode } | Sort-Object
    Assert (($codes -join ',') -eq '200,401') 'concurrent refresh has one winner'
    $winner = $responses | Where-Object { [int]$_.StatusCode -eq 200 }
    $latest = $winner.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
} finally { $client.Dispose() }
$hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($latest.refreshToken)))
$stored = docker compose exec -T postgres psql -U focus -d focus -Atc "SELECT count(*) FROM refresh_tokens WHERE token_hash='$hash' AND length(token_hash)=64;"
Assert ($LASTEXITCODE -eq 0 -and "$stored".Trim() -eq '1') 'refresh stored as SHA256 hash'
$storedPassword = docker compose exec -T postgres psql -U focus -d focus -Atc "SELECT password_hash FROM users WHERE id='$id';"
Assert ($LASTEXITCODE -eq 0 -and "$storedPassword".Trim() -ne $password -and "$storedPassword".Length -gt 60) 'password hash stored, not plaintext'
docker compose exec -T postgres psql -U focus -d focus -c "UPDATE refresh_tokens SET expires_at=now()-interval '1 second' WHERE token_hash='$hash';" | Out-Null
Assert ((Call POST '/auth/refresh' @{refreshToken=$latest.refreshToken}).Code -eq 401) 'expired refresh rejected'
Write-Output "Completed: $script:checks checks. Test user retained: $email"
