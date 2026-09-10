const handlers = new WeakMap();

export function registerFullscreenChange(element, dotNetRef) {
    if (!element) {
        return;
    }

    const handler = () => {
        dotNetRef.invokeMethodAsync('OnFullscreenChanged', document.fullscreenElement === element);
    };

    handlers.set(element, handler);
    document.addEventListener('fullscreenchange', handler);
}

export function unregisterFullscreenChange(element) {
    if (!element) {
        return;
    }

    const handler = handlers.get(element);
    if (handler) {
        document.removeEventListener('fullscreenchange', handler);
        handlers.delete(element);
    }
}

export async function toggleFullscreen(element) {
    if (!element) {
        return false;
    }

    if (document.fullscreenElement === element) {
        await document.exitFullscreen();
        return false;
    }

    await element.requestFullscreen();
    return true;
}
