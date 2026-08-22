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
