const path = require("path");
const HtmlWebpackPlugin = require("html-webpack-plugin");
const webpack = require('webpack');
require('dotenv').config({ path: './.env' });

module.exports = {
  mode: 'development',
  entry: "./www/src/index.js",
  stats: {
    warnings: false
  },
  performance: {
    hints: false
  },
  output: {
    filename: "main.js",
    path: path.resolve(__dirname, "build"),
  },
  plugins: [
    new HtmlWebpackPlugin({
      template: path.join(__dirname, "public", "index.html"),
      favicon: path.join(__dirname, "public", "favicon.ico"),
    }),
    new webpack.DefinePlugin({
      'pubhost': JSON.stringify(process.env.PUBHOST),
      'pubport': JSON.stringify(process.env.PUBPORT),
      'autoplay': JSON.stringify(process.env.ENABLE_AUTOPLAY),
    })
  ],
  module: {
    rules: [
      {
        test: /\.(js|jsx)$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: {
            presets: [
              "@babel/preset-env",
              [
                "@babel/preset-react", { "runtime": "automatic" }
              ]
            ]
          }
        }
      },
    ],
  },
  resolve: {
    extensions: ["*", ".js", ".jsx"],
  },
};
