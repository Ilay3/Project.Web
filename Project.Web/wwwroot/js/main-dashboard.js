// Глобальные переменные
let dashboardData = {
    details: [],
    machineTypes: [],
    machines: [],
    batches: [],
    ganttStages: [],
    queueItems: []
};

// Инициализация при загрузке страницы
$(document).ready(function () {
    console.log('jQuery loaded, initializing dashboard...');

    // Проверяем, что данные переданы
    if (typeof window.initialData === 'undefined') {
        console.warn('initialData not found, using empty data');
        window.initialData = {
            details: [],
            machineTypes: [],
            machines: [],
            batches: [],
            ganttStages: [],
            queueItems: []
        };
    }

    initializeDashboard();

    // Автоматическое обновление каждые 30 секунд
    setInterval(refreshData, 30000);
});

// Инициализация главной панели
function initializeDashboard() {
    setupEventHandlers();
    loadInitialData();
}

// Настройка обработчиков событий
function setupEventHandlers() {
    // Обновление данных
    $('#refreshDataBtn').on('click', refreshData);

    // Обработчики для подпартий
    $('#splitBatchCheck').on('change', function () {
        $('#subBatchesContainer').toggleClass('d-none', !this.checked);
        if (this.checked) {
            generateSubBatchInputs();
        }
    });

    $('#subBatchCount').on('change', generateSubBatchInputs);

    // Обработчики для вкладок
    $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function (e) {
        var target = $(e.target).attr("data-bs-target");

        switch (target) {
            case '#gantt-pane':
                renderGanttChart();
                break;
            case '#queue-pane':
                renderQueue();
                break;
            case '#batches-pane':
                renderBatches();
                break;
            case '#machines-pane':
                renderMachines();
                break;
            case '#references-pane':
                renderDetails();
                renderMachineTypes();
                break;
        }
    });

    // Обработчики модальных окон
    $('#saveDetailBtn').on('click', saveDetail);
    $('#saveMachineTypeBtn').on('click', saveMachineType);
    $('#saveMachineBtn').on('click', saveMachine);
    $('#createBatchBtn').on('click', createBatch);
    $('#addSetupTimeBtn').on('click', addSetupTime);
}

// Загрузка начальных данных
function loadInitialData() {
    refreshData();
}

// Обновление данных с сервера
function refreshData() {
    $.get('/Main/RefreshData')
        .done(function (response) {
            if (response.success) {
                dashboardData.ganttStages = response.ganttStages || [];
                dashboardData.queueItems = response.queueItems || [];
                dashboardData.batches = response.batches || [];

                // Обновляем активную вкладку
                var activeTab = $('.nav-link.active').attr('data-bs-target');
                switch (activeTab) {
                    case '#gantt-pane':
                        renderGanttChart();
                        break;
                    case '#queue-pane':
                        renderQueue();
                        break;
                    case '#batches-pane':
                        renderBatches();
                        break;
                }

                showNotification('Данные обновлены', 'success');
            }
        })
        .fail(function () {
            showNotification('Ошибка при обновлении данных', 'error');
        });

    // Загружаем справочные данные
    loadReferenceData();
}

// Загрузка справочных данных
function loadReferenceData() {
    // Загружаем детали
    $.get('/Detail/GetDetails')
        .done(function (data) {
            dashboardData.details = data || [];
            renderDetails();
            populateDetailSelects();
        });

    // Загружаем типы станков
    $.get('/api/MachineTypes')
        .done(function (data) {
            dashboardData.machineTypes = data || [];
            renderMachineTypes();
            populateMachineTypeSelects();
        });

    // Загружаем станки
    $.get('/api/Machines')
        .done(function (data) {
            dashboardData.machines = data || [];
            renderMachines();
            populateMachineSelects();
        });
}

