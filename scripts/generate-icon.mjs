// Optional asset authoring step; ordinary application builds use the committed ICO.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const sharp = createRequire(import.meta.url)('sharp');
const assets = fileURLToPath(new URL('../assets/', import.meta.url));
const whiteSvg = await fs.readFile(path.join(assets, 'EasyShut.svg'), 'utf8');
for (const [name, source] of [['EasyShut', whiteSvg], ['EasyShut-black', whiteSvg.replaceAll('stroke="#fff"', 'stroke="#000"')]]) {
  const svg = Buffer.from(source);
  if (name !== 'EasyShut') await fs.writeFile(path.join(assets, name + '.svg'), svg);
  const sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
  const frames = [];
  for (const size of sizes) {
    const rgba = await sharp(svg, { density: 288 }).resize(size, size).ensureAlpha().raw().toBuffer();
    // Windows ICO: bottom-up 32-bit BGRA DIB followed by a DWORD-aligned AND mask.
    const stride = Math.ceil(size / 32) * 4;
    const pixels = size * size * 4;
    const dib = Buffer.alloc(40 + pixels + stride * size);
    dib.writeUInt32LE(40, 0);
    dib.writeInt32LE(size, 4);
    dib.writeInt32LE(size * 2, 8);
    dib.writeUInt16LE(1, 12);
    dib.writeUInt16LE(32, 14);
    dib.writeUInt32LE(pixels + stride * size, 20);
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      const src = (y * size + x) * 4, row = size - y - 1;
      const dest = 40 + (row * size + x) * 4;
      dib[dest] = rgba[src+2]; dib[dest+1] = rgba[src+1];
      dib[dest+2] = rgba[src]; dib[dest+3] = rgba[src+3];
      if (rgba[src+3] === 0) dib[40 + pixels + row * stride + (x >> 3)] |= 0x80 >> (x % 8);
    }
    frames.push(dib);
  }
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  for (let i = 0; i < sizes.length; i++) {
    const p = 6 + i * 16;
    header[p] = header[p+1] = sizes[i] === 256 ? 0 : sizes[i];
    header.writeUInt16LE(1, p+4); header.writeUInt16LE(32, p+6);
    header.writeUInt32LE(frames[i].length, p+8); header.writeUInt32LE(offset, p+12);
    offset += frames[i].length;
  }
  await fs.writeFile(path.join(assets, name + '.ico'), Buffer.concat([header, ...frames]));
  await sharp(svg, { density: 144 }).png().toFile(path.join(assets, name + '.png'));
  console.log(name + '.ico: ' + sizes.join(', ') + ' px; PNG: 1024 px');
}
