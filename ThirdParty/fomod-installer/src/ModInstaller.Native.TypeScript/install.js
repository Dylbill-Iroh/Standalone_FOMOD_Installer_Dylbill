#!/usr/bin/env node

'use strict';

var path = require('path');
var fs = require('fs');

var prebuildsDir = path.join(__dirname, 'prebuilds');
if (!fs.existsSync(prebuildsDir)) {
  // No prebuilds directory — this is a development install (e.g. pnpm install
  // in the repo). The native binaries will be built separately via build.js.
  process.exit(0);
}

// The .node addon's rpath points to the package root (<module_root_dir>).
// Copy the companion .dll/.so/.lib from prebuilds/ to the package root so the
// dynamic linker can find them when the .node is loaded.
var platformDir = path.join(prebuildsDir, process.platform + '-' + process.arch);
if (fs.existsSync(platformDir)) {
  fs.readdirSync(platformDir).forEach(function (file) {
    if (/\.(dll|lib|so)$/i.test(file)) {
      var src = path.join(platformDir, file);
      var dst = path.join(__dirname, file);
      fs.copyFileSync(src, dst);
    }
  });
}

try {
  // Try to load the addon — works if a prebuild matches this platform
  require('node-gyp-build')(__dirname);
} catch (_) {
  // Prebuild missing or broken — compile the .node from source.
  // The .dll/.so/.lib are already copied above for linking.
  var result = require('child_process').spawnSync(
    'node-gyp', ['rebuild', '--jobs', 'max'],
    { stdio: 'inherit', cwd: __dirname, shell: true }
  );
  process.exit(result.status != null ? result.status : 1);
}
