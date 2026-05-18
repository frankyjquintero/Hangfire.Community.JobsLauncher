/* global window, document, $, moment */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;

    function buildCronExpressionFromModal() {
        var minute = '*';
        var hour = '*';
        var dayMonth = '*';
        var month = '*';
        var dayWeek = '*';

        var minEveryN = utils.$('minEveryN').value;
        var minSpecific = utils.$('minSpecific').value.trim();
        var minRangeFrom = utils.$('minRangeFrom').value;
        var minRangeTo = utils.$('minRangeTo').value;
        if (minEveryN && minEveryN !== '1') minute = '*/' + minEveryN;
        else if (minSpecific) minute = minSpecific;
        else if (minRangeFrom && minRangeTo) minute = minRangeFrom + '-' + minRangeTo;

        var hourEveryN = utils.$('hourEveryN').value;
        var hourSpecific = utils.$('hourSpecific').value.trim();
        var hourRangeFrom = utils.$('hourRangeFrom').value;
        var hourRangeTo = utils.$('hourRangeTo').value;
        if (hourEveryN && hourEveryN !== '1') hour = '*/' + hourEveryN;
        else if (hourSpecific) hour = hourSpecific;
        else if (hourRangeFrom && hourRangeTo) hour = hourRangeFrom + '-' + hourRangeTo;

        var dayMonthSpecific = utils.$('dayMonthSpecific').value.trim();
        var dayMonthEveryN = utils.$('dayMonthEveryN').value;
        var dayMonthStart = utils.$('dayMonthStart').value;
        var checkedDays = [];
        document.querySelectorAll('.day-month-check:checked').forEach(function (cb) { checkedDays.push(cb.value); });
        if (checkedDays.length > 0) dayMonth = checkedDays.join(',');
        else if (dayMonthSpecific) dayMonth = dayMonthSpecific;
        else if (dayMonthEveryN && dayMonthEveryN !== '1') dayMonth = (dayMonthStart ? dayMonthStart + '/' + dayMonthEveryN : '*/' + dayMonthEveryN);

        var monthSpecific = utils.$('monthSpecific').value.trim();
        var checkedMonths = [];
        document.querySelectorAll('.month-check:checked').forEach(function (cb) { checkedMonths.push(cb.value); });
        if (checkedMonths.length > 0) month = checkedMonths.join(',');
        else if (monthSpecific) month = monthSpecific;

        var checkedWeekdays = [];
        document.querySelectorAll('.weekday-check:checked').forEach(function (cb) { checkedWeekdays.push(cb.value); });
        var weekdaySpecific = utils.$('weekdaySpecific').value.trim();
        var weekdayEveryN = utils.$('weekdayEveryN').value;
        var weekdayStart = utils.$('weekdayStart').value;
        if (checkedWeekdays.length > 0) dayWeek = checkedWeekdays.join(',');
        else if (weekdaySpecific) dayWeek = weekdaySpecific;
        else if (weekdayEveryN && weekdayEveryN !== '1') dayWeek = (weekdayStart ? weekdayStart + '/' + weekdayEveryN : '*/' + weekdayEveryN);

        return minute + ' ' + hour + ' ' + dayMonth + ' ' + month + ' ' + dayWeek;
    }

    function formatTime(hour, minute) {
        var h = hour === '*' ? '0' : hour;
        var m = minute === '*' ? '0' : minute;
        return moment({ hour: h, minute: m }).format('h:mm A');
    }

    function generateCronDescription() {
        var parts = buildCronExpressionFromModal().split(' ');
        var minute = parts[0], hour = parts[1], dayM = parts[2], month = parts[3], dayW = parts[4];
        var desc = [];
        if (minute === '*') desc.push('every minute');
        else if (minute.indexOf('*/') === 0) desc.push('every ' + minute.slice(2) + ' minutes');
        else desc.push('at minute ' + minute);
        if (hour === '*') desc.push('of every hour');
        else if (hour.indexOf('*/') === 0) desc.push('every ' + hour.slice(2) + ' hours');
        else desc.push('at ' + formatTime(hour, minute));
        if (dayM !== '*') {
            if (dayM.indexOf('/') >= 0) {
                var dmParts = dayM.split('/');
                desc.push('every ' + dmParts[1] + ' days starting on day ' + dmParts[0]);
            } else {
                desc.push('on day(s) ' + dayM);
            }
        }
        if (month !== '*') desc.push('in month(s) ' + month);
        if (dayW !== '*') {
            var days = dayW.split(',').map(function (d) {
                return moment().day(parseInt(d, 10)).format('dddd');
            }).join(', ');
            desc.push('on ' + days);
        }
        return desc.join(' ') || 'Custom expression';
    }

    function updateCronPreview() {
        var expression = buildCronExpressionFromModal();
        utils.$('cronPreviewExpression').value = expression;
        utils.$('cronExecutionsPreview').innerHTML = '';
        utils.$('cronDescription').textContent = generateCronDescription();
    }

    function resetCronGenerator() {
        utils.$('minEveryN').value = '';
        utils.$('minSpecific').value = '';
        utils.$('minRangeFrom').value = '';
        utils.$('minRangeTo').value = '';

        utils.$('hourEveryN').value = '';
        utils.$('hourSpecific').value = '';
        utils.$('hourRangeFrom').value = '';
        utils.$('hourRangeTo').value = '';

        utils.$('dayMonthSpecific').value = '';
        utils.$('dayMonthEveryN').value = '1';
        utils.$('dayMonthStart').value = '1';
        document.querySelectorAll('.day-month-check').forEach(function (cb) { cb.checked = false; });

        utils.$('monthSpecific').value = '';
        document.querySelectorAll('.month-check').forEach(function (cb) { cb.checked = false; });

        utils.$('weekdaySpecific').value = '';
        utils.$('weekdayEveryN').value = '1';
        utils.$('weekdayStart').value = '1';
        document.querySelectorAll('.weekday-check').forEach(function (cb) { cb.checked = false; });
    }

    function showExecutions() {
        var expr = utils.$('cronPreviewExpression').value;
        if (!expr) return;
        api.validateCron(expr).then(function (resp) {
            var div = utils.$('cronExecutionsPreview');
            if (!resp.success) {
                div.innerHTML = '<span class="text-danger">' + resp.error + '</span>';
                return;
            }
            var occurrences = resp.occurrences.map(function (isoDate) {
                var m = moment.utc(isoDate);
                return m.local().format('ddd, MMM Do YYYY, h:mm:ss A');
            });
            var firstMoment = moment.utc(resp.occurrences[0]);
            var fromNow = firstMoment.fromNow();
            var html = '<strong>Next executions:</strong><br>';
            html += '<span style="font-weight:bold; color:#337ab7;">' + fromNow + '</span><br>';
            html += occurrences.join('<br>');
            div.innerHTML = html;
        }).catch(function (err) {
            utils.$('cronExecutionsPreview').innerHTML = '<span class="text-danger">Error: ' + err.message + '</span>';
        });
    }

    function validateCronInline() {
        var expr = utils.$('cronExpression').value.trim();
        if (!expr) { alert('Cron expression required'); return; }
        api.validateCron(expr).then(function (resp) {
            var div = utils.$('cronPreview');
            if (!resp.success) {
                div.innerHTML = '<span class="text-danger">' + resp.error + '</span>';
            } else {
                div.innerHTML = 'Next occurrences: ' + resp.occurrences.join(', ');
            }
            div.style.display = 'block';
        });
    }

    function initCronModal() {
        if (typeof $ === 'undefined') return;
        $('#btnGenerateCron').on('click', function () {
            var expression = buildCronExpressionFromModal();
            if (expression) {
                utils.$('cronExpression').value = expression;
                $('#cronGeneratorModal').modal('hide');
                validateCronInline();
            }
        });
        $('#cronGeneratorModal').on('shown.bs.modal', function () {
            resetCronGenerator();
            $('.nav-tabs a[href="#tabMinutes"]').tab('show');
            updateCronPreview();
        });
        $('#cronGeneratorModal').on('change input', 'input, select', function () {
            updateCronPreview();
        });
        $('a[data-toggle="tab"]').on('shown.bs.tab', function () {
            updateCronPreview();
        });
        $('#btnShowExecutions').on('click', showExecutions);
    }

    ns.cron = {
        validateCron: validateCronInline,
        initCronModal: initCronModal
    };
})(window);