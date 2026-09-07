const fs = require('fs');
const path = require('path');
const { Resvg } = require('@resvg/resvg-js');

const DIR = __dirname;
const emblem = fs.readFileSync(path.join(DIR, 'emblem-512.png'));
const emblemUri = 'data:image/png;base64,' + emblem.toString('base64');

const W = 1080, H = 1780;
const FONT = 'Segoe UI, Arial, sans-serif';

// VRAM stacked bar segments (illustrative)
const barX = 70, barY = 486, barW = 940, barH = 46;
const segs = [
  { label: 'System', frac: 0.14, color: '#3a5877' },
  { label: 'RimWorld', frac: 0.20, color: '#3b82f6' },
  { label: 'LM Studio model', frac: 0.42, color: '#2dd4bf' },
  { label: 'Free', frac: 0.24, color: '#22304a' },
];
let acc = barX;
const segRects = segs.map(s => {
  const w = s.frac * barW;
  const r = `<rect x="${acc.toFixed(1)}" y="${barY}" width="${w.toFixed(1)}" height="${barH}" fill="${s.color}"/>`;
  const cx = acc + w / 2;
  acc += w;
  return { r, cx, ...s };
});
const segLabels = segRects.map(s =>
  `<text x="${s.cx.toFixed(1)}" y="${barY + barH + 30}" font-family="${FONT}" font-size="22" fill="#9fb3c8" text-anchor="middle">${s.label}</text>`
).join('');

// feature tiles 2 x 3
const tiles = [
  { t: 'Live telemetry', d: 'VRAM, temperature, power, clocks, fan — read straight from NVML.' },
  { t: 'VRAM breakdown', d: 'Splits used memory into System / RimWorld / your local model.' },
  { t: 'On-load advisory', d: 'Warns you before the model spills out of VRAM and stalls.' },
  { t: 'Overlay + dashboard', d: 'A toggleable in-game HUD and a full stats window.' },
  { t: 'Optional LM Studio', d: 'Reads the loaded model and estimates its VRAM footprint.' },
  { t: 'Remote-aware', d: 'A model on another machine is detected, never counted local.' },
];
const gx = 70, gy = 660, gw = 470, gh = 150, gapx = 30, gapy = 26;
const tileSvg = tiles.map((tile, i) => {
  const col = i % 2, row = (i / 2) | 0;
  const x = gx + col * (gw + gapx);
  const y = gy + row * (gh + gapy);
  return `<g>
    <rect x="${x}" y="${y}" width="${gw}" height="${gh}" rx="18" fill="#111e30" stroke="#22374f" stroke-width="1.5"/>
    <rect x="${x}" y="${y + 22}" width="6" height="${gh - 44}" rx="3" fill="#2dd4bf"/>
    <text x="${x + 30}" y="${y + 52}" font-family="${FONT}" font-size="30" font-weight="700" fill="#eaf1f8">${tile.t}</text>
    <text x="${x + 30}" y="${y + 92}" font-family="${FONT}" font-size="23" fill="#9fb3c8">${wrap(tile.d, 40).map((ln, k) => `<tspan x="${x + 30}" dy="${k ? 30 : 0}">${ln}</tspan>`).join('')}</text>
  </g>`;
}).join('');

function wrap(s, n) {
  const words = s.split(' '); const out = []; let line = '';
  for (const w of words) { if ((line + ' ' + w).trim().length > n) { out.push(line.trim()); line = w; } else line += ' ' + w; }
  if (line.trim()) out.push(line.trim());
  return out;
}

// why-it-helps bullets
const why = [
  'RimWorld now hosts local LLMs (RimTalk, the RimSynapse suite, and more) — the game and the model share one pool of VRAM.',
  'Running out of VRAM is the #1 failure: the model spills to system RAM and everything slows by an order of magnitude.',
  'This makes VRAM visible so you can right-size your model and context window — real numbers instead of guesswork.',
  'Works with any local LLM served over an OpenAI-compatible endpoint. No dependency on any specific mod.',
];
let whyCursor = 1310;
const whySvg = why.map((b) => {
  const lines = wrap(b, 82);
  const y = whyCursor;
  const g = `<g>
    <circle cx="86" cy="${y - 8}" r="6" fill="#2dd4bf"/>
    <text x="110" y="${y}" font-family="${FONT}" font-size="24" fill="#c6d5e4">${lines.map((ln, k) => `<tspan x="110" dy="${k ? 32 : 0}">${ln}</tspan>`).join('')}</text>
  </g>`;
  whyCursor += lines.length * 32 + 30;
  return g;
}).join('');

