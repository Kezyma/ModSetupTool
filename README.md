# Mod Setup Tool

This tool is designed to be used with Wabbajack mod lists, or any mod list setup that requires the user to perform post-setup steps themselves. It can be configured to help the user navigate these steps and perform them automatically.

## Installation

- The tool should be downloaded from NexusMods and the zip should be included in the `downloads` folder of Mod Organizer 2, you may need to create a custom `meta.ini` file pointing to the mod to allow it to do this.
- The zip should be extracted and the contents placed directly in the Mod Organizer 2 folder, so `ModSetup.exe` is alongside `ModOrganizer.exe`.
- Create a `setup_steps.json` file to contain your configuration, or run `ModSetup.exe` once to create a demo configuration.
- Your `setup_steps.json` and any additional files you include must be marked to be included in Wabbajack.

The tool contains multiple files and folders that are required.
- `ModSetup.exe` - the executable to run the setup tasks.
- `Plugins\ModSetup.py` - a Mod Organizer 2 plugin to prevent Mod Organizer from launching without completing the setup steps.
- `setup_steps.json` - The steps to be executed when the mod setup tool runs.

The `Demo` folder contains files required by the default demonstration version of `setup_steps.json`, and is only required to run those steps. Once you write your own steps, you no longer need this.

### Meta.ini

As this tool is hosted on the modding tools section of NexusMods, you will need to create a meta file for it if you intend to include it in your Wabbajack list.
- Download the mod from NexusMods.
- Place the downloaded zip into your Mod Organizer downloads folder.
- Open Mod Organizer and navigate to the downloads list.
- Right click the zip you added in the list, and click `Open Meta File`.
- Add the following lines to the opened file;
```
gameName=site
modID=1003
fileID=FILE_ID
```
- To find the FILE_ID for this specific version of the download, go to the [NexusMods download page](https://www.nexusmods.com/site/mods/1003?tab=files) and find the file you are creating the meta information for.
- Right-click the `Mod Manager Download` or `Manual Download` button, and click `Copy link address`.
- Paste the link into notepad and look for `DownloadPopUp?id=`, the following number is the `FILE_ID` to add to the meta file.
- e.g. for `https://www.nexusmods.com/Core/Libs/Common/Widgets/DownloadPopUp?id=3827&game_id=2295` the file id is `3827`.

For reference, the `modID` is the number in the url for the current mod, and the `gameName` is the game listed in the url for the current mod. In the case of modding tools, the `gameName` is simply `site`.

e.g. for `https://www.nexusmods.com/site/mods/1003` the game name is `site` and the mod id is `1003`.

With the meta file configured, Wabbajack will be able to automatically install the setup tool alongside any mods you are also installing.

## Setup Steps

The `setup_steps.json` contains an array of steps, executed in order, each step can have multiple actions (or none at all) which are run when the user continues to the next step.

A simple step with no actions may look something like this.
```
[
	{
		"ContentPath": "Path\\to\\some\\markdown.md",
		"Actions": [],
		"Skippable": true
	}
]
```

- `ContentPath` - Specifies the relative path to a markdown file containing the content to be displayed for this step.
- `Content` - A raw string containing markdown to be displayed for this step (if no ContentPath has been specified).
- `SwitchStep` - If true, this step displays Yes or No options, and allows for different actions depending on the user's choice.
- `Skippable` - If true, the skip button will be enabled and this step can be bypassed.
- `Actions` - An array of actions to take when the user clicks the continue button.
- `YesActions` - If `SwitchStep` is enabled, an array of actions to take when the user clicks the yes button.
- `NoActions` - If `SwitchStep` is enabled, an array of actions to take when the user clicks the no button.

## Setup Actions

Each step can contain a selection of actions, either in the `Actions` array or in the `YesActions` and `NoActions` arrays if `SwitchStep` is enabled.

All actions have two main properties.
- `StepType` - Defines the type of action to run.
- `Wait` - If true, the app will wait for the action to continue before proceeding to the next action.

### Run As Administrator

This action forces this current application to restart as an administrator, this step should usually be the first step, assuming your setup requires it.

```
{
    "ContentPath": "Path\\to\\some\\markdown.md",
    "Actions": [
        {
            "StepType": "RunAsAdmin",
            "Wait": true
        }
    ],
    "SwitchStep": false,
    "Skippable": true
}
```

### Run Application

This action will launch an application with optional arguments. It can be used to run apps or scripts in the setup process. If `Wait` is enabled, it will wait for the application to close before continuing.

```
{
    "ContentPath": "Path\\to\\some\\markdown.md",
    "Actions": [
        {
            "StepType": "RunApplication",
            "AppPath": "Demo\\TestApplication.bat",
            "AppArgs": "-demo",
            "Wait": true
        }
    ],
    "SwitchStep": false,
    "Skippable": false
}
```

### Move To Step

This action will immediately move to a step based on the index of the step (the first step is index 0). This can be used with the `SwitchStep` to create different branches of options.

```
{
    "ContentPath": "Path\\to\\some\\markdown.md",
    "SwitchStep": true,
    "YesActions": [
        {
            "StepType": "MoveToStep",
            "StepIndex": 0,
            "Wait": true
        }
    ],
    "NoActions": [],
    "Skippable": false
}
```

### Copy/Move/Delete Files

These steps allow you to copy, move, or delete files and folders. Useful when it is not worth writing a script just to move a handful of files around.

For `CopyFiles` and `MoveFiles` step types, use the `FileMaps` dictionary to map source and destination. For `DeleteFiles` use the `FilePaths` array to specify files and folders to be deleted.
```
{
    "ContentPath": "Demo\\Step_04.md",
    "Actions": [
        {
            "StepType": "CopyFiles",
            "FileMaps": {
                "Demo\\DemoCopyDir\\DemoCopyFile.txt": "Demo\\CopyOutput\\DemoCopyFile.txt",
                "Demo\\DemoCopyDir": "Demo\\CopyOutput\\DemoCopyDir"
            },
            "Wait": true
        },
        {
            "StepType": "MoveFiles",
            "FileMaps": {
                "Demo\\DemoCopyDir\\DemoCopyFile.txt": "Demo\\CopyOutput\\DemoCopyFile.txt",
                "Demo\\DemoCopyDir": "Demo\\CopyOutput\\DemoCopyDir"
            },
            "Wait": true
        },
        {
            "StepType": "DeleteFiles",
            "FilePaths": [
                "Demo\\DemoCopyDir\\DemoCopyFile.txt",
                "Demo\\DemoCopyDir"
            ],
            "Wait": true
        },
    ],
    "SwitchStep": false,
    "Skippable": true
}
```

## Mod Organizer Plugin

This tool comes with an optional Mod Organizer plugin.

If installed, when `ModOrganizer.exe` is run, if the setup tool has not run to completion yet, it will prevent Mod Organizer opening, and instead run `ModSetup.exe`.

Once the mod setup process has completed once, it will no longer prevent Mod Organizer from opening.

If you wish to test the plugin multiple times, you can reset it in Mod Organizer.
- Open Mod Organizer's settings.
- Go to the Plugins tab.
- Scroll down to the section titled `Plugins`.
- Click on `Mod Setup Tool`.
- Change the `setupcomplete` setting to `False`.