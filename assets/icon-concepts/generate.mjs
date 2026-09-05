// Native vector artwork: no generated bitmap is used as a rendering input.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const sharp = require('sharp');
const out = path.dirname(fileURLToPath(import.meta.url));
const concepts = [
  {
    id: '01-czytelny', title: 'Czytelny', subtitle: 'Oddzielne E i S', width: 29,
    paths: [
      'M 207,133 C 146,149 98,210 98,279 C 98,359 156,417 226,425',
      'M 106,229 L 217,229', 'M 101,310 L 216,310',
      'M 406,247 C 391,191 349,147 309,137 C 280,153 262,181 262,215 C 262,251 293,270 338,285 C 383,300 405,319 401,351 C 396,390 343,419 279,425'
    ], bar: 'M 256,64 L 256,126'
  },
  {
    id: '02-spleciony', title: 'Spleciony', subtitle: 'Płynnie połączone litery', width: 29,
    paths: [
      'M 207,128 C 132,135 80,204 80,276 C 80,365 148,425 246,425 C 307,425 344,395 344,359 C 344,331 322,312 290,289 C 254,263 238,245 243,222 C 249,190 290,177 328,185 C 388,197 422,242 430,280',
      'M 96,230 L 193,230', 'M 95,311 L 213,311',
      'M 305,128 C 380,136 433,201 433,278 C 433,344 392,403 334,438'
    ], bar: 'M 256,64 L 256,117'
  },
  {
    id: '03-wyrazisty', title: 'Wyrazisty', subtitle: 'Grubsza linia, mała ikona', width: 42,
    paths: [
      'M 204,137 C 143,148 87,213 87,283 C 87,358 143,418 228,430',
      'M 99,253 L 215,253', 'M 104,340 L 225,340',
      'M 418,243 C 397,181 349,143 309,135 C 290,151 272,176 271,207 C 270,240 302,257 345,275 C 391,294 422,315 413,349 C 402,387 343,419 282,430'
    ], bar: 'M 256,51 L 256,112'
  },
  {
    id: '04-otwarty', title: 'Otwarty', subtitle: 'Lekki, prawie pełny okrąg', width: 24,
    paths: [
      'M 207,126 C 128,131 73,199 73,271 C 73,366 151,430 250,430 C 329,430 404,403 415,355 C 424,314 364,303 322,284 C 282,266 271,247 282,222 C 299,184 365,184 434,218',
      'M 306,126 C 365,128 416,164 434,218',
      'M 90,215 L 195,215', 'M 82,294 L 195,294'
    ], bar: 'M 256,64 L 256,122'
  },
  {
    id: '05-wspolna-linia', title: 'Wspólna linia', subtitle: 'Najbliżej odręcznego szkicu', width: 37,
    paths: [
      'M 209,137 C 139,137 76,199 76,274 C 76,356 143,422 231,435',
      'M 96,237 L 191,237',
      'M 84,316 C 147,324 197,316 224,270 C 246,230 246,198 284,163 C 324,125 386,139 418,177 C 457,223 412,264 365,264 L 320,264',
      'M 400,311 C 429,329 436,355 412,384 C 384,413 332,430 279,435'
    ], bar: 'M 256,51 L 256,113'
  }
];
function strokes(c) {
  return `<g fill="none" stroke="#ffffff" stroke-width="${c.width}" stroke-linecap="round" stroke-linejoin="round">${c.paths.map(d => `<path d="${d}"/>`).join('')}<path d="${c.bar}"/></g>`;
}
for (const c of concepts) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512" role="img" aria-label="EasyShut: ${c.title}"><title>EasyShut — ${c.title}</title>${strokes(c)}</svg>\n`;
  await fs.writeFile(path.join(out, c.id + '.svg'), svg);
  await sharp(Buffer.from(svg), { density: 144 }).png().toFile(path.join(out, c.id + '.png'));
}
// Comparison board is a separate presentation; the five icon files have no background.
const cards = concepts.map((c,i) => {
  const x = 28 + i * 292;
  return `<rect x="${x}" y="78" width="276" height="344" rx="18" fill="#20242b"/><svg x="${x+14}" y="91" width="248" height="248" viewBox="0 0 512 512">${strokes(c)}</svg><text x="${x+138}" y="370" text-anchor="middle" fill="#ffffff" font-family="Segoe UI,Arial,sans-serif" font-size="21" font-weight="600">${i+1}. ${c.title}</text><text x="${x+138}" y="399" text-anchor="middle" fill="#aeb8c5" font-family="Segoe UI,Arial,sans-serif" font-size="14">${c.subtitle}</text>`;
}).join('');
const sheet = `<svg xmlns="http://www.w3.org/2000/svg" width="1500" height="466" viewBox="0 0 1500 466"><rect width="1500" height="466" fill="#111419"/><text x="28" y="45" font-family="Segoe UI,Arial,sans-serif" font-size="23" font-weight="600" fill="#ffffff">EasyShut — 5 propozycji ikony</text><text x="1472" y="45" text-anchor="end" font-family="Segoe UI,Arial,sans-serif" font-size="15" fill="#aeb8c5">Biały znak · zaokrąglone linie · przezroczyste pliki PNG i SVG</text>${cards}<text x="28" y="450" font-family="Segoe UI,Arial,sans-serif" font-size="13" fill="#aeb8c5">Ciemne tło służy tylko do porównania. Każda ikona jest dostępna osobno bez tła.</text></svg>`;
await fs.writeFile(path.join(out, 'porownanie.svg'), sheet);
await sharp(Buffer.from(sheet)).png().toFile(path.join(out, 'porownanie.png'));
const html = `<!doctype html><html lang="pl"><meta charset="utf-8"><title>EasyShut — propozycje ikony</title><style>body{margin:0;background:#111419;color:#fff;font:16px 'Segoe UI',sans-serif;padding:32px}h1{font-size:26px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:18px}.card{background:#20242b;border-radius:16px;padding:20px;text-align:center}.card img{width:100%;max-width:280px}.card h2{font-size:21px}a{color:#afd9ff;margin:8px}p{color:#b6bec9}</style><h1>EasyShut — wybierz ikonę</h1><p>Ciemne tło jest wyłącznie podglądem. Pliki PNG i SVG mają przezroczyste tło.</p><div class="grid">${concepts.map((c,i)=>`<article class="card"><img src="${c.id}.svg" alt="Wariant ${i+1}: ${c.title}"><h2>${i+1}. ${c.title}</h2><p>${c.subtitle}</p><a href="${c.id}.png" download>PNG</a><a href="${c.id}.svg" download>SVG</a></article>`).join('')}</div></html>`;
await fs.writeFile(path.join(out, 'index.html'), html);
console.log('Created 5 native SVG icons, transparent 1024px PNG exports, and comparison views.');
