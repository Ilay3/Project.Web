// Диаграмма Ганта
class GanttChart {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.data = [];
        this.machines = [];
        this.timeScale = 'hours'; // hours, days
        this.viewStart = moment().subtract(2, 'hours');
        this.viewEnd = moment().add(10, 'hours');
    }

    setData(stages, machines) {
        this.data = stages || [];
        this.machines = machines || [];
        this.render();
    }

    render() {
        if (!this.container) return;

        this.container.innerHTML = '';

        if (this.data.length === 0) {
            this.container.innerHTML = `
                <div class="text-center p-5">
                    <i class="bi bi-calendar3" style="font-size: 3rem; color: #6c757d;"></i>
                    <h4 class="mt-3">Нет активных этапов</h4>
                    <p class="text-muted">В настоящее время нет этапов в производстве</p>
                </div>
            `;
            return;
        }

        // Создаем структуру диаграммы
        const ganttHtml = `
            <div class="gantt-header">
                ${this.renderTimeHeader()}
            </div>
            <div class="gantt-body">
                ${this.renderMachineRows()}
            </div>
        `;

        this.container.innerHTML = ganttHtml;
        this.attachEventHandlers();
    }

    renderTimeHeader() {
        const headerHtml = [];
        const current = this.viewStart.clone();
        const hourWidth = 100; // ширина часа в пикселях

        headerHtml.push('<div class="gantt-time-header d-flex" style="margin-left: 200px;">');

        while (current.isBefore(this.viewEnd)) {
            const isNow = current.isSame(moment(), 'hour');
            const className = isNow ? 'gantt-time-cell current-time' : 'gantt-time-cell';

            headerHtml.push(`
                <div class="${className}" style="min-width: ${hourWidth}px; border-right: 1px solid #dee2e6; padding: 10px; text-align: center; background: ${isNow ? '#fff3cd' : '#f8f9fa'};">
                    <div style="font-weight: 600;">${current.format('HH:00')}</div>
                    <div style="font-size: 0.8em; color: #6c757d;">${current.format('DD.MM')}</div>
                </div>
            `);
            current.add(1, 'hour');
        }

        headerHtml.push('</div>');
        return headerHtml.join('');
    }

    renderMachineRows() {
        const machineGroups = this.groupStagesByMachine();
        const rowsHtml = [];

        // Добавляем строку для неназначенных этапов
        if (machineGroups['unassigned'] && machineGroups['unassigned'].length > 0) {
            rowsHtml.push(this.renderMachineRow('Не назначен', machineGroups['unassigned'], null));
        }

        // Добавляем строки для каждого станка
        this.machines.forEach(machine => {
            const stages = machineGroups[machine.id] || [];
            rowsHtml.push(this.renderMachineRow(machine.name, stages, machine));
        });

        return rowsHtml.join('');
    }

    groupStagesByMachine() {
        const groups = {};

        this.data.forEach(stage => {
            const key = stage.machineId || 'unassigned';
            if (!groups[key]) {
                groups[key] = [];
            }
            groups[key].push(stage);
        });

        // Сортируем этапы в каждой группе по времени начала
        Object.keys(groups).forEach(key => {
            groups[key].sort((a, b) => {
                const timeA = a.startTime ? moment(a.startTime) : moment();
                const timeB = b.startTime ? moment(b.startTime) : moment();
                return timeA.diff(timeB);
            });
        });

        return groups;
    }

    renderMachineRow(machineName, stages, machine) {
        const currentStage = stages.find(s => s.status === 'InProgress');
        const machineStatus = currentStage ?
            (currentStage.isSetup ? 'Переналадка' : 'В работе') :
            'Свободен';

        const statusClass = currentStage ?
            (currentStage.isSetup ? 'text-info' : 'text-primary') :
            'text-success';

        const rowHtml = `
            <div class="gantt-row d-flex border-bottom">
                <div class="gantt-machine-header d-flex flex-column justify-content-center" style="width: 200px; padding: 15px; background: #f8f9fa; border-right: 1px solid #dee2e6;">
                    <div style="font-weight: 600; font-size: 0.9em;">${machineName}</div>
                    ${machine ? `<div style="font-size: 0.8em; color: #6c757d;">${machine.inventoryNumber || ''}</div>` : ''}
                    <div class="mt-1">
                        <span class="badge ${statusClass.replace('text-', 'bg-')} bg-opacity-25 ${statusClass}">${machineStatus}</span>
                    </div>
                </div>
                <div class="gantt-stages-container position-relative" style="flex: 1; min-height: 80px; padding: 10px 0;">
                    ${this.renderStagesTimeline(stages)}
                </div>
            </div>
        `;

        return rowHtml;
    }

    renderStagesTimeline(stages) {
        const stageElements = [];
        const hourWidth = 100;
        const totalWidth = this.viewEnd.diff(this.viewStart, 'hours') * hourWidth;

        stages.forEach(stage => {
            const stageElement = this.renderStageElement(stage, hourWidth);
            if (stageElement) {
                stageElements.push(stageElement);
            }
        });

        return `
            <div style="position: relative; width: ${totalWidth}px; height: 60px;">
                ${stageElements.join('')}
            </div>
        `;
    }

    renderStageElement(stage, hourWidth) {
        const stageStart = stage.startTime ? moment(stage.startTime) : moment();
        const stageEnd = stage.endTime ? moment(stage.endTime) :
            (stage.startTime ? moment(stage.startTime).add(stage.plannedDuration || 3600000, 'milliseconds') : moment().add(1, 'hour'));

        // Проверяем, попадает ли этап в видимую область
        if (stageEnd.isBefore(this.viewStart) || stageStart.isAfter(this.viewEnd)) {
            return null;
        }

        // Ограничиваем границы видимой областью
        const displayStart = stageStart.isBefore(this.viewStart) ? this.viewStart : stageStart;
        const displayEnd = stageEnd.isAfter(this.viewEnd) ? this.viewEnd : stageEnd;

        // Вычисляем позицию и размер
        const leftOffset = displayStart.diff(this.viewStart, 'minutes') * (hourWidth / 60);
        const width = Math.max(displayEnd.diff(displayStart, 'minutes') * (hourWidth / 60), 60);

        // Определяем стили в зависимости от статуса
        const statusStyles = this.getStageStatusStyles(stage);

        const stageHtml = `
            <div class="gantt-stage position-absolute cursor-pointer" 
                 data-stage-id="${stage.id}"
                 style="left: ${leftOffset}px; width: ${width}px; top: 5px; height: 50px; ${statusStyles.style}"
                 onclick="showStageDetails(${stage.id})"
                 title="${stage.detailName} - ${stage.stageName}">
                <div class="p-2 h-100 d-flex flex-column justify-content-center">
                    <div style="font-size: 0.8em; font-weight: 600; line-height: 1.1; margin-bottom: 2px;">
                        ${stage.isSetup ? '🔧 ' : ''}${this.truncateText(stage.stageName, 15)}
                    </div>
                    <div style="font-size: 0.7em; opacity: 0.8; line-height: 1.1;">
                        ${this.truncateText(stage.detailName, 18)}
                    </div>
                    ${stage.startTime ? `<div style="font-size: 0.65em; opacity: 0.7; margin-top: 2px;">${moment(stage.startTime).format('HH:mm')}</div>` : ''}
                </div>
                <div class="gantt-stage-status-indicator position-absolute" 
                     style="top: 0; right: 0; width: 4px; height: 100%; background: ${statusStyles.indicator};"></div>
            </div>
        `;

        return stageHtml;
    }

    getStageStatusStyles(stage) {
        const styles = {
            'Pending': {
                style: 'background: linear-gradient(135deg, #e9ecef 0%, #f8f9fa 100%); color: #495057; border: 1px solid #ced4da; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);',
                indicator: '#6c757d'
            },
            'Waiting': {
                style: 'background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%); color: #856404; border: 1px solid #ffeaa7; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);',
                indicator: '#ffc107'
            },
            'InProgress': {
                style: 'background: linear-gradient(135deg, #d1ecf1 0%, #bee5eb 100%); color: #0c5460; border: 1px solid #bee5eb; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.15);',
                indicator: '#17a2b8'
            },
            'Paused': {
                style: 'background: linear-gradient(135deg, #d6d8db 0%, #c3c4c7 100%); color: #383d41; border: 1px solid #c3c4c7; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);',
                indicator: '#6c757d'
            },
            'Completed': {
                style: 'background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%); color: #155724; border: 1px solid #c3e6cb; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);',
                indicator: '#28a745'
            }
        };

        return styles[stage.status] || styles['Pending'];
    }

    truncateText(text, maxLength) {
        if (!text) return '';
        return text.length > maxLength ? text.substring(0, maxLength) + '...' : text;
    }

    attachEventHandlers() {
        // Обработчики событий для интерактивности
        const stageElements = this.container.querySelectorAll('.gantt-stage');
        stageElements.forEach(element => {
            element.addEventListener('mouseenter', (e) => {
                e.target.style.transform = 'translateY(-2px)';
                e.target.style.boxShadow = '0 4px 8px rgba(0,0,0,0.2)';
                e.target.style.zIndex = '10';
            });

            element.addEventListener('mouseleave', (e) => {
                e.target.style.transform = 'translateY(0)';
                e.target.style.boxShadow = '0 1px 3px rgba(0,0,0,0.1)';
                e.target.style.zIndex = '1';
            });
        });
    }

    // Методы для управления временным масштабом
    zoomIn() {
        const currentRange = this.viewEnd.diff(this.viewStart, 'hours');
        if (currentRange > 4) {
            const center = moment(this.viewStart).add(currentRange / 2, 'hours');
            const newRange = Math.max(4, currentRange * 0.7);
            this.viewStart = moment(center).subtract(newRange / 2, 'hours');
            this.viewEnd = moment(center).add(newRange / 2, 'hours');
            this.render();
        }
    }

    zoomOut() {
        const currentRange = this.viewEnd.diff(this.viewStart, 'hours');
        if (currentRange < 48) {
            const center = moment(this.viewStart).add(currentRange / 2, 'hours');
            const newRange = Math.min(48, currentRange * 1.3);
            this.viewStart = moment(center).subtract(newRange / 2, 'hours');
            this.viewEnd = moment(center).add(newRange / 2, 'hours');
            this.render();
        }
    }

    moveLeft() {
        const shift = this.viewEnd.diff(this.viewStart, 'hours') * 0.3;
        this.viewStart.subtract(shift, 'hours');
        this.viewEnd.subtract(shift, 'hours');
        this.render();
    }

    moveRight() {
        const shift = this.viewEnd.diff(this.viewStart, 'hours') * 0.3;
        this.viewStart.add(shift, 'hours');
        this.viewEnd.add(shift, 'hours');
        this.render();
    }

    resetView() {
        this.viewStart = moment().subtract(2, 'hours');
        this.viewEnd = moment().add(10, 'hours');
        this.render();
    }
}

