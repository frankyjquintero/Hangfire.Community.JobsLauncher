/* global window */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    ns.state = {
        apiBaseUrl: '',
        dynamicJobsAvailable: false,
        criticalQueues: [],
        currentMethods: [],
        selectedMethod: null,
        history: {
            currentPage: 1,
            pageSize: 10,
            total: 0
        },
        audit: {
            currentPage: 1,
            pageSize: 20,
            total: 0
        }
    };
})(window);