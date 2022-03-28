using System;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Printing;

namespace AcsPrinter
{
    internal class Printer
    {
        private PrintManager _printManager;
        private PrintDocument _printDocument;
        private IPrintDocumentSource _printDocumentSource;
        private readonly CoreDispatcher _dispatcher;
        private UIElement _printContainer;

        public Printer(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void RegisterForPrinting()
        {
            _printDocument = new PrintDocument();
            _printDocumentSource = _printDocument.DocumentSource;
            _printDocument.Paginate += Paginate;
            _printDocument.GetPreviewPage += GetPreviewPage;
            _printDocument.AddPages += AddPages;

            _printManager = PrintManager.GetForCurrentView();
            _printManager.PrintTaskRequested += PrintTaskRequested;
        }

        public void UnregisterForPrinting()
        {
            if (_printDocument == null)
            {
                return;
            }

            _printDocument.Paginate -= Paginate;
            _printDocument.GetPreviewPage -= GetPreviewPage;
            _printDocument.AddPages -= AddPages;

            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested -= PrintTaskRequested;
        }

        public async Task Print(UIElement printContainer)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                _printContainer = printContainer;
                if (PrintManager.IsSupported())
                {
                    try
                    {
                        _ = await PrintManager.ShowPrintUIAsync();
                    }
                    catch (Exception ex)
                    {

                    }
                }
            });
        }

        protected virtual void GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            _printDocument.SetPreviewPage(e.PageNumber, _printContainer);
        }

        protected virtual void Paginate(object sender, PaginateEventArgs e)
        {
            BuildPage();
            _printDocument.SetPreviewPageCount(1, PreviewPageCountType.Final);
        }

        protected virtual void AddPages(object sender, AddPagesEventArgs e)
        {
            BuildPage();
            _printDocument.AddPage(_printContainer);
            _printDocument.AddPagesComplete();
        }

        private void BuildPage()
        {
            _printContainer.InvalidateMeasure();
            _printContainer.UpdateLayout();
        }

        protected virtual async void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs e)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var printTask = e.Request.CreatePrintTask("ACS Printer", sourceRequested => sourceRequested.SetSource(_printDocumentSource));

                // Print Task event handler is invoked when the print job is completed.
                printTask.Completed += async (s, args) =>
                {
                    // Notify the user when the print operation fails.
                    if (args.Completion == PrintTaskCompletion.Failed)
                    {
                    }
                };
            });
        }
    }
}