// Глобальный экземпляр диаграммы Ганта
let ganttChart;

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    ganttChart = new GanttChart('gantt-container');

    // Добавляем кнопки управления
    addGanttControls();
});

function addGanttControls() {
    const ganttPane = document.getElementById('gantt-pane');
    if (!ganttPane) return;

    const controlsHtml = `
        <div class="gantt-controls mb-3 d-flex justify-content-between align-items-center">
            <div class="btn-group" role="group">
                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="ganttChart.zoomIn()" title="Увеличить">
                    <i class="bi bi-zoom-in"></i>
                </button>
                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="ganttChart.zoomOut()" title="Уменьшить">
                    <i class="bi bi-zoom-out"></i>
                </button>
                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="ganttChart.resetView()" title="Сбросить">
                    <i class="bi bi-house"></i>
                </button>
            </div>
            <div class="btn-group" role="group">
                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="ganttChart.moveLeft()" title="Влево">
                    <i class="bi bi-arrow-left"></i>
                </button>
                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="ganttChart.moveRight()" title="Вправо">
                    <i class="bi bi-arrow-right"></i>
                </button>
            </div>
            <div class="text-muted small">
                <i class="bi bi-info-circle"></i> Нажмите на этап для управления
            </div>
        </div>
    `;

    ganttPane.insertAdjacentHTML('afterbegin', controlsHtml);
}

