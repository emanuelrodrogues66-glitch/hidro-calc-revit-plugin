using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace BimFireHidroCalc
{
    [Transaction(TransactionMode.Manual)]
    public class App : IExternalApplication
    {
        internal static readonly DockablePaneId PanelId =
            new DockablePaneId(new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));

        public Result OnStartup(UIControlledApplication application)
        {
            // Registrar painel dockável
            application.RegisterDockablePane(PanelId, "BIM FIRE HIDRO CALC",
                new HidroCalcPaneProvider());

            // Criar aba na ribbon
            try { application.CreateRibbonTab("BIM FIRE"); } catch { }

            var panel = application.CreateRibbonPanel("BIM FIRE", "Hidrantes");

            var btnAbrir = new PushButtonData(
                "btnAbrirHidroCalc",
                "Abrir\nHIDRO CALC",
                Assembly.GetExecutingAssembly().Location,
                typeof(CmdAbrirPainel).FullName!);
            btnAbrir.ToolTip = "Abre o BIM FIRE HIDRO CALC dentro do Revit.";
            panel.AddItem(btnAbrir);

            panel.AddSeparator();

            var btnEnviar = new PushButtonData(
                "btnEnviarTrecho",
                "Enviar\nTrecho",
                Assembly.GetExecutingAssembly().Location,
                typeof(CmdEnviarTrecho).FullName!);
            btnEnviar.ToolTip = "Selecione tubos e conexões e envie ao HIDRO CALC.";
            panel.AddItem(btnEnviar);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
            => Result.Succeeded;
    }
}
