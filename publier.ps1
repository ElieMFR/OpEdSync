# Crée sur Gitea une release pour chaque version du manifest, avec ses notes (notes/<version>.md) et son zip.
# Les releases déjà présentes sont ignorées. Le jeton est demandé au lancement et n'est jamais écrit sur le disque.
$ErrorActionPreference = 'Stop'
$base = 'https://gitlab.bmeouchi.fr'
$repo = 'Elie/OpEdSync'

$secure = Read-Host 'Jeton Gitea' -AsSecureString
$token  = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
$h = @{ Authorization = "token $token" }

$manifest = Get-Content (Join-Path $PSScriptRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
# Du plus ancien au plus récent, pour que la dernière version soit marquée « Latest » sur Gitea.
$versions = @($manifest[0].versions) | Sort-Object { [version]$_.version }

foreach ($v in $versions) {
    $tag = $v.version
    try {
        Invoke-RestMethod -Uri "$base/api/v1/repos/$repo/releases/tags/$tag" -Headers $h | Out-Null
        Write-Host "$tag : release déjà présente, ignorée."
        continue
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
    }

    $body = Get-Content (Join-Path $PSScriptRoot "notes/$tag.md") -Raw -Encoding UTF8
    $title = ($body -split "`n")[0] -replace '^#+\s*', ''
    $payload = @{ tag_name = $tag; name = $title.Trim(); body = $body } | ConvertTo-Json
    $rel = Invoke-RestMethod -Method Post -Uri "$base/api/v1/repos/$repo/releases" -Headers $h `
        -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))

    # Le zip publié est celui contenu dans le tag.
    $zipName = Split-Path $v.sourceUrl -Leaf
    $tmp = Join-Path ([IO.Path]::GetTempPath()) $zipName
    # Passage par cmd : la redirection PowerShell réencoderait le binaire.
    cmd /c "git -C `"$PSScriptRoot`" show ${tag}:dist/$zipName > `"$tmp`""
    curl.exe -s -f -X POST "$base/api/v1/repos/$repo/releases/$($rel.id)/assets?name=$zipName" `
        -H "Authorization: token $token" -F "attachment=@$tmp" | Out-Null
    Remove-Item $tmp
    Write-Host "$tag : release créée avec $zipName."
}

Write-Host "Terminé : $base/$repo/releases"
