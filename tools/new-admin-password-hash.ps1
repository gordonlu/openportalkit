param(
    [Parameter(Mandatory = $false)]
    [Security.SecureString] $Password
)

$ErrorActionPreference = 'Stop'

if ($null -eq $Password) {
    $Password = Read-Host 'Administrator password' -AsSecureString
}

$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $plainText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($plainText)) {
        throw 'Administrator password cannot be empty.'
    }

    $salt = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($salt)
    $derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $plainText,
        $salt,
        210000,
        [Security.Cryptography.HashAlgorithmName]::SHA512)
    try {
        $hash = $derive.GetBytes(32)
    }
    finally {
        $derive.Dispose()
    }

    'pbkdf2-sha512$210000${0}${1}' -f 
        [Convert]::ToBase64String($salt),
        [Convert]::ToBase64String($hash)
}
finally {
    if ($null -ne $plainText) {
        $plainText = $null
    }
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
}
