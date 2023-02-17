# Imposter dev in VSCode

## What you will need

- [Visual Studio Code](https://code.visualstudio.com/) to edit your plugins
- [.NET Core SDK](https://dotnet.microsoft.com/download) to compile your code (optional)
- [.NET Framework 3.5 Targeting Pack](https://stackoverflow.com/a/47621616/154480) (you might already have this)
- [Virt-A-Mate](https://www.patreon.com/meshedvr/) to use your plugins

## Work on this plugin

Clone (or unzip) this repo under `(VaM install path)\Custom\Scripts\Author\Imposter`, replacing `Author` and `Imposter` by yours, so that the `Imposter.cs` is directly under the `Imposter` folder.

You should now be able to open the project in vscode by using `File`, `Open Folder` and select the `Imposter` folder.


## About `MVRScript`

The plugin is really a Unity [MonoBehavior](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html), which means you can use methods like `Update`, `FixedUpdate`, `OnEnable`, `OnDisable` and `OnDestroy`. `Init` however is called by Virt-A-Mate.

Keep in mind however that `OnEnable` will be called _before_ `Init`.

## Validate locally

You can run `dotnet build` in the plugin folder, and it'll show you any compilation errors. This is faster than going in VaM to do so! I recommend installing .NET 5 or more recent.


## License

[MIT](LICENSE.md)
