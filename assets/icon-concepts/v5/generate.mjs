// Modify only the detached power bar in the existing D5 vector.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const sharp = createRequire(import.meta.url)('sharp');
const out = path.dirname(fileURLToPath(import.meta.url));
const source = await fs.readFile(path.join(out, '../v4/D5-lagodniejsze-petle.svg'), 'utf8');
const oldBar = '<path data-part="power-bar" d="M 256,100 L 256,140"/>';
const newBar = '<path data-part="power-bar" stroke-width="36" d="M 256,92 L 256,146"/>';
if (source.split(oldBar).length !== 2) throw new Error('Expected exactly one original D5 power bar');
const updated = source.replace(oldBar, newBar);
if (updated.replace(newBar, oldBar) !== source) throw new Error('An unrelated element changed');
const svg = updated.replaceAll('EasyShut D5: Łagodniejsze pętle S', 'EasyShut D5: grubsza i dłuższa kreska');
const id = 'D5-grubsza-dluzsza-kreska';

// Sample the existing cubic S path, measuring the gap between rounded strokes.
const s = source.match(/data-letter="S" d="([^"]+)"/)[1];
const commands = [...s.matchAll(/([MC])\s*([^MC]+)/g)];
let previous, gap = Infinity;
function measure(p) {
  const y = Math.max(92, Math.min(146, p[1]));
  gap = Math.min(gap, Math.hypot(p[0] - 256, p[1] - y) - 16 - 18);
}
for (const [, command, values] of commands) {
  const n = values.match(/-?\d+(?:\.\d+)?/g).map(Number);
  if (command === 'M') { previous = n; measure(previous); continue; }
  if (n.length !== 6) throw new Error('Unexpected S curve');
  for (let i = 0; i <= 32; i++) {
    const t = i / 32, u = 1 - t;
    measure([0, 1].map(k => u*u*u*previous[k] + 3*u*u*t*n[k] + 3*u*t*t*n[k+2] + t*t*t*n[k+4]));
  }
  previous = n.slice(4);
}
if (gap < 8) throw new Error(`Insufficient power bar clearance: ${gap}`);
await fs.writeFile(path.join(out, id + '.svg'), svg);
const png = await sharp(Buffer.from(svg), { density: 144 }).png().toBuffer();
const { data, info } = await sharp(png).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
let transparent = 0, opaque = 0;
if (info.width !== 1024 || info.height !== 1024) throw new Error('Incorrect PNG dimensions');
for (let i = 0; i < data.length; i += 4) {
  if (data[i+3] === 0) transparent++;
  else if (data[i] !== 255 || data[i+1] !== 255 || data[i+2] !== 255) throw new Error('Non-white artwork');
  if (data[i+3] === 255) opaque++;
}
if (!transparent || !opaque) throw new Error('Missing transparent background or solid artwork');
await fs.writeFile(path.join(out, id + '.png'), png);
const inner = x => x.slice(x.indexOf('<g '), x.lastIndexOf('</svg>'));
function card(art, x, title, detail) {
  return `<rect x="${x}" y="100" width="480" height="526" rx="18" fill="#20242b"/><svg x="${x+40}" y="110" width="400" height="400" viewBox="0 0 512 512">${inner(art)}</svg><text x="${x+240}" y="557" text-anchor="middle" fill="#fff" font-size="24" font-weight="600">${title}</text><text x="${x+240}" y="592" text-anchor="middle" fill="#b7c2d0" font-size="17">${detail}</text>`;
}
const board = `<svg xmlns="http://www.w3.org/2000/svg" width="1040" height="675"><rect width="1040" height="675" fill="#111419"/><g font-family="Segoe UI,Arial,sans-serif"><text x="28" y="48" fill="#fff" font-size="28" font-weight="600">EasyShut — D5 z mocniejszą kreską</text>${card(source, 28, 'D5 · poprzednia', 'Oryginalne proporcje kreski')}${card(svg, 532, 'D5 · poprawiona', 'Lekko grubsza i dłuższa kreska')}<text x="28" y="658" fill="#9daab9" font-size="14">Ciemne tło tylko w podglądzie. Pliki ikony mają przezroczyste tło.</text></g></svg>`;
await fs.writeFile(path.join(out, 'porownanie.svg'), board);
await sharp(Buffer.from(board)).png().toFile(path.join(out, 'porownanie.png'));
const checks = { lettersUnchanged: true, barWidthBefore: 32, barWidthAfter: 36,
  barCenterlineBefore: [100,140], barCenterlineAfter: [92,146],
  visibleBarLengthBefore: 72, visibleBarLengthAfter: 90,
  powerBarGapAt512px: Number(gap.toFixed(5)), pngWidth: info.width, pngHeight: info.height,
  allVisiblePixelsWhite: true, transparentPixels: transparent, opaquePixels: opaque };
await fs.writeFile(path.join(out, 'geometry-checks.json'), JSON.stringify(checks, null, 2) + '\n');
console.log(JSON.stringify(checks, null, 2));
