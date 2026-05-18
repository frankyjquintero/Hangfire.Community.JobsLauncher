/* global window, document, $, moment */
(function (w) {
    'use strict';

    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;

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

    function validateJson() {
        var text = utils.$('jsonParams').value.trim();
        if (!text) return;
        try {
            JSON.parse(text);
            utils.$('jsonValidationMsg').style.display = 'none';
            alert('Valid JSON.');
        } catch (e) {
            utils.$('jsonValidationMsg').style.display = 'inline';
        }
    }

    function formatJson() {
        var text = utils.$('jsonParams').value.trim();
        try {
            var obj = JSON.parse(text);
            utils.$('jsonParams').value = JSON.stringify(obj, null, 2);
            utils.$('jsonValidationMsg').style.display = 'none';
        } catch (e) {
            utils.$('jsonValidationMsg').style.display = 'inline';
        }
    }

    function getDefaultValueForType(type, isComplex) {
        if (type.endsWith('?')) return null;
        if (isComplex) return {};

        var t = type.toLowerCase();
        if (t.includes('int') || t.includes('long') || t.includes('short') || t.includes('byte')) return 0;
        if (t.includes('double') || t.includes('float') || t.includes('decimal') || t.includes('single')) return 0.0;
        if (t.includes('bool')) return true;
        if (t.includes('datetime') || t.includes('datetimeoffset')) return new Date().toISOString();
        if (t === 'system.guid' || t === 'guid') return '00000000-0000-0000-0000-000000000000';
        if (t === 'system.timespan' || t === 'timespan') return '00:00:00';
        if (t === 'system.string' || t === 'string') return '';
        return '';
    }

    function suggestJsonStructure() {
        var className = utils.$('classNameManual').value.trim();
        var methodName = utils.$('methodNameManual').value.trim();

        if (!className || !methodName) {
            alert('Please enter Class Name and Method Name first.');
            return;
        }

        utils.fetchJson(state.apiBaseUrl + '/api/methods?className=' + encodeURIComponent(className))
            .then(function (resp) {
                if (!resp.success) {
                    alert('Assembly no disponible: ' + resp.error);
                    return;
                }
                var method = resp.methods.find(function (m) { return m.methodName === methodName; });
                if (!method) {
                    alert('Method not found.');
                    return;
                }
                var suggestion = {};
                method.parameters.forEach(function (p) {
                    suggestion[p.name] = getDefaultValueForType(p.type, p.isComplex);
                });
                utils.$('jsonParams').value = JSON.stringify(suggestion, null, 2);
            })
            .catch(function (err) {
                alert('Error loading suggestion: ' + err.message);
            });
    }

    function validateCron() {
        var expr = utils.$('cronExpression').value.trim();
        if (!expr) { alert('Cron expression required'); return; }

        utils.fetchJson(state.apiBaseUrl + '/api/validate-cron?expression=' + encodeURIComponent(expr))
            .then(function (resp) {
                var div = utils.$('cronPreview');
                if (!resp.success) {
                    div.innerHTML = '<span class="text-danger">' + resp.error + '</span>';
                } else {
                    div.innerHTML = 'Next occurrences: ' + resp.occurrences.join(', ');
                }
                div.style.display = 'block';
            });
    }

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

        utils.fetchJson(state.apiBaseUrl + '/api/validate-cron?expression=' + encodeURIComponent(expr))
            .then(function (resp) {
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
            })
            .catch(function (err) {
                utils.$('cronExecutionsPreview').innerHTML = '<span class="text-danger">Error: ' + err.message + '</span>';
            });
    }

    function initCronModal() {
        if (typeof $ === 'undefined') return;

        $('#btnGenerateCron').on('click', function () {
            var expression = buildCronExpressionFromModal();
            if (expression) {
                utils.$('cronExpression').value = expression;
                $('#cronGeneratorModal').modal('hide');
                validateCron();
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

    function isQueueCritical(queue) {
        return state.criticalQueues.indexOf(queue) >= 0;
    }

    function checkCriticalQueue(queue) {
        if (isQueueCritical(queue)) {
            utils.$('criticalQueueWarning').style.display = 'block';
            return true;
        }
        utils.$('criticalQueueWarning').style.display = 'none';
        return false;
    }

    function loadQueues() {
        return utils.fetchJson(state.apiBaseUrl + '/api/queues').then(function (resp) {
            var datalist = utils.$('queueList');
            datalist.innerHTML = '';
            (resp.queues || []).forEach(function (q) {
                datalist.innerHTML += '<option value="' + q + '">';
            });
        });
    }

    function isListType(type) {
        return type.indexOf('System.Collections.Generic.List') === 0 ||
            type.indexOf('System.Collections.Generic.IList') === 0 ||
            type.lastIndexOf('[]') === (type.length - 2);
    }

    function isDictType(type) {
        return type.indexOf('System.Collections.Generic.Dictionary') === 0;
    }

    function extractGenericArgument(typeName, index) {
        var match = typeName.match(/<(.+)>/);
        if (!match) return null;

        var args = match[1].split(',');
        return args[index] ? args[index].trim() : null;
    }

    function getInputTypeForSimple(typeName) {
        var t = (typeName || '').toLowerCase();
        if (t.indexOf('int') >= 0 || t.indexOf('long') >= 0 || t.indexOf('short') >= 0 || t.indexOf('byte') >= 0 ||
            t.indexOf('double') >= 0 || t.indexOf('float') >= 0 || t.indexOf('decimal') >= 0) return 'number';
        if (t.indexOf('datetime') >= 0) return 'datetime-local';
        if (t.indexOf('bool') >= 0) return 'checkbox';
        return 'text';
    }

    function createListItem(elementType) {
        var inputType = getInputTypeForSimple(elementType);
        return '<div class="list-item">' +
            '<input type="' + inputType + '" class="form-control input-sm" value="" />' +
            '<button type="button" class="btn btn-xs btn-danger remove-list-item">×</button>' +
            '</div>';
    }

    function createDictRow(keyType, valueType) {
        var keyInputType = getInputTypeForSimple(keyType);
        var valueInputType = getInputTypeForSimple(valueType);
        return '<tr class="dict-row">' +
            '<td><input type="' + keyInputType + '" class="form-control input-sm dict-key" placeholder="key" /></td>' +
            '<td><input type="' + valueInputType + '" class="form-control input-sm dict-value" placeholder="value" /></td>' +
            '<td><button type="button" class="btn btn-xs btn-danger remove-dict-row">×</button></td>' +
            '</tr>';
    }

    function generateListInput(paramInfo) {
        var name = paramInfo.name;
        var elementType = extractGenericArgument(paramInfo.type, 0) || 'string';
        var html = '<div class="list-container" data-param-name="' + name + '" data-element-type="' + elementType + '">';
        html += '<label>' + name + ' (List of ' + elementType + ')</label>';
        html += '<div class="list-items"></div>';
        html += '<button type="button" class="btn btn-xs btn-default add-list-item">+ Add item</button>';
        html += '</div>';
        return html;
    }

    function generateDictionaryInput(paramInfo) {
        var name = paramInfo.name;
        var keyType = extractGenericArgument(paramInfo.type, 0) || 'string';
        var valueType = extractGenericArgument(paramInfo.type, 1) || 'string';
        var html = '<div class="dict-container" data-param-name="' + name + '" data-key-type="' + keyType + '" data-value-type="' + valueType + '">';
        html += '<label>' + name + ' (Dictionary<' + keyType + ', ' + valueType + '>)</label>';
        html += '<table class="table table-condensed dict-items"><tbody></tbody></table>';
        html += '<button type="button" class="btn btn-xs btn-default add-dict-item">+ Add entry</button>';
        html += '</div>';
        return html;
    }

    function generateInputForSimpleType(paramInfo) {
        var type = paramInfo.type;
        var name = paramInfo.name;
        var isNullable = type.lastIndexOf('?') === (type.length - 1);
        var underlyingType = isNullable ? type.slice(0, -1).toLowerCase() : type.toLowerCase();

        var html = '';

        if (underlyingType.indexOf('int') >= 0 || underlyingType.indexOf('long') >= 0 ||
            underlyingType.indexOf('short') >= 0 || underlyingType.indexOf('byte') >= 0) {
            html = '<input type="number" class="form-control" data-param-name="' + name + '" step="1" value="' + (isNullable ? '' : '0') + '" />';
        }
        else if (underlyingType.indexOf('double') >= 0 || underlyingType.indexOf('float') >= 0 ||
            underlyingType.indexOf('decimal') >= 0 || underlyingType.indexOf('single') >= 0) {
            html = '<input type="number" class="form-control" data-param-name="' + name + '" step="any" value="' + (isNullable ? '' : '0.0') + '" />';
        }
        else if (underlyingType.indexOf('bool') >= 0) {
            html = '<select class="form-control" data-param-name="' + name + '">';
            if (isNullable) html += '<option value="">-- Not set --</option>';
            html += '<option value="true">True</option><option value="false">False</option>';
            html += '</select>';
        }
        else if (underlyingType.indexOf('datetime') >= 0 || underlyingType.indexOf('datetimeoffset') >= 0) {
            var defaultDate = isNullable ? '' : new Date(new Date().getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
            html = '<input type="datetime-local" class="form-control" data-param-name="' + name + '" value="' + defaultDate + '" />';
            html += '<small class="help-block">Click the calendar icon to select a date and time.</small>';
        }
        else if (underlyingType === 'system.datetime' || underlyingType === 'datetime') {
            var defaultDate2 = isNullable ? '' : new Date().toISOString().slice(0, 10);
            html = '<input type="date" class="form-control" data-param-name="' + name + '" value="' + defaultDate2 + '" />';
        }
        else if (underlyingType === 'system.guid' || underlyingType === 'guid') {
            html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="00000000-0000-0000-0000-000000000000" value="' + (isNullable ? '' : '00000000-0000-0000-0000-000000000000') + '" />';
        }
        else if (underlyingType === 'system.timespan' || underlyingType === 'timespan') {
            html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="hh:mm:ss" value="' + (isNullable ? '' : '00:00:00') + '" />';
        }
        else if (underlyingType.indexOf('.') >= 0 && underlyingType.indexOf('system.') !== 0) {
            if (paramInfo.enumValues && paramInfo.enumValues.length > 0) {
                html += '<select class="form-control" data-param-name="' + name + '">';
                paramInfo.enumValues.forEach(function (val) {
                    html += '<option value="' + val + '">' + val + '</option>';
                });
                html += '</select>';
            } else {
                html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="Enum value of ' + paramInfo.type + '" />';
            }
        }
        else {
            html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="' + paramInfo.type + '" />';
        }

        if (isNullable) {
            html += '<small class="text-muted">(Optional, leave empty for null)</small>';
        }

        return html;
    }

    function onMethodChange() {
        var idx = utils.$('methodSelect').value;
        if (idx === '') {
            utils.$('paramsContainer').innerHTML = '';
            state.selectedMethod = null;
            return;
        }

        var method = state.currentMethods[parseInt(idx, 10)];
        state.selectedMethod = method;

        var html = '';
        method.parameters.forEach(function (p) {
            html += '<div class="form-group param-field">';
            html += '<label>' + p.name + ' (' + p.type + ')</label>';

            if (p.isComplex) {
                if (isListType(p.type)) html += generateListInput(p);
                else if (isDictType(p.type)) html += generateDictionaryInput(p);
                else html += '<textarea class="form-control param-complex" data-param-name="' + p.name + '" rows="3" placeholder=\'Insert JSON for ' + p.type + '\'></textarea>';
            } else {
                html += generateInputForSimpleType(p);
            }

            html += '</div>';
        });

        utils.$('paramsContainer').innerHTML = html;

        // bind list/dict events
        document.querySelectorAll('.add-list-item').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var container = this.closest('.list-container');
                var elementType = container.getAttribute('data-element-type');
                var itemsDiv = container.querySelector('.list-items');
                itemsDiv.insertAdjacentHTML('beforeend', createListItem(elementType));
                bindRemoveListItems(itemsDiv);
            });
        });

        document.querySelectorAll('.add-dict-item').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var container = this.closest('.dict-container');
                var keyType = container.getAttribute('data-key-type');
                var valueType = container.getAttribute('data-value-type');
                var tbody = container.querySelector('.dict-items tbody');
                tbody.insertAdjacentHTML('beforeend', createDictRow(keyType, valueType));
                bindRemoveDictRows(tbody);
            });
        });

        document.querySelectorAll('.list-items').forEach(bindRemoveListItems);
        document.querySelectorAll('.dict-items tbody').forEach(bindRemoveDictRows);
    }

    function bindRemoveListItems(container) {
        container.querySelectorAll('.remove-list-item').forEach(function (btn) {
            btn.onclick = function () { this.closest('.list-item').remove(); };
        });
    }

    function bindRemoveDictRows(tbody) {
        tbody.querySelectorAll('.remove-dict-row').forEach(function (btn) {
            btn.onclick = function () { this.closest('.dict-row').remove(); };
        });
    }

    function loadMethods() {
        var className = utils.$('classNameAssisted').value.trim();
        if (!className) { alert('Class name required'); return; }

        utils.fetchJson(state.apiBaseUrl + '/api/methods?className=' + encodeURIComponent(className))
            .then(function (resp) {
                if (!resp.success) {
                    alert(resp.error + ' Switched to manual mode.');
                    document.querySelector('input[name="launchMode"][value="manual"]').checked = true;
                    toggleMode();
                    return;
                }

                state.currentMethods = resp.methods;

                var sel = utils.$('methodSelect');
                sel.innerHTML = '<option value="">-- Select method --</option>';
                state.currentMethods.forEach(function (m, i) {
                    var params = m.parameters.map(function (p) { return p.name; }).join(', ');
                    var display = m.methodName + '(' + params + ')';
                    sel.innerHTML += '<option value="' + i + '">' + display + '</option>';
                });

                utils.$('methodSelectGroup').style.display = 'block';
                utils.$('paramsContainer').innerHTML = '';
            });
    }

    function convertToType(value, type, isComplex) {
        var isNullable = type.lastIndexOf('?') === (type.length - 1);
        var underlyingType = isNullable ? type.slice(0, -1).toLowerCase() : type.toLowerCase();

        if (isNullable && (value === '' || value === null || value === undefined)) return null;

        if (isComplex) {
            if (value === '' && isNullable) return null;
            try { return JSON.parse(value); }
            catch (e) { return value; }
        }

        if (underlyingType.indexOf('int') >= 0 || underlyingType.indexOf('long') >= 0 ||
            underlyingType.indexOf('short') >= 0 || underlyingType.indexOf('byte') >= 0) {
            var num = parseInt(value, 10);
            return isNaN(num) ? (isNullable ? null : value) : num;
        }

        if (underlyingType.indexOf('double') >= 0 || underlyingType.indexOf('float') >= 0 ||
            underlyingType.indexOf('decimal') >= 0 || underlyingType.indexOf('single') >= 0) {
            var num2 = parseFloat(value);
            return isNaN(num2) ? (isNullable ? null : value) : num2;
        }

        if (underlyingType.indexOf('bool') >= 0) {
            if (typeof value === 'boolean') return value;
            if (value === '' && isNullable) return null;
            return value === 'true' || value === '1' || value === 'on';
        }

        return value;
    }

    function buildRequest() {
        var mode = document.querySelector('input[name="launchMode"]:checked').value;
        var className = (mode === 'assisted') ? utils.$('classNameAssisted').value.trim() : utils.$('classNameManual').value.trim();
        var methodName = (mode === 'assisted')
            ? (function () {
                var idx = utils.$('methodSelect').value;
                return idx === '' ? '' : state.currentMethods[parseInt(idx, 10)].methodName;
            })()
            : utils.$('methodNameManual').value.trim();

        var request = {
            mode: mode,
            className: className,
            methodName: methodName,
            queue: utils.$('queue').value.trim() || 'default',
            executionMode: document.querySelector('input[name="execMode"]:checked').value,
            includePerformContext: utils.$('chkPerformContext').checked,
            includeCancellationToken: utils.$('chkCancellationToken').checked,
            parameters: null,
            rawParametersJson: null
        };

        if (mode === 'manual') {
            request.rawParametersJson = utils.$('jsonParams').value.trim() || '{}';
        } else {
            var paramsObj = {};
            var allElements = document.querySelectorAll('#paramsContainer [data-param-name]');

            allElements.forEach(function (el) {
                var name = el.getAttribute('data-param-name');
                if (!name) return;

                if (el.classList.contains('list-container')) {
                    var elementType = el.getAttribute('data-element-type');
                    var inputs = el.querySelectorAll('.list-item input');
                    var values = [];
                    inputs.forEach(function (inp) {
                        if (inp.value.trim() !== '') values.push(convertToType(inp.value, elementType, false));
                    });
                    paramsObj[name] = values;
                }
                else if (el.classList.contains('dict-container')) {
                    var rows = el.querySelectorAll('.dict-row');
                    var obj = {};
                    rows.forEach(function (row) {
                        var keyInp = row.querySelector('.dict-key');
                        var valInp = row.querySelector('.dict-value');
                        if (keyInp && valInp && keyInp.value.trim() !== '') obj[keyInp.value] = valInp.value;
                    });
                    paramsObj[name] = obj;
                }
                else {
                    var value = (el.type === 'checkbox') ? el.checked : el.value;
                    var paramDef = state.selectedMethod ? state.selectedMethod.parameters.find(function (p) { return p.name === name; }) : null;
                    if (paramDef) value = convertToType(value, paramDef.type, paramDef.isComplex);
                    paramsObj[name] = value;
                }
            });

            request.rawParametersJson = JSON.stringify(paramsObj);
            request.parameters = null;
        }

        if (request.executionMode === 'Schedule') {
            request.delayMinutes = parseInt(utils.$('delayMinutes').value, 10) || 30;
        }
        if (request.executionMode === 'ScheduleDateTime') {
            request.scheduledDateTime = utils.$('scheduledDateTime').value
                ? new Date(utils.$('scheduledDateTime').value).toISOString()
                : null;
        }
        if (request.executionMode === 'Recurring') {
            request.cronExpression = utils.$('cronExpression').value.trim() || '* * * * *';
            request.recurringEngine = utils.$('recurringEngine').value;
        }
        if (request.executionMode === 'Continuation') {
            request.parentJobId = utils.$('parentJobId').value.trim();
        }

        return request;
    }

    function showPreview() {
        var req = buildRequest();
        var summary = 'Class: ' + req.className + '\nMethod: ' + req.methodName + '\nQueue: ' + req.queue +
            '\nMode: ' + req.executionMode + '\nEngine: ' + (req.recurringEngine || 'Direct') +
            '\nParameters: ' + (req.rawParametersJson || JSON.stringify(req.parameters));
        utils.$('previewContent').textContent = summary;
        utils.$('previewPanel').style.display = 'block';
    }

    var pendingLaunchRequest = null;

    function confirmedLaunch() {
        if (typeof $ !== 'undefined') $('#criticalConfirmModal').modal('hide');
        if (pendingLaunchRequest) launchJob(pendingLaunchRequest);
    }

    function submitJob() {
        var req = buildRequest();
        if (!req.className || !req.methodName) { alert('ClassName and MethodName are required.'); return; }

        if (isQueueCritical(req.queue)) {
            pendingLaunchRequest = req;
            utils.$('criticalQueueName').textContent = req.queue;
            utils.$('criticalJobSummary').textContent = 'Class: ' + req.className + '\nMethod: ' + req.methodName + '\nMode: ' + req.executionMode;
            if (typeof $ !== 'undefined') $('#criticalConfirmModal').modal('show');
        } else {
            launchJob(req);
        }
    }

    function launchJob(req) {
        var formData = new FormData();
        formData.append('json', JSON.stringify(req));

        fetch(state.apiBaseUrl + '/api/launch', { method: 'POST', body: formData })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                var alertDiv = utils.$('launchResult');
                alertDiv.style.display = 'block';
                if (result.success) {
                    alertDiv.className = 'alert alert-success';
                    alertDiv.innerHTML = 'Job launched successfully! <a href="' + result.link + '" target="_blank">' + result.jobId + '</a>';
                    loadQueues();
                    loadHistory();
                    setTimeout(function () { alertDiv.style.display = 'none'; }, 7000);
                } else {
                    alertDiv.className = 'alert alert-danger';
                    alertDiv.textContent = result.error || 'Failed.';
                }
            });
    }

    function loadHistory(page) {
        if (!page) page = 1;

        utils.fetchJson(state.apiBaseUrl + '/api/history?page=' + page + '&pageSize=' + state.history.pageSize)
            .then(function (resp) {
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
                        '</td>' +
                        '</tr>';
                    tbody.innerHTML += row;
                });

                updateHistoryPaginationControls();
                bindHistoryButtons();
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
        fetch(state.apiBaseUrl + '/api/history', { method: 'DELETE' }).then(function () { loadHistory(); });
    }

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

        utils.fetchJson(state.apiBaseUrl + '/api/audit-log?' + params.toString())
            .then(function (resp) {
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
    }

    function loadTemplates() {
        utils.fetchJson(state.apiBaseUrl + '/api/templates')
            .then(function (resp) {
                var tbody = utils.$('templatesTable').querySelector('tbody');
                tbody.innerHTML = '';

                resp.templates.forEach(function (t) {
                    var row = '<tr>' +
                        '<td>' + t.name + '</td>' +
                        '<td>' + t.className + '</td>' +
                        '<td>' + t.methodName + '</td>' +
                        '<td>' + t.queue + '</td>' +
                        '<td>' + (t.executionMode || '') + '</td>' +
                        '<td>' + (t.recurringEngine || '') + '</td>' +
                        '<td>' +
                        '<button class="btn btn-xs btn-info btn-load" data-tpl="' + encodeURIComponent(JSON.stringify(t)) + '">Load</button> ' +
                        '<button class="btn btn-xs btn-default btn-preview" data-tpl="' + encodeURIComponent(JSON.stringify(t)) + '">Preview</button> ' +
                        '<button class="btn btn-xs btn-danger btn-delete" data-name="' + encodeURIComponent(t.name) + '">Delete</button> ' +
                        '<button class="btn btn-xs btn-default btn-export" data-name="' + encodeURIComponent(t.name) + '">Export</button>' +
                        '</td>' +
                        '</tr>';
                    tbody.innerHTML += row;
                });

                bindTemplateButtons();
            });
    }

    function showTemplatePreview(template) {
        utils.$('prevName').textContent = template.name || '';
        utils.$('prevClass').textContent = template.className || '';
        utils.$('prevMethod').textContent = template.methodName || '';
        utils.$('prevQueue').textContent = template.queue || '';
        utils.$('prevCron').textContent = template.cronExpression || '';
        utils.$('prevDelay').textContent = template.delayMinutes || '';
        utils.$('prevScheduled').textContent = template.scheduledDateTime || '';
        utils.$('prevParentId').textContent = template.parentJobId || '';
        utils.$('prevEngine').textContent = template.recurringEngine || (template.engine || 'N/A');
        utils.$('prevMode').textContent = template.mode || 'N/A';
        utils.$('prevPerformContext').textContent = template.includePerformContext ? 'Yes' : 'No';
        utils.$('prevCancellationToken').textContent = template.includeCancellationToken ? 'Yes' : 'No';

        var rawParams = template.rawParametersJson || '{}';
        try {
            var parsed = JSON.parse(rawParams);
            utils.$('prevParams').textContent = JSON.stringify(parsed, null, 2);
        } catch (e) {
            utils.$('prevParams').textContent = rawParams;
        }

        if (typeof $ !== 'undefined') $('#templatePreviewModal').modal('show');
    }

    function bindTemplateButtons() {
        document.querySelectorAll('.btn-load').forEach(function (btn) {
            btn.onclick = function () {
                var tpl = JSON.parse(decodeURIComponent(this.getAttribute('data-tpl')));
                loadTemplateToForm(tpl);
                if (typeof $ !== 'undefined') $('.nav-tabs a[href="#launchTab"]').tab('show');
            };
        });

        document.querySelectorAll('.btn-preview').forEach(function (btn) {
            btn.onclick = function () {
                var tpl = JSON.parse(decodeURIComponent(this.getAttribute('data-tpl')));
                showTemplatePreview(tpl);
            };
        });

        document.querySelectorAll('.btn-delete').forEach(function (btn) {
            btn.onclick = function () {
                var name = decodeURIComponent(this.getAttribute('data-name'));
                if (!confirm('Delete template "' + name + '"?')) return;
                fetch(state.apiBaseUrl + '/api/templates?name=' + encodeURIComponent(name), { method: 'DELETE' })
                    .then(function () { loadTemplates(); });
            };
        });

        document.querySelectorAll('.btn-export').forEach(function (btn) {
            btn.onclick = function () {
                var name = decodeURIComponent(this.getAttribute('data-name'));
                w.location = state.apiBaseUrl + '/api/export-import?templateName=' + encodeURIComponent(name);
            };
        });
    }

    function saveCurrentAsTemplate() {
        var req = buildRequest();
        if (!req.className || !req.methodName) {
            alert('Please configure a class and method before saving as template.');
            return;
        }

        var name = prompt('Template name:', req.className + '.' + req.methodName);
        if (!name) return;

        var template = {
            name: name,
            mode: req.mode,
            className: req.className,
            methodName: req.methodName,
            queue: req.queue,
            executionMode: req.executionMode,
            cronExpression: req.cronExpression,
            delayMinutes: req.delayMinutes,
            scheduledDateTime: req.scheduledDateTime,
            parentJobId: req.parentJobId,
            recurringEngine: req.recurringEngine,
            includePerformContext: req.includePerformContext,
            includeCancellationToken: req.includeCancellationToken,
            rawParametersJson: req.rawParametersJson,
            parameters: req.parameters
        };

        var formData = new FormData();
        formData.append('json', JSON.stringify(template));

        fetch(state.apiBaseUrl + '/api/templates', { method: 'POST', body: formData })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res.success) {
                    alert('Template "' + name + '" saved.');
                    loadTemplates();
                } else {
                    alert('Error: ' + (res.error || res.message));
                }
            });
    }

    function fillCommonFields(template) {
        utils.$('queue').value = template.queue || 'default';
        checkCriticalQueue(utils.$('queue').value);
        utils.$('chkPerformContext').checked = !!template.includePerformContext;
        utils.$('chkCancellationToken').checked = !!template.includeCancellationToken;
    }

    function fillExecModeFields(template) {
        if (template.executionMode === 'Schedule') {
            utils.$('delayMinutes').value = template.delayMinutes || 30;
        }
        if (template.executionMode === 'ScheduleDateTime') {
            if (template.scheduledDateTime) {
                var dt = new Date(template.scheduledDateTime);
                utils.$('scheduledDateTime').value = new Date(dt.getTime() - dt.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
            }
        }
        if (template.executionMode === 'Recurring') {
            utils.$('cronExpression').value = template.cronExpression || '* * * * *';
            if (template.recurringEngine) utils.$('recurringEngine').value = template.recurringEngine;
        }
        if (template.executionMode === 'Continuation') {
            utils.$('parentJobId').value = template.parentJobId || '';
        }
    }

    function fillManualFields(template) {
        utils.$('classNameManual').value = template.className || '';
        utils.$('methodNameManual').value = template.methodName || '';
        utils.$('jsonParams').value = template.rawParametersJson || '{}';
    }

    function fillAssistedParams(obj) {
        Object.keys(obj || {}).forEach(function (k) {
            var el = document.querySelector('#paramsContainer [data-param-name="' + k + '"]');
            if (!el) return;

            if (el.type === 'checkbox') el.checked = !!obj[k];
            else el.value = obj[k];
        });
    }

    function fillAssistedFieldsForTemplate(template) {
        utils.$('classNameAssisted').value = template.className || '';
        if (!template.className) return;

        utils.fetchJson(state.apiBaseUrl + '/api/methods?className=' + encodeURIComponent(template.className))
            .then(function (resp) {
                if (!resp.success) {
                    alert(resp.error + ' Switched to manual mode.');
                    document.querySelector('input[name="launchMode"][value="manual"]').checked = true;
                    toggleMode();
                    fillManualFields(template);
                    return;
                }

                state.currentMethods = resp.methods;

                var sel = utils.$('methodSelect');
                sel.innerHTML = '<option value="">-- Select method --</option>';
                state.currentMethods.forEach(function (m, i) {
                    var params = m.parameters.map(function (p) { return p.name; }).join(', ');
                    var display = m.methodName + '(' + params + ')';
                    sel.innerHTML += '<option value="' + i + '">' + display + '</option>';
                });
                utils.$('methodSelectGroup').style.display = 'block';

                var idx = state.currentMethods.findIndex(function (m) { return m.methodName === template.methodName; });
                if (idx >= 0) {
                    sel.value = idx;
                    onMethodChange();
                    try {
                        var parsed = JSON.parse(template.rawParametersJson || '{}');
                        fillAssistedParams(parsed);
                    } catch (e) { }
                }
            });
    }

    function inferModeAndLoad(template) {
        var inferred = (template.rawParametersJson && template.rawParametersJson !== '{}') ? 'manual' : 'assisted';
        var radio = document.querySelector('input[name="launchMode"][value="' + inferred + '"]');
        if (radio) radio.checked = true;

        toggleMode();
        fillCommonFields(template);

        if (template.executionMode) {
            var execRadio = document.querySelector('input[name="execMode"][value="' + template.executionMode + '"]');
            if (execRadio) execRadio.checked = true;
            toggleExecMode();
            fillExecModeFields(template);
        }

        if (inferred === 'manual') fillManualFields(template);
        else fillAssistedFieldsForTemplate(template);
    }

    function loadTemplateToForm(template) {
        var mode = template.mode;
        if (!mode) {
            inferModeAndLoad(template);
            return;
        }

        var radio = document.querySelector('input[name="launchMode"][value="' + mode + '"]');
        if (radio) radio.checked = true;

        toggleMode();
        fillCommonFields(template);

        if (template.executionMode) {
            var execRadio = document.querySelector('input[name="execMode"][value="' + template.executionMode + '"]');
            if (execRadio) execRadio.checked = true;
            toggleExecMode();
            fillExecModeFields(template);
        }

        if (mode === 'manual') fillManualFields(template);
        else fillAssistedFieldsForTemplate(template);
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

        loadTemplateToForm(template);
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

        var formData = new FormData();
        formData.append('json', JSON.stringify(template));

        fetch(state.apiBaseUrl + '/api/launch', { method: 'POST', body: formData })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res.success) {
                    alert('Cloned job launched: ' + res.jobId);
                    loadHistory();
                } else {
                    alert('Error: ' + (res.error || 'Failed'));
                }
            });
    }

    function importTemplate() {
        var fileInput = utils.$('importFile');
        if (!fileInput.files[0]) { alert('Select a file.'); return; }

        var reader = new FileReader();
        reader.onload = function (e) {
            var template = JSON.parse(e.target.result);
            var formData = new FormData();
            formData.append('json', JSON.stringify(template));

            fetch(state.apiBaseUrl + '/api/templates', { method: 'POST', body: formData })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.conflict) {
                        if (confirm(res.message + ' Overwrite?')) {
                            fetch(state.apiBaseUrl + '/api/templates', { method: 'POST', body: formData })
                                .then(function () { loadTemplates(); });
                        }
                    } else {
                        alert(res.message || 'Imported.');
                        loadTemplates();
                    }
                });
        };

        reader.readAsText(fileInput.files[0]);
    }

    function bindEvents() {
        document.querySelectorAll('input[name="launchMode"]').forEach(function (r) {
            r.addEventListener('change', toggleMode);
        });

        utils.$('btnLoadMethods').addEventListener('click', loadMethods);
        utils.$('methodSelect').addEventListener('change', onMethodChange);

        document.querySelectorAll('input[name="execMode"]').forEach(function (r) {
            r.addEventListener('change', toggleExecMode);
        });

        utils.$('validateJsonBtn').addEventListener('click', validateJson);
        utils.$('formatJsonBtn').addEventListener('click', formatJson);
        utils.$('suggestJsonBtn').addEventListener('click', suggestJsonStructure);

        utils.$('btnValidateCron').addEventListener('click', validateCron);
        utils.$('btnOpenCronGenerator').addEventListener('click', function () {
            if (typeof $ !== 'undefined') $('#cronGeneratorModal').modal('show');
        });

        utils.$('btnPreview').addEventListener('click', showPreview);
        utils.$('btnLaunch').addEventListener('click', submitJob);
        utils.$('confirmCriticalLaunch').addEventListener('click', confirmedLaunch);

        utils.$('btnClearHistory').addEventListener('click', clearHistory);

        utils.$('btnImport').addEventListener('click', importTemplate);
        utils.$('btnSaveAsTemplate').addEventListener('click', saveCurrentAsTemplate);

        utils.$('queue').addEventListener('input', function () { checkCriticalQueue(this.value); });
        utils.$('queue').addEventListener('change', function () { checkCriticalQueue(this.value); });

        document.addEventListener('keydown', function (e) {
            if (e.ctrlKey && e.key === 'Enter') { e.preventDefault(); submitJob(); }
        });

        utils.$('btnApplyAuditFilters').addEventListener('click', function () { loadAuditLog(1); });
        utils.$('btnClearAuditFilters').addEventListener('click', function () {
            utils.$('auditUserFilter').value = '';
            utils.$('auditFromFilter').value = '';
            utils.$('auditToFilter').value = '';
            utils.$('auditCountFilter').value = '20';
            loadAuditLog(1);
        });

        toggleExecMode();
        toggleMode();
    }

    function init() {
        state.apiBaseUrl = (ns.config && ns.config.apiBaseUrl) ? ns.config.apiBaseUrl : '';

        utils.fetchJson(state.apiBaseUrl + '/api/capabilities').then(function (caps) {
            state.dynamicJobsAvailable = !!caps.dynamicJobsAvailable;
            if (caps.criticalQueues) {
                state.criticalQueues = caps.criticalQueues;
            }
            buildRecurringEngineOptions();

            if (caps.auditLogEnabled) {
                utils.$('auditLogTab').style.display = '';
                document.querySelector('a[href="#auditLogPane"]').addEventListener('click', function () {
                    loadAuditLog();
                });
            }
        });

        loadQueues();
        bindEvents();
        initCronModal();
        loadHistory();
        loadTemplates();
    }

    ns.app = {
        init: init,
        setCriticalQueues: function (queues) {
            state.criticalQueues = queues || [];
            checkCriticalQueue(utils.$('queue').value);
        }
    };

    document.addEventListener('DOMContentLoaded', init);
})(window);
