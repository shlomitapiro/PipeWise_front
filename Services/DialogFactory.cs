using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PipeWiseClient.Interfaces;

namespace PipeWiseClient.Services
{
    public class DialogFactory : IDialogFactory
    {
        private Window? _owner;

        public DialogFactory(IServiceProvider services) { }

        public void SetOwner(Window owner) => _owner = owner;

        public Windows.ValuePromptDialog CreateValuePromptDialog(string columnName, string dataType, int maxLength)
        {
            var dlg = new Windows.ValuePromptDialog(columnName, dataType, maxLength);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }

        public Windows.NumericRangeDialog CreateNumericRangeDialog(string columnName)
        {
            var dlg = new Windows.NumericRangeDialog(columnName);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }

        public Windows.DateFormatDialog CreateDateFormatDialog(string columnName, bool looksLikeDate)
        {
            var dlg = new Windows.DateFormatDialog(columnName, looksLikeDate);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }

        public Windows.RenameColumnDialog CreateRenameColumnDialog(string currentName, System.Collections.Generic.List<string> existingColumns)
        {
            var dlg = new Windows.RenameColumnDialog(currentName, existingColumns);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }

        public Windows.MergeColumnsDialog CreateMergeColumnsDialog(System.Collections.Generic.List<string> availableColumns, string currentColumn)
        {
            var dlg = new Windows.MergeColumnsDialog(availableColumns, currentColumn);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }

        public Windows.SplitFieldWindow CreateSplitFieldWindow(string fieldName, System.Collections.Generic.List<string> existingFields)
        {
            var dlg = new Windows.SplitFieldWindow(existingFields);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }

        public Windows.CategoricalEncodingWindow CreateCategoricalEncodingWindow(PipeWiseClient.Interfaces.IApiClient apiClient, string filePath, string fieldName)
        {
            var dlg = new Windows.CategoricalEncodingWindow(apiClient, filePath, fieldName);
            if (_owner != null) dlg.Owner = _owner;
            return dlg;
        }
    }
}
