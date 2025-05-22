// Создать файл Project.Web/wwwroot/js/enhanced-gantt-chart.js

// Расширенная диаграмма Ганта с интерактивностью
class EnhancedGanttChart {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.data = [];
        this.machines = [];
        this.selectedStage = null;
        this.viewStart = moment().subtract(2, 'hours');
        this.viewEnd = moment().add(12, 'hours');
        this.hourWidth = 120; // ширина часа в пикселях

        this.setupEventListeners();
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
            this.renderEmptyState();
            return;
        }

        const ganttWrapper = document.createElement('div');
        ganttWrapper.className = 'gantt-wrapper';
        ganttWrapper.innerHTML = this.buildGanttHTML();

        this.container.appendChild(ganttWrapper);

        // Добавляем контролы
        this.addControls();

        // Добавляем обработчики событий
        this.attachEventHandlers();

        // Прокручиваем к текущему времени
        this.scrollToCurrentTime();
    }

    renderEmptyState() {
        this.container.innerHTML = `
            <div class="gantt-empty-state text-center p-5">
                <div class="empty-icon mb-3">
                    <i class="bi bi-calendar3" style="font-size: 4rem; color: #6c757d;"></i>
                </div>
                <h3 class="text-muted">Нет активных этапов</h3>
                <p class="text-muted mb-4">В настоящее время нет этапов в производстве</p>
                <button class="btn btn-primary" onclick="refreshGanttData()">
                    <i class="bi bi-arrow-clockwise"></i> Обновить данные
                </button>
            </div>
        `;
    }

    buildGanttHTML() {
        const timelineWidth = this.calculateTimelineWidth();

        return `
            <div class="gantt-controls-bar">
                ${this.buildControlsHTML()}
            </div>
            <div class="gantt-content">
                <div class="gantt-header">
                    ${this.buildTimeHeaderHTML(timelineWidth)}
                </div>
                <div class="gantt-body">
                    ${this.buildMachineRowsHTML(timelineWidth)}
                </div>
            </div>
        `;
    }

    buildControlsHTML() {
        return `
            <div class="d-flex justify-content-between align-items-center p-3 bg-light border-bottom">
                <div class="gantt-zoom-controls">
                    <div class="btn-group btn-group-sm" role="group">
                        <button type="button" class="btn btn-outline-secondary" onclick="ganttChart.zoomOut()" title="Уменьшить масштаб">
                            <i class="bi bi-zoom-out"></i>
                        </button>
                        <button type="button" class="btn btn-outline-secondary" onclick="ganttChart.resetZoom()" title="Сбросить масштаб">
                            <i class="bi bi-arrows-angle-expand"></i>
                        </button>
                        <button type="button" class="btn btn-outline-secondary" onclick="ganttChart.zoomIn()" title="Увеличить масштаб">
                            <i class="bi bi-zoom-in"></i>
                        </button>
                    </div>
                </div>
                <div class="gantt-time-info">
                    <span class="badge bg-primary me-2">
                        <i class="bi bi-clock"></i> ${this.viewStart.format('HH:mm')} - ${this.viewEnd.format('HH:mm')}
                    </span>
                    <span class="badge bg-info">
                        <i class="bi bi-calendar-date"></i> ${moment().format('DD.MM.YYYY')}
                    </span>
                </div>
                <div class="gantt-navigation-controls">
                    <div class="btn-group btn-group-sm" role="group">
                        <button type="button" class="btn btn-outline-secondary" onclick="ganttChart.moveToPrevious()" title="Предыдущий период">
                            <i class="bi bi-chevron-left"></i>
                        </button>
                        <button type="button" class="btn btn-outline-secondary" onclick="ganttChart.moveToNow()" title="Текущее время">
                            <i class="bi bi-house"></i>
                        </button>
                        <button type="button" class="btn btn-outline-secondary" onclick="ganttChart.moveToNext()" title="Следующий период">
                            <i class="bi bi-chevron-right"></i>
                        </button>
                    </div>
                </div>
            </div>
        `;
    }

    buildTimeHeaderHTML(timelineWidth) {
        const headerHtml = ['<div class="gantt-time-header d-flex">'];

        // Заголовок для колонки с названиями станков
        headerHtml.push('<div class="gantt-machine-header-placeholder" style="width: 250px; min-width: 250px;"></div>');

        // Временная шкала
        headerHtml.push(`<div class="gantt-timeline-header" style="width: ${timelineWidth}px;">`);
        headerHtml.push(this.buildTimeScaleHTML());
        headerHtml.push('</div>');

        headerHtml.push('</div>');
        return headerHtml.join('');
    }

    buildTimeScaleHTML() {
        const scaleHtml = [];
        const current = this.viewStart.clone();

        while (current.isBefore(this.viewEnd)) {
            const isNow = current.isSame(moment(), 'hour');
            const isWorkingHour = current.hour() >= 8 && current.hour() < 18;

            scaleHtml.push(`
                <div class="gantt-time-cell ${isNow ? 'current-time' : ''} ${isWorkingHour ? 'working-hour' : 'non-working-hour'}" 
                     style="width: ${this.hourWidth}px;">
                    <div class="time-label">
                        <div class="hour">${current.format('HH:mm')}</div>
                        <div class="date">${current.format('DD.MM')}</div>
                    </div>
                </div>
            `);

            current.add(1, 'hour');
        }

        return scaleHtml.join('');
    }

    buildMachineRowsHTML(timelineWidth) {
        const machineGroups = this.groupStagesByMachine();
        const rowsHtml = [];

        // Строка для неназначенных этапов
        if (machineGroups['unassigned'] && machineGroups['unassigned'].length > 0) {
            rowsHtml.push(this.buildMachineRowHTML('Не назначен', machineGroups['unassigned'], null, timelineWidth));
        }

        // Строки для станков
        this.machines.forEach(machine => {
            const stages = machineGroups[machine.id] || [];
            rowsHtml.push(this.buildMachineRowHTML(machine.name, stages, machine, timelineWidth));
        });

        return rowsHtml.join('');
    }

    buildMachineRowHTML(machineName, stages, machine, timelineWidth) {
        const currentStage = stages.find(s => s.status === 'InProgress');
        const queuedStages = stages.filter(s => s.status === 'Waiting' || s.status === 'Pending');

        const machineStatus = this.getMachineStatusInfo(currentStage, queuedStages.length);

        return `
            <div class="gantt-machine-row" data-machine-id="${machine?.id || 'unassigned'}">
                <div class="gantt-machine-info" style="width: 250px; min-width: 250px;">
                    <div class="machine-header">
                        <div class="machine-name">
                            <i class="bi bi-tools me-2"></i>
                            ${machineName}
                        </div>
                        ${machine ? `
                            <div class="machine-details">
                                <small class="text-muted">${machine.inventoryNumber || ''}</small>
                                <span class="badge ${machineStatus.badgeClass} ms-2">${machineStatus.text}</span>
                            </div>
                        ` : ''}
                    </div>
                    ${queuedStages.length > 0 ? `
                        <div class="machine-queue">
                            <small class="text-info">
                                <i class="bi bi-clock-history"></i> В очереди: ${queuedStages.length}
                            </small>
                        </div>
                    ` : ''}
                </div>
                <div class="gantt-machine-timeline" style="width: ${timelineWidth}px; position: relative;">
                    ${this.buildStageElementsHTML(stages)}
                    ${this.buildCurrentTimeLineHTML()}
                </div>
            </div>
        `;
    }

    getMachineStatusInfo(currentStage, queueCount) {
        if (currentStage) {
            return {
                text: currentStage.isSetup ? 'Переналадка' : 'В работе',
                badgeClass: currentStage.isSetup ? 'bg-info' : 'bg-success'
            };
        } else if (queueCount > 0) {
            return {
                text: 'Ожидает',
                badgeClass: 'bg-warning text-dark'
            };
        } else {
            return {
                text: 'Свободен',
                badgeClass: 'bg-secondary'
            };
        }
    }

    buildStageElementsHTML(stages) {
        return stages.map(stage => this.buildStageElementHTML(stage)).join('');
    }

    buildStageElementHTML(stage) {
        const stageStart = stage.startTime ? moment(stage.startTime) : moment();
        const stageEnd = this.calculateStageEndTime(stage);

        // Проверяем видимость этапа
        if (stageEnd.isBefore(this.viewStart) || stageStart.isAfter(this.viewEnd)) {
            return '';
        }

        // Вычисляем позицию и размер
        const position = this.calculateStagePosition(stageStart, stageEnd);
        const stageStyles = this.getStageStyles(stage);

        return `
            <div class="gantt-stage ${stage.status.toLowerCase()} ${stage.isSetup ? 'setup-stage' : 'main-stage'}"
                 data-stage-id="${stage.id}"
                 data-detail-name="${stage.detailName}"
                 data-stage-name="${stage.stageName}"
                 style="left: ${position.left}px; width: ${position.width}px; ${stageStyles.style}"
                 title="${this.buildStageTooltip(stage)}">
                
                <div class="stage-content">
                    <div class="stage-title">
                        ${stage.isSetup ? '<i class="bi bi-gear-fill me-1"></i>' : ''}
                        ${this.truncateText(stage.stageName, 20)}
                    </div>
                    <div class="stage-detail">
                        ${this.truncateText(stage.detailName, 25)}
                    </div>
                    <div class="stage-time">
                        ${stageStart.format('HH:mm')} - ${stageEnd.format('HH:mm')}
                    </div>
                </div>
                
                <div class="stage-progress" style="width: ${this.calculateStageProgress(stage)}%"></div>
                
                <div class="stage-status-indicator ${stageStyles.indicator}"></div>
                
                ${this.buildStageActionsHTML(stage)}
            </div>
        `;
    }

    calculateStageEndTime(stage) {
        if (stage.endTime) {
            return moment(stage.endTime);
        }

        if (stage.startTime) {
            const duration = stage.plannedDuration || 3600000; // 1 час по умолчанию
            return moment(stage.startTime).add(duration, 'milliseconds');
        }

        return moment().add(1, 'hour');
    }

    calculateStagePosition(stageStart, stageEnd) {
        const viewStart = this.viewStart;
        const displayStart = stageStart.isBefore(viewStart) ? viewStart : stageStart;
        const displayEnd = stageEnd.isAfter(this.viewEnd) ? this.viewEnd : stageEnd;

        const left = displayStart.diff(viewStart, 'minutes') * (this.hourWidth / 60);
        const width = Math.max(displayEnd.diff(displayStart, 'minutes') * (this.hourWidth / 60), 80);

        return { left, width };
    }

    getStageStyles(stage) {
        const styleMap = {
            'pending': {
                style: 'background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%); color: #495057; border: 2px solid #ced4da;',
                indicator: 'bg-secondary'
            },
            'waiting': {
                style: 'background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%); color: #856404; border: 2px solid #ffc107;',
                indicator: 'bg-warning'
            },
            'inprogress': {
                style: 'background: linear-gradient(135deg, #d1ecf1 0%, #b8e6f0 100%); color: #0c5460; border: 2px solid #17a2b8; box-shadow: 0 4px 8px rgba(23,162,184,0.3);',
                indicator: 'bg-info'
            },
            'paused': {
                style: 'background: linear-gradient(135deg, #d6d8db 0%, #c6c8ca 100%); color: #383d41; border: 2px solid #6c757d;',
                indicator: 'bg-secondary'
            },
            'completed': {
                style: 'background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%); color: #155724; border: 2px solid #28a745;',
                indicator: 'bg-success'
            },
            'error': {
                style: 'background: linear-gradient(135deg, #f8d7da 0%, #f5c6cb 100%); color: #721c24; border: 2px solid #dc3545;',
                indicator: 'bg-danger'
            }
        };

        return styleMap[stage.status.toLowerCase()] || styleMap['pending'];
    }

    calculateStageProgress(stage) {
        if (stage.status === 'Completed') return 100;
        if (!stage.startTime || stage.status !== 'InProgress') return 0;

        const elapsed = moment().diff(moment(stage.startTime), 'milliseconds');
        const planned = stage.plannedDuration || 3600000;

        return Math.min(Math.round((elapsed / planned) * 100), 100);
    }

    buildStageTooltip(stage) {
        const parts = [
            `Деталь: ${stage.detailName}`,
            `Этап: ${stage.stageName}`,
            `Статус: ${this.translateStatus(stage.status)}`,
            `Тип: ${stage.isSetup ? 'Переналадка' : 'Основная операция'}`
        ];

        if (stage.startTime) {
            parts.push(`Начало: ${moment(stage.startTime).format('DD.MM.YYYY HH:mm')}`);
        }

        if (stage.endTime) {
            parts.push(`Окончание: ${moment(stage.endTime).format('DD.MM.YYYY HH:mm')}`);
        }

        return parts.join('\\n');
    }

    buildStageActionsHTML(stage) {
        const actions = [];

        switch (stage.status) {
            case 'Pending':
                actions.push('<i class="bi bi-play-fill stage-action" title="Запустить" onclick="startStageFromGantt(event, ' + stage.id + ')"></i>');
                break;
            case 'InProgress':
                actions.push('<i class="bi bi-pause-fill stage-action" title="Пауза" onclick="pauseStageFromGantt(event, ' + stage.id + ')"></i>');
                actions.push('<i class="bi bi-check-lg stage-action" title="Завершить" onclick="completeStageFromGantt(event, ' + stage.id + ')"></i>');
                break;
            case 'Paused':
                actions.push('<i class="bi bi-play-fill stage-action" title="Возобновить" onclick="resumeStageFromGantt(event, ' + stage.id + ')"></i>');
                break;
        }

        actions.push('<i class="bi bi-info-circle stage-action" title="Подробнее" onclick="showStageDetailsFromGantt(event, ' + stage.id + ')"></i>');

        return `<div class="stage-actions d-none">${actions.join('')}</div>`;
    }

    buildCurrentTimeLineHTML() {
        const now = moment();
        if (now.isBefore(this.viewStart) || now.isAfter(this.viewEnd)) {
            return '';
        }

        const left = now.diff(this.viewStart, 'minutes') * (this.hourWidth / 60);

        return `
            <div class="gantt-current-time-line" style="left: ${left}px;">
                <div class="time-line"></div>
                <div class="time-marker">
                    <i class="bi bi-clock-fill"></i>
                    <span class="time-text">${now.format('HH:mm')}</span>
                </div>
            </div>
        `;
    }

    // Утилиты и вспомогательные методы

    groupStagesByMachine() {
        const groups = {};

        this.data.forEach(stage => {
            const key = stage.machineId || 'unassigned';
            if (!groups[key]) {
                groups[key] = [];
            }
            groups[key].push(stage);
        });

        // Сортируем этапы в каждой группе
        Object.keys(groups).forEach(key => {
            groups[key].sort((a, b) => {
                const timeA = a.startTime ? moment(a.startTime) : moment();
                const timeB = b.startTime ? moment(b.startTime) : moment();
                return timeA.diff(timeB);
            });
        });

        return groups;
    }

    calculateTimelineWidth() {
        return this.viewEnd.diff(this.viewStart, 'hours') * this.hourWidth;
    }

    truncateText(text, maxLength) {
        if (!text) return '';
        return text.length > maxLength ? text.substring(0, maxLength) + '...' : text;
    }

    translateStatus(status) {
        const translations = {
            'Pending': 'Ожидает запуска',
            'Waiting': 'В очереди',
            'InProgress': 'В работе',
            'Paused': 'На паузе',
            'Completed': 'Завершено',
            'Error': 'Ошибка'
        };
        return translations[status] || status;
    }

    // Методы управления масштабом и навигацией

    zoomIn() {
        const currentRange = this.viewEnd.diff(this.viewStart, 'hours');
        if (currentRange > 2 && this.hourWidth < 200) {
            this.hourWidth = Math.min(200, this.hourWidth * 1.3);
            this.render();
        }
    }

    zoomOut() {
        if (this.hourWidth > 60) {
            this.hourWidth = Math.max(60, this.hourWidth * 0.7);
            this.render();
        } else {
            const currentRange = this.viewEnd.diff(this.viewStart, 'hours');
            if (currentRange < 24) {
                const center = moment(this.viewStart).add(currentRange / 2, 'hours');
                const newRange = Math.min(24, currentRange * 1.5);
                this.viewStart = moment(center).subtract(newRange / 2, 'hours');
                this.viewEnd = moment(center).add(newRange / 2, 'hours');
                this.render();
            }
        }
    }

    resetZoom() {
        this.hourWidth = 120;
        this.viewStart = moment().subtract(2, 'hours');
        this.viewEnd = moment().add(12, 'hours');
        this.render();
    }

    moveToPrevious() {
        const shift = this.viewEnd.diff(this.viewStart, 'hours') * 0.7;
        this.viewStart.subtract(shift, 'hours');
        this.viewEnd.subtract(shift, 'hours');
        this.render();
    }

    moveToNext() {
        const shift = this.viewEnd.diff(this.viewStart, 'hours') * 0.7;
        this.viewStart.add(shift, 'hours');
        this.viewEnd.add(shift, 'hours');
        this.render();
    }

    moveToNow() {
        const range = this.viewEnd.diff(this.viewStart, 'hours');
        this.viewStart = moment().subtract(range * 0.2, 'hours');
        this.viewEnd = moment().add(range * 0.8, 'hours');
        this.render();
    }

    scrollToCurrentTime() {
        setTimeout(() => {
            const currentTimeLine = this.container.querySelector('.gantt-current-time-line');
            if (currentTimeLine) {
                currentTimeLine.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
            }
        }, 100);
    }

    // Обработчики событий

    setupEventListeners() {
        // Обработчик клика по этапу будет добавлен в attachEventHandlers
    }

    attachEventHandlers() {
        // Обработчики для этапов
        const stages = this.container.querySelectorAll('.gantt-stage');
        stages.forEach(stage => {
            stage.addEventListener('mouseenter', this.onStageMouseEnter.bind(this));
            stage.addEventListener('mouseleave', this.onStageMouseLeave.bind(this));
            stage.addEventListener('click', this.onStageClick.bind(this));
        });

        // Горизонтальная прокрутка
        const ganttBody = this.container.querySelector('.gantt-body');
        if (ganttBody) {
            ganttBody.addEventListener('wheel', this.onWheel.bind(this), { passive: false });
        }
    }

    onStageMouseEnter(event) {
        const stage = event.currentTarget;
        stage.style.transform = 'translateY(-2px)';
        stage.style.zIndex = '10';

        const actions = stage.querySelector('.stage-actions');
        if (actions) {
            actions.classList.remove('d-none');
        }
    }

    onStageMouseLeave(event) {
        const stage = event.currentTarget;
        stage.style.transform = 'translateY(0)';
        stage.style.zIndex = '1';

        const actions = stage.querySelector('.stage-actions');
        if (actions) {
            actions.classList.add('d-none');
        }
    }

    onStageClick(event) {
        const stageId = event.currentTarget.dataset.stageId;
        if (stageId && typeof showStageDetailsFromGantt === 'function') {
            showStageDetailsFromGantt(event, parseInt(stageId));
        }
    }

    onWheel(event) {
        if (event.ctrlKey) {
            // Зум с помощью Ctrl + колесико
            event.preventDefault();
            if (event.deltaY < 0) {
                this.zoomIn();
            } else {
                this.zoomOut();
            }
        } else if (event.shiftKey) {
            // Горизонтальная прокрутка с помощью Shift + колесико
            event.preventDefault();
            const shift = this.viewEnd.diff(this.viewStart, 'hours') * 0.1;
            if (event.deltaY < 0) {
                this.viewStart.subtract(shift, 'hours');
                this.viewEnd.subtract(shift, 'hours');
            } else {
                this.viewStart.add(shift, 'hours');
                this.viewEnd.add(shift, 'hours');
            }
            this.render();
        }
    }
}

// Глобальные функции для управления этапами из диаграммы Ганта

function startStageFromGantt(event, stageId) {
    event.stopPropagation();
    if (typeof executeStageAction === 'function') {
        executeStageAction('start', stageId);
    }
}

function pauseStageFromGantt(event, stageId) {
    event.stopPropagation();
    if (typeof executeStageAction === 'function') {
        executeStageAction('pause', stageId);
    }
}

function resumeStageFromGantt(event, stageId) {
    event.stopPropagation();
    if (typeof executeStageAction === 'function') {
        executeStageAction('resume', stageId);
    }
}

function completeStageFromGantt(event, stageId) {
    event.stopPropagation();
    if (typeof executeStageAction === 'function') {
        executeStageAction('complete', stageId);
    }
}

function showStageDetailsFromGantt(event, stageId) {
    event.stopPropagation();
    if (typeof showStageDetails === 'function') {
        showStageDetails(stageId);
    }
}

function refreshGanttData() {
    if (typeof refreshData === 'function') {
        refreshData();
    }
}