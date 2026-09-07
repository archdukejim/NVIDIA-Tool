const fs = require('fs');
const path = require('path');
const { Resvg } = require('@resvg/resvg-js');

const DIR = __dirname;
const emblemSvg = fs.readFileSync(path.join(DIR, 'emblem.svg'), 'utf8');

function render(svg, width) {
  const r = new Resvg(svg, {
    fitTo: { mode: 'width', value: width },
    font: { loadSystemFonts: true },
    background: 'rgba(0,0,0,0)',
  });
  return r.render().asPng();
}

// 1) emblem PNGs (transparent-cornered tile)
const emblem512 = render(emblemSvg, 512);
const emblem256 = render(emblemSvg, 256);
fs.writeFileSync(path.join(DIR, 'emblem-512.png'), emblem512);
fs.writeFileSync(path.join(DIR, 'emblem-256.png'), emblem256);

// data URI for embedding into the banner
const emblemDataUri = 'data:image/png;base64,' + emblem512.toString('base64');

// 2) preview banner 1200x675
const preview = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 675" width="1200" height="675">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#0c1420"/>
      <stop offset="1" stop-color="#0a0f19"/>
    </linearGradient>
    <linearGradient id="teal" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#2dd4bf"/>
      <stop offset="1" stop-color="#5eead4"/>
    </linearGradient>
    <linearGradient id="strip" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#2dd4bf"/>
      <stop offset="0.7" stop-color="#f6b53c"/>
      <stop offset="1" stop-color="#ef6f5c"/>
    </linearGradient>
  </defs>

  <rect width="1200" height="675" fill="url(#bg)"/>
  <!-- faint grid -->
  <g stroke="#213249" stroke-width="1" opacity="0.35">
    ${Array.from({length: 12}, (_, i) => `<line x1="${100 + i*90}" y1="70" x2="${100 + i*90}" y2="560"/>`).join('')}
    ${Array.from({length: 6}, (_, i) => `<line x1="90" y1="${110 + i*90}" x2="1110" y2="${110 + i*90}"/>`).join('')}
  </g>

  <!-- emblem -->
  <image x="70" y="150" width="360" height="360" href="${emblemDataUri}"/>

  <!-- title -->
  <text x="470" y="270" font-family="Segoe UI, Arial, sans-serif" font-size="104" font-weight="700" fill="#f4f7fb">GPU Monitor</text>
  <text x="474" y="340" font-family="Segoe UI, Arial, sans-serif" font-size="58" font-weight="600" fill="url(#teal)">for NVIDIA GPUs</text>
  <text x="474" y="400" font-family="Segoe UI, Arial, sans-serif" font-size="32" fill="#9fb3c8">In-game VRAM &amp; GPU telemetry for RimWorld</text>

  <!-- feature chips -->
  <g font-family="Segoe UI, Arial, sans-serif" font-size="24" fill="#cfe0ee">
    <g>
      <rect x="474" y="430" width="206" height="48" rx="24" fill="#14283f" stroke="#2dd4bf" stroke-opacity="0.5"/>
      <text x="577" y="461" text-anchor="middle">VRAM breakdown</text>
    </g>
    <g>
      <rect x="696" y="430" width="166" height="48" rx="24" fill="#14283f" stroke="#2dd4bf" stroke-opacity="0.5"/>
      <text x="779" y="461" text-anchor="middle">Overlay HUD</text>
    </g>
    <g>
      <rect x="878" y="430" width="244" height="48" rx="24" fill="#14283f" stroke="#2dd4bf" stroke-opacity="0.5"/>
      <text x="1000" y="461" text-anchor="middle">Optional LM Studio</text>
    </g>
  </g>

  <!-- accent strip -->
  <rect x="474" y="506" width="648" height="8" rx="4" fill="url(#strip)"/>

  <!-- footer / disclaimer -->
  <text x="70" y="628" font-family="Segoe UI, Arial, sans-serif" font-size="22" fill="#6f8398">Independent, standalone mod — requires only Harmony. Not affiliated with or endorsed by NVIDIA Corporation.</text>
</svg>`;

fs.writeFileSync(path.join(DIR, 'preview.svg'), preview);
const previewPng = render(preview, 1200);
fs.writeFileSync(path.join(DIR, 'preview.png'), previewPng);

console.log('emblem-512.png', emblem512.length, 'bytes');
console.log('emblem-256.png', emblem256.length, 'bytes');
console.log('preview.png', previewPng.length, 'bytes');
