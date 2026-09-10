export function downloadDataUrl(dataUrl, filename) {
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = filename || 'image';
    document.body.appendChild(a);
    a.click();
    a.remove();
}
