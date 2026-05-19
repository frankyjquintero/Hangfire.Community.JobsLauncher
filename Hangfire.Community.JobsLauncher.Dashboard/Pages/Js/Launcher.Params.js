/* global window, document */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;

    /* ── Helpers de tipos ── */
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

    function convertToType(value, type, isComplex) {
        var isNullable = type.lastIndexOf('?') === (type.length - 1);
        var underlyingType = isNullable ? type.slice(0, -1).toLowerCase() : type.toLowerCase();
        if (isNullable && (value === '' || value === null || value === undefined)) return null;
        if (isComplex) {
            if (value === '' && isNullable) return null;
            try { return JSON.parse(value); } catch (e) { return value; }
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

    /* ── Generadores de UI para parámetros ── */
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
        return '<div class="list-container" data-param-name="' + name + '" data-element-type="' + elementType + '">' +
            '<label>' + name + ' (List of ' + elementType + ')</label>' +
            '<div class="list-items"></div>' +
            '<button type="button" class="btn btn-xs btn-default add-list-item">+ Add item</button>' +
            '</div>';
    }

    function generateDictionaryInput(paramInfo) {
        var name = paramInfo.name;
        var keyType = extractGenericArgument(paramInfo.type, 0) || 'string';
        var valueType = extractGenericArgument(paramInfo.type, 1) || 'string';
        return '<div class="dict-container" data-param-name="' + name + '" data-key-type="' + keyType + '" data-value-type="' + valueType + '">' +
            '<label>' + name + ' (Dictionary&lt;' + keyType + ', ' + valueType + '&gt;)</label>' +
            '<table class="table table-condensed dict-items"><tbody></tbody></table>' +
            '<button type="button" class="btn btn-xs btn-default add-dict-item">+ Add entry</button>' +
            '</div>';
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
        } else if (underlyingType.indexOf('double') >= 0 || underlyingType.indexOf('float') >= 0 ||
            underlyingType.indexOf('decimal') >= 0 || underlyingType.indexOf('single') >= 0) {
            html = '<input type="number" class="form-control" data-param-name="' + name + '" step="any" value="' + (isNullable ? '' : '0.0') + '" />';
        } else if (underlyingType.indexOf('bool') >= 0) {
            html = '<select class="form-control" data-param-name="' + name + '">';
            if (isNullable) html += '<option value="">-- Not set --</option>';
            html += '<option value="true">True</option><option value="false">False</option></select>';
        } else if (underlyingType.indexOf('datetime') >= 0 || underlyingType.indexOf('datetimeoffset') >= 0) {
            var defaultDate = isNullable ? '' : new Date(new Date().getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
            html = '<input type="datetime-local" class="form-control" data-param-name="' + name + '" value="' + defaultDate + '" />';
            html += '<small class="help-block">Click the calendar icon to select a date and time.</small>';
        } else if (underlyingType === 'system.datetime' || underlyingType === 'datetime') {
            var defaultDate2 = isNullable ? '' : new Date().toISOString().slice(0, 10);
            html = '<input type="date" class="form-control" data-param-name="' + name + '" value="' + defaultDate2 + '" />';
        } else if (underlyingType === 'system.guid' || underlyingType === 'guid') {
            html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="00000000-0000-0000-0000-000000000000" value="' + (isNullable ? '' : '00000000-0000-0000-0000-000000000000') + '" />';
        } else if (underlyingType === 'system.timespan' || underlyingType === 'timespan') {
            html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="hh:mm:ss" value="' + (isNullable ? '' : '00:00:00') + '" />';
        } else if (underlyingType.indexOf('.') >= 0 && underlyingType.indexOf('system.') !== 0) {
            if (paramInfo.enumValues && paramInfo.enumValues.length > 0) {
                html += '<select class="form-control" data-param-name="' + name + '">';
                paramInfo.enumValues.forEach(function (val) { html += '<option value="' + val + '">' + val + '</option>'; });
                html += '</select>';
            } else {
                html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="Enum value of ' + paramInfo.type + '" />';
            }
        } else {
            html = '<input type="text" class="form-control" data-param-name="' + name + '" placeholder="' + paramInfo.type + '" />';
        }
        if (isNullable) {
            html += '<small class="text-muted">(Optional, leave empty for null)</small>';
        }
        return html;
    }

    /* ── Bindings de listas/diccionarios ── */
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

    function attachAddHandlers() {
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

    /* ── Lógica de carga de métodos (asistido) ── */
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
        attachAddHandlers();
    }

    function loadMethods() {
        var classNameEl = utils.$('classNameAssisted');
        if (!classNameEl) return;
        var className = classNameEl.value.trim();
        if (!className) { alert('Class name required'); return; }

        api.getMethods(className).then(function (resp) {
            if (!resp.success) {
                alert(resp.error + ' Switched to manual mode.');
                var manualRadio = document.querySelector('input[name="launchMode"][value="manual"]');
                if (manualRadio) manualRadio.checked = true;
                if (ns.ui && ns.ui.toggleMode) {
                    ns.ui.toggleMode();
                } else {
                    document.getElementById('assistedFields').classList.add('jobslauncher-hidden');
                    document.getElementById('manualFields').classList.remove('jobslauncher-hidden');
                }
                return;
            }
            state.currentMethods = resp.methods || [];
            var sel = utils.$('methodSelect');
            if (!sel) return;
            sel.innerHTML = '<option value="">-- Select method --</option>';
            state.currentMethods.forEach(function (m, i) {
                var params = m.parameters.map(function (p) { return p.name; }).join(', ');
                sel.innerHTML += '<option value="' + i + '">' + m.methodName + '(' + params + ')</option>';
            });
            var methodGroup = utils.$('methodSelectGroup');
            if (methodGroup) methodGroup.classList.remove('jobslauncher-hidden');
            var container = utils.$('paramsContainer');
            if (container) container.innerHTML = '';
        });
    }

    /* ── Funciones públicas de parámetros ── */
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

    function suggestJsonStructure() {
        var className = utils.$('classNameManual').value.trim();
        var methodName = utils.$('methodNameManual').value.trim();
        if (!className || !methodName) {
            alert('Please enter Class Name and Method Name first.');
            return;
        }
        api.getMethods(className).then(function (resp) {
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
        }).catch(function (err) {
            alert('Error loading suggestion: ' + err.message);
        });
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
                } else if (el.classList.contains('dict-container')) {
                    var rows = el.querySelectorAll('.dict-row');
                    var obj = {};
                    rows.forEach(function (row) {
                        var keyInp = row.querySelector('.dict-key');
                        var valInp = row.querySelector('.dict-value');
                        if (keyInp && valInp && keyInp.value.trim() !== '') obj[keyInp.value] = valInp.value;
                    });
                    paramsObj[name] = obj;
                } else {
                    var value = (el.type === 'checkbox') ? el.checked : el.value;
                    var paramDef = state.selectedMethod ? state.selectedMethod.parameters.find(function (p) { return p.name === name; }) : null;
                    if (paramDef) value = convertToType(value, paramDef.type, paramDef.isComplex);
                    paramsObj[name] = value;
                }
            });
            request.rawParametersJson = JSON.stringify(paramsObj);
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
        utils.$('previewPanel').classList.remove('jobslauncher-hidden');
    }

    /* ── Funciones auxiliares para rellenar campos (usadas por templates/history) ── */
    function fillCommonFields(template) {
        utils.$('queue').value = template.queue || 'default';
        ns.ui.checkCriticalQueue(utils.$('queue').value);
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

    function buildRequestFromTemplate(template) {
        return {
            mode: template.mode,
            className: template.className,
            methodName: template.methodName,
            queue: template.queue,
            executionMode: template.executionMode,
            cronExpression: template.cronExpression,
            delayMinutes: template.delayMinutes,
            scheduledDateTime: template.scheduledDateTime,
            parentJobId: template.parentJobId,
            recurringEngine: template.recurringEngine || template.engine || 'BuiltIn',
            includePerformContext: template.includePerformContext,
            includeCancellationToken: template.includeCancellationToken,
            rawParametersJson: template.rawParametersJson,
            buildRequestFromTemplate: buildRequestFromTemplate,
            parameters: null
        };
    }

    /* ── Funciones de UI (toggle, colas, submit) ── */
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
            utils.$('dynamicJobsWarning').classList.remove('jobslauncher-hidden');
        } else {
            utils.$('dynamicJobsWarning').classList.add('jobslauncher-hidden');
        }
    }

    function toggleMode() {
        var mode = document.querySelector('input[name="launchMode"]:checked').value;
        if (mode === 'assisted') {
            utils.$('assistedFields').classList.remove('jobslauncher-hidden');
            utils.$('manualFields').classList.add('jobslauncher-hidden');
        } else {
            utils.$('assistedFields').classList.add('jobslauncher-hidden');
            utils.$('manualFields').classList.remove('jobslauncher-hidden');
        }
        buildRecurringEngineOptions();
        toggleExecMode();
    }

    function toggleExecMode() {
        var mode = document.querySelector('input[name="execMode"]:checked').value;
        document.getElementById('scheduleFields').classList.toggle('jobslauncher-hidden', mode !== 'Schedule');
        document.getElementById('scheduleDateTimeFields').classList.toggle('jobslauncher-hidden', mode !== 'ScheduleDateTime');
        document.getElementById('recurringFields').classList.toggle('jobslauncher-hidden', mode !== 'Recurring');
        document.getElementById('continuationFields').classList.toggle('jobslauncher-hidden', mode !== 'Continuation');
        if (mode === 'Recurring') buildRecurringEngineOptions();
    }

    function checkCriticalQueue(queue) {
        var warning = utils.$('criticalQueueWarning');
        if (state.criticalQueues.indexOf(queue) >= 0) {
            warning.classList.remove('jobslauncher-hidden');
            return true;
        }
        warning.classList.add('jobslauncher-hidden');
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

    var pendingLaunchRequest = null;

    function launchJob(req) {
        api.launchJob(req).then(function (result) {
            var alertDiv = utils.$('launchResult');
            alertDiv.classList.remove('jobslauncher-hidden');
            if (result.success) {
                alertDiv.className = 'alert alert-success';
                alertDiv.innerHTML = 'Job launched successfully! <a href="' + result.link + '" target="_blank">' + result.jobId + '</a>';
                loadQueues();
                ns.history.load();
            } else {
                alertDiv.className = 'alert alert-danger';
                alertDiv.textContent = 'Error: ' + (result.error || 'Unknown error');
            }
        }).catch(function (err) {
            var alertDiv = utils.$('launchResult');
            alertDiv.classList.remove('jobslauncher-hidden');
            alertDiv.className = 'alert alert-danger';
            alertDiv.textContent = 'Network error: ' + err.message;
        });
    }

    function submitJob() {
        var req = buildRequest();
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

    function confirmedLaunch() {
        if (typeof $ !== 'undefined') $('#criticalConfirmModal').modal('hide');
        if (pendingLaunchRequest) launchJob(pendingLaunchRequest);
    }

    /* ── Exponer ── */
    ns.params = {
        loadMethods: loadMethods,
        onMethodChange: onMethodChange,
        validateJson: validateJson,
        formatJson: formatJson,
        suggestJsonStructure: suggestJsonStructure,
        buildRequest: buildRequest,
        showPreview: showPreview,
        fillCommonFields: fillCommonFields,
        fillExecModeFields: fillExecModeFields,
        fillManualFields: fillManualFields,
        fillAssistedParams: fillAssistedParams,
        buildRequestFromTemplate: buildRequestFromTemplate,
        attachAddHandlers: attachAddHandlers
    };

    // Reubicar funciones de UI bajo ns.ui (así Init.js sigue funcionando)
    ns.ui = {
        buildRecurringEngineOptions: buildRecurringEngineOptions,
        toggleMode: toggleMode,
        toggleExecMode: toggleExecMode,
        checkCriticalQueue: checkCriticalQueue,
        loadQueues: loadQueues,
        submitJob: submitJob,
        confirmedLaunch: confirmedLaunch,
        launchJob: launchJob    // expuesta por si otros módulos la usan
    };
})(window);