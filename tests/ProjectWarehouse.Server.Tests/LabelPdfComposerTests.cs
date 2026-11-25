using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using ProjectWarehouse.Server.Infrastructure.Labels;

namespace ProjectWarehouse.Server.Tests;

public class LabelPdfComposerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void FromPdf_FitsVisiblePageAtRightEdgeAndVerticalCenter(int rotation)
    {
        using var source = new PdfDocument();
        var original = source.AddPage();
        original.Width = XUnit.FromPoint(rotation % 180 == 0 ? 164 : 113);
        original.Height = XUnit.FromPoint(rotation % 180 == 0 ? 113 : 164);
        original.Rotate = rotation;
        using (var gfx = XGraphics.FromPdfPage(original))
            gfx.DrawRectangle(XBrushes.Black, 0, 0, original.Width.Point, original.Height.Point);
        using var input = new MemoryStream();
        source.Save(input, false);

        var result = LabelPdfComposer.FromPdf(input.ToArray(), 58, 40, 0.92);
        using var output = PdfReader.Open(new MemoryStream(result), PdfDocumentOpenMode.Import);
        var page = output.Pages[0];
        var transform = Assert.Single(ContentReader.ReadContent(page).OfType<COperator>(), op => op.Name == "cm");
        var placement = Matrix(transform.Operands.Select(Number).ToArray());
        var forms = page.Resources.Elements.GetDictionary("/XObject")!;
        var form = forms.Elements.GetDictionary(forms.Elements.Keys.Single())!;
        var bounds = form.Elements.GetRectangle("/BBox");
        var matrix = form.Elements.GetArray("/Matrix");
        var formTransform = matrix is null ? XMatrix.Identity
            : Matrix(Enumerable.Range(0, 6).Select(i => matrix.Elements.GetReal(i)).ToArray());
        var corners = new[]
        {
            new XPoint(bounds.X1, bounds.Y1), new XPoint(bounds.X1, bounds.Y2),
            new XPoint(bounds.X2, bounds.Y1), new XPoint(bounds.X2, bounds.Y2),
        }.Select(p => placement.Transform(formTransform.Transform(p))).ToArray();
        var left = corners.Min(p => p.X);
        var right = corners.Max(p => p.X);
        var bottom = corners.Min(p => p.Y);
        var top = corners.Max(p => p.Y);

        Assert.Equal(page.Width.Point, right, 2);
        Assert.Equal(page.Height.Point / 2, (bottom + top) / 2, 2);
        Assert.Equal(page.Width.Point * 0.92, right - left, 2);
        Assert.Equal(164d / 113, (right - left) / (top - bottom), 3);
    }

    private static double Number(CObject value) => double.Parse(value.ToString()!, CultureInfo.InvariantCulture);

    private static XMatrix Matrix(double[] values) => new(values[0], values[1], values[2], values[3], values[4], values[5]);
}
