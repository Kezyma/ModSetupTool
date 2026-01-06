using ModSetup.Steps;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ModSetup;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public List<SetupStep> SetupSteps { get; set; }
    public SetupStep CurrentStep { get; set; }
    public int CurrentStepIx { get; set; } = 0;

    public MainWindow()
    {
        InitializeComponent();

        LoadSetupSteps();

        YesButton.Click += YesButton_Click;
        NoButton.Click += NoButton_Click;
        ContinueButton.Click += ContinueButton_Click;
        SkipButton.Click += SkipButton_Click;

        RenderStep();
    }

    private void LoadSetupSteps()
    {
        var setupPath = System.IO.Path.GetFullPath("setup_steps.json");
        if (!File.Exists(setupPath))
        {
            var defaultSteps = new List<SetupStep>
            {
                new() {
                    ContentPath = "Demo\\Step_01.md",
                    Actions = [new SetupAction { StepType = StepType.RunAsAdmin }],
                    Skippable = true,
                    SwitchStep = false
                },
                new()
                {
                    ContentPath = "Demo\\Step_02.md",
                    Actions = [new SetupAction { StepType = StepType.RunApplication, AppPath = "Demo\\TestApplication.bat", AppArgs = "-demo" }],
                    Skippable = false,
                    SwitchStep = false
                },
                new()
                {
                    ContentPath = "Demo\\Step_03.md",
                    SwitchStep = true,
                    YesActions = [new SetupAction { StepType = StepType.MoveToStep, StepIndex = 0 }],
                    NoActions = [],
                    Skippable = false
                },
                new()
                {
                    ContentPath = "Demo\\Step_04.md",
                    Actions = [
                        new SetupAction { StepType = StepType.CopyFiles, FileMaps = new Dictionary<string, string>
                        {
                            { "Demo\\DemoCopyDir\\DemoCopyFile.txt", "Demo\\CopyOutput\\DemoCopyFile.txt" },
                            { "Demo\\DemoCopyDir", "Demo\\CopyOutput\\DemoCopyDir" }
                        }}
                        ]
                },
                new()
                {
                    Content = "## Setup Complete\nThe demo setup is now complete, click `Continue` to close the setup tool.",
                    Skippable = false,
                }
            };
            var defaultJson = JsonConvert.SerializeObject(defaultSteps, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,

            });
            File.WriteAllText(setupPath, defaultJson);

        }
        var setupJson = File.ReadAllText(setupPath);
        SetupSteps = JsonConvert.DeserializeObject<List<SetupStep>>(setupJson) ?? [];
        CurrentStepIx = 0;
        CurrentStep = SetupSteps[CurrentStepIx];
    }

    private void NextStep()
    {
        GoToStep(CurrentStepIx + 1);
    }

    private void GoToStep(int stepIx)
    {
        CurrentStepIx = stepIx;
        if (SetupSteps.Count > CurrentStepIx)
        {
            CurrentStep = SetupSteps[CurrentStepIx];
            RenderStep();
        }
        else
        {
            File.WriteAllText(System.IO.Path.GetFullPath(".setup_complete.txt"), $"Setup completed at {DateTime.Now}");

            var loadCheckPath = System.IO.Path.GetFullPath("plugin_loadcheck.tmp");
            if (File.Exists(loadCheckPath))
                File.Delete(loadCheckPath);

            var moIniPath = System.IO.Path.GetFullPath("ModOrganizer.ini");
            if (File.Exists(moIniPath))
            {
                var iniLines = File.ReadAllLines(moIniPath);
                for (var i = 0; i < iniLines.Length; i++)
                {
                    if (iniLines[i].Contains("Python%20Proxy\\tryInit=true"))
                        iniLines[i] = "Python%20Proxy\\tryInit=false";
                }
                File.WriteAllLines(moIniPath, iniLines);
            }
            Close();
        }
    }

    private void RenderStep()
    {
        var currentStep = SetupSteps[CurrentStepIx];
        var mdFilePath = currentStep.ContentPath;
        var mdContent = currentStep.Content;
        if (mdFilePath != null)
        {
            var mdFullPath = System.IO.Path.GetFullPath(mdFilePath);
            if (File.Exists(mdFullPath))
            {
                try
                {
                    mdContent = File.ReadAllText(mdFullPath);
                }
                catch (Exception)
                {
                    mdContent = $"Error: Could not read markdown file at {mdFullPath}";
                }
            }
        }
        StepMarkdownView.Markdown = mdContent ?? string.Empty;

        EnableButtons();
    }

    private void EnableButtons()
    {
        var currentStep = SetupSteps[CurrentStepIx];
        if (currentStep.SwitchStep)
        {
            YesButton.Visibility = Visibility.Visible;
            NoButton.Visibility = Visibility.Visible;
            ContinueButton.Visibility = Visibility.Collapsed;
            YesButton.IsEnabled = true;
            NoButton.IsEnabled = true;
            ContinueButton.IsEnabled = false;
        }
        else
        {
            YesButton.Visibility = Visibility.Collapsed;
            NoButton.Visibility = Visibility.Collapsed;
            ContinueButton.Visibility = Visibility.Visible;
            YesButton.IsEnabled = false;
            NoButton.IsEnabled = false;
            ContinueButton.IsEnabled = true;
        }
        SkipButton.IsEnabled = currentStep.Skippable;
    }

    private void DisableButtons()
    {
        SkipButton.IsEnabled = false;
        YesButton.IsEnabled = false;
        NoButton.IsEnabled = false;
        ContinueButton.IsEnabled = false;
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        NextStep();
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();
        ExecuteActions(CurrentStep.Actions ?? []);
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();
        ExecuteActions(CurrentStep.NoActions ?? []);
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();
        ExecuteActions(CurrentStep.YesActions ?? []);
    }

    private int CurrentActionIx { get; set; } = 0;
    private List<SetupAction> CurrentActions { get; set; }

    private void ExecuteActions(List<SetupAction> actions)
    {
        CurrentActionIx = 0;
        CurrentActions = actions;
        ExecuteAction(CurrentActionIx);
    }

    private void ExecuteAction(int actionIx)
    {
        if (actionIx < CurrentActions.Count)
        {
            CurrentActionIx = actionIx;
            var currentAction = CurrentActions[actionIx];

            switch (currentAction.StepType)
            {
                case StepType.RunAsAdmin:
                    RunAsAdminAction();
                    break;
                case StepType.RunApplication:
                    RunApplicationAction(currentAction);
                    break;
                case StepType.MoveToStep:
                    MoveToStepAction(currentAction);
                    break;
                case StepType.CopyFiles:
                    CopyFilesAction(currentAction);
                    break;
                case StepType.MoveFiles:
                    MoveFilesAction(currentAction);
                    break;
                case StepType.DeleteFiles:
                    DeleteFilesAction(currentAction);
                    break;
            }
        }
        else
        {
            CurrentActionIx = 0;
            CurrentActions = [];
            NextStep();
        }
    }


    private void RunAsAdminAction()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        var idAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        if (!idAdmin)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    FileName = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas"
                }
            };
            proc.Start();
            Environment.Exit(0);
        }
        else
        {
            NextStep();
        }
    }

    private BackgroundWorker RunApplication_Worker { get; set; }
    private void RunApplicationAction(SetupAction action)
    {
        RunApplication_Worker = new BackgroundWorker();
        RunApplication_Worker.DoWork += (sender, e) =>
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = System.IO.Path.GetFullPath(action.AppPath ?? string.Empty),
                    Arguments = action.AppArgs ?? string.Empty
                }
            };
            proc.Start();
            if (action.Wait)
            {
                proc.WaitForExit();
            }
        };
        RunApplication_Worker.RunWorkerCompleted += (sender, e) =>
        {
            var nextAction = CurrentActionIx + 1;
            ExecuteAction(nextAction);
        };
        RunApplication_Worker.RunWorkerAsync();
    }

    private void MoveToStepAction(SetupAction action)
    {
        CurrentActions = [];
        CurrentActionIx = 0;
        GoToStep(action.StepIndex ?? 0);
    }

    private BackgroundWorker CopyFiles_Worker { get; set; }
    private void CopyFilesAction(SetupAction action)
    {
        CopyFiles_Worker = new BackgroundWorker();
        CopyFiles_Worker.DoWork += (sender, e) =>
        {
            var fileDict = action.FileMaps ?? [];
            foreach (var kvp in fileDict)
            {
                var srcPath = System.IO.Path.GetFullPath(kvp.Key);
                var dstPath = System.IO.Path.GetFullPath(kvp.Value);
                if (Directory.Exists(srcPath) || File.Exists(srcPath))
                {
                    var fileAttr = File.GetAttributes(srcPath);
                    if (fileAttr.HasFlag(FileAttributes.Directory))
                    {
                        CopyFolder(srcPath, dstPath);
                    }
                    else
                    {
                        var destDir = System.IO.Path.GetDirectoryName(dstPath);
                        Directory.CreateDirectory(destDir);
                        File.Copy(srcPath, dstPath, true);
                    }
                }
            }
        };
        CopyFiles_Worker.RunWorkerCompleted += (sender, e) =>
        {
            var nextAction = CurrentActionIx + 1;
            ExecuteAction(nextAction);
        };
        CopyFiles_Worker.RunWorkerAsync();
    }

    private static void CopyFolder(string src, string dst)
    {
        Directory.CreateDirectory(dst);

        foreach (string file in Directory.GetFiles(src))
            File.Copy(file, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(file)));

        foreach (string directory in Directory.GetDirectories(src))
            CopyFolder(directory, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(directory)));
    }

    private BackgroundWorker MoveFiles_Worker { get; set; }
    private void MoveFilesAction(SetupAction action)
    {
        MoveFiles_Worker = new BackgroundWorker();
        MoveFiles_Worker.DoWork += (sender, e) =>
        {
            var fileDict = action.FileMaps ?? [];
            foreach (var kvp in fileDict)
            {
                var srcPath = System.IO.Path.GetFullPath(kvp.Key);
                var dstPath = System.IO.Path.GetFullPath(kvp.Value);
                if (Directory.Exists(srcPath) || File.Exists(srcPath))
                {
                    var fileAttr = File.GetAttributes(srcPath);
                    if (fileAttr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.CreateDirectory(dstPath);
                        Directory.Move(srcPath, dstPath);
                    }
                    else
                    {
                        var destDir = System.IO.Path.GetDirectoryName(dstPath);
                        Directory.CreateDirectory(destDir);
                        File.Move(srcPath, dstPath, true);
                    }
                }
            }
        };
        MoveFiles_Worker.RunWorkerCompleted += (sender, e) =>
        {
            var nextAction = CurrentActionIx + 1;
            ExecuteAction(nextAction);
        };
        MoveFiles_Worker.RunWorkerAsync();
    }

    private BackgroundWorker DeleteFiles_Worker { get; set; }
    private void DeleteFilesAction(SetupAction action)
    {
        DeleteFiles_Worker = new BackgroundWorker();
        DeleteFiles_Worker.DoWork += (sender, e) =>
        {
            var fileList = action.FilePaths ?? [];
            foreach (var file in fileList)
            {
                var srcPath = System.IO.Path.GetFullPath(file);
                if (Directory.Exists(srcPath) || File.Exists(srcPath))
                {
                    var fileAttr = File.GetAttributes(srcPath);
                    if (fileAttr.HasFlag(FileAttributes.Directory))
                    {
                        Directory.Delete(srcPath, true);
                    }
                    else
                    {
                        File.Delete(srcPath);
                    }
                }
            }
        };
        DeleteFiles_Worker.RunWorkerCompleted += (sender, e) =>
        {
            var nextAction = CurrentActionIx + 1;
            ExecuteAction(nextAction);
        };
        MoveFiles_Worker.RunWorkerAsync();
    }
}