# Genera docs/scorecard-ejemplo.pdf: un PDF minimo, valido y con texto real
# (una pagina, fuente Helvetica) para probar el flujo end-to-end sin depender
# de herramientas externas. Ejecutar:  pwsh ./tools/generar-pdf-ejemplo.ps1
$ErrorActionPreference = 'Stop'

$outDir  = Join-Path $PSScriptRoot '..\docs'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path $outDir 'scorecard-ejemplo.pdf'

$lines = @(
    'SCORECARD ENRIQUECIDO - RIESGO CREDITICIO',
    'Razon Social: ACME CONSTRUCCIONES S.A.',
    'CUIT: 30-71234567-9',
    'Actividad: Construccion de obras de ingenieria civil',
    'Provincia: Buenos Aires',
    'Score interno: 612 / 1000 (banda B-)',
    'Deuda financiera declarada: $ 145.000.000',
    'Situacion BCRA: 2 (con atrasos 31-90 dias)',
    'Directorio: Juan P. Gomez (presidente), Marta L. Ferreira',
    'Empresas vinculadas: ACME SERVICIOS S.R.L., INMOBILIARIA DEL SUR S.A.',
    'Observaciones: cliente con expansion agresiva 2023-2024'
)

function Escape-PdfText([string]$s) {
    return $s.Replace('\', '\\').Replace('(', '\(').Replace(')', '\)')
}

$nl = "`n"

$content = "BT${nl}/F1 12 Tf${nl}72 760 Td${nl}16 TL${nl}"
foreach ($l in $lines) { $content += '(' + (Escape-PdfText $l) + ") Tj T*${nl}" }
$content += 'ET'

$objs = @(
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
    "<< /Length $($content.Length) >>${nl}stream${nl}$content${nl}endstream"
)

$pdf = "%PDF-1.4${nl}"
$offsets = @()
for ($i = 0; $i -lt $objs.Count; $i++) {
    $offsets += $pdf.Length
    $pdf += "$($i + 1) 0 obj${nl}$($objs[$i])${nl}endobj${nl}"
}

$xrefPos = $pdf.Length
$size = $objs.Count + 1
$pdf += "xref${nl}0 $size${nl}"
$pdf += "0000000000 65535 f ${nl}"
foreach ($o in $offsets) { $pdf += ('{0:D10} 00000 n ' -f $o) + $nl }
$pdf += "trailer${nl}<< /Size $size /Root 1 0 R >>${nl}"
$pdf += "startxref${nl}$xrefPos${nl}%%EOF"

[System.IO.File]::WriteAllText($outPath, $pdf, [System.Text.Encoding]::ASCII)
Write-Host "PDF generado: $outPath ($($pdf.Length) bytes)"
