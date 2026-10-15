using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

public partial class MainWindow
{
    private readonly PrinterService _printerService;
    private readonly PrinterDiscovery _discovery=new();
    private readonly ObservableCollection<PrinterDestination> _destinations=new();
    private readonly ObservableCollection<string> _candidates=new();
    private CancellationTokenSource? _discoveryCancellation;
    private bool _discoveryBusy,_loadingPrinters,_windowClosed,_initialDiscovery;
    private void InitializePrinterService()
    {
        PrinterPicker.ItemsSource=_destinations;NetworkCandidates.ItemsSource=_candidates;
        _loadingPrinters=true;DirectWindowsMode.IsChecked=_printer.WindowsDriverOnly;_loadingPrinters=false;
        if(_printerService.Remembered is {Kind:"network",Identity:not null} remembered)
        {
            try
            {
                var printer=remembered with {Status="Previously identified; checked again before printing"};
                _printerService.Select(printer);_destinations.Add(printer);PrinterPicker.SelectedItem=printer;
                PrinterDestinationStatus.Text=$"Print to: {printer.Caption} · checked again before printing";QuickPrinterName.Text="To: "+printer.Name;
            }
            catch(Exception ex) when(ex is FormatException or ArgumentException){AppDiagnostics.Log("Invalid saved network destination",ex);}
        }
        Loaded+=async(_,_)=>{if(!_initialDiscovery){_initialDiscovery=true;await RefreshInstalledPrinters();}};
        Closed+=(_,_)=>{_windowClosed=true;_discoveryCancellation?.Cancel();};
    }
    private void SetDiscoveryBusy(bool value)
    {
        _discoveryBusy=value;
        RefreshPrintersButton.IsEnabled=ScanNetworkButton.IsEnabled=CheckNetworkButton.IsEnabled=!value&&!_sending;
        CancelDiscoveryButton.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        PrinterPicker.IsEnabled=!_sending&&!value; // A known destination can still print; do not race selection against discovery.
    }
    private async void RefreshPrinters_Click(object sender,RoutedEventArgs e)=>await RefreshInstalledPrinters();
    private async Task RefreshInstalledPrinters()
    {
        if(_discoveryBusy || _sending || _windowClosed)return;
        using var cancellation=new CancellationTokenSource();_discoveryCancellation=cancellation;SetDiscoveryBusy(true);
        PrinterScanStatus.Text="Finding Windows USB and network queues…";
        try
        {
            var found=await _discovery.WindowsAsync().WaitAsync(TimeSpan.FromSeconds(12),cancellation.Token);
            if(_windowClosed || _sending)return;
            var desired=_printerService.Selected??_printerService.Remembered;
            _loadingPrinters=true;
            try
            {
                _destinations.Clear();foreach(var printer in found)_destinations.Add(printer);
                if(desired is {Kind:"network",Identity:not null})_destinations.Add(desired with {Status="Previously identified; identity is checked again before printing"});
                PrinterPicker.SelectedItem=_destinations.FirstOrDefault(p=>p.Key==desired?.Key);
            }
            finally{_loadingPrinters=false;}
            ApplyPrinterSelection();
            PrinterScanStatus.Text=$"{found.Count} installed Windows printer(s). USB requires its Windows driver. Use Scan local network or enter an IP for an uninstalled ZT411.";
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException or OperationCanceledException or ArgumentException)
        {if(!_windowClosed)PrinterScanStatus.Text=ex is OperationCanceledException?"Printer search stopped.":"Printer search: "+ex.Message;}
        finally{_discoveryCancellation=null;if(!_windowClosed)SetDiscoveryBusy(false);}
    }
    private void PrinterPicker_Changed(object sender,SelectionChangedEventArgs e)
    {if(!_loadingPrinters && _ready && !_sending)ApplyPrinterSelection();}
    private void ApplyPrinterSelection()
    {
        try
        {
            var printer=PrinterPicker.SelectedItem as PrinterDestination;_printerService.Select(printer);
            PrinterDestinationStatus.Text=printer==null?"Select the printer you want to use. No printer is selected.":$"Print to: {printer.Caption}\n{printer.Status}";
            QuickPrinterName.Text=printer==null?"No printer selected":"To: "+printer.Name;
            JobPreview.ClearPrinter();
        }
        catch(Exception ex) when(ex is FormatException or ArgumentException)
        {_printerService.Select(null);PrinterDestinationStatus.Text=ex.Message;QuickPrinterName.Text="No printer selected";}
    }
    private void FocusPrinterSelection()
    {
        StickersSection.IsExpanded=true;PrinterPicker.Focus();PrinterPicker.IsDropDownOpen=true;
        PrinterServiceCard.BringIntoView();Status.Text="Select the printer above the live sticker, then click Print sticker.";
    }
    private async void ScanNetwork_Click(object sender,RoutedEventArgs e)
    {
        if(_sending || _discoveryBusy)return;
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15));_discoveryCancellation=cancellation;SetDiscoveryBusy(true);_candidates.Clear();
        PrinterScanStatus.Text="Scanning nearby private IPv4 addresses for port 9100 (up to 15 seconds). No print data is sent…";
        try
        {
            await PrinterDiscovery.ScanLanAsync(ip=>Dispatcher.BeginInvoke(new Action(()=>{if(!_windowClosed && !_candidates.Contains(ip))_candidates.Add(ip);})),cancellation.Token);
            if(!_windowClosed)PrinterScanStatus.Text=_candidates.Count==0?"No nearby port-9100 devices responded. Enter the printer IP or select an installed Windows printer.":"Scan finished. These are possible devices, not verified printers. Select an IP and click Check & use IP.";
        }
        catch(OperationCanceledException){if(!_windowClosed)PrinterScanStatus.Text="Scan stopped or reached its time limit. Found addresses remain available to check.";}
        catch(Exception ex) when(ex is System.Net.NetworkInformation.NetworkInformationException or InvalidOperationException or System.Net.Sockets.SocketException)
        {if(!_windowClosed)PrinterScanStatus.Text=ex.Message;}
        finally{_discoveryCancellation=null;if(!_windowClosed)SetDiscoveryBusy(false);}
    }
    private void CancelDiscovery_Click(object sender,RoutedEventArgs e)=>_discoveryCancellation?.Cancel();
    private void NetworkCandidates_Changed(object sender,SelectionChangedEventArgs e)
    {if(NetworkCandidates.SelectedItem is string ip)NetworkPrinterAddress.Text=ip;}
    private async void CheckNetwork_Click(object sender,RoutedEventArgs e)
    {
        if(_sending || _discoveryBusy)return;
        using var cancellation=new CancellationTokenSource();_discoveryCancellation=cancellation;SetDiscoveryBusy(true);
        PrinterScanStatus.Text="Checking printer model, resolution and identity… No sticker is sent.";
        try
        {
            var printer=await PrinterService.CheckNetworkAsync(NetworkPrinterAddress.Text,cancellation.Token);
            if(_windowClosed || _sending)return;
            var old=_destinations.FirstOrDefault(p=>p.Key==printer.Key);if(old!=null)_destinations.Remove(old);
            _destinations.Add(printer);PrinterPicker.SelectedItem=printer;PrinterScanStatus.Text="Printer identified and selected. Click Print sticker.";
        }
        catch(Exception ex) when(ex is IOException or System.Net.Sockets.SocketException or FormatException or TimeoutException or OperationCanceledException or ArgumentException)
        {if(!_windowClosed)PrinterScanStatus.Text=ex is OperationCanceledException?"Printer check cancelled.":ex.Message;}
        finally{_discoveryCancellation=null;if(!_windowClosed)SetDiscoveryBusy(false);}
    }
    private void AddWindowsPrinter_Click(object sender,RoutedEventArgs e)
    {
        try{Process.Start(new ProcessStartInfo("ms-settings:printers") {UseShellExecute=true})?.Dispose();}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException){Status.Text=ex.Message;}
    }
    private void WindowsPrinterPreferences_Click(object sender,RoutedEventArgs e)
    {
        if(_sending)return;
        if(_printerService.Selected is not {Kind:"windows"} selected){Status.Text="Select an installed Windows printer first.";return;}
        try
        {
            var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"rundll32.exe")) {UseShellExecute=false};
            foreach(var arg in new[]{"printui.dll,PrintUIEntry","/e","/n",selected.Name})info.ArgumentList.Add(arg);
            Process.Start(info)?.Dispose();
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException){Status.Text=ex.Message;}
    }
    private void DirectWindowsMode_Changed(object sender,RoutedEventArgs e)
    {
        if(!_ready || _loadingPrinters || _sending)return;
        try
        {
            var size=PreviewHost.Content is LabelView view?new Size(view.Width,view.Height):new Size(LabelView.LabelWidth,LabelView.LabelHeight);
            _printer.SetWindowsDriverOnly(DirectWindowsMode.IsChecked==true,size);JobPreview.ClearPrinter();
            Status.Text="Print connection setting saved. Your selected printer is unchanged.";
        }
        catch(Exception ex) when(ex is System.Printing.PrintSystemException or InvalidOperationException or ArgumentException or FormatException or NotSupportedException or System.Runtime.InteropServices.COMException or System.Xml.XmlException)
        {Status.Text=ex.Message;_loadingPrinters=true;DirectWindowsMode.IsChecked=_printer.WindowsDriverOnly;_loadingPrinters=false;}
    }
    public LiveStickerPaginator CaptureLiveSticker()
    {
        if(_sending)throw new InvalidOperationException("Wait for the current print to finish.");
        if(!_samplePreview && _previewRow!=null)SyncCuttingForPrint();
        if(PreviewHost.Content is not LabelView live)throw new FormatException("Select a valid sticker or an SGU / DGU sample first.");
        return new LiveStickerPaginator(live,_company);
    }
    public LabelPaginator CapturePrintBatch()
    {
        if(_sending)throw new InvalidOperationException("Wait for the current print to finish.");
        if(!_samplePreview)SyncCuttingForPrint();
        if(_editing!=null)throw new FormatException("Save the cutting entry edit before printing the batch. Print sticker can print the displayed label directly.");
        if(!_samplePreview && _cuttingIssues.Count>0)throw new FormatException("Complete the cutting rows before printing all. "+_cuttingIssues[0]);
        var labels=_samplePreview&&_previewEntry!=null?_previewEntry.Labels.ToArray():GetPrintLabels().ToArray();
        if(labels.Length==0)throw new FormatException("No stickers match this batch selection.");
        return new LabelPaginator(labels,RepeatQuantity.IsChecked==true,_company,_layouts,FormatOverride,NumberStyle);
    }
    private async void PrintLive_Click(object sender,RoutedEventArgs e)
    {
        if(_sending)return;
        try{await SendSelectedAsync(CaptureLiveSticker(),"current sticker",()=>{});}
        catch(Exception ex) when(ex is FormatException or InvalidOperationException or ArgumentException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException){Status.Text=ex.Message;}
    }
    private async void PrintBatchDirect_Click(object sender,RoutedEventArgs e)
    {
        if(_sending)return;
        try{var pages=CapturePrintBatch();await SendSelectedAsync(pages,$"all {pages.PageCount:N0} stickers",()=>{});}
        catch(Exception ex) when(ex is FormatException or InvalidOperationException or ArgumentException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException){Status.Text=ex.Message;}
    }
    private void CancelDirectPrint_Click(object sender,RoutedEventArgs e)=>_printCancellation?.Cancel();
    private void SetPrintBusy(bool busy)
    {
        _sending=busy;JobPreview.SetBusy(busy);PrintLiveButton.IsEnabled=QuickPrintButton.IsEnabled=!busy&&PreviewHost.Content is LabelView;
        CancelDirectPrintButton.Visibility=busy?Visibility.Visible:Visibility.Collapsed;
        DirectWindowsMode.IsEnabled=!busy;PrinterPicker.IsEnabled=!busy;SetDiscoveryBusy(_discoveryBusy);RefreshBatch();
    }
    private async Task SendSelectedAsync(DocumentPaginator pages,string action,Action validate)
    {
        if(_sending)return;
        if(_printerService.Selected is not {} destination){FocusPrinterSelection();return;}
        using var cancellation=new CancellationTokenSource();_printCancellation=cancellation;var submitted=false;SetPrintBusy(true);
        try
        {
            Status.Text=$"Printing {action} to {destination.Caption}…";
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            var printer=await _printerService.SendAsync(pages,cancellation.Token,(done,total)=>Status.Text=$"Printing {action} to {destination.Name}… {done} / {total}",validate,()=>{submitted=true;});
            Status.Text=$"Sent {action} to {printer}.";
        }
        catch(OperationCanceledException){Status.Text=submitted?"Cancelled. Some stickers may already have printed; check before retrying.":"Cancelled. No stickers sent.";}
        catch(Exception ex) when(ex is System.Printing.PrintSystemException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException or System.Windows.Xps.XpsException or IOException or UnauthorizedAccessException or TimeoutException)
        {
            AppDiagnostics.Log("Print to selected destination",ex);
            Status.Text=(submitted?"Printing stopped. Some stickers may have printed; check before retrying. ":"No sticker sent. ")+ex.Message;
            PrinterDestinationStatus.Text=$"{destination.Caption}\n{ex.Message}";
        }
        finally{_printCancellation=null;SetPrintBusy(false);}
    }
}
