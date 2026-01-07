using ModSetup.Steps;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
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
using static System.Windows.Forms.LinkLabel;

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
        FixPythonProxySetting();

        YesButton.Click += YesButton_Click;
        NoButton.Click += NoButton_Click;
        ContinueButton.Click += ContinueButton_Click;
        SkipButton.Click += SkipButton_Click;

        RenderStep();
    }

    private void LoadSetupSteps()
    {
        var setupPath = System.IO.Path.GetFullPath("setup_steps.json");
        if (!File.Exists(setupPath)) CreateDemoSetupSteps();
        var setupJson = File.ReadAllText(setupPath);
        SetupSteps = JsonConvert.DeserializeObject<List<SetupStep>>(setupJson) ?? [];
        CurrentStepIx = 0;
        CurrentStep = SetupSteps[CurrentStepIx];
        File.WriteAllText(System.IO.Path.GetFullPath(".setup_in_progress.txt"), $"Setup started at {DateTime.Now}");
    }

    private void NextStep() => GoToStep(CurrentStepIx + 1);

    private void GoToStep(int stepIx)
    {
        Task.Run(FixPythonProxySetting);
        CurrentStepIx = stepIx;
        if (SetupSteps.Count > CurrentStepIx)
        {
            CurrentStep = SetupSteps[CurrentStepIx];
            RenderStep();
        }
        else
        {
            File.WriteAllText(System.IO.Path.GetFullPath(".setup_complete.txt"), $"Setup completed at {DateTime.Now}");
            Close();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var inProgPath = System.IO.Path.GetFullPath(".setup_in_progress.txt");
        if (File.Exists(inProgPath)) File.Delete(inProgPath);
        base.OnClosing(e);
    }

    /// <summary>
    /// Creates demonstration setup steps for the first run if no setup_steps.json is found.
    /// </summary>
    private void CreateDemoSetupSteps()
    {
        var setupPath = System.IO.Path.GetFullPath("setup_steps.json");
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

    /// <summary>
    /// When the ModSetup.py plugin closes Mod Organizer, it marks python as having failed to run.
    /// This triggers a popup on the next launch asking if the user wants to disable python support.
    /// The fix is to remove the loadcheck, and remove the flag for the warning, so the user doesn't accidentally disable python.
    /// </summary>
    private static void FixPythonProxySetting()
    {
        try
        {
            var loadCheckPath = System.IO.Path.GetFullPath("plugin_loadcheck.tmp");
            if (File.Exists(loadCheckPath))
                File.Delete(loadCheckPath);
        }
        catch (Exception)
        {
            // Ignore errors deleting the loadcheck file.
        }

        try
        {
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
                // GrantAccess(moIniPath);
            }
        }
        catch (Exception)
        {
            // Ignore errors modifying the INI file.
        }
    }

    /// <summary>
    /// Grants full control to a file path, used to make the ModOrganizer.ini editable again if it was edited as an admin.
    /// </summary>
    /// <param name="fullPath"></param>
    private static void GrantAccess(string fullPath)
    {
        var dInfo = new DirectoryInfo(fullPath);
        var dSecurity = dInfo.GetAccessControl();
        dSecurity.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
        dInfo.SetAccessControl(dSecurity);
    }

    /// <summary>
    /// Renders the current step markdown content, and enables the appropriate buttons.
    /// </summary>
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

    #region Buttons

    /// <summary>
    /// Enables the appropriate buttons for the current step.
    /// </summary>
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

    /// <summary>
    /// Disables all buttons on the page.
    /// </summary>
    private void DisableButtons()
    {
        SkipButton.IsEnabled = false;
        YesButton.IsEnabled = false;
        NoButton.IsEnabled = false;
        ContinueButton.IsEnabled = false;
    }

    /// <summary>
    /// Skips the current step and moves to the next one.
    /// </summary>
    private void SkipButton_Click(object sender, RoutedEventArgs e) => NextStep();

    /// <summary>
    /// Execute the actions for the current step when the Continue button is clicked.
    /// </summary>
    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();
        ExecuteActions(CurrentStep.Actions ?? []);
    }

    /// <summary>
    /// Execute the NoActions for the current step when the No button is clicked.
    /// </summary>
    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();
        ExecuteActions(CurrentStep.NoActions ?? []);
    }

    /// <summary>
    /// Execute the YesActions for the current step when the Yes button is clicked.
    /// </summary>
    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DisableButtons();
        ExecuteActions(CurrentStep.YesActions ?? []);
    }

    #endregion


    #region Actions

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
        // Small delay to allow any prior action to fully finish, just in case.
        Thread.Sleep(1000);

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
            Parallel.ForEach(fileDict, kvp =>
            {
                var srcPath = System.IO.Path.GetFullPath(kvp.Key);
                var dstPath = System.IO.Path.GetFullPath(kvp.Value);
                if (Directory.Exists(srcPath) || File.Exists(srcPath))
                {
                    var fileAttr = File.GetAttributes(srcPath);
                    if (fileAttr.HasFlag(FileAttributes.Directory)) CopyFolder(srcPath, dstPath);
                    else
                    {
                        var destPath = new FileInfo(dstPath).Directory?.FullName;
                        Directory.CreateDirectory(destPath);
                        File.Copy(srcPath, dstPath, true);
                    }
                }
            });
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

        Parallel.ForEach(Directory.GetFiles(src), file => File.Copy(file, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(file)), true));

        Parallel.ForEach(Directory.GetDirectories(src), directory => CopyFolder(directory, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(directory))));
    }

    private BackgroundWorker MoveFiles_Worker { get; set; }
    private void MoveFilesAction(SetupAction action)
    {
        MoveFiles_Worker = new BackgroundWorker();
        MoveFiles_Worker.DoWork += (sender, e) =>
        {
            var fileDict = action.FileMaps ?? [];
            Parallel.ForEach(fileDict, kvp =>
            {
                var srcPath = System.IO.Path.GetFullPath(kvp.Key);
                var dstPath = System.IO.Path.GetFullPath(kvp.Value);
                if (Directory.Exists(srcPath) || File.Exists(srcPath))
                {
                    var fileAttr = File.GetAttributes(srcPath);
                    if (fileAttr.HasFlag(FileAttributes.Directory)) MoveFolder(srcPath, dstPath);
                    else
                    {
                        var destDir = new FileInfo(dstPath).Directory?.FullName;
                        Directory.CreateDirectory(destDir);
                        File.Copy(srcPath, dstPath, true);
                        DeleteFile(srcPath);
                    }
                }
            });
        };
        MoveFiles_Worker.RunWorkerCompleted += (sender, e) =>
        {
            var nextAction = CurrentActionIx + 1;
            ExecuteAction(nextAction);
        };
        MoveFiles_Worker.RunWorkerAsync();
    }

    private void MoveFolder(string src, string dst)
    {
        Directory.CreateDirectory(dst);

        Parallel.ForEach(Directory.GetFiles(src), (file) =>
        {
            File.Copy(file, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(file)), true);
            DeleteFile(file);
        });

        Parallel.ForEach(Directory.GetDirectories(src), (directory) =>
        {
            MoveFolder(directory, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(directory)));
        });

        DeleteFolder(src);
    }

    private BackgroundWorker DeleteFiles_Worker { get; set; }
    private void DeleteFilesAction(SetupAction action)
    {
        DeleteFiles_Worker = new BackgroundWorker();
        DeleteFiles_Worker.DoWork += (sender, e) =>
        {
            var fileList = action.FilePaths ?? [];
            Parallel.ForEach(fileList, file =>
            {
                var srcPath = System.IO.Path.GetFullPath(file);
                if (Directory.Exists(srcPath) || File.Exists(srcPath))
                {
                    var fileAttr = File.GetAttributes(srcPath);
                    if (fileAttr.HasFlag(FileAttributes.Directory)) DeleteFolder(srcPath);
                    else DeleteFile(srcPath);
                }
            });
        };
        DeleteFiles_Worker.RunWorkerCompleted += (sender, e) =>
        {
            var nextAction = CurrentActionIx + 1;
            ExecuteAction(nextAction);
        };
        DeleteFiles_Worker.RunWorkerAsync();
    }

    private static void DeleteFolder(string path, int retry = 0)
    {
        if (retry <= 10)
        {
            Thread.Sleep(1000);

            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception)
            {
                DeleteFolder(path, retry + 1);
            }
        }
    }
    private static void DeleteFile(string path, int retry = 0)
    {
        if (retry <= 10)
        {
            Thread.Sleep(1000);

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                DeleteFile(path, retry + 1);
            }
        }
    }
    #endregion
}