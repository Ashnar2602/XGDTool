using Avalonia.Controls;
using XGDTool.GUI.Services;
using XGDTool.GUI.ViewModels;

namespace XGDTool.GUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var dialogService = new DialogService(this);
        var conversionService = new ConversionService();

        DataContext = new MainWindowViewModel(dialogService, conversionService);
    }
}