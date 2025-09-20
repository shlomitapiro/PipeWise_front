using System.Windows;
using System.Windows.Controls;
using PipeWiseClient.Helpers;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        // ===== UI Phase Management =====

        public enum UiPhase
        {
            Idle,
            FileSelected,
            ConfigLoadedCompatible,
            ConfigLoadedMismatch,
            Running,
            Completed
        }

        private void SetPhase(UiPhase next)
        {
            _phase = next;
            UpdateUiByPhase();

            if (RunProgressBar != null) RunProgressBar.Visibility = _phase == UiPhase.Running ? Visibility.Visible : Visibility.Collapsed;
            if (RunProgressText != null) RunProgressText.Visibility = _phase == UiPhase.Running ? Visibility.Visible : Visibility.Collapsed;
            Btn("CancelRunBtn").Let(b => b.IsEnabled = _phase == UiPhase.Running);
        }

        private Button? Btn(string name) => FindName(name) as Button;

        private void UpdateUiByPhase()
        {
            Btn("BrowseFileBtn").Let((Button b) =>
                b.IsEnabled = _phase is UiPhase.Idle or UiPhase.FileSelected or UiPhase.ConfigLoadedCompatible or UiPhase.ConfigLoadedMismatch or UiPhase.Completed);

            Btn("LoadConfigBtn").Let((Button b) =>
                b.IsEnabled = _hasFile && _phase != UiPhase.Running);

            Btn("SaveConfigBtn").Let((Button b) =>
                b.IsEnabled = _hasFile && _phase != UiPhase.Running);

            Btn("RunBtn").Let((Button b) =>
            {
                var canRun =
                    _hasFile &&
                    _phase != UiPhase.Running &&
                    (_loadedConfig == null || _hasCompatibleConfig);

                b.IsEnabled = canRun;

                b.ToolTip = canRun ? null :
                    (!_hasFile ? "?? ????? ???? ???? ????"
                     : (_loadedConfig != null && !_hasCompatibleConfig ? "???????????? ???? ????? ?????" : "?????? ???? ????? ???"));
            });

            Btn("RunSavedPipelineBtn").Let((Button b) =>
                b.IsEnabled = _phase != UiPhase.Running);

            Btn("SaveAsServerPipelineBtn").Let((Button b) =>
                b.IsEnabled = (_hasFile || _loadedConfig != null) && _phase != UiPhase.Running);

            Btn("ViewReportsBtn").Let((Button b) =>
                b.IsEnabled = _hasLastRunReport && _phase != UiPhase.Running);

            Btn("ResetSettingsBtn").Let((Button b) =>
                b.IsEnabled = _phase != UiPhase.Running);

            this.Cursor = _phase == UiPhase.Running ? System.Windows.Input.Cursors.AppStarting : null;
        }
    }
}

