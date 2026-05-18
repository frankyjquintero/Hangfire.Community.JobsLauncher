/* global window */
(function (w) {
    'use strict';

    var ns = w.JobLauncher = w.JobLauncher || {};

    ns.state = {
        apiBaseUrl: null,
        currentMethods: [],
        dynamicJobsAvailable: false,
        criticalQueues: [],
        selectedMethod: null,

        history: { currentPage: 1, pageSize: 20, total: 0 },
        audit: { currentPage: 1, pageSize: 20, total: 0 }
    };
})(window);
