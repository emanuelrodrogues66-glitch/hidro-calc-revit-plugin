using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BimFireHidroCalc
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdAbrirPainel : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var pane = data.Application.GetDockablePane(App.PanelId);
            if (pane.IsShown()) pane.Hide(); else pane.Show();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class CmdEnviarTrecho : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet els)
        {
            var uiDoc = data.Application.ActiveUIDocument;
            var doc   = uiDoc.Document;
            var selIds = uiDoc.Selection.GetElementIds();

            if (selIds.Count == 0)
            {
                TaskDialog.Show("BIM FIRE HIDRO CALC",
                    "Selecione os tubos e conexões do trecho no modelo e clique novamente.");
                return Result.Cancelled;
            }

            // Extrair dados
            var fittings = new Dictionary<string, (int qtd, string tam, double dn)>();
            double compM = 0;
            var nomes = new List<string>();

            foreach (var id in selIds)
            {
                var el = doc.GetElement(id);
                if (el is Pipe pipe)
                {
                    compM += (pipe.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0) * 0.3048;

                    // 1º: parâmetro de projeto/instância "BimFire_Trecho" (nome exato do trecho no app)
                    var nomePar = pipe.LookupParameter("BimFire_Trecho")?.AsString() ?? "";
                    // 2º: fallback para o campo Comentários
                    if (string.IsNullOrWhiteSpace(nomePar))
                        nomePar = pipe.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
                    if (!string.IsNullOrWhiteSpace(nomePar)) nomes.Add(nomePar.Trim().ToUpper());
                    continue;
                }
                var catId = el.Category?.Id.Value;
                if (catId != (long)BuiltInCategory.OST_PipeFitting &&
                    catId != (long)BuiltInCategory.OST_PipeAccessory) continue;

                var fam = el.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? el.Name;
                var tam = el.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "";
                var dn  = Math.Round((el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0) * 304.8);
                var key = $"{fam}||{tam}";
                if (fittings.ContainsKey(key)) fittings[key] = (fittings[key].qtd + 1, tam, dn);
                else fittings[key] = (1, tam, dn);
            }

            var nomeTrecho = nomes.Count > 0
                ? nomes.GroupBy(n => n).OrderByDescending(g => g.Count()).First().Key
                : "Trecho Revit";

            // Montar JSON manualmente (sem System.Text.Json para evitar deps extras)
            var pecasJson = string.Join(",", fittings.Select(kv =>
            {
                var parts = kv.Key.Split("||");
                var fam2 = parts[0].Replace("\"", "\\\"");
                var tam2 = kv.Value.tam.Replace("\"", "\\\"");
                return $"{{\"familiaOriginal\":\"{fam2}\",\"tamanho\":\"{tam2}\",\"quantidade\":{kv.Value.qtd},\"diametroNominal\":{kv.Value.dn}}}";
            }));

            var json = $"{{\"nomeTrecho\":\"{nomeTrecho.Replace("\"", "\\\"")}\",\"comprimentoReal\":{Math.Round(compM, 2)},\"pecas\":[{pecasJson}]}}";

            // Abrir painel e enviar
            var pane = data.Application.GetDockablePane(App.PanelId);
            if (!pane.IsShown()) pane.Show();

            var panel = HidroCalcPaneProvider.Panel;
            if (panel == null)
            {
                TaskDialog.Show("BIM FIRE HIDRO CALC", "Abra o painel primeiro.");
                return Result.Failed;
            }

            panel.Dispatcher.InvokeAsync(async () => await panel.SendTrechoAsync(json));
            return Result.Succeeded;
        }
    }
}
