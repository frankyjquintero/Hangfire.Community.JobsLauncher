/* global window */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    ns.utils = {
        $: function (id) {
            return document.getElementById(id);
        },

        fetchJson: function (url, options) {
            return fetch(url, options)
                .then(function (resp) {
                    if (!resp.ok) throw new Error('HTTP ' + resp.status);
                    return resp.json();
                });
        },

        toUtcString: function (localDateTimeStr) {
            if (!localDateTimeStr) return null;
            return new Date(localDateTimeStr).toISOString();
        }
    };
})(window);