﻿using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;
using System;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Acklann.TSMin
{
    [Guid(Symbol.Package.GuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(ConfigurationPage), Symbol.Name, "General", 0, 0, true)]
    [InstalledProductRegistration("#110", "#112", Symbol.Version, IconResourceID = 500)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class VSPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            NodeJS.Install((msg, _, __) => { System.Diagnostics.Debug.WriteLine(msg); });
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            vs = (EnvDTE.DTE)await GetServiceAsync(typeof(EnvDTE.DTE));
            Assumes.Present(vs);

            var commandService = (await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService);
            commandService.AddCommand(new OleMenuCommand(OnGotoConfigurationPageCommandInvoked, new CommandID(Symbol.CmdSet.Guid, Symbol.CmdSet.GotoConfigurationPageCommandId)));
            commandService.AddCommand(new OleMenuCommand(OnConfigureCompileOnBuildCommandInvoked, new CommandID(Symbol.CmdSet.Guid, Symbol.CmdSet.ConfigureCompileOnBuildCommandId)));

            var outputWindow = (IVsOutputWindow)await GetServiceAsync(typeof(SVsOutputWindow));
            if (outputWindow != null)
            {
                var guid = new Guid("3b0eb69d-dbb6-495b-9e72-f8967f02bd23");
                outputWindow.CreatePane(ref guid, Symbol.Name, 1, 1);
                outputWindow.GetPane(ref guid, out IVsOutputWindowPane pane);

                _watcher = new TypescriptWatcher(this, pane);
            }

            vs.StatusBar.Text = $"{Symbol.Name}: installation completed.";
        }

        private void OnConfigureCompileOnBuildCommandInvoked(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (vs != null && vs.TryGetSelectedProject(out EnvDTE.Project project))
            {
                IComponentModel componentModel = (IComponentModel)GetGlobalService(typeof(SComponentModel));
                IVsPackageInstallerServices nuget = componentModel.GetService<IVsPackageInstallerServices>();
                IVsPackageInstaller installer = componentModel.GetService<IVsPackageInstaller>();
                EnvDTE.StatusBar status = vs.StatusBar;

                if (!nuget.IsPackageInstalled(project, nameof(TSMin)))
                    try
                    {
                        status.Text = $"{Symbol.Name}: installing {nameof(TSMin)} package...";
                        status.Animate(true, EnvDTE.vsStatusAnimation.vsStatusAnimationBuild);

                        installer.InstallPackage(null, project, nameof(TSMin), Convert.ToString(null), false);
                    }
                    catch { status.Text = $"{Symbol.Name}: failed to install {nameof(TSMin)}."; }
                    finally { status.Animate(false, EnvDTE.vsStatusAnimation.vsStatusAnimationBuild); }
            }
        }

        private void OnGotoConfigurationPageCommandInvoked(object sender, EventArgs e)
        {
            ShowOptionPage(typeof(ConfigurationPage));
        }

        #region Backing Members

        private EnvDTE.DTE vs;
        private TypescriptWatcher _watcher;

        #endregion Backing Members
    }
}