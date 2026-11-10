import * as path from 'path';

// node-gyp-build searches prebuilds/{platform}-{arch}/ for the correct binary,
// falling back to build/Release/ if a local compilation exists.
// The package root is one level up from dist/ where this file lives at runtime.
const packageRoot = path.resolve(__dirname, '..');

// eslint-disable-next-line @typescript-eslint/no-var-requires
export const addon: any = require('node-gyp-build')(packageRoot);
