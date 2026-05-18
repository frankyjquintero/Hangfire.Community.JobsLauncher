/* global window, document */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;

    function buildRecurringEngineOptions() {
        var mode = document.querySelector('input[name="launchMode"]:checked').value;
        var sel = utils.$('recurringEngine');
        sel.innerHTML = '';

        function addOption(value, text, enabled) {
            var opt = document.createElement('option');
            opt.value = value;
            opt.textContent = text;
            if (!enabled) opt.disabled = true;
            sel.appendChild(opt);
        }

        if (mode === 'assisted') {
            addOption('Direct', 'Direct', true);
            addOption('BuiltIn', 'Built-in (lightweight)', true);
            addOption('DynamicJobs', 'DynamicJobs (advanced)', state.dynamicJobsAvailable);
        } else {
            addOption('BuiltIn', 'Built-in (lightweight)', true);
            addOption('DynamicJobs', 'DynamicJobs (advanced)', state.dynamicJobsAvailable);
        }

        if (!state.dynamicJobsAvailable && sel.value === 'DynamicJobs') {
            utils.$('dynamicJobsWarning').style.display = 'inline';
        } else {
            utils.$('dynamicJobsWarning').style.display = 'none';
        }
    }

    function toggleMode() {
        var mode = document.querySelector('input[name="launchMode"]:checked').value;
        if (mode === 'assisted') {
            utils.$('assistedFields').style.display = 'block';
            utils.$('manualFields').style.display = 'none';
        } else {
            utils.$('assistedFields').style.display = 'none';
            utils.$('manualFields').style.display = 'block';
        }
        buildRecurringEngineOptions();
        toggleExecMode();
    }

    function toggleExecMode() {
        var mode = document.querySelector('input[name="execMode"]:checked').value;
        utils.$('scheduleFields').style.display = (mode === 'Schedule') ? 'block' : 'none';
        utils.$('scheduleDateTimeFields').style.display = (mode === 'ScheduleDateTime') ? 'block' : 'none';
        utils.$('recurringFields').style.display = (mode === 'Recurring') ? 'block' : 'none';
        utils.$('continuationFields').style.display = (mode === 'Continuation') ? 'block' : 'none';
        if (mode === 'Recurring') {
            buildRecurringEngineOptions();
        }
    }

    function checkCriticalQueue(queue) {
        if (state.criticalQueues.indexOf(queue) >= 0) {
            utils.$('criticalQueueWarning').style.display = 'block';
            return true;
        }
        utils.$('criticalQueueWarning').style.display = 'none';
        return false;
    }

    function loadQueues() {
        api.getQueues().then(function (resp) {
            var datalist = utils.$('queueList');
            datalist.innerHTML = '';
            (resp.queues || []).forEach(function (q) {
                datalist.innerHTML += '<option value="' + q + '">';
            });
        });
    }

    /* ── Submit y lanzamiento ── */
    var pendingLaunchRequest = null;

    function confirmedLaunch() {
        if (typeof $ !== 'undefined') $('#criticalConfirmModal').modal('hide');
        if (pendingLaunchRequest) launchJob(pendingLaunchRequest);
    }

    function submitJob() {
        var req = ns.params.buildRequest();
        if (!req.className || !req.methodName) { alert('ClassName and MethodName are required.'); return; }
        if (checkCriticalQueue(req.queue)) {
            pendingLaunchRequest = req;
            utils.$('criticalQueueName').textContent = req.queue;
            utils.$('criticalJobSummary').textContent = 'Class: ' + req.className + '\nMethod: ' + req.methodName + '\nMode: ' + req.executionMode;
            if (typeof $ !== 'undefined') $('#criticalConfirmModal').modal('show');
        } else {
            launchJob(req);
        }
    }

    function launchJob(req) {
        api.launchJob(req).then(function (result) {
            var alertDiv = utils.$('launchResult');
            alertDiv.style.display = 'block';
            if (result.success) {
                alertDiv.className = 'alert alert-success';
                alertDiv.innerHTML = 'Job launched successfully! <a href="' + result.link + '" target="_blank">' + result.jobId + '</a>';
                loadQueues();
                ns.history.load();
                setTimeout(function () { alertDiv.style.display = 'none'; }, 7000);
            } else {
                alertDiv.className = 'alert alert-danger';
                alertDiv.textContent = result.error || 'Failed.';
            }
        });
    }

    ns.ui = {
        buildRecurringEngineOptions: buildRecurringEngineOptions,
        toggleMode: toggleMode,
        toggleExecMode: toggleExecMode,
        checkCriticalQueue: checkCriticalQueue,
        loadQueues: loadQueues,
        submitJob: submitJob,
        confirmedLaunch: confirmedLaunch
    };
})(window);