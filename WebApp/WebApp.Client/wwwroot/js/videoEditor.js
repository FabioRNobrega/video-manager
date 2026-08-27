let fillTabEscapeHandler;

export function measureAndCapture(viewport, video, pointerId) {
    if (video.setPointerCapture) {
        video.setPointerCapture(pointerId);
    }

    const viewportBounds = viewport.getBoundingClientRect();
    return {
        viewportWidth: viewportBounds.width,
        viewportHeight: viewportBounds.height,
        videoWidth: video.videoWidth,
        videoHeight: video.videoHeight
    };
}

export function releasePointer(video, pointerId) {
    if (video.hasPointerCapture?.(pointerId)) {
        video.releasePointerCapture(pointerId);
    }
}

export function setMuted(video) {
    video.defaultMuted = true;
    video.muted = true;
}

export function enterFillTab(dotNetReference) {
    exitFillTab();

    fillTabEscapeHandler = event => {
        if (event.key !== "Escape") {
            return;
        }

        event.preventDefault();
        const activeReference = dotNetReference;
        exitFillTab();
        activeReference.invokeMethodAsync("ExitFillTabFromEscapeAsync").catch(() => { });
    };

    window.addEventListener("keydown", fillTabEscapeHandler);
    document.body.classList.add("fill-tab-active");
}

export function exitFillTab() {
    if (fillTabEscapeHandler) {
        window.removeEventListener("keydown", fillTabEscapeHandler);
        fillTabEscapeHandler = undefined;
    }

    document.body.classList.remove("fill-tab-active");
}
