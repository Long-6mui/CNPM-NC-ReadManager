using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;

namespace ReadManager.Web.Services;

public record ChartSeries(string Name, IReadOnlyList<double> Values);

// Biểu đồ SVG dựng ở server (không cần thư viện JS). Màu lấy từ CSS (statistics.css) nên tự đổi theo sáng/tối.
public static class SvgCharts
{
    private const int W = 720, L = 48, R = 12, T = 12, B = 30;
    private static string F(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
    private static string E(string s) => WebUtility.HtmlEncode(s);

    public static string Short(double v)
    {
        var a = Math.Abs(v);
        return a >= 1e9 ? $"{(v / 1e9).ToString("0.#", CultureInfo.InvariantCulture)} tỷ"
             : a >= 1e6 ? $"{(v / 1e6).ToString("0.#", CultureInfo.InvariantCulture)} tr"
             : a >= 1e4 ? $"{(v / 1e3).ToString("0.#", CultureInfo.InvariantCulture)}k"
             : v.ToString("#,0", CultureInfo.InvariantCulture);
    }

    // Chọn mức trần sao cho 4 khoảng chia đều đều là số tròn (vd: 0, 10, 20, 30, 40). Dữ liệu là số đếm/tiền nên bước tối thiểu là 1.
    private static double NiceMax(double v)
    {
        if (v <= 0) return 4;
        var raw = v / 4;
        var exp = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var f = raw / exp;
        var nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
        return Math.Max(1, nf * exp) * 4;
    }

    // Biểu đồ đường nhiều chuỗi (vd: lượt đọc, theo dõi, bình luận theo ngày)
    public static HtmlString Line(string title, IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series, int height = 230)
    {
        var n = labels.Count;
        if (n == 0 || series.Count == 0) return HtmlString.Empty;
        double max = NiceMax(series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max());
        double pw = W - L - R, ph = height - T - B;
        double X(int i) => L + (n == 1 ? pw / 2 : i * pw / (n - 1));
        double Y(double v) => T + ph * (1 - v / max);

        var sb = new StringBuilder();
        Frame(sb, title, labels, series, height, max, X, pw, ph, n);
        for (var s = 0; s < series.Count; s++)
        {
            var pts = string.Join(' ', series[s].Values.Select((v, i) => $"{F(X(i))},{F(Y(v))}"));
            sb.Append($"<polyline class=\"st-line st-s{s % 4}\" fill=\"none\" points=\"{pts}\"/>");
            if (n <= 40)
                for (var i = 0; i < n; i++)
                    sb.Append($"<circle class=\"st-dot st-s{s % 4}\" cx=\"{F(X(i))}\" cy=\"{F(Y(series[s].Values[i]))}\" r=\"2.6\"><title>{E(labels[i])} — {E(series[s].Name)}: {Short(series[s].Values[i])}</title></circle>");
        }
        sb.Append("</svg>").Append(Legend(series));
        return new HtmlString(sb.ToString());
    }

    // Biểu đồ cột 1 chuỗi (vd: doanh thu theo ngày)
    public static HtmlString Columns(string title, IReadOnlyList<string> labels, ChartSeries serie, int height = 230)
    {
        var n = labels.Count;
        if (n == 0) return HtmlString.Empty;
        double max = NiceMax(serie.Values.DefaultIfEmpty(0).Max());
        double pw = W - L - R, ph = height - T - B, slot = pw / n, bw = Math.Max(2, slot * 0.68);
        double X(int i) => L + (n == 1 ? pw / 2 : slot * i + slot / 2);

        var sb = new StringBuilder();
        Frame(sb, title, labels, [serie], height, max, X, pw, ph, n);
        for (var i = 0; i < n; i++)
        {
            var h = ph * serie.Values[i] / max;
            sb.Append($"<rect class=\"st-col st-s0\" x=\"{F(X(i) - bw / 2)}\" y=\"{F(T + ph - h)}\" width=\"{F(bw)}\" height=\"{F(h)}\"><title>{E(labels[i])} — {E(serie.Name)}: {Short(serie.Values[i])}</title></rect>");
        }
        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    private static void Frame(StringBuilder sb, string title, IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series,
        int height, double max, Func<int, double> x, double pw, double ph, int n)
    {
        var desc = string.Join("; ", series.Select(s => $"{s.Name}: tổng {Short(s.Values.Sum())}"));
        sb.Append($"<svg class=\"st-chart\" viewBox=\"0 0 {W} {height}\" role=\"img\" aria-label=\"{E(title)}\" preserveAspectRatio=\"xMidYMid meet\">")
          .Append($"<title>{E(title)}</title><desc>{E(desc)}</desc>");
        for (var g = 0; g <= 4; g++)
        {
            var y = T + ph * g / 4;
            sb.Append($"<line class=\"st-grid\" x1=\"{L}\" x2=\"{W - R}\" y1=\"{F(y)}\" y2=\"{F(y)}\"/>")
              .Append($"<text class=\"st-axis\" x=\"{L - 6}\" y=\"{F(y + 4)}\" text-anchor=\"end\">{Short(max * (4 - g) / 4)}</text>");
        }
        var step = Math.Max(1, (int)Math.Ceiling(n / 7.0));
        for (var i = 0; i < n; i += step)
            sb.Append($"<text class=\"st-axis\" x=\"{F(x(i))}\" y=\"{height - 8}\" text-anchor=\"middle\">{E(labels[i])}</text>");
    }

    private static string Legend(IReadOnlyList<ChartSeries> series)
    {
        if (series.Count < 2) return "";
        var sb = new StringBuilder("<ul class=\"st-legend\">");
        for (var s = 0; s < series.Count; s++) sb.Append($"<li><i class=\"st-sw st-b{s % 4}\"></i>{E(series[s].Name)}</li>");
        return sb.Append("</ul>").ToString();
    }
}
