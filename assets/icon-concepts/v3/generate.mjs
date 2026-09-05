// Refinements of v2 option 3. Only S geometry and the power-bar height vary.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const sharp = createRequire(import.meta.url)('sharp');
const out = path.dirname(fileURLToPath(import.meta.url));
const round = v => Number(v.toFixed(4));
const xy = p => p.map(round).join(',');
const width = 32, rx = 77, ry = 82, left = 157, right = 355, centerY = 200;
const cos = 2 * rx / (right - left), angle = Math.acos(cos);
const variants = [
  { id:'A-delikatny', label:'A', title:'Delikatne zwężenie', detail:'S węższe o 6%', sx:.94, sy:1, barEnd:125 },
  { id:'B-umiarkowany', label:'B', title:'Lekkie ściśnięcie', detail:'S węższe o 10%, niższe o 5%', sx:.90, sy:.95, barEnd:132 },
  { id:'C-okragly', label:'C', title:'Bardziej okrągły', detail:'S węższe o 12%, niższe o 10%', sx:.88, sy:.90, barEnd:136 },
  { id:'D-zwarty', label:'D', title:'Najbardziej zwarty', detail:'S węższe o 15%, niższe o 14%', sx:.85, sy:.86, barEnd:140 }
];
const base = { id:'baza-opcja-3', sx:1, sy:1, barEnd:76, label:'BAZA', title:'Poprzednia opcja 3', detail:'' };
// Bake the scale into coordinates: stroke thickness and round caps remain 32.
const map = (v,p) => [256+(p[0]-256)*v.sx, 282+(p[1]-282)*v.sy];
function sPath(v) {
  const exit=map(v,[left+rx*cos,centerY+ry*Math.sin(angle)]);
  const enter=map(v,[right-rx*cos,centerY-ry*Math.sin(angle)]);
  const arc=`${round(rx*v.sx)},${round(ry*v.sy)}`;
  return `M ${xy(map(v,[left,centerY-ry]))} A ${arc} 0 1 0 ${xy(exit)} L ${xy(enter)} A ${arc} 0 1 1 ${xy(map(v,[right,centerY+ry]))}`;
}
function artwork(v) {
  return `<g fill="none" stroke="#fff" stroke-width="${width}" stroke-linecap="round" stroke-linejoin="round"><path data-letter="E-contour" d="M 432,296 A 176,176 0 0 1 80,296"/><path data-letter="E-middle-arm" d="M 256,472 L 256,307"/><path data-letter="S" d="${sPath(v)}"/><path data-part="power-bar" d="M 256,${v.barEnd-40} L 256,${v.barEnd}"/></g>`;
}
// Numerically check the outline gap, including the circular end caps. Sampling
// arcs is supplemented by the exact minimum distance to the central tangent.
function gap(v) {
  const barTop=v.barEnd-40, barBottom=v.barEnd;
  const distance=p=>Math.hypot(p[0]-256, p[1]<barTop?p[1]-barTop:p[1]>barBottom?p[1]-barBottom:0);
  let minimum=Infinity;
  const start=-Math.PI/2, end=angle-2*Math.PI;
  const begin=angle+Math.PI, finish=5*Math.PI/2;
  for(let i=0;i<=16384;i++) {
    const a=start+(end-start)*i/16384, b=begin+(finish-begin)*i/16384;
    minimum=Math.min(minimum,distance(map(v,[left+rx*Math.cos(a),centerY+ry*Math.sin(a)])),distance(map(v,[right+rx*Math.cos(b),centerY+ry*Math.sin(b)])));
  }
  const p=map(v,[left+rx*cos,centerY+ry*Math.sin(angle)]), q=map(v,[right-rx*cos,centerY-ry*Math.sin(angle)]);
  for(const y of [barTop,barBottom]) {
    const dx=q[0]-p[0],dy=q[1]-p[1];
    const t=Math.max(0,Math.min(1,((256-p[0])*dx+(y-p[1])*dy)/(dx*dx+dy*dy)));
    minimum=Math.min(minimum,Math.hypot(p[0]+t*dx-256,p[1]+t*dy-y));
  }
  return minimum-width;
}
const checks=[];
for(const v of [...variants,base]) {
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512" role="img" aria-label="EasyShut ${v.label}: ${v.title}"><title>EasyShut ${v.label}: ${v.title}</title>${artwork(v)}</svg>\n`;
  await fs.writeFile(path.join(out,v.id+'.svg'),svg);
  await sharp(Buffer.from(svg),{density:144}).png().toFile(path.join(out,v.id+'.png'));
  const minGap=gap(v);
  if(minGap<10) throw new Error(`${v.id}: power bar too close to S (${minGap}).`);
  checks.push({variant:v.id,barToSGapAt512px:round(minGap),sScaleX:v.sx,sScaleY:v.sy,powerBarTop:v.barEnd-40,powerBarBottom:v.barEnd});
}
await fs.writeFile(path.join(out,'geometry-checks.json'),JSON.stringify(checks,null,2)+'\n');
function card(v,x,y) {
  return `<rect x="${x}" y="${y}" width="516" height="435" rx="18" fill="#20242b"/><svg x="${x+94}" y="${y+1}" width="328" height="328" viewBox="0 0 512 512">${artwork(v)}</svg><text x="${x+258}" y="${y+366}" text-anchor="middle" font-size="23" font-weight="600" fill="#fff">${v.label}. ${v.title}</text><text x="${x+258}" y="${y+396}" text-anchor="middle" font-size="16" fill="#b6c0cd">${v.detail}</text>`;
}
const board=`<svg xmlns="http://www.w3.org/2000/svg" width="1120" height="1060" viewBox="0 0 1120 1060"><rect width="1120" height="1060" fill="#111419"/><g font-family="Segoe UI,Arial,sans-serif"><text x="32" y="49" font-size="28" font-weight="600" fill="#fff">EasyShut — dopracowanie opcji 3</text><text x="32" y="82" font-size="17" fill="#bac4d0">Pionowo · ściśnięte S · opuszczona kreska zasilania</text><text x="32" y="111" font-size="15" fill="#9daab9">To samo E i ta sama grubość linii. Kreska w żadnym wariancie nie dotyka S.</text>${variants.map((v,i)=>card(v,32+(i%2)*540,140+Math.floor(i/2)*453)).join('')}<text x="32" y="1042" font-size="14" fill="#9daab9">Ciemne tło służy tylko do porównania. Osobne ikony PNG i SVG mają przezroczyste tło.</text></g></svg>`;
await fs.writeFile(path.join(out,'porownanie.svg'),board);
await sharp(Buffer.from(board)).png().toFile(path.join(out,'porownanie.png'));
const all=[base,...variants];
const strip=`<svg xmlns="http://www.w3.org/2000/svg" width="1500" height="460"><rect width="1500" height="460" fill="#111419"/><g font-family="Segoe UI,Arial,sans-serif"><text x="28" y="43" font-size="24" fill="#fff">Od poprzedniej opcji 3 do bardziej okrągłego znaku</text>${all.map((v,i)=>`<rect x="${20+i*296}" y="67" width="280" height="365" rx="16" fill="#20242b"/><svg x="${30+i*296}" y="76" width="260" height="260" viewBox="0 0 512 512">${artwork(v)}</svg><text x="${160+i*296}" y="384" text-anchor="middle" font-size="23" fill="#fff">${v.label}</text>`).join('')}</g></svg>`;
await sharp(Buffer.from(strip)).png().toFile(path.join(out,'porownanie-z-baza.png'));
const html=`<!doctype html><html lang="pl"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>EasyShut — warianty opcji 3</title><style>body{margin:0;background:#111419;color:#fff;padding:28px;font:16px 'Segoe UI',sans-serif}h1{font-size:28px}p{color:#b6c0cd}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}.card{text-align:center;background:#20242b;padding:18px;border-radius:16px}.card img{width:100%;max-width:330px}a{color:#bedbff;display:inline-block;margin:8px}h2{font-size:23px}details{margin-top:28px}details img{width:100%}@media(max-width:650px){.grid{grid-template-columns:1fr}}</style><h1>EasyShut — dopracowanie opcji 3</h1><p>E i grubość linii są takie same we wszystkich wariantach. S jest lekko ściśnięte, a kreska opuszczona z zachowaniem przerwy.</p><div class="grid">${variants.map(v=>`<article class="card"><img src="${v.id}.svg" alt="Wariant ${v.label}"><h2>${v.label}. ${v.title}</h2><p>${v.detail}</p><a href="${v.id}.png" download>PNG 1024 px</a><a href="${v.id}.svg" download>SVG</a></article>`).join('')}</div><details><summary>Porównanie z poprzednią opcją 3</summary><img src="porownanie-z-baza.png" alt="Baza oraz warianty A, B, C i D"></details><p>Osobne pliki ikon są białe i przezroczyste. Ciemne tło jest tylko w podglądzie.</p></html>`;
await fs.writeFile(path.join(out,'index.html'),html);
console.log(JSON.stringify(checks,null,2));
