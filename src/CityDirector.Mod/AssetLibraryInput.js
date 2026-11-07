(function () {
    if (window.__cityDirectorInputGuard) return;
    var backdrop = document.createElement('div');
    backdrop.id = 'city-director-input-backdrop';
    backdrop.style.cssText = 'position:fixed;left:0;top:0;width:100%;height:100%;z-index:2147483647;pointer-events:auto;background:transparent;';
    document.body.appendChild(backdrop);
    // Native UI input is separate from the game's action-map barriers. Keep it
    // out of underlying toolbar controls while Unity IMGUI handles our window.
    var events = ['mousedown', 'mouseup', 'click', 'dblclick', 'contextmenu',
        'mousemove', 'mouseover', 'mouseout', 'wheel', 'mousewheel',
        'pointerdown', 'pointerup', 'pointermove', 'touchstart', 'touchmove', 'touchend',
        'keydown', 'keyup', 'keypress'];
    function stop(event) {
        event.preventDefault();
        event.stopImmediatePropagation();
    }
    events.forEach(function (name) { window.addEventListener(name, stop, true); });
    window.__cityDirectorInputGuard = function () {
        events.forEach(function (name) { window.removeEventListener(name, stop, true); });
        if (backdrop.parentNode) backdrop.parentNode.removeChild(backdrop);
        delete window.__cityDirectorInputGuard;
    };
}());
