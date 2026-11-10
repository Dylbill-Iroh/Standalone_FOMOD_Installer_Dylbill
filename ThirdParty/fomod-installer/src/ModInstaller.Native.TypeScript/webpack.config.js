const path = require('path');
// vortex-api's exports field doesn't expose bin/webpack.js, so resolve manually
const webpack = require(path.join(__dirname, 'node_modules', 'vortex-api', 'bin', 'webpack.js')).default;

const config = webpack('fomod-installer-native', __dirname, 5);

// resolve-native is kept as a separate runtime file (not bundled) because it
// uses dynamic require() to load platform-specific .node binaries at runtime.
config.externals = config.externals || {};
config.externals['./resolve-native'] = './resolve-native';

module.exports = config;