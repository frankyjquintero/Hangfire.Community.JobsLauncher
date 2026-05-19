/* global window, document, $ */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;

    function loadHistory(page) {
        if (!page) page = 1;
        api.getHistory(page, state.history.pageSize).then(function (resp) {
            var tbody = utils.$('historyTable').querySelector('tbody');
            tbody.innerHTML = '';
            state.history.total = resp.total;
            state.history.currentPage = resp.page;
            state.history.pageSize = resp.pageSize;
            resp.items.forEach(function (e) {
                var row = '<tr>' +
                    '<td>' + new Date(e.timestamp) + '</td>' +
                    '<td>' + (e.jobId ? ('<a href="' + e.link + '" target="_blank">' + e.jobId + '</a>') : '') + '</td>' +
                    '<td>' + e.className + '</td>' +
                    '<td>' + e.methodName + '</td>' +
                    '<td>' + e.queue + '</td>' +
                    '<td>' + e.executionMode + '</td>' +
                    '<td>' + (e.recurringEngine || e.engine || '') + '</td>' +
                    '<td>' +
                    '<button class="btn btn-xs btn-default btn-relaunch" data-job="' + encodeURIComponent(JSON.stringify(e)) + '">Relaunch</button> ' +
                    '<button class="btn btn-xs btn-success btn-clone" data-job="' + encodeURIComponent(JSON.stringify(e)) + '">Clone & Launch</button>' +
                    '<button class="btn btn-xs btn-primary btn-save-template" data-job="' + encodeURIComponent(JSON.stringify(e)) + '">Save as Template</button>' +
                    '</td>' +
                    '</tr>';
                tbody.innerHTML += row;
            });
            updateHistoryPaginationControls();
            bindHistoryButtons();
        });
    }

    function saveEntryAsTemplate(entry) {
        var name = prompt('Template name:', entry.className + '.' + entry.methodName);
        if (!name) return;
        var template = {
            name: name,
            mode: entry.mode,
            className: entry.className,
            methodName: entry.methodName,
            queue: entry.queue,
            executionMode: entry.executionMode,
            cronExpression: entry.cronExpression,
            delayMinutes: entry.delayMinutes,
            scheduledDateTime: entry.scheduledDateTime,
            parentJobId: entry.parentJobId,
            recurringEngine: entry.recurringEngine || entry.engine || 'BuiltIn',
            includePerformContext: entry.includePerformContext,
            includeCancellationToken: entry.includeCancellationToken,
            rawParametersJson: entry.parametersJson,
            parameters: null
        };
        api.saveTemplate(template).then(function (res) {
            if (res.success) {
                alert('Template "' + name + '" saved.');
                ns.templates.load();
            } else {
                alert('Error: ' + (res.error || res.message));
            }
        });
    }

    function updateHistoryPaginationControls() {
        var totalPages = Math.ceil(state.history.total / state.history.pageSize) || 1;
        utils.$('historyPageInfo').textContent = 'Page ' + state.history.currentPage + ' of ' + totalPages + ' (Total: ' + state.history.total + ')';
        utils.$('btnPrevPage').disabled = (state.history.currentPage <= 1);
        utils.$('btnNextPage').disabled = (state.history.currentPage >= totalPages);
        utils.$('btnPrevPage').onclick = function () {
            if (state.history.currentPage > 1) loadHistory(state.history.currentPage - 1);
        };
        utils.$('btnNextPage').onclick = function () {
            if (state.history.currentPage < totalPages) loadHistory(state.history.currentPage + 1);
        };
    }

    function clearHistory() {
        if (!confirm('Clear history?')) return;
        api.deleteHistory().then(function () { loadHistory(); });
    }

    function bindHistoryButtons() {
        document.querySelectorAll('.btn-relaunch').forEach(function (btn) {
            btn.onclick = function () {
                var entry = JSON.parse(decodeURIComponent(this.getAttribute('data-job')));
                loadEntryToForm(entry);
                if (typeof $ !== 'undefined') $('.nav-tabs a[href="#launchTab"]').tab('show');
            };
        });
        document.querySelectorAll('.btn-clone').forEach(function (btn) {
            btn.onclick = function () {
                var entry = JSON.parse(decodeURIComponent(this.getAttribute('data-job')));
                cloneAndLaunch(entry);
            };
        });
        document.querySelectorAll('.btn-save-template').forEach(function (btn) {
            btn.onclick = function () {
                var entry = JSON.parse(decodeURIComponent(this.getAttribute('data-job')));
                saveEntryAsTemplate(entry);
            };
        });
    }

    function loadEntryToForm(entry) {
        var template = {
            mode: entry.mode,
            className: entry.className,
            methodName: entry.methodName,
            queue: entry.queue,
            executionMode: entry.executionMode,
            cronExpression: entry.cronExpression,
            delayMinutes: entry.delayMinutes,
            scheduledDateTime: entry.scheduledDateTime,
            parentJobId: entry.parentJobId,
            recurringEngine: entry.recurringEngine || entry.engine || 'BuiltIn',
            includePerformContext: entry.includePerformContext,
            includeCancellationToken: entry.includeCancellationToken,
            rawParametersJson: entry.parametersJson,
            parameters: null
        };
        ns.templates.loadTemplateToForm(template);
    }

    function cloneAndLaunch(entry) {
        var template = {
            mode: entry.mode,
            className: entry.className,
            methodName: entry.methodName,
            queue: entry.queue,
            executionMode: entry.executionMode,
            cronExpression: entry.cronExpression,
            delayMinutes: entry.delayMinutes,
            scheduledDateTime: entry.scheduledDateTime,
            parentJobId: entry.parentJobId,
            recurringEngine: entry.recurringEngine || entry.engine || 'BuiltIn',
            includePerformContext: entry.includePerformContext,
            includeCancellationToken: entry.includeCancellationToken,
            rawParametersJson: entry.parametersJson,
            parameters: null
        };
        api.launchJob(template).then(function (res) {
            if (res.success) {
                alert('Cloned job launched: ' + res.jobId);
                loadHistory();
            } else {
                alert('Error: ' + (res.error || 'Failed'));
            }
        });
    }

    ns.history = {
        load: loadHistory,
        clear: clearHistory,
        saveEntryAsTemplate: saveEntryAsTemplate,
        bindButtons: bindHistoryButtons
    };
})(window);