let fillTabEscapeHandler;
let arrowKeyNavigationHandler;

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

export function measureAndCaptureElement(element, pointerId) {
    if (element.setPointerCapture) {
        element.setPointerCapture(pointerId);
    }

    const bounds = element.getBoundingClientRect();
    return { width: bounds.width, height: bounds.height };
}

export function measureRenderedImage(viewportElement) {
    const img = viewportElement.querySelector(".carousel-item.active img");
    if (!img) {
        return null;
    }

    const bounds = img.getBoundingClientRect();
    const naturalWidth = img.naturalWidth;
    const naturalHeight = img.naturalHeight;
    if (!naturalWidth || !naturalHeight) {
        return { offsetX: 0, offsetY: 0, renderedWidth: bounds.width, renderedHeight: bounds.height, naturalWidth, naturalHeight };
    }

    const scale = Math.min(bounds.width / naturalWidth, bounds.height / naturalHeight);
    const renderedWidth = naturalWidth * scale;
    const renderedHeight = naturalHeight * scale;
    return {
        offsetX: (bounds.width - renderedWidth) / 2,
        offsetY: (bounds.height - renderedHeight) / 2,
        renderedWidth,
        renderedHeight,
        naturalWidth,
        naturalHeight
    };
}

export function releasePointer(video, pointerId) {
    if (video.hasPointerCapture?.(pointerId)) {
        video.releasePointerCapture(pointerId);
    }
}

export function setMuted(video, muted = true) {
    video.defaultMuted = muted;
    video.muted = muted;
}

export async function playVideo(video) {
    await video.play();
}

export function pauseVideo(video) {
    video.pause();
}

export function seekVideo(video, time) {
    video.currentTime = time;
}

export function setVolume(video, volume) {
    video.volume = volume;
}

export function setPlaybackRate(video, rate) {
    video.playbackRate = rate;
}

export function setLoop(video, enabled) {
    video.loop = enabled;
}

export function setSubtitlesEnabled(video, enabled) {
    for (const track of video.textTracks ?? []) {
        track.mode = enabled ? "showing" : "hidden";
    }
}

export function readMediaSnapshot(video) {
    return {
        currentTime: Number.isFinite(video.currentTime) ? video.currentTime : 0,
        duration: Number.isFinite(video.duration) ? video.duration : null,
        volume: video.volume,
        muted: video.muted,
        playbackRate: video.playbackRate,
        loop: video.loop,
        paused: video.paused,
        ended: video.ended
    };
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

export function addArrowKeyNavigation(dotNetReference) {
    removeArrowKeyNavigation();

    arrowKeyNavigationHandler = event => {
        if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
            return;
        }

        event.preventDefault();
        const method = event.key === "ArrowLeft" ? "SelectPreviousFromKeyboardAsync" : "SelectNextFromKeyboardAsync";
        dotNetReference.invokeMethodAsync(method).catch(() => { });
    };

    window.addEventListener("keydown", arrowKeyNavigationHandler);
}

export function removeArrowKeyNavigation() {
    if (arrowKeyNavigationHandler) {
        window.removeEventListener("keydown", arrowKeyNavigationHandler);
        arrowKeyNavigationHandler = undefined;
    }
}
