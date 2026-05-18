/* global window, document, $ */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;
    var ui = ns.ui;
    var params = ns.params;
    var cron = ns.cron;
    var history = ns.history;
    var templates = ns.templates;
    var audit = ns.audit;

    function bindEvents() {
        document.querySelectorAll('input[name="launchMode"]').forEach(function (r) {
            r.addEventListener('change', ui.toggleMode);
        });
        utils.$('btnLoadMethods').addEventListener('click', params.loadMethods);
        utils.$('methodSelect').addEventListener('change', params.onMethodChange);
        document.querySelectorAll('input[name="execMode"]').forEach(function (r) {
            r.addEventListener('change', ui.toggleExecMode);
        });

        utils.$('validateJsonBtn').addEventListener('click', params.validateJson);
        utils.$('formatJsonBtn').addEventListener('click', params.formatJson);
        utils.$('suggestJsonBtn').addEventListener('click', params.suggestJsonStructure);

        utils.$('btnValidateCron').addEventListener('click', cron.validateCron);
        utils.$('btnOpenCronGenerator').addEventListener('click', function () {
            if (typeof $ !== 'undefined') $('#cronGeneratorModal').modal('show');
        });

        utils.$('btnPreview').addEventListener('click', params.showPreview);
        utils.$('btnLaunch').addEventListener('click', ui.submitJob);
        utils.$('confirmCriticalLaunch').addEventListener('click', ui.confirmedLaunch);

        utils.$('btnClearHistory').addEventListener('click', history.clear);
        utils.$('btnImport').addEventListener('click', templates.import);
        utils.$('btnSaveAsTemplate').addEventListener('click', templates.saveCurrent);

        utils.$('queue').addEventListener('input', function () { ui.checkCriticalQueue(this.value); });
        utils.$('queue').addEventListener('change', function () { ui.checkCriticalQueue(this.value); });

        document.addEventListener('keydown', function (e) {
            if (e.ctrlKey && e.key === 'Enter') { e.preventDefault(); ui.submitJob(); }
        });

        utils.$('btnApplyAuditFilters').addEventListener('click', function () { audit.load(1); });
        utils.$('btnClearAuditFilters').addEventListener('click', audit.clearFilters);
    }

    function init() {
        state.apiBaseUrl = (ns.config && ns.config.apiBaseUrl) ? ns.config.apiBaseUrl : '';

        api.getCapabilities().then(function (caps) {
            state.dynamicJobsAvailable = !!caps.dynamicJobsAvailable;
            if (caps.criticalQueues) state.criticalQueues = caps.criticalQueues;
            ui.buildRecurringEngineOptions();

            if (caps.auditLogEnabled) {
                utils.$('auditLogTab').style.display = '';
                document.querySelector('a[href="#auditLogPane"]').addEventListener('click', function () {
                    audit.load();
                });
            }
        });

        ui.loadQueues();
        bindEvents();
        cron.initCronModal();
        history.load();
        templates.load();
        ui.toggleExecMode();
        ui.toggleMode();
    }

    ns.init = init;
    document.addEventListener('DOMContentLoaded', init);
})(window);