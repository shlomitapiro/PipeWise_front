using System;
using System.Windows;

namespace PipeWiseClient.Interfaces
{
    public interface IDialogFactory
    {
        void SetOwner(Window owner);

        // Specific factories to avoid DI constructor resolution issues
        Windows.ValuePromptDialog CreateValuePromptDialog(string columnName, string dataType, int maxLength);
        Windows.NumericRangeDialog CreateNumericRangeDialog(string columnName);
        Windows.DateFormatDialog CreateDateFormatDialog(string columnName, bool looksLikeDate);
        Windows.RenameColumnDialog CreateRenameColumnDialog(string currentName, System.Collections.Generic.List<string> existingColumns);
        Windows.MergeColumnsDialog CreateMergeColumnsDialog(System.Collections.Generic.List<string> availableColumns, string currentColumn);
        Windows.SplitFieldWindow CreateSplitFieldWindow(string fieldName, System.Collections.Generic.List<string> existingFields);
        Windows.CategoricalEncodingWindow CreateCategoricalEncodingWindow(PipeWiseClient.Interfaces.IApiClient apiClient, string filePath, string fieldName);
    }
}
