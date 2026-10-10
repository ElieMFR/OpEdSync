# Publie le projet sur Gitea : crée le dépôt, pousse le code, crée la release et envoie le zip.
# Le jeton est demandé au lancement (jamais écrit sur le disque).
$ErrorActionPreference = 'Stop'
$base = 'https://gitlab.bmeouchi.fr'
$user = 'Elie'
$repo = 'OpEdSync'
$tag  = '1.3.0.0'
$zip  = Join-Path $PSScriptRoot 'dist/opedsync_1.3.0.0.zip'

$secure = Read-Host 'Jeton Gitea' -AsSecureString
$token  = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
$h = @{ Authorization = "token $token" }

# 1. Dépôt (ignoré s'il existe déjà)
try {
    Invoke-RestMethod -Method Post -Uri "$base/api/v1/user/repos" -Headers $h -ContentType 'application/json' -Body (@{
        name = $repo; description = "Génériques d'animes (OP/ED) pour Jellyfin 12"; private = $false
    } | ConvertTo-Json)
    Write-Host 'Dépôt créé.'
} catch { Write-Host "Dépôt déjà présent ou erreur : $($_.Exception.Message)" }

# 2. Code (le jeton passe par un en-tête temporaire, pas par l'URL)
git -c "http.extraHeader=Authorization: token $token" push -u gitea main

# 3. Release + zip
$body = Get-Content (Join-Path $PSScriptRoot 'RELEASE_NOTES_1.3.0.0.md') -Raw -Encoding UTF8
$rel = Invoke-RestMethod -Method Post -Uri "$base/api/v1/repos/$user/$repo/releases" -Headers $h -ContentType 'application/json; charset=utf-8' -Body (@{
    tag_name = $tag; target_commitish = 'main'; name = "$tag — Compatibilité Jellyfin 12"; body = $body
} | ConvertTo-Json)
curl.exe -s -X POST "$base/api/v1/repos/$user/$repo/releases/$($rel.id)/assets?name=opedsync_1.3.0.0.zip" -H "Authorization: token $token" -F "attachment=@$zip" | Out-Null
Write-Host "Terminé : $base/$user/$repo/releases"
