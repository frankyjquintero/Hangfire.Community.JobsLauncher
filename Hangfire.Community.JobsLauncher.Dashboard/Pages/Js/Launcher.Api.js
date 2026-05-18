/* global window */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;

    ns.api = {
        getCapabilities: function () {
            return utils.fetchJson(state.apiBaseUrl + '/api/capabilities');
        },

        getQueues: function () {
            return utils.fetchJson(state.apiBaseUrl + '/api/queues');
        },

        getMethods: function (className) {
            return utils.fetchJson(state.apiBaseUrl + '/api/methods?className=' + encodeURIComponent(className));
        },

        validateCron: function (expression) {
            return utils.fetchJson(state.apiBaseUrl + '/api/validate-cron?expression=' + encodeURIComponent(expression));
        },

        launchJob: function (request) {
            var formData = new FormData();
            formData.append('json', JSON.stringify(request));
            return fetch(state.apiBaseUrl + '/api/launch', {
                method: 'POST',
                body: formData
            }).then(function (r) { return r.json(); });
        },

        getHistory: function (page, pageSize) {
            return utils.fetchJson(state.apiBaseUrl + '/api/history?page=' + page + '&pageSize=' + pageSize);
        },

        deleteHistory: function () {
            return fetch(state.apiBaseUrl + '/api/history', { method: 'DELETE' });
        },

        getTemplates: function () {
            return utils.fetchJson(state.apiBaseUrl + '/api/templates');
        },

        saveTemplate: function (template) {
            var formData = new FormData();
            formData.append('json', JSON.stringify(template));
            return fetch(state.apiBaseUrl + '/api/templates', {
                method: 'POST',
                body: formData
            }).then(function (r) { return r.json(); });
        },

        deleteTemplate: function (name) {
            return fetch(state.apiBaseUrl + '/api/templates?name=' + encodeURIComponent(name), { method: 'DELETE' });
        },

        exportTemplateUrl: function (name) {
            return state.apiBaseUrl + '/api/export-import?templateName=' + encodeURIComponent(name);
        },

        getAuditLog: function (params) {
            return utils.fetchJson(state.apiBaseUrl + '/api/audit-log?' + params.toString());
        }
    };
})(window);