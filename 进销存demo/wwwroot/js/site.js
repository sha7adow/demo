// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('keydown', function (e) {
    if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
    var tag = (document.activeElement && document.activeElement.tagName) || '';
    if (['INPUT', 'TEXTAREA', 'SELECT'].indexOf(tag) >= 0) return;
    e.preventDefault();
    var el = document.querySelector('input[name="q"]');
    if (el) el.focus();
});

(function () {
    if (typeof window.jQuery === 'undefined' || !window.jQuery.validator || !window.jQuery.validator.unobtrusive) return;
    window.jQuery.validator.unobtrusive.adapters.add('comparegte', ['other'], function (options) {
        options.rules.comparegte = options.params.other;
        options.messages.comparegte = options.message;
    });
    window.jQuery.validator.addMethod('comparegte', function (value, element, otherName) {
        if (value === undefined || value === null || value === '') return true;
        var other = window.jQuery(element).closest('form').find('[name="' + otherName + '"]');
        if (!other.length) return true;
        var a = parseFloat(String(value).replace(',', '.'));
        var b = parseFloat(String(other.val()).replace(',', '.'));
        if (isNaN(a) || isNaN(b)) return true;
        return a >= b;
    });
})();
