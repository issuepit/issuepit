#!/usr/bin/env node
// @ts-check
/**
 * Screenshot similarity checker for IssuePit documentation.
 *
 * Compares PNG screenshots in two directories and reports similarity percentages.
 * Screenshots above the similarity threshold are considered "nearly identical"
 * and are skipped when --apply is used.
 *
 * Usage:
 *   node scripts/compare-screenshots.js <new-dir> <existing-dir> [options]
 *
 * Options:
 *   --threshold=<0-1>   Similarity threshold above which screenshots are considered
 *                       unchanged (default: 0.99 = 99%)
 *   --apply             Copy only changed/new screenshots from <new-dir> to
 *                       <existing-dir>, leaving nearly-identical ones untouched.
 *   --output=<file>     Write the markdown similarity report to <file>.
 *
 * Exit codes:
 *   0  Report generated successfully (always, unless a fatal argument error occurs).
 */

'use strict';

const fs = require('fs');
const path = require('path');

// ---------------------------------------------------------------------------
// Argument parsing
// ---------------------------------------------------------------------------
const args = process.argv.slice(2);

const newDir = args.find((a) => !a.startsWith('--'));
const existingDir = args.filter((a) => !a.startsWith('--'))[1];

const thresholdArg = args.find((a) => a.startsWith('--threshold='));
const SIMILARITY_THRESHOLD = thresholdArg ? parseFloat(thresholdArg.split('=')[1]) : 0.99;

const APPLY = args.includes('--apply');

const outputArg = args.find((a) => a.startsWith('--output='));
const OUTPUT_FILE = outputArg ? outputArg.split('=').slice(1).join('=') : null;

if (!newDir || !existingDir) {
  console.error('Usage: node compare-screenshots.js <new-dir> <existing-dir> [--threshold=0.99] [--apply] [--output=<file>]');
  process.exit(1);
}

// ---------------------------------------------------------------------------
// Dependencies (installed by the workflow before this script runs)
// ---------------------------------------------------------------------------
let PNG, pixelmatch;
try {
  ({ PNG } = require('pngjs'));
  const pixelmatchModule = require('pixelmatch');
  // Handle both CommonJS default export and ES module interop wrappers
  pixelmatch = typeof pixelmatchModule === 'function' ? pixelmatchModule : pixelmatchModule.default;
} catch (err) {
  console.error('Missing dependencies. Run: npm install pngjs pixelmatch@5.3.0');
  console.error(err.message);
  process.exit(1);
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * @param {string} filePath
 * @returns {{ width: number, height: number, data: Buffer }}
 */
function loadPng(filePath) {
  const data = fs.readFileSync(filePath);
  return PNG.sync.read(data);
}

/**
 * Compare two PNG images and return the similarity ratio (0–1).
 * Returns similarity 0 (all pixels treated as different) if dimensions differ.
 *
 * @param {{ width: number, height: number, data: Buffer }} img1
 * @param {{ width: number, height: number, data: Buffer }} img2
 * @returns {{ similarity: number, differentPixels: number, totalPixels: number }}
 */
function comparePngs(img1, img2) {
  if (img1.width !== img2.width || img1.height !== img2.height) {
    // Dimension mismatch — treat as completely different
    const totalPixels = img1.width * img1.height;
    return { similarity: 0, differentPixels: totalPixels, totalPixels };
  }
  const { width, height } = img1;
  const totalPixels = width * height;
  const diffImg = new PNG({ width, height });
  const differentPixels = pixelmatch(img1.data, img2.data, diffImg.data, width, height, {
    threshold: 0.1,
  });
  return {
    similarity: (totalPixels - differentPixels) / totalPixels,
    differentPixels,
    totalPixels,
  };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

function main() {
  if (!fs.existsSync(newDir)) {
    console.error(`New screenshots directory not found: ${newDir}`);
    process.exit(1);
  }

  // If the existing dir doesn't exist yet (first run), treat all screenshots as new.
  const existingDirExists = fs.existsSync(existingDir);
  if (!existingDirExists) {
    fs.mkdirSync(existingDir, { recursive: true });
  }

  const newFiles = fs.readdirSync(newDir).filter((f) => f.endsWith('.png'));

  /** @type {Array<{filename: string, status: string, similarity: number|null, differentPixels?: number, totalPixels?: number}>} */
  const results = [];

  for (const filename of newFiles) {
    const newPath = path.join(newDir, filename);
    const existingPath = path.join(existingDir, filename);

    if (!fs.existsSync(existingPath)) {
      results.push({ filename, status: 'new', similarity: null });
      if (APPLY) {
        fs.copyFileSync(newPath, existingPath);
        console.log(`  🆕  ${filename} — copied (new)`);
      }
      continue;
    }

    let comparison;
    try {
      const newImg = loadPng(newPath);
      const existingImg = loadPng(existingPath);
      comparison = comparePngs(newImg, existingImg);
    } catch (err) {
      results.push({ filename, status: 'error', similarity: null });
      console.error(`  ❌  ${filename} — comparison error: ${err.message}`);
      continue;
    }

    const { similarity, differentPixels, totalPixels } = comparison;
    const pct = Math.round(similarity * 10000) / 100;
    const nearlyIdentical = similarity >= SIMILARITY_THRESHOLD;

    results.push({
      filename,
      status: nearlyIdentical ? 'unchanged' : 'changed',
      similarity: pct,
      differentPixels,
      totalPixels,
    });

    if (APPLY) {
      if (nearlyIdentical) {
        console.log(`  ✅  ${filename} — skipped (${pct}% similar, above threshold)`);
      } else {
        fs.copyFileSync(newPath, existingPath);
        console.log(`  🔄  ${filename} — updated (${pct}% similar, below threshold)`);
      }
    }
  }

  // -------------------------------------------------------------------------
  // Markdown report
  // -------------------------------------------------------------------------
  const lines = [];
  lines.push('## Screenshot Similarity Report');
  lines.push('');
  lines.push(`Threshold: **${Math.round(SIMILARITY_THRESHOLD * 100)}%** — screenshots at or above this similarity are considered unchanged and not updated.`);
  lines.push('');
  lines.push('| Screenshot | Status | Similarity |');
  lines.push('|------------|--------|------------|');

  for (const r of results) {
    const sim = r.similarity !== null ? `${r.similarity}%` : '—';
    let icon;
    if (r.status === 'unchanged') icon = '✅ unchanged';
    else if (r.status === 'new') icon = '🆕 new';
    else if (r.status === 'changed') icon = '🔄 changed';
    else icon = '❌ error';
    lines.push(`| \`${r.filename}\` | ${icon} | ${sim} |`);
  }

  lines.push('');
  const changed = results.filter((r) => r.status === 'changed');
  const unchanged = results.filter((r) => r.status === 'unchanged');
  const newScreenshots = results.filter((r) => r.status === 'new');
  lines.push(
    `**Summary:** ${unchanged.length} unchanged · ${changed.length} changed · ${newScreenshots.length} new`,
  );
  lines.push('');

  const report = lines.join('\n');

  console.log('\n' + report);

  if (OUTPUT_FILE) {
    const outputDir = path.dirname(path.resolve(OUTPUT_FILE));
    if (outputDir !== '/') {
      fs.mkdirSync(outputDir, { recursive: true });
    }
    fs.writeFileSync(OUTPUT_FILE, report, 'utf8');
    console.log(`Report saved to ${OUTPUT_FILE}`);
  }
}

main();
