// Serializes a rendered Recharts <svg> to a PNG data URL using only
// native browser APIs (XMLSerializer + Image + Canvas) - no capture
// library needed. Recharts already renders plain SVG with colors set as
// inline attributes (props like `stroke={...}` become inline `stroke="#.."`
// on the element, not CSS classes), so a standalone serialized copy
// carries its own styling with it; a DOM-rasterizer like html2canvas
// would add a real dependency to solve a problem this format doesn't have.
export async function captureSvgAsPng(svg: SVGSVGElement, scale = 2): Promise<string> {
  const { width, height } = svg.getBoundingClientRect();
  if (width === 0 || height === 0) throw new Error("Chart has no rendered size to capture.");

  const clone = svg.cloneNode(true) as SVGSVGElement;
  clone.setAttribute("width", String(width));
  clone.setAttribute("height", String(height));
  clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");

  const svgString = new XMLSerializer().serializeToString(clone);
  const svgDataUrl = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svgString)}`;

  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => {
      const canvas = document.createElement("canvas");
      canvas.width = width * scale;
      canvas.height = height * scale;
      const ctx = canvas.getContext("2d");
      if (!ctx) {
        reject(new Error("Canvas 2D context unavailable."));
        return;
      }
      // White background - the chart itself has no opaque background of
      // its own, and a PDF page is always white.
      ctx.fillStyle = "#ffffff";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.scale(scale, scale);
      ctx.drawImage(img, 0, 0, width, height);
      resolve(canvas.toDataURL("image/png"));
    };
    img.onerror = () => reject(new Error("Failed to rasterize chart SVG."));
    img.src = svgDataUrl;
  });
}

// Every Recharts plot inside `container`, stacked top to bottom into one
// PNG - the I-MR view is two charts that belong in one figure. Only the
// plot surface of each chart is taken (see TrendingTab's export: legend
// icons are svg.recharts-surface too, but never a direct wrapper child).
export async function captureChartsAsPng(container: HTMLElement, scale = 2): Promise<string> {
  const svgs = Array.from(container.querySelectorAll<SVGSVGElement>(".recharts-wrapper > svg.recharts-surface"));
  if (svgs.length === 0) throw new Error("No chart is currently rendered to capture.");
  if (svgs.length === 1) return captureSvgAsPng(svgs[0], scale);

  const parts = await Promise.all(svgs.map((svg) => captureSvgAsPng(svg, scale)));
  const images = await Promise.all(parts.map((src) => new Promise<HTMLImageElement>((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error("Failed to combine chart images."));
    img.src = src;
  })));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(...images.map((i) => i.width));
  canvas.height = images.reduce((h, i) => h + i.height, 0);
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("Canvas 2D context unavailable.");
  ctx.fillStyle = "#ffffff";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  let y = 0;
  for (const img of images) {
    ctx.drawImage(img, 0, y);
    y += img.height;
  }
  return canvas.toDataURL("image/png");
}

export function downloadCsv(csv: string, filename: string): void {
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
