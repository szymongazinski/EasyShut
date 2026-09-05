// Corrected native-vector concepts. E is a circular arc plus ONE middle arm.
// The vertical construction is primary: rotated S above rotated E.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const sharp = createRequire(import.meta.url)('sharp');
const out = path.dirname(fileURLToPath(import.meta.url));
const cx = 256, cy = 296, radius = 176;
const n = v => Number(v.toFixed(3));
const xy = p => `${n(p[0])},${n(p[1])}`;
const point = angle => {
  const t = angle * Math.PI / 180;
  return [cx + radius * Math.cos(t), cy + radius * Math.sin(t)];
};
// Two equal ellipses and their exact inner common tangent form S.
// All arc-to-line joins are tangent-continuous, without hand-shaped kinks.
function sPath(style) {
  const {rx,ry,sy}=style, left=80+rx, right=432-rx;
  const cos=2*rx/(right-left), sin=Math.sqrt(1-cos*cos);
  const exit=[left+rx*cos,sy+ry*sin], enter=[right-rx*cos,sy-ry*sin];
  return `M ${left},${sy-ry} A ${rx},${ry} 0 1 0 ${xy(exit)} L ${xy(enter)} A ${rx},${ry} 0 1 1 ${right},${sy+ry}`;
}
const styles = [
  { key:'plynny', name:'Płynny', width:24, rx:80, ry:78, sy:202, arm:300 },
  { key:'okragly', name:'Okrągły', width:26, rx:74, ry:74, sy:202, arm:300 },
  { key:'mocny', name:'Mocny', width:32, rx:77, ry:82, sy:200, arm:307 }
];
const variants = ['pionowy','poziomy'].flatMap((orientation,row) => styles.map((style,col) => ({
  number:row*3+col+1, id:`${row*3+col+1}-${orientation}-${style.key}`,
  orientation, style, title:`${orientation === 'pionowy' ? 'Pionowy' : 'Poziomy'} · ${style.name.toLowerCase()}`
})));
function artwork(v) {
  const s = v.style;
  const rotation = v.orientation === 'poziomy' ? ` transform="rotate(90 ${cx} ${cy})"` : '';
  // The two ends of the arc are E's outer arms. Only one internal arm is drawn.
  const e = `M ${xy(point(0))} A ${radius},${radius} 0 0 1 ${xy(point(180))}`;
  return `<g fill="none" stroke="#fff" stroke-width="${s.width}" stroke-linecap="round" stroke-linejoin="round"><g${rotation}><path data-letter="E-contour" d="${e}"/><path data-letter="E-middle-arm" d="M 256,472 L 256,${s.arm}"/><path data-letter="S" d="${sPath(s)}"/></g><path data-part="power-bar" d="M 256,36 L 256,76"/></g>`;
}
for (const v of variants) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512" role="img" aria-label="EasyShut ${v.number}: ${v.title}"><title>EasyShut ${v.number}: ${v.title}</title>${artwork(v)}</svg>\n`;
  await fs.writeFile(path.join(out,v.id+'.svg'),svg);
  await sharp(Buffer.from(svg), {density:144}).png().toFile(path.join(out,v.id+'.png'));
}
function card(v,x,y,w=392) {
  return `<rect x="${x}" y="${y}" width="${w}" height="374" rx="16" fill="#20242b"/><svg x="${x+(w-278)/2}" y="${y+4}" width="278" height="278" viewBox="0 0 512 512">${artwork(v)}</svg><text x="${x+w/2}" y="${y+319}" text-anchor="middle" font-size="22" font-weight="600" fill="#fff">${v.number}. ${v.style.name}</text><svg x="${x+w/2-43}" y="${y+333}" width="28" height="28" viewBox="0 0 512 512">${artwork(v)}</svg><svg x="${x+w/2+8}" y="${y+329}" width="36" height="36" viewBox="0 0 512 512">${artwork(v)}</svg>`;
}
const board = `<svg xmlns="http://www.w3.org/2000/svg" width="1260" height="962" viewBox="0 0 1260 962"><rect width="1260" height="962" fill="#111419"/><g font-family="Segoe UI,Arial,sans-serif"><text x="28" y="46" font-size="26" font-weight="600" fill="#fff">EasyShut — poprawione propozycje</text><text x="28" y="79" font-size="16" fill="#b8c1ce">E ma jeden środkowy element. Łuk tworzy dwa pozostałe ramiona litery.</text><text x="28" y="117" font-size="17" fill="#e0e6ee">PIONOWO — kreska zasilania, S u góry, E u dołu</text>${variants.slice(0,3).map((v,i)=>card(v,28+i*406,132)).join('')}<text x="28" y="547" font-size="17" fill="#e0e6ee">POZIOMO — E po lewej, S po prawej</text>${variants.slice(3).map((v,i)=>card(v,28+i*406,562)).join('')}<text x="1232" y="46" text-anchor="end" font-size="14" fill="#aeb8c5">PNG + SVG · biały znak · przezroczyste tło</text></g></svg>`;
await fs.writeFile(path.join(out,'porownanie.svg'),board);
await sharp(Buffer.from(board)).png().toFile(path.join(out,'porownanie.png'));
for (const orientation of ['pionowy','poziomy']) {
  const caption = orientation === 'pionowy' ? 'Pionowo: kreska → S → E' : 'Poziomo: E + S, kreska nad środkiem';
  const strip = `<svg xmlns="http://www.w3.org/2000/svg" width="1260" height="474"><rect width="1260" height="474" fill="#111419"/><g font-family="Segoe UI,Arial,sans-serif"><text x="28" y="46" font-size="24" font-weight="600" fill="#fff">${caption}</text>${variants.filter(v=>v.orientation===orientation).map((v,i)=>card(v,28+i*406,72)).join('')}</g></svg>`;
  await sharp(Buffer.from(strip)).png().toFile(path.join(out,`${orientation}-porownanie.png`));
}
const section = orientation => `<h2>${orientation==='pionowy'?'Pionowo — S na górze, E na dole':'Poziomo — E po lewej, S po prawej'}</h2><div class="grid">${variants.filter(v=>v.orientation===orientation).map(v=>`<article class="card"><img src="${v.id}.svg" alt="${v.number}. ${v.title}"><h3>${v.number}. ${v.style.name}</h3><a href="${v.id}.png" download>PNG 1024 px</a><a href="${v.id}.svg" download>SVG</a></article>`).join('')}</div>`;
await fs.writeFile(path.join(out,'index.html'),`<!doctype html><html lang="pl"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>EasyShut — poprawione ikony</title><style>body{margin:0;background:#111419;color:white;padding:28px;font:16px 'Segoe UI',sans-serif}h1{font-size:27px}h2{font-size:19px;margin-top:32px}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px}.card{background:#20242b;text-align:center;padding:20px;border-radius:16px}.card img{width:100%;max-width:290px}.card h3{font-size:22px;margin:6px}a{color:#b5d5ff;display:inline-block;margin:10px}p{color:#b8c1ce}@media(max-width:650px){.grid{grid-template-columns:1fr}}</style><h1>EasyShut — poprawione propozycje</h1><p>E: dwa ramiona z łuku oraz jedna kreska w środku. Pliki ikon są białe i przezroczyste; ciemne tło służy wyłącznie do podglądu.</p>${section('pionowy')}${section('poziomy')}</html>`);
console.log('Rendered 3 vertical and 3 horizontal native-vector concepts.');
