mergeInto(LibraryManager.library, {
  LK_IsTouchDevice: function () {
    try {
      var touch = typeof navigator !== 'undefined' && navigator.maxTouchPoints > 0;
      var coarse = typeof window !== 'undefined' && !!window.matchMedia && window.matchMedia('(pointer: coarse)').matches;
      return (touch && coarse) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});
