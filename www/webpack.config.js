const path = require("path");
const HtmlWebpackPlugin = require("html-webpack-plugin");

module.exports = {
  mode: 'production',
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
