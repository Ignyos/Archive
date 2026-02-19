using System.Globalization;
using System.Windows;

namespace Archive.Desktop;

public partial class JobPreviewWindow : Window
{
    public JobPreviewWindow(JobPreviewResult preview)
    {
        InitializeComponent();

        FilesToAddTextBlock.Text = preview.FilesToAdd.ToString(CultureInfo.InvariantCulture);
        FilesToUpdateTextBlock.Text = preview.FilesToUpdate.ToString(CultureInfo.InvariantCulture);
        FilesToDeleteTextBlock.Text = preview.FilesToDelete.ToString(CultureInfo.InvariantCulture);
        FilesUnchangedTextBlock.Text = preview.FilesUnchanged.ToString(CultureInfo.InvariantCulture);
        FilesSkippedTextBlock.Text = preview.FilesSkipped.ToString(CultureInfo.InvariantCulture);
        BytesTextBlock.Text = preview.TotalBytesToTransfer.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
