let pageNavigationHandler = null;

export function registerPageNavigation(dotNetReference) {
    unregisterPageNavigation();

    pageNavigationHandler = event => {
        const method = event.key === "ArrowLeft"
            ? "GoToPreviousPageFromKeyboardAsync"
            : event.key === "ArrowRight"
                ? "GoToNextPageFromKeyboardAsync"
                : null;

        if (!method) {
            return;
        }

        event.preventDefault();

        if (event.repeat) {
            return;
        }

        dotNetReference.invokeMethodAsync(method).catch(() => { });
    };

    window.addEventListener("keydown", pageNavigationHandler);
}

export function unregisterPageNavigation() {
    if (!pageNavigationHandler) {
        return;
    }

    window.removeEventListener("keydown", pageNavigationHandler);
    pageNavigationHandler = null;
}

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

// Reserve a couple of CSS pixels inside the column edge so sub-pixel rounding
// between the measured container width and the browser's actual (fractional)
// column layout never clips a glyph at the page boundary.
const PAGE_WIDTH_SAFETY_MARGIN_PX = 2;

function getPageMetrics(container) {
    const preciseWidth = container.getBoundingClientRect().width;
    const pageWidth = Math.max(1, Math.floor(preciseWidth) - PAGE_WIDTH_SAFETY_MARGIN_PX);
    container.style.setProperty("--epub-reader-page-width", `${pageWidth}px`);

    const chapter = container.querySelector(".epub-chapter");
    const chapterStyle = chapter ? getComputedStyle(chapter) : null;
    const columnGap = Math.max(0, Number.parseFloat(chapterStyle?.columnGap ?? "0") || 0);

    return {
        pageWidth,
        columnGap,
        pageStride: pageWidth + columnGap
    };
}

export function measurePagination(container) {
    if (!container) {
        return 1;
    }

    const { pageWidth, pageStride } = getPageMetrics(container);
    container.scrollLeft = Math.min(container.scrollLeft, Math.max(0, container.scrollWidth - pageWidth));

    return Math.max(1, Math.round((container.scrollWidth + pageStride - pageWidth) / pageStride));
}

export function goToPage(container, pageIndex) {
    if (!container) {
        return;
    }

    const { pageWidth, pageStride } = getPageMetrics(container);
    const maxScrollLeft = Math.max(0, container.scrollWidth - pageWidth);
    container.scrollLeft = Math.min(maxScrollLeft, Math.max(0, pageIndex) * pageStride);
}

export function getPageFraction(container) {
    if (!container) {
        return 0;
    }

    const { pageWidth, pageStride } = getPageMetrics(container);
    const pageCount = Math.max(1, Math.round((container.scrollWidth + pageStride - pageWidth) / pageStride));
    if (pageCount <= 1) {
        return 0;
    }

    return Math.min(1, Math.max(0, Math.round(container.scrollLeft / pageStride) / (pageCount - 1)));
}

export function getVisibleWordOffset(container, chapterWordCount) {
    const wordCount = Math.max(0, Number.parseInt(chapterWordCount, 10) || 0);
    if (!container || wordCount === 0) {
        return 0;
    }

    return Math.round(getPageFraction(container) * wordCount);
}
