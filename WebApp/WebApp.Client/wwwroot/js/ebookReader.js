export function getSelectionText(container) {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0 || selection.isCollapsed) {
        return "";
    }

    const range = selection.getRangeAt(0);
    if (!container || !container.contains(range.commonAncestorContainer)) {
        return "";
    }

    return selection.toString().trim();
}

export function clearSelection() {
    window.getSelection()?.removeAllRanges();
}

export async function copyText(text) {
    if (!text) {
        return false;
    }

    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

export function getDefaultContentPaddingPercent() {
    return window.matchMedia("(max-width: 47.98rem)").matches ? 5 : 25;
}

export function paginateChapter(container, html) {
    if (!container) {
        return [html ?? ""];
    }

    const template = document.createElement("template");
    template.innerHTML = html ?? "";

    const measurer = document.createElement("article");
    measurer.className = "epub-chapter epub-chapter-measurer";
    const containerStyle = getComputedStyle(container);
    measurer.style.position = "fixed";
    measurer.style.left = "-10000px";
    measurer.style.top = "0";
    measurer.style.visibility = "hidden";
    measurer.style.pointerEvents = "none";
    measurer.style.boxSizing = "border-box";
    measurer.style.overflow = "hidden";
    measurer.style.font = containerStyle.font;
    measurer.style.fontFamily = containerStyle.fontFamily;
    measurer.style.fontSize = containerStyle.fontSize;
    measurer.style.lineHeight = containerStyle.lineHeight;
    measurer.style.color = containerStyle.color;
    measurer.style.width = `${Math.max(1, container.clientWidth)}px`;
    measurer.style.height = `${Math.max(1, container.clientHeight)}px`;
    document.body.appendChild(measurer);

    const pages = [];
    const currentNodes = [];

    const publishPage = () => {
        pages.push(currentNodes.map((node) => node.outerHTML ?? node.textContent ?? "").join(""));
        currentNodes.length = 0;
        measurer.replaceChildren();
    };

    for (const sourceNode of Array.from(template.content.childNodes)) {
        const node = sourceNode.cloneNode(true);
        measurer.appendChild(node);

        if (measurer.scrollHeight > measurer.clientHeight && currentNodes.length > 0) {
            measurer.removeChild(node);
            publishPage();
            measurer.appendChild(node);
        }

        currentNodes.push(node);
    }

    if (currentNodes.length > 0 || pages.length === 0) {
        publishPage();
    }

    measurer.remove();

    return pages;
}
