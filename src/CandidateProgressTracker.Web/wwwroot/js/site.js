// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Fragment navigation stays on the dashboard; close its mobile offcanvas first.
document.addEventListener('click', event => {
    const link = event.target.closest('#app-navigation a[href*="#"]');
    if (link && window.innerWidth < 992) {
        const panel = document.getElementById('app-navigation');
        bootstrap.Offcanvas.getInstance(panel)?.hide();
        if (link.hash === '#learning-context') document.getElementById('learning-context')?.setAttribute('open', '');
    }
});
