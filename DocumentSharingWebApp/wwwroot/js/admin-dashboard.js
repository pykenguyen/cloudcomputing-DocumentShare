document.addEventListener('DOMContentLoaded', () => {
    const dataEl = document.getElementById('uploadsChartData');
    const canvas = document.getElementById('uploadsChart');
    if (!dataEl || !canvas || !window.Chart) return;

    let payload = { labels: [], values: [] };
    try { payload = JSON.parse(dataEl.textContent || '{}'); } catch (e) { /* ignore */ }

    new Chart(canvas, {
        type: 'line',
        data: {
            labels: payload.labels || [],
            datasets: [{ label: 'Uploads', data: payload.values || [], tension: 0.3, fill: false }]
        },
        options: { responsive: true, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, precision: 0 } } }
    });
});
