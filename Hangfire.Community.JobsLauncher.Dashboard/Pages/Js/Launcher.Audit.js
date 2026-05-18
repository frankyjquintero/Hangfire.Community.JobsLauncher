/* global window, document */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;

    function loadAuditLog(page) {
        if (!page) page = 1;
        var user = utils.$('auditUserFilter').value.trim();
        var from = utils.toUtcString(utils.$('auditFromFilter').value);
        var to = utils.toUtcString(utils.$('auditToFilter').value);
        var pageSize = parseInt(utils.$('auditCountFilter').value, 10) || 20;
        var params = new URLSearchParams();
        if (user) params.append('user', user);
        if (from) params.append('from', from);
        if (to) params.append('to', to);
        params.append('page', page);
        params.append('pageSize', pageSize);

        api.getAuditLog(params).then(function (resp) {
            var entries = resp.items;
            state.audit.total = resp.total;
            state.audit.currentPage = resp.page;
            state.audit.pageSize = resp.pageSize;
            var tbody = document.querySelector('#auditLogTable tbody');
            tbody.innerHTML = '';
            entries.forEach(function (e) {
                var row = '<tr>' +
                    '<td>' + new Date(e.timestamp) + '</td>' +
                    '<td>' + (e.jobId || '') + '</td>' +
                    '<td>' + (e.className || '') + '</td>' +
                    '<td>' + (e.methodName || '') + '</td>' +
                    '<td>' + (e.queue || '') + '</td>' +
                    '<td>' + (e.mode || '') + '</td>' +
                    '<td>' + (e.engine || '') + '</td>' +
                    '<td>' + (e.user || '') + '</td>' +
                    '</tr>';
                tbody.innerHTML += row;
            });
            updateAuditPaginationControls();
        });
    }

    function updateAuditPaginationControls() {
        var totalPages = Math.ceil(state.audit.total / state.audit.pageSize) || 1;
        var prevBtn = utils.$('btnAuditPrevPage');
        var nextBtn = utils.$('btnAuditNextPage');
        var info = utils.$('auditPageInfo');
        info.textContent = 'Page ' + state.audit.currentPage + ' of ' + totalPages + ' (Total: ' + state.audit.total + ')';
        prevBtn.disabled = (state.audit.currentPage <= 1);
        nextBtn.disabled = (state.audit.currentPage >= totalPages);
        prevBtn.onclick = function () {
            if (state.audit.currentPage > 1) loadAuditLog(state.audit.currentPage - 1);
        };
        nextBtn.onclick = function () {
            if (state.audit.currentPage < totalPages) loadAuditLog(state.audit.currentPage + 1);
        };
    }

    ns.audit = {
        load: loadAuditLog,
        applyFilters: function () { loadAuditLog(1); },
        clearFilters: function () {
            utils.$('auditUserFilter').value = '';
            utils.$('auditFromFilter').value = '';
            utils.$('auditToFilter').value = '';
            utils.$('auditCountFilter').value = '20';
            loadAuditLog(1);
        }
    };
})(window);