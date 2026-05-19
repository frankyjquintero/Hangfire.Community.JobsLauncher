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
            return utils.fetchJson(state.apiBaseUrl + '/api/methods?className=' + encodeURIComponent(className))
                .then(function (resp) {
                    if (Array.isArray(resp)) return { success: true, methods: resp };
                    if (resp && resp.methods) return resp;
                    if (resp && resp.data && Array.isArray(resp.data)) return { success: true, methods: resp.data };
                    if (resp && !resp.success && resp.methods) return { success: true, methods: resp.methods };
                    return { success: false, error: 'Unexpected response format' };
                })
                .catch(function (err) {
                    console.error('getMethods failed:', err);
                    return { success: false, error: err.message };
                });
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
            return utils.fetchJson(state.apiBaseUrl + '/api/templates')
                .then(function (resp) {
                    return Array.isArray(resp) ? resp : (resp.templates || resp.data || []);
                });
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
            return state.apiBaseUrl + '/api/export-import?action=export&templateName=' + encodeURIComponent(name);
        },

        getAuditLog: function (params) {
            return utils.fetchJson(state.apiBaseUrl + '/api/audit-log?' + params.toString());
        }
    };
})(window);