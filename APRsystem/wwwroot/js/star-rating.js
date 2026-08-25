(function () {
    function initStarRatings(root) {
        (root || document).querySelectorAll('.star-rating:not(.readonly)').forEach(function (widget) {
            if (widget.dataset.starBound === '1') return;
            widget.dataset.starBound = '1';

            var input = document.getElementById(widget.getAttribute('data-target'));
            if (!input) return;

            var stars = widget.querySelectorAll('.star-icon');

            function render(val) {
                stars.forEach(function (star) {
                    var v = parseInt(star.getAttribute('data-value'), 10);
                    var isFilled = v <= val;
                    star.classList.toggle('bi-star-fill', isFilled);
                    star.classList.toggle('bi-star', !isFilled);
                    star.classList.toggle('filled', isFilled);
                });
                widget.setAttribute('title', val + ' of 4');
            }

            stars.forEach(function (star) {
                star.addEventListener('mouseenter', function () {
                    render(parseInt(star.getAttribute('data-value'), 10));
                });

                star.addEventListener('click', function () {
                    var clicked = parseInt(star.getAttribute('data-value'), 10);
                    var current = parseInt(input.value || '0', 10);
                    var newVal = (clicked === current) ? 0 : clicked;
                    input.value = newVal;
                    render(newVal);
                });
            });

            widget.addEventListener('mouseleave', function () {
                render(parseInt(input.value || '0', 10));
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { initStarRatings(); });
    } else {
        initStarRatings();
    }

    var observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (node.nodeType !== 1) return;
                if (node.matches && node.matches('.star-rating')) {
                    initStarRatings(node.parentNode);
                } else if (node.querySelectorAll) {
                    var found = node.querySelectorAll('.star-rating');
                    if (found.length) initStarRatings(node);
                }
            });
        });
    });
    observer.observe(document.body, { childList: true, subtree: true });

    window.initStarRatings = initStarRatings;
})();