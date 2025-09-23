// PipeWise_Client/MainWindow/Parts/MainWindow.RunHelpers.cs

using System;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        private IProgress<(string Status, int Percent)> CreateRunProgress()
        {
            return new Progress<(string Status, int Percent)>(pr =>
            {
                if (RunProgressBar != null) RunProgressBar.Value = pr.Percent;
                if (RunProgressText != null) RunProgressText.Text = $"{pr.Percent}%";
                if (SystemStatusText != null) SystemStatusText.Text = $"🟢 {pr.Status} ({pr.Percent}%)";
            });
        }

        private void BeginRunUi(string label)
        {
            SetPhase(UiPhase.Running);
            UpdateSystemStatus(label, true);
            if (RunProgressBar != null) RunProgressBar.Value = 0;
            if (RunProgressText != null) RunProgressText.Text = "0%";
        }

        private void EndRunUiSuccess(string label)
        {
            UpdateSystemStatus(label, true);
            _hasLastRunReport = true;
            SetPhase(UiPhase.Completed);
        }

        private void EndRunUiError(string label)
        {
            UpdateSystemStatus(label, false);
            SetPhase(_hasCompatibleConfig ? UiPhase.ConfigLoadedCompatible : _hasFile ? UiPhase.FileSelected : UiPhase.Idle);
        }
    }
}