// Отображение диаграммы Ганта
function renderGanttChart() {
    const container = $('#gantt-container');
    container.empty();

    if (!dashboardData.ganttStages || dashboardData.ganttStages.length === 0) {
        container.html('<div class="text-center p-5"><i class="bi bi-calendar3" style="font-size: 3rem;"></i><h4 class="mt-3">Нет активных этапов</h4></div>');
        return;
    }

    // Группируем этапы по станкам
    const machineGroups = {};
    dashboardData.ganttStages.forEach(stage => {
        const machineKey = stage.machineId || 'unassigned';
        if (!machineGroups[machineKey]) {
            machineGroups[machineKey] = {
                machineName: stage.machineName || 'Не назначен',
                stages: []
            };
        }
        machineGroups[machineKey].stages.push(stage);
    });

    // Создаем строки для каждого станка
    let html = '<div class="gantt-timeline">';

    Object.keys(machineGroups).forEach(machineKey => {
        const group = machineGroups[machineKey];
        html += `<div class="gantt-row">
            <div class="gantt-row-header">${group.machineName}</div>
            <div class="gantt-row-content">`;

        group.stages.forEach(stage => {
            const statusClass = getStatusClass(stage.status);
            const setupClass = stage.isSetup ? 'setup' : '';

            html += `<div class="gantt-stage ${statusClass} ${setupClass}" 
                         data-stage-id="${stage.id}"
                         onclick="showStageControl(${stage.id})">
                <div class="stage-title">${stage.stageName}</div>
                <div class="stage-detail">${stage.detailName}</div>
                <div class="stage-time">${formatTime(stage.startTime)} - ${formatTime(stage.endTime)}</div>
            </div>`;
        });

        html += '</div></div>';
    });

    html += '</div>';
    container.html(html);
}

// Отображение очереди
function renderQueue() {
    const container = $('#queue-container');

    if (!dashboardData.queueItems || dashboardData.queueItems.length === 0) {
        container.html('<div class="text-center p-5"><i class="bi bi-check-circle text-success" style="font-size: 3rem;"></i><h4 class="mt-3">Очередь пуста</h4></div>');
        return;
    }

    let html = '';
    dashboardData.queueItems.forEach(item => {
        html += `<div class="queue-item mb-3 p-3 border rounded">
            <div class="d-flex justify-content-between align-items-center mb-2">
                <h6 class="mb-0">${item.detailName} - ${item.stageName}</h6>
                <span class="badge bg-warning text-dark">${item.status}</span>
            </div>
            <div class="row">
                <div class="col-md-8">
                    <p class="mb-1"><strong>Станок:</strong> ${item.expectedMachineName}</p>
                    <p class="mb-0"><strong>Ожидаемое начало:</strong> ${formatDateTime(item.expectedStartTime)}</p>
                </div>
                <div class="col-md-4">
                    <div class="btn-group-vertical w-100">
                        <button class="btn btn-sm btn-outline-primary" onclick="prioritizeStage(${item.stageExecutionId}, ${item.expectedMachineId})">
                            <i class="bi bi-arrow-up"></i> Приоритет
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="showReassignModal(${item.stageExecutionId})">
                            <i class="bi bi-arrow-left-right"></i> Переназначить
                        </button>
                    </div>
                </div>
            </div>
        </div>`;
    });

    container.html(html);
}

// Отображение партий
function renderBatches() {
    const tbody = $('#batches-table tbody');
    tbody.empty();

    if (!dashboardData.batches || dashboardData.batches.length === 0) {
        tbody.append('<tr><td colspan="6" class="text-center">Нет партий</td></tr>');
        return;
    }

    dashboardData.batches.forEach(batch => {
        const totalStages = batch.subBatches?.reduce((sum, sb) => sum + (sb.stageExecutions?.length || 0), 0) || 0;
        const completedStages = batch.subBatches?.reduce((sum, sb) => {
            return sum + (sb.stageExecutions?.filter(se => se.status === 'Completed').length || 0);
        }, 0) || 0;

        const progress = totalStages > 0 ? Math.round((completedStages / totalStages) * 100) : 0;
        const statusText = progress === 100 ? 'Завершено' : progress > 0 ? 'В работе' : 'Не начато';
        const statusClass = progress === 100 ? 'bg-success' : progress > 0 ? 'bg-primary' : 'bg-secondary';

        tbody.append(`
            <tr>
                <td>${batch.id}</td>
                <td>${batch.detailName}</td>
                <td>${batch.quantity}</td>
                <td>${formatDateTime(batch.createdUtc)}</td>
                <td>
                    <span class="badge ${statusClass}">${statusText}</span>
                    <div class="progress mt-1" style="height: 5px;">
                        <div class="progress-bar" style="width: ${progress}%"></div>
                    </div>
                </td>
                <td>
                    <button class="btn btn-sm btn-outline-info" onclick="showBatchDetails(${batch.id})">
                        <i class="bi bi-eye"></i> Подробнее
                    </button>
                </td>
            </tr>
        `);
    });
}

