/* global window, fetch */
(function (w) {
    'use strict';

    var ns = w.JobLauncher = w.JobLauncher || {};

    ns.utils = {
        $: function (id) { return document.getElementById(id); },
        fetchJson: function (url, options) {
            return fetch(url, options).then(function (r) { return r.json(); });
        },

        toUtcString: function (localDateTime) {
            if (!localDateTime) return '';
            return new Date(localDateTime).toISOString();
        }
    };
})(window);