// Функции для показа деталей этапа
function showStageDetails(stageId) {
    const stage = window.dashboardData.ganttStages.find(s => s.id === stageId);
    if (!stage) {
        console.error('Stage not found:', stageId);
        return;
    }

    // Заполняем модальное окно информацией об этапе
    const modalTitle = document.querySelector('#stageControlModal .modal-title');
    const stageInfo = document.getElementById('stageInfo');
    const stageActions = document.getElementById('stageActions');

    if (modalTitle) modalTitle.textContent = `Этап: ${stage.stageName}`;

    if (stageInfo) {
        stageInfo.innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <p><strong>Деталь:</strong> ${stage.detailName}</p>
                    <p><strong>Этап:</strong> ${stage.stageName}</p>
                    <p><strong>Станок:</strong> ${stage.machineName || 'Не назначен'}</p>
                    <p><strong>Тип:</strong> ${stage.isSetup ? 'Переналадка' : 'Основная операция'}</p>
                </div>
                <div class="col-md-6">
                    <p><strong>Статус:</strong> <span class="badge ${getStatusBadgeClass(stage.status)}">${translateStatus(stage.status)}</span></p>
                    ${stage.startTime ? `<p><strong>Начало:</strong> ${moment(stage.startTime).format('DD.MM.YYYY HH:mm')}</p>` : ''}
                    ${stage.endTime ? `<p><strong>Завершение:</strong> ${moment(stage.endTime).format('DD.MM.YYYY HH:mm')}</p>` : ''}
                    ${stage.scheduledStartTime ? `<p><strong>Запланировано:</strong> ${moment(stage.scheduledStartTime).format('DD.MM.YYYY HH:mm')}</p>` : ''}
                </div>
            </div>
        `;
    }

    if (stageActions) {
        stageActions.innerHTML = getStageActionButtons(stage);
    }

    // Показываем модальное окно
    const modal = new bootstrap.Modal(document.getElementById('stageControlModal'));
    modal.show();
}

function getStatusBadgeClass(status) {
    const classes = {
        'Pending': 'bg-light text-dark',
        'Waiting': 'bg-warning text-dark',
        'InProgress': 'bg-primary',
        'Paused': 'bg-secondary',
        'Completed': 'bg-success'
    };
    return classes[status] || 'bg-light text-dark';
}

function translateStatus(status) {
    const translations = {
        'Pending': 'Ожидает запуска',
        'Waiting': 'В очереди',
        'InProgress': 'В работе',
        'Paused': 'На паузе',
        'Completed': 'Завершено'
    };
    return translations[status] || status;
}

function getStageActionButtons(stage) {
    let buttons = '';

    switch (stage.status) {
        case 'Pending':
            buttons = `
                <button class="btn btn-success me-2" onclick="executeStageAction('start', ${stage.id})">
                    <i class="bi bi-play-fill"></i> Начать
                </button>
                <button class="btn btn-outline-secondary" onclick="showReassignModal(${stage.id})">
                    <i class="bi bi-arrow-left-right"></i> Переназначить
                </button>
            `;
            break;
        case 'InProgress':
            buttons = `
                <button class="btn btn-warning me-2" onclick="executeStageAction('pause', ${stage.id})">
                    <i class="bi bi-pause-fill"></i> Пауза
                </button>
                <button class="btn btn-success" onclick="executeStageAction('complete', ${stage.id})">
                    <i class="bi bi-check-lg"></i> Завершить
                </button>
            `;
            break;
        case 'Paused':
            buttons = `
                <button class="btn btn-primary me-2" onclick="executeStageAction('resume', ${stage.id})">
                    <i class="bi bi-play-fill"></i> Возобновить
                </button>
                <button class="btn btn-outline-secondary" onclick="showReassignModal(${stage.id})">
                    <i class="bi bi-arrow-left-right"></i> Переназначить
                </button>
            `;
            break;
        case 'Waiting':
            buttons = `
                <button class="btn btn-primary me-2" onclick="executeStageAction('prioritize', ${stage.id})">
                    <i class="bi bi-arrow-up-circle"></i> Приоритет
                </button>
                <button class="btn btn-outline-secondary" onclick="showReassignModal(${stage.id})">
                    <i class="bi bi-arrow-left-right"></i> Переназначить
                </button>
            `;
            break;
    }

    return buttons;
}

function executeStageAction(action, stageId) {
    const urls = {
        'start': '/Main/StartStage',
        'pause': '/Main/PauseStage',
        'resume': '/Main/ResumeStage',
        'complete': '/Main/CompleteStage',
        'prioritize': '/Main/PrioritizeStage'
    };

    const url = urls[action];
    if (!url) {
        console.error('Unknown action:', action);
        return;
    }

    fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
        },
        body: JSON.stringify({ stageId: stageId })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Закрываем модальное окно
                const modal = bootstrap.Modal.getInstance(document.getElementById('stageControlModal'));
                if (modal) modal.hide();

                // Обновляем данные
                if (typeof refreshData === 'function') {
                    refreshData();
                }

                // Показываем уведомление
                showNotification(data.message || 'Операция выполнена успешно', 'success');
            } else {
                showNotification(data.message || 'Произошла ошибка', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Произошла ошибка при выполнении операции', 'error');
        });
}