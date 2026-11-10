const path = require('path');
// vortex-api's exports field doesn't expose bin/webpack.js, so resolve manually
const webpack = require(path.join(__dirname, 'node_modules', 'vortex-api', 'bin', 'webpack.js')).default;

const config = webpack('fomod-installer-ipc', __dirname, 5);

module.exports = config;