// Отображение станков
function renderMachines() {
    const container = $('#machines-container');

    if (!dashboardData.machines || dashboardData.machines.length === 0) {
        container.html('<div class="text-center p-5"><i class="bi bi-tools" style="font-size: 3rem;"></i><h4 class="mt-3">Нет станков</h4></div>');
        return;
    }

    let html = '<div class="row">';
    dashboardData.machines.forEach(machine => {
        const currentStage = dashboardData.ganttStages?.find(s => s.machineId === machine.id && s.status === 'InProgress');
        const statusText = currentStage ? (currentStage.isSetup ? 'Переналадка' : 'В работе') : 'Свободен';
        const statusClass = currentStage ? (currentStage.isSetup ? 'bg-info' : 'bg-primary') : 'bg-success';

        html += `<div class="col-md-6 col-lg-4 mb-3">
            <div class="card">
                <div class="card-body">
                    <h6 class="card-title">${machine.name}</h6>
                    <p class="card-text">
                        <strong>Инв. №:</strong> ${machine.inventoryNumber}<br>
                        <strong>Тип:</strong> ${machine.machineTypeName}<br>
                        <strong>Приоритет:</strong> ${machine.priority}
                    </p>
                    <span class="badge ${statusClass}">${statusText}</span>
                    <div class="mt-2">
                        <button class="btn btn-sm btn-outline-primary" onclick="editMachine(${machine.id})">
                            <i class="bi bi-pencil"></i> Изменить
                        </button>
                        <button class="btn btn-sm btn-outline-success" onclick="showMachineWorkspace(${machine.id})">
                            <i class="bi bi-tools"></i> Рабочее место
                        </button>
                    </div>
                </div>
            </div>
        </div>`;
    });
    html += '</div>';

    container.html(html);
}

// Отображение деталей
function renderDetails() {
    const container = $('#details-container');

    if (!dashboardData.details || dashboardData.details.length === 0) {
        container.html('<div class="text-center p-5"><i class="bi bi-box" style="font-size: 3rem;"></i><h4 class="mt-3">Нет деталей</h4></div>');
        return;
    }

    let html = '<div class="list-group">';
    dashboardData.details.forEach(detail => {
        html += `<div class="list-group-item d-flex justify-content-between align-items-center">
            <div>
                <h6 class="mb-1">${detail.name}</h6>
                <small class="text-muted">№ ${detail.number}</small>
            </div>
            <div class="btn-group">
                <button class="btn btn-sm btn-outline-primary" onclick="editDetail(${detail.id})">
                    <i class="bi bi-pencil"></i>
                </button>
                <button class="btn btn-sm btn-outline-info" onclick="showRouteModal(${detail.id})">
                    <i class="bi bi-diagram-3"></i>
                </button>
            </div>
        </div>`;
    });
    html += '</div>';

    container.html(html);
}

// Отображение типов станков
function renderMachineTypes() {
    const container = $('#machine-types-container');

    if (!dashboardData.machineTypes || dashboardData.machineTypes.length === 0) {
        container.html('<div class="text-center p-5"><i class="bi bi-gear" style="font-size: 3rem;"></i><h4 class="mt-3">Нет типов станков</h4></div>');
        return;
    }

    let html = '<div class="list-group">';
    dashboardData.machineTypes.forEach(type => {
        html += `<div class="list-group-item d-flex justify-content-between align-items-center">
            <h6 class="mb-0">${type.name}</h6>
            <button class="btn btn-sm btn-outline-primary" onclick="editMachineType(${type.id})">
                <i class="bi bi-pencil"></i>
            </button>
        </div>`;
    });
    html += '</div>';

    container.html(html);
}

// === ФУНКЦИИ УПРАВЛЕНИЯ ===

// Работа с деталями
function addDetail() {
    clearDetailForm();
    $('#detailModalLabel').text('Добавить деталь');
    $('#detailModal').modal('show');
}

function editDetail(id) {
    const detail = dashboardData.details.find(d => d.id === id);
    if (detail) {
        $('#detailId').val(detail.id);
        $('#detailNumber').val(detail.number);
        $('#detailName').val(detail.name);
        $('#detailModalLabel').text('Редактировать деталь');
        $('#detailModal').modal('show');
    }
}

