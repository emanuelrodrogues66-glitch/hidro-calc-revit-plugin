using Autodesk.Revit.UI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BimFireHidroCalc
{
    /// <summary>
    /// Painel dockável construído 100% em código (sem XAML).
    /// </summary>
    public class HidroCalcPanel : UserControl
    {
        private readonly WebView2 _webView;
        private readonly TextBlock _lblStatus;
        private bool _ready = false;
        private string? _pendingMsg = null;

        public HidroCalcPanel()
        {
            // Layout raiz
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Cabeçalho ────────────────────────────────────────────────
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 52, 96)),
                Padding = new Thickness(8, 6, 8, 6)
            };
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "🔥 BIM FIRE HIDRO CALC",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });
            var btnReload = new Button
            {
                Content = " ↺ ",
                ToolTip = "Recarregar",
                Margin = new Thickness(8, 0, 0, 0),
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnReload.Click += (s, e) => { _webView.Reload(); _lblStatus.Text = "Recarregando..."; };
            headerPanel.Children.Add(btnReload);
            header.Child = headerPanel;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // ── WebView2 ─────────────────────────────────────────────────
            // NÃO definir Source aqui — aguardar EnsureCoreWebView2Async em InitWebViewAsync
            _webView = new WebView2();
            Grid.SetRow(_webView, 1);
            grid.Children.Add(_webView);

            // ── Rodapé ───────────────────────────────────────────────────
            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 52, 96)),
                Padding = new Thickness(8, 4, 8, 4)
            };
            _lblStatus = new TextBlock
            {
                Text = "Carregando...",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170))
            };
            footer.Child = _lblStatus;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            Content = grid;
            InitWebViewAsync();
        }

        private async void InitWebViewAsync()
        {
            try
            {
                // Pasta de dados do usuário — necessário dentro do Revit para evitar E_ACCESSDENIED
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BimFireHidroCalc",
                    "WebView2"
                );
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder
                );

                await _webView.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                // Impedir abertura de nova janela — redirecionar para o próprio WebView2
                _webView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    _webView.CoreWebView2.Navigate(e.Uri);
                };

                // Garantir que links internos da SPA não sejam bloqueados
                _webView.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    _lblStatus.Text = "Carregando...";
                };

                // Navegar APÓS o CoreWebView2 estar pronto
                _webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        _lblStatus.Text = "✔ BIM FIRE HIDRO CALC conectado";
                    }
                    else
                    {
                        _lblStatus.Text = $"⚠ Erro de navegação ({e.WebErrorStatus}) — tentando novamente...";
                        // Auto-reload após 3 segundos em caso de falha
                        System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                        {
                            _webView.Dispatcher.Invoke(() =>
                            {
                                try { _webView.CoreWebView2.Navigate("https://bim-fire-hidro-calc.vercel.app"); }
                                catch { }
                            });
                        });
                    }
                };

                _webView.CoreWebView2.Navigate("https://bim-fire-hidro-calc.vercel.app");

                _ready = true;
                if (_pendingMsg != null)
                {
                    await PostMessageAsync(_pendingMsg);
                    _pendingMsg = null;
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Erro WebView2: {ex.Message}";
            }
        }

        public async Task SendTrechoAsync(string jsonPayload)
        {
            var msg = $"{{\"type\":\"REVIT_TRECHO\",\"data\":{jsonPayload}}}";
            if (_ready)
                await PostMessageAsync(msg);
            else
                _pendingMsg = msg;
        }

        /// <summary>Envia um lote de trechos (JSON array) de uma só vez.</summary>
        public async Task SendTrechosAsync(string jsonArray)
        {
            var msg = $"{{\"type\":\"REVIT_TRECHOS_LOTE\",\"data\":{jsonArray}}}";
            if (_ready)
                await PostMessageAsync(msg);
            else
                _pendingMsg = msg;
        }

        private async Task PostMessageAsync(string json)
        {
            // Canal 1: window.postMessage (padrão)
            var script1 = $"window.postMessage({json}, '*');";
            await _webView.CoreWebView2.ExecuteScriptAsync(script1);

            // Canal 2: funções globais (mais confiável no WebView2)
            var script2 = $@"
(function() {{
  var msg = {json};
  if (msg && msg.type === 'REVIT_TRECHOS_LOTE' && typeof window.__revitTrechosLote__ === 'function') {{
    window.__revitTrechosLote__(msg.data);
  }} else if (msg && msg.type === 'REVIT_TRECHO' && typeof window.__revitTrecho__ === 'function' && msg.data) {{
    window.__revitTrecho__(msg.data);
  }}
}})();
";
            await _webView.CoreWebView2.ExecuteScriptAsync(script2);
        }
    }

    public class HidroCalcPaneProvider : IDockablePaneProvider
    {
        public static HidroCalcPanel? Panel { get; private set; }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            Panel = new HidroCalcPanel();
            data.FrameworkElement = Panel;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                MinimumWidth = 460,
                MinimumHeight = 600
            };
        }
    }
}
