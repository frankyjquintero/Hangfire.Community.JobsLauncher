/* global window, document, $, FileReader */
(function (w) {
    'use strict';
    var ns = w.JobLauncher = w.JobLauncher || {};
    var state = ns.state;
    var utils = ns.utils;
    var api = ns.api;
    var params = ns.params;
    var ui = ns.ui;

    function loadTemplates() {
        api.getTemplates().then(function (resp) {
            var tbody = utils.$('templatesTable').querySelector('tbody');
            tbody.innerHTML = '';

            // La API devuelve un array de plantillas directamente
            var templates = Array.isArray(resp) ? resp : [];
            templates.forEach(function (t) {
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
                    '<button class="btn btn-xs btn-success btn-clone-launch" data-name="' + encodeURIComponent(t.name) + '">Clone & Launch</button> ' +
                    '<button class="btn btn-xs btn-danger btn-delete" data-name="' + encodeURIComponent(t.name) + '">Delete</button> ' +
                    '<button class="btn btn-xs btn-default btn-export" data-name="' + encodeURIComponent(t.name) + '">Export</button>' +
                    '</td>' +
                    '</tr>';
                tbody.innerHTML += row;
            });

            bindTemplateButtons();
        }).catch(function (err) {
            console.error('Failed to load templates:', err);
        });
    }

    function showTemplatePreview(template) {
        var setText = function (id, value) {
            var el = document.getElementById(id);
            if (el) el.textContent = value || '';
        };

        setText('prevName', template.name);
        setText('prevClass', template.className);
        setText('prevMethod', template.methodName);
        setText('prevQueue', template.queue);
        setText('prevCron', template.cronExpression);
        setText('prevDelay', template.delayMinutes);
        setText('prevScheduled', template.scheduledDateTime);
        setText('prevParentId', template.parentJobId);
        setText('prevEngine', template.recurringEngine || template.engine || 'N/A');
        setText('prevMode', template.mode);
        setText('prevPerformContext', template.includePerformContext ? 'Yes' : 'No');
        setText('prevCancellationToken', template.includeCancellationToken ? 'Yes' : 'No');

        var rawParams = template.rawParametersJson || '{}';
        try {
            var parsed = JSON.parse(rawParams);
            var paramsEl = document.getElementById('prevParams');
            if (paramsEl) paramsEl.textContent = JSON.stringify(parsed, null, 2);
        } catch (e) {
            var paramsEl = document.getElementById('prevParams');
            if (paramsEl) paramsEl.textContent = rawParams;
        }

        // Mostrar el modal si jQuery está disponible
        if (typeof $ !== 'undefined' && $('#templatePreviewModal').length) {
            $('#templatePreviewModal').modal('show');
        }
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
                api.deleteTemplate(name).then(function () { loadTemplates(); });
            };
        });
        document.querySelectorAll('.btn-export').forEach(function (btn) {
            btn.onclick = function () {
                var name = decodeURIComponent(this.getAttribute('data-name'));
                var downloadUrl = state.apiBaseUrl + '/api/export-import?action=export&templateName=' + encodeURIComponent(name);
                fetch(downloadUrl)
                    .then(function (response) { return response.blob(); })
                    .then(function (blob) {
                        var url = URL.createObjectURL(blob);
                        var a = document.createElement('a');
                        a.href = url;
                        a.download = name + '.json';
                        document.body.appendChild(a);
                        a.click();
                        document.body.removeChild(a);
                        URL.revokeObjectURL(url);
                    })
                    .catch(function (err) { alert('Error downloading template: ' + err.message); });
            };
        });
        document.querySelectorAll('.btn-clone-launch').forEach(function (btn) {
            btn.onclick = function () {
                var tpl = JSON.parse(decodeURIComponent(this.getAttribute('data-tpl')));
                if (confirm('Clone and launch?')) {
                    var req = ns.params.buildRequestFromTemplate(tpl); // necesitas exponer buildRequestFromTemplate en params o definirla aquí
                    ns.ui.launchJob(req);
                }
            };
        });
    }

    function saveCurrentAsTemplate() {
        var req = params.buildRequest();
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
        api.saveTemplate(template).then(function (res) {
            if (res.success) {
                alert('Template "' + name + '" saved.');
                loadTemplates();
            } else {
                alert('Error: ' + (res.error || res.message));
            }
        });
    }

    function importTemplate() {
        var fileInput = utils.$('importFile');
        if (!fileInput.files[0]) { alert('Select a file.'); return; }
        var reader = new FileReader();
        reader.onload = function (e) {
            var template = JSON.parse(e.target.result);
            api.saveTemplate(template).then(function (res) {
                if (res.conflict) {
                    if (confirm(res.message + ' Overwrite?')) {
                        api.saveTemplate(template).then(function () { loadTemplates(); });
                    }
                } else {
                    alert(res.message || 'Imported.');
                    loadTemplates();
                }
            });
        };
        reader.readAsText(fileInput.files[0]);
    }

    /* ── Llenado del formulario desde template ── */
    function inferModeAndLoad(template) {
        var inferred = (template.rawParametersJson && template.rawParametersJson !== '{}') ? 'manual' : 'assisted';
        var radio = document.querySelector('input[name="launchMode"][value="' + inferred + '"]');
        if (radio) radio.checked = true;
        ui.toggleMode();
        params.fillCommonFields(template);
        if (template.executionMode) {
            var execRadio = document.querySelector('input[name="execMode"][value="' + template.executionMode + '"]');
            if (execRadio) execRadio.checked = true;
            ui.toggleExecMode();
            params.fillExecModeFields(template);
        }
        if (inferred === 'manual') {
            params.fillManualFields(template);
        } else {
            fillAssistedFieldsForTemplate(template);
        }
    }

    function fillAssistedFieldsForTemplate(template) {
        utils.$('classNameAssisted').value = template.className || '';
        if (!template.className) return;
        api.getMethods(template.className).then(function (resp) {
            if (!resp.success) {
                alert(resp.error + ' Switched to manual mode.');
                document.querySelector('input[name="launchMode"][value="manual"]').checked = true;
                ui.toggleMode();
                params.fillManualFields(template);
                return;
            }
            state.currentMethods = resp.methods;
            var sel = utils.$('methodSelect');
            sel.innerHTML = '<option value="">-- Select method --</option>';
            state.currentMethods.forEach(function (m, i) {
                var pnames = m.parameters.map(function (p) { return p.name; }).join(', ');
                sel.innerHTML += '<option value="' + i + '">' + m.methodName + '(' + pnames + ')</option>';
            });
            utils.$('methodSelectGroup').style.display = 'block';
            var idx = state.currentMethods.findIndex(function (m) { return m.methodName === template.methodName; });
            if (idx >= 0) {
                sel.value = idx;
                params.onMethodChange();
                try {
                    var parsed = JSON.parse(template.rawParametersJson || '{}');
                    params.fillAssistedParams(parsed);
                } catch (e) { }
            }
        });
    }

    function loadTemplateToForm(template) {
        var mode = template.mode;
        if (!mode) {
            inferModeAndLoad(template);
            return;
        }
        var radio = document.querySelector('input[name="launchMode"][value="' + mode + '"]');
        if (radio) radio.checked = true;
        ui.toggleMode();
        params.fillCommonFields(template);
        if (template.executionMode) {
            var execRadio = document.querySelector('input[name="execMode"][value="' + template.executionMode + '"]');
            if (execRadio) execRadio.checked = true;
            ui.toggleExecMode();
            params.fillExecModeFields(template);
        }
        if (mode === 'manual') {
            params.fillManualFields(template);
        } else {
            fillAssistedFieldsForTemplate(template);
        }
    }

    ns.templates = {
        load: loadTemplates,
        saveCurrent: saveCurrentAsTemplate,
        import: importTemplate,
        bindButtons: bindTemplateButtons,
        loadTemplateToForm: loadTemplateToForm
    };
})(window);