const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#0c1421"/><stop offset="1" stop-color="#090e18"/>
    </linearGradient>
    <linearGradient id="teal" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#2dd4bf"/><stop offset="1" stop-color="#5eead4"/>
    </linearGradient>
    <linearGradient id="strip" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#2dd4bf"/><stop offset="0.7" stop-color="#f6b53c"/><stop offset="1" stop-color="#ef6f5c"/>
    </linearGradient>
  </defs>
  <rect width="${W}" height="${H}" fill="url(#bg)"/>

  <!-- header -->
  <image x="60" y="54" width="150" height="150" href="${emblemUri}"/>
  <text x="232" y="120" font-family="${FONT}" font-size="60" font-weight="700" fill="#f4f7fb">GPU Monitor</text>
  <text x="235" y="170" font-family="${FONT}" font-size="34" font-weight="600" fill="url(#teal)">for NVIDIA GPUs</text>
  <text x="235" y="205" font-family="${FONT}" font-size="24" fill="#8ba0b6">See your VRAM before it runs out — inside RimWorld.</text>

  <!-- section 1 -->
  <text x="70" y="300" font-family="${FONT}" font-size="30" font-weight="700" fill="#2dd4bf">ONE GPU, TWO JOBS</text>
  <text x="70" y="352" font-family="${FONT}" font-size="26" fill="#c6d5e4"><tspan x="70">RimWorld draws the game while a local language model runs in the</tspan><tspan x="70" dy="34">background. Both live in the same VRAM — and when it fills up, the</tspan><tspan x="70" dy="34">model spills to system RAM and your framerate and tokens crater.</tspan></text>

  <!-- vram bar -->
  ${segRects.map(s => s.r).join('')}
  <rect x="${barX}" y="${barY}" width="${barW}" height="${barH}" rx="6" fill="none" stroke="#33445e" stroke-width="1.5"/>
  ${segLabels}
  <text x="${barX}" y="${barY - 14}" font-family="${FONT}" font-size="20" fill="#6f8398">Live VRAM breakdown — measured total, estimated split</text>

  <!-- section 2 -->
  <text x="70" y="628" font-family="${FONT}" font-size="30" font-weight="700" fill="#2dd4bf">WHAT IT SHOWS</text>
  ${tileSvg}

  <!-- section 3 -->
  <text x="70" y="1250" font-family="${FONT}" font-size="30" font-weight="700" fill="#2dd4bf">WHY IT HELPS LOCAL-AI PLAYERS</text>
  ${whySvg}

  <!-- footer -->
  <rect x="70" y="${H-116}" width="940" height="6" rx="3" fill="url(#strip)"/>
  <text x="70" y="${H-74}" font-family="${FONT}" font-size="22" fill="#8ba0b6">Standalone · requires only Harmony · reads NVIDIA's NVML library · zero save-file footprint</text>
  <text x="70" y="${H-40}" font-family="${FONT}" font-size="18" fill="#5f7488">Independent community mod — not affiliated with, endorsed by, or sponsored by NVIDIA Corporation.</text>
  <text x="70" y="${H-16}" font-family="${FONT}" font-size="18" fill="#5f7488">"NVIDIA" is a trademark of NVIDIA Corporation, used nominatively to describe hardware compatibility.</text>
</svg>`;

fs.writeFileSync(path.join(DIR, 'infographic.svg'), svg);
const png = new Resvg(svg, { fitTo: { mode: 'width', value: W }, font: { loadSystemFonts: true }, background: 'rgba(0,0,0,0)' }).render().asPng();
fs.writeFileSync(path.join(DIR, 'infographic.png'), png);
console.log('infographic.png', png.length, 'bytes', W + 'x' + H);
