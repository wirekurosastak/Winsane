using Avalonia.Controls;

namespace Winsane.UI.Views;

public partial class FeatureView : UserControl
{
    public FeatureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        MainScrollViewer.Offset = Avalonia.Vector.Zero;
    }
}
