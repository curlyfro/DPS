// Chart.js initialization and management
window.charts = {};

window.initializeChart = (canvasId, type, labels, datasetLabel, data, backgroundColor) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    // Destroy existing chart if it exists
    if (window.charts[canvasId]) {
        window.charts[canvasId].destroy();
    }

    const config = {
        type: type,
        data: {
            labels: labels,
            datasets: [{
                label: datasetLabel,
                data: data,
                backgroundColor: type === 'doughnut' ? backgroundColor : backgroundColor,
                borderColor: type === 'line' ? backgroundColor : undefined,
                borderWidth: type === 'line' ? 2 : 1,
                fill: type === 'line' ? false : undefined,
                tension: type === 'line' ? 0.1 : undefined
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: type === 'doughnut',
                    position: 'bottom'
                },
                title: {
                    display: false
                }
            },
            scales: type === 'line' ? {
                y: {
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1
                    }
                }
            } : undefined
        }
    };

    window.charts[canvasId] = new Chart(ctx, config);
};

window.updateChart = (canvasId, labels, data) => {
    if (window.charts[canvasId]) {
        window.charts[canvasId].data.labels = labels;
        window.charts[canvasId].data.datasets[0].data = data;
        window.charts[canvasId].update();
    }
};

window.destroyChart = (canvasId) => {
    if (window.charts[canvasId]) {
        window.charts[canvasId].destroy();
        delete window.charts[canvasId];
    }
};