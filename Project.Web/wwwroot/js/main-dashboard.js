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
    initializeDashboard();

    // Автоматическое обновление каждые 30 секунд
    setInterval(refreshData, 30000);
});

// Инициализация главной панели
function initializeDashboard() {
    loadInitialData();
    setupEventHandlers();
}

// Загрузка начальных данных
function loadInitialData() {
    // Получаем данные с сервера (они уже переданы в модели)
    dashboardData.details = @Html.Raw(Json.Serialize(Model.Details));
    dashboardData.machineTypes = @Html.Raw(Json.Serialize(Model.MachineTypes));
    dashboardData.machines = @Html.Raw(Json.Serialize(Model.Machines));
    dashboardData.batches = @Html.Raw(Json.Serialize(Model.Batches));
    dashboardData.ganttStages = @Html.Raw(Json.Serialize(Model.GanttStages));
    dashboardData.queueItems = @Html.Raw(Json.Serialize(Model.QueueItems));

    // Рендерим компоненты
    renderGanttChart();
    renderQueue();
    renderBatches();
    renderMachines();
    renderDetails();
    renderMachineTypes();
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
}

// Обновление данных с сервера
function refreshData() {
    $.get('/Main/RefreshData')
        .done(function (response) {
            if (response.success) {
                dashboardData.ganttStages = response.ganttStages;
                dashboardData.queueItems = response.queueItems;
                dashboardData.batches = response.batches;

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
}

// Отображение диаграммы Ганта
function renderGanttChart() {
    const container = $('#gantt-container');
    container.empty();

    if (dashboardData.ganttStages.length === 0) {
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

    if (dashboardData.queueItems.length === 0) {
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

    dashboardData.batches.forEach(batch => {
        const totalStages = batch.subBatches.reduce((sum, sb) => sum + (sb.stageExecutions?.length || 0), 0);
        const completedStages = batch.subBatches.reduce((sum, sb) => {
            return sum + (sb.stageExecutions?.filter(se => se.status === 'Completed').length || 0);
        }, 0);

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

    let html = '<div class="row">';
    dashboardData.machines.forEach(machine => {
        const currentStage = dashboardData.ganttStages.find(s => s.machineId === machine.id && s.status === 'InProgress');
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

// === МОДАЛЬНЫЕ ОКНА И ДЕЙСТВИЯ ===

// Сохранение детали
function saveDetail() {
    const id = $('#detailId').val();
    const data = {
        id: id || 0,
        number: $('#detailNumber').val(),
        name: $('#detailName').val()
    };

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
                refreshData();
                showNotification(response.message, 'success');
            } else {
                showNotification(response.message, 'error');
            }
        }
    });
}

// Создание партии
function createBatch() {
    const detailId = $('#batchDetailSelect').val();
    const quantity = parseInt($('#batchQuantity').val());
    const splitBatch = $('#splitBatchCheck').is(':checked');

    const data = {
        detailId: parseInt(detailId),
        quantity: quantity,
        subBatches: []
    };

    if (splitBatch) {
        $('.subBatchQuantity').each(function () {
            const qty = parseInt($(this).val());
            if (qty > 0) {
                data.subBatches.push({ quantity: qty });
            }
        });
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
            } else {
                showNotification(response.message, 'error');
            }
        }
    });
}

// Управление этапом
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