function saveDetail() {
    const id = $('#detailId').val();
    const data = {
        id: id || 0,
        number: $('#detailNumber').val(),
        name: $('#detailName').val()
    };

    if (!data.number || !data.name) {
        showNotification('Заполните все поля', 'error');
        return;
    }

    const url = id ? '/Main/UpdateDetail' : '/Main/CreateDetail';
    const method = id ? 'PUT' : 'POST';

    $.ajax({
        url: url,
        method: method,
        data: JSON.stringify(data),
        contentType: 'application/json',
        success: function (response) {
            if (response.success) {
                $('#detailModal').modal('hide');
                loadReferenceData();
                showNotification(response.message, 'success');
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function () {
            showNotification('Ошибка при сохранении детали', 'error');
        }
    });
}

function clearDetailForm() {
    $('#detailId').val('');
    $('#detailNumber').val('');
    $('#detailName').val('');
}

// Работа с типами станков
function addMachineType() {
    clearMachineTypeForm();
    $('#machineTypeModalLabel').text('Добавить тип станка');
    $('#machineTypeModal').modal('show');
}

function editMachineType(id) {
    const type = dashboardData.machineTypes.find(t => t.id === id);
    if (type) {
        $('#machineTypeId').val(type.id);
        $('#machineTypeName').val(type.name);
        $('#machineTypeModalLabel').text('Редактировать тип станка');
        $('#machineTypeModal').modal('show');
    }
}

function saveMachineType() {
    const id = $('#machineTypeId').val();
    const data = {
        id: id || 0,
        name: $('#machineTypeName').val()
    };

    if (!data.name) {
        showNotification('Введите название типа станка', 'error');
        return;
    }

    const url = id ? '/Main/UpdateMachineType' : '/Main/CreateMachineType';
    const method = id ? 'PUT' : 'POST';

    $.ajax({
        url: url,
        method: method,
        data: JSON.stringify(data),
        contentType: 'application/json',
        success: function (response) {
            if (response.success) {
                $('#machineTypeModal').modal('hide');
                loadReferenceData();
                showNotification(response.message, 'success');
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function () {
            showNotification('Ошибка при сохранении типа станка', 'error');
        }
    });
}

function clearMachineTypeForm() {
    $('#machineTypeId').val('');
    $('#machineTypeName').val('');
}

// Работа со станками
function addMachine() {
    clearMachineForm();
    $('#machineModal').modal('show');
}

function editMachine(id) {
    const machine = dashboardData.machines.find(m => m.id === id);
    if (machine) {
        $('#machineId').val(machine.id);
        $('#machineName').val(machine.name);
        $('#machineInventoryNumber').val(machine.inventoryNumber);
        $('#machineMachineTypeId').val(machine.machineTypeId);
        $('#machinePriority').val(machine.priority);
        $('#machineModal').modal('show');
    }
}

function saveMachine() {
    const id = $('#machineId').val();
    const data = {
        id: id || 0,
        name: $('#machineName').val(),
        inventoryNumber: $('#machineInventoryNumber').val(),
        machineTypeId: parseInt($('#machineMachineTypeId').val()),
        priority: parseInt($('#machinePriority').val()) || 0
    };

    if (!data.name || !data.inventoryNumber || !data.machineTypeId) {
        showNotification('Заполните все обязательные поля', 'error');
        return;
    }

    const url = id ? '/Main/UpdateMachine' : '/Main/CreateMachine';
    const method = id ? 'PUT' : 'POST';

    $.ajax({
        url: url,
        method: method,
        data: JSON.stringify(data),
        contentType: 'application/json',
        success: function (response) {
            if (response.success) {
                $('#machineModal').modal('hide');
                loadReferenceData();
                showNotification(response.message, 'success');
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function () {
            showNotification('Ошибка при сохранении станка', 'error');
        }
    });
}

function clearMachineForm() {
    $('#machineId').val('');
    $('#machineName').val('');
    $('#machineInventoryNumber').val('');
    $('#machineMachineTypeId').val('');
    $('#machinePriority').val('0');
}

// Создание партии
function createBatch() {
    const detailId = $('#batchDetailSelect').val();
    const quantity = parseInt($('#batchQuantity').val());
    const splitBatch = $('#splitBatchCheck').is(':checked');

    if (!detailId || !quantity || quantity <= 0) {
        showNotification('Заполните все поля корректно', 'error');
        return;
    }

    const data = {
        detailId: parseInt(detailId),
        quantity: quantity,
        subBatches: []
    };

    if (splitBatch) {
        let totalSubQuantity = 0;
        $('.subBatchQuantity').each(function () {
            const qty = parseInt($(this).val()) || 0;
            if (qty > 0) {
                data.subBatches.push({ quantity: qty });
                totalSubQuantity += qty;
            }
        });

        if (totalSubQuantity !== quantity) {
            showNotification('Сумма подпартий должна равняться общему количеству', 'error');
            return;
        }
    }

    $.ajax({
        url: '/Main/CreateBatch',
        method: 'POST',
        data: JSON.stringify(data),
        contentType: 'application/json',
        success: function (response) {
            if (response.success) {
                $('#createBatchModal').modal('hide');
                refreshData();
                showNotification(response.message, 'success');
                clearBatchForm();
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function () {
            showNotification('Ошибка при создании партии', 'error');
        }
    });
}

function clearBatchForm() {
    $('#batchDetailSelect').val('');
    $('#batchQuantity').val('');
    $('#splitBatchCheck').prop('checked', false);
    $('#subBatchesContainer').addClass('d-none');
    $('#subBatchQuantities').empty();
}

// Времена переналадки
function addSetupTime() {
    const data = {
        machineId: parseInt($('#setupMachineId').val()),
        fromDetailId: parseInt($('#setupFromDetailId').val()),
        toDetailId: parseInt($('#setupToDetailId').val()),
        time: parseFloat($('#setupTime').val())
    };

    if (!data.machineId || !data.fromDetailId || !data.toDetailId || !data.time || data.time <= 0) {
        showNotification('Заполните все поля корректно', 'error');
        return;
    }

    if (data.fromDetailId === data.toDetailId) {
        showNotification('Деталь "откуда" и "куда" не могут быть одинаковыми', 'error');
        return;
    }

    $.ajax({
        url: '/Main/CreateSetupTime',
        method: 'POST',
        data: JSON.stringify(data),
        contentType: 'application/json',
        success: function (response) {
            if (response.success) {
                $('#setupTimeModal').modal('hide');
                showNotification(response.message, 'success');
                clearSetupTimeForm();
                loadSetupTimes();
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function () {
            showNotification('Ошибка при добавлении времени переналадки', 'error');
        }
    });
}

function clearSetupTimeForm() {
    $('#setupMachineId').val('');
    $('#setupFromDetailId').val('');
    $('#setupToDetailId').val('');
    $('#setupTime').val('');
}

function loadSetupTimes() {
    $.get('/Main/GetSetupTimes')
        .done(function (response) {
            if (response.success) {
                // Обновляем таблицу времен переналадки
                renderSetupTimes(response.data);
            }
        });
}

function renderSetupTimes(setupTimes) {
    // Здесь можно добавить отображение таблицы времен переналадки
    console.log('Setup times:', setupTimes);
}

// Управление этапами
function showStageControl(stageId) {
    const stage = dashboardData.ganttStages.find(s => s.id === stageId);
    if (!stage) return;

    $('#stageInfo').html(`
        <div><strong>Деталь:</strong> ${stage.detailName}</div>
        <div><strong>Этап:</strong> ${stage.stageName}</div>
        <div><strong>Станок:</strong> ${stage.machineName || 'Не назначен'}</div>
        <div><strong>Статус:</strong> ${translateStatus(stage.status)}</div>
        ${stage.startTime ? `<div><strong>Начало:</strong> ${formatDateTime(stage.startTime)}</div>` : ''}
        ${stage.endTime ? `<div><strong>Завершение:</strong> ${formatDateTime(stage.endTime)}</div>` : ''}
    `);

    let actions = '';
    switch (stage.status) {
        case 'Pending':
            actions = `<button class="btn btn-success me-2" onclick="startStage(${stageId})">Начать</button>`;
            break;
        case 'InProgress':
            actions = `
                <button class="btn btn-warning me-2" onclick="pauseStage(${stageId})">Пауза</button>
                <button class="btn btn-success" onclick="completeStage(${stageId})">Завершить</button>
            `;
            break;
        case 'Paused':
            actions = `<button class="btn btn-primary" onclick="resumeStage(${stageId})">Возобновить</button>`;
            break;
    }

    $('#stageActions').html(actions);
    $('#stageControlModal').modal('show');
}

// Действия с этапами
function startStage(stageId) {
    executeStageAction('/Main/StartStage', { stageId: stageId }, 'Этап запущен');
}

function pauseStage(stageId) {
    executeStageAction('/Main/PauseStage', { stageId: stageId }, 'Этап приостановлен');
}

function resumeStage(stageId) {
    executeStageAction('/Main/ResumeStage', { stageId: stageId }, 'Этап возобновлен');
}

function completeStage(stageId) {
    executeStageAction('/Main/CompleteStage', { stageId: stageId }, 'Этап завершен');
}

function executeStageAction(url, data, successMessage) {
    $.post(url, data)
        .done(function (response) {
            if (response.success) {
                $('#stageControlModal').modal('hide');
                refreshData();
                showNotification(successMessage, 'success');
            } else {
                showNotification(response.message, 'error');
            }
        })
        .fail(function () {
            showNotification('Ошибка при выполнении операции', 'error');
        });
}

// Заполнение выпадающих списков
function populateDetailSelects() {
    const selects = ['#batchDetailSelect', '#setupFromDetailId', '#setupToDetailId'];

    selects.forEach(selector => {
        const $select = $(selector);
        $select.empty();
        $select.append('<option value="">Выберите деталь...</option>');

        dashboardData.details.forEach(detail => {
            $select.append(`<option value="${detail.id}">${detail.name} (${detail.number})</option>`);
        });
    });
}

function populateMachineTypeSelects() {
    const $select = $('#machineMachineTypeId');
    $select.empty();
    $select.append('<option value="">Выберите тип станка...</option>');

    dashboardData.machineTypes.forEach(type => {
        $select.append(`<option value="${type.id}">${type.name}</option>`);
    });
}

function populateMachineSelects() {
    const $select = $('#setupMachineId');
    $select.empty();
    $select.append('<option value="">Выберите станок...</option>');

    dashboardData.machines.forEach(machine => {
        $select.append(`<option value="${machine.id}">${machine.name} (${machine.inventoryNumber})</option>`);
    });
}

// === ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ===

function getStatusClass(status) {
    switch (status) {
        case 'InProgress': return 'status-in-progress';
        case 'Completed': return 'status-completed';
        case 'Waiting': return 'status-waiting';
        case 'Paused': return 'status-paused';
        default: return 'status-pending';
    }
}

function translateStatus(status) {
    switch (status) {
        case 'InProgress': return 'В работе';
        case 'Completed': return 'Завершено';
        case 'Waiting': return 'В очереди';
        case 'Paused': return 'На паузе';
        case 'Pending': return 'Ожидает запуска';
        default: return status;
    }
}

function formatTime(dateStr) {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleTimeString();
}

function formatDateTime(dateStr) {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
}

function showNotification(message, type) {
    const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
    const notification = $(`<div class="alert ${alertClass} alert-dismissible fade show position-fixed" style="top: 20px; right: 20px; z-index: 9999;">
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>`);

    $('body').append(notification);

    setTimeout(() => {
        notification.alert('close');
    }, 3000);
}

// Генерация полей для подпартий
function generateSubBatchInputs() {
    const count = parseInt($('#subBatchCount').val()) || 2;
    const totalQuantity = parseInt($('#batchQuantity').val()) || 0;
    const container = $('#subBatchQuantities');

    container.empty();

    if (totalQuantity > 0) {
        const baseQuantity = Math.floor(totalQuantity / count);
        const remainder = totalQuantity % count;

        for (let i = 0; i < count; i++) {
            const quantity = i === 0 ? baseQuantity + remainder : baseQuantity;
            container.append(`
                <div class="mb-2">
                    <label class="form-label">Подпартия ${i + 1}</label>
                    <input type="number" class="form-control subBatchQuantity" min="1" value="${quantity}" required>
                </div>
            `);
        }
    }
}

// Заглушки для других функций
function showBatchDetails(batchId) {
    showNotification('Функция в разработке', 'info');
}

// Функция показа маршрута детали
function showRouteModal(detailId) {
    const detail = dashboardData.details.find(d => d.id === detailId);
    if (!detail) {
        showNotification('Деталь не найдена', 'error');
        return;
    }

    $('#routeModalLabel').text(`Маршрут детали: ${detail.name}`);
    $('#routeContent').html(`
        <div class="text-center p-4">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Загрузка...</span>
            </div>
            <p class="mt-2">Загрузка маршрута...</p>
        </div>
    `);

    // Показываем модальное окно
    $('#routeModal').modal('show');

    // Загружаем маршрут
    $.get(`/Route/GetRouteForDetail?detailId=${detailId}`)
        .done(function (route) {
            if (route && route.stages && route.stages.length > 0) {
                let stagesHtml = `
                    <div class="table-responsive">
                        <table class="table table-hover">
                            <thead class="table-light">
                                <tr>
                                    <th width="10%">№</th>
                                    <th width="30%">Операция</th>
                                    <th width="25%">Тип станка</th>
                                    <th width="15%">Время на ед. (ч)</th>
                                    <th width="20%">Время переналадки (ч)</th>
                                </tr>
                            </thead>
                            <tbody>
                `;

                route.stages.forEach(stage => {
                    stagesHtml += `
                        <tr>
                            <td><span class="badge bg-primary">${stage.order}</span></td>
                            <td>
                                <strong>${stage.name}</strong>
                                <br><small class="text-muted">${stage.stageType || 'Основная операция'}</small>
                            </td>
                            <td>
                                <i class="bi bi-tools text-secondary me-1"></i>
                                ${stage.machineTypeName}
                            </td>
                            <td>
                                <span class="badge bg-info">${stage.normTime}</span>
                            </td>
                            <td>
                                <span class="badge bg-warning text-dark">${stage.setupTime}</span>
                            </td>
                        </tr>
                    `;
                });

                stagesHtml += `
                            </tbody>
                        </table>
                    </div>
                    <div class="mt-3 p-3 bg-light rounded">
                        <div class="row">
                            <div class="col-md-6">
                                <strong>Общее время изготовления 1 детали:</strong>
                                <span class="text-primary ms-2">${route.stages.reduce((sum, s) => sum + s.normTime, 0).toFixed(2)} ч</span>
                            </div>
                            <div class="col-md-6">
                                <strong>Общее время переналадки:</strong>
                                <span class="text-warning ms-2">${route.stages.reduce((sum, s) => sum + s.setupTime, 0).toFixed(2)} ч</span>
                            </div>
                        </div>
                    </div>
                `;

                $('#routeContent').html(stagesHtml);
                $('#editRouteBtn').show().off('click').on('click', function () {
                    window.location.href = `/Route/Edit/${route.id}`;
                });
            } else {
                $('#routeContent').html(`
                    <div class="text-center p-5">
                        <i class="bi bi-diagram-3" style="font-size: 3rem; color: #6c757d;"></i>
                        <h4 class="mt-3">Маршрут не создан</h4>
                        <p class="text-muted">Для этой детали ещё не создан маршрут изготовления.</p>
                        <button class="btn btn-primary" onclick="createRouteForDetail(${detailId})">
                            <i class="bi bi-plus"></i> Создать маршрут
                        </button>
                    </div>
                `);
                $('#editRouteBtn').hide();
            }
        })
        .fail(function () {
            $('#routeContent').html(`
                <div class="alert alert-danger">
                    <i class="bi bi-exclamation-triangle"></i>
                    Ошибка при загрузке маршрута
                </div>
            `);
            $('#editRouteBtn').hide();
        });
}

// Функция создания маршрута для детали
function createRouteForDetail(detailId) {
    $('#routeModal').modal('hide');
    window.location.href = `/Route/Create?detailId=${detailId}`;
}

// Обновленная функция показа деталей партии
function showBatchDetails(batchId) {
    window.location.href = `/Batch/Details/${batchId}`;
}

// Функция приоритизации этапа
function prioritizeStage(stageId, machineId) {
    if (!confirm('Вы уверены, что хотите приоритизировать этот этап? Текущий этап на станке будет приостановлен.')) {
        return;
    }

    $.post('/Main/PrioritizeStage', { stageId: stageId, machineId: machineId })
        .done(function (response) {
            if (response.success) {
                refreshData();
                showNotification('Этап успешно приоритизирован', 'success');
            } else {
                showNotification(response.message || 'Ошибка при приоритизации этапа', 'error');
            }
        })
        .fail(function () {
            showNotification('Ошибка при приоритизации этапа', 'error');
        });
}

// Функция показа модального окна переназначения этапа
function showReassignModal(stageId) {
    const stage = dashboardData.ganttStages.find(s => s.id === stageId);
    if (!stage) {
        showNotification('Этап не найден', 'error');
        return;
    }

    // Создаем модальное окно переназначения если его нет
    if (!$('#reassignStageModal').length) {
        const modalHtml = `
            <div class="modal fade" id="reassignStageModal" tabindex="-1" aria-labelledby="reassignStageModalLabel" aria-hidden="true">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="reassignStageModalLabel">Переназначить этап</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div id="reassignStageInfo" class="mb-3"></div>
                            <div class="mb-3">
                                <label for="reassignMachineSelect" class="form-label">Выберите станок</label>
                                <select class="form-select" id="reassignMachineSelect" required>
                                    <option value="">Загрузка станков...</option>
                                </select>
                            </div>
                            <div id="reassignMachineInfo" class="alert alert-info d-none">
                                <h6>Информация о станке:</h6>
                                <div id="machineStatus"></div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Отмена</button>
                            <button type="button" class="btn btn-primary" id="confirmReassignBtn">Переназначить</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('body').append(modalHtml);
    }

    // Заполняем информацию об этапе
    $('#reassignStageInfo').html(`
        <div class="card">
            <div class="card-body">
                <h6 class="card-title">${stage.stageName}</h6>
                <p class="card-text">
                    <strong>Деталь:</strong> ${stage.detailName}<br>
                    <strong>Текущий станок:</strong> ${stage.machineName || 'Не назначен'}<br>
                    <strong>Статус:</strong> <span class="badge ${getStatusBadgeClass(stage.status)}">${translateStatus(stage.status)}</span><br>
                    <strong>Тип:</strong> ${stage.isSetup ? 'Переналадка' : 'Основная операция'}
                </p>
            </div>
        </div>
    `);

    // Загружаем подходящие станки
    loadAvailableMachinesForStage(stageId);

    // Обработчик подтверждения переназначения
    $('#confirmReassignBtn').off('click').on('click', function () {
        const newMachineId = $('#reassignMachineSelect').val();
        if (!newMachineId) {
            showNotification('Выберите станок', 'error');
            return;
        }

        reassignStageToMachine(stageId, parseInt(newMachineId));
    });

    // Обработчик изменения выбора станка
    $('#reassignMachineSelect').off('change').on('change', function () {
        const machineId = $(this).val();
        if (machineId) {
            showMachineInfo(parseInt(machineId));
        } else {
            $('#reassignMachineInfo').addClass('d-none');
        }
    });

    $('#reassignStageModal').modal('show');
}

// Загрузка доступных станков для этапа
function loadAvailableMachinesForStage(stageId) {
    $.get(`/api/gantt/machines/available/${stageId}`)
        .done(function (machines) {
            const select = $('#reassignMachineSelect');
            select.empty();
            select.append('<option value="">Выберите станок...</option>');

            machines.forEach(machine => {
                const currentStage = dashboardData.ganttStages.find(s =>
                    s.machineId === machine.id && s.status === 'InProgress'
                );

                const status = currentStage ?
                    (currentStage.isSetup ? ' (переналадка)' : ' (в работе)') :
                    ' (свободен)';

                select.append(`<option value="${machine.id}">${machine.name} - ${machine.machineTypeName}${status}</option>`);
            });
        })
        .fail(function () {
            $('#reassignMachineSelect').html('<option value="">Ошибка загрузки станков</option>');
        });
}

// Показ информации о станке
function showMachineInfo(machineId) {
    const machine = dashboardData.machines.find(m => m.id === machineId);
    const currentStage = dashboardData.ganttStages.find(s =>
        s.machineId === machineId && s.status === 'InProgress'
    );

    let statusHtml = '';
    if (currentStage) {
        statusHtml = `
            <div class="alert alert-warning">
                <strong>Внимание!</strong> На данном станке выполняется этап:<br>
                <strong>${currentStage.stageName}</strong> (${currentStage.detailName})
                ${currentStage.isSetup ? '<br><span class="badge bg-info">Переналадка</span>' : ''}
            </div>
        `;
    } else {
        statusHtml = '<div class="alert alert-success">Станок свободен</div>';
    }

    if (machine) {
        statusHtml += `
            <p><strong>Инвентарный номер:</strong> ${machine.inventoryNumber}</p>
            <p><strong>Приоритет:</strong> ${machine.priority}</p>
        `;
    }

    $('#machineStatus').html(statusHtml);
    $('#reassignMachineInfo').removeClass('d-none');
}

// Переназначение этапа на станок
function reassignStageToMachine(stageId, machineId) {
    $.post('/Main/ReassignStage', { stageId: stageId, machineId: machineId })
        .done(function (response) {
            if (response.success) {
                $('#reassignStageModal').modal('hide');
                refreshData();
                showNotification('Этап успешно переназначен', 'success');
            } else {
                showNotification(response.message || 'Ошибка при переназначении этапа', 'error');
            }
        })
        .fail(function () {
            showNotification('Ошибка при переназначении этапа', 'error');
        });
}

// Функция показа рабочего места станка
function showMachineWorkspace(machineId) {
    window.location.href = `/OperatorWorkspace?machineId=${machineId}`;
}

// Функция получения класса бейджа статуса
function getStatusBadgeClass(status) {
    switch (status) {
        case 'Pending': return 'bg-light text-dark';
        case 'Waiting': return 'bg-warning text-dark';
        case 'InProgress': return 'bg-primary';
        case 'Paused': return 'bg-secondary';
        case 'Completed': return 'bg-success';
        case 'Error': return 'bg-danger';
        default: return 'bg-light text-dark';
    }
}