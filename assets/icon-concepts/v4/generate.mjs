// Controlled native-vector experiments around v3 D. E and power bar are fixed.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const sharp=createRequire(import.meta.url)('sharp');
const out=path.dirname(fileURLToPath(import.meta.url));
const width=32, circle=[256,296], radius=176;
const rx=65.45, ry=70.52, left=171.85, right=340.15, cy=211.48;
const num=v=>Number(v.toFixed(5));
const xy=p=>p.map(num).join(',');
const variants=[
  {id:'D1-subtelny',label:'D1',title:'Subtelna korekta',detail:'Najbliżej dotychczasowego D',shrink:.04,inset:2.5},
  {id:'D2-lagodny-obrys',label:'D2',title:'Łagodniejszy obrys',detail:'Lekko cofnięte górne łuki S',shrink:.08,inset:5},
  {id:'D3-luki-kola',label:'D3',title:'Łuki bliżej koła',detail:'Mocniej dopasowane, nadal płynne S',shrink:.11,inset:9,tangentScale:1.02},
  {id:'D4-otwarte-koncowki',label:'D4',title:'Lżejsze końcówki',detail:'Delikatnie krótsze końce S',shrink:.08,inset:5,trim:7},
  {id:'D5-lagodniejsze-petle',label:'D5',title:'Łagodniejsze pętle S',detail:'Okrąglejsze zaokrąglenia obu części S',shrink:.035,inset:3,flatten:65.45/70.52}
];
const base={id:'D-baza',label:'D · BAZA',title:'Dotychczasowy wariant',detail:'Punkt wyjścia do porównania',shrink:0,inset:0};
function ellipse(center,v){
  const flatten=v.flatten??1,c=[center,282+(cy-282)*flatten];
  const dx=c[0]-circle[0],dy=c[1]-circle[1],length=Math.hypot(dx,dy),ux=dx/length,uy=dy/length;
  const radial=1-v.shrink,tan=v.tangentScale??1,k=radial-tan;
  // Gently compress each entire ellipse in its outward radial direction.
  // Keeping an ellipse, rather than projecting points onto a circle, avoids cusps.
  const a=(tan+k*ux*ux)*rx,b=k*ux*uy*ry*flatten;
  const d=k*ux*uy*rx,e=(tan+k*uy*uy)*ry*flatten;
  const origin=[c[0]-ux*v.inset,c[1]-uy*v.inset];
  const q00=a*a+b*b,q01=a*d+b*e,q11=d*d+e*e;
  return{
    origin,
    at:t=>[origin[0]+a*Math.cos(t)+b*Math.sin(t),origin[1]+d*Math.cos(t)+e*Math.sin(t)],
    support:n=>{
      const x=q00*n[0]+q01*n[1],y=q01*n[0]+q11*n[1],h=Math.sqrt(n[0]*x+n[1]*y);
      return{h,p:[x/h,y/h]};
    },
    parameter:p=>{
      const x=p[0]-origin[0],y=p[1]-origin[1],det=a*e-b*d;
      return Math.atan2((a*y-d*x)/det,(e*x-b*y)/det);
    }
  };
}
function segments(v){
  const trim=(v.trim??0)*Math.PI/180;
  const l=ellipse(left,v),r=ellipse(right,v);
  // Solve the inner common tangent of the two rotated ellipses. Both joins
  // therefore have exactly the same tangent direction as the middle line.
  let low=0,high=Math.PI/2;
  for(let i=0;i<64;i++){
    const theta=(low+high)/2,n=[Math.cos(theta),Math.sin(theta)];
    const h=l.support(n).h+r.support(n).h;
    const separation=n[0]*(r.origin[0]-l.origin[0])+n[1]*(r.origin[1]-l.origin[1]);
    if(separation>h)low=theta;else high=theta;
  }
  const normal=[Math.cos((low+high)/2),Math.sin((low+high)/2)],ls=l.support(normal).p,rs=r.support(normal).p;
  const exit=[l.origin[0]+ls[0],l.origin[1]+ls[1]],enter=[r.origin[0]-rs[0],r.origin[1]-rs[1]];
  let a1=l.parameter(exit);while(a1>-Math.PI/2)a1-=2*Math.PI;
  let b0=r.parameter(enter);while(b0<0)b0+=2*Math.PI;
  const a0=-Math.PI/2-trim,b1=5*Math.PI/2-trim;
  return[
    t=>l.at(a0+(a1-a0)*t),
    t=>[exit[0]+(enter[0]-exit[0])*t,exit[1]+(enter[1]-exit[1])*t],
    t=>r.at(b0+(b1-b0)*t)
  ];
}
function tangent(f,t){const h=1e-5,a=f(t-h),b=f(t+h);return[(b[0]-a[0])/(2*h),(b[1]-a[1])/(2*h)];}
function sPath(v){
  const curves=segments(v);let d='M '+xy(curves[0](0));
  // Piecewise cubic Hermite interpolation keeps all joins smooth and accurate.
  for(let k=0;k<curves.length;k++){
    const f=curves[k],count=k===1?32:112;
    for(let i=0;i<count;i++){
      const a=i/count,b=(i+1)/count,p=f(a),q=f(b),u=tangent(f,a),w=tangent(f,b),h=(b-a)/3;
      d+=` C ${xy([p[0]+h*u[0],p[1]+h*u[1]])} ${xy([q[0]-h*w[0],q[1]-h*w[1]])} ${xy(q)}`;
    }
  }return d;
}
const shape=new Map([...variants,base].map(v=>[v.id,sPath(v)]));
function artwork(v){return `<g fill="none" stroke="#fff" stroke-width="32" stroke-linecap="round" stroke-linejoin="round"><path data-letter="E-contour" d="M 432,296 A 176,176 0 0 1 80,296"/><path data-letter="E-middle-arm" d="M 256,472 L 256,307"/><path data-letter="S" d="${shape.get(v.id)}"/><path data-part="power-bar" d="M 256,100 L 256,140"/></g>`;}
function clearance(v){
  let bar=Infinity,e=Infinity,maxMove=0;
  const baseline=segments(base),parts=segments(v);
  for(let j=0;j<3;j++)for(let i=0;i<=8192;i++){
    const p=parts[j](i/8192),q=baseline[j](i/8192);
    const toSegment=(x,y1,y2)=>Math.hypot(p[0]-x,p[1]<y1?p[1]-y1:p[1]>y2?p[1]-y2:0);
    bar=Math.min(bar,toSegment(256,100,140));
    // S lies above the lower semicircle; its nearest arc points are its ends.
    e=Math.min(e,toSegment(256,307,472),Math.hypot(p[0]-80,p[1]-296),Math.hypot(p[0]-432,p[1]-296));
    maxMove=Math.max(maxMove,Math.hypot(p[0]-q[0],p[1]-q[1]));
  }
  if(bar-width<8||e-width<10)throw new Error(`Insufficient clearance for ${v.id}: bar ${bar-width}, E ${e-width}`);
  return{id:v.id,powerBarGapAt512px:num(bar-width),letterGapAt512px:num(e-width),maxSampleDisplacementFromD:num(maxMove)};
}
const checks=[];
for(const v of [...variants,base]){
  checks.push(clearance(v));
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512" role="img" aria-label="EasyShut ${v.label}: ${v.title}"><title>EasyShut ${v.label}: ${v.title}</title>${artwork(v)}</svg>\n`;
  await fs.writeFile(path.join(out,v.id+'.svg'),svg);
  await sharp(Buffer.from(svg),{density:144}).png().toFile(path.join(out,v.id+'.png'));
}
await fs.writeFile(path.join(out,'geometry-checks.json'),JSON.stringify(checks,null,2)+'\n');
const all=[base,...variants];
function card(v,x,y){return `<rect x="${x}" y="${y}" width="416" height="413" rx="18" fill="${v===base?'#191e25':'#20242b'}"/><svg x="${x+52}" y="${y+3}" width="312" height="312" viewBox="0 0 512 512">${artwork(v)}</svg><text x="${x+208}" y="${y+338}" text-anchor="middle" font-size="23" font-weight="600" fill="#fff">${v.label}</text><text x="${x+208}" y="${y+369}" text-anchor="middle" font-size="18" fill="#d3dbe5">${v.title}</text><text x="${x+208}" y="${y+397}" text-anchor="middle" font-size="13" fill="#9daab9">${v.detail}</text>`;}
const board=`<svg xmlns="http://www.w3.org/2000/svg" width="1340" height="987"><rect width="1340" height="987" fill="#111419"/><g font-family="Segoe UI,Arial,sans-serif"><text x="30" y="48" font-size="28" font-weight="600" fill="#fff">EasyShut — eksperymenty blisko D</text><text x="30" y="83" font-size="17" fill="#b7c2d0">Zmieniają się łuki S. Układ pionowy, E i kreska zasilania pozostają takie same.</text>${all.map((v,i)=>card(v,30+(i%3)*432,112+Math.floor(i/3)*429)).join('')}<text x="30" y="976" font-size="14" fill="#9daab9">Ciemne tło tylko do porównania. Każdy wariant jest dostępny jako biała ikona PNG i SVG z przezroczystością.</text></g></svg>`;
await fs.writeFile(path.join(out,'porownanie.svg'),board);
await sharp(Buffer.from(board)).png().toFile(path.join(out,'porownanie.png'));
const guide=`<circle class="guide" cx="256" cy="296" r="176" fill="none" stroke="#66c6ff" stroke-width="1.5" stroke-dasharray="6 6"/>`;
await fs.writeFile(path.join(out,'index.html'),`<!doctype html><html lang="pl"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>EasyShut — eksperymenty blisko D</title><style>body{margin:0;background:#111419;color:white;padding:28px;font:16px 'Segoe UI',sans-serif}h1{font-size:28px}p{color:#b7c2d0}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}.card{background:#20242b;border-radius:16px;text-align:center;padding:16px}.card svg{width:100%;max-width:320px}.card h2{font-size:24px;margin:4px}.card h3{font-size:18px;font-weight:400}.card p{font-size:14px}a{color:#b8dcff;display:inline-block;margin:8px}.guide{display:none}body.guides .guide{display:block}label{display:block;margin:22px 0}@media(max-width:900px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:550px){.grid{grid-template-columns:1fr}}</style><h1>EasyShut — eksperymenty blisko D</h1><p>Pięć propozycji obok dotychczasowego D. E, grubość linii i kreska zasilania bez zmian. Kreska nie dotyka S.</p><label><input type="checkbox" onchange="document.body.classList.toggle('guides',this.checked)"> Pokaż obrys koła do porównania</label><div class="grid">${all.map(v=>`<article class="card"><svg viewBox="0 0 512 512" role="img" aria-label="${v.label}">${guide}${artwork(v)}</svg><h2>${v.label}</h2><h3>${v.title}</h3><p>${v.detail}</p><a href="${v.id}.png" download>PNG 1024 px</a><a href="${v.id}.svg" download>SVG</a></article>`).join('')}</div><p>Obrys koła i ciemne tło są tylko w podglądzie. Osobne pliki ikon są białe i przezroczyste.</p></html>`);
console.log(JSON.stringify(checks,null,2));
