param([string]$BaseUrl = 'http://127.0.0.1:5081')
$ErrorActionPreference = 'Stop'
$secretPath = Join-Path $env:APPDATA 'Microsoft/UserSecrets/9ab9f25b-28fd-41b2-b8e0-60854effc81f/secrets.json'
$settings = Get-Content $secretPath -Raw | ConvertFrom-Json
$key = $settings.'Jwt:Key'
if (-not $key) { $key = $settings.Jwt.Key }
$userId = (docker compose exec -T postgres psql -U focus -d focus -Atc "SELECT id FROM users WHERE email LIKE 'm1-check-%@example.test' ORDER BY created_at DESC LIMIT 1;").Trim()
if (-not $userId) { throw 'Run M1.Auth.ps1 first.' }
function Encode([byte[]]$value) { [Convert]::ToBase64String($value).TrimEnd('=').Replace('+','-').Replace('/','_') }
function Token($expiry, $audience, $secret, $subject = $userId) {
    $header = Encode ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $payload = @{sub=$subject;role='User';iss='Focus';aud=$audience;exp=$expiry;nbf=([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-3600)} | ConvertTo-Json -Compress
    $content = $header + '.' + (Encode ([Text.Encoding]::UTF8.GetBytes($payload)))
    $signature = [Security.Cryptography.HMACSHA256]::HashData([Text.Encoding]::UTF8.GetBytes($secret), [Text.Encoding]::UTF8.GetBytes($content))
    return $content + '.' + (Encode $signature)
}
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$cases = @(
    @{Name='valid signed token';Token=(Token ($now+60) 'Focus.App' $key);Code=200},
    @{Name='expired access token';Token=(Token ($now-1) 'Focus.App' $key);Code=401},
    @{Name='wrong audience';Token=(Token ($now+60) 'Other.App' $key);Code=401},
    @{Name='wrong signature';Token=(Token ($now+60) 'Focus.App' ('x'*48));Code=401},
    @{Name='missing account';Token=(Token ($now+60) 'Focus.App' $key ([Guid]::NewGuid().ToString()));Code=401}
)
foreach ($case in $cases) {
    $r = Invoke-WebRequest "$BaseUrl/auth/me" -Headers @{Authorization="Bearer $($case.Token)"} -SkipHttpErrorCheck
    if ([int]$r.StatusCode -ne $case.Code) { throw "FAIL: $($case.Name)" }
    Write-Output "PASS: $($case.Name)"
}
Write-Output 'Completed: 5 JWT checks; no secrets printed.'
