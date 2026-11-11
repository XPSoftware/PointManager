// Generates a multi-size .ico (PNG-compressed entries) for the tray icon.
// Design: indigo rounded circle with a white five-point star (points motif).
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

function crc32(buf) {
  if (typeof zlib.crc32 === 'function') return zlib.crc32(buf);
  let c, table = crc32.table;
  if (!table) {
    table = crc32.table = [];
    for (let n = 0; n < 256; n++) {
      c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      table[n] = c >>> 0;
    }
  }
  c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = table[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(td), 0);
  return Buffer.concat([len, td, crc]);
}

function encodePng(size, rgba) {
  const sig = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;  // bit depth
  ihdr[9] = 6;  // color type RGBA
  ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  // add filter byte (0) per scanline
  const raw = Buffer.alloc((size * 4 + 1) * size);
  let o = 0;
  for (let y = 0; y < size; y++) {
    raw[o++] = 0;
    rgba.copy(raw, o, y * size * 4, (y + 1) * size * 4);
    o += size * 4;
  }
  const idat = zlib.deflateSync(raw, { level: 9 });
  return Buffer.concat([sig, chunk('IHDR', ihdr), chunk('IDAT', idat), chunk('IEND', Buffer.alloc(0))]);
}

// Star polygon test
function starPoints(cx, cy, outer, inner) {
  const pts = [];
  for (let i = 0; i < 10; i++) {
    const r = i % 2 === 0 ? outer : inner;
    const a = -Math.PI / 2 + (i * Math.PI) / 5;
    pts.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
  }
  return pts;
}
function inPoly(pts, x, y) {
  let inside = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
}

function render(size) {
  const buf = Buffer.alloc(size * size * 4);
  const ss = 3; // supersampling
  const cx = size / 2, cy = size / 2;
  const R = size / 2 - Math.max(0.5, size * 0.02);
  const star = starPoints(cx, cy + size * 0.01, size * 0.31, size * 0.31 * 0.42);
  const bg = [63, 81, 181];   // indigo #3F51B5
  const fg = [255, 255, 255];
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let covCircle = 0, covStar = 0;
      for (let sy = 0; sy < ss; sy++) {
        for (let sx = 0; sx < ss; sx++) {
          const px = x + (sx + 0.5) / ss, py = y + (sy + 0.5) / ss;
          const d = Math.hypot(px - cx, py - cy);
          const c = d <= R - 0.5 ? 1 : d >= R + 0.5 ? 0 : R + 0.5 - d;
          covCircle += c;
          if (c > 0 && inPoly(star, px, py)) covStar += 1;
        }
      }
      const tot = ss * ss;
      covCircle /= tot; covStar /= tot;
      const i = (y * size + x) * 4;
      if (covCircle <= 0) { buf[i + 3] = 0; continue; }
      // blend star over circle bg
      let r = bg[0], g = bg[1], b = bg[2];
      const s = Math.min(covStar, covCircle) / Math.max(covCircle, 1e-6);
      r = r * (1 - s) + fg[0] * s;
      g = g * (1 - s) + fg[1] * s;
      b = b * (1 - s) + fg[2] * s;
      buf[i] = Math.round(r); buf[i + 1] = Math.round(g); buf[i + 2] = Math.round(b);
      buf[i + 3] = Math.round(covCircle * 255);
    }
  }
  return buf;
}

const sizes = [16, 24, 32, 48, 64, 128, 256];
const pngs = sizes.map((s) => ({ size: s, png: encodePng(s, render(s)) }));

const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(pngs.length, 4);

const entries = [];
let offset = 6 + 16 * pngs.length;
for (const p of pngs) {
  const e = Buffer.alloc(16);
  e[0] = p.size >= 256 ? 0 : p.size;
  e[1] = p.size >= 256 ? 0 : p.size;
  e[2] = 0; e[3] = 0;
  e.writeUInt16LE(1, 4);   // planes
  e.writeUInt16LE(32, 6);  // bit count
  e.writeUInt32LE(p.png.length, 8);
  e.writeUInt32LE(offset, 12);
  offset += p.png.length;
  entries.push(e);
}

const ico = Buffer.concat([header, ...entries, ...pngs.map((p) => p.png)]);
const outDir = path.join(__dirname, '..', 'Assets');
fs.mkdirSync(outDir, { recursive: true });
const out = path.join(outDir, 'app.ico');
fs.writeFileSync(out, ico);
console.log('wrote', out, ico.length, 'bytes');
