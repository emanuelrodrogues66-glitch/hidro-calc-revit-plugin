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
        public Result Execute(ExternalCommandData commandData, ref string msg, ElementSet els)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            var doc   = uiDoc.Document;
            var selIds = uiDoc.Selection.GetElementIds();

            if (selIds.Count == 0)
            {
                TaskDialog.Show("BIM FIRE HIDRO CALC",
                    "Selecione os tubos e conexões do trecho no modelo e clique novamente.");
                return Result.Cancelled;
            }

            // ── Agrupar por trecho ──────────────────────────────────────────────
            // Chave: nome do trecho (BimFire_Trecho ou Comentários)
            // Valor: (fittings por tipo, comprimento acumulado)
            var grupos = new Dictionary<string, (Dictionary<string, (int qtd, string tam, double dn)> fittings, double compM)>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in selIds)
            {
                var el = doc.GetElement(id);

                if (el is Pipe pipe)
                {
                    // Comprimento em metros
                    var comp = (pipe.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0) * 0.3048;

                    // Nome do trecho: BimFire_Trecho > Comentários > "Trecho Revit"
                    var nomeTrecho = pipe.LookupParameter("BimFire_Trecho")?.AsString() ?? "";
                    if (string.IsNullOrWhiteSpace(nomeTrecho))
                        nomeTrecho = pipe.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
                    if (string.IsNullOrWhiteSpace(nomeTrecho))
                        nomeTrecho = "Trecho Revit";

                    nomeTrecho = nomeTrecho.Trim();

                    if (!grupos.ContainsKey(nomeTrecho))
                        grupos[nomeTrecho] = (new Dictionary<string, (int, string, double)>(), 0);

                    var g = grupos[nomeTrecho];
                    grupos[nomeTrecho] = (g.fittings, g.compM + comp);
                    continue;
                }

                // Conexões (fitting / accessory)
                var catId = el.Category?.Id.Value;
                if (catId != (long)BuiltInCategory.OST_PipeFitting &&
                    catId != (long)BuiltInCategory.OST_PipeAccessory) continue;

                // Nome do trecho via BimFire_Trecho ou Comentários
                var nomeFit = el.LookupParameter("BimFire_Trecho")?.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(nomeFit))
                    nomeFit = el.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(nomeFit))
                    nomeFit = "Trecho Revit";
                nomeFit = nomeFit.Trim();

                if (!grupos.ContainsKey(nomeFit))
                    grupos[nomeFit] = (new Dictionary<string, (int, string, double)>(), 0);

                var fam = el.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? el.Name;
                var tam = el.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "";
                var dn  = Math.Round((el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0) * 304.8);
                var key = $"{fam}||{tam}";

                var gf = grupos[nomeFit];
                if (gf.fittings.ContainsKey(key))
                    gf.fittings[key] = (gf.fittings[key].qtd + 1, tam, dn);
                else
                    gf.fittings[key] = (1, tam, dn);
            }

            // ── Montar JSON array de trechos ───────────────────────────────────
            var trechosJson = string.Join(",", grupos.Select(kv =>
            {
                var nome = kv.Key.Replace("\"", "\\\"");
                var comp = Math.Round(kv.Value.compM, 2);
                var pecasJson = string.Join(",", kv.Value.fittings.Select(f =>
                {
                    var parts = f.Key.Split("||");
                    var fam2 = parts[0].Replace("\"", "\\\"");
                    var tam2 = f.Value.tam.Replace("\"", "\\\"");
                    return $"{{\"familiaOriginal\":\"{fam2}\",\"tamanho\":\"{tam2}\",\"quantidade\":{f.Value.qtd},\"diametroNominal\":{f.Value.dn}}}";
                }));
                return $"{{\"nomeTrecho\":\"{nome}\",\"comprimentoReal\":{comp},\"pecas\":[{pecasJson}]}}";
            }));

            var jsonFinal = $"[{trechosJson}]";

            // ── Enviar para o painel ────────────────────────────────────────────
            var pane = commandData.Application.GetDockablePane(App.PanelId);
            if (!pane.IsShown()) pane.Show();

            var panel = HidroCalcPaneProvider.Panel;
            if (panel == null)
            {
                TaskDialog.Show("BIM FIRE HIDRO CALC", "Abra o painel primeiro.");
                return Result.Failed;
            }

            panel.Dispatcher.InvokeAsync(async () => await panel.SendTrechosAsync(jsonFinal));

            // Mostrar resumo
            var resumo = string.Join("\n", grupos.Keys.Select(k => $"• {k}"));
            TaskDialog.Show("BIM FIRE HIDRO CALC",
                $"{grupos.Count} trecho(s) identificado(s):\n{resumo}\n\nVerifique o painel.");

            return Result.Succeeded;
        }
    }